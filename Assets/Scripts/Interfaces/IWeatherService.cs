using System;

public interface IWeatherService
{
    // =====================================================
    // Rain
    // =====================================================
    /// <summary>Current rain intensity [0..1].</summary>
    float RainDropDensity { get; }
    float RainFallSpeed { get; }

    /// <summary>Event triggered when rain intensity changes.</summary>
    event Action<float, float> OnRainDropDensityChanged;
    event Action<float, float> OnRainFallSpeedChanged;


    /// <summary>Returns rain delta (rainRate * intensity)</summary>
    //float GetRainDelta();

    // =====================================================
    // Wind
    // =====================================================
    /// <summary>Current wind [0..1].</summary>
    float Wind { get; }

    /// <summary>Maximum wind speed in world units.</summary>
    float MaxWindSpeed { get; }

    /// <summary>Event triggered when wind changes.</summary>
    event Action<float, float> OnWindChanged;

    // =====================================================
    // Clouds
    // =====================================================
    /// <summary>Current cloud coverage [0..1].</summary>
    float CloudCoverage { get; }

    /// <summary>Event triggered when cloud coverage changes.</summary>
    event Action<float, float> OnCloudCoverageChanged;

    // =====================================================
    // Fog
    // =====================================================
    /// <summary>Current fog coverage [0..1].</summary>
    float FogIntensity { get; }
    float FogBrightness { get; }

    /// <summary>Event triggered when cloud coverage changes.</summary>
    event Action<float, float> OnFogIntensityChanged;
    event Action<float, float> OnFogBrightnessChanged;

    // =====================================================
    // Waves
    // =====================================================
    /// <summary>Current fog coverage [0..1].</summary>
    float WaveAmplitude { get; }
    float WaveFrequency { get; }
    float WaveSpeed { get; }

    /// <summary>Event triggered when cloud coverage changes.</summary>
    event Action<float, float> OnWaveAmplitudeChanged;
    event Action<float, float> OnWaveFrequencyChanged;
    event Action<float, float> OnWaveSpeedChanged;
}
