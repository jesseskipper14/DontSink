#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static partial class BoatBuilderSceneTools
{

    public struct Context
    {
        public BoatKit Kit;
        public Transform BoatRootOverride;
        public ModuleDefinition HardpointStartingModuleDefinition;

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
        public bool StairAscendRight;

        public BoatVisibilityMode SelectedVisibilityZoneMode;
        public int VisibilityZonePriority;
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

    private static void OnSceneGUI(SceneView view)
    {
        view.wantsMouseMove = true;

        DrawSelectedHardpointControllerLinks();

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

        string placingLabel =
            _ctx.ActiveTool == BoatBuilderWindow.Tool.Hardpoint
                ? $"PLACING: {_ctx.ActiveTool} ({_ctx.SelectedHardpointType})"
                : _ctx.ActiveTool == BoatBuilderWindow.Tool.BoatVisibilityZone
                    ? $"PLACING: {_ctx.ActiveTool} ({_ctx.SelectedVisibilityZoneMode})"
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
            else if (_ctx.ActiveTool == BoatBuilderWindow.Tool.Ledge)
            {
                placed = TryPlaceLedgeWithOptionalFloorSplit(prefab, world, boatRoot, parent);
            }
            else if (_ctx.ActiveTool == BoatBuilderWindow.Tool.CompartmentRect)
            {
                placed = TryPlaceDetectedCompartment(prefab, world, boatRoot, parent);
            }
            else if (_ctx.ActiveTool == BoatBuilderWindow.Tool.Door)
            {
                placed = TryPlaceDoorWithWallSplit(prefab, world, boatRoot, parent);
            }
            else
            {
                placed = PlacePrefab(prefab, world, parent);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.Wall)
            {
                InitializePlacedWall(placed);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.Stairs)
            {
                InitializePlacedStair(placed, _ctx.StairAscendRight);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.BoardedVolume && boatRoot != null)
            {
                AutoFitBoardedVolume(
                    boatRoot,
                    placed,
                    _ctx.BoardedVolumePadding,
                    _ctx.BoardedVolumeExtraUp,
                    _ctx.BoardedVolumeExtraDown);

                AutoFitBoatGeometryFromVisualRenderers(boatRoot);
            }

            if (placed != null && _ctx.ActiveTool == BoatBuilderWindow.Tool.BoatVisibilityZone)
            {
                InitializePlacedVisibilityZone(
                    placed,
                    boatRoot != null ? boatRoot : placed.transform.parent,
                    _ctx.SelectedVisibilityZoneMode,
                    _ctx.VisibilityZonePriority);
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
                    _ctx.HardpointRenameObjectToId,
                    _ctx.HardpointStartingModuleDefinition);
            }

            e.Use();
            SceneView.RepaintAll();
        }
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
        grid = Mathf.Max(0.01f, grid);

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
            BoatBuilderWindow.Tool.Door => kit.Door,
            BoatBuilderWindow.Tool.PilotChair => kit.PilotChair,
            BoatBuilderWindow.Tool.CompartmentRect => kit.CompartmentRect,
            BoatBuilderWindow.Tool.Deck => kit.Deck,
            BoatBuilderWindow.Tool.Ladder => kit.Ladder,
            BoatBuilderWindow.Tool.Stairs => kit.Stairs,
            BoatBuilderWindow.Tool.Ledge => kit.Ledge,
            BoatBuilderWindow.Tool.BoatBoardObject => kit.BoatBoardObject,
            BoatBuilderWindow.Tool.MapTable => kit.MapTable,
            BoatBuilderWindow.Tool.PlayerSpawnPoint => kit.PlayerSpawnPoint,
            BoatBuilderWindow.Tool.BoardedVolume => kit.BoardedVolume,
            BoatBuilderWindow.Tool.BoatVisibilityZone => kit.BoatVisibilityZone,
            BoatBuilderWindow.Tool.Hardpoint => GetHardpointPrefab(kit, selectedHardpointType),
            BoatBuilderWindow.Tool.ExteriorShell => kit.ExteriorShell,
            BoatBuilderWindow.Tool.TurretControllerChair => kit.TurretControllerChair,
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
