using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SeatController2D))]
public class PilotChairInteractable :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    ILinkInteractable,
    IBoatControlAuthorityDisplayNameProvider,
    IEscapeClosable
{
    [Header("Identity")]
    [Tooltip("Stable ID used to persist Helm wiring. Boat Builder assigns one when a station is editor-linked. Existing stations fall back to a stable relative hierarchy path.")]
    [SerializeField] private string pilotStationId;

    [Header("Interaction")]
    [SerializeField] private int priority = 100;
    [SerializeField] private float maxUseDistance = 1.5f;

    [Header("Escape")]
    [Tooltip("Occupied chairs participate in the global Escape close stack. Keep below full-screen overlays (normally 1000) and above the pause-menu fallback.")]
    [SerializeField] private int escapePriority = 900;

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

    [Header("Helm Connection")]
    [Tooltip("Helm hardpoint this pilot station is wired to. The helm hardpoint must also list this chair in its Controllers array.")]
    [SerializeField] private Hardpoint helmHardpoint;

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

    public enum PilotingHelmStatus
    {
        Unlinked = 0,
        Online = 1,
        Damaged = 2,
        Offline = 3
    }

    private enum HelmConnectionStatus
    {
        Ready,
        Unlinked,
        WrongHardpointType,
        DifferentBoat,
        NotRegistered
    }

    public int InteractionPriority => priority;
    public Hardpoint LinkedHelmHardpoint => helmHardpoint;
    public string PilotStationId => pilotStationId;

    public int EscapePriority => escapePriority;
    public bool IsEscapeOpen => Occupant != null;

    public string PersistenceStationId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    pilotStationId))
            {
                return pilotStationId.Trim();
            }

            return BuildFallbackPersistenceStationId();
        }
    }

    public string ControlAuthorityDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(
                    pilotStationId))
            {
                return pilotStationId
                    .Trim()
                    .Replace("_", " ")
                    .ToUpperInvariant();
            }

            return string.IsNullOrWhiteSpace(name)
                ? "PILOT STATION"
                : name.Trim()
                      .Replace("_", " ")
                      .ToUpperInvariant();
        }
    }

    public bool HasPilotControlAuthority
    {
        get
        {
            ResolvePilotingSimulation();
            return pilotingSimulation != null &&
                   pilotingSimulation.HasControlAuthority(this);
        }
    }

    public PilotingHelmStatus CurrentPilotingHelmStatus =>
        ResolvePilotingHelmStatus();

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

    private string BuildFallbackPersistenceStationId()
    {
        CacheBoat();

        Transform root =
            _cachedBoat != null
                ? _cachedBoat.transform
                : transform.root;

        if (root == null)
            return $"instance:{GetInstanceID()}";

        System.Collections.Generic.List<string> segments =
            new System.Collections.Generic.List<string>();

        Transform current =
            transform;

        while (current != null &&
               current != root)
        {
            segments.Add(
                $"{current.name}[{current.GetSiblingIndex()}]");

            current =
                current.parent;
        }

        segments.Reverse();

        if (segments.Count == 0)
            return $"path:{name}[{transform.GetSiblingIndex()}]";

        return "path:" +
               string.Join(
                   "/",
                   segments);
    }

#if UNITY_EDITOR
    public void EditorSetPilotStationId(
        string stableId)
    {
        pilotStationId =
            stableId != null
                ? stableId.Trim()
                : string.Empty;
    }
