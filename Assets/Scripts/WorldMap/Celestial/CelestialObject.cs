using System;
using UnityEngine;

public enum CelestialObjectKind
{
    AmbientStar = 0,
    LandmarkStar = 1,
    Nebula = 2,
    DeepSkyObject = 3
}

public enum CelestialColorClass
{
    White = 0,
    BlueWhite = 1,
    Gold = 2,
    Orange = 3,
    Red = 4,
    Cyan = 5,
    Violet = 6
}

/// <summary>
/// Deterministic celestial world truth. This is not player knowledge and is not
/// intended to be mutated by charting, naming, or UI systems.
/// </summary>
[Serializable]
public sealed class CelestialObject
{
    [SerializeField] private string stableId;
    [SerializeField] private CelestialObjectKind kind;
    [SerializeField] private Vector2 worldPosition;
    [SerializeField, Range(0f, 1f)] private float brightness01;
    [SerializeField, Range(0f, 1f)] private float prominence01;
    [SerializeField] private CelestialColorClass colorClass;
    [SerializeField] private int visualVariant;
    [SerializeField] private float rotationDegrees;
    [SerializeField, Min(0f)] private float footprintRadiusWorld;

    [NonSerialized] private int cellX;
    [NonSerialized] private int cellY;
    [NonSerialized] private int localIndex;

    public string StableId => stableId;
    public CelestialObjectKind Kind => kind;
    public Vector2 WorldPosition => worldPosition;
    public float Brightness01 => brightness01;
    public float Prominence01 => prominence01;
    public CelestialColorClass ColorClass => colorClass;
    public int VisualVariant => visualVariant;
    public float RotationDegrees => rotationDegrees;
    public float FootprintRadiusWorld => footprintRadiusWorld;

    public int CellX => cellX;
    public int CellY => cellY;
    public int LocalIndex => localIndex;

    internal CelestialObject(
        string stableId,
        CelestialObjectKind kind,
        Vector2 worldPosition,
        float brightness01,
        float prominence01,
        CelestialColorClass colorClass,
        int visualVariant,
        float rotationDegrees,
        float footprintRadiusWorld,
        int cellX,
        int cellY,
        int localIndex)
    {
        this.stableId = stableId;
        this.kind = kind;
        this.worldPosition = worldPosition;
        this.brightness01 = Mathf.Clamp01(brightness01);
        this.prominence01 = Mathf.Clamp01(prominence01);
        this.colorClass = colorClass;
        this.visualVariant = Mathf.Max(0, visualVariant);
        this.rotationDegrees = Mathf.Repeat(rotationDegrees, 360f);
        this.footprintRadiusWorld = Mathf.Max(0f, footprintRadiusWorld);
        this.cellX = cellX;
        this.cellY = cellY;
        this.localIndex = localIndex;
    }

    public bool IntersectsWorldRect(Rect worldRect)
    {
        if (footprintRadiusWorld <= 0f)
            return worldRect.Contains(worldPosition);

        float closestX = Mathf.Clamp(worldPosition.x, worldRect.xMin, worldRect.xMax);
        float closestY = Mathf.Clamp(worldPosition.y, worldRect.yMin, worldRect.yMax);

        float dx = worldPosition.x - closestX;
        float dy = worldPosition.y - closestY;
        float radiusSqr = footprintRadiusWorld * footprintRadiusWorld;

        return dx * dx + dy * dy <= radiusSqr;
    }
}
