using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ModularVirtualInstrument.Editor
{
    /// <summary>
    /// Custom editor for ModularSynthController with edit-mode procedural generation,
    /// gizmo handles for per-stem positioning, and prefab saving.
    /// </summary>
    [CustomEditor(typeof(ModularSynthController))]
    public class ModularSynthControllerEditor : UnityEditor.Editor
    {
        private ModularSynthController controller;
        private SemicircularLayout semicircularLayout;
        private int selectedStemIndex = -1;
        
        private void OnEnable()
        {
            controller = (ModularSynthController)target;
            
            if (controller.layoutStrategy is SemicircularLayout)
            {
                semicircularLayout = controller.layoutStrategy as SemicircularLayout;
            }
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Edit Mode Layout Tools", EditorStyles.boldLabel);
            
            // Generate button
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate Stems in Edit Mode", GUILayout.Height(35)))
            {
                GenerateStemsInEditMode();
            }
            GUI.backgroundColor = Color.white;
            
            // Clear button
            if (GUILayout.Button("Clear All Stems", GUILayout.Height(30)))
            {
                ClearAllStems();
            }
            
            EditorGUILayout.Space(5);
            
            // Regenerate layout button
            if (controller.transform.childCount > 0)
            {
                if (GUILayout.Button("Regenerate Layout (Keep Stems)", GUILayout.Height(30)))
                {
                    RegenerateLayoutPositions();
                }
            }
            
            EditorGUILayout.Space(10);
            
            // Visualization Controls
            EditorGUILayout.LabelField("Visualization Controls", EditorStyles.boldLabel);
            
            // Find all StemVisualizer components and control their wireframe visibility
            StemVisualizer[] visualizers = controller.GetComponentsInChildren<StemVisualizer>();
            if (visualizers.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                bool currentWireframeState = visualizers[0].showWireframe;
                bool newWireframeState = EditorGUILayout.Toggle("Show Interaction Wireframes", currentWireframeState);
                
                if (EditorGUI.EndChangeCheck())
                {
                    // Update the default setting on the controller
                    Undo.RecordObject(controller, "Toggle Wireframe Visibility");
                    controller.defaultWireframeVisibility = newWireframeState;
                    
                    // Update all existing visualizers (including any we might have missed)
                    StemVisualizer[] allVisualizers = controller.GetComponentsInChildren<StemVisualizer>();
                    foreach (var visualizer in allVisualizers)
                    {
                        Undo.RecordObject(visualizer, "Toggle Wireframe Visibility");
                        visualizer.showWireframe = newWireframeState;
                        visualizer.SetWireframeVisible(newWireframeState);
                        EditorUtility.SetDirty(visualizer);
                    }
                    
                    EditorUtility.SetDirty(controller);
                    Debug.Log($"[MVI] Applied wireframe visibility ({newWireframeState}) to {allVisualizers.Length} stems");
                }
            }
            else
            {
                // Show default setting when no visualizers exist
                EditorGUI.BeginChangeCheck();
                bool newWireframeState = EditorGUILayout.Toggle("Show Interaction Wireframes (Default)", controller.defaultWireframeVisibility);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(controller, "Toggle Default Wireframe Visibility");
                    controller.defaultWireframeVisibility = newWireframeState;
                    EditorUtility.SetDirty(controller);
                }
                
                EditorGUILayout.HelpBox("No StemVisualizers found. Generate stems to apply wireframe settings.", MessageType.Info);
            }
            
            // Force apply button for existing stems
            if (visualizers.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                
                GUI.backgroundColor = new Color(0.8f, 0.4f, 0.4f);
                if (GUILayout.Button("🔧 Fix Existing Wireframes", GUILayout.Height(25)))
                {
                    controller.ApplyWireframeSettingsToExistingStems();
                }
                
                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
                if (GUILayout.Button("🚫 FORCE HIDE ALL", GUILayout.Height(25), GUILayout.Width(130)))
                {
                    controller.ForceHideAllWireframes();
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("Use 'Fix' to apply current wireframe setting, or 'FORCE HIDE ALL' to immediately hide all wireframes regardless of settings.", MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);
            
            // Live Layout Editing Section
            if (semicircularLayout != null && controller.transform.childCount > 0)
            {
                DrawLiveLayoutControls();
            }
            
#if MVI_FLEXALON
            // Flexalon Layout Editing Section
            FlexalonLayoutAdapter flexalonAdapter = controller.layoutStrategy as FlexalonLayoutAdapter;
            if (flexalonAdapter != null && controller.transform.childCount > 0)
            {
                DrawFlexalonLayoutControls(flexalonAdapter);
            }
#endif
            
            EditorGUILayout.Space(10);
            
            // Save as prefab
            GUI.backgroundColor = new Color(0.4f, 0.6f, 1f);
            if (GUILayout.Button("Save as Instrument Prefab", GUILayout.Height(35)))
            {
                SaveAsInstrumentPrefab();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            // Stem selection for gizmo manipulation
            if (controller.transform.childCount > 0)
            {
                EditorGUILayout.LabelField("Stem Selection for Gizmo Control", EditorStyles.boldLabel);
                
                string[] stemNames = new string[controller.transform.childCount + 1];
                stemNames[0] = "None (View Only)";
                for (int i = 0; i < controller.transform.childCount; i++)
                {
                    stemNames[i + 1] = controller.transform.GetChild(i).name;
                }
                
                int newSelection = EditorGUILayout.Popup("Selected Stem", selectedStemIndex + 1, stemNames) - 1;
                if (newSelection != selectedStemIndex)
                {
                    selectedStemIndex = newSelection;
                    SceneView.RepaintAll();
                }
                
                if (selectedStemIndex >= 0 && semicircularLayout != null)
                {
                    EditorGUILayout.HelpBox(
                        "Use Scene View handles to adjust:\n" +
                        "• Position Handle (T): Move stem along arc\n" +
                        "• Custom Sliders: Height, Radius, Depth",
                        MessageType.Info
                    );
                    
                    // Per-stem controls
                    DrawPerStemControls(selectedStemIndex);
                }
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(controller);
                if (semicircularLayout != null)
                {
                    EditorUtility.SetDirty(semicircularLayout);
                }
            }
        }
        
        private void DrawLiveLayoutControls()
        {
            EditorGUILayout.LabelField("Live Layout Controls", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            // Global layout controls
            semicircularLayout.radius = EditorGUILayout.Slider("Radius", semicircularLayout.radius, 0.5f, 5f);
            semicircularLayout.radiusMultiplier = EditorGUILayout.Slider("Radius Multiplier", semicircularLayout.radiusMultiplier, 0.1f, 2f);
            semicircularLayout.heightOffset = EditorGUILayout.Slider("Height Offset", semicircularLayout.heightOffset, -2f, 2f);
            semicircularLayout.arcRotation = EditorGUILayout.Slider("Arc Rotation", semicircularLayout.arcRotation, 0f, 360f);
            semicircularLayout.arcAngle = EditorGUILayout.Slider("Arc Angle", semicircularLayout.arcAngle, 30f, 360f);
            semicircularLayout.faceCenter = EditorGUILayout.Toggle("Face Center", semicircularLayout.faceCenter);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(semicircularLayout, "Adjust Layout");
                RegenerateLayoutPositions();
                EditorUtility.SetDirty(semicircularLayout);
            }
            
            EditorGUILayout.Space(5);
            
            // Runtime editor components
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Runtime Editors to All Stems"))
            {
                AddRuntimeEditorsToStems();
            }
            if (GUILayout.Button("Remove Runtime Editors"))
            {
                RemoveRuntimeEditorsFromStems();
            }
            EditorGUILayout.EndHorizontal();
        }
        
#if MVI_FLEXALON
        private void DrawFlexalonLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Flexalon Layout Controls", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            
            // Layout type selector
            adapter.layoutType = (FlexalonLayoutAdapter.FlexalonLayoutType)EditorGUILayout.EnumPopup("Layout Type", adapter.layoutType);
            
            EditorGUILayout.Space(5);
            
            // Type-specific controls
            switch (adapter.layoutType)
            {
                case FlexalonLayoutAdapter.FlexalonLayoutType.Grid:
                    DrawGridLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Flexible:
                    DrawFlexibleLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Circle:
                    DrawCircleLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Curve:
                    DrawCurveLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Align:
                    DrawAlignLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Random:
                    DrawRandomLayoutControls(adapter);
                    break;
                    
                case FlexalonLayoutAdapter.FlexalonLayoutType.Shape:
                    DrawShapeLayoutControls(adapter);
                    break;
            }
            
            EditorGUILayout.Space(5);
            
            // Animation controls
            adapter.useAnimations = EditorGUILayout.Toggle("Use Animations", adapter.useAnimations);
            if (adapter.useAnimations)
            {
                adapter.animationSpeed = EditorGUILayout.Slider("Animation Speed", adapter.animationSpeed, 1f, 20f);
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(adapter, "Adjust Flexalon Layout");
                RegenerateLayoutPositions();
                EditorUtility.SetDirty(adapter);
            }
            
            EditorGUILayout.Space(5);
            
            // Runtime editor components
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Runtime Editors to All Stems"))
            {
                AddRuntimeEditorsToStems();
            }
            if (GUILayout.Button("Remove Runtime Editors"))
            {
                RemoveRuntimeEditorsFromStems();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawGridLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.miniBoldLabel);
            adapter.gridColumns = EditorGUILayout.IntSlider("Columns", adapter.gridColumns, 1, 10);
            adapter.gridRows = EditorGUILayout.IntSlider("Rows", adapter.gridRows, 1, 10);
            adapter.columnSpacing = EditorGUILayout.Slider("Column Spacing", adapter.columnSpacing, 0.1f, 5f);
            adapter.rowSpacing = EditorGUILayout.Slider("Row Spacing", adapter.rowSpacing, 0.1f, 5f);
        }
        
        private void DrawFlexibleLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Flexible Settings", EditorStyles.miniBoldLabel);
            adapter.flexDirection = (Flexalon.Direction)EditorGUILayout.EnumPopup("Direction", adapter.flexDirection);
            adapter.flexGap = EditorGUILayout.Slider("Gap", adapter.flexGap, 0f, 5f);
            adapter.flexWrap = EditorGUILayout.Toggle("Wrap", adapter.flexWrap);
            if (adapter.flexWrap)
            {
                adapter.flexWrapDirection = (Flexalon.Direction)EditorGUILayout.EnumPopup("Wrap Direction", adapter.flexWrapDirection);
            }
        }
        
        private void DrawCircleLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Circle Settings", EditorStyles.miniBoldLabel);
            adapter.circleRadius = EditorGUILayout.Slider("Radius", adapter.circleRadius, 0.5f, 10f);
            adapter.circleRadiusMultiplier = EditorGUILayout.Slider("Radius Multiplier", adapter.circleRadiusMultiplier, 0.1f, 2f);
            adapter.circleStartAngle = EditorGUILayout.Slider("Start Angle", adapter.circleStartAngle, 0f, 360f);
            adapter.circleSpacingDegrees = EditorGUILayout.Slider("Spacing (Degrees)", adapter.circleSpacingDegrees, 1f, 90f);
            adapter.circleSpiral = EditorGUILayout.Toggle("Spiral", adapter.circleSpiral);
            if (adapter.circleSpiral)
            {
                adapter.spiralSpacing = EditorGUILayout.Slider("Spiral Spacing", adapter.spiralSpacing, 0.1f, 2f);
            }
        }
        
        private void DrawCurveLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Curve Settings", EditorStyles.miniBoldLabel);
            adapter.curvePath = EditorGUILayout.CurveField("Curve Path", adapter.curvePath);
            adapter.curveLength = EditorGUILayout.Slider("Curve Length", adapter.curveLength, 1f, 20f);
            adapter.curveSpacing = EditorGUILayout.Slider("Spacing", adapter.curveSpacing, 0.1f, 5f);
        }
        
        private void DrawAlignLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Align Settings", EditorStyles.miniBoldLabel);
            adapter.horizontalAlign = (Flexalon.Align)EditorGUILayout.EnumPopup("Horizontal Align", adapter.horizontalAlign);
            adapter.verticalAlign = (Flexalon.Align)EditorGUILayout.EnumPopup("Vertical Align", adapter.verticalAlign);
            adapter.depthAlign = (Flexalon.Align)EditorGUILayout.EnumPopup("Depth Align", adapter.depthAlign);
            
            EditorGUILayout.HelpBox("Align layout places all items at the same position. Use RuntimeLayoutEditor to offset individual stems.", MessageType.Info);
        }
        
        private void DrawRandomLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Random Settings", EditorStyles.miniBoldLabel);
            adapter.randomBoundsMin = EditorGUILayout.Vector3Field("Bounds Min", adapter.randomBoundsMin);
            adapter.randomBoundsMax = EditorGUILayout.Vector3Field("Bounds Max", adapter.randomBoundsMax);
            adapter.randomSeed = EditorGUILayout.IntField("Random Seed", adapter.randomSeed);
            
            if (GUILayout.Button("Randomize Seed"))
            {
                adapter.randomSeed = Random.Range(0, 10000);
                EditorUtility.SetDirty(adapter);
            }
        }
        
        private void DrawShapeLayoutControls(FlexalonLayoutAdapter adapter)
        {
            EditorGUILayout.LabelField("Shape Settings", EditorStyles.miniBoldLabel);
            adapter.shapeType = (FlexalonLayoutAdapter.ShapeType)EditorGUILayout.EnumPopup("Shape Type", adapter.shapeType);
            adapter.shapeRadius = EditorGUILayout.Slider("Radius", adapter.shapeRadius, 0.5f, 10f);
            adapter.shapeRadiusMultiplier = EditorGUILayout.Slider("Radius Multiplier", adapter.shapeRadiusMultiplier, 0.1f, 2f);
            
            if (adapter.shapeType == FlexalonLayoutAdapter.ShapeType.Pentagon || 
                adapter.shapeType == FlexalonLayoutAdapter.ShapeType.Hexagon)
            {
                adapter.shapeSides = EditorGUILayout.IntSlider("Sides", adapter.shapeSides, 3, 12);
            }
        }
