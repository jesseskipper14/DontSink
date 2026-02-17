using System;
using System.Collections.Generic;
using UnityEngine;
using WorldMap.Player.Trade;

namespace MiniGames
{
    /// <summary>
    /// Trade UI cartridge:
    /// - Renders offers + player inventory (read-only)
    /// - Builds a TradeTransactionDraft
    /// - Emits MiniGameEffect(Transaction, system="Trade", payloadJson=draft)
    /// - Stays open after confirm (supports multiple transactions per visit)
    /// </summary>
    public sealed class TradeCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private MiniGameContext _ctx;

        private readonly string _nodeId;
        private readonly int _timeBucket;
        private readonly IReadOnlyList<NodeMarketOffer> _offers;

        private enum TradeTab { Buy, Sell, All }
        private TradeTab _tab = TradeTab.Buy;

        // Read-only display source. Mutation happens via TradeEffectApplier/TradeService.
        private readonly WorldMapPlayerState _player;

        // Fee preview (keep)
        private readonly MapNodeState _nodeState;
        private readonly ITradeFeePreview _feePreview;

        // NEW: base price lookup for "(base)" and deal coloring
        private readonly ResourceCatalog _resourceCatalog;
        private readonly Dictionary<string, int> _basePriceByItemId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // UI state
        private readonly Dictionary<string, int> _qtyByOfferId = new Dictionary<string, int>();
        private Vector2 _offerScroll;
        private Vector2 _invScroll;
        private string _invFilter = "";
        private bool _invOnlyRelevant = false;
        private bool _requestedClose;
        private string _lastUiNote;

        public TradeCartridge(
            string nodeId,
            int timeBucket,
            IReadOnlyList<NodeMarketOffer> offers,
            WorldMapPlayerState player,
            MapNodeState nodeState,
            ITradeFeePreview feePreview,
            ResourceCatalog resourceCatalog)
        {
            _nodeId = nodeId;
            _timeBucket = timeBucket;
            _offers = offers ?? Array.Empty<NodeMarketOffer>();
            _player = player;

            _nodeState = nodeState;
            _feePreview = feePreview;

            _resourceCatalog = resourceCatalog;
            BuildBasePriceLookup();
        }

        private void BuildBasePriceLookup()
        {
            _basePriceByItemId.Clear();

            if (_resourceCatalog == null || _resourceCatalog.Resources == null) return;

            for (int i = 0; i < _resourceCatalog.Resources.Count; i++)
            {
                var def = _resourceCatalog.Resources[i];
                if (def == null) continue;
                if (string.IsNullOrWhiteSpace(def.itemId)) continue;

                _basePriceByItemId[def.itemId] = Mathf.Max(1, def.basePrice);
            }
        }

