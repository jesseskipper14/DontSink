using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Refs")]
    [SerializeField] private DoorRuntime doorRuntime;
    [SerializeField] private Transform promptAnchor;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private bool allowInteractWhenOpen = true;
    [SerializeField] private bool allowInteractWhenClosed = true;

    [Header("Boat Access")]
    [Tooltip("If true, doors that belong to a Boat can only be used by players who are boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    public int InteractionPriority => interactionPriority;

    private Boat _cachedBoat;

    private void Reset()
    {
        if (doorRuntime == null)
            doorRuntime = GetComponent<DoorRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (doorRuntime == null)
            doorRuntime = GetComponent<DoorRuntime>();

        if (promptAnchor == null)
            promptAnchor = transform;

        CacheBoat();

        if (doorRuntime == null)
        {
            Debug.LogError("[DoorInteractable] Missing DoorRuntime.", this);
            enabled = false;
        }
    }

    public bool CanInteract(in InteractContext context)
    {
        if (doorRuntime == null)
            return false;

        if (!CanAccessDoorByBoatContext(context))
            return false;

        if (doorRuntime.IsOpen && !allowInteractWhenOpen)
            return false;

        if (!doorRuntime.IsOpen && !allowInteractWhenClosed)
            return false;

        // Same as hatches:
        // don't call CanToggle here, because blocked close should still show the prompt,
        // then runtime denies it and plays feedback.
        return true;
    }

    public void Interact(in InteractContext context)
    {
        if (doorRuntime == null)
            return;

        if (!CanAccessDoorByBoatContext(context))
            return;

        doorRuntime.Toggle();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (doorRuntime == null)
            return "Use Door";

        return doorRuntime.IsOpen ? "Close Door" : "Open Door";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private bool CanAccessDoorByBoatContext(in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        // If this door is not part of a boat, allow normal use.
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