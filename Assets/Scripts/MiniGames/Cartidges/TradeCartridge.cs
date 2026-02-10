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
    /// - Returns Completed after confirm so the host closes
    /// </summary>
    public sealed class TradeCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private MiniGameContext _ctx;

        private readonly string _nodeId;
        private readonly int _timeBucket;
        private readonly IReadOnlyList<NodeMarketOffer> _offers;

        // Read-only display source. Mutation happens via TradeEffectApplier/TradeService.
        private readonly WorldMapPlayerState _player;

        // UI state
        private readonly Dictionary<string, int> _qtyByOfferId = new Dictionary<string, int>();
        private Vector2 _scroll;
        private bool _requestedClose;
        private string _lastUiNote;

        public TradeCartridge(string nodeId, int timeBucket, IReadOnlyList<NodeMarketOffer> offers, WorldMapPlayerState player)
        {
            _nodeId = nodeId;
            _timeBucket = timeBucket;
            _offers = offers ?? Array.Empty<NodeMarketOffer>();
            _player = player;
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
                    outcome = MiniGameOutcome.Completed,
                    quality01 = 1f,
                    note = _lastUiNote ?? "Trade confirmed",
                    hasMeaningfulProgress = true
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

            // Header
            GUI.Label(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 22),
                $"TRADE @ {_nodeId} (day {_timeBucket})");

            GUI.Label(new Rect(panel.x + 14, panel.y + 32, panel.width - 28, 22),
                $"Credits: {_player.credits}");

            // Offers list
            float listY = panel.y + 60;
            float listH = panel.height - 120;

            var viewRect = new Rect(panel.x + 14, listY, panel.width - 28, listH);
            var contentRect = new Rect(0, 0, viewRect.width - 16, Mathf.Max(1, _offers.Count) * 34f + 8f);

            _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect);

            float y = 6f;
            if (_offers.Count == 0)
            {
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22),
                    "(No offers)");
            }
            else
            {
                for (int i = 0; i < _offers.Count; i++)
                {
                    var o = _offers[i];
                    if (o == null) continue;

                    DrawOfferRow(o, y, contentRect.width);
                    y += 34f;
                }
            }

            GUI.EndScrollView();

            // Footer buttons
            var btnRow = new Rect(panel.x + 14, panel.yMax - 48, panel.width - 28, 34);
            float bw = 110f;

            if (GUI.Button(new Rect(btnRow.x, btnRow.y, bw, btnRow.height), "Clear"))
                ClearAll();

            if (GUI.Button(new Rect(btnRow.xMax - bw * 2f - 10f, btnRow.y, bw, btnRow.height), "Preview"))
                _lastUiNote = BuildPreviewString();

            if (GUI.Button(new Rect(btnRow.xMax - bw, btnRow.y, bw, btnRow.height), "Confirm"))
                ConfirmTransaction();
        }

        private void DrawOfferRow(NodeMarketOffer o, float y, float w)
        {
            string label = o.kind == MarketOfferKind.SellToPlayer ? "BUY" : "SELL";
            int qty = _qtyByOfferId.TryGetValue(o.offerId, out var q) ? q : 0;

            int have = _player.inventory.GetCount(o.itemId);

            // Columns (IMGUI, so: vibes and rectangles)
            GUI.Label(new Rect(6, y, 42, 22), label);
            GUI.Label(new Rect(52, y, 140, 22), o.itemId);
            GUI.Label(new Rect(196, y, 70, 22), $"{o.unitPrice}c");

            if (o.kind == MarketOfferKind.BuyFromPlayer)
                GUI.Label(new Rect(270, y, 90, 22), $"have {have}");

            // Qty controls
            float x = w - 140;

            if (GUI.Button(new Rect(x, y, 24, 22), "-"))
                SetQty(o.offerId, qty - 1);

            GUI.Label(new Rect(x + 30, y, 44, 22), qty.ToString());

            if (GUI.Button(new Rect(x + 80, y, 24, 22), "+"))
                SetQty(o.offerId, qty + 1);

            // Soft hint if selling more than you have
            if (o.kind == MarketOfferKind.BuyFromPlayer && qty > have)
            {
                GUI.Label(new Rect(x + 110, y, 40, 22), "!");
            }
        }

        private void SetQty(string offerId, int qty)
        {
            qty = Mathf.Clamp(qty, 0, 999);
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
            int creditsDelta = 0;

            foreach (var o in _offers)
            {
                if (o == null) continue;
                if (!_qtyByOfferId.TryGetValue(o.offerId, out var qty) || qty <= 0) continue;

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

                draft.lines.Add(new TradeLine
                {
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
            _requestedClose = true;
        }
    }
}
