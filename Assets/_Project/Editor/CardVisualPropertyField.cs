using System;
using System.Collections.Generic;
using CoH.Presentation.CardVisuals;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Draws one authored value, whatever its type.
    ///
    /// A table from type to control, so that a property the schema has just
    /// discovered can be edited without anybody writing a control for it. Adding
    /// a property of a type already in the table costs nothing; adding a
    /// genuinely new kind of value costs one entry here, which is the price the
    /// design is willing to pay.
    ///
    /// Nothing in this file knows what any particular property means. It is
    /// handed a type, a label and a value.
    /// </summary>
    public static class CardVisualPropertyField
    {
        private delegate object Drawer(GUIContent label, object value, CardVisualProperty property);

        private static readonly Dictionary<Type, Drawer> Drawers = new Dictionary<Type, Drawer>
        {
            [typeof(float)] = (label, value, property) => property.HasRange
                ? EditorGUILayout.Slider(label, (float)value, property.Lowest, property.Highest)
                : EditorGUILayout.FloatField(label, (float)value),

            [typeof(int)] = (label, value, property) => property.HasRange
                ? EditorGUILayout.IntSlider(
                    label, (int)value, Mathf.RoundToInt(property.Lowest),
                    Mathf.RoundToInt(property.Highest))
                : EditorGUILayout.IntField(label, (int)value),

            [typeof(bool)] = (label, value, _) => EditorGUILayout.Toggle(label, (bool)value),
            [typeof(string)] = (label, value, _) => EditorGUILayout.TextField(label, (string)value),
            [typeof(Color)] = (label, value, _) => EditorGUILayout.ColorField(label, (Color)value),
            [typeof(Vector2)] = (label, value, _) => EditorGUILayout.Vector2Field(label, (Vector2)value),
            [typeof(Vector3)] = (label, value, _) => EditorGUILayout.Vector3Field(label, (Vector3)value),
            [typeof(Rect)] = (label, value, _) => EditorGUILayout.RectField(label, (Rect)value)
        };

        /// <summary>Whether anything here can show that type.</summary>
        public static bool CanDraw(Type type) =>
            type != null && (type.IsEnum || Drawers.ContainsKey(type));

        /// <summary>
        /// Draws the value and returns what it became.
        ///
        /// Enumerations are handled outside the table because every one of them
        /// is a different type and listing them would be exactly the hardcoding
        /// this exists to avoid.
        /// </summary>
        public static object Draw(CardVisualProperty property, object value)
        {
            if (property == null || value == null)
            {
                return value;
            }

            GUIContent label = new GUIContent(property.DisplayName, property.Tooltip);

            if (property.Type.IsEnum)
            {
                return EditorGUILayout.EnumPopup(label, (Enum)value);
            }

            return Drawers.TryGetValue(property.Type, out Drawer drawer)
                ? drawer(label, value, property)
                : value;
        }
    }
}
