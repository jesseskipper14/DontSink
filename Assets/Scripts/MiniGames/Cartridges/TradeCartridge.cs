using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

namespace MiniGames
{
    public sealed class TradeCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private enum TradeTab { Buy, Sell, All }

        private sealed class TradeUiState
        {
            public TradeTab tab = TradeTab.Buy;

            public readonly Dictionary<string, int> qtyByOfferId =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public Vector2 offerScroll;
            public Vector2 invScroll;
            public string invFilter;
            public bool invOnlyRelevant;
            public string lastUiNote;
        }

        private static readonly Dictionary<string, TradeUiState> UiStateByKey =
            new Dictionary<string, TradeUiState>(StringComparer.Ordinal);

        private readonly string _nodeId;
        private readonly int _timeBucket;
        private readonly IReadOnlyList<NodeMarketOffer> _offers;

        private readonly WorldMapPlayerState _player;
        private readonly IItemStore _itemStore;

        private readonly MapNodeState _nodeState;
        private readonly ITradeFeePreview _feePreview;

        private readonly ResourceCatalog _resourceCatalog;

        private MiniGameContext _ctx;
        private bool _requestedClose;

        private TradeUiState _uiState;

        private readonly Dictionary<string, int> _basePriceByItemId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly List<TradeLine> _tmpLines = new List<TradeLine>(32);

        private string StateKey
        {
            get
            {
                // Intentionally stable across cartridge recreation. If the time bucket changes
                // while the overlay rebuilds, including it here would reset UI state.
                return string.IsNullOrWhiteSpace(_nodeId)
                    ? "trade:unknown-node"
                    : $"trade:{_nodeId}";
            }
        }

        // Back-compat signature (WorldMap inventory).
        public TradeCartridge(
            string nodeId,
            int timeBucket,
            IReadOnlyList<NodeMarketOffer> offers,
            WorldMapPlayerState player,
            MapNodeState nodeState,
            ITradeFeePreview feePreview,
            ResourceCatalog resourceCatalog)
            : this(
                nodeId,
                timeBucket,
                offers,
                player,
                player != null ? (IItemStore)player.inventory : null,
                nodeState,
                feePreview,
                resourceCatalog)
        {
        }

