#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class SortingLayerUsageReportWindow : EditorWindow
{
    private Vector2 scroll;
    private string report = "";

    private bool includeSceneObjects = true;
    private bool includePrefabAssets = true;
    private bool includeInactive = true;
    private bool includePackages = false;
    private bool includeCanvases = true;
    private bool includeSortingGroups = true;
    private bool includeRenderers = true;

    [MenuItem("Tools/Diagnostics/Sorting Layer Usage Report")]
    public static void Open()
    {
        GetWindow<SortingLayerUsageReportWindow>("Sorting Layers");
    }

    private void OnEnable()
    {
        BuildReport();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sorting Layer Usage Report", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        includeSceneObjects = EditorGUILayout.ToggleLeft("Include loaded scene objects", includeSceneObjects);
        includePrefabAssets = EditorGUILayout.ToggleLeft("Include prefab assets", includePrefabAssets);
        includeInactive = EditorGUILayout.ToggleLeft("Include inactive objects", includeInactive);
        includePackages = EditorGUILayout.ToggleLeft("Include Packages folder", includePackages);

        EditorGUILayout.Space();

        includeRenderers = EditorGUILayout.ToggleLeft("Include Renderers", includeRenderers);
        includeSortingGroups = EditorGUILayout.ToggleLeft("Include SortingGroups", includeSortingGroups);
        includeCanvases = EditorGUILayout.ToggleLeft("Include Canvases", includeCanvases);

        EditorGUILayout.Space();

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
        Dictionary<int, List<SortingUsageEntry>> sceneUsage = CreateLayerMap();
        Dictionary<int, List<SortingUsageEntry>> prefabUsage = CreateLayerMap();

        if (includeSceneObjects)
            CollectSceneObjects(sceneUsage);

        if (includePrefabAssets)
            CollectPrefabObjects(prefabUsage);

        StringBuilder sb = new();

        sb.AppendLine("=== SORTING LAYER USAGE REPORT ===");
        sb.AppendLine("Counts SpriteRenderer/Renderer sorting, SortingGroup, and Canvas sorting.");
        sb.AppendLine();

        AppendSortingLayerStack(sb);
        sb.AppendLine();

        for (int i = 0; i < SortingLayer.layers.Length; i++)
        {
            SortingLayer sortingLayer = SortingLayer.layers[i];
            int layerId = sortingLayer.id;
            string layerName = sortingLayer.name;

            sceneUsage.TryGetValue(layerId, out List<SortingUsageEntry> sceneEntries);
            prefabUsage.TryGetValue(layerId, out List<SortingUsageEntry> prefabEntries);

            int sceneCount = sceneEntries != null ? sceneEntries.Count : 0;
            int prefabCount = prefabEntries != null ? prefabEntries.Count : 0;

            if (sceneCount == 0 && prefabCount == 0)
                continue;

            sb.AppendLine($"[{i:00}] {layerName}  id={layerId}");
            sb.AppendLine($"  Scene entries:  {sceneCount}");
            sb.AppendLine($"  Prefab entries: {prefabCount}");

            if (sceneCount > 0)
            {
                sb.AppendLine("  Scene examples:");
                AppendExamples(sb, sceneEntries);
            }

            if (prefabCount > 0)
            {
                sb.AppendLine("  Prefab examples:");
                AppendExamples(sb, prefabEntries);
            }

            sb.AppendLine();
        }

        AppendUnusedSortingLayers(sb, sceneUsage, prefabUsage);

        report = sb.ToString();
    }

    private static Dictionary<int, List<SortingUsageEntry>> CreateLayerMap()
    {
        Dictionary<int, List<SortingUsageEntry>> map = new();

        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
            map[layers[i].id] = new List<SortingUsageEntry>();

        return map;
    }

    private static void AppendSortingLayerStack(StringBuilder sb)
    {
        sb.AppendLine("Current Sorting Layer Stack, back to front:");

        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
            sb.AppendLine($"  [{i:00}] {layers[i].name}  id={layers[i].id}");
    }

    private void AppendUnusedSortingLayers(
        StringBuilder sb,
        Dictionary<int, List<SortingUsageEntry>> sceneUsage,
        Dictionary<int, List<SortingUsageEntry>> prefabUsage)
    {
        sb.AppendLine("Unused sorting layers:");

        bool foundAny = false;

        SortingLayer[] layers = SortingLayer.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            int id = layers[i].id;

            int sceneCount = sceneUsage.TryGetValue(id, out List<SortingUsageEntry> sceneEntries)
                ? sceneEntries.Count
                : 0;

            int prefabCount = prefabUsage.TryGetValue(id, out List<SortingUsageEntry> prefabEntries)
                ? prefabEntries.Count
                : 0;

            if (sceneCount == 0 && prefabCount == 0)
            {
                foundAny = true;
                sb.AppendLine($"  - [{i:00}] {layers[i].name}");
            }
        }

        if (!foundAny)
            sb.AppendLine("  None. Apparently everything has found a job. Suspicious.");
    }

    private void CollectSceneObjects(Dictionary<int, List<SortingUsageEntry>> usage)
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

    private void CollectPrefabObjects(Dictionary<int, List<SortingUsageEntry>> usage)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

            if (!includePackages && path.StartsWith("Packages/"))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            CollectHierarchy(prefab.transform, usage, path);
        }
    }

    private void CollectHierarchy(
        Transform root,
        Dictionary<int, List<SortingUsageEntry>> usage,
        string source)
    {
        if (root == null)
            return;

        GameObject go = root.gameObject;

        if (includeInactive || go.activeInHierarchy || IsPrefabAssetObject(go))
            CollectSortingComponents(go, usage, source);

        for (int i = 0; i < root.childCount; i++)
            CollectHierarchy(root.GetChild(i), usage, source);
    }

    private void CollectSortingComponents(
        GameObject go,
        Dictionary<int, List<SortingUsageEntry>> usage,
        string source)
    {
        string path = GetHierarchyPath(go.transform);

        if (includeRenderers)
        {
            Renderer[] renderers = go.GetComponents<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                AddEntry(
                    usage,
                    renderer.sortingLayerID,
                    new SortingUsageEntry(
                        source,
                        path,
                        renderer.GetType().Name,
                        renderer.sortingLayerName,
                        renderer.sortingOrder,
                        go.activeSelf,
                        DescribeRenderer(renderer)));
            }
        }

        if (includeSortingGroups)
        {
            SortingGroup[] groups = go.GetComponents<SortingGroup>();

            for (int i = 0; i < groups.Length; i++)
            {
                SortingGroup group = groups[i];
                if (group == null)
                    continue;

                AddEntry(
                    usage,
                    group.sortingLayerID,
                    new SortingUsageEntry(
                        source,
                        path,
                        "SortingGroup",
                        group.sortingLayerName,
                        group.sortingOrder,
                        go.activeSelf,
                        "SortingGroup controls child renderer sorting"));
            }
        }

        if (includeCanvases)
        {
            Canvas[] canvases = go.GetComponents<Canvas>();

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                AddEntry(
                    usage,
                    canvas.sortingLayerID,
                    new SortingUsageEntry(
                        source,
                        path,
                        "Canvas",
                        canvas.sortingLayerName,
                        canvas.sortingOrder,
                        go.activeSelf,
                        $"renderMode={canvas.renderMode}, overrideSorting={canvas.overrideSorting}"));
            }
        }
    }

    private static void AddEntry(
        Dictionary<int, List<SortingUsageEntry>> usage,
        int sortingLayerId,
        SortingUsageEntry entry)
    {
        if (!usage.TryGetValue(sortingLayerId, out List<SortingUsageEntry> entries))
        {
            entries = new List<SortingUsageEntry>();
            usage[sortingLayerId] = entries;
        }

        entries.Add(entry);
    }

    private static void AppendExamples(StringBuilder sb, List<SortingUsageEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        entries.Sort((a, b) =>
        {
            int orderCompare = a.SortingOrder.CompareTo(b.SortingOrder);
            if (orderCompare != 0)
                return orderCompare;

            return string.CompareOrdinal(a.Path, b.Path);
        });

        int max = Mathf.Min(entries.Count, 60);

        for (int i = 0; i < max; i++)
        {
            SortingUsageEntry e = entries[i];

            sb.AppendLine(
                $"    - order={e.SortingOrder,5} | {e.ComponentType,-22} | activeSelf={e.ActiveSelf,-5} | " +
                $"{e.Source} :: {e.Path} :: {e.Details}");
        }

        if (entries.Count > max)
            sb.AppendLine($"    ...and {entries.Count - max} more");
    }

    private static string DescribeRenderer(Renderer renderer)
    {
        if (renderer == null)
            return "";

        if (renderer is SpriteRenderer sr)
        {
            string spriteName = sr.sprite != null ? sr.sprite.name : "NO_SPRITE";
            return $"sprite={spriteName}";
        }

        return $"rendererEnabled={renderer.enabled}";
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

    private static bool IsPrefabAssetObject(GameObject go)
    {
        return !go.scene.IsValid();
    }

    private readonly struct SortingUsageEntry
    {
        public readonly string Source;
        public readonly string Path;
        public readonly string ComponentType;
        public readonly string SortingLayerName;
        public readonly int SortingOrder;
        public readonly bool ActiveSelf;
        public readonly string Details;

        public SortingUsageEntry(
            string source,
            string path,
            string componentType,
            string sortingLayerName,
            int sortingOrder,
            bool activeSelf,
            string details)
        {
            Source = source;
            Path = path;
            ComponentType = componentType;
            SortingLayerName = sortingLayerName;
            SortingOrder = sortingOrder;
            ActiveSelf = activeSelf;
            Details = details;
        }
    }
}
#endif