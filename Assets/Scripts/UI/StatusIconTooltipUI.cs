using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatusIconTooltipUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Position")]
    [SerializeField] private bool followCursor = true;
    [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);

    private bool _visible;

    private void Reset()
    {
        tooltipRoot = transform as RectTransform;
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        if (texts.Length > 0)
            titleText = texts[0];

        if (texts.Length > 1)
            bodyText = texts[1];
    }

    private void Awake()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (!_visible || !followCursor || tooltipRoot == null)
            return;

        tooltipRoot.position = (Vector2)Input.mousePosition + screenOffset;
    }

    public void Show(string title, string body, Vector2 screenPosition)
    {
        if (tooltipRoot == null)
            tooltipRoot = transform as RectTransform;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(title) ? "Status" : title;

        if (bodyText != null)
            bodyText.text = string.IsNullOrWhiteSpace(body) ? string.Empty : body;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(true);
            tooltipRoot.position = screenPosition + screenOffset;
        }

        _visible = true;
    }

    public void Hide()
    {
        _visible = false;

        if (tooltipRoot == null)
            tooltipRoot = transform as RectTransform;

        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);
    }
}