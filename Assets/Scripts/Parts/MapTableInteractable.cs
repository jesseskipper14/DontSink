using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapTableInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 40;
    [SerializeField] private float maxUseDistance = 1.8f;

    [Header("Map")]
    [SerializeField] private MapOverlayToggleService mapService;
    [SerializeField] private bool toggle = true;

    [Header("Boat Access")]
    [Tooltip("If true, map tables that belong to a Boat can only be used by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, map tables not under a Boat remain usable. Useful for future dock/world map tables.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    public int InteractionPriority => priority;

    private Boat _cachedBoat;

    public string GetPromptVerb(in InteractContext context)
    {
        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        return "Chart";
    }

    public Transform GetPromptAnchor() => transform;

    private void Reset()
    {
        CacheBoat();
    }

    private void Awake()
    {
        CacheBoat();

        if (mapService == null)
            mapService = FindAnyObjectByType<MapOverlayToggleService>();

        if (mapService == null)
            Debug.LogError($"{name}: Missing MapOverlayToggleService in scene.", this);
    }

    public bool CanInteract(in InteractContext context)
    {
        if (mapService == null)
            return false;

        if (!IsInRange(context))
            return false;

        if (!CanAccessByBoatContext(context))
            return false;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        if (toggle)
            mapService.Toggle(transform);
        else
            mapService.Open(transform);
    }

    private bool IsInRange(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        return dist <= maxUseDistance;
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

            fromGO =
                context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);

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