#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PhysicsLayerUsageReportWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "";

    [MenuItem("Tools/Diagnostics/Physics Layer Usage Report")]
    public static void Open()
    {
        GetWindow<PhysicsLayerUsageReportWindow>("Physics Layers");
    }

    private void OnEnable()
    {
        BuildReport();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Physics Layer Usage Report", EditorStyles.boldLabel);

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

        Dictionary<int, List<string>> sceneUsage = CreateLayerMap();
        Dictionary<int, List<string>> prefabUsage = CreateLayerMap();

        CollectScenePhysicsObjects(sceneUsage);
        CollectPrefabPhysicsObjects(prefabUsage);

        sb.AppendLine("=== PHYSICS LAYER USAGE REPORT ===");
        sb.AppendLine("Only GameObjects with Collider2D, Rigidbody2D, or Effector2D are counted.");
        sb.AppendLine();

        for (int layer = 0; layer < 32; layer++)
        {
            string layerName = LayerMask.LayerToName(layer);
            if (string.IsNullOrWhiteSpace(layerName))
                layerName = $"<Unnamed Layer {layer}>";

            int sceneCount = sceneUsage[layer].Count;
            int prefabCount = prefabUsage[layer].Count;

            if (sceneCount == 0 && prefabCount == 0)
                continue;

            sb.AppendLine($"[{layer:00}] {layerName}");
            sb.AppendLine($"  Scene physics objects:  {sceneCount}");
            sb.AppendLine($"  Prefab physics objects: {prefabCount}");

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

    private static Dictionary<int, List<string>> CreateLayerMap()
    {
        Dictionary<int, List<string>> map = new();

        for (int i = 0; i < 32; i++)
            map[i] = new List<string>();

        return map;
    }

    private static void CollectScenePhysicsObjects(Dictionary<int, List<string>> usage)
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

    private static void CollectPrefabPhysicsObjects(Dictionary<int, List<string>> usage)
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

        GameObject go = root.gameObject;

        bool hasPhysics =
            go.GetComponent<Collider2D>() != null ||
            go.GetComponent<Rigidbody2D>() != null ||
            go.GetComponent<Effector2D>() != null;

        if (hasPhysics)
            usage[go.layer].Add($"{source} :: {GetHierarchyPath(root)} :: {DescribePhysics(go)}");

        for (int i = 0; i < root.childCount; i++)
            CollectHierarchy(root.GetChild(i), usage, source);
    }

    private static string DescribePhysics(GameObject go)
    {
        List<string> parts = new();

        Collider2D col = go.GetComponent<Collider2D>();
        if (col != null)
            parts.Add($"{col.GetType().Name}(trigger={col.isTrigger})");

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
            parts.Add($"Rigidbody2D({rb.bodyType})");

        Effector2D effector = go.GetComponent<Effector2D>();
        if (effector != null)
            parts.Add(effector.GetType().Name);

        return string.Join(", ", parts);
    }

    private static string GetHierarchyPath(Transform t)
    {
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
        int max = Mathf.Min(entries.Count, 40);

        for (int i = 0; i < max; i++)
            sb.AppendLine($"    - {entries[i]}");

        if (entries.Count > max)
            sb.AppendLine($"    ...and {entries.Count - max} more");
    }
}
#endif