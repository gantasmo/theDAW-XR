using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Arranges stems in a semicircle with adjustable height, perimeter position, and depth.
    /// Each stem can be positioned independently along the arc with forward/backward offset.
    /// </summary>
    [CreateAssetMenu(fileName = "SemicircularLayout", menuName = "Modular Virtual Instrument/Layout/Semicircular Layout")]
    public class SemicircularLayout : StemLayoutStrategy
    {
        [Header("Semicircle Settings")]
        [Tooltip("Radius of the semicircle")]
        [Range(0.5f, 5f)]
        public float radius = 2f;
        
        [Tooltip("Radius multiplier for tighter/looser layouts")]
        [Range(0.1f, 2f)]
        public float radiusMultiplier = 1f;
        
        [Tooltip("Height offset for all stems")]
        [Range(-2f, 2f)]
        public float heightOffset = 0f;
        
        [Tooltip("Rotation of the entire semicircle around the center (degrees)")]
        [Range(0f, 360f)]
        public float arcRotation = 0f;
        
        [Tooltip("Arc angle in degrees (180 = semicircle, 360 = full circle)")]
        [Range(30f, 360f)]
        public float arcAngle = 180f;
        
        [Header("Per-Stem Controls")]
        [Tooltip("Individual position adjustments for each stem")]
        public StemPositionControl[] stemControls = new StemPositionControl[0];
        
        [Header("Auto-Configuration")]
        [Tooltip("Automatically create stem controls when stem count changes")]
        public bool autoCreateControls = true;
        
        [Tooltip("Face stems toward center point")]
        public bool faceCenter = true;
        
        /// <summary>
        /// Per-stem position control
        /// </summary>
        [System.Serializable]
        public class StemPositionControl
        {
            [Header("Position on Arc")]
            [Tooltip("Position along the perimeter (0 = start, 1 = end of arc)")]
            [Range(0f, 1f)]
            public float perimeterPosition = 0.5f;
            
            [Header("Height Adjustment")]
            [Tooltip("Additional height offset for this stem")]
            [Range(-2f, 2f)]
            public float heightAdjustment = 0f;
            
            [Header("Depth Control")]
            [Tooltip("Forward/backward offset relative to the arc position (positive = forward/outward)")]
            [Range(-2f, 2f)]
            public float depthOffset = 0f;
            
            [Header("Individual Rotation")]
            [Tooltip("Additional rotation around Y axis (degrees)")]
            [Range(-180f, 180f)]
            public float additionalRotation = 0f;
        }
        
        public override Vector3[] CalculatePositions(int stemCount, Vector3 centerPoint)
        {
            // Auto-create controls if needed
            if (autoCreateControls && (stemControls == null || stemControls.Length != stemCount))
            {
                InitializeStemControls(stemCount);
            }
            
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                positions[i] = CalculateStemPosition(i, stemCount, centerPoint);
            }
            
            return positions;
        }
        
        public override Quaternion[] CalculateRotations(int stemCount, Vector3 centerPoint)
        {
            Quaternion[] rotations = new Quaternion[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                rotations[i] = CalculateStemRotation(i, stemCount, centerPoint);
            }
            
            return rotations;
        }
        
        /// <summary>
        /// Calculate position for a single stem
        /// </summary>
        private Vector3 CalculateStemPosition(int stemIndex, int totalStems, Vector3 centerPoint)
        {
            // Get stem control or use defaults
            StemPositionControl control = GetStemControl(stemIndex);
            
            // Calculate angle on the arc
            float normalizedPosition = control.perimeterPosition;
            
            // If no custom controls, distribute evenly
            if (stemControls == null || stemControls.Length == 0)
            {
                normalizedPosition = totalStems > 1 ? (float)stemIndex / (totalStems - 1) : 0.5f;
            }
            
            // Convert normalized position to angle
            float startAngle = -arcAngle / 2f;
            float angle = startAngle + (normalizedPosition * arcAngle);
            
            // Apply arc rotation
            angle += arcRotation;
            
            // Convert to radians
            float angleRad = angle * Mathf.Deg2Rad;
            
            // Calculate base position on arc
            float effectiveRadius = radius * radiusMultiplier;
            float x = Mathf.Sin(angleRad) * effectiveRadius;
            float z = Mathf.Cos(angleRad) * effectiveRadius;
            
            // Apply depth offset (radial offset)
            float radialOffset = effectiveRadius + control.depthOffset;
            x = Mathf.Sin(angleRad) * radialOffset;
            z = Mathf.Cos(angleRad) * radialOffset;
            
            // Apply height
            float y = heightOffset + control.heightAdjustment;
            
            // Combine with center point
            return centerPoint + new Vector3(x, y, z);
        }
        
        /// <summary>
        /// Calculate rotation for a single stem
        /// </summary>
        private Quaternion CalculateStemRotation(int stemIndex, int totalStems, Vector3 centerPoint)
        {
            StemPositionControl stemControl = GetStemControl(stemIndex);
            
            if (!faceCenter)
            {
                return Quaternion.Euler(0, stemControl.additionalRotation, 0);
            }
            
            // Calculate position to determine look direction
            Vector3 stemPosition = CalculateStemPosition(stemIndex, totalStems, centerPoint);
            Vector3 directionToCenter = (centerPoint - stemPosition).normalized;
            directionToCenter.y = 0; // Keep rotation horizontal
            
            // Calculate base rotation to face center
            Quaternion baseRotation = Quaternion.LookRotation(directionToCenter);
            
            // Apply additional rotation if specified
            Quaternion additionalRot = Quaternion.Euler(0, stemControl.additionalRotation, 0);
            
            return baseRotation * additionalRot;
        }
        
        /// <summary>
        /// Get stem control for index, or return default
        /// </summary>
        private StemPositionControl GetStemControl(int index)
        {
            if (stemControls != null && index >= 0 && index < stemControls.Length)
            {
                return stemControls[index];
            }
            
            // Return default control
            return new StemPositionControl
            {
                perimeterPosition = 0.5f,
                heightAdjustment = 0f,
                depthOffset = 0f,
                additionalRotation = 0f
            };
        }
        
        /// <summary>
        /// Initialize stem controls with default values
        /// </summary>
        private void InitializeStemControls(int count)
        {
            stemControls = new StemPositionControl[count];
            
            for (int i = 0; i < count; i++)
            {
                float normalizedPos = count > 1 ? (float)i / (count - 1) : 0.5f;
                
                stemControls[i] = new StemPositionControl
                {
                    perimeterPosition = normalizedPos,
                    heightAdjustment = 0f,
                    depthOffset = 0f,
                    additionalRotation = 0f
                };
            }
        }
        
        public override void DrawLayoutGizmos(int stemCount, Vector3 centerPoint)
        {
            if (stemCount == 0) return;
            
            // Draw semicircle arc
            int arcSegments = 32;
            Vector3 previousPoint = Vector3.zero;
            
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments;
                float angle = (-arcAngle / 2f + t * arcAngle + arcRotation) * Mathf.Deg2Rad;
                
                float x = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                float y = heightOffset;
                
                Vector3 point = centerPoint + new Vector3(x, y, z);
                
                if (i > 0)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(previousPoint, point);
                }
                
                previousPoint = point;
            }
            
            // Draw stem positions
            Vector3[] positions = CalculatePositions(stemCount, centerPoint);
            
            for (int i = 0; i < positions.Length; i++)
            {
                // Draw position marker
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(positions[i], 0.1f);
                
                // Draw line to center
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(positions[i], centerPoint + Vector3.up * heightOffset);
            }
            
            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(centerPoint, 0.05f);
        }
    }
}
