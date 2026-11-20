using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ParkourRange))]
public class ParkourRangeDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Labels = { new("Start"), new("End") };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty rangeProp = property.FindPropertyRelative("range");
        Vector2 val = rangeProp.vector2Value;
        float[] values = { val.x, val.y };

        position = EditorGUI.PrefixLabel(position, label);
        EditorGUI.MultiFloatField(position, Labels, values);

        rangeProp.vector2Value = new(values[0], values[1]);

        EditorGUI.EndProperty();
    }
}