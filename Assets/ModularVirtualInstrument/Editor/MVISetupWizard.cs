using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ModularVirtualInstrument.Editor
{
    /// <summary>
    /// Editor tool to setup MVI in the scene hierarchy with proper configuration
    /// and validate readiness for runtime playback.
    /// </summary>
    public class MVISetupWizard : EditorWindow
    {
        private ModularVirtualInstrument.ModularSynthController existingController;
        private ModularVirtualInstrument.UnifiedHandTrackingManager existingHandTracker;
        
        // Configuration options
        private string mviRootName = "ModularVirtualInstrument";
        private Vector3 mviPosition = Vector3.zero;
        private int defaultStemCount = 4;
        private bool createExampleStemData = true;
        private bool setupHandTracking = true;
        private bool createAudioListener = true;
        private bool autoCreateEffects = true;
        private bool autoCreateAudioMixer = true;
        private bool autoFindOculusHands = true;
        
        // Validation results
        private List<string> validationErrors = new List<string>();
        private List<string> validationWarnings = new List<string>();
        private bool hasValidated = false;
        
        private Vector2 scrollPosition;
        
        [MenuItem("GANTASMO/Modular Instrument/Setup Wizard", false, 70)]
        public static void ShowWindow()
        {
            MVISetupWizard window = GetWindow<MVISetupWizard>("MVI Setup Wizard");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Check for existing MVI components in scene
            existingController = Object.FindFirstObjectByType<ModularVirtualInstrument.ModularSynthController>();
            existingHandTracker = Object.FindFirstObjectByType<ModularVirtualInstrument.UnifiedHandTrackingManager>();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Modular Virtual Instrument Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This wizard will create and configure the MVI system in your scene hierarchy.", MessageType.Info);
            
            GUILayout.Space(10);
            DrawExistingComponentsSection();
            
            GUILayout.Space(10);
            DrawConfigurationSection();
            
            GUILayout.Space(10);
            DrawSetupButtons();
            
            GUILayout.Space(15);
            DrawValidationSection();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawExistingComponentsSection()
        {
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Existing Controller", existingController, typeof(ModularVirtualInstrument.ModularSynthController), true);
            EditorGUILayout.ObjectField("Existing Hand Tracker", existingHandTracker, typeof(ModularVirtualInstrument.UnifiedHandTrackingManager), true);
            EditorGUI.EndDisabledGroup();
            
            if (existingController != null)
            {
                EditorGUILayout.HelpBox("MVI Controller already exists in scene. Setup will update existing configuration.", MessageType.Warning);
            }
        }
        
        private void DrawConfigurationSection()
        {
            EditorGUILayout.LabelField("Configuration Options", EditorStyles.boldLabel);
            
            mviRootName = EditorGUILayout.TextField("Root GameObject Name", mviRootName);
            mviPosition = EditorGUILayout.Vector3Field("Position", mviPosition);
            
            GUILayout.Space(5);
            defaultStemCount = EditorGUILayout.IntSlider("Default Stem Count", defaultStemCount, 1, 8);
            
            GUILayout.Space(5);
            createExampleStemData = EditorGUILayout.Toggle("Create Example StemData", createExampleStemData);
            setupHandTracking = EditorGUILayout.Toggle("Setup Hand Tracking", setupHandTracking);
            createAudioListener = EditorGUILayout.Toggle("Create Audio Listener", createAudioListener);
            
            GUILayout.Space(5);
            EditorGUILayout.LabelField("One-Click Automation", EditorStyles.boldLabel);
            autoCreateEffects = EditorGUILayout.Toggle("Auto-Create All Effects", autoCreateEffects);
            autoCreateAudioMixer = EditorGUILayout.Toggle("Auto-Create AudioMixer", autoCreateAudioMixer);
            autoFindOculusHands = EditorGUILayout.Toggle("Auto-Find Oculus Hands", autoFindOculusHands);
            
            if (createExampleStemData)
            {
                EditorGUILayout.HelpBox($"Will create {defaultStemCount} example StemData assets in Assets/ModularVirtualInstrument/Data/ExampleStems/", MessageType.Info);
            }
        }
        
        private void DrawSetupButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Build MVI in Scene", GUILayout.Height(35)))
            {
                BuildMVIInScene();
                hasValidated = false; // Reset validation after setup
            }
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("Clear MVI from Scene", GUILayout.Height(35)))
            {
                ClearMVIFromScene();
                hasValidated = false;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Force Initialize button (for runtime debugging)
            if (Application.isPlaying && existingController != null)
            {
                GUILayout.Space(5);
                GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
                if (GUILayout.Button("Force Initialize MVI (Runtime)", GUILayout.Height(30)))
                {
                    existingController.InitializeSystem();
                    Debug.Log("[MVI Wizard] Manually triggered InitializeSystem()");
                }
                GUI.backgroundColor = Color.white;
            }
        }
        
        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Validation & Readiness Check", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.4f, 0.6f, 1f);
            if (GUILayout.Button("Validate MVI Setup", GUILayout.Height(30)))
            {
                ValidateMVISetup();
            }
            GUI.backgroundColor = Color.white;
            
            if (hasValidated)
            {
                GUILayout.Space(10);
                
                // Show errors
            if (validationErrors.Count > 0)
            {
                EditorGUILayout.HelpBox($"Found {validationErrors.Count} error(s) - MVI is NOT ready to play", MessageType.Error);
                foreach (string error in validationErrors)
                {
                    EditorGUILayout.LabelField("• " + error, EditorStyles.wordWrappedLabel);
                }
                
                GUILayout.Space(5);
                if (GUILayout.Button("Auto-Fix Missing Assets", GUILayout.Height(25)))
                {
                    AutoFixMissingAssets();
                }
            }                // Show warnings
                if (validationWarnings.Count > 0)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.HelpBox($"Found {validationWarnings.Count} warning(s)", MessageType.Warning);
                    foreach (string warning in validationWarnings)
                    {
                        EditorGUILayout.LabelField("• " + warning, EditorStyles.wordWrappedLabel);
                    }
                }
                
                // Show success
                if (validationErrors.Count == 0)
                {
                    GUILayout.Space(5);
                    GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
                    EditorGUILayout.HelpBox("✓ MVI is ready to play!", MessageType.Info);
                    GUI.backgroundColor = Color.white;
                }
            }
        }
        
        // ===== SETUP IMPLEMENTATION =====
        
        private void BuildMVIInScene()
        {
            Undo.SetCurrentGroupName("Build MVI in Scene");
            int undoGroup = Undo.GetCurrentGroup();
            
            try
            {
                // Auto-create effects library first
                if (autoCreateEffects)
                {
                    CreateEffectsLibrary();
                }
                
                // Auto-create audio mixer
                AudioMixer mainMixer = null;
                if (autoCreateAudioMixer)
                {
                    mainMixer = CreateAudioMixerWithStemGroups(defaultStemCount);
                }
                
                // Find or create root GameObject
                GameObject mviRoot = existingController != null ? existingController.gameObject : new GameObject(mviRootName);
                Undo.RegisterCreatedObjectUndo(mviRoot, "Create MVI Root");
                mviRoot.transform.position = mviPosition;
                
                // Add/get ModularSynthController
                ModularVirtualInstrument.ModularSynthController controller = mviRoot.GetComponent<ModularVirtualInstrument.ModularSynthController>();
                if (controller == null)
                {
                    controller = Undo.AddComponent<ModularVirtualInstrument.ModularSynthController>(mviRoot);
                }
                
                // Setup layout strategy
                ModularVirtualInstrument.SemicircularLayout layout = ScriptableObject.CreateInstance<ModularVirtualInstrument.SemicircularLayout>();
                layout.radius = 1.5f;
                layout.heightOffset = 1.5f;
                layout.arcAngle = 180f;
                layout.autoCreateControls = true;
                
                string layoutPath = "Assets/ModularVirtualInstrument/Data/DefaultSemicircularLayout.asset";
                AssetDatabase.CreateAsset(layout, layoutPath);
                controller.layoutStrategy = layout;
                
                // Setup hand tracking
                if (setupHandTracking)
                {
                    GameObject handTrackingObj = GameObject.Find("HandTrackingManager");
                    if (handTrackingObj == null)
                    {
                        handTrackingObj = new GameObject("HandTrackingManager");
                        Undo.RegisterCreatedObjectUndo(handTrackingObj, "Create Hand Tracking Manager");
                    }
                    
                    ModularVirtualInstrument.UnifiedHandTrackingManager handTracker = handTrackingObj.GetComponent<ModularVirtualInstrument.UnifiedHandTrackingManager>();
                    if (handTracker == null)
                    {
                        handTracker = Undo.AddComponent<ModularVirtualInstrument.UnifiedHandTrackingManager>(handTrackingObj);
                    }
                    
                    // Assign controller using SerializedObject (private field)
                    SerializedObject serializedTracker = new SerializedObject(handTracker);
                    serializedTracker.FindProperty("synthController").objectReferenceValue = controller;
                    serializedTracker.ApplyModifiedProperties();
                }
                
                // Create audio listener if needed
                if (createAudioListener && Camera.main != null)
                {
                    AudioListener listener = Camera.main.GetComponent<AudioListener>();
                    if (listener == null)
                    {
                        Undo.AddComponent<AudioListener>(Camera.main.gameObject);
                    }
                }
                
                // Create example stem data
                if (createExampleStemData)
                {
                    CreateExampleStemDataAssets(controller, mainMixer);
                }
                
                // Auto-find and assign Oculus hands
                if (autoFindOculusHands && setupHandTracking)
                {
                    AutoAssignOculusHands();
                }
                
                // Mark scene dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Refresh references
                existingController = controller;
                existingHandTracker = Object.FindFirstObjectByType<ModularVirtualInstrument.UnifiedHandTrackingManager>();
                
                Debug.Log($"[MVI Setup] Successfully built MVI in scene at '{mviRoot.name}'");

                EditorGUIUtility.PingObject(mviRoot);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MVI Setup] Error building MVI: {e.Message}");
                Undo.RevertAllDownToGroup(undoGroup);
            }
            
            Undo.CollapseUndoOperations(undoGroup);
        }
        
        // ===== ONE-CLICK AUTOMATION METHODS =====
        
        /// <summary>
        /// Creates all 6 effect assets (Pitch, BitCrush, Stutter, Delay, Filter, Distortion)
        /// </summary>
        private void CreateEffectsLibrary()
        {
            string effectsFolder = "Assets/ModularVirtualInstrument/Effects/Presets";
            if (!AssetDatabase.IsValidFolder(effectsFolder))
            {
                string parentFolder = "Assets/ModularVirtualInstrument/Effects";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets/ModularVirtualInstrument", "Effects");
                }
                AssetDatabase.CreateFolder(parentFolder, "Presets");
            }
            
            // PitchEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.PitchEffect>(
                $"{effectsFolder}/PitchEffect.asset",
                effect => {
                    effect.minPitch = 0.5f;
                    effect.maxPitch = 2.0f;
                    effect.enableGrowl = true;
                }
            );
            
            // BitCrushEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.BitCrushEffect>(
                $"{effectsFolder}/BitCrushEffect.asset",
                effect => {
                    effect.minBitDepth = 16;
                    effect.maxBitDepth = 4;
                    effect.enableGlitchEffect = true;
                }
            );
            
            // StutterEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.StutterEffect>(
                $"{effectsFolder}/StutterEffect.asset",
                effect => {
                    effect.minInterval = 0.05f;
                    effect.maxInterval = 0.5f;
                }
            );
            
            // DelayEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.DelayEffect>(
                $"{effectsFolder}/DelayEffect.asset",
                effect => {
                    effect.minDelayMs = 10f;
                    effect.maxDelayMs = 500f;
                }
            );
            
            // FilterEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.FilterEffect>(
                $"{effectsFolder}/FilterEffect.asset",
                effect => {
                    effect.filterType = ModularVirtualInstrument.Effects.FilterEffect.FilterType.LowPass;
                    effect.minCutoff = 100f;
                    effect.maxCutoff = 22000f;
                }
            );
            
            // DistortionEffect
            CreateEffectAssetIfMissing<ModularVirtualInstrument.Effects.DistortionEffect>(
                $"{effectsFolder}/DistortionEffect.asset",
                effect => {
                    effect.minDistortion = 0f;
                    effect.maxDistortion = 0.8f;
                    effect.distortionType = ModularVirtualInstrument.Effects.DistortionEffect.DistortionType.Standard;
                }
            );
            
            AssetDatabase.SaveAssets();
            Debug.Log("[MVI Setup] Created all 6 effect presets");
        }
        
        /// <summary>
        /// Helper to create effect asset if it doesn't exist
        /// </summary>
        private void CreateEffectAssetIfMissing<T>(string path, System.Action<T> configure = null) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return;
            
            T effect = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(effect);
            AssetDatabase.CreateAsset(effect, path);
        }
        
        /// <summary>
        /// Creates AudioMixer with master + per-stem groups
        /// </summary>
        private AudioMixer CreateAudioMixerWithStemGroups(int stemCount)
        {
            string mixerFolder = "Assets/ModularVirtualInstrument/Audio";
            if (!AssetDatabase.IsValidFolder(mixerFolder))
            {
                AssetDatabase.CreateFolder("Assets/ModularVirtualInstrument", "Audio");
            }
            
            string mixerPath = $"{mixerFolder}/MVI_MainMixer.mixer";
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            
            if (mixer == null)
            {
                // AudioMixer can only be created through AssetDatabase, not via constructor
                // We'll use a workaround by copying from a template or skipping this
                Debug.LogWarning($"[MVI Setup] AudioMixer creation skipped - Unity API doesn't support programmatic AudioMixer creation.");
                Debug.LogWarning($"[MVI Setup] Please manually create an AudioMixer at {mixerPath} and add {stemCount} groups named 'Stem_1', 'Stem_2', etc.");
                return null;
            }
            
            return mixer;
        }
        
        /// <summary>
        /// Auto-finds Oculus Hand components in scene and assigns to hand tracker
        /// </summary>
        private void AutoAssignOculusHands()
        {
            var handTracker = Object.FindFirstObjectByType<ModularVirtualInstrument.UnifiedHandTrackingManager>();
            if (handTracker == null) return;
            
            // Find OVRHand components (Oculus Interaction SDK hands)
            var ovrHands = Object.FindObjectsByType<Oculus.Interaction.Input.Hand>(FindObjectsSortMode.None);
            
            SerializedObject serializedTracker = new SerializedObject(handTracker);
            var leftHandProp = serializedTracker.FindProperty("leftHand");
            var rightHandProp = serializedTracker.FindProperty("rightHand");
            
            foreach (var hand in ovrHands)
            {
                // Check handedness by GameObject name or component properties
                string handName = hand.gameObject.name.ToLower();
                if (handName.Contains("left") && leftHandProp.objectReferenceValue == null)
                {
                    leftHandProp.objectReferenceValue = hand;
                    Debug.Log($"[MVI Setup] Assigned Left Hand: {hand.gameObject.name}");
                }
                else if (handName.Contains("right") && rightHandProp.objectReferenceValue == null)
                {
                    rightHandProp.objectReferenceValue = hand;
                    Debug.Log($"[MVI Setup] Assigned Right Hand: {hand.gameObject.name}");
                }
            }
            
            serializedTracker.ApplyModifiedProperties();
            
            if (leftHandProp.objectReferenceValue == null || rightHandProp.objectReferenceValue == null)
            {
                Debug.LogWarning("[MVI Setup] Could not find both Oculus hands. Please assign manually in UnifiedHandTrackingManager.");
            }
        }
        
        private void CreateExampleStemDataAssets(ModularVirtualInstrument.ModularSynthController controller, AudioMixer mixer)
        {
            string stemDataFolder = "Assets/ModularVirtualInstrument/Data/ExampleStems";
            if (!AssetDatabase.IsValidFolder(stemDataFolder))
            {
                AssetDatabase.CreateFolder("Assets/ModularVirtualInstrument/Data", "ExampleStems");
            }
            
            List<ModularVirtualInstrument.StemData> createdStems = new List<ModularVirtualInstrument.StemData>();
            
            // Load effect presets
            ModularVirtualInstrument.AxisEffect[] effectPresets = LoadEffectPresets();
            
            for (int i = 0; i < defaultStemCount; i++)
            {
                string stemPath = $"{stemDataFolder}/ExampleStem_{i + 1}.asset";
                
                // Check if already exists
                ModularVirtualInstrument.StemData existingStem = AssetDatabase.LoadAssetAtPath<ModularVirtualInstrument.StemData>(stemPath);
                if (existingStem != null)
                {
                    createdStems.Add(existingStem);
                    continue;
                }
                
                // Create new stem data
                ModularVirtualInstrument.StemData stem = ScriptableObject.CreateInstance<ModularVirtualInstrument.StemData>();
                stem.stemName = $"Stem {i + 1}";
                stem.themeColor = GetStemColor(i);
                stem.emissionIntensity = 2f;
                stem.cubeSize = new Vector3(0.3f, 0.3f, 0.3f);
                
                // Auto-assign effects
                if (effectPresets.Length >= 3)
                {
                    // Rotate through effects for variety
                    stem.xAxisEffect = effectPresets[(i * 3) % effectPresets.Length];
                    stem.yAxisEffect = effectPresets[(i * 3 + 1) % effectPresets.Length];
                    stem.zAxisEffect = effectPresets[(i * 3 + 2) % effectPresets.Length];
                }
                
                // Assign to mixer group if available
                if (mixer != null)
                {
                    var groups = mixer.FindMatchingGroups($"Stem_{i + 1}");
                    if (groups.Length > 0)
                    {
                        stem.outputMixerGroup = groups[0];
                    }
                }
                
                AssetDatabase.CreateAsset(stem, stemPath);
                createdStems.Add(stem);
            }
            
            AssetDatabase.SaveAssets();
            
            // Assign to controller
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty stemsProp = serializedController.FindProperty("stems");
            stemsProp.arraySize = createdStems.Count;
            
            for (int i = 0; i < createdStems.Count; i++)
            {
                stemsProp.GetArrayElementAtIndex(i).objectReferenceValue = createdStems[i];
            }
            
            serializedController.ApplyModifiedProperties();
            
            Debug.Log($"[MVI Setup] Created {createdStems.Count} example StemData assets");
        }
        
        /// <summary>
        /// Loads all effect presets from the Effects/Presets folder
        /// </summary>
        private ModularVirtualInstrument.AxisEffect[] LoadEffectPresets()
        {
            string presetsFolder = "Assets/ModularVirtualInstrument/Effects/Presets";
            if (!AssetDatabase.IsValidFolder(presetsFolder))
            {
                return new ModularVirtualInstrument.AxisEffect[0];
            }
            
            List<ModularVirtualInstrument.AxisEffect> effects = new List<ModularVirtualInstrument.AxisEffect>();
            
            string[] guids = AssetDatabase.FindAssets("t:AxisEffect", new[] { presetsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<ModularVirtualInstrument.AxisEffect>(path);
                if (effect != null)
                {
                    effects.Add(effect);
                }
            }
            
            return effects.ToArray();
        }
        
        private Color GetStemColor(int index)
        {
            Color[] colors = new Color[]
            {
                new Color(0f, 1f, 1f),      // Cyan
                new Color(1f, 0f, 1f),      // Magenta
                new Color(1f, 1f, 0f),      // Yellow
                new Color(0f, 1f, 0f),      // Green
                new Color(1f, 0.5f, 0f),    // Orange
                new Color(0.5f, 0f, 1f),    // Purple
                new Color(1f, 0f, 0f),      // Red
                new Color(0f, 0.5f, 1f)     // Blue
            };
            
            return colors[index % colors.Length];
        }
        
        private void ClearMVIFromScene()
        {
            if (!EditorUtility.DisplayDialog("Clear MVI from Scene", 
                "This will remove all MVI GameObjects from the scene. Continue?", 
                "Yes", "Cancel"))
            {
                return;
            }
            
            Undo.SetCurrentGroupName("Clear MVI from Scene");
            int undoGroup = Undo.GetCurrentGroup();
            
            // Remove all MVI components
            ModularVirtualInstrument.ModularSynthController[] controllers = Object.FindObjectsByType<ModularVirtualInstrument.ModularSynthController>(FindObjectsSortMode.None);
            foreach (var controller in controllers)
            {
                Undo.DestroyObjectImmediate(controller.gameObject);
            }
            
            ModularVirtualInstrument.UnifiedHandTrackingManager[] handTrackers = Object.FindObjectsByType<ModularVirtualInstrument.UnifiedHandTrackingManager>(FindObjectsSortMode.None);
            foreach (var tracker in handTrackers)
            {
                Undo.DestroyObjectImmediate(tracker.gameObject);
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            existingController = null;
            existingHandTracker = null;
            
            Debug.Log("[MVI Setup] Cleared MVI from scene");
        }
        
        // ===== VALIDATION IMPLEMENTATION =====
        
        private void ValidateMVISetup()
        {
            validationErrors.Clear();
            validationWarnings.Clear();
            hasValidated = true;
            
            // Check for ModularSynthController
            ModularVirtualInstrument.ModularSynthController controller = Object.FindFirstObjectByType<ModularVirtualInstrument.ModularSynthController>();
            if (controller == null)
            {
                validationErrors.Add("No ModularSynthController found in scene");
                return;
            }
            
            // Check layout strategy
            if (controller.layoutStrategy == null)
            {
                validationErrors.Add("ModularSynthController is missing LayoutStrategy");
            }
            
            // Check stems array
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty stemsProp = serializedController.FindProperty("stems");
            
            if (stemsProp.arraySize == 0)
            {
                validationWarnings.Add("No stems assigned to ModularSynthController");
            }
            else
            {
                // Validate each stem
                for (int i = 0; i < stemsProp.arraySize; i++)
                {
                    ModularVirtualInstrument.StemData stem = stemsProp.GetArrayElementAtIndex(i).objectReferenceValue as ModularVirtualInstrument.StemData;
                    if (stem == null)
                    {
                        validationErrors.Add($"Stem slot {i} is null");
                        continue;
                    }
                    
                    // Check audio clip
                    if (stem.audioClip == null)
                    {
                        validationWarnings.Add($"Stem '{stem.stemName}' has no AudioClip assigned");
                    }
                    
                    // Check effects (optional but warn if all missing)
                    if (stem.xAxisEffect == null && stem.yAxisEffect == null && stem.zAxisEffect == null)
                    {
                        validationWarnings.Add($"Stem '{stem.stemName}' has no effects assigned to any axis");
                    }
                }
            }
            
            // Check hand tracking
            ModularVirtualInstrument.UnifiedHandTrackingManager handTracker = Object.FindFirstObjectByType<ModularVirtualInstrument.UnifiedHandTrackingManager>();
            if (handTracker == null)
            {
                validationWarnings.Add("No UnifiedHandTrackingManager found - hand interaction will not work");
            }
            else
            {
                // Use SerializedObject to check the private field
                SerializedObject serializedTracker = new SerializedObject(handTracker);
                var controllerProp = serializedTracker.FindProperty("synthController");
                if (controllerProp.objectReferenceValue != controller)
                {
                    validationErrors.Add("UnifiedHandTrackingManager.synthController is not set to the scene's ModularSynthController");
                }
            }
            
            // Check audio listener
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                validationErrors.Add("No AudioListener found in scene - audio will not be heard");
            }
            
            // Check Oculus hand tracking components (if hand tracker exists)
            if (handTracker != null)
            {
                SerializedObject serializedTracker = new SerializedObject(handTracker);
                var leftHandProp = serializedTracker.FindProperty("leftHand");
                var rightHandProp = serializedTracker.FindProperty("rightHand");
                
                if (leftHandProp.objectReferenceValue == null || rightHandProp.objectReferenceValue == null)
                {
                    validationWarnings.Add("Hand tracking references not set - assign OVRHand components in UnifiedHandTrackingManager");
                }
            }
            
            Debug.Log($"[MVI Validation] Completed - {validationErrors.Count} errors, {validationWarnings.Count} warnings");
        }
        
        // ===== AUTO-FIX METHODS =====
        
        /// <summary>
        /// Attempts to automatically fix missing assets/references detected during validation
        /// </summary>
        private void AutoFixMissingAssets()
        {
            bool changesMade = false;
            
            var controller = Object.FindFirstObjectByType<ModularVirtualInstrument.ModularSynthController>();
            
            // Fix missing layout strategy
            if (controller != null && controller.layoutStrategy == null)
            {
                string layoutPath = "Assets/ModularVirtualInstrument/Data/DefaultSemicircularLayout.asset";
                var layout = AssetDatabase.LoadAssetAtPath<ModularVirtualInstrument.SemicircularLayout>(layoutPath);
                if (layout == null)
                {
                    layout = ScriptableObject.CreateInstance<ModularVirtualInstrument.SemicircularLayout>();
                    layout.radius = 1.5f;
                    layout.heightOffset = 1.5f;
                    AssetDatabase.CreateAsset(layout, layoutPath);
                }
                controller.layoutStrategy = layout;
                changesMade = true;
                Debug.Log("[MVI Auto-Fix] Created/assigned layout strategy");
            }
            
            // Fix missing effects
            var effectPresets = LoadEffectPresets();
            if (effectPresets.Length == 0)
            {
                CreateEffectsLibrary();
                effectPresets = LoadEffectPresets();
                changesMade = true;
                Debug.Log("[MVI Auto-Fix] Created effect library");
            }
            
            // Fix stems with missing effects
            if (controller != null)
            {
                SerializedObject serializedController = new SerializedObject(controller);
                SerializedProperty stemsProp = serializedController.FindProperty("stems");
                
                for (int i = 0; i < stemsProp.arraySize; i++)
                {
                    var stem = stemsProp.GetArrayElementAtIndex(i).objectReferenceValue as ModularVirtualInstrument.StemData;
                    if (stem != null && effectPresets.Length >= 3)
                    {
                        if (stem.xAxisEffect == null || stem.yAxisEffect == null || stem.zAxisEffect == null)
                        {
                            stem.xAxisEffect = effectPresets[0];
                            stem.yAxisEffect = effectPresets[Mathf.Min(1, effectPresets.Length - 1)];
                            stem.zAxisEffect = effectPresets[Mathf.Min(2, effectPresets.Length - 1)];
                            EditorUtility.SetDirty(stem);
                            changesMade = true;
                            Debug.Log($"[MVI Auto-Fix] Assigned effects to {stem.stemName}");
                        }
                    }
                }
            }
            
            // Fix missing audio listener
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null && Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
                changesMade = true;
                Debug.Log("[MVI Auto-Fix] Added AudioListener to Main Camera");
            }
            
            // Fix missing hand tracker
            var handTracker = Object.FindFirstObjectByType<ModularVirtualInstrument.UnifiedHandTrackingManager>();
            if (handTracker == null)
            {
                GameObject handTrackingObj = new GameObject("HandTrackingManager");
                handTracker = handTrackingObj.AddComponent<ModularVirtualInstrument.UnifiedHandTrackingManager>();
                
                SerializedObject serializedTracker = new SerializedObject(handTracker);
                serializedTracker.FindProperty("synthController").objectReferenceValue = controller;
                serializedTracker.ApplyModifiedProperties();
                
                changesMade = true;
                Debug.Log("[MVI Auto-Fix] Created UnifiedHandTrackingManager");
            }
            
            // Auto-find Oculus hands
            AutoAssignOculusHands();
            
            if (changesMade)
            {
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[MVI Auto-Fix] Completed - please re-validate");
                
                // Re-validate after fixes
                ValidateMVISetup();
            }
            else
            {
                Debug.Log("[MVI Auto-Fix] No automatic fixes available for current errors");
            }
        }
    }
}
