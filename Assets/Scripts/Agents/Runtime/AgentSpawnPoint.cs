using UnityEngine;

[DisallowMultipleComponent]
public class AgentSpawnPoint : MonoBehaviour
{
    [Header("Spawn Identity")]
    [SerializeField] private string stableId = "agent_spawn_new";
    [SerializeField] private string nodeId;

    [Header("Agent")]
    [SerializeField] private AgentDefinition definition;
    [SerializeField] private GameObject prefabOverride;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private AgentHomeBounds homeBounds;

    [Header("Ground Snap")]
    [SerializeField] private bool snapToGroundAfterSpawn = true;

    private AgentController spawnedAgent;

    public string StableId => stableId;
    public AgentController SpawnedAgent => spawnedAgent;

    private void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    public AgentController Spawn()
    {
        if (spawnedAgent != null)
            return spawnedAgent;

        if (definition == null)
        {
            Debug.LogWarning($"AgentSpawnPoint '{name}' has no AgentDefinition.", this);
            return null;
        }

        GameObject prefab = prefabOverride != null ? prefabOverride : definition.Prefab;

        if (prefab == null)
        {
            Debug.LogWarning($"AgentSpawnPoint '{name}' has no prefab. Assign prefabOverride or AgentDefinition.prefab.", this);
            return null;
        }

        Transform parent = spawnParent != null ? spawnParent : transform.parent;
        GameObject instance = Instantiate(prefab, transform.position, transform.rotation, parent);
        instance.name = $"{definition.DisplayName} ({stableId})";

        spawnedAgent = instance.GetComponent<AgentController>();

        if (spawnedAgent == null)
        {
            Debug.LogError($"Spawned prefab '{prefab.name}' does not have AgentController.", instance);
            Destroy(instance);
            return null;
        }

        spawnedAgent.Initialize(definition, stableId, nodeId, homeBounds);

        if (snapToGroundAfterSpawn)
        {
            AgentGroundSnapper snapper = spawnedAgent.GetComponent<AgentGroundSnapper>();
            if (snapper != null)
                snapper.TrySnapToGround();
        }

        return spawnedAgent;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}