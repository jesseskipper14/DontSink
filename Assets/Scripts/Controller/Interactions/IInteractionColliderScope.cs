using UnityEngine;

/// <summary>
/// Optional source-collider filter for interaction owners.
///
/// Interactor2D may discover an interaction component by walking upward from a
/// hit child collider. Implement this interface when only specific authored
/// colliders are allowed to represent that interaction owner.
///
/// Returning false is an explicit branch veto: Interactor2D will not continue
/// climbing above that rejected owner and reinterpret the same collider as some
/// unrelated ancestor interaction.
/// </summary>
public interface IInteractionColliderScope
{
    bool AllowsInteractionCollider(Collider2D sourceCollider);
}
