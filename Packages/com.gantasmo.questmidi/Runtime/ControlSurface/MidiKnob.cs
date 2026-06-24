using UnityEngine;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Reads a grabbed knob's rotation about one local axis and publishes it as a
    /// Control Change. Pair it with a <see cref="Oculus.Interaction.OneGrabRotateTransformer"/>
    /// constrained to the same axis and angle range. The rest pose (captured at
    /// Awake) reads as the centre of the range, so a symmetric range starts at 0.5.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/MIDI Knob")]
    public class MidiKnob : MonoBehaviour
    {
        public enum KnobAxis { X, Y, Z }

        [Tooltip("Surface this knob reports to. Auto-found in a parent when left empty.")]
        public MidiControlSurface surface;

        [Range(0, 127)]
        [Tooltip("Control Change number this knob sends.")]
        public int cc = 40;

        [Tooltip("Local axis the knob twists about.")]
        public KnobAxis axis = KnobAxis.Z;

        [Tooltip("Angle in degrees (relative to the rest pose) that maps to value 0.")]
        public float minAngle = -135f;

        [Tooltip("Angle in degrees (relative to the rest pose) that maps to value 1.")]
        public float maxAngle = 135f;

        Quaternion _restLocal;
        int _lastSent = int.MinValue;

        void Awake()
        {
            if (surface == null) surface = GetComponentInParent<MidiControlSurface>();
            _restLocal = transform.localRotation;
        }

        Vector3 LocalAxis =>
            axis == KnobAxis.X ? Vector3.right : axis == KnobAxis.Y ? Vector3.up : Vector3.forward;

        /// <summary>Signed angle in degrees from the rest pose about the configured axis.</summary>
        public float CurrentAngle
        {
            get
            {
                Quaternion delta = Quaternion.Inverse(_restLocal) * transform.localRotation;
                delta.ToAngleAxis(out float ang, out Vector3 ax);
                if (float.IsNaN(ang) || float.IsInfinity(ang)) return 0f;
                if (Vector3.Dot(ax, LocalAxis) < 0f) ang = -ang;
                if (ang > 180f) ang -= 360f;
                return ang;
            }
        }

        /// <summary>Current normalized angle of the knob, 0..1.</summary>
        public float Value01 => Mathf.InverseLerp(minAngle, maxAngle, CurrentAngle);

        void Update()
        {
            if (surface == null) return;
            float v = Mathf.Clamp01(Value01);
            int q = Mathf.RoundToInt(v * (surface.Steps - 1));
            if (q == _lastSent) return;
            _lastSent = q;
            surface.SendCC(cc, v);
        }
    }
}
