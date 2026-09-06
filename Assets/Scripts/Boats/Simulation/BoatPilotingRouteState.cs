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

    // A successful celestial fix never destroys or rewrites the original route.
    // Recovery guidance lives separately and merges back into that route.
    private readonly List<RoutePoint> _recoveryPoints =
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

    // Prototype lostness progression.
    // At full course instability, certainty reaches zero after roughly this many
    // recent-course memory distances of actual physical travel.
    private const float NavigationCertaintyLossDistanceMultiplier = 6f;

    private bool _hasCourseQualitySamples;
    private float _weightedAdherenceDistance;
    private float _distanceInsideAdherencePath;

    private float _generationHeading;
    private float _generationHeadingVelocity;

    private bool _isRecovering;
    private float _recoveryRejoinRouteDistance;
    private float _visibleOriginalRouteStartDistance;

    private bool _initialized;

    public IReadOnlyList<RoutePoint> Points =>
        _points;

    public IReadOnlyList<RoutePoint> RecoveryPoints =>
        _recoveryPoints;

    public bool IsRecovering =>
        _isRecovering &&
        _recoveryPoints.Count >= 2;

    public float RecoveryRejoinRouteDistance =>
        _recoveryRejoinRouteDistance;

    /// <summary>
    /// Original-route distance before which the renderer must not reveal route
    /// history after a positional fix. A fix tells the player where they are now
    /// and how to proceed, not where every lost mile was traveled.
    /// </summary>
    public float VisibleOriginalRouteStartDistance =>
        _visibleOriginalRouteStartDistance;

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

    /// <summary>
    /// Player navigation confidence.
    ///
    /// 1 = route/path knowledge is fully trusted.
    /// 0 = lost; the route still exists internally, but presentation must not
    /// reveal it to the player.
    ///
    /// Sustained course instability degrades this value. A successful positional
    /// fix restores it and creates temporary recovery guidance back to the original
    /// route.
    /// </summary>
    public float NavigationCertainty01 { get; private set; }

    public float NavigationUncertainty01 =>
        1f -
        NavigationCertainty01;

    public bool IsLost =>
        NavigationCertainty01 <= 0.0001f;

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
        _recoveryPoints.Clear();

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
        NavigationCertainty01 = 1f;

        PhysicalTravelDistance = 0f;
        ForwardPhysicalTravelDistance = 0f;

        _generationHeading = 0f;
        _generationHeadingVelocity = 0f;

        _isRecovering = false;
        _recoveryRejoinRouteDistance = 0f;
        _visibleOriginalRouteStartDistance = 0f;

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

        if (!TryProjectOntoPath(
                _points,
                boatPosition,
                out float originalProjectedDistance,
                out float originalOffRouteDistance))
        {
            return;
        }

        CurrentRouteDistance =
            originalProjectedDistance;

        float guidanceOffRouteDistance =
            originalOffRouteDistance;

        float recoveryProjectedDistance =
            0f;

        bool usingRecovery =
            IsRecovering &&
            TryProjectOntoPath(
                _recoveryPoints,
                boatPosition,
                out recoveryProjectedDistance,
                out guidanceOffRouteDistance);

        ApplyGuidanceMetrics(
            guidanceOffRouteDistance);

        if (IsInsideProgressCorridor)
        {
            FarthestRouteDistance =
                Mathf.Max(
                    FarthestRouteDistance,
                    CurrentRouteDistance);
        }

        if (usingRecovery &&
            HasReachedRecoveryRejoin(
                boatPosition,
                recoveryProjectedDistance,
                guidanceOffRouteDistance))
        {
            CompleteRecovery();

            // The boat is now back on the original route. Refresh the displayed
            // guidance metrics immediately so there is no one-tick seam.
            ApplyGuidanceMetrics(
                originalOffRouteDistance);

            if (IsInsideProgressCorridor)
            {
                FarthestRouteDistance =
                    Mathf.Max(
                        FarthestRouteDistance,
                        CurrentRouteDistance);
            }
        }
    }

    /// <summary>
    /// Applies the result of a successful positional fix.
    ///
    /// The original route is never rewritten. Instead, a temporary recovery path
    /// starts at the boat's TRUE current navigation position and curves into a
    /// sensible future point on the original route.
    ///
    /// Later, the star-map matching game should call this same operation after a
    /// successful alignment.
    /// </summary>
    public bool TryRecoverKnownPosition(
        Vector2 currentPosition,
        float currentHeadingDegrees,
        float minimumRejoinLeadDistance,
        float rejoinLeadPerOffRouteUnit,
        float recoveryPointSpacing)
    {
        if (!IsInitialized)
            return false;

        if (!TryProjectOntoPath(
                _points,
                currentPosition,
                out float originalProjectedDistance,
                out float originalOffRouteDistance))
        {
            return false;
        }

        float minimumLead =
            Mathf.Max(
                _segmentLength * 2f,
                minimumRejoinLeadDistance);

        float leadPerOffRoute =
            Mathf.Max(
                0f,
                rejoinLeadPerOffRouteUnit);

        float rejoinLead =
            Mathf.Max(
                minimumLead,
                originalOffRouteDistance *
                leadPerOffRoute);

        float anchorRouteDistance =
            Mathf.Max(
                originalProjectedDistance,
                FarthestRouteDistance);

        float targetRouteDistance =
            anchorRouteDistance +
            rejoinLead;

        EnsureCoverageToRouteDistance(
            targetRouteDistance +
            _segmentLength * 2f);

        if (!TrySampleAtRouteDistance(
                targetRouteDistance,
                out Vector2 targetPosition,
                out float targetHeadingDegrees))
        {
            return false;
        }

        BuildRecoveryPath(
            currentPosition,
            currentHeadingDegrees,
            targetPosition,
            targetHeadingDegrees,
            recoveryPointSpacing);

        if (_recoveryPoints.Count < 2)
            return false;

        _isRecovering = true;
        _recoveryRejoinRouteDistance =
            targetRouteDistance;

        // Never reveal the old route behind the newly established fix.
        _visibleOriginalRouteStartDistance =
            Mathf.Max(
                _visibleOriginalRouteStartDistance,
                targetRouteDistance);

        NavigationCertainty01 =
            1f;

        // A positional fix starts a fresh course-quality window. Otherwise the
        // pre-fix instability would immediately begin destroying the certainty
        // that the player just legitimately recovered.
        _hasCourseQualitySamples = false;
        RecentCourseQuality01 = 0f;
        CourseInstability01 = 0f;
        HandlingEfficiency01 = 0f;

        CurrentAdherence01 = 1f;
        OffRouteDistance = 0f;
        IsInsideProgressCorridor = true;

        CurrentRouteDistance =
            originalProjectedDistance;

        return true;
    }

    private void ApplyGuidanceMetrics(
        float guidanceOffRouteDistance)
    {
        OffRouteDistance =
            Mathf.Max(
                0f,
                guidanceOffRouteDistance);

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
    }

    private bool HasReachedRecoveryRejoin(
        Vector2 boatPosition,
        float recoveryProjectedDistance,
        float recoveryOffRouteDistance)
    {
        if (!IsRecovering)
            return false;

        RoutePoint last =
            _recoveryPoints[
                _recoveryPoints.Count - 1];

        float captureRadius =
            Mathf.Max(
                AdherencePathHalfWidth,
                _segmentLength *
                0.75f);

        if (Vector2.Distance(
                boatPosition,
                last.position) <=
            captureRadius)
        {
            return true;
        }

        float remainingRecoveryDistance =
            Mathf.Max(
                0f,
                last.cumulativeDistance -
                recoveryProjectedDistance);

        return
            remainingRecoveryDistance <=
                captureRadius &&
            recoveryOffRouteDistance <=
                _progressCorridor;
    }

    private void CompleteRecovery()
    {
        _isRecovering = false;
        _recoveryPoints.Clear();
    }

    private void BuildRecoveryPath(
        Vector2 startPosition,
        float startHeadingDegrees,
        Vector2 targetPosition,
        float targetHeadingDegrees,
        float pointSpacing)
    {
        _recoveryPoints.Clear();

        Vector2 direct =
            targetPosition -
            startPosition;

        float directDistance =
            direct.magnitude;

        if (directDistance <= 0.001f)
        {
            _recoveryPoints.Add(
                new RoutePoint(
                    startPosition,
                    0f,
                    startHeadingDegrees));

            _recoveryPoints.Add(
                new RoutePoint(
                    targetPosition,
                    directDistance,
                    targetHeadingDegrees));

            return;
        }

        Vector2 directDirection =
            direct /
            directDistance;

        Vector2 currentForward =
            HeadingToForward(
                startHeadingDegrees);

        // Bias toward the target so a terrible current heading cannot create a
        // huge looping recovery route, while still allowing the curve to begin in
        // a direction that feels related to the boat's current orientation.
        Vector2 startTangent =
            directDirection *
            0.78f +
            currentForward *
            0.22f;

        if (startTangent.sqrMagnitude <=
            0.000001f)
        {
            startTangent =
                directDirection;
        }
        else
        {
            startTangent.Normalize();
        }

        Vector2 targetForward =
            HeadingToForward(
                targetHeadingDegrees);

        float handleDistance =
            Mathf.Clamp(
                directDistance *
                0.34f,
                Mathf.Min(
                    _segmentLength * 2f,
                    directDistance * 0.25f),
                directDistance *
                0.45f);

        Vector2 p0 =
            startPosition;

        Vector2 p1 =
            startPosition +
            startTangent *
            handleDistance;

        Vector2 p3 =
            targetPosition;

        Vector2 p2 =
            targetPosition -
            targetForward *
            handleDistance;

        float spacing =
            Mathf.Max(
                2f,
                pointSpacing);

        int sampleCount =
            Mathf.Clamp(
                Mathf.CeilToInt(
                    directDistance /
                    spacing) +
                4,
                8,
                160);

        float cumulativeDistance =
            0f;

        Vector2 previous =
            p0;

        for (int i = 0;
             i < sampleCount;
             i++)
        {
            float t =
                sampleCount > 1
                    ? i /
                      (float)(sampleCount - 1)
                    : 0f;

            Vector2 position =
                CubicBezier(
                    p0,
                    p1,
                    p2,
                    p3,
                    t);

            if (i > 0)
            {
                cumulativeDistance +=
                    Vector2.Distance(
                        previous,
                        position);
            }

            Vector2 tangent =
                CubicBezierTangent(
                    p0,
                    p1,
                    p2,
                    p3,
                    t);

            float headingDegrees =
                tangent.sqrMagnitude >
                0.000001f
                    ? Mathf.Atan2(
                        tangent.x,
                        tangent.y) *
                      Mathf.Rad2Deg
                    : targetHeadingDegrees;

            _recoveryPoints.Add(
                new RoutePoint(
                    position,
                    cumulativeDistance,
                    headingDegrees));

            previous =
                position;
        }
    }

    private void EnsureCoverageToRouteDistance(
        float targetRouteDistance)
    {
        if (!_initialized ||
            _points.Count == 0)
        {
            return;
        }

        int safety = 0;

        while (GeneratedDistance <
               targetRouteDistance &&
               safety < 10000)
        {
            AddNextPoint();
            safety++;
        }
    }

    public bool TrySampleAtRouteDistance(
        float routeDistance,
        out Vector2 position,
        out float headingDegrees)
    {
        position =
            Vector2.zero;

        headingDegrees =
            0f;

        if (!IsInitialized ||
            _points.Count < 2)
        {
            return false;
        }

        float clampedDistance =
            Mathf.Clamp(
                routeDistance,
                0f,
                GeneratedDistance);

        for (int i = 0;
             i < _points.Count - 1;
             i++)
        {
            RoutePoint a =
                _points[i];

            RoutePoint b =
                _points[i + 1];

            if (clampedDistance >
                b.cumulativeDistance)
            {
                continue;
            }

            float span =
                Mathf.Max(
                    0.000001f,
                    b.cumulativeDistance -
                    a.cumulativeDistance);

            float t =
                Mathf.Clamp01(
                    (clampedDistance -
                     a.cumulativeDistance) /
                    span);

            position =
                Vector2.Lerp(
                    a.position,
                    b.position,
                    t);

            Vector2 direction =
                b.position -
                a.position;

            headingDegrees =
                direction.sqrMagnitude >
                0.000001f
                    ? Mathf.Atan2(
                        direction.x,
                        direction.y) *
                      Mathf.Rad2Deg
                    : b.headingDegrees;

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

    private static bool TryProjectOntoPath(
        List<RoutePoint> path,
        Vector2 position,
        out float projectedDistance,
        out float offRouteDistance)
    {
        projectedDistance =
            0f;

        offRouteDistance =
            0f;

        if (path == null ||
            path.Count < 2)
        {
            return false;
        }

        float bestSqrDistance =
            float.PositiveInfinity;

        float bestProjectedDistance =
            0f;

        for (int i = 0;
             i < path.Count - 1;
             i++)
        {
            RoutePoint a =
                path[i];

            RoutePoint b =
                path[i + 1];

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
                        position -
                        a.position,
                        ab) /
                    sqrLength);

            Vector2 closest =
                a.position +
                ab *
                t;

            float sqrDistance =
                (position -
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
                Vector2.Distance(
                    a.position,
                    b.position);

            bestProjectedDistance =
                a.cumulativeDistance +
                segmentDistance *
                t;
        }

        if (float.IsPositiveInfinity(
                bestSqrDistance))
        {
            return false;
        }

        projectedDistance =
            bestProjectedDistance;

        offRouteDistance =
            Mathf.Sqrt(
                bestSqrDistance);

        return true;
    }

    private static Vector2 CubicBezier(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float t)
    {
        float oneMinusT =
            1f -
            t;

        return
            oneMinusT *
            oneMinusT *
            oneMinusT *
            p0 +
            3f *
            oneMinusT *
            oneMinusT *
            t *
            p1 +
            3f *
            oneMinusT *
            t *
            t *
            p2 +
            t *
            t *
            t *
            p3;
    }

    private static Vector2 CubicBezierTangent(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        float t)
    {
        float oneMinusT =
            1f -
            t;

        return
            3f *
            oneMinusT *
            oneMinusT *
            (p1 - p0) +
            6f *
            oneMinusT *
            t *
            (p2 - p1) +
            3f *
            t *
            t *
            (p3 - p2);
    }

    private static Vector2 HeadingToForward(
        float headingDegrees)
    {
        float radians =
            headingDegrees *
            Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(
                radians),
            Mathf.Cos(
                radians));
    }

    /// <summary>
    /// Records actual BoatScene travel against the current route adherence.
    ///
    /// RecentCourseQuality01 is exponentially weighted by DISTANCE, not time.
    /// Stopping the boat therefore does not improve or damage course quality.
    /// The configured recent-course distance acts like a soft rolling window.
    ///
    /// Course instability also drives navigation-certainty loss. Handling
    /// efficiency remains diagnostic for now.
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

        AdvanceNavigationCertainty(
            travelDistance);
    }

    /// <summary>
    /// Sustained bad navigation erodes certainty by ACTUAL distance traveled.
    /// Sitting still therefore cannot make the player more lost.
    ///
    /// Good navigation stops further loss but does not restore certainty.
    /// Certainty returns only through an explicit successful positional fix.
    /// </summary>
    private void AdvanceNavigationCertainty(
        float travelDistance)
    {
        if (NavigationCertainty01 <= 0f ||
            travelDistance <= 0f ||
            CourseInstability01 <= 0f)
        {
            return;
        }

        float fullLossDistance =
            Mathf.Max(
                1f,
                _recentCourseQualityDistance *
                NavigationCertaintyLossDistanceMultiplier);

        float certaintyLoss =
            CourseInstability01 *
            travelDistance /
            fullLossDistance;

        NavigationCertainty01 =
            Mathf.Clamp01(
                NavigationCertainty01 -
                certaintyLoss);
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