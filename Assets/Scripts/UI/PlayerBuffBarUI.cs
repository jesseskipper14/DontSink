using Survival.Buffs;
// Adjust this namespace if yours differs.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBuffBarUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerBuffSystem playerBuffSystem;
    [SerializeField] private StatusIconUI iconPrefab;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private StatusIconTooltipUI tooltip;

    [Header("Layout")]
    [SerializeField, Min(1)] private int maxColumns = 20;
    [SerializeField, Min(8f)] private float iconSize = 34f;
    [SerializeField, Min(0f)] private float columnSpacing = 6f;
    [SerializeField, Min(0f)] private float rowSpacing = 6f;
    [SerializeField] private Vector2 padding = new Vector2(0f, 0f);
    [SerializeField] private bool hideRootWhenEmpty = true;

    [Header("Colors")]
    [SerializeField] private Color positiveColor = new Color(0.20f, 0.85f, 0.35f, 0.95f);
    [SerializeField] private Color negativeColor = new Color(0.90f, 0.20f, 0.20f, 0.95f);
    [SerializeField] private Color neutralColor = new Color(0.55f, 0.55f, 0.55f, 0.95f);

    private readonly List<StatusIconUI> _icons = new();

    private void Reset()
    {
        if (iconRoot == null)
            iconRoot = transform as RectTransform;

        if (playerBuffSystem == null)
            playerBuffSystem = FindFirstObjectByType<PlayerBuffSystem>();

        if (tooltip == null)
            tooltip = FindFirstObjectByType<StatusIconTooltipUI>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        if (iconRoot == null)
            iconRoot = transform as RectTransform;
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (playerBuffSystem == null || iconPrefab == null || iconRoot == null)
        {
            SetVisible(false);
            return;
        }

        IReadOnlyList<PlayerBuffInstance> buffs = playerBuffSystem.Current;
        int count = buffs != null ? buffs.Count : 0;

        EnsureIconCount(count);

        int shown = 0;

        for (int i = 0; i < count; i++)
        {
            PlayerBuffInstance instance = buffs[i];
            PlayerBuffDefinition definition = instance.definition;

            if (definition == null)
                continue;

            StatusIconUI icon = _icons[shown];
            RectTransform rt = icon.transform as RectTransform;

            PositionIcon(rt, shown);

            icon.SetSize(iconSize);
            icon.Bind(
                definition.Icon,
                ResolveColor(definition.Polarity),
                definition.DisplayName,
                definition.Description,
                tooltip);

            shown++;
        }

        for (int i = shown; i < _icons.Count; i++)
            _icons[i].Clear();

        SetVisible(!hideRootWhenEmpty || shown > 0);
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

    private Color ResolveColor(PlayerBuffPolarity polarity)
    {
        return polarity switch
        {
            PlayerBuffPolarity.Positive => positiveColor,
            PlayerBuffPolarity.Negative => negativeColor,
            _ => neutralColor
        };
    }

    private void SetVisible(bool visible)
    {
        if (iconRoot != null)
            iconRoot.gameObject.SetActive(visible);
    }
}