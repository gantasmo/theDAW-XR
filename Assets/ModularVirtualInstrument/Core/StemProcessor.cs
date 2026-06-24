using UnityEngine;
using UnityEngine.Audio;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Processes audio and effects for a single stem.
    /// Manages the AudioSource, three axis effect slots, and interaction bounds.
    /// Note: AudioSource is on a separate child GameObject to allow spatial positioning
    /// independent of the visual cube.
    /// </summary>
    public class StemProcessor : MonoBehaviour
    {
        [Header("Stem Configuration")]
        public StemData stemData;
        public int stemIndex = 0;
        
        [Header("Audio Components")]
        public AudioSource audioSource;
        private GameObject audioSourceObject; // Separate child for spatial audio
        
        [Header("Spatial Audio")]
        [Tooltip("Center point for spatial audio positioning (typically the MVI controller). If null, uses this transform.")]
        public Transform spatialAudioCenter;
        
        [Header("Effect Slots")]
        public AxisEffectSlot xAxis = new AxisEffectSlot { axis = AxisEffectSlot.AxisType.X, axisLabel = "X-Axis" };
        public AxisEffectSlot yAxis = new AxisEffectSlot { axis = AxisEffectSlot.AxisType.Y, axisLabel = "Y-Axis" };
        public AxisEffectSlot zAxis = new AxisEffectSlot { axis = AxisEffectSlot.AxisType.Z, axisLabel = "Z-Axis" };
        
        [Header("Interaction Volume")]
        public Bounds interactionBounds;
        public bool showDebugBounds = true;
        
        [Header("State")]
        public bool isHandInside = false;
        public Vector3 normalizedHandPosition = Vector3.zero;
        
        [Header("Freeze State")]
        [Tooltip("When frozen, effect values won't update from hand position")]
        public bool isFrozen = false;
        private Vector3 frozenPosition = Vector3.zero;
        
        // Events
        public System.Action<StemProcessor> OnHandEntered;
        public System.Action<StemProcessor> OnHandExited;
        public System.Action<StemProcessor, Vector3> OnHandUpdated;
        
        // Internal references
        private StemVisualizer visualizer;
        private float[] audioFilterBuffer;
        private bool isInitialized = false;
        
        private void Awake()
        {
            // Create separate child object for AudioSource to allow spatial positioning
            // independent of the visual cube transform
            if (audioSourceObject == null)
            {
                audioSourceObject = new GameObject("AudioSource");
                audioSourceObject.transform.SetParent(transform);
                audioSourceObject.transform.localPosition = Vector3.zero;
                audioSourceObject.transform.localRotation = Quaternion.identity;
            }
            
            // Get or create audio source on the child object
            if (audioSource == null)
            {
                audioSource = audioSourceObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = audioSourceObject.AddComponent<AudioSource>();
                }
            }
            
            // Get visualizer if present (on parent)
            visualizer = GetComponent<StemVisualizer>();
        }
        
        private void Start()
        {
            if (stemData != null)
            {
                InitializeFromStemData();
            }
        }
        
        /// <summary>
        /// Initialize this processor with stem data
        /// </summary>
        public void InitializeFromStemData()
        {
            if (stemData == null)
            {
                Debug.LogError($"StemProcessor on {gameObject.name} has no StemData assigned!");
                return;
            }
            
            // Setup audio source
            SetupAudioSource();
            
            // Setup effect slots
            SetupEffectSlots();
            
            // Setup interaction bounds
            UpdateInteractionBounds();
            
            // Initialize visualizer if present
            if (visualizer != null)
            {
                visualizer.Initialize(this);
            }
            
            isInitialized = true;
            
            Debug.Log($"StemProcessor initialized: {stemData.stemName}");
        }
        
        private void SetupAudioSource()
        {
            // Ensure audio source child object exists
            if (audioSourceObject == null)
            {
                audioSourceObject = new GameObject("AudioSource");
                audioSourceObject.transform.SetParent(transform);
                audioSourceObject.transform.localPosition = Vector3.zero;
                audioSourceObject.transform.localRotation = Quaternion.identity;
            }
            
            if (audioSource == null)
            {
                audioSource = audioSourceObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = audioSourceObject.AddComponent<AudioSource>();
                }
            }
            
            audioSource.clip = stemData.audioClip;
            audioSource.volume = stemData.defaultVolume;
            audioSource.loop = stemData.loopAudio;
            audioSource.outputAudioMixerGroup = stemData.outputMixerGroup;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio by default (effects can change this)
            
            // Only play audio in play mode
            if (Application.isPlaying && stemData.audioClip != null)
            {
                audioSource.Play();
            }
        }
        
        private void SetupEffectSlots()
        {
            // Setup X axis
            xAxis.axis = AxisEffectSlot.AxisType.X;
            xAxis.SetEffect(stemData.xAxisEffect);
            xAxis.smoothingFactor = stemData.handTrackingSmoothing;
            xAxis.Initialize(audioSource);
            
            // Setup Y axis
            yAxis.axis = AxisEffectSlot.AxisType.Y;
            yAxis.SetEffect(stemData.yAxisEffect);
            yAxis.smoothingFactor = stemData.handTrackingSmoothing;
            yAxis.Initialize(audioSource);
            
            // Setup Z axis
            zAxis.axis = AxisEffectSlot.AxisType.Z;
            zAxis.SetEffect(stemData.zAxisEffect);
            zAxis.smoothingFactor = stemData.handTrackingSmoothing;
            zAxis.Initialize(audioSource);
        }
        
        private void UpdateInteractionBounds()
        {
            interactionBounds = new Bounds(transform.position, stemData.cubeSize);
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            // Update bounds if transform moved
            UpdateInteractionBounds();
            
            // Update effects if hand is inside
            if (isHandInside)
            {
                UpdateEffects(Time.deltaTime);
            }
        }
        
        /// <summary>
        /// Called when a hand enters the interaction volume
        /// </summary>
        public void OnHandEnter(Vector3 worldPosition)
        {
            if (!isInitialized) return;
            
            isHandInside = true;
            normalizedHandPosition = WorldToNormalizedPosition(worldPosition);
            
            OnHandEntered?.Invoke(this);
            
            if (visualizer != null)
            {
                visualizer.OnHandEnter();
            }
        }
        
        /// <summary>
        /// Called when a hand is inside and updates position
        /// </summary>
        public void OnHandUpdate(Vector3 worldPosition)
        {
            if (!isInitialized) return;
            
            Vector3 newPosition = WorldToNormalizedPosition(worldPosition);
            
            // Only update position if not frozen
            if (!isFrozen)
            {
                normalizedHandPosition = newPosition;
            }
            
            OnHandUpdated?.Invoke(this, normalizedHandPosition);
            
            if (visualizer != null)
            {
                visualizer.OnHandUpdate(normalizedHandPosition);
            }
        }
        
        /// <summary>
        /// Called when a hand exits the interaction volume
        /// </summary>
        public void OnHandExit()
        {
            if (!isInitialized) return;
            
            isHandInside = false;
            
            OnHandExited?.Invoke(this);
            
            if (visualizer != null)
            {
                visualizer.OnHandExit();
            }
        }
        
        /// <summary>
        /// Convert world position to normalized position within the cube (0-1 range)
        /// </summary>
        private Vector3 WorldToNormalizedPosition(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector3 halfSize = stemData.cubeSize * 0.5f;
            
            float x = Mathf.Clamp01((localPosition.x + halfSize.x) / stemData.cubeSize.x);
            float y = Mathf.Clamp01((localPosition.y + halfSize.y) / stemData.cubeSize.y);
            float z = Mathf.Clamp01((localPosition.z + halfSize.z) / stemData.cubeSize.z);
            
            return new Vector3(x, y, z);
        }
        
        /// <summary>
        /// Update all effect slots
        /// </summary>
        private void UpdateEffects(float deltaTime)
        {
            xAxis.UpdateEffect(normalizedHandPosition.x, deltaTime);
            yAxis.UpdateEffect(normalizedHandPosition.y, deltaTime);
            zAxis.UpdateEffect(normalizedHandPosition.z, deltaTime);
            
            // Update spatial position if any effect is SpatialPositionEffect
            UpdateSpatialEffects();
        }
        
        /// <summary>
        /// Update spatial position effects with hand position
        /// </summary>
        private void UpdateSpatialEffects()
        {
            // Use spatialAudioCenter if set, otherwise use this transform
            Transform centerTransform = spatialAudioCenter != null ? spatialAudioCenter : transform;
            
            // Check each axis for spatial effects
            if (xAxis.effect is SpatialPositionEffect spatialX)
            {
                spatialX.UpdatePosition(audioSource, normalizedHandPosition, centerTransform);
            }
            
            if (yAxis.effect is SpatialPositionEffect spatialY)
            {
                spatialY.UpdatePosition(audioSource, normalizedHandPosition, centerTransform);
            }
            
            if (zAxis.effect is SpatialPositionEffect spatialZ)
            {
                spatialZ.UpdatePosition(audioSource, normalizedHandPosition, centerTransform);
            }
        }
        
        /// <summary>
        /// Process audio filters for effects that use OnAudioFilterRead
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!isInitialized || !isHandInside) return;
            
            // Allow effects to process audio if they need DSP
            if (xAxis.effect != null)
                xAxis.effect.ProcessAudioFilter(data, channels, xAxis.smoothedValue);
            
            if (yAxis.effect != null)
                yAxis.effect.ProcessAudioFilter(data, channels, yAxis.smoothedValue);
            
            if (zAxis.effect != null)
                zAxis.effect.ProcessAudioFilter(data, channels, zAxis.smoothedValue);
        }
        
        private void OnDestroy()
        {
            // Cleanup effect slots
            xAxis?.Cleanup();
            yAxis?.Cleanup();
            zAxis?.Cleanup();
        }
        
        /// <summary>
        /// Freeze effect values at current position
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            
            if (frozen)
            {
                // Store current position when freezing
                frozenPosition = normalizedHandPosition;
            }
            
            // Update visualizer if present
            if (visualizer != null)
            {
                visualizer.SetFrozen(frozen);
            }
        }
        
        /// <summary>
        /// Check if effects are currently frozen
        /// </summary>
        public bool IsFrozen()
        {
            return isFrozen;
        }
        
        /// <summary>
        /// Get the frozen position values
        /// </summary>
        public Vector3 GetFrozenPosition()
        {
            return frozenPosition;
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugBounds) return;
            
            // Draw interaction bounds
            Gizmos.color = isHandInside ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(interactionBounds.center, interactionBounds.size);
            
            // Draw hand position if inside
            if (isHandInside)
            {
                Vector3 worldHandPos = transform.TransformPoint(
                    new Vector3(
                        (normalizedHandPosition.x - 0.5f) * stemData.cubeSize.x,
                        (normalizedHandPosition.y - 0.5f) * stemData.cubeSize.y,
                        (normalizedHandPosition.z - 0.5f) * stemData.cubeSize.z
                    )
                );
                
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(worldHandPos, 0.02f);
            }
        }
    }
}
