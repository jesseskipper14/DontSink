using System;
using UnityEngine;

/// <summary>
/// Simulation-owned sea disturbance model.
///
/// The physical WaveField supplies a continuously sampled trough-to-crest load.
/// This class converts that load into navigation-space lateral acceleration and
/// yaw angular acceleration based on sea severity and the boat's orientation.
///
/// It deliberately does not apply discrete "wave kicks".
/// </summary>
[Serializable]
public sealed class PilotingEnvironmentalDisturbance
{
    [Header("Temporary Severity Proxy")]
    [SerializeField, Min(0f)]
    private float minimumWaveAmplitude = 0.5f;

    [SerializeField, Min(0.01f)]
    private float waveAmplitudeForFullSeverity = 10f;

    [Header("Sea Direction")]
    [Tooltip("Navigation-space wave travel direction. Default down-screen means a north-facing boat meets head seas.")]
    [SerializeField]
    private Vector2 waveTravelDirection = Vector2.down;

    [Header("Continuous Lateral Load")]
    [Tooltip("Maximum navigation-space lateral acceleration at full storm severity, full beam exposure, and physical wave crest.")]
    [SerializeField, Min(0f)]
    private float maxLateralAcceleration = 0.85f;

    [Tooltip("Exponential damping applied to accumulated environmental lateral velocity. Higher values shed sideways drift faster.")]
    [SerializeField, Min(0f)]
    private float lateralVelocityDamping = 0.225f;

    [Header("Continuous Yaw Load")]
    [Tooltip("Maximum yaw angular acceleration in degrees/sec^2 at full storm severity, full beam exposure, and physical wave crest.")]
    [SerializeField, Min(0f)]
    private float maxYawAngularAccelerationDegrees = 42f;

    private float _currentWaveAmplitude;
    private float _severity01;
    private float _waveLoad01;

    private float _signedBeamComponent;
    private float _beamExposure01;
    private float _encounterBroadsideDegrees;

    private Vector2 _currentLateralAcceleration;
    private float _currentYawAngularAccelerationDegrees;

    public float CurrentWaveAmplitude =>
        _currentWaveAmplitude;

    public float Severity01 =>
        _severity01;

    /// <summary>
    /// 0 = sampled physical trough, 1 = sampled physical crest.
    /// </summary>
    public float WaveLoad01 =>
        _waveLoad01;

    public float BeamExposure01 =>
        _beamExposure01;

    public float EncounterBroadsideDegrees =>
        _encounterBroadsideDegrees;

    public Vector2 CurrentLateralAcceleration =>
        _currentLateralAcceleration;

    public float CurrentYawAngularAccelerationDegrees =>
        _currentYawAngularAccelerationDegrees;

    public float LateralVelocityDamping =>
        Mathf.Max(
            0f,
            lateralVelocityDamping);

    public Vector2 WaveTravelDirection
    {
        get
        {
            if (waveTravelDirection.sqrMagnitude <= 0.000001f)
                return Vector2.down;

            return waveTravelDirection.normalized;
        }
    }

    /// <summary>
    /// Converts the current physical wave-phase load into continuous environmental
    /// accelerations. The caller integrates them and applies installed-module
    /// resistance.
    /// </summary>
    public void Tick(
        bool disturbanceEnabled,
        float waveAmplitude,
        float headingDegrees,
        float physicalWaveLoad01,
        out Vector2 lateralAcceleration,
        out float yawAngularAccelerationDegrees)
    {
        _currentWaveAmplitude =
            Mathf.Max(
                0f,
                waveAmplitude);

        _waveLoad01 =
            disturbanceEnabled
                ? Mathf.Clamp01(
                    physicalWaveLoad01)
                : 0f;

        RefreshEncounterState(
            headingDegrees);

        _severity01 =
            disturbanceEnabled
                ? EvaluateSeverity01(
                    _currentWaveAmplitude)
                : 0f;

        _currentLateralAcceleration =
            Vector2.zero;

        _currentYawAngularAccelerationDegrees =
            0f;

        if (_severity01 <= 0.0001f ||
            _waveLoad01 <= 0.0001f ||
            _beamExposure01 <= 0.0001f)
        {
            lateralAcceleration =
                Vector2.zero;

            yawAngularAccelerationDegrees =
                0f;

            return;
        }

        Vector2 forward =
            HeadingToForward(
                headingDegrees);

        Vector2 right =
            new Vector2(
                forward.y,
                -forward.x);

        float effectiveLoad =
            _severity01 *
            _waveLoad01;

        _currentLateralAcceleration =
            right *
            _signedBeamComponent *
            Mathf.Max(
                0f,
                maxLateralAcceleration) *
            effectiveLoad;

        // Deterministic sign: broadside seas now create a learnable yaw tendency
        // instead of choosing a random left/right kick at every crest.
        //
        // With the current heading convention this tends to turn the bow toward
        // the wave-travel direction. Exact real-world yaw depends heavily on hull
        // shape/loading, so this remains a gameplay-facing handling abstraction.
        _currentYawAngularAccelerationDegrees =
            _signedBeamComponent *
            Mathf.Max(
                0f,
                maxYawAngularAccelerationDegrees) *
            effectiveLoad;

        lateralAcceleration =
            _currentLateralAcceleration;

        yawAngularAccelerationDegrees =
            _currentYawAngularAccelerationDegrees;
    }

    private void RefreshEncounterState(
        float headingDegrees)
    {
        Vector2 forward =
            HeadingToForward(
                headingDegrees);

        Vector2 right =
            new Vector2(
                forward.y,
                -forward.x);

        _signedBeamComponent =
            Mathf.Clamp(
                Vector2.Dot(
                    WaveTravelDirection,
                    right),
                -1f,
                1f);

        _beamExposure01 =
            Mathf.Abs(
                _signedBeamComponent);

        _encounterBroadsideDegrees =
            Mathf.Asin(
                Mathf.Clamp01(
                    _beamExposure01)) *
            Mathf.Rad2Deg;
    }

    private float EvaluateSeverity01(
        float waveAmplitude)
    {
        float minimum =
            Mathf.Max(
                0f,
                minimumWaveAmplitude);

        float full =
            Mathf.Max(
                minimum + 0.0001f,
                waveAmplitudeForFullSeverity);

        return Mathf.InverseLerp(
            minimum,
            full,
            waveAmplitude);
    }

    private static Vector2 HeadingToForward(
        float headingDegrees)
    {
        float radians =
            headingDegrees *
            Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(radians),
            Mathf.Cos(radians));
    }
}
