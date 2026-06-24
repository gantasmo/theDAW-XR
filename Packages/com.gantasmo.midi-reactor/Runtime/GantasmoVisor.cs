using System;
using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo
{
    /// <summary>
    /// GANTASMO MIDI Reactor.
    ///
    /// MIDI Reactor: a head-mounted, MIDI-reactive chrome shield. It procedurally
    /// builds a curved chrome shield in front of the headset camera and drives its
    /// glow, hue, pulse, and warp from MIDI arriving on the return circuit
    /// (DAW to loopMIDI "QuestMIDI-Return" to bridge to QuestMidiSender to here).
    ///
    /// Drop-in: add this component (or use GANTASMO > MIDI Reactor > Add To Scene);
    /// it finds the QuestMidiSender, mounts itself to the main camera, and builds its
    /// mesh and chrome material at runtime. Nothing else to author.
    /// </summary>
    [AddComponentMenu("GANTASMO/MIDI Reactor")]
    public class GantasmoVisor : MonoBehaviour
    {
        [Header("MIDI source (auto-found if empty)")]
        [Tooltip("The QuestMidiSender whose return-circuit events drive the reactor. Found automatically if left empty.")]
        public QuestMidiSender sender;

        [Tooltip("Only react to this MIDI channel (1-16). 0 = accept all channels.")]
        [Range(0, 16)] public int channel = 0;

        [Header("Mount (auto = main camera)")]
        [Tooltip("Transform the reactor follows. Defaults to Camera.main (the headset eye) when empty.")]
        public Transform mountTarget;
        [Tooltip("Distance in front of the eyes (metres).")] public float distance = 0.42f;
        [Tooltip("Vertical offset; negative drops it toward a helmet brim.")] public float verticalOffset = -0.05f;
        [Tooltip("Horizontal sweep of the shield arc, degrees.")] [Range(40f, 200f)] public float arcDegrees = 150f;
        [Tooltip("Shield height (metres).")] public float visorHeight = 0.14f;

        [Header("Chrome look")]
        public Color chromeColor = new Color(0.18f, 0.19f, 0.22f);
        [ColorUsage(false, true)] public Color glowColor = new Color(0.30f, 0.85f, 1.0f);
        [Range(0f, 1f)] public float smoothness = 0.96f;

        [Header("Reactivity (MIDI CC numbers)")]
        [Tooltip("CC that sets the steady glow level.")] public int ccGlow = 1;
        [Tooltip("CC that shifts the hue.")] public int ccHue = 2;
        [Tooltip("CC that drives the warp/scale pulse (e.g. an energy/beat envelope).")] public int ccWarp = 3;
        [Tooltip("How hard incoming notes flash the reactor.")] [Range(0f, 4f)] public float noteFlash = 1.6f;
        [Tooltip("Flash/decay speed.")] public float decay = 3.5f;

        // ---- runtime state -------------------------------------------------
        Material _mat;
        MeshRenderer _renderer;
        static readonly int _BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int _EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int _Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int _Metallic = Shader.PropertyToID("_Metallic");

        float _glow;     // steady glow 0..1 (ccGlow)
        float _hue;      // 0..1 (ccHue)
        float _warp;     // 0..1 (ccWarp)
        float _pulse;    // transient note flash, decays to 0
        float _baseScale = 1f;

        void OnEnable()
        {
            EnsureMount();
            BuildVisor();
            HookSender(true);
        }

        void OnDisable() => HookSender(false);

        void EnsureMount()
        {
            if (mountTarget == null)
            {
                var cam = Camera.main;
                mountTarget = cam != null ? cam.transform : null;
            }
            if (mountTarget != null && transform.parent != mountTarget)
                transform.SetParent(mountTarget, worldPositionStays: false);
            transform.localPosition = new Vector3(0f, verticalOffset, 0f);
            transform.localRotation = Quaternion.identity;
        }

        void HookSender(bool on)
        {
            if (sender == null) sender = FindAnyObjectByType<QuestMidiSender>();
            if (sender == null) return;
            if (on)
            {
                sender.ControlChangeReceived += OnCC;
                sender.NoteOnReceived += OnNoteOn;
                sender.PitchBendReceived += OnBend;
            }
            else
            {
                sender.ControlChangeReceived -= OnCC;
                sender.NoteOnReceived -= OnNoteOn;
                sender.PitchBendReceived -= OnBend;
            }
        }

        bool ChannelOk(int ch) => channel == 0 || ch == channel;

        void OnCC(int cc, int value, int ch)
        {
            if (!ChannelOk(ch)) return;
            float v = value / 127f;
            if (cc == ccGlow) _glow = v;
            else if (cc == ccHue) _hue = v;
            else if (cc == ccWarp) _warp = v;
        }

        void OnNoteOn(int note, int velocity, int ch)
        {
            if (!ChannelOk(ch)) return;
            _pulse = Mathf.Clamp01(_pulse + (velocity / 127f) * noteFlash);
        }

        void OnBend(float bend, int ch)
        {
            if (!ChannelOk(ch)) return;
            // Pitch bend nudges the warp directly for expressive hand control.
            _warp = Mathf.Clamp01(Mathf.Abs(bend));
        }

        void Update()
        {
            if (_mat == null) return;
            _pulse = Mathf.Max(0f, _pulse - decay * Time.deltaTime);

            // Hue-rotate the glow colour, then scale it by steady glow + the
            // transient note pulse for the emission.
            Color shifted = ShiftHue(glowColor, _hue);
            float intensity = 0.15f + _glow * 1.6f + _pulse * 2.2f;
            _mat.SetColor(_EmissionColor, shifted * intensity);
            _mat.SetColor(_BaseColor, Color.Lerp(chromeColor, shifted * 0.4f, _glow * 0.5f));

            // Warp: a subtle breathing scale + tilt so the reactor feels alive.
            float s = _baseScale * (1f + (_warp * 0.06f) + (_pulse * 0.05f));
            transform.localScale = new Vector3(s, s, s);
            transform.localRotation = Quaternion.Euler(_warp * 4f * Mathf.Sin(Time.time * 6f), 0f, 0f);
            transform.localPosition = new Vector3(0f, verticalOffset, 0f);
        }

        // ---- chrome colour helpers ----------------------------------------
        static Color ShiftHue(Color c, float shift01)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + shift01, 1f);
            Color outc = Color.HSVToRGB(h, s, v, hdr: true);
            outc.a = c.a;
            return outc;
        }

        // ---- mesh + material build ----------------------------------------
        void BuildVisor()
        {
            var mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildArcMesh();
            _mat = new Material(FindChromeShader()) { name = "GANTASMO MIDI Reactor" };
            _mat.SetFloat(_Metallic, 1f);
            _mat.SetFloat(_Smoothness, smoothness);
            _mat.SetColor(_BaseColor, chromeColor);
            _mat.EnableKeyword("_EMISSION");
            _mat.SetColor(_EmissionColor, glowColor * 0.2f);
            _mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _renderer.sharedMaterial = _mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        static Shader FindChromeShader()
        {
            // URP first (this project is URP 17.x); fall back to built-in Standard.
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
        }

        /// <summary>A curved shield: a 2-row strip on a cylinder arc in front of
        /// the eyes, normals facing inward so the wearer sees the chrome.</summary>
        Mesh BuildArcMesh()
        {
            const int segs = 64;
            float arc = arcDegrees * Mathf.Deg2Rad;
            float r = distance;
            float halfH = visorHeight * 0.5f;
            int rows = 2;

            var verts = new Vector3[(segs + 1) * rows];
            var norms = new Vector3[(segs + 1) * rows];
            var uvs = new Vector2[(segs + 1) * rows];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float a = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t);
                float sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                // slight downward bow toward the edges for a helmet-brim feel
                float bow = Mathf.Cos(a) * 0.02f;
                for (int row = 0; row < rows; row++)
                {
                    float y = (row == 0 ? halfH : -halfH) - bow;
                    int idx = i * rows + row;
                    verts[idx] = new Vector3(sin * r, y, cos * r);
                    norms[idx] = new Vector3(-sin, 0f, -cos); // inward, toward the eyes
                    uvs[idx] = new Vector2(t, row);
                }
            }

            var tris = new int[segs * 6];
            int ti = 0;
            for (int i = 0; i < segs; i++)
            {
                int a0 = i * rows, b0 = a0 + 1, a1 = (i + 1) * rows, b1 = a1 + 1;
                // wind so the inward-facing side is front-facing
                tris[ti++] = a0; tris[ti++] = a1; tris[ti++] = b0;
                tris[ti++] = b0; tris[ti++] = a1; tris[ti++] = b1;
            }

            var mesh = new Mesh { name = "GANTASMO MIDI Reactor Arc" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
