using UnityEngine;

[DisallowMultipleComponent]
public class PilotChairInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private int priority = 100; // chairs should beat random stuff
    [SerializeField] private float maxUseDistance = 1.5f;

    [Header("Seat")]
    [SerializeField] private Transform seatPoint; // where the player gets pinned
    [SerializeField] private bool pinOccupantToSeat = true;

    [Header("Boat Control Output")]
    [Tooltip("Anything that implements IThrottleReceiver (usually ThrottleForce on the boat).")]
    [SerializeField] private ThrottleForce throttleForce; // direct
    private IThrottleReceiver throttleReceiver;


    public int InteractionPriority => priority;

    private GameObject occupant;
    private IBoatControlIntentSource occupantBoatIntent;

    void Awake()
    {
        throttleReceiver = throttleForce;
        if (throttleReceiver == null)
            Debug.LogError($"{name}: PilotChairInteractable needs a throttle receiver (IThrottleReceiver).", this);

        if (seatPoint == null)
            seatPoint = transform; // fallback
    }

    void Update()
    {
        if (occupant == null)
        {
            // ensure boat isn't being driven by ghosts
            throttleReceiver?.SetThrottle(0f);
            return;
        }
        // Keep occupant seated (simple and effective)
        if (pinOccupantToSeat && seatPoint != null)
        {
            occupant.transform.position = seatPoint.position;

            // Stop physics drift if they have a Rigidbody2D
            var rb = occupant.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        // Read intent
        if (occupantBoatIntent == null)
        {
            throttleReceiver?.SetThrottle(0f);
            return;
        }
        var intent = occupantBoatIntent.Current;
        throttleReceiver?.SetThrottle(intent.Throttle);

        // Allow exit without needing to look at chair again
        if (intent.ExitPressed)
        {
            EjectOccupant();
        }
    }

    public bool CanInteract(in InteractContext context)
    {
        // Distance gate
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance) return false;

        // If unoccupied, anyone can sit.
        if (occupant == null) return true;

        // If occupied, only the current occupant can toggle to exit via interact.
        return context.InteractorGO == occupant;
    }

    public void Interact(in InteractContext context)
    {
        if (occupant == null)
        {
            Seat(context.InteractorGO);
        }
        else if (context.InteractorGO == occupant)
        {
            EjectOccupant();
        }
    }

    private void Seat(GameObject interactor)
    {
        occupant = interactor;
        occupantBoatIntent = interactor.GetComponent<IBoatControlIntentSource>();

        if (occupantBoatIntent == null)
        {
            Debug.LogWarning($"{name}: Occupant '{interactor.name}' has no IBoatControlIntentSource. Add LocalBoatControlIntentSource.", interactor);
        }

        // Optional: tell player systems they're piloting (hook later)
        // interactor.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.Piloting);
    }

    private void EjectOccupant()
    {
        // Optional: notify player systems
        // occupant?.GetComponent<IControlModeSink>()?.SetControlMode(ControlMode.OnFoot);

        occupant = null;
        occupantBoatIntent = null;
        throttleReceiver?.SetThrottle(0f);
    }
}
