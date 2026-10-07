using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Host resolves exact carried item identity; clients must never supply chart payloads.</summary>
public static class CartographicChartIntegration
{
    public sealed class CarriedChart
    {
        public ItemInstance Item { get; internal set; }
        internal InventorySlot slot;
        internal ItemContainerState parent;
        internal ItemDefinition parentDefinition;
        internal PlayerEquipment equipment;
        internal BottomBarSlotType anchor;
        public bool StillOwned => slot != null ? ReferenceEquals(slot.Instance, Item) :
            equipment != null && ReferenceEquals(equipment.Get(anchor), Item);
    }

    private static bool integrating;
    public static List<CarriedChart> Collect(GameObject requester, bool chartsOnly = true)
    {
        var result = new List<CarriedChart>();
        var inventory = CelestialChartPaperConsumption.ResolveInventory(requester);
        if (inventory == null) return result;
        var visited = new HashSet<ItemInstance>();
        for (int i = 0; i < inventory.HotbarSlotCount; i++)
            Visit(inventory.GetSlot(i)?.Instance, inventory.GetSlot(i), null, null, default, visited, result, chartsOnly);
        var equipment = inventory.Equipment;
        if (equipment != null)
            foreach (var anchor in new[] { BottomBarSlotType.Hands, BottomBarSlotType.Head, BottomBarSlotType.Feet,
                BottomBarSlotType.Body, BottomBarSlotType.Toolbelt, BottomBarSlotType.Backpack })
                Visit(equipment.Get(anchor), null, null, equipment, anchor, visited, result, chartsOnly);
        return result;
    }

    private static void Visit(ItemInstance item, InventorySlot slot, ItemContainerState parent,
        PlayerEquipment equipment, BottomBarSlotType anchor, HashSet<ItemInstance> visited, List<CarriedChart> result, bool chartsOnly,
        ItemDefinition parentDefinition = null)
    {
        if (item == null || item.IsDepleted() || !visited.Add(item)) return;
        if (!chartsOnly || item.HasCartographicChart)
            result.Add(new CarriedChart { Item = item, slot = slot, parent = parent, parentDefinition = parentDefinition, equipment = equipment, anchor = anchor });
        var container = item.ContainerState;
        if (container == null) return;
        for (int i = 0; i < container.SlotCount; i++)
            Visit(container.GetSlot(i)?.Instance, container.GetSlot(i), container, null, default, visited, result, chartsOnly, item.Definition);
    }

    public static bool TryIntegrate(MapTableCartridge table, string itemId, out string reason)
    {
        reason = null;
        if (!GameplayAuthority.IsAuthoritative) { reason = "Chart integration requires the host."; return false; }
        if (integrating) { reason = "Another chart integration is in progress."; return false; }
        if (table == null || table.Requester == null || table.Runner == null || !table.Runner.IsCurrentTable(table))
        { reason = "Open the Mapping Table to integrate a chart."; return false; }
        if (!table.TryGetCartographicContext(out var source, out var field))
        { reason = "The shared map is not ready."; return false; }
        var inventory = CelestialChartPaperConsumption.ResolveInventory(table.Requester);
        var location = Collect(table.Requester).Find(c => c.Item.InstanceId == itemId);
        if (location == null || !location.StillOwned || location.Item.Quantity != 1 || location.Item.IsContainer)
        { reason = "The exact chart is no longer carried, or its item is malformed."; return false; }
        var chart = location.Item.CartographicChart;
        if (chart == null || !chart.CanIntegrate(field, out reason)) return false;

        integrating = true;
        try
        {
            var previous = new WorldMapKnowledgeSaveSnapshot();
            source.State.CopyToSnapshot(previous);
            // No yield or inventory callbacks between ownership validation and these two writes.
            // Payload validation is atomic and precedes every map mutation.
            if (!source.TryCommitCartographicSource(chart.payload, out reason)) return false;
            if (location.slot != null) location.slot.Clear();
            else
            {
                // Remove clears the equipment anchor before notifying observers.
                try { location.equipment.Remove(location.anchor); }
                catch (Exception error) { Debug.LogException(error); }
            }
            table.BeginCartographicReveal(previous);
            try { location.parent?.NotifyChanged(); inventory?.NotifyChanged(); }
            catch (Exception error) { Debug.LogException(error); }
            reason = "Chart integrated into the shared map.";
            return true;
        }
        finally { integrating = false; }
    }
}
