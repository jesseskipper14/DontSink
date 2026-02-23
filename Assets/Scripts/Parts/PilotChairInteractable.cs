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

    [Header("Boat Control Output")]
    [Tooltip("Anything that implements IThrottleReceiver (usually ThrottleForce on the boat). If null, auto-resolves.")]
    [SerializeField] private ThrottleForce throttleForce; // direct (optional)

    private IThrottleReceiver throttleReceiver;
    private bool _missingThrottleLogged;

    public int InteractionPriority => priority;
    public string GetPromptVerb(in InteractContext context) => "Pilot";
    public Transform GetPromptAnchor() => seatPoint != null ? seatPoint : transform;

    private GameObject occupant;
    private IBoatControlIntentSource occupantBoatIntent;

    private void Awake()
    {
        if (seatPoint == null)
            seatPoint = transform; // fallback

        ResolveThrottleReceiver();
        LogMissingThrottleOnceIfNeeded();
    }

    private void OnValidate()
    {
        // Keep it honest in the inspector: throttleForce is allowed to be null.
        // No further validation needed since ThrottleForce should implement IThrottleReceiver.
        if (seatPoint == null)
            seatPoint = transform;
    }

    private void ResolveThrottleReceiver()
    {
        // 1) Explicit assignment
        if (throttleForce != null)
        {
            throttleReceiver = throttleForce;
            return;
        }

        // 2) Prefer parent chain (same boat)
        // Interfaces can't be fetched directly, so scan MonoBehaviours.
        var parents = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            if (parents[i] is IThrottleReceiver r)
            {
                throttleReceiver = r;

                // If it's actually a ThrottleForce, cache it for inspector visibility.
                if (parents[i] is ThrottleForce tf)
                    throttleForce = tf;

                return;
            }
        }

        // 3) Fallback: search within root (still same boat, just broader)
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
        if (throttleReceiver != null) return;
        if (_missingThrottleLogged) return;
        _missingThrottleLogged = true;

        Debug.LogError($"{name}: PilotChairInteractable needs a throttle receiver (IThrottleReceiver). " +
                       $"Assign ThrottleForce on the chair prefab OR ensure the boat contains an IThrottleReceiver.", this);
    }

    private void Update()
    {
        // If we’re missing throttle wiring, try again occasionally (spawn order can be weird).
        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();
            LogMissingThrottleOnceIfNeeded();
        }

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

        // If chair can't drive anything, don't allow sitting (prevents “pilot but nothing happens”).
        if (throttleReceiver == null)
        {
            ResolveThrottleReceiver();
            if (throttleReceiver == null)
            {
                LogMissingThrottleOnceIfNeeded();
                return false;
            }
        }

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