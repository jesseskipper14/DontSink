using UnityEngine;

/// <summary>Authored civic sockets. Generation places this building; later services populate its sockets.</summary>
[DisallowMultipleComponent]
public sealed class TownCenterBuilding : MonoBehaviour
{
    [SerializeField] private Transform leaderSocket;
    [SerializeField] private Transform statusBoardSocket;
    [Header("Node Leader")]
    [SerializeField] private AgentDefinition leaderDefinition;
    [Tooltip("LeaderSocket is the feet position; NpcBase's root is above its feet.")]
    [SerializeField] private float leaderRootHeight = 1.02f;
    private AgentSpawnPoint leaderSpawn;
    public AgentController Leader => leaderSpawn != null ? leaderSpawn.SpawnedAgent : null;
    public Transform LeaderSocket => leaderSocket;
    public Transform StatusBoardSocket => statusBoardSocket;
    public string NodeStableId { get; private set; }
    public string PlotId { get; private set; }
    public string LeaderStableId => string.IsNullOrWhiteSpace(NodeStableId) ? null : NodeStableId + "/civic/node_leader";
    public string BuildingStableId => string.IsNullOrWhiteSpace(NodeStableId) ? null : NodeStableId + "/civic/town_center";

    public bool HasValidSockets => leaderSocket != null && statusBoardSocket != null && leaderSocket != statusBoardSocket &&
        leaderSocket.IsChildOf(transform) && statusBoardSocket.IsChildOf(transform);

    public void Bind(string nodeId, string plotId)
    {
        if (Leader != null && (NodeStableId != nodeId || PlotId != plotId))
        { Debug.LogError("[Town Center] An existing leader cannot be reassigned by rebinding the building.", this); return; }
        NodeStableId = nodeId;
        PlotId = plotId;
        if (!HasValidSockets) Debug.LogError("[Town Center] Assign distinct authored LeaderSocket and StatusBoardSocket children.", this);
        else
        {
            PopulateLeader();
            var board = statusBoardSocket.GetComponent<TownCenterStatusBoard>();
            if (board == null) board = statusBoardSocket.gameObject.AddComponent<TownCenterStatusBoard>();
            board.Bind(this);
        }
    }

    private void PopulateLeader()
    {
        if (string.IsNullOrWhiteSpace(NodeStableId) || Leader != null) return;
        if (AgentRegistry.TryGet(LeaderStableId, out var existing) && existing != null &&
            existing.gameObject.scene == gameObject.scene && existing.isActiveAndEnabled)
        { Debug.LogWarning("[Town Center] A leader already exists for " + NodeStableId + "; duplicate spawn refused.", this); return; }
        if (leaderDefinition == null) leaderDefinition = Resources.Load<AgentDefinition>("NodeLeaderNpc");
        if (leaderDefinition == null) { Debug.LogError("[Town Center] Missing NodeLeaderNpc definition.", this); return; }
        if (leaderSpawn == null)
        {
            var spawn = new GameObject("LeaderAgentSpawn");
            spawn.transform.SetParent(leaderSocket, false);
            spawn.transform.localPosition = new Vector3(0, leaderRootHeight, 0);
            var box = spawn.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(.25f, .25f);
            var bounds = spawn.AddComponent<AgentHomeBounds>();
            bounds.Configure(box);
            leaderSpawn = spawn.AddComponent<AgentSpawnPoint>();
            // Authored feet placement is authoritative. Do not raycast/snap through a composite quay.
            leaderSpawn.Configure(leaderDefinition, LeaderStableId, NodeStableId, leaderSocket, bounds, false);
        }
        Physics2D.SyncTransforms();
        var leader = leaderSpawn.Spawn();
        if (leader != null)
        {
            var handler = leader.GetComponent<NodeLeaderAgentServiceHandler>();
            if (handler != null) handler.Bind(this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (leaderSocket != null) Gizmos.DrawWireSphere(leaderSocket.position, .25f);
        Gizmos.color = Color.yellow;
        if (statusBoardSocket != null) Gizmos.DrawWireCube(statusBoardSocket.position, new Vector3(1.6f, 1.2f, .1f));
    }

    [ContextMenu("Debug Civic/Seed adjacent reports (manual test contact)")]
    private void DebugSeedAdjacentReports()
    {
        if (!Application.isPlaying || !GameplayAuthority.IsAuthoritative ||
            !NodeAdjacentIntelService.TryGetWorldHours(out double hours))
        { Debug.LogWarning("[Town Center] Manual reports require play mode, authority and a world clock.", this); return; }
        int seeded = 0;
        foreach (string neighbor in NodeAdjacentIntelService.GetNeighborIds(NodeStableId))
        {
            var report = NodeAdjacentIntelService.CaptureManualReport(NodeStableId, neighbor, hours);
            if (report != null && NodeAdjacentIntelService.TryReceive(report, out _)) seeded++;
        }
        Debug.Log("[Town Center] Stored " + seeded + " manual adjacent reports. No map knowledge was granted.", this);
    }
}