        private int GetBasePrice(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return 1;
            return _basePriceByItemId.TryGetValue(itemId, out var p) ? Mathf.Max(1, p) : 1;
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();

            _qtyByOfferId.Clear();
            for (int i = 0; i < _offers.Count; i++)
            {
                var o = _offers[i];
                if (o == null || string.IsNullOrWhiteSpace(o.offerId)) continue;
                _qtyByOfferId[o.offerId] = 0;
            }

            _requestedClose = false;
            _lastUiNote = null;
        }

        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (_requestedClose)
            {
                return new MiniGameResult
                {
                    outcome = MiniGameOutcome.Cancelled,
                    quality01 = 1f,
                    note = _lastUiNote ?? "Closed",
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

        // ===== Rendering =====

        public void DrawOverlayGUI(Rect panel)
        {
            if (_player == null || _player.inventory == null)
            {
                GUI.Label(new Rect(panel.x + 14, panel.y + 14, panel.width - 28, 22),
                    "TRADE (missing player/inventory)");
                return;
            }

            float pad = 14f;

            // Header
            GUI.Label(new Rect(panel.x + pad, panel.y + 10, panel.width - pad * 2f, 22),
                $"TRADE @ {_nodeId} (day {_timeBucket})");

            GUI.Label(new Rect(panel.x + pad, panel.y + 32, panel.width - pad * 2f, 22),
                $"Credits: {_player.credits}");

            // Close button (top-right)
            float bx = panel.xMax - 34;
            float by = panel.y + 8;
            if (GUI.Button(new Rect(bx, by, 26, 22), "X"))
            {
                _requestedClose = true;
                _lastUiNote = "Closed";
            }

            // Inventory on FAR RIGHT
            float invW = Mathf.Clamp(panel.width * 0.28f, 260f, 420f);
            var invRect = new Rect(panel.xMax - pad - invW, panel.y + 60f, invW, panel.height - 110f);
            DrawInventoryPanel(invRect);

            // Divider line
            GUI.Box(new Rect(invRect.x - 6, panel.y + 50, 2, panel.height - 70), GUIContent.none);

            // Tabs directly above offers list (left side)
            float tabY = panel.y + 60f;
            float tabX = panel.x + pad;
            float tabW = 80f;
            float tabH = 22f;

            if (GUI.Button(new Rect(tabX + 0 * (tabW + 6), tabY, tabW, tabH), "BUY"))
                _tab = TradeTab.Buy;

            if (GUI.Button(new Rect(tabX + 1 * (tabW + 6), tabY, tabW, tabH), "SELL"))
                _tab = TradeTab.Sell;

            if (GUI.Button(new Rect(tabX + 2 * (tabW + 6), tabY, tabW, tabH), "ALL"))
                _tab = TradeTab.All;

            // Offers list: left block up to inventory
            float listX = panel.x + pad;
            float listY = panel.y + 90f;
            float listW = invRect.xMin - pad - listX;
            float listH = panel.height - 170f; // a bit more room for preview lines

            var viewRect = new Rect(listX, listY, listW, listH);

            // Extra height for header row
            var contentRect = new Rect(
                0, 0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, _offers.Count + 1) * 34f + 24f);

            _offerScroll = GUI.BeginScrollView(viewRect, _offerScroll, contentRect);

            float y = 6f;

            DrawHeaderRow(y, contentRect.width);
            y += 28f;

            bool any = false;

            for (int i = 0; i < _offers.Count; i++)
            {
                var o = _offers[i];
                if (o == null) continue;
                if (o.quantityRemaining <= 0) continue;

                if (_tab == TradeTab.Buy && o.kind != MarketOfferKind.SellToPlayer) continue;
                if (_tab == TradeTab.Sell && o.kind != MarketOfferKind.BuyFromPlayer) continue;

                any = true;
                DrawOfferRow(o, y, contentRect.width);
                y += 34f;
            }

            if (!any)
            {
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No offers)");
            }

            GUI.EndScrollView();

            // Preview line (fee-aware)
            var preview = ComputePreview();
            string signFinal = preview.finalDelta >= 0 ? "+" : "-";

            GUI.Label(new Rect(panel.x + pad, panel.yMax - 118, panel.width - pad * 2f, 22),
                $"Trade Balance: {preview.tradeBalance}");

            GUI.Label(new Rect(panel.x + pad, panel.yMax - 102, panel.width - pad * 2f, 22),
                $"Fee: {preview.fee}");

            GUI.Label(new Rect(panel.x + pad, panel.yMax - 86, panel.width - pad * 2f, 22),
                $"Final: {preview.finalDelta}");

            GUI.Label(new Rect(panel.x + pad, panel.yMax - 70, panel.width - pad * 2f, 22),
                $"Preview: Δcredits {signFinal}{Mathf.Abs(preview.finalDelta)} → {preview.creditsAfter}");

            if (preview.insufficientCredits)
            {
                GUI.Label(new Rect(panel.x + pad, panel.yMax - 58, panel.width - pad * 2f, 22),
                    "Not enough credits.");
            }

            // Footer buttons
            var btnRow = new Rect(panel.x + pad, panel.yMax - 48, panel.width - pad * 2f, 34);
            float bw = 110f;

            if (GUI.Button(new Rect(btnRow.x, btnRow.y, bw, btnRow.height), "Clear"))
                ClearAll();

            if (GUI.Button(new Rect(btnRow.xMax - bw * 2f - 10f, btnRow.y, bw, btnRow.height), "Preview"))
                _lastUiNote = BuildPreviewString();

            GUI.enabled = !preview.insufficientCredits;

            if (GUI.Button(new Rect(btnRow.xMax - bw, btnRow.y, bw, btnRow.height), "Confirm"))
                ConfirmTransaction();

            GUI.enabled = true;
        }

