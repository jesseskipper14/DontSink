using System.Collections.Generic;
using UnityEngine;

namespace MiniGames
{
    public sealed partial class SurveyorCartridge
    {
        private bool showSurveyWork;
        private List<SurfaceSurveyContract> surveyOffers = new();
        private string inspectedSurvey;
        private int inspectedZone;
#if UNITY_EDITOR
        private bool debugSurveyCoordinates;
#endif
        private void RefreshSurveyOffers()
        { surveyOffers = SurfaceSurveyAuthority.RefreshOffers(knowledge, nodeId, out note); }

        private void DrawSurveyWork()
        {
            bool prior = GUI.enabled;
            GUI.enabled = prior && !closed && IsInRange() && GameplayAuthority.IsAuthoritative;
            if (GUILayout.Button("Refresh available work")) RefreshSurveyOffers();
            GUILayout.Label("Available jobs");
            foreach (var offer in surveyOffers)
            {
                if (SurfaceSurveyAuthority.Book?.Find(offer.id) != null) continue;
                GUILayout.Label(offer.title);
                if (GUILayout.Button("Inspect " + offer.title)) { inspectedSurvey = offer.id; inspectedZone = 0; }
                if (GUILayout.Button("Accept job") && IsInRange())
                {
                    if (SurfaceSurveyAuthority.TryAccept(knowledge, nodeId, offer.id, out note)) inspectedSurvey = offer.id;
                }
            }
            var book = SurfaceSurveyAuthority.Book;
            GUILayout.Label("Crew's accepted jobs");
            if (book != null) foreach (var contract in new List<SurfaceSurveyContract>(book.contracts))
            {
                if (contract == null || contract.rewardIssued) continue;
                GUILayout.Label($"{contract.title} — {contract.CompletedCount}/{contract.zones.Count} readings");
                GUILayout.Label("Turn in at " + contract.originName + (string.IsNullOrEmpty(contract.otherNodeId) ? "" : " or " + contract.otherNodeName));
                if (GUILayout.Button("Inspect accepted job")) { inspectedSurvey = contract.id; inspectedZone = 0; }
                GUI.enabled = prior && !closed && IsInRange() && GameplayAuthority.IsAuthoritative && contract.IsComplete && contract.IsEndpoint(nodeId);
                if (GUILayout.Button("Process observations / receive chart") && IsInRange())
                    SurfaceSurveyAuthority.TryTurnIn(context.Actor, knowledge, nodeId, contract.id, out note);
                GUI.enabled = prior && !closed && IsInRange() && GameplayAuthority.IsAuthoritative;
            }
            GUI.enabled = prior;
            var selected = book?.Find(inspectedSurvey) ?? surveyOffers.Find(c => c.id == inspectedSurvey);
            if (selected != null)
            {
                SurfaceSurveyUI.DrawInstructions(selected, ref inspectedZone);
#if UNITY_EDITOR
                debugSurveyCoordinates = GUILayout.Toggle(debugSurveyCoordinates, "DEBUG: survey target coordinates");
                if (debugSurveyCoordinates)
                {
                    var position = selected.zones[inspectedZone].center;
                    GUILayout.Label($"Point {inspectedZone + 1} — X {position.x:0.000}, Y {position.y:0.000}");
                }
#endif
            }
        }
    }
}
