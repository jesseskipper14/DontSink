using System.Collections.Generic;
using UnityEngine;

public sealed partial class BoatObservationPresentationController
{
    private string surfaceSurveyToken, surfaceSurveyJob, surfaceSurveyNote;
    private int surfaceSurveyTarget, surfaceSurveyReferenceZone;
    private Vector2 surfaceSurveyScroll;
    private readonly Vector2[] surfaceSurveyTargets = new Vector2[3];
    private bool showSurfaceSurveyReference;
    private CelestialSkyRenderer surfaceSurveySkyRenderer;

    private void CancelSurfaceSurvey()
    {
        SurfaceSurveyAuthority.CancelReading(gameObject, surfaceSurveyToken);
        surfaceSurveyToken = null; surfaceSurveyTarget = 0;
        surfaceSurveySkyRenderer = null;
    }

    private void OnGUI()
    {
        if (!IsEscapeOpen || telescope == null || sessionCamera == null || !HasLocalAuthority(out _)) return;
        DrawReferenceComparisonGUI();
        var book = SurfaceSurveyAuthority.Book;
        if (book == null) return;
        var jobs = book.contracts.FindAll(c => c != null && !c.rewardIssued);
        if (jobs.Count == 0) return;
        var selected = jobs.Find(c => c.id == surfaceSurveyJob);
        if (selected == null) { selected = jobs[0]; surfaceSurveyJob = selected.id; }
        Rect cameraRect = sessionCamera.pixelRect;
        float left = cameraRect.x + 12, top = Screen.height - cameraRect.yMax + 12;
        float width = Mathf.Min(370, cameraRect.width - 24);
        Rect panel = new Rect(left, top, width, Mathf.Min(540, cameraRect.height - 24));
        GUI.Box(panel, "");
        GUILayout.BeginArea(new Rect(panel.x + 8, panel.y + 8, panel.width - 16, panel.height - 16));
        surfaceSurveyScroll = GUILayout.BeginScrollView(surfaceSurveyScroll);
        GUILayout.Label("SURFACE SURVEY");
        if (surfaceSurveyToken == null)
        {
            foreach (var job in jobs)
                if (GUILayout.Button(job.title)) { surfaceSurveyJob = job.id; selected = job; surfaceSurveyReferenceZone = 0; }
        }
        GUILayout.Label($"{selected.title} — {selected.CompletedCount}/{selected.zones.Count}");
        if (surfaceSurveyToken == null)
        {
            bool prior = GUI.enabled; GUI.enabled = prior && GameplayAuthority.IsAuthoritative && !selected.IsComplete;
            if (GUILayout.Button("Survey", GUILayout.Height(36)))
            {
                var source = FindFirstObjectByType<WorldMapKnowledgeSource>();
                if (SurfaceSurveyAuthority.TryBeginReading(gameObject, telescope, selected.id, source,
                    out surfaceSurveyToken, out surfaceSurveyNote))
                {
                    surfaceSurveyTarget = 0;
                    surfaceSurveySkyRenderer = FindFirstObjectByType<CelestialSkyRenderer>();
                    var random = new System.Random(SurfaceSurveyContractGenerator.StableHash(surfaceSurveyToken));
                    for (int i = 0; i < 3; i++) surfaceSurveyTargets[i] = new Vector2(.52f + (float)random.NextDouble() * .35f, .28f + (float)random.NextDouble() * .44f);
                }
            }
            GUI.enabled = prior;
            if (selected.IsComplete) GUILayout.Label("All readings recorded. Return to an eligible Surveyor for processing.");
        }
        else
        {
            GUILayout.Label($"Horizon target {surfaceSurveyTarget + 1}/3 · hold {SurfaceSurveyAuthority.SecondsUntilTarget(surfaceSurveyToken):0.0}s");
            GUILayout.Label("Move your reticle onto the circle and click when ready.");
            if (GUILayout.Button("Cancel reading")) CancelSurfaceSurvey();
        }
        if (!string.IsNullOrEmpty(surfaceSurveyNote)) GUILayout.Label(surfaceSurveyNote);
        showSurfaceSurveyReference = GUILayout.Toggle(showSurfaceSurveyReference, "Show job's sky reference / directions");
        if (showSurfaceSurveyReference) SurfaceSurveyUI.DrawInstructions(selected, ref surfaceSurveyReferenceZone);
        GUILayout.EndScrollView(); GUILayout.EndArea();
        if (surfaceSurveyToken == null) return;
        if (surfaceSurveySkyRenderer != null && surfaceSurveySkyRenderer.TryGetSurveyCenterScreen(sessionCamera, out var skyCenter))
            SurfaceSurveyUI.DrawCenterReference(skyCenter);
        Vector2 target = new Vector2(cameraRect.x + surfaceSurveyTargets[surfaceSurveyTarget].x * cameraRect.width,
            top - 12 + surfaceSurveyTargets[surfaceSurveyTarget].y * cameraRect.height);
        bool ready = SurfaceSurveyAuthority.SecondsUntilTarget(surfaceSurveyToken) <= 0;
        SurfaceSurveyUI.DrawRing(target, 22, ready ? Color.cyan : Color.gray);
        Vector2 pointer = Event.current.mousePosition;
        SurfaceSurveyUI.DrawRing(pointer, 7, Color.white);
        GUI.Label(new Rect(target.x - 60, target.y + 26, 120, 25), ready ? "Click to confirm" : "Observing...");
        if (ready && Event.current.type == EventType.MouseDown && Event.current.button == 0 && (pointer - target).sqrMagnitude <= 22 * 22)
        {
            bool confirmed = SurfaceSurveyAuthority.TryConfirmTarget(gameObject, telescope, surfaceSurveyToken,
                surfaceSurveyTarget, out bool finished, out surfaceSurveyNote);
            if (finished) GameMessageService.PostInfo(surfaceSurveyNote);
            if (!confirmed || finished) { surfaceSurveyToken = null; surfaceSurveyTarget = 0; }
            else surfaceSurveyTarget++;
            Event.current.Use();
        }
    }
}
