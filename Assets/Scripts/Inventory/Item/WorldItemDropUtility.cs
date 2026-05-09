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

        if (instance == null || instance.Definition == null)
            return false;

        if (!instance.Definition.Droppable || instance.Definition.WorldPrefab == null)
            return false;

        dropped = Object.Instantiate(
            instance.Definition.WorldPrefab,
            worldPosition,
            Quaternion.identity);

        dropped.Initialize(instance);

        ApplyBoatOwnership(dropped, actor);

        return true;
    }

    public static void ApplyBoatOwnership(WorldItem dropped, GameObject actor)
    {
        if (dropped == null)
            return;

        BoatOwnedItem owned = dropped.GetComponent<BoatOwnedItem>();
        if (owned == null)
            owned = dropped.gameObject.AddComponent<BoatOwnedItem>();

        BoatOwnedItemLayerPolicy layerPolicy = dropped.GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy == null)
            layerPolicy = dropped.gameObject.AddComponent<BoatOwnedItemLayerPolicy>();

        BoatOwnedItemVisualPolicy visualPolicy = dropped.GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy == null)
            visualPolicy = dropped.gameObject.AddComponent<BoatOwnedItemVisualPolicy>();

        if (TryFindBoardedBoat(actor, out Boat boat))
        {
            owned.AssignToBoat(boat);
            return;
        }

        owned.ClearOwnership();
    }

    private static bool TryFindBoardedBoat(GameObject actor, out Boat boat)
    {
        boat = null;

        if (actor == null)
            return false;

        PlayerBoardingState boarding =
            actor.GetComponentInParent<PlayerBoardingState>() ??
            actor.GetComponentInChildren<PlayerBoardingState>(true);

        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        return boarding.CurrentBoatRoot.TryGetComponent(out boat) && boat != null;
    }
}