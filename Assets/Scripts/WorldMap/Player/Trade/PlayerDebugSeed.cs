using UnityEngine;

public sealed class PlayerDebugSeed : MonoBehaviour
{
    public WorldMapPlayer player;

    [Header("Seed Once")]
    public bool seedOnStart = true;
    public bool seedOnlyIfUninitialized = true;

    [Header("Starting Credits")]
    public int startingCredits = 100;

    [Header("Starting Items")]
    public string[] itemIds = { "fish", "scrap" };
    public int[] amounts = { 10, 5 };

    private void Reset()
    {
        player = FindAnyObjectByType<WorldMapPlayer>();
    }

    private void Start()
    {
        if (!seedOnStart) return;

        if (player == null || player.State == null)
        {
            Debug.LogError("[PlayerDebugSeed] Missing WorldMapPlayer/State.");
            return;
        }

        if (seedOnlyIfUninitialized && (player.State.credits != 0 || player.State.inventory == null))
            return;

        player.State.credits = startingCredits;

        player.State.inventory ??= new InventoryState();

        int n = Mathf.Min(itemIds?.Length ?? 0, amounts?.Length ?? 0);
        for (int i = 0; i < n; i++)
        {
            var id = itemIds[i];
            var amt = amounts[i];
            if (string.IsNullOrWhiteSpace(id) || amt <= 0) continue;
            player.State.inventory.Add(id, amt);
        }

        Debug.Log("[PlayerDebugSeed] Seeded player credits + inventory.");
    }
}
