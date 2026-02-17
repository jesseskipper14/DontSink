using System.Collections.Generic;
using UnityEngine;

public class WeatherEventManager : MonoBehaviour
{
    [Header("Reference to WeatherManager")]
    public WeatherManager weatherManager;

    [Header("Preset Events")]
    public List<WeatherEventSO> weatherEvents;

    [Header("Transition to Next Event")]
    public int duration = 0;
    private float currentDuration = 0;

    [Header("Debug Custom Event")]
    public bool injectCustomEvent = false;
    public WeatherEventSO customEvent;

    [Header("Selected Event")]
    public int selectedEventIndex = 0;

    [ContextMenu("Inject Selected Event")]

    private void Awake()
    {
        InjectSelectedEvent();
    }

    int lastSecond = 0;      // The last whole second we printed

    private void Update()
    {
        if (currentDuration >= duration) return;

        currentDuration += Time.deltaTime;
        int currentSecond = Mathf.FloorToInt(currentDuration);

        if (currentSecond > lastSecond && currentSecond % 5 == 0)
        {
            lastSecond = currentSecond;
            Debug.Log("Timer: " + currentSecond);
        }
    }

    public void InjectSelectedEvent()
    {
        if (weatherManager == null) return;
        if (weatherEvents == null || weatherEvents.Count == 0) return;

        selectedEventIndex = Mathf.Clamp(selectedEventIndex, 0, weatherEvents.Count - 1);
        InjectWeatherEvent(weatherEvents[selectedEventIndex]);
    }

    [ContextMenu("Inject Custom Event")]
    public void InjectCustom()
    {
        if (weatherManager == null) return;
        if (customEvent == null) return;

        InjectWeatherEvent(customEvent);
    }

    private void InjectWeatherEvent(WeatherEventSO evt)
    {
        Debug.Log("Injecting Weather Event: " + evt);
        Debug.Log("Transition duration: " + duration);
        currentDuration = 0;

        // Use the transition duration in the SO

        weatherManager.SetRainDropDensity(evt.rainDropDensity, duration);
        weatherManager.SetRainFallSpeed(evt.rainFallSpeed, duration);
        weatherManager.SetWind(evt.wind, duration);
        weatherManager.SetCloudCoverage(evt.cloudCoverage, duration);
        weatherManager.SetFogIntensity(evt.fogIntensity, duration);
        weatherManager.SetFogBrightness(evt.fogBrightness, duration);
        weatherManager.SetWaveAmplitude(evt.waveAmplitude, duration);
        weatherManager.SetWaveFrequency(evt.waveFrequency, duration);
        weatherManager.SetWaveSpeed(evt.waveSpeed, duration);
    }
}