        private void DrawHeaderRow(float y, float w)
        {
            // Keep aligned with DrawOfferRow columns.
            GUI.Label(new Rect(6, y, 60, 22), "OFFER");
            GUI.Label(new Rect(66, y, 120, 22), "RESOURCE");
            GUI.Label(new Rect(186, y, 140, 22), "PRICE");
            GUI.Label(new Rect(326, y, 140, 22), "STOCK");
        }

        private void DrawOfferRow(NodeMarketOffer o, float y, float w)
        {
            string label = o.kind == MarketOfferKind.SellToPlayer ? "BUY" : "SELL";
            int qty = _qtyByOfferId.TryGetValue(o.offerId, out var q) ? q : 0;
            int have = _player.inventory.GetCount(o.itemId);

            int basePrice = GetBasePrice(o.itemId);

            GUI.Label(new Rect(6, y, 60, 22), label);
            GUI.Label(new Rect(66, y, 120, 22), o.itemId);

            // PRICE: "curc (basec)" with color on the current price only
            var prevColor = GUI.color;
            GUI.color = GetDealColor(o.kind, o.unitPrice, basePrice);
            GUI.Label(new Rect(186, y, 50, 22), $"{o.unitPrice}c");
            GUI.color = prevColor;
            GUI.Label(new Rect(236, y, 90, 22), $"({basePrice}c)");

            // Availability column
            if (o.kind == MarketOfferKind.SellToPlayer)
                GUI.Label(new Rect(326, y, 240, 22), $"Trader has {o.quantityRemaining}");
            else
                GUI.Label(new Rect(326, y, 240, 22), $"Trader wants {o.quantityRemaining} / You have {have}");

            // Qty controls INSIDE the row (near the right edge of the offers list)
            float bx = w - 84f;

            if (GUI.Button(new Rect(bx, y, 24, 22), "-"))
            {
                var ev = Event.current;
                if (ev != null && ev.shift && ev.control)
                    SetQty(o.offerId, 0);
                else if (ev != null && ev.shift)
                    SetQty(o.offerId, qty - 10);
                else if (ev != null && ev.control)
                    SetQty(o.offerId, qty - 5);
                else
                    SetQty(o.offerId, qty - 1);
            }

            GUI.Label(new Rect(bx + 28, y, 28, 22), qty.ToString());

            if (GUI.Button(new Rect(bx + 58, y, 24, 22), "+"))
            {
                var ev = Event.current;
                int max = GetMaxQtyForOffer(o);

                if (ev != null && ev.shift && ev.control)
                    SetQty(o.offerId, max);
                else if (ev != null && ev.shift)
                    SetQty(o.offerId, Mathf.Min(qty + 10, max));
                else if (ev != null && ev.control)
                    SetQty(o.offerId, Mathf.Min(qty + 5, max));
                else
                    SetQty(o.offerId, Mathf.Min(qty + 1, max));
            }
        }

        private static Color GetDealColor(MarketOfferKind kind, int currentPrice, int basePrice)
        {
            basePrice = Mathf.Max(1, basePrice);
            currentPrice = Mathf.Max(1, currentPrice);

            float ratio = currentPrice / (float)basePrice;

            // BUYING: cheaper better. SELLING: higher better.
            float signed = (kind == MarketOfferKind.SellToPlayer) ? (1f - ratio) : (ratio - 1f);

            // Clamp so wild prices don't go nuclear.
            signed = Mathf.Clamp(signed, -0.75f, 0.75f);

            // Map to 0..1: 0=red, 0.5=yellow, 1=green
            float t = (signed + 0.75f) / 1.5f;

            // red -> yellow -> green
            if (t < 0.5f)
                return Color.Lerp(new Color(1f, 0.35f, 0.35f, 1f), new Color(1f, 0.92f, 0.35f, 1f), t / 0.5f);

            return Color.Lerp(new Color(1f, 0.92f, 0.35f, 1f), new Color(0.35f, 1f, 0.50f, 1f), (t - 0.5f) / 0.5f);
        }

