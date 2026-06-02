using System;
using System.Collections.Generic;
using UnityEngine;

public enum WorldMapGraphGenerationMode
{
    LegacyClusterFirst,
    TopographyFirst
}

public class WorldMapGraphGenerator : MonoBehaviour
{
    [Header("Generation")]
    [Min(1)] public int seed = 12345;

    [Tooltip("Legacy: how many clusters. Topography-first: target number of geographic node groups.")]
    [Range(1, 50)] public int clusterCount = 10;

    [Tooltip("Legacy nodes per cluster. Topography-first uses this only when topographyTargetNodeCount is 0.")]
    [Range(1, 20)] public int nodesPerClusterMin = 4;

    [Tooltip("Legacy nodes per cluster. Topography-first uses this only when topographyTargetNodeCount is 0.")]
    [Range(1, 20)] public int nodesPerClusterMax = 6;

    [Header("Generation Mode")]
    public WorldMapGraphGenerationMode generationMode = WorldMapGraphGenerationMode.LegacyClusterFirst;

    [Tooltip("If topography-first generation fails or has no topography source, fall back to the old cluster-first generator.")]
    public bool fallbackToLegacyIfTopographyMissing = true;

    [Header("Topography Source")]
    [SerializeField] private WorldMapTopographyDebugSource topographySource;

    [Tooltip("If true, calls LoadOrGenerate on the topography source when the field is missing.")]
    public bool loadOrGenerateTopographyIfMissing = true;

    [Header("Node Layer Startup")]
    [Tooltip("Cheap v1 behavior: generate the node graph automatically during Awake so the graph exists before runtime binding/map UI.")]
    public bool generateNodeLayerOnAwake = true;

    [Tooltip("If false, Awake generation only runs when graph is missing or empty. Leave false until persistence owns locked node placement.")]
    public bool regenerateNodeLayerOnAwakeEvenIfGraphExists = false;

    [Header("Topography Node Candidates")]
    [Tooltip("If 0, derives target node count from clusterCount * average legacy nodes-per-cluster.")]
    [Min(0)] public int topographyTargetNodeCount = 0;

    [Tooltip("Candidate scan grid width over the topography field.")]
    [Range(16, 512)] public int topographyCandidateGridWidth = 160;

    [Tooltip("Candidate scan grid height over the topography field.")]
    [Range(16, 512)] public int topographyCandidateGridHeight = 100;

    [Tooltip("Reject candidates this close to the UV edge. Helps keep nodes away from map borders.")]
    [Range(0f, 0.35f)] public float topographyCandidateEdgeInset01 = 0.045f;

    [Tooltip("World-space radius used to inspect local coast/land/water mix around a candidate.")]
    [Min(0.1f)] public float topographyCandidateSampleRadiusWorld = 8f;

    [Tooltip("Local sample grid per candidate. Odd values like 5 or 7 work best.")]
    [Range(3, 11)] public int topographyCandidateSampleGrid = 5;

    [Tooltip("Minimum candidate score required before spacing selection.")]
    public float topographyCandidateMinScore = 0.35f;

    [Header("Topography Candidate Scoring")]
    public float coastPresenceWeight = 2.2f;
    public float beachWeight = 1.35f;
    public float lowlandWeight = 1.05f;
    public float shelfWaterWeight = 0.65f;
    public float shallowWaterWeight = 0.75f;
    public float highlandWeight = 0.20f;

    public float deepOceanPenalty = 1.6f;
    public float openOceanPenalty = 0.75f;
    public float mountainPenalty = 0.65f;

    [Tooltip("Bonus if the exact candidate point is beach or lowland.")]
    public float centerBeachLowlandBonus = 0.9f;

    [Tooltip("Bonus if the exact candidate point is shallow/shelf water near land, useful for ports just offshore.")]
    public float centerShallowOrShelfBonus = 0.35f;

    [Tooltip("Small deterministic score noise to break ties and avoid perfect grids.")]
    [Range(0f, 1f)] public float topographyCandidateNoise = 0.08f;

    [Header("Topography Node Spacing")]
    [Tooltip("Preferred spacing between selected nodes in graph/world units.")]
    [Min(0.1f)] public float topographyNodeMinSpacing = 9f;

    [Tooltip("If too few nodes are found, selection retries down to this fraction of topographyNodeMinSpacing.")]
    [Range(0.25f, 1f)] public float topographyMinSpacingFallbackFraction = 0.55f;

    [Header("Topography Landmass Coverage")]
    [Tooltip("If true, tries to place one node on each meaningful landmass before placing multiple nodes on the same island.")]
    public bool topographyPreferOneNodePerLandmass = true;

    [Tooltip("Minimum land cells in the candidate grid for a landmass to deserve coverage priority. Prevents every tiny rock from demanding a node.")]
    [Min(1)] public int topographyMinLandmassCellsForCoverage = 5;

    [Tooltip("Coverage pass spacing as a multiplier of topographyNodeMinSpacing. Lower lets nearby islands both get a node.")]
    [Range(0.25f, 1f)] public float topographyLandmassCoverageSpacingMultiplier = 0.65f;

    [Tooltip("How far, in candidate-grid cells, an offshore/shallow candidate may search for a nearby landmass ID.")]
    [Range(0, 6)] public int topographyCandidateLandmassSearchRadius = 3;

    [Header("Topography Cluster Routing")]
    [Tooltip("Extra intra-cluster route loops after the cluster spanning tree.")]
    [Range(0, 10)] public int topographyExtraEdgesPerCluster = 2;

    [Tooltip("Max route length for extra intra-cluster edges. Spanning tree edges can exceed this if needed.")]
    [Min(1f)] public float topographyIntraClusterExtraEdgeMaxLength = 42f;

    [Tooltip("Extra inter-cluster route loops after the cluster spanning tree.")]
    [Range(0, 50)] public int topographyExtraInterClusterEdges = 6;

    [Tooltip("Max route length for extra inter-cluster edges. Spanning tree edges can exceed this if needed.")]
    [Min(1f)] public float topographyInterClusterExtraEdgeMaxLength = 140f;

    [Header("Legacy Layout")]
    [Tooltip("Legacy: distance between cluster centers.")]
    [Min(1f)] public float clusterSpacing = 12f;

    [Tooltip("Legacy: radius of nodes around each cluster center.")]
    [Min(0.1f)] public float clusterRadius = 3.5f;

    [Tooltip("Legacy: jitter applied to each node position.")]
    [Min(0f)] public float nodeJitter = 0.6f;

    [Header("Legacy Connectivity")]
    [Tooltip("Legacy: extra connections within each cluster, in addition to a spanning tree.")]
    [Range(0, 10)] public int extraEdgesPerCluster = 2;

