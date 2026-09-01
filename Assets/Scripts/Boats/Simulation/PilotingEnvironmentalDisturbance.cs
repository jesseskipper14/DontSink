using System;
using UnityEngine;

/// <summary>
/// Temporary simulation-owned sea disturbance model.
/// Wave amplitude is only a loose severity proxy until the weather/wave pass.
/// Produces navigation-space lateral drift and discrete yaw-velocity kicks.
/// </summary>
[Serializable]
public sealed class PilotingEnvironmentalDisturbance
{
    [Header("Temporary Severity Proxy")]
    [SerializeField, Min(0f)] private float minimumWaveAmplitude = 0.5f;
    [SerializeField, Min(0.01f)] private float waveAmplitudeForFullSeverity = 10f;

    [Header("Sea Direction")]
    [Tooltip("Navigation-space wave travel direction. Default down-screen means a north-facing boat meets head seas.")]
    [SerializeField] private Vector2 waveTravelDirection = Vector2.down;

    [Header("Pulse Timing")]
    [SerializeField, Min(0.05f)] private float maxPulseInterval = 4.5f;
    [SerializeField, Min(0.05f)] private float minPulseInterval = 0.9f;
    [SerializeField] private Vector2 pulseIntervalRandomMultiplier = new Vector2(0.75f, 1.25f);

    [Header("Lateral Disturbance")]
    [SerializeField, Min(0f)] private float maxLateralVelocityKick = 2.4f;
    [SerializeField, Min(0f)] private float lateralVelocityDrag = 1.35f;

    [Header("Yaw Disturbance")]
    [SerializeField, Min(0f)] private float maxYawVelocityKickDegrees = 16f;

    [Header("Pulse Variation")]
    [SerializeField] private Vector2 pulseStrengthRandomMultiplier = new Vector2(0.75f, 1.25f);

    private System.Random _random;
    private int _activeSeed;
    private bool _hasSeed;
    private float _secondsUntilNextPulse;
    private Vector2 _navigationDriftVelocity;
    private float _currentWaveAmplitude;
    private float _severity01;
    private float _beamExposure01;
    private float _encounterBroadsideDegrees;
    private Vector2 _lastLateralVelocityKick;
    private float _lastYawVelocityKickDegrees;
    private int _pulseCount;

    public float CurrentWaveAmplitude => _currentWaveAmplitude;
    public float Severity01 => _severity01;
    public float BeamExposure01 => _beamExposure01;
    public float EncounterBroadsideDegrees => _encounterBroadsideDegrees;
    public Vector2 NavigationDriftVelocity => _navigationDriftVelocity;
    public Vector2 LastLateralVelocityKick => _lastLateralVelocityKick;
    public float LastYawVelocityKickDegrees => _lastYawVelocityKickDegrees;
    public int PulseCount => _pulseCount;
    public float SecondsUntilNextPulse => Mathf.Max(0f, _secondsUntilNextPulse);

    public Vector2 WaveTravelDirection
    {
        get
        {
            if (waveTravelDirection.sqrMagnitude <= 0.000001f)
                return Vector2.down;
            return waveTravelDirection.normalized;
        }
    }

    public void EnsureSeed(int seed)
    {
        if (_hasSeed && _activeSeed == seed)
            return;

        _activeSeed = seed;
        _hasSeed = true;
        _random = new System.Random(seed);
        _secondsUntilNextPulse = 0f;
        _navigationDriftVelocity = Vector2.zero;
        _lastLateralVelocityKick = Vector2.zero;
        _lastYawVelocityKickDegrees = 0f;
        _pulseCount = 0;
    }

