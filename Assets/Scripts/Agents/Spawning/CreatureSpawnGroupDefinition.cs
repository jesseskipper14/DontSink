using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Agents/Spawning/Creature Spawn Group", fileName = "CreatureSpawnGroup")]
public sealed class CreatureSpawnGroupDefinition : AgentSpawnGroupDefinition
{
    [Header("Creature")]
    [SerializeField] private GameObject creaturePrefab;
    [SerializeField] private CreatureSchoolProfile schoolProfile;

    [Header("Spawn Zone Query")]
    [SerializeField] private AgentSpawnDomain domain = AgentSpawnDomain.Underwater;
    [SerializeField] private string[] requiredZoneTags;
    [SerializeField] private bool requireAllTags = true;

    [Header("Group Count")]
    [Tooltip("How many separate groups/schools this spawn group creates.")]
    [SerializeField, Min(0)] private int minGroups = 1;

    [SerializeField, Min(0)] private int maxGroups = 3;

    [Header("Fish Count Per Group")]
    [SerializeField, Min(0)] private int minCountPerGroup = 4;
    [SerializeField, Min(0)] private int maxCountPerGroup = 9;

    [Header("Grouping")]
    [SerializeField] private AgentSpawnGroupMode groupMode = AgentSpawnGroupMode.School;

    [Tooltip("If true, each school gets its own root object.")]
    [SerializeField] private bool createSchoolController = true;

    [Header("Placement")]
    [Tooltip("If true, each group can pick a different matching spawn zone.")]
    [SerializeField] private bool chooseZonePerGroup = true;

    [Tooltip("Minimum distance between spawned school anchors when possible.")]
    [SerializeField, Min(0f)] private float minGroupAnchorDistance = 3f;

    [Tooltip("Attempts to find a group anchor far enough away from previous groups.")]
    [SerializeField, Min(1)] private int groupAnchorPlacementAttempts = 12;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public override void Spawn(AgentSpawnContext context)
    {
        if (context == null)
            return;

        if (creaturePrefab == null)
        {
            Log("Missing creature prefab.");
            return;
        }

        if (schoolProfile == null)
        {
            Log("Missing school profile.");
            return;
        }

        List<AgentSpawnZone> zones = FindMatchingZones();
        if (zones.Count == 0)
        {
            Log($"No matching spawn zones for domain={domain}.");
            return;
        }

        int groupCount = context.NextIntInclusive(
            Mathf.Min(minGroups, maxGroups),
            Mathf.Max(minGroups, maxGroups));

        if (groupCount <= 0)
        {
            Log("Group count rolled zero. Nothing spawned.");
            return;
        }

        List<Vector2> usedAnchors = new();

        AgentSpawnZone fixedZone = zones[context.NextIntInclusive(0, zones.Count - 1)];

        int totalSpawned = 0;

        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            AgentSpawnZone zone = chooseZonePerGroup
                ? zones[context.NextIntInclusive(0, zones.Count - 1)]
                : fixedZone;

            int count = context.NextIntInclusive(
                Mathf.Min(minCountPerGroup, maxCountPerGroup),
                Mathf.Max(minCountPerGroup, maxCountPerGroup));

            if (count <= 0)
                continue;

            Vector2 anchor = ChooseGroupAnchor(context, zone, usedAnchors);
            usedAnchors.Add(anchor);

            if (groupMode == AgentSpawnGroupMode.School)
                SpawnSchool(context, zone, groupIndex, count, anchor);
            else
                SpawnIndividuals(context, zone, groupIndex, count, anchor);

            totalSpawned += count;
        }

