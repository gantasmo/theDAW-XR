using System;
using UnityEngine;
using QuestMidiBridge;

namespace Gantasmo.XrViz
{
    /// <summary>
    /// Receives theDAW's visualization feed off the questmidi return channel and
    /// exposes it for renderers. theDAW sends valid MIDI SysEx frames
    /// [0xF0, 0x7D, type, ...7-bit payload, 0xF7]; this listens to
    /// <see cref="QuestMidiSender.MidiReceived"/> (raw bytes, main thread),
    /// keeps only the 0xF0/0x7D frames, and decodes them. Plain MIDI (CC/Note)
    /// is untouched, so the MIDI Reactor keeps working alongside this.
    /// </summary>
    [AddComponentMenu("GANTASMO/Visuals/XR Viz Receiver")]
    public class XrVizReceiver : MonoBehaviour
    {
        [Tooltip("The MIDI bridge to read from. Auto-found in the scene when empty.")]
        public QuestMidiSender sender;

        const byte SysExStart = 0xF0;
        const byte Mfg = 0x7D;
        const byte SysExEnd = 0xF7;
        const byte TypeWaveform = 0x01;

        /// <summary>Latest waveform, each sample in -1..1. Length tracks the feed.</summary>
        public float[] Waveform { get; private set; } = new float[128];

        /// <summary>Raised on the main thread after a new waveform frame decodes.</summary>
        public event Action WaveformUpdated;

        void Awake()
        {
            if (sender == null) sender = FindAnyObjectByType<QuestMidiSender>();
        }

        void OnEnable()
        {
            if (sender != null) sender.MidiReceived += OnMidi;
        }

        void OnDisable()
        {
            if (sender != null) sender.MidiReceived -= OnMidi;
        }

        void OnMidi(byte[] m)
        {
            // Smallest viz frame is F0 7D type ... F7.
            if (m == null || m.Length < 4) return;
            if (m[0] != SysExStart || m[1] != Mfg) return;

            byte type = m[2];
            int end = m.Length;
            if (end > 0 && m[end - 1] == SysExEnd) end--; // drop the terminator

            if (type == TypeWaveform)
            {
                int n = end - 3;
                if (n <= 0) return;
                if (Waveform.Length != n) Waveform = new float[n];
                for (int i = 0; i < n; i++)
                {
                    // 7-bit 0..127 (64 = silence) -> -1..1
                    Waveform[i] = (m[3 + i] / 127f) * 2f - 1f;
                }
                try { WaveformUpdated?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
