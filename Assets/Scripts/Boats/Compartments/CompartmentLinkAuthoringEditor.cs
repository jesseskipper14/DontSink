#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CompartmentLinkAuthoring))]
public class CompartmentLinkAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var a = (CompartmentLinkAuthoring)target;

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);

            if (GUILayout.Button("Resolve Preview", GUILayout.Height(28)))
            {
                var result = a.Resolve();

                string aName = result.A != null ? result.A.name : "NULL";
                string bName = result.B != null ? result.B.name : "NULL";

                Debug.Log(
                    $"[CompartmentLinkAuthoring] '{a.name}' => {result.resolutionType} | " +
                    $"A={aName}, B={bName}, reason={result.reason}",
                    a);
            }

            EditorGUILayout.HelpBox(
                "This resolves the opening by sampling on both sides of the opening collider.\n" +
                "Internal: both sides hit compartments.\n" +
                "ExternalExposure: only one side hits a compartment.\n" +
                "Invalid: neither side, or both sides hit the same compartment.",
                MessageType.Info);
        }
    }
}
#endif