        Log(
            $"Spawn complete. groups={groupCount}, totalSpawned={totalSpawned}, " +
            $"zones={zones.Count}, prefab='{creaturePrefab.name}'.");
    }

    private void SpawnSchool(
        AgentSpawnContext context,
        AgentSpawnZone zone,
        int groupIndex,
        int count,
        Vector2 anchor)
    {
        FishSchoolController school = null;

        if (createSchoolController)
        {
            GameObject schoolObj = new GameObject($"{creaturePrefab.name}_School_{groupIndex:00}");
            schoolObj.transform.SetParent(context.SpawnedRoot, false);
            schoolObj.transform.position = anchor;

            school = schoolObj.AddComponent<FishSchoolController>();
            school.Initialize(schoolProfile, zone, context.NextSeed());
            schoolObj.AddComponent<FishSchoolSimulationLod>();
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPoint =
                anchor + RandomInsideCircle(context.Rng, schoolProfile.SchoolRadius);

            spawnPoint = zone.ClampWorldPoint(spawnPoint);

            GameObject spawned = Object.Instantiate(
                creaturePrefab,
                spawnPoint,
                Quaternion.identity,
                context.SpawnedRoot);

            spawned.name = $"{creaturePrefab.name}_School_{groupIndex:00}_Fish_{i:00}";

            FishSchoolMember2D member =
                spawned.GetComponent<FishSchoolMember2D>() ??
                spawned.AddComponent<FishSchoolMember2D>();

            Vector2 offset = RandomInsideCircle(context.Rng, schoolProfile.SchoolRadius);

            if (school != null)
            {
                member.JoinSchool(
                    school,
                    schoolProfile,
                    offset,
                    context.NextSeed());
            }
            else
            {
                member.InitializeSolo(
                    schoolProfile,
                    zone,
                    context.NextSeed());
            }
        }

        Log(
            $"Spawned school group={groupIndex} count={count} " +
            $"zone='{zone.name}' anchor={anchor}.");
    }

    private void SpawnIndividuals(
        AgentSpawnContext context,
        AgentSpawnZone zone,
        int groupIndex,
        int count,
        Vector2 anchor)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spawnPoint =
                anchor + RandomInsideCircle(context.Rng, schoolProfile.SchoolRadius);

            spawnPoint = zone.ClampWorldPoint(spawnPoint);

            GameObject spawned = Object.Instantiate(
                creaturePrefab,
                spawnPoint,
                Quaternion.identity,
                context.SpawnedRoot);

            spawned.name = $"{creaturePrefab.name}_Group_{groupIndex:00}_Solo_{i:00}";

            FishSchoolMember2D member =
                spawned.GetComponent<FishSchoolMember2D>() ??
                spawned.AddComponent<FishSchoolMember2D>();

            member.InitializeSolo(
                schoolProfile,
                zone,
                context.NextSeed());
        }

        Log(
            $"Spawned individual group={groupIndex} count={count} " +
            $"zone='{zone.name}' anchor={anchor}.");
    }

    private Vector2 ChooseGroupAnchor(
        AgentSpawnContext context,
        AgentSpawnZone zone,
        List<Vector2> usedAnchors)
    {
        Vector2 best = zone.GetRandomPoint(context.Rng);

        if (usedAnchors == null || usedAnchors.Count == 0 || minGroupAnchorDistance <= 0f)
            return best;

        float bestScore = -1f;

        int attempts = Mathf.Max(1, groupAnchorPlacementAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 candidate = zone.GetRandomPoint(context.Rng);

            float nearest = float.PositiveInfinity;

            for (int i = 0; i < usedAnchors.Count; i++)
            {
                float d = Vector2.Distance(candidate, usedAnchors[i]);
                if (d < nearest)
                    nearest = d;
            }

            if (nearest >= minGroupAnchorDistance)
                return candidate;

            if (nearest > bestScore)
            {
                bestScore = nearest;
                best = candidate;
            }
        }

        return best;
    }

    private List<AgentSpawnZone> FindMatchingZones()
    {
        List<AgentSpawnZone> result = new();

        AgentSpawnZone[] zones = Object.FindObjectsByType<AgentSpawnZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        if (zones == null)
            return result;

        for (int i = 0; i < zones.Length; i++)
        {
            AgentSpawnZone zone = zones[i];
            if (zone == null)
                continue;

            if (!zone.Matches(domain, requiredZoneTags, requireAllTags))
                continue;

            result.Add(zone);
        }

        return result;
    }

    private static Vector2 RandomInsideCircle(System.Random rng, float radius)
    {
        rng ??= new System.Random();

        float angle = Mathf.Lerp(0f, Mathf.PI * 2f, (float)rng.NextDouble());
        float r = Mathf.Sqrt((float)rng.NextDouble()) * Mathf.Max(0f, radius);

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[CreatureSpawnGroupDefinition:{name}] {message}", this);
    }
}