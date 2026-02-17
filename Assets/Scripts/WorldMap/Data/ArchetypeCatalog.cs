using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Archetype Catalog", fileName = "ArchetypeCatalog")]
public sealed class ArchetypeCatalog : ScriptableObject
{
    [Header("Catalog")]
    public List<NodeArchetypeDef> archetypes = new List<NodeArchetypeDef>();
    public List<ClusterAffinityDef> clusterAffinities = new List<ClusterAffinityDef>();

    [Header("Cluster Affinity Policy")]
    [Tooltip("Repeat penalty: effectiveWeight = baseWeight / (1 + repeatCount * repeatPenalty).")]
    [Min(0f)] public float affinityRepeatPenalty = .7f;

    [Tooltip("Optional boost multiplier applied to affinity types not yet used.")]
    [Min(0f)] public float affinityMissingTypeBoost = 1.5f;

    [Tooltip("If true, missingTypeBoost applies only until every affinity has appeared at least once.")]
    public bool boostOnlyUntilCovered = true;

    [Header("Cluster Affinity Uniqueness Bias")]
    [Tooltip("Additional boost for affinities that have not yet appeared (>= 1).")]
    [Min(1f)] public float unseenAffinityBoost = 1.5f;

    [Tooltip("Extra repeat suppression: 1 = none, 0 = ban repeats until forced.")]
    [Range(0f, 1f)] public float repeatMultiplier = 0.5f;

    [Header("Node Archetype Coverage")]
    [Tooltip("These archetypes MUST exist at least once on the map. If empty, no coverage is applied.")]
    public List<NodeArchetypeDef> mustExistAtLeastOnce = new List<NodeArchetypeDef>();

    // =========================
    // Cached plan (per graph+seed)
    // =========================
    private int _cachedSeed = int.MinValue;
    private int _cachedGraphHash = int.MinValue;

    private readonly Dictionary<int, ClusterAffinityDef> _clusterPick = new(); // clusterId -> affinity
    private readonly Dictionary<string, NodeArchetypeDef> _nodePick = new();  // stableNodeId -> archetype

    // =========================
    // Public: called by binder once
    // =========================
    public void BuildPlan(MapGraph graph, int worldSeed)
    {
        if (graph == null || graph.nodes == null) return;
        if (clusterAffinities == null || clusterAffinities.Count == 0) return;

        int graphHash = ComputeGraphHash(graph, worldSeed);
        bool cacheHit = (_cachedSeed == worldSeed && _cachedGraphHash == graphHash);
        bool planValid = (_clusterPick.Count > 0 && _nodePick.Count > 0);

        if (cacheHit && planValid)
            return;

        _cachedSeed = worldSeed;
        _cachedGraphHash = graphHash;

        _clusterPick.Clear();
        _nodePick.Clear();

        // 1) Collect cluster IDs + stable IDs deterministically.
        var clusterIds = new List<int>(16);
        var nodeKeys = new List<NodeKey>(graph.nodes.Count);

        for (int i = 0; i < graph.nodes.Count; i++)
        {
            var n = graph.nodes[i];
            int clusterId = n.clusterId;
            string stableId = $"{worldSeed}:{n.id}";

            nodeKeys.Add(new NodeKey(stableId, clusterId));

            if (!ContainsInt(clusterIds, clusterId))
                clusterIds.Add(clusterId);
        }

        clusterIds.Sort(); // deterministic

        // 2) Assign cluster affinities
        AssignClusterAffinities(clusterIds, worldSeed);

        // 3) Assign node archetypes (coverage list first, then fill)
        AssignNodeArchetypes(nodeKeys, worldSeed);
    }

    // =========================
    // Runtime API used by binder
    // =========================
    public ClusterAffinityDef PickClusterAffinity(int clusterId, int worldSeed)
    {
        if (_cachedSeed == worldSeed && _clusterPick.TryGetValue(clusterId, out var planned))
            return planned;

        return PickClusterAffinityFallback(clusterId, worldSeed);
    }

