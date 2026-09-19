using UnityEngine;

/// <summary>
/// One physical/internal bell control button.
///
/// Author three child trigger objects under the bell console and configure them
/// as Lower, Stop, and Raise. The button itself owns no winch state; it delegates
/// validation/routing to DivingBellWinchControlPanel.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellWinchControlButton :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider,
    IInteractionPromptDisplayPolicyProvider
{
    [Header("Control")]
    [SerializeField]
    private WinchControlIntent intent =
        WinchControlIntent.Stop;

    [SerializeField] private DivingBellWinchControlPanel panel;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 85;
    [SerializeField, Min(0.1f)] private float maxUseDistance = 1.25f;
    [SerializeField] private Transform promptAnchor;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public int InteractionPriority =>
        interactionPriority;

    private void Reset()
    {
        ResolveRefs();

        Collider2D col =
            GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        ResolveRefs();

        Collider2D col =
            GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    public bool CanInteract(
        in InteractContext context)
    {
        ResolveRefs();

        if (panel == null ||
            context.InteractorGO == null ||
            !IsInRange(context) ||
            !panel.IsIntentAllowedInternally(intent))
        {
            return false;
        }

        return
            panel.CanControl(
                context.InteractorGO,
                out _);
    }

    public void Interact(
        in InteractContext context)
    {
        ResolveRefs();

        if (panel == null ||
            context.InteractorGO == null ||
            !IsInRange(context))
        {
            return;
        }

        bool ok =
            panel.TryApplyControlIntent(
                context.InteractorGO,
                intent,
                out string message);

        if (!ok &&
            !string.IsNullOrWhiteSpace(message))
        {
            GameMessageService.PostWarning(
                message);
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[DivingBellWinchControlButton:{name}] " +
                $"intent={intent} success={ok} message='{message}'",
                this);
        }
    }

    public string GetPromptVerb(
        in InteractContext context)
    {
        switch (intent)
        {
            case WinchControlIntent.Lower:
                return "Lower Bell";

            case WinchControlIntent.Raise:
                return "Raise Bell";

            case WinchControlIntent.Stop:
                return "Stop Winch";

            case WinchControlIntent.QuickRelease:
                return "Release Winch Brake";

            case WinchControlIntent.CutLine:
                return "Cut Winch Line";

            default:
                return "Use Winch Control";
        }
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
        switch (intent)
        {
            case WinchControlIntent.Lower:
                return "Bell Winch - Lower";

            case WinchControlIntent.Raise:
                return "Bell Winch - Raise";

            case WinchControlIntent.Stop:
                return "Bell Winch - Stop";

            default:
                return "Bell Winch Control";
        }
    }

    public bool ShouldShowHoverLabel(
        in InteractContext context)
    {
        return
            CanInteract(
                context);
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
        if (panel == null)
        {
            panel =
                GetComponentInParent<DivingBellWinchControlPanel>(
                    true);
        }

        if (promptAnchor == null)
            promptAnchor = transform;
    }
}
