using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using MiniGames;

public sealed class CargoSecuringMiniGameRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("Tether Consumable")]
    [Tooltip(
        "Any ItemDefinition present in this catalog may be consumed as securing tether. " +
        "Add new rope/cable types to the catalog rather than hardcoding them here.")]
    [SerializeField] private TetherLineCatalog tetherLineCatalog;

    [Min(1)]
    [FormerlySerializedAs("secureRopeCost")]
    [SerializeField] private int secureTetherCost = 3;

    [Min(1)]
    [FormerlySerializedAs("fastenRopeCost")]
    [SerializeField] private int fastenTetherCost = 1;

    [Tooltip(
        "Stored on secured cargo when tether is used. This remains a generic securing " +
        "bonus for now; line-specific strength data remains authoritative in TetherLineCatalog.")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("ropeBonus01")]
    [SerializeField] private float tetherBonus01 = 0.15f;

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
            tetherOptions:
                BuildSecureTetherOptions(
                    item,
                    zone,
                    inventory));

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
            tetherOptions:
                BuildFastenTetherOptions(
                    item,
                    inventory));

        overlay.Open(cart, BuildContext(item));
        return true;
    }

    private List<CargoSecuringTimingCartridge.TetherOption>
        BuildSecureTetherOptions(
            BoatSecuredItem item,
            BoatSecureZone zone,
            PlayerInventory inventory)
    {
        return BuildTetherOptions(
            inventory,
            secureTetherCost,
            "Secure with",
            definition =>
                TrySecureWithTether(
                    item,
                    zone,
                    inventory,
                    definition),
            extraCanUse: null);
    }

    private List<CargoSecuringTimingCartridge.TetherOption>
        BuildFastenTetherOptions(
            BoatSecuredItem item,
            PlayerInventory inventory)
    {
        return BuildTetherOptions(
            inventory,
            fastenTetherCost,
            "Fasten with",
            definition =>
                TryFastenWithTether(
                    item,
                    inventory,
                    definition),
            extraCanUse: () =>
                item != null &&
                item.IsSecured &&
                item.SecureQualityCurrent01 <
                item.SecureQualityMax01);
    }

    private List<CargoSecuringTimingCartridge.TetherOption>
        BuildTetherOptions(
            PlayerInventory inventory,
            int cost,
            string verb,
            System.Func<ItemDefinition, bool> onCompleted,
            System.Func<bool> extraCanUse)
    {
        List<CargoSecuringTimingCartridge.TetherOption>
            options =
                new List<
                    CargoSecuringTimingCartridge.TetherOption>();

        if (inventory == null ||
            tetherLineCatalog == null ||
            tetherLineCatalog.Entries == null)
        {
            return options;
        }

        HashSet<ItemDefinition> visited =
            new HashSet<ItemDefinition>();

        IReadOnlyList<TetherLineCatalog.Entry> entries =
            tetherLineCatalog.Entries;

        for (int i = 0;
             i < entries.Count;
             i++)
        {
            TetherLineCatalog.Entry entry =
                entries[i];

            ItemDefinition definition =
                entry != null
                    ? entry.ItemDefinition
                    : null;

            if (definition == null ||
                !visited.Add(
                    definition))
            {
                continue;
            }

            int initialCount =
                InventoryConsumableUtility.Count(
                    inventory,
                    definition);

            // The player asked to choose among tether materials they actually
            // have. Do not clutter the mini-game with catalog entries at zero.
            if (initialCount <= 0)
                continue;

            ItemDefinition capturedDefinition =
                definition;

            options.Add(
                new CargoSecuringTimingCartridge.TetherOption(
                    buttonLabel:
                        $"{verb} " +
                        $"{capturedDefinition.DisplayName} " +
                        $"(cost {cost})",
                    resultLabel:
                        capturedDefinition.DisplayName,
                    cost:
                        cost,
                    getCount:
                        () =>
                            GetTetherCount(
                                inventory,
                                capturedDefinition),
                    canUse:
                        () =>
                            CanUseTether(
                                inventory,
                                capturedDefinition,
                                cost) &&
                            (extraCanUse == null ||
                             extraCanUse()),
                    onCompleted:
                        () =>
                            onCompleted != null &&
                            onCompleted(
                                capturedDefinition)));
        }

        return options;
    }

    private bool TrySecureWithTether(
        BoatSecuredItem item,
        BoatSecureZone zone,
        PlayerInventory inventory,
        ItemDefinition tetherDefinition)
    {
        if (item == null ||
            zone == null ||
            tetherDefinition == null)
        {
            return false;
        }

        if (!TryConsumeTether(
                inventory,
                tetherDefinition,
                secureTetherCost))
        {
            return false;
        }

        float q =
            Mathf.Clamp01(
                secureQualityAtPerfect);

        bool ok =
            item.SecureInZone(
                zone,
                q,
                q,
                usedRope: true,
                ropeBonus01: tetherBonus01);

        if (!ok)
        {
            RefundTether(
                inventory,
                tetherDefinition,
                secureTetherCost);

            Log(
                "Secure with tether failed after consumption. " +
                $"Refunded {tetherDefinition.DisplayName}.");

            return false;
        }

        Log(
            $"Secure with tether quality={q:0.00} " +
            $"cost={secureTetherCost} " +
            $"material={tetherDefinition.DisplayName}");

        return true;
    }

    private bool TryFastenWithTether(
        BoatSecuredItem item,
        PlayerInventory inventory,
        ItemDefinition tetherDefinition)
    {
        if (item == null ||
            !item.IsSecured ||
            tetherDefinition == null)
        {
            return false;
        }

        if (item.SecureQualityCurrent01 >=
            item.SecureQualityMax01)
        {
            return false;
        }

        if (!TryConsumeTether(
                inventory,
                tetherDefinition,
                fastenTetherCost))
        {
            return false;
        }

        bool ok =
            item.TryFasten(
                fastenRestoreAtPerfect);

        if (!ok)
        {
            RefundTether(
                inventory,
                tetherDefinition,
                fastenTetherCost);

            Log(
                "Fasten with tether failed after consumption. " +
                $"Refunded {tetherDefinition.DisplayName}.");

            return false;
        }

        Log(
            $"Fasten with tether restore={fastenRestoreAtPerfect:0.00} " +
            $"cost={fastenTetherCost} " +
            $"material={tetherDefinition.DisplayName}");

        return true;
    }

    private static int GetTetherCount(
        PlayerInventory inventory,
        ItemDefinition tetherDefinition)
    {
        if (inventory == null ||
            tetherDefinition == null)
        {
            return 0;
        }

        return
            InventoryConsumableUtility.Count(
                inventory,
                tetherDefinition);
    }

    private static bool CanUseTether(
        PlayerInventory inventory,
        ItemDefinition tetherDefinition,
        int cost)
    {
        if (inventory == null ||
            tetherDefinition == null ||
            cost <= 0)
        {
            return false;
        }

        return
            GetTetherCount(
                inventory,
                tetherDefinition) >= cost;
    }

    private static bool TryConsumeTether(
        PlayerInventory inventory,
        ItemDefinition tetherDefinition,
        int cost)
    {
        if (inventory == null ||
            tetherDefinition == null ||
            cost <= 0)
        {
            return false;
        }

        return
            InventoryConsumableUtility.TryConsume(
                inventory,
                tetherDefinition,
                cost);
    }

    private void RefundTether(
        PlayerInventory inventory,
        ItemDefinition tetherDefinition,
        int amount)
    {
        if (inventory == null ||
            tetherDefinition == null ||
            amount <= 0)
        {
            return;
        }

        ItemInstance refund =
            ItemInstance.Create(
                tetherDefinition,
                amount);

        if (!inventory.TryAutoInsert(
                refund,
                out ItemInstance remainder) ||
            remainder != null &&
            !remainder.IsDepleted())
        {
            Debug.LogWarning(
                "[CargoSecuringMiniGameRunner] " +
                $"Failed to fully refund tether " +
                $"item='{tetherDefinition.DisplayName}' " +
                $"amount={amount}. " +
                "The inventory gods demand tribute.",
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