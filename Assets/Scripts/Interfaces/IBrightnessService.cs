using System;

public interface IBrightnessService
{
    /// <summary>
    /// Normalized global brightness [0,1]
    /// </summary>
    float Brightness01 { get; }

    /// <summary>
    /// Fired whenever brightness changes
    /// </summary>
    event Action<float> OnBrightnessChanged;

    void Register(UnityEngine.SpriteRenderer sr);
    void Unregister(UnityEngine.SpriteRenderer sr);
}
