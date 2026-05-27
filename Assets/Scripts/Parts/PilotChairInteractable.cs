using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SeatController2D))]
public class PilotChairInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 100; // chairs should beat random stuff
    [SerializeField] private float maxUseDistance = 1.5f;

    [Header("Seat")]
    [Tooltip("Generic seat controller. Auto-resolves from this GameObject.")]
    [SerializeField] private SeatController2D seatController;

    [Tooltip("Legacy/fallback seat point. Existing prefabs can keep using this. New generic chairs should configure SeatController2D directly.")]
    [SerializeField] private Transform seatPoint;

    [Tooltip("Legacy/fallback pin setting. Existing prefabs can keep using this. New generic chairs should configure SeatController2D directly.")]
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
    private IBoatControlIntentSource occupantBoatIntent;

    public int InteractionPriority => priority;

    private GameObject Occupant => seatController != null ? seatController.Occupant : null;

    public string GetPromptVerb(in InteractContext context)
    {
        if (Occupant == context.InteractorGO)
            return "Leave Helm";

        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        return "Pilot";
    }

    public Transform GetPromptAnchor()
    {
        if (seatController != null)
            return seatController.SeatPoint;

        return seatPoint != null ? seatPoint : transform;
    }

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;

        ResolveSeatController();
        CacheBoat();
    }

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform;

        ResolveSeatController();
        CacheBoat();

        ResolveThrottleReceiver();
        LogMissingThrottleOnceIfNeeded();
    }

    private void OnValidate()
    {
        if (seatPoint == null)
            seatPoint = transform;

        if (seatController == null)
            seatController = GetComponent<SeatController2D>();
    }

    private void Update()
    {
        ResolveSeatController();

        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();
            LogMissingThrottleOnceIfNeeded();
        }

        GameObject occupant = Occupant;

        if (occupant == null)
        {
            ClearPilotOutput();
            return;
        }

        // SeatController handles pinning and underwater auto-eject.
        if (!seatController.TickSeat())
        {
            ClearPilotOutput();
            return;
        }

        occupant = Occupant;

        if (occupant == null)
        {
            ClearPilotOutput();
            return;
        }

        if (!CanOccupantStillAccessSeat())
        {
            EjectOccupant(SeatEjectReason.AccessInvalid);
            return;
        }

        if (occupantBoatIntent == null)
        {
            occupantBoatIntent = FindBoatControlIntentSource(Occupant);

            if (occupantBoatIntent == null)
            {
                throttleReceiver?.SetThrottle(0f);
                return;
            }
        }

        var intent = occupantBoatIntent.Current;
        Debug.Log(
            $"[PilotChair:{name}] Occupant={Occupant.name} Throttle={intent.Throttle:0.00} Receiver={(throttleReceiver != null ? throttleReceiver.ToString() : "NULL")}",
            this);
        throttleReceiver?.SetThrottle(intent.Throttle);

        if (intent.ExitPressed)
            EjectOccupant(SeatEjectReason.Manual);
    }

    public bool CanInteract(in InteractContext context)
    {
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance)
            return false;

        GameObject occupant = Occupant;

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

        GameObject occupant = Occupant;

        if (occupant != null && context.InteractorGO == occupant)
        {
            EjectOccupant(SeatEjectReason.Manual);
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

        if (Occupant == null)
            Seat(context.InteractorGO);
    }

    private void Seat(GameObject interactor)
    {
        if (interactor == null)
            return;

        ResolveSeatController();

        if (seatController == null)
            return;

        if (!seatController.TrySeat(interactor))
            return;

        occupantBoatIntent = FindBoatControlIntentSource(interactor);

        if (occupantBoatIntent == null)
        {
            Debug.LogWarning(
                $"{name}: Occupant '{interactor.name}' has no IBoatControlIntentSource. Add LocalBoatControlIntentSource.",
                interactor);
        }

        // Optional later:
        // interactor.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.Piloting);
    }

    private void EjectOccupant(SeatEjectReason reason)
    {
        // Optional later:
        // Occupant?.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.OnFoot);

        if (seatController != null)
            seatController.Eject(reason);

        ClearPilotOutput();
    }

    private void ClearPilotOutput()
    {
        occupantBoatIntent = null;
        throttleReceiver?.SetThrottle(0f);
    }

    private void ResolveSeatController()
    {
        if (seatController == null)
            seatController = GetComponent<SeatController2D>();

        if (seatController == null)
            seatController = gameObject.AddComponent<SeatController2D>();

        Transform fallbackSeat = seatPoint != null ? seatPoint : transform;
        seatController.SetRuntimeFallbackSeat(fallbackSeat, pinOccupantToSeat);
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

        GameObject occupant = Occupant;

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

    private IBoatControlIntentSource FindBoatControlIntentSource(GameObject interactor)
    {
        if (interactor == null)
            return null;

        // Exact object first.
        if (interactor.TryGetComponent(out IBoatControlIntentSource direct))
            return direct;

        // Parents next, because InteractorGO is often a child/helper object.
        MonoBehaviour[] parents = interactor.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IBoatControlIntentSource source)
                return source;
        }

        // Children last, in case input/intent lives under the player root.
        MonoBehaviour[] children = interactor.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] is IBoatControlIntentSource source)
                return source;
        }

        return null;
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }
}