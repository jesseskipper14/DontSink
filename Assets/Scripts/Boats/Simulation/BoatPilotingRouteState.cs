using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime piloting route guidance state.
///
/// This is not UI state. It owns the generated recommended route,
/// cumulative arc-length progress, and nearest-route diagnostics.
///
/// Prototype assumptions:
/// - Navigation +Y is roughly route-forward.
/// - The generated route remains within a bounded heading from +Y.
/// - Progress only advances while the boat remains inside the configured
///   route corridor. Moving backward does not erase earned progress.
///
/// Later, celestial navigation / chart uncertainty can decide whether this
/// route remains visible or trustworthy. The route itself does not imply
/// that the player magically knows north.
/// </summary>
[Serializable]
public sealed class BoatPilotingRouteState
{
    [Serializable]
    public struct RoutePoint
    {
        public Vector2 position;
        public float cumulativeDistance;
        public float headingDegrees;

        public RoutePoint(
            Vector2 position,
            float cumulativeDistance,
            float headingDegrees)
        {
            this.position = position;
            this.cumulativeDistance =
                cumulativeDistance;
            this.headingDegrees =
                headingDegrees;
        }
    }

    private readonly List<RoutePoint> _points =
        new List<RoutePoint>();

    private System.Random _random;

    private float _segmentLength;
    private float _maxHeadingDegrees;
    private float _progressCorridor;

    // The visible wake/path is the actual adherence scoring corridor.
    // This is intentionally much narrower than the broader progress corridor.
    private float _adherencePathWorldWidth;
    private float _recentCourseQualityDistance;
    private float _courseNeutralQuality;

    private bool _hasCourseQualitySamples;
    private float _weightedAdherenceDistance;
    private float _distanceInsideAdherencePath;

    private float _generationHeading;
    private float _generationHeadingVelocity;

    private bool _initialized;

    public IReadOnlyList<RoutePoint> Points =>
        _points;

    public bool IsInitialized =>
        _initialized &&
        _points.Count >= 2;

    /// <summary>
    /// Current position projected onto the route arc.
    /// This can move forward OR backward and is the value that should
    /// eventually participate in destination-arrival logic.
    /// </summary>
    public float CurrentRouteDistance { get; private set; }

    /// <summary>
    /// Furthest valid route distance ever reached while inside the broader
    /// route-tracking corridor. Useful for diagnostics/stats, not arrival.
    /// </summary>
    public float FarthestRouteDistance { get; private set; }

    // Backward-compatible aliases while other project code migrates.
    [Obsolete("Use FarthestRouteDistance. ProgressDistance meant furthest-ever progress, not current journey position.")]
    public float ProgressDistance =>
        FarthestRouteDistance;

    [Obsolete("Use CurrentRouteDistance.")]
    public float ProjectedDistance =>
        CurrentRouteDistance;

    public float OffRouteDistance { get; private set; }
    public bool IsInsideProgressCorridor { get; private set; }

    // Phase 1 course-performance telemetry.
    // These values do NOT yet modify handling, fuel, weather, or propulsion.
    public float CurrentAdherence01 { get; private set; }
    public float RecentCourseQuality01 { get; private set; }
    public float CourseInstability01 { get; private set; }
    public float HandlingEfficiency01 { get; private set; }

    public float PhysicalTravelDistance { get; private set; }
    public float ForwardPhysicalTravelDistance { get; private set; }

    public bool HasCourseQualitySamples =>
        _hasCourseQualitySamples;

    public float ProgressCorridor =>
        _progressCorridor;

    public float AdherencePathWorldWidth =>
        _adherencePathWorldWidth;

    public float AdherencePathHalfWidth =>
        _adherencePathWorldWidth * 0.5f;

    public bool IsInsideAdherencePath =>
        CurrentAdherence01 > 0f;

    /// <summary>
    /// Whole-voyage summary only. Do not use this to drive immediate gameplay.
    /// </summary>
    public float VoyageAverageAdherence01 =>
        PhysicalTravelDistance > 0.0001f
            ? Mathf.Clamp01(
                _weightedAdherenceDistance /
                PhysicalTravelDistance)
            : 0f;

    /// <summary>
    /// Fraction of physically traveled distance whose sample point was still
    /// inside the visible wake/path.
    /// </summary>
    public float DistanceInsideAdherencePath01 =>
        PhysicalTravelDistance > 0.0001f
            ? Mathf.Clamp01(
                _distanceInsideAdherencePath /
                PhysicalTravelDistance)
            : 0f;

    /// <summary>
    /// Natural voyage efficiency diagnostic:
    /// CURRENT net route position per unit of actual physical distance sailed.
    ///
    /// Example:
    /// sail 100 route units forward, then 100 back toward the start:
    /// PhysicalTravelDistance = 200
    /// CurrentRouteDistance   = 0
    /// RouteTravelEfficiency  = 0
    ///
    /// This is intentionally not based on FarthestRouteDistance.
    /// </summary>
    public float RouteTravelEfficiency01 =>
        PhysicalTravelDistance > 0.0001f
            ? Mathf.Clamp01(
                CurrentRouteDistance /
                PhysicalTravelDistance)
            : 0f;

