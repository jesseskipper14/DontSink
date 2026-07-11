using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AgentSceneSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearPreviousSpawnedAgents = true;

    [Tooltip("Stable-ish extra salt so multiple AgentSceneSpawners in same scene don't spawn identically.")]
    [SerializeField] private int seedOffset;

    [SerializeField] private AgentSpawnGroupDefinition[] spawnGroups;

    [Header("Hierarchy")]
    [SerializeField] private Transform spawnedRoot;

    [Header("Authority")]
    [SerializeField]
    private GameplayAuthorityMode authorityMode =
    GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [SerializeField] private bool logSkippedForAuthority = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool hasSpawned;

    private void Awake()
    {
        if (spawnedRoot == null)
        {
            GameObject root = new GameObject("_SpawnedAgents");
            root.transform.SetParent(transform, false);
            spawnedRoot = root.transform;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
            SpawnAll();
    }

    [ContextMenu("Spawn All Agents")]
    public void SpawnAll()
    {
        if (hasSpawned && !clearPreviousSpawnedAgents)
        {
            Log("SpawnAll ignored because this spawner has already spawned.");
            return;
        }

        if (!GameplayAuthority.CanRun(authorityMode))
        {
            if (logSkippedForAuthority)
            {
                Debug.Log(
                    $"[AgentSceneSpawner] Skipped spawning in '{gameObject.scene.name}' because authorityMode={authorityMode} and IsAuthoritative={GameplayAuthority.IsAuthoritative}.",
                    this);
            }

            return;
        }

        if (clearPreviousSpawnedAgents)
            ClearSpawnedAgents();

        AgentSpawnContext context = BuildContext();

        List<AgentSpawnGroupDefinition> groups = BuildSortedGroups();

        Log($"Spawning {groups.Count} group(s). seed={context.Seed} scene='{context.SceneName}'");

        for (int i = 0; i < groups.Count; i++)
        {
            AgentSpawnGroupDefinition group = groups[i];
            if (group == null || !group.Enabled)
                continue;

            try
            {
                Log($"Running spawn group '{group.DisplayName}' priority={group.Priority}");
                group.Spawn(context);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[AgentSceneSpawner] Spawn group '{group.DisplayName}' failed: {ex}",
                    this);
            }
        }

        hasSpawned = true;
    }

    [ContextMenu("Clear Spawned Agents")]
    public void ClearSpawnedAgents()
    {
        if (spawnedRoot == null)
            return;

        for (int i = spawnedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = spawnedRoot.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        hasSpawned = false;
    }

    private AgentSpawnContext BuildContext()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        int seed = StableHash(sceneName) ^ seedOffset;

        return new AgentSpawnContext(
            sceneName,
            seed,
            transform,
            spawnedRoot);
    }

    private List<AgentSpawnGroupDefinition> BuildSortedGroups()
    {
        List<AgentSpawnGroupDefinition> result = new();

        if (spawnGroups != null)
        {
            for (int i = 0; i < spawnGroups.Length; i++)
            {
                if (spawnGroups[i] != null)
                    result.Add(spawnGroups[i]);
            }
        }

        result.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        return result;
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 23;

            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
            }

            return hash;
        }
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[AgentSceneSpawner] {message}", this);
    }
}