using UnityEngine;

[DisallowMultipleComponent]
public sealed class TurretControlStation : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Refs")]
    [SerializeField] private Hardpoint hardpoint;

    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 25;
    [SerializeField] private float maxDistance = 1.75f;

    [Header("Seat")]
    [SerializeField] private Transform seatPoint;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private bool pinOccupantToSeat = true;

    [Header("Boat Access")]
    [Tooltip("If true, turret stations that belong to a Boat can only be used by players boarded on that same boat.")]
    [SerializeField] private bool requireMatchingBoatBoardingContext = true;

    [Tooltip("If true, turret stations not under a Boat remain usable. Mostly future-proofing for dock/ruin/world stations.")]
    [SerializeField] private bool allowAccessWhenNotPartOfBoat = true;

    private GameObject occupant;
    private PlayerTurretController occupantTurretController;

    private Boat _cachedBoat;

    public int InteractionPriority => interactionPriority;

    public Hardpoint LinkedHardpoint => hardpoint;
    public Transform LinkAnchor => promptAnchor != null ? promptAnchor : transform;

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;

        if (promptAnchor == null)
            promptAnchor = seatPoint != null ? seatPoint : transform;

        CacheBoat();
    }

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform;

        if (promptAnchor == null)
            promptAnchor = seatPoint != null ? seatPoint : transform;

        if (hardpoint == null)
            hardpoint = GetComponentInParent<Hardpoint>();

        CacheBoat();
    }

    private void Update()
    {
        if (occupant == null)
            return;

        if (!TryResolveTurret(out TurretModule turret))
        {
            EjectOccupant();
            return;
        }

        if (occupantTurretController == null)
        {
            EjectOccupant();
            return;
        }

        if (!occupantTurretController.IsControllingTurret ||
            occupantTurretController.ActiveTurret != turret)
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
    }

    public bool CanInteract(in InteractContext context)
    {
        if (!IsInRange(context))
            return false;

        // Always allow the current occupant to leave.
        if (occupant != null && context.InteractorGO == occupant)
            return true;

        if (!CanAccessByBoatContext(context))
            return false;

        if (!TryResolveTurret(out _))
            return false;

        return occupant == null;
    }

    public void Interact(in InteractContext context)
    {
        if (!IsInRange(context))
            return;

        if (occupant != null && context.InteractorGO == occupant)
        {
            EjectOccupant();
            return;
        }

        if (!CanAccessByBoatContext(context))
            return;

        if (!TryResolveTurret(out _))
            return;

        if (occupant == null)
            Seat(context.InteractorGO);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (occupant != null && context.InteractorGO == occupant)
            return "Leave Turret";

        if (!CanAccessByBoatContext(context))
            return "Board Boat";

        if (!TryResolveTurret(out _))
            return "No Turret";

        return "Control Turret";
    }

    public Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    private void Seat(GameObject interactor)
    {
        if (interactor == null)
            return;

        if (!TryResolveTurret(out TurretModule turret))
            return;

        PlayerTurretController controller =
            interactor.GetComponentInChildren<PlayerTurretController>(true);

        if (controller == null)
        {
            Debug.LogWarning(
                $"[TurretControlStation] Interactor '{interactor.name}' has no PlayerTurretController.",
                interactor);
            return;
        }

        occupant = interactor;
        occupantTurretController = controller;

        if (pinOccupantToSeat && seatPoint != null)
        {
            occupant.transform.position = seatPoint.position;

            Rigidbody2D rb = occupant.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        occupantTurretController.EnterTurretControl(turret);
    }

    private void EjectOccupant()
    {
        if (occupantTurretController != null)
            occupantTurretController.ExitTurretControl();

        occupant = null;
        occupantTurretController = null;
    }

    private bool TryResolveTurret(out TurretModule turret)
    {
        turret = null;

        if (hardpoint == null)
            hardpoint = GetComponentInParent<Hardpoint>();

        if (hardpoint == null)
            return false;

        if (!hardpoint.HasInstalledModule)
            return false;

        return hardpoint.TryGetInstalledModuleComponent(out turret) && turret != null;
    }

    private bool IsInRange(in InteractContext context)
    {
        return Vector2.Distance(context.Origin, transform.position) <= maxDistance;
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
        if (_cachedBoat != null)
            return;

        _cachedBoat = GetComponentInParent<Boat>();

        if (_cachedBoat == null && hardpoint != null)
            _cachedBoat = hardpoint.GetComponentInParent<Boat>();
    }
}