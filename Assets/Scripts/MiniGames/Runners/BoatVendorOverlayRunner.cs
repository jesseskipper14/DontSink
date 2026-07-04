using System.Collections.Generic;
using MiniGames;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatVendorOverlayRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MiniGameOverlayHost overlay;
    [SerializeField] private BoatCatalog boatCatalog;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;
    [SerializeField] private BoatVendorModuleTransactionService moduleTransactionService;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool subscribed;

    private string activeSessionId;
    private BoatVendorPlaceholderServiceDefinition activeVendor;
    private AgentServiceContext activeAgentContext;
    private BoatVendorCartridge activeCartridge;

    private List<ItemVendorSellOffer> activeModuleBuyOffers = new();

    private readonly List<ItemVendorSellSource> activePlayerModuleSellSources = new();
    private readonly List<ItemVendorSellSource> activeBoatModuleSellSources = new();

    private void Reset()
    {
        overlay = FindFirstObjectByType<MiniGameOverlayHost>();
        moduleTransactionService = FindFirstObjectByType<BoatVendorModuleTransactionService>();
    }

    private void Awake()
    {
        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();

        if (moduleTransactionService == null)
            moduleTransactionService = FindFirstObjectByType<BoatVendorModuleTransactionService>();
    }

    private void OnDisable()
    {
        Unsubscribe();

        activeVendor = null;
        activeCartridge = null;
        activeSessionId = null;

        activeModuleBuyOffers.Clear();
        activePlayerModuleSellSources.Clear();
        activeBoatModuleSellSources.Clear();
    }

    public bool Open(BoatVendorPlaceholderServiceDefinition vendor, AgentServiceContext context)
    {
        if (vendor == null)
        {
            Debug.LogWarning("[BoatVendorOverlayRunner] Cannot open. Vendor service is null.", this);
            return false;
        }

        if (overlay == null)
            overlay = FindFirstObjectByType<MiniGameOverlayHost>();

        if (moduleTransactionService == null)
            moduleTransactionService = FindFirstObjectByType<BoatVendorModuleTransactionService>();

        if (overlay == null)
        {
            Debug.LogError("[BoatVendorOverlayRunner] Missing MiniGameOverlayHost.", this);
            return false;
        }

        if (boatCatalog == null)
        {
            Debug.LogError("[BoatVendorOverlayRunner] Missing BoatCatalog.", this);
            return false;
        }

        if (itemCatalog == null)
        {
            Debug.LogError("[BoatVendorOverlayRunner] Missing ItemDefinitionCatalog.", this);
            return false;
        }

        if (moduleTransactionService == null)
        {
            Debug.LogError("[BoatVendorOverlayRunner] Missing BoatVendorModuleTransactionService.", this);
            return false;
        }

        activeVendor = vendor;
        activeAgentContext = context;
        activeSessionId = BuildSessionId(vendor, context);

        activeModuleBuyOffers = BoatVendorModuleOfferBuilder.BuildModuleOffers(
            vendor,
            itemCatalog);

        RebuildModuleSellSources();

        Log(
            $"Opening boat vendor '{vendor.ServiceId}' " +
            $"session='{activeSessionId}' " +
            $"boats={(boatCatalog.Entries != null ? boatCatalog.Entries.Count : 0)} " +
            $"moduleBuyOffers={activeModuleBuyOffers.Count} " +
            $"playerModuleSellSources={activePlayerModuleSellSources.Count} " +
            $"boatModuleSellSources={activeBoatModuleSellSources.Count}");

        Subscribe();

        MiniGameContext miniGameContext = new MiniGameContext
        {
            targetId = activeSessionId,
            difficulty = 1f,
            pressure = 0f,
            seed = BuildSeed(activeSessionId)
        };

        activeCartridge = new BoatVendorCartridge(
            activeSessionId,
            vendor,
            boatCatalog,
            activeModuleBuyOffers,
            activePlayerModuleSellSources,
            activeBoatModuleSellSources,
            itemCatalog,
            moduleTransactionService,
            context);

        overlay.Open(activeCartridge, miniGameContext);
        return true;
    }

    private void RebuildModuleSellSources()
    {
        activePlayerModuleSellSources.Clear();
        activeBoatModuleSellSources.Clear();

        List<ItemVendorSellSource> tmp = new();

        ItemVendorSellSourceCollector.CollectPlayerSources(activeAgentContext, tmp);
        CopyBoatVendorModules(tmp, activePlayerModuleSellSources);

        tmp.Clear();

        ItemVendorSellSourceCollector.CollectBoatSources(activeAgentContext, tmp);
        CopyBoatVendorModules(tmp, activeBoatModuleSellSources);
    }

    private static void CopyBoatVendorModules(
        List<ItemVendorSellSource> source,
        List<ItemVendorSellSource> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ItemVendorSellSource sellSource = source[i];
            if (sellSource == null)
                continue;

            ItemInstance item = sellSource.Item;
            if (item == null || item.Definition == null)
                continue;

            if (!BoatVendorModuleOfferBuilder.IsBoatVendorModule(item.Definition))
                continue;

            target.Add(sellSource);
        }
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

        if (effect.system == "BoatVendorModuleBuy")
        {
            ApplyModuleBuyEffect(effect);
            return;
        }

        if (effect.system == "BoatVendorModuleSell")
        {
            ApplyModuleSellEffect(effect);
            return;
        }
    }

    private void ApplyModuleBuyEffect(MiniGameEffect effect)
    {
        if (string.IsNullOrWhiteSpace(effect.payloadJson))
        {
            activeCartridge?.NotifyModuleBuyApplied(-1, false, "Missing module purchase payload.");
            return;
        }

        ItemVendorPurchaseDraft draft =
            JsonUtility.FromJson<ItemVendorPurchaseDraft>(effect.payloadJson);

        if (draft == null)
        {
            activeCartridge?.NotifyModuleBuyApplied(-1, false, "Invalid module purchase payload.");
            return;
        }

        if (activeVendor == null)
        {
            activeCartridge?.NotifyModuleBuyApplied(draft.offerIndex, false, "No active boat vendor.");
            return;
        }

        if (activeModuleBuyOffers == null || activeModuleBuyOffers.Count == 0)
        {
            activeCartridge?.NotifyModuleBuyApplied(draft.offerIndex, false, "No active module offers.");
            return;
        }

        if (draft.offerIndex < 0 || draft.offerIndex >= activeModuleBuyOffers.Count)
        {
            activeCartridge?.NotifyModuleBuyApplied(draft.offerIndex, false, "Invalid module offer.");
            return;
        }

        if (activeCartridge != null && activeCartridge.GetRuntimeModuleStock(draft.offerIndex) == 0)
        {
            activeCartridge.NotifyModuleBuyApplied(draft.offerIndex, false, "Out of stock.");
            return;
        }

        ItemVendorSellOffer offer = activeModuleBuyOffers[draft.offerIndex];

        bool ok = moduleTransactionService.TryBuyModule(
            activeVendor,
            offer,
            activeAgentContext,
            out string message);

        activeCartridge?.NotifyModuleBuyApplied(draft.offerIndex, ok, message);

        if (ok)
        {
            RebuildModuleSellSources();
            activeCartridge?.NotifyModuleSellSourcesChanged();
            Log($"Module purchase OK: {message}");
        }
        else
        {
            Debug.LogWarning($"[BoatVendorOverlayRunner] Module purchase failed: {message}", this);
        }
    }

    private void ApplyModuleSellEffect(MiniGameEffect effect)
    {
        if (string.IsNullOrWhiteSpace(effect.payloadJson))
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Missing module sell payload.");
            return;
        }

        ItemVendorSellDraft draft =
            JsonUtility.FromJson<ItemVendorSellDraft>(effect.payloadJson);

        if (draft == null)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Invalid module sell payload.");
            return;
        }

        List<ItemVendorSellSource> sources =
            draft.area == ItemVendorSellArea.Boat
                ? activeBoatModuleSellSources
                : activePlayerModuleSellSources;

        if (sources == null || sources.Count == 0)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "No module sell sources.");
            return;
        }

        if (draft.sourceIndex < 0 || draft.sourceIndex >= sources.Count)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Invalid module sell source.");
            return;
        }

        ItemVendorSellSource source = sources[draft.sourceIndex];

        if (source == null)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Missing module sell source.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(draft.sourceKey) &&
            draft.sourceKey != source.SourceKey)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Module source changed. Try again.");
            RebuildModuleSellSources();
            activeCartridge?.NotifyModuleSellSourcesChanged();
            return;
        }

        ItemInstance item = source.Item;
        if (item == null || item.Definition == null)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Module no longer exists.");
            RebuildModuleSellSources();
            activeCartridge?.NotifyModuleSellSourcesChanged();
            return;
        }

        if (!string.IsNullOrWhiteSpace(draft.expectedInstanceId) &&
            draft.expectedInstanceId != item.InstanceId)
        {
            activeCartridge?.NotifyModuleSellApplied(false, "Module changed before sale. Try again.");
            RebuildModuleSellSources();
            activeCartridge?.NotifyModuleSellSourcesChanged();
            return;
        }

        bool ok = moduleTransactionService.TrySellModule(
            activeVendor,
            source,
            Mathf.Max(1, draft.quantity),
            out string message);

        RebuildModuleSellSources();
        activeCartridge?.NotifyModuleSellSourcesChanged();
        activeCartridge?.NotifyModuleSellApplied(ok, message);

        if (ok)
            Log($"Module sell OK: {message}");
        else
            Debug.LogWarning($"[BoatVendorOverlayRunner] Module sell failed: {message}", this);
    }

    private static string BuildSessionId(
        BoatVendorPlaceholderServiceDefinition vendor,
        AgentServiceContext context)
    {
        string vendorId = vendor != null && !string.IsNullOrWhiteSpace(vendor.ServiceId)
            ? vendor.ServiceId
            : "unknown_boat_vendor";

        string agentId = context.Agent != null && !string.IsNullOrWhiteSpace(context.Agent.StableId)
            ? context.Agent.StableId
            : "unknown_agent";

        return $"boat_vendor:{agentId}:{vendorId}";
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

        Debug.Log($"[BoatVendorOverlayRunner] {message}", this);
    }
}