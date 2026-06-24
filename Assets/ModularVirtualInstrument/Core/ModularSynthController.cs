using UnityEngine;
using System.Collections.Generic;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Defines how stems should be generated at runtime
    /// </summary>
    public enum StemGenerationMode
    {
        UseExistingOrGenerate,  // Use existing child stems if found, otherwise generate new ones
        AlwaysGenerate,         // Always clear and regenerate stems from StemData array
        UseExistingOnly         // Only use existing children, don't generate (for prefabs)
    }
    
    /// <summary>
    /// Main controller for the Modular Virtual Instrument system.
    /// Manages multiple stems, their processors, and spatial layout.
    /// </summary>
    [ExecuteInEditMode]
    public class ModularSynthController : MonoBehaviour
    {
        [Header("Stem Configuration")]
        [Tooltip("Array of stem data to load. Can be modified at runtime.")]
        public StemData[] stems = new StemData[0];
        
        [Header("Layout")]
        [Tooltip("Strategy for positioning stem cubes in space")]
        public StemLayoutStrategy layoutStrategy;
        
        [Tooltip("Center point for layout positioning")]
        public Vector3 layoutCenter = Vector3.zero;
        
        [Tooltip("Use local space for layout (relative to this transform)")]
        public bool useLocalSpace = true;
        
        [Header("Stem Processor")]
        [Tooltip("Prefab for stem processors. Leave null to create empty GameObjects.")]
        public GameObject stemProcessorPrefab;
        
        [Header("Runtime Behavior")]
        [Tooltip("How stems should be handled at runtime")]
        public StemGenerationMode generationMode = StemGenerationMode.UseExistingOrGenerate;
        
        [Tooltip("Automatically initialize and generate layout on Start")]
        public bool autoInitialize = true;
        
        [Header("Edit Mode Preview")]
        [Tooltip("Generate stem visuals in edit mode for prefab creation")]
        public bool editModePreview = false;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        public bool showLayoutGizmos = true;
        
        [Header("Visualization")]
        [Tooltip("Default wireframe visibility for newly created stems")]
        public bool defaultWireframeVisibility = false;
        
        // Runtime state
        private List<StemProcessor> activeProcessors = new List<StemProcessor>();
        private bool isInitialized = false;
        
        /// <summary>
        /// Whether the system has been initialized
        /// </summary>
        public bool IsInitialized => isInitialized;
        
        // Events
        public System.Action<int> OnStemsLoaded;
        public System.Action OnLayoutRegenerated;
        
        private void Start()
        {
            // Only auto-initialize in play mode
            if (Application.isPlaying && autoInitialize)
            {
                InitializeSystem();
            }
        }
        
        /// <summary>
        /// Initialize the system and load stems
        /// </summary>
        public void InitializeSystem()
        {
            // Check for existing stems first
            StemProcessor[] existingProcessors = GetComponentsInChildren<StemProcessor>(true);
            
            switch (generationMode)
            {
                case StemGenerationMode.UseExistingOnly:
                    if (existingProcessors.Length > 0)
                    {
                        Debug.Log($"[MVI] Using {existingProcessors.Length} existing stem processors (UseExistingOnly mode)");
                        InitializeExistingStems(existingProcessors);
                        isInitialized = true;
                        return;
                    }
                    else
                    {
                        Debug.LogWarning("[MVI] UseExistingOnly mode but no existing stems found!");
                        return;
                    }
                    
                case StemGenerationMode.UseExistingOrGenerate:
                    if (existingProcessors.Length > 0)
                    {
                        Debug.Log($"[MVI] Using {existingProcessors.Length} existing stem processors");
                        InitializeExistingStems(existingProcessors);
                        isInitialized = true;
                        return;
                    }
                    // Fall through to generation
                    break;
                    
                case StemGenerationMode.AlwaysGenerate:
                    Debug.Log("[MVI] AlwaysGenerate mode - clearing existing stems");
                    break;
            }
            
            // Generate new stems
            if (stems == null || stems.Length == 0)
            {
                Debug.LogWarning("[MVI] ModularSynthController: No stems assigned! Please assign StemData assets in the inspector.");
                return;
            }
            
            if (layoutStrategy == null)
            {
                Debug.LogError("[MVI] ModularSynthController: No layout strategy assigned! Please assign a StemLayoutStrategy.");
                return;
            }
            
            Debug.Log($"[MVI] Initializing system with {stems.Length} stems...");
            LoadStems(stems);
            isInitialized = true;
        }
        
        /// <summary>
        /// Initialize existing stem processors without regenerating
        /// </summary>
        private void InitializeExistingStems(StemProcessor[] processors)
        {
            ClearProcessorList();
            
            for (int i = 0; i < processors.Length; i++)
            {
                StemProcessor processor = processors[i];
                
                // Ensure it has a stem index
                if (processor.stemIndex < 0)
                {
                    processor.stemIndex = i;
                }
                
                // Set spatial audio center to MVI controller
                processor.spatialAudioCenter = transform;
                
                // Initialize if it has stemData
                if (processor.stemData != null)
                {
                    processor.InitializeFromStemData();
                }
                else
                {
                    Debug.LogWarning($"[MVI] Stem processor '{processor.name}' has no StemData assigned!");
                }
                
                activeProcessors.Add(processor);
            }
            
            Debug.Log($"[MVI] Initialized {activeProcessors.Count} existing stems");
            OnStemsLoaded?.Invoke(activeProcessors.Count);
        }
        
        /// <summary>
        /// Load an array of stems and create processors for them
        /// </summary>
        public void LoadStems(StemData[] newStems)
        {
            if (newStems == null || newStems.Length == 0)
            {
                Debug.LogWarning("LoadStems called with null or empty array");
                return;
            }
            
            // Clear existing processors
            ClearAllProcessors();
            
            stems = newStems;
            
            // Calculate positions using layout strategy
            Vector3 center = useLocalSpace ? transform.position + layoutCenter : layoutCenter;
            Vector3[] positions = layoutStrategy.CalculatePositions(stems.Length, center);
            Quaternion[] rotations = layoutStrategy.CalculateRotations(stems.Length, center);
            
            // Create processor for each stem
            for (int i = 0; i < stems.Length; i++)
            {
                if (stems[i] == null)
                {
                    Debug.LogWarning($"Stem at index {i} is null, skipping");
                    continue;
                }
                
                CreateStemProcessor(stems[i], i, positions[i], rotations[i]);
            }
            
            if (Application.isPlaying)
            {
                Debug.Log($"[MVI] Loaded {activeProcessors.Count} stem processors with wireframe visualization");
            }
            OnStemsLoaded?.Invoke(activeProcessors.Count);
        }
        
        /// <summary>
        /// Create a single stem processor
        /// </summary>
        private void CreateStemProcessor(StemData stemData, int index, Vector3 position, Quaternion rotation)
        {
            GameObject processorObj;
            
            // Create from prefab or empty GameObject
            if (stemProcessorPrefab != null)
            {
                processorObj = Instantiate(stemProcessorPrefab, position, rotation, transform);
            }
            else
            {
                processorObj = new GameObject($"Stem_{index}_{stemData.stemName}");
                processorObj.transform.SetParent(transform);
                processorObj.transform.position = position;
                processorObj.transform.rotation = rotation;
            }
            
            // Add or get StemProcessor component
            StemProcessor processor = processorObj.GetComponent<StemProcessor>();
            if (processor == null)
            {
                processor = processorObj.AddComponent<StemProcessor>();
            }
            
            // Add StemVisualizer component for wireframe rendering
            StemVisualizer visualizer = processorObj.GetComponent<StemVisualizer>();
            if (visualizer == null)
            {
                visualizer = processorObj.AddComponent<StemVisualizer>();
            }
            
            // Apply default wireframe visibility setting
            visualizer.showWireframe = defaultWireframeVisibility;
            visualizer.SetWireframeVisible(defaultWireframeVisibility);
            
            // Configure processor
            processor.stemData = stemData;
            processor.stemIndex = index;
            processor.spatialAudioCenter = transform; // Set MVI controller as spatial audio center
            processor.InitializeFromStemData();
            
            // Apply stem-specific rotation if specified
            if (stemData.preferredRotation != Vector3.zero)
            {
                processorObj.transform.Rotate(stemData.preferredRotation);
            }
            
            activeProcessors.Add(processor);
        }
        
        /// <summary>
        /// Force apply wireframe visibility to all existing stems
        /// </summary>
        [ContextMenu("Apply Wireframe Settings to Existing Stems")]
        public void ApplyWireframeSettingsToExistingStems()
        {
            StemVisualizer[] allVisualizers = GetComponentsInChildren<StemVisualizer>();
            
            Debug.Log($"[MVI] Applying wireframe visibility ({defaultWireframeVisibility}) to {allVisualizers.Length} existing stems");
            
            foreach (var visualizer in allVisualizers)
            {
                if (visualizer != null)
                {
                    visualizer.showWireframe = defaultWireframeVisibility;
                    visualizer.SetWireframeVisible(defaultWireframeVisibility);
                    Debug.Log($"Updated wireframe visibility for {visualizer.name}: {defaultWireframeVisibility}");
                }
            }
        }

        /// <summary>
        /// Emergency method to force hide ALL wireframes immediately
        /// </summary>
        [ContextMenu("FORCE HIDE ALL WIREFRAMES")]
        public void ForceHideAllWireframes()
        {
            StemVisualizer[] allVisualizers = GetComponentsInChildren<StemVisualizer>();
            defaultWireframeVisibility = false;
            
            Debug.Log($"[MVI] FORCE HIDING all {allVisualizers.Length} wireframes");
            
            foreach (var visualizer in allVisualizers)
            {
                if (visualizer != null)
                {
                    visualizer.showWireframe = false;
                    visualizer.SetWireframeVisible(false);
                }
            }
        }

        /// <summary>
        /// Nuclear option: Completely recreate all wireframe LineRenderers
        /// </summary>
        [ContextMenu("NUCLEAR: Recreate All Wireframes")]
        public void RecreateAllWireframes()
        {
            StemVisualizer[] allVisualizers = GetComponentsInChildren<StemVisualizer>();
            defaultWireframeVisibility = false;
            
            Debug.Log($"[MVI] RECREATING all wireframes for {allVisualizers.Length} stems");
            
            foreach (var visualizer in allVisualizers)
            {
                if (visualizer != null && visualizer.stemProcessor != null)
                {
                    // Force recreate by re-initializing the entire visualizer
                    visualizer.Initialize(visualizer.stemProcessor);
                    visualizer.showWireframe = false;
                    visualizer.SetWireframeVisible(false);
                }
            }
        }

        /// <summary>
        /// Add a new stem at runtime
        /// </summary>
        public void AddStem(StemData stemData)
        {
            if (stemData == null)
            {
                Debug.LogWarning("Cannot add null stem");
                return;
            }
            
            // Add to stems array
            StemData[] newStems = new StemData[stems.Length + 1];
            stems.CopyTo(newStems, 0);
            newStems[stems.Length] = stemData;
            
            // Reload all stems with new layout
            LoadStems(newStems);
        }
        
        /// <summary>
        /// Remove a stem at runtime
        /// </summary>
        public void RemoveStem(int index)
        {
            if (index < 0 || index >= stems.Length)
            {
                Debug.LogWarning($"Invalid stem index: {index}");
                return;
            }
            
            // Remove from array
            List<StemData> stemList = new List<StemData>(stems);
            stemList.RemoveAt(index);
            
            // Reload remaining stems
            LoadStems(stemList.ToArray());
        }
        
        /// <summary>
        /// Regenerate layout without reloading stems
        /// </summary>
        public void RegenerateLayout()
        {
            if (stems == null || stems.Length == 0 || layoutStrategy == null)
            {
                return;
            }
            
            Vector3 center = useLocalSpace ? transform.position + layoutCenter : layoutCenter;
            Vector3[] positions = layoutStrategy.CalculatePositions(stems.Length, center);
            Quaternion[] rotations = layoutStrategy.CalculateRotations(stems.Length, center);
            
            // Update existing processors
            for (int i = 0; i < activeProcessors.Count && i < positions.Length; i++)
            {
                if (activeProcessors[i] != null)
                {
                    activeProcessors[i].transform.position = positions[i];
                    activeProcessors[i].transform.rotation = rotations[i];
                }
            }
            
            OnLayoutRegenerated?.Invoke();
        }
        
        /// <summary>
        /// Clear the active processor list without destroying GameObjects
        /// </summary>
        private void ClearProcessorList()
        {
            activeProcessors.Clear();
        }
        
        /// <summary>
        /// Clear all active processors
        /// </summary>
        public void ClearAllProcessors()
        {
            foreach (var processor in activeProcessors)
            {
                if (processor != null)
                {
                    if (Application.isPlaying)
                        Destroy(processor.gameObject);
                    else
                        DestroyImmediate(processor.gameObject);
                }
            }
            
            activeProcessors.Clear();
        }
        
        /// <summary>
        /// Get processor by index
        /// </summary>
        public StemProcessor GetProcessor(int index)
        {
            if (index >= 0 && index < activeProcessors.Count)
            {
                return activeProcessors[index];
            }
            return null;
        }
        
        /// <summary>
        /// Get all active processors (defensive copy; safe for callers that mutate).
        /// </summary>
        public List<StemProcessor> GetAllProcessors()
        {
            return new List<StemProcessor>(activeProcessors);
        }

        /// <summary>
        /// Read-only view of the active processors with no per-call allocation.
        /// Use this on hot paths (hand tracking) instead of GetAllProcessors().
        /// </summary>
        public IReadOnlyList<StemProcessor> ActiveProcessors => activeProcessors;
        
        /// <summary>
        /// Play all stems
        /// </summary>
        public void PlayAll()
        {
            foreach (var processor in activeProcessors)
            {
                if (processor != null && processor.audioSource != null)
                {
                    processor.audioSource.Play();
                }
            }
        }
        
        /// <summary>
        /// Stop all stems
        /// </summary>
        public void StopAll()
        {
            foreach (var processor in activeProcessors)
            {
                if (processor != null && processor.audioSource != null)
                {
                    processor.audioSource.Stop();
                }
            }
        }
        
        /// <summary>
        /// Pause all stems
        /// </summary>
        public void PauseAll()
        {
            foreach (var processor in activeProcessors)
            {
                if (processor != null && processor.audioSource != null)
                {
                    processor.audioSource.Pause();
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showLayoutGizmos || layoutStrategy == null) return;
            
            Vector3 center = useLocalSpace ? transform.position + layoutCenter : layoutCenter;
            
            // Draw center point
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, 0.05f);
            
            // Let layout strategy draw its gizmos
            int stemCount = stems != null ? stems.Length : 0;
            if (stemCount > 0)
            {
                layoutStrategy.DrawLayoutGizmos(stemCount, center);
            }
        }
        
        private void OnDestroy()
        {
            ClearAllProcessors();
        }
    }
}
