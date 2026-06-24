using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Ring modulator for metallic, robotic EDM sounds.
    /// Multiplies audio signal with a carrier frequency.
    /// </summary>
    [CreateAssetMenu(fileName = "RingModulatorEffect", menuName = "Modular Virtual Instrument/Effects/Ring Modulator")]
    public class RingModulatorEffect : AxisEffect
    {
        [Header("Ring Modulation Settings")]
        [Tooltip("Carrier frequency in Hz")]
        [Range(20f, 5000f)]
        public float minFrequency = 100f;
        
        [Range(20f, 5000f)]
        public float maxFrequency = 2000f;
        
        [Tooltip("Wet/dry mix")]
        [Range(0f, 1f)]
        public float wetMix = 0.7f;
        
        [Tooltip("Carrier waveform type")]
        public enum CarrierWaveform { Sine, Square, Triangle, Saw }
        public CarrierWaveform carrierType = CarrierWaveform.Sine;
        
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
            // Calculate carrier frequency
            float frequency = Mathf.Lerp(minFrequency, maxFrequency, value);
            float phaseIncrement = frequency / AudioSettings.outputSampleRate;
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Generate carrier signal
                float carrier = GetCarrierValue(phase);
                
                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i + ch;
                    
                    // Ring modulation: multiply input by carrier
                    float modulated = data[idx] * carrier;
                    
                    // Mix wet and dry
                    data[idx] = Mathf.Lerp(data[idx], modulated, wetMix * value);
                }
                
                // Increment phase
                phase += phaseIncrement;
                if (phase > 1f) phase -= 1f;
            }
        }
        
        private float GetCarrierValue(float p)
        {
            switch (carrierType)
            {
                case CarrierWaveform.Sine:
                    return Mathf.Sin(p * Mathf.PI * 2f);
                    
                case CarrierWaveform.Square:
                    return p < 0.5f ? 1f : -1f;
                    
                case CarrierWaveform.Triangle:
                    return p < 0.5f ? (p * 4f - 1f) : (3f - p * 4f);
                    
                case CarrierWaveform.Saw:
                    return p * 2f - 1f;
                    
                default:
                    return 0f;
            }
        }
        
        public override Color GetEffectColor(float value)
        {
            // Silver to gold metallic
            return Color.Lerp(new Color(0.75f, 0.75f, 0.75f), new Color(1f, 0.84f, 0f), value);
        }
    }
}