    public NodeArchetypeDef PickNodeArchetype(string stableNodeId, int clusterId, int worldSeed, ClusterAffinityDef affinity)
    {
        if (_cachedSeed == worldSeed &&
            !string.IsNullOrEmpty(stableNodeId) &&
            _nodePick.TryGetValue(stableNodeId, out var planned) &&
            planned != null)
        {
            return planned;
        }

        return PickNodeArchetypeFallback(stableNodeId, clusterId, worldSeed, affinity);
    }

    // =========================
    // Cluster affinity assignment
    // =========================
    private void AssignClusterAffinities(List<int> clusterIds, int worldSeed)
    {
        var repeatCounts = new int[clusterAffinities.Count];
        int uniqueUsed = 0;

        float repeatPenalty = Mathf.Max(0f, affinityRepeatPenalty);
        float missingBoost = Mathf.Max(0f, affinityMissingTypeBoost);
        float unseenBoost = Mathf.Max(1f, unseenAffinityBoost);
        float rm = Mathf.Clamp01(repeatMultiplier);

        for (int ci = 0; ci < clusterIds.Count; ci++)
        {
            int clusterId = clusterIds[ci];

            int seed = (clusterId * 92821) ^ (worldSeed * 15485863) ^ 0x51ED270B;
            var rng = new System.Random(seed);

            bool allCovered = uniqueUsed >= clusterAffinities.Count;

            float total = 0f;
            var eff = new float[clusterAffinities.Count];

            for (int i = 0; i < clusterAffinities.Count; i++)
            {
                var a = clusterAffinities[i];
                float baseW = (a != null) ? Mathf.Max(0f, a.baseWeight) : 0f;

                int k = repeatCounts[i];
                bool isMissing = (k == 0);

                float w = baseW / (1f + k * repeatPenalty);

                if (k > 0)
                {
                    if (rm <= 0f) w = 0f;               // ban repeats until forced
                    else if (rm < 1f) w *= Mathf.Pow(rm, k);
                }

                bool canMissingBoost =
                    missingBoost > 0f &&
                    isMissing &&
                    (!boostOnlyUntilCovered || !allCovered);

                if (canMissingBoost)
                    w *= missingBoost;

                if (isMissing && (!boostOnlyUntilCovered || !allCovered))
                    w *= unseenBoost;

                eff[i] = w;
                total += w;
            }

            // If repeats were hard-banned and everything went to zero, allow repeats again.
            if (total <= 0f)
            {
                total = 0f;
                for (int i = 0; i < clusterAffinities.Count; i++)
                {
                    var a = clusterAffinities[i];
                    float baseW = (a != null) ? Mathf.Max(0f, a.baseWeight) : 0f;

                    int k = repeatCounts[i];
                    float w = baseW / (1f + k * repeatPenalty);

                    eff[i] = w;
                    total += w;
                }
            }

            int pickedIdx = PickIndex(rng, eff, total);
            _clusterPick[clusterId] = clusterAffinities[pickedIdx];

            if (repeatCounts[pickedIdx] == 0) uniqueUsed++;
            repeatCounts[pickedIdx]++;
        }
    }

    // =========================
    // Node archetype assignment
    // =========================
    private void AssignNodeArchetypes(List<NodeKey> nodes, int worldSeed)
    {
        if (nodes == null || nodes.Count == 0) return;

        // Deterministic order independent of graph list order changes
        nodes.Sort((a, b) => string.CompareOrdinal(a.stableId, b.stableId));

        // (A) Coverage pass: place each required archetype once (if possible)
        if (mustExistAtLeastOnce != null && mustExistAtLeastOnce.Count > 0)
        {
            // Deduplicate + ignore nulls
            var req = new List<NodeArchetypeDef>(mustExistAtLeastOnce.Count);
            for (int i = 0; i < mustExistAtLeastOnce.Count; i++)
            {
                var a = mustExistAtLeastOnce[i];
                if (a == null) continue;
                if (!req.Contains(a)) req.Add(a);
            }

            // Deterministic ordering
            req.Sort((a, b) => string.CompareOrdinal(a.archetypeId, b.archetypeId));

            // Pick nodes deterministically
            var rng = new System.Random(worldSeed ^ 0x2B992DD7);
            var free = new List<int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++) free.Add(i);

            int limit = Mathf.Min(req.Count, free.Count);
            for (int r = 0; r < limit; r++)
            {
                int pickPos = rng.Next(0, free.Count);
                int nodeIdx = free[pickPos];
                free.RemoveAt(pickPos);

                _nodePick[nodes[nodeIdx].stableId] = req[r];
            }
        }

