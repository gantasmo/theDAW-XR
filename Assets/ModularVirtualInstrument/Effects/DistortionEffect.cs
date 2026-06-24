using UnityEngine;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Distortion effect - waveshaping/overdrive.
    /// Ported from KaossPad3D.DistortionEffect.
    /// Single-axis version with configurable distortion curve.
    /// </summary>
    [CreateAssetMenu(fileName = "DistortionEffect", menuName = "Modular Virtual Instrument/Effects/Distortion Effect")]
    public class DistortionEffect : AxisEffect
    {
        [Header("Distortion Amount")]
        [Tooltip("Minimum distortion level (0 = clean)")]
        [Range(0f, 0.2f)]
        public float minDistortion = 0f;
        
        [Tooltip("Maximum distortion level (1 = maximum)")]
        [Range(0.2f, 1f)]
        public float maxDistortion = 0.8f;
        
        [Header("Tone Shaping")]
        [Tooltip("Pre-distortion gain boost")]
        [Range(1f, 5f)]
        public float preGain = 1.5f;
        
        [Tooltip("Post-distortion volume compensation")]
        [Range(0.1f, 1f)]
        public float postGain = 0.7f;
        
        [Tooltip("Apply low-pass filter after distortion to smooth harsh frequencies")]
        public bool enablePostFilter = true;
        
        [Tooltip("Post-filter cutoff frequency")]
        [Range(1000f, 22000f)]
        public float postFilterCutoff = 8000f;
        
        [Header("Distortion Character")]
        [Tooltip("Type of distortion curve")]
        public DistortionType distortionType = DistortionType.Standard;
        
        public enum DistortionType
        {
            Standard,   // Unity's built-in distortion
            Soft,       // Softer saturation
            Hard,       // Hard clipping
            Foldback    // Foldback distortion (experimental)
        }
        
        // Audio components
        private AudioDistortionFilter distortionFilter;
        private AudioLowPassFilter postFilter;
        private float currentDistortionLevel = 0f;
        private float originalVolume = 1f;

        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;

            // Remember the source volume so disable can restore it (ProcessEffect drives
            // volume down to postGain for compensation).
            originalVolume = audioSource.volume;

            // Get or add distortion filter
            distortionFilter = audioSource.GetComponent<AudioDistortionFilter>();
            if (distortionFilter == null)
            {
                distortionFilter = audioSource.gameObject.AddComponent<AudioDistortionFilter>();
            }
            distortionFilter.distortionLevel = minDistortion;
            distortionFilter.enabled = true;
            
            // Add post-filter if enabled
            if (enablePostFilter)
            {
                postFilter = audioSource.GetComponent<AudioLowPassFilter>();
                if (postFilter == null)
                {
                    postFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                }
                postFilter.cutoffFrequency = postFilterCutoff;
                postFilter.enabled = true;
            }
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (distortionFilter != null)
            {
                distortionFilter.distortionLevel = 0f;
                distortionFilter.enabled = false;
            }
            
            if (postFilter != null)
            {
                postFilter.cutoffFrequency = 22000f;
                postFilter.enabled = false;
            }

            // Restore the volume the effect compensated down.
            if (audioSource != null)
            {
                audioSource.volume = originalVolume;
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (distortionFilter == null || audioSource == null) return;
            
            // Map value using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            
            // Calculate distortion level
            currentDistortionLevel = Mathf.Lerp(minDistortion, maxDistortion, mappedValue);
            
            // Apply based on distortion type
            if (distortionType == DistortionType.Standard)
            {
                // Use Unity's built-in distortion
                distortionFilter.distortionLevel = currentDistortionLevel;
            }
            else
            {
                // For other types, we'll use a lower distortion level and process in audio filter
                distortionFilter.distortionLevel = currentDistortionLevel * 0.5f;
            }
            
            // Adjust volume based on distortion amount
            float volumeCompensation = Mathf.Lerp(1f, postGain, mappedValue);
            audioSource.volume = volumeCompensation;
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float normalizedValue)
        {
            // Apply custom distortion curves for non-standard types
            if (distortionType == DistortionType.Standard)
                return;
            
            for (int i = 0; i < data.Length; i++)
            {
                float sample = data[i] * preGain;
                
                switch (distortionType)
                {
                    case DistortionType.Soft:
                        // Soft saturation using tanh curve
                        sample = SoftClip(sample, currentDistortionLevel);
                        break;
                        
                    case DistortionType.Hard:
                        // Hard clipping
                        sample = HardClip(sample, currentDistortionLevel);
                        break;
                        
                    case DistortionType.Foldback:
                        // Foldback distortion
                        sample = FoldbackClip(sample, currentDistortionLevel);
                        break;
                }
                
                data[i] = sample;
            }
        }
        
        /// <summary>
        /// Soft saturation curve (smooth distortion)
        /// </summary>
        private float SoftClip(float sample, float amount)
        {
            // Tanh-based soft clipping (using System.Math)
            float threshold = 1f - amount;
            if (Mathf.Abs(sample) < threshold)
                return sample;
            
            float sign = Mathf.Sign(sample);
            float excess = Mathf.Abs(sample) - threshold;
            float compressed = threshold + (float)System.Math.Tanh(excess * 3f) * amount;
            return sign * compressed;
        }
        
        /// <summary>
        /// Hard clipping (aggressive distortion)
        /// </summary>
        private float HardClip(float sample, float amount)
        {
            float threshold = 1f - amount;
            return Mathf.Clamp(sample, -threshold, threshold);
        }
        
        /// <summary>
        /// Foldback distortion (experimental, creates interesting harmonics)
        /// </summary>
        private float FoldbackClip(float sample, float amount)
        {
            float threshold = 1f - amount * 0.5f;
            
            while (Mathf.Abs(sample) > threshold)
            {
                if (sample > threshold)
                    sample = 2f * threshold - sample;
                else if (sample < -threshold)
                    sample = -2f * threshold - sample;
            }
            
            return sample;
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Color gradient from orange (clean) to red (heavily distorted)
            Color clean = new Color(1f, 0.7f, 0.2f);    // Orange
            Color distorted = new Color(1f, 0f, 0f);    // Red
            Color extreme = new Color(0.8f, 0f, 0.2f);  // Dark red
            
            if (normalizedValue < 0.5f)
            {
                return Color.Lerp(clean, distorted, normalizedValue * 2f);
            }
            else
            {
                return Color.Lerp(distorted, extreme, (normalizedValue - 0.5f) * 2f);
            }
        }
    }
}
