using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Dynamic range compressor for EDM loudness and punch.
    /// Essential for making tracks sound full and tight.
    /// </summary>
    [CreateAssetMenu(fileName = "CompressorEffect", menuName = "Modular Virtual Instrument/Effects/Compressor")]
    public class CompressorEffect : AxisEffect
    {
        [Header("Compressor Settings")]
        [Tooltip("Threshold in dB")]
        [Range(-60f, 0f)]
        public float minThreshold = -20f;
        
        [Range(-60f, 0f)]
        public float maxThreshold = -5f;
        
        [Tooltip("Compression ratio")]
        [Range(1f, 20f)]
        public float ratio = 4f;
        
        [Tooltip("Attack time in seconds")]
        [Range(0.0001f, 1f)]
        public float attack = 0.005f;
        
        [Tooltip("Release time in seconds")]
        [Range(0.01f, 3f)]
        public float release = 0.1f;
        
        [Tooltip("Make-up gain in dB")]
        [Range(0f, 24f)]
        public float makeupGain = 6f;
        
        private float envelope = 0f;
        
        public override void OnEffectEnabled(AudioSource audioSource)
        {
            envelope = 0f;
        }
        
        public override void OnEffectDisabled(AudioSource audioSource)
        {
            // Nothing to clean up
        }
        
        public override void ProcessEffect(float normalizedValue, AudioSource audioSource, float deltaTime)
        {
            // Effect is processed in ProcessAudioFilter
        }
        
        public override void ProcessAudioFilter(float[] data, int channels, float value)
        {
            if (value < 0.01f) return;
            
            float threshold = Mathf.Lerp(minThreshold, maxThreshold, value);
            float thresholdLinear = DbToLinear(threshold);
            float makeupLinear = DbToLinear(makeupGain * value);
            
            float attackCoeff = Mathf.Exp(-1f / (attack * AudioSettings.outputSampleRate));
            float releaseCoeff = Mathf.Exp(-1f / (release * AudioSettings.outputSampleRate));
            
            for (int i = 0; i < data.Length; i += channels)
            {
                // Get peak amplitude across channels
                float peak = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    float sample = Mathf.Abs(data[i + ch]);
                    if (sample > peak) peak = sample;
                }
                
                // Envelope follower
                if (peak > envelope)
                    envelope = attackCoeff * envelope + (1f - attackCoeff) * peak;
                else
                    envelope = releaseCoeff * envelope + (1f - releaseCoeff) * peak;
                
                // Calculate gain reduction
                float gainReduction = 1f;
                if (envelope > thresholdLinear)
                {
                    float overThreshold = envelope / thresholdLinear;
                    gainReduction = Mathf.Pow(overThreshold, (1f / ratio) - 1f);
                }
                
                // Apply compression with makeup gain
                float totalGain = gainReduction * makeupLinear;
                
                for (int ch = 0; ch < channels; ch++)
                {
                    data[i + ch] *= totalGain;
                }
            }
        }
        
        private float DbToLinear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }
        
        public override Color GetEffectColor(float value)
        {
            // Green to red (louder)
            return Color.Lerp(Color.green, Color.red, value);
        }
    }
}
