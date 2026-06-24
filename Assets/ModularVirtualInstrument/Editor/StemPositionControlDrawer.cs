using UnityEngine;
using UnityEditor;

namespace ModularVirtualInstrument.Editor
{
    /// <summary>
    /// Custom property drawer for StemPositionControl to provide a cleaner inspector UI
    /// </summary>
    [CustomPropertyDrawer(typeof(SemicircularLayout.StemPositionControl))]
    public class StemPositionControlDrawer : PropertyDrawer
    {
        private const float lineHeight = 18f;
        private const float spacing = 2f;
        private const float labelWidth = 140f;
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 4 properties + header + spacing
            return (lineHeight + spacing) * 5 + 4f;
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            // Draw background box
            Rect boxRect = new Rect(position.x, position.y, position.width, GetPropertyHeight(property, label));
            GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
            
            // Indent for content
            float indent = 8f;
            position.x += indent;
            position.width -= indent * 2;
            position.y += 4f;
            
            // Draw label
            EditorGUI.LabelField(new Rect(position.x, position.y, position.width, lineHeight), label, EditorStyles.boldLabel);
            position.y += lineHeight + spacing;
            
            // Save indent level
            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            
            // Draw each property with custom styling
            DrawSliderProperty(ref position, property, "perimeterPosition", "Perimeter Position", 0f, 1f, "Position along arc (0=start, 1=end)");
            DrawSliderProperty(ref position, property, "heightAdjustment", "Height Offset", -2f, 2f, "Vertical offset from base height");
            DrawSliderProperty(ref position, property, "depthOffset", "Depth Offset", -2f, 2f, "Radial offset (+ = outward)");
            DrawSliderProperty(ref position, property, "additionalRotation", "Rotation", -180f, 180f, "Additional Y-axis rotation (degrees)");
            
            // Restore indent
            EditorGUI.indentLevel = originalIndent;
            
            EditorGUI.EndProperty();
        }
        
        private void DrawSliderProperty(ref Rect position, SerializedProperty parent, string propertyName, string displayName, float min, float max, string tooltip)
        {
            SerializedProperty prop = parent.FindPropertyRelative(propertyName);
            
            Rect labelRect = new Rect(position.x, position.y, labelWidth, lineHeight);
            Rect sliderRect = new Rect(position.x + labelWidth + 4f, position.y, position.width - labelWidth - 4f, lineHeight);
            
            // Draw label with tooltip
            EditorGUI.LabelField(labelRect, new GUIContent(displayName, tooltip));
            
            // Draw slider
            prop.floatValue = EditorGUI.Slider(sliderRect, prop.floatValue, min, max);
            
            position.y += lineHeight + spacing;
        }
    }
}
