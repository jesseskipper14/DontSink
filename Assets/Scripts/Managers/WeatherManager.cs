using System;
using UnityEngine;

public class WeatherManager : MonoBehaviour, IWeatherService
{
    // =====================================================
    // Rain
    // =====================================================
    [Range(0f, 1f)][SerializeField] private float rainDropDensity = 0f;
    [SerializeField] private float rainFallSpeed = 0.03f;

    // =====================================================
    // Wind
    // =====================================================
    [Range(0f, 0.3f)][SerializeField] private float wind = 0.00f;
    [SerializeField] private float maxWindSpeed = 0.3f;

    // =====================================================
    // Clouds
    // =====================================================
    [Range(0.4f, 1f)][SerializeField] private float cloudCoverage = 0.4f;

    // =====================================================
    // Fog
    // =====================================================
    [Range(0f, 1f)][SerializeField] private float fogIntensity = 0.0f;
    [Range(0f, 1f)][SerializeField] private float fogBrightness = 0.0f;

    // =====================================================
    // Waves
    // =====================================================
    [Range(0f, 20f)][SerializeField] private float waveAmplitude = 0.5f;
    [Range(0f, 1f)][SerializeField] private float waveFrequency = 0.03f;
    [Range(0f, 1f)][SerializeField] private float waveSpeed = 0.5f;

    // =====================================================
    // Storms
    // =====================================================

    // =====================================================
    // Exposed properties
    // =====================================================
    public float RainDropDensity => rainDropDensity;
    public float RainFallSpeed => rainFallSpeed;
    public float Wind => wind;
    public float MaxWindSpeed => maxWindSpeed;
    public float CloudCoverage => cloudCoverage;
    public float FogIntensity => fogIntensity;
    public float FogBrightness => fogBrightness;
    public float WaveAmplitude => waveAmplitude;
    public float WaveFrequency => waveFrequency;
    public float WaveSpeed => waveSpeed;


    // =====================================================
    // Events
    // =====================================================
    private Action<float, float> onRainDropDensityChanged;
    private Action<float, float> onRainFallSpeedChanged;
    private Action<float, float> onWindChanged;
    private Action<float, float> onCloudCoverageChanged;
    private Action<float, float> onFogIntensityChanged;
    private Action<float, float> onFogBrightnessChanged;
    private Action<float, float> onWaveAmplitudeChanged;
    private Action<float, float> onWaveFrequencyChanged;
    private Action<float, float> onWaveSpeedChanged;

    public event Action<float, float> OnRainDropDensityChanged
    {
        add
        {
            onRainDropDensityChanged += value;
            value?.Invoke(RainDropDensity, 0f); // Push current value immediately
        }
        remove { onRainDropDensityChanged -= value; }
    }

    public event Action<float, float> OnRainFallSpeedChanged
    {
        add
        {
            onRainFallSpeedChanged += value;
            value?.Invoke(RainFallSpeed, 0f); // Push current value immediately
        }
        remove { onRainFallSpeedChanged -= value; }
    }

    public event Action<float, float> OnWindChanged
    {
        add
        {
            onWindChanged += value;
            value?.Invoke(wind, 0f); // Push current value immediately
        }
        remove { onWindChanged -= value; }
    }

    public event Action<float, float> OnCloudCoverageChanged
    {
        add
        {
            onCloudCoverageChanged += value;
            value?.Invoke(cloudCoverage, 0f); // Push current value immediately
        }
        remove { onCloudCoverageChanged -= value; }
    }

    public event Action<float, float> OnFogIntensityChanged
    {
        add
        {
            onFogIntensityChanged += value;
            value?.Invoke(fogIntensity, 0f);
        }
        remove { onFogIntensityChanged -= value; }
    }

    public event Action<float, float> OnFogBrightnessChanged
    {
        add
        {
            onFogBrightnessChanged += value;
            value?.Invoke(fogBrightness, 0f);
        }
        remove { onFogBrightnessChanged -= value; }
    }

    public event Action<float, float> OnWaveAmplitudeChanged
    {
        add
        {
            onWaveAmplitudeChanged += value;
            value?.Invoke(waveAmplitude, 0f);
        }
        remove { onWaveAmplitudeChanged -= value; }
    }

    public event Action<float, float> OnWaveFrequencyChanged
    {
        add
        {
            onWaveFrequencyChanged += value;
            value?.Invoke(waveFrequency, 0f);
        }
        remove { onWaveFrequencyChanged -= value; }
    }

    public event Action<float, float> OnWaveSpeedChanged
    {
        add
        {
            onWaveSpeedChanged += value;
            value?.Invoke(waveSpeed, 0f);
        }
        remove { onWaveSpeedChanged -= value; }
    }

    // =====================================================
    // API - setters trigger events
    // =====================================================
    public void SetCloudCoverage(float coverage, float duration)
    {
        coverage = Mathf.Clamp01(coverage);
        if (Mathf.Approximately(cloudCoverage, coverage)) return;

        cloudCoverage = coverage;
        onCloudCoverageChanged?.Invoke(cloudCoverage, duration);
    }

    public void SetRainDropDensity(float dropDensity, float duration)
    {
        dropDensity = Mathf.Clamp01(dropDensity);
        if (Mathf.Approximately(rainDropDensity, dropDensity)) return;

        rainDropDensity = dropDensity;
        onRainDropDensityChanged?.Invoke(rainDropDensity, duration);
    }

    public void SetRainFallSpeed(float fallSpeed, float duration)
    {
        fallSpeed = Mathf.Clamp01(fallSpeed);
        if (Mathf.Approximately(rainFallSpeed, fallSpeed)) return;

        rainFallSpeed = fallSpeed;
        onRainFallSpeedChanged?.Invoke(rainFallSpeed, duration);
    }

    public void SetWind(float newWind, float duration)
    {
        newWind = Mathf.Clamp(newWind, 0f, maxWindSpeed);
        if (Mathf.Approximately(wind, newWind)) return;

        wind = newWind;
        onWindChanged?.Invoke(wind, duration);
    }

    public void SetFogIntensity(float newFogInt, float duration)
    {
        newFogInt = Mathf.Clamp01(newFogInt);
        if (Mathf.Approximately(fogIntensity, newFogInt)) return;

        fogIntensity = newFogInt;
        onFogIntensityChanged?.Invoke(fogIntensity, duration);
    }

    public void SetFogBrightness(float newFogBrt, float duration)
    {
        newFogBrt = Mathf.Clamp01(newFogBrt);
        if (Mathf.Approximately(fogBrightness, newFogBrt)) return;

        fogBrightness = newFogBrt;
        onFogBrightnessChanged?.Invoke(fogBrightness, duration);
    }

    public void SetWaveAmplitude(float newAmpitude, float duration)
    {
        if (Mathf.Approximately(waveAmplitude, newAmpitude)) return;

        waveAmplitude = newAmpitude;
        onWaveAmplitudeChanged?.Invoke(waveAmplitude, duration);
    }

    public void SetWaveFrequency(float newFrequency, float duration)
    {
        newFrequency = Mathf.Clamp01(newFrequency);

        if (Mathf.Approximately(waveFrequency, newFrequency)) return;

        waveFrequency = newFrequency;
        onWaveFrequencyChanged?.Invoke(waveFrequency, duration);
    }

    public void SetWaveSpeed(float newSpeed, float duration)
    {
        newSpeed = Mathf.Clamp01(newSpeed);

        if (Mathf.Approximately(waveSpeed, newSpeed)) return;

        waveSpeed = newSpeed;
        onWaveSpeedChanged?.Invoke(waveSpeed, duration);
    }
}
