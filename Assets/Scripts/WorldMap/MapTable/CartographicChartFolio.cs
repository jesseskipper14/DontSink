using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Carried chart inspection and deliberate integration at the existing table.</summary>
public sealed class CartographicChartFolio
{
    private Vector2 scroll;
    private string status;
    private bool debug;
    private string selected;
    private readonly Dictionary<ItemInstance, CartographicChartState> previews = new();
    public void Draw(Rect panel, MapTableCartridge table)
    {
        GUI.BeginGroup(panel);
        var area = new Rect(12f, 10f, Mathf.Max(100f, panel.width - 24f), Mathf.Max(80f, panel.height - 20f));
        GUILayout.BeginArea(area);
        GUILayout.Label("CARRIED CHARTS — deliberate integration into the shared World Map");
        GUILayout.Label(status ?? "Inspect a chart. Integration consumes it only after the shared map accepts its data.");
        scroll = GUILayout.BeginScrollView(scroll);
        var charts = CartographicChartIntegration.Collect(table.Requester);
        var present = new HashSet<ItemInstance>();
        foreach (var entry in charts) present.Add(entry.Item);
        foreach (var item in new List<ItemInstance>(previews.Keys)) if (!present.Contains(item)) previews.Remove(item);
        if (charts.Count == 0) GUILayout.Label("No charts carried. Charts in Hands, pockets and portable containers appear here.");
        foreach (var location in charts)
        {
            var item = location.Item;
            if (!previews.TryGetValue(item, out var chart) || item.HasSoundingEvidence != (chart.kind == CartographicChartKind.SoundingEvidence))
                previews[item] = chart = item.CartographicChart;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(chart.title ?? "Untitled chart");
            GUILayout.Label(chart.kind == CartographicChartKind.SoundingEvidence ? "Sounding evidence — requires Surveyor processing" :
                chart.kind == CartographicChartKind.Reference ? "Reference evidence — retained for interpretation" : "Georeferenced — consumed on integration");
            if (GUILayout.Button(selected == item.InstanceId ? "Hide details" : "Inspect")) selected = selected == item.InstanceId ? null : item.InstanceId;
            if (selected == item.InstanceId)
            {
                GUILayout.Label(chart.referenceText ?? "No accompanying notes.");
                if (!string.IsNullOrWhiteSpace(chart.referenceSpriteResourcePath))
                {
                    var sprite = Resources.Load<Sprite>(chart.referenceSpriteResourcePath);
                    if (sprite != null)
                    {
                        var rect = GUILayoutUtility.GetRect(220f, 140f);
                        var uv = sprite.textureRect;
                        uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height,
                            uv.width / sprite.texture.width, uv.height / sprite.texture.height);
                        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv);
                    }
                }
                if (chart.kind == CartographicChartKind.Georeferenced && chart.payload != null)
                    GUILayout.Label($"Surface: {chart.payload.surface != null}   Depths: {chart.payload.bathymetry != null}\n" +
                        $"Nodes: {chart.payload.nodeIds?.Length ?? 0}   Surface POIs: {chart.payload.surfacePoiIds?.Length ?? 0}   Underwater POIs: {chart.payload.underwaterPoiIds?.Length ?? 0}");
            }
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && chart.kind == CartographicChartKind.Georeferenced && GameplayAuthority.IsAuthoritative;
            if (GUILayout.Button("Integrate"))
            {
                if (CartographicChartIntegration.TryIntegrate(table, item.InstanceId, out status)) table.SetActivePage(MapTablePage.WorldMap);
            }
            GUI.enabled = enabled;
            GUILayout.EndVertical();
        }
        debug = GUILayout.Toggle(debug, "DEBUG: chart test items");
        if (debug)
        {
            GUILayout.Label("Creates one physical chart at the current map center; does not reveal anything on pickup.");
            if (GUILayout.Button("Give test surface chart")) GiveTest(table, false, false);
            if (GUILayout.Button("Give test depth chart")) GiveTest(table, true, false);
            if (GUILayout.Button("Give reference chart")) GiveTest(table, false, true);
            GUILayout.Space(8f);
            GUILayout.Label("Local Surveyor seam — current node (debug-only until station placement)");
            if (GUILayout.Button("Issue current node's local island chart"))
            {
                if (table.TryGetCartographicContext(out var knowledge, out _))
                    SurveyorCartographyService.TryIssueLocalChart(table.Requester, knowledge, knowledge.CurrentNodeId, out status);
                else status = "Shared map is not ready.";
            }
            if (GUILayout.Button("Fix believed position to current node"))
            {
                if (table.TryGetCartographicContext(out var knowledge, out _))
                    SurveyorCartographyService.TryFixPosition(table.Requester, knowledge.CurrentNodeId, out status);
                else status = "Shared map is not ready.";
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.EndGroup();
    }
    private void GiveTest(MapTableCartridge table, bool depths, bool reference)
    {
        if (!GameplayAuthority.IsAuthoritative || !table.Runner.IsCurrentTable(table) ||
            !table.TryGetCartographicContext(out var source, out var field)) { status = "Host/shared map unavailable."; return; }
        var inventory = CelestialChartPaperConsumption.ResolveInventory(table.Requester);
        var definition = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        if (inventory == null || definition == null) { status = "Chart definition or inventory unavailable."; return; }
        var center = table.Viewport.CenterWorld;
        var mask = WorldMapCoverageMask.Circle(source.State.Width, source.State.Height, field.WorldBounds, center, 28f);
        var chart = new CartographicChartState {
            title = reference ? "Reference sketch" : depths ? "Test hydrographic chart" : "Test coastline chart",
            kind = reference ? CartographicChartKind.Reference : CartographicChartKind.Georeferenced,
            referenceText = reference ? "An old sailor sketched an island beyond the western stars. Interpret this clue yourself." : $"Test chart centered at {center.x:F2}, {center.y:F2}; radius 28 map units.",
            worldSeed = field.Seed, topographyVersion = field.GenerationVersion, worldBounds = field.WorldBounds,
            payload = reference ? null : new WorldMapCartographicPayload { sourceId = "debug:chart:" + Guid.NewGuid().ToString("N"),
                surface = depths ? null : mask, bathymetry = depths ? mask : null }
        };
        var item = ItemInstance.Create(definition);
        item.SetCartographicChart(chart);
        status = inventory.TryAddInstance(item) ? "Chart added; inspect or integrate it here." : "No room for a chart.";
    }
}
