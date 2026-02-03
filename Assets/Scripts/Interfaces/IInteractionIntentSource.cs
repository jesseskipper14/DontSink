using UnityEngine;

public struct InteractionIntent
{
    public bool InteractPressed;   // rising edge
    public Vector2 AimWorld;       // optional (mouse/world aim), can be zero
}

public interface IInteractionIntentSource
{
    InteractionIntent Current { get; }
}
