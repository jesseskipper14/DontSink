using System;
using UnityEngine;

/// <summary>
/// Base class for components that provide player-facing text to a DynamicTextBinder.
/// Sources own meaning/data; the binder owns presentation.
/// </summary>
public abstract class DynamicTextSourceBehaviour : MonoBehaviour
{
    public event Action TextChanged;

    /// <summary>
    /// Returns true when this source currently has a valid value to display.
    /// An empty string may still be a valid value.
    /// </summary>
    public abstract bool TryGetText(out string text);

    /// <summary>
    /// Call when the source value changes so bound text can refresh immediately.
    /// </summary>
    protected void NotifyTextChanged()
    {
        TextChanged?.Invoke();
    }
}
