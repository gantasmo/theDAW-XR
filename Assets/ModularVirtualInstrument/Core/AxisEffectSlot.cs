using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Represents a single axis (X, Y, or Z) with an assigned effect.
    /// Links the axis to its effect and tracks the current parameter value.
    /// </summary>
    [System.Serializable]
    public class AxisEffectSlot
    {
        public enum AxisType
        {
            X,  // Typically horizontal (Red)
            Y,  // Typically vertical (Green)
            Z   // Typically depth (Blue)
        }
        
        [Header("Axis Configuration")]
        public AxisType axis;
        public string axisLabel = "";
        
        [Header("Effect Assignment")]
        public AxisEffect effect;
        
        [Header("Current State")]
        [Range(0f, 1f)]
        public float currentValue = 0f;
        
        [Range(0f, 1f)]
        public float smoothedValue = 0f;
        
        [Header("Settings")]
        [Tooltip("Invert the axis direction")]
        public bool invertAxis = false;
        
        [Tooltip("Smoothing factor for value changes (0 = no smoothing, 1 = max smoothing)")]
        [Range(0f, 1f)]
        public float smoothingFactor = 0.8f;
        
        [Tooltip("Enable this axis")]
        public bool enabled = true;
        
        // Internal state
        private AudioSource cachedAudioSource;
        private bool isEffectActive = false;

        // The effect asset assigned in data is treated as a TEMPLATE. At runtime each
        // slot processes a private clone so multiple stems referencing the same effect
        // asset can never corrupt each other's DSP/component state (or leak that state
        // across play sessions). 'effect' points at the clone while playing.
        private AxisEffect templateEffect;
        private bool ownsEffectInstance = false;

        /// <summary>
        /// Initialize the slot with an audio source
        /// </summary>
        public void Initialize(AudioSource audioSource)
        {
            cachedAudioSource = audioSource;
            smoothedValue = 0f;
            currentValue = 0f;

            // Adopt a directly-assigned (inspector) effect as the template if SetEffect
            // was never called.
            if (templateEffect == null && effect != null && !ownsEffectInstance)
            {
                templateEffect = effect;
            }

            EnsureRuntimeInstance();

            if (effect != null && enabled)
            {
                effect.OnEffectEnabled(audioSource);
                isEffectActive = true;
            }
        }

        /// <summary>
        /// In play mode, swap 'effect' to a private clone of the template so this slot
        /// owns its own effect state. No-op in edit mode (assets are used directly).
        /// </summary>
        private void EnsureRuntimeInstance()
        {
            if (Application.isPlaying && templateEffect != null && !ownsEffectInstance)
            {
                effect = Object.Instantiate(templateEffect);
                effect.name = templateEffect.name + " (Runtime)";
                ownsEffectInstance = true;
            }
        }
        
        /// <summary>
        /// Update the effect with a new normalized value
        /// </summary>
        public void UpdateEffect(float normalizedValue, float deltaTime)
        {
            if (!enabled || effect == null || cachedAudioSource == null)
            {
                return;
            }
            
            // Apply inversion if needed
            float processedValue = invertAxis ? (1f - normalizedValue) : normalizedValue;

            // Framerate-independent exponential smoothing. smoothingFactor is the
            // fraction of the old value retained per 1/60 s frame; rescaling by actual
            // deltaTime keeps the same wall-clock response (and latency) at 60, 72, or
            // 90 Hz, so the feel no longer changes with framerate.
            if (smoothingFactor > 0.01f && deltaTime > 0f)
            {
                const float referenceDelta = 1f / 60f;
                float retain = Mathf.Pow(Mathf.Clamp01(smoothingFactor), deltaTime / referenceDelta);
                smoothedValue = Mathf.Lerp(processedValue, smoothedValue, retain);
            }
            else
            {
                smoothedValue = processedValue;
            }
            
            currentValue = smoothedValue;
            
            // Process the effect
            effect.ProcessEffect(smoothedValue, cachedAudioSource, deltaTime);
        }
        
        /// <summary>
        /// Set a new effect and reinitialize
        /// </summary>
        public void SetEffect(AxisEffect newEffect)
        {
            // Tear down + dispose any current runtime effect first.
            DisposeRuntimeEffect();

            templateEffect = newEffect;
            effect = newEffect;
            ownsEffectInstance = false;

            EnsureRuntimeInstance();

            // Enable new effect
            if (effect != null && enabled && cachedAudioSource != null)
            {
                effect.OnEffectEnabled(cachedAudioSource);
                isEffectActive = true;
            }
            else
            {
                isEffectActive = false;
            }
        }

        /// <summary>
        /// Clean up when slot is destroyed
        /// </summary>
        public void Cleanup()
        {
            DisposeRuntimeEffect();
        }

        /// <summary>
        /// Disable the active effect (restoring the AudioSource) and destroy the
        /// per-slot runtime clone if we own one.
        /// </summary>
        private void DisposeRuntimeEffect()
        {
            if (effect != null && isEffectActive && cachedAudioSource != null)
            {
                effect.OnEffectDisabled(cachedAudioSource);
            }
            isEffectActive = false;

            if (ownsEffectInstance && effect != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(effect);
                }
                else
                {
                    Object.DestroyImmediate(effect);
                }
                ownsEffectInstance = false;
            }

            // Fall back to referencing the template so the slot stays valid.
            effect = templateEffect;
        }
        
        /// <summary>
        /// Get the axis color for visualization
        /// </summary>
        public Color GetAxisColor()
        {
            switch (axis)
            {
                case AxisType.X: return Color.red;
                case AxisType.Y: return Color.green;
                case AxisType.Z: return Color.blue;
                default: return Color.white;
            }
        }
        
        /// <summary>
        /// Get effect color blended with axis color
        /// </summary>
        public Color GetEffectColor()
        {
            if (effect != null)
            {
                return effect.GetEffectColor(smoothedValue);
            }
            return GetAxisColor();
        }
    }
}
