using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using ModularVirtualInstrument;

namespace ModularVirtualInstrument.Debugging
{
    /// <summary>
    /// Debug tool to diagnose wireframe visibility and freeze gesture issues
    /// </summary>
    public class MVIDebugTool : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("Show detailed debug logs")]
        public bool enableDebugLogs = true;
        
        [Tooltip("Show wireframe debug info")]
        public bool debugWireframes = true;
        
        [Tooltip("Show freeze gesture debug info")]  
        public bool debugFreezeGesture = true;
        
        [Tooltip("Show hand tracking debug info")]
        public bool debugHandTracking = true;

        [Header("Manual Testing")]
        [Tooltip("Force toggle all wireframes")]
        public bool forceToggleWireframes = false;
        
        [Tooltip("Test freeze gesture without hand tracking")]
        public bool testFreezeGesture = false;

        private ModularSynthController synthController;
        private UnifiedHandTrackingManager handTracker;
        private List<StemVisualizer> visualizers = new List<StemVisualizer>();
        private List<StemProcessor> processors = new List<StemProcessor>();
        
        void Start()
        {
            // Find components
            synthController = FindFirstObjectByType<ModularSynthController>();
            handTracker = FindFirstObjectByType<UnifiedHandTrackingManager>();
            
            // Find all visualizers and processors
            visualizers.AddRange(FindObjectsByType<StemVisualizer>(FindObjectsSortMode.None));
            processors.AddRange(FindObjectsByType<StemProcessor>(FindObjectsSortMode.None));
            
            if (enableDebugLogs)
            {
                LogSystemStatus();
            }
        }
        
        void Update()
        {
            // Manual wireframe toggle test
            if (forceToggleWireframes)
            {
                forceToggleWireframes = false;
                TestWireframeToggle();
            }
            
            // Manual freeze gesture test
            if (testFreezeGesture)
            {
                testFreezeGesture = false;
                TestFreezeGesture();
            }
            
            // Continuous debug logging
            if (debugHandTracking && handTracker != null)
            {
                DebugHandTracking();
            }
            
            if (debugWireframes)
            {
                DebugWireframes();
            }
        }
        
        private void LogSystemStatus()
        {
            Debug.Log("=== MVI DEBUG TOOL - SYSTEM STATUS ===");
            
            // Core components
            Debug.Log($"ModularSynthController: {(synthController != null ? "FOUND" : "MISSING")}");
            Debug.Log($"UnifiedHandTrackingManager: {(handTracker != null ? "FOUND" : "MISSING")}");
            Debug.Log($"StemVisualizers found: {visualizers.Count}");
            Debug.Log($"StemProcessors found: {processors.Count}");
            
            // Hand tracking setup
            if (handTracker != null)
            {
                Debug.Log($"Freeze Gesture Enabled: {handTracker.enableFreezeGesture}");
                Debug.Log($"Freeze Angle Threshold: {handTracker.freezeAngleThreshold}°");
                Debug.Log($"Freeze Hold Time: {handTracker.freezeHoldTime}s");
                Debug.Log($"Hand Events Logging: {handTracker.logHandEvents}");
                
                // Check hand references
                var leftHandField = typeof(UnifiedHandTrackingManager).GetField("leftHand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rightHandField = typeof(UnifiedHandTrackingManager).GetField("rightHand", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (leftHandField != null && rightHandField != null)
                {
                    var leftHand = leftHandField.GetValue(handTracker) as Hand;
                    var rightHand = rightHandField.GetValue(handTracker) as Hand;
                    
                    Debug.Log($"Left Hand Reference: {(leftHand != null ? "SET" : "MISSING")}");
                    Debug.Log($"Right Hand Reference: {(rightHand != null ? "SET" : "MISSING")}");
                }
            }
            
            // Wireframe status
            foreach (var viz in visualizers)
            {
                if (viz != null)
                {
                    Debug.Log($"StemVisualizer '{viz.name}': showWireframe={viz.showWireframe}, showAxisIndicators={viz.showAxisIndicators}");
                }
            }
            
            Debug.Log("=== END SYSTEM STATUS ===");
        }
        
        private void TestWireframeToggle()
        {
            Debug.Log("=== TESTING WIREFRAME TOGGLE ===");
            
            foreach (var viz in visualizers)
            {
                if (viz != null)
                {
                    bool newState = !viz.showWireframe;
                    Debug.Log($"Toggling {viz.name} wireframe: {viz.showWireframe} -> {newState}");
                    
                    viz.SetWireframeVisible(newState);
                    
                    // Verify the change took effect
                    if (viz.showWireframe == newState)
                    {
                        Debug.Log($"✓ Wireframe toggle successful for {viz.name}");
                    }
                    else
                    {
                        Debug.LogError($"✗ Wireframe toggle FAILED for {viz.name}");
                    }
                }
            }
        }
        
        private void TestFreezeGesture()
        {
            Debug.Log("=== TESTING FREEZE GESTURE ===");
            
            foreach (var processor in processors)
            {
                if (processor != null)
                {
                    bool currentState = processor.IsFrozen();
                    bool newState = !currentState;
                    
                    Debug.Log($"Testing freeze toggle on {processor.name}: {currentState} -> {newState}");
                    processor.SetFrozen(newState);
                    
                    // Verify the change
                    if (processor.IsFrozen() == newState)
                    {
                        Debug.Log($"✓ Freeze toggle successful for {processor.name}");
                    }
                    else
                    {
                        Debug.LogError($"✗ Freeze toggle FAILED for {processor.name}");
                    }
                }
            }
        }
        
        private void DebugHandTracking()
        {
            if (handTracker == null) return;
            
            // Get hand state info using reflection
            var leftHandStateField = typeof(UnifiedHandTrackingManager).GetField("leftHandState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rightHandStateField = typeof(UnifiedHandTrackingManager).GetField("rightHandState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (leftHandStateField != null && rightHandStateField != null)
            {
                var leftState = leftHandStateField.GetValue(handTracker);
                var rightState = rightHandStateField.GetValue(handTracker);
                
                if (leftState != null)
                {
                    LogHandState("Left", leftState);
                }
                
                if (rightState != null)
                {
                    LogHandState("Right", rightState);
                }
            }
        }
        
        private void LogHandState(string handName, object handState)
        {
            var handStateType = handState.GetType();
            
            try
            {
                var isTrackedField = handStateType.GetField("isTracked");
                var palmUpAngleField = handStateType.GetField("palmUpAngle");
                var isPalmUpField = handStateType.GetField("isPalmUp");
                var freezeTimerField = handStateType.GetField("freezeGestureTimer");
                var assignedStemField = handStateType.GetField("assignedStem");
                
                if (isTrackedField != null && (bool)isTrackedField.GetValue(handState))
                {
                    var palmUpAngle = palmUpAngleField?.GetValue(handState);
                    var isPalmUp = isPalmUpField?.GetValue(handState);
                    var freezeTimer = freezeTimerField?.GetValue(handState);
                    var assignedStem = assignedStemField?.GetValue(handState);
                    
                    Debug.Log($"{handName} Hand - Tracked: True, PalmAngle: {palmUpAngle}°, PalmUp: {isPalmUp}, FreezeTimer: {freezeTimer}, AssignedStem: {(assignedStem != null ? "Yes" : "No")}");
                }
                else
                {
                    Debug.Log($"{handName} Hand - Tracked: False");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading hand state: {e.Message}");
            }
        }
        
        private void DebugWireframes()
        {
            foreach (var viz in visualizers)
            {
                if (viz != null)
                {
                    // Check if wireframe components exist and are properly set up. The cube
                    // is now a single LineRenderer (was an array of 12).
                    var wireframeCubeField = typeof(StemVisualizer).GetField("wireframeCube", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var axisLinesField = typeof(StemVisualizer).GetField("axisLines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (wireframeCubeField != null && axisLinesField != null)
                    {
                        var wireframeCube = wireframeCubeField.GetValue(viz) as LineRenderer;
                        var axisLines = axisLinesField.GetValue(viz) as LineRenderer[];

                        if (wireframeCube != null)
                        {
                            bool active = wireframeCube.gameObject.activeSelf;
                            Debug.Log($"{viz.name} - Wireframe cube active: {active}, ShowWireframe: {viz.showWireframe}");
                        }
                        
                        if (axisLines != null)
                        {
                            int activeAxis = 0;
                            foreach (var line in axisLines)
                            {
                                if (line != null && line.gameObject.activeSelf) activeAxis++;
                            }
                            Debug.Log($"{viz.name} - Axis Lines: {activeAxis}/{axisLines.Length} active, ShowAxisIndicators: {viz.showAxisIndicators}");
                        }
                    }
                }
            }
        }
        
        [ContextMenu("Force Wireframe Refresh")]
        public void ForceWireframeRefresh()
        {
            Debug.Log("=== FORCING WIREFRAME REFRESH ===");
            
            foreach (var viz in visualizers)
            {
                if (viz != null)
                {
                    viz.SetWireframeVisible(viz.showWireframe);
                    Debug.Log($"Refreshed wireframes for {viz.name}");
                }
            }
        }
        
        [ContextMenu("Enable Freeze Gesture Debug")]
        public void EnableFreezeGestureDebug()
        {
            if (handTracker != null)
            {
                handTracker.logHandEvents = true;
                handTracker.enableFreezeGesture = true;
                Debug.Log("Enabled freeze gesture and hand event logging");
            }
        }
        
        [ContextMenu("Test All Systems")]
        public void TestAllSystems()
        {
            LogSystemStatus();
            TestWireframeToggle();
            TestFreezeGesture();
        }
    }
}