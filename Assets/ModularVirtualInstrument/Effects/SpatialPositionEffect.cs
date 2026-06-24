using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Spatializes audio by moving the audio source in 3D space based on hand position.
    /// Creates dramatic spatial movement that's highly noticeable.
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialPositionEffect", menuName = "Modular Virtual Instrument/Effects/Spatial Position")]
    public class SpatialPositionEffect : AxisEffect
    {
        [Header("Spatialization Settings")]
        [Tooltip("Always fully 3D spatialized for maximum spatial effect")]
        [Range(0f, 1f)]
        public float maxSpatialBlend = 1f;
        
        [Tooltip("Closest distance before audio reaches maximum volume (in meters) - closer = louder")]
        public float minDistance = 0.5f;
        
        [Tooltip("Distance at which audio fades to silence (in meters) - controls how far sound travels")]
        public float maxDistance = 15f;
        
        [Tooltip("Strength of pitch shift when moving toward/away from listener - higher values = more dramatic frequency change as you move")]
        [Range(0f, 5f)]
        public float dopplerLevel = 2f;
        
        [Tooltip("Angular spread of sound in degrees - 0° is directional (like a laser), 360° is omnidirectional (surrounds you)")]
        [Range(0f, 360f)]
        public float spread = 180f;
        
        [Header("Movement Range")]
        [Tooltip("How far the audio source moves from center in meters - larger values = more dramatic spatial movement")]
        [Range(0.5f, 10f)]
        public float movementRadius = 3f;
        
        // Snapshot of every AudioSource value this effect overwrites, captured on enable
        // so disable can fully restore the source to its prior state.
        private Vector3 originalPosition;
        private float originalSpatialBlend;
        private float originalMinDistance;
        private float originalMaxDistance;
        private float originalDopplerLevel;
        private float originalSpread;
        private AudioRolloffMode originalRolloffMode;

        public override void OnEffectEnabled(AudioSource audioSource)
        {
            if (audioSource == null) return;

            // Capture everything we are about to change.
            originalPosition = audioSource.transform.position;
            originalSpatialBlend = audioSource.spatialBlend;
            originalMinDistance = audioSource.minDistance;
            originalMaxDistance = audioSource.maxDistance;
            originalDopplerLevel = audioSource.dopplerLevel;
            originalSpread = audioSource.spread;
            originalRolloffMode = audioSource.rolloffMode;

            // Configure for FULL 3D spatialization
            audioSource.spatialBlend = maxSpatialBlend;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.dopplerLevel = dopplerLevel;
            audioSource.spread = spread;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (audioSource == null) return;

            // Fully restore the source to its pre-effect state.
            audioSource.spatialBlend = originalSpatialBlend;
            audioSource.minDistance = originalMinDistance;
            audioSource.maxDistance = originalMaxDistance;
            audioSource.dopplerLevel = originalDopplerLevel;
            audioSource.spread = originalSpread;
            audioSource.rolloffMode = originalRolloffMode;
            audioSource.transform.position = originalPosition;
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (audioSource == null) return;
            
            // Map normalized value to a position offset using response curve
            float mappedValue = GetMappedValue(normalizedValue);
            
            // This is typically called for a single axis, but we'll use it to control distance
            // The actual 3D position is set via UpdatePosition method
            // Here we just ensure spatialization stays enabled
            audioSource.spatialBlend = maxSpatialBlend;
        }
        
        /// <summary>
        /// Updates the 3D position of the audio source based on hand position within the cube.
        /// This creates dramatic spatial movement.
        /// </summary>
        public void UpdatePosition(AudioSource audioSource, Vector3 normalizedHandPos, Transform cubeTransform)
        {
            if (audioSource == null || cubeTransform == null) return;
            
            // Convert normalized position (0-1) to centered position (-0.5 to 0.5)
            Vector3 centeredPos = normalizedHandPos - new Vector3(0.5f, 0.5f, 0.5f);
            
            // Scale by movement radius and apply to world space
            Vector3 offset = centeredPos * movementRadius;
            
            // Position audio source in world space relative to cube center
            audioSource.transform.position = cubeTransform.position + offset;
        }
        
        public override Color GetEffectColor(float value)
        {
            // Gradient from purple (center) to cyan (edges) to show spatial position
            return Color.Lerp(new Color(0.6f, 0.2f, 1f), Color.cyan, value);
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            // Spatialization is handled by Unity's built-in 3D audio system
            // No custom DSP needed here
        }
    }
}
