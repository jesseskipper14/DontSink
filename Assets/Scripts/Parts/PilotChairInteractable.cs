using UnityEngine;

[DisallowMultipleComponent]
public class PilotChairInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 100; // chairs should beat random stuff
    [SerializeField] private float maxUseDistance = 1.5f;

    [Header("Seat")]
    [SerializeField] private Transform seatPoint; // where the player gets pinned
    [SerializeField] private bool pinOccupantToSeat = true;

    [Header("Boat Access")]
    [Tooltip("If true, pilot chairs that belong to a Boat can only be used by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, pilot chairs not under a Boat remain usable. Mostly future-proofing for dock/world test rigs.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Boat Control Output")]
    [Tooltip("Anything that implements IThrottleReceiver (usually ThrottleForce on the boat). If null, auto-resolves.")]
    [SerializeField] private ThrottleForce throttleForce; // direct (optional)

    private IThrottleReceiver throttleReceiver;
    private bool _missingThrottleLogged;

    private Boat _cachedBoat;

    public int InteractionPriority => priority;

    public string GetPromptVerb(in InteractContext context)
    {
        if (occupant == context.InteractorGO)
            return "Leave Helm";

        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        return "Pilot";
    }

    public Transform GetPromptAnchor() => seatPoint != null ? seatPoint : transform;

    private GameObject occupant;
    private IBoatControlIntentSource occupantBoatIntent;

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform;

        CacheBoat();

        ResolveThrottleReceiver();
        LogMissingThrottleOnceIfNeeded();
    }

    private void OnValidate()
    {
        if (seatPoint == null)
            seatPoint = transform;
    }

    private void ResolveThrottleReceiver()
    {
        if (throttleForce != null)
        {
            throttleReceiver = throttleForce;
            return;
        }

        var parents = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IThrottleReceiver r)
            {
                throttleReceiver = r;

                if (parents[i] is ThrottleForce tf)
                    throttleForce = tf;

                return;
            }
        }

        var root = transform.root;
        var all = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IThrottleReceiver r)
            {
                throttleReceiver = r;

                if (all[i] is ThrottleForce tf)
                    throttleForce = tf;

                return;
            }
        }

        throttleReceiver = null;
    }

    private void LogMissingThrottleOnceIfNeeded()
    {
        if (throttleReceiver != null)
            return;

        if (_missingThrottleLogged)
            return;

        _missingThrottleLogged = true;

        Debug.LogError(
            $"{name}: PilotChairInteractable needs a throttle receiver (IThrottleReceiver). " +
            $"Assign ThrottleForce on the chair prefab OR ensure the boat contains an IThrottleReceiver.",
            this);
    }

    private void Update()
    {
        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();
            LogMissingThrottleOnceIfNeeded();
        }

        if (occupant == null)
        {
            throttleReceiver?.SetThrottle(0f);
            return;
        }

        if (!CanOccupantStillAccessSeat())
        {
            EjectOccupant();
            return;
        }

        if (pinOccupantToSeat && seatPoint != null)
        {
            occupant.transform.position = seatPoint.position;

            Rigidbody2D rb = occupant.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        if (occupantBoatIntent == null)
        {
            throttleReceiver?.SetThrottle(0f);
            return;
        }

        var intent = occupantBoatIntent.Current;
        throttleReceiver?.SetThrottle(intent.Throttle);

        if (intent.ExitPressed)
            EjectOccupant();
    }

    public bool CanInteract(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance)
            return false;

        // Current occupant can always leave, even if the boat context got weird.
        if (occupant != null && context.InteractorGO == occupant)
            return true;

        if (!CanAccessByBoatContext(context))
            return false;

        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();

            if (throttleReceiver == null)
            {
                LogMissingThrottleOnceIfNeeded();
                return false;
            }
        }

        return occupant == null;
    }

    public void Interact(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance)
            return;

        if (occupant != null && context.InteractorGO == occupant)
        {
            EjectOccupant();
            return;
        }

        if (!CanAccessByBoatContext(context))
            return;

        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();

            if (throttleReceiver == null)
            {
                LogMissingThrottleOnceIfNeeded();
                return;
            }
        }

        if (occupant == null)
            Seat(context.InteractorGO);
    }

    private void Seat(GameObject interactor)
    {
        if (interactor == null)
            return;

        occupant = interactor;
        occupantBoatIntent = interactor.GetComponent<IBoatControlIntentSource>();

        if (occupantBoatIntent == null)
        {
            Debug.LogWarning(
                $"{name}: Occupant '{interactor.name}' has no IBoatControlIntentSource. Add LocalBoatControlIntentSource.",
                interactor);
        }

        if (pinOccupantToSeat && seatPoint != null)
        {
            occupant.transform.position = seatPoint.position;

            Rigidbody2D rb = occupant.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        // Optional later:
        // interactor.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.Piloting);
    }

    private void EjectOccupant()
    {
        // Optional later:
        // occupant?.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.OnFoot);

        occupant = null;
        occupantBoatIntent = null;
        throttleReceiver?.SetThrottle(0f);
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

    private bool CanOccupantStillAccessSeat()
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        if (_cachedBoat == null)
            return allowAccessWhenNotPartOfBoat;

        if (occupant == null)
            return false;

        PlayerBoardingState boarding = occupant.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            boarding = occupant.GetComponentInChildren<PlayerBoardingState>(true);

        if (boarding == null)
            return false;

        return boarding.IsBoarded && boarding.CurrentBoatRoot == _cachedBoat.transform;
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