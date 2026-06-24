using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Flanger effect for sweeping phase modulation (EDM staple).
    /// Creates that classic "jet plane" whooshing sound.
    /// </summary>
    [CreateAssetMenu(fileName = "FlangerEffect", menuName = "Modular Virtual Instrument/Effects/Flanger")]
    public class FlangerEffect : AxisEffect
    {
        [Header("Flanger Settings")]
        [Tooltip("Delay time in milliseconds")]
        [Range(0f, 20f)]
        public float minDelay = 0.5f;
        
        [Range(0f, 20f)]
        public float maxDelay = 10f;
        
        [Tooltip("Modulation rate (LFO speed)")]
        [Range(0.1f, 10f)]
        public float lfoRate = 0.5f;
        
        [Tooltip("Depth of modulation")]
        [Range(0f, 1f)]
        public float lfoDepth = 0.7f;
        
        [Tooltip("Feedback amount")]
        [Range(0f, 0.95f)]
        public float feedback = 0.5f;
        
        [Tooltip("Wet/dry mix")]
        [Range(0f, 1f)]
        public float wetMix = 0.5f;
        
        private float[] delayBuffer;
        private int bufferSize = 44100; // 1 second at 44.1kHz
        private int writeIndex = 0;
        private float lfoPhase = 0f;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            // Initialize delay buffer
            delayBuffer = new float[bufferSize];
            writeIndex = 0;
            lfoPhase = 0f;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            delayBuffer = null;
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            // Effect is processed in ProcessAudioFilter
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            if (delayBuffer == null) return;
            
            // Calculate current delay time based on control value
            float currentDelay = Mathf.Lerp(minDelay, maxDelay, value);
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Update LFO
                lfoPhase += lfoRate * 0.0001f;
                if (lfoPhase > 1f) lfoPhase -= 1f;
                
                // Calculate modulated delay
                float lfoValue = Mathf.Sin(lfoPhase * Mathf.PI * 2f);
                float modulatedDelay = currentDelay + (lfoValue * lfoDepth * currentDelay);
                
                // Convert delay time to samples
                int delaySamples = (int)(modulatedDelay * 44.1f); // Assuming 44.1kHz
                delaySamples = Mathf.Clamp(delaySamples, 1, bufferSize - 1);
                
                // Calculate read position
                int readIndex = writeIndex - delaySamples;
                if (readIndex < 0) readIndex += bufferSize;
                
                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i + ch;
                    
                    // Read delayed sample
                    float delayedSample = delayBuffer[readIndex];
                    
                    // Mix dry and wet with feedback
                    float outputSample = data[idx] + (delayedSample * wetMix * value);
                    
                    // Write to delay buffer with feedback
                    delayBuffer[writeIndex] = data[idx] + (delayedSample * feedback);
                    
                    // Output mixed signal
                    data[idx] = outputSample;
                }
                
                // Increment write index
                writeIndex++;
                if (writeIndex >= bufferSize) writeIndex = 0;
            }
        }
        
        public override Color GetEffectColor(float value)
        {
            // Cyan to magenta sweep
            return Color.Lerp(Color.cyan, Color.magenta, value);
        }
    }
}
