using System;

public interface IRainService
{
    void Initialize(IWeatherService weatherService);
    /// <summary>
    /// Current rain intensity [0..1].
    /// </summary>
    float RainDropDensity { get; }
    float RainFallSpeed { get; }

    /// <summary>
    /// Fired whenever rain intensity changes.
    /// </summary>
    event Action<float> OnRainDropDensityChanged;
    event Action<float> OnRainFallSpeedChanged;

}
