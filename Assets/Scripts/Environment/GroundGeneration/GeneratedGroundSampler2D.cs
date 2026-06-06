using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GeneratedGroundSampler2D : MonoBehaviour
{
    [SerializeField] private EdgeCollider2D edge;

    private Vector2[] worldPoints = Array.Empty<Vector2>();

    private void Awake()
    {
        Refresh();
    }

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
        Refresh();

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
        Refresh();

        groundY = 0f;
        slopeDegrees = 0f;

        if (worldPoints.Length < 2)
            return false;

        if (worldX < worldPoints[0].x || worldX > worldPoints[^1].x)
            return false;

        for (int i = 0; i < worldPoints.Length - 1; i++)
        {
            Vector2 a = worldPoints[i];
            Vector2 b = worldPoints[i + 1];

            if (worldX < a.x || worldX > b.x)
                continue;

            float dx = b.x - a.x;
            if (Mathf.Abs(dx) < 0.0001f)
                continue;

            float t = Mathf.InverseLerp(a.x, b.x, worldX);
            groundY = Mathf.Lerp(a.y, b.y, t);

            Vector2 segment = b - a;
            slopeDegrees = Mathf.Abs(Mathf.Atan2(segment.y, segment.x) * Mathf.Rad2Deg);

            return true;
        }

        return false;
    }
}