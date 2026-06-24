using UnityEngine;

namespace QuestMidiBridge
{
    /// <summary>
    /// EXAMPLE ONLY — shows how to drive MIDI from your own hand-tracking values.
    /// It deliberately doesn't depend on the Meta SDK so it always compiles:
    /// feed it a Transform (e.g. a hand/finger anchor) and a couple of values that
    /// your real hand logic sets, and it turns them into MIDI.
    ///
    /// Key idea: only send a CC when the value actually changes, so you don't flood
    /// the bridge with one message per frame.
    /// </summary>
    [AddComponentMenu("Quest MIDI Bridge/Hand MIDI Example")]
    public class HandMidiExample : MonoBehaviour
    {
        public QuestMidiSender sender;

        [Header("Map a tracked height -> CC")]
        public Transform tracked;     // e.g. your right index fingertip anchor
        public int heightCC = 1;      // CC1 = mod wheel
        public float minY = 0f;
        public float maxY = 2f;
        [Tooltip("Use 14-bit CC for buttery-smooth control instead of 0-127 steps.")]
        public bool highResolution = false;

        [Header("Pinch -> Note")]
        public bool pinch;            // set this from your hand logic each frame
        public int pinchNote = 60;    // middle C
        public int pinchVelocity = 110;

        int _lastCC = -1;
        bool _lastPinch;

        void Reset() => sender = GetComponent<QuestMidiSender>();

        void Update()
        {
            if (sender == null) return;

            // Continuous control from the tracked transform's height.
            if (tracked != null)
            {
                float t = Mathf.InverseLerp(minY, maxY, tracked.position.y);
                if (highResolution)
                {
                    sender.SendControlChange14(heightCC, t);
                }
                else
                {
                    int v = Mathf.RoundToInt(Mathf.Clamp01(t) * 127f);
                    if (v != _lastCC) { sender.SendControlChange(heightCC, v); _lastCC = v; }
                }
            }

            // Pinch -> Note On/Off, only on the edges.
            if (pinch && !_lastPinch) sender.SendNoteOn(pinchNote, pinchVelocity);
            else if (!pinch && _lastPinch) sender.SendNoteOff(pinchNote);
            _lastPinch = pinch;
        }
    }
}
