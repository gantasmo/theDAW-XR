using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Allows switching between different layout strategies at runtime with smooth transitions.
    /// Useful for creating dynamic instrument configurations or responding to user input.
    /// </summary>
    [RequireComponent(typeof(ModularSynthController))]
    public class LayoutSwitcher : MonoBehaviour
    {
        [Header("Available Layouts")]
        [Tooltip("List of layout strategies to switch between")]
        public StemLayoutStrategy[] availableLayouts = new StemLayoutStrategy[0];
        
        [Header("Switching Options")]
        [Tooltip("Current active layout index")]
        [SerializeField]
        private int currentLayoutIndex = 0;
        
        [Tooltip("Enable smooth transitions when switching")]
        public bool smoothTransitions = true;
        
        [Tooltip("Duration of transition animation (seconds)")]
        [Range(0.1f, 5f)]
        public float transitionDuration = 1f;
        
        [Tooltip("Easing curve for transitions")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Auto-Switch Settings")]
        [Tooltip("Automatically cycle through layouts")]
        public bool autoSwitch = false;
        
        [Tooltip("Time between auto-switches (seconds)")]
        [Range(1f, 60f)]
        public float autoSwitchInterval = 5f;
        
        [Header("Input Bindings")]
        [Tooltip("Key to switch to next layout")]
        public KeyCode nextLayoutKey = KeyCode.RightArrow;
        
        [Tooltip("Key to switch to previous layout")]
        public KeyCode previousLayoutKey = KeyCode.LeftArrow;
        
        [Tooltip("Enable keyboard input")]
        public bool enableKeyboardInput = true;
        
        private ModularSynthController controller;
        private bool isTransitioning = false;
        private float autoSwitchTimer = 0f;
#if MVI_FLEXALON
        private LayoutAnimationController animationController;
#endif
        
        // For smooth transitions
        private class StemTransitionData
        {
            public Transform stem;
            public Vector3 startPosition;
            public Quaternion startRotation;
            public Vector3 targetPosition;
            public Quaternion targetRotation;
        }
        
        private List<StemTransitionData> currentTransition = new List<StemTransitionData>();
        
        public int CurrentLayoutIndex
        {
            get => currentLayoutIndex;
            set
            {
                if (value >= 0 && value < availableLayouts.Length)
                {
                    SwitchToLayout(value);
                }
            }
        }
        
        public StemLayoutStrategy CurrentLayout
        {
            get
            {
                if (currentLayoutIndex >= 0 && currentLayoutIndex < availableLayouts.Length)
                {
                    return availableLayouts[currentLayoutIndex];
                }
                return null;
            }
        }
        
        private void OnEnable()
        {
            controller = GetComponent<ModularSynthController>();
#if MVI_FLEXALON
            animationController = GetComponent<LayoutAnimationController>();
#endif
            
            // Set initial layout
            if (availableLayouts.Length > 0 && currentLayoutIndex < availableLayouts.Length)
            {
                controller.layoutStrategy = availableLayouts[currentLayoutIndex];
            }
        }
        
        private void Update()
        {
            // Handle keyboard input
            if (enableKeyboardInput && !isTransitioning)
            {
                if (Input.GetKeyDown(nextLayoutKey))
                {
                    NextLayout();
                }
                else if (Input.GetKeyDown(previousLayoutKey))
                {
                    PreviousLayout();
                }
            }
            
            // Handle auto-switch
            if (autoSwitch && !isTransitioning)
            {
                autoSwitchTimer += Time.deltaTime;
                if (autoSwitchTimer >= autoSwitchInterval)
                {
                    autoSwitchTimer = 0f;
                    NextLayout();
                }
            }
        }
        
        /// <summary>
        /// Switch to the next layout in the list
        /// </summary>
        public void NextLayout()
        {
            if (availableLayouts.Length == 0) return;
            
            int nextIndex = (currentLayoutIndex + 1) % availableLayouts.Length;
            SwitchToLayout(nextIndex);
        }
        
        /// <summary>
        /// Switch to the previous layout in the list
        /// </summary>
        public void PreviousLayout()
        {
            if (availableLayouts.Length == 0) return;
            
            int prevIndex = currentLayoutIndex - 1;
            if (prevIndex < 0) prevIndex = availableLayouts.Length - 1;
            SwitchToLayout(prevIndex);
        }
        
        /// <summary>
        /// Switch to a specific layout by index
        /// </summary>
        public void SwitchToLayout(int index)
        {
            if (index < 0 || index >= availableLayouts.Length)
            {
                Debug.LogWarning($"[LayoutSwitcher] Invalid layout index: {index}");
                return;
            }
            
            if (index == currentLayoutIndex && controller.layoutStrategy == availableLayouts[index])
            {
                return; // Already on this layout
            }
            
            if (isTransitioning)
            {
                StopAllCoroutines();
                isTransitioning = false;
            }
            
            currentLayoutIndex = index;
            StemLayoutStrategy newLayout = availableLayouts[index];
            
            if (smoothTransitions && Application.isPlaying)
            {
                StartCoroutine(TransitionToLayout(newLayout));
            }
            else
            {
                // Instant switch
                controller.layoutStrategy = newLayout;
                controller.RegenerateLayout();
            }
            
            Debug.Log($"[LayoutSwitcher] Switched to layout: {newLayout.layoutName} ({index})");
        }
        
        /// <summary>
        /// Switch to a specific layout by name
        /// </summary>
        public void SwitchToLayoutByName(string layoutName)
        {
            for (int i = 0; i < availableLayouts.Length; i++)
            {
                if (availableLayouts[i].layoutName == layoutName)
                {
                    SwitchToLayout(i);
                    return;
                }
            }
            
            Debug.LogWarning($"[LayoutSwitcher] Layout not found: {layoutName}");
        }
        
        private IEnumerator TransitionToLayout(StemLayoutStrategy newLayout)
        {
            isTransitioning = true;
            
#if MVI_FLEXALON
            // Temporarily disable Flexalon animators to control transition manually
            bool hadAnimations = false;
            if (animationController != null && animationController.enableAnimations)
            {
                hadAnimations = true;
                animationController.DisableAnimations();
            }
#endif
            
            // Capture current positions
            currentTransition.Clear();
            Vector3 center = controller.useLocalSpace ? controller.transform.position + controller.layoutCenter : controller.layoutCenter;
            
            foreach (Transform child in controller.transform)
            {
                currentTransition.Add(new StemTransitionData
                {
                    stem = child,
                    startPosition = child.position,
                    startRotation = child.rotation
                });
            }
            
            // Calculate target positions with new layout
            controller.layoutStrategy = newLayout;
            Vector3[] targetPositions = newLayout.CalculatePositions(controller.transform.childCount, center);
            Quaternion[] targetRotations = newLayout.CalculateRotations(controller.transform.childCount, center);
            
            // Assign targets
            for (int i = 0; i < currentTransition.Count && i < targetPositions.Length; i++)
            {
                currentTransition[i].targetPosition = targetPositions[i];
                currentTransition[i].targetRotation = targetRotations[i];
            }
            
            // Animate transition
            float elapsed = 0f;
            
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                float curveValue = transitionCurve.Evaluate(t);
                
                foreach (var data in currentTransition)
                {
                    data.stem.position = Vector3.Lerp(data.startPosition, data.targetPosition, curveValue);
                    data.stem.rotation = Quaternion.Slerp(data.startRotation, data.targetRotation, curveValue);
                }
                
                yield return null;
            }
            
            // Ensure final positions are exact
            for (int i = 0; i < currentTransition.Count && i < targetPositions.Length; i++)
            {
                currentTransition[i].stem.position = targetPositions[i];
                currentTransition[i].stem.rotation = targetRotations[i];
            }
            
#if MVI_FLEXALON
            // Re-enable animations if they were active
            if (hadAnimations && animationController != null)
            {
                animationController.EnableAnimations();
            }
#endif
            
            isTransitioning = false;
            currentTransition.Clear();
        }
        
        /// <summary>
        /// Add a new layout to the available layouts at runtime
        /// </summary>
        public void AddLayout(StemLayoutStrategy layout)
        {
            var newLayouts = new StemLayoutStrategy[availableLayouts.Length + 1];
            availableLayouts.CopyTo(newLayouts, 0);
            newLayouts[availableLayouts.Length] = layout;
            availableLayouts = newLayouts;
        }
        
        /// <summary>
        /// Remove a layout from available layouts
        /// </summary>
        public void RemoveLayout(int index)
        {
            if (index < 0 || index >= availableLayouts.Length) return;
            
            var newLayouts = new StemLayoutStrategy[availableLayouts.Length - 1];
            int writeIndex = 0;
            
            for (int i = 0; i < availableLayouts.Length; i++)
            {
                if (i != index)
                {
                    newLayouts[writeIndex++] = availableLayouts[i];
                }
            }
            
            availableLayouts = newLayouts;
            
            // Adjust current index if needed
            if (currentLayoutIndex >= availableLayouts.Length)
            {
                currentLayoutIndex = Mathf.Max(0, availableLayouts.Length - 1);
            }
        }
        
        /// <summary>
        /// Get names of all available layouts
        /// </summary>
        public string[] GetLayoutNames()
        {
            string[] names = new string[availableLayouts.Length];
            for (int i = 0; i < availableLayouts.Length; i++)
            {
                names[i] = availableLayouts[i] != null ? availableLayouts[i].layoutName : "Null";
            }
            return names;
        }
    }
}
