using UnityEngine;

namespace MiniGames
{
    /// <summary>Local presentation. Each consequential button revalidates the station/requester
    /// before calling the existing host-only cartographic services.</summary>
    public sealed partial class SurveyorCartridge : IMiniGameCartridge, IOverlayRenderable
    {
        private readonly AgentServiceContext context;
        private readonly WorldMapKnowledgeSource knowledge;
        private readonly string nodeId;
        private bool closed;
        private string note;
        private Vector2 scroll;

        public SurveyorCartridge(AgentServiceContext context, WorldMapKnowledgeSource knowledge, string nodeId)
        { this.context = context; this.knowledge = knowledge; this.nodeId = nodeId; }

        public bool IsInRange()
        {
            var agent = context.Agent;
            var actor = context.Actor;
            if (agent == null || !agent.isActiveAndEnabled || actor == null || !actor.activeInHierarchy ||
                agent.gameObject.scene.name != "NodeScene" || actor.scene != agent.gameObject.scene) return false;
            if (!string.IsNullOrWhiteSpace(agent.NodeId) && agent.NodeId != nodeId) return false;
            if (knowledge != null && knowledge.CurrentNodeId != nodeId) return false;
            var boarding = actor.GetComponentInParent<PlayerBoardingState>();
            if (boarding == null) boarding = actor.GetComponentInChildren<PlayerBoardingState>(true);
            if (boarding != null && boarding.IsBoarded) return false;
            float range = 1.8f;
            var interactable = agent.GetComponent<AgentInteractable>();
            if (interactable != null && interactable.TryGetActionRange(out float configured)) range = configured;
            return Vector2.Distance(actor.transform.position, agent.transform.position) <= range;
        }

        public void Begin(MiniGameContext _) { closed = false; }
        public MiniGameResult Tick(float dt, MiniGameInput input) =>
            new MiniGameResult { outcome = closed || !IsInRange() ? MiniGameOutcome.Cancelled : MiniGameOutcome.None };
        public MiniGameResult Cancel() { closed = true; return new MiniGameResult { outcome = MiniGameOutcome.Cancelled }; }
        public MiniGameResult Interrupt(string reason) => Cancel();
        public void End() { closed = true; }

        public void DrawOverlayGUI(Rect panel)
        {
            GUILayout.BeginArea(panel);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SURVEYOR");
            GUILayout.Label("Local charts and navigation services");
            bool prior = GUI.enabled;
            GUI.enabled = prior && !closed && IsInRange() && GameplayAuthority.IsAuthoritative;
            if (GUILayout.Button("Local Chart", GUILayout.Height(36)))
            {
                if (IsInRange()) SurveyorCartographyService.TryIssueLocalChart(context.Actor, knowledge, nodeId, out note);
            }
            if (GUILayout.Button("Fix Position", GUILayout.Height(36)))
            {
                if (IsInRange())
                {
                    bool fixedPosition = SurveyorCartographyService.TryFixPosition(context.Actor, nodeId, out note);
                    if (fixedPosition) note = "Believed-position marker corrected to this node.";
                }
            }
            GUI.enabled = prior;
            if (GUILayout.Button("Survey Work", GUILayout.Height(36)))
            {
                showSurveyWork = !showSurveyWork;
                if (showSurveyWork && IsInRange()) RefreshSurveyOffers();
            }
            if (showSurveyWork) DrawSurveyWork();
            showSoundings = GUILayout.Toggle(showSoundings, "Sounding Chart Processing");
            if (showSoundings) DrawSoundings();
            if (GUILayout.Button("Charts for Sale", GUILayout.Height(36))) note = "No charts offered for sale yet.";
            if (!GameplayAuthority.IsAuthoritative) GUILayout.Label("Surveyor transactions currently require the host.");
            if (!string.IsNullOrEmpty(note)) GUILayout.Label(note);
            GUILayout.Space(12);
            if (GUILayout.Button("Close", GUILayout.Height(30))) closed = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
