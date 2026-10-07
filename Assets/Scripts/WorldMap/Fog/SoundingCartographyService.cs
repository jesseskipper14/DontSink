using System;
using UnityEngine;

/// <summary>Trusted host service. Recording callers supply a frozen physical measurement; station callers validate range.</summary>
public static class SoundingCartographyService
{
    private static bool busy;
    public static bool TryRecord(GameObject actor, CartographicChartState evidence, out string reason)
    {
        reason = "Sounding evidence is unavailable.";
        if (!GameplayAuthority.IsAuthoritative || busy) { reason = "Recording requires the host and an idle chart transaction."; return false; }
        if (actor == null || evidence?.kind != CartographicChartKind.SoundingEvidence || evidence.version != 1 ||
            !evidence.HasState || evidence.sounding?.IsValid(evidence.worldBounds) != true) return false;
        var inventory = CelestialChartPaperConsumption.ResolveInventory(actor);
        var definition = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        var paper = CartographicChartIntegration.Collect(actor, false).Find(c => c.slot != null &&
            c.Item.Definition.ItemId == "item_paper_charting" && !c.Item.HasCartographicChart && !c.Item.IsContainer);
        if (inventory == null || definition == null || paper == null || !paper.StillOwned)
        { reason = "Carry Charting Paper in a pocket or portable container to record a sounding."; return false; }
        InventorySlot destination = paper.Item.Quantity == 1 &&
            (paper.parentDefinition == null || paper.parentDefinition.CanContainerAccept(definition)) ? paper.slot : null;
        if (destination == null) for (int i = 0; i < inventory.HotbarSlotCount; i++)
            if (inventory.GetSlot(i)?.IsEmpty == true) { destination = inventory.GetSlot(i); break; }
        if (destination == null) { reason = "Make room for one Sounding Chart first."; return false; }
        var item = ItemInstance.Create(definition); item.SetCartographicChart(evidence);
        busy = true;
        try
        {
            // Both writes precede callbacks: no partial paper consumption or duplicate grant on reentry.
            if (paper.Item.Quantity == 1) paper.slot.Clear();
            else if (paper.Item.RemoveQuantityForTransaction(1) != 1) return false;
            destination.Set(item);
            try { paper.Item.PublishTransactionChange(); }
            catch (Exception error) { Debug.LogException(error); }
            try { paper.parent?.NotifyChanged(); inventory.NotifyChanged(); }
            catch (Exception error) { Debug.LogException(error); }
            reason = "Sounding Chart recorded. Take it to a Surveyor for processing.";
            return true;
        }
        finally { busy = false; }
    }

    public static bool TryProcess(GameObject actor, WorldMapKnowledgeSource source, string itemId, out string reason)
    {
        reason = "Carry a valid Sounding Chart from this world.";
        if (!GameplayAuthority.IsAuthoritative || busy || actor == null || source == null) return false;
        var carried = CartographicChartIntegration.Collect(actor).Find(c => c.Item.InstanceId == itemId);
        if (carried == null || !carried.StillOwned || carried.Item.Quantity != 1 || carried.Item.IsContainer ||
            !source.TryGetSurfaceSurveyWorld(out var field, out _) ||
            !SoundingChartBuilder.TryProcess(carried.Item.CartographicChart, itemId, field, source.State, out var processed,
                source.SoundingRevealRadius)) return false;
        busy = true;
        try
        {
            // Transform the physical evidence in place, preserving exact item identity and requiring no new slot.
            try { carried.Item.SetCartographicChart(processed); }
            catch (Exception error) { Debug.LogException(error); }
            if (carried.Item.HasSoundingEvidence) return false;
            try { carried.parent?.NotifyChanged(); CelestialChartPaperConsumption.ResolveInventory(actor)?.NotifyChanged(); }
            catch (Exception error) { Debug.LogException(error); }
            reason = "Seafloor chart processed. Integrate it at the Mapping Table.";
            return true;
        }
        finally { busy = false; }
    }
}
