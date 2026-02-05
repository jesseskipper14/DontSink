using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorldMapHoverController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera cam;
    [SerializeField] private WorldMapGraphGenerator generator;
    [SerializeField] private WorldMapTooltipUI tooltip;

    [Header("Raycast")]
    [SerializeField] private LayerMask hoverLayers = ~0;

    private MapNodeView _current;

    private void Reset()
    {
        cam = Camera.main;
        generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        tooltip = FindAnyObjectByType<WorldMapTooltipUI>();
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (generator == null) generator = FindAnyObjectByType<WorldMapGraphGenerator>();
        if (tooltip == null) tooltip = FindAnyObjectByType<WorldMapTooltipUI>();
    }

    private void Update()
    {
        if (cam == null || generator?.graph == null || tooltip == null) return;

        // If UI is blocking pointer, don't hover nodes through it
        if (PointerOverInteractiveUI())
        {
            ClearHover();
            return;
        }

        Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = new Vector2(w.x, w.y);

        var hit = Physics2D.Raycast(p, Vector2.zero, 0f, hoverLayers);
        var view = hit.collider ? hit.collider.GetComponent<MapNodeView>() : null;

        if (view == _current && view != null)
        {
            UpdateTooltip(view);
            return;
        }

        var buffIcon = hit.collider ? hit.collider.GetComponent<BuffIconView>() : null;
        if (buffIcon != null)
        {
            tooltip.Show(buffIcon.GetTooltipText(), Input.mousePosition);
            _current = null;
            return;
        }

        _current = view;

        if (_current == null)
        {
            tooltip.Hide();
            return;
        }

        UpdateTooltip(_current);
    }

    private void UpdateTooltip(MapNodeView view)
    {
        int id = view.NodeId;
        if (id < 0 || id >= generator.graph.nodes.Count) return;

        var node = generator.graph.nodes[id];

        string text =
            $"** {node.displayName} **\n" +
            $"Dock: {node.dock.rating:0.00}   Trade: {node.tradeHub.rating:0.00}\n" +
            $"{FormatStats(node)}\n" +
            $"{FormatBuffs(node)}";

        tooltip.Show(text, Input.mousePosition);
    }

    private static string FormatStats(MapNode node)
    {
        if (node.stats == null || node.stats.Count == 0) return "Stats: (none)";

        string s = "Stats:\n";
        for (int i = 0; i < node.stats.Count; i++)
        {
            var st = node.stats[i];
            s += $"  - {st.id}: {st.stat.value:0.00}\n";
        }
        return s.TrimEnd();
    }

    private static string FormatBuffs(MapNode node)
    {
        if (node.activeBuffs == null || node.activeBuffs.Count == 0) return "Buffs: (none)";

        int n = Mathf.Min(node.activeBuffs.Count, 8);
        string s = $"Buffs ({node.activeBuffs.Count}):\n";
        for (int i = 0; i < n; i++)
        {
            var b = node.activeBuffs[i];
            if (b.buff == null) continue;
            float rem = Mathf.Max(0f, b.durationHours - b.elapsedHours);
            s += $"  - {b.buff.displayName} ({rem:0.0}h)\n";
        }
        if (node.activeBuffs.Count > n) s += "  - ...\n";
        return s.TrimEnd();
    }

    private void ClearHover()
    {
        _current = null;
        tooltip.Hide();
    }

    private static bool PointerOverInteractiveUI()
    {
        if (EventSystem.current == null) return false;

        var results = new List<RaycastResult>();
        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(data, results);

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.GetComponent<Selectable>() != null)
                return true; // Button/Slider/etc.
        }

        return false;
    }
}