    public float GeneratedDistance =>
        _points.Count > 0
            ? _points[_points.Count - 1].
                cumulativeDistance
            : 0f;

    public void Initialize(
        Vector2 startPosition,
        int seed,
        float segmentLength,
        float maxHeadingDegrees,
        float progressCorridor,
        float adherencePathWorldWidth,
        float recentCourseQualityDistance,
        float courseNeutralQuality,
        float coverageAheadWorldY)
    {
        _points.Clear();

        _random =
            new System.Random(seed);

        _segmentLength =
            Mathf.Max(
                2f,
                segmentLength);

        _maxHeadingDegrees =
            Mathf.Clamp(
                maxHeadingDegrees,
                1f,
                45f);

        _progressCorridor =
            Mathf.Max(
                1f,
                progressCorridor);

        _adherencePathWorldWidth =
            Mathf.Max(
                0.5f,
                adherencePathWorldWidth);

        _recentCourseQualityDistance =
            Mathf.Max(
                1f,
                recentCourseQualityDistance);

        _courseNeutralQuality =
            Mathf.Clamp(
                courseNeutralQuality,
                0.05f,
                0.95f);

        _hasCourseQualitySamples = false;
        _weightedAdherenceDistance = 0f;
        _distanceInsideAdherencePath = 0f;

        CurrentAdherence01 = 1f;
        RecentCourseQuality01 = 0f;
        CourseInstability01 = 0f;
        HandlingEfficiency01 = 0f;

        PhysicalTravelDistance = 0f;
        ForwardPhysicalTravelDistance = 0f;

        _generationHeading = 0f;
        _generationHeadingVelocity = 0f;

        CurrentRouteDistance = 0f;
        FarthestRouteDistance = 0f;
        OffRouteDistance = 0f;
        IsInsideProgressCorridor = true;

        _points.Add(
            new RoutePoint(
                startPosition,
                0f,
                0f));

        _initialized = true;

        EnsureCoverageToWorldY(
            startPosition.y +
            Mathf.Max(
                _segmentLength * 2f,
                coverageAheadWorldY));

        UpdateProgress(
            startPosition);
    }

    public void EnsureCoverageToWorldY(
        float targetWorldY)
    {
        if (!_initialized ||
            _points.Count == 0)
        {
            return;
        }

        int safety = 0;

        while (_points[
                   _points.Count - 1].
                   position.y <
               targetWorldY &&
               safety < 10000)
        {
            AddNextPoint();
            safety++;
        }
    }

    public void UpdateProgress(
        Vector2 boatPosition)
    {
        if (!IsInitialized)
            return;

        float bestSqrDistance =
            float.PositiveInfinity;

        float bestProjectedDistance =
            0f;

        for (int i = 0;
             i < _points.Count - 1;
             i++)
        {
            RoutePoint a =
                _points[i];

            RoutePoint b =
                _points[i + 1];

            Vector2 ab =
                b.position -
                a.position;

            float sqrLength =
                ab.sqrMagnitude;

            if (sqrLength <=
                0.000001f)
            {
                continue;
            }

            float t =
                Mathf.Clamp01(
                    Vector2.Dot(
                        boatPosition -
                        a.position,
                        ab) /
                    sqrLength);

            Vector2 closest =
                a.position +
                ab *
                t;

            float sqrDistance =
                (boatPosition -
                 closest).
                sqrMagnitude;

            if (sqrDistance >=
                bestSqrDistance)
            {
                continue;
            }

            bestSqrDistance =
                sqrDistance;

            float segmentDistance =
                Mathf.Sqrt(
                    sqrLength);

            bestProjectedDistance =
                a.cumulativeDistance +
                segmentDistance *
                t;
        }

        if (float.IsPositiveInfinity(
                bestSqrDistance))
        {
            return;
        }

        OffRouteDistance =
            Mathf.Sqrt(
                bestSqrDistance);

        CurrentRouteDistance =
            bestProjectedDistance;

        IsInsideProgressCorridor =
            OffRouteDistance <=
            _progressCorridor;

        float adherenceHalfWidth =
            Mathf.Max(
                0.0001f,
                AdherencePathHalfWidth);

        CurrentAdherence01 =
            Mathf.Clamp01(
                1f -
                OffRouteDistance /
                adherenceHalfWidth);

        if (IsInsideProgressCorridor)
        {
            FarthestRouteDistance =
                Mathf.Max(
                    FarthestRouteDistance,
                    CurrentRouteDistance);
        }
    }

