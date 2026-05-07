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

    [Tooltip("If true, world containers not under a Boat remain usable. This preserves normal dock/world containers.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Container UI")]
    [SerializeField] private float autoCloseDistance = 2.25f;

    public int InteractionPriority => interactionPriority;

    private Boat _cachedBoat;

    private void Reset()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (worldItem == null)
            worldItem = GetComponent<WorldItem>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
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

        CacheBoat();

        if (_cachedBoat == null)
            return allowAccessWhenNotPartOfBoat;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        return boarding.CurrentBoatRoot == _cachedBoat.transform;
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

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }
}