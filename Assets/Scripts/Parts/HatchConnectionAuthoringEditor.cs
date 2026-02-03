#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HatchConnectionAuthoring))]
public class HatchConnectionAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var a = (HatchConnectionAuthoring)target;

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

            if (GUILayout.Button("Resolve + Apply Connection", GUILayout.Height(28)))
            {
                Undo.RecordObject(a, "Resolve Hatch Connection");
                a.ResolveAndApply();
            }

            EditorGUILayout.HelpBox(
                "This finds the adjacent compartments by sampling points along the hatch's right axis.\n" +
                "It writes/updates a CompartmentConnection entry on the owning Boat.",
                MessageType.Info);
        }
    }
}
#endif
