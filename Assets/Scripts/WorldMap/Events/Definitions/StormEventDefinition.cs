using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Events/Storm Event")]
public class StormEventDefinition : WorldMapEventDefinition, IWorldMapEventResolver
{
    [Header("Storm Timing")]
    [Min(0.1f)] public float durationHours = 24f;

    [Header("Outcomes (3-way)")]
    public EventOutcome outcomeWeathersStorm;   // +stability etc
    public EventOutcome outcomeDamaged;         // -dock, -prosperity etc
    public EventOutcome outcomeCatastrophic;    // worse version, or weird wildcard

    [Header("Weights (relative)")]
    [Min(0f)] public float weightWeathersStorm = 0.6f;
    [Min(0f)] public float weightDamaged = 0.3f;
    [Min(0f)] public float weightCatastrophic = 0.1f;

    [Header("Optional: bias by node stats")]
    public bool biasByStability = true;
    [Tooltip("How much stability (0..4) shifts the odds toward 'Weathers Storm'.")]
    [Min(0f)] public float stabilityBiasStrength = 0.15f;

    public override WorldMapEventInstance CreateInstance(int sourceNodeId, int seed)
    {
        return new WorldMapEventInstance
        {
            def = this,
            sourceNodeId = sourceNodeId,
            seed = seed,

            elapsedHours = 0f,
            durationHours = durationHours,
            isResolved = false,

            stateJson = "" // unused for now
        };
    }

    public bool TryResolve(ref WorldMapEventInstance ev, WorldMapGraphGenerator generator, out WorldMapEventResolved resolved)
    {
        resolved = default;

        if (ev.elapsedHours < ev.durationHours)
            return false;

        // Choose outcome
        var outcome = RollOutcome(ev, generator);

        resolved = new WorldMapEventResolved(
            ev.sourceNodeId,
            outcome,
            eventId,
            displayName
        );

        return true;
    }

    public bool TryPlayerComplete(ref WorldMapEventInstance ev, WorldMapGraphGenerator generator, out WorldMapEventResolved resolved)
    {
        // Storm doesn’t have a player-complete path (yet).
        resolved = default;
        return false;
    }

    private EventOutcome RollOutcome(WorldMapEventInstance ev, WorldMapGraphGenerator generator)
    {
        // Base weights
        float wA = weightWeathersStorm;
        float wB = weightDamaged;
        float wC = weightCatastrophic;

        // Optional bias: higher Stability slightly increases chance of "Weathers"
        if (biasByStability && generator != null && generator.graph != null)
        {
            if (TryGetStat(generator, ev.sourceNodeId, NodeStatId.Stability, out float stability))
            {
                // stability ~ 0..4 -> bias factor 0..(strength*4)
                float bias = stability * stabilityBiasStrength;
                wA += bias;
                // keep total-ish stable by subtracting from the negative outcomes
                float take = bias * 0.5f;
                wB = Mathf.Max(0f, wB - take);
                wC = Mathf.Max(0f, wC - take);
            }
        }

        // Safety: if everything is 0, default to A
        float sum = wA + wB + wC;
        if (sum <= 0.0001f)
            return outcomeWeathersStorm;

        // Deterministic per event instance (seed + node + duration)
        int rollSeed = ev.seed ^ (ev.sourceNodeId * 73856093) ^ 0x5bd1e995;
        var rng = new System.Random(rollSeed);

        double r = rng.NextDouble() * sum;

        if (r < wA) return outcomeWeathersStorm != null ? outcomeWeathersStorm : outcomeDamaged;
        r -= wA;

        if (r < wB) return outcomeDamaged != null ? outcomeDamaged : outcomeWeathersStorm;

        return outcomeCatastrophic != null ? outcomeCatastrophic : (outcomeDamaged != null ? outcomeDamaged : outcomeWeathersStorm);
    }

    private static bool TryGetStat(WorldMapGraphGenerator generator, int nodeId, NodeStatId id, out float value)
    {
        value = 0f;
        if (generator.graph == null) return false;
        if (nodeId < 0 || nodeId >= generator.graph.nodes.Count) return false;

        var node = generator.graph.nodes[nodeId];
        if (node.stats == null) return false;

        for (int i = 0; i < node.stats.Count; i++)
        {
            if (node.stats[i].id == id)
            {
                value = node.stats[i].stat.value;
                return true;
            }
        }

        return false;
    }
}
