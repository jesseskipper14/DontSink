using UnityEngine;

[DisallowMultipleComponent]
public sealed class MoneyChestInteractable :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractionLabelProvider,
    IInteractionRangeProvider
{
    [Header("Refs")]
    [SerializeField] private MoneyChestState chest;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 35;
    [SerializeField, Min(0f)] private float hoverNameRange = 4.0f;
    [SerializeField, Min(0f)] private float actionRange = 1.75f;

    [Header("Recovery")]
    [SerializeField] private bool requireBoardedPlayerForLostRecovery = true;
    [SerializeField] private bool requireChestInsideBoatBoardedVolume = true;
    [SerializeField] private bool destroyRetiredRecoveredChest = true;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = true;

    public int InteractionPriority => interactionPriority;

    private void Reset()
    {
        if (chest == null)
            chest = GetComponent<MoneyChestState>();

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    private void Awake()
    {
        if (chest == null)
            chest = GetComponent<MoneyChestState>();

        if (chest == null)
            chest = GetComponentInParent<MoneyChestState>();

        if (promptAnchor == null)
            promptAnchor = transform;
    }

    public bool CanInteract(in InteractContext context)
    {
        if (chest == null)
            return false;

        if (chest.IsRetired)
            return false;

        if (!IsInRange(context))
            return false;

        if (chest.IsActive)
            return true;

        if (chest.IsLost)
            return CanRecoverLostChest(context);

        return false;
    }

    public void Interact(in InteractContext context)
    {
        if (chest == null)
            return;

        if (chest.IsRetired)
            return;

        if (!IsInRange(context))
            return;

        if (chest.IsLost)
        {
            RecoverLostChest(context);
            return;
        }

        if (chest.IsActive)
        {
            OpenActiveChestPlaceholder();
            return;
        }
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (chest == null)
            return "Inspect";

        if (chest.IsLost)
            return "Recover Chest";

        if (chest.IsActive)
            return "Open Chest";

        return "Inspect";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public string GetInteractionLabel(in InteractContext context)
    {
        if (chest == null)
            return "Money Chest";

        if (chest.IsLost)
            return "Lost Money Chest";

        if (chest.IsActive)
            return "Money Chest";

        if (chest.IsRetired)
            return "Empty Money Chest";

        return "Money Chest";
    }

    public bool TryGetHoverNameRange(out float range)
    {
        range = hoverNameRange;
        return true;
    }

    public bool TryGetActionRange(out float range)
    {
        range = actionRange;
        return true;
    }

    private bool IsInRange(in InteractContext context)
    {
        if (context.InteractorTransform == null)
            return false;

        Vector2 origin = context.Origin;
        Vector2 target = promptAnchor != null ? promptAnchor.position : transform.position;

        return Vector2.Distance(origin, target) <= actionRange;
    }

    private bool CanRecoverLostChest(in InteractContext context)
    {
        if (chest == null || !chest.IsLost || chest.IsRetired)
            return false;

        BoatBoardedVolume chestVolume = null;

        if (requireChestInsideBoatBoardedVolume)
        {
            if (!BoatBoardedVolume.TryFindContainingVolume(chest, out chestVolume))
                return false;
        }

        if (!requireBoardedPlayerForLostRecovery)
            return true;

        if (context.InteractorGO == null)
            return false;

        PlayerBoardingState boarding = context.InteractorGO.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        if (requireChestInsideBoatBoardedVolume)
        {
            if (chestVolume == null)
                return false;

            if (boarding.CurrentBoatRoot != chestVolume.BoatRoot)
                return false;
        }

        return true;
    }

    private void RecoverLostChest(in InteractContext context)
    {
        if (!CanRecoverLostChest(context))
        {
            if (logDebugMessages)
            {
                Debug.Log(
                    $"Cannot recover lost money chest. " +
                    $"ChestInsideBoatVolume={BoatBoardedVolume.IsInsideAnyVolume(chest)} " +
                    $"Interactor='{(context.InteractorGO != null ? context.InteractorGO.name : "NULL")}'",
                    this);
            }

            return;
        }

        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
        if (treasury == null)
        {
            Debug.LogWarning("Cannot recover lost money chest because no MoneyChestTreasuryService exists.", this);
            return;
        }

        int recoveredFunds = chest.Balance;
        bool recovered = treasury.RecoverLostChest(chest);

        if (!recovered)
        {
            Debug.LogWarning(
                $"Treasury refused lost chest recovery. ChestId='{chest.ChestInstanceId}', Balance={chest.Balance}",
                this);

            return;
        }

        if (logDebugMessages)
        {
            Debug.Log(
                $"Recovered lost money chest. RecoveredFunds={recoveredFunds}, ActiveBalance={treasury.ActiveBalance}",
                this);
        }

        if (destroyRetiredRecoveredChest && chest.IsRetired)
            Destroy(gameObject);
    }

    private void OpenActiveChestPlaceholder()
    {
        MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;

        int shownBalance =
            treasury != null && treasury.ActiveChest == chest
                ? treasury.ActiveBalance
                : chest.Balance;

        if (logDebugMessages)
        {
            Debug.Log(
                $"Open Money Chest placeholder. ChestId='{chest.ChestInstanceId}', Balance={shownBalance}",
                this);
        }

        // Next slice:
        // MiniGameOverlayHost opens MoneyChestOverlayRunner here.
        // For now, this proves interaction routing without dragging the shiny coin theater
        // into the sacred plumbing, because we have suffered enough.
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Open Active Chest Placeholder")]
    private void DebugOpenActiveChestPlaceholder()
    {
        OpenActiveChestPlaceholder();
    }

    [ContextMenu("Debug/Recover Lost Chest Without Context")]
    private void DebugRecoverLostChestWithoutContext()
    {
        bool oldRequireBoarded = requireBoardedPlayerForLostRecovery;
        bool oldRequireVolume = requireChestInsideBoatBoardedVolume;

        requireBoardedPlayerForLostRecovery = false;
        requireChestInsideBoatBoardedVolume = false;

        InteractContext fakeContext = new InteractContext(
            gameObject,
            transform,
            transform.position,
            Vector2.right);

        RecoverLostChest(fakeContext);

        requireBoardedPlayerForLostRecovery = oldRequireBoarded;
        requireChestInsideBoatBoardedVolume = oldRequireVolume;
    }
#endif
}