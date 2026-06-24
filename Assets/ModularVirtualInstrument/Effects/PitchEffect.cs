using UnityEngine;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Pitch effect - changes the tone/frequency of the sound (making it higher or lower).
    /// Note: This will also affect playback speed slightly since Unity's AudioSource.pitch
    /// changes both pitch and tempo together (like playing a record faster/slower).
    /// For tempo-independent pitch shifting, a dedicated audio DSP plugin would be needed.
    /// </summary>
    [CreateAssetMenu(fileName = "PitchEffect", menuName = "Modular Virtual Instrument/Effects/Pitch Effect")]
    public class PitchEffect : AxisEffect
    {
        [Header("Pitch Range")]
        [Tooltip("Lowest pitch multiplier - 0.5 = one octave lower (half frequency), 0.25 = two octaves lower")]
        [Range(0.25f, 1f)]
        public float minPitch = 0.5f;
        
        [Tooltip("Highest pitch multiplier - 2.0 = one octave higher (double frequency), 3.0 = ~1.5 octaves higher")]
        [Range(1f, 4f)]
        public float maxPitch = 2.5f;
        
        [Header("Growl Effect (Low Pitch)")]
        [Tooltip("Add a gritty, distorted character to low-pitched sounds for extra depth")]
        public bool enableGrowl = true;
        
        [Tooltip("Pitch value below which the growl effect activates (0.7 = activates when pitch is lower than 70% of range)")]
        [Range(0.1f, 0.9f)]
        public float growlThreshold = 0.7f;
        
        [Tooltip("Amount of harmonic distortion added to create growl texture (0 = clean, 1 = heavily distorted)")]
        [Range(0f, 1f)]
        public float growlDistortion = 0.5f;
        
        // Audio components (added dynamically)
        private AudioLowPassFilter lowPassFilter;
        private AudioDistortionFilter distortionFilter;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Set initial pitch to neutral
            audioSource.pitch = 1.0f;
            
            // Add filters if growl is enabled
            if (enableGrowl)
            {
                lowPassFilter = audioSource.GetComponent<AudioLowPassFilter>();
                if (lowPassFilter == null)
                {
                    lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                }
                lowPassFilter.cutoffFrequency = 22000f;
                
                distortionFilter = audioSource.GetComponent<AudioDistortionFilter>();
                if (distortionFilter == null)
                {
                    distortionFilter = audioSource.gameObject.AddComponent<AudioDistortionFilter>();
                }
                distortionFilter.distortionLevel = 0f;
            }
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Reset pitch to neutral
            audioSource.pitch = 1.0f;
            
            // Disable filters
            if (lowPassFilter != null)
            {
                lowPassFilter.cutoffFrequency = 22000f;
                lowPassFilter.enabled = false;
            }
            
            if (distortionFilter != null)
            {
                distortionFilter.distortionLevel = 0f;
                distortionFilter.enabled = false;
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (audioSource == null) return;
            
            // Map normalized value to pitch range using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, mappedValue);
            
            // Apply pitch change - this will affect both tone AND playback speed
            audioSource.pitch = targetPitch;
            
            // Apply growl effect if enabled and pitch is low
            if (enableGrowl && targetPitch < growlThreshold)
            {
                ApplyGrowlEffect(targetPitch);
            }
            else if (enableGrowl)
            {
                // Disable growl when pitch is normal/high
                if (lowPassFilter != null)
                {
                    lowPassFilter.cutoffFrequency = 22000f;
                }
                if (distortionFilter != null)
                {
                    distortionFilter.distortionLevel = 0f;
                }
            }
        }
        
        /// <summary>
        /// Apply growl effect using low-pass filter and distortion for extra character on low pitches
        /// </summary>
        private void ApplyGrowlEffect(float currentPitch)
        {
            if (lowPassFilter == null || distortionFilter == null) return;
            
            // Calculate growl intensity based on how low the pitch is
            float growlIntensity = Mathf.InverseLerp(growlThreshold, minPitch, currentPitch);
            
            // Low-pass filter: darker, muffled sound when pitch is lower
            float cutoffFreq = Mathf.Lerp(2000f, 200f, growlIntensity);
            lowPassFilter.cutoffFrequency = cutoffFreq;
            lowPassFilter.enabled = true;
            
            // Distortion: add harmonic grit to low pitches
            float distortion = Mathf.Lerp(0f, growlDistortion, growlIntensity);
            distortionFilter.distortionLevel = distortion;
            distortionFilter.enabled = true;
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            // Color shifts from deep blue (low pitch) to bright red (high pitch)
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Default: Deep Blue (low) -> Yellow (mid) -> Bright Red (high)
            if (normalizedValue < 0.5f)
            {
                return Color.Lerp(new Color(0.1f, 0.1f, 0.8f), Color.yellow, normalizedValue * 2f);
            }
            else
            {
                return Color.Lerp(Color.yellow, new Color(1f, 0.2f, 0.2f), (normalizedValue - 0.5f) * 2f);
            }
        }
    }
}