        // (B) Fill pass: assign remaining nodes by affinity distribution (or global fallback)
        for (int i = 0; i < nodes.Count; i++)
        {
            var key = nodes[i];
            if (_nodePick.ContainsKey(key.stableId)) continue;

            _clusterPick.TryGetValue(key.clusterId, out var affinity);
            _nodePick[key.stableId] = PickNodeArchetypeFallback(key.stableId, key.clusterId, worldSeed, affinity);
        }
    }

    // =========================
    // Fallbacks
    // =========================
    private ClusterAffinityDef PickClusterAffinityFallback(int clusterId, int worldSeed)
    {
        if (clusterAffinities == null || clusterAffinities.Count == 0) return null;

        int h = clusterId * 92821 ^ (worldSeed * 15485863);
        int idx = Mod(h, clusterAffinities.Count);
        return clusterAffinities[idx];
    }

    private NodeArchetypeDef PickNodeArchetypeFallback(string stableNodeId, int clusterId, int worldSeed, ClusterAffinityDef affinity)
    {
        if (affinity == null || affinity.archetypeWeights == null || affinity.archetypeWeights.Count == 0)
            return PickArchetypeGlobalFallback(stableNodeId, worldSeed);

        int seed = StableHash(stableNodeId) ^ (clusterId * 73856093) ^ (worldSeed * 486187739);
        var rng = new System.Random(seed);

        var pick = affinity.PickArchetype(rng);
        if (pick != null) return pick;

        return PickArchetypeGlobalFallback(stableNodeId, worldSeed);
    }

    private NodeArchetypeDef PickArchetypeGlobalFallback(string stableNodeId, int worldSeed)
    {
        if (archetypes == null || archetypes.Count == 0) return null;

        int h = StableHash(stableNodeId) ^ (worldSeed * 486187739);
        int idx = Mod(h, archetypes.Count);
        return archetypes[idx];
    }

    // =========================
    // Utilities
    // =========================
    private static int PickIndex(System.Random rng, float[] weights, float total)
    {
        if (weights == null || weights.Length == 0) return 0;

        if (total <= 0f)
        {
            for (int i = 0; i < weights.Length; i++)
                if (weights[i] > 0f) return i;
            return 0;
        }

        float roll = (float)(rng.NextDouble() * total);
        for (int i = 0; i < weights.Length; i++)
        {
            float w = Mathf.Max(0f, weights[i]);
            roll -= w;
            if (roll <= 0f) return i;
        }
        return weights.Length - 1;
    }

    private static bool ContainsInt(List<int> list, int v)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == v) return true;
        return false;
    }

    private static int Mod(int x, int m) => (x % m + m) % m;

    private static int StableHash(string s)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int hash = 23;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];
            return hash;
        }
    }

    private static int ComputeGraphHash(MapGraph graph, int worldSeed)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + worldSeed;
            h = h * 31 + (graph.nodes != null ? graph.nodes.Count : 0);

            if (graph.nodes != null && graph.nodes.Count > 0)
            {
                int step = Mathf.Max(1, graph.nodes.Count / 8);
                for (int i = 0; i < graph.nodes.Count; i += step)
                    h = h * 31 + graph.nodes[i].id;
            }

            return h;
        }
    }

    private readonly struct NodeKey
    {
        public readonly string stableId;
        public readonly int clusterId;

        public NodeKey(string stableId, int clusterId)
        {
            this.stableId = stableId;
            this.clusterId = clusterId;
        }
    }
}
