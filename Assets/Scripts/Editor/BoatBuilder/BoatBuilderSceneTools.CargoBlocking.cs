#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static partial class BoatBuilderSceneTools
{
    private const float CargoReservedFootprintOverlapPadding = 0.01f;

    private static bool TryValidatePlacedCargoBlocker(
        GameObject placed,
        Transform boatRoot)
    {
        if (placed == null || boatRoot == null)
            return true;

        BoatSecureZone[] zones = placed.GetComponentsInChildren<BoatSecureZone>(true);

        for (int i = 0; i < zones.Length; i++)
        {
            BoatSecureZone zone = zones[i];

            if (!TryGetSecureZoneBlockingBounds(zone, out Bounds zoneBounds))
                continue;

            if (!TryFindHardpointFootprintOverlap(
                    boatRoot,
                    zoneBounds,
                    placed.transform,
                    out HardpointReservedFootprint conflictFootprint,
                    out _))
            {
                continue;
            }

            Debug.LogWarning(
                $"[BoatBuilder] Cannot place cargo secure zone '{placed.name}'. " +
                $"Its reserved cargo volume overlaps hardpoint reserved module footprint '{conflictFootprint.name}'.",
                conflictFootprint);

            Undo.DestroyObjectImmediate(placed);
            return false;
        }

        return true;
    }

    private static bool TryValidatePlacedHardpointAgainstCargo(
        GameObject placed,
        Transform boatRoot)
    {
        if (placed == null || boatRoot == null)
            return true;

        HardpointReservedFootprint footprint =
            placed.GetComponent<HardpointReservedFootprint>() ??
            placed.GetComponentInChildren<HardpointReservedFootprint>(true);

        if (footprint == null || !footprint.EnabledForBuilderBlocking)
        {
            Debug.LogWarning(
                $"[BoatBuilder] Hardpoint '{placed.name}' has no enabled HardpointReservedFootprint. " +
                "Cargo/module space blocking cannot be enforced for this hardpoint.",
                placed);

            return true;
        }

        Bounds footprintBounds = footprint.GetWorldBounds();

        if (!TryFindCargoZoneOverlap(
                boatRoot,
                footprintBounds,
                placed.transform,
                out BoatSecureZone conflictZone,
                out _))
        {
            return true;
        }

        Debug.LogWarning(
            $"[BoatBuilder] Cannot place hardpoint '{placed.name}'. " +
            $"Its reserved module footprint overlaps cargo reserved volume '{conflictZone.name}'.",
            conflictZone);

        Undo.DestroyObjectImmediate(placed);
        return false;
    }

    private static bool CanInstallModuleWithoutCargoOverlap(
        Hardpoint hardpoint,
        ModuleDefinition module,
        out BoatSecureZone conflictZone)
    {
        conflictZone = null;

        if (hardpoint == null)
            return true;

        HardpointReservedFootprint footprint =
            hardpoint.GetComponent<HardpointReservedFootprint>() ??
            hardpoint.GetComponentInChildren<HardpointReservedFootprint>(true);

        if (footprint == null || !footprint.EnabledForBuilderBlocking)
            return true;

        Transform boatRoot = hardpoint.GetComponentInParent<Boat>() != null
            ? hardpoint.GetComponentInParent<Boat>().transform
            : hardpoint.transform.root;

        return !TryFindCargoZoneOverlap(
            boatRoot,
            footprint.GetWorldBounds(),
            hardpoint.transform,
            out conflictZone,
            out _);
    }

    private static bool TryFindHardpointFootprintOverlap(
        Transform boatRoot,
        Bounds cargoZoneBounds,
        Transform ignoreRoot,
        out HardpointReservedFootprint conflictFootprint,
        out Bounds conflictBounds)
    {
        conflictFootprint = null;
        conflictBounds = default;

        if (boatRoot == null)
            return false;

        HardpointReservedFootprint[] footprints =
            boatRoot.GetComponentsInChildren<HardpointReservedFootprint>(true);

        for (int i = 0; i < footprints.Length; i++)
        {
            HardpointReservedFootprint footprint = footprints[i];

            if (footprint == null || !footprint.EnabledForBuilderBlocking)
                continue;

            if (ignoreRoot != null && footprint.transform.IsChildOf(ignoreRoot))
                continue;

            Bounds bounds = footprint.GetWorldBounds();

            if (!BoundsOverlapXY(cargoZoneBounds, bounds, CargoReservedFootprintOverlapPadding))
                continue;

            conflictFootprint = footprint;
            conflictBounds = bounds;
            return true;
        }

        return false;
    }

    private static bool TryFindCargoZoneOverlap(
        Transform boatRoot,
        Bounds footprintBounds,
        Transform ignoreRoot,
        out BoatSecureZone conflictZone,
        out Bounds conflictBounds)
    {
        conflictZone = null;
        conflictBounds = default;

        if (boatRoot == null)
            return false;

        BoatSecureZone[] zones = boatRoot.GetComponentsInChildren<BoatSecureZone>(true);

        for (int i = 0; i < zones.Length; i++)
        {
            BoatSecureZone zone = zones[i];

            if (zone == null)
                continue;

            if (!zone.AcceptsCargo)
                continue;

            if (ignoreRoot != null && zone.transform.IsChildOf(ignoreRoot))
                continue;

            if (!TryGetSecureZoneBlockingBounds(zone, out Bounds zoneBounds))
                continue;

            if (!BoundsOverlapXY(zoneBounds, footprintBounds, CargoReservedFootprintOverlapPadding))
                continue;

            conflictZone = zone;
            conflictBounds = zoneBounds;
            return true;
        }

        return false;
    }

    private static bool TryGetSecureZoneBlockingBounds(
        BoatSecureZone zone,
        out Bounds bounds)
    {
        bounds = default;

        if (zone == null)
            return false;

        Collider2D collider = zone.GetComponent<Collider2D>();

        if (collider == null || !collider.enabled)
            return false;

        bounds = collider.bounds;
        return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
    }

    private static bool BoundsOverlapXY(Bounds a, Bounds b, float padding)
    {
        padding = Mathf.Max(0f, padding);

        return
            a.max.x > b.min.x + padding &&
            a.min.x < b.max.x - padding &&
            a.max.y > b.min.y + padding &&
            a.min.y < b.max.y - padding;
    }
}
#endif