        private void SetQty(string offerId, int qty)
        {
            qty = Mathf.Clamp(qty, 0, 999);

            var offer = FindOfferById(offerId);
            if (offer != null)
                qty = Mathf.Min(qty, GetMaxQtyForOffer(offer));

            _qtyByOfferId[offerId] = qty;
        }

        private void ClearAll()
        {
            var keys = new List<string>(_qtyByOfferId.Keys);
            for (int i = 0; i < keys.Count; i++)
                _qtyByOfferId[keys[i]] = 0;

            _lastUiNote = null;
        }

        private string BuildPreviewString()
        {
            // This is still the old raw preview string (no fee). It's just a debug button anyway.
            int creditsDelta = 0;

            foreach (var o in _offers)
            {
                if (o == null) continue;
                if (!_qtyByOfferId.TryGetValue(o.offerId, out var qty) || qty <= 0) continue;

                qty = Mathf.Min(qty, GetMaxQtyForOffer(o));
                if (qty <= 0) continue;

                int total = o.unitPrice * qty;
                if (o.kind == MarketOfferKind.SellToPlayer) creditsDelta -= total;
                else creditsDelta += total;
            }

            string sign = creditsDelta >= 0 ? "+" : "-";
            return $"Preview Δcredits {sign}{Mathf.Abs(creditsDelta)} (after: {_player.credits + creditsDelta})";
        }

