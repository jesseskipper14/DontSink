using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Front-door interaction for a DivingBellOccupancy.
///
/// This class only emits the player's board/exit intent into DivingBellOccupancy.
/// It never changes PlayerBoardingState itself.
///
/// V1 dry-pass rule:
///     docked + outside bell -> Board Diving Bell
///     docked + occupant     -> Leave Diving Bell
///     undocked              -> front interaction unavailable
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellBoardInteractable :
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
        70;

    [SerializeField, Min(0.1f)]
    private float maxUseDistance =
        1.8f;

    [Header("Bell")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Tooltip(
        "Optional prompt anchor at the visible front door. Falls back to this transform.")]
    [SerializeField] private Transform promptAnchor;

    [Header("Prompt")]
    [SerializeField]
    private string boardPrompt =
        "Board Diving Bell";

    [SerializeField]
    private string exitPrompt =
        "Leave Diving Bell";

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
                occupancy.CanExitFront(
                    context.InteractorGO,
                    out _);
        }

        return
            occupancy.CanEnterFront(
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

        bool success =
            exiting
                ? occupancy.TryExitFront(
                    context.InteractorGO,
                    out string message)
                : occupancy.TryEnterFront(
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
                    $"[DivingBellBoardInteractable:{name}] {message}",
                    this);
            }

            return;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[DivingBellBoardInteractable:{name}] {message}",
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

        return boardPrompt;
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
                1f,
                0.75f,
                0.15f,
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
