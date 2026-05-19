using UnityEngine;
using MiniGames;

public sealed class CargoSecuringMiniGameRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("Rope Consumable")]
    [SerializeField] private ItemDefinition ropeItemDefinition;
    [Min(1)]
    [SerializeField] private int secureRopeCost = 3;
    [Min(1)]
    [SerializeField] private int fastenRopeCost = 1;

    [Tooltip("Stored on secured cargo when rope is used. Quality still clamps to 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float ropeBonus01 = 0.15f;

    [Header("Secure Result")]
    [Tooltip("Max/current quality applied when the timing result is perfect.")]
    [Range(0f, 1f)]
    [SerializeField] private float secureQualityAtPerfect = 1f;

    [Header("Fasten Result")]
    [Tooltip("Amount restored by Fasten when the timing result is perfect.")]
    [Range(0f, 1f)]
    [SerializeField] private float fastenRestoreAtPerfect = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private static CargoSecuringMiniGameRunner _cached;

    private void Awake()
    {
        _cached = this;

        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>();
    }

    public static CargoSecuringMiniGameRunner GetOrFind()
    {
        if (_cached != null)
            return _cached;

        _cached = FindAnyObjectByType<CargoSecuringMiniGameRunner>();
        return _cached;
    }

    public bool TryOpenSecure(BoatSecuredItem item, BoatSecureZone zone)
    {
        return TryOpenSecure(item, zone, null);
    }

    public bool TryOpenSecure(BoatSecuredItem item, BoatSecureZone zone, GameObject actor)
    {
        if (item == null || zone == null)
            return false;

        if (!ResolveOverlay())
            return false;

        if (overlay.IsOpen)
            return false;

        PlayerInventory inventory = ResolveInventory(actor);

        var cart = new CargoSecuringTimingCartridge(
            onCompleted: (quality01, rating) =>
            {
                float q = Mathf.Clamp01(secureQualityAtPerfect * quality01);

                bool ok = item.SecureInZone(
                    zone,
                    q,
                    q,
                    usedRope: false,
                    ropeBonus01: 0f);

                Log($"Secure result rating={rating} quality={q:0.00} ok={ok}");
            },
            ropeButtonLabel: $"Secure with Rope (cost {secureRopeCost})",
            ropeCost: secureRopeCost,
            getRopeCount: () => GetRopeCount(inventory),
            canUseRope: () => CanUseRope(inventory, secureRopeCost),
            onRopeCompleted: () => TrySecureWithRope(item, zone, inventory));

        overlay.Open(cart, BuildContext(item));
        return true;
    }

    public bool TryOpenFasten(BoatSecuredItem item)
    {
        return TryOpenFasten(item, null);
    }

    public bool TryOpenFasten(BoatSecuredItem item, GameObject actor)
    {
        if (item == null || !item.IsSecured)
            return false;

        if (!ResolveOverlay())
            return false;

        if (overlay.IsOpen)
            return false;

        PlayerInventory inventory = ResolveInventory(actor);

        var cart = new CargoSecuringTimingCartridge(
            onCompleted: (quality01, rating) =>
            {
                float restore = Mathf.Clamp01(fastenRestoreAtPerfect * quality01);
                bool ok = item.TryFasten(restore);

                Log($"Fasten result rating={rating} restore={restore:0.00} ok={ok}");
            },
            ropeButtonLabel: $"Fasten with Rope (cost {fastenRopeCost})",
            ropeCost: fastenRopeCost,
            getRopeCount: () => GetRopeCount(inventory),
            canUseRope: () => CanUseRope(inventory, fastenRopeCost) && item.SecureQualityCurrent01 < item.SecureQualityMax01,
            onRopeCompleted: () => TryFastenWithRope(item, inventory));

        overlay.Open(cart, BuildContext(item));
        return true;
    }

    private bool TrySecureWithRope(BoatSecuredItem item, BoatSecureZone zone, PlayerInventory inventory)
    {
        if (item == null || zone == null)
            return false;

        if (!TryConsumeRope(inventory, secureRopeCost))
            return false;

        float q = Mathf.Clamp01(secureQualityAtPerfect);

        bool ok = item.SecureInZone(
            zone,
            q,
            q,
            usedRope: true,
            ropeBonus01: ropeBonus01);

        if (!ok)
        {
            RefundRope(inventory, secureRopeCost);
            Log("Secure with rope failed after consuming rope. Refunded rope.");
            return false;
        }

        Log($"Secure with rope quality={q:0.00} cost={secureRopeCost}");
        return true;
    }

    private bool TryFastenWithRope(BoatSecuredItem item, PlayerInventory inventory)
    {
        if (item == null || !item.IsSecured)
            return false;

        if (item.SecureQualityCurrent01 >= item.SecureQualityMax01)
            return false;

        if (!TryConsumeRope(inventory, fastenRopeCost))
            return false;

        bool ok = item.TryFasten(fastenRestoreAtPerfect);

        if (!ok)
        {
            RefundRope(inventory, fastenRopeCost);
            Log("Fasten with rope failed after consuming rope. Refunded rope.");
            return false;
        }

        Log($"Fasten with rope restore={fastenRestoreAtPerfect:0.00} cost={fastenRopeCost}");
        return true;
    }

    private int GetRopeCount(PlayerInventory inventory)
    {
        if (ropeItemDefinition == null || inventory == null)
            return 0;

        return InventoryConsumableUtility.Count(inventory, ropeItemDefinition);
    }

    private bool CanUseRope(PlayerInventory inventory, int cost)
    {
        if (ropeItemDefinition == null || inventory == null || cost <= 0)
            return false;

        return InventoryConsumableUtility.Count(inventory, ropeItemDefinition) >= cost;
    }

    private bool TryConsumeRope(PlayerInventory inventory, int cost)
    {
        if (ropeItemDefinition == null || inventory == null || cost <= 0)
            return false;

        return InventoryConsumableUtility.TryConsume(inventory, ropeItemDefinition, cost);
    }

    private void RefundRope(PlayerInventory inventory, int amount)
    {
        if (inventory == null || ropeItemDefinition == null || amount <= 0)
            return;

        ItemInstance refund = ItemInstance.Create(ropeItemDefinition, amount);

        if (!inventory.TryAutoInsert(refund, out ItemInstance remainder) ||
            remainder != null && !remainder.IsDepleted())
        {
            Debug.LogWarning(
                $"[CargoSecuringMiniGameRunner] Failed to fully refund rope amount={amount}. The inventory gods demand tribute.",
                this);
        }
    }

    private PlayerInventory ResolveInventory(GameObject actor)
    {
        if (actor != null)
        {
            PlayerInventory inventory =
                actor.GetComponentInParent<PlayerInventory>() ??
                actor.GetComponentInChildren<PlayerInventory>(true);

            if (inventory != null)
                return inventory;
        }

        return FindAnyObjectByType<PlayerInventory>();
    }

    private MiniGameContext BuildContext(BoatSecuredItem item)
    {
        return new MiniGameContext
        {
            targetId = item != null ? item.name : "cargo",
            difficulty = 1f,
            pressure = 0f,
            seed = Random.Range(1, int.MaxValue)
        };
    }

    private bool ResolveOverlay()
    {
        if (overlay == null)
            overlay = FindAnyObjectByType<MiniGameOverlayHost>();

        if (overlay != null)
            return true;

        Debug.LogError("[CargoSecuringMiniGameRunner] Missing MiniGameOverlayHost.", this);
        return false;
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[CargoSecuringMiniGameRunner] {msg}", this);
    }
}