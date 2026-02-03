#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Boat))]
public class BoatCompartmentMergerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Compartments", EditorStyles.boldLabel);

            if (GUILayout.Button("Merge Compartments", GUILayout.Height(32)))
            {
                var boat = (Boat)target;
                BoatCompartmentMerger.MergeOnBoat(boat);
            }

            EditorGUILayout.HelpBox(
                "Merges touching compartment fragments into larger compartments unless a CompartmentConnection exists between them.\n" +
                "Order-independent: works whether you created connections first or after merging.",
                MessageType.Info);
        }
    }
}
#endif