    [Tooltip("Legacy: how many links between clusters, in addition to a spanning tree across clusters.")]
    [Range(0, 50)] public int extraInterClusterEdges = 6;

    [Tooltip("Legacy: max distance allowed for an inter-cluster edge, in cluster steps.")]
    [Range(1, 10)] public int interClusterNeighborRange = 3;

    [Header("Initial Jitter")]
    [Tooltip("Random +/- applied to node stats at generation, deterministic via seed.")]
    [Range(0f, 1f)] public float statJitter = 0.25f;

    [Tooltip("Smaller jitter for building ratings so ports don't start absurd.")]
    [Range(0f, 1f)] public float buildingJitter = 0.10f;

    public event Action OnGraphGenerated;

    [Header("Debug / Viz")]
    public bool autoRegenerate = false;
    public bool drawGizmos = true;
    public bool drawNodeLabels = true;

    [Min(0.05f)] public float nodeGizmoRadius = 0.25f;

    [SerializeField] public MapGraph graph;

    private struct TopographyCandidate
    {
        public Vector2 position;
        public float u;
        public float v;
        public float score;
        public int landmassId;
        public WorldMapTopographyClass centerClass;
        public CandidateMetrics metrics;
    }

    private struct CandidateMetrics
    {
        public int totalSamples;

        public int deepOcean;
        public int openOcean;
        public int shelfWater;
        public int shallowWater;
        public int beach;
        public int lowland;
        public int highland;
        public int mountain;

        public int WaterCount => deepOcean + openOcean + shelfWater + shallowWater;
        public int LandCount => beach + lowland + highland + mountain;

        public float DeepOcean01 => Ratio(deepOcean);
        public float OpenOcean01 => Ratio(openOcean);
        public float ShelfWater01 => Ratio(shelfWater);
        public float ShallowWater01 => Ratio(shallowWater);
        public float Beach01 => Ratio(beach);
        public float Lowland01 => Ratio(lowland);
        public float Highland01 => Ratio(highland);
        public float Mountain01 => Ratio(mountain);

        public float Water01 => Ratio(WaterCount);
        public float Land01 => Ratio(LandCount);

        public float CoastPresence01
        {
            get
            {
                if (WaterCount <= 0 || LandCount <= 0)
                    return 0f;

                return 1f - Mathf.Abs(Water01 - Land01);
            }
        }

        private float Ratio(int count)
        {
            return totalSamples <= 0 ? 0f : count / (float)totalSamples;
        }
    }

    private void Reset()
    {
        topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>();
    }

    private void Awake()
    {
        if (topographySource == null)
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>(FindObjectsInactive.Include);

        if (generateNodeLayerOnAwake)
        {
            if (!WorldMapSaveRestorer.TryRestoreGraphToGenerator(this))
                EnsureGenerated();
        }
    }

    public bool HasGeneratedGraph =>
        graph != null &&
        graph.nodes != null &&
        graph.nodes.Count > 0;

    public void EnsureGenerated()
    {
        if (HasGeneratedGraph && !regenerateNodeLayerOnAwakeEvenIfGraphExists)
            return;

        Generate();
    }

