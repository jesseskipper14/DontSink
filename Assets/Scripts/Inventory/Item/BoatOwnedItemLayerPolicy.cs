using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItemLayerPolicy :
    MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoatOwnedItem ownedItem;
    [SerializeField] private Rigidbody2D rb;

    [Header("Layer Names")]
    [SerializeField]
    private string hullLayerName =
        "Hull";

    [SerializeField]
    private string groundLayerName =
        "Ground";

    [SerializeField]
    private string worldLedgeLayerName =
        "WorldLedge";

    [SerializeField]
    private string ghostCollisionLayerName =
        "GhostCollision";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    private int _hullLayer;
    private int _groundLayer;
    private int _worldLedgeLayer;
    private int _ghostCollisionLayer;

    private int _hullBit;
    private int _ghostCollisionBit;
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
        {
            ownedItem.OwnershipChanged +=
                OnOwnershipChanged;
        }

        GhostCollisionProxy.ActiveProxySetChanged +=
            OnGhostProxySetChanged;

        ApplyNow();
    }

    private void OnDisable()
    {
        if (ownedItem != null)
        {
            ownedItem.OwnershipChanged -=
                OnOwnershipChanged;
        }

        GhostCollisionProxy.ActiveProxySetChanged -=
            OnGhostProxySetChanged;
    }

    private void OnOwnershipChanged(
        BoatOwnedItem item)
    {
        ApplyNow();
    }

    private void OnGhostProxySetChanged()
    {
        ApplyNow();
    }

    [ContextMenu("Apply Layer Policy Now")]
    public void ApplyNow()
    {
        Initialize();

        if (rb == null)
        {
            LogWarning(
                "ApplyNow skipped because Rigidbody2D is missing.");

            return;
        }

        DivingBellContainedItem bellContained =
            GetComponent<DivingBellContainedItem>();

        if (bellContained != null &&
            bellContained.IsContainedInBell)
        {
            // BellItem is a complete, explicit physical context.
            //
            // Stop ordinary boat GhostCollision pair routing, then let the bell
            // component place the item hierarchy on BellItem. No Rigidbody2D
            // includeLayers/excludeLayers trickery is required for bell physics.
            ApplyGhostCollisionPairs(
                null);

            bellContained.ReapplyPhysicalContext();

            Log(
                $"ApplyNow | BELL CONTEXT " +
                $"bell='{(bellContained.CurrentBell != null ? bellContained.CurrentBell.name : "NULL")}' " +
                $"| boatOwned={(ownedItem != null && ownedItem.IsOwnedByBoat)}");

            return;
        }

        bool boatOwned =
            ownedItem != null &&
            ownedItem.IsOwnedByBoat;

        GhostCollisionProxy ownerGhost =
            ResolveOwningGhostProxy();

        bool useGhost =
            boatOwned &&
            ownerGhost != null &&
            ownerGhost.IsBuilt;

        int mask =
            rb.excludeLayers;

        if (useGhost)
        {
            // Ghost mode:
            // - ignore the real boat hull
            // - allow the owning GhostCollision shell
            // - ignore normal world ground/ledges while aboard
            mask |=
                _hullBit;

            mask &=
                ~_ghostCollisionBit;

            mask |=
                _nonBoatWorldBits;
        }
        else if (boatOwned)
        {
            // Safe fallback if this boat has no working ghost proxy.
            mask &=
                ~_hullBit;

            mask |=
                _ghostCollisionBit;

            mask |=
                _nonBoatWorldBits;
        }
        else
        {
            // Ordinary world item:
            // - ignore real boat hull
            // - ignore every ghost shell
            // - collide with normal world ground/ledges
            mask |=
                _hullBit;

            mask |=
                _ghostCollisionBit;

            mask &=
                ~_nonBoatWorldBits;
        }

        rb.excludeLayers =
            mask;

        ApplyGhostCollisionPairs(
            useGhost
                ? ownerGhost
                : null);

        Log(
            $"ApplyNow | boatOwned={boatOwned} " +
            $"| ghost={(useGhost ? ownerGhost.name : "NONE/FALLBACK")} " +
            $"| owningBoatId='{(ownedItem != null ? ownedItem.OwningBoatInstanceId : "NULL")}' " +
            $"| excludeLayers={rb.excludeLayers}");
    }

    private GhostCollisionProxy ResolveOwningGhostProxy()
    {
        if (ownedItem == null ||
            ownedItem.OwningBoat == null)
        {
            return null;
        }

        return
            ownedItem.OwningBoat
                .GetComponent<GhostCollisionProxy>();
    }

    private void ApplyGhostCollisionPairs(
        GhostCollisionProxy allowedProxy)
    {
        if (rb == null)
            return;

        Collider2D[] all =
            GetComponentsInChildren<Collider2D>(
                true);

        if (all == null ||
            all.Length == 0)
        {
            return;
        }

        System.Collections.Generic.List<Collider2D> solids =
            new System.Collections.Generic.List<Collider2D>();

        for (int i = 0;
             i < all.Length;
             i++)
        {
            Collider2D collider =
                all[i];

            if (collider == null ||
                collider.isTrigger ||
                collider.attachedRigidbody != rb)
            {
                continue;
            }

            solids.Add(
                collider);
        }

        GhostCollisionProxy.ConfigureExclusiveCollisions(
            solids,
            allowedProxy);
    }

    private void Initialize()
    {
        if (_initialized)
            return;

        if (ownedItem == null)
        {
            ownedItem =
                GetComponent<BoatOwnedItem>();
        }

        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody2D>();
        }

        _hullLayer =
            LayerMask.NameToLayer(
                hullLayerName);

        _groundLayer =
            LayerMask.NameToLayer(
                groundLayerName);

        _worldLedgeLayer =
            LayerMask.NameToLayer(
                worldLedgeLayerName);

        _ghostCollisionLayer =
            LayerMask.NameToLayer(
                ghostCollisionLayerName);

        if (_hullLayer < 0)
        {
            Debug.LogError(
                $"[BoatOwnedItemLayerPolicy:{name}] Layer '{hullLayerName}' not found.",
                this);
        }

        if (_groundLayer < 0)
        {
            Debug.LogError(
                $"[BoatOwnedItemLayerPolicy:{name}] Layer '{groundLayerName}' not found.",
                this);
        }

        if (_worldLedgeLayer < 0)
        {
            Debug.LogError(
                $"[BoatOwnedItemLayerPolicy:{name}] Layer '{worldLedgeLayerName}' not found.",
                this);
        }

        if (_ghostCollisionLayer < 0)
        {
            Debug.LogError(
                $"[BoatOwnedItemLayerPolicy:{name}] Layer '{ghostCollisionLayerName}' not found.",
                this);
        }

        _hullBit =
            LayerBitOrZero(
                _hullLayer);

        _ghostCollisionBit =
            LayerBitOrZero(
                _ghostCollisionLayer);

        _nonBoatWorldBits =
            LayerBitOrZero(
                _groundLayer) |
            LayerBitOrZero(
                _worldLedgeLayer);

        _initialized =
            true;
    }

    private static int LayerBitOrZero(
        int layer)
    {
        return
            layer >= 0
                ? 1 << layer
                : 0;
    }

    private void Log(
        string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log(
            $"[BoatOwnedItemLayerPolicy:{name}] {msg}",
            this);
    }

    private void LogWarning(
        string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning(
            $"[BoatOwnedItemLayerPolicy:{name}] {msg}",
            this);
    }
}
