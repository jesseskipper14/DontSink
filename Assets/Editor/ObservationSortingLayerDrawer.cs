using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ObservationSortingLayerAttribute))]
public sealed class ObservationSortingLayerDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        { EditorGUI.PropertyField(position, property, label, true); return; }
        EditorGUI.BeginProperty(position, label, property);
        SortingLayer[] layers = SortingLayer.layers;
        string[] names = new string[layers.Length + 1];
        names[0] = "None";
        int current = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            names[i + 1] = layers[i].name;
            if (property.stringValue == layers[i].name) current = i + 1;
        }
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(position, label.text, current, names);
        if (EditorGUI.EndChangeCheck()) property.stringValue = selected == 0 ? "" : names[selected];
        EditorGUI.EndProperty();
    }
}
