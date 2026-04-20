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
        public Transform BoatRootOverride;

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

    private const float HatchSplitMinPieceWidth = 0.1f;
    private const float HatchSearchVerticalTolerance = 0.6f;

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

    private static Transform ResolveBoatRootParent()
    {
        if (_ctx.BoatRootOverride != null)
            return _ctx.BoatRootOverride;

        return FindBestBoatRootParent();
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

    public static Transform PeekBestBoatRoot() => ResolveBoatRootParent();

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

        string rootLabel = _ctx.BoatRootOverride != null
            ? $" | Root: {_ctx.BoatRootOverride.name}"
            : " | Root: auto";

        DrawStatus(view, $"{placingLabel}{rootLabel} | Left-click to place | Right-click/Esc to cancel");

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

            Transform boatRoot = _ctx.AutoParentToBoatRoot ? ResolveBoatRootParent() : null;
            Transform parent = boatRoot != null
                ? ResolvePlacementParentForTool(_ctx.ActiveTool, boatRoot)
                : null;

            if (_ctx.EnforceRequiredPieces && boatRoot != null)
            {
                if (HandleRequiredDuplicateBlock(boatRoot, _ctx.ActiveTool))
                {
                    e.Use();
                    SceneView.RepaintAll();
                    return;
                }
            }

            GameObject placed = null;

            if (_ctx.ActiveTool == BoatBuilderWindow.Tool.Hatch)
            {
                placed = TryPlaceHatchWithFloorSplit(prefab, world, parent);
            }
            else
            {
                placed = PlacePrefab(prefab, world, parent);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.BoardedVolume && boatRoot != null)
            {
                AutoFitBoardedVolume(boatRoot, placed, _ctx.BoardedVolumePadding, _ctx.BoardedVolumeExtraUp, _ctx.BoardedVolumeExtraDown);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.CompartmentRect)
            {
                InitializePlacedCompartment(
                    placed,
                    boatRoot != null ? boatRoot : placed.transform.parent);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.ExteriorShell)
            {
                InitializePlacedVisualMarker(placed, BoatVisualCategory.ExteriorShell);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.Hardpoint)
            {
                InitializePlacedHardpoint(
                    placed,
                    boatRoot != null ? boatRoot : placed.transform.parent,
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

    private static void InitializePlacedCompartment(GameObject placed, Transform boatRoot)
    {
        if (placed == null)
            return;

        if (boatRoot == null)
        {
            Debug.LogWarning("[BoatBuilder] Cannot register compartment: no BoatRoot resolved.", placed);
            return;
        }

        Boat boat = boatRoot.GetComponent<Boat>();
        if (boat == null)
        {
            boat = boatRoot.GetComponentInParent<Boat>();
        }

        if (boat == null)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Cannot register compartment '{placed.name}': no Boat component found on BoatRoot '{boatRoot.name}' or its parents.",
                placed);
            return;
        }

        Compartment[] compartments = placed.GetComponentsInChildren<Compartment>(true);
        if (compartments == null || compartments.Length == 0)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Placed CompartmentRect '{placed.name}' has no Compartment component.",
                placed);
            return;
        }

        Undo.RecordObject(boat, "Register placed compartment");

        int added = 0;

        foreach (Compartment compartment in compartments)
        {
            if (compartment == null)
                continue;

            if (boat.Compartments == null)
                boat.Compartments = new System.Collections.Generic.List<Compartment>();

            if (boat.Compartments.Contains(compartment))
                continue;

            boat.Compartments.Add(compartment);
            added++;

            EditorUtility.SetDirty(compartment);
        }

        EditorUtility.SetDirty(boat);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            $"[BoatBuilder] Registered {added} compartment(s) from '{placed.name}' to Boat '{boat.name}'.",
            placed);
    }

    public static void RebuildCompartmentsFromBoatRoot(Transform boatRoot)
    {
        if (boatRoot == null)
        {
            Debug.LogWarning("[BoatBuilder] Cannot rebuild compartments: BoatRoot is null.");
            return;
        }

        Boat boat = boatRoot.GetComponent<Boat>();
        if (boat == null)
            boat = boatRoot.GetComponentInParent<Boat>();

        if (boat == null)
        {
            Debug.LogWarning($"[BoatBuilder] Cannot rebuild compartments: no Boat component found for '{boatRoot.name}'.", boatRoot);
            return;
        }

        Compartment[] found = boatRoot.GetComponentsInChildren<Compartment>(true);

        Undo.RecordObject(boat, "Rebuild Boat Compartments");

        boat.Compartments = new System.Collections.Generic.List<Compartment>();

        foreach (Compartment c in found)
        {
            if (c == null)
                continue;

            if (!boat.Compartments.Contains(c))
                boat.Compartments.Add(c);
        }

        EditorUtility.SetDirty(boat);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[BoatBuilder] Rebuilt Boat.Compartments for '{boat.name}'. Found {boat.Compartments.Count} compartment(s).", boat);
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

            while (t != null)
            {
                var n = t.name.ToLowerInvariant();

                if (n.Contains("boat") || n.Contains("testboat") || n.Contains("boatroot"))
                    return t;

                t = t.parent;
            }
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
            BoatBuilderWindow.Tool.ExteriorShell => kit.ExteriorShell,
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

    private static GameObject TryPlaceHatchWithFloorSplit(GameObject hatchPrefab, Vector3 requestedWorldPos, Transform preferredParent)
    {
        if (hatchPrefab == null)
            return null;

        var hatchAuthoring = hatchPrefab.GetComponent<HatchAuthoring>();
        if (hatchAuthoring == null)
        {
            Debug.LogWarning("[BoatBuilder] Hatch prefab is missing HatchAuthoring. Cannot perform floor split placement.", hatchPrefab);
            return null;
        }

        float openingWidth = hatchAuthoring.OpeningWidth;
        float minPieceWidth = HatchSplitMinPieceWidth;

        if (!TryFindBestFloorSplit(
                requestedWorldPos,
                openingWidth,
                minPieceWidth,
                preferredParent,
                out var floor,
                out var split))
        {
            Debug.LogWarning("[BoatBuilder] No valid floor segment found under hatch placement. Hatch placement requires a splittable FloorSegmentAuthoring beneath it.");
            return null;
        }

        Transform actualParent = floor.transform.parent != null ? floor.transform.parent : preferredParent;
        Quaternion baseRot = floor.transform.rotation;

        Debug.Log(
            $"[BoatBuilder:Hatch] Splitting '{floor.name}' " +
            $"leftWidth={split.LeftWidth:F3}, rightWidth={split.RightWidth:F3}, " +
            $"leftCenter={split.LeftCenterX:F3}, rightCenter={split.RightCenterX:F3}",
            floor);

        // Create the new right-side piece first.
        // The original floor remains untouched until hatch + right piece are confirmed.
        GameObject rightPiece = CreateResizedFloorClone(
            floor,
            split.RightWidth,
            split.RightCenterX,
            actualParent,
            "_Right");

        if (rightPiece == null)
        {
            Debug.LogWarning("[BoatBuilder] Failed to create right split floor piece. Original floor preserved.", floor);
            return null;
        }

        Vector3 hatchWorldPos = new Vector3(
            requestedWorldPos.x,
            floor.WorldCenterY,
            requestedWorldPos.z);

        GameObject hatchInstance = PlacePrefab(hatchPrefab, hatchWorldPos, actualParent);

        if (hatchInstance == null)
        {
            Debug.LogWarning("[BoatBuilder] Hatch placement failed. Destroying right split clone. Original floor preserved.", floor);

            Undo.DestroyObjectImmediate(rightPiece);
            return null;
        }

        hatchInstance.transform.rotation = baseRot;

        // Now mutate the original floor into the left piece.
        // No deletion. No vanishing boat floor. Society heals, briefly.
        RecordFloorSegmentForUndo(floor, "Resize original floor into left split piece");

        floor.ApplyWidth(split.LeftWidth);
        floor.SetWorldCenterXPreservingColliderOffset(split.LeftCenterX);

        Undo.RecordObject(floor.gameObject, "Rename left split floor piece");
        floor.gameObject.name = floor.gameObject.name + "_Left";

        InitializePlacedHatch(hatchInstance, actualParent);

        EditorUtility.SetDirty(floor);
        EditorUtility.SetDirty(floor.gameObject);
        EditorUtility.SetDirty(rightPiece);
        EditorUtility.SetDirty(hatchInstance);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return hatchInstance;
    }

    private static bool TryFindBestFloorSplit(
    Vector3 requestedWorldPos,
    float openingWidth,
    float minPieceWidth,
    Transform preferredParent,
    out FloorSegmentAuthoring bestFloor,
    out FloorSplitUtility.SplitResult bestSplit)
    {
        bestFloor = null;
        bestSplit = default;

        var candidates = preferredParent != null
            ? preferredParent.GetComponentsInChildren<FloorSegmentAuthoring>(true)
            : UnityEngine.Object.FindObjectsByType<FloorSegmentAuthoring>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        float bestScore = float.PositiveInfinity;

        //Debug.Log($"[BoatBuilder:Hatch] Searching split. requested=({requestedWorldPos.x:F2},{requestedWorldPos.y:F2}) openingWidth={openingWidth:F2} minPieceWidth={minPieceWidth:F2} candidates={candidates.Length}");

        foreach (var floor in candidates)
        {
            if (floor == null)
                continue;

            float yDelta = Mathf.Abs(floor.transform.position.y - requestedWorldPos.y);
            if (yDelta > HatchSearchVerticalTolerance)
            {
                Debug.Log($"[BoatBuilder:Hatch] Reject floor '{floor.name}' due to Y delta. floorY={floor.transform.position.y:F2} requestedY={requestedWorldPos.y:F2} yDelta={yDelta:F2} tol={HatchSearchVerticalTolerance:F2}", floor);
                continue;
            }

            float segLeft = floor.WorldLeftX;
            float segRight = floor.WorldRightX;
            float openLeft = requestedWorldPos.x - openingWidth * 0.5f;
            float openRight = requestedWorldPos.x + openingWidth * 0.5f;

            //Debug.Log(
            //    $"[BoatBuilder:Hatch] Candidate '{floor.name}' floorWidth={floor.Width:F2} floorX={floor.transform.position.x:F2} seg=[{segLeft:F2},{segRight:F2}] opening=[{openLeft:F2},{openRight:F2}]",
            //    floor);

            if (!FloorSplitUtility.TryComputeSplit(
                    floor,
                    requestedWorldPos.x,
                    openingWidth,
                    minPieceWidth,
                    out var split))
            {
                Debug.LogWarning(
                    $"[BoatBuilder:Hatch] Reject floor '{floor.name}' because split is invalid. " +
                    $"Likely causes: opening wider than allowed span, click too close to edge, or minPieceWidth too large. " +
                    $"floorWidth={floor.Width:F2} openingWidth={openingWidth:F2} minPieceWidth={minPieceWidth:F2}",
                    floor);
                continue;
            }

            float centerDelta = Mathf.Abs(floor.WorldCenterX - requestedWorldPos.x);
            float score = yDelta * 10f + centerDelta;

            Debug.Log(
                $"[BoatBuilder:Hatch] VALID split on '{floor.name}'. leftWidth={split.LeftWidth:F2} rightWidth={split.RightWidth:F2} score={score:F2}",
                floor);

            if (score < bestScore)
            {
                bestScore = score;
                bestFloor = floor;
                bestSplit = split;
            }
        }

        return bestFloor != null && bestSplit.IsValid;
    }

    private static GameObject CreateResizedFloorClone(
    FloorSegmentAuthoring sourceFloor,
    float newWidth,
    float worldCenterX,
    Transform parent,
    string nameSuffix)
    {
        if (sourceFloor == null)
            return null;

        if (newWidth <= 0.01f)
        {
            Debug.LogWarning($"[BoatBuilder] Refusing to create split floor clone with invalid width {newWidth:F3}.", sourceFloor);
            return null;
        }

        Transform resolvedParent = parent != null ? parent : sourceFloor.transform.parent;

        // Instantiate directly under the intended parent while preserving world pose.
        // This avoids the "instantiate, then reparent, then pray" ritual.
        GameObject piece = UnityEngine.Object.Instantiate(
            sourceFloor.gameObject,
            sourceFloor.transform.position,
            sourceFloor.transform.rotation,
            resolvedParent);

        if (piece == null)
            return null;

        Undo.RegisterCreatedObjectUndo(piece, $"Create split floor clone {nameSuffix}");

        piece.transform.localScale = sourceFloor.transform.localScale;
        piece.name = sourceFloor.name + nameSuffix;

        var pieceAuthoring = piece.GetComponent<FloorSegmentAuthoring>();
        if (pieceAuthoring == null)
        {
            Debug.LogWarning("[BoatBuilder] Split floor clone is missing FloorSegmentAuthoring. Destroying failed clone.", piece);
            Undo.DestroyObjectImmediate(piece);
            return null;
        }

        RecordFloorSegmentForUndo(pieceAuthoring, "Resize split floor clone");

        pieceAuthoring.ApplyWidth(newWidth);

        // Critical: move AFTER resizing, because the collider center/bounds may change.
        pieceAuthoring.SetWorldCenterXPreservingColliderOffset(worldCenterX);

        Debug.Log(
            $"[BoatBuilder:Hatch] Created split clone '{piece.name}' " +
            $"requestedCenterX={worldCenterX:F3}, actualCenterX={pieceAuthoring.WorldCenterX:F3}, " +
            $"width={newWidth:F3}, actualBounds=[{pieceAuthoring.WorldLeftX:F3}, {pieceAuthoring.WorldRightX:F3}]",
            piece);

        EditorUtility.SetDirty(piece);
        EditorUtility.SetDirty(pieceAuthoring);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        return piece;
    }

    private static void InitializePlacedHatch(GameObject placed, Transform boatRoot)
    {
        if (placed == null)
            return;

        var authoring = placed.GetComponent<HatchAuthoring>();
        if (authoring == null)
            return;

        if (!string.IsNullOrWhiteSpace(authoring.HatchId))
            return;

        Undo.RecordObject(authoring, "Configure Hatch");

        string id = GenerateNextHatchId(boatRoot, "hatch");
        authoring.HatchId = id;

        Undo.RecordObject(placed, "Rename Hatch");
        placed.name = id;

        EditorUtility.SetDirty(authoring);
        EditorUtility.SetDirty(placed);
    }

    private static string GenerateNextHatchId(Transform boatRoot, string prefix)
    {
        int maxFound = 0;

        if (boatRoot != null)
        {
            var existing = boatRoot.GetComponentsInChildren<HatchAuthoring>(true);
            foreach (var h in existing)
            {
                if (h == null)
                    continue;

                string id = h.HatchId;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!id.StartsWith(prefix + "_", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = id.Substring(prefix.Length + 1);
                if (int.TryParse(suffix, out int n))
                    maxFound = Mathf.Max(maxFound, n);
            }
        }

        return $"{prefix}_{(maxFound + 1):00}";
    }

    private static void RecordFloorSegmentForUndo(FloorSegmentAuthoring floor, string label)
    {
        if (floor == null)
            return;

        Undo.RecordObject(floor, label);
        Undo.RecordObject(floor.transform, label);

        if (floor.FloorCollider != null)
            Undo.RecordObject(floor.FloorCollider, label);

        if (floor.SpriteRenderer != null)
        {
            Undo.RecordObject(floor.SpriteRenderer, label);
            Undo.RecordObject(floor.SpriteRenderer.transform, label);
        }

        if (floor.ResizableSegment != null)
        {
            Undo.RecordObject(floor.ResizableSegment, label);

            if (floor.ResizableSegment.BoxCollider != null)
                Undo.RecordObject(floor.ResizableSegment.BoxCollider, label);

            if (floor.ResizableSegment.SpriteRenderer != null)
            {
                Undo.RecordObject(floor.ResizableSegment.SpriteRenderer, label);
                Undo.RecordObject(floor.ResizableSegment.SpriteRenderer.transform, label);
            }
        }
    }

    private static Transform ResolvePlacementParentForTool(
    BoatBuilderWindow.Tool tool,
    Transform boatRoot)
    {
        if (boatRoot == null)
            return null;

        return tool switch
        {
            // Exterior shell / visual occluder
            BoatBuilderWindow.Tool.ExteriorShell =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorShell),

            // Hull/body structural visuals
            BoatBuilderWindow.Tool.HullSegment =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Hull),

            BoatBuilderWindow.Tool.Wall =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Hull),

            // Interior simulation/visuals
            BoatBuilderWindow.Tool.CompartmentRect =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

            BoatBuilderWindow.Tool.Ladder =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Interior),

            // Exterior deck objects
            BoatBuilderWindow.Tool.Deck =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

            BoatBuilderWindow.Tool.Hatch =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

            BoatBuilderWindow.Tool.PilotChair =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.ExteriorDeck),

            // Volumes/triggers
            BoatBuilderWindow.Tool.BoardedVolume =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Volume),

            // Gameplay helpers/interactables
            BoatBuilderWindow.Tool.PlayerSpawnPoint =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

            BoatBuilderWindow.Tool.BoatBoardObject =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

            BoatBuilderWindow.Tool.MapTable =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

            BoatBuilderWindow.Tool.Hardpoint =>
                GetOrCreateBoatCategoryRoot(boatRoot, BoatVisualCategory.Gameplay),

            _ => boatRoot
        };
    }

    private static Transform GetOrCreateBoatCategoryRoot(Transform boatRoot, BoatVisualCategory category)
    {
        if (boatRoot == null)
            return null;

        string childName = category switch
        {
            BoatVisualCategory.ExteriorShell => "_Exterior",
            BoatVisualCategory.Interior => "_Interior",
            BoatVisualCategory.ExteriorDeck => "_Deck",
            BoatVisualCategory.Gameplay => "_Gameplay",
            BoatVisualCategory.Volume => "_Volumes",
            BoatVisualCategory.AlwaysVisible => "_AlwaysVisible",
            BoatVisualCategory.Hull => "_Hull",
            _ => "_Misc"
        };

        Transform existing = boatRoot.Find(childName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, $"Create boat category root {childName}");

        Transform t = go.transform;
        Undo.SetTransformParent(t, boatRoot, $"Parent {childName} to BoatRoot");

        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        EditorUtility.SetDirty(go);
        EditorSceneManager.MarkSceneDirty(boatRoot.gameObject.scene);

        return t;
    }

    private static void InitializePlacedVisualMarker(GameObject placed, BoatVisualCategory category)
    {
        if (placed == null)
            return;

        BoatVisualMarker marker = placed.GetComponent<BoatVisualMarker>();
        if (marker == null)
        {
            marker = placed.AddComponent<BoatVisualMarker>();
            Undo.RegisterCreatedObjectUndo(marker, "Add Boat Visual Marker");
        }

        Undo.RecordObject(marker, "Configure Boat Visual Marker");
        marker.EditorSetCategory(category);

        EditorUtility.SetDirty(marker);
    }
}
#endif