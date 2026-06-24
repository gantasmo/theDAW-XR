using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Applies reverb/echo effect for EDM spaciousness.
    /// Uses AudioReverbFilter for room simulation.
    /// </summary>
    [CreateAssetMenu(fileName = "ReverbEffect", menuName = "Modular Virtual Instrument/Effects/Reverb")]
    public class ReverbEffect : AxisEffect
    {
        [Header("Reverb Settings")]
        [Tooltip("Reverb preset to use")]
        public AudioReverbPreset reverbPreset = AudioReverbPreset.Hangar;
        
        [Tooltip("Dry/wet mix (0=dry, 1=wet)")]
        [Range(-10000f, 0f)]
        public float minReverbLevel = -10000f;
        
        [Range(-10000f, 0f)]
        public float maxReverbLevel = -200f;
        
        [Tooltip("Room size simulation")]
        [Range(-10000f, 0f)]
        public float roomSize = -1000f;
        
        private AudioReverbFilter reverbFilter;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Add or get reverb filter
            reverbFilter = audioSource.GetComponent<AudioReverbFilter>();
            if (reverbFilter == null)
            {
                reverbFilter = audioSource.gameObject.AddComponent<AudioReverbFilter>();
            }
            
            // Set preset
            reverbFilter.reverbPreset = reverbPreset;
            reverbFilter.room = roomSize;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (reverbFilter != null)
            {
                Object.Destroy(reverbFilter);
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (reverbFilter == null) return;
            
            // Control reverb level
            reverbFilter.reverbLevel = Mathf.Lerp(minReverbLevel, maxReverbLevel, normalizedValue);
            
            // Adjust room size slightly based on value
            reverbFilter.room = roomSize + (normalizedValue * 1000f);
        }
        
        public override Color GetEffectColor(float value)
        {
            // Purple to white gradient for reverb depth
            return Color.Lerp(new Color(0.5f, 0f, 1f), Color.white, value);
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            // Reverb is handled by AudioReverbFilter component
        }
    }
}
