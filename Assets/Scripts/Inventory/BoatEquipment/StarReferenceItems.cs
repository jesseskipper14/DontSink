using System;
using UnityEngine;

/// <summary>Owned reference capability and explicit paper-copy transaction. No knowledge writes.</summary>
public static class StarReferenceItems
{
    private static bool copying;
    public static bool HasStars(CelestialChartFragmentSnapshot fragment)
    {
        if (fragment?.version != 1 || fragment.marks == null ||
            !WorldTopology.IsFinite(fragment.recordedInstrumentRotationDegrees) ||
            !WorldTopology.IsFinite(fragment.observationDatumWorldPosition.x) ||
            !WorldTopology.IsFinite(fragment.observationDatumWorldPosition.y)) return false;
        bool any = false;
        foreach (var mark in fragment.marks)
        {
            if (mark == null) continue;
            if (!WorldTopology.IsFinite(mark.celestialWorldPosition.x) ||
                !WorldTopology.IsFinite(mark.celestialWorldPosition.y)) return false;
            any = true;
        }
        return any;
    }

    public static CelestialChartFragmentSnapshot CopyFragment(CelestialChartFragmentSnapshot fragment) =>
        HasStars(fragment) ? JsonUtility.FromJson<CelestialChartFragmentSnapshot>(JsonUtility.ToJson(fragment)) : null;

    public static bool IsEligible(ItemInstance item) => item != null && !item.IsDepleted() &&
        item.HasStarReference;

    public static bool IsOwned(GameObject actor, ItemInstance item) => item != null &&
        CartographicChartIntegration.Collect(actor).Exists(c => ReferenceEquals(c.Item, item) && c.StillOwned);

    public static bool TryCopyRecordedFragment(GameObject actor, string fragmentId, ItemDefinition paper, out string reason)
    {
        reason = "Open the Star Chart and select recorded evidence first.";
        if (!GameplayAuthority.IsAuthoritative || copying || actor == null ||
            CameraManager.ForActor(actor)?.CanProvideGameplayInput != true) return false;
        // Resolve host-owned recorded evidence by ID, never accept client-supplied marks.
        var fragment = GameState.I?.celestialCharts?.fragments?.Find(f => f != null && f.fragmentId == fragmentId);
        if (!HasStars(fragment)) { reason = "This fragment contains no usable star reference."; return false; }
        var inventory = CelestialChartPaperConsumption.ResolveInventory(actor);
        var carrier = Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart");
        if (inventory == null || carrier == null) { reason = "Chart carrier or inventory unavailable."; return false; }
        copying = true;
        CelestialChartPaperConsumption.Receipt receipt = null;
        bool added = false;
        try
        {
            if (!CelestialChartPaperConsumption.TryConsumeOne(actor, paper, out receipt, out reason)) return false;
            var item = ItemInstance.Create(carrier);
            item.SetCartographicChart(new CartographicChartState {
                kind = CartographicChartKind.Reference,
                title = fragment.isStarterPatch ? "Starter sky reference" : $"Survey {fragment.surveySequence} sky reference",
                referenceText = "A carried copy of recorded stars. Raise it while using a telescope to compare by eye.",
                starReference = CopyFragment(fragment)
            });
            try { added = inventory.TryAddInstance(item); }
            catch (Exception error) { added = IsOwned(actor, item); Debug.LogException(error); }
            if (!added) { reason = "Make room for one reference chart."; return false; }
            receipt.Commit();
            reason = "Reference copy placed in your inventory. Raise it with R while observing through a telescope.";
            return true;
        }
        finally { if (!added) receipt?.Rollback(); copying = false; }
    }
}
