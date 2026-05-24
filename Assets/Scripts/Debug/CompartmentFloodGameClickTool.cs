using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CompartmentFloodGameClickTool : MonoBehaviour
{
    public enum FloodMode
    {
        Add,
        Remove,
        SetFraction,
        Empty,
        Fill
    }

    [Header("Refs")]
    [SerializeField] private Boat boat;
    [SerializeField] private Transform boatRoot;
    [SerializeField] private Camera targetCamera;

    [Header("Controls")]
    [SerializeField] private bool active = true;
    [SerializeField] private FloodMode mode = FloodMode.Add;

    [Tooltip("Left mouse button applies the selected mode.")]
    [SerializeField] private int mouseButton = 0;

    [Tooltip("Holding Shift while clicking removes water regardless of selected mode.")]
    [SerializeField] private bool shiftClickRemoves = true;

    [Header("Amounts")]
    [Min(0f)]
    [SerializeField] private float amount = 0.25f;

    [Header("Amount Hotkeys")]
    [SerializeField] private KeyCode decreaseAmountKey = KeyCode.Comma;
    [SerializeField] private KeyCode increaseAmountKey = KeyCode.Period;

    [SerializeField] private float amountSmallStep = 0.25f;
    [SerializeField] private float amountLargeStep = 2.5f;

    [SerializeField] private float[] amountPresets = { 0.25f, 1f, 5f, 10f, 25f };

    [Range(0f, 1f)]
    [SerializeField] private float setFraction01 = 0.5f;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode toggleActiveKey = KeyCode.F11;
    [SerializeField] private KeyCode addModeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode removeModeKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode setModeKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode emptyModeKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode fillModeKey = KeyCode.Alpha5;
    [SerializeField] private KeyCode logMouseKey = KeyCode.F9;

    [Header("Input Blocking")]
    [SerializeField] private bool respectGameplayInputBlocker = true;
    [SerializeField] private bool ignoreClicksWhenPointerOverUI = true;
    [SerializeField] private bool clearHoverWhenInputBlocked = true;

    [Header("Debug Overlay")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private Vector2 overlayPosition = new Vector2(12f, 120f);
    [SerializeField] private Vector2 overlaySize = new Vector2(520f, 170f);

    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F10;
    [SerializeField] private bool overlayDraggable = true;
    [SerializeField] private bool ignoreFloodClicksOverOverlay = true;

    private Rect _overlayRect;
    private const int OverlayWindowId = 782341;

    [Header("Logging")]
    [SerializeField] private bool logClicks = true;
    [SerializeField] private bool logMisses = true;
    [SerializeField] private bool logMouseWhenPressed = true;

    private readonly List<Compartment> _compartments = new();

    private Compartment _hovered;
    private Vector2 _mouseWorld;
    private string _lastMouseDebug = "No mouse sample yet.";

    private void Awake()
    {
        ResolveRefs();
        RefreshCompartmentCache();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshCompartmentCache();
    }

    private void Update()
    {
        ResolveRefs();

        if (Input.GetKeyDown(toggleActiveKey))
            active = !active;

        if (Input.GetKeyDown(addModeKey)) mode = FloodMode.Add;
        if (Input.GetKeyDown(removeModeKey)) mode = FloodMode.Remove;
        if (Input.GetKeyDown(setModeKey)) mode = FloodMode.SetFraction;
        if (Input.GetKeyDown(emptyModeKey)) mode = FloodMode.Empty;
        if (Input.GetKeyDown(fillModeKey)) mode = FloodMode.Fill;
        if (Input.GetKeyDown(toggleOverlayKey))
            showOverlay = !showOverlay;

        if (!active)
            return;

        RefreshCompartmentCacheIfEmpty();

        bool hasMouseWorld = TryGetMouseWorld(out _mouseWorld);
        _hovered = hasMouseWorld ? FindCompartmentAtPoint(_mouseWorld) : null;

        _lastMouseDebug = BuildMouseDebug(hasMouseWorld, _mouseWorld, _hovered);

        if (Input.GetKeyDown(logMouseKey))
            Debug.Log(_lastMouseDebug, this);

        if (logMouseWhenPressed && Input.GetMouseButtonDown(mouseButton))
            Debug.Log(_lastMouseDebug, this);

        if (Input.GetMouseButtonDown(mouseButton))
        {
            if (ShouldBlockFloodToolClick())
                return;

            if (_hovered == null)
            {
                if (logMisses)
                    Debug.Log(_lastMouseDebug, this);

                return;
            }

            FloodMode appliedMode =
                shiftClickRemoves && IsShiftHeld()
                    ? FloodMode.Remove
                    : mode;

            ApplyMode(_hovered, appliedMode);
        }

        if (Input.GetKeyDown(decreaseAmountKey))
            AdjustAmount(IsShiftHeld() ? -amountLargeStep : -amountSmallStep);

        if (Input.GetKeyDown(increaseAmountKey))
            AdjustAmount(IsShiftHeld() ? amountLargeStep : amountSmallStep);
    }

    private void AdjustAmount(float delta)
    {
        amount = Mathf.Max(0f, amount + delta);
    }

    [ContextMenu("Refresh Compartment Cache")]
    public void RefreshCompartmentCache()
    {
        _compartments.Clear();

        ResolveRefs();

        if (boat != null && boat.Compartments != null)
        {
            for (int i = 0; i < boat.Compartments.Count; i++)
            {
                Compartment c = boat.Compartments[i];
                if (c != null && !_compartments.Contains(c))
                    _compartments.Add(c);
            }
        }

        Transform root = boatRoot != null ? boatRoot : transform;

        Compartment[] found = root.GetComponentsInChildren<Compartment>(true);
        for (int i = 0; i < found.Length; i++)
        {
            Compartment c = found[i];
            if (c != null && !_compartments.Contains(c))
                _compartments.Add(c);
        }
    }

    private void RefreshCompartmentCacheIfEmpty()
    {
        if (_compartments.Count == 0)
            RefreshCompartmentCache();
    }

    private void ResolveRefs()
    {
        if (boat == null)
            boat = GetComponent<Boat>() ?? GetComponentInParent<Boat>();

        if (boatRoot == null && boat != null)
            boatRoot = boat.transform;

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private bool TryGetMouseWorld(out Vector2 world)
    {
        world = default;

        if (targetCamera == null)
            return false;

        float zPlane = boatRoot != null ? boatRoot.position.z : 0f;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, zPlane));

        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 hit = ray.GetPoint(enter);
        world = hit;
        return true;
    }

    private Compartment FindCompartmentAtPoint(Vector2 worldPoint)
    {
        Compartment best = null;
        float bestArea = float.PositiveInfinity;

        for (int i = 0; i < _compartments.Count; i++)
        {
            Compartment c = _compartments[i];

            if (c == null || !c.isActiveAndEnabled)
                continue;

            bool hit = false;
            float area = float.PositiveInfinity;

            Vector2[] poly = c.GetWorldCorners();
            if (poly != null && poly.Length >= 3)
            {
                hit = ConvexPolygonUtil.PointInsideConvex(worldPoint, poly);
                area = Mathf.Abs(SignedArea(poly));
            }

            if (!hit)
            {
                BoxCollider2D box = c.GetComponent<BoxCollider2D>();
                if (box != null)
                    hit = box.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, c.transform.position.z));
            }

            if (!hit)
                continue;

            if (area < bestArea)
            {
                bestArea = area;
                best = c;
            }
        }

        return best;
    }

    private void ApplyMode(Compartment compartment, FloodMode appliedMode)
    {
        if (compartment == null)
            return;

        switch (appliedMode)
        {
            case FloodMode.Add:
                compartment.AcceptWater(amount);
                break;

            case FloodMode.Remove:
                compartment.RemoveWater(amount);
                break;

            case FloodMode.SetFraction:
                SetFraction(compartment, setFraction01);
                break;

            case FloodMode.Empty:
                compartment.RemoveWater(float.MaxValue);
                break;

            case FloodMode.Fill:
                SetFraction(compartment, 1f);
                break;
        }

        compartment.RecomputeWaterSurface();

        if (logClicks)
        {
            Debug.Log(
                $"[CompartmentFloodGameClickTool] {appliedMode} '{compartment.name}' " +
                $"water={GetFraction(compartment):P0} amount={amount:0.###} mouseWorld={_mouseWorld}",
                compartment);
        }
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

    private string BuildMouseDebug(bool hasMouseWorld, Vector2 mouseWorld, Compartment hovered)
    {
        StringBuilder sb = new();

        sb.AppendLine("[CompartmentFloodGameClickTool] Mouse Debug");
        sb.AppendLine($"Active: {active}");
        sb.AppendLine($"Mode: {mode}");
        sb.AppendLine($"Camera: {(targetCamera != null ? targetCamera.name : "NULL")}");
        sb.AppendLine($"Boat Root: {(boatRoot != null ? boatRoot.name : "NULL")}");
        sb.AppendLine($"Compartments: {_compartments.Count}");
        sb.AppendLine($"Screen Mouse: {Input.mousePosition}");
        sb.AppendLine($"Has Mouse World: {hasMouseWorld}");
        sb.AppendLine($"Mouse World: {mouseWorld}");
        sb.AppendLine($"Hovered: {(hovered != null ? hovered.name : "none")}");

        sb.AppendLine("Compartment scan:");

        for (int i = 0; i < _compartments.Count; i++)
        {
            Compartment c = _compartments[i];

            if (c == null)
            {
                sb.AppendLine($"  [{i}] NULL");
                continue;
            }

            bool activeEnabled = c.isActiveAndEnabled;

            Vector2[] poly = c.GetWorldCorners();
            bool polyValid = poly != null && poly.Length >= 3;
            bool polyHit = false;

            if (polyValid)
                polyHit = ConvexPolygonUtil.PointInsideConvex(mouseWorld, poly);

            BoxCollider2D box = c.GetComponent<BoxCollider2D>();
            bool boxHit = box != null &&
                          box.bounds.Contains(new Vector3(mouseWorld.x, mouseWorld.y, c.transform.position.z));

            sb.AppendLine(
                $"  [{i}] {c.name}: active={activeEnabled} polyValid={polyValid} " +
                $"polyHit={polyHit} boxHit={boxHit} water={GetFraction(c):P0}");
        }

        return sb.ToString();
    }

    private void OnGUI()
    {
        if (!showOverlay)
            return;

        if (_overlayRect.width <= 0f || _overlayRect.height <= 0f)
        {
            _overlayRect = new Rect(
                overlayPosition.x,
                overlayPosition.y,
                overlaySize.x,
                overlaySize.y);
        }

        _overlayRect = RuntimeDebugOverlayGUI.DrawWindow(
            OverlayWindowId,
            _overlayRect,
            "Flood Tool",
            () => DrawOverlayWindow(OverlayWindowId),
            overlayDraggable);

        overlayPosition = _overlayRect.position;
    }

    private void DrawOverlayWindow(int id)
    {
        GUILayout.Label($"Active: {active}   Toggle: {toggleActiveKey}");
        GUILayout.Label($"Overlay: {toggleOverlayKey}");
        GUILayout.Label($"Mode: {mode}   1 Add / 2 Remove / 3 Set / 4 Empty / 5 Fill");
        GUILayout.Label($"Hovered: {(_hovered != null ? _hovered.name : "none")}");
        GUILayout.Label($"Compartments: {_compartments.Count}");

        GUILayout.Space(4);

        GUILayout.Label($"Amount: {amount:0.###}   {decreaseAmountKey}/{increaseAmountKey}: -/+   Shift = large step");

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("- Big")) AdjustAmount(-amountLargeStep);
            if (GUILayout.Button("-")) AdjustAmount(-amountSmallStep);
            if (GUILayout.Button("+")) AdjustAmount(amountSmallStep);
            if (GUILayout.Button("+ Big")) AdjustAmount(amountLargeStep);
        }

        if (amountPresets != null && amountPresets.Length > 0)
        {
            using (new GUILayout.HorizontalScope())
            {
                for (int i = 0; i < amountPresets.Length; i++)
                {
                    float preset = Mathf.Max(0f, amountPresets[i]);
                    if (GUILayout.Button(preset.ToString("0.##")))
                        amount = preset;
                }
            }
        }

        GUILayout.Label($"Click: apply   Shift+Click: remove   Log: {logMouseKey}");

        if (overlayDraggable)
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    private bool IsMouseOverOverlay()
    {
        if (!showOverlay)
            return false;

        Rect r = _overlayRect.width > 0f && _overlayRect.height > 0f
            ? _overlayRect
            : new Rect(overlayPosition.x, overlayPosition.y, overlaySize.x, overlaySize.y);

        return RuntimeDebugOverlayGUI.IsScreenMouseOverGUIRect(r);
    }

    private static bool IsShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private static float GetFraction(Compartment c)
    {
        if (c == null || c.MaxWaterArea <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(c.WaterArea / c.MaxWaterArea);
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

    private bool ShouldBlockFloodToolClick()
    {
        if (respectGameplayInputBlocker && GameplayInputBlocker.IsBlocked)
            return true;

        if (ignoreFloodClicksOverOverlay && IsMouseOverOverlay())
            return true;

        if (ignoreClicksWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        return false;
    }
}