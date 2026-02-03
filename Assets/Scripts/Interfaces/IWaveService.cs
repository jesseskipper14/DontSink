using System;

public interface IWaveService
{
    // Wave properties & subscription
    float Amplitude { get; }
    float Frequency { get; }
    float Speed { get; }

    event Action<float> OnAmplitudeChanged;
    event Action<float> OnFrequencyChanged;
    event Action<float> OnSpeedChanged;

    // Core WaveField data access (renderers depend on these)
    float SampleHeight(float worldX);
    float SampleHeightAtWorldXWrapped(float worldX);
    float SampleHorizontalVelocity(float worldX);
    float SampleSurfaceVelocity(float worldX);
    void AddImpulse(float worldX, float totalForce, float radius = 2f);
}
