using UnityEngine;
using UnityEngine.UI;

public class WorldMapTooltipUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform panel;
    [SerializeField] private Text label;

    [Header("Layout")]
    public Vector2 screenOffset = new Vector2(16f, -16f);
    public int fontSize = 16;
    public Vector2 padding = new Vector2(10f, 8f);

    private void Awake()
    {
        EnsureUI();
        Hide();
    }

    private void EnsureUI()
    {
        if (canvas == null)
        {
            // Find an existing canvas or create one
            canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var cgo = new GameObject("WorldMapUI");
                canvas = cgo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                cgo.AddComponent<CanvasScaler>();
                cgo.AddComponent<GraphicRaycaster>();
            }
        }

        if (panel != null && label != null) return;

        var pgo = new GameObject("TooltipPanel");
        pgo.transform.SetParent(canvas.transform, false);

        panel = pgo.AddComponent<RectTransform>();
        var img = pgo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.75f);

        // Top-left anchored (we position near cursor)
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);

        var tgo = new GameObject("TooltipText");
        tgo.transform.SetParent(panel, false);

        var tr = tgo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(padding.x, padding.y);
        tr.offsetMax = new Vector2(-padding.x, -padding.y);

        label = tgo.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    public void Show(string text, Vector2 screenPos)
    {
        EnsureUI();

        label.text = text;

        float width = Mathf.Clamp(420f, 240f, 520f);
        float height = Mathf.Clamp(24f + label.preferredHeight + padding.y * 2f, 40f, 600f);
        panel.sizeDelta = new Vector2(width, height);

        panel.gameObject.SetActive(true);

        // Convert screen -> canvas local (origin at canvas center)
        var canvasRect = canvas.transform as RectTransform;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localPoint
        );

        // Our panel is anchored at TOP-LEFT, so anchoredPosition is relative to that anchor point.
        // The top-left point in canvas local space is (xMin, yMax).
        Vector2 topLeftLocal = new Vector2(canvasRect.rect.xMin, canvasRect.rect.yMax);

        // We want the panel's TOP-LEFT (pivot) to land near the cursor:
        Vector2 desiredPivotLocal = localPoint + screenOffset;

        // Convert to anchoredPosition (relative to top-left anchor reference)
        panel.anchoredPosition = desiredPivotLocal - topLeftLocal;

        // Optional: keep it inside screen
        KeepOnScreen(canvasRect);
    }

    private void KeepOnScreen(RectTransform canvasRect)
    {
        Vector2 pos = panel.anchoredPosition;
        Vector2 size = panel.sizeDelta;

        // For top-left anchored + top-left pivot:
        // anchoredPosition.x increases right
        // anchoredPosition.y increases DOWN (because it's offset from top edge)
        float minX = 0f;
        float maxX = canvasRect.rect.width - size.x;

        float minY = -canvasRect.rect.height + size.y; // downwards (negative in anchoredPosition space)
        float maxY = 0f;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        panel.anchoredPosition = pos;
    }


    public void Hide()
    {
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
