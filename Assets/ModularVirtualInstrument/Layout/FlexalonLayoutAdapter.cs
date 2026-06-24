#if MVI_FLEXALON
using UnityEngine;
using Flexalon;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Comprehensive adapter for Flexalon Pro layouts.
    /// Supports Grid, Flexible, Circle, Curve, Align, Random, and Shape layouts.
    /// </summary>
    [CreateAssetMenu(fileName = "FlexalonLayoutAdapter", menuName = "Modular Virtual Instrument/Layout/Flexalon Adapter")]
    public class FlexalonLayoutAdapter : StemLayoutStrategy
    {
        [Header("Layout Type")]
        [Tooltip("Which Flexalon layout to use")]
        public FlexalonLayoutType layoutType = FlexalonLayoutType.Grid;
        
        [Header("Grid Layout")]
        public int gridColumns = 3;
        public int gridRows = 2;
        public float columnSpacing = 0.5f;
        public float rowSpacing = 0.5f;
        
        [Header("Flexible Layout")]
        public Direction flexDirection = Direction.PositiveX;
        public float flexGap = 0.5f;
        public bool flexWrap = false;
        public Direction flexWrapDirection = Direction.NegativeY;
        
        [Header("Circle Layout")]
        public float circleRadius = 2f;
        public float circleStartAngle = 0f;
        public float circleSpacingDegrees = 30f;
        public bool circleSpiral = false;
        public float spiralSpacing = 0.5f;
        
        [Tooltip("Radius multiplier for tighter/looser circle layouts")]
        [Range(0.1f, 2f)]
        public float circleRadiusMultiplier = 1f;
        
        [Header("Curve Layout")]
        public AnimationCurve curvePath = AnimationCurve.Linear(0, 0, 1, 1);
        public float curveLength = 5f;
        public float curveSpacing = 0.5f;
        
        [Header("Align Layout")]
        public Align horizontalAlign = Align.Center;
        public Align verticalAlign = Align.Center;
        public Align depthAlign = Align.Center;
        
        [Header("Random Layout")]
        public Vector3 randomBoundsMin = new Vector3(-2, 0, -2);
        public Vector3 randomBoundsMax = new Vector3(2, 0, 2);
        public int randomSeed = 0;
        
        [Header("Shape Layout")]
        public ShapeType shapeType = ShapeType.Circle;
        public float shapeRadius = 2f;
        public int shapeSides = 6;
        
        [Tooltip("Radius multiplier for tighter/looser shape layouts")]
        [Range(0.1f, 2f)]
        public float shapeRadiusMultiplier = 1f;
        
        [Header("Animation")]
        public bool useAnimations = true;
        public float animationSpeed = 5f;
        
        public enum FlexalonLayoutType
        {
            Grid,
            Flexible,
            Circle,
            Curve,
            Align,
            Random,
            Shape
        }
        
        public enum ShapeType
        {
            Circle,
            Square,
            Pentagon,
            Hexagon,
            Star
        }
        
        public override Vector3[] CalculatePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            switch (layoutType)
            {
                case FlexalonLayoutType.Grid:
                    positions = CalculateGridPositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Flexible:
                    positions = CalculateFlexiblePositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Circle:
                    positions = CalculateCirclePositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Curve:
                    positions = CalculateCurvePositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Align:
                    positions = CalculateAlignPositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Random:
                    positions = CalculateRandomPositions(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Shape:
                    positions = CalculateShapePositions(stemCount, centerPoint);
                    break;
            }
            
            return positions;
        }
        
        public override Quaternion[] CalculateRotations(int stemCount, Vector3 centerPoint)
        {
            Quaternion[] rotations = new Quaternion[stemCount];
            
            if (layoutType == FlexalonLayoutType.Circle)
            {
                // Face tangent to circle
                float angleStep = circleSpacingDegrees;
                for (int i = 0; i < stemCount; i++)
                {
                    float angle = circleStartAngle + (i * angleStep);
                    rotations[i] = Quaternion.Euler(0, angle + 90f, 0);
                }
            }
            else if (layoutType == FlexalonLayoutType.Shape && shapeType == ShapeType.Circle)
            {
                // Face outward from center
                for (int i = 0; i < stemCount; i++)
                {
                    float angle = (i / (float)stemCount) * 360f;
                    rotations[i] = Quaternion.Euler(0, angle, 0);
                }
            }
            else
            {
                // Default: face forward
                for (int i = 0; i < stemCount; i++)
                {
                    rotations[i] = Quaternion.identity;
                }
            }
            
            return rotations;
        }
        
        private Vector3[] CalculateGridPositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            int actualColumns = Mathf.Min(gridColumns, stemCount);
            int actualRows = Mathf.CeilToInt((float)stemCount / actualColumns);
            
            float totalWidth = (actualColumns - 1) * columnSpacing;
            float totalDepth = (actualRows - 1) * rowSpacing;
            Vector3 offset = new Vector3(-totalWidth * 0.5f, 0, -totalDepth * 0.5f);
            
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
        
        private Vector3[] CalculateFlexiblePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            Vector3 dirVector = GetDirectionVector(flexDirection);
            
            float totalLength = (stemCount - 1) * flexGap;
            Vector3 startPos = centerPoint - (dirVector * totalLength * 0.5f);
            
            for (int i = 0; i < stemCount; i++)
            {
                positions[i] = startPos + (dirVector * i * flexGap);
            }
            
            return positions;
        }
        
        private Vector3[] CalculateCirclePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                float angle = (circleStartAngle + i * circleSpacingDegrees) * Mathf.Deg2Rad;
                float radius = circleRadius * circleRadiusMultiplier;
                
                if (circleSpiral)
                {
                    radius += i * spiralSpacing;
                }
                
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = circleSpiral ? i * spiralSpacing : 0;
                
                positions[i] = centerPoint + new Vector3(x, y, z);
            }
            
            return positions;
        }
        
        private Vector3[] CalculateCurvePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                float t = i / (float)(stemCount - 1);
                float curveValue = curvePath.Evaluate(t);
                
                // Simple curve along X axis with height from curve
                Vector3 pos = new Vector3(
                    t * curveLength - curveLength * 0.5f,
                    curveValue,
                    0
                );
                
                positions[i] = centerPoint + pos;
            }
            
            return positions;
        }
        
        private Vector3[] CalculateAlignPositions(int stemCount, Vector3 centerPoint)
        {
            // Align layout places all items at the same position
            // They can be offset individually via Flexalon Object component
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                positions[i] = centerPoint;
            }
            
            return positions;
        }
        
        private Vector3[] CalculateRandomPositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            Random.State oldState = Random.state;
            Random.InitState(randomSeed);
            
            for (int i = 0; i < stemCount; i++)
            {
                Vector3 randomPos = new Vector3(
                    Random.Range(randomBoundsMin.x, randomBoundsMax.x),
                    Random.Range(randomBoundsMin.y, randomBoundsMax.y),
                    Random.Range(randomBoundsMin.z, randomBoundsMax.z)
                );
                
                positions[i] = centerPoint + randomPos;
            }
            
            Random.state = oldState;
            return positions;
        }
        
        private Vector3[] CalculateShapePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            switch (shapeType)
            {
                case ShapeType.Circle:
                    return CalculateCircleShapePositions(stemCount, centerPoint);
                    
                case ShapeType.Square:
                    return CalculateSquareShapePositions(stemCount, centerPoint);
                    
                case ShapeType.Pentagon:
                case ShapeType.Hexagon:
                    return CalculatePolygonShapePositions(stemCount, centerPoint, shapeSides);
                    
                case ShapeType.Star:
                    return CalculateStarShapePositions(stemCount, centerPoint);
            }
            
            return positions;
        }
        
        private Vector3[] CalculateCircleShapePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                float angle = (i / (float)stemCount) * 2f * Mathf.PI;
                float radius = shapeRadius * shapeRadiusMultiplier;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                
                positions[i] = centerPoint + new Vector3(x, 0, z);
            }
            
            return positions;
        }
        
        private Vector3[] CalculateSquareShapePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            int perSide = Mathf.CeilToInt(stemCount / 4f);
            float radius = shapeRadius * shapeRadiusMultiplier;
            
            for (int i = 0; i < stemCount; i++)
            {
                int side = i / perSide;
                int index = i % perSide;
                float t = index / (float)(perSide - 1);
                
                Vector3 pos = Vector3.zero;
                switch (side)
                {
                    case 0: pos = new Vector3(t * radius * 2 - radius, 0, -radius); break;
                    case 1: pos = new Vector3(radius, 0, t * radius * 2 - radius); break;
                    case 2: pos = new Vector3(radius - t * radius * 2, 0, radius); break;
                    case 3: pos = new Vector3(-radius, 0, radius - t * radius * 2); break;
                }
                
                positions[i] = centerPoint + pos;
            }
            
            return positions;
        }
        
        private Vector3[] CalculatePolygonShapePositions(int stemCount, Vector3 centerPoint, int sides)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                float angle = (i / (float)stemCount) * 2f * Mathf.PI;
                float radius = shapeRadius * shapeRadiusMultiplier;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                
                positions[i] = centerPoint + new Vector3(x, 0, z);
            }
            
            return positions;
        }
        
        private Vector3[] CalculateStarShapePositions(int stemCount, Vector3 centerPoint)
        {
            Vector3[] positions = new Vector3[stemCount];
            
            for (int i = 0; i < stemCount; i++)
            {
                float angle = (i / (float)stemCount) * 2f * Mathf.PI;
                float radius = (i % 2 == 0) ? shapeRadius * shapeRadiusMultiplier : shapeRadius * shapeRadiusMultiplier * 0.5f; // Alternating inner/outer points
                
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                
                positions[i] = centerPoint + new Vector3(x, 0, z);
            }
            
            return positions;
        }
        
        private Vector3 GetDirectionVector(Direction direction)
        {
            switch (direction)
            {
                case Direction.PositiveX: return Vector3.right;
                case Direction.NegativeX: return Vector3.left;
                case Direction.PositiveY: return Vector3.up;
                case Direction.NegativeY: return Vector3.down;
                case Direction.PositiveZ: return Vector3.forward;
                case Direction.NegativeZ: return Vector3.back;
                default: return Vector3.right;
            }
        }
        
        public override void DrawLayoutGizmos(int stemCount, Vector3 centerPoint)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
            
            switch (layoutType)
            {
                case FlexalonLayoutType.Grid:
                    DrawGridGizmos(stemCount, centerPoint);
                    break;
                    
                case FlexalonLayoutType.Circle:
                case FlexalonLayoutType.Shape:
                    Gizmos.DrawWireSphere(centerPoint, circleRadius);
                    break;
                    
                case FlexalonLayoutType.Random:
                    Vector3 size = randomBoundsMax - randomBoundsMin;
                    Vector3 center = centerPoint + (randomBoundsMin + randomBoundsMax) * 0.5f;
                    Gizmos.DrawWireCube(center, size);
                    break;
                    
                default:
                    Gizmos.DrawWireCube(centerPoint, Vector3.one * 0.2f);
                    break;
            }
        }
        
        private void DrawGridGizmos(int stemCount, Vector3 centerPoint)
        {
            int actualColumns = Mathf.Min(gridColumns, stemCount);
            int actualRows = Mathf.CeilToInt((float)stemCount / actualColumns);
            
            float width = (actualColumns - 1) * columnSpacing;
            float height = (actualRows - 1) * rowSpacing;
            
            Gizmos.DrawWireCube(centerPoint, new Vector3(width, 0.1f, height));
        }
    }
}
#endif
