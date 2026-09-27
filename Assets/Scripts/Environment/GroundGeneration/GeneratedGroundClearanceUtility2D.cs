using UnityEngine;

/// <summary>
/// Shared generated-ground placement safety helpers.
///
/// Generated ground in BoatScene/NodeScene is an EdgeCollider2D surface, so a
/// teleport can place a body below it without ever crossing the collision edge.
/// These helpers answer the question the normal solver cannot: "would this body
/// center leave its solid collider below the generated surface at this X?"
/// </summary>
public static class GeneratedGroundClearanceUtility2D
{
    public static bool TryGetRequiredUpwardCorrection(
        GeneratedGroundSampler2D sampler,
        Collider2D solidCollider,
        Vector2 intendedColliderCenter,
        float clearance,
        out float correction,
        out float groundY)
    {
        correction = 0f;
        groundY = 0f;

        if (sampler == null ||
            solidCollider == null ||
            !solidCollider.enabled ||
            solidCollider.isTrigger)
        {
            return false;
        }

        if (!sampler.TrySampleGround(
                intendedColliderCenter.x,
                out groundY,
                out _))
        {
            return false;
        }

        // Bounds already include the collider's current rotation/scale. Player
        // transfer code positions the primary collider by its bounds center, so
        // this is the matching conservative vertical footprint for exit safety.
        float halfHeight = Mathf.Max(0f, solidCollider.bounds.extents.y);
        float intendedBottom = intendedColliderCenter.y - halfHeight;
        float minimumBottom = groundY + Mathf.Max(0f, clearance);

        correction = Mathf.Max(0f, minimumBottom - intendedBottom);
        return true;
    }

    public static bool IsPlacementClear(
        GeneratedGroundSampler2D sampler,
        Collider2D solidCollider,
        Vector2 intendedColliderCenter,
        float clearance,
        float tolerance,
        out float requiredUpwardCorrection)
    {
        requiredUpwardCorrection = 0f;

        if (!TryGetRequiredUpwardCorrection(
                sampler,
                solidCollider,
                intendedColliderCenter,
                clearance,
                out requiredUpwardCorrection,
                out _))
        {
            // No generated ground at this X (or no sampler) means this utility
            // has no reason to reject the authored placement.
            return true;
        }

        return requiredUpwardCorrection <= Mathf.Max(0f, tolerance);
    }
}
