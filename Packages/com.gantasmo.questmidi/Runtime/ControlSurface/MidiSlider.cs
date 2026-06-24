using UnityEngine;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Reads a grabbed handle's local position along one axis and publishes it
    /// as a Control Change. Pair it with a <see cref="Oculus.Interaction.OneGrabTranslateTransformer"/>
    /// constrained to the same axis so the handle slides on its rail. The value
    /// is read from the transform every frame, so it tracks regardless of whether
    /// a hand, a controller, or code moved the handle.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/MIDI Slider")]
    public class MidiSlider : MonoBehaviour
    {
        public enum SlideAxis { X, Y, Z }

        [Tooltip("Surface this slider reports to. Auto-found in a parent when left empty.")]
        public MidiControlSurface surface;

        [Range(0, 127)]
        [Tooltip("Control Change number this slider sends.")]
        public int cc = 1;

        [Tooltip("Local axis the handle travels along.")]
        public SlideAxis axis = SlideAxis.Y;

        [Tooltip("Local coordinate (along the axis) that maps to value 0.")]
        public float minLocal = 0f;

        [Tooltip("Local coordinate (along the axis) that maps to value 1.")]
        public float maxLocal = 0.12f;

        int _lastSent = int.MinValue;

        void Awake()
        {
            if (surface == null) surface = GetComponentInParent<MidiControlSurface>();
        }

        /// <summary>Current normalized position of the handle, 0..1.</summary>
        public float Value01
        {
            get
            {
                Vector3 p = transform.localPosition;
                float along = axis == SlideAxis.X ? p.x : axis == SlideAxis.Y ? p.y : p.z;
                return Mathf.InverseLerp(minLocal, maxLocal, along);
            }
        }

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