        // Injected store: world-map inventory OR physical cargo item store.
        public TradeCartridge(
            string nodeId,
            int timeBucket,
            IReadOnlyList<NodeMarketOffer> offers,
            WorldMapPlayerState player,
            IItemStore itemStore,
            MapNodeState nodeState,
            ITradeFeePreview feePreview,
            ResourceCatalog resourceCatalog)
        {
            _nodeId = nodeId;
            _timeBucket = timeBucket;
            _offers = offers ?? Array.Empty<NodeMarketOffer>();

            _player = player;
            _itemStore = itemStore;

            _nodeState = nodeState;
            _feePreview = feePreview;
            _resourceCatalog = resourceCatalog;

            BuildBasePriceCache();
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();
            _uiState = GetOrCreateUiState();

            EnsureQuantityState();

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

            EnsureQuantityState();

            if (_player == null || _itemStore == null)
            {
                GUI.Label(
                    new Rect(panel.x + 14, panel.y + 14, panel.width - 28, 22),
                    "TRADE (missing player/item store)");

                return;
            }

            float pad = 14f;

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 10, panel.width - pad * 2f, 22),
                $"TRADE @ {_nodeId} (day {_timeBucket})");

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 32, panel.width - pad * 2f, 22),
                GetTradeMoneyLabel());

            float closeX = panel.xMax - 34;
            float closeY = panel.y + 8;
            if (GUI.Button(new Rect(closeX, closeY, 26, 22), "X"))
            {
                _requestedClose = true;
                _uiState.lastUiNote = "Closed";
            }

            float invW = Mathf.Clamp(panel.width * 0.28f, 260f, 420f);
            Rect invRect = new Rect(
                panel.xMax - pad - invW,
                panel.y + 60f,
                invW,
                panel.height - 110f);

            DrawInventoryPanel(invRect);

            GUI.Box(new Rect(invRect.x - 6, panel.y + 50, 2, panel.height - 70), GUIContent.none);

            float tabY = panel.y + 60f;
            float tabX = panel.x + pad;
            float tabW = 80f;
            float tabH = 22f;

            if (GUI.Button(new Rect(tabX + 0 * (tabW + 6), tabY, tabW, tabH), "BUY"))
                SetTab(TradeTab.Buy);

            if (GUI.Button(new Rect(tabX + 1 * (tabW + 6), tabY, tabW, tabH), "SELL"))
                SetTab(TradeTab.Sell);

            if (GUI.Button(new Rect(tabX + 2 * (tabW + 6), tabY, tabW, tabH), "ALL"))
                SetTab(TradeTab.All);

            float listX = panel.x + pad;
            float listY = panel.y + 90f;
            float listW = invRect.xMin - pad - listX;
            float listH = panel.height - 170f;

            Rect viewRect = new Rect(listX, listY, listW, listH);

            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, _offers.Count + 1) * 34f + 24f);

            _uiState.offerScroll = GUI.BeginScrollView(viewRect, _uiState.offerScroll, contentRect);

            float y = 6f;

            DrawHeaderRow(y, contentRect.width);
            y += 28f;

            bool any = false;

            for (int i = 0; i < _offers.Count; i++)
            {
                NodeMarketOffer offer = _offers[i];
                if (offer == null)
                    continue;

                if (offer.quantityRemaining <= 0)
                    continue;

                if (_uiState.tab == TradeTab.Buy && offer.kind != MarketOfferKind.SellToPlayer)
                    continue;

                if (_uiState.tab == TradeTab.Sell && offer.kind != MarketOfferKind.BuyFromPlayer)
                    continue;

                any = true;
                DrawOfferRow(offer, y, contentRect.width);
                y += 34f;
            }

            if (!any)
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No offers)");

            GUI.EndScrollView();

            TradePreview preview = ComputePreview();
            string signFinal = preview.finalDelta >= 0 ? "+" : "-";

            GUI.Label(
                new Rect(panel.x + pad, panel.yMax - 118, panel.width - pad * 2f, 22),
                $"Trade Balance: {preview.tradeBalance}");

            GUI.Label(
                new Rect(panel.x + pad, panel.yMax - 102, panel.width - pad * 2f, 22),
                $"Fee: {preview.fee}");

            GUI.Label(
                new Rect(panel.x + pad, panel.yMax - 86, panel.width - pad * 2f, 22),
                $"Final: {preview.finalDelta}");

            GUI.Label(
                new Rect(panel.x + pad, panel.yMax - 70, panel.width - pad * 2f, 22),
                $"Preview: Δmoney {signFinal}${Mathf.Abs(preview.finalDelta):n0} → ${preview.moneyAfter:n0}");

            if (preview.missingActiveChest)
            {
                GUI.Label(
                    new Rect(panel.x + pad, panel.yMax - 58, panel.width - pad * 2f, 22),
                    "No active money chest.");
            }
            else if (preview.insufficientMoney)
            {
                GUI.Label(
                    new Rect(panel.x + pad, panel.yMax - 58, panel.width - pad * 2f, 22),
                    "Not enough money in chest.");
            }

            Rect btnRow = new Rect(panel.x + pad, panel.yMax - 48, panel.width - pad * 2f, 34);
            float buttonWidth = 110f;

            if (GUI.Button(new Rect(btnRow.x, btnRow.y, buttonWidth, btnRow.height), "Clear"))
                ClearAll();

            if (GUI.Button(new Rect(btnRow.xMax - buttonWidth * 2f - 10f, btnRow.y, buttonWidth, btnRow.height), "Preview"))
                _uiState.lastUiNote = BuildPreviewString();

            GUI.enabled = !preview.Blocked;

            if (GUI.Button(new Rect(btnRow.xMax - buttonWidth, btnRow.y, buttonWidth, btnRow.height), "Confirm"))
                ConfirmTransaction();

            GUI.enabled = true;
        }

        private void BuildBasePriceCache()
        {
            _basePriceByItemId.Clear();

            if (_resourceCatalog == null || _resourceCatalog.Resources == null)
                return;

            for (int i = 0; i < _resourceCatalog.Resources.Count; i++)
            {
                var def = _resourceCatalog.Resources[i];
                if (def == null)
                    continue;

                if (string.IsNullOrWhiteSpace(def.itemId))
                    continue;

                _basePriceByItemId[def.itemId] = Mathf.Max(1, def.basePrice);
            }
        }

        private int GetBasePrice(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 1;

            return _basePriceByItemId.TryGetValue(itemId, out int price)
                ? Mathf.Max(1, price)
                : 1;
        }

        private TradeUiState GetOrCreateUiState()
        {
            string key = StateKey;

            if (!UiStateByKey.TryGetValue(key, out TradeUiState state) || state == null)
            {
                state = new TradeUiState();
                UiStateByKey[key] = state;
            }

            return state;
        }

        private void EnsureQuantityState()
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            HashSet<string> valid = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _offers.Count; i++)
            {
                NodeMarketOffer offer = _offers[i];
                if (offer == null || string.IsNullOrWhiteSpace(offer.offerId))
                    continue;

                valid.Add(offer.offerId);

                if (!_uiState.qtyByOfferId.ContainsKey(offer.offerId))
                    _uiState.qtyByOfferId[offer.offerId] = 0;
            }

            List<string> keys = new List<string>(_uiState.qtyByOfferId.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!valid.Contains(keys[i]))
                    _uiState.qtyByOfferId.Remove(keys[i]);
            }
        }

        private void SetTab(TradeTab tab)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.tab = tab;
        }

        private void DrawHeaderRow(float y, float width)
        {
            GUI.Label(new Rect(6, y, 60, 22), "OFFER");
            GUI.Label(new Rect(66, y, 120, 22), "RESOURCE");
            GUI.Label(new Rect(186, y, 140, 22), "PRICE");
            GUI.Label(new Rect(326, y, 140, 22), "STOCK");
        }

        private void DrawOfferRow(NodeMarketOffer offer, float y, float width)
        {
            string label = offer.kind == MarketOfferKind.SellToPlayer ? "BUY" : "SELL";

            int qty = GetQty(offer.offerId);
            int have = _itemStore.GetCount(offer.itemId);
            int basePrice = GetBasePrice(offer.itemId);

            GUI.Label(new Rect(6, y, 60, 22), label);
            GUI.Label(new Rect(66, y, 120, 22), offer.itemId);

            Color previousColor = GUI.color;
            GUI.color = GetDealColor(offer.kind, offer.unitPrice, basePrice);
            GUI.Label(new Rect(186, y, 50, 22), $"{offer.unitPrice}c");
            GUI.color = previousColor;

            GUI.Label(new Rect(236, y, 90, 22), $"({basePrice}c)");

            if (offer.kind == MarketOfferKind.SellToPlayer)
                GUI.Label(new Rect(326, y, 240, 22), $"Trader has {offer.quantityRemaining}");
            else
                GUI.Label(new Rect(326, y, 240, 22), $"Trader wants {offer.quantityRemaining} / You have {have}");

            float buttonX = width - 84f;

            if (GUI.Button(new Rect(buttonX, y, 24, 22), "-"))
            {
                Event ev = Event.current;

                if (ev != null && ev.shift && ev.control)
                    SetQty(offer.offerId, 0);
                else if (ev != null && ev.shift)
                    SetQty(offer.offerId, qty - 10);
                else if (ev != null && ev.control)
                    SetQty(offer.offerId, qty - 5);
                else
                    SetQty(offer.offerId, qty - 1);
            }

            GUI.Label(new Rect(buttonX + 28, y, 28, 22), qty.ToString());

            if (GUI.Button(new Rect(buttonX + 58, y, 24, 22), "+"))
            {
                Event ev = Event.current;
                int max = GetMaxQtyForOffer(offer);

                if (ev != null && ev.shift && ev.control)
                    SetQty(offer.offerId, max);
                else if (ev != null && ev.shift)
                    SetQty(offer.offerId, Mathf.Min(qty + 10, max));
                else if (ev != null && ev.control)
                    SetQty(offer.offerId, Mathf.Min(qty + 5, max));
                else
                    SetQty(offer.offerId, Mathf.Min(qty + 1, max));
            }
        }

        private static Color GetDealColor(MarketOfferKind kind, int currentPrice, int basePrice)
        {
            basePrice = Mathf.Max(1, basePrice);
            currentPrice = Mathf.Max(1, currentPrice);

            float ratio = currentPrice / (float)basePrice;
            float signed = kind == MarketOfferKind.SellToPlayer
                ? 1f - ratio
                : ratio - 1f;

            signed = Mathf.Clamp(signed, -0.75f, 0.75f);
            float t = (signed + 0.75f) / 1.5f;

            if (t < 0.5f)
            {
                return Color.Lerp(
                    new Color(1f, 0.35f, 0.35f, 1f),
                    new Color(1f, 0.92f, 0.35f, 1f),
                    t / 0.5f);
            }

            return Color.Lerp(
                new Color(1f, 0.92f, 0.35f, 1f),
                new Color(0.35f, 1f, 0.50f, 1f),
                (t - 0.5f) / 0.5f);
        }

        private int GetQty(string offerId)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            if (string.IsNullOrWhiteSpace(offerId))
                return 0;

            return _uiState.qtyByOfferId.TryGetValue(offerId, out int qty)
                ? Mathf.Max(0, qty)
                : 0;
        }

        private void SetQty(string offerId, int qty)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                return;

            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            EnsureQuantityState();

            qty = Mathf.Clamp(qty, 0, 999);

            NodeMarketOffer offer = FindOfferById(offerId);
            if (offer != null)
                qty = Mathf.Min(qty, GetMaxQtyForOffer(offer));

            _uiState.qtyByOfferId[offerId] = Mathf.Max(0, qty);
        }

        private void ClearAll()
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            List<string> keys = new List<string>(_uiState.qtyByOfferId.Keys);
            for (int i = 0; i < keys.Count; i++)
                _uiState.qtyByOfferId[keys[i]] = 0;

            _uiState.lastUiNote = null;
        }

        private string BuildPreviewString()
        {
            TradePreview preview = ComputePreview();

            if (preview.missingActiveChest)
                return "No active money chest.";

            if (preview.insufficientMoney)
                return $"Not enough money in chest. Need ${Mathf.Abs(preview.finalDelta):n0}, have ${GetTradeMoneyBalance():n0}.";

            string sign = preview.finalDelta >= 0 ? "+" : "-";
            return $"Preview Δmoney {sign}${Mathf.Abs(preview.finalDelta):n0} (after: ${preview.moneyAfter:n0})";
        }

        private void ConfirmTransaction()
        {
            TradePreview preview = ComputePreview();
            if (preview.Blocked)
            {
                if (_uiState == null)
                    _uiState = GetOrCreateUiState();

                if (preview.missingActiveChest)
                    _uiState.lastUiNote = "No active money chest";
                else if (preview.insufficientMoney)
                    _uiState.lastUiNote = "Not enough money in chest";
                else
                    _uiState.lastUiNote = "Trade blocked";

                return;
            }

            TradeTransactionDraft draft = new TradeTransactionDraft
            {
                nodeId = _nodeId,
                vendorId = null
            };

            foreach (NodeMarketOffer offer in _offers)
            {
                if (offer == null)
                    continue;

                int qty = GetQty(offer.offerId);
                if (qty <= 0)
                    continue;

                qty = Mathf.Min(qty, GetMaxQtyForOffer(offer));
                if (qty <= 0)
                    continue;

                draft.lines.Add(new TradeLine
                {
                    offerId = offer.offerId,
                    itemId = offer.itemId,
                    quantity = qty,
                    unitPrice = offer.unitPrice,
                    direction = offer.kind == MarketOfferKind.SellToPlayer
                        ? TradeDirection.BuyFromNode
                        : TradeDirection.SellToNode
                });
            }

            if (draft.lines.Count == 0)
            {
                if (_uiState == null)
                    _uiState = GetOrCreateUiState();

                _uiState.lastUiNote = "No lines selected";
                return;
            }

            string json = JsonUtility.ToJson(draft);

            _ctx.emitEffect?.Invoke(new MiniGameEffect
            {
                kind = MiniGameEffectKind.Transaction,
                system = "Trade",
                targetId = _nodeId,
                payloadJson = json
            });

            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.lastUiNote = "Trade submitted";
            ClearAll();
        }

        private int GetMaxQtyForOffer(NodeMarketOffer offer)
        {
            if (offer == null)
                return 0;

            int max = Mathf.Max(0, offer.quantityRemaining);

            if (offer.kind == MarketOfferKind.BuyFromPlayer)
            {
                int have = _itemStore.GetCount(offer.itemId);
                max = Mathf.Min(max, have);
            }

            return Mathf.Max(0, max);
        }

        private NodeMarketOffer FindOfferById(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                return null;

            for (int i = 0; i < _offers.Count; i++)
            {
                NodeMarketOffer offer = _offers[i];
                if (offer != null && offer.offerId == offerId)
                    return offer;
            }

            return null;
        }

        private struct TradePreview
        {
            public int tradeBalance;
            public int fee;
            public int finalDelta;
            public int moneyAfter;
            public bool missingActiveChest;
            public bool insufficientMoney;

            public bool Blocked => missingActiveChest || insufficientMoney;
        }

        private TradePreview ComputePreview()
        {
            _tmpLines.Clear();

            int tradeBalance = 0;

            foreach (NodeMarketOffer offer in _offers)
            {
                if (offer == null)
                    continue;

                int qty = GetQty(offer.offerId);
                if (qty <= 0)
                    continue;

                qty = Mathf.Min(qty, GetMaxQtyForOffer(offer));
                if (qty <= 0)
                    continue;

                int lineTotal = offer.unitPrice * qty;

                if (offer.kind == MarketOfferKind.SellToPlayer)
                    tradeBalance -= lineTotal;
                else
                    tradeBalance += lineTotal;

                _tmpLines.Add(new TradeLine
                {
                    offerId = offer.offerId,
                    itemId = offer.itemId,
                    quantity = qty,
                    unitPrice = offer.unitPrice,
                    direction = offer.kind == MarketOfferKind.SellToPlayer
                        ? TradeDirection.BuyFromNode
                        : TradeDirection.SellToNode
                });
            }

            int fee = 0;

            if (_feePreview != null && _nodeState != null && _tmpLines.Count > 0)
                fee = Mathf.Max(0, _feePreview.ComputeFeeTotal(_nodeState, _tmpLines, _timeBucket));

            int finalDelta = tradeBalance - fee;

            bool hasSelectedLines = _tmpLines.Count > 0;
            bool hasActiveChest = HasActiveMoneyChest();

            int before = hasActiveChest ? GetTradeMoneyBalance() : 0;
            int after = before + finalDelta;

            bool missingActiveChest = hasSelectedLines && !hasActiveChest;
            bool insufficientMoney =
                !missingActiveChest &&
                finalDelta < 0 &&
                !MoneyService.CanSpend(-finalDelta);

            return new TradePreview
            {
                tradeBalance = tradeBalance,
                fee = fee,
                finalDelta = finalDelta,
                moneyAfter = Mathf.Max(0, after),
                missingActiveChest = missingActiveChest,
                insufficientMoney = insufficientMoney
            };
        }

        private void DrawInventoryPanel(Rect rect)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            GUI.Box(rect, GUIContent.none);

            float x = rect.x + 10;
            float y = rect.y + 8;
            float width = rect.width - 20;

            GUI.Label(new Rect(x, y, width, 20), "PLAYER INVENTORY");
            y += 22;

            GUI.Label(new Rect(x, y, 44, 20), "Filter");
            _uiState.invFilter = GUI.TextField(
                new Rect(x + 48, y, width - 48 - 110, 20),
                _uiState.invFilter ?? "");

            _uiState.invOnlyRelevant = GUI.Toggle(
                new Rect(x + width - 110, y, 110, 20),
                _uiState.invOnlyRelevant,
                "Relevant");

            y += 26;

            List<(string id, int count)> entries = new List<(string id, int count)>();
            CollectInventoryEntries(entries);

            string filter = (_uiState.invFilter ?? "").Trim();
            if (!string.IsNullOrEmpty(filter))
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    if (entries[i].id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        entries.RemoveAt(i);
                }
            }

            if (_uiState.invOnlyRelevant)
            {
                HashSet<string> relevant = BuildRelevantItemSet();

                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    if (!relevant.Contains(entries[i].id))
                        entries.RemoveAt(i);
                }
            }

            entries.Sort((a, b) =>
            {
                int countCompare = b.count.CompareTo(a.count);
                return countCompare != 0
                    ? countCompare
                    : string.Compare(a.id, b.id, StringComparison.OrdinalIgnoreCase);
            });

            float listY = y;
            float listH = rect.yMax - 10 - listY;

            Rect view = new Rect(x, listY, width, listH);
            Rect content = new Rect(0, 0, width - 16, Mathf.Max(1, entries.Count) * 22 + 6);

            _uiState.invScroll = GUI.BeginScrollView(view, _uiState.invScroll, content);

            float yy = 4;

            if (entries.Count == 0)
            {
                GUI.Label(new Rect(4, yy, content.width - 8, 20), "(empty)");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    (string id, int count) entry = entries[i];

                    GUI.Label(new Rect(4, yy, content.width - 70, 20), entry.id);
                    GUI.Label(new Rect(content.width - 60, yy, 56, 20), entry.count.ToString());

                    yy += 22;
                }
            }

            GUI.EndScrollView();
        }

        private void CollectInventoryEntries(List<(string id, int count)> outList)
        {
            outList.Clear();

            foreach (KeyValuePair<string, int> kvp in _itemStore.Enumerate())
            {
                if (kvp.Value <= 0)
                    continue;

                outList.Add((kvp.Key, kvp.Value));
            }
        }

        private HashSet<string> BuildRelevantItemSet()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _offers.Count; i++)
            {
                NodeMarketOffer offer = _offers[i];
                if (offer == null)
                    continue;

                set.Add(offer.itemId);
            }

            return set;
        }

        private bool HasActiveMoneyChest()
        {
            MoneyChestTreasuryService treasury = MoneyChestTreasuryService.Instance;
            return treasury != null && treasury.HasActiveChest;
        }

        private int GetTradeMoneyBalance()
        {
            return Mathf.Max(0, MoneyService.Balance);
        }

        private string GetTradeMoneyLabel()
        {
            if (!HasActiveMoneyChest())
                return "Money Chest: NONE";

            return $"Money Chest: ${GetTradeMoneyBalance():n0}";
        }
    }
}