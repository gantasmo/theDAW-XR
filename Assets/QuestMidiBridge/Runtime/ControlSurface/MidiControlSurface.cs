using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo.XRMidi
{
    /// <summary>
    /// Routes every control on a GANTASMO XR control surface to one
    /// <see cref="QuestMidiSender"/> on a shared channel. Sliders and knobs
    /// publish Control Change; buttons publish Note On/Off or a latched CC.
    /// Drop this on the surface root; the child controls find it in their parent.
    /// </summary>
    [AddComponentMenu("GANTASMO XR MIDI/Control Surface")]
    public class MidiControlSurface : MonoBehaviour
    {
        [Tooltip("The bridge that ships MIDI to theDAW. Auto-found on this object or a parent when left empty.")]
        public QuestMidiSender sender;

        [Range(1, 16)]
        [Tooltip("MIDI channel that every control on this surface sends on.")]
        public int channel = 1;

        [Tooltip("Send 14-bit (high-resolution) CC for sliders and knobs. " +
                 "14-bit consumes the CC at cc and its pair at cc+32, so re-allocate " +
                 "CC numbers to the 0-31 range before enabling it for a full surface.")]
        public bool highResolution = false;

        /// <summary>Quantization steps for the current resolution: 128 (7-bit) or 16384 (14-bit).</summary>
        public int Steps => highResolution ? 16384 : 128;

        void Awake()
        {
            if (sender == null) sender = GetComponent<QuestMidiSender>();
            if (sender == null) sender = GetComponentInParent<QuestMidiSender>();
        }

        /// <summary>Send a normalized 0..1 value as Control Change on <paramref name="cc"/>.</summary>
        public void SendCC(int cc, float value01)
        {
            if (sender == null) return;
            if (highResolution) sender.SendControlChange14(cc, value01, channel);
            else sender.SendFloat01AsCC(cc, value01, channel);
        }

        /// <summary>Send a raw 7-bit Control Change value (used by latched buttons).</summary>
        public void SendCCValue(int cc, int value7bit)
        {
            if (sender == null) return;
            sender.SendControlChange(cc, value7bit, channel);
        }

        public void SendNoteOn(int note, int velocity)
        {
            if (sender != null) sender.SendNoteOn(note, velocity, channel);
        }

        public void SendNoteOff(int note)
        {
            if (sender != null) sender.SendNoteOff(note, 0, channel);
        }
    }
}
