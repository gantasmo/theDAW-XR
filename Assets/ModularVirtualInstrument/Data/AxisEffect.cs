using UnityEngine;
using UnityEngine.Audio;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Base class for all axis-based effects.
    /// Each effect operates on a single axis (X, Y, or Z) with a 0-1 normalized value.
    /// </summary>
    public abstract class AxisEffect : ScriptableObject
    {
        [Header("Effect Settings")]
        public string effectName = "Effect";
        
        [Header("Response Curve")]
        [Tooltip("Maps normalized input (0-1) to effect parameter")]
        public AnimationCurve responseCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Header("Visual Feedback")]
        [Tooltip("Color used for visual feedback of this effect")]
        public Color effectColor = Color.white;
        public Gradient effectGradient;
        
        /// <summary>
        /// Process the effect with the given normalized value (0-1)
        /// </summary>
        /// <param name="normalizedValue">Value from 0 to 1 representing position along the axis</param>
        /// <param name="audioSource">AudioSource to apply the effect to</param>
        /// <param name="deltaTime">Time since last update</param>
        public abstract void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime);
        
        /// <summary>
        /// Called when this effect is enabled/assigned to an axis
        /// </summary>
        /// <param name="audioSource">AudioSource to initialize effect on</param>
        public abstract void OnEffectEnabled(AudioSource audioSource);
        
        /// <summary>
        /// Called when this effect is disabled/unassigned from an axis
        /// </summary>
        /// <param name="audioSource">AudioSource to cleanup effect on</param>
        public abstract void OnEffectDisabled(AudioSource audioSource);
        
        /// <summary>
        /// Get the mapped value using the response curve
        /// </summary>
        protected float GetMappedValue(float normalizedValue)
        {
            return responseCurve.Evaluate(Mathf.Clamp01(normalizedValue));
        }
        
        /// <summary>
        /// Optional: Override for custom OnAudioFilterRead processing
        /// </summary>
        public virtual void ProcessAudioFilter(float[] data, int channels, float normalizedValue)
        {
            // Override in derived classes if needed
        }
        
        /// <summary>
        /// Get visual color for current effect value
        /// </summary>
        public virtual Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            return Color.Lerp(Color.white, effectColor, normalizedValue);
        }
    }
}
