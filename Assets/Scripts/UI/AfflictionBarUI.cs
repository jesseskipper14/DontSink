using System.Collections.Generic;
using Survival.Afflictions;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AfflictionBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AfflictionSystem afflictionSystem;
    [SerializeField] private StatusIconUI iconPrefab;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private StatusIconTooltipUI tooltip;

    [SerializeField] private AfflictionCatalog catalog;

    [Header("Layout")]
    [SerializeField, Min(1)] private int maxColumns = 20;
    [SerializeField, Min(8f)] private float iconSize = 34f;
    [SerializeField, Min(0f)] private float columnSpacing = 6f;
    [SerializeField, Min(0f)] private float rowSpacing = 6f;
    [SerializeField] private Vector2 padding = new Vector2(0f, 0f);
    [SerializeField] private bool hideRootWhenEmpty = true;

    [Header("Severity Colors")]
    [SerializeField] private Color unknownColor = new Color(0.55f, 0.55f, 0.55f, 0.95f);
    [SerializeField] private Color mildColor = new Color(0.95f, 0.75f, 0.20f, 0.95f);
    [SerializeField] private Color moderateColor = new Color(1.00f, 0.45f, 0.15f, 0.95f);
    [SerializeField] private Color severeColor = new Color(0.90f, 0.12f, 0.12f, 0.95f);

    [Header("Severity Thresholds")]
    [SerializeField, Range(0f, 1f)] private float mildThreshold = 0.10f;
    [SerializeField, Range(0f, 1f)] private float moderateThreshold = 0.35f;
    [SerializeField, Range(0f, 1f)] private float severeThreshold = 0.70f;

    private readonly List<StatusIconUI> _icons = new();
    private readonly Dictionary<string, AfflictionDefinition> _definitionById = new();

    private void Reset()
    {
        if (iconRoot == null)
            iconRoot = transform as RectTransform;

        if (afflictionSystem == null)
            afflictionSystem = FindFirstObjectByType<AfflictionSystem>();

        if (tooltip == null)
            tooltip = FindFirstObjectByType<StatusIconTooltipUI>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (iconRoot == null)
            iconRoot = transform as RectTransform;
    }

    private void OnValidate()
    {
        moderateThreshold = Mathf.Max(moderateThreshold, mildThreshold);
        severeThreshold = Mathf.Max(severeThreshold, moderateThreshold);
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (afflictionSystem == null || iconPrefab == null || iconRoot == null)
        {
            SetVisible(false);
            return;
        }

        IReadOnlyList<AfflictionInstance> afflictions = afflictionSystem.Current;
        int count = afflictions != null ? afflictions.Count : 0;

        EnsureIconCount(count);

        int shown = 0;

        for (int i = 0; i < count; i++)
        {
            AfflictionInstance instance = afflictions[i];

            if (instance.severity01 <= 0f)
                continue;

            string stableId = instance.stableId.value;
            AfflictionDefinition definition = ResolveDefinition(stableId);

            StatusIconUI icon = _icons[shown];
            RectTransform rt = icon.transform as RectTransform;

            PositionIcon(rt, shown);

            string tierLabel = definition != null
                ? definition.GetTierLabel(instance.severity01)
                : string.Empty;

            string title = BuildTitle(definition, stableId, tierLabel);
            string description = BuildDescription(definition, instance.severity01);

            icon.SetSize(iconSize);
            icon.Bind(
                definition != null ? definition.icon : null,
                ResolveSeverityColor(instance.severity01),
                title,
                description,
                tooltip);

            shown++;
        }

        for (int i = shown; i < _icons.Count; i++)
            _icons[i].Clear();

        SetVisible(!hideRootWhenEmpty || shown > 0);
    }

    private AfflictionDefinition ResolveDefinition(string stableId)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(stableId))
            return null;

        return catalog.TryGet(stableId, out AfflictionDefinition definition)
            ? definition
            : null;
    }

    private string BuildTitle(AfflictionDefinition definition, string stableId, string tierLabel)
    {
        string baseName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
            ? definition.displayName
            : stableId;

        if (string.IsNullOrWhiteSpace(tierLabel))
            return baseName;

        return $"{baseName} - {tierLabel}";
    }

    private string BuildDescription(AfflictionDefinition definition, float severity01)
    {
        string description = definition != null
            ? definition.description
            : string.Empty;

        string severityLine = $"Severity: {Mathf.RoundToInt(Mathf.Clamp01(severity01) * 100f)}%";

        if (string.IsNullOrWhiteSpace(description))
            return severityLine;

        return $"{description}\n\n{severityLine}";
    }

    private void EnsureIconCount(int desired)
    {
        while (_icons.Count < desired)
        {
            StatusIconUI icon = Instantiate(iconPrefab, iconRoot);
            icon.Clear();
            _icons.Add(icon);
        }
    }

    private void PositionIcon(RectTransform rt, int index)
    {
        if (rt == null)
            return;

        int column = index % maxColumns;
        int row = index / maxColumns;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        float x = padding.x + column * (iconSize + columnSpacing);
        float y = -padding.y - row * (iconSize + rowSpacing);

        rt.anchoredPosition = new Vector2(x, y);
    }

    private Color ResolveSeverityColor(float severity01)
    {
        float severity = Mathf.Clamp01(severity01);

        if (severity >= severeThreshold)
            return severeColor;

        if (severity >= moderateThreshold)
            return moderateColor;

        if (severity >= mildThreshold)
            return mildColor;

        return unknownColor;
    }

    private void SetVisible(bool visible)
    {
        if (iconRoot != null)
            iconRoot.gameObject.SetActive(visible);
    }
}