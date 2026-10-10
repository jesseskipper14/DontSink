using UnityEngine;
using System.Collections.Generic;
using System.Globalization;

namespace MiniGames
{
    /// <summary>Local civic services and live settlement report; later checkpoints supply work and neighbor reports.</summary>
    public sealed class NodeLeaderCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private readonly AgentServiceContext context;
        private readonly TownCenterBuilding building;
        private readonly string nodeId;
        private readonly CameraManager cameraManager;
        private readonly int cameraBinding;
        private bool closed;
        private string note;
        private enum Page { Services, Local, Work, Nearby }
        private Page page;
        private bool preciseValues;
        private readonly INodeCivicQuestProvider civicProvider;
        private IReadOnlyList<CivicQuestEntry> civicWork = new List<CivicQuestEntry>();
        private List<string> contacts = new();
        private string selectedContact;
        private AdjacentNodeIntelSnapshot selectedReport;
        private Vector2 reportScroll;
        private string localReport;
        private float nextReportRefresh;
        private GUIStyle reportStyle;
        public NodeLeaderCartridge(AgentServiceContext context, TownCenterBuilding building, INodeCivicQuestProvider civicProvider = null)
        {
            this.civicProvider = civicProvider ?? NodeCivicQuestProvider.Instance;
            this.context = context;
            this.building = building;
            nodeId = building != null ? building.NodeStableId : null;
            cameraManager = CameraManager.ForActor(context.Actor);
            cameraBinding = cameraManager != null ? cameraManager.BindingVersion : -1;
        }

        public bool IsInRange()
        {
            var agent = context.Agent;
            var actor = context.Actor;
            if (closed || building == null || !building.isActiveAndEnabled || agent == null || !agent.isActiveAndEnabled ||
                actor == null || !actor.activeInHierarchy || building.gameObject.scene.name != "NodeScene" ||
                actor.scene != agent.gameObject.scene || agent.gameObject.scene != building.gameObject.scene ||
                building.Leader != agent || building.NodeStableId != nodeId || agent.NodeId != nodeId ||
                agent.StableId != building.LeaderStableId || !HarborTravelService.TryGetNode(nodeId, out _)) return false;
            if (cameraManager == null || !cameraManager.CanProvideGameplayInput || cameraManager.BindingVersion != cameraBinding) return false;
            var boarding = actor.GetComponentInParent<PlayerBoardingState>() ?? actor.GetComponentInChildren<PlayerBoardingState>(true);
            if (boarding != null && boarding.IsBoarded) return false;
            var knowledge = Object.FindFirstObjectByType<WorldMapKnowledgeSource>();
            if (knowledge != null && knowledge.CurrentNodeId != nodeId) return false;
            float range = 1.8f;
            var interactable = agent.GetComponent<AgentInteractable>();
            if (interactable != null && interactable.TryGetActionRange(out float configured)) range = configured;
            return Vector2.Distance(actor.transform.position, agent.transform.position) <= range;
        }

        public void Begin(MiniGameContext _) { closed = false; }
        public MiniGameResult Tick(float dt, MiniGameInput input)
        {
            if (!IsInRange()) return new MiniGameResult { outcome = MiniGameOutcome.Cancelled };
            if (Time.unscaledTime >= nextReportRefresh)
            {
                if (page == Page.Local) RefreshLocalReport();
                else if (page == Page.Work || page == Page.Nearby) RefreshCivicPages();
            }
            return new MiniGameResult { outcome = MiniGameOutcome.None };
        }
        public MiniGameResult Cancel() { closed = true; return new MiniGameResult { outcome = MiniGameOutcome.Cancelled }; }
        public MiniGameResult Interrupt(string reason) => Cancel();
        public void End() { closed = true; }

        public void ShowLocalReport()
        {
            if (!IsInRange()) return;
            page = Page.Local;
            note = null;
            reportScroll = Vector2.zero;
            RefreshLocalReport();
        }

        public string LocalReport => localReport;
        public IReadOnlyList<CivicQuestEntry> CivicWork => civicWork;
        public AdjacentNodeIntelSnapshot SelectedNeighborReport => selectedReport?.Copy();

        public void ShowWork()
        {
            if (!IsInRange()) return;
            page = Page.Work; note = null; reportScroll = Vector2.zero;
            RefreshCivicPages();
        }

        public bool TryAcceptWork(string questId)
        {
            if (!IsInRange()) return false;
            bool result = civicProvider.TryAccept(nodeId, questId, out note);
            RefreshCivicPages();
            return result;
        }

        public bool TryTurnInWork(string questId)
        {
            if (!IsInRange()) return false;
            bool result = civicProvider.TryTurnIn(nodeId, questId, out note);
            RefreshCivicPages();
            return result;
        }

        public void ShowNearby()
        {
            if (!IsInRange()) return;
            page = Page.Nearby; note = null; reportScroll = Vector2.zero;
            RefreshCivicPages();
        }

        public void SelectNeighbor(string observedId)
        {
            if (!IsInRange() || !NodeAdjacentIntelService.GetNeighborIds(nodeId).Contains(observedId)) return;
            selectedContact = observedId; reportScroll = Vector2.zero;
            RefreshCivicPages();
        }

