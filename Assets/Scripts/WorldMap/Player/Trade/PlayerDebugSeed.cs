using System.Collections;
using UnityEngine;

public sealed class PlayerDebugSeed : MonoBehaviour
{
    [Header("Seed Once")]
    public bool seedOnStart = true;
    public bool seedOnlyIfUninitialized = true;

    [Header("Starting Credits")]
    public int startingCredits = 100;

    [Header("Starting Items")]
    public string[] itemIds = { "fish", "scrap" };
    public int[] amounts = { 10, 5 };

    [Header("Init Wait")]
    [Min(0f)] public float maxWaitSeconds = 1.0f;

    private void Start()
    {
        if (!seedOnStart) return;
        StartCoroutine(SeedWhenReady());
    }

    private IEnumerator SeedWhenReady()
    {
        // Wait for GameState to exist (scene transitions can race Awake/Start order)
        float t = 0f;
        while (GameState.I == null && t < maxWaitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (GameState.I == null)
        {
            Debug.LogWarning("[PlayerDebugSeed] GameState not ready; skipping seed this scene.");
            yield break;
        }

        var state = GameState.I.player;
        if (state == null)
        {
            Debug.LogWarning("[PlayerDebugSeed] GameState.player is null; skipping seed this scene.");
            yield break;
        }

        if (seedOnlyIfUninitialized && (state.credits != 0 || state.inventory != null))
            yield break;

        state.credits = startingCredits;

        state.inventory ??= new InventoryState();

        int n = Mathf.Min(itemIds?.Length ?? 0, amounts?.Length ?? 0);
        for (int i = 0; i < n; i++)
        {
            var id = itemIds[i];
            var amt = amounts[i];
            if (string.IsNullOrWhiteSpace(id) || amt <= 0) continue;
            state.inventory.Add(id, amt);
        }

        Debug.Log("[PlayerDebugSeed] Seeded player credits + inventory.");
    }
}