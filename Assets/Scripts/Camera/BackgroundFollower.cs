using UnityEngine;

public class BackgroundFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public bool lockY = true;

    private IBoatRegistry _registry;
    private float _nextRetryTime;
    [SerializeField] private float retryInterval = 0.25f;

    [Range(0f, 1f)]
    public float celestialScale = 0.1f;

    private ServiceRoot env;

    private void Awake()
    {
        // Optional: keep inspector target if already set.
        // Otherwise we’ll resolve via registry.
    }

    private void OnEnable()
    {
        _registry = GameState.I != null ? GameState.I.boatRegistry : null;

        if (_registry != null)
        {
            _registry.BoatAdded += OnBoatAdded;
            _registry.BoatRemoved += OnBoatRemoved;
        }

        _nextRetryTime = Time.time;
    }

    private void OnDisable()
    {
        if (_registry != null)
        {
            _registry.BoatAdded -= OnBoatAdded;
            _registry.BoatRemoved -= OnBoatRemoved;
            _registry = null;
        }
    }

    private void Start()
    {
        // Old env lookup, but safe:
        env = ServiceRoot.Instance; // stop Find("EnvironmentManager") if possible

        // Resolve target if missing
        if (target == null)
            TryResolveTarget();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (Time.time >= _nextRetryTime)
            {
                TryResolveTarget();
                _nextRetryTime = Time.time + retryInterval;
            }
            return;
        }

        Vector3 pos = target.position + offset;
        if (lockY) pos.y = offset.y;
        transform.position = pos;

        CelestialBodyManager celestial = ServiceRoot.Instance?.CelestialBodyManager;
        if (celestial != null)
            celestial.SetHorizontalOffset(transform.position.x * celestialScale);
    }

    private void OnBoatAdded(Boat boat)
    {
        if (target != null) return;

        if (IsPlayerBoat(boat))
            target = boat.transform;
    }

    private void OnBoatRemoved(Boat boat)
    {
        if (boat == null) return;
        if (target == boat.transform)
            target = null; // will retry in LateUpdate
    }

    private void TryResolveTarget()
    {
        if (_registry == null)
            _registry = GameState.I != null ? GameState.I.boatRegistry : null;

        if (_registry == null) return;

        var gs = GameState.I;
        if (gs == null || gs.boat == null) return;

        string playerBoatId = gs.boat.boatInstanceId;
        if (string.IsNullOrEmpty(playerBoatId)) return;

        if (_registry.TryGetById(playerBoatId, out var boat) && boat != null)
        {
            target = boat.transform;
        }
    }


    // Phase-1: assume first boat is player boat.
    // Later: use boatInstanceId from GameState.activeTravel / GameState.boat.
    private bool IsPlayerBoat(Boat boat)
    {
        if (boat == null) return false;

        var gs = GameState.I;
        if (gs == null || gs.boat == null) return false;

        return boat.BoatInstanceId == gs.boat.boatInstanceId;
    }
}