    public void UseRestoredGraph(MapGraph restoredGraph, string reason = "")
    {
        if (restoredGraph == null)
        {
            Debug.LogWarning($"[WorldMapGraphGenerator] UseRestoredGraph ignored null graph. reason='{reason}'", this);
            return;
        }

        graph = restoredGraph;
        graph.RebuildEdgeSet();

        Debug.Log(
            $"[WorldMapGraphGenerator] Restored persisted graph. " +
            $"Nodes={graph.nodes.Count}, Edges={graph.edges.Count}, reason='{reason}'",
            this
        );

        OnGraphGenerated?.Invoke();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnValidate()
    {
        nodesPerClusterMin = Mathf.Max(1, nodesPerClusterMin);
        nodesPerClusterMax = Mathf.Max(nodesPerClusterMin, nodesPerClusterMax);

        topographyTargetNodeCount = Mathf.Max(0, topographyTargetNodeCount);
        topographyCandidateGridWidth = Mathf.Max(1, topographyCandidateGridWidth);
        topographyCandidateGridHeight = Mathf.Max(1, topographyCandidateGridHeight);
        topographyCandidateSampleRadiusWorld = Mathf.Max(0.1f, topographyCandidateSampleRadiusWorld);
        topographyNodeMinSpacing = Mathf.Max(0.1f, topographyNodeMinSpacing);
        topographyMinLandmassCellsForCoverage = Mathf.Max(1, topographyMinLandmassCellsForCoverage);
        topographyCandidateLandmassSearchRadius = Mathf.Max(0, topographyCandidateLandmassSearchRadius);
        topographyIntraClusterExtraEdgeMaxLength = Mathf.Max(1f, topographyIntraClusterExtraEdgeMaxLength);
        topographyInterClusterExtraEdgeMaxLength = Mathf.Max(1f, topographyInterClusterExtraEdgeMaxLength);

        if (autoRegenerate)
            Generate();
    }

    private static float Jitter(System.Random rng, float magnitude)
    {
        return ((float)rng.NextDouble() * 2f - 1f) * magnitude;
    }

    [ContextMenu("Ensure Generated Map Graph")]
    private void ContextEnsureGenerated()
    {
        EnsureGenerated();
    }

    [ContextMenu("Generate Map Graph")]
    public void Generate()
    {
        bool generated = false;

        if (generationMode == WorldMapGraphGenerationMode.TopographyFirst)
        {
            generated = GenerateTopographyFirst();

            if (!generated && !fallbackToLegacyIfTopographyMissing)
            {
                Debug.LogError(
                    "[WorldMapGraphGenerator] Topography-first generation failed and fallback is disabled.",
                    this
                );

                return;
            }
        }

        if (!generated)
            GenerateLegacyClusterFirst();

        graph.RebuildEdgeSet();
        OnGraphGenerated?.Invoke();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void GenerateLegacyClusterFirst()
    {
        var rng = new System.Random(seed);
        graph = new MapGraph(seed);

        var clusterCenters = GenerateClusterCenters(rng, clusterCount, clusterSpacing);
        var clusterNodes = new List<List<int>>(clusterCount);

        for (int c = 0; c < clusterCount; c++)
        {
            int n = rng.Next(nodesPerClusterMin, nodesPerClusterMax + 1);
            clusterNodes.Add(new List<int>(n));

            for (int i = 0; i < n; i++)
            {
                Vector2 pos = ScatterInCluster(rng, clusterCenters[c], clusterRadius, nodeJitter);
                int nodeId = AddGeneratedNode(graph, pos, c, $"cluster_{c:00}_node_{i:00}", rng);

                clusterNodes[c].Add(nodeId);
            }
        }

        for (int c = 0; c < clusterCount; c++)
        {
            MakeRandomSpanningTree(rng, graph, clusterNodes[c]);
            AddExtraEdges(rng, graph, clusterNodes[c], extraEdgesPerCluster);
        }

        var clusterRep = new int[clusterCount];
        for (int c = 0; c < clusterCount; c++)
        {
            clusterRep[c] = FindNearestNodeToPoint(graph, clusterNodes[c], clusterCenters[c]);
            graph.nodes[clusterRep[c]].isPrimary = true;
        }

        MakeClusterSpanningTree(graph, clusterCenters, clusterRep);
        AddExtraInterClusterEdgesLegacy(rng, graph, clusterCenters, clusterRep, extraInterClusterEdges, interClusterNeighborRange);

        MarkStartAndFar(graph);

        Debug.Log(
            $"[WorldMapGraphGenerator] Generated legacy cluster-first graph. Nodes={graph.nodes.Count}, Edges={graph.edges.Count}",
            this
        );
    }

    private bool GenerateTopographyFirst()
    {
        if (topographySource == null)
            topographySource = FindAnyObjectByType<WorldMapTopographyDebugSource>();

        if (topographySource == null)
        {
            Debug.LogWarning("[WorldMapGraphGenerator] Missing WorldMapTopographyDebugSource.", this);
            return false;
        }

        if ((topographySource.Field == null || !topographySource.Field.IsValid) && loadOrGenerateTopographyIfMissing)
            topographySource.LoadOrGenerate();

        WorldMapTopographyField field = topographySource.Field;
        WorldMapTopographySettings settings = topographySource.Settings;

        if (field == null || !field.IsValid || settings == null)
        {
            Debug.LogWarning("[WorldMapGraphGenerator] Topography source has no valid field/settings.", this);
            return false;
        }

        float effectiveSeaLevel = topographySource.EffectiveSeaLevel01;
        if (effectiveSeaLevel <= 0f)
            effectiveSeaLevel = settings.seaLevel01;

        int targetNodeCount = GetTopographyTargetNodeCount();
        if (targetNodeCount <= 0)
            return false;

        List<TopographyCandidate> candidates = BuildTopographyCandidates(field, settings, effectiveSeaLevel);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[WorldMapGraphGenerator] No topography node candidates found.", this);
            return false;
        }

        List<TopographyCandidate> selected = SelectTopographyNodes(candidates, targetNodeCount);

        if (selected.Count == 0)
        {
            Debug.LogWarning("[WorldMapGraphGenerator] Topography candidate selection produced no nodes.", this);
            return false;
        }

        graph = new MapGraph(seed);

        int actualClusterCount = Mathf.Clamp(clusterCount, 1, selected.Count);
        List<List<int>> clusterCandidateIndices = BuildTopographyClusters(selected, actualClusterCount, out List<Vector2> clusterCenters);

        var clusterNodes = new List<List<int>>(actualClusterCount);
        var clusterRep = new int[actualClusterCount];

        for (int c = 0; c < actualClusterCount; c++)
        {
            List<int> candidateIndices = clusterCandidateIndices[c];
            SortCandidateIndicesAroundCenter(candidateIndices, selected, clusterCenters[c]);

            clusterNodes.Add(new List<int>(candidateIndices.Count));

            for (int i = 0; i < candidateIndices.Count; i++)
            {
                TopographyCandidate candidate = selected[candidateIndices[i]];
                string localStableId = $"topo_cluster_{c:00}_node_{i:00}";

                var nodeRng = new System.Random(
                    StableHash(localStableId) ^
                    (seed * 486187739)
                );

                int nodeId = AddGeneratedNode(
                    graph,
                    candidate.position,
                    c,
                    localStableId,
                    nodeRng
                );

                clusterNodes[c].Add(nodeId);
            }

            clusterRep[c] = FindNearestNodeToPoint(graph, clusterNodes[c], clusterCenters[c]);
            graph.nodes[clusterRep[c]].isPrimary = true;
        }

        for (int c = 0; c < actualClusterCount; c++)
        {
            MakeDistanceSpanningTree(graph, clusterNodes[c]);
            AddExtraEdgesByDistance(
                new System.Random(seed ^ (c * 19349663) ^ 0x41B00B1E),
                graph,
                clusterNodes[c],
                topographyExtraEdgesPerCluster,
                topographyIntraClusterExtraEdgeMaxLength
            );
        }

        MakeClusterSpanningTree(graph, clusterCenters, clusterRep);

        AddExtraInterClusterEdgesByDistance(
            new System.Random(seed ^ 0x6827A7D1),
            graph,
            clusterCenters,
            clusterRep,
            topographyExtraInterClusterEdges,
            topographyInterClusterExtraEdgeMaxLength
        );

        MarkStartAndFar(graph);

        Debug.Log(
            $"[WorldMapGraphGenerator] Generated topography-first graph. " +
            $"Candidates={candidates.Count}, Nodes={graph.nodes.Count}/{targetNodeCount}, " +
            $"Clusters={actualClusterCount}, Edges={graph.edges.Count}",
            this
        );

        return true;
    }

    private int GetTopographyTargetNodeCount()
    {
        if (topographyTargetNodeCount > 0)
            return topographyTargetNodeCount;

        int averageNodesPerCluster = Mathf.RoundToInt((nodesPerClusterMin + nodesPerClusterMax) * 0.5f);
        return Mathf.Max(1, clusterCount * Mathf.Max(1, averageNodesPerCluster));
    }

    private List<TopographyCandidate> BuildTopographyCandidates(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        float effectiveSeaLevel)
    {
        var candidates = new List<TopographyCandidate>(
            topographyCandidateGridWidth * topographyCandidateGridHeight / 4
        );

        int gw = Mathf.Max(1, topographyCandidateGridWidth);
        int gh = Mathf.Max(1, topographyCandidateGridHeight);
        float edgeInset = Mathf.Clamp01(topographyCandidateEdgeInset01);

        int[] landmassIds = BuildLandmassIds(
            field,
            settings,
            effectiveSeaLevel,
            gw,
            gh,
            topographyMinLandmassCellsForCoverage
        );

        for (int y = 0; y < gh; y++)
        {
            float v = (y + 0.5f) / gh;

            for (int x = 0; x < gw; x++)
            {
                float u = (x + 0.5f) / gw;

                if (DistanceToUvEdge(u, v) < edgeInset)
                    continue;

                float centerHeight = field.Sample01UV(u, v);
                WorldMapTopographyClass centerClass = ClassifyHeight(centerHeight, settings, effectiveSeaLevel);

                CandidateMetrics metrics = CalculateCandidateMetrics(field, settings, effectiveSeaLevel, u, v);
                float score = ScoreTopographyCandidate(metrics, centerClass);

                score += SignedHash01(seed, x, y, 0x71A5) * topographyCandidateNoise;

                if (score < topographyCandidateMinScore)
                    continue;

                Vector2 pos = new Vector2(
                    Mathf.Lerp(field.WorldBounds.xMin, field.WorldBounds.xMax, u),
                    Mathf.Lerp(field.WorldBounds.yMin, field.WorldBounds.yMax, v)
                );

                int landmassId = FindNearbyLandmassId(
                    landmassIds,
                    gw,
                    gh,
                    x,
                    y,
                    topographyCandidateLandmassSearchRadius
                );

                candidates.Add(new TopographyCandidate
                {
                    position = pos,
                    u = u,
                    v = v,
                    score = score,
                    landmassId = landmassId,
                    centerClass = centerClass,
                    metrics = metrics
                });
            }
        }

        candidates.Sort((a, b) =>
        {
            int byScore = b.score.CompareTo(a.score);
            if (byScore != 0)
                return byScore;

            int byX = a.position.x.CompareTo(b.position.x);
            if (byX != 0)
                return byX;

            return a.position.y.CompareTo(b.position.y);
        });

        return candidates;
    }

    private CandidateMetrics CalculateCandidateMetrics(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        float effectiveSeaLevel,
        float centerU,
        float centerV)
    {
        var m = new CandidateMetrics();

        int grid = Mathf.Max(1, topographyCandidateSampleGrid);
        if (grid % 2 == 0)
            grid++;

        float radiusU = topographyCandidateSampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.width);
        float radiusV = topographyCandidateSampleRadiusWorld / Mathf.Max(0.0001f, field.WorldBounds.height);

        for (int sy = 0; sy < grid; sy++)
        {
            float ty = grid <= 1 ? 0f : sy / (float)(grid - 1);
            float oy = Mathf.Lerp(-radiusV, radiusV, ty);

            for (int sx = 0; sx < grid; sx++)
            {
                float tx = grid <= 1 ? 0f : sx / (float)(grid - 1);
                float ox = Mathf.Lerp(-radiusU, radiusU, tx);

                float u = Mathf.Clamp01(centerU + ox);
                float v = Mathf.Clamp01(centerV + oy);

                WorldMapTopographyClass cls = ClassifyHeight(
                    field.Sample01UV(u, v),
                    settings,
                    effectiveSeaLevel
                );

                AddClass(ref m, cls);
                m.totalSamples++;
            }
        }

        return m;
    }