    public void Tick(float dt, bool disturbanceEnabled, float waveAmplitude, float headingDegrees, out float yawVelocityKickDegrees)
    {
        yawVelocityKickDegrees = 0f;
        if (dt <= 0f)
            return;

        _currentWaveAmplitude = Mathf.Max(0f, waveAmplitude);
        RefreshEncounterState(headingDegrees);
        _navigationDriftVelocity *= Mathf.Exp(-Mathf.Max(0f, lateralVelocityDrag) * dt);

        if (!disturbanceEnabled)
        {
            _severity01 = 0f;
            _secondsUntilNextPulse = 0f;
            _navigationDriftVelocity = Vector2.zero;
            return;
        }

        _severity01 = EvaluateSeverity01(_currentWaveAmplitude);
        if (_severity01 <= 0.0001f)
        {
            _secondsUntilNextPulse = 0f;
            return;
        }

        _secondsUntilNextPulse -= dt;
        if (_secondsUntilNextPulse > 0f)
            return;

        GeneratePulse(headingDegrees, out yawVelocityKickDegrees);
        ScheduleNextPulse();
    }

    private void GeneratePulse(float headingDegrees, out float yawVelocityKickDegrees)
    {
        yawVelocityKickDegrees = 0f;

        Vector2 forward = HeadingToForward(headingDegrees);
        Vector2 right = new Vector2(forward.y, -forward.x);
        float signedBeamComponent = Mathf.Clamp(Vector2.Dot(WaveTravelDirection, right), -1f, 1f);

        _beamExposure01 = Mathf.Abs(signedBeamComponent);
        _encounterBroadsideDegrees = Mathf.Asin(Mathf.Clamp01(_beamExposure01)) * Mathf.Rad2Deg;

        float strengthRandom = Mathf.Max(0f, RandomRange(pulseStrengthRandomMultiplier.x, pulseStrengthRandomMultiplier.y));
        float effectiveStrength = _severity01 * _beamExposure01 * strengthRandom;

        Vector2 lateralKick =
            right * signedBeamComponent * Mathf.Max(0f, maxLateralVelocityKick) * _severity01 * strengthRandom;

        _navigationDriftVelocity += lateralKick;
        _lastLateralVelocityKick = lateralKick;

        if (effectiveStrength > 0.0001f)
        {
            float yawPolarity = Random01() < 0.5f ? -1f : 1f;
            yawVelocityKickDegrees =
                yawPolarity * Mathf.Max(0f, maxYawVelocityKickDegrees) * effectiveStrength;
        }

        _lastYawVelocityKickDegrees = yawVelocityKickDegrees;
        _pulseCount++;
    }

    private void RefreshEncounterState(float headingDegrees)
    {
        Vector2 forward = HeadingToForward(headingDegrees);
        Vector2 right = new Vector2(forward.y, -forward.x);
        float signedBeamComponent = Mathf.Clamp(Vector2.Dot(WaveTravelDirection, right), -1f, 1f);
        _beamExposure01 = Mathf.Abs(signedBeamComponent);
        _encounterBroadsideDegrees = Mathf.Asin(Mathf.Clamp01(_beamExposure01)) * Mathf.Rad2Deg;
    }

    private float EvaluateSeverity01(float waveAmplitude)
    {
        float minimum = Mathf.Max(0f, minimumWaveAmplitude);
        float full = Mathf.Max(minimum + 0.0001f, waveAmplitudeForFullSeverity);
        return Mathf.InverseLerp(minimum, full, waveAmplitude);
    }

    private void ScheduleNextPulse()
    {
        float low = Mathf.Max(0.05f, minPulseInterval);
        float high = Mathf.Max(low, maxPulseInterval);
        float baseInterval = Mathf.Lerp(high, low, _severity01);
        float multiplier = Mathf.Max(0.05f, RandomRange(pulseIntervalRandomMultiplier.x, pulseIntervalRandomMultiplier.y));
        _secondsUntilNextPulse = Mathf.Max(0.05f, baseInterval * multiplier);
    }

    private float Random01()
    {
        if (_random == null)
            _random = new System.Random(_activeSeed);
        return (float)_random.NextDouble();
    }

    private float RandomRange(float a, float b)
    {
        return Mathf.Lerp(Mathf.Min(a, b), Mathf.Max(a, b), Random01());
    }

    private static Vector2 HeadingToForward(float headingDegrees)
    {
        float radians = headingDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }
}
