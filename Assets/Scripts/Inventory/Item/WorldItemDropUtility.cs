using UnityEngine;

public static class WorldItemDropUtility
{
    public static bool TryDrop(
        ItemInstance instance,
        Vector3 worldPosition,
        GameObject actor,
        out WorldItem dropped)
    {
        dropped = null;

        if (instance == null ||
            instance.Definition == null)
        {
            return false;
        }

        if (!instance.Definition.Droppable ||
            instance.Definition.WorldPrefab == null)
        {
            return false;
        }

        dropped =
            Object.Instantiate(
                instance.Definition.WorldPrefab,
                worldPosition,
                Quaternion.identity);

        dropped.Initialize(
            instance);

        ApplyBoatOwnership(
            dropped,
            actor);

        InheritReleaseVelocity(dropped, actor);

        return true;
    }

    public static void InheritReleaseVelocity(WorldItem item, GameObject actor)
    {
        if (item == null || actor == null) return;
        var carrier = ResolveReleaseCarrier(actor);
        if (carrier == null || !carrier.simulated) return;
        // Set once, never add: initialization/policy callbacks must not double carry.
        foreach (var body in item.GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (body == carrier || body.bodyType != RigidbodyType2D.Dynamic) continue;
            body.linearVelocity = carrier.GetPointVelocity(body.worldCenterOfMass);
            body.angularVelocity = carrier.angularVelocity;
        }
    }

    public static Vector2 GetReleaseVelocity(GameObject actor, Vector2 position, out float angular)
    {
        angular = 0f;
        if (actor == null) return Vector2.zero;
        var carrier = ResolveReleaseCarrier(actor);
        if (carrier == null || !carrier.simulated) return Vector2.zero;
        angular = carrier.angularVelocity;
        return carrier.GetPointVelocity(position);
    }

    private static Rigidbody2D ResolveReleaseCarrier(GameObject actor)
    {
        Rigidbody2D carrier = null;
        var bellState = actor.GetComponentInParent<PlayerBellOccupantState>()
            ?? actor.GetComponentInChildren<PlayerBellOccupantState>(true);
        if (bellState != null && bellState.IsInsideBell && bellState.CurrentBell != null)
            carrier = bellState.CurrentBell.GetComponent<Rigidbody2D>();
        TryFindBoardedBoat(actor, out var boat);
        if (carrier == null && boat != null) carrier = boat.rb;
        if (carrier == null) carrier = actor.GetComponentInParent<Rigidbody2D>();
        return carrier;
    }

    public static void ApplyBoatOwnership(
        WorldItem dropped,
        GameObject actor)
    {
        if (dropped == null)
            return;

        // IMPORTANT ORDER:
        // Cache/create the ordinary item policies BEFORE adding bell containment.
        //
        // BoatOwnedItemVisualPolicy snapshots the prefab's normal world
        // presentation when it is created. If bell presentation happens first,
        // "restore original" can accidentally restore BellItem state later.
        EnsureOrdinaryItemPolicies(
            dropped,
            out BoatOwnedItem owned,
            out _,
            out _);

        DivingBellContainedItem bellContained =
            PrepareDivingBellContainment(
                dropped,
                actor);

        // Ownership and physical-container context are separate.
        //
        // A docked bell item can legitimately be:
        //     BoatOwnedItem -> TestBoat
        //     DivingBellContainedItem -> DivingBell_01
        if (TryFindBoardedBoat(
                actor,
                out Boat boat))
        {
            owned.AssignToBoat(
                boat);
        }
        else
        {
            owned.ClearOwnership();
        }

        // Ownership events may have fired ordinary policies.
        // Bell containment is the more-specific physical/presentation context.
        bellContained?.ReapplyBellContext();
    }

    private static void EnsureOrdinaryItemPolicies(
        WorldItem dropped,
        out BoatOwnedItem owned,
        out BoatOwnedItemLayerPolicy layerPolicy,
        out BoatOwnedItemVisualPolicy visualPolicy)
    {
        owned =
            dropped.GetComponent<BoatOwnedItem>();

        if (owned == null)
        {
            owned =
                dropped.gameObject.AddComponent<BoatOwnedItem>();
        }

        layerPolicy =
            dropped.GetComponent<BoatOwnedItemLayerPolicy>();

        if (layerPolicy == null)
        {
            layerPolicy =
                dropped.gameObject.AddComponent<BoatOwnedItemLayerPolicy>();
        }

        visualPolicy =
            dropped.GetComponent<BoatOwnedItemVisualPolicy>();

        if (visualPolicy == null)
        {
            visualPolicy =
                dropped.gameObject.AddComponent<BoatOwnedItemVisualPolicy>();
        }
    }

    private static DivingBellContainedItem PrepareDivingBellContainment(
        WorldItem dropped,
        GameObject actor)
    {
        if (dropped == null ||
            !TryFindOccupiedBell(
                actor,
                out DivingBellOccupancy bell))
        {
            return null;
        }

        DivingBellContainedItem contained =
            dropped.GetComponent<DivingBellContainedItem>();

        if (contained == null)
        {
            contained =
                dropped.gameObject.AddComponent<DivingBellContainedItem>();
        }

        contained.AssignToBell(
            bell);

        return contained;
    }

    private static bool TryFindOccupiedBell(
        GameObject actor,
        out DivingBellOccupancy bell)
    {
        bell =
            null;

        if (actor == null)
            return false;

        PlayerBellOccupantState state =
            actor.GetComponentInParent<PlayerBellOccupantState>() ??
            actor.GetComponentInChildren<PlayerBellOccupantState>(
                true);

        if (state == null ||
            !state.IsInsideBell ||
            state.CurrentBell == null)
        {
            return false;
        }

        bell =
            state.CurrentBell;

        return true;
    }

    private static bool TryFindBoardedBoat(
        GameObject actor,
        out Boat boat)
    {
        boat =
            null;

        if (actor == null)
            return false;

        PlayerBoardingState boarding =
            actor.GetComponentInParent<PlayerBoardingState>() ??
            actor.GetComponentInChildren<PlayerBoardingState>(
                true);

        if (boarding == null ||
            !boarding.IsBoarded ||
            boarding.CurrentBoatRoot == null)
        {
            return false;
        }

        return
            boarding.CurrentBoatRoot.TryGetComponent(
                out boat) &&
            boat != null;
    }
}
