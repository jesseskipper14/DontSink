using System;
using System.Collections.Generic;
using UnityEngine;

public class WorldMapGraphGenerator : MonoBehaviour
{
    [Header("Generation")]
    [Min(1)] public int seed = 12345;

    [Tooltip("How many clusters (regions) in the world map.")]
    [Range(1, 50)] public int clusterCount = 10;

    [Tooltip("Nodes per cluster: inclusive min/max.")]
    [Range(1, 20)] public int nodesPerClusterMin = 4;
    [Range(1, 20)] public int nodesPerClusterMax = 6;

    [Header("Layout")]
    [Tooltip("Distance between cluster centers.")]
    [Min(1f)] public float clusterSpacing = 12f;

    [Tooltip("Radius of nodes around each cluster center.")]
    [Min(0.1f)] public float clusterRadius = 3.5f;

    [Tooltip("Jitter applied to each node position.")]
    [Min(0f)] public float nodeJitter = 0.6f;

    [Header("Connectivity")]
    [Tooltip("Extra connections within each cluster (in addition to a spanning tree).")]
    [Range(0, 10)] public int extraEdgesPerCluster = 2;

    [Tooltip("How many links between clusters (in addition to a spanning tree across clusters).")]
    [Range(0, 50)] public int extraInterClusterEdges = 6;

    [Tooltip("Max distance allowed for an inter-cluster edge (in cluster steps).")]
    [Range(1, 10)] public int interClusterNeighborRange = 3;

    [Header("Debug / Viz")]
    public bool autoRegenerate = false;
    public bool drawGizmos = true;
    public bool drawNodeLabels = true;

    [Min(0.05f)] public float nodeGizmoRadius = 0.25f;

    [NonSerialized] public MapGraph graph;

    private void OnValidate()
    {
        nodesPerClusterMin = Mathf.Max(1, nodesPerClusterMin);
        nodesPerClusterMax = Mathf.Max(nodesPerClusterMin, nodesPerClusterMax);

        if (autoRegenerate)
            Generate();
    }

    [ContextMenu("Generate Map Graph")]
    public void Generate()
    {
        var rng = new System.Random(seed);
        graph = new MapGraph(seed);

        // 1) Create cluster centers in a loose ring/spiral so it looks like a “world”
        var clusterCenters = GenerateClusterCenters(rng, clusterCount, clusterSpacing);

        // 2) Create nodes per cluster and position them around the center
        var clusterNodes = new List<List<int>>(clusterCount);

        for (int c = 0; c < clusterCount; c++)
        {
            int n = rng.Next(nodesPerClusterMin, nodesPerClusterMax + 1);
            clusterNodes.Add(new List<int>(n));

            for (int i = 0; i < n; i++)
            {
                var pos = ScatterInCluster(rng, clusterCenters[c], clusterRadius, nodeJitter);

                int nodeId = graph.AddNode(new MapNode
                {
                    id = graph.nodes.Count,
                    clusterId = c,
                    position = pos,
                    kind = NodeKind.Island
                });

                clusterNodes[c].Add(nodeId);
            }
        }

        // 3) Ensure each cluster is internally connected (spanning tree)
        for (int c = 0; c < clusterCount; c++)
        {
            MakeSpanningTree(rng, graph, clusterNodes[c]);

            // Add extra intra-cluster edges for loops/choices
            AddExtraEdges(rng, graph, clusterNodes[c], extraEdgesPerCluster);
        }

        // 4) Connect clusters together (spanning tree across clusters)
        var clusterRep = new int[clusterCount];
        for (int c = 0; c < clusterCount; c++)
        {
            // Representative node for the cluster (pick the one nearest its center)
            clusterRep[c] = FindNearestNodeToPoint(graph, clusterNodes[c], clusterCenters[c]);
            graph.nodes[clusterRep[c]].isPrimary = true;
        }

        MakeClusterSpanningTree(rng, graph, clusterCenters, clusterRep);

        // 5) Add extra inter-cluster edges for a richer world map
        AddExtraInterClusterEdges(rng, graph, clusterCenters, clusterRep, extraInterClusterEdges, interClusterNeighborRange);

        // 6) Optional: mark one node as start for now (left-most), one as “far” (right-most)
        MarkStartAndFar(graph);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private static List<Vector2> GenerateClusterCenters(System.Random rng, int count, float spacing)
    {
        var centers = new List<Vector2>(count);

        // Spiral-ish ring: angle increases, radius increases slightly
        float baseRadius = spacing * 0.75f;
        for (int i = 0; i < count; i++)
        {
            float t = (count <= 1) ? 0f : (i / (float)(count - 1));
            float angle = t * Mathf.PI * 2f + (float)rng.NextDouble() * 0.35f;
            float radius = baseRadius + t * spacing * 0.8f + (float)rng.NextDouble() * spacing * 0.25f;

            var p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            centers.Add(p);
        }

        // Small global jitter so it’s less “mathy”
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
        // Uniform-ish disk scatter
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;

        var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

        p += new Vector2(
            ((float)rng.NextDouble() - 0.5f) * jitter,
            ((float)rng.NextDouble() - 0.5f) * jitter
        );

        return p;
    }

    private static void MakeSpanningTree(System.Random rng, MapGraph g, List<int> nodeIds)
    {
        if (nodeIds.Count <= 1) return;

        // Simple random tree: connect each node (except first) to a prior node
        for (int i = 1; i < nodeIds.Count; i++)
        {
            int a = nodeIds[i];
            int b = nodeIds[rng.Next(0, i)];
            g.AddEdge(a, b, EdgeKind.Route);
        }
    }

    private static void AddExtraEdges(System.Random rng, MapGraph g, List<int> nodeIds, int extraEdges)
    {
        if (nodeIds.Count <= 2 || extraEdges <= 0) return;

        int tries = extraEdges * 10;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = nodeIds[rng.Next(nodeIds.Count)];
            int b = nodeIds[rng.Next(nodeIds.Count)];
            if (a == b) continue;

            if (g.HasEdge(a, b)) continue;

            // Bias towards closer nodes for nicer local loops
            float d = Vector2.Distance(g.nodes[a].position, g.nodes[b].position);
            if (d > 6.0f && rng.NextDouble() < 0.7) continue;

            g.AddEdge(a, b, EdgeKind.Route);
            added++;
        }
    }

