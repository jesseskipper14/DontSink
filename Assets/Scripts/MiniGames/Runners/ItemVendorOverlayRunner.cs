using System.Collections.Generic;
using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemVendorOverlayRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;
    [SerializeField] private ItemVendorPurchaseService purchaseService;
    [SerializeField] private ItemVendorSellService sellService;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool subscribed;

    private string activeSessionId;
    private ItemVendorServiceDefinition activeVendor;
    private AgentServiceContext activeAgentContext;
    private ItemVendorCartridge activeCartridge;

    private List<ItemVendorSellOffer> activeSellOffers = new();

    private readonly List<ItemVendorSellSource> activePlayerSellSources = new();
    private readonly List<ItemVendorSellSource> activeBoatSellSources = new();

    private void Reset()
    {
        overlay = FindFirstObjectByType<MiniGameOverlayHost>();
        purchaseService = FindFirstObjectByType<ItemVendorPurchaseService>();
        sellService = FindFirstObjectByType<ItemVendorSellService>();
    }

    private void Awake()
    {
        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();

        if (purchaseService == null)
            purchaseService = FindFirstObjectByType<ItemVendorPurchaseService>();

        if (sellService == null)
            sellService = FindFirstObjectByType<ItemVendorSellService>();
    }

    private void OnDisable()
    {
        Unsubscribe();

        activeVendor = null;
        activeCartridge = null;
        activeSessionId = null;

        activeSellOffers.Clear();
        activePlayerSellSources.Clear();
        activeBoatSellSources.Clear();
    }

    public bool Open(ItemVendorServiceDefinition vendor, AgentServiceContext context)
    {
        if (vendor == null)
        {
            Debug.LogWarning("[ItemVendorOverlayRunner] Cannot open. Vendor service is null.", this);
            return false;
        }

        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();

        if (purchaseService == null)
            purchaseService = FindFirstObjectByType<ItemVendorPurchaseService>();

        if (sellService == null)
            sellService = FindFirstObjectByType<ItemVendorSellService>();

        if (overlay == null)
        {
            Debug.LogError("[ItemVendorOverlayRunner] Missing MiniGameOverlayHost.", this);
            return false;
        }

        if (purchaseService == null)
        {
            Debug.LogError("[ItemVendorOverlayRunner] Missing ItemVendorPurchaseService.", this);
            return false;
        }

        if (sellService == null)
        {
            Debug.LogError("[ItemVendorOverlayRunner] Missing ItemVendorSellService.", this);
            return false;
        }

        if (itemCatalog == null)
        {
            Debug.LogError("[ItemVendorOverlayRunner] Missing ItemDefinitionCatalog. Assign it in the inspector.", this);
            return false;
        }

        activeVendor = vendor;
        activeAgentContext = context;
        activeSessionId = BuildSessionId(vendor, context);

        ItemVendorAvailabilityContext availabilityContext = ItemVendorAvailabilityContext.Unknown;

        activeSellOffers = ItemVendorOfferBuilder.BuildSellOffers(
            vendor,
            itemCatalog,
            availabilityContext);

        RebuildSellSources();

        Log(
            $"Opening vendor '{vendor.ServiceId}' " +
            $"session='{activeSessionId}' " +
            $"generatedOffers={activeSellOffers.Count} " +
            $"playerSellSources={activePlayerSellSources.Count} " +
            $"boatSellSources={activeBoatSellSources.Count}");

        Subscribe();

        MiniGameContext miniGameContext = new MiniGameContext
        {
            targetId = activeSessionId,
            difficulty = 1f,
            pressure = 0f,
            seed = BuildSeed(activeSessionId)
        };

        activeCartridge = new ItemVendorCartridge(
            activeSessionId,
            vendor,
            activeSellOffers,
            activePlayerSellSources,
            activeBoatSellSources,
            itemCatalog,
            purchaseService,
            sellService,
            context);

        overlay.Open(activeCartridge, miniGameContext);
        return true;
    }

    private void RebuildSellSources()
    {
        activePlayerSellSources.Clear();
        activeBoatSellSources.Clear();

        ItemVendorSellSourceCollector.CollectPlayerSources(
            activeAgentContext,
            activePlayerSellSources);

        ItemVendorSellSourceCollector.CollectBoatSources(
            activeAgentContext,
            activeBoatSellSources);
    }

    private void Subscribe()
    {
        if (subscribed || overlay == null)
            return;

        overlay.EffectEmitted += OnMiniGameEffect;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || overlay == null)
            return;

        overlay.EffectEmitted -= OnMiniGameEffect;
        subscribed = false;
    }

    private void OnMiniGameEffect(MiniGameEffect effect)
    {
        if (effect.kind != MiniGameEffectKind.Transaction)
            return;

        if (effect.targetId != activeSessionId)
            return;

        if (effect.system == "ItemVendor")
        {
            ApplyBuyEffect(effect);
            return;
        }

        if (effect.system == "ItemVendorSell")
        {
            ApplySellEffect(effect);
            return;
        }
    }

    private void ApplyBuyEffect(MiniGameEffect effect)
    {
        if (string.IsNullOrWhiteSpace(effect.payloadJson))
        {
            activeCartridge?.NotifyPurchaseApplied(-1, false, "Missing purchase payload.");
            return;
        }

        ItemVendorPurchaseDraft draft =
            JsonUtility.FromJson<ItemVendorPurchaseDraft>(effect.payloadJson);

        if (draft == null)
        {
            activeCartridge?.NotifyPurchaseApplied(-1, false, "Invalid purchase payload.");
            return;
        }

        if (activeVendor == null)
        {
            activeCartridge?.NotifyPurchaseApplied(draft.offerIndex, false, "No active vendor.");
            return;
        }

        if (activeSellOffers == null || activeSellOffers.Count == 0)
        {
            activeCartridge?.NotifyPurchaseApplied(draft.offerIndex, false, "No active offers.");
            return;
        }

        if (draft.offerIndex < 0 || draft.offerIndex >= activeSellOffers.Count)
        {
            activeCartridge?.NotifyPurchaseApplied(draft.offerIndex, false, "Invalid offer.");
            return;
        }

        if (activeCartridge != null && activeCartridge.GetRuntimeStock(draft.offerIndex) == 0)
        {
            activeCartridge.NotifyPurchaseApplied(draft.offerIndex, false, "Out of stock.");
            return;
        }

        ItemVendorSellOffer offer = activeSellOffers[draft.offerIndex];

        bool ok = purchaseService.TryBuy(
            activeVendor,
            offer,
            activeAgentContext,
            out string message);

        activeCartridge?.NotifyPurchaseApplied(draft.offerIndex, ok, message);

        if (ok)
        {
            RebuildSellSources();
            activeCartridge?.NotifySellSourcesChanged();
            Log($"Purchase OK: {message}");
        }
        else
        {
            Debug.LogWarning($"[ItemVendorOverlayRunner] Purchase failed: {message}", this);
        }
    }

    private void ApplySellEffect(MiniGameEffect effect)
    {
        if (string.IsNullOrWhiteSpace(effect.payloadJson))
        {
            activeCartridge?.NotifySellApplied(false, "Missing sell payload.");
            return;
        }

        ItemVendorSellDraft draft =
            JsonUtility.FromJson<ItemVendorSellDraft>(effect.payloadJson);

        if (draft == null)
        {
            activeCartridge?.NotifySellApplied(false, "Invalid sell payload.");
            return;
        }

        List<ItemVendorSellSource> sources =
            draft.area == ItemVendorSellArea.Boat
                ? activeBoatSellSources
                : activePlayerSellSources;

        if (sources == null || sources.Count == 0)
        {
            activeCartridge?.NotifySellApplied(false, "No sellable sources.");
            return;
        }

        if (draft.sourceIndex < 0 || draft.sourceIndex >= sources.Count)
        {
            activeCartridge?.NotifySellApplied(false, "Invalid sell source.");
            return;
        }

        ItemVendorSellSource source = sources[draft.sourceIndex];

        if (source == null)
        {
            activeCartridge?.NotifySellApplied(false, "Missing sell source.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(draft.sourceKey) &&
            draft.sourceKey != source.SourceKey)
        {
            activeCartridge?.NotifySellApplied(false, "Sell source changed. Try again.");
            RebuildSellSources();
            activeCartridge?.NotifySellSourcesChanged();
            return;
        }

        ItemInstance item = source.Item;
        if (item == null || item.Definition == null)
        {
            activeCartridge?.NotifySellApplied(false, "Item no longer exists.");
            RebuildSellSources();
            activeCartridge?.NotifySellSourcesChanged();
            return;
        }

        if (!string.IsNullOrWhiteSpace(draft.expectedInstanceId) &&
            draft.expectedInstanceId != item.InstanceId)
        {
            activeCartridge?.NotifySellApplied(false, "Item changed before sale. Try again.");
            RebuildSellSources();
            activeCartridge?.NotifySellSourcesChanged();
            return;
        }

        bool ok = sellService.TrySell(
            activeVendor,
            source,
            Mathf.Max(1, draft.quantity),
            out string message);

        RebuildSellSources();
        activeCartridge?.NotifySellSourcesChanged();
        activeCartridge?.NotifySellApplied(ok, message);

        if (ok)
            Log($"Sell OK: {message}");
        else
            Debug.LogWarning($"[ItemVendorOverlayRunner] Sell failed: {message}", this);
    }

    private static string BuildSessionId(
        ItemVendorServiceDefinition vendor,
        AgentServiceContext context)
    {
        string vendorId = vendor != null && !string.IsNullOrWhiteSpace(vendor.ServiceId)
            ? vendor.ServiceId
            : "unknown_vendor";

        string agentId = context.Agent != null && !string.IsNullOrWhiteSpace(context.Agent.StableId)
            ? context.Agent.StableId
            : "unknown_agent";

        return $"item_vendor:{agentId}:{vendorId}";
    }

    private static int BuildSeed(string text)
    {
        unchecked
        {
            int seed = 17;

            if (!string.IsNullOrWhiteSpace(text))
            {
                for (int i = 0; i < text.Length; i++)
                    seed = seed * 31 + text[i];
            }

            return seed;
        }
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[ItemVendorOverlayRunner] {message}", this);
    }
}