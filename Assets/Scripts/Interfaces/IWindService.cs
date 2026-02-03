using System;
using UnityEngine;

public interface IWindService
{
    void Initialize(IWeatherService weatherService);

    /// <summary>
    /// Normalized wind strength [0..1].
    /// </summary>
    float WindStrength01 { get; }

    /// <summary>
    /// Wind direction in 2D (normalized).
    /// </summary>
    Vector2 WindDirection { get; }

    /// <summary>
    /// Maximum wind speed (world units / second).
    /// </summary>
    float MaxWindSpeed { get; }

    /// <summary>
    /// Final wind velocity in world space.
    /// </summary>
    Vector2 WindVelocity { get; }

    /// <summary>
    /// Fired whenever wind strength changes.
    /// </summary>
    event Action<float> OnWindChanged;

    /// <summary>
    /// Fired whenever wind direction changes.
    /// </summary>
    event Action<Vector2> OnWindDirectionChanged;
}