        private void RefreshCivicPages()
        {
            nextReportRefresh = Time.unscaledTime + .5f;
            if (page == Page.Work) civicWork = civicProvider.GetAvailable(nodeId);
            if (page == Page.Nearby)
            {
                contacts = NodeAdjacentIntelService.GetNeighborIds(nodeId);
                selectedReport = selectedContact != null ? NodeAdjacentIntelService.GetLastReport(nodeId, selectedContact) : null;
            }
        }

        private void RefreshLocalReport()
        {
            nextReportRefresh = Time.unscaledTime + .5f;
            MapNodeState state = null;
            var store = GameState.I?.worldMap?.byNodeStableId;
            if (store != null) store.TryGetValue(nodeId, out state);
            var graph = HarborTravelService.CurrentGraph;
            string name = null;
            int index = -1;
            if (graph != null)
                for (int i = 0; i < graph.nodes.Count; i++)
                    if (graph.nodes[i] != null && WorldMapStableIdUtility.BuildNodeStableId(graph.seed, graph.nodes[i]) == nodeId)
                    { index = i; name = graph.nodes[i].displayName; break; }
            var eventManager = Object.FindFirstObjectByType<WorldMapEventManager>();
            var trade = Object.FindFirstObjectByType<TradeBoatSceneRunner>();
            localReport = NodeLocalInformationReport.Build(nodeId, name, state, index,
                eventManager != null ? eventManager.active : null, preciseValues, trade != null ? trade.resourceCatalog : null);
        }

        public void DrawOverlayGUI(Rect panel)
        {
            GUILayout.BeginArea(panel);
            GUILayout.Label(context.Agent != null ? context.Agent.DisplayName.ToUpperInvariant() : "NODE LEADER");
            GUILayout.Label("Town Center · civic services");
            bool prior = GUI.enabled;
            GUI.enabled = prior && IsInRange();
            reportStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = false };
            if (page != Page.Services)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Back to services", GUILayout.Height(32))) { page = Page.Services; note = null; }
                if (page == Page.Local)
                {
                    bool precise = GUILayout.Toggle(preciseValues, "Show precise values");
                    if (precise != preciseValues) { preciseValues = precise; RefreshLocalReport(); }
                }
                GUILayout.EndHorizontal();
                reportScroll = GUILayout.BeginScrollView(reportScroll, GUILayout.ExpandHeight(true));
                if (page == Page.Local) GUILayout.Label(localReport ?? "Current settlement information is unavailable.", reportStyle);
                else if (page == Page.Work) DrawWork();
                else if (page == Page.Nearby) DrawNearby();
                GUILayout.EndScrollView();
            }
            else
            {
                if (GUILayout.Button("Work", GUILayout.Height(36))) ShowWork();
                if (GUILayout.Button("About This Settlement", GUILayout.Height(36))) ShowLocalReport();
                if (GUILayout.Button("Nearby Settlements", GUILayout.Height(36))) ShowNearby();
            }
            if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, reportStyle);
            GUI.enabled = prior;
            GUILayout.Space(12);
            if (GUILayout.Button("Leave", GUILayout.Height(30))) closed = true;
            GUILayout.EndArea();
        }

        private void DrawWork()
        {
            GUILayout.Label("Civic work", reportStyle);
            if (civicWork.Count == 0) GUILayout.Label("Work information is unavailable from the host.", reportStyle);
            foreach (var quest in civicWork)
            {
                GUILayout.Space(12);
                GUILayout.Label(quest.title + (quest.temporary ? " (temporary)" : ""), reportStyle);
                GUILayout.Label(quest.description, reportStyle);
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && GameplayAuthority.IsAuthoritative;
                if (quest.status == CivicQuestStatus.Offered && GUILayout.Button("Accept work", GUILayout.Height(32))) TryAcceptWork(quest.id);
                else if (quest.status == CivicQuestStatus.Accepted && GUILayout.Button("Confirm check-in / Turn in", GUILayout.Height(32))) TryTurnInWork(quest.id);
                GUI.enabled = enabled;
            }
        }

        private void DrawNearby()
        {
            GUILayout.Label("Contact neighbors · last received reports", reportStyle);
            if (contacts.Count == 0) GUILayout.Label("No contact neighbors are recorded.", reportStyle);
            foreach (string contact in contacts)
                if (GUILayout.Button(NodeAdjacentIntelService.ContactName(contact), GUILayout.Height(32))) SelectNeighbor(contact);
            GUILayout.Space(12);
            if (selectedContact == null) GUILayout.Label("Choose a settlement. These reports do not add locations to your map.", reportStyle);
            else if (selectedReport == null) GUILayout.Label("No reliable report has been received about this settlement.", reportStyle);
            else
            {
                if (NodeAdjacentIntelService.TryGetWorldHours(out double hours))
                {
                    double observedAge = System.Math.Max(0, hours - selectedReport.observedAtWorldHours) / 24;
                    double receivedAge = System.Math.Max(0, hours - selectedReport.receivedAtWorldHours) / 24;
                    GUILayout.Label("Observed " + observedAge.ToString("0.0", CultureInfo.InvariantCulture) + " days ago · received " +
                        receivedAge.ToString("0.0", CultureInfo.InvariantCulture) + " days ago", reportStyle);
                }
                else GUILayout.Label("Report age unavailable without a world clock.", reportStyle);
                GUILayout.Label(selectedReport.provenance, reportStyle);
                GUILayout.Label("Values and remaining times are from the observation, and may have changed.", reportStyle);
                GUILayout.Label(selectedReport.report, reportStyle);
            }
        }
    }
}

