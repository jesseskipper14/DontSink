using UnityEngine;

/// <summary>
/// Authority-side validation seam for instant cutting actions.
/// The current tool architecture is hands-only, so this deliberately reuses
/// ToolUseChargeUtility instead of inventing knife-specific inventory logic.
/// </summary>
public static class CuttingAuthority
{
    public static bool TryCut(
        ICuttable target,
        in InteractContext context,
        ToolCapabilityDefinition requiredCapability,
        float maxRange,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (!GameplayAuthority.IsAuthoritative)
        {
            failureReason = "Not Authoritative";
            return false;
        }

        if (!IsLive(target) || !target.IsCuttable)
        {
            failureReason = "Cannot Cut";
            return false;
        }

        if (context.InteractorGO == null &&
            context.InteractorTransform == null)
        {
            failureReason = "No Requester";
            return false;
        }

        float allowedRange = Mathf.Max(0f, maxRange);
        float distance = Vector2.Distance(
            context.Origin,
            target.CutWorldPosition);

        if (distance > allowedRange)
        {
            failureReason = "Too Far";
            return false;
        }

        if (!ToolUseChargeUtility.CanUseHeldTool(
                context,
                requiredCapability,
                out _,
                out failureReason))
        {
            return false;
        }

        // Re-check immediately before mutation. Another authoritative action may
        // have severed the same relationship earlier this frame.
        if (!IsLive(target) || !target.IsCuttable)
        {
            failureReason = "Already Cut";
            return false;
        }

        target.ApplyCut();
        return true;
    }

    private static bool IsLive(ICuttable target)
    {
        if (target == null)
            return false;

        if (target is Object unityObject &&
            unityObject == null)
        {
            return false;
        }

        return true;
    }
}
