using UnityEngine;

/// <summary>
/// Physical left/right ballast mount on a diving bell.
///
/// This component is intentionally thin. It owns presentation, interaction range,
/// and the world-drop bridge; DivingBellBallastSystem remains the authoritative
/// state mutator so future client -> host routing has one seam.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellBallastSlot :
    MonoBehaviour,
    IWorldItemDropTarget,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider
{
    [Header("Slot")]
    [Tooltip("0 = left ballast, 1 = right ballast.")]
    [SerializeField, Range(0, 1)] private int slotIndex;
    [SerializeField] private DivingBellBallastSystem ballastSystem;

    [Header("Physical Points")]
    [Tooltip(
        "Where this secured ballast contributes mass/COM. Put this at the visual " +
        "center of the ballast mount near the bottom of the bell.")]
    [SerializeField] private Transform contributionPoint;

    [Tooltip(
        "Where emergency-jettisoned ballast appears. Put this just OUTSIDE the bell " +
        "collider so it does not spawn trapped in the shell.")]
    [SerializeField] private Transform dumpPoint;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer ballastRenderer;
    [SerializeField] private bool preferWorldPrefabSprite = true;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 75;
    [SerializeField, Min(0.1f)] private float actionRange = 1.5f;
    [SerializeField, Min(0.1f)] private float maxDepositDistance = 2.25f;
    [SerializeField] private Transform promptAnchor;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    public int SlotIndex => slotIndex;
    public int InteractionPriority => interactionPriority;

    public Transform ContributionPoint =>
        contributionPoint != null
            ? contributionPoint
            : transform;

    public Transform DumpPoint =>
        dumpPoint != null
            ? dumpPoint
            : ContributionPoint;

    private void Reset()
    {
        if (ballastSystem == null)
            ballastSystem = GetComponentInParent<DivingBellBallastSystem>();

        if (contributionPoint == null)
            contributionPoint = transform;

        if (promptAnchor == null)
            promptAnchor = transform;

        ResolveRenderer();
        EnsureColliderIsTrigger();
    }

    private void Awake()
    {
        ResolveRefs();
        EnsureColliderIsTrigger();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (ballastSystem != null)
        {
            ballastSystem.RegisterSlot(this);
            ballastSystem.BallastChanged += HandleBallastChanged;
        }

        RefreshVisual();
    }

    private void OnDisable()
    {
        if (ballastSystem != null)
        {
            ballastSystem.BallastChanged -= HandleBallastChanged;
            ballastSystem.UnregisterSlot(this);
        }
    }

    private void OnValidate()
    {
        slotIndex = Mathf.Clamp(slotIndex, 0, 1);

        if (contributionPoint == null)
            contributionPoint = transform;

        if (promptAnchor == null)
            promptAnchor = transform;

        EnsureColliderIsTrigger();
    }

    public bool CanAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance incoming)
    {
        ResolveRefs();

        if (ballastSystem == null ||
            !IsRequesterInDepositRange(
                in context))
        {
            return false;
        }

        bool ok =
            ballastSystem.CanAcceptBallast(
                slotIndex,
                incoming,
                context.Requester,
                out string reason);

        Log(
            $"CanAcceptWorldDrop slot={slotIndex} ok={ok} reason='{reason}'");

        return ok;
    }

    public bool TryAcceptWorldDrop(
        in WorldItemDropContext context,
        ItemInstance incoming,
        out ItemInstance remainder)
    {
        remainder = incoming;

        ResolveRefs();

        if (ballastSystem == null ||
            !IsRequesterInDepositRange(
                in context))
        {
            return false;
        }

        bool ok =
            ballastSystem.TryInsertBallast(
                slotIndex,
                incoming,
                context.Requester,
                out remainder,
                out string message);

        Log(
            $"TryAcceptWorldDrop slot={slotIndex} ok={ok} message='{message}'");

        if (ok)
            RefreshVisual();

        return ok;
    }

    public bool CanInteract(
        in InteractContext context)
    {
        ResolveRefs();

        if (ballastSystem == null ||
            !IsInRange(context) ||
            ballastSystem.GetBallast(slotIndex) == null)
        {
            return false;
        }

        return
            ballastSystem.CanRequesterUseBallast(
                context.InteractorGO,
                out _);
    }

    public void Interact(
        in InteractContext context)
    {
        if (!CanInteract(context) ||
            ballastSystem == null)
        {
            return;
        }

        bool ok =
            ballastSystem.TryTakeBallast(
                slotIndex,
                context.InteractorGO,
                out string message);

        Log(
            $"Interact slot={slotIndex} ok={ok} message='{message}'");

        if (ok)
            RefreshVisual();
    }

    public string GetPromptVerb(
        in InteractContext context)
    {
        return
            ballastSystem != null &&
            ballastSystem.GetBallast(slotIndex) != null
                ? "Take Ballast"
                : null;
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
        ItemInstance item =
            ballastSystem != null
                ? ballastSystem.GetBallast(slotIndex)
                : null;

        if (item != null &&
            item.Definition != null)
        {
            return
                $"{GetSideName()} Ballast: {item.Definition.DisplayName}";
        }

        return
            $"{GetSideName()} Ballast Slot";
    }

    public void RefreshVisual()
    {
        ResolveRefs();

        if (ballastRenderer == null)
            return;

        ItemInstance item =
            ballastSystem != null
                ? ballastSystem.GetBallast(slotIndex)
                : null;

        Sprite sprite =
            item != null &&
            item.Definition != null
                ? ResolveSprite(item.Definition)
                : null;

        ballastRenderer.sprite =
            sprite;

        ballastRenderer.enabled =
            sprite != null;
    }

    private Sprite ResolveSprite(
        ItemDefinition definition)
    {
        if (definition == null)
            return null;

        if (preferWorldPrefabSprite &&
            definition.WorldPrefab != null)
        {
            SpriteRenderer source =
                definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(
                    true);

            if (source != null &&
                source.sprite != null)
            {
                return source.sprite;
            }
        }

        return definition.Icon;
    }

    private bool IsRequesterInDepositRange(
        in WorldItemDropContext context)
    {
        if (!context.HasRequester)
            return false;

        Vector2 point =
            ContributionPoint.position;

        return
            Vector2.Distance(
                context.Origin,
                point) <=
            Mathf.Max(0.1f, maxDepositDistance);
    }

    private bool IsInRange(
        in InteractContext context)
    {
        Vector2 point =
            promptAnchor != null
                ? promptAnchor.position
                : transform.position;

        return
            Vector2.Distance(
                context.Origin,
                point) <=
            Mathf.Max(0.1f, actionRange);
    }

    private void HandleBallastChanged(
        DivingBellBallastSystem changedSystem)
    {
        if (changedSystem == ballastSystem)
            RefreshVisual();
    }

    private void ResolveRefs()
    {
        if (ballastSystem == null)
            ballastSystem = GetComponentInParent<DivingBellBallastSystem>();

        if (contributionPoint == null)
            contributionPoint = transform;

        if (promptAnchor == null)
            promptAnchor = transform;

        ResolveRenderer();
    }

    private void ResolveRenderer()
    {
        if (ballastRenderer != null)
            return;

        ballastRenderer =
            GetComponent<SpriteRenderer>() ??
            GetComponentInChildren<SpriteRenderer>(true);
    }

    private void EnsureColliderIsTrigger()
    {
        Collider2D collider =
            GetComponent<Collider2D>();

        if (collider != null)
            collider.isTrigger = true;
    }

    private string GetSideName()
    {
        return
            slotIndex == DivingBellBallastSystem.LeftSlotIndex
                ? "Left"
                : "Right";
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[DivingBellBallastSlot:{name}] {message}",
            this);
    }
}
