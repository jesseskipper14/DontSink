using System.Collections;
using UnityEngine;
using WorldMap.Player.Trade; // for IItemStore

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
        float t = 0f;

        // Wait for GameState to exist
        while (GameState.I == null && t < maxWaitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (GameState.I == null || GameState.I.player == null)
        {
            Debug.LogWarning("[PlayerDebugSeed] GameState not ready; skipping seed.");
            yield break;
        }

        var state = GameState.I.player;

        // Try to find an IItemStore in scene (physical crate store in boat scene)
        IItemStore store = null;
        while (store == null && t < maxWaitSeconds)
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var b in behaviours)
            {
                if (b is IItemStore s)
                {
                    store = s;
                    break;
                }
            }

            if (store != null) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (seedOnlyIfUninitialized)
        {
            bool hasCredits = state.credits != 0;

            bool hasItems = false;
            if (store != null)
            {
                int nCheck = Mathf.Min(itemIds?.Length ?? 0, amounts?.Length ?? 0);
                for (int i = 0; i < nCheck; i++)
                {
                    var id = itemIds[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (store.GetCount(id) > 0)
                    {
                        hasItems = true;
                        break;
                    }
                }
            }
            else if (state.inventory != null)
            {
                hasItems = true;
            }

            if (hasCredits || hasItems)
                yield break;
        }

        // Seed credits
        state.credits = startingCredits;

        int n = Mathf.Min(itemIds?.Length ?? 0, amounts?.Length ?? 0);

        if (store != null)
        {
            // Seed through physical or inventory-backed store
            for (int i = 0; i < n; i++)
            {
                var id = itemIds[i];
                var amt = amounts[i];
                if (string.IsNullOrWhiteSpace(id) || amt <= 0) continue;
                store.Add(id, amt);
            }

            Debug.Log("[PlayerDebugSeed] Seeded credits + items via IItemStore.");
        }
        else
        {
            // Fallback to legacy inventory
            state.inventory ??= new InventoryState();

            for (int i = 0; i < n; i++)
            {
                var id = itemIds[i];
                var amt = amounts[i];
                if (string.IsNullOrWhiteSpace(id) || amt <= 0) continue;
                state.inventory.Add(id, amt);
            }

            Debug.Log("[PlayerDebugSeed] Seeded credits + inventory fallback.");
        }
    }
}