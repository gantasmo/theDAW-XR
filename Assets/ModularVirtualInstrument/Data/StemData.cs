using UnityEngine;
using UnityEngine.Audio;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Configuration data for a single audio stem.
    /// Defines the audio clip, default effects, and visual appearance.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStemData", menuName = "Modular Virtual Instrument/Stem Data")]
    public class StemData : ScriptableObject
    {
        [Header("Stem Identity")]
        public string stemName = "Stem";
        [TextArea(2, 4)]
        public string description;
        
        [Header("Audio")]
        public AudioClip audioClip;
        [Range(0f, 1f)]
        public float defaultVolume = 0.7f;
        public bool loopAudio = true;
        public AudioMixerGroup outputMixerGroup;
        
        [Header("Default Effects")]
        [Tooltip("Effect applied to X-axis (Red) - typically horizontal movement")]
        public AxisEffect xAxisEffect;
        
        [Tooltip("Effect applied to Y-axis (Green) - typically vertical movement")]
        public AxisEffect yAxisEffect;
        
        [Tooltip("Effect applied to Z-axis (Blue) - typically depth movement")]
        public AxisEffect zAxisEffect;
        
        [Header("Interaction Volume")]
        [Tooltip("Size of the interaction cube")]
        public Vector3 cubeSize = Vector3.one;
        
        [Tooltip("Enable smoothing for hand tracking within this stem")]
        [Range(0.1f, 1.0f)]
        public float handTrackingSmoothing = 0.8f;
        
        [Header("Visual Appearance")]
        [Tooltip("Primary color for this stem's visualization")]
        public Color themeColor = Color.cyan;
        
        [Tooltip("Secondary color for visual effects")]
        public Color accentColor = Color.white;
        
        [Tooltip("Emission intensity for neon glow")]
        [Range(0f, 10f)]
        public float emissionIntensity = 2f;
        
        [Header("Spatial Preferences")]
        [Tooltip("Preferred position offset (will be overridden by layout strategy)")]
        public Vector3 preferredPositionOffset = Vector3.zero;
        
        [Tooltip("Custom rotation for this stem's cube")]
        public Vector3 preferredRotation = Vector3.zero;
        
        [Header("Advanced")]
        [Tooltip("Priority for hand assignment (higher = preferred)")]
        [Range(0, 10)]
        public int handAssignmentPriority = 5;
        
        [Tooltip("Enable audio reactive visualization")]
        public bool audioReactiveVisuals = true;
        
        [Tooltip("Show waveform on cube faces")]
        public bool showWaveform = true;
    }
}
