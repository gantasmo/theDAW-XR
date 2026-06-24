using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections.Generic;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Manages hand tracking for multiple stems.
    /// Allows any hand to control any stem with automatic detection and assignment.
    /// </summary>
    public class UnifiedHandTrackingManager : MonoBehaviour
    {
        [Header("Hand Tracking")]
        [SerializeField] private Hand leftHand;
        [SerializeField] private Hand rightHand;
        
        [Header("Controller Reference")]
        [SerializeField] private ModularSynthController synthController;
        
        [Header("Tracking Settings")]
        [Tooltip("Allow both hands to control different stems simultaneously")]
        public bool allowDualHandControl = true;
        
        [Tooltip("Allow one hand to control multiple stems if they overlap")]
        public bool allowMultiStemControl = false;
        
        [Tooltip("Hand tracking update rate (Hz)")]
        [Range(30f, 90f)]
        public float updateRate = 60f;
        
        [Header("Hand Assignment")]
        [Tooltip("Minimum time hand must be in cube before assignment (seconds)")]
        [Range(0f, 1f)]
        public float assignmentDelay = 0.1f;
        
        [Header("Freeze Gesture")]
        [Tooltip("Enable palm-up gesture to freeze effect values")]
        public bool enableFreezeGesture = true;
        
        [Tooltip("Minimum palm-up angle to trigger freeze (degrees from horizontal)")]
        [Range(45f, 90f)]
        public float freezeAngleThreshold = 70f;
        
        [Tooltip("Time palm must be held up to freeze/unfreeze (seconds)")]
        [Range(0.1f, 2f)]
        public float freezeHoldTime = 0.5f;
        
        [Header("Debug")]
        public bool showDebugRays = true;
        public bool logHandEvents = false;
        
        [Header("Diagnostic Info (Read Only)")]
        [SerializeField] private bool leftHandTracked = false;
        [SerializeField] private bool rightHandTracked = false;
        [SerializeField] private float leftPalmAngle = 0f;
        [SerializeField] private float rightPalmAngle = 0f;
        [SerializeField] private bool leftPalmUp = false;
        [SerializeField] private bool rightPalmUp = false;
        
        // Hand tracking state
        private class HandState
        {
            public Hand hand;
            public bool isTracked;
            public Vector3 indexTipPosition;
            public Vector3 palmNormal;
            public float palmUpAngle;
            public StemProcessor assignedStem;
            public float assignmentTimer;
            public float freezeGestureTimer;
            public bool isPalmUp;
            public List<StemProcessor> overlappingStems = new List<StemProcessor>();
        }
        
        private HandState leftHandState = new HandState();
        private HandState rightHandState = new HandState();
        private float updateTimer;
        
        private void Awake()
        {
            // Initialize hand states
            leftHandState.hand = leftHand;
            rightHandState.hand = rightHand;
            
            // Find synth controller if not assigned
            if (synthController == null)
            {
                synthController = FindFirstObjectByType<ModularSynthController>();
            }
        }
        
        private void OnEnable()
        {
            // Subscribe to hand tracking events
            if (leftHand != null)
            {
                leftHand.WhenHandUpdated += OnLeftHandUpdated;
            }
            
            if (rightHand != null)
            {
                rightHand.WhenHandUpdated += OnRightHandUpdated;
            }
        }
        
        private void OnDisable()
        {
            // Unsubscribe from events
            if (leftHand != null)
            {
                leftHand.WhenHandUpdated -= OnLeftHandUpdated;
            }
            
            if (rightHand != null)
            {
                rightHand.WhenHandUpdated -= OnRightHandUpdated;
            }
            
            // Clear assignments
            ClearHandAssignment(leftHandState);
            ClearHandAssignment(rightHandState);
        }
        
        private void Update()
        {
            // Throttle update rate
            updateTimer += Time.deltaTime;
            float updateInterval = 1f / updateRate;
            
            if (updateTimer < updateInterval)
                return;
            
            updateTimer = 0f;
            
            // Update hand tracking
            UpdateHandTracking(leftHandState);
            UpdateHandTracking(rightHandState);
        }
        
        private void OnLeftHandUpdated()
        {
            UpdateHandState(leftHandState);
        }
        
        private void OnRightHandUpdated()
        {
            UpdateHandState(rightHandState);
        }
        
        private void UpdateHandState(HandState state)
        {
            if (state.hand == null) return;
            
            state.isTracked = state.hand.IsTrackedDataValid;
            
            // Update diagnostic fields
            if (state.hand == leftHand)
            {
                leftHandTracked = state.isTracked;
            }
            else if (state.hand == rightHand)
            {
                rightHandTracked = state.isTracked;
            }
            
            if (!state.isTracked) return;
            
            // Get index finger tip position
            if (state.hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexPose))
            {
                state.indexTipPosition = indexPose.position;
            }
            
            // Get palm normal for gesture detection
            if (state.hand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose))
            {
                // Palm normal points from wrist in the direction perpendicular to palm
                state.palmNormal = state.hand.Handedness == Handedness.Left 
                    ? wristPose.rotation * Vector3.left 
                    : wristPose.rotation * Vector3.right;
                
                // Calculate angle from horizontal (palm up = normal points up)
                state.palmUpAngle = Vector3.Angle(state.palmNormal, Vector3.up);
                
                // Update diagnostic fields
                if (state.hand == leftHand)
                {
                    leftPalmAngle = state.palmUpAngle;
                    leftPalmUp = state.palmUpAngle < (90f - freezeAngleThreshold);
                }
                else if (state.hand == rightHand)
                {
                    rightPalmAngle = state.palmUpAngle;
                    rightPalmUp = state.palmUpAngle < (90f - freezeAngleThreshold);
                }
            }
        }
        
        private void UpdateHandTracking(HandState state)
        {
            if (!state.isTracked || synthController == null)
            {
                // Clear assignment if hand lost tracking
                if (state.assignedStem != null)
                {
                    ClearHandAssignment(state);
                }
                return;
            }
            
            // Check for freeze gesture
            if (enableFreezeGesture && state.assignedStem != null)
            {
                UpdateFreezeGesture(state);
            }
            
            // Find overlapping stems (fills the hand's reusable buffer, no allocation)
            FindOverlappingStems(state.indexTipPosition, state.overlappingStems);
            List<StemProcessor> overlapping = state.overlappingStems;

            // Handle assignment
            if (overlapping.Count > 0)
            {
                HandleStemAssignment(state, overlapping);
            }
            else
            {
                // Hand left all cubes
                if (state.assignedStem != null)
                {
                    ClearHandAssignment(state);
                }
            }
            
            // Update assigned stem
            if (state.assignedStem != null)
            {
                state.assignedStem.OnHandUpdate(state.indexTipPosition);
            }
        }
        
        private void FindOverlappingStems(Vector3 handPosition, List<StemProcessor> results)
        {
            results.Clear();

            if (synthController == null) return;

            // Non-allocating read of the live processor list.
            IReadOnlyList<StemProcessor> processors = synthController.ActiveProcessors;

            for (int i = 0; i < processors.Count; i++)
            {
                StemProcessor processor = processors[i];
                if (processor != null && processor.interactionBounds.Contains(handPosition))
                {
                    results.Add(processor);
                }
            }
        }
        
        private void HandleStemAssignment(HandState state, List<StemProcessor> overlapping)
        {
            // Check if already assigned to one of the overlapping stems
            if (state.assignedStem != null && overlapping.Contains(state.assignedStem))
            {
                // Continue with current assignment
                return;
            }
            
            // Need new assignment
            StemProcessor targetStem = SelectStem(overlapping, state);
            
            if (targetStem == null) return;
            
            // Check assignment delay
            state.assignmentTimer += Time.deltaTime;
            
            if (state.assignmentTimer < assignmentDelay)
                return;
            
            // Assign to new stem
            AssignHandToStem(state, targetStem);
        }
        
        private StemProcessor SelectStem(List<StemProcessor> candidates, HandState state)
        {
            // Single pass, no allocation: skip null/unassignable stems and pick the
            // highest priority. Also null-safe on stemData (the old FindAll path would
            // throw if a candidate had no StemData).
            StemProcessor selected = null;
            int highestPriority = int.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                StemProcessor stem = candidates[i];
                if (stem == null || stem.stemData == null) continue;

                // If not allowing multi-stem control, skip stems already held by another hand.
                if (!allowMultiStemControl && IsStemAssigned(stem, state)) continue;

                int priority = stem.stemData.handAssignmentPriority;
                if (selected == null || priority > highestPriority)
                {
                    selected = stem;
                    highestPriority = priority;
                }
            }

            return selected;
        }
        
        private bool IsStemAssigned(StemProcessor stem, HandState excludeHand)
        {
            if (leftHandState != excludeHand && leftHandState.assignedStem == stem)
                return true;
            
            if (rightHandState != excludeHand && rightHandState.assignedStem == stem)
                return true;
            
            return false;
        }
        
        private void AssignHandToStem(HandState state, StemProcessor stem)
        {
            // Clear previous assignment
            if (state.assignedStem != null && state.assignedStem != stem)
            {
                state.assignedStem.OnHandExit();
                
                if (logHandEvents)
                {
                    Debug.Log($"Hand exited: {state.assignedStem.stemData.stemName}");
                }
            }
            
            // Assign to new stem
            state.assignedStem = stem;
            state.assignmentTimer = 0f;
            stem.OnHandEnter(state.indexTipPosition);
            
            if (logHandEvents)
            {
                string handName = state.hand == leftHand ? "Left" : "Right";
                Debug.Log($"{handName} hand entered: {stem.stemData.stemName}");
            }
        }
        
        private void ClearHandAssignment(HandState state)
        {
            if (state.assignedStem != null)
            {
                state.assignedStem.OnHandExit();
                
                if (logHandEvents)
                {
                    Debug.Log($"Hand exited: {state.assignedStem.stemData.stemName}");
                }
                
                state.assignedStem = null;
            }
            
            state.assignmentTimer = 0f;
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugRays) return;
            
            // Draw left hand position and ray
            if (leftHandState != null && leftHandState.isTracked)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(leftHandState.indexTipPosition, 0.02f);
                
                if (leftHandState.assignedStem != null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(leftHandState.indexTipPosition, leftHandState.assignedStem.transform.position);
                }
            }
            
            // Draw right hand position and ray
            if (rightHandState != null && rightHandState.isTracked)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(rightHandState.indexTipPosition, 0.02f);
                
                if (rightHandState.assignedStem != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(rightHandState.indexTipPosition, rightHandState.assignedStem.transform.position);
                }
            }
        }
        
        private void UpdateFreezeGesture(HandState state)
        {
            if (state.assignedStem == null) return;
            
            // Check if palm is facing up
            bool palmUpNow = state.palmUpAngle < (90f - freezeAngleThreshold);
            
            if (palmUpNow && !state.isPalmUp)
            {
                // Palm just went up - start timer
                state.isPalmUp = true;
                state.freezeGestureTimer = 0f;
            }
            else if (!palmUpNow && state.isPalmUp)
            {
                // Palm went down - reset
                state.isPalmUp = false;
                state.freezeGestureTimer = 0f;
            }
            else if (palmUpNow)
            {
                // Palm held up - increment timer
                state.freezeGestureTimer += Time.deltaTime;
                
                if (state.freezeGestureTimer >= freezeHoldTime)
                {
                    // Toggle freeze state
                    bool currentlyFrozen = state.assignedStem.IsFrozen();
                    state.assignedStem.SetFrozen(!currentlyFrozen);
                    
                    if (logHandEvents)
                    {
                        string handName = state.hand == leftHand ? "Left" : "Right";
                        Debug.Log($"{handName} hand {(!currentlyFrozen ? "froze" : "unfroze")} stem: {state.assignedStem.stemData.stemName}");
                    }
                    
                    // Reset timer to prevent repeated toggles
                    state.freezeGestureTimer = 0f;
                    state.isPalmUp = false;
                }
            }
        }
        
        #region Debug Methods
        
        /// <summary>
        /// Get diagnostic info for debugging
        /// </summary>
        public string GetDiagnosticInfo()
        {
            string info = "=== Hand Tracking Diagnostic ===\n";
            info += $"Left Hand: {(leftHand != null ? "Connected" : "Missing")}, Tracked: {leftHandTracked}, PalmAngle: {leftPalmAngle:F1}°, PalmUp: {leftPalmUp}\n";
            info += $"Right Hand: {(rightHand != null ? "Connected" : "Missing")}, Tracked: {rightHandTracked}, PalmAngle: {rightPalmAngle:F1}°, PalmUp: {rightPalmUp}\n";
            info += $"Freeze Gesture: {(enableFreezeGesture ? "Enabled" : "Disabled")}, Threshold: {freezeAngleThreshold}°, Hold Time: {freezeHoldTime}s\n";
            
            if (leftHandState.assignedStem != null)
            {
                info += $"Left Hand -> Stem: {leftHandState.assignedStem.stemData.stemName}, Frozen: {leftHandState.assignedStem.IsFrozen()}\n";
            }
            
            if (rightHandState.assignedStem != null)
            {
                info += $"Right Hand -> Stem: {rightHandState.assignedStem.stemData.stemName}, Frozen: {rightHandState.assignedStem.IsFrozen()}\n";
            }
            
            return info;
        }
        
        /// <summary>
        /// Force enable debug logging
        /// </summary>
        [ContextMenu("Enable Debug Logging")]
        public void EnableDebugLogging()
        {
            logHandEvents = true;
            Debug.Log("Hand tracking debug logging enabled");
        }
        
        /// <summary>
        /// Test freeze gesture manually
        /// </summary>
        [ContextMenu("Test Freeze Gesture")]
        public void TestFreezeGesture()
        {
            if (leftHandState.assignedStem != null)
            {
                bool currentState = leftHandState.assignedStem.IsFrozen();
                leftHandState.assignedStem.SetFrozen(!currentState);
                Debug.Log($"Left hand stem freeze toggled: {currentState} -> {!currentState}");
            }
            
            if (rightHandState.assignedStem != null)
            {
                bool currentState = rightHandState.assignedStem.IsFrozen();
                rightHandState.assignedStem.SetFrozen(!currentState);
                Debug.Log($"Right hand stem freeze toggled: {currentState} -> {!currentState}");
            }
        }
        
        #endregion
    }
}
