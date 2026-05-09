using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldItemContainerInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [SerializeField] private WorldItem worldItem;
    [SerializeField] private float maxDistance = 1.5f;
    [SerializeField] private int interactionPriority = 5;
    [SerializeField] private Transform promptAnchor;

    [Header("Boat Access")]
    [Tooltip("If true, world containers that belong to a Boat can only be opened by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, world containers not owned by or parented under a Boat remain usable. This preserves normal dock/world containers.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Container UI")]
    [SerializeField] private float autoCloseDistance = 2.25f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public int InteractionPriority => interactionPriority;

    private BoatOwnedItem _ownedItem;
    private Boat _cachedParentBoat;

    private void Reset()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoatContext();
    }

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoatContext();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (!IsInRange(context))
            return false;

        if (!CanAccessByBoatContext(context))
            return false;

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null)
            return false;

        if (!containerItem.IsContainer || containerItem.ContainerState == null)
            return false;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        ExternalContainerOverlayUI overlay = Object.FindFirstObjectByType<ExternalContainerOverlayUI>();
        if (overlay == null)
        {
            Debug.LogWarning("[WorldItemContainerInteractable] No ExternalContainerOverlayUI found.");
            return;
        }

        ItemInstance containerItem = worldItem != null ? worldItem.Instance : null;
        if (containerItem == null || !containerItem.IsContainer || containerItem.ContainerState == null)
            return;

        if (overlay.IsOpen && ReferenceEquals(overlay.CurrentContainer, containerItem))
        {
            overlay.Close();
            return;
        }

        string title = worldItem.Item != null ? worldItem.Item.DisplayName : "Container";
        overlay.Open(title, containerItem, transform, autoCloseDistance);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        return "Open";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxDistance;
    }

    private bool CanAccessByBoatContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoatContext();

        // Registry-authoritative loose item ownership.
        if (_ownedItem != null && _ownedItem.IsOwnedByBoat)
        {
            bool ok = IsInteractorBoardedOnBoatId(context, _ownedItem.OwningBoatInstanceId);

            Log(
                $"Access by BoatOwnedItem | item='{name}' ownedBoatId='{_ownedItem.OwningBoatInstanceId}' ok={ok}");

            return ok;
        }

        // Backward-compatible parented boat object access.
        // Useful for built-in boat fixtures that are actually under BoatRoot.
        if (_cachedParentBoat != null)
        {
            bool ok = IsInteractorBoardedOnBoat(context, _cachedParentBoat);

            Log(
                $"Access by parent Boat | item='{name}' boat='{_cachedParentBoat.name}' id='{_cachedParentBoat.BoatInstanceId}' ok={ok}");

            return ok;
        }

        // Normal world/dock containers.
        // If the interactor is currently boarded, do NOT allow access to unowned world objects.
        // This prevents inside-boat players from reaching outside containers through the hull.
        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding != null && boarding.IsBoarded)
        {
            Log($"Access denied: interactor is boarded, but container '{name}' is not owned by/parented to that boat.");
            return false;
        }

        return allowAccessWhenNotPartOfBoat;
    }

    private bool IsInteractorBoardedOnBoatId(in InteractContext context, string boatInstanceId)
    {
        if (string.IsNullOrWhiteSpace(boatInstanceId))
            return false;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        Boat currentBoat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (currentBoat == null)
            return false;

        return currentBoat.BoatInstanceId == boatInstanceId;
    }

    private bool IsInteractorBoardedOnBoat(in InteractContext context, Boat requiredBoat)
    {
        if (requiredBoat == null)
            return false;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        Boat currentBoat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (currentBoat == null)
            return false;

        if (!string.IsNullOrWhiteSpace(requiredBoat.BoatInstanceId) &&
            !string.IsNullOrWhiteSpace(currentBoat.BoatInstanceId))
        {
            return currentBoat.BoatInstanceId == requiredBoat.BoatInstanceId;
        }

        return currentBoat == requiredBoat || boarding.CurrentBoatRoot == requiredBoat.transform;
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO = context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private void CacheBoatContext()
    {
        if (_ownedItem == null)
            _ownedItem = GetComponent<BoatOwnedItem>();

        if (_cachedParentBoat == null)
            _cachedParentBoat = GetComponentInParent<Boat>();
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[WorldItemContainerInteractable:{name}] {msg}", this);
    }
}