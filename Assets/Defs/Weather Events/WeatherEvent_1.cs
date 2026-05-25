using UnityEngine;

[CreateAssetMenu(fileName = "WeatherEvent", menuName = "Weather/Weather Event")]
public class WeatherEventSO : ScriptableObject
{
    [Header("Rain")]
    [Range(0f, 1f)] public float rainDropDensity; // how many drops
    [Range(0f, 1f)] public float rainFallSpeed;   // how fast they fall

    [Header("Wind")]
    [Range(0f, 0.3f)] public float wind;
    public float maxWindSpeed;

    [Header("Clouds")]
    [Range(0.4f, 1f)] public float cloudCoverage;

    [Header("Fog")]
    [Range(0f, 1f)] public float fogIntensity;
    [Range(0f, 1f)] public float fogBrightness;

    [Header("Waves")]
    [Range(0f, 20f)] public float waveAmplitude;
    [Range(0f, 1f)] public float waveFrequency;
    [Range(0f, 1f)] public float waveSpeed;
}
