using System;

public interface ISunriseSunsetService
{
    void Initialize(ITimeOfDayService timeService, ICloudService cloudService);

    /// <summary>
    /// Normalized sunrise/sunset intensity [0–1]
    /// </summary>
    float Tint01 { get; }

    /// <summary>
    /// Fired whenever tint intensity changes
    /// </summary>
    event Action<float> OnTintChanged;
}
