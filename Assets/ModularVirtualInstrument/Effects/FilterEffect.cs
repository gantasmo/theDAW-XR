using UnityEngine;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Filter sweep effect - controls frequency cutoff.
    /// Ported from KaossPad3D.FilterEffect.
    /// Single-axis version with low-pass filtering.
    /// </summary>
    [CreateAssetMenu(fileName = "FilterEffect", menuName = "Modular Virtual Instrument/Effects/Filter Effect")]
    public class FilterEffect : AxisEffect
    {
        [Header("Filter Type")]
        [Tooltip("Type of filter to use")]
        public FilterType filterType = FilterType.LowPass;
        
        public enum FilterType
        {
            LowPass,    // Cuts high frequencies
            HighPass,   // Cuts low frequencies
            BandPass    // Allows only a band of frequencies
        }
        
        [Header("Frequency Range")]
        [Tooltip("Minimum cutoff frequency (Hz) - darker sound")]
        [Range(100f, 5000f)]
        public float minCutoff = 200f;
        
        [Tooltip("Maximum cutoff frequency (Hz) - brighter sound")]
        [Range(1000f, 22000f)]
        public float maxCutoff = 10000f;
        
        [Header("Resonance")]
        [Tooltip("Control resonance with axis value")]
        public bool axisControlsResonance = false;
        
        [Tooltip("Minimum resonance (Q factor)")]
        [Range(1f, 10f)]
        public float minResonance = 1f;
        
        [Tooltip("Maximum resonance (Q factor) - emphasizes cutoff frequency")]
        [Range(1f, 10f)]
        public float maxResonance = 5f;
        
        [Tooltip("Fixed resonance value (if not controlled by axis)")]
        [Range(1f, 10f)]
        public float fixedResonance = 2f;
        
        // Filter components
        private AudioLowPassFilter lowPassFilter;
        private AudioHighPassFilter highPassFilter;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Create appropriate filter based on type
            switch (filterType)
            {
                case FilterType.LowPass:
                    lowPassFilter = audioSource.GetComponent<AudioLowPassFilter>();
                    if (lowPassFilter == null)
                    {
                        lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                    lowPassFilter.cutoffFrequency = maxCutoff;
                    lowPassFilter.lowpassResonanceQ = fixedResonance;
                    lowPassFilter.enabled = true;
                    break;
                    
                case FilterType.HighPass:
                    highPassFilter = audioSource.GetComponent<AudioHighPassFilter>();
                    if (highPassFilter == null)
                    {
                        highPassFilter = audioSource.gameObject.AddComponent<AudioHighPassFilter>();
                    }
                    highPassFilter.cutoffFrequency = minCutoff;
                    highPassFilter.highpassResonanceQ = fixedResonance;
                    highPassFilter.enabled = true;
                    break;
                    
                case FilterType.BandPass:
                    // BandPass uses both filters
                    lowPassFilter = audioSource.GetComponent<AudioLowPassFilter>();
                    if (lowPassFilter == null)
                    {
                        lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                    }
                    highPassFilter = audioSource.GetComponent<AudioHighPassFilter>();
                    if (highPassFilter == null)
                    {
                        highPassFilter = audioSource.gameObject.AddComponent<AudioHighPassFilter>();
                    }
                    lowPassFilter.enabled = true;
                    highPassFilter.enabled = true;
                    break;
            }
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (lowPassFilter != null)
            {
                lowPassFilter.cutoffFrequency = 22000f;
                lowPassFilter.enabled = false;
            }
            
            if (highPassFilter != null)
            {
                highPassFilter.cutoffFrequency = 10f;
                highPassFilter.enabled = false;
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (audioSource == null) return;
            
            // Map value using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            
            // Calculate cutoff frequency
            float cutoffFreq = Mathf.Lerp(minCutoff, maxCutoff, mappedValue);
            
            // Calculate resonance
            float resonance = axisControlsResonance 
                ? Mathf.Lerp(minResonance, maxResonance, mappedValue)
                : fixedResonance;
            
            // Apply to appropriate filter
            switch (filterType)
            {
                case FilterType.LowPass:
                    if (lowPassFilter != null)
                    {
                        lowPassFilter.cutoffFrequency = cutoffFreq;
                        lowPassFilter.lowpassResonanceQ = resonance;
                    }
                    break;
                    
                case FilterType.HighPass:
                    if (highPassFilter != null)
                    {
                        highPassFilter.cutoffFrequency = cutoffFreq;
                        highPassFilter.highpassResonanceQ = resonance;
                    }
                    break;
                    
                case FilterType.BandPass:
                    if (lowPassFilter != null && highPassFilter != null)
                    {
                        // Create a band by setting both filters
                        float bandWidth = 2000f; // Hz
                        lowPassFilter.cutoffFrequency = cutoffFreq + bandWidth;
                        highPassFilter.cutoffFrequency = cutoffFreq - bandWidth;
                        lowPassFilter.lowpassResonanceQ = resonance;
                        highPassFilter.highpassResonanceQ = resonance;
                    }
                    break;
            }
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Color gradient based on frequency
            // Low frequency = red (warm/dark), High frequency = blue (cool/bright)
            Color lowFreq = new Color(1f, 0.3f, 0f);   // Orange-red
            Color midFreq = new Color(1f, 1f, 0.5f);   // Yellow-white
            Color highFreq = new Color(0.5f, 0.8f, 1f); // Light blue
            
            if (normalizedValue < 0.5f)
            {
                return Color.Lerp(lowFreq, midFreq, normalizedValue * 2f);
            }
            else
            {
                return Color.Lerp(midFreq, highFreq, (normalizedValue - 0.5f) * 2f);
            }
        }
    }
}
