using System;
using UnityEngine;

public interface ICloudService
{
    void Initialize(IBrightnessService brightnessService, ISunriseSunsetService sunriseSunsetService, IWeatherService weatherService);
    void UpdateBrightness(float brightness);
    void UpdateSunTint(float sunrise);
    void SetCoverage(float coverage01, float duration);

    float CloudCoverage { get; }
    event Action<float> OnCloudCoverageChanged;
}