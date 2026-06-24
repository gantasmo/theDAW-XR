using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Runtime component for editing stem positions within a layout.
    /// Attach this to individual stems to enable per-stem position control during play mode or edit mode.
    /// </summary>
    [ExecuteInEditMode]
    public class RuntimeLayoutEditor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The ModularSynthController managing this stem")]
        public ModularSynthController controller;
        
        [Tooltip("Index of this stem in the layout")]
        public int stemIndex = 0;
        
        [Header("Position Controls")]
        [Tooltip("Position along the perimeter (0 = start, 1 = end of arc)")]
        [Range(0f, 1f)]
        public float perimeterPosition = 0.5f;
        
        [Tooltip("Additional height offset for this stem")]
        [Range(-2f, 2f)]
        public float heightAdjustment = 0f;
        
        [Tooltip("Forward/backward offset (positive = outward)")]
        [Range(-2f, 2f)]
        public float depthOffset = 0f;
        
        [Tooltip("Additional rotation around Y axis (degrees)")]
        [Range(-180f, 180f)]
        public float additionalRotation = 0f;
        
        [Header("Runtime Options")]
        [Tooltip("Automatically update position when values change")]
        public bool autoUpdate = true;
        
        [Tooltip("Smoothly interpolate to new positions")]
        public bool smoothTransition = true;
        
        [Tooltip("Speed of smooth transitions")]
        [Range(1f, 20f)]
        public float transitionSpeed = 5f;
        
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private bool needsUpdate = false;
        
        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<ModularSynthController>();
            }
            
            // Load initial values from layout if available
            LoadFromLayout();
        }
        
        private void Update()
        {
            if (autoUpdate && needsUpdate)
            {
                UpdatePosition();
                needsUpdate = false;
            }
            
            if (smoothTransition && Vector3.Distance(transform.position, targetPosition) > 0.001f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * transitionSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * transitionSpeed);
            }
        }
        
        private void OnValidate()
        {
            if (autoUpdate)
            {
                needsUpdate = true;
            }
        }
        
        /// <summary>
        /// Load position values from the layout's stem controls
        /// </summary>
        public void LoadFromLayout()
        {
            if (controller == null || !(controller.layoutStrategy is SemicircularLayout layout))
                return;
            
            if (layout.stemControls != null && stemIndex >= 0 && stemIndex < layout.stemControls.Length)
            {
                var control = layout.stemControls[stemIndex];
                perimeterPosition = control.perimeterPosition;
                heightAdjustment = control.heightAdjustment;
                depthOffset = control.depthOffset;
                additionalRotation = control.additionalRotation;
            }
        }
        
        /// <summary>
        /// Save current values back to the layout's stem controls
        /// </summary>
        public void SaveToLayout()
        {
            if (controller == null || !(controller.layoutStrategy is SemicircularLayout layout))
                return;
            
            // Ensure controls array is initialized
            if (layout.stemControls == null || layout.stemControls.Length != controller.transform.childCount)
            {
                var newControls = new SemicircularLayout.StemPositionControl[controller.transform.childCount];
                
                // Copy existing if available
                if (layout.stemControls != null)
                {
                    int copyCount = Mathf.Min(layout.stemControls.Length, newControls.Length);
                    System.Array.Copy(layout.stemControls, newControls, copyCount);
                }
                
                // Initialize new ones
                for (int i = (layout.stemControls?.Length ?? 0); i < newControls.Length; i++)
                {
                    newControls[i] = new SemicircularLayout.StemPositionControl
                    {
                        perimeterPosition = controller.transform.childCount > 1 ? (float)i / (controller.transform.childCount - 1) : 0.5f,
                        heightAdjustment = 0f,
                        depthOffset = 0f,
                        additionalRotation = 0f
                    };
                }
                
                layout.stemControls = newControls;
            }
            
            if (stemIndex >= 0 && stemIndex < layout.stemControls.Length)
            {
                layout.stemControls[stemIndex].perimeterPosition = perimeterPosition;
                layout.stemControls[stemIndex].heightAdjustment = heightAdjustment;
                layout.stemControls[stemIndex].depthOffset = depthOffset;
                layout.stemControls[stemIndex].additionalRotation = additionalRotation;
            }
        }
        
        /// <summary>
        /// Update this stem's position based on current control values
        /// </summary>
        public void UpdatePosition()
        {
            if (controller == null || !(controller.layoutStrategy is SemicircularLayout layout))
                return;
            
            // Save current values to layout
            SaveToLayout();
            
            // Recalculate position
            Vector3 center = controller.useLocalSpace ? controller.transform.position + controller.layoutCenter : controller.layoutCenter;
            Vector3[] positions = layout.CalculatePositions(controller.transform.childCount, center);
            Quaternion[] rotations = layout.CalculateRotations(controller.transform.childCount, center);
            
            if (stemIndex >= 0 && stemIndex < positions.Length)
            {
                targetPosition = positions[stemIndex];
                targetRotation = rotations[stemIndex];
                
                if (!smoothTransition)
                {
                    transform.position = targetPosition;
                    transform.rotation = targetRotation;
                }
            }
        }
        
        /// <summary>
        /// Reset position to default based on even distribution
        /// </summary>
        public void ResetToDefault()
        {
            int totalStems = controller.transform.childCount;
            perimeterPosition = totalStems > 1 ? (float)stemIndex / (totalStems - 1) : 0.5f;
            heightAdjustment = 0f;
            depthOffset = 0f;
            additionalRotation = 0f;
            
            UpdatePosition();
        }
    }
}
