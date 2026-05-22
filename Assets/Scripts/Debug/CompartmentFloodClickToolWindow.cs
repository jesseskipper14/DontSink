#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class CompartmentFloodClickToolWindow : EditorWindow
{
    private enum FloodMode
    {
        Add,
        Remove,
        SetFraction,
        Empty,
        Fill
    }

    private FloodMode _mode = FloodMode.Add;

    private Transform _boatRoot;
    private float _amount = 0.25f;
    private float _fraction01 = 0.5f;

    private bool _active = true;
    private bool _drawLabels = true;
    private bool _drawPolygons = true;
    private bool _drawClickTargets = true;
    private bool _useSelectedBoatRoot = true;

    private float _clickTargetHandleScale = 0.18f;

    [Header("Debug")]
    private bool _logMouseOnMove = false;
    private double _nextMouseMoveLogTime;
    private float _mouseMoveLogInterval = 0.35f;

    private Compartment _hovered;
    private Vector2 _lastGuiMouse;
    private Vector2 _lastMouseWorldApprox;
    private string _lastMouseDebug = "Move mouse over Scene View.";

    [MenuItem("Tools/Boat Builder/Flood Click Tool")]
    public static void Open()
    {
        GetWindow<CompartmentFloodClickToolWindow>("Flood Click Tool").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);

        _active = EditorGUILayout.ToggleLeft("Active in Scene View", _active);
        _useSelectedBoatRoot = EditorGUILayout.ToggleLeft("Use selected boat root when possible", _useSelectedBoatRoot);

        using (new EditorGUILayout.HorizontalScope())
        {
            _boatRoot = (Transform)EditorGUILayout.ObjectField(
                "Boat Root",
                _boatRoot,
                typeof(Transform),
                true);

            if (GUILayout.Button("From Selection", GUILayout.Width(110)))
                _boatRoot = ResolveBoatRootFromSelection();
        }

        Transform activeRoot = ResolveActiveRoot();
        Compartment[] previewCompartments = ResolveCompartments(activeRoot);

        EditorGUILayout.HelpBox(
            $"Active Root: {(activeRoot != null ? activeRoot.name : "ALL SCENE")}\n" +
            $"Compartments Found: {(previewCompartments != null ? previewCompartments.Length : 0)}\n" +
            $"Hovered: {(_hovered != null ? _hovered.name : "none")}",
            MessageType.Info);

        EditorGUILayout.Space(6);

        _mode = (FloodMode)EditorGUILayout.EnumPopup("Click Mode", _mode);
        _amount = Mathf.Max(0f, EditorGUILayout.FloatField("Add/Remove Amount", _amount));
        _fraction01 = EditorGUILayout.Slider("Set Fraction", _fraction01, 0f, 1f);

        EditorGUILayout.Space(6);

        _drawPolygons = EditorGUILayout.ToggleLeft("Draw compartment outlines", _drawPolygons);
        _drawLabels = EditorGUILayout.ToggleLeft("Draw water % labels", _drawLabels);
        _drawClickTargets = EditorGUILayout.ToggleLeft("Draw clickable compartment targets", _drawClickTargets);
        _clickTargetHandleScale = EditorGUILayout.Slider("Click Target Size", _clickTargetHandleScale, 0.05f, 0.45f);

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mouse Debug", EditorStyles.boldLabel);

            _logMouseOnMove = EditorGUILayout.ToggleLeft("Log mouse while moving", _logMouseOnMove);
            _mouseMoveLogInterval = Mathf.Max(0.05f, EditorGUILayout.FloatField("Move Log Interval", _mouseMoveLogInterval));

            if (GUILayout.Button("Log Mouse Now", GUILayout.Height(24)))
            {
                Debug.Log(_lastMouseDebug, this);
            }

            EditorGUILayout.TextArea(_lastMouseDebug, GUILayout.MinHeight(90));
        }

        EditorGUILayout.Space(8);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Scene View Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Click compartment target: apply selected mode");
            EditorGUILayout.LabelField("Shift + click target: remove water");
            EditorGUILayout.LabelField("Polygon click also works if hover detection succeeds");
            EditorGUILayout.LabelField("Alt/Cmd/Ctrl: ignored, so normal Scene View navigation still works");
        }

        if (_hovered != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Hovered", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_hovered.name);
            EditorGUILayout.LabelField($"Water: {GetFraction(_hovered):P0}");
        }
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (!_active)
            return;

        Event e = Event.current;
        if (e == null)
            return;

        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);

        if (e.alt || e.command || e.control)
            return;

        Transform root = ResolveActiveRoot();
        Compartment[] compartments = ResolveCompartments(root);

        _lastGuiMouse = e.mousePosition;
        _lastMouseWorldApprox = GetMouseWorldOnZPlane(e.mousePosition, GetRootZ(root));

        _hovered = FindCompartmentUnderMouse(
            compartments,
            e.mousePosition,
            out string pickSummary);

        _lastMouseDebug = BuildMouseDebug(
            root,
            compartments,
            e.mousePosition,
            _lastMouseWorldApprox,
            _hovered,
            pickSummary);

        DrawSceneOverlay(compartments, _hovered);

        if (_drawClickTargets)
            DrawCompartmentClickTargets(compartments);

        if (e.type == EventType.MouseMove)
        {
            Repaint();
            sceneView.Repaint();

            if (_logMouseOnMove && EditorApplication.timeSinceStartup >= _nextMouseMoveLogTime)
            {
                _nextMouseMoveLogTime = EditorApplication.timeSinceStartup + _mouseMoveLogInterval;
                Debug.Log(_lastMouseDebug, this);
            }
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (_hovered != null)
            {
                FloodMode mode = e.shift ? FloodMode.Remove : _mode;
                ApplyMode(_hovered, mode);

                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
            else
            {
                Debug.Log(_lastMouseDebug, this);
            }
        }
    }

    private Transform ResolveActiveRoot()
    {
        if (_useSelectedBoatRoot)
        {
            Transform selectedRoot = ResolveBoatRootFromSelection();
            if (selectedRoot != null)
                return selectedRoot;
        }

        return _boatRoot;
    }

    private static Transform ResolveBoatRootFromSelection()
    {
        if (Selection.activeTransform == null)
            return null;

        Boat boat =
            Selection.activeTransform.GetComponentInParent<Boat>() ??
            Selection.activeTransform.GetComponentInChildren<Boat>(true);

        return boat != null ? boat.transform : null;
    }

    private static Compartment[] ResolveCompartments(Transform root)
    {
        if (root != null)
            return root.GetComponentsInChildren<Compartment>(true);

        return FindObjectsByType<Compartment>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    private static float GetRootZ(Transform root)
    {
        return root != null ? root.position.z : 0f;
    }

    private static Vector2 GetMouseWorldOnZPlane(Vector2 guiMousePosition, float zPlane)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(guiMousePosition);

        if (Mathf.Abs(ray.direction.z) < 0.00001f)
            return ray.origin;

        float t = (zPlane - ray.origin.z) / ray.direction.z;
        return ray.origin + ray.direction * t;
    }

    private static Compartment FindCompartmentUnderMouse(
        Compartment[] compartments,
        Vector2 guiMousePosition,
        out string summary)
    {
        summary = "No compartments.";

        if (compartments == null || compartments.Length == 0)
            return null;

        Compartment best = null;
        float bestArea = float.PositiveInfinity;

        StringBuilder sb = new();
        sb.AppendLine("Pick scan:");

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];

            if (c == null || !c.isActiveAndEnabled)
            {
                sb.AppendLine($"  [{i}] null/inactive");
                continue;
            }

            Vector2[] worldPoly = c.GetWorldCorners();
            if (worldPoly == null || worldPoly.Length < 3)
            {
                sb.AppendLine($"  [{i}] {c.name}: no valid polygon");
                continue;
            }

            float z = c.transform.position.z;
            Vector2 worldAtCompZ = GetMouseWorldOnZPlane(guiMousePosition, z);

            bool worldInside =
                ConvexPolygonUtil.PointInsideConvex(worldAtCompZ, worldPoly);

            bool colliderInside = false;
            BoxCollider2D box = c.GetComponent<BoxCollider2D>();
            if (box != null)
                colliderInside = box.bounds.Contains(new Vector3(worldAtCompZ.x, worldAtCompZ.y, z));

            bool guiInside = PointInGuiPolygon(guiMousePosition, worldPoly, z, out float guiArea);

            bool hit = guiInside || worldInside || colliderInside;

            sb.AppendLine(
                $"  [{i}] {c.name}: hit={hit} gui={guiInside} world={worldInside} box={colliderInside} " +
                $"z={z:0.###} worldAtZ={worldAtCompZ} guiArea={guiArea:0.##}");

            if (!hit)
                continue;

            if (guiArea < bestArea)
            {
                bestArea = guiArea;
                best = c;
            }
        }

        summary = sb.ToString();
        return best;
    }

    private static bool PointInGuiPolygon(
        Vector2 guiPoint,
        Vector2[] worldPolygon,
        float worldZ,
        out float absArea)
    {
        absArea = 0f;

        if (worldPolygon == null || worldPolygon.Length < 3)
            return false;

        Vector2[] guiPoly = new Vector2[worldPolygon.Length];

        for (int i = 0; i < worldPolygon.Length; i++)
        {
            guiPoly[i] = HandleUtility.WorldToGUIPoint(
                new Vector3(worldPolygon[i].x, worldPolygon[i].y, worldZ));
        }

        absArea = Mathf.Abs(SignedArea(guiPoly));

        bool inside = false;

        for (int i = 0, j = guiPoly.Length - 1; i < guiPoly.Length; j = i++)
        {
            Vector2 a = guiPoly[i];
            Vector2 b = guiPoly[j];

            bool crosses =
                (a.y > guiPoint.y) != (b.y > guiPoint.y) &&
                guiPoint.x < (b.x - a.x) * (guiPoint.y - a.y) / ((b.y - a.y) + 0.000001f) + a.x;

            if (crosses)
                inside = !inside;
        }

        return inside;
    }

    private static float SignedArea(Vector2[] poly)
    {
        if (poly == null || poly.Length < 3)
            return 0f;

        float area = 0f;

        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];

            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    private void DrawCompartmentClickTargets(Compartment[] compartments)
    {
        if (compartments == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];

            if (c == null || !c.isActiveAndEnabled)
                continue;

            Vector2[] poly = c.GetWorldCorners();
            if (poly == null || poly.Length < 3)
                continue;

            Bounds b = BoundsFromPolygon(poly, c.transform.position.z);
            Vector3 pos = b.center;

            float handleSize = HandleUtility.GetHandleSize(pos) * _clickTargetHandleScale;
            float fraction = GetFraction(c);

            Color oldColor = Handles.color;

            Handles.color = c == _hovered
                ? new Color(0.2f, 1f, 1f, 0.95f)
                : new Color(0.1f, 0.65f, 1f, 0.85f);

            if (Handles.Button(
                    pos,
                    Quaternion.identity,
                    handleSize,
                    handleSize * 1.8f,
                    Handles.CircleHandleCap))
            {
                FloodMode mode = Event.current != null && Event.current.shift
                    ? FloodMode.Remove
                    : _mode;

                ApplyMode(c, mode);

                GUI.changed = true;
                Repaint();
                SceneView.RepaintAll();
            }

            Handles.color = oldColor;

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            style.normal.textColor = Color.cyan;

            Handles.Label(
                pos + Vector3.up * handleSize * 1.8f,
                $"{Mathf.RoundToInt(fraction * 100f)}%",
                style);
        }
    }

    private void ApplyMode(Compartment compartment, FloodMode mode)
    {
        if (compartment == null)
            return;

        Undo.RecordObject(compartment, $"Flood {mode} Compartment");

        switch (mode)
        {
            case FloodMode.Add:
                compartment.AcceptWater(_amount);
                break;

            case FloodMode.Remove:
                compartment.RemoveWater(_amount);
                break;

            case FloodMode.SetFraction:
                SetFraction(compartment, _fraction01);
                break;

            case FloodMode.Empty:
                compartment.RemoveWater(float.MaxValue);
                break;

            case FloodMode.Fill:
                SetFraction(compartment, 1f);
                break;
        }

        compartment.RecomputeWaterSurface();

        EditorUtility.SetDirty(compartment);
        EditorSceneManager.MarkSceneDirty(compartment.gameObject.scene);

        Debug.Log(
            $"[FloodClickTool] {mode} '{compartment.name}' water={GetFraction(compartment):P0}",
            compartment);
    }

    private static void SetFraction(Compartment compartment, float fraction01)
    {
        if (compartment == null)
            return;

        fraction01 = Mathf.Clamp01(fraction01);

        float target = compartment.MaxWaterArea * fraction01;
        float current = compartment.WaterArea;
        float delta = target - current;

        if (delta > 0f)
            compartment.AcceptWater(delta);
        else if (delta < 0f)
            compartment.RemoveWater(-delta);
    }

    private void DrawSceneOverlay(Compartment[] compartments, Compartment hovered)
    {
        if (compartments == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        for (int i = 0; i < compartments.Length; i++)
        {
            Compartment c = compartments[i];

            if (c == null)
                continue;

            Vector2[] poly = c.GetWorldCorners();
            if (poly == null || poly.Length < 3)
                continue;

            bool isHovered = c == hovered;
            float fraction = GetFraction(c);

            if (_drawPolygons)
                DrawCompartmentPolygon(poly, c.transform.position.z, isHovered, fraction);

            if (_drawLabels)
                DrawCompartmentLabel(c, poly, c.transform.position.z, fraction, isHovered);
        }
    }

    private static void DrawCompartmentPolygon(Vector2[] poly, float z, bool hovered, float fraction)
    {
        Color fill = hovered
            ? new Color(0.2f, 0.85f, 1f, 0.22f)
            : new Color(0.2f, 0.55f, 1f, 0.07f);

        Color wire = hovered
            ? new Color(0.1f, 0.95f, 1f, 0.95f)
            : new Color(0.2f, 0.55f, 1f, 0.45f);

        Vector3[] verts = new Vector3[poly.Length];
        for (int i = 0; i < poly.Length; i++)
            verts[i] = new Vector3(poly[i].x, poly[i].y, z);

        Handles.DrawSolidRectangleWithOutline(verts, fill, wire);

        if (fraction > 0f)
        {
            Bounds b = BoundsFromPolygon(poly, z);
            float y = Mathf.Lerp(b.min.y, b.max.y, fraction);

            Handles.color = new Color(0.1f, 0.7f, 1f, 0.9f);
            Handles.DrawLine(
                new Vector3(b.min.x, y, z),
                new Vector3(b.max.x, y, z),
                2f);
        }
    }

    private static void DrawCompartmentLabel(
        Compartment compartment,
        Vector2[] poly,
        float z,
        float fraction,
        bool hovered)
    {
        Bounds b = BoundsFromPolygon(poly, z);
        Vector3 pos = b.center;

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        style.normal.textColor = hovered
            ? Color.cyan
            : new Color(0.75f, 0.95f, 1f, 0.9f);

        Handles.Label(
            pos,
            $"{compartment.name}\n{Mathf.RoundToInt(fraction * 100f)}%",
            style);
    }

    private static Bounds BoundsFromPolygon(Vector2[] poly, float z = 0f)
    {
        Bounds b = new Bounds(new Vector3(poly[0].x, poly[0].y, z), Vector3.zero);

        for (int i = 1; i < poly.Length; i++)
            b.Encapsulate(new Vector3(poly[i].x, poly[i].y, z));

        return b;
    }

    private static float GetFraction(Compartment c)
    {
        if (c == null || c.MaxWaterArea <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(c.WaterArea / c.MaxWaterArea);
    }

    private static string BuildMouseDebug(
        Transform root,
        Compartment[] compartments,
        Vector2 guiMouse,
        Vector2 mouseWorldApprox,
        Compartment hovered,
        string pickSummary)
    {
        StringBuilder sb = new();

        sb.AppendLine("[FloodClickTool] Mouse Debug");
        sb.AppendLine($"Root: {(root != null ? root.name : "ALL SCENE")}");
        sb.AppendLine($"Compartments: {(compartments != null ? compartments.Length : 0)}");
        sb.AppendLine($"Hovered: {(hovered != null ? hovered.name : "none")}");
        sb.AppendLine($"GUI Mouse: {guiMouse}");
        sb.AppendLine($"World Approx: {mouseWorldApprox}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(pickSummary))
            sb.AppendLine(pickSummary);

        return sb.ToString();
    }
}
#endif