using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Layout-edit mode for a control surface. A hamburger button toggles it on. While
    /// editing, the controls stop sending MIDI and a grab handle lets the whole surface
    /// be moved, raised, rotated, and scaled; Save writes the placement as the scene
    /// default through <see cref="SurfaceLayoutStore"/>, which is reapplied on Start.
    ///
    /// The hamburger, the grab handle, and the Save/Done buttons are built by the
    /// editor builder and assigned here, so this component only sequences them.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/Surface Edit Mode")]
    public class SurfaceEditMode : MonoBehaviour
    {
        [Tooltip("Transform that gets moved/rotated/scaled. Defaults to this object.")]
        public Transform target;

        [Tooltip("Objects shown only while editing (grab handle, Save, Done). Hidden otherwise.")]
        public GameObject editRig;

        bool _editing;

        public bool IsEditing => _editing;

        void Awake()
        {
            if (target == null) target = transform;
        }

        void Start()
        {
            // Reapply the saved scene default, then leave edit mode closed.
            SurfaceLayoutStore.LoadInto(target);
            SetEditing(false);
        }

        public void ToggleEdit() => SetEditing(!_editing);

        public void SetEditing(bool on)
        {
            _editing = on;
            if (editRig != null) editRig.SetActive(on);
            SetControlsActive(!on);
        }

        public void SaveLayout()
        {
            SurfaceLayoutStore.SaveFrom(target);
            SetEditing(false);
        }

        /// <summary>Freeze or unfreeze the MIDI controls so editing does not blast MIDI
        /// and the surface handle is the only thing grabbed.</summary>
        void SetControlsActive(bool active)
        {
            foreach (var s in GetComponentsInChildren<MidiSlider>(true)) ToggleControl(s, active);
            foreach (var k in GetComponentsInChildren<MidiKnob>(true)) ToggleControl(k, active);
            foreach (var b in GetComponentsInChildren<MidiButton>(true)) ToggleControl(b, active);
        }

        static void ToggleControl(MonoBehaviour midi, bool active)
        {
            midi.enabled = active;
            GameObject go = midi.gameObject;

            var handGrab = go.GetComponent<HandGrabInteractable>();
            if (handGrab != null) handGrab.enabled = active;

            var grab = go.GetComponent<GrabInteractable>();
            if (grab != null) grab.enabled = active;

            var poke = go.GetComponent<PokeInteractable>();
            if (poke != null) poke.enabled = active;
        }
    }
}
