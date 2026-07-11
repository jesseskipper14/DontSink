using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AgentSpawnZone : MonoBehaviour
{
    [Header("Domain")]
    [SerializeField] private AgentSpawnDomain domain = AgentSpawnDomain.Underwater;

    [Tooltip("Optional tags like ShallowWater, DeepWater, Vendor, Ruins, Perch, etc.")]
    [SerializeField] private string[] tags;

    [Header("Shape")]
    [SerializeField] private Vector2 localSize = new Vector2(6f, 3f);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.25f);

    public AgentSpawnDomain Domain => domain;
    public IReadOnlyList<string> Tags => tags;

    public Vector2 Center => transform.position;
    public Vector2 LocalSize => localSize;

    public bool Matches(
        AgentSpawnDomain requestedDomain,
        string[] requiredTags,
        bool requireAllTags)
    {
        if (requestedDomain != AgentSpawnDomain.Any &&
            domain != requestedDomain)
        {
            return false;
        }

        if (requiredTags == null || requiredTags.Length == 0)
            return true;

        if (tags == null || tags.Length == 0)
            return false;

        if (requireAllTags)
        {
            for (int i = 0; i < requiredTags.Length; i++)
            {
                if (!HasTag(requiredTags[i]))
                    return false;
            }

            return true;
        }

        for (int i = 0; i < requiredTags.Length; i++)
        {
            if (HasTag(requiredTags[i]))
                return true;
        }

        return false;
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tags == null)
            return false;

        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public Vector2 GetRandomPoint(System.Random rng)
    {
        rng ??= new System.Random();

        float x = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0.01f, localSize.x);
        float y = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0.01f, localSize.y);

        Vector3 world = transform.TransformPoint(new Vector3(x, y, 0f));
        return world;
    }

    public Vector2 ClampWorldPoint(Vector2 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        float halfX = Mathf.Max(0.005f, localSize.x * 0.5f);
        float halfY = Mathf.Max(0.005f, localSize.y * 0.5f);

        local.x = Mathf.Clamp(local.x, -halfX, halfX);
        local.y = Mathf.Clamp(local.y, -halfY, halfY);

        return transform.TransformPoint(local);
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);

        float halfX = Mathf.Max(0.005f, localSize.x * 0.5f);
        float halfY = Mathf.Max(0.005f, localSize.y * 0.5f);

        return local.x >= -halfX &&
               local.x <= halfX &&
               local.y >= -halfY &&
               local.y <= halfY;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Color old = Gizmos.color;
        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawCube(Vector3.zero, new Vector3(localSize.x, localSize.y, 0.05f));

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.85f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(localSize.x, localSize.y, 0.05f));

        Gizmos.matrix = oldMatrix;
        Gizmos.color = old;
    }
}