#endif

        private void DrawPerStemControls(int stemIndex)
        {
            if (semicircularLayout.stemControls == null || stemIndex >= semicircularLayout.stemControls.Length)
                return;
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Controls for {controller.transform.GetChild(stemIndex).name}", EditorStyles.boldLabel);
            
            var stemControl = semicircularLayout.stemControls[stemIndex];
            
            EditorGUI.BeginChangeCheck();
            
            stemControl.perimeterPosition = EditorGUILayout.Slider("Perimeter Position", stemControl.perimeterPosition, 0f, 1f);
            stemControl.heightAdjustment = EditorGUILayout.Slider("Height Adjustment", stemControl.heightAdjustment, -2f, 2f);
            stemControl.depthOffset = EditorGUILayout.Slider("Depth Offset", stemControl.depthOffset, -2f, 2f);
            stemControl.additionalRotation = EditorGUILayout.Slider("Additional Rotation", stemControl.additionalRotation, -180f, 180f);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(semicircularLayout, "Adjust Stem Position");
                RegenerateLayoutPositions();
                EditorUtility.SetDirty(semicircularLayout);
            }
            
            if (GUILayout.Button("Reset to Default Position"))
            {
                Undo.RecordObject(semicircularLayout, "Reset Stem Position");
                int totalStems = controller.transform.childCount;
                stemControl.perimeterPosition = totalStems > 1 ? (float)stemIndex / (totalStems - 1) : 0.5f;
                stemControl.heightAdjustment = 0f;
                stemControl.depthOffset = 0f;
                stemControl.additionalRotation = 0f;
                RegenerateLayoutPositions();
                EditorUtility.SetDirty(semicircularLayout);
            }
        }
        
        private void AddRuntimeEditorsToStems()
        {
            Undo.SetCurrentGroupName("Add Runtime Editors");
            int undoGroup = Undo.GetCurrentGroup();
            
            for (int i = 0; i < controller.transform.childCount; i++)
            {
                Transform stem = controller.transform.GetChild(i);
                RuntimeLayoutEditor editor = stem.GetComponent<RuntimeLayoutEditor>();
                
                if (editor == null)
                {
                    editor = Undo.AddComponent<RuntimeLayoutEditor>(stem.gameObject);
                    editor.controller = controller;
                    editor.stemIndex = i;
                    editor.LoadFromLayout();
                }
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MVI Editor] Added RuntimeLayoutEditor to all stems");
        }
        
        private void RemoveRuntimeEditorsFromStems()
        {
            Undo.SetCurrentGroupName("Remove Runtime Editors");
            int undoGroup = Undo.GetCurrentGroup();
            
            for (int i = 0; i < controller.transform.childCount; i++)
            {
                Transform stem = controller.transform.GetChild(i);
                RuntimeLayoutEditor editor = stem.GetComponent<RuntimeLayoutEditor>();
                
                if (editor != null)
                {
                    Undo.DestroyObjectImmediate(editor);
                }
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MVI Editor] Removed RuntimeLayoutEditor from all stems");
        }
        
        private void GenerateStemsInEditMode()
        {
            if (controller.stems == null || controller.stems.Length == 0)
            {
                EditorUtility.DisplayDialog("No Stems", "Please assign StemData assets to the Stems array first.", "OK");
                return;
            }
            
            if (controller.layoutStrategy == null)
            {
                EditorUtility.DisplayDialog("No Layout", "Please assign a Layout Strategy first.", "OK");
                return;
            }
            
            Undo.SetCurrentGroupName("Generate MVI Stems");
            int undoGroup = Undo.GetCurrentGroup();
            
            // Clear existing
            ClearAllStems();
            
            // Generate
            controller.LoadStems(controller.stems);
            
            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            
            Undo.CollapseUndoOperations(undoGroup);
            
            Debug.Log($"[MVI Editor] Generated {controller.transform.childCount} stems in edit mode");
        }
        
        private void ClearAllStems()
        {
            if (controller.transform.childCount == 0) return;
            
            Undo.SetCurrentGroupName("Clear MVI Stems");
            int undoGroup = Undo.GetCurrentGroup();
            
            // Destroy all children
            for (int i = controller.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(controller.transform.GetChild(i).gameObject);
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            selectedStemIndex = -1;
            SceneView.RepaintAll();
            
            Debug.Log("[MVI Editor] Cleared all stems");
        }
        
        private void RegenerateLayoutPositions()
        {
            if (controller.layoutStrategy == null) return;
            
            Undo.RecordObject(controller.transform, "Regenerate Layout");
            
            Vector3 center = controller.useLocalSpace ? controller.transform.position + controller.layoutCenter : controller.layoutCenter;
            Vector3[] positions = controller.layoutStrategy.CalculatePositions(controller.transform.childCount, center);
            Quaternion[] rotations = controller.layoutStrategy.CalculateRotations(controller.transform.childCount, center);
            
            for (int i = 0; i < controller.transform.childCount; i++)
            {
                Transform child = controller.transform.GetChild(i);
                Undo.RecordObject(child, "Regenerate Layout");
                child.position = positions[i];
                child.rotation = rotations[i];
            }
            
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MVI Editor] Regenerated layout positions");
        }
        
        private void SaveAsInstrumentPrefab()
        {
            if (controller.transform.childCount == 0)
            {
                EditorUtility.DisplayDialog("No Stems", "Generate stems first before saving as prefab.", "OK");
                return;
            }
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Instrument Prefab",
                $"Instrument_{controller.stems.Length}Stems",
                "prefab",
                "Save the configured MVI instrument as a prefab"
            );
            
            if (string.IsNullOrEmpty(path)) return;
            
            // Create prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(controller.gameObject, path);
            
            if (prefab != null)
            {
                Debug.Log($"[MVI Editor] Saved instrument prefab to: {path}");
                EditorGUIUtility.PingObject(prefab);
            }
        }
        
        // ===== SCENE VIEW GIZMOS AND HANDLES =====
        
        private void OnSceneGUI()
        {
            if (controller == null) return;
            
            // Draw semicircular arc visualization
            DrawArcVisualization();
            
            if (semicircularLayout == null || controller.transform.childCount == 0)
                return;
            
            // Draw gizmos for all stems
            DrawStemGizmos();
            
            // Draw manipulation handles for selected stem
            if (selectedStemIndex >= 0 && selectedStemIndex < controller.transform.childCount)
            {
                DrawStemHandles(selectedStemIndex);
            }
        }
        
        private void DrawArcVisualization()
        {
            if (semicircularLayout == null) return;
            
            Vector3 center = controller.useLocalSpace ? controller.transform.position + controller.layoutCenter : controller.layoutCenter;
            
            // Draw arc radius
            Handles.color = new Color(0.5f, 0.5f, 1f, 0.3f);
            Handles.DrawWireDisc(center + Vector3.up * semicircularLayout.heightOffset, Vector3.up, semicircularLayout.radius);
            
            // Draw arc angle
            float startAngle = semicircularLayout.arcRotation - (semicircularLayout.arcAngle * 0.5f);
            float endAngle = semicircularLayout.arcRotation + (semicircularLayout.arcAngle * 0.5f);
            
            Handles.color = new Color(0f, 1f, 1f, 0.6f);
            Handles.DrawWireArc(center + Vector3.up * semicircularLayout.heightOffset, Vector3.up, 
                Quaternion.Euler(0, startAngle, 0) * Vector3.forward, 
                semicircularLayout.arcAngle, 
                semicircularLayout.radius);
            
            // Draw arc endpoints
            Vector3 startPos = center + Vector3.up * semicircularLayout.heightOffset + 
                Quaternion.Euler(0, startAngle, 0) * Vector3.forward * semicircularLayout.radius;
            Vector3 endPos = center + Vector3.up * semicircularLayout.heightOffset + 
                Quaternion.Euler(0, endAngle, 0) * Vector3.forward * semicircularLayout.radius;
            
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, startPos, Quaternion.identity, 0.1f, EventType.Repaint);
            Handles.SphereHandleCap(0, endPos, Quaternion.identity, 0.1f, EventType.Repaint);
            
            // Draw center point
            Handles.color = Color.white;
            Handles.DrawWireCube(center, Vector3.one * 0.1f);
            Handles.Label(center + Vector3.up * 0.2f, "Layout Center");
        }
        
        private void DrawStemGizmos()
        {
            for (int i = 0; i < controller.transform.childCount; i++)
            {
                Transform stem = controller.transform.GetChild(i);
                StemProcessor processor = stem.GetComponent<StemProcessor>();
                
                if (processor != null && processor.stemData != null)
                {
                    // Draw interaction bounds
                    Handles.color = i == selectedStemIndex ? Color.yellow : processor.stemData.themeColor;
                    Handles.DrawWireCube(stem.position, processor.stemData.cubeSize);
                    
                    // Draw label
                    Handles.Label(stem.position + Vector3.up * (processor.stemData.cubeSize.y * 0.5f + 0.1f), 
                        processor.stemData.stemName);
                }
            }
        }
        
        private void DrawStemHandles(int stemIndex)
        {
            if (semicircularLayout.stemControls == null || stemIndex >= semicircularLayout.stemControls.Length)
                return;
            
            Transform stem = controller.transform.GetChild(stemIndex);
            var stemControl = semicircularLayout.stemControls[stemIndex];
            
            EditorGUI.BeginChangeCheck();
            
            // Position handle (allows free movement)
            Vector3 newPosition = Handles.PositionHandle(stem.position, Handles.RotationHandle(stem.rotation, stem.position));
            
            // Custom slider handles for height
            Handles.color = Color.green;
            Vector3 heightHandlePos = stem.position + Vector3.up * 0.5f;
            float newHeight = Handles.Slider(heightHandlePos, Vector3.up).y - stem.position.y + stemControl.heightAdjustment;
            
            // Custom slider for radius (depth offset)
            Handles.color = Color.blue;
            Vector3 center = controller.useLocalSpace ? controller.transform.position + controller.layoutCenter : controller.layoutCenter;
            Vector3 toStem = (stem.position - center).normalized;
            Vector3 depthHandlePos = stem.position + toStem * 0.3f;
            float newDepth = Vector3.Distance(Handles.Slider(depthHandlePos, toStem), stem.position) - 0.3f + stemControl.depthOffset;
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(semicircularLayout, "Adjust Stem Position");
                Undo.RecordObject(stem, "Adjust Stem Position");
                
                // Update stem control values based on handle movement
                stemControl.heightAdjustment = newHeight;
                stemControl.depthOffset = newDepth;
                
                // Recalculate position from arc parameters
                Vector3[] positions = semicircularLayout.CalculatePositions(controller.transform.childCount, center);
                stem.position = positions[stemIndex];
                
                EditorUtility.SetDirty(semicircularLayout);
                EditorUtility.SetDirty(stem);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            
            // Draw info box
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 250, 120));
            GUILayout.Box($"Stem: {stem.name}\n" +
                         $"Perimeter: {stemControl.perimeterPosition:F2}\n" +
                         $"Height: {stemControl.heightAdjustment:F2}\n" +
                         $"Depth: {stemControl.depthOffset:F2}\n" +
                         $"Rotation: {stemControl.additionalRotation:F1}°",
                         GUILayout.Width(240), GUILayout.Height(110));
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
