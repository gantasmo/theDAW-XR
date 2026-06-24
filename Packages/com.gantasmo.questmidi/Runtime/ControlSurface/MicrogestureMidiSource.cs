using System.Collections;
using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Turns Meta Quest microgestures (the thumb swipes and tap the hand-tracking
    /// runtime detects on the index finger) into MIDI, so they can be mapped in
    /// theDAW's MIDI learn exactly like a knob, pad, or the XR MIDI surface. Each of
    /// the five microgesture types fires a momentary event on the shared surface
    /// channel; theDAW decides what each one drives.
    ///
    /// A microgesture is a discrete, fire-and-forget event, so a momentary MIDI Note
    /// (Note On, then a short Note Off) is the natural representation. Turn on
    /// <see cref="emitAsControlChange"/> to send a 127 -&gt; 0 CC pulse instead when a
    /// target expects a CC trigger.
    ///
    /// Wiring: drop this beside (or under) a <see cref="MidiControlSurface"/> and point
    /// it at an <c>OVRMicrogestureEventSource</c>, or just at an <c>OVRHand</c> and let
    /// it build the event source for you. One component drives one hand; add a second
    /// component for the other hand so left and right can map to different notes.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/Microgesture MIDI Source")]
    public class MicrogestureMidiSource : MonoBehaviour
    {
        [Tooltip("Routes MIDI to theDAW. Auto-found on this object or a parent when left empty.")]
        public MidiControlSurface surface;

        [Header("Hand")]
        [Tooltip("Existing microgesture event source. When empty, one is created at runtime from Hand.")]
        public OVRMicrogestureEventSource eventSource;
        [Tooltip("The tracked hand to read microgestures from. Used to build an event source when none is assigned.")]
        public OVRHand hand;

        [System.Serializable]
        public struct GestureMap
        {
            public OVRHand.MicrogestureType gesture;
            [Tooltip("MIDI note fired for this gesture (or CC number when 'Emit As Control Change' is on).")]
            public int number;
            [Range(1, 127)] public int velocity;
            public bool enabled;
        }

        [Header("Mapping")]
        [Tooltip("One entry per microgesture. Each fires a momentary Note (or CC pulse) theDAW can learn.")]
        public GestureMap[] map = DefaultMap();

        [Tooltip("Send a 127 -> 0 CC pulse on the mapped number instead of a Note On/Off pair.")]
        public bool emitAsControlChange = false;

        [Min(0.01f)]
        [Tooltip("Seconds the momentary Note (or CC = 127) is held before it releases.")]
        public float pulseSeconds = 0.08f;

        void Reset()
        {
            surface = GetComponentInParent<MidiControlSurface>();
            hand = GetComponent<OVRHand>();
            map = DefaultMap();
        }

        void Awake()
        {
            if (surface == null) surface = GetComponentInParent<MidiControlSurface>();

            if (eventSource == null)
            {
                eventSource = GetComponent<OVRMicrogestureEventSource>();
                if (eventSource == null && hand != null)
                {
                    eventSource = gameObject.AddComponent<OVRMicrogestureEventSource>();
                    eventSource.Hand = hand;
                }
            }
            else if (eventSource.Hand == null && hand != null)
            {
                eventSource.Hand = hand;
            }
        }

        void OnEnable()
        {
            if (eventSource != null) eventSource.WhenGestureRecognized += OnGesture;
        }

        void OnDisable()
        {
            if (eventSource != null) eventSource.WhenGestureRecognized -= OnGesture;
        }

        void OnGesture(OVRHand.MicrogestureType gesture)
        {
            if (surface == null || !isActiveAndEnabled) return;
            for (int i = 0; i < map.Length; i++)
            {
                if (!map[i].enabled || map[i].gesture != gesture) continue;
                StartCoroutine(Pulse(map[i].number, map[i].velocity));
                return;
            }
        }

        IEnumerator Pulse(int number, int velocity)
        {
            if (emitAsControlChange)
            {
                surface.SendCCValue(number, 127);
                yield return new WaitForSeconds(pulseSeconds);
                surface.SendCCValue(number, 0);
            }
            else
            {
                surface.SendNoteOn(number, velocity);
                yield return new WaitForSeconds(pulseSeconds);
                surface.SendNoteOff(number);
            }
        }

        // Five microgestures mapped to consecutive notes (C3..E3). Arbitrary on
        // purpose: they only need to be distinct and learnable in theDAW.
        static GestureMap[] DefaultMap()
        {
            return new[]
            {
                new GestureMap { gesture = OVRHand.MicrogestureType.SwipeLeft,     number = 48, velocity = 110, enabled = true },
                new GestureMap { gesture = OVRHand.MicrogestureType.SwipeRight,    number = 49, velocity = 110, enabled = true },
                new GestureMap { gesture = OVRHand.MicrogestureType.SwipeForward,  number = 50, velocity = 110, enabled = true },
                new GestureMap { gesture = OVRHand.MicrogestureType.SwipeBackward, number = 51, velocity = 110, enabled = true },
                new GestureMap { gesture = OVRHand.MicrogestureType.ThumbTap,      number = 52, velocity = 110, enabled = true },
            };
        }
    }
}
