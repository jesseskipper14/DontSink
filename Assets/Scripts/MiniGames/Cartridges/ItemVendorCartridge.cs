using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    public sealed class ItemVendorCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private enum MainTab
        {
            Buy,
            Sell
        }

        private enum SellTab
        {
            Player,
            Boat
        }

        private sealed class ItemVendorUiState
        {
            public MainTab mainTab = MainTab.Buy;
            public SellTab sellTab = SellTab.Player;

            public Vector2 offerScroll;
            public Vector2 playerSellScroll;
            public Vector2 boatSellScroll;
            public Vector2 noteScroll;

            public string lastUiNote;
            public string pendingContainerConfirmKey;

            public bool hideUnsellableSellItems;

            public readonly Dictionary<int, int> runtimeStockByOfferIndex = new();
        }

        private static readonly Dictionary<string, ItemVendorUiState> UiStateByKey =
            new(StringComparer.Ordinal);

        private readonly string _sessionId;
        private readonly ItemVendorServiceDefinition _vendor;
        private readonly IReadOnlyList<ItemVendorSellOffer> _offers;

        private readonly IReadOnlyList<ItemVendorSellSource> _playerSellSources;
        private readonly IReadOnlyList<ItemVendorSellSource> _boatSellSources;

        private readonly ItemDefinitionCatalog _itemCatalog;
        private readonly ItemVendorPurchaseService _purchaseService;
        private readonly ItemVendorSellService _sellService;
        private readonly AgentServiceContext _agentContext;

        private MiniGameContext _ctx;
        private bool _requestedClose;
        private ItemVendorUiState _uiState;

        public ItemVendorCartridge(
            string sessionId,
            ItemVendorServiceDefinition vendor,
            IReadOnlyList<ItemVendorSellOffer> offers,
            IReadOnlyList<ItemVendorSellSource> playerSellSources,
            IReadOnlyList<ItemVendorSellSource> boatSellSources,
            ItemDefinitionCatalog itemCatalog,
            ItemVendorPurchaseService purchaseService,
            ItemVendorSellService sellService,
            AgentServiceContext agentContext)
        {
            _sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? "item_vendor:unknown"
                : sessionId;

            _vendor = vendor;
            _offers = offers ?? Array.Empty<ItemVendorSellOffer>();

            _playerSellSources = playerSellSources ?? Array.Empty<ItemVendorSellSource>();
            _boatSellSources = boatSellSources ?? Array.Empty<ItemVendorSellSource>();

            _itemCatalog = itemCatalog;
            _purchaseService = purchaseService;
            _sellService = sellService;
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

            DrawMainTabs(panel, pad);

            if (_uiState.mainTab == MainTab.Buy)
                DrawBuyTab(panel, pad);
            else
                DrawSellTab(panel, pad);

            DrawFooter(panel, pad);
        }

        private const float FooterHeight = 98f;

        private float GetListBottomY(Rect panel)
        {
            return panel.yMax - FooterHeight;
        }

        private void DrawFooter(Rect panel, float pad)
        {
            float footerTop = panel.yMax - FooterHeight + 8f;
            float footerBottom = panel.yMax - 12f;

            Rect closeRect = new Rect(
                panel.x + pad,
                footerBottom - 26f,
                110f,
                26f);

            Rect noteRect = new Rect(
                closeRect.xMax + 12f,
                footerTop,
                panel.width - pad * 2f - closeRect.width - 12f,
                footerBottom - footerTop);

            if (!string.IsNullOrWhiteSpace(_uiState.lastUiNote))
                DrawScrollableNote(noteRect, _uiState.lastUiNote);

            if (GUI.Button(closeRect, "Close"))
            {
                _requestedClose = true;
                _uiState.lastUiNote = "Closed";
            }
        }

        private void DrawScrollableNote(Rect rect, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            GUI.Box(rect, GUIContent.none);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };

            float innerPad = 6f;
            float viewWidth = Mathf.Max(1f, rect.width - 18f);
            float textHeight = Mathf.Max(
                rect.height - innerPad * 2f,
                style.CalcHeight(new GUIContent(text), viewWidth - innerPad * 2f));

            Rect viewRect = new Rect(
                0f,
                0f,
                viewWidth,
                textHeight + innerPad * 2f);

            _uiState.noteScroll = GUI.BeginScrollView(
                rect,
                _uiState.noteScroll,
                viewRect);

            GUI.Label(
                new Rect(innerPad, innerPad, viewWidth - innerPad * 2f, textHeight),
                text,
                style);

            GUI.EndScrollView();
        }

        private void DrawMainTabs(Rect panel, float pad)
        {
            float y = panel.y + 58f;
            float x = panel.x + pad;

            if (GUI.Button(new Rect(x, y, 80, 24), "BUY"))
            {
                _uiState.mainTab = MainTab.Buy;
                _uiState.pendingContainerConfirmKey = null;
            }

            if (GUI.Button(new Rect(x + 88, y, 80, 24), "SELL"))
            {
                _uiState.mainTab = MainTab.Sell;
                _uiState.pendingContainerConfirmKey = null;
            }
        }

        private void DrawBuyTab(Rect panel, float pad)
        {
            float listX = panel.x + pad;
            float listY = panel.y + 92f;
            float listW = panel.width - pad * 2f;
            float listH = Mathf.Max(60f, GetListBottomY(panel) - listY);

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

            DrawBuyHeaderRow(y, contentRect.width);
            y += 28f;

            bool any = false;

            if (_offers != null)
            {
                for (int i = 0; i < _offers.Count; i++)
                {
                    ItemVendorSellOffer offer = _offers[i];
                    if (offer == null)
                        continue;

                    DrawBuyOfferRow(i, offer, y, contentRect.width);
                    y += 38f;
                    any = true;
                }
            }

            if (!any)
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No items for sale)");

            GUI.EndScrollView();
        }

        private void DrawSellTab(Rect panel, float pad)
        {
            float tabY = panel.y + 92f;
            float tabX = panel.x + pad;

            if (GUI.Button(new Rect(tabX, tabY, 140, 24), "PLAYER INVENTORY"))
            {
                _uiState.sellTab = SellTab.Player;
                _uiState.pendingContainerConfirmKey = null;
            }

            if (GUI.Button(new Rect(tabX + 148, tabY, 140, 24), "BOAT INVENTORY"))
            {
                _uiState.sellTab = SellTab.Boat;
                _uiState.pendingContainerConfirmKey = null;
            }

            _uiState.hideUnsellableSellItems = GUI.Toggle(
                new Rect(tabX + 304, tabY + 2, 180, 22),
                _uiState.hideUnsellableSellItems,
                "Hide unsellable items");

            IReadOnlyList<ItemVendorSellSource> sources =
                _uiState.sellTab == SellTab.Boat
                    ? _boatSellSources
                    : _playerSellSources;

            DrawSellSourceList(
                panel,
                pad,
                sources,
                _uiState.sellTab == SellTab.Boat ? ItemVendorSellArea.Boat : ItemVendorSellArea.Player,
                ref _uiState.sellTab == SellTab.Boat
                    ? ref _uiState.boatSellScroll
                    : ref _uiState.playerSellScroll);
        }

        private void DrawSellSourceList(
    Rect panel,
    float pad,
    IReadOnlyList<ItemVendorSellSource> sources,
    ItemVendorSellArea area,
    ref Vector2 scroll)
        {
            float listX = panel.x + pad;
            float listY = panel.y + 126f;
            float listW = panel.width - pad * 2f;
            float listH = Mathf.Max(60f, GetListBottomY(panel) - listY);

            int visibleCount = CountVisibleSellSources(sources);

            Rect viewRect = new Rect(listX, listY, listW, listH);
            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, visibleCount + 1) * 46f + 24f);

            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);

            float y = 6f;

            DrawSellHeaderRow(y, contentRect.width);
            y += 28f;

            bool any = false;

            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    ItemVendorSellSource source = sources[i];
                    if (source == null)
                        continue;

                    if (ShouldHideSellSource(source))
                        continue;

                    DrawSellSourceRow(area, i, source, y, contentRect.width);
                    y += 46f;
                    any = true;
                }
            }

            if (!any)
            {
                string emptyLabel = _uiState.hideUnsellableSellItems
                    ? "(No sellable items)"
                    : "(Nothing to sell)";

                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), emptyLabel);
            }

            GUI.EndScrollView();
        }

        private int CountVisibleSellSources(IReadOnlyList<ItemVendorSellSource> sources)
        {
            if (sources == null)
                return 0;

            int count = 0;

            for (int i = 0; i < sources.Count; i++)
            {
                ItemVendorSellSource source = sources[i];
                if (source == null)
                    continue;

                if (ShouldHideSellSource(source))
                    continue;

                count++;
            }

            return count;
        }

        private bool ShouldHideSellSource(ItemVendorSellSource source)
        {
            if (!_uiState.hideUnsellableSellItems)
                return false;

            return !IsSellSourceCurrentlySellable(source);
        }

        private bool IsSellSourceCurrentlySellable(ItemVendorSellSource source)
        {
            if (source == null || _sellService == null)
                return false;

            ItemInstance item = source.Item;
            if (item == null || item.Definition == null)
                return false;

            int quantity = Mathf.Max(1, source.MaxQuantity);

            ItemVendorSellPreview preview =
                _sellService.BuildPreview(_vendor, source, quantity);

            return preview != null && preview.canSell;
        }

        private void DrawBuyHeaderRow(float y, float width)
        {
            GUI.Label(new Rect(6, y, 180, 22), "ITEM");
            GUI.Label(new Rect(190, y, 100, 22), "PRICE");
            GUI.Label(new Rect(300, y, 100, 22), "STOCK");
        }

        private void DrawBuyOfferRow(int index, ItemVendorSellOffer offer, float y, float width)
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

        private void DrawSellHeaderRow(float y, float width)
        {
            GUI.Label(new Rect(6, y, 280, 22), "SOURCE");
            GUI.Label(new Rect(300, y, 180, 22), "ITEM");
            GUI.Label(new Rect(484, y, 50, 22), "QTY");
            GUI.Label(new Rect(540, y, 100, 22), "VALUE");

            float actionX = GetSellActionX(width);
            GUI.Label(new Rect(actionX, y, 90, 22), "ACTION");
        }

        private void DrawSellSourceRow(
    ItemVendorSellArea area,
    int index,
    ItemVendorSellSource source,
    float y,
    float width)
        {
            ItemInstance item = source.Item;

            if (item == null || item.Definition == null)
            {
                GUI.Label(new Rect(6, y, width - 12, 22), "(missing item)");
                return;
            }

            float actionX = GetSellActionX(width);
            float rowWidth = Mathf.Min(width - 6f, actionX + 92f);

            DrawSellRowBackground(index, y, rowWidth);

            int quantity = Mathf.Max(1, source.MaxQuantity);

            ItemVendorSellPreview preview = _sellService != null
                ? _sellService.BuildPreview(_vendor, source, quantity)
                : null;

            string itemLabel = ItemVendorSellValueUtility.GetItemLabel(item);

            DrawSellSourceTreeLabel(source, y);

            GUI.Label(new Rect(300, y, 180, 22), itemLabel);
            GUI.Label(new Rect(484, y, 50, 22), quantity.ToString());

            int value = preview != null ? preview.totalValue : 0;
            GUI.Label(new Rect(540, y, 100, 22), $"${value:n0}");

            bool canSell = preview != null && preview.canSell;
            string reason = preview != null ? preview.blockReason : "Missing sell service.";

            bool needsConfirm = preview != null &&
                                preview.RequiresContainerConfirmation;

            bool confirmed =
                needsConfirm &&
                _uiState.pendingContainerConfirmKey == source.SourceKey;

            string buttonLabel = needsConfirm && !confirmed
                ? "Review"
                : needsConfirm
                    ? "Confirm"
                    : "Sell";

            float noteX = 640f;
            float noteW = Mathf.Max(80f, actionX - noteX - 10f);

            if (!canSell && !string.IsNullOrWhiteSpace(reason))
            {
                GUI.Label(new Rect(noteX, y, noteW, 22), reason);
            }
            else if (needsConfirm)
            {
                GUI.Label(new Rect(noteX, y, noteW, 22), "Contains items");
            }

            GUI.enabled = canSell;

            if (GUI.Button(new Rect(actionX, y - 1f, 86f, 24f), buttonLabel))
            {
                if (needsConfirm && !confirmed)
                {
                    _uiState.pendingContainerConfirmKey = source.SourceKey;
                    _uiState.lastUiNote = preview.BuildContainedSummary();
                }
                else
                {
                    SubmitSell(area, index, source, quantity);
                }
            }

            GUI.enabled = true;
        }

        private static float GetSellActionX(float width)
        {
            // Keep the button near the row data instead of exiling it to the far right
            // like it failed a citizenship test.
            float preferred = 760f;
            float max = width - 92f;

            if (max <= 0f)
                return 0f;

            return Mathf.Min(preferred, max);
        }

        private static void DrawSellRowBackground(int index, float y, float width)
        {
            Color old = GUI.color;

            GUI.color = index % 2 == 0
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(1f, 1f, 1f, 0.02f);

            GUI.DrawTexture(
                new Rect(0f, y - 5f, Mathf.Max(1f, width), 34f),
                Texture2D.whiteTexture);

            GUI.color = old;
        }

        private static void DrawSellSourceTreeLabel(ItemVendorSellSource source, float y)
        {
            if (source == null)
                return;

            int depth = Mathf.Clamp(source.Depth, 0, 8);

            if (depth <= 0)
            {
                GUI.Label(new Rect(6, y, 280, 22), source.SourceLabel);
                return;
            }

            float indent = depth * 26f;

            Color old = GUI.color;

            GUI.color = new Color(1f, 1f, 1f, 0.35f);

            for (int d = 1; d <= depth; d++)
            {
                float markerX = 8f + (d - 1) * 26f;
                string marker = d == depth ? "↳" : "│";

                GUI.Label(new Rect(markerX, y, 22, 22), marker);
            }

            GUI.color = old;

            string shortLabel = GetNestedSellSourceLabel(source.SourceLabel, depth);

            GUI.Label(
                new Rect(6f + indent, y, Mathf.Max(60f, 280f - indent), 22),
                shortLabel);
        }

        private static string GetNestedSellSourceLabel(string sourceLabel, int depth)
        {
            if (string.IsNullOrWhiteSpace(sourceLabel))
                return "Item";

            string[] parts = sourceLabel.Split(
                new[] { " / " },
                StringSplitOptions.None);

            if (parts == null || parts.Length == 0)
                return sourceLabel;

            // For shallow children, show just "Slot 1".
            // For deeper nesting, show "Slot 5 / Slot 1" so it does not look like
            // every cursed sub-item came from the same magical slot.
            int take = Mathf.Clamp(depth, 1, 2);
            int start = Mathf.Max(0, parts.Length - take);
            int count = parts.Length - start;

            return string.Join(" / ", parts, start, count);
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

        public void NotifySellApplied(bool success, string message)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.pendingContainerConfirmKey = null;
            _uiState.lastUiNote = message;
        }

        public void NotifySellSourcesChanged()
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.pendingContainerConfirmKey = null;
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

        private void SubmitSell(
            ItemVendorSellArea area,
            int sourceIndex,
            ItemVendorSellSource source,
            int quantity)
        {
            if (_ctx == null || source == null)
                return;

            ItemInstance item = source.Item;
            if (item == null || item.Definition == null)
                return;

            ItemVendorSellDraft draft = new ItemVendorSellDraft
            {
                version = 1,
                vendorId = _vendor != null ? _vendor.ServiceId : null,
                area = area,
                sourceIndex = sourceIndex,
                sourceKey = source.SourceKey,
                expectedInstanceId = item.InstanceId,
                quantity = Mathf.Max(1, quantity)
            };

            string json = JsonUtility.ToJson(draft);

            _ctx.emitEffect?.Invoke(new MiniGameEffect
            {
                kind = MiniGameEffectKind.Transaction,
                system = "ItemVendorSell",
                targetId = _sessionId,
                payloadJson = json
            });

            _uiState.lastUiNote = "Sell submitted.";
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