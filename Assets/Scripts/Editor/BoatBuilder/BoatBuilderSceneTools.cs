#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BoatBuilderSceneTools
{
    public struct Context
    {
        public BoatKit Kit;
        public BoatBuilderWindow.Tool ActiveTool;
        public float GridSize;
        public float ZPlane;
        public bool AutoParentToBoatRoot;
        public bool SnapOnPlace;

        public bool ShowSnapPreview;
        public bool EnforceRequiredPieces;
        public int RequiredPlayerSpawnPoints;

        public float BoardedVolumePadding;
        public float BoardedVolumeExtraUp;
        public float BoardedVolumeExtraDown;

        public HardpointType SelectedHardpointType;
        public string HardpointIdPrefix;
        public bool HardpointAutoCreateMountPoint;
        public bool HardpointRenameObjectToId;
    }

    private static Context _ctx;
    private static bool _attached;

    public static bool IsPlacementEnabled { get; private set; }

    private static GameObject _previewInstance;
    private static GameObject _previewPrefabAsset;
    private static Vector3 _previewLastPos;
    private static bool _previewVisible;

    static BoatBuilderSceneTools()
    {
        Attach();
    }

    public static void Attach()
    {
        if (_attached) return;
        SceneView.duringSceneGui += OnSceneGUI;
        _attached = true;
    }

    public static void SetContext(Context ctx)
    {
        if (ctx.BoardedVolumePadding <= 0f) ctx.BoardedVolumePadding = 0f;
        if (ctx.BoardedVolumeExtraUp <= 0f) ctx.BoardedVolumeExtraUp = 5.0f;
        if (ctx.BoardedVolumeExtraDown <= 0f) ctx.BoardedVolumeExtraDown = 0f;

        if (string.IsNullOrWhiteSpace(ctx.HardpointIdPrefix))
            ctx.HardpointIdPrefix = "hardpoint";

        _ctx = ctx;
        SceneView.RepaintAll();
    }

    public static void TogglePlacement()
    {
        IsPlacementEnabled = !IsPlacementEnabled;
        if (!IsPlacementEnabled) DestroyPreview();
        SceneView.RepaintAll();
    }

    public static void EnablePlacement()
    {
        IsPlacementEnabled = true;
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Boat Builder/Snap Selection %#g")]
    public static void SnapSelectionNow()
    {
        var tr = Selection.transforms;
        if (tr == null || tr.Length == 0) return;

        Undo.RecordObjects(tr, "Snap Selection to Grid");
        foreach (var t in tr)
        {
            var p = t.position;
            t.position = Snap(p, _ctx.GridSize);
            EditorUtility.SetDirty(t);
        }
    }

    public static Transform PeekBestBoatRoot() => FindBestBoatRootParent();

    public static void GetRequiredPiecesStatus(
        Transform boatRoot,
        out bool hasBoatBoardObject,
        out bool hasMapTable,
        out int spawnPointCount,
        out bool hasBoardedVolume)
    {
        hasBoatBoardObject = false;
        hasMapTable = false;
        hasBoardedVolume = false;
        spawnPointCount = 0;

        if (boatRoot == null) return;

        hasBoatBoardObject = boatRoot.GetComponentsInChildren<BoatBoardingInteractable>(true).Any();
        hasMapTable = boatRoot.GetComponentsInChildren<MapTableInteractable>(true).Any();
        hasBoardedVolume = boatRoot.GetComponentsInChildren<BoatBoardedVolume>(true).Any();

        var trs = boatRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            var n = trs[i].name;
            if (string.Equals(n, "PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase))
            {
                spawnPointCount++;
            }
        }
    }

    public static bool TryGetFirstMissingRequiredTool(Transform boatRoot, out BoatBuilderWindow.Tool tool)
    {
        tool = default;
        if (boatRoot == null) return false;

        GetRequiredPiecesStatus(boatRoot, out var hasBoard, out var hasMap, out var spawnCount, out var hasVol);

        if (!hasVol) { tool = BoatBuilderWindow.Tool.BoardedVolume; return true; }
        if (spawnCount < Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints)) { tool = BoatBuilderWindow.Tool.PlayerSpawnPoint; return true; }
        if (!hasBoard) { tool = BoatBuilderWindow.Tool.BoatBoardObject; return true; }
        if (!hasMap) { tool = BoatBuilderWindow.Tool.MapTable; return true; }

        return false;
    }

    private static void OnSceneGUI(SceneView view)
    {
        view.wantsMouseMove = true;

        if (!IsPlacementEnabled)
        {
            if (_previewVisible) DestroyPreview();
            return;
        }

        if (_ctx.Kit == null)
        {
            DrawStatus(view, "BoatKit missing. Assign one in Tools > Boat Builder > Window.");
            DestroyPreview();
            return;
        }

        var prefab = GetPrefabForTool(_ctx.Kit, _ctx.ActiveTool, _ctx.SelectedHardpointType);
        if (prefab == null)
        {
            DrawStatus(view, $"Missing prefab reference for {_ctx.ActiveTool} in BoatKit.");
            DestroyPreview();
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var e = Event.current;

        string placingLabel = _ctx.ActiveTool == BoatBuilderWindow.Tool.Hardpoint
            ? $"PLACING: {_ctx.ActiveTool} ({_ctx.SelectedHardpointType})"
            : $"PLACING: {_ctx.ActiveTool}";

        DrawStatus(view, $"{placingLabel} | Left-click to place | Right-click/Esc to cancel");

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            IsPlacementEnabled = false;
            DestroyPreview();
            e.Use();
            SceneView.RepaintAll();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            IsPlacementEnabled = false;
            DestroyPreview();
            e.Use();
            SceneView.RepaintAll();
            return;
        }

        if (_ctx.ShowSnapPreview && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint || e.type == EventType.Layout))
        {
            var world = MouseToWorldOnZPlane(e.mousePosition, _ctx.ZPlane);
            if (_ctx.SnapOnPlace) world = Snap(world, _ctx.GridSize);
            UpdatePreview(prefab, world);

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Layout)
                view.Repaint();
        }
        else
        {
            if (_previewVisible) DestroyPreview();
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            var world = MouseToWorldOnZPlane(e.mousePosition, _ctx.ZPlane);
            if (_ctx.SnapOnPlace) world = Snap(world, _ctx.GridSize);

            var parent = _ctx.AutoParentToBoatRoot ? FindBestBoatRootParent() : null;

            if (_ctx.EnforceRequiredPieces && parent != null)
            {
                if (HandleRequiredDuplicateBlock(parent, _ctx.ActiveTool))
                {
                    e.Use();
                    SceneView.RepaintAll();
                    return;
                }
            }

            var placed = PlacePrefab(prefab, world, parent);

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.BoardedVolume && parent != null)
            {
                AutoFitBoardedVolume(parent, placed, _ctx.BoardedVolumePadding, _ctx.BoardedVolumeExtraUp, _ctx.BoardedVolumeExtraDown);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.Hardpoint)
            {
                InitializePlacedHardpoint(
                    placed,
                    parent != null ? parent : placed.transform.parent,
                    _ctx.SelectedHardpointType,
                    _ctx.HardpointIdPrefix,
                    _ctx.HardpointAutoCreateMountPoint,
                    _ctx.HardpointRenameObjectToId);
            }

            e.Use();
            SceneView.RepaintAll();
        }
    }

    private static bool HandleRequiredDuplicateBlock(Transform boatRoot, BoatBuilderWindow.Tool tool)
    {
        switch (tool)
        {
            case BoatBuilderWindow.Tool.BoardedVolume:
                {
                    var existing = boatRoot.GetComponentInChildren<BoatBoardedVolume>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] BoardedVolume already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.BoatBoardObject:
                {
                    var existing = boatRoot.GetComponentInChildren<BoatBoardingInteractable>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] BoatBoardObject already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.MapTable:
                {
                    var existing = boatRoot.GetComponentInChildren<MapTableInteractable>(true);
                    if (existing != null)
                    {
                        Selection.activeObject = existing.gameObject;
                        EditorGUIUtility.PingObject(existing.gameObject);
                        Debug.LogWarning("[BoatBuilder] MapTable already exists under boat root. Selecting existing instead.");
                        return true;
                    }
                    break;
                }
            case BoatBuilderWindow.Tool.PlayerSpawnPoint:
                {
                    int count = CountSpawnPoints(boatRoot);
                    int req = Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints);
                    if (count >= req)
                    {
                        Debug.LogWarning($"[BoatBuilder] PlayerSpawnPoint count is already {count} (required {req}). Placing more is allowed but usually unnecessary.");
                    }
                    break;
                }
        }

        return false;
    }

    private static int CountSpawnPoints(Transform boatRoot)
    {
        if (boatRoot == null) return 0;
        var trs = boatRoot.GetComponentsInChildren<Transform>(true);
        int c = 0;
        for (int i = 0; i < trs.Length; i++)
        {
            var n = trs[i].name;
            if (string.Equals(n, "PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase) ||
                n.StartsWith("PlayerSpawnPoint", StringComparison.OrdinalIgnoreCase))
            {
                c++;
            }
        }
        return c;
    }

    private static GameObject PlacePrefab(GameObject prefab, Vector3 worldPos, Transform parent)
    {
        if (prefab == null)
            return null;

        GameObject instance = null;

        if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        else
            instance = UnityEngine.Object.Instantiate(prefab);

        if (instance == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");

        instance.transform.position = worldPos;
        instance.transform.rotation = prefab.transform.rotation;

        if (parent != null)
            Undo.SetTransformParent(instance.transform, parent, "Parent placed object to BoatRoot");

        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return instance;
    }

    private static void AutoFitBoardedVolume(Transform boatRoot, GameObject placed, float padding, float extraUp, float extraDown)
    {
        if (boatRoot == null || placed == null)
            return;

        var box = placed.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            Debug.LogWarning("[BoatBuilder] BoardedVolume placed, but no BoxCollider2D found to auto-fit.", placed);
            return;
        }

        var renderers = boatRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null && r.gameObject != placed)
            .ToArray();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("[BoatBuilder] Could not auto-fit BoardedVolume: no renderers found under boat root.", boatRoot);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        bounds.Expand(new Vector3(padding * 2f, padding * 2f, 0f));
        bounds.min += new Vector3(0f, -extraDown, 0f);
        bounds.max += new Vector3(0f, extraUp, 0f);

        Undo.RecordObject(placed.transform, "Auto-fit BoardedVolume");
        Undo.RecordObject(box, "Resize BoardedVolume");

        placed.transform.position = new Vector3(bounds.center.x, bounds.center.y, placed.transform.position.z);

        Vector2 localSize = boatRoot.InverseTransformVector(bounds.size);
        box.offset = Vector2.zero;
        box.size = new Vector2(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y));

        EditorUtility.SetDirty(placed.transform);
        EditorUtility.SetDirty(box);
    }

    private static void InitializePlacedHardpoint(
        GameObject placed,
        Transform boatRoot,
        HardpointType selectedType,
        string idPrefix,
        bool autoCreateMountPoint,
        bool renameObjectToId)
    {
        if (placed == null)
            return;

        Hardpoint hardpoint = placed.GetComponent<Hardpoint>();
        if (hardpoint == null)
        {
            Debug.LogWarning("[BoatBuilder] Placed hardpoint prefab has no Hardpoint component.", placed);
            return;
        }

        Undo.RecordObject(hardpoint, "Configure Hardpoint");

        SerializedObject hardpointSO = new SerializedObject(hardpoint);

        SerializedProperty typeProp = hardpointSO.FindProperty("hardpointType");
        if (typeProp != null)
            typeProp.enumValueIndex = (int)selectedType;

        string resolvedPrefix = ResolveHardpointPrefix(idPrefix, selectedType);
        string generatedId = GenerateNextHardpointId(boatRoot, resolvedPrefix);

        SerializedProperty idProp = hardpointSO.FindProperty("hardpointId");
        if (idProp != null)
            idProp.stringValue = generatedId;

        SerializedProperty mountProp = hardpointSO.FindProperty("mountPoint");
        if (mountProp != null)
        {
            Transform mount = placed.transform.Find("MountPoint");
            if (mount == null && autoCreateMountPoint)
            {
                var mountGO = new GameObject("MountPoint");
                Undo.RegisterCreatedObjectUndo(mountGO, "Create Hardpoint MountPoint");
                mount = mountGO.transform;
                Undo.SetTransformParent(mount, placed.transform, "Parent MountPoint");
                mount.localPosition = Vector3.zero;
                mount.localRotation = Quaternion.identity;
                mount.localScale = Vector3.one;
            }

            if (mount != null)
                mountProp.objectReferenceValue = mount;
        }

        hardpointSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hardpoint);

        var interactable = placed.GetComponent<HardpointInteractable>();
        if (interactable != null)
        {
            Undo.RecordObject(interactable, "Configure Hardpoint Interactable");
            SerializedObject interactableSO = new SerializedObject(interactable);

            SerializedProperty hpRef = interactableSO.FindProperty("hardpoint");
            if (hpRef != null)
                hpRef.objectReferenceValue = hardpoint;

            SerializedProperty promptAnchorProp = interactableSO.FindProperty("promptAnchor");
            if (promptAnchorProp != null && promptAnchorProp.objectReferenceValue == null)
                promptAnchorProp.objectReferenceValue = hardpoint.MountPoint;

            interactableSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(interactable);
        }

        if (renameObjectToId)
        {
            Undo.RecordObject(placed, "Rename Hardpoint");
            placed.name = generatedId;
            EditorUtility.SetDirty(placed);
        }

        Selection.activeGameObject = placed;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static string ResolveHardpointPrefix(string configuredPrefix, HardpointType type)
    {
        if (!string.IsNullOrWhiteSpace(configuredPrefix) &&
            !string.Equals(configuredPrefix.Trim(), "hardpoint", StringComparison.OrdinalIgnoreCase))
        {
            return SanitizeIdToken(configuredPrefix.Trim());
        }

        return type switch
        {
            HardpointType.Engine => "engine",
            HardpointType.Pump => "pump",
            HardpointType.Utility => "utility",
            HardpointType.Weapon => "weapon",
            HardpointType.Electronics => "electronics",
            HardpointType.Helm => "helm",
            _ => "hardpoint"
        };
    }

    private static string GenerateNextHardpointId(Transform boatRoot, string prefix)
    {
        prefix = SanitizeIdToken(prefix);
        int maxFound = 0;

        if (boatRoot != null)
        {
            var existing = boatRoot.GetComponentsInChildren<Hardpoint>(true);
            foreach (var hp in existing)
            {
                if (hp == null)
                    continue;

                string existingId = hp.HardpointId;
                if (string.IsNullOrWhiteSpace(existingId))
                    continue;

                if (!existingId.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = existingId.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out int n))
                    maxFound = Mathf.Max(maxFound, n);
            }
        }

        return $"{prefix}_{(maxFound + 1):00}";
    }

    private static string SanitizeIdToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "hardpoint";

        string s = value.Trim().ToLowerInvariant();
        s = s.Replace(" ", "_");
        return s;
    }

    private static void DrawStatus(SceneView view, string text)
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12, 12, 520, 32), EditorStyles.helpBox);
        GUILayout.Label(text, EditorStyles.boldLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static void UpdatePreview(GameObject prefab, Vector3 worldPos)
    {
        if (prefab == null)
        {
            DestroyPreview();
            return;
        }

        if (_previewInstance == null || _previewPrefabAsset != prefab)
        {
            DestroyPreview();

            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                _previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            else
                _previewInstance = UnityEngine.Object.Instantiate(prefab);

            if (_previewInstance == null)
                return;

            _previewPrefabAsset = prefab;
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;

            foreach (var col in _previewInstance.GetComponentsInChildren<Collider2D>(true))
                col.enabled = false;

            foreach (var sr in _previewInstance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.color = new Color(0f, 1f, 0f, 0.35f);
                sr.sortingOrder += 5000;
            }

            foreach (var mr in _previewInstance.GetComponentsInChildren<MeshRenderer>(true))
                mr.enabled = false;

            _previewVisible = true;
        }

        if (_previewInstance != null && _previewLastPos != worldPos)
        {
            _previewInstance.transform.position = worldPos;
            _previewLastPos = worldPos;
        }
    }

    private static void DestroyPreview()
    {
        _previewVisible = false;
        _previewPrefabAsset = null;
        _previewLastPos = default;

        if (_previewInstance != null)
        {
            try { UnityEngine.Object.DestroyImmediate(_previewInstance); }
            catch { }
        }

        _previewInstance = null;
    }

    private static Vector3 MouseToWorldOnZPlane(Vector2 mousePos, float zPlane)
    {
        var ray = HandleUtility.GUIPointToWorldRay(mousePos);

        if (Mathf.Abs(ray.direction.z) < 0.0001f)
        {
            var o = ray.origin;
            o.z = zPlane;
            return o;
        }

        var t = (zPlane - ray.origin.z) / ray.direction.z;
        var p = ray.origin + ray.direction * t;
        p.z = zPlane;
        return p;
    }

    private static Vector3 Snap(Vector3 p, float grid)
    {
        float Snap1(float v) => Mathf.Round(v / grid) * grid;
        return new Vector3(Snap1(p.x), Snap1(p.y), p.z);
    }

    private static Transform FindBestBoatRootParent()
    {
        var go = Selection.activeGameObject;

        if (go != null)
        {
            var t = go.transform;
            Transform top = t;
            while (t != null)
            {
                top = t;
                var n = t.name.ToLowerInvariant();
                if (n.Contains("boat") || n.Contains("testboat") || n.Contains("boatroot"))
                    return t;
                t = t.parent;
            }
            return top;
        }

        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            var n = root.name.ToLowerInvariant();
            if (n.Contains("boat") || n.Contains("testboat") || n.Contains("boatroot"))
                return root.transform;
        }

        return null;
    }

    private static GameObject GetPrefabForTool(BoatKit kit, BoatBuilderWindow.Tool tool, HardpointType selectedHardpointType)
    {
        return tool switch
        {
            BoatBuilderWindow.Tool.HullSegment => kit.HullSegment,
            BoatBuilderWindow.Tool.Wall => kit.Wall,
            BoatBuilderWindow.Tool.Hatch => kit.Hatch,
            BoatBuilderWindow.Tool.PilotChair => kit.PilotChair,
            BoatBuilderWindow.Tool.CompartmentRect => kit.CompartmentRect,
            BoatBuilderWindow.Tool.Deck => kit.Deck,
            BoatBuilderWindow.Tool.Ladder => kit.Ladder,

            BoatBuilderWindow.Tool.BoatBoardObject => kit.BoatBoardObject,
            BoatBuilderWindow.Tool.MapTable => kit.MapTable,
            BoatBuilderWindow.Tool.PlayerSpawnPoint => kit.PlayerSpawnPoint,
            BoatBuilderWindow.Tool.BoardedVolume => kit.BoardedVolume,

            BoatBuilderWindow.Tool.Hardpoint => GetHardpointPrefab(kit, selectedHardpointType),
            _ => null
        };
    }

    private static GameObject GetHardpointPrefab(BoatKit kit, HardpointType type)
    {
        return type switch
        {
            HardpointType.Engine => kit.HardpointEngine,
            HardpointType.Pump => kit.HardpointPump,
            HardpointType.Utility => kit.HardpointUtility,
            HardpointType.Weapon => kit.HardpointWeapon,
            HardpointType.Electronics => kit.HardpointElectronics,
            HardpointType.Helm => kit.HardpointHelm,
            _ => null
        };
    }
}
#endif