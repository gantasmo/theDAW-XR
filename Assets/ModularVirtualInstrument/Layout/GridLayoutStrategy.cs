using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Grid-based layout strategy for positioning stems.
    /// Arranges stems in a grid pattern with configurable columns, rows, and spacing.
    /// </summary>
    [CreateAssetMenu(fileName = "GridLayoutStrategy", menuName = "Modular Virtual Instrument/Layout/Grid Layout")]
    public class GridLayoutStrategy : StemLayoutStrategy
    {
        [Header("Grid Layout Settings")]
        [Tooltip("Number of columns in grid layout")]
        public int columns = 3;
        
        [Tooltip("Number of rows in grid layout")]
        public int rows = 2;
        
        [Tooltip("Spacing between columns")]
        public float columnSpacing = 0.5f;
        
        [Tooltip("Spacing between rows")]
        public float rowSpacing = 0.5f;
        
        [Header("Height Settings")]
        [Tooltip("Height offset for all stems")]
        public float heightOffset = 0f;
        
        [Header("Centering")]
        [Tooltip("Center the grid around the parent's position")]
        public bool centerGrid = true;
        
        public override Vector3[] CalculatePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            // Calculate grid dimensions
            int actualColumns = Mathf.Min(columns, stemCount);
            int actualRows = Mathf.CeilToInt((float)stemCount / actualColumns);
            
            // Calculate offset for centering
            Vector3 offset = Vector3.zero;
            if (centerGrid)
            {
                float totalWidth = (actualColumns - 1) * columnSpacing;
                float totalDepth = (actualRows - 1) * rowSpacing;
                offset = new Vector3(-totalWidth * 0.5f, heightOffset, -totalDepth * 0.5f);
            }
            else
            {
                offset = new Vector3(0, heightOffset, 0);
            }
            
            // Position each stem in the grid
            for (int i = 0; i < stemCount; i++)
            {
                int row = i / actualColumns;
                int col = i % actualColumns;
                
                float x = col * columnSpacing;
                float z = row * rowSpacing;
                
                positions[i] = centerPoint + offset + new Vector3(x, 0, z);
            }
            
            return positions;
        }
        
        public override Quaternion[] CalculateRotations(int stemCount, Vector3 centerPoint)
        {
            Quaternion[] rotations = new Quaternion[stemCount];
            
            // All stems face forward in grid layout
            for (int i = 0; i < stemCount; i++)
            {
                rotations[i] = Quaternion.identity;
            }
            
            return rotations;
        }
        
        public override void DrawLayoutGizmos(int stemCount, Vector3 centerPoint)
        {
            int actualColumns = Mathf.Min(columns, stemCount);
            int actualRows = Mathf.CeilToInt((float)stemCount / actualColumns);
            
            float totalWidth = (actualColumns - 1) * columnSpacing;
            float totalDepth = (actualRows - 1) * rowSpacing;
            
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            
            Vector3 boxCenter = centerPoint;
            if (centerGrid)
            {
                boxCenter += new Vector3(0, heightOffset, 0);
            }
            else
            {
                boxCenter += new Vector3(totalWidth * 0.5f, heightOffset, totalDepth * 0.5f);
            }
            
            Gizmos.DrawWireCube(boxCenter, new Vector3(totalWidth, 0.1f, totalDepth));
        }
    }
}
