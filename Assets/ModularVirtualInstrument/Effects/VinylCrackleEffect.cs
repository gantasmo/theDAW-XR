using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Vinyl crackle/noise effect for lo-fi EDM aesthetic.
    /// Simple but effective for texture.
    /// </summary>
    [CreateAssetMenu(fileName = "VinylCrackleEffect", menuName = "Modular Virtual Instrument/Effects/Vinyl Crackle")]
    public class VinylCrackleEffect : AxisEffect
    {
        [Header("Crackle Settings")]
        [Tooltip("Noise amplitude")]
        [Range(0f, 0.2f)]
        public float noiseAmount = 0.05f;
        
        [Tooltip("Crackle frequency")]
        [Range(0f, 1f)]
        public float crackleRate = 0.3f;
        
        [Tooltip("Add low-pass filtering")]
        public bool enableFiltering = true;
        
        [Range(500f, 10000f)]
        public float cutoffFrequency = 4000f;
        
        private System.Random random;
        private float lastSample = 0f;
        private AudioLowPassFilter lowPassFilter;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            random = new System.Random();
            lastSample = 0f;
            
            if (enableFiltering)
            {
                lowPassFilter = audioSource.GetComponent<AudioLowPassFilter>();
                if (lowPassFilter == null)
                {
                    lowPassFilter = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                }
                lowPassFilter.cutoffFrequency = cutoffFrequency;
            }
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            if (lowPassFilter != null)
            {
                Object.Destroy(lowPassFilter);
            }
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            if (enableFiltering && lowPassFilter != null)
            {
                // Reduce cutoff as effect increases
                lowPassFilter.cutoffFrequency = Mathf.Lerp(22000f, cutoffFrequency, normalizedValue);
            }
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            if (value < 0.01f) return;
            
            float currentNoiseAmount = noiseAmount * value;
            
            for (int i = 0; i < data.Length; i++)
            {
                // Add noise
                float noise = ((float)random.NextDouble() * 2f - 1f) * currentNoiseAmount;
                
                // Occasional crackles (sharp spikes)
                if (random.NextDouble() < crackleRate * value * 0.001f)
                {
                    noise += ((float)random.NextDouble() * 2f - 1f) * currentNoiseAmount * 3f;
                }
                
                // Smooth slightly for vinyl warmth
                float smoothedNoise = Mathf.Lerp(lastSample, noise, 0.7f);
                lastSample = smoothedNoise;
                
                // Add to signal
                data[i] += smoothedNoise;
            }
        }
        
        public override Color GetEffectColor(float value)
        {
            // Sepia to brown
            return Color.Lerp(new Color(0.9f, 0.8f, 0.6f), new Color(0.4f, 0.3f, 0.2f), value);
        }
    }
}
