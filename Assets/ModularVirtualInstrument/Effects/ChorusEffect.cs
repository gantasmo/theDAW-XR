using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Chorus effect for thickening sounds (EDM pads and synths).
    /// Creates multiple delayed copies with pitch modulation.
    /// </summary>
    [CreateAssetMenu(fileName = "ChorusEffect", menuName = "Modular Virtual Instrument/Effects/Chorus")]
    public class ChorusEffect : AxisEffect
    {
        [Header("Chorus Settings")]
        [Tooltip("Base delay time in milliseconds")]
        [Range(5f, 50f)]
        public float baseDelay = 20f;
        
        [Tooltip("LFO rate for modulation")]
        [Range(0.1f, 5f)]
        public float lfoRate = 1.5f;
        
        [Tooltip("Depth of delay modulation")]
        [Range(0f, 1f)]
        public float minDepth = 0.1f;
        
        [Range(0f, 1f)]
        public float maxDepth = 0.8f;
        
        [Tooltip("Number of voices")]
        [Range(1, 4)]
        public int voiceCount = 3;
        
        [Tooltip("Wet/dry mix")]
        [Range(0f, 1f)]
        public float wetMix = 0.5f;
        
        private float[] delayBuffer;
        private int bufferSize = 88200; // 2 seconds at 44.1kHz
        private int writeIndex = 0;
        private float[] lfoPhases;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            delayBuffer = new float[bufferSize];
            writeIndex = 0;
            
            // Initialize LFO phases for each voice (slightly offset)
            lfoPhases = new float[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                lfoPhases[i] = (float)i / voiceCount;
            }
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            delayBuffer = null;
            lfoPhases = null;
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            // Effect is processed in ProcessAudioFilter
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            if (delayBuffer == null) return;
            
            float currentDepth = Mathf.Lerp(minDepth, maxDepth, value);
            
            for (int i = 0; i < data.Length; i += channels)
            {
                float wetSignal = 0f;
                
                // Generate chorus voices
                for (int v = 0; v < voiceCount; v++)
                {
                    // Update LFO for this voice
                    lfoPhases[v] += lfoRate * 0.0001f;
                    if (lfoPhases[v] > 1f) lfoPhases[v] -= 1f;
                    
                    // Calculate modulated delay
                    float lfoValue = Mathf.Sin(lfoPhases[v] * Mathf.PI * 2f);
                    float modulatedDelay = baseDelay + (lfoValue * currentDepth * baseDelay);
                    
                    // Convert to samples
                    int delaySamples = (int)(modulatedDelay * 44.1f); // 44.1kHz
                    delaySamples = Mathf.Clamp(delaySamples, 1, bufferSize - 1);
                    
                    // Read delayed sample
                    int readIndex = writeIndex - delaySamples;
                    if (readIndex < 0) readIndex += bufferSize;
                    
                    wetSignal += delayBuffer[readIndex];
                }
                
                // Average the voices
                wetSignal /= voiceCount;
                
                // Process each channel
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = i + ch;
                    
                    // Write to delay buffer
                    delayBuffer[writeIndex] = data[idx];
                    
                    // Mix dry and wet
                    data[idx] = Mathf.Lerp(data[idx], data[idx] + wetSignal, wetMix * value);
                }
                
                // Increment write index
                writeIndex++;
                if (writeIndex >= bufferSize) writeIndex = 0;
            }
        }
        
        public override Color GetEffectColor(float value)
        {
            // Blue to pink gradient
            return Color.Lerp(new Color(0f, 0.5f, 1f), new Color(1f, 0.4f, 0.8f), value);
        }
    }
}
