#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LayerUsageReportWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "";

    [MenuItem("Tools/Diagnostics/Layer Usage Report")]
    public static void Open()
    {
        GetWindow<LayerUsageReportWindow>("Layer Usage");
    }

    private void OnEnable()
    {
        BuildReport();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Layer Usage Report", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            BuildReport();

        if (GUILayout.Button("Copy", GUILayout.Width(100)))
            EditorGUIUtility.systemCopyBuffer = report;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void BuildReport()
    {
        StringBuilder sb = new();

        Dictionary<int, List<string>> sceneUsage = new();
        Dictionary<int, List<string>> prefabUsage = new();

        for (int i = 0; i < 32; i++)
        {
            sceneUsage[i] = new List<string>();
            prefabUsage[i] = new List<string>();
        }

        CollectSceneObjects(sceneUsage);
        CollectPrefabObjects(prefabUsage);

        sb.AppendLine("=== LAYER USAGE REPORT ===");
        sb.AppendLine();

        for (int layer = 0; layer < 32; layer++)
        {
            string layerName = LayerMask.LayerToName(layer);

            if (string.IsNullOrWhiteSpace(layerName))
                layerName = $"<Unnamed Layer {layer}>";

            int sceneCount = sceneUsage[layer].Count;
            int prefabCount = prefabUsage[layer].Count;

            sb.AppendLine($"[{layer:00}] {layerName}");
            sb.AppendLine($"  Scene objects:  {sceneCount}");
            sb.AppendLine($"  Prefab objects: {prefabCount}");

            if (sceneCount == 0 && prefabCount == 0)
            {
                sb.AppendLine("  UNUSED");
                sb.AppendLine();
                continue;
            }

            if (sceneCount > 0)
            {
                sb.AppendLine("  Scene examples:");
                AppendExamples(sb, sceneUsage[layer]);
            }

            if (prefabCount > 0)
            {
                sb.AppendLine("  Prefab examples:");
                AppendExamples(sb, prefabUsage[layer]);
            }

            sb.AppendLine();
        }

        report = sb.ToString();
    }

    private static void CollectSceneObjects(Dictionary<int, List<string>> usage)
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
                CollectHierarchy(roots[i].transform, usage, $"Scene/{scene.name}");
        }
    }

    private static void CollectPrefabObjects(Dictionary<int, List<string>> usage)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                continue;

            CollectHierarchy(prefab.transform, usage, path);
        }
    }

    private static void CollectHierarchy(
        Transform root,
        Dictionary<int, List<string>> usage,
        string source)
    {
        if (root == null)
            return;

        int layer = root.gameObject.layer;

        if (!usage.TryGetValue(layer, out List<string> list))
        {
            list = new List<string>();
            usage[layer] = list;
        }

        list.Add($"{source} :: {GetHierarchyPath(root)}");

        for (int i = 0; i < root.childCount; i++)
            CollectHierarchy(root.GetChild(i), usage, source);
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
            return "";

        Stack<string> parts = new();

        Transform current = t;
        while (current != null)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", parts);
    }

    private static void AppendExamples(StringBuilder sb, List<string> entries)
    {
        int max = Mathf.Min(entries.Count, 20);

        for (int i = 0; i < max; i++)
            sb.AppendLine($"    - {entries[i]}");

        if (entries.Count > max)
            sb.AppendLine($"    ...and {entries.Count - max} more");
    }
}
#endif