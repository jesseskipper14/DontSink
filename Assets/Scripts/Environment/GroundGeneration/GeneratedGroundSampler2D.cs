using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GeneratedGroundSampler2D : MonoBehaviour
{
    [SerializeField] private EdgeCollider2D edge;

    private Vector2[] worldPoints = Array.Empty<Vector2>();
    private readonly List<IGroundGeneratedNotifier> _notifiers = new();

    private void Awake()
    {
        Refresh();
    }

    private void OnEnable()
    {
        SubscribeToGenerationNotifications();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromGenerationNotifications();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            Refresh();
    }
#endif

    public void Refresh()
    {
        if (edge == null)
            edge = GetComponent<EdgeCollider2D>();

        if (edge == null || edge.pointCount < 2)
        {
            worldPoints = Array.Empty<Vector2>();
            return;
        }

        Vector2[] localPoints = edge.points;
        worldPoints = new Vector2[localPoints.Length];

        for (int i = 0; i < localPoints.Length; i++)
            worldPoints[i] = edge.transform.TransformPoint(localPoints[i]);

        Array.Sort(worldPoints, (a, b) => a.x.CompareTo(b.x));
    }

    public bool TryGetWorldSpan(out float minX, out float maxX)
    {
        EnsureCache();

        if (worldPoints.Length < 2)
        {
            minX = 0f;
            maxX = 0f;
            return false;
        }

        minX = worldPoints[0].x;
        maxX = worldPoints[^1].x;
        return maxX > minX;
    }

    public bool TrySampleGround(
        float worldX,
        out float groundY,
        out float slopeDegrees)
    {
        EnsureCache();

        groundY = 0f;
        slopeDegrees = 0f;

        if (worldPoints.Length < 2)
            return false;

        if (worldX < worldPoints[0].x || worldX > worldPoints[^1].x)
            return false;

        // Ground points are sorted by X. Binary search keeps the player safety
        // guard cheap enough to sample every physics tick without walking all
        // ~300 generated points each time.
        int lo = 0;
        int hi = worldPoints.Length - 2;

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            Vector2 a = worldPoints[mid];
            Vector2 b = worldPoints[mid + 1];

            if (worldX < a.x)
            {
                hi = mid - 1;
                continue;
            }

            if (worldX > b.x)
            {
                lo = mid + 1;
                continue;
            }

            float dx = b.x - a.x;
            if (Mathf.Abs(dx) < 0.0001f)
                return false;

            float t = Mathf.InverseLerp(a.x, b.x, worldX);
            groundY = Mathf.Lerp(a.y, b.y, t);

            Vector2 segment = b - a;
            slopeDegrees = Mathf.Abs(
                Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);

            return true;
        }

        return false;
    }

    private void EnsureCache()
    {
        if (worldPoints.Length < 2)
            Refresh();
    }

    private void SubscribeToGenerationNotifications()
    {
        UnsubscribeFromGenerationNotifications();

        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is not IGroundGeneratedNotifier notifier)
                continue;

            notifier.OnGenerated += HandleGroundGenerated;
            _notifiers.Add(notifier);
        }
    }

    private void UnsubscribeFromGenerationNotifications()
    {
        for (int i = 0; i < _notifiers.Count; i++)
        {
            IGroundGeneratedNotifier notifier = _notifiers[i];
            if (notifier != null)
                notifier.OnGenerated -= HandleGroundGenerated;
        }

        _notifiers.Clear();
    }

    private void HandleGroundGenerated()
    {
        Refresh();
    }
}