#endif

    public bool TryLinkToHelm(
        Hardpoint newHelmHardpoint)
    {
        if (newHelmHardpoint == null ||
            !HardpointAcceptsHelm(newHelmHardpoint))
        {
            return false;
        }

        CacheBoat();

        Boat helmBoat =
            newHelmHardpoint.GetComponentInParent<Boat>();

        if (_cachedBoat != null &&
            helmBoat != null &&
            helmBoat != _cachedBoat)
        {
            return false;
        }

        Hardpoint previousHelm =
            helmHardpoint;

        if (previousHelm != null &&
            previousHelm != newHelmHardpoint)
        {
            previousHelm.TryRemoveController(this);
        }

        helmHardpoint =
            newHelmHardpoint;

        helmHardpoint.TryAddController(this);

        return true;
    }

    public bool UnlinkHelm()
    {
        if (helmHardpoint == null)
            return false;

        Hardpoint previousHelm =
            helmHardpoint;

        helmHardpoint =
            null;

        previousHelm.TryRemoveController(this);

        ReleasePilotAuthority();
        ClosePilotingOverlay("Helm unlinked");

        return true;
    }

    public bool CanLink(
        in InteractContext context)
    {
        float dist =
            Vector2.Distance(
                context.Origin,
                transform.position);

        if (dist > maxUseDistance)
            return false;

        if (!CanAccessByBoatContext(context))
            return false;

        HelmLinkSession session =
            HelmLinkSession.Get(
                context.InteractorGO);

        if (session != null &&
            session.HasPendingHelm)
        {
            return IsCompatibleRuntimeHelm(
                session.PendingHelmHardpoint);
        }

        // With no pending selection, L acts as an unlink shortcut for an
        // already-wired station. Unlinked chairs remain plain chairs.
        return helmHardpoint != null;
    }

    public string GetLinkPromptVerb(
        in InteractContext context)
    {
        HelmLinkSession session =
            HelmLinkSession.Get(
                context.InteractorGO);

        if (session != null &&
            session.HasPendingHelm)
        {
            Hardpoint pending =
                session.PendingHelmHardpoint;

            string helmName =
                pending != null &&
                !string.IsNullOrWhiteSpace(
                    pending.HardpointId)
                    ? pending.HardpointId
                    : "Selected Helm";

            return $"Link to {helmName}";
        }

        return helmHardpoint != null
            ? "Unlink Helm"
            : "Link Helm";
    }

    public void Link(
        in InteractContext context)
    {
        HelmLinkSession session =
            HelmLinkSession.Get(
                context.InteractorGO);

        if (session != null &&
            session.HasPendingHelm)
        {
            Hardpoint pending =
                session.PendingHelmHardpoint;

            if (IsCompatibleRuntimeHelm(pending) &&
                TryLinkToHelm(pending))
            {
                Debug.Log(
                    $"[HelmLink] Linked pilot chair '{name}' to helm hardpoint " +
                    $"'{pending.HardpointId}'.",
                    this);
            }

            session.Cancel();
            return;
        }

        if (helmHardpoint != null)
        {
            string oldId =
                helmHardpoint.HardpointId;

            if (UnlinkHelm())
            {
                Debug.Log(
                    $"[HelmLink] Unlinked pilot chair '{name}' from helm hardpoint '{oldId}'.",
                    this);
            }
        }
    }

    private bool IsCompatibleRuntimeHelm(
        Hardpoint candidate)
    {
        if (candidate == null ||
            !HardpointAcceptsHelm(candidate))
        {
            return false;
        }

        CacheBoat();

        Boat helmBoat =
            candidate.GetComponentInParent<Boat>();

        return _cachedBoat == null ||
               helmBoat == null ||
               helmBoat == _cachedBoat;
    }

    public bool CloseFromEscape()
    {
        if (Occupant == null)
            return false;

        EjectOccupant(
            SeatEjectReason.Manual);

        return true;
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (Occupant == context.InteractorGO)
            return "Leave Chair";

        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        if (!CanReceiveHelmControl())
            return "Sit";

        ResolvePilotingSimulation();

        if (pilotingSimulation != null &&
            pilotingSimulation.CanClaimControl(this))
        {
            return "Pilot";
        }

        return "Sit";
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

        if (Occupant != null)
            RegisterSeatEscape();
    }

    private void OnDisable()
    {
        UnregisterSeatEscape();
        ReleasePilotAuthority();
        ClosePilotingOverlay("Helm disabled");
        UnsubscribeSeatEvents();
    }

    private void OnDestroy()
    {
        UnregisterSeatEscape();
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

        RefreshPilotAuthority();
        RefreshPilotingOverlay();

        // Closing an actively-open piloting overlay still means leaving the chair.
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

        if (pilotingSimulation != null &&
            pilotingSimulation.HasControlAuthority(this))
        {
            pilotingSimulation.SubmitIntent(this, intent);
        }

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

        // A PilotChair is still a perfectly serviceable chair without a helm.
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

        if (Occupant == null)
            Seat(context.InteractorGO);
    }

    private void Seat(GameObject interactor)
    {
        if (interactor == null)
            return;

        ResolveSeatController();
        ResolvePilotingSimulation();

        if (seatController == null)
            return;

        if (!seatController.TrySeat(interactor))
            return;

        RegisterSeatEscape();

        _occupantBoatIntent =
            FindBoatControlIntentSource(interactor);

        if (_occupantBoatIntent == null)
        {
            Debug.LogWarning(
                $"{name}: Occupant '{interactor.name}' has no " +
                $"IBoatControlIntentSource. Seat works, but piloting intent cannot be submitted.",
                interactor);
        }

        RefreshPilotAuthority();
        RefreshPilotingOverlay();
    }

    private void EjectOccupant(
        SeatEjectReason reason)
    {
        UnregisterSeatEscape();
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
        UnregisterSeatEscape();
        ClosePilotingOverlay(reason.ToString());
        ReleasePilotAuthority();
        _occupantBoatIntent = null;
    }

    private void RegisterSeatEscape()
    {
        EscapeCloseRegistry registry =
            EscapeCloseRegistry.TryGetOrFind();

        if (registry != null)
            registry.Register(this);
    }

    private void UnregisterSeatEscape()
    {
        if (EscapeCloseRegistry.I != null)
            EscapeCloseRegistry.I.Unregister(this);
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

    private void RefreshPilotAuthority()
    {
        ResolvePilotingSimulation();

        if (pilotingSimulation == null)
            return;

        if (!CanReceiveHelmControl())
        {
            pilotingSimulation.ReleaseControl(this);
            return;
        }

        if (pilotingSimulation.HasControlAuthority(this))
            return;

        if (pilotingSimulation.CanClaimControl(this))
            pilotingSimulation.TryClaimControl(this);
    }

    private void RefreshPilotingOverlay()
    {
        bool hasConfiguredHelmLink =
            TryResolveConfiguredHelmLink(
                out _,
                out _);

        if (!hasConfiguredHelmLink)
        {
            if (_pilotingOverlayStarted)
                ClosePilotingOverlay("Helm unlinked");

            return;
        }

        if (_pilotingOverlayStarted)
            return;

        ResolvePilotingSimulation();

        if (pilotingSimulation == null ||
            pilotingSimulation.State == null)
        {
            LogMissingSimulationOnce();
            return;
        }

        ResolvePilotingOverlayRunner();

        _pilotingOverlayStarted =
            pilotingOverlayRunner != null &&
            pilotingOverlayRunner.OpenForHelm(this);

        if (!_pilotingOverlayStarted)
        {
            Debug.LogWarning(
                $"{name}: Chair is linked to a helm, but the piloting overlay could not be opened. " +
                "The player remains seated.",
                this);
        }
    }

    private PilotingHelmStatus ResolvePilotingHelmStatus()
    {
        if (!TryResolveConfiguredHelmLink(
                out HelmModule helm,
                out _))
        {
            return PilotingHelmStatus.Unlinked;
        }

        if (helm == null ||
            !helm.IsOnline)
        {
            return PilotingHelmStatus.Offline;
        }

        return helm.IsDamaged
            ? PilotingHelmStatus.Damaged
            : PilotingHelmStatus.Online;
    }

    private bool CanReceiveHelmControl()
    {
        if (!TryResolveConfiguredHelmLink(
                out HelmModule helm,
                out bool withinCapacity))
        {
            return false;
        }

        return helm != null &&
               helm.IsOnline &&
               withinCapacity;
    }

    /// <summary>
    /// Validates the authored chair <-> helm relationship independently of helm
    /// health. A configured link can therefore remain valid while the installed
    /// helm is damaged, destroyed, disabled, or even temporarily absent.
    /// </summary>
    private bool TryResolveConfiguredHelmLink(
        out HelmModule helm,
        out bool withinCapacity)
    {
        helm = null;
        withinCapacity = false;

        if (helmHardpoint == null)
            return false;

        if (!HardpointAcceptsHelm(helmHardpoint))
            return false;

        CacheBoat();

        Boat helmBoat =
            helmHardpoint.GetComponentInParent<Boat>();

        if (_cachedBoat != null &&
            helmBoat != null &&
            helmBoat != _cachedBoat)
        {
            return false;
        }

        if (!IsRegisteredWithHelmHardpoint())
            return false;

        if (!helmHardpoint.TryGetInstalledModuleComponent(
                out helm))
        {
            // The wiring is valid, but there is currently no helm installed.
            // The linked chair therefore gets an OFFLINE cartridge.
            return true;
        }

        if (!helm.TryGetStationSlot(
                this,
                out int slotIndex))
        {
            return false;
        }

        withinCapacity =
            slotIndex >= 0 &&
            slotIndex < helm.StationConnectionCapacity;

        return true;
    }

    private bool IsRegisteredWithHelmHardpoint()
    {
        if (helmHardpoint == null ||
            helmHardpoint.Controllers == null)
        {
            return false;
        }

        var controllers =
            helmHardpoint.Controllers;

        for (int i = 0; i < controllers.Count; i++)
        {
            if (ReferenceEquals(
                    controllers[i],
                    this))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HardpointAcceptsHelm(
        Hardpoint hardpoint)
    {
        if (hardpoint == null)
            return false;

        HardpointType[] accepted =
            hardpoint.GetAcceptedTypes();

        if (accepted == null)
            return false;

        for (int i = 0; i < accepted.Length; i++)
        {
            if (accepted[i] == HardpointType.Helm)
                return true;
        }

        return false;
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