    private float ScoreTopographyCandidate(CandidateMetrics m, WorldMapTopographyClass centerClass)
    {
        if (m.totalSamples <= 0)
            return float.NegativeInfinity;

        float score = 0f;

        score += m.CoastPresence01 * coastPresenceWeight;
        score += m.Beach01 * beachWeight;
        score += m.Lowland01 * lowlandWeight;
        score += m.ShelfWater01 * shelfWaterWeight;
        score += m.ShallowWater01 * shallowWaterWeight;
        score += m.Highland01 * highlandWeight;

        score -= m.DeepOcean01 * deepOceanPenalty;
        score -= m.OpenOcean01 * openOceanPenalty;
        score -= m.Mountain01 * mountainPenalty;

        switch (centerClass)
        {
            case WorldMapTopographyClass.Beach:
            case WorldMapTopographyClass.Lowland:
                score += centerBeachLowlandBonus;
                break;

            case WorldMapTopographyClass.ShelfWater:
            case WorldMapTopographyClass.ShallowWater:
                score += centerShallowOrShelfBonus;
                break;

            case WorldMapTopographyClass.DeepOcean:
            case WorldMapTopographyClass.OpenOcean:
            case WorldMapTopographyClass.Mountain:
                score -= 1.0f;
                break;
        }

        if (m.LandCount <= 0 || m.WaterCount <= 0)
            score -= 1.5f;

        return score;
    }

    private List<TopographyCandidate> SelectTopographyNodes(
        List<TopographyCandidate> candidates,
        int targetNodeCount)
    {
        var selected = new List<TopographyCandidate>(targetNodeCount);

        if (candidates == null || candidates.Count == 0 || targetNodeCount <= 0)
            return selected;

        bool[] used = new bool[candidates.Count];

        float preferredSpacing = Mathf.Max(0.1f, topographyNodeMinSpacing);
        float minSpacing = preferredSpacing * Mathf.Clamp(topographyMinSpacingFallbackFraction, 0.25f, 1f);

        if (topographyPreferOneNodePerLandmass)
        {
            float coverageSpacing =
                preferredSpacing *
                Mathf.Clamp(topographyLandmassCoverageSpacingMultiplier, 0.25f, 1f);

            AddBestCandidatePerLandmass(
                candidates,
                selected,
                used,
                targetNodeCount,
                coverageSpacing
            );
        }

        float spacing = preferredSpacing;

        while (selected.Count < targetNodeCount && spacing >= minSpacing - 0.001f)
        {
            for (int i = 0; i < candidates.Count && selected.Count < targetNodeCount; i++)
            {
                if (used[i])
                    continue;

                if (!IsFarEnoughFromSelected(candidates[i].position, selected, spacing))
                    continue;

                selected.Add(candidates[i]);
                used[i] = true;
            }

            spacing *= 0.78f;
        }

        return selected;
    }

    private static void AddBestCandidatePerLandmass(
        List<TopographyCandidate> candidates,
        List<TopographyCandidate> selected,
        bool[] used,
        int targetNodeCount,
        float spacing)
    {
        if (candidates == null || selected == null || used == null)
            return;

        var bestByLandmass = new Dictionary<int, int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            int landmassId = candidates[i].landmassId;

            if (landmassId < 0)
                continue;

            if (!bestByLandmass.TryGetValue(landmassId, out int existingIndex))
            {
                bestByLandmass[landmassId] = i;
                continue;
            }

            if (candidates[i].score > candidates[existingIndex].score)
                bestByLandmass[landmassId] = i;
        }

        var picks = new List<int>(bestByLandmass.Values);

        picks.Sort((a, b) =>
        {
            int byScore = candidates[b].score.CompareTo(candidates[a].score);
            if (byScore != 0)
                return byScore;

            int byLandmass = candidates[a].landmassId.CompareTo(candidates[b].landmassId);
            if (byLandmass != 0)
                return byLandmass;

            int byX = candidates[a].position.x.CompareTo(candidates[b].position.x);
            if (byX != 0)
                return byX;

            return candidates[a].position.y.CompareTo(candidates[b].position.y);
        });

