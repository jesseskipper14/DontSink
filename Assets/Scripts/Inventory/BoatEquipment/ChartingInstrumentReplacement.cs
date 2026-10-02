using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Town/debug authority seam. Existence is derived from live boat-persisted items and crew
/// loadouts, not a Lost/recovery registry. No static cache survives a new game.
/// </summary>
public static class ChartingInstrumentReplacement
{
    private sealed class Source
    {
        public ItemInstance item;
        public ItemInstanceSnapshot snapshot;
        public string Id => item != null ? item.InstanceId : snapshot?.instanceId;
        public Action remove;
    }
    private static readonly BottomBarSlotType[] EquipmentSlots = {
        BottomBarSlotType.Hands, BottomBarSlotType.Backpack, BottomBarSlotType.Toolbelt,
        BottomBarSlotType.Head, BottomBarSlotType.Body, BottomBarSlotType.Feet };

    public static bool IsInstrumentDefinition(ItemDefinition definition) => definition != null &&
        (definition.ItemId == "item_charting_object" || (definition.WorldPrefab != null &&
        definition.WorldPrefab.GetComponent<ChartingInstrumentInteractable>() != null));

    public static bool HasValidPersistedInstrument(Boat boat) => Collect(boat).Count > 0;

    // Corrupt/debug duplicate items remain physically removable, but only one can chart.
    public static bool IsValidInstrument(Boat boat, ItemInstance item)
    {
        if (boat == null || item == null || item.IsDepleted() || item.ChartingInstrument?.invalidated == true) return false;
        var sources = Collect(boat);
        sources.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return sources.Count > 0 && ReferenceEquals(sources[0].item, item);
    }

    public static bool TryIssueReplacement(Boat boat, ItemDefinition definition, Vector2 position,
        out WorldItem replacement, out string error) => TryReplace(boat, definition, position, false, out replacement, out error);

    public static bool ForceReplaceChartingInstrument(Boat boat, ItemDefinition definition, Vector2 position,
        out WorldItem replacement, out string error) => TryReplace(boat, definition, position, true, out replacement, out error);

    private static bool TryReplace(Boat boat, ItemDefinition definition, Vector2 position, bool force,
        out WorldItem replacement, out string error)
    {
        replacement = null;
        error = null;
        if (!GameplayAuthority.IsAuthoritative) { error = "Instrument replacement requires gameplay authority."; return false; }
        if (boat == null || !boat.gameObject.activeInHierarchy || boat.GetComponent<BoatItemRegistry>() == null ||
            string.IsNullOrWhiteSpace(boat.BoatInstanceId) || definition == null ||
            !IsInstrumentDefinition(definition) || !definition.IsSacred || !definition.IsContainer ||
            definition.WorldPersistence != WorldItemPersistencePolicy.BoatOnly || definition.WorldPrefab == null ||
            definition.WorldPrefab.GetComponent<ChartingInstrumentInteractable>() == null)
        { error = "Configure the sacred BoatOnly Charting Instrument definition and its physical prefab first."; return false; }
        // A detached UI drag must be reconciled before it can be deleted/replaced safely.
        foreach (var drag in UnityEngine.Object.FindObjectsByType<InventoryDragController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ContainsInstrument(drag.DraggedItem) && !drag.PrepareForPersistenceCapture())
            { error = "Finish moving the charting instrument before replacing it."; return false; }
        }
        var sources = Collect(boat);
        if (!force && sources.Count > 0) { error = "This boat already has a valid charting instrument."; return false; }

        // Also destroy obsolete, unowned physical devices formerly associated with this boat.
        // They cannot become valid again after this issuance.
        foreach (var world in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (world.Instance?.ChartingInstrument?.boatInstanceId != boat.BoatInstanceId) continue;
            var currentBoat = world.GetComponent<BoatOwnedItem>()?.OwningBoat;
            if (currentBoat != null && currentBoat != boat) continue;
            var old = world.Instance;
            if (!sources.Exists(s => ReferenceEquals(s.item, old))) sources.Add(new Source {
                item = old, remove = () => DeleteWorld(world) });
        }
        foreach (var source in sources)
        {
            if (source.item != null) Invalidate(source.item);
            source.remove?.Invoke();
        }
        ScrubStoredBoatState(boat.BoatInstanceId);

