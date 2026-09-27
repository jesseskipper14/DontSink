using UnityEngine;

/// <summary>
/// Lightweight source for the shared contextual-control hint overlay.
/// Providers own the meaning/state; the overlay owns presentation.
/// </summary>
public interface IContextHintProvider
{
    int ContextHintPriority { get; }

    bool TryGetContextHint(
        out string text,
        out Transform worldAnchor);
}
