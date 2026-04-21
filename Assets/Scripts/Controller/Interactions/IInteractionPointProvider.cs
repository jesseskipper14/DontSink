using UnityEngine;

public interface IInteractionPointProvider
{
    Vector2 GetClosestInteractionPoint(Vector2 fromWorldPoint);
}