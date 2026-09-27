using UnityEngine;
using Survival.Death;

public static class PlayerDebugRespawnService
{
    public static bool TryRespawn(bool keepInventory)
    {
        PlayerDeathSystem deathSystem =
            Object.FindAnyObjectByType<PlayerDeathSystem>(
                FindObjectsInactive.Include);

        if (deathSystem == null)
            return false;

        Transform player = ResolvePlayerTransform(deathSystem);
        if (player == null)
            return false;

        if (!keepInventory)
            ClearPlayerLoadout(player);

        PrepareWorldState(player);

        Vector3 respawnPosition = ResolveRespawnPosition();

        Rigidbody2D body =
            player.GetComponent<Rigidbody2D>() ??
            player.GetComponentInChildren<Rigidbody2D>(true);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        player.position = respawnPosition;
        player.rotation = Quaternion.identity;

        deathSystem.DebugRespawnNow();

        Debug.Log(
            $"[PlayerDebugRespawnService] Respawned player at {respawnPosition}. " +
            $"keepInventory={keepInventory}");

        return true;
    }

    private static Transform ResolvePlayerTransform(
        PlayerDeathSystem deathSystem)
    {
        if (deathSystem == null)
            return null;

        Rigidbody2D body =
            deathSystem.GetComponentInParent<Rigidbody2D>() ??
            deathSystem.GetComponent<Rigidbody2D>() ??
            deathSystem.GetComponentInChildren<Rigidbody2D>(true);

        return body != null
            ? body.transform
            : deathSystem.transform;
    }

    private static Vector3 ResolveRespawnPosition()
    {
        BoatSpawner boatSpawner =
            Object.FindAnyObjectByType<BoatSpawner>(
                FindObjectsInactive.Include);

        if (boatSpawner != null &&
            boatSpawner.TryGetBoatSpawnPoint(out Transform spawnPoint) &&
            spawnPoint != null)
        {
            return spawnPoint.position;
        }

        return Vector3.zero;
    }

    private static void PrepareWorldState(Transform player)
    {
        if (player == null)
            return;

        PlayerBoardingState boarding =
            player.GetComponent<PlayerBoardingState>() ??
            player.GetComponentInChildren<PlayerBoardingState>(true) ??
            player.GetComponentInParent<PlayerBoardingState>();

        if (boarding != null && boarding.IsBoarded)
            boarding.Unboard();

        player.SetParent(null, worldPositionStays: true);
    }

    private static void ClearPlayerLoadout(Transform player)
    {
        if (player == null)
            return;

        PlayerEquipment equipment =
            player.GetComponent<PlayerEquipment>() ??
            player.GetComponentInChildren<PlayerEquipment>(true) ??
            player.GetComponentInParent<PlayerEquipment>();

        PlayerInventory inventory =
            player.GetComponent<PlayerInventory>() ??
            player.GetComponentInChildren<PlayerInventory>(true) ??
            player.GetComponentInParent<PlayerInventory>();

        if (equipment != null)
            equipment.RestoreSnapshot(null, null);

        if (inventory != null)
            inventory.RestoreSnapshot(null, null);
    }
}