        for (int i = 0; i < picks.Count && selected.Count < targetNodeCount; i++)
        {
            int candidateIndex = picks[i];

            if (candidateIndex < 0 || candidateIndex >= candidates.Count)
                continue;

            if (used[candidateIndex])
                continue;

            if (!IsFarEnoughFromSelected(candidates[candidateIndex].position, selected, spacing))
                continue;

            selected.Add(candidates[candidateIndex]);
            used[candidateIndex] = true;
        }
    }

    private static bool IsFarEnoughFromSelected(
        Vector2 position,
        List<TopographyCandidate> selected,
        float minDistance)
    {
        float minSqr = minDistance * minDistance;

        for (int i = 0; i < selected.Count; i++)
        {
            if (Vector2.SqrMagnitude(position - selected[i].position) < minSqr)
                return false;
        }

        return true;
    }

    private List<List<int>> BuildTopographyClusters(
        List<TopographyCandidate> selected,
        int targetClusterCount,
        out List<Vector2> clusterCenters)
    {
        clusterCenters = new List<Vector2>(targetClusterCount);
        var centerCandidateIndices = PickClusterCenterCandidates(selected, targetClusterCount);

        centerCandidateIndices.Sort((a, b) =>
        {
            Vector2 pa = selected[a].position;
            Vector2 pb = selected[b].position;

            int byX = pa.x.CompareTo(pb.x);
            if (byX != 0)
                return byX;

            return pa.y.CompareTo(pb.y);
        });

        for (int i = 0; i < centerCandidateIndices.Count; i++)
            clusterCenters.Add(selected[centerCandidateIndices[i]].position);

        var clusters = new List<List<int>>(clusterCenters.Count);
        for (int i = 0; i < clusterCenters.Count; i++)
            clusters.Add(new List<int>());

        for (int i = 0; i < selected.Count; i++)
        {
            int nearest = FindNearestPointIndex(selected[i].position, clusterCenters);
            clusters[nearest].Add(i);
        }

        return clusters;
    }

    private static List<int> PickClusterCenterCandidates(
        List<TopographyCandidate> selected,
        int targetClusterCount)
    {
        var centers = new List<int>(targetClusterCount);

        if (selected == null || selected.Count == 0 || targetClusterCount <= 0)
            return centers;

        int first = 0;
        float minX = float.PositiveInfinity;

        for (int i = 0; i < selected.Count; i++)
        {
            float x = selected[i].position.x;
            if (x < minX)
            {
                minX = x;
                first = i;
            }
        }

        centers.Add(first);

        while (centers.Count < targetClusterCount && centers.Count < selected.Count)
        {
            int best = -1;
            float bestD = float.NegativeInfinity;

            for (int i = 0; i < selected.Count; i++)
            {
                if (centers.Contains(i))
                    continue;

                float nearest = float.PositiveInfinity;

                for (int c = 0; c < centers.Count; c++)
                {
                    float d = Vector2.SqrMagnitude(selected[i].position - selected[centers[c]].position);
                    if (d < nearest)
                        nearest = d;
                }

                if (nearest > bestD)
                {
                    bestD = nearest;
                    best = i;
                }
            }

            if (best < 0)
                break;

            centers.Add(best);
        }

        return centers;
    }

    private static int FindNearestPointIndex(Vector2 position, List<Vector2> points)
    {
        int best = 0;
        float bestD = float.PositiveInfinity;

        for (int i = 0; i < points.Count; i++)
        {
            float d = Vector2.SqrMagnitude(position - points[i]);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }

        return best;
    }

    private static void SortCandidateIndicesAroundCenter(
        List<int> indices,
        List<TopographyCandidate> candidates,
        Vector2 center)
    {
        indices.Sort((a, b) =>
        {
            Vector2 da = candidates[a].position - center;
            Vector2 db = candidates[b].position - center;

            float aa = Mathf.Atan2(da.y, da.x);
            float ab = Mathf.Atan2(db.y, db.x);

            return aa.CompareTo(ab);
        });
    }

    private int AddGeneratedNode(
        MapGraph g,
        Vector2 pos,
        int clusterId,
        string localStableId,
        System.Random rng)
    {
        float dock0 = Mathf.Clamp(1f + Jitter(rng, buildingJitter), 0f, 4f);
        float trade0 = Mathf.Clamp(1f + Jitter(rng, buildingJitter), 0f, 4f);

        float prosperity0 = Mathf.Clamp(1f + Jitter(rng, statJitter), 0f, 4f);
        float stability0 = Mathf.Clamp(1f + Jitter(rng, statJitter), 0f, 4f);
        float security0 = Mathf.Clamp(1f + Jitter(rng, statJitter), 0f, 4f);
        float foodBalance0 = Mathf.Clamp(0f + Jitter(rng, statJitter), -4f, 4f);

        float basePop = 100f;
        float population0 = Mathf.Lerp(0.5f, 2.0f, (float)rng.NextDouble()) * basePop;

        int nodeId = g.AddNode(new MapNode
        {
            id = g.nodes.Count,
            localStableId = localStableId,
            clusterId = clusterId,
            position = pos,
            kind = NodeKind.Island,
            population = population0,

            displayName = GeneratePlaceholderNodeName(localStableId, pos, clusterId, g.nodes.Count),
            biome = BiomeId.None,

            primaryResource = ResourceId.None,
            secondaryResource = ResourceId.None,

            primaryFaction = FactionId.None,
            secondaryFaction = FactionId.None,

            dock = new BuildingRating(dock0),
            tradeHub = new BuildingRating(trade0),

            stats = new List<NodeStat>
            {
                new NodeStat { id = NodeStatId.Prosperity, stat = new SimStat(prosperity0, 1.0f, 0.08f) },
                new NodeStat { id = NodeStatId.Stability,   stat = new SimStat(stability0,  1.0f, 0.10f) },
                new NodeStat { id = NodeStatId.Security,    stat = new SimStat(security0,   1.0f, 0.06f) },
                new NodeStat { id = NodeStatId.FoodBalance, stat = new SimStat(foodBalance0, 0f, 0.06f, -4f, 4f) },
            }
        });

        return nodeId;
    }


    private string GeneratePlaceholderNodeName(
        string localStableId,
        Vector2 position,
        int clusterId,
        int nodeIndex)
    {
        int hash =
            StableHash(localStableId) ^
            (seed * 486187739) ^
            (clusterId * 19349663) ^
            (nodeIndex * 83492791) ^
            Mathf.RoundToInt(position.x * 100f) ^
            (Mathf.RoundToInt(position.y * 100f) * 31);

        var rng = new System.Random(hash);

        string[] starts =
        {
            "Aro", "Bel", "Cai", "Dun", "Eli", "Faro", "Galen", "Hara", "Ilo", "Juna",
            "Karo", "Luma", "Miri", "Nalo", "Oren", "Pala", "Quin", "Rava", "Sora", "Tavi",
            "Uma", "Vela", "Wyn", "Yara", "Zeno"
        };

        string[] middles =
        {
            "", "la", "ra", "mi", "na", "to", "ve", "sha", "lo", "ki", "mar", "ren", "sol", "tan"
        };

        string[] endings =
        {
            "a", "an", "ar", "el", "en", "ia", "in", "is", "oa", "on", "or", "os", "um"
        };

        string[] suffixes =
        {
            "Isle", "Cay", "Atoll", "Haven", "Harbor", "Shoal", "Reach", "Key", "Point", "Sound", "Rest", "Landing"
        };

        string core =
            starts[rng.Next(starts.Length)] +
            middles[rng.Next(middles.Length)] +
            endings[rng.Next(endings.Length)];

        // Clean up a couple of ugly accidental doubles. This is placeholder naming, not linguistics,
        // despite humanity's heroic ability to make even fake names complicated.
        core = core.Replace("aaa", "aa").Replace("ooa", "oa").Replace("ii", "i");

        int pattern = rng.Next(4);
        string suffix = suffixes[rng.Next(suffixes.Length)];

        switch (pattern)
        {
            case 0:
                return $"{core} {suffix}";

            case 1:
                return $"{core} Bay";

            case 2:
                return $"Port {core}";

            default:
                return core;
        }
    }

    private static void AddClass(ref CandidateMetrics m, WorldMapTopographyClass cls)
    {
        switch (cls)
        {
            case WorldMapTopographyClass.DeepOcean:
                m.deepOcean++;
                break;

            case WorldMapTopographyClass.OpenOcean:
                m.openOcean++;
                break;

            case WorldMapTopographyClass.ShelfWater:
                m.shelfWater++;
                break;

            case WorldMapTopographyClass.ShallowWater:
                m.shallowWater++;
                break;

            case WorldMapTopographyClass.Beach:
                m.beach++;
                break;

            case WorldMapTopographyClass.Lowland:
                m.lowland++;
                break;

            case WorldMapTopographyClass.Highland:
                m.highland++;
                break;

            case WorldMapTopographyClass.Mountain:
                m.mountain++;
                break;
        }
    }

    private static int[] BuildLandmassIds(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings,
        float effectiveSeaLevel,
        int width,
        int height,
        int minCellsForCoverage)
    {
        int[] ids = new int[width * height];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = -1;

        bool[] land = new bool[ids.Length];

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float h = field.Sample01UV(u, v);

                WorldMapTopographyClass cls = ClassifyHeight(h, settings, effectiveSeaLevel);
                land[y * width + x] = IsLandClass(cls);
            }
        }

        int nextId = 0;
        var queue = new Queue<int>();
        var cellsInCurrent = new List<int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int start = y * width + x;

                if (!land[start] || ids[start] >= 0)
                    continue;

                cellsInCurrent.Clear();
                queue.Clear();

                ids[start] = nextId;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int idx = queue.Dequeue();
                    cellsInCurrent.Add(idx);

                    int cx = idx % width;
                    int cy = idx / width;

                    TryAddLandNeighbor(cx - 1, cy, width, height, land, ids, nextId, queue);
                    TryAddLandNeighbor(cx + 1, cy, width, height, land, ids, nextId, queue);
                    TryAddLandNeighbor(cx, cy - 1, width, height, land, ids, nextId, queue);
                    TryAddLandNeighbor(cx, cy + 1, width, height, land, ids, nextId, queue);
                }

                if (cellsInCurrent.Count < Mathf.Max(1, minCellsForCoverage))
                {
                    for (int i = 0; i < cellsInCurrent.Count; i++)
                        ids[cellsInCurrent[i]] = -1;
                }
                else
                {
                    nextId++;
                }
            }
        }

        return ids;
    }

    private static void TryAddLandNeighbor(
        int x,
        int y,
        int width,
        int height,
        bool[] land,
        int[] ids,
        int id,
        Queue<int> queue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        int idx = y * width + x;

        if (!land[idx] || ids[idx] >= 0)
            return;

        ids[idx] = id;
        queue.Enqueue(idx);
    }

    private static int FindNearbyLandmassId(
        int[] landmassIds,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius)
    {
        if (landmassIds == null || landmassIds.Length != width * height)
            return -1;

        radius = Mathf.Max(0, radius);

        int own = landmassIds[centerY * width + centerX];
        if (own >= 0)
            return own;

        int bestId = -1;
        int bestDist = int.MaxValue;

        for (int oy = -radius; oy <= radius; oy++)
        {
            int y = centerY + oy;
            if (y < 0 || y >= height)
                continue;

            for (int ox = -radius; ox <= radius; ox++)
            {
                int x = centerX + ox;
                if (x < 0 || x >= width)
                    continue;

                int id = landmassIds[y * width + x];
                if (id < 0)
                    continue;

                int d = ox * ox + oy * oy;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestId = id;
                }
            }
        }

        return bestId;
    }

    private static bool IsLandClass(WorldMapTopographyClass cls)
    {
        return cls == WorldMapTopographyClass.Beach ||
               cls == WorldMapTopographyClass.Lowland ||
               cls == WorldMapTopographyClass.Highland ||
               cls == WorldMapTopographyClass.Mountain;
    }

    private static WorldMapTopographyClass ClassifyHeight(
        float height01,
        WorldMapTopographySettings settings,
        float effectiveSeaLevel)
    {
        float h = Mathf.Clamp01(height01);
        float sea = Mathf.Clamp01(effectiveSeaLevel);

        if (h < sea)
        {
            float depth = sea - h;

            if (depth <= settings.shallowDepth01 * 0.35f)
                return WorldMapTopographyClass.ShallowWater;

            if (depth <= settings.shallowDepth01)
                return WorldMapTopographyClass.ShelfWater;

            if (depth <= settings.openOceanDepth01)
                return WorldMapTopographyClass.OpenOcean;

            return WorldMapTopographyClass.DeepOcean;
        }

        float landHeight = h - sea;

        if (landHeight <= settings.beachHeight01)
            return WorldMapTopographyClass.Beach;

        if (landHeight <= settings.lowlandHeight01)
            return WorldMapTopographyClass.Lowland;

        if (landHeight <= settings.highlandHeight01)
            return WorldMapTopographyClass.Highland;

        return WorldMapTopographyClass.Mountain;
    }

    private static float DistanceToUvEdge(float u, float v)
    {
        return Mathf.Min(
            Mathf.Min(u, 1f - u),
            Mathf.Min(v, 1f - v)
        );
    }

    private static float SignedHash01(int seed, int x, int y, int salt)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seed) * 16777619u;
            h = (h ^ (uint)(x * 73856093)) * 16777619u;
            h = (h ^ (uint)(y * 19349663)) * 16777619u;
            h = (h ^ (uint)(salt * 83492791)) * 16777619u;

            float v = (h & 0x00FFFFFF) / (float)0x00FFFFFF;
            return (v * 2f) - 1f;
        }
    }

    private static List<Vector2> GenerateClusterCenters(System.Random rng, int count, float spacing)
    {
        var centers = new List<Vector2>(count);

        float baseRadius = spacing * 0.75f;
        for (int i = 0; i < count; i++)
        {
            float t = (count <= 1) ? 0f : (i / (float)(count - 1));
            float angle = t * Mathf.PI * 2f + (float)rng.NextDouble() * 0.35f;
            float radius = baseRadius + t * spacing * 0.8f + (float)rng.NextDouble() * spacing * 0.25f;

            Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            centers.Add(p);
        }

        for (int i = 0; i < centers.Count; i++)
        {
            centers[i] += new Vector2(
                ((float)rng.NextDouble() - 0.5f) * spacing * 0.25f,
                ((float)rng.NextDouble() - 0.5f) * spacing * 0.25f
            );
        }

        return centers;
    }

    private static Vector2 ScatterInCluster(System.Random rng, Vector2 center, float radius, float jitter)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;

        Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

        p += new Vector2(
            ((float)rng.NextDouble() - 0.5f) * jitter,
            ((float)rng.NextDouble() - 0.5f) * jitter
        );

        return p;
    }

    private static void MakeRandomSpanningTree(System.Random rng, MapGraph g, List<int> nodeIds)
    {
        if (nodeIds.Count <= 1)
            return;

        for (int i = 1; i < nodeIds.Count; i++)
        {
            int a = nodeIds[i];
            int b = nodeIds[rng.Next(0, i)];
            g.AddEdge(a, b, EdgeKind.Route);
        }
    }

    private static void MakeDistanceSpanningTree(MapGraph g, List<int> nodeIds)
    {
        if (nodeIds == null || nodeIds.Count <= 1)
            return;

        var connected = new List<int> { nodeIds[0] };
        var remaining = new List<int>(nodeIds);
        remaining.RemoveAt(0);

        while (remaining.Count > 0)
        {
            int bestConnected = connected[0];
            int bestRemainingIndex = 0;
            float bestD = float.PositiveInfinity;

            for (int ci = 0; ci < connected.Count; ci++)
            {
                int a = connected[ci];

                for (int ri = 0; ri < remaining.Count; ri++)
                {
                    int b = remaining[ri];
                    float d = Vector2.SqrMagnitude(g.nodes[a].position - g.nodes[b].position);

                    if (d < bestD)
                    {
                        bestD = d;
                        bestConnected = a;
                        bestRemainingIndex = ri;
                    }
                }
            }

            int picked = remaining[bestRemainingIndex];
            remaining.RemoveAt(bestRemainingIndex);

            g.AddEdge(bestConnected, picked, EdgeKind.Route);
            connected.Add(picked);
        }
    }

    private static void AddExtraEdges(System.Random rng, MapGraph g, List<int> nodeIds, int extraEdges)
    {
        if (nodeIds.Count <= 2 || extraEdges <= 0)
            return;

        int tries = extraEdges * 10;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = nodeIds[rng.Next(nodeIds.Count)];
            int b = nodeIds[rng.Next(nodeIds.Count)];
            if (a == b)
                continue;

            if (g.HasEdge(a, b))
                continue;

            float d = Vector2.Distance(g.nodes[a].position, g.nodes[b].position);
            if (d > 6.0f && rng.NextDouble() < 0.7)
                continue;

            g.AddEdge(a, b, EdgeKind.Route);
            added++;
        }
    }

    private static void AddExtraEdgesByDistance(
        System.Random rng,
        MapGraph g,
        List<int> nodeIds,
        int extraEdges,
        float maxDistance)
    {
        if (nodeIds == null || nodeIds.Count <= 2 || extraEdges <= 0)
            return;

        int tries = extraEdges * 25;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = nodeIds[rng.Next(nodeIds.Count)];
            int b = nodeIds[rng.Next(nodeIds.Count)];

            if (a == b || g.HasEdge(a, b))
                continue;

            float d = Vector2.Distance(g.nodes[a].position, g.nodes[b].position);
            if (d > maxDistance)
                continue;

            g.AddEdge(a, b, EdgeKind.Route);
            added++;
        }
    }

    private static int FindNearestNodeToPoint(MapGraph g, List<int> nodeIds, Vector2 point)
    {
        int best = nodeIds[0];
        float bestD = float.PositiveInfinity;

        foreach (int id in nodeIds)
        {
            float d = Vector2.SqrMagnitude(g.nodes[id].position - point);
            if (d < bestD)
            {
                bestD = d;
                best = id;
            }
        }

        return best;
    }

    private static void MakeClusterSpanningTree(MapGraph g, List<Vector2> centers, int[] reps)
    {
        int n = reps.Length;
        if (n <= 1)
            return;

        for (int c = 1; c < n; c++)
        {
            int bestPrev = 0;
            float bestD = float.PositiveInfinity;

            for (int p = 0; p < c; p++)
            {
                float d = Vector2.SqrMagnitude(centers[c] - centers[p]);
                if (d < bestD)
                {
                    bestD = d;
                    bestPrev = p;
                }
            }

            g.AddEdge(reps[c], reps[bestPrev], EdgeKind.Route);
        }
    }

    private static void AddExtraInterClusterEdgesLegacy(
        System.Random rng,
        MapGraph g,
        List<Vector2> centers,
        int[] reps,
        int extraEdges,
        int neighborRange)
    {
        int n = reps.Length;
        if (n <= 2 || extraEdges <= 0)
            return;

        int tries = extraEdges * 20;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = rng.Next(0, n);
            int b = rng.Next(0, n);
            if (a == b)
                continue;

            if (Mathf.Abs(a - b) > neighborRange && rng.NextDouble() < 0.85)
                continue;

            int na = reps[a];
            int nb = reps[b];

            if (g.HasEdge(na, nb))
                continue;

            float worldD = Vector2.Distance(centers[a], centers[b]);
            if (worldD > neighborRange * 1.2f * 12f && rng.NextDouble() < 0.85)
                continue;

            g.AddEdge(na, nb, EdgeKind.Route);
            added++;
        }
    }

    private static void AddExtraInterClusterEdgesByDistance(
        System.Random rng,
        MapGraph g,
        List<Vector2> centers,
        int[] reps,
        int extraEdges,
        float maxDistance)
    {
        int n = reps.Length;
        if (n <= 2 || extraEdges <= 0)
            return;

        int tries = extraEdges * 30;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = rng.Next(0, n);
            int b = rng.Next(0, n);

            if (a == b)
                continue;

            int na = reps[a];
            int nb = reps[b];

            if (g.HasEdge(na, nb))
                continue;

            float d = Vector2.Distance(centers[a], centers[b]);
            if (d > maxDistance)
                continue;

            g.AddEdge(na, nb, EdgeKind.Route);
            added++;
        }
    }

    private static void MarkStartAndFar(MapGraph g)
    {
        if (g.nodes.Count == 0)
            return;

        int left = 0;
        int right = 0;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            float x = g.nodes[i].position.x;
            if (x < minX)
            {
                minX = x;
                left = i;
            }

            if (x > maxX)
            {
                maxX = x;
                right = i;
            }
        }

        g.nodes[left].kind = NodeKind.StartDock;
        g.nodes[right].kind = NodeKind.Destination;
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(s))
                return 0;

            int hash = 23;
            for (int i = 0; i < s.Length; i++)
                hash = hash * 31 + s[i];

            return hash;
        }
    }

    private Vector3 ToWorld(Vector2 p)
    {
        return transform.TransformPoint(new Vector3(p.x, p.y, 0f));
    }
}