        private void ConfirmTransaction()
        {
            var draft = new TradeTransactionDraft
            {
                nodeId = _nodeId,
                vendorId = null
            };

            foreach (var o in _offers)
            {
                if (o == null) continue;
                if (!_qtyByOfferId.TryGetValue(o.offerId, out var qty) || qty <= 0) continue;

                qty = Mathf.Min(qty, GetMaxQtyForOffer(o));
                if (qty <= 0) continue;

                draft.lines.Add(new TradeLine
                {
                    offerId = o.offerId,
                    itemId = o.itemId,
                    quantity = qty,
                    unitPrice = o.unitPrice,
                    direction = o.kind == MarketOfferKind.SellToPlayer
                        ? TradeDirection.BuyFromNode
                        : TradeDirection.SellToNode
                });
            }

            if (draft.lines.Count == 0)
            {
                _lastUiNote = "No lines selected";
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

            _lastUiNote = "Trade submitted";
            ClearAll(); // stay open, allow repeat transactions
        }

        private int GetMaxQtyForOffer(NodeMarketOffer o)
        {
            if (o == null) return 0;

            int max = Mathf.Max(0, o.quantityRemaining);

            // BUY_FROM_PLAYER offer means player is selling to node; clamp to have
            if (o.kind == MarketOfferKind.BuyFromPlayer)
            {
                int have = _player.inventory.GetCount(o.itemId);
                max = Mathf.Min(max, have);
            }

            return Mathf.Max(0, max);
        }

        private NodeMarketOffer FindOfferById(string offerId)
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                var o = _offers[i];
                if (o != null && o.offerId == offerId) return o;
            }
            return null;
        }

        private readonly List<TradeLine> _tmpLines = new List<TradeLine>(32);

        private struct TradePreview
        {
            public int tradeBalance;     // before fee
            public int fee;              // always >= 0
            public int finalDelta;       // tradeBalance - fee
            public int creditsAfter;
            public bool insufficientCredits;
        }

        private TradePreview ComputePreview()
        {
            _tmpLines.Clear();

            int tradeBalance = 0;

            foreach (var o in _offers)
            {
                if (o == null) continue;
                if (!_qtyByOfferId.TryGetValue(o.offerId, out var qty) || qty <= 0) continue;

                qty = Mathf.Min(qty, GetMaxQtyForOffer(o));
                if (qty <= 0) continue;

                int lineTotal = o.unitPrice * qty;

                if (o.kind == MarketOfferKind.SellToPlayer) tradeBalance -= lineTotal;
                else tradeBalance += lineTotal;

                _tmpLines.Add(new TradeLine
                {
                    offerId = o.offerId,
                    itemId = o.itemId,
                    quantity = qty,
                    unitPrice = o.unitPrice,
                    direction = o.kind == MarketOfferKind.SellToPlayer
                        ? TradeDirection.BuyFromNode
                        : TradeDirection.SellToNode
                });
            }

            int fee = 0;
            if (_feePreview != null && _nodeState != null && _tmpLines.Count > 0)
                fee = Mathf.Max(0, _feePreview.ComputeFeeTotal(_nodeState, _tmpLines, _timeBucket));

            int finalDelta = tradeBalance - fee;
            int after = _player.credits + finalDelta;

            return new TradePreview
            {
                tradeBalance = tradeBalance,
                fee = fee,
                finalDelta = finalDelta,
                creditsAfter = after,
                insufficientCredits = after < 0
            };
        }

        private void DrawInventoryPanel(Rect r)
        {
            GUI.Box(r, GUIContent.none);

            float x = r.x + 10;
            float y = r.y + 8;
            float w = r.width - 20;

            GUI.Label(new Rect(x, y, w, 20), "PLAYER INVENTORY");
            y += 22;

            GUI.Label(new Rect(x, y, 44, 20), "Filter");
            _invFilter = GUI.TextField(new Rect(x + 48, y, w - 48 - 110, 20), _invFilter ?? "");
            _invOnlyRelevant = GUI.Toggle(new Rect(x + w - 110, y, 110, 20), _invOnlyRelevant, "Relevant");
            y += 26;

            var entries = new List<(string id, int count)>();
            CollectInventoryEntries(entries);

            string f = (_invFilter ?? "").Trim();
            if (!string.IsNullOrEmpty(f))
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (entries[i].id.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                        entries.RemoveAt(i);
            }

            if (_invOnlyRelevant)
            {
                var relevant = BuildRelevantItemSet();
                for (int i = entries.Count - 1; i >= 0; i--)
                    if (!relevant.Contains(entries[i].id))
                        entries.RemoveAt(i);
            }

            entries.Sort((a, b) =>
            {
                int c = b.count.CompareTo(a.count);
                return c != 0 ? c : string.Compare(a.id, b.id, StringComparison.OrdinalIgnoreCase);
            });

            float listY = y;
            float listH = r.yMax - 10 - listY;

            var view = new Rect(x, listY, w, listH);
            var content = new Rect(0, 0, w - 16, Mathf.Max(1, entries.Count) * 22 + 6);

            _invScroll = GUI.BeginScrollView(view, _invScroll, content);

            float yy = 4;
            if (entries.Count == 0)
            {
                GUI.Label(new Rect(4, yy, content.width - 8, 20), "(empty)");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    GUI.Label(new Rect(4, yy, content.width - 70, 20), e.id);
                    GUI.Label(new Rect(content.width - 60, yy, 56, 20), e.count.ToString());
                    yy += 22;
                }
            }

            GUI.EndScrollView();
        }

        private void CollectInventoryEntries(List<(string id, int count)> outList)
        {
            outList.Clear();
            foreach (var kvp in _player.inventory.Enumerate())
            {
                if (kvp.Value <= 0) continue;
                outList.Add((kvp.Key, kvp.Value));
            }
        }

        private HashSet<string> BuildRelevantItemSet()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _offers.Count; i++)
            {
                var o = _offers[i];
                if (o == null) continue;
                set.Add(o.itemId);
            }

            return set;
        }
    }
}
