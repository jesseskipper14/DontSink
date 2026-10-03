using System;
using UnityEngine;

/// <summary>
/// Session-only signed distance through a voyage, independent of geographic heading.
/// Owned and observed by the piloting simulation, never by a camera or debug window.
/// </summary>
public sealed class BoatVoyageStripState
{
    private object _voyage;
    private object _body;
    public bool IsActive => _voyage != null;
    public double Position { get; private set; }
    public float LastTravelDelta { get; private set; }
    public double LocalOriginCoordinate { get; private set; }
    public Vector2 SceneAxis { get; private set; } = Vector2.right;
    public int Seed { get; private set; }
    public int Revision { get; private set; }
    public long SampleCount { get; private set; }
    public int RebaseCount { get; private set; }

    // Called once, immediately after the existing physical displacement sample.
    // Context changes establish a baseline rather than treating spawn/restore as travel.
    public void Observe(object voyage, object body, Vector2 localPosition, Vector2 sceneAxis,
        float signedDelta, int seed, bool authoritative)
    {
        if (!authoritative) return;
        if (voyage == null || (body == null && !ReferenceEquals(_voyage, voyage)))
        {
            _voyage = _body = null;
            Position = 0;
            LastTravelDelta = 0;
            LocalOriginCoordinate = 0;
            SampleCount = 0;
            RebaseCount = 0;
            Seed = 0;
            return;
        }
        if (body == null)
        {
            _body = null;
            LastTravelDelta = 0;
            return;
        }
        if (!Finite(localPosition.x) || !Finite(localPosition.y) ||
            !Finite(sceneAxis.x) || !Finite(sceneAxis.y) || !Finite(signedDelta)) return;
        sceneAxis = sceneAxis.sqrMagnitude > .000001f ? sceneAxis.normalized : Vector2.right;
        if (!ReferenceEquals(_voyage, voyage))
        {
            _voyage = voyage;
            Position = 0;
            SampleCount = 0;
            RebaseCount = 0;
            Seed = seed;
            Revision++;
            _body = body;
            SceneAxis = sceneAxis;
            LocalOriginCoordinate = Coordinate(localPosition);
            LastTravelDelta = 0;
            return;
        }
        if (!ReferenceEquals(_body, body) || sceneAxis != SceneAxis)
        {
            _body = body;
            SceneAxis = sceneAxis;
            Rebase(localPosition);
            return;
        }
        Position += signedDelta;
        LastTravelDelta = signedDelta;
        SampleCount++;
    }

    // Explicit physical teleport/resume: keep journey history and remap the local origin.
    // Geographic-only debug warps never call this.
    public void Rebase(Vector2 localPosition)
    {
        if (!IsActive || !Finite(localPosition.x) || !Finite(localPosition.y)) return;
        LocalOriginCoordinate = Coordinate(localPosition) - Position;
        LastTravelDelta = 0;
        RebaseCount++;
    }

    public double LocalToStrip(Vector2 localPosition) => Coordinate(localPosition) - LocalOriginCoordinate;
    public double StripToLocalCoordinate(double stripPosition) => LocalOriginCoordinate + stripPosition;
    private double Coordinate(Vector2 localPosition) =>
        (double)localPosition.x * SceneAxis.x + (double)localPosition.y * SceneAxis.y;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
