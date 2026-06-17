using Oculus.Interaction;
using UnityEngine;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// A poke button that drives the surface layout editor instead of sending MIDI.
    /// Place it on the same object as a <see cref="PokeInteractable"/>; a press runs
    /// the chosen <see cref="Action"/> on the owning <see cref="SurfaceEditMode"/>.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/Surface Edit Button")]
    public class SurfaceEditButton : MonoBehaviour
    {
        public enum Action { ToggleEdit, Save, Cancel }

        [Tooltip("Editor this button controls. Auto-found in a parent when left empty.")]
        public SurfaceEditMode editor;

        public Action action = Action.ToggleEdit;

        [Tooltip("Interactable that fires this button. Auto-found on this object when left empty.")]
        [SerializeField] MonoBehaviour _interactableView;

        IInteractableView _view;

        void Awake()
        {
            if (editor == null) editor = GetComponentInParent<SurfaceEditMode>();
            _view = _interactableView as IInteractableView;
            if (_view == null) _view = GetComponent<IInteractableView>();
        }

        void OnEnable()
        {
            if (_view != null) _view.WhenStateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            if (_view != null) _view.WhenStateChanged -= OnStateChanged;
        }

        void OnStateChanged(InteractableStateChangeArgs args)
        {
            if (editor == null || args.NewState != InteractableState.Select) return;
            switch (action)
            {
                case Action.ToggleEdit: editor.ToggleEdit(); break;
                case Action.Save: editor.SaveLayout(); break;
                case Action.Cancel: editor.SetEditing(false); break;
            }
        }
    }
}
