using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace QuestMidiBridge
{
    /// <summary>
    /// Sends MIDI to the desktop bridge over a localhost TCP socket.
    ///
    /// On the headset this works because the desktop runs:  adb reverse tcp:PORT tcp:PORT
    /// which tunnels the Quest's 127.0.0.1:PORT to the PC over the USB-C cable.
    /// In the Editor (Play mode) it just connects to the PC's own localhost, so it
    /// works without adb -- handy for testing before you deploy to the headset.
    ///
    /// Wire format: each MIDI message is sent as [length:1 byte][midi bytes...].
    /// The bridge re-frames the TCP stream and forwards each message to loopMIDI.
    ///
    /// All Send* methods are safe to call from the main thread; a background thread
    /// owns the socket and drains a lock-free queue.
    /// </summary>
    [AddComponentMenu("Quest MIDI Bridge/Quest MIDI Sender")]
    public class QuestMidiSender : MonoBehaviour
    {
        [Header("Connection (must match the bridge)")]
        [Tooltip("Always 127.0.0.1 -- the adb reverse tunnel makes the PC reachable on the Quest's localhost.")]
        public string host = "127.0.0.1";

        [Tooltip("Must match the TCP port in the Setup Wizard / bridge-config.json.")]
        public int port = 8765;

        [Header("Defaults")]
        [Range(1, 16)]
        [Tooltip("MIDI channel (1-16) used when a Send* call doesn't specify one.")]
        public int defaultChannel = 1;

        [Tooltip("Connect automatically when this component is enabled.")]
        public bool autoStart = true;

        [Tooltip("Log connect/disconnect and (optionally) every message to the Console.")]
        public bool logVerbose = false;

        // ---- public status -------------------------------------------------
        public bool IsConnected => _connected != 0;

        /// <summary>Raised on the main thread when the connection state flips.</summary>
        public event Action<bool> ConnectionChanged;

        // ---- inbound (return circuit: DAW -> loopMIDI -> bridge -> Quest) ----
        // All inbound events fire on the MAIN thread (drained in Update), so it's
        // safe to touch GameObjects/materials directly from a handler.
        /// <summary>Every received MIDI message, raw bytes (status, data1, data2…).</summary>
        public event Action<byte[]> MidiReceived;
        /// <summary>Control Change: (cc 0-127, value 0-127, channel 1-16).</summary>
        public event Action<int, int, int> ControlChangeReceived;
        /// <summary>Note On with velocity &gt; 0: (note, velocity, channel 1-16).</summary>
        public event Action<int, int, int> NoteOnReceived;
        /// <summary>Note Off (or Note On vel 0): (note, channel 1-16).</summary>
        public event Action<int, int> NoteOffReceived;
        /// <summary>Pitch bend: (bend -1..+1, channel 1-16).</summary>
        public event Action<float, int> PitchBendReceived;

        readonly ConcurrentQueue<byte[]> _inbound = new ConcurrentQueue<byte[]>();

        // ---- internals -----------------------------------------------------
        const int MaxQueuedWhileDisconnected = 1024;

        int _connected;                 // 0/1, written from the network thread
        int _lastNotified = -1;
        volatile bool _run;
        Thread _thread;
        readonly ConcurrentQueue<byte[]> _queue = new ConcurrentQueue<byte[]>();
        readonly AutoResetEvent _signal = new AutoResetEvent(false);

        void OnEnable()
        {
            if (autoStart) StartBridge();
        }

        void OnDisable() => StopBridge();
        void OnApplicationQuit() => StopBridge();

        void Update()
        {
            // Surface connection changes on the main thread so listeners (and the
            // custom inspector) stay on Unity's thread.
            int c = _connected;
            if (c != _lastNotified)
            {
                _lastNotified = c;
                try { ConnectionChanged?.Invoke(c != 0); } catch (Exception e) { Debug.LogException(e); }
            }

            // Drain inbound (return-circuit) MIDI and raise events on this thread.
            while (_inbound.TryDequeue(out var msg))
            {
                try { MidiReceived?.Invoke(msg); } catch (Exception e) { Debug.LogException(e); }
                Dispatch(msg);
            }
        }

        /// <summary>Parse a received MIDI message and raise the typed event.</summary>
        void Dispatch(byte[] m)
        {
            if (m == null || m.Length < 2) return;
            int type = m[0] & 0xF0;
            int ch = (m[0] & 0x0F) + 1;
            try
            {
                if (type == 0xB0 && m.Length >= 3) ControlChangeReceived?.Invoke(m[1], m[2], ch);
                else if (type == 0x90 && m.Length >= 3)
                {
                    if (m[2] == 0) NoteOffReceived?.Invoke(m[1], ch);
                    else NoteOnReceived?.Invoke(m[1], m[2], ch);
                }
                else if (type == 0x80 && m.Length >= 3) NoteOffReceived?.Invoke(m[1], ch);
                else if (type == 0xE0 && m.Length >= 3)
                {
                    int v = (m[2] << 7) | m[1];
                    PitchBendReceived?.Invoke((v - 8192) / 8192f, ch);
                }
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Start the background connection loop (called automatically if autoStart).</summary>
        public void StartBridge()
        {
            if (_run) return;
            _run = true;
            _thread = new Thread(NetworkLoop) { IsBackground = true, Name = "QuestMidiSender" };
            _thread.Start();
        }

        /// <summary>Stop the background loop. Non-blocking.</summary>
        public void StopBridge()
        {
            _run = false;
            _signal.Set();
            _thread = null; // background thread exits on its own
        }

        // ---- high-level helpers (1-16 channel; 0 = use defaultChannel) ------

        public void SendNoteOn(int note, int velocity, int channel = 0)
            => SendRaw((byte)(0x90 | Ch(channel)), S7(note), S7(velocity));

        public void SendNoteOff(int note, int velocity = 0, int channel = 0)
            => SendRaw((byte)(0x80 | Ch(channel)), S7(note), S7(velocity));

        /// <summary>7-bit Control Change (value 0-127).</summary>
        public void SendControlChange(int cc, int value, int channel = 0)
            => SendRaw((byte)(0xB0 | Ch(channel)), S7(cc), S7(value));

        /// <summary>Convenience: map a normalized 0..1 value onto a 7-bit CC.</summary>
        public void SendFloat01AsCC(int cc, float value01, int channel = 0)
            => SendControlChange(cc, Mathf.RoundToInt(Mathf.Clamp01(value01) * 127f), channel);

        /// <summary>
        /// 14-bit Control Change for smooth, high-resolution control (0..1 input).
        /// Sends MSB on <paramref name="cc"/> and LSB on cc+32, per the MIDI spec.
        /// Use this for hand-tracking values that feel "steppy" at 7-bit.
        /// </summary>
        public void SendControlChange14(int cc, float value01, int channel = 0)
        {
            int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(value01) * 16383f), 0, 16383);
            byte status = (byte)(0xB0 | Ch(channel));
            SendRaw(status, S7(cc), (byte)((v >> 7) & 0x7F));
            SendRaw(status, S7(cc + 32), (byte)(v & 0x7F));
        }

        /// <summary>Pitch bend from -1..+1 (0 = center).</summary>
        public void SendPitchBend(float bend, int channel = 0)
        {
            int v = Mathf.Clamp(Mathf.RoundToInt((bend * 0.5f + 0.5f) * 16383f), 0, 16383);
            SendRaw((byte)(0xE0 | Ch(channel)), (byte)(v & 0x7F), (byte)((v >> 7) & 0x7F));
        }

        /// <summary>Send arbitrary, already-formed MIDI bytes (e.g. program change, SysEx).</summary>
        public void SendRaw(params byte[] midiBytes)
        {
            if (midiBytes == null || midiBytes.Length == 0 || midiBytes.Length > 255) return;

            // Don't grow without bound if the bridge isn't up yet.
            if (_connected == 0)
            {
                while (_queue.Count > MaxQueuedWhileDisconnected && _queue.TryDequeue(out _)) { }
            }

            var frame = new byte[midiBytes.Length + 1];
            frame[0] = (byte)midiBytes.Length;
            Buffer.BlockCopy(midiBytes, 0, frame, 1, midiBytes.Length);
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
                        client.NoDelay = true; // disable Nagle -> low latency for live use
                        var ar = client.BeginConnect(host, port, null, null);
                        if (!ar.AsyncWaitHandle.WaitOne(2000))
                            throw new TimeoutException("connect timeout");
                        client.EndConnect(ar);

                        using (var stream = client.GetStream())
                        {
                            Interlocked.Exchange(ref _connected, 1);
                            if (logVerbose) Debug.Log($"[QuestMidi] Connected to {host}:{port}");

                            var rx = new byte[4096];
                            var accum = new System.Collections.Generic.List<byte>(256);

                            while (_run && client.Connected)
                            {
                                // Wake immediately on new outbound data; otherwise poll the
                                // inbound (return) stream ~every 10 ms — plenty for visuals.
                                _signal.WaitOne(10);

                                // Outbound: Quest -> bridge -> loopMIDI -> DAW
                                while (_queue.TryDequeue(out var frame))
                                    stream.Write(frame, 0, frame.Length);
                                stream.Flush();

                                // Inbound: DAW -> loopMIDI -> bridge -> here. Re-frame the
                                // [len][bytes…] stream and queue messages for the main thread.
                                while (stream.DataAvailable)
                                {
                                    int n = stream.Read(rx, 0, rx.Length);
                                    if (n <= 0) { Interlocked.Exchange(ref _connected, 0); break; }
                                    for (int i = 0; i < n; i++) accum.Add(rx[i]);
                                }
                                int off = 0;
                                while (off < accum.Count)
                                {
                                    int len = accum[off];
                                    if (off + 1 + len > accum.Count) break; // wait for the rest
                                    if (len > 0)
                                    {
                                        var msg = new byte[len];
                                        accum.CopyTo(off + 1, msg, 0, len);
                                        _inbound.Enqueue(msg);
                                    }
                                    off += 1 + len;
                                }
                                if (off > 0) accum.RemoveRange(0, off);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    if (_run && logVerbose) Debug.LogWarning($"[QuestMidi] {e.Message} (retrying)");
                }
                finally
                {
                    Interlocked.Exchange(ref _connected, 0);
                }

                if (_run) Thread.Sleep(1000); // backoff before reconnecting
            }
        }

        // ---- helpers -------------------------------------------------------
        int Ch(int channel) => ((channel <= 0 ? defaultChannel : channel) - 1) & 0x0F;
        static byte S7(int v) => (byte)(v & 0x7F);
    }
}
