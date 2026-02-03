using System;

public interface IFogService
{
    void Initialize(IWeatherService weatherService);
    /// <summary>
    /// Current fog intensity [0..1].
    /// </summary>
    float FogIntensity { get; }
    float FogBrightness { get; }

    /// <summary>
    /// Fired whenever fog intensity changes.
    /// </summary>
    event Action<float> OnFogIntensityChanged;
    event Action<float> OnFogBrightnessChanged;
}
