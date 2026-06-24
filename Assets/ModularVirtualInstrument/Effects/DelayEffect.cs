using UnityEngine;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Delay/Echo effect with feedback control.
    /// Ported from KaossPad3D.DelayEffect.
    /// Single-axis version of the original 2D effect.
    /// </summary>
    [CreateAssetMenu(fileName = "DelayEffect", menuName = "Modular Virtual Instrument/Effects/Delay Effect")]
    public class DelayEffect : AxisEffect
    {
        [Header("Delay Time Settings")]
        [Tooltip("Minimum delay time in milliseconds")]
        [Range(10f, 500f)]
        public float minDelayMs = 10f;
        
        [Tooltip("Maximum delay time in milliseconds")]
        [Range(100f, 2000f)]
        public float maxDelayMs = 1000f;
        
        [Header("Feedback Settings")]
        [Tooltip("Control feedback with axis value (if false, uses fixed feedback)")]
        public bool axisControlsFeedback = false;
        
        [Tooltip("Minimum feedback amount (echo decay)")]
        [Range(0f, 0.95f)]
        public float minFeedback = 0f;
        
        [Tooltip("Maximum feedback amount")]
        [Range(0f, 0.95f)]
        public float maxFeedback = 0.8f;
        
        [Tooltip("Fixed feedback value (used if axisControlsFeedback is false)")]
        [Range(0f, 0.95f)]
        public float fixedFeedback = 0.5f;
        
        [Header("Mix Settings")]
        [Tooltip("Dry/Wet mix - 0 = dry only, 1 = wet only")]
        [Range(0f, 1f)]
        public float wetMix = 0.5f;
        
        // Unity's AudioEchoFilter component
        private AudioEchoFilter echoFilter;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Get or add echo filter
            echoFilter = audioSource.GetComponent<AudioEchoFilter>();
            if (echoFilter == null)
            {
                echoFilter = audioSource.gameObject.AddComponent<AudioEchoFilter>();
            }
            
            // Initialize with minimum values
            echoFilter.delay = minDelayMs;
            echoFilter.decayRatio = minFeedback;
            echoFilter.wetMix = wetMix;
            echoFilter.dryMix = 1f;
            echoFilter.enabled = true;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (echoFilter != null)
            {
                echoFilter.enabled = false;
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (echoFilter == null || audioSource == null) return;
            
            // Map value using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            
            // Calculate delay time
            float delayMs = Mathf.Lerp(minDelayMs, maxDelayMs, mappedValue);
            echoFilter.delay = delayMs;
            
            // Calculate feedback
            float feedback;
            if (axisControlsFeedback)
            {
                // Use axis value to control feedback too
                feedback = Mathf.Lerp(minFeedback, maxFeedback, mappedValue);
            }
            else
            {
                // Use fixed feedback value
                feedback = fixedFeedback;
            }
            echoFilter.decayRatio = feedback;
            
            // Set mix levels
            echoFilter.wetMix = wetMix;
            echoFilter.dryMix = 1f;
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Color gradient from green (short delay) to cyan (long delay)
            Color shortDelay = new Color(0f, 1f, 0.3f); // Bright green
            Color longDelay = new Color(0f, 0.8f, 1f);  // Cyan
            
            return Color.Lerp(shortDelay, longDelay, normalizedValue);
        }
    }
}
