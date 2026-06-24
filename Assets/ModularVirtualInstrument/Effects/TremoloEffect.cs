using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Tremolo effect - rhythmic volume modulation.
    /// Perfect for EDM builds and drops.
    /// </summary>
    [CreateAssetMenu(fileName = "TremoloEffect", menuName = "Modular Virtual Instrument/Effects/Tremolo")]
    public class TremoloEffect : AxisEffect
    {
        [Header("Tremolo Settings")]
        [Tooltip("Modulation rate in Hz")]
        [Range(0.1f, 30f)]
        public float minRate = 1f;
        
        [Range(0.1f, 30f)]
        public float maxRate = 16f;
        
        [Tooltip("Depth of volume modulation")]
        [Range(0f, 1f)]
        public float depth = 0.8f;
        
        [Tooltip("Waveform shape")]
        public enum WaveformType { Sine, Square, Triangle, Sawtooth }
        public WaveformType waveform = WaveformType.Sine;
        
        private float phase = 0f;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            phase = 0f;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            // Nothing to clean up
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            // Effect is processed in ProcessAudioFilter
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            // Calculate current rate
            float currentRate = Mathf.Lerp(minRate, maxRate, value);
            float phaseIncrement = currentRate / AudioSettings.outputSampleRate;
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Generate modulation waveform
                float modulation = GetWaveformValue(phase);
                
                // Scale modulation by depth
                float amplitude = 1f - (depth * value * (1f - modulation));
                
                // Apply to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i + ch] *= amplitude;
                }
                
                // Increment phase
                phase += phaseIncrement;
                if (phase > 1f) phase -= 1f;
            }
        }
        
        private float GetWaveformValue(float phase)
        {
            switch (waveform)
            {
                case WaveformType.Sine:
                    return (Mathf.Sin(phase * Mathf.PI * 2f) + 1f) * 0.5f;
                    
                case WaveformType.Square:
                    return phase < 0.5f ? 1f : 0f;
                    
                case WaveformType.Triangle:
                    return phase < 0.5f ? phase * 2f : 2f - (phase * 2f);
                    
                case WaveformType.Sawtooth:
                    return phase;
                    
                default:
                    return 0.5f;
            }
        }
        
        public override Color GetEffectColor(float value)
        {
            // Pulsing yellow to red
            return Color.Lerp(Color.yellow, Color.red, value);
        }
    }
}