    private static int FindNearestNodeToPoint(MapGraph g, List<int> nodeIds, Vector2 point)
    {
        int best = nodeIds[0];
        float bestD = float.PositiveInfinity;

        foreach (var id in nodeIds)
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

    private static void MakeClusterSpanningTree(System.Random rng, MapGraph g, List<Vector2> centers, int[] reps)
    {
        int n = reps.Length;
        if (n <= 1) return;

        // Build a spanning tree across clusters using a “connect to nearest prior cluster” heuristic
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

    private static void AddExtraInterClusterEdges(System.Random rng, MapGraph g, List<Vector2> centers, int[] reps, int extraEdges, int neighborRange)
    {
        int n = reps.Length;
        if (n <= 2 || extraEdges <= 0) return;

        int tries = extraEdges * 20;
        int added = 0;

        while (tries-- > 0 && added < extraEdges)
        {
            int a = rng.Next(0, n);
            int b = rng.Next(0, n);
            if (a == b) continue;

            // Keep edges somewhat local in “cluster space”
            // Using index distance is a cheap proxy for “near” in the spiral ordering.
            if (Mathf.Abs(a - b) > neighborRange && rng.NextDouble() < 0.85) continue;

            int na = reps[a];
            int nb = reps[b];

            if (g.HasEdge(na, nb)) continue;

            // Bias against very long connections in world space too
            float worldD = Vector2.Distance(centers[a], centers[b]);
            if (worldD > neighborRange * 1.2f * 12f && rng.NextDouble() < 0.85) continue;

            g.AddEdge(na, nb, EdgeKind.Route);
            added++;
        }
    }

    private static void MarkStartAndFar(MapGraph g)
    {
        if (g.nodes.Count == 0) return;

        int left = 0;
        int right = 0;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < g.nodes.Count; i++)
        {
            float x = g.nodes[i].position.x;
            if (x < minX) { minX = x; left = i; }
            if (x > maxX) { maxX = x; right = i; }
        }

        g.nodes[left].kind = NodeKind.StartDock;
        g.nodes[right].kind = NodeKind.Destination;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || graph == null) return;

        // Edges
        Gizmos.color = Color.white;
        foreach (var e in graph.edges)
        {
            var a = graph.nodes[e.a].position;
            var b = graph.nodes[e.b].position;
            Gizmos.DrawLine(ToWorld(a), ToWorld(b));
        }

        // Nodes
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            var n = graph.nodes[i];

            if (n.isPrimary)
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f); // orange-ish
            else
                Gizmos.color = n.kind switch
                {
                    NodeKind.StartDock => new Color(0.2f, 1f, 0.3f, 1f),
                    NodeKind.Destination => new Color(1f, 0.4f, 0.2f, 1f),
                    _ => new Color(0.9f, 0.9f, 0.9f, 1f)
                };

            Gizmos.DrawSphere(ToWorld(n.position), nodeGizmoRadius);
        }


#if UNITY_EDITOR
        if (drawNodeLabels)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                var n = graph.nodes[i];
                UnityEditor.Handles.Label(ToWorld(n.position) + Vector3.up * 0.35f,
                    $"#{n.id} C{n.clusterId}\n{n.kind}");
            }
        }
#endif
    }

    private Vector3 ToWorld(Vector2 p) => transform.TransformPoint(new Vector3(p.x, p.y, 0f));
}

[Serializable]
public class MapGraph
{
    public int seed;
    public List<MapNode> nodes = new();
    public List<MapEdge> edges = new();

    // quick adjacency set for edge existence checks
    private HashSet<ulong> _edgeSet = new();

    public MapGraph(int seed) { this.seed = seed; }

    public int AddNode(MapNode node)
    {
        nodes.Add(node);
        return node.id;
    }

    public void AddEdge(int a, int b, EdgeKind kind)
    {
        if (a == b) return;
        if (HasEdge(a, b)) return;

        edges.Add(new MapEdge { a = a, b = b, kind = kind });
        _edgeSet.Add(Key(a, b));
    }

    public bool HasEdge(int a, int b) => _edgeSet.Contains(Key(a, b));

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
}

[Serializable]
public struct MapEdge
{
    public int a;
    public int b;
    public EdgeKind kind;
}