[Serializable]
public class MapGraph
{
    public int seed;
    public List<MapNode> nodes = new();
    public List<MapEdge> edges = new();

    private HashSet<ulong> _edgeSet = new();

    public MapGraph(int seed)
    {
        this.seed = seed;
    }

    public int AddNode(MapNode node)
    {
        nodes.Add(node);
        return node.id;
    }

    public void AddEdge(int a, int b, EdgeKind kind)
    {
        if (a == b)
            return;

        if (HasEdge(a, b))
            return;

        edges.Add(new MapEdge { a = a, b = b, kind = kind });
        _edgeSet.Add(Key(a, b));
    }

    public void RebuildEdgeSet()
    {
        _edgeSet ??= new HashSet<ulong>();
        _edgeSet.Clear();

        foreach (MapEdge e in edges)
            _edgeSet.Add(Key(e.a, e.b));
    }

    public bool HasEdge(int a, int b)
    {
        _edgeSet ??= new HashSet<ulong>();

        if (_edgeSet.Count == 0 && edges != null && edges.Count > 0)
            RebuildEdgeSet();

        return _edgeSet.Contains(Key(a, b));
    }

    private static ulong Key(int a, int b)
    {
        uint x = (uint)Mathf.Min(a, b);
        uint y = (uint)Mathf.Max(a, b);
        return ((ulong)x << 32) | y;
    }
}

