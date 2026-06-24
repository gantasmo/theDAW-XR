using UnityEngine;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Bit crushing effect - reduces bit depth for lo-fi/glitch sound.
    /// Ported from Synth69.AudioEffectsManager bit crushing logic.
    /// Uses OnAudioFilterRead for real-time DSP.
    /// </summary>
    [CreateAssetMenu(fileName = "BitCrushEffect", menuName = "Modular Virtual Instrument/Effects/Bit Crush Effect")]
    public class BitCrushEffect : AxisEffect
    {
        [Header("Bit Depth Settings")]
        [Tooltip("Minimum bit depth (1 = extreme crushing)")]
        [Range(1, 16)]
        public int minBitDepth = 1;
        
        [Tooltip("Maximum bit depth (24 = near original quality)")]
        [Range(1, 24)]
        public int maxBitDepth = 24;
        
        [Header("Sample Rate Reduction")]
        [Tooltip("Enable sample rate reduction (additional lo-fi effect)")]
        public bool enableDownsampling = true;
        
        [Tooltip("Sample hold interval at maximum effect")]
        [Range(1, 16)]
        public int maxSampleHold = 8;
        
        [Header("Glitch Enhancement")]
        [Tooltip("Add random glitches at high crush values")]
        public bool enableGlitchEffect = true;
        
        [Tooltip("Threshold above which glitches start (0-1)")]
        [Range(0.5f, 1f)]
        public float glitchThreshold = 0.7f;
        
        // Internal state
        private float currentBitDepth = 24f;
        private int sampleCounter = 0;
        private float lastSample = 0f;
        private int currentSampleHold = 1;
        private System.Random random = new System.Random();
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            // Reset state
            currentBitDepth = maxBitDepth;
            sampleCounter = 0;
            lastSample = 0f;
            currentSampleHold = 1;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            // Reset to clean state
            currentBitDepth = 24f;
            sampleCounter = 0;
            lastSample = 0f;
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            // Map value to bit depth using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            currentBitDepth = Mathf.Lerp(maxBitDepth, minBitDepth, mappedValue);
            
            // Calculate sample hold based on value
            if (enableDownsampling)
            {
                currentSampleHold = Mathf.RoundToInt(Mathf.Lerp(1, maxSampleHold, mappedValue));
            }
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float normalizedValue)
        {
            if (currentBitDepth >= maxBitDepth - 1)
            {
                // No processing needed at maximum quality
                return;
            }
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Apply bit crushing to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    int sampleIndex = i + ch;
                    
                    // Downsampling (sample and hold)
                    if (enableDownsampling)
                    {
                        if (sampleCounter % currentSampleHold == 0)
                        {
                            lastSample = data[sampleIndex];
                        }
                        data[sampleIndex] = lastSample;
                    }
                    
                    // Bit depth reduction
                    data[sampleIndex] = ApplyBitCrushing(data[sampleIndex], currentBitDepth);
                    
                    // Glitch effect
                    if (enableGlitchEffect && normalizedValue > glitchThreshold)
                    {
                        data[sampleIndex] = ApplyGlitchEffect(data[sampleIndex], normalizedValue);
                    }
                }
                
                sampleCounter++;
            }
        }
        
        /// <summary>
        /// Reduce bit depth of a sample
        /// </summary>
        private float ApplyBitCrushing(float sample, float bitDepth)
        {
            // Calculate number of quantization levels
            float levels = Mathf.Pow(2f, bitDepth);
            
            // Quantize the sample
            float quantized = Mathf.Round(sample * levels) / levels;
            
            return quantized;
        }
        
        /// <summary>
        /// Apply random glitches for extra lo-fi character
        /// </summary>
        private float ApplyGlitchEffect(float sample, float intensity)
        {
            // Calculate glitch probability based on intensity
            float glitchIntensity = Mathf.InverseLerp(glitchThreshold, 1f, intensity);
            
            // Random chance to glitch
            if (random.NextDouble() < glitchIntensity * 0.1f)
            {
                // Random amplitude disturbance
                float glitchAmount = ((float)random.NextDouble() - 0.5f) * 2f * glitchIntensity;
                sample += glitchAmount * 0.3f;
                sample = Mathf.Clamp(sample, -1f, 1f);
            }
            
            return sample;
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Color from clean white to glitchy magenta/cyan
            Color cleanColor = new Color(0.9f, 0.9f, 1f); // Light blue-white
            Color crushedColor = new Color(1f, 0f, 1f); // Magenta (digital glitch)
            
            return Color.Lerp(cleanColor, crushedColor, normalizedValue);
        }
    }
}
