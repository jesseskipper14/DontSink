#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class SceneHierarchyDiagnosticsWindow : EditorWindow
{
    private const string DefaultFolderName = "Diagnostics";

    [Header("Report Options")]
    [SerializeField] private bool includeInactiveObjects = true;
    [SerializeField] private bool includeComponentSerializedFields = true;
    [SerializeField] private bool includeNullObjectReferenceFields = false;
    [SerializeField] private bool includeTransformDetails = true;
    [SerializeField] private bool includeComponentTypeSummary = true;
    [SerializeField] private bool includeSuspicionReport = true;
    [SerializeField] private bool includeDontDestroyOnLoadScene = true;

    [Header("Limits")]
    [SerializeField] private int maxSerializedFieldsPerComponent = 80;
    [SerializeField] private int maxStringLength = 160;
    [SerializeField] private int maxObjectReferencePathLength = 180;

    [Header("Output")]
    [SerializeField] private bool saveToAssetsDiagnosticsFolder = true;
    [SerializeField] private bool alsoCopyToClipboard = true;
    [SerializeField] private bool pingCreatedReport = true;

    private Vector2 scroll;
    private string lastReportPath;
    private string lastReportPreview;
    private int lastLineCount;

    [MenuItem("Tools/Project Diagnostics/Scene Hierarchy Diagnostics")]
    public static void Open()
    {
        SceneHierarchyDiagnosticsWindow window =
            GetWindow<SceneHierarchyDiagnosticsWindow>("Scene Diagnostics");

        window.minSize = new Vector2(520f, 520f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Scene Hierarchy Diagnostics", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generates a Markdown report of loaded scene hierarchies, components, serialized fields, missing scripts, duplicate stable IDs, singleton-like duplicates, and other suspicious scene setup. In other words, the hierarchy stops hiding in the walls.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        DrawOptions();

        EditorGUILayout.Space(12);

        if (GUILayout.Button("Generate Report", GUILayout.Height(34)))
            GenerateReport();

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(lastReportPath) || !File.Exists(lastReportPath)))
        {
            if (GUILayout.Button("Open Last Report"))
                EditorUtility.RevealInFinder(lastReportPath);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(lastReportPreview)))
        {
            if (GUILayout.Button("Copy Last Report To Clipboard"))
                EditorGUIUtility.systemCopyBuffer = lastReportPreview;
        }

        EditorGUILayout.Space(12);

        if (!string.IsNullOrWhiteSpace(lastReportPath))
        {
            EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(lastReportPath, EditorStyles.textField, GUILayout.Height(20));

            EditorGUILayout.LabelField($"Lines: {lastLineCount}");
        }

        if (!string.IsNullOrWhiteSpace(lastReportPreview))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            string preview = lastReportPreview.Length > 12000
                ? lastReportPreview.Substring(0, 12000) + "\n\n... preview truncated ..."
                : lastReportPreview;

            EditorGUILayout.TextArea(preview, GUILayout.MinHeight(220));
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawOptions()
    {
        EditorGUILayout.LabelField("Report Options", EditorStyles.boldLabel);

        includeInactiveObjects = EditorGUILayout.ToggleLeft(
            "Include inactive objects",
            includeInactiveObjects);

        includeComponentSerializedFields = EditorGUILayout.ToggleLeft(
            "Include component serialized fields",
            includeComponentSerializedFields);

        using (new EditorGUI.DisabledScope(!includeComponentSerializedFields))
        {
            includeNullObjectReferenceFields = EditorGUILayout.ToggleLeft(
                "Include null object reference fields",
                includeNullObjectReferenceFields);
        }

        includeTransformDetails = EditorGUILayout.ToggleLeft(
            "Include transform details",
            includeTransformDetails);

        includeComponentTypeSummary = EditorGUILayout.ToggleLeft(
            "Include component type summary",
            includeComponentTypeSummary);

        includeSuspicionReport = EditorGUILayout.ToggleLeft(
            "Include suspicion report",
            includeSuspicionReport);

        includeDontDestroyOnLoadScene = EditorGUILayout.ToggleLeft(
            "Include DontDestroyOnLoad scene while playing",
            includeDontDestroyOnLoadScene);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Limits", EditorStyles.boldLabel);

        maxSerializedFieldsPerComponent = EditorGUILayout.IntSlider(
            "Max fields per component",
            maxSerializedFieldsPerComponent,
            0,
            500);

        maxStringLength = EditorGUILayout.IntSlider(
            "Max string length",
            maxStringLength,
            20,
            1000);

        maxObjectReferencePathLength = EditorGUILayout.IntSlider(
            "Max object ref path length",
            maxObjectReferencePathLength,
            40,
            1000);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        saveToAssetsDiagnosticsFolder = EditorGUILayout.ToggleLeft(
            "Save to Assets/Diagnostics",
            saveToAssetsDiagnosticsFolder);

        alsoCopyToClipboard = EditorGUILayout.ToggleLeft(
            "Also copy to clipboard",
            alsoCopyToClipboard);

        pingCreatedReport = EditorGUILayout.ToggleLeft(
            "Ping created report asset",
            pingCreatedReport);
    }

    private void GenerateReport()
    {
        SceneHierarchyReportData data = ScanLoadedScenes();

        string report = BuildMarkdownReport(data);

        lastReportPreview = report;
        lastLineCount = CountLines(report);

        if (alsoCopyToClipboard)
            EditorGUIUtility.systemCopyBuffer = report;

        if (saveToAssetsDiagnosticsFolder)
        {
            lastReportPath = SaveReportToAssets(report);

            if (pingCreatedReport)
            {
                AssetDatabase.Refresh();

                string projectRelativePath = AbsoluteToProjectRelativePath(lastReportPath);
                Object asset = AssetDatabase.LoadAssetAtPath<TextAsset>(projectRelativePath);

                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }
            }
        }
        else
        {
            string path = EditorUtility.SaveFilePanel(
                "Save Scene Hierarchy Report",
                Application.dataPath,
                BuildFileName(),
                "md");

            if (!string.IsNullOrWhiteSpace(path))
            {
                File.WriteAllText(path, report);
                lastReportPath = path;
            }
        }

        Debug.Log(
            $"[SceneHierarchyDiagnostics] Generated report. " +
            $"Objects={data.objects.Count}, Components={data.totalComponentCount}, MissingScripts={data.missingScriptEntries.Count}, Path='{lastReportPath}'");
    }

    private SceneHierarchyReportData ScanLoadedScenes()
    {
        var data = new SceneHierarchyReportData
        {
            generatedAtLocal = DateTime.Now,
            generatedAtUtc = DateTime.UtcNow,
            unityVersion = Application.unityVersion,
            projectName = Application.productName,
            isPlaying = Application.isPlaying
        };

        Dictionary<string, int> componentCounts = data.componentTypeCounts;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            ScanScene(scene, data, componentCounts, isDontDestroyOnLoadScene: false);
        }

        if (includeDontDestroyOnLoadScene && Application.isPlaying)
        {
            Scene ddolScene = TryGetDontDestroyOnLoadScene();

            if (ddolScene.IsValid() && ddolScene.isLoaded)
                ScanScene(ddolScene, data, componentCounts, isDontDestroyOnLoadScene: true);
        }

        BuildSuspicionData(data);

        return data;
    }

    private void ScanScene(
        Scene scene,
        SceneHierarchyReportData data,
        Dictionary<string, int> componentCounts,
        bool isDontDestroyOnLoadScene)
    {
        var sceneInfo = new SceneReportInfo
        {
            name = scene.name,
            path = scene.path,
            buildIndex = scene.buildIndex,
            isDirty = scene.isDirty,
            isLoaded = scene.isLoaded,
            isValid = scene.IsValid(),
            isDontDestroyOnLoadScene = isDontDestroyOnLoadScene
        };

        data.scenes.Add(sceneInfo);

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            ScanGameObjectRecursive(
                roots[i],
                null,
                0,
                sceneInfo,
                data,
                componentCounts);
        }
    }

    private void ScanGameObjectRecursive(
        GameObject go,
        GameObjectReportInfo parent,
        int depth,
        SceneReportInfo scene,
        SceneHierarchyReportData data,
        Dictionary<string, int> componentCounts)
    {
        if (go == null)
            return;

        if (!includeInactiveObjects && !go.activeInHierarchy)
            return;

        var info = new GameObjectReportInfo
        {
            gameObject = go,
            name = go.name,
            path = GetGameObjectPath(go),
            sceneName = scene.name,
            depth = depth,
            activeSelf = go.activeSelf,
            activeInHierarchy = go.activeInHierarchy,
            tag = go.tag,
            layer = go.layer,
            layerName = LayerMask.LayerToName(go.layer),
            isStatic = go.isStatic,
            parentPath = parent != null ? parent.path : null
        };

        if (includeTransformDetails)
            CaptureTransform(go.transform, info);

        Component[] components = go.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];

            if (component == null)
            {
                info.components.Add(ComponentReportInfo.Missing(i));

                data.missingScriptEntries.Add(new MissingScriptEntry
                {
                    gameObjectPath = info.path,
                    sceneName = scene.name,
                    componentIndex = i
                });

                continue;
            }

            Type type = component.GetType();
            string typeName = type.Name;
            string fullTypeName = type.FullName;

            if (!componentCounts.ContainsKey(typeName))
                componentCounts[typeName] = 0;

            componentCounts[typeName]++;
            data.totalComponentCount++;

            var componentInfo = new ComponentReportInfo
            {
                component = component,
                index = i,
                typeName = typeName,
                fullTypeName = fullTypeName,
                enabledState = GetEnabledState(component)
            };

            if (includeComponentSerializedFields)
                CaptureSerializedFields(component, componentInfo, data, info.path, scene.name);

            info.components.Add(componentInfo);

            TrackSpecialComponent(data, info, componentInfo);
        }

        data.objects.Add(info);
        scene.objectCount++;
        scene.componentCount += info.components.Count;

        for (int i = 0; i < go.transform.childCount; i++)
        {
            Transform child = go.transform.GetChild(i);
            ScanGameObjectRecursive(
                child.gameObject,
                info,
                depth + 1,
                scene,
                data,
                componentCounts);
        }
    }

    private void CaptureTransform(Transform transform, GameObjectReportInfo info)
    {
        info.localPosition = transform.localPosition;
        info.localRotationEuler = transform.localEulerAngles;
        info.localScale = transform.localScale;
        info.worldPosition = transform.position;
        info.worldRotationEuler = transform.eulerAngles;

        Vector3 scale = transform.localScale;

        if (Mathf.Approximately(scale.x, 0f) ||
            Mathf.Approximately(scale.y, 0f) ||
            Mathf.Approximately(scale.z, 0f))
        {
            info.suspicionNotes.Add("Transform has zero or near-zero local scale.");
        }

        if (Mathf.Abs(scale.x) > 100f ||
            Mathf.Abs(scale.y) > 100f ||
            Mathf.Abs(scale.z) > 100f)
        {
            info.suspicionNotes.Add("Transform has very large local scale.");
        }
    }

    private void CaptureSerializedFields(
        Component component,
        ComponentReportInfo componentInfo,
        SceneHierarchyReportData data,
        string gameObjectPath,
        string sceneName)
    {
        if (component == null)
            return;

        SerializedObject so;

        try
        {
            so = new SerializedObject(component);
        }
        catch (Exception ex)
        {
            componentInfo.serializedFields.Add(new SerializedFieldReportInfo
            {
                propertyPath = "<SerializedObject failed>",
                value = ex.Message,
                depth = 0
            });

            return;
        }

        SerializedProperty iterator = so.GetIterator();

        bool enterChildren = true;
        int count = 0;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (count >= maxSerializedFieldsPerComponent)
            {
                componentInfo.serializedFields.Add(new SerializedFieldReportInfo
                {
                    propertyPath = "...",
                    displayName = "...",
                    value = $"truncated after {maxSerializedFieldsPerComponent} fields",
                    depth = 0
                });

                break;
            }

            if (!includeNullObjectReferenceFields &&
                iterator.propertyType == SerializedPropertyType.ObjectReference &&
                iterator.objectReferenceValue == null &&
                iterator.propertyPath != "m_Script")
            {
                continue;
            }

            var field = new SerializedFieldReportInfo
            {
                propertyPath = iterator.propertyPath,
                displayName = iterator.displayName,
                propertyType = iterator.propertyType.ToString(),
                depth = iterator.depth,
                value = FormatSerializedProperty(iterator)
            };

            componentInfo.serializedFields.Add(field);
            count++;

            if (IsStableIdProperty(iterator))
            {
                string stableId = iterator.stringValue;

                if (!string.IsNullOrWhiteSpace(stableId))
                {
                    data.stableIdEntries.Add(new StableIdEntry
                    {
                        stableId = stableId,
                        propertyPath = iterator.propertyPath,
                        componentType = component.GetType().Name,
                        gameObjectPath = gameObjectPath,
                        sceneName = sceneName
                    });
                }
            }

            if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                iterator.objectReferenceValue == null &&
                iterator.propertyPath != "m_Script")
            {
                data.nullReferenceEntries.Add(new NullReferenceEntry
                {
                    gameObjectPath = gameObjectPath,
                    sceneName = sceneName,
                    componentType = component.GetType().Name,
                    propertyPath = iterator.propertyPath
                });
            }
        }
    }

    private void TrackSpecialComponent(
        SceneHierarchyReportData data,
        GameObjectReportInfo goInfo,
        ComponentReportInfo componentInfo)
    {
        string type = componentInfo.typeName;

        if (LooksSingletonLike(type))
        {
            data.singletonLikeEntries.Add(new SingletonLikeEntry
            {
                typeName = type,
                gameObjectPath = goInfo.path,
                sceneName = goInfo.sceneName
            });
        }

        if (type == "AgentController" ||
            type == "AgentSpawnPoint" ||
            type == "AgentSceneSpawner" ||
            type == "GameState" ||
            type == "SceneTransitionController" ||
            type == "Boat" ||
            type == "BoatRegistry" ||
            type == "MoneyChestTreasuryService")
        {
            data.importantComponents.Add(new ImportantComponentEntry
            {
                typeName = type,
                gameObjectPath = goInfo.path,
                sceneName = goInfo.sceneName
            });
        }

        if (type.Contains("Debug", StringComparison.OrdinalIgnoreCase) ||
            goInfo.name.Contains("Debug", StringComparison.OrdinalIgnoreCase) ||
            goInfo.name.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
            goInfo.name.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            data.debugOrTemporaryEntries.Add(new DebugOrTemporaryEntry
            {
                typeName = type,
                gameObjectPath = goInfo.path,
                sceneName = goInfo.sceneName
            });
        }
    }

    private void BuildSuspicionData(SceneHierarchyReportData data)
    {
        data.duplicateStableIdGroups =
            data.stableIdEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.stableId))
                .GroupBy(e => e.stableId)
                .Where(g => g.Count() > 1)
                .Select(g => g.ToList())
                .ToList();

        data.duplicateSingletonLikeGroups =
            data.singletonLikeEntries
                .GroupBy(e => e.typeName)
                .Where(g => g.Count() > 1)
                .Select(g => g.ToList())
                .ToList();
    }

    private string BuildMarkdownReport(SceneHierarchyReportData data)
    {
        var sb = new StringBuilder(128 * 1024);

        sb.AppendLine("# Scene Hierarchy Diagnostics Report");
        sb.AppendLine();
        sb.AppendLine($"Generated Local: `{data.generatedAtLocal:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"Generated UTC: `{data.generatedAtUtc:O}`");
        sb.AppendLine($"Unity: `{data.unityVersion}`");
        sb.AppendLine($"Project: `{data.projectName}`");
        sb.AppendLine($"Play Mode: `{data.isPlaying}`");
        sb.AppendLine();

        AppendSceneSummary(sb, data);

        if (includeSuspicionReport)
            AppendSuspicionReport(sb, data);

        if (includeComponentTypeSummary)
            AppendComponentTypeSummary(sb, data);

        AppendImportantComponents(sb, data);
        AppendHierarchy(sb, data);

        return sb.ToString();
    }

    private void AppendSceneSummary(StringBuilder sb, SceneHierarchyReportData data)
    {
        sb.AppendLine("## Scene Summary");
        sb.AppendLine();

        sb.AppendLine("| Scene | Path | Build Index | Loaded | Dirty | Objects | Components | DDOL |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");

        for (int i = 0; i < data.scenes.Count; i++)
        {
            SceneReportInfo scene = data.scenes[i];

            sb.AppendLine(
                $"| `{EscapeTable(scene.name)}` | `{EscapeTable(scene.path)}` | {scene.buildIndex} | {scene.isLoaded} | {scene.isDirty} | {scene.objectCount} | {scene.componentCount} | {scene.isDontDestroyOnLoadScene} |");
        }

        sb.AppendLine();
        sb.AppendLine($"Total Objects: `{data.objects.Count}`");
        sb.AppendLine($"Total Components: `{data.totalComponentCount}`");
        sb.AppendLine($"Missing Scripts: `{data.missingScriptEntries.Count}`");
        sb.AppendLine($"Stable ID Entries: `{data.stableIdEntries.Count}`");
        sb.AppendLine($"Duplicate Stable ID Groups: `{data.duplicateStableIdGroups.Count}`");
        sb.AppendLine();
    }

    private void AppendSuspicionReport(StringBuilder sb, SceneHierarchyReportData data)
    {
        sb.AppendLine("## Suspicion Report");
        sb.AppendLine();

        if (data.missingScriptEntries.Count == 0 &&
            data.duplicateStableIdGroups.Count == 0 &&
            data.duplicateSingletonLikeGroups.Count == 0 &&
            data.nullReferenceEntries.Count == 0 &&
            data.debugOrTemporaryEntries.Count == 0)
        {
            sb.AppendLine("No obvious suspicious entries found. Suspicious, frankly.");
            sb.AppendLine();
            return;
        }

        if (data.missingScriptEntries.Count > 0)
        {
            sb.AppendLine("### Missing Scripts");
            sb.AppendLine();

            for (int i = 0; i < data.missingScriptEntries.Count; i++)
            {
                MissingScriptEntry entry = data.missingScriptEntries[i];
                sb.AppendLine($"- `{entry.sceneName}` `{entry.gameObjectPath}` component index `{entry.componentIndex}`");
            }

            sb.AppendLine();
        }

        if (data.duplicateStableIdGroups.Count > 0)
        {
            sb.AppendLine("### Duplicate Stable IDs");
            sb.AppendLine();

            for (int g = 0; g < data.duplicateStableIdGroups.Count; g++)
            {
                List<StableIdEntry> group = data.duplicateStableIdGroups[g];
                sb.AppendLine($"- Stable ID `{group[0].stableId}` appears `{group.Count}` times:");

                for (int i = 0; i < group.Count; i++)
                {
                    StableIdEntry entry = group[i];
                    sb.AppendLine($"  - `{entry.sceneName}` `{entry.gameObjectPath}` `{entry.componentType}.{entry.propertyPath}`");
                }
            }

            sb.AppendLine();
        }

        if (data.duplicateSingletonLikeGroups.Count > 0)
        {
            sb.AppendLine("### Duplicate Singleton-Like Components");
            sb.AppendLine();

            for (int g = 0; g < data.duplicateSingletonLikeGroups.Count; g++)
            {
                List<SingletonLikeEntry> group = data.duplicateSingletonLikeGroups[g];
                sb.AppendLine($"- `{group[0].typeName}` appears `{group.Count}` times:");

                for (int i = 0; i < group.Count; i++)
                    sb.AppendLine($"  - `{group[i].sceneName}` `{group[i].gameObjectPath}`");
            }

            sb.AppendLine();
        }

        if (data.nullReferenceEntries.Count > 0)
        {
            sb.AppendLine("### Null Object Reference Fields");
            sb.AppendLine();

            for (int i = 0; i < data.nullReferenceEntries.Count; i++)
            {
                NullReferenceEntry entry = data.nullReferenceEntries[i];

                sb.AppendLine(
                    $"- `{entry.sceneName}` `{entry.gameObjectPath}` `{entry.componentType}.{entry.propertyPath}`");
            }

            sb.AppendLine();
        }

        if (data.debugOrTemporaryEntries.Count > 0)
        {
            sb.AppendLine("### Debug / Temp / Test Named Objects or Components");
            sb.AppendLine();

            for (int i = 0; i < data.debugOrTemporaryEntries.Count; i++)
            {
                DebugOrTemporaryEntry entry = data.debugOrTemporaryEntries[i];

                sb.AppendLine(
                    $"- `{entry.sceneName}` `{entry.gameObjectPath}` `{entry.typeName}`");
            }

            sb.AppendLine();
        }
    }

    private void AppendComponentTypeSummary(StringBuilder sb, SceneHierarchyReportData data)
    {
        sb.AppendLine("## Component Type Summary");
        sb.AppendLine();

        foreach (KeyValuePair<string, int> pair in data.componentTypeCounts.OrderByDescending(p => p.Value))
            sb.AppendLine($"- `{pair.Key}`: `{pair.Value}`");

        sb.AppendLine();
    }

    private void AppendImportantComponents(StringBuilder sb, SceneHierarchyReportData data)
    {
        sb.AppendLine("## Important Components");
        sb.AppendLine();

        if (data.importantComponents.Count == 0)
        {
            sb.AppendLine("No tracked important components found.");
            sb.AppendLine();
            return;
        }

        foreach (ImportantComponentEntry entry in data.importantComponents.OrderBy(e => e.typeName).ThenBy(e => e.gameObjectPath))
            sb.AppendLine($"- `{entry.typeName}` in `{entry.sceneName}` at `{entry.gameObjectPath}`");

        sb.AppendLine();
    }

    private void AppendHierarchy(StringBuilder sb, SceneHierarchyReportData data)
    {
        sb.AppendLine("## Full Hierarchy");
        sb.AppendLine();

        foreach (SceneReportInfo scene in data.scenes)
        {
            sb.AppendLine($"### Scene: `{scene.name}`");
            sb.AppendLine();

            List<GameObjectReportInfo> sceneObjects =
                data.objects
                    .Where(o => o.sceneName == scene.name)
                    .OrderBy(o => o.path)
                    .ToList();

            for (int i = 0; i < sceneObjects.Count; i++)
            {
                GameObjectReportInfo obj = sceneObjects[i];
                AppendGameObject(sb, obj);
            }

            sb.AppendLine();
        }
    }

    private void AppendGameObject(StringBuilder sb, GameObjectReportInfo obj)
    {
        string indent = new string(' ', Mathf.Max(0, obj.depth) * 2);

        sb.AppendLine(
            $"{indent}- `{obj.name}` | path=`{obj.path}` | activeSelf={obj.activeSelf} | activeInHierarchy={obj.activeInHierarchy} | layer=`{obj.layerName}` | tag=`{obj.tag}` | static={obj.isStatic}");

        if (includeTransformDetails)
        {
            sb.AppendLine($"{indent}  - Transform:");
            sb.AppendLine($"{indent}    - localPosition: `{FormatVector3(obj.localPosition)}`");
            sb.AppendLine($"{indent}    - worldPosition: `{FormatVector3(obj.worldPosition)}`");
            sb.AppendLine($"{indent}    - localRotationEuler: `{FormatVector3(obj.localRotationEuler)}`");
            sb.AppendLine($"{indent}    - worldRotationEuler: `{FormatVector3(obj.worldRotationEuler)}`");
            sb.AppendLine($"{indent}    - localScale: `{FormatVector3(obj.localScale)}`");
        }

        if (obj.suspicionNotes.Count > 0)
        {
            sb.AppendLine($"{indent}  - Suspicion Notes:");

            for (int i = 0; i < obj.suspicionNotes.Count; i++)
                sb.AppendLine($"{indent}    - {obj.suspicionNotes[i]}");
        }

        if (obj.components.Count == 0)
        {
            sb.AppendLine($"{indent}  - Components: none");
            return;
        }

        sb.AppendLine($"{indent}  - Components:");

        for (int i = 0; i < obj.components.Count; i++)
        {
            ComponentReportInfo component = obj.components[i];

            if (component.missingScript)
            {
                sb.AppendLine($"{indent}    - `[Missing Script]` index={component.index}");
                continue;
            }

            sb.AppendLine(
                $"{indent}    - `{component.typeName}` index={component.index} enabled=`{component.enabledState}`");

            if (!includeComponentSerializedFields || component.serializedFields.Count == 0)
                continue;

            for (int f = 0; f < component.serializedFields.Count; f++)
            {
                SerializedFieldReportInfo field = component.serializedFields[f];
                string fieldIndent = new string(' ', Mathf.Max(0, field.depth) * 2);

                sb.AppendLine(
                    $"{indent}      {fieldIndent}- `{field.propertyPath}` ({field.propertyType}) = `{EscapeBackticks(field.value)}`");
            }
        }
    }

    private string SaveReportToAssets(string report)
    {
        string diagnosticsFolder = Path.Combine(Application.dataPath, DefaultFolderName);

        if (!Directory.Exists(diagnosticsFolder))
            Directory.CreateDirectory(diagnosticsFolder);

        string path = Path.Combine(diagnosticsFolder, BuildFileName());
        File.WriteAllText(path, report);

        return path;
    }

    private string BuildFileName()
    {
        return $"SceneHierarchyDiagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.md";
    }

    private static Scene TryGetDontDestroyOnLoadScene()
    {
        if (!Application.isPlaying)
            return default;

        GameObject temp = new GameObject("__SceneDiagnostics_DDOL_Temp");
        DontDestroyOnLoad(temp);

        Scene scene = temp.scene;
        DestroyImmediate(temp);

        return scene;
    }

    private static string GetGameObjectPath(GameObject go)
    {
        if (go == null)
            return "<null>";

        var stack = new Stack<string>();
        Transform current = go.transform;

        while (current != null)
        {
            stack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", stack);
    }

    private string FormatSerializedProperty(SerializedProperty property)
    {
        try
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();

                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();

                case SerializedPropertyType.Float:
                    return property.floatValue.ToString("0.###");

                case SerializedPropertyType.String:
                    return Truncate(property.stringValue, maxStringLength);

                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();

                case SerializedPropertyType.ObjectReference:
                    return FormatObjectReference(property.objectReferenceValue);

                case SerializedPropertyType.LayerMask:
                    return property.intValue.ToString();

                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames != null &&
                           property.enumValueIndex >= 0 &&
                           property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString();

                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();

                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();

                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();

                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();

                case SerializedPropertyType.ArraySize:
                    return property.intValue.ToString();

                case SerializedPropertyType.Character:
                    return ((char)property.intValue).ToString();

                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue != null
                        ? $"keys={property.animationCurveValue.length}"
                        : "null";

                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();

                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.eulerAngles.ToString();

                case SerializedPropertyType.ExposedReference:
                    return FormatObjectReference(property.exposedReferenceValue);

                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.ToString();

                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.ToString();

                case SerializedPropertyType.RectInt:
                    return property.rectIntValue.ToString();

                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue.ToString();

                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceValue != null
                        ? property.managedReferenceFullTypename
                        : "null";

                case SerializedPropertyType.Generic:
                    if (property.isArray)
                        return $"array size={property.arraySize}";

                    return "<generic>";

                default:
                    return "<unsupported>";
            }
        }
        catch (Exception ex)
        {
            return $"<error: {ex.Message}>";
        }
    }

    private string FormatObjectReference(Object obj)
    {
        if (obj == null)
            return "null";

        string assetPath = AssetDatabase.GetAssetPath(obj);

        if (!string.IsNullOrWhiteSpace(assetPath))
            return Truncate($"{obj.name} ({obj.GetType().Name}) asset='{assetPath}'", maxObjectReferencePathLength);

        if (obj is GameObject go)
            return Truncate($"{go.name} (GameObject) scenePath='{GetGameObjectPath(go)}'", maxObjectReferencePathLength);

        if (obj is Component component)
            return Truncate($"{component.name} ({component.GetType().Name}) scenePath='{GetGameObjectPath(component.gameObject)}'", maxObjectReferencePathLength);

        return Truncate($"{obj.name} ({obj.GetType().Name})", maxObjectReferencePathLength);
    }

    private static string GetEnabledState(Component component)
    {
        switch (component)
        {
            case Behaviour behaviour:
                return behaviour.enabled.ToString();

            case Renderer renderer:
                return renderer.enabled.ToString();

            case Collider collider:
                return collider.enabled.ToString();

            default:
                return "n/a";
        }
    }

    private static bool IsStableIdProperty(SerializedProperty property)
    {
        if (property.propertyType != SerializedPropertyType.String)
            return false;

        string path = property.propertyPath;

        return string.Equals(path, "stableId", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".stableId", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "StableId", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".StableId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSingletonLike(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        return typeName.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Service", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Registry", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase) ||
               typeName == "GameState" ||
               typeName == "SceneTransitionController";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";
    }

    private string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        maxLength = Mathf.Max(4, maxLength);

        if (value.Length <= maxLength)
            return value;

        return value.Substring(0, maxLength - 3) + "...";
    }

    private static string EscapeTable(string value)
    {
        return (value ?? string.Empty).Replace("|", "\\|");
    }

    private static string EscapeBackticks(string value)
    {
        return (value ?? string.Empty).Replace("`", "'");
    }

    private static int CountLines(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int count = 1;

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\n')
                count++;
        }

        return count;
    }

    private static string AbsoluteToProjectRelativePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return null;

        absolutePath = absolutePath.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");

        if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return absolutePath;

        return "Assets" + absolutePath.Substring(dataPath.Length);
    }

    private sealed class SceneHierarchyReportData
    {
        public DateTime generatedAtLocal;
        public DateTime generatedAtUtc;
        public string unityVersion;
        public string projectName;
        public bool isPlaying;

        public readonly List<SceneReportInfo> scenes = new();
        public readonly List<GameObjectReportInfo> objects = new();

        public readonly Dictionary<string, int> componentTypeCounts = new();

        public readonly List<MissingScriptEntry> missingScriptEntries = new();
        public readonly List<StableIdEntry> stableIdEntries = new();
        public readonly List<NullReferenceEntry> nullReferenceEntries = new();
        public readonly List<SingletonLikeEntry> singletonLikeEntries = new();
        public readonly List<ImportantComponentEntry> importantComponents = new();
        public readonly List<DebugOrTemporaryEntry> debugOrTemporaryEntries = new();

        public List<List<StableIdEntry>> duplicateStableIdGroups = new();
        public List<List<SingletonLikeEntry>> duplicateSingletonLikeGroups = new();

        public int totalComponentCount;
    }

    private sealed class SceneReportInfo
    {
        public string name;
        public string path;
        public int buildIndex;
        public bool isDirty;
        public bool isLoaded;
        public bool isValid;
        public bool isDontDestroyOnLoadScene;

        public int objectCount;
        public int componentCount;
    }

    private sealed class GameObjectReportInfo
    {
        public GameObject gameObject;
        public string name;
        public string path;
        public string parentPath;
        public string sceneName;
        public int depth;

        public bool activeSelf;
        public bool activeInHierarchy;
        public string tag;
        public int layer;
        public string layerName;
        public bool isStatic;

        public Vector3 localPosition;
        public Vector3 worldPosition;
        public Vector3 localRotationEuler;
        public Vector3 worldRotationEuler;
        public Vector3 localScale;

        public readonly List<ComponentReportInfo> components = new();
        public readonly List<string> suspicionNotes = new();
    }

    private sealed class ComponentReportInfo
    {
        public Component component;
        public int index;
        public string typeName;
        public string fullTypeName;
        public string enabledState;
        public bool missingScript;

        public readonly List<SerializedFieldReportInfo> serializedFields = new();

        public static ComponentReportInfo Missing(int index)
        {
            return new ComponentReportInfo
            {
                index = index,
                typeName = "<Missing Script>",
                fullTypeName = "<Missing Script>",
                enabledState = "n/a",
                missingScript = true
            };
        }
    }

    private sealed class SerializedFieldReportInfo
    {
        public string propertyPath;
        public string displayName;
        public string propertyType;
        public string value;
        public int depth;
    }

    private sealed class MissingScriptEntry
    {
        public string sceneName;
        public string gameObjectPath;
        public int componentIndex;
    }

    private sealed class StableIdEntry
    {
        public string stableId;
        public string propertyPath;
        public string componentType;
        public string gameObjectPath;
        public string sceneName;
    }

    private sealed class NullReferenceEntry
    {
        public string sceneName;
        public string gameObjectPath;
        public string componentType;
        public string propertyPath;
    }

    private sealed class SingletonLikeEntry
    {
        public string typeName;
        public string sceneName;
        public string gameObjectPath;
    }

    private sealed class ImportantComponentEntry
    {
        public string typeName;
        public string sceneName;
        public string gameObjectPath;
    }

    private sealed class DebugOrTemporaryEntry
    {
        public string typeName;
        public string sceneName;
        public string gameObjectPath;
    }
}

#endif