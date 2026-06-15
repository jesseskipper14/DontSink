using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StatusIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image circleImage;
    [SerializeField] private Image iconImage;

    private StatusIconTooltipUI _tooltip;
    private string _title;
    private string _description;

    private void Reset()
    {
        rectTransform = transform as RectTransform;

        Image[] images = GetComponentsInChildren<Image>(true);
        if (images.Length > 0)
            circleImage = images[0];

        if (images.Length > 1)
            iconImage = images[1];
    }

    private void Awake()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
    }

    public void SetSize(float size)
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(size, size);
    }

    public void Bind(
        Sprite icon,
        Color circleColor,
        string title,
        string description,
        StatusIconTooltipUI tooltip)
    {
        _title = title;
        _description = description;
        _tooltip = tooltip;

        if (circleImage != null)
        {
            circleImage.color = circleColor;
            circleImage.enabled = true;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        gameObject.SetActive(true);
    }

    public void Clear()
    {
        _tooltip = null;
        _title = null;
        _description = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_tooltip == null)
            return;

        _tooltip.Show(_title, _description, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltip == null)
            return;

        _tooltip.Hide();
    }
}