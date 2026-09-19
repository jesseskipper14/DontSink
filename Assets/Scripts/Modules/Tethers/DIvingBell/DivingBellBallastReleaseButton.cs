using UnityEngine;

/// <summary>
/// Physical emergency ballast-release control mounted inside a diving bell.
///
/// This button owns no ballast state. It validates that the requesting player is
/// allowed to operate this exact bell, then delegates the actual authoritative
/// jettison to DivingBellBallastSystem.TryDumpAllBallast().
///
/// Both ballast slots are released in one action. Each stored ItemInstance is
/// converted back into its ordinary WorldItem at that slot's authored DumpPoint.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellBallastReleaseButton :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider,
    IInteractionPromptDisplayPolicyProvider
{
    [Header("Ballast")]
    [SerializeField] private DivingBellBallastSystem ballastSystem;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 90;
    [SerializeField, Min(0.1f)] private float maxUseDistance = 1.25f;
    [SerializeField] private Transform promptAnchor;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public int InteractionPriority => interactionPriority;

    private void Reset()
    {
        ResolveRefs();
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        ResolveRefs();
        EnsureTriggerCollider();
    }

    public bool CanInteract(in InteractContext context)
    {
        ResolveRefs();

        if (ballastSystem == null ||
            !ballastSystem.StateAuthority ||
            context.InteractorGO == null ||
            !IsInRange(context))
        {
            return false;
        }

        if (!ballastSystem.CanRequesterUseBallast(
                context.InteractorGO,
                out _))
        {
            return false;
        }

        return HasAnyBallastLoaded();
    }

    public void Interact(in InteractContext context)
    {
        ResolveRefs();

        if (ballastSystem == null ||
            context.InteractorGO == null ||
            !IsInRange(context))
        {
            return;
        }

        if (!ballastSystem.CanRequesterUseBallast(
                context.InteractorGO,
                out string reason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
                GameMessageService.PostWarning(reason);

            return;
        }

        int dumpedCount =
            ballastSystem.TryDumpAllBallast(
                context.InteractorGO);

        if (dumpedCount <= 0)
        {
            GameMessageService.PostWarning(
                "NO BALLAST LOADED");
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[DivingBellBallastReleaseButton:{name}] " +
                $"dumpedCount={dumpedCount} " +
                $"requester='{context.InteractorGO.name}'",
                this);
        }
    }

    public string GetPromptVerb(in InteractContext context)
    {
        return "Release Ballast";
    }

    public Transform GetPromptAnchor()
    {
        return
            promptAnchor != null
                ? promptAnchor
                : transform;
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        return "Emergency Ballast Release";
    }

    public bool ShouldShowHoverLabel(in InteractContext context)
    {
        return CanInteract(context);
    }

    private bool HasAnyBallastLoaded()
    {
        if (ballastSystem == null)
            return false;

        return
            ballastSystem.GetBallast(
                DivingBellBallastSystem.LeftSlotIndex) != null ||
            ballastSystem.GetBallast(
                DivingBellBallastSystem.RightSlotIndex) != null;
    }

    private bool IsInRange(in InteractContext context)
    {
        Vector2 anchor =
            promptAnchor != null
                ? (Vector2)promptAnchor.position
                : (Vector2)transform.position;

        return
            Vector2.Distance(
                context.Origin,
                anchor) <=
            maxUseDistance;
    }

    private void ResolveRefs()
    {
        if (ballastSystem == null)
        {
            ballastSystem =
                GetComponentInParent<DivingBellBallastSystem>(
                    true);
        }

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void EnsureTriggerCollider()
    {
        Collider2D col =
            GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }
}
