using UnityEngine;

public sealed class MapOverlayPanZoom : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup overlayRoot;
    [SerializeField] private RectTransform mapViewport;   // visible rect (MapPanel)
    [SerializeField] private RectTransform content;       // movable/scalable container (MapContent)

    [Header("Pan")]
    [Tooltip("Hold RMB and drag to pan.")]
    public int panMouseButton = 1; // 1 = right mouse
    public float dragPanSpeed = 1f; // multiplier on drag delta
    public bool requirePointerOverViewport = true;

    [Header("Optional Keyboard Pan")]
    public bool enableKeyboardPan = false;
    public float panSpeed = 900f;
    public float fastMultiplier = 2.5f;

    [Header("Zoom")]
    public bool enableZoom = true;
    public float zoomSpeed = 0.20f;
    public float minScale = 0.6f;
    public float maxScale = 3.0f;

    [Header("Behavior")]
    public bool clampToViewport = true;

    private bool _dragging;
    private Vector2 _lastMousePos;

    private void Reset()
    {
        overlayRoot = GetComponentInParent<CanvasGroup>();
    }

    private void Update()
    {
        if (!IsActive()) return;

        if (enableZoom)
            HandleZoom();

        HandleDragPan();

        if (enableKeyboardPan)
            HandleKeyboardPan();

        if (clampToViewport)
            ClampContentToViewport();
    }

    private bool IsActive()
    {
        if (overlayRoot != null && (overlayRoot.alpha <= 0.001f || !overlayRoot.blocksRaycasts))
            return false;

        if (mapViewport == null || content == null) return false;

        if (!requirePointerOverViewport) return true;

        return RectTransformUtility.RectangleContainsScreenPoint(
            mapViewport,
            Input.mousePosition,
            null
        );
    }

    private void HandleDragPan()
    {
        // Begin drag
        if (!_dragging && Input.GetMouseButtonDown(panMouseButton))
        {
            if (!PointerOverViewport()) return;
            _dragging = true;
            _lastMousePos = Input.mousePosition;
        }

        // End drag
        if (_dragging && Input.GetMouseButtonUp(panMouseButton))
        {
            _dragging = false;
            return;
        }

        if (!_dragging) return;

        // While dragging
        Vector2 cur = Input.mousePosition;
        Vector2 deltaScreen = cur - _lastMousePos;
        _lastMousePos = cur;

        // Convert screen delta to UI anchored delta.
        // Screen Space Overlay: 1 screen pixel ~= 1 UI unit (usually).
        // If you use CanvasScaler, this still "feels" right for a debug pan.
        Vector2 delta = deltaScreen * dragPanSpeed;

        // Drag direction: moving mouse right should move map left (grab & drag)
        content.anchoredPosition += delta;
    }

    private bool PointerOverViewport()
    {
        if (!requirePointerOverViewport) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(mapViewport, Input.mousePosition, null);
    }

    private void HandleKeyboardPan()
    {
        float h = -Input.GetAxisRaw("Horizontal");
        float v = -Input.GetAxisRaw("Vertical");
        if (h == 0f && v == 0f) return;

        float speed = panSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMultiplier;

        Vector2 delta = new Vector2(h, v).normalized * speed * Time.unscaledDeltaTime;
        content.anchoredPosition += delta;
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        // Zoom around mouse position
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapViewport, Input.mousePosition, null, out var mouseLocal);

        Vector2 before = ContentLocalPoint(mouseLocal);

        float s = content.localScale.x;
        s *= (1f + scroll * zoomSpeed);
        s = Mathf.Clamp(s, minScale, maxScale);
        content.localScale = new Vector3(s, s, 1f);

        Vector2 after = ContentLocalPoint(mouseLocal);
        Vector2 diff = after - before;

        content.anchoredPosition += diff * s;
    }

    private Vector2 ContentLocalPoint(Vector2 viewportLocalPoint)
    {
        Vector3 world = mapViewport.TransformPoint(viewportLocalPoint);
        Vector2 local = content.InverseTransformPoint(world);
        return local;
    }

    private void ClampContentToViewport()
    {
        Rect v = mapViewport.rect;
        Rect c = content.rect;

        float s = content.localScale.x;
        float cW = c.width * s;
        float cH = c.height * s;

        float vW = v.width;
        float vH = v.height;

        Vector2 pos = content.anchoredPosition;

        float minX, maxX, minY, maxY;

        if (cW <= vW) minX = maxX = 0f;
        else
        {
            float excessX = (cW - vW) * 0.5f;
            minX = -excessX;
            maxX = +excessX;
        }

        if (cH <= vH) minY = maxY = 0f;
        else
        {
            float excessY = (cH - vH) * 0.5f;
            minY = -excessY;
            maxY = +excessY;
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        content.anchoredPosition = pos;
    }
}