        replacement = UnityEngine.Object.Instantiate(definition.WorldPrefab, position, boat.transform.rotation);
        var fresh = ItemInstance.Create(definition);
        fresh.SetChartingInstrumentState(new ChartingInstrumentState { boatInstanceId = boat.BoatInstanceId });
        replacement.Initialize(fresh);
        replacement.GetComponent<BoatOwnedItem>().AssignToBoat(boat);
        // Starts loose, with empty paper and no checkpoint; normal deployment rules still apply.
        return true;
    }

    private static void DeleteWorld(WorldItem world)
    {
        if (world == null) return;
        world.gameObject.SetActive(false); // Closes operator before end-of-frame destruction.
        world.GetComponent<BoatOwnedItem>()?.ClearOwnership();
        UnityEngine.Object.Destroy(world.gameObject);
    }

    private static void Invalidate(ItemInstance item)
    {
        if (item == null) return;
        var state = item.ChartingInstrument?.Copy() ?? new ChartingInstrumentState();
        state.pendingObservation = null;
        state.invalidated = true;
        state.revision++;
        item.SetChartingInstrumentState(state);
    }

    private static List<Source> Collect(Boat boat)
    {
        var result = new List<Source>();
        if (boat == null) return result;
        var visited = new HashSet<ItemInstance>();
        var registry = boat.GetComponent<BoatItemRegistry>();
        var liveKeys = new HashSet<string>(StringComparer.Ordinal);
        if (registry != null)
            foreach (var owned in registry.SnapshotItems())
            {
                if (owned.OwningBoat != boat) continue;
                var world = owned.GetComponent<WorldItem>();
                if (world?.Instance == null || world.Item.WorldPersistence == WorldItemPersistencePolicy.Never) continue;
                Add(world.Instance, () => DeleteWorld(world), result, visited);
            }
        foreach (var storage in boat.GetComponentsInChildren<StorageModule>(true))
            AddContainer(storage.ContainerState, result, visited);
        foreach (var inventory in UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var persistence = inventory.GetComponentInParent<PlayerLoadoutPersistence>();
            if (persistence != null) liveKeys.Add(persistence.PersistenceKey);
            bool isCrew = IsCrewInventory(inventory, boat);
            for (int i = 0; i < inventory.HotbarSlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                Add(slot.Instance, () => { slot.Clear(); inventory.NotifyChanged(); }, result, visited,
                    isCrew ? null : boat.BoatInstanceId);
            }
            var equipment = inventory.Equipment;
            if (equipment == null) continue;
            foreach (var type in EquipmentSlots)
                Add(equipment.Get(type), () => { equipment.Remove(type); inventory.NotifyChanged(); }, result, visited,
                    isCrew ? null : boat.BoatInstanceId);
        }
        // Offline crew loadouts remain crew property. Live actors supersede stale saved mirrors.
        var gs = GameState.I;
        if (gs?.playerPersistenceStates != null)
            foreach (var player in gs.playerPersistenceStates)
            {
                if (player == null || liveKeys.Contains(player.playerKey)) continue;
                AddSavedLoadout(player.loadout, result, boat.BoatInstanceId,
                    player.sceneContext?.boatInstanceId == boat.BoatInstanceId ||
                    (player.playerKey == gs.LocalPlayerPersistenceKey && gs.boat?.boatInstanceId == boat.BoatInstanceId));
            }
        if (gs != null && !liveKeys.Contains(gs.LocalPlayerPersistenceKey))
            AddSavedLoadout(gs.playerLoadout, result, boat.BoatInstanceId,
                gs.playerSceneContext?.boatInstanceId == boat.BoatInstanceId || gs.boat?.boatInstanceId == boat.BoatInstanceId);
        return result;
    }

    private static void AddSavedLoadout(PlayerLoadoutSnapshot loadout, List<Source> result, string boatId, bool includeUnbound)
    {
        if (loadout == null) return;
        if (loadout.inventory?.hotbarSlots != null)
            foreach (var item in loadout.inventory.hotbarSlots) AddSavedItem(item, result, boatId, includeUnbound);
        var eq = loadout.equipment;
        if (eq == null) return;
        AddSavedItem(eq.hands, result, boatId, includeUnbound); AddSavedItem(eq.backpack, result, boatId, includeUnbound);
        AddSavedItem(eq.toolbelt, result, boatId, includeUnbound); AddSavedItem(eq.head, result, boatId, includeUnbound);
        AddSavedItem(eq.body, result, boatId, includeUnbound); AddSavedItem(eq.feet, result, boatId, includeUnbound);
    }
    private static void AddSavedItem(ItemInstanceSnapshot item, List<Source> result, string boatId, bool includeUnbound)
    {
        if (item == null || item.quantity <= 0) return;
        if (IsSnapshotInstrument(item))
        {
            bool belongs = item.chartingInstrument?.boatInstanceId == boatId || (includeUnbound && item.chartingInstrument?.HasState != true);
            if (belongs && item.chartingInstrument?.invalidated != true && !result.Exists(s => s.Id == item.instanceId))
                result.Add(new Source { snapshot = item, remove = () => ScrubStoredBoatState(boatId) });
            return;
        }
        if (item.container?.slots != null)
            foreach (var child in item.container.slots) AddSavedItem(child, result, boatId, includeUnbound);
    }

    private static bool IsCrewInventory(PlayerInventory inventory, Boat boat)
    {
        var boarding = inventory.GetComponentInParent<PlayerBoardingState>();
        if (boarding != null && boarding.IsBoarded) return boarding.CurrentBoatRoot == boat.transform;
        var persistence = inventory.GetComponentInParent<PlayerLoadoutPersistence>();
        var gs = GameState.I;
        if (gs == null || persistence == null) return false;
        var context = gs.GetPlayerSceneContext(persistence.PersistenceKey);
        return context?.boatInstanceId == boat.BoatInstanceId ||
            (gs.boat?.boatInstanceId == boat.BoatInstanceId && persistence.PersistenceKey == gs.LocalPlayerPersistenceKey);
    }

    private static void Add(ItemInstance item, Action remove, List<Source> result, HashSet<ItemInstance> visited, string requiredAssociation = null)
    {
        if (item == null || item.IsDepleted() || !visited.Add(item)) return;
        if (IsInstrumentDefinition(item.Definition))
        {
            if (item.ChartingInstrument?.invalidated != true &&
                (requiredAssociation == null || item.ChartingInstrument?.boatInstanceId == requiredAssociation))
                result.Add(new Source { item = item, remove = remove });
            return;
        }
        AddContainer(item.ContainerState, result, visited, requiredAssociation);
    }

    private static void AddContainer(ItemContainerState container, List<Source> result, HashSet<ItemInstance> visited, string requiredAssociation = null)
    {
        if (container == null) return;
        for (int i = 0; i < container.SlotCount; i++)
        {
            var slot = container.GetSlot(i);
            Add(slot.Instance, () => { slot.Clear(); container.NotifyChanged(); }, result, visited, requiredAssociation);
        }
    }

    private static bool ContainsInstrument(ItemInstance item)
    {
        if (item == null) return false;
        if (IsInstrumentDefinition(item.Definition)) return true;
        if (item.ContainerState != null)
            foreach (var slot in item.ContainerState.Slots)
                if (ContainsInstrument(slot?.Instance)) return true;
        return false;
    }

    // Remove stale snapshots as well as physical instances, so an immediate reload/scene restore
    // cannot revive the replaced device. Normal later capture writes the new replacement.
    private static void ScrubStoredBoatState(string boatId)
    {
        var gs = GameState.I;
        if (gs == null) return;
        if (gs.boat?.boatInstanceId == boatId)
        {
            gs.boat.looseItems?.looseItems?.RemoveAll(s => s != null && IsSnapshotInstrument(s.item));
            if (gs.boat.looseItems?.looseItems != null)
                foreach (var loose in gs.boat.looseItems.looseItems) ScrubItem(loose.item);
            if (gs.boat.moduleStates?.modules != null)
                foreach (var module in gs.boat.moduleStates.modules) ScrubContainer(module.storageContainer);
        }
        ScrubLoadout(gs.playerLoadout, boatId, gs.playerSceneContext?.boatInstanceId == boatId || gs.boat?.boatInstanceId == boatId);
        if (gs.playerPersistenceStates != null)
            foreach (var player in gs.playerPersistenceStates)
                if (player != null) ScrubLoadout(player.loadout, boatId, player.sceneContext?.boatInstanceId == boatId);
    }
    private static bool IsSnapshotInstrument(ItemInstanceSnapshot item) => item != null &&
        (item.itemId == "item_charting_object" || item.chartingInstrument?.HasState == true);
    private static ItemInstanceSnapshot ScrubItem(ItemInstanceSnapshot item)
    {
        if (IsSnapshotInstrument(item)) return null;
        if (item != null) ScrubContainer(item.container);
        return item;
    }
    private static void ScrubContainer(ItemContainerSnapshot container)
    {
        if (container?.slots == null) return;
        for (int i = 0; i < container.slots.Count; i++) container.slots[i] = ScrubItem(container.slots[i]);
    }
    private static void ScrubLoadout(PlayerLoadoutSnapshot loadout, string boatId, bool includeUnbound)
    {
        if (loadout == null) return;
        // Only associated devices or the legacy configured device belong to this crew pass.
        if (loadout.inventory?.hotbarSlots != null)
            for (int i = 0; i < loadout.inventory.hotbarSlots.Count; i++)
                loadout.inventory.hotbarSlots[i] = ScrubCrewItem(loadout.inventory.hotbarSlots[i], boatId, includeUnbound);
        var eq = loadout.equipment;
        if (eq == null) return;
        eq.hands = ScrubCrewItem(eq.hands, boatId, includeUnbound); eq.backpack = ScrubCrewItem(eq.backpack, boatId, includeUnbound);
        eq.toolbelt = ScrubCrewItem(eq.toolbelt, boatId, includeUnbound); eq.head = ScrubCrewItem(eq.head, boatId, includeUnbound);
        eq.body = ScrubCrewItem(eq.body, boatId, includeUnbound); eq.feet = ScrubCrewItem(eq.feet, boatId, includeUnbound);
    }
    private static ItemInstanceSnapshot ScrubCrewItem(ItemInstanceSnapshot item, string boatId, bool includeUnbound)
    {
        if (item == null) return null;
        if (IsSnapshotInstrument(item) && ((includeUnbound && item.chartingInstrument?.HasState != true) ||
            item.chartingInstrument?.boatInstanceId == boatId)) return null;
        if (item.container?.slots != null)
            for (int i = 0; i < item.container.slots.Count; i++) item.container.slots[i] = ScrubCrewItem(item.container.slots[i], boatId, includeUnbound);
        return item;
    }
}
