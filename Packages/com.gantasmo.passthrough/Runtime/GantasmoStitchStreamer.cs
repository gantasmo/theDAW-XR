using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gantasmo.Passthrough
{
    /// <summary>
    /// Streams the CLEAN stitched passthrough (<see cref="GantasmoPassthroughStitch.OutputTexture"/>)
    /// out of the headset as H.264 so theDAW's VJ can use it as a live source, WITHOUT
    /// casting the whole headset display (the performer is wearing it and sees the MR
    /// scene + MIDI surface, which delinQuest/scrcpy would mirror instead of the stitch).
    ///
    /// Pipeline:
    ///   stitch OutputTexture
    ///     -> Graphics.Blit into a smaller stream RenderTexture (resolution/fps tunable live)
    ///     -> AsyncGPUReadback (non-blocking) -> RGBA bytes on the main thread
    ///     -> RGBA->NV12 (BT.601) -> Android MediaCodec H.264 encoder (ByteBuffer input)
    ///     -> Annex-B NAL units -> framed over a localhost TCP socket
    ///     -> adb reverse tcp:PORT tcp:PORT tunnels it to the PC (same trick QuestMidiSender uses)
    ///     -> theDAW's `queststitch` backend module fans it out to the VJ over WebSocket,
    ///        in the SAME wire format as questcast, so the VJ's WebCodecs decoder is reused.
    ///
    /// Setup: drop this on the SAME GameObject as (or near) <see cref="GantasmoPassthroughStitch"/>.
    /// On the PC, start theDAW and select the QUEST STITCH source in the VJ; the backend runs the
    /// adb reverse for you. Connection mirrors QuestMidiSender: 127.0.0.1 + adb reverse, so it also
    /// works from the Editor against the PC's own localhost for wiring tests (the encoder itself only
    /// runs on the Android device; in the Editor this component connects + sends meta but encodes
    /// nothing).
    ///
    /// All MediaCodec / JNI calls run on the MAIN thread (inside the readback callback), so no JVM
    /// thread attach is needed. The only background thread owns the socket and is pure C# (no JNI),
    /// exactly like QuestMidiSender.
    ///
    /// Wire format to the PC (per frame): [u32 frameLen LE][u8 type][u8 keyframe][f64 ptsUs LE][payload].
    ///   type 0 = codec config (Annex-B SPS/PPS), 1 = data NALs, 2 = meta (UTF-8 JSON {w,h,fps,codec}).
    ///   frameLen counts the 10-byte header + payload (everything after the u32).
    /// </summary>
    [DefaultExecutionOrder(200)] // after GantasmoPassthroughStitch (100) has blitted this frame
    [AddComponentMenu("GANTASMO Passthrough/Passthrough Stitch Streamer")]
    public class GantasmoStitchStreamer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The stitch whose OutputTexture is streamed. Auto-found on this GameObject / scene when empty.")]
        public GantasmoPassthroughStitch stitch;

        [Header("Stream")]
        [Tooltip("Encoded resolution. Lower = less CPU for the RGBA->NV12 conversion + less bandwidth. The VJ composites/effects this, so it need not be the full stitch resolution.")]
        public Vector2Int streamResolution = new Vector2Int(1280, 720);
        [Range(5, 60)]
        [Tooltip("Encoded frames per second. The readback + encode is throttled to this; raise/lower live to trade smoothness against headset CPU budget.")]
        public int streamFps = 30;
        [Range(500, 20000)]
        [Tooltip("Target H.264 bitrate in kbps.")]
        public int bitrateKbps = 6000;
        [Range(1, 5)]
        [Tooltip("Seconds between forced keyframes. Lower = faster recovery for a late-joining / reconnecting VJ, slightly more bitrate.")]
        public int keyframeIntervalSec = 1;
        [Tooltip("Flip the image vertically. GPU readback is bottom-up while video is top-down, so this is usually ON.")]
        public bool flipVertical = true;
        [Tooltip("Swap the U/V chroma order (NV12 vs NV21). Flip this if colours look wrong (greens/purples swapped) on your device's encoder.")]
        public bool swapUV = false;

        [Header("Connection (must match the backend; adb reverse makes the PC reachable on the Quest's localhost)")]
        [Tooltip("Always 127.0.0.1 on device — adb reverse tunnels the PC's listener onto the Quest's localhost.")]
        public string host = "127.0.0.1";
        [Tooltip("Must match the queststitch backend module's TCP port.")]
        public int port = 8940;
        [Tooltip("Connect automatically when enabled.")]
        public bool autoStart = true;
        [Tooltip("Log lifecycle + per-second stats to the Console.")]
        public bool logVerbose = false;

        public bool IsConnected => _connected != 0;

        // ---- encoder (Android only) ----------------------------------------
        const string Mime = "video/avc";
        const int COLOR_FormatYUV420SemiPlanar = 21; // NV12-style: Y plane then interleaved chroma
        const int CONFIGURE_FLAG_ENCODE = 1;
        const int BUFFER_FLAG_KEY_FRAME = 1;
        const int BUFFER_FLAG_CODEC_CONFIG = 2;
        const int INFO_TRY_AGAIN_LATER = -1;
        const int INFO_OUTPUT_FORMAT_CHANGED = -2;
        const int INFO_OUTPUT_BUFFERS_CHANGED = -3;

        // Packet types on the wire.
        const byte TYPE_CONFIG = 0;
        const byte TYPE_DATA = 1;
        const byte TYPE_META = 2;

        AndroidJavaObject _codec;
        AndroidJavaObject _bufferInfo;
        bool _encoderReady;
        bool _encoderFailed;
        int _encW, _encH;
        long _frameIndex;
        // MediaCodec emits the SPS/PPS codec-config buffer only ONCE at encoder start. The
        // browser decoder cannot configure without it, so we cache the built config frame and
        // re-send it before every keyframe — that way any VJ that joins or reconnects mid-
        // stream gets a config within one keyframe interval instead of being stuck forever.
        byte[] _configFrame;

        // Cached java.nio.ByteBuffer.get([B) method id. ONLY the output copy-back uses a
        // low-level call (high-level Call<byte[]> does not copy array out-params back). All
        // other buffer ops use high-level AndroidJavaObject calls, whose reflection-based
        // method lookup tolerates the Buffer-vs-ByteBuffer covariant-return variance across
        // Android core-lib versions that a manual GetMethodID signature would not.
        IntPtr _bbClass;
        IntPtr _mGet;

        // ---- readback / conversion -----------------------------------------
        RenderTexture _streamRT;
        bool _readbackPending;
        float _lastFrameTime;
        byte[] _nv12;            // reused NV12 scratch (managed, for the MediaCodec put)
        NativeArray<byte> _rgbaNA; // readback reads RGBA directly here (persistent, job-safe)
        NativeArray<byte> _nv12NA; // reused NV12 target the Burst job writes into
        bool _sentMeta;

        // ---- network thread (pure C#, mirrors QuestMidiSender) -------------
        const int MaxQueuedWhileDisconnected = 90; // ~3s at 30fps; drop oldest beyond this
        int _connected;
        volatile bool _run;
        Thread _thread;
        readonly ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();
        readonly AutoResetEvent _signal = new AutoResetEvent(false);

        // stats
        int _statFrames, _statBytes, _statKeys;
        float _statTime;

        bool OnAndroid => Application.platform == RuntimePlatform.Android;

        void OnEnable()
        {
            if (autoStart) StartStreaming();
        }

        void OnDisable() => StopStreaming();
        void OnApplicationQuit() => StopStreaming();

        public void StartStreaming()
        {
            if (_run) return;
            if (stitch == null) stitch = GetComponent<GantasmoPassthroughStitch>();
            if (stitch == null) stitch = FindAnyObjectByType<GantasmoPassthroughStitch>();
            if (stitch == null)
            {
                Debug.LogError("[GantasmoStitchStreamer] No GantasmoPassthroughStitch found to stream.", this);
                return;
            }

            _streamRT = new RenderTexture(streamResolution.x, streamResolution.y, 0, RenderTextureFormat.ARGB32)
            {
                name = "GantasmoStitchStreamRT",
                useMipMap = false,
                autoGenerateMips = false,
            };
            _streamRT.Create();
            _encW = streamResolution.x;
            _encH = streamResolution.y;
            _nv12 = new byte[_encW * _encH * 3 / 2];
            if (_rgbaNA.IsCreated) _rgbaNA.Dispose();
            _rgbaNA = new NativeArray<byte>(_encW * _encH * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (_nv12NA.IsCreated) _nv12NA.Dispose();
            _nv12NA = new NativeArray<byte>(_nv12.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _sentMeta = false;
            _frameIndex = 0;
            _lastFrameTime = -1f;

            _run = true;
            _thread = new Thread(NetworkLoop) { IsBackground = true, Name = "GantasmoStitchStreamer" };
            _thread.Start();
            if (logVerbose) Debug.Log($"[GantasmoStitchStreamer] started -> {host}:{port} {_encW}x{_encH}@{streamFps} (android={OnAndroid})", this);
        }

        public void StopStreaming()
        {
            _run = false;
            _signal.Set();
            _thread = null;

            // Tear the encoder down on the main thread (where it was created).
            ReleaseEncoder();

            if (_streamRT != null)
            {
                _streamRT.Release();
                Destroy(_streamRT);
                _streamRT = null;
            }
            if (_rgbaNA.IsCreated) _rgbaNA.Dispose();
            if (_nv12NA.IsCreated) _nv12NA.Dispose();
        }

        void LateUpdate()
        {
            if (!_run || _streamRT == null || stitch == null) return;
            // Prefer the composite (passthrough + virtual 3D); fall back to clean passthrough.
            var src = stitch.CompositeTexture != null ? stitch.CompositeTexture : stitch.OutputTexture;
            if (src == null) return;

            // Push a meta packet once we know the source is live so the backend can
            // label the WebSocket metadata for the browser decoder.
            if (!_sentMeta)
            {
                var meta = $"{{\"w\":{_encW},\"h\":{_encH},\"fps\":{streamFps},\"codec\":\"h264\"}}";
                Enqueue(BuildFrame(TYPE_META, false, 0, Encoding.UTF8.GetBytes(meta)));
                _sentMeta = true;
            }

            if (_readbackPending) return;
            float interval = 1f / Mathf.Max(1, streamFps);
            if (_lastFrameTime >= 0f && Time.unscaledTime - _lastFrameTime < interval) return;
            _lastFrameTime = Time.unscaledTime;

            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                // Without async readback we'd have to stall the GPU every frame; not worth it.
                if (logVerbose && _frameIndex == 0) Debug.LogWarning("[GantasmoStitchStreamer] AsyncGPUReadback unsupported; no frames will stream.", this);
                return;
            }

            // When the composite already matches the stream resolution, read it back directly —
            // a 1:1 full-screen blit is pure GPU waste. Only blit when we actually downscale.
            RenderTexture readTex;
            if (src.width == _encW && src.height == _encH)
            {
                readTex = src;
            }
            else
            {
                Graphics.Blit(src, _streamRT);
                readTex = _streamRT;
            }

            _readbackPending = true;
            // Read straight into our persistent, job-safe NativeArray (no temp GetData handle).
            AsyncGPUReadback.RequestIntoNativeArray(ref _rgbaNA, readTex, 0, TextureFormat.RGBA32, OnReadback);
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            _readbackPending = false;
            if (!_run) return;
            if (req.hasError)
            {
                if (logVerbose) Debug.LogWarning("[GantasmoStitchStreamer] readback error", this);
                return;
            }

            // RGBA (already in _rgbaNA from the readback) -> NV12 on Burst worker threads.
            // This conversion on the main thread was the bottleneck that dropped the whole
            // app to ~5fps at 1080p; Burst-parallel it is ~1-2ms. Complete() within the
            // callback so the buffers are free to be reused next frame.
            new Nv12Job
            {
                rgba = _rgbaNA,
                nv12 = _nv12NA,
                w = _encW,
                h = _encH,
                flipV = flipVertical,
                swapUV = swapUV,
            }.Schedule(_encH, 8).Complete();

            if (!OnAndroid)
            {
                // Editor / non-Android: we have the pixels + the socket, but no MediaCodec.
                // Wiring (adb reverse, backend, VJ) can still be exercised; just no video.
                return;
            }
            if (_encoderFailed) return;
            if (!_encoderReady && !SetupEncoder()) { _encoderFailed = true; return; }

            try
            {
                _nv12NA.CopyTo(_nv12); // NativeArray -> managed for the MediaCodec ByteBuffer put
                FeedEncoder(_nv12);
                DrainEncoder();
            }
            catch (Exception e)
            {
                _encoderFailed = true;
                Debug.LogError($"[GantasmoStitchStreamer] encoder error, stopping video: {e}", this);
            }

            // Per-second stats.
            if (logVerbose)
            {
                _statTime += Time.unscaledDeltaTime;
                if (_statTime >= 1f)
                {
                    Debug.Log($"[GantasmoStitchStreamer] {_statFrames}fps {_statBytes / 1024}KB/s keys={_statKeys} q={_queue.Count} conn={IsConnected}", this);
                    _statFrames = _statBytes = _statKeys = 0;
                    _statTime = 0f;
                }
            }
        }

        // ---- RGBA -> NV12 (BT.601 limited range), Burst-parallel over output rows ----
        // One job index = one output row Y. Even rows also write the interleaved chroma
        // for their 2x2 block. Y rows and chroma rows occupy disjoint regions of nv12, so
        // the parallel-for restriction is safely disabled. row 0 = bottom of the readback,
        // so flipV maps it to a top-down output.
        [BurstCompile]
        struct Nv12Job : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> rgba;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<byte> nv12;
            public int w;
            public int h;
            public bool flipV;
            public bool swapUV;

            public void Execute(int y)
            {
                int srcRow = (flipV ? (h - 1 - y) : y) * w * 4;
                int yRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    int i = srcRow + x * 4;
                    int r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
                    nv12[yRow + x] = (byte)(((66 * r + 129 * g + 25 * b + 128) >> 8) + 16);
                }
                if ((y & 1) != 0) return;
                int uvRow = w * h + (y >> 1) * w;
                for (int x = 0; x < w; x += 2)
                {
                    int i = srcRow + x * 4;
                    int r = rgba[i], g = rgba[i + 1], b = rgba[i + 2];
                    int u = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
                    int v = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;
                    if (u < 0) u = 0; else if (u > 255) u = 255;
                    if (v < 0) v = 0; else if (v > 255) v = 255;
                    int o = uvRow + x;
                    nv12[o] = (byte)(swapUV ? v : u);
                    nv12[o + 1] = (byte)(swapUV ? u : v);
                }
            }
        }

        // ---- MediaCodec (Android, main thread) -----------------------------
        bool SetupEncoder()
        {
            try
            {
                using (var fmtClass = new AndroidJavaClass("android.media.MediaFormat"))
                using (var codecClass = new AndroidJavaClass("android.media.MediaCodec"))
                using (var format = fmtClass.CallStatic<AndroidJavaObject>("createVideoFormat", Mime, _encW, _encH))
                {
                    format.Call("setInteger", "color-format", COLOR_FormatYUV420SemiPlanar);
                    format.Call("setInteger", "bitrate", bitrateKbps * 1000);
                    format.Call("setInteger", "frame-rate", streamFps);
                    format.Call("setInteger", "i-frame-interval", Mathf.Max(1, keyframeIntervalSec));

                    _codec = codecClass.CallStatic<AndroidJavaObject>("createEncoderByType", Mime);
                    _codec.Call("configure", format, null, null, CONFIGURE_FLAG_ENCODE);
                    _codec.Call("start");
                }
                _bufferInfo = new AndroidJavaObject("android.media.MediaCodec$BufferInfo");

                // get(byte[]) is declared on ByteBuffer with a stable return type, so a
                // direct method-id lookup is safe (and necessary for the reliable copy-back).
                _bbClass = AndroidJNI.FindClass("java/nio/ByteBuffer");
                _mGet = AndroidJNI.GetMethodID(_bbClass, "get", "([B)Ljava/nio/ByteBuffer;");

                _configFrame = null; // re-cache from this encoder's codec-config buffer
                _encoderReady = true;
                if (logVerbose) Debug.Log($"[GantasmoStitchStreamer] MediaCodec H.264 encoder up {_encW}x{_encH}@{streamFps} {bitrateKbps}kbps", this);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GantasmoStitchStreamer] MediaCodec setup failed: {e}", this);
                ReleaseEncoder();
                return false;
            }
        }

        void FeedEncoder(byte[] nv12)
        {
            int inIdx = _codec.Call<int>("dequeueInputBuffer", 10000L); // 10ms
            if (inIdx < 0) return; // no input buffer free; drop this frame to stay live
            var inBuf = _codec.Call<AndroidJavaObject>("getInputBuffer", inIdx);
            try
            {
                // clear() then put(byte[]). High-level calls: the returned Buffer/ByteBuffer
                // is wrapped + disposed; put's byte[] arg is marshaled (copied) into the JVM.
                using (var _ = inBuf.Call<AndroidJavaObject>("clear")) { }
                using (var _ = inBuf.Call<AndroidJavaObject>("put", nv12)) { }
            }
            finally { inBuf.Dispose(); }

            long ptsUs = _frameIndex * 1000000L / Mathf.Max(1, streamFps);
            _frameIndex++;
            _codec.Call("queueInputBuffer", inIdx, 0, nv12.Length, ptsUs, 0);
        }

        void DrainEncoder()
        {
            while (true)
            {
                int outIdx = _codec.Call<int>("dequeueOutputBuffer", _bufferInfo, 0L);
                if (outIdx == INFO_TRY_AGAIN_LATER) break;
                if (outIdx == INFO_OUTPUT_FORMAT_CHANGED || outIdx == INFO_OUTPUT_BUFFERS_CHANGED) continue;
                if (outIdx < 0) break;

                int size = _bufferInfo.Get<int>("size");
                int offset = _bufferInfo.Get<int>("offset");
                int flags = _bufferInfo.Get<int>("flags");
                long pts = _bufferInfo.Get<long>("presentationTimeUs");

                byte[] payload = null;
                if (size > 0)
                {
                    var outBuf = _codec.Call<AndroidJavaObject>("getOutputBuffer", outIdx);
                    try
                    {
                        // Bound the readable region to the valid data, then copy it out.
                        // position()/limit() via high-level calls (covariant-return tolerant);
                        // get([B) via low-level JNI because high-level Call<byte[]> does not
                        // copy the filled array back to managed memory.
                        using (var _ = outBuf.Call<AndroidJavaObject>("position", offset)) { }
                        using (var _ = outBuf.Call<AndroidJavaObject>("limit", offset + size)) { }

                        IntPtr jArr = AndroidJNI.ToSByteArray(new sbyte[size]);
                        var gargs = new jvalue[1]; gargs[0].l = jArr;
                        IntPtr ret = AndroidJNI.CallObjectMethod(outBuf.GetRawObject(), _mGet, gargs);
                        if (ret != IntPtr.Zero) AndroidJNI.DeleteLocalRef(ret);
                        sbyte[] sbytes = AndroidJNI.FromSByteArray(jArr);
                        AndroidJNI.DeleteLocalRef(jArr);
                        payload = new byte[sbytes.Length];
                        Buffer.BlockCopy(sbytes, 0, payload, 0, sbytes.Length); // sbyte->byte, bit-identical
                    }
                    finally { outBuf.Dispose(); }
                }

                _codec.Call("releaseOutputBuffer", outIdx, false);

                if (payload == null || payload.Length == 0) continue;
                bool isConfig = (flags & BUFFER_FLAG_CODEC_CONFIG) != 0;
                bool isKey = (flags & BUFFER_FLAG_KEY_FRAME) != 0;
                var frame = BuildFrame(isConfig ? TYPE_CONFIG : TYPE_DATA, isKey, pts, payload);

                if (isConfig)
                {
                    _configFrame = frame; // cache SPS/PPS to re-send before every keyframe
                    if (logVerbose) Debug.Log($"[GantasmoStitchStreamer] cached codec config ({payload.Length}B SPS/PPS)", this);
                }
                else if (isKey && _configFrame != null)
                {
                    // Re-send config ahead of each IDR so a (re)joining client can configure.
                    Enqueue(_configFrame);
                }
                Enqueue(frame);

                if (logVerbose)
                {
                    if (!isConfig) _statFrames++;
                    if (isKey) _statKeys++;
                    _statBytes += payload.Length;
                }
            }
        }

        void ReleaseEncoder()
        {
            _encoderReady = false;
            _configFrame = null;
            if (_codec != null)
            {
                try { _codec.Call("stop"); } catch { /* not started */ }
                try { _codec.Call("release"); } catch { /* already gone */ }
                _codec.Dispose();
                _codec = null;
            }
            if (_bufferInfo != null) { _bufferInfo.Dispose(); _bufferInfo = null; }
        }

        // ---- framing -------------------------------------------------------
        byte[] BuildFrame(byte type, bool keyframe, long ptsUs, byte[] payload)
        {
            int bodyLen = 1 + 1 + 8 + payload.Length; // type + keyframe + ptsUs(f64) + payload
            var frame = new byte[4 + bodyLen];
            // u32 frameLen LE (everything after these 4 bytes)
            frame[0] = (byte)(bodyLen & 0xFF);
            frame[1] = (byte)((bodyLen >> 8) & 0xFF);
            frame[2] = (byte)((bodyLen >> 16) & 0xFF);
            frame[3] = (byte)((bodyLen >> 24) & 0xFF);
            frame[4] = type;
            frame[5] = (byte)(keyframe ? 1 : 0);
            // f64 ptsUs LE
            var pts = BitConverter.GetBytes((double)ptsUs);
            if (!BitConverter.IsLittleEndian) Array.Reverse(pts);
            Buffer.BlockCopy(pts, 0, frame, 6, 8);
            Buffer.BlockCopy(payload, 0, frame, 14, payload.Length);
            return frame;
        }

        void Enqueue(byte[] frame)
        {
            if (_connected == 0)
            {
                while (_queue.Count > MaxQueuedWhileDisconnected && _queue.TryDequeue(out _)) { }
            }
            _queue.Enqueue(frame);
            _signal.Set();
        }

        // ---- network thread ------------------------------------------------
        void NetworkLoop()
        {
            while (_run)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        client.NoDelay = true;
                        var ar = client.BeginConnect(host, port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(2000))
                            throw new TimeoutException("connect timeout");
                        client.EndConnect(ar);

                        using (var stream = client.GetStream())
                        {
                            Interlocked.Exchange(ref _connected, 1);
                            if (logVerbose) Debug.Log($"[GantasmoStitchStreamer] connected {host}:{port}");
                            _sentMeta = false; // re-send meta to the (possibly new) backend session

                            while (_run && client.Connected)
                            {
                                _signal.WaitOne(50);
                                while (_queue.TryDequeue(out var frame))
                                    stream.Write(frame, 0, frame.Length);
                                stream.Flush();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_run && logVerbose) Debug.LogWarning($"[GantasmoStitchStreamer] {e.Message} (retrying)");
                }
                finally
                {
                    Interlocked.Exchange(ref _connected, 0);
                }
                if (_run) Thread.Sleep(1000);
            }
        }
    }
}
