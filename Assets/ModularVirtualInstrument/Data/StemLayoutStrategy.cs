using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Base class for layout strategies that position stems in 3D space.
    /// </summary>
    public abstract class StemLayoutStrategy : ScriptableObject
    {
        [Header("Layout Settings")]
        public string layoutName = "Layout";
        [TextArea(2, 3)]
        public string description;
        
        /// <summary>
        /// Calculate positions for all stems
        /// </summary>
        /// <param name="stemCount">Number of stems to position</param>
        /// <param name="centerPoint">Center point of the layout</param>
        /// <returns>Array of positions, one for each stem</returns>
        public abstract Vector3[] CalculatePositions(int stemCount, Vector3 centerPoint);
        
        /// <summary>
        /// Calculate rotations for all stems (optional override)
        /// </summary>
        /// <param name="stemCount">Number of stems to position</param>
        /// <param name="centerPoint">Center point of the layout</param>
        /// <returns>Array of rotations, one for each stem</returns>
        public virtual Quaternion[] CalculateRotations(int stemCount, Vector3 centerPoint)
        {
            Quaternion[] rotations = new Quaternion[stemCount];
            for (int i = 0; i < stemCount; i++)
            {
                rotations[i] = Quaternion.identity;
            }
            return rotations;
        }
        
        /// <summary>
        /// Optional: Draw gizmos to visualize the layout in editor
        /// </summary>
        public virtual void DrawLayoutGizmos(int stemCount, Vector3 centerPoint)
        {
            // Override in derived classes
        }
    }
}
