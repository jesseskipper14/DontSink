using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Refs")]
    [SerializeField] private HatchRuntime hatchRuntime;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private bool allowInteractWhenOpen = true;
    [SerializeField] private bool allowInteractWhenClosed = true;

    [Header("Boat Access")]
    [Tooltip("If true, hatches that belong to a Boat can only be used by players who are boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    public int InteractionPriority => interactionPriority;

    private Boat _cachedBoat;

    private void Reset()
    {
        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (hatchRuntime == null)
            hatchRuntime = GetComponent<HatchRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();

        if (hatchRuntime == null)
        {
            Debug.LogError("[HatchInteractable] Missing HatchRuntime.", this);
            enabled = false;
        }
    }

    public bool CanInteract(in InteractContext context)
    {
        if (hatchRuntime == null)
            return false;

        if (!CanAccessHatchByBoatContext(context))
            return false;

        if (hatchRuntime.IsOpen && !allowInteractWhenOpen)
            return false;

        if (!hatchRuntime.IsOpen && !allowInteractWhenClosed)
            return false;

        // Important:
        // Do NOT call hatchRuntime.CanToggle() here.
        // A blocked close should still show "Close Hatch",
        // then the runtime denies it and plays feedback.
        return true;
    }

    public void Interact(in InteractContext context)
    {
        if (hatchRuntime == null)
            return;

        if (!CanAccessHatchByBoatContext(context))
            return;

        hatchRuntime.Toggle();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (hatchRuntime == null)
            return "Use Hatch";

        return hatchRuntime.IsOpen ? "Close Hatch" : "Open Hatch";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private bool CanAccessHatchByBoatContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        // If this hatch is not part of a boat, allow normal use.
        if (_cachedBoat == null)
            return true;

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
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

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