using UnityEngine;

public sealed class MapOverlayPanZoom : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup overlayRoot;     // optional: disable controls when hidden
    [SerializeField] private RectTransform mapViewport;   // visible rect (MapPanel)
    [SerializeField] private RectTransform content;       // movable/scalable container (MapContent)

    [Header("Pan")]
    public float panSpeed = 900f;        // UI units per second at zoom=1
    public float fastMultiplier = 2.5f;  // shift boost
    public bool requireMiddleMouseToPan = false;

    [Header("Zoom")]
    public bool enableZoom = true;
    public float zoomSpeed = 0.20f;      // scale change per scroll tick (tweak)
    public float minScale = 0.6f;
    public float maxScale = 3.0f;

    [Header("Behavior")]
    public bool onlyWhenPointerOverViewport = true;
    public bool clampToViewport = true;

    private void Reset()
    {
        // best-effort auto wiring
        overlayRoot = GetComponentInParent<CanvasGroup>();
    }

    private void Update()
    {
        if (!IsActive()) return;

        if (enableZoom)
            HandleZoom();

        HandlePan();

        if (clampToViewport)
            ClampContentToViewport();
    }

    private bool IsActive()
    {
        if (overlayRoot != null && (overlayRoot.alpha <= 0.001f || !overlayRoot.blocksRaycasts))
            return false;

        if (mapViewport == null || content == null) return false;

        if (!onlyWhenPointerOverViewport) return true;

        return RectTransformUtility.RectangleContainsScreenPoint(
            mapViewport,
            Input.mousePosition,
            null // overlay canvas is screen space overlay
        );
    }

    private void HandlePan()
    {
        if (requireMiddleMouseToPan && !Input.GetMouseButton(2))
            return;

        float h = -Input.GetAxisRaw("Horizontal"); // A/D negative to feel correct
        float v = -Input.GetAxisRaw("Vertical");   // W/S negative to feel correct
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
        Vector2 mouse;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, Input.mousePosition, null, out mouse);

        Vector2 before = ContentLocalPoint(mouse);

        float s = content.localScale.x;
        s *= (1f + scroll * zoomSpeed);
        s = Mathf.Clamp(s, minScale, maxScale);
        content.localScale = new Vector3(s, s, 1f);

        Vector2 after = ContentLocalPoint(mouse);
        Vector2 diff = after - before;

        // Adjust position so the point under the cursor stays under cursor
        content.anchoredPosition += diff * s;
    }

    // Convert viewport-local point into content-local point in a stable way
    private Vector2 ContentLocalPoint(Vector2 viewportLocalPoint)
    {
        // viewport local -> world
        Vector3 world = mapViewport.TransformPoint(viewportLocalPoint);
        // world -> content local
        Vector2 local = content.InverseTransformPoint(world);
        return local;
    }

    private void ClampContentToViewport()
    {
        // If content is smaller than viewport, center it.
        Rect v = mapViewport.rect;
        Rect c = content.rect;

        // Effective content size after scale
        float s = content.localScale.x;
        float cW = c.width * s;
        float cH = c.height * s;

        float vW = v.width;
        float vH = v.height;

        Vector2 pos = content.anchoredPosition;

        float minX, maxX, minY, maxY;

        if (cW <= vW)
        {
            minX = maxX = 0f;
        }
        else
        {
            float excessX = (cW - vW) * 0.5f;
            minX = -excessX;
            maxX = +excessX;
        }

        if (cH <= vH)
        {
            minY = maxY = 0f;
        }
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
