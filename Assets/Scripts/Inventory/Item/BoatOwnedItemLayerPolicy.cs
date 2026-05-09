using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItemLayerPolicy : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoatOwnedItem ownedItem;
    [SerializeField] private Rigidbody2D rb;

    [Header("Layer Names")]
    [SerializeField] private string hullLayerName = "Hull";
    [SerializeField] private string groundLayerName = "Ground";
    [SerializeField] private string nodeGroundLayerName = "NodeGround";
    [SerializeField] private string nodeDockLayerName = "NodeDock";

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private int _hullLayer;
    private int _groundLayer;
    private int _nodeGroundLayer;
    private int _nodeDockLayer;

    private int _hullBit;
    private int _nonBoatWorldBits;

    private bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();

        if (ownedItem != null)
            ownedItem.OwnershipChanged += OnOwnershipChanged;

        ApplyNow();
    }

    private void OnDisable()
    {
        if (ownedItem != null)
            ownedItem.OwnershipChanged -= OnOwnershipChanged;
    }

    private void OnOwnershipChanged(BoatOwnedItem item)
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        Initialize();

        if (rb == null)
        {
            LogWarning("ApplyNow skipped because Rigidbody2D is missing.");
            return;
        }

        int mask = rb.excludeLayers;

        bool boatOwned = ownedItem != null && ownedItem.IsOwnedByBoat;

        if (boatOwned)
        {
            // Boat-owned item behaves like boarded player:
            // collide with boat hull, ignore node/world/dock geometry.
            mask &= ~_hullBit;
            mask |= _nonBoatWorldBits;
        }
        else
        {
            // World/unowned item behaves like unboarded player:
            // ignore boat hull, collide with normal world/dock geometry.
            mask |= _hullBit;
            mask &= ~_nonBoatWorldBits;
        }

        rb.excludeLayers = mask;

        Log(
            $"ApplyNow | boatOwned={boatOwned} " +
            $"| owningBoatId='{(ownedItem != null ? ownedItem.OwningBoatInstanceId : "NULL")}' " +
            $"| excludeLayers={rb.excludeLayers}");
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        if (ownedItem == null)
            ownedItem = GetComponent<BoatOwnedItem>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        _hullLayer = LayerMask.NameToLayer(hullLayerName);
        _groundLayer = LayerMask.NameToLayer(groundLayerName);
        _nodeGroundLayer = LayerMask.NameToLayer(nodeGroundLayerName);
        _nodeDockLayer = LayerMask.NameToLayer(nodeDockLayerName);

        if (_hullLayer < 0) Debug.LogError($"[BoatOwnedItemLayerPolicy:{name}] Layer '{hullLayerName}' not found.", this);
        if (_groundLayer < 0) Debug.LogError($"[BoatOwnedItemLayerPolicy:{name}] Layer '{groundLayerName}' not found.", this);
        if (_nodeGroundLayer < 0) Debug.LogError($"[BoatOwnedItemLayerPolicy:{name}] Layer '{nodeGroundLayerName}' not found.", this);
        if (_nodeDockLayer < 0) Debug.LogError($"[BoatOwnedItemLayerPolicy:{name}] Layer '{nodeDockLayerName}' not found.", this);

        _hullBit = LayerBitOrZero(_hullLayer);

        _nonBoatWorldBits =
            LayerBitOrZero(_groundLayer) |
            LayerBitOrZero(_nodeGroundLayer) |
            LayerBitOrZero(_nodeDockLayer);

        _initialized = true;
    }

    private static int LayerBitOrZero(int layer)
    {
        return layer >= 0 ? 1 << layer : 0;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatOwnedItemLayerPolicy:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[BoatOwnedItemLayerPolicy:{name}] {msg}", this);
    }
}