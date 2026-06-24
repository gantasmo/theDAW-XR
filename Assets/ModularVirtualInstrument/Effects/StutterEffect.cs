using UnityEngine;
using System.Collections;

namespace ModularVirtualInstrument.Effects
{
    /// <summary>
    /// Stutter effect - rhythmic muting/gating of audio.
    /// Ported from Synth69.AudioEffectsManager stutter logic.
    /// Creates a DJ-style stutter/gating effect.
    /// </summary>
    [CreateAssetMenu(fileName = "StutterEffect", menuName = "Modular Virtual Instrument/Effects/Stutter Effect")]
    public class StutterEffect : AxisEffect
    {
        [Header("Stutter Settings")]
        [Tooltip("Minimum stutter interval (seconds) - faster stuttering")]
        [Range(0.02f, 0.5f)]
        public float minInterval = 0.02f;
        
        [Tooltip("Maximum stutter interval (seconds) - slower stuttering")]
        [Range(0.02f, 1f)]
        public float maxInterval = 0.5f;
        
        [Tooltip("Threshold below which stutter is disabled")]
        [Range(0f, 0.5f)]
        public float disableThreshold = 0.1f;
        
        [Header("Pattern Settings")]
        [Tooltip("Use rhythmic patterns instead of simple on/off")]
        public bool useRhythmicPattern = true;
        
        [Tooltip("Pattern: 1 = play, 0 = mute")]
        public int[] rhythmPattern = new int[] { 1, 1, 0, 1 };
        
        [Header("Volume Fade")]
        [Tooltip("Fade volume instead of hard mute")]
        public bool useFade = true;
        
        [Tooltip("Fade time in seconds")]
        [Range(0.001f, 0.1f)]
        public float fadeTime = 0.01f;
        
        // Internal state
        private bool isStutterActive = false;
        private float currentInterval = 0.5f;
        private float stutterTimer = 0f;
        private bool isMuted = false;
        private int patternIndex = 0;
        private float originalVolume = 1f;
        private float targetVolume = 1f;
        private float currentVolume = 1f;
        
        // Reference to audio source (stored to handle coroutines if needed)
        private AudioSource cachedAudioSource;
        private MonoBehaviour coroutineRunner;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            cachedAudioSource = audioSource;
            originalVolume = audioSource.volume;
            currentVolume = originalVolume;
            targetVolume = originalVolume;
            isStutterActive = false;
            isMuted = false;
            stutterTimer = 0f;
            patternIndex = 0;
            
            // Ensure audio is playing
            audioSource.mute = false;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (audioSource == null) return;
            
            // Restore original state
            audioSource.mute = false;
            audioSource.volume = originalVolume;
            isStutterActive = false;
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (audioSource == null) return;
            
            // Map value to stutter interval
            float mappedValue = GetMappedValue(normalizedValue);
            
            // Check if stutter should be active
            if (normalizedValue < disableThreshold)
            {
                // Disable stutter
                if (isStutterActive)
                {
                    DisableStutter(audioSource);
                }
                return;
            }
            
            // Enable stutter if not active
            if (!isStutterActive)
            {
                EnableStutter(audioSource);
            }
            
            // Update interval based on value (inverse - higher value = faster stutter)
            currentInterval = Mathf.Lerp(maxInterval, minInterval, mappedValue);
            
            // Update stutter timing
            UpdateStutter(audioSource, deltaTime);
        }
        
        private void EnableStutter(AudioSource audioSource)
        {
            isStutterActive = true;
            stutterTimer = 0f;
            patternIndex = 0;
            originalVolume = audioSource.volume;
        }
        
        private void DisableStutter(AudioSource audioSource)
        {
            isStutterActive = false;
            audioSource.mute = false;
            audioSource.volume = originalVolume;
            isMuted = false;
        }
        
        private void UpdateStutter(AudioSource audioSource, float deltaTime)
        {
            stutterTimer += deltaTime;
            
            if (stutterTimer >= currentInterval)
            {
                stutterTimer = 0f;
                ToggleStutter(audioSource);
            }
            
            // Update volume fade if enabled
            if (useFade && audioSource.volume != targetVolume)
            {
                float fadeSpeed = 1f / fadeTime;
                currentVolume = Mathf.MoveTowards(currentVolume, targetVolume, fadeSpeed * deltaTime);
                audioSource.volume = currentVolume;
            }
        }
        
        private void ToggleStutter(AudioSource audioSource)
        {
            if (useRhythmicPattern && rhythmPattern != null && rhythmPattern.Length > 0)
            {
                // Use pattern
                int patternValue = rhythmPattern[patternIndex];
                patternIndex = (patternIndex + 1) % rhythmPattern.Length;
                
                SetStutterState(audioSource, patternValue == 1);
            }
            else
            {
                // Simple toggle
                isMuted = !isMuted;
                SetStutterState(audioSource, !isMuted);
            }
        }
        
        private void SetStutterState(AudioSource audioSource, bool shouldPlay)
        {
            if (useFade)
            {
                // Fade volume
                targetVolume = shouldPlay ? originalVolume : 0f;
            }
            else
            {
                // Hard mute
                audioSource.mute = !shouldPlay;
            }
        }
        
        public override Color GetEffectColor(float normalizedValue)
        {
            if (effectGradient != null && effectGradient.colorKeys.Length > 0)
            {
                return effectGradient.Evaluate(normalizedValue);
            }
            
            // Pulse between two colors based on stutter state
            Color baseColor = new Color(0.5f, 0f, 1f); // Purple
            Color activeColor = new Color(1f, 1f, 0f); // Yellow
            
            if (isStutterActive && isMuted)
            {
                return Color.Lerp(baseColor, activeColor, Mathf.PingPong(Time.time * 10f, 1f));
            }
            
            return Color.Lerp(Color.white, baseColor, normalizedValue);
        }
    }
}
