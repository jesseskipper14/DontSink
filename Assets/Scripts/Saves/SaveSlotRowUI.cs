using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SaveSlotRowUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    [Header("Selection Visual")]
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color selectedColor = new Color(0.45f, 0.65f, 1f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.25f, 0.35f);

    private SaveSlotSummary _summary;
    private SaveLoadPanelUI _owner;

    public string SlotId => _summary != null ? _summary.slotId : null;

    private void Awake()
    {
        CacheRefs();
        ApplyVisual(selected: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheRefs();
        ApplyVisual(selected: false);
    }
#endif

    private void CacheRefs()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (background == null)
            background = GetComponent<Image>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
    }

    public void Bind(SaveLoadPanelUI owner, SaveSlotSummary summary)
    {
        CacheRefs();

        _owner = owner;
        _summary = summary;

        if (label != null)
        {
            string display = string.IsNullOrWhiteSpace(summary.displayName)
                ? summary.slotId
                : summary.displayName;

            string updated = FormatUtc(summary.updatedUtc);
            string validity = summary.isValid
                ? ""
                : $"  [INVALID] Save Version: {summary.schemaVersion}, Game Version: {SafeText(summary.gameVersion)}";

            label.text =
                $"{summary.slotId}\n" +
                $"{display}\n" +
                $"{updated}{validity}";
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);

            // Invalid saves remain visible, but cannot be loaded/selected.
            button.interactable = summary != null && summary.isValid;
        }

        ApplyVisual(selected: false);
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    public void SetSelectedVisual(bool selected)
    {
        ApplyVisual(selected);
    }

    private void ApplyVisual(bool selected)
    {
        if (background == null)
            return;

        if (_summary != null && !_summary.isValid)
        {
            background.color = invalidColor;
            return;
        }

        background.color = selected ? selectedColor : normalColor;
    }

    private void HandleClicked()
    {
        if (_owner != null && _summary != null && _summary.isValid)
            _owner.SelectSlot(_summary);
    }

    private static string FormatUtc(string utc)
    {
        if (DateTime.TryParse(utc, out DateTime dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        return utc;
    }
}