public enum NodeKind
{
    Island,
    StartDock,
    Destination
}

public enum EdgeKind
{
    Route
}

[Serializable]
public class MapNode
{
    public bool isPrimary;
    public int id;
    public int clusterId;
    public Vector2 position;
    public NodeKind kind;

    [Header("Stable Identity")]
    public string localStableId;

    [Header("Identity")]
    public string displayName;
    public BiomeId biome;

    [Header("Economy")]
    public ResourceId primaryResource;
    public ResourceId secondaryResource;

    [Header("Factions")]
    public FactionId primaryFaction;
    public FactionId secondaryFaction;

    [Header("Settlement Buildings (ratings 0..4)")]
    public BuildingRating dock = new BuildingRating(1f);
    public BuildingRating tradeHub = new BuildingRating(1f);

    public List<OptionalBuilding> optionalBuildings = new();

    [Header("Simulation Stats (drift-capable)")]
    public List<NodeStat> stats = new();

    [Header("Population")]
    public float population = 100f;
    public float minPopulation = 10f;
    public float maxPopulation = 500f;

    [Header("Flags / Notes")]
    public List<string> flags = new();

    [TextArea] public string notes;

    [NonSerialized] public Dictionary<NodeStatId, float> statInfluenceAccel;
    [NonSerialized] public float dockInfluenceAccel;
    [NonSerialized] public float tradeInfluenceAccel;

