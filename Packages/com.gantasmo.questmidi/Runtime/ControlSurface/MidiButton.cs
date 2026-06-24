using UnityEngine;
using Oculus.Interaction;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Turns an interactable's Select state into MIDI. Drive it from a
    /// <see cref="PokeInteractable"/> (push with a fingertip) or any other
    /// <see cref="IInteractableView"/>. In Note mode it sends Note On while
    /// pressed and Note Off on release; in ToggleCC mode each press flips a
    /// latched CC between 0 and 127.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/MIDI Button")]
    public class MidiButton : MonoBehaviour
    {
        public enum Mode { Note, ToggleCC }

        [Tooltip("Surface this button reports to. Auto-found in a parent when left empty.")]
        public MidiControlSurface surface;

        [Tooltip("The interactable whose Select state fires this button. " +
                 "Auto-found on this object when left empty.")]
        [SerializeField] MonoBehaviour _interactableView;

        public Mode mode = Mode.Note;

        [Range(0, 127)]
        [Tooltip("Note number sent in Note mode.")]
        public int note = 36;

        [Range(1, 127)]
        [Tooltip("Velocity sent in Note mode.")]
        public int velocity = 110;

        [Range(0, 127)]
        [Tooltip("CC number latched in ToggleCC mode.")]
        public int toggleCC = 64;

        IInteractableView _view;
        bool _toggleState;

        void Awake()
        {
            if (surface == null) surface = GetComponentInParent<MidiControlSurface>();
            _view = _interactableView as IInteractableView;
            if (_view == null) _view = GetComponent<IInteractableView>();
        }

        void OnEnable()
        {
            if (_view != null) _view.WhenStateChanged += HandleStateChanged;
        }

        void OnDisable()
        {
            if (_view != null) _view.WhenStateChanged -= HandleStateChanged;
        }

        void HandleStateChanged(InteractableStateChangeArgs args)
        {
            if (surface == null) return;

            if (args.NewState == InteractableState.Select)
            {
                if (mode == Mode.Note)
                {
                    surface.SendNoteOn(note, velocity);
                }
                else
                {
                    _toggleState = !_toggleState;
                    surface.SendCCValue(toggleCC, _toggleState ? 127 : 0);
                }
            }
            else if (args.PreviousState == InteractableState.Select && mode == Mode.Note)
            {
                surface.SendNoteOff(note);
            }
        }
    }
}
