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

    private GameObject occupant;
    private PlayerTurretController occupantTurretController;

    public int InteractionPriority => interactionPriority;

    public Hardpoint LinkedHardpoint => hardpoint;
    public Transform LinkAnchor => promptAnchor != null ? promptAnchor : transform;

    private void Reset()
    {
        if (seatPoint == null)
            seatPoint = transform;

        if (promptAnchor == null)
            promptAnchor = seatPoint != null ? seatPoint : transform;
    }

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform;

        if (promptAnchor == null)
            promptAnchor = seatPoint != null ? seatPoint : transform;

        if (hardpoint == null)
            hardpoint = GetComponentInParent<Hardpoint>();
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

        // If the player/controller exited turret mode through its own intent path,
        // clear the chair occupancy too. No ghost gunners. Very rude to ghosts.
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

        if (!TryResolveTurret(out _))
            return false;

        // If empty, any valid interactor can sit/control.
        if (occupant == null)
            return true;

        // If occupied, only the occupant can use E to exit.
        return context.InteractorGO == occupant;
    }

    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context))
            return;

        if (occupant == null)
        {
            Seat(context.InteractorGO);
            return;
        }

        if (context.InteractorGO == occupant)
            EjectOccupant();
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (occupant != null && context.InteractorGO == occupant)
            return "Leave Turret";

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
}