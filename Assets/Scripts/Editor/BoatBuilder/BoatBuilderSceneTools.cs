#if UNITY_EDITOR
using System;
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
    }

    private static Context _ctx;
    private static bool _attached;
    public static bool IsPlacementEnabled { get; private set; }

    static BoatBuilderSceneTools()
    {
        // Auto attach on domain reload.
        Attach();
    }

    public static void Attach()
    {
        if (_attached) return;
        SceneView.duringSceneGui += OnSceneGUI;
        _attached = true;
    }

    public static void SetContext(Context ctx) => _ctx = ctx;

    public static void TogglePlacement()
    {
        IsPlacementEnabled = !IsPlacementEnabled;
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

    private static void OnSceneGUI(SceneView view)
    {
        if (!IsPlacementEnabled) return;

        // Avoid doing anything if we’re missing a kit.
        if (_ctx.Kit == null)
        {
            DrawStatus(view, "BoatKit missing. Assign one in Tools > Boat Builder > Window.");
            return;
        }

        var prefab = GetPrefabForTool(_ctx.Kit, _ctx.ActiveTool);
        if (prefab == null)
        {
            DrawStatus(view, $"Missing prefab reference for {_ctx.ActiveTool} in BoatKit.");
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var e = Event.current;

        // Draw status text
        DrawStatus(view, $"PLACING: {_ctx.ActiveTool} | Left-click to place | Right-click/Esc to cancel");

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            IsPlacementEnabled = false;
            e.Use();
            SceneView.RepaintAll();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            IsPlacementEnabled = false;
            e.Use();
            SceneView.RepaintAll();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            var world = MouseToWorldOnZPlane(e.mousePosition, _ctx.ZPlane);

            if (_ctx.SnapOnPlace)
                world = Snap(world, _ctx.GridSize);

            var parent = _ctx.AutoParentToBoatRoot ? FindBestBoatRootParent() : null;
            PlacePrefab(prefab, world, parent);

            e.Use();
            SceneView.RepaintAll();
        }
    }

    private static void PlacePrefab(GameObject prefab, Vector3 worldPos, Transform parent)
    {
        GameObject instance = null;

        // This normally instantiates into the correct current stage automatically.
        instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (instance == null)
            instance = UnityEngine.Object.Instantiate(prefab);

        Undo.RegisterCreatedObjectUndo(instance, "Place Boat Prefab");

        instance.transform.position = worldPos;

        if (parent != null)
            instance.transform.SetParent(parent, true);

        Selection.activeGameObject = instance;
        EditorUtility.SetDirty(instance);

        // Mark scene dirty so changes save correctly.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(instance.scene);
    }

    private static Vector3 MouseToWorldOnZPlane(Vector2 mousePos, float zPlane)
    {
        // mousePos is GUI space. Convert to world ray, intersect with Z plane.
        var ray = HandleUtility.GUIPointToWorldRay(mousePos);

        // Solve ray-plane intersection for plane z = zPlane.
        // ray origin: O, direction: D. We want O.z + t*D.z = zPlane => t = (zPlane - O.z)/D.z
        if (Mathf.Abs(ray.direction.z) < 0.0001f)
        {
            // Direction is parallel to plane. Fallback: just use origin.
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
        // Best effort:
        // 1) If selection has something that looks like a boat root, use it.
        // 2) Else, use selected object's top-most parent (common case).
        // 3) Else, look for a root object named like "Boat", "TestBoat", etc.
        var go = Selection.activeGameObject;

        if (go != null)
        {
            // Walk up hierarchy, look for name that suggests root.
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
            // If nothing matched, still parent to top-level selection root (keeps stuff grouped)
            return top;
        }

        // Fallback: find a root object with a boat-y name in the scene
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
            BoatBuilderWindow.Tool.Deck => kit.Deck, // NEW
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
