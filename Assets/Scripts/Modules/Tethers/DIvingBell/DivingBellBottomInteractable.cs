using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bottom-opening interaction for a DivingBellOccupancy.
///
/// This is deliberately separate from DivingBellBoardInteractable:
///     docked   -> front door is authoritative
///     undocked -> bottom opening is authoritative
///
/// The interactable only emits enter/exit intent. DivingBellOccupancy remains
/// authoritative for occupancy, hierarchy, collision-context events, and transfer.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellBottomInteractable :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractPromptActionProvider,
    IInteractionLabelProvider,
    IInteractionPromptDisplayPolicyProvider
{
    [Header("Interaction")]
    [SerializeField]
    private int priority =
        75;

    [SerializeField, Min(0.1f)]
    private float maxUseDistance =
        1.6f;

    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Tooltip(
        "Optional prompt/range anchor at the bottom opening. Falls back to this transform.")]
    [SerializeField] private Transform promptAnchor;

    [Header("Prompt")]
    [SerializeField]
    private string enterPrompt =
        "Enter Diving Bell";

    [SerializeField]
    private string exitPrompt =
        "Exit Diving Bell";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    public int InteractionPriority =>
        priority;

    private void Awake()
    {
        Collider2D col =
            GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;

        ResolveRefs();
    }

    private void Reset()
    {
        ResolveRefs();
    }

    public bool CanInteract(
        in InteractContext context)
    {
        ResolveRefs();

        if (occupancy == null ||
            context.InteractorGO == null ||
            !IsInRange(
                context))
        {
            return false;
        }

        if (occupancy.Contains(
                context.InteractorGO))
        {
            return
                occupancy.CanExitBottom(
                    context.InteractorGO,
                    out _);
        }

        // Bottom access is the deployed/world-side entrance. A player who is
        // currently boarded on a boat must not be able to reach through the boat
        // presentation/geometry and target this opening. Once unboarded, ordinary
        // deployed-bell entry rules become authoritative again.
        if (IsInteractorBoarded(
                context))
        {
            return false;
        }

        return
            occupancy.CanEnterBottom(
                context.InteractorGO,
                out _);
    }

    public void Interact(
        in InteractContext context)
    {
        ResolveRefs();

        if (occupancy == null ||
            context.InteractorGO == null ||
            !IsInRange(
                context))
        {
            return;
        }

        bool exiting =
            occupancy.Contains(
                context.InteractorGO);

        // Defensive mirror of CanInteract. Interaction resolution normally calls
        // CanInteract first, but never let a direct invocation bypass the boarding
        // context rule.
        if (!exiting &&
            IsInteractorBoarded(
                context))
        {
            return;
        }

        bool success =
            exiting
                ? occupancy.TryExitBottom(
                    context.InteractorGO,
                    out string message)
                : occupancy.TryEnterBottom(
                    context.InteractorGO,
                    out message);

        if (!success)
        {
            if (!string.IsNullOrWhiteSpace(
                    message))
            {
                GameMessageService.PostWarning(
                    message);
            }

            if (verboseLogging)
            {
                Debug.LogWarning(
                    $"[DivingBellBottomInteractable:{name}] {message}",
                    this);
            }

            return;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[DivingBellBottomInteractable:{name}] {message}",
                this);
        }
    }

    public string GetPromptVerb(
        in InteractContext context)
    {
        ResolveRefs();

        if (occupancy != null &&
            context.InteractorGO != null &&
            occupancy.Contains(
                context.InteractorGO))
        {
            return exitPrompt;
        }

        return enterPrompt;
    }

    public Transform GetPromptAnchor()
    {
        return
            promptAnchor != null
                ? promptAnchor
                : transform;
    }

    public string GetInteractionLabel(
        in InteractContext context)
    {
        return
            "Diving Bell";
    }

    public bool ShouldShowHoverLabel(
        in InteractContext context)
    {
        return
            CanInteract(
                context);
    }

    public void GetPromptActions(
        in InteractContext context,
        List<PromptAction> actions)
    {
        if (actions == null ||
            !CanInteract(
                context))
        {
            return;
        }

        string verb =
            GetPromptVerb(
                context);

        actions.Add(
            new PromptAction(
                $"Press E to {verb}",
                priority: 100));
    }

    private static bool IsInteractorBoarded(
        in InteractContext context)
    {
        PlayerBoardingState boarding =
            null;

        if (context.InteractorGO != null)
        {
            boarding =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>() ??
                context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);
        }

        if (boarding == null &&
            context.InteractorTransform != null)
        {
            boarding =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();
        }

        return
            boarding != null &&
            boarding.IsBoarded;
    }

    private bool IsInRange(
        in InteractContext context)
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
        if (occupancy == null)
        {
            occupancy =
                GetComponentInParent<DivingBellOccupancy>();

            if (occupancy == null)
            {
                occupancy =
                    GetComponentInChildren<DivingBellOccupancy>(
                        true);
            }
        }

        if (promptAnchor == null)
            promptAnchor = transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxUseDistance =
            Mathf.Max(
                0.1f,
                maxUseDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                0.15f,
                0.85f,
                1f,
                0.9f);

        Transform anchor =
            promptAnchor != null
                ? promptAnchor
                : transform;

        Gizmos.DrawWireSphere(
            anchor.position,
            maxUseDistance);
    }
#endif
}