    public void EnsureInitializedForSim()
    {
        statInfluenceAccel ??= new Dictionary<NodeStatId, float>();
    }

    public float GetOptionalBuildingRating(SettlementBuildingId id)
    {
        foreach (OptionalBuilding b in optionalBuildings)
        {
            if (b.id == id && b.present)
                return b.rating.rating;
        }

        return 0f;
    }
}

[Serializable]
public struct OptionalBuilding
{
    public SettlementBuildingId id;
    public bool present;
    public BuildingRating rating;
}

[Serializable]
public struct NodeStat
{
    public NodeStatId id;
    public SimStat stat;
}

[Serializable]
public struct MapEdge
{
    public int a;
    public int b;
    public EdgeKind kind;
}

public enum ResourceId
{
    None,
    Bananas,
    Uranium,
}

public enum FactionId
{
    None,
    Merchants,
    Pirates,
    Cult,
}

public enum BiomeId
{
    None,
    Reef,
    StormBelt,
    ToxicBloom,
    Ice,
    Graveyard
}

public enum SettlementBuildingId
{
    Dock,
    TradeHub,
    Shipyard,
    Tavern,
    Cartographer,
    HiringHall,
    CustomsOffice,
    Clinic,
    Embassy,
    Warehouse
}

[Serializable]
public struct BuildingRating
{
    [Range(0f, 4f)] public float rating;

    public BuildingRating(float r)
    {
        rating = Mathf.Clamp(r, 0f, 4f);
    }
}

public enum NodeStatId
{
    Prosperity,
    Stability,
    Security,
    DockRating,
    TradeRating,
    FoodBalance
}

[Serializable]
public struct SimStat
{
    [Range(0f, 4f)] public float value;
    public float velocity;
    [Range(0f, 4f)] public float equilibrium;
    [Min(0f)] public float restoreStrength;

    public float minValue;
    public float maxValue;

    public SimStat(float initial, float eq, float restore, float minValue = 0f, float maxValue = 4f)
    {
        this.minValue = minValue;
        this.maxValue = maxValue;

        value = Mathf.Clamp(initial, minValue, maxValue);
        velocity = 0f;
        equilibrium = Mathf.Clamp(eq, minValue, maxValue);
        restoreStrength = Mathf.Max(0f, restore);
    }

    public bool Tick(float dt, float influenceAccel)
    {
        float old = value;

        float toEq = equilibrium - value;
        float eqAccel = toEq * restoreStrength;

        velocity += (eqAccel + influenceAccel) * dt;
        velocity *= Mathf.Clamp01(1f - 0.8f * dt);

        value += velocity * dt;
        value = Mathf.Clamp(value, minValue, maxValue);

        if (value <= minValue && velocity < 0f)
            velocity = 0f;

        if (value >= maxValue && velocity > 0f)
            velocity = 0f;

        return !Mathf.Approximately(old, value);
    }
}
