using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    public sealed class ItemVendorCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private sealed class ItemVendorUiState
        {
            public Vector2 offerScroll;
            public string lastUiNote;
            public readonly Dictionary<int, int> runtimeStockByOfferIndex = new();
        }

        private static readonly Dictionary<string, ItemVendorUiState> UiStateByKey =
            new(StringComparer.Ordinal);

        private readonly string _sessionId;
        private readonly ItemVendorServiceDefinition _vendor;
        private readonly IReadOnlyList<ItemVendorSellOffer> _offers;
        private readonly ItemDefinitionCatalog _itemCatalog;
        private readonly ItemVendorPurchaseService _purchaseService;
        private readonly AgentServiceContext _agentContext;

        private MiniGameContext _ctx;
        private bool _requestedClose;
        private ItemVendorUiState _uiState;

        public ItemVendorCartridge(
            string sessionId,
            ItemVendorServiceDefinition vendor,
            IReadOnlyList<ItemVendorSellOffer> offers,
            ItemDefinitionCatalog itemCatalog,
            ItemVendorPurchaseService purchaseService,
            AgentServiceContext agentContext)
        {
            _sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? "item_vendor:unknown"
                : sessionId;

            _vendor = vendor;
            _offers = offers ?? Array.Empty<ItemVendorSellOffer>();
            _itemCatalog = itemCatalog;
            _purchaseService = purchaseService;
            _agentContext = agentContext;
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();
            _uiState = GetOrCreateUiState();
            EnsureRuntimeStock();
            _requestedClose = false;
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_requestedClose)
            {
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.Cancelled,
                    quality01 = 1f,
                    note = _uiState != null && !string.IsNullOrWhiteSpace(_uiState.lastUiNote)
                        ? _uiState.lastUiNote
                        : "Closed",
                    hasMeaningfulProgress = false
                };
            }

            return new MiniGameResult
            {
                outcome = MiniGameOutcome.None,
                quality01 = 1f,
                note = null,
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Cancel()
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = "Cancelled",
                hasMeaningfulProgress = false
            };
        }

        public MiniGameResult Interrupt(string reason)
        {
            return new MiniGameResult
            {
                outcome = MiniGameOutcome.Cancelled,
                quality01 = 1f,
                note = $"Interrupted: {reason}",
                hasMeaningfulProgress = false
            };
        }

        public void End()
        {
            _ctx = null;
        }

        public void DrawOverlayGUI(Rect panel)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            EnsureRuntimeStock();

            float pad = 14f;

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 10, panel.width - pad * 2f, 22),
                ResolveTitle());

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 32, panel.width - pad * 2f, 22),
                GetMoneyLabel());

            float closeX = panel.xMax - 34;
            float closeY = panel.y + 8;

            if (GUI.Button(new Rect(closeX, closeY, 26, 22), "X"))
            {
                _requestedClose = true;
                _uiState.lastUiNote = "Closed";
            }

            float listX = panel.x + pad;
            float listY = panel.y + 66f;
            float listW = panel.width - pad * 2f;
            float listH = panel.height - 140f;

            int offerCount = _offers != null ? _offers.Count : 0;

            Rect viewRect = new Rect(listX, listY, listW, listH);
            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, offerCount + 1) * 38f + 24f);

            _uiState.offerScroll =
                GUI.BeginScrollView(viewRect, _uiState.offerScroll, contentRect);

            float y = 6f;

            DrawHeaderRow(y, contentRect.width);
            y += 28f;

            bool any = false;

            if (_offers != null)
            {
                for (int i = 0; i < _offers.Count; i++)
                {
                    ItemVendorSellOffer offer = _offers[i];
                    if (offer == null)
                        continue;

                    DrawOfferRow(i, offer, y, contentRect.width);
                    y += 38f;
                    any = true;
                }
            }

            if (!any)
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No items for sale)");

            GUI.EndScrollView();

            if (!string.IsNullOrWhiteSpace(_uiState.lastUiNote))
            {
                GUI.Label(
                    new Rect(panel.x + pad, panel.yMax - 64, panel.width - pad * 2f, 22),
                    _uiState.lastUiNote);
            }

            if (GUI.Button(new Rect(panel.x + pad, panel.yMax - 38, 110f, 26f), "Close"))
            {
                _requestedClose = true;
                _uiState.lastUiNote = "Closed";
            }
        }

        public int GetRuntimeStock(int offerIndex)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            EnsureRuntimeStock();

            return _uiState.runtimeStockByOfferIndex.TryGetValue(offerIndex, out int stock)
                ? stock
                : 0;
        }

        public void NotifyPurchaseApplied(int offerIndex, bool success, string message)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.lastUiNote = message;

            if (!success)
                return;

            int stock = GetRuntimeStock(offerIndex);
            if (stock < 0)
                return;

            _uiState.runtimeStockByOfferIndex[offerIndex] = Mathf.Max(0, stock - 1);
        }

        private string ResolveTitle()
        {
            string agentName = _agentContext.Agent != null
                ? _agentContext.Agent.DisplayName
                : "Item Vendor";

            return $"{agentName} - Items";
        }

        private string GetMoneyLabel()
        {
            if (!MoneyService.HasActiveChest)
                return "Money Chest: NONE";

            return $"Money Chest: ${MoneyService.Balance:n0}";
        }

        private void DrawHeaderRow(float y, float width)
        {
            GUI.Label(new Rect(6, y, 180, 22), "ITEM");
            GUI.Label(new Rect(190, y, 100, 22), "PRICE");
            GUI.Label(new Rect(300, y, 100, 22), "STOCK");
        }

        private void DrawOfferRow(int index, ItemVendorSellOffer offer, float y, float width)
        {
            ItemDefinition def = offer.ResolveDefinition(_itemCatalog);
            int price = offer.ResolvePrice(def);
            string label = offer.ResolveLabel(def);

            int stock = GetRuntimeStock(index);
            string stockLabel = stock < 0 ? "∞" : stock.ToString();

            GUI.Label(new Rect(6, y, 180, 22), label);
            GUI.Label(new Rect(190, y, 100, 22), $"${price:n0}");
            GUI.Label(new Rect(300, y, 100, 22), stockLabel);

            bool canBuy = false;
            string reason = null;

            if (stock == 0)
            {
                reason = "Out of stock.";
            }
            else if (_purchaseService == null)
            {
                reason = "Missing purchase service.";
            }
            else
            {
                canBuy = _purchaseService.CanBuy(
                    _vendor,
                    offer,
                    _agentContext,
                    out _,
                    out _,
                    out reason);
            }

            GUI.enabled = canBuy;

            if (GUI.Button(new Rect(width - 84f, y, 78f, 24f), "Buy"))
                SubmitPurchase(index, offer, def, price);

            GUI.enabled = true;

            if (!canBuy && !string.IsNullOrWhiteSpace(reason))
                GUI.Label(new Rect(width - 270f, y, 180f, 22), reason);
        }

        private void SubmitPurchase(
            int offerIndex,
            ItemVendorSellOffer offer,
            ItemDefinition resolvedDefinition,
            int unitPrice)
        {
            if (_ctx == null)
                return;

            string itemId = resolvedDefinition != null
                ? resolvedDefinition.ItemId
                : offer.itemId;

            ItemVendorPurchaseDraft draft = new ItemVendorPurchaseDraft
            {
                version = 1,
                vendorId = _vendor != null ? _vendor.ServiceId : null,
                offerIndex = offerIndex,
                itemId = itemId,
                quantity = 1,
                unitPrice = unitPrice
            };

            string json = JsonUtility.ToJson(draft);

            _ctx.emitEffect?.Invoke(new MiniGameEffect
            {
                kind = MiniGameEffectKind.Transaction,
                system = "ItemVendor",
                targetId = _sessionId,
                payloadJson = json
            });

            _uiState.lastUiNote = "Purchase submitted.";
        }

        private ItemVendorUiState GetOrCreateUiState()
        {
            if (!UiStateByKey.TryGetValue(_sessionId, out ItemVendorUiState state) || state == null)
            {
                state = new ItemVendorUiState();
                UiStateByKey[_sessionId] = state;
            }

            return state;
        }

        private void EnsureRuntimeStock()
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            if (_offers == null)
                return;

            for (int i = 0; i < _offers.Count; i++)
            {
                if (_uiState.runtimeStockByOfferIndex.ContainsKey(i))
                    continue;

                ItemVendorSellOffer offer = _offers[i];
                _uiState.runtimeStockByOfferIndex[i] = offer != null ? offer.stock : 0;
            }

            List<int> keys = new List<int>(_uiState.runtimeStockByOfferIndex.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                if (key < 0 || key >= _offers.Count)
                    _uiState.runtimeStockByOfferIndex.Remove(key);
            }
        }
    }
}