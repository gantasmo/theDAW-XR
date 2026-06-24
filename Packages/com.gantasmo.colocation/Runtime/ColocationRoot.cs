using UnityEngine;

namespace Gantasmo.Colocation
{
    /// <summary>
    /// Marker for the transform that parents all co-located content (the MIDI
    /// control surface, the MIDI Reactor, holograms). Keeping shared content
    /// under one node makes alignment and any future re-origin a single reparent.
    ///
    /// The Meta Colocation building block aligns every headset's world origin to
    /// the same shared spatial anchor, so any content left at this root appears in
    /// the same physical place for every co-located player. Presence proxies and
    /// networked objects use raw world space (already aligned), so they do not
    /// need to live under this root; only the local creative content does.
    /// </summary>
    [AddComponentMenu("GANTASMO/Colocation/Colocation Root")]
    [DisallowMultipleComponent]
    public class ColocationRoot : MonoBehaviour
    {
        /// <summary>The active root in the loaded scene, if one exists.</summary>
        public static ColocationRoot Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
