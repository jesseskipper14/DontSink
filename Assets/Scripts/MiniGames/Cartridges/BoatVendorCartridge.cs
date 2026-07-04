using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    public sealed class BoatVendorCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private enum MainTab
        {
            Boats,
            Modules,
            Builder
        }

        private enum ModuleTab
        {
            Buy,
            Sell
        }

        private enum ModuleSellArea
        {
            Player,
            Boat
        }

        private sealed class BoatVendorUiState
        {
            public MainTab mainTab = MainTab.Boats;
            public ModuleTab moduleTab = ModuleTab.Buy;
            public ModuleSellArea moduleSellArea = ModuleSellArea.Player;

            public Vector2 boatScroll;
            public Vector2 moduleBuyScroll;
            public Vector2 playerModuleSellScroll;
            public Vector2 boatModuleSellScroll;
            public Vector2 noteScroll;

            public string lastUiNote;
            public bool hideUnsellableModules;

            public readonly Dictionary<int, int> runtimeModuleStockByOfferIndex = new();
        }

        private const float FooterHeight = 98f;

        private static readonly Dictionary<string, BoatVendorUiState> UiStateByKey =
            new(StringComparer.Ordinal);

        private readonly string _sessionId;
        private readonly BoatVendorPlaceholderServiceDefinition _vendor;
        private readonly BoatCatalog _boatCatalog;

        private readonly IReadOnlyList<ItemVendorSellOffer> _moduleBuyOffers;
        private readonly IReadOnlyList<ItemVendorSellSource> _playerModuleSellSources;
        private readonly IReadOnlyList<ItemVendorSellSource> _boatModuleSellSources;

        private readonly ItemDefinitionCatalog _itemCatalog;
        private readonly BoatVendorModuleTransactionService _moduleTransactionService;
        private readonly AgentServiceContext _agentContext;

        private MiniGameContext _ctx;
        private bool _requestedClose;
        private BoatVendorUiState _uiState;

        public BoatVendorCartridge(
            string sessionId,
            BoatVendorPlaceholderServiceDefinition vendor,
            BoatCatalog boatCatalog,
            IReadOnlyList<ItemVendorSellOffer> moduleBuyOffers,
            IReadOnlyList<ItemVendorSellSource> playerModuleSellSources,
            IReadOnlyList<ItemVendorSellSource> boatModuleSellSources,
            ItemDefinitionCatalog itemCatalog,
            BoatVendorModuleTransactionService moduleTransactionService,
            AgentServiceContext agentContext)
        {
            _sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? "boat_vendor:unknown"
                : sessionId;

            _vendor = vendor;
            _boatCatalog = boatCatalog;

            _moduleBuyOffers = moduleBuyOffers ?? Array.Empty<ItemVendorSellOffer>();
            _playerModuleSellSources = playerModuleSellSources ?? Array.Empty<ItemVendorSellSource>();
            _boatModuleSellSources = boatModuleSellSources ?? Array.Empty<ItemVendorSellSource>();

            _itemCatalog = itemCatalog;
            _moduleTransactionService = moduleTransactionService;
            _agentContext = agentContext;
        }

        public void Begin(MiniGameContext context)
        {
            _ctx = context ?? new MiniGameContext();
            _uiState = GetOrCreateUiState();
            EnsureRuntimeModuleStock();
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

            EnsureRuntimeModuleStock();

            float pad = 14f;

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 10, panel.width - pad * 2f, 22),
                ResolveTitle());

            GUI.Label(
                new Rect(panel.x + pad, panel.y + 32, panel.width - pad * 2f, 22),
                GetMoneyLabel());

            if (GUI.Button(new Rect(panel.xMax - 34, panel.y + 8, 26, 22), "X"))
            {
                _requestedClose = true;
                _uiState.lastUiNote = "Closed";
            }

            DrawMainTabs(panel, pad);

            switch (_uiState.mainTab)
            {
                case MainTab.Boats:
                    DrawBoatsTab(panel, pad);
                    break;

                case MainTab.Modules:
                    DrawModulesTab(panel, pad);
                    break;

                case MainTab.Builder:
                    DrawBuilderTab(panel, pad);
                    break;
            }

            DrawFooter(panel, pad);
        }

        public int GetRuntimeModuleStock(int offerIndex)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            EnsureRuntimeModuleStock();

            return _uiState.runtimeModuleStockByOfferIndex.TryGetValue(offerIndex, out int stock)
                ? stock
                : 0;
        }

        public void NotifyModuleBuyApplied(int offerIndex, bool success, string message)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.lastUiNote = message;

            if (!success)
                return;

            int stock = GetRuntimeModuleStock(offerIndex);
            if (stock < 0)
                return;

            _uiState.runtimeModuleStockByOfferIndex[offerIndex] = Mathf.Max(0, stock - 1);
        }

        public void NotifyModuleSellApplied(bool success, string message)
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            _uiState.lastUiNote = message;
        }

        public void NotifyModuleSellSourcesChanged()
        {
            // Sources are held by reference to the runner lists. Runner rebuilds them.
            // Nothing to do here except avoid future stale confirmation state if we add one.
        }

        private void DrawMainTabs(Rect panel, float pad)
        {
            float y = panel.y + 58f;
            float x = panel.x + pad;

            if (GUI.Button(new Rect(x, y, 90, 24), "BOATS"))
                _uiState.mainTab = MainTab.Boats;

            if (GUI.Button(new Rect(x + 98, y, 100, 24), "MODULES"))
                _uiState.mainTab = MainTab.Modules;

            if (GUI.Button(new Rect(x + 206, y, 100, 24), "BUILDER"))
                _uiState.mainTab = MainTab.Builder;
        }

        private void DrawBoatsTab(Rect panel, float pad)
        {
            float listX = panel.x + pad;
            float listY = panel.y + 92f;
            float listW = panel.width - pad * 2f;
            float listH = Mathf.Max(60f, GetListBottomY(panel) - listY);

            IReadOnlyList<BoatCatalog.Entry> entries =
                _boatCatalog != null ? _boatCatalog.Entries : null;

            int count = entries != null ? entries.Count : 0;

            Rect viewRect = new Rect(listX, listY, listW, listH);
            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, count + 1) * 38f + 24f);

            _uiState.boatScroll =
                GUI.BeginScrollView(viewRect, _uiState.boatScroll, contentRect);

            float y = 6f;

            GUI.Label(new Rect(6, y, 240, 22), "BOAT");
            GUI.Label(new Rect(250, y, 320, 22), "GUID");
            GUI.Label(new Rect(580, y, 180, 22), "STATUS");

            y += 28f;

            bool any = false;

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    BoatCatalog.Entry entry = entries[i];
                    if (entry == null)
                        continue;

                    DrawBoatRow(i, entry, y, contentRect.width);
                    y += 38f;
                    any = true;
                }
            }

            if (!any)
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No boats in catalog)");

            GUI.EndScrollView();
        }

        private void DrawBoatRow(int index, BoatCatalog.Entry entry, float y, float width)
        {
            DrawRowBackground(index, y, Mathf.Min(width - 6f, 880f));

            string boatName = entry.prefab != null ? CleanName(entry.prefab.name) : "<missing prefab>";
            string guid = !string.IsNullOrWhiteSpace(entry.guid) ? entry.guid : "<missing guid>";

            GUI.Label(new Rect(6, y, 240, 22), boatName);
            GUI.Label(new Rect(250, y, 320, 22), guid);
            GUI.Label(new Rect(580, y, 180, 22), "Pending Development");

            GUI.enabled = false;
            GUI.Button(new Rect(GetActionX(width), y - 1f, 110f, 24f), "Coming Soon");
            GUI.enabled = true;
        }

        private void DrawModulesTab(Rect panel, float pad)
        {
            float y = panel.y + 92f;
            float x = panel.x + pad;

            if (GUI.Button(new Rect(x, y, 120, 24), "BUY MODULES"))
                _uiState.moduleTab = ModuleTab.Buy;

            if (GUI.Button(new Rect(x + 128, y, 120, 24), "SELL MODULES"))
                _uiState.moduleTab = ModuleTab.Sell;

            if (_uiState.moduleTab == ModuleTab.Buy)
                DrawModuleBuyTab(panel, pad);
            else
                DrawModuleSellTab(panel, pad);
        }

        private void DrawModuleBuyTab(Rect panel, float pad)
        {
            float listX = panel.x + pad;
            float listY = panel.y + 126f;
            float listW = panel.width - pad * 2f;
            float listH = Mathf.Max(60f, GetListBottomY(panel) - listY);

            int count = _moduleBuyOffers != null ? _moduleBuyOffers.Count : 0;

            Rect viewRect = new Rect(listX, listY, listW, listH);
            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, count + 1) * 38f + 24f);

            _uiState.moduleBuyScroll =
                GUI.BeginScrollView(viewRect, _uiState.moduleBuyScroll, contentRect);

            float y = 6f;

            GUI.Label(new Rect(6, y, 250, 22), "MODULE");
            GUI.Label(new Rect(270, y, 100, 22), "PRICE");
            GUI.Label(new Rect(380, y, 100, 22), "STOCK");
            GUI.Label(new Rect(GetActionX(contentRect.width), y, 90, 22), "ACTION");

            y += 28f;

            bool any = false;

            if (_moduleBuyOffers != null)
            {
                for (int i = 0; i < _moduleBuyOffers.Count; i++)
                {
                    ItemVendorSellOffer offer = _moduleBuyOffers[i];
                    if (offer == null)
                        continue;

                    DrawModuleBuyRow(i, offer, y, contentRect.width);
                    y += 38f;
                    any = true;
                }
            }

            if (!any)
                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), "(No boat modules for sale)");

            GUI.EndScrollView();
        }

        private void DrawModuleBuyRow(int index, ItemVendorSellOffer offer, float y, float width)
        {
            DrawRowBackground(index, y, Mathf.Min(width - 6f, GetActionX(width) + 92f));

            ItemDefinition def = offer.ResolveDefinition(_itemCatalog);
            int price = offer.ResolvePrice(def);
            string label = offer.ResolveLabel(def);

            int stock = GetRuntimeModuleStock(index);
            string stockLabel = stock < 0 ? "∞" : stock.ToString();

            GUI.Label(new Rect(6, y, 250, 22), label);
            GUI.Label(new Rect(270, y, 100, 22), $"${price:n0}");
            GUI.Label(new Rect(380, y, 100, 22), stockLabel);

            bool canBuy = false;
            string reason = null;

            if (stock == 0)
            {
                reason = "Out of stock.";
            }
            else if (_moduleTransactionService == null)
            {
                reason = "Missing module transaction service.";
            }
            else
            {
                canBuy = _moduleTransactionService.CanBuyModule(
                    _vendor,
                    offer,
                    _agentContext,
                    out _,
                    out _,
                    out reason);
            }

            float actionX = GetActionX(width);
            float noteX = 500f;
            float noteW = Mathf.Max(80f, actionX - noteX - 10f);

            if (!canBuy && !string.IsNullOrWhiteSpace(reason))
                GUI.Label(new Rect(noteX, y, noteW, 22), reason);

            GUI.enabled = canBuy;

            if (GUI.Button(new Rect(actionX, y - 1f, 86f, 24f), "Buy"))
                SubmitModuleBuy(index, offer, def, price);

            GUI.enabled = true;
        }

        private void DrawModuleSellTab(Rect panel, float pad)
        {
            float y = panel.y + 126f;
            float x = panel.x + pad;

            if (GUI.Button(new Rect(x, y, 150, 24), "PLAYER INVENTORY"))
                _uiState.moduleSellArea = ModuleSellArea.Player;

            if (GUI.Button(new Rect(x + 158, y, 150, 24), "BOAT INVENTORY"))
                _uiState.moduleSellArea = ModuleSellArea.Boat;

            _uiState.hideUnsellableModules = GUI.Toggle(
                new Rect(x + 326, y + 2, 190, 22),
                _uiState.hideUnsellableModules,
                "Hide unsellable modules");

            if (_uiState.moduleSellArea == ModuleSellArea.Boat)
            {
                DrawModuleSellSourceList(
                    panel,
                    pad,
                    _boatModuleSellSources,
                    ItemVendorSellArea.Boat,
                    ref _uiState.boatModuleSellScroll);
            }
            else
            {
                DrawModuleSellSourceList(
                    panel,
                    pad,
                    _playerModuleSellSources,
                    ItemVendorSellArea.Player,
                    ref _uiState.playerModuleSellScroll);
            }
        }

        private void DrawModuleSellSourceList(
            Rect panel,
            float pad,
            IReadOnlyList<ItemVendorSellSource> sources,
            ItemVendorSellArea area,
            ref Vector2 scroll)
        {
            float listX = panel.x + pad;
            float listY = panel.y + 160f;
            float listW = panel.width - pad * 2f;
            float listH = Mathf.Max(60f, GetListBottomY(panel) - listY);

            int visibleCount = CountVisibleModuleSellSources(sources);

            Rect viewRect = new Rect(listX, listY, listW, listH);
            Rect contentRect = new Rect(
                0,
                0,
                Mathf.Max(1f, viewRect.width - 16f),
                Mathf.Max(1, visibleCount + 1) * 42f + 24f);

            scroll = GUI.BeginScrollView(viewRect, scroll, contentRect);

            float y = 6f;

            GUI.Label(new Rect(6, y, 280, 22), "SOURCE");
            GUI.Label(new Rect(300, y, 220, 22), "MODULE");
            GUI.Label(new Rect(530, y, 70, 22), "VALUE");
            GUI.Label(new Rect(GetActionX(contentRect.width), y, 90, 22), "ACTION");

            y += 28f;

            bool any = false;

            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    ItemVendorSellSource source = sources[i];
                    if (source == null)
                        continue;

                    if (ShouldHideModuleSellSource(source))
                        continue;

                    DrawModuleSellSourceRow(area, i, source, y, contentRect.width);
                    y += 42f;
                    any = true;
                }
            }

            if (!any)
            {
                string label = _uiState.hideUnsellableModules
                    ? "(No sellable boat modules)"
                    : "(No boat modules found)";

                GUI.Label(new Rect(6, y, contentRect.width - 12, 22), label);
            }

            GUI.EndScrollView();
        }

        private void DrawModuleSellSourceRow(
            ItemVendorSellArea area,
            int index,
            ItemVendorSellSource source,
            float y,
            float width)
        {
            ItemInstance item = source.Item;

            if (item == null || item.Definition == null)
            {
                GUI.Label(new Rect(6, y, width - 12, 22), "(missing module)");
                return;
            }

            DrawRowBackground(index, y, Mathf.Min(width - 6f, GetActionX(width) + 92f));

            int quantity = Mathf.Max(1, source.MaxQuantity);

            BoatVendorModuleSellPreview preview = _moduleTransactionService != null
                ? _moduleTransactionService.BuildSellPreview(_vendor, source, quantity)
                : null;

            string itemLabel = !string.IsNullOrWhiteSpace(item.Definition.DisplayName)
                ? item.Definition.DisplayName
                : item.Definition.ItemId;

            GUI.Label(new Rect(6, y, 280, 22), source.SourceLabel);
            GUI.Label(new Rect(300, y, 220, 22), itemLabel);

            int value = preview != null ? preview.totalValue : 0;
            GUI.Label(new Rect(530, y, 70, 22), $"${value:n0}");

            bool canSell = preview != null && preview.canSell;
            string reason = preview != null ? preview.blockReason : "Missing module transaction service.";

            float actionX = GetActionX(width);
            float noteX = 610f;
            float noteW = Mathf.Max(80f, actionX - noteX - 10f);

            if (!canSell && !string.IsNullOrWhiteSpace(reason))
                GUI.Label(new Rect(noteX, y, noteW, 22), reason);

            GUI.enabled = canSell;

            if (GUI.Button(new Rect(actionX, y - 1f, 86f, 24f), "Sell"))
                SubmitModuleSell(area, index, source, quantity);

            GUI.enabled = true;
        }

        private void DrawBuilderTab(Rect panel, float pad)
        {
            Rect box = new Rect(
                panel.x + pad,
                panel.y + 92f,
                panel.width - pad * 2f,
                Mathf.Max(60f, GetListBottomY(panel) - (panel.y + 92f)));

            GUI.Box(box, GUIContent.none);

            float x = box.x + 14f;
            float y = box.y + 14f;
            float w = box.width - 28f;

            GUI.Label(new Rect(x, y, w, 24), "BOAT BUILDER");
            y += 30f;

            string msg = _vendor != null
                ? _vendor.BuilderPlaceholderMessage
                : "Runtime boat builder is pending development.";

            GUI.Label(new Rect(x, y, w, 80), msg);
            y += 90f;

            GUI.enabled = false;
            GUI.Button(new Rect(x, y, 180f, 28f), "Open Builder Soon™");
            GUI.enabled = true;
        }

        private int CountVisibleModuleSellSources(IReadOnlyList<ItemVendorSellSource> sources)
        {
            if (sources == null)
                return 0;

            int count = 0;

            for (int i = 0; i < sources.Count; i++)
            {
                ItemVendorSellSource source = sources[i];
                if (source == null)
                    continue;

                if (ShouldHideModuleSellSource(source))
                    continue;

                count++;
            }

            return count;
        }

        private bool ShouldHideModuleSellSource(ItemVendorSellSource source)
        {
            if (!_uiState.hideUnsellableModules)
                return false;

            if (source == null || _moduleTransactionService == null)
                return true;

            int quantity = Mathf.Max(1, source.MaxQuantity);

            BoatVendorModuleSellPreview preview =
                _moduleTransactionService.BuildSellPreview(_vendor, source, quantity);

            return preview == null || !preview.canSell;
        }

        private void SubmitModuleBuy(
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
                system = "BoatVendorModuleBuy",
                targetId = _sessionId,
                payloadJson = json
            });

            _uiState.lastUiNote = "Module purchase submitted.";
        }

        private void SubmitModuleSell(
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
                system = "BoatVendorModuleSell",
                targetId = _sessionId,
                payloadJson = json
            });

            _uiState.lastUiNote = "Module sale submitted.";
        }

        private string ResolveTitle()
        {
            string agentName = _agentContext.Agent != null
                ? _agentContext.Agent.DisplayName
                : "Boat Vendor";

            return $"{agentName} - Boats";
        }

        private string GetMoneyLabel()
        {
            if (!MoneyService.HasActiveChest)
                return "Money Chest: NONE";

            return $"Money Chest: ${MoneyService.Balance:n0}";
        }

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

        private static float GetActionX(float width)
        {
            float preferred = 760f;
            float max = width - 120f;

            if (max <= 0f)
                return 0f;

            return Mathf.Min(preferred, max);
        }

        private static void DrawRowBackground(int index, float y, float width)
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

        private BoatVendorUiState GetOrCreateUiState()
        {
            if (!UiStateByKey.TryGetValue(_sessionId, out BoatVendorUiState state) || state == null)
            {
                state = new BoatVendorUiState();
                UiStateByKey[_sessionId] = state;
            }

            return state;
        }

        private void EnsureRuntimeModuleStock()
        {
            if (_uiState == null)
                _uiState = GetOrCreateUiState();

            if (_moduleBuyOffers == null)
                return;

            for (int i = 0; i < _moduleBuyOffers.Count; i++)
            {
                if (_uiState.runtimeModuleStockByOfferIndex.ContainsKey(i))
                    continue;

                ItemVendorSellOffer offer = _moduleBuyOffers[i];
                _uiState.runtimeModuleStockByOfferIndex[i] = offer != null ? offer.stock : 0;
            }

            List<int> keys = new List<int>(_uiState.runtimeModuleStockByOfferIndex.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                if (key < 0 || key >= _moduleBuyOffers.Count)
                    _uiState.runtimeModuleStockByOfferIndex.Remove(key);
            }
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Boat";

            return raw
                .Replace("(Clone)", "")
                .Replace("_", " ")
                .Trim();
        }
    }
}