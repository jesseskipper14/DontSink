using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SeatController2D))]
public class PilotChairInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 100;
    [SerializeField] private float maxUseDistance = 1.5f;

    [Header("Seat")]
    [Tooltip("Generic seat controller. Auto-resolves from this GameObject.")]
    [SerializeField] private SeatController2D seatController;

    [Tooltip("Legacy/fallback seat point.")]
    [SerializeField] private Transform seatPoint;

    [Tooltip("Legacy/fallback pin setting.")]
    [SerializeField] private bool pinOccupantToSeat = true;

    [Header("Boat Access")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    [Header("Piloting")]
    [Tooltip("Boat-level authoritative piloting simulation. Auto-resolves from parent boat.")]
    [SerializeField] private BoatPilotingSimulation pilotingSimulation;

    [Tooltip("Scene-level runner that opens the piloting cartridge when this helm is occupied.")]
    [SerializeField] private PilotingOverlayRunner pilotingOverlayRunner;

    private Boat _cachedBoat;
    private IBoatControlIntentSource _occupantBoatIntent;
    private bool _pilotingOverlayStarted;
    private bool _missingSimulationLogged;
    private bool _seatEventSubscribed;

    public int InteractionPriority => priority;

    public Boat OwningBoat
    {
        get
        {
            CacheBoat();
            return _cachedBoat;
        }
    }

    public BoatPilotingSimulation PilotingSimulation
    {
        get
        {
            ResolvePilotingSimulation();
            return pilotingSimulation;
        }
    }

    private GameObject Occupant =>
        seatController != null
            ? seatController.Occupant
            : null;

    public string GetPromptVerb(in InteractContext context)
    {
        if (Occupant == context.InteractorGO)
            return "Leave Helm";

        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        ResolvePilotingSimulation();

        if (pilotingSimulation != null &&
            !pilotingSimulation.CanClaimControl(this))
        {
            return "Helm In Use";
        }

        return "Pilot";
    }

    public Transform GetPromptAnchor()
    {
        if (seatController != null)
            return seatController.SeatPoint;

        return seatPoint != null
            ? seatPoint
            : transform;
    }

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;

        ResolveSeatController();
        CacheBoat();
        ResolvePilotingSimulation();
    }

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform;

        ResolveSeatController();
        CacheBoat();
        ResolvePilotingSimulation();
        ResolvePilotingOverlayRunner();
    }

    private void OnEnable()
    {
        ResolveSeatController();
        SubscribeSeatEvents();
    }

    private void OnDisable()
    {
        ReleasePilotAuthority();
        ClosePilotingOverlay("Helm disabled");
        UnsubscribeSeatEvents();
    }

    private void OnDestroy()
    {
        ReleasePilotAuthority();
        UnsubscribeSeatEvents();
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
        ResolvePilotingSimulation();

        GameObject occupant = Occupant;

        if (occupant == null)
        {
            ReleasePilotAuthority();
            ClosePilotingOverlay("Seat empty");
            _occupantBoatIntent = null;
            return;
        }

        // SeatController owns underwater ejection and pinning.
        if (!seatController.TickSeat())
        {
            ReleasePilotAuthority();
            ClosePilotingOverlay("Seat ejected");
            _occupantBoatIntent = null;
            return;
        }

        occupant = Occupant;

        if (occupant == null)
        {
            ReleasePilotAuthority();
            ClosePilotingOverlay("Seat empty");
            _occupantBoatIntent = null;
            return;
        }

        if (!CanOccupantStillAccessSeat())
        {
            EjectOccupant(SeatEjectReason.AccessInvalid);
            return;
        }

        if (pilotingSimulation == null ||
            !pilotingSimulation.HasControlAuthority(this))
        {
            EjectOccupant(SeatEjectReason.AccessInvalid);
            return;
        }

        // Closing the overlay means leaving this helm.
        if (_pilotingOverlayStarted &&
            (pilotingOverlayRunner == null ||
             !pilotingOverlayRunner.IsOpenFor(this)))
        {
            _pilotingOverlayStarted = false;
            EjectOccupant(SeatEjectReason.Manual);
            return;
        }

        if (_occupantBoatIntent == null)
            _occupantBoatIntent =
                FindBoatControlIntentSource(occupant);

        BoatControlIntent intent =
            _occupantBoatIntent != null
                ? _occupantBoatIntent.Current
                : BoatControlIntent.Neutral;

        pilotingSimulation.SubmitIntent(this, intent);

        if (intent.ExitPressed)
            EjectOccupant(SeatEjectReason.Manual);
    }

    public bool CanInteract(in InteractContext context)
    {
        float dist =
            Vector2.Distance(
                context.Origin,
                transform.position);

        if (dist > maxUseDistance)
            return false;

        GameObject occupant = Occupant;

        // Current occupant can always leave.
        if (occupant != null &&
            context.InteractorGO == occupant)
        {
            return true;
        }

        if (!CanAccessByBoatContext(context))
            return false;

        ResolvePilotingSimulation();

        if (pilotingSimulation == null)
        {
            LogMissingSimulationOnce();
            return false;
        }

        if (!pilotingSimulation.CanClaimControl(this))
            return false;

        return occupant == null;
    }

    public void Interact(in InteractContext context)
    {
        float dist =
            Vector2.Distance(
                context.Origin,
                transform.position);

        if (dist > maxUseDistance)
            return;

        GameObject occupant = Occupant;

        if (occupant != null &&
            context.InteractorGO == occupant)
        {
            EjectOccupant(SeatEjectReason.Manual);
            return;
        }

        if (!CanAccessByBoatContext(context))
            return;

        ResolvePilotingSimulation();

        if (pilotingSimulation == null)
        {
            LogMissingSimulationOnce();
            return;
        }

        if (!pilotingSimulation.CanClaimControl(this))
            return;

        if (Occupant == null)
            Seat(context.InteractorGO);
    }

    private void Seat(GameObject interactor)
    {
        if (interactor == null)
            return;

        ResolveSeatController();
        ResolvePilotingSimulation();

        if (seatController == null ||
            pilotingSimulation == null)
        {
            LogMissingSimulationOnce();
            return;
        }

        if (!seatController.TrySeat(interactor))
            return;

        if (!pilotingSimulation.TryClaimControl(this))
        {
            seatController.Eject(
                SeatEjectReason.AccessInvalid);
            return;
        }

        _occupantBoatIntent =
            FindBoatControlIntentSource(interactor);

        if (_occupantBoatIntent == null)
        {
            Debug.LogWarning(
                $"{name}: Occupant '{interactor.name}' has no " +
                $"IBoatControlIntentSource. Piloting intent will remain neutral.",
                interactor);
        }

        ResolvePilotingOverlayRunner();

        _pilotingOverlayStarted =
            pilotingOverlayRunner != null &&
            pilotingOverlayRunner.OpenForHelm(this);

        if (!_pilotingOverlayStarted)
        {
            Debug.LogWarning(
                $"{name}: Pilot seated, but the piloting overlay could not be opened.",
                this);

            EjectOccupant(
                SeatEjectReason.AccessInvalid);
        }
    }

    private void EjectOccupant(
        SeatEjectReason reason)
    {
        ClosePilotingOverlay(reason.ToString());
        ReleasePilotAuthority();

        if (seatController != null)
            seatController.Eject(reason);

        _occupantBoatIntent = null;
    }

    private void HandleSeatEjected(
        GameObject oldOccupant,
        SeatEjectReason reason)
    {
        // This catches underwater ejection or any future direct SeatController eject.
        ClosePilotingOverlay(reason.ToString());
        ReleasePilotAuthority();
        _occupantBoatIntent = null;
    }

    private void ReleasePilotAuthority()
    {
        if (pilotingSimulation != null)
            pilotingSimulation.ReleaseControl(this);
    }

    private void ClosePilotingOverlay(string reason)
    {
        if (!_pilotingOverlayStarted)
            return;

        _pilotingOverlayStarted = false;

        pilotingOverlayRunner?.CloseForHelm(
            this,
            reason);
    }

    private void ResolvePilotingOverlayRunner()
    {
        if (pilotingOverlayRunner == null)
            pilotingOverlayRunner =
                FindAnyObjectByType<PilotingOverlayRunner>();
    }

    private void ResolveSeatController()
    {
        if (seatController == null)
            seatController =
                GetComponent<SeatController2D>();

        if (seatController == null)
            seatController =
                gameObject.AddComponent<SeatController2D>();

        Transform fallbackSeat =
            seatPoint != null
                ? seatPoint
                : transform;

        seatController.SetRuntimeFallbackSeat(
            fallbackSeat,
            pinOccupantToSeat);
    }

    private void ResolvePilotingSimulation()
    {
        if (pilotingSimulation != null)
            return;

        CacheBoat();

        if (_cachedBoat != null)
        {
            pilotingSimulation =
                _cachedBoat.GetComponent<BoatPilotingSimulation>() ??
                _cachedBoat.GetComponentInChildren<BoatPilotingSimulation>(true);
        }

        if (pilotingSimulation == null)
            pilotingSimulation =
                GetComponentInParent<BoatPilotingSimulation>();
    }

    private void SubscribeSeatEvents()
    {
        if (_seatEventSubscribed ||
            seatController == null)
        {
            return;
        }

        seatController.OccupantEjected +=
            HandleSeatEjected;

        _seatEventSubscribed = true;
    }

    private void UnsubscribeSeatEvents()
    {
        if (!_seatEventSubscribed ||
            seatController == null)
        {
            return;
        }

        seatController.OccupantEjected -=
            HandleSeatEjected;

        _seatEventSubscribed = false;
    }

    private void LogMissingSimulationOnce()
    {
        if (_missingSimulationLogged)
            return;

        _missingSimulationLogged = true;

        Debug.LogError(
            $"{name}: Pilot helm requires a BoatPilotingSimulation on the owning boat.",
            this);
    }

    private bool CanAccessByBoatContext(
        in InteractContext context)
    {
        if (!requireMatchingBoatBoardingContext)
            return true;

        CacheBoat();

        if (_cachedBoat == null)
            return allowAccessWhenNotPartOfBoat;

        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null)
            return false;

        if (!boarding.IsBoarded)
            return false;

        return boarding.CurrentBoatRoot ==
               _cachedBoat.transform;
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

        PlayerBoardingState boarding =
            occupant.GetComponentInParent<PlayerBoardingState>();

        if (boarding == null)
        {
            boarding =
                occupant.GetComponentInChildren<PlayerBoardingState>(true);
        }

        if (boarding == null)
            return false;

        return boarding.IsBoarded &&
               boarding.CurrentBoatRoot ==
               _cachedBoat.transform;
    }

    private PlayerBoardingState FindBoardingState(
        in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO
                    .GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO =
                context.InteractorGO
                    .GetComponentInChildren<PlayerBoardingState>(true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform
                    .GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform
                    .GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private IBoatControlIntentSource
        FindBoatControlIntentSource(
            GameObject interactor)
    {
        if (interactor == null)
            return null;

        if (interactor.TryGetComponent(
                out IBoatControlIntentSource direct))
        {
            return direct;
        }

        MonoBehaviour[] parents =
            interactor.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IBoatControlIntentSource source)
                return source;
        }

        MonoBehaviour[] children =
            interactor.GetComponentsInChildren<MonoBehaviour>(true);

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