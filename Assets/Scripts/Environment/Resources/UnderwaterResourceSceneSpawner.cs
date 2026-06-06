using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UnderwaterResourceSceneSpawner : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private UnderwaterResourceCatalog resourceCatalog;

    [Header("Ground Alignment")]
    [SerializeField] private GeneratedGroundSampler2D groundSampler;

    [Tooltip("Optional component implementing IGroundGeneratedNotifier, such as BoatSeaFloorGenerator2D or NodeGroundGenerator2D.")]
    [SerializeField] private MonoBehaviour groundGeneratedNotifierSource;

    [SerializeField] private bool respawnWhenGroundRegenerates = true;

    [Tooltip("Extra padding inside generated ground span so resources do not spawn on boundary walls / exact ends.")]
    [Min(0f)]
    [SerializeField] private float spawnXPadding = 2f;

    [Min(1)]
    [SerializeField] private int maxPlacementAttempts = 60;

    [Header("Water / Depth")]
    [Tooltip("World Y position of the water surface. Later this can come from water manager if needed.")]
    [SerializeField] private float waterSurfaceY = 0f;

    [Tooltip("Scene-level minimum depth below water surface.")]
    [Min(0f)]
    [SerializeField] private float sceneMinDepth = 0.5f;

    [Tooltip("Scene-level maximum depth below water surface.")]
    [Min(0f)]
    [SerializeField] private float sceneMaxDepth = 20f;

    [Header("Dynamic Seed")]
    [SerializeField] private int worldSeed = 12345;

    [SerializeField] private string routeStableId = "debug_route";

    [Tooltip("If true, every scene load gets a fresh salt. Later replace this with route visit count / host-generated manifest seed.")]
    [SerializeField] private bool randomizeVisitSaltOnAwake = true;

    [SerializeField] private int sceneVisitSalt = 0;

    [Header("Baseline Budgets")]
    [Min(0)]
    [SerializeField] private int collectableBudget = 16;

    [Min(0)]
    [SerializeField] private int extractableBudget = 5;

    [Min(0)]
    [SerializeField] private int craneableBudget = 1;

    [Header("Active Modifiers")]
    [Tooltip("For now, manually add POI-like modifiers here. Later, BoatScene context should populate these.")]
    [SerializeField] private List<UnderwaterResourceSpawnModifier> activeModifiers = new();

    [Header("Output")]
    [SerializeField] private Transform spawnedRoot;

    [SerializeField] private bool spawnOnStart = true;

    private readonly List<GameObject> spawnedObjects = new();
    private readonly List<UnderwaterResourceDefinition> categoryScratch = new();

    private IGroundGeneratedNotifier groundNotifier;

    private void Awake()
    {
        if (randomizeVisitSaltOnAwake)
            sceneVisitSalt = Random.Range(int.MinValue, int.MaxValue);

        ResolveGroundRefs();
    }

    private void OnEnable()
    {
        ResolveGroundRefs();
        SubscribeToGroundNotifier();
    }

    private void OnDisable()
    {
        UnsubscribeFromGroundNotifier();
    }

    private void Start()
    {
        if (spawnOnStart)
            Respawn();
    }

    private void ResolveGroundRefs()
    {
        if (groundSampler == null)
            groundSampler = FindAnyObjectByType<GeneratedGroundSampler2D>();

        if (groundGeneratedNotifierSource == null)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IGroundGeneratedNotifier)
                {
                    groundGeneratedNotifierSource = behaviours[i];
                    break;
                }
            }
        }

        groundNotifier = groundGeneratedNotifierSource as IGroundGeneratedNotifier;
    }

    private void SubscribeToGroundNotifier()
    {
        if (!respawnWhenGroundRegenerates)
            return;

        if (groundNotifier == null)
            return;

        groundNotifier.OnGenerated -= HandleGroundGenerated;
        groundNotifier.OnGenerated += HandleGroundGenerated;
    }

    private void UnsubscribeFromGroundNotifier()
    {
        if (groundNotifier == null)
            return;

        groundNotifier.OnGenerated -= HandleGroundGenerated;
    }

    private void HandleGroundGenerated()
    {
        if (!respawnWhenGroundRegenerates)
            return;

        if (groundSampler != null)
            groundSampler.Refresh();

        Respawn();
    }

    [ContextMenu("Respawn Underwater Resources")]
    public void Respawn()
    {
        ClearSpawned();

        if (resourceCatalog == null)
        {
            Debug.LogWarning("[UnderwaterResourceSceneSpawner] Missing resource catalog.", this);
            return;
        }

        if (groundSampler == null)
        {
            ResolveGroundRefs();
        }

        if (groundSampler == null)
        {
            Debug.LogWarning("[UnderwaterResourceSceneSpawner] Missing GeneratedGroundSampler2D.", this);
            return;
        }

        groundSampler.Refresh();

        if (!groundSampler.TryGetWorldSpan(out float minX, out float maxX))
        {
            // Not warning loudly here because Start order can briefly beat ground generation.
            return;
        }

        if (maxX - minX <= spawnXPadding * 2f)
            return;

        if (spawnedRoot == null)
            spawnedRoot = transform;

        System.Random rng = new(BuildSeed());

        SpawnCategory(UnderwaterResourceCategory.Collectable, GetBudget(UnderwaterResourceCategory.Collectable), rng);
        SpawnCategory(UnderwaterResourceCategory.Extractable, GetBudget(UnderwaterResourceCategory.Extractable), rng);
        SpawnCategory(UnderwaterResourceCategory.Craneable, GetBudget(UnderwaterResourceCategory.Craneable), rng);

        Debug.Log(
            $"[UnderwaterResourceSceneSpawner] Spawned {spawnedObjects.Count} underwater resources. " +
            $"Route={routeStableId}, VisitSalt={sceneVisitSalt}",
            this);
    }

    [ContextMenu("Clear Underwater Resources")]
    public void ClearSpawned()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = spawnedObjects[i];
            if (obj == null)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
                Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        spawnedObjects.Clear();
    }

    private void SpawnCategory(
        UnderwaterResourceCategory category,
        int budget,
        System.Random rng)
    {
        for (int i = 0; i < budget; i++)
        {
            if (!TryPickDefinition(category, rng, out UnderwaterResourceDefinition definition))
                continue;

            if (!TryPickSpawnPosition(definition, rng, out Vector2 position, out float depth))
                continue;

            string instanceId =
                $"{routeStableId}_{sceneVisitSalt}_{category}_{definition.stableId}_{i}";

            float quality01 = (float)rng.NextDouble();

            UnderwaterResourceRuntimeInstance instance =
                UnderwaterResourceRuntimeInstance.Create(
                    instanceId,
                    definition,
                    position,
                    depth,
                    quality01);

            SpawnResourceObject(definition, instance, position);
        }
    }

    private bool TryPickDefinition(
        UnderwaterResourceCategory category,
        System.Random rng,
        out UnderwaterResourceDefinition picked)
    {
        picked = null;

        if (resourceCatalog == null)
            return false;

        resourceCatalog.GetEnabledByCategory(category, categoryScratch);

        float totalWeight = 0f;

        for (int i = 0; i < categoryScratch.Count; i++)
        {
            UnderwaterResourceDefinition definition = categoryScratch[i];

            if (!IsEligible(definition, category))
                continue;

            totalWeight += GetFinalWeight(definition);
        }

        if (totalWeight <= 0f)
            return false;

        float roll = (float)rng.NextDouble() * totalWeight;
        float running = 0f;

        for (int i = 0; i < categoryScratch.Count; i++)
        {
            UnderwaterResourceDefinition definition = categoryScratch[i];

            if (!IsEligible(definition, category))
                continue;

            running += GetFinalWeight(definition);

            if (roll <= running)
            {
                picked = definition;
                return true;
            }
        }

        return false;
    }

    private bool IsEligible(
        UnderwaterResourceDefinition definition,
        UnderwaterResourceCategory category)
    {
        if (definition == null)
            return false;

        if (definition.category != category)
            return false;

        if (definition.baselineWeight <= 0f)
            return false;

        float minDepth = Mathf.Max(sceneMinDepth, definition.minDepth);
        float maxDepth = Mathf.Min(sceneMaxDepth, definition.maxDepth);

        return maxDepth >= minDepth;
    }

    private float GetFinalWeight(UnderwaterResourceDefinition definition)
    {
        if (definition == null)
            return 0f;

        float weight = Mathf.Max(0f, definition.baselineWeight);

        for (int i = 0; i < activeModifiers.Count; i++)
        {
            UnderwaterResourceSpawnModifier modifier = activeModifiers[i];
            if (modifier == null)
                continue;

            weight *= modifier.GetWeightMultiplier(definition);
        }

        return Mathf.Max(0f, weight);
    }

    private int GetBudget(UnderwaterResourceCategory category)
    {
        int budget = category switch
        {
            UnderwaterResourceCategory.Collectable => collectableBudget,
            UnderwaterResourceCategory.Extractable => extractableBudget,
            UnderwaterResourceCategory.Craneable => craneableBudget,
            _ => 0
        };

        for (int i = 0; i < activeModifiers.Count; i++)
        {
            UnderwaterResourceSpawnModifier modifier = activeModifiers[i];
            if (modifier == null)
                continue;

            budget += modifier.GetExtraBudget(category);
        }

        return Mathf.Max(0, budget);
    }

    private bool TryPickSpawnPosition(
        UnderwaterResourceDefinition definition,
        System.Random rng,
        out Vector2 position,
        out float depth)
    {
        position = default;
        depth = 0f;

        if (definition == null || groundSampler == null)
            return false;

        groundSampler.Refresh();

        if (!groundSampler.TryGetWorldSpan(out float minX, out float maxX))
            return false;

        minX += spawnXPadding;
        maxX -= spawnXPadding;

        if (maxX <= minX)
            return false;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());

            if (!groundSampler.TrySampleGround(x, out float groundY, out float slopeDegrees))
                continue;

            if (slopeDegrees > definition.maxGroundSlopeDegrees)
                continue;

            float y = groundY + definition.groundClearance;

            if (definition.verticalJitter > 0f)
            {
                float jitter = Mathf.Lerp(
                    -definition.verticalJitter,
                    definition.verticalJitter,
                    (float)rng.NextDouble());

                y += jitter;
            }

            depth = waterSurfaceY - y;

            float minDepth = Mathf.Max(sceneMinDepth, definition.minDepth);
            float maxDepth = Mathf.Min(sceneMaxDepth, definition.maxDepth);

            if (depth < minDepth || depth > maxDepth)
                continue;

            position = new Vector2(x, y);
            return true;
        }

        return false;
    }

    private void SpawnResourceObject(
        UnderwaterResourceDefinition definition,
        UnderwaterResourceRuntimeInstance instance,
        Vector2 position)
    {
        GameObject obj;

        if (definition.scenePrefab != null)
        {
            obj = Instantiate(definition.scenePrefab, position, Quaternion.identity, spawnedRoot);
        }
        else
        {
            obj = new GameObject($"UnderwaterResource_{definition.displayName}");
            obj.transform.SetParent(spawnedRoot);
            obj.transform.position = position;

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = definition.fallbackSprite;
        }

        Collider2D collider = obj.GetComponent<Collider2D>();
        if (collider == null)
        {
            CircleCollider2D circle = obj.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;
            collider = circle;
        }

        collider.isTrigger = true;

        UnderwaterResourceInteractable interactable =
            obj.GetComponent<UnderwaterResourceInteractable>();

        if (interactable == null)
            interactable = obj.AddComponent<UnderwaterResourceInteractable>();

        interactable.Initialize(definition, instance);

        UnderwaterResourceFrozenBody2D frozenBody =
            obj.GetComponent<UnderwaterResourceFrozenBody2D>();

        if (frozenBody == null)
            frozenBody = obj.AddComponent<UnderwaterResourceFrozenBody2D>();

        frozenBody.FreezeNow();

        spawnedObjects.Add(obj);
    }

    private int BuildSeed()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + worldSeed;
            hash = hash * 31 + StableHash(routeStableId);
            hash = hash * 31 + sceneVisitSalt;
            return hash;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;

            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private void OnValidate()
    {
        if (sceneMaxDepth < sceneMinDepth)
            sceneMaxDepth = sceneMinDepth;

        spawnXPadding = Mathf.Max(0f, spawnXPadding);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
    }
}