    /// <summary>
    /// Records actual BoatScene travel against the current route adherence.
    ///
    /// RecentCourseQuality01 is exponentially weighted by DISTANCE, not time.
    /// Stopping the boat therefore does not improve or damage course quality.
    /// The configured recent-course distance acts like a soft rolling window.
    ///
    /// Phase 1 only measures these values. Nothing consumes the handling/
    /// instability outputs yet.
    /// </summary>
    public void RecordPhysicalTravel(
        float signedPhysicalTravelDelta)
    {
        if (!IsInitialized)
            return;

        float travelDistance =
            Mathf.Abs(
                signedPhysicalTravelDelta);

        if (travelDistance <=
            0.000001f)
        {
            return;
        }

        PhysicalTravelDistance +=
            travelDistance;

        if (signedPhysicalTravelDelta > 0f)
        {
            ForwardPhysicalTravelDistance +=
                signedPhysicalTravelDelta;
        }

        _weightedAdherenceDistance +=
            travelDistance *
            CurrentAdherence01;

        if (IsInsideAdherencePath)
        {
            _distanceInsideAdherencePath +=
                travelDistance;
        }

        if (!_hasCourseQualitySamples)
        {
            RecentCourseQuality01 =
                CurrentAdherence01;

            _hasCourseQualitySamples =
                true;
        }
        else
        {
            // Exponential weighting by actual distance traveled.
            // After roughly one configured memory distance, about 63% of the
            // old course-quality state has been replaced by newer samples.
            float alpha =
                1f -
                Mathf.Exp(
                    -travelDistance /
                    _recentCourseQualityDistance);

            RecentCourseQuality01 =
                Mathf.Lerp(
                    RecentCourseQuality01,
                    CurrentAdherence01,
                    alpha);
        }

        RefreshDerivedCoursePerformance();
    }

    private void RefreshDerivedCoursePerformance()
    {
        if (!_hasCourseQualitySamples)
        {
            CourseInstability01 = 0f;
            HandlingEfficiency01 = 0f;
            return;
        }

        if (RecentCourseQuality01 <
            _courseNeutralQuality)
        {
            CourseInstability01 =
                Mathf.InverseLerp(
                    _courseNeutralQuality,
                    0f,
                    RecentCourseQuality01);

            HandlingEfficiency01 =
                0f;
        }
        else
        {
            HandlingEfficiency01 =
                Mathf.InverseLerp(
                    _courseNeutralQuality,
                    1f,
                    RecentCourseQuality01);

            CourseInstability01 =
                0f;
        }
    }

    public bool TrySampleAtWorldY(
        float worldY,
        out Vector2 position,
        out float headingDegrees)
    {
        position =
            Vector2.zero;

        headingDegrees =
            0f;

        if (!IsInitialized)
            return false;

        RoutePoint first =
            _points[0];

        if (worldY <=
            first.position.y)
        {
            position =
                first.position;

            headingDegrees =
                first.headingDegrees;

            return true;
        }

        for (int i = 0;
             i < _points.Count - 1;
             i++)
        {
            RoutePoint a =
                _points[i];

            RoutePoint b =
                _points[i + 1];

            if (worldY <
                    a.position.y ||
                worldY >
                    b.position.y)
            {
                continue;
            }

            float dy =
                b.position.y -
                a.position.y;

            float t =
                Mathf.Abs(dy) >
                0.000001f
                    ? Mathf.Clamp01(
                        (worldY -
                         a.position.y) /
                        dy)
                    : 0f;

            position =
                Vector2.Lerp(
                    a.position,
                    b.position,
                    t);

            Vector2 direction =
                (b.position -
                 a.position).
                normalized;

            headingDegrees =
                Mathf.Atan2(
                    direction.x,
                    direction.y) *
                Mathf.Rad2Deg;

            return true;
        }

        RoutePoint last =
            _points[
                _points.Count - 1];

        position =
            last.position;

        headingDegrees =
            last.headingDegrees;

        return true;
    }

    private void AddNextPoint()
    {
        RoutePoint previous =
            _points[
                _points.Count - 1];

        // Second-order random walk:
        // heading velocity changes slowly, so the route sways rather than
        // choosing a brand-new random direction at each control point.
        float randomAcceleration =
            Mathf.Lerp(
                -1.35f,
                1.35f,
                (float)_random.NextDouble());

        _generationHeadingVelocity =
            _generationHeadingVelocity *
            0.82f +
            randomAcceleration;

        _generationHeadingVelocity =
            Mathf.Clamp(
                _generationHeadingVelocity,
                -2.4f,
                2.4f);

        float candidateHeading =
            _generationHeading +
            _generationHeadingVelocity;

        if (candidateHeading >
            _maxHeadingDegrees)
        {
            candidateHeading =
                _maxHeadingDegrees;

            _generationHeadingVelocity =
                -Mathf.Abs(
                    _generationHeadingVelocity) *
                0.45f;
        }
        else if (candidateHeading <
                 -_maxHeadingDegrees)
        {
            candidateHeading =
                -_maxHeadingDegrees;

            _generationHeadingVelocity =
                Mathf.Abs(
                    _generationHeadingVelocity) *
                0.45f;
        }

        _generationHeading =
            candidateHeading;

        float radians =
            _generationHeading *
            Mathf.Deg2Rad;

        Vector2 forward =
            new Vector2(
                Mathf.Sin(
                    radians),
                Mathf.Cos(
                    radians));

        Vector2 nextPosition =
            previous.position +
            forward *
            _segmentLength;

        float segmentDistance =
            Vector2.Distance(
                previous.position,
                nextPosition);

        _points.Add(
            new RoutePoint(
                nextPosition,
                previous.cumulativeDistance +
                segmentDistance,
                _generationHeading));
    }
}