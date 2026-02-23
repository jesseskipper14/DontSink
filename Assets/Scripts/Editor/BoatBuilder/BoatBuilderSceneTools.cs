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

        // NEW: BoardedVolume sizing
        public float BoardedVolumePadding;
        public float BoardedVolumeExtraUp;
        public float BoardedVolumeExtraDown;
    }

    private static Context _ctx;
    private static bool _attached;

    public static bool IsPlacementEnabled { get; private set; }

    // Preview ghost
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
        // Default safety if caller didn't set these
        if (ctx.BoardedVolumePadding <= 0f) ctx.BoardedVolumePadding = 0f;
        if (ctx.BoardedVolumeExtraUp <= 0f) ctx.BoardedVolumeExtraUp = 5.0f;   // IMPORTANT: headroom
        if (ctx.BoardedVolumeExtraDown <= 0f) ctx.BoardedVolumeExtraDown = 0f;

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

    [MenuItem("Tools/Boat Builder/Snap Selection %#g")] // Ctrl/Cmd + Shift + G
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

    // ===========================
    // Required pieces utilities
    // ===========================
    public static Transform PeekBestBoatRoot() => FindBestBoatRootParent();

    public static void GetRequiredPiecesStatus(Transform boatRoot,
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

        // By component type (preferred when available)
        hasBoatBoardObject = boatRoot.GetComponentsInChildren<BoatBoardingInteractable>(true).Any();
        hasMapTable = boatRoot.GetComponentsInChildren<MapTableInteractable>(true).Any();
        hasBoardedVolume = boatRoot.GetComponentsInChildren<BoatBoardedVolume>(true).Any();

        // Spawn points: use name match
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

        // Order: volume first (safety), spawns, board, map
        if (!hasVol) { tool = BoatBuilderWindow.Tool.BoardedVolume; return true; }
        if (spawnCount < Mathf.Max(1, _ctx.RequiredPlayerSpawnPoints)) { tool = BoatBuilderWindow.Tool.PlayerSpawnPoint; return true; }
        if (!hasBoard) { tool = BoatBuilderWindow.Tool.BoatBoardObject; return true; }
        if (!hasMap) { tool = BoatBuilderWindow.Tool.MapTable; return true; }

        return false;
    }

    // ===========================
    // Scene GUI + Placement
    // ===========================
    private static void OnSceneGUI(SceneView view)
    {
        // This is the big one for preview responsiveness.
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

        var prefab = GetPrefabForTool(_ctx.Kit, _ctx.ActiveTool);
        if (prefab == null)
        {
            DrawStatus(view, $"Missing prefab reference for {_ctx.ActiveTool} in BoatKit.");
            DestroyPreview();
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var e = Event.current;

        DrawStatus(view, $"PLACING: {_ctx.ActiveTool} | Left-click to place | Right-click/Esc to cancel");

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

        // Preview update: update on mouse move OR drag OR repaint/layout.
        if (_ctx.ShowSnapPreview && (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint || e.type == EventType.Layout))
        {
            var world = MouseToWorldOnZPlane(e.mousePosition, _ctx.ZPlane);
            if (_ctx.SnapOnPlace) world = Snap(world, _ctx.GridSize);
            UpdatePreview(prefab, world);

            // Force frequent repaint while placing so preview doesn't feel delayed.
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

            // Enforce required pieces (avoid duplicates for unique pieces)
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

            // NEW: if this is BoardedVolume, auto-fit it to boat bounds + headroom
            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.BoardedVolume && parent != null)
            {
                AutoFitBoardedVolume(parent, placed, _ctx.BoardedVolumePadding, _ctx.BoardedVolumeExtraUp, _ctx.BoardedVolumeExtraDown);
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
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) instance = UnityEngine.Object.Instantiate(prefab);

        Undo.RegisterCreatedObjectUndo(instance, "Place Boat Prefab");

        instance.transform.position = worldPos;
        if (parent != null) instance.transform.SetParent(parent, true);

        Selection.activeGameObject = instance;
        EditorUtility.SetDirty(instance);
        EditorSceneManager.MarkSceneDirty(instance.scene);

        return instance;
    }

    // ===========================
    // BoardedVolume auto fit
    // ===========================
    [MenuItem("Tools/Boat Builder/Fit BoardedVolume to Selected Boat")]
    public static void FitBoardedVolumeToSelectedBoat()
    {
        var root = FindBestBoatRootParent();
        if (root == null)
        {
            Debug.LogWarning("[BoatBuilder] Select a boat root or child first.");
            return;
        }

        var bv = root.GetComponentInChildren<BoatBoardedVolume>(true);
        if (bv == null)
        {
            Debug.LogWarning("[BoatBuilder] No BoatBoardedVolume found under selected boat.");
            return;
        }

        AutoFitBoardedVolume(root, bv.gameObject, _ctx.BoardedVolumePadding, _ctx.BoardedVolumeExtraUp, _ctx.BoardedVolumeExtraDown);
        Selection.activeObject = bv.gameObject;
    }

    private static void AutoFitBoardedVolume(Transform boatRoot, GameObject boardedVolumeGO, float pad, float extraUp, float extraDown)
    {
        if (boatRoot == null || boardedVolumeGO == null) return;

        // We assume BoardedVolume uses BoxCollider2D as a trigger.
        var box = boardedVolumeGO.GetComponentInChildren<BoxCollider2D>(true);
        if (box == null)
        {
            Debug.LogWarning("[BoatBuilder] BoardedVolume prefab needs a BoxCollider2D to auto-fit.");
            return;
        }

        // Compute bounds from colliders and renderers under boat root (excluding the boarded volume itself).
        bool hasAny = false;
        Bounds b = default;

        void Encapsulate(Bounds bb)
        {
            if (!hasAny) { b = bb; hasAny = true; }
            else b.Encapsulate(bb);
        }

        var colliders = boatRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            var c = colliders[i];
            if (c == null) continue;
            if (c.transform.IsChildOf(boardedVolumeGO.transform)) continue;
            Encapsulate(c.bounds);
        }

        var renderers = boatRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (r.transform.IsChildOf(boardedVolumeGO.transform)) continue;
            Encapsulate(r.bounds);
        }

        if (!hasAny)
        {
            Debug.LogWarning("[BoatBuilder] Could not compute boat bounds (no colliders/renderers?).");
            return;
        }

        // Expand with padding + extra vertical headroom.
        b.Expand(new Vector3(pad * 2f, pad * 2f, 0f));

        // Apply asymmetric vertical expansion.
        // Bounds are symmetric by default; we emulate asymmetry by shifting center upward.
        float addUp = Mathf.Max(0f, extraUp);
        float addDown = Mathf.Max(0f, extraDown);

        var size = b.size;
        size.y += (addUp + addDown);

        var center = b.center;
        center.y += (addUp - addDown) * 0.5f;

        // Place BoardedVolume object at boat root (so it's stable), and set collider in local space.
        Undo.RecordObject(boardedVolumeGO.transform, "Fit BoardedVolume");
        boardedVolumeGO.transform.SetParent(boatRoot, true);
        boardedVolumeGO.transform.position = boatRoot.position;
        boardedVolumeGO.transform.rotation = Quaternion.identity;

        // Convert world-space bounds center to local space of the collider's transform.
        var colT = box.transform;

        Undo.RecordObject(box, "Fit BoardedVolume Collider");
        Vector3 localCenter = colT.InverseTransformPoint(center);

        box.offset = (Vector2)localCenter;
        box.size = new Vector2(size.x, size.y);
        box.isTrigger = true;

        EditorUtility.SetDirty(boardedVolumeGO);
        EditorUtility.SetDirty(box);
        EditorSceneManager.MarkSceneDirty(boardedVolumeGO.scene);
    }

    // ===========================
    // Preview Ghost
    // ===========================
    private static void UpdatePreview(GameObject prefabAsset, Vector3 worldPos)
    {
        if (prefabAsset == null) { DestroyPreview(); return; }

        if (_previewInstance == null || _previewPrefabAsset != prefabAsset)
        {
            DestroyPreview();

            _previewInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (_previewInstance == null) _previewInstance = UnityEngine.Object.Instantiate(prefabAsset);

            _previewPrefabAsset = prefabAsset;
            _previewInstance.name = $"__BoatBuilderPreview__{prefabAsset.name}";
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;

            // Disable colliders and scripts to avoid side effects.
            foreach (var c in _previewInstance.GetComponentsInChildren<Collider2D>(true))
                c.enabled = false;

            foreach (var mb in _previewInstance.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;

            // Tint sprites green.
            foreach (var sr in _previewInstance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.color = new Color(0f, 1f, 0f, 0.35f);
                sr.sortingOrder += 5000;
            }

            // Hide mesh renderers if any.
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

    // ===========================
    // Helpers
    // ===========================
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

    private static GameObject GetPrefabForTool(BoatKit kit, BoatBuilderWindow.Tool tool)
    {
        return tool switch
        {
            BoatBuilderWindow.Tool.HullSegment => kit.HullSegment,
            BoatBuilderWindow.Tool.Wall => kit.Wall,
            BoatBuilderWindow.Tool.Hatch => kit.Hatch,
            BoatBuilderWindow.Tool.PilotChair => kit.PilotChair,
            BoatBuilderWindow.Tool.CompartmentRect => kit.CompartmentRect,
            BoatBuilderWindow.Tool.Deck => kit.Deck,

            BoatBuilderWindow.Tool.BoatBoardObject => kit.BoatBoardObject,
            BoatBuilderWindow.Tool.MapTable => kit.MapTable,
            BoatBuilderWindow.Tool.PlayerSpawnPoint => kit.PlayerSpawnPoint,
            BoatBuilderWindow.Tool.BoardedVolume => kit.BoardedVolume,
            _ => null
        };
    }

    private static void DrawStatus(SceneView view, string text)
    {
        Handles.BeginGUI();
        var r = new Rect(10, 10, view.position.width - 20, 40);
        GUI.Label(r, text, EditorStyles.boldLabel);
        Handles.EndGUI();
    }
}
#endif