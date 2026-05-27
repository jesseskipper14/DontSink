using System.Collections.Generic;
using MiniGames;
using UnityEngine;

public sealed class WorldGenCartridge : IMiniGameCartridge, IOverlayRenderable
{
    private readonly WorldGenerationPipelineRunner _runner;
    private MiniGameContext _ctx;
    private bool _requestedClose;
    private Vector2 _logScroll;

    private static Texture2D _whiteTex;

    public WorldGenCartridge(WorldGenerationPipelineRunner runner)
    {
        _runner = runner;
    }

    public void Begin(MiniGameContext context)
    {
        _ctx = context ?? new MiniGameContext();
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
                note = "World generation lab closed.",
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
            note = "World generation lab cancelled.",
            hasMeaningfulProgress = false
        };
    }

    public MiniGameResult Interrupt(string reason)
    {
        return new MiniGameResult
        {
            outcome = MiniGameOutcome.Cancelled,
            quality01 = 1f,
            note = $"World generation lab interrupted: {reason}",
            hasMeaningfulProgress = false
        };
    }

    public void End()
    {
        _ctx = null;
    }

    public void DrawOverlayGUI(Rect panel)
    {
        EnsureWhiteTexture();

        DrawWindowBackground(panel);

        const float pad = 14f;
        const float headerH = 34f;
        const float footerH = 24f;

        float leftW = 285f;
        float rightW = 330f;

        Rect header = new Rect(
            panel.x + pad,
            panel.y + 8f,
            panel.width - pad * 2f,
            headerH
        );

        Rect content = new Rect(
            panel.x + pad,
            panel.y + headerH + 14f,
            panel.width - pad * 2f,
            panel.height - headerH - footerH - 28f
        );

        Rect leftRect = new Rect(content.x, content.y, leftW, content.height);
        Rect previewRect = new Rect(
            leftRect.xMax + pad,
            content.y,
            content.width - leftW - rightW - pad * 2f,
            content.height
        );

        Rect rightRect = new Rect(previewRect.xMax + pad, content.y, rightW, content.height);

        Rect footer = new Rect(
            panel.x + pad,
            panel.yMax - footerH - 6f,
            panel.width - pad * 2f,
            footerH
        );

        DrawHeader(header);
        DrawControls(leftRect);
        DrawPreview(previewRect);
        DrawStatus(rightRect);
        DrawFooter(footer);
    }

    private void DrawHeader(Rect rect)
    {
        GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width - 44f, 24f), "World Generation Lab");

        if (GUI.Button(new Rect(rect.xMax - 34f, rect.y, 30f, 26f), "X"))
            _requestedClose = true;
    }

    private void DrawControls(Rect rect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Layer Pipeline");
        y += 28f;

        bool running = _runner != null && _runner.Progress.isRunning;
        GUI.enabled = _runner != null && !running;

        if (GUI.Button(new Rect(x, y, w, 28f), "1. Generate Base Ocean"))
            _runner.GenerateBaseOcean();

        y += 34f;

        if (GUI.Button(new Rect(x, y, w, 28f), "2. Generate Ocean Features"))
            _runner.GenerateOceanFeatures();

        y += 34f;

        if (GUI.Button(new Rect(x, y, w, 28f), "Compose Final Height"))
            _runner.ComposeFinalHeight();

        y += 34f;

        if (GUI.Button(new Rect(x, y, w, 28f), "Run V1 Pipeline"))
            _runner.RunCurrentV1Pipeline();

        y += 42f;

        GUI.Label(new Rect(x, y, w, 22f), "Preview");
        y += 24f;

        if (GUI.Button(new Rect(x, y, w, 26f), "Preview Base Ocean"))
            _runner.PreviewBaseOcean();

        y += 30f;

        if (GUI.Button(new Rect(x, y, w, 26f), "Preview Ocean Features"))
            _runner.PreviewOceanFeatures();

        y += 30f;

        if (GUI.Button(new Rect(x, y, w, 26f), "Preview Final Height"))
            _runner.PreviewFinalHeight();

        y += 40f;

        GUI.enabled = _runner != null && running;

        if (GUI.Button(new Rect(x, y, w, 28f), "Cancel Generation"))
            _runner.RequestCancel();

        GUI.enabled = true;
        y += 42f;

        GUI.Label(new Rect(x, y, w, 22f), "Planned Layers");
        y += 24f;

        GUI.Label(
            new Rect(x, y, w, 170f),
            "✓ Smooth base ocean\n" +
            "✓ Ocean features\n" +
            "□ Island mass\n" +
            "□ Island detail\n" +
            "□ Sea/classification\n" +
            "□ Biomes\n" +
            "□ Nodes\n" +
            "□ Bake final world"
        );
    }

    private void DrawPreview(Rect rect)
    {
        DrawPanelBox(rect);

        Rect inner = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);

        if (_runner == null)
        {
            GUI.Label(inner, "Missing WorldGenerationPipelineRunner.");
            return;
        }

        if (!_runner.HasPreviewTexture)
        {
            GUI.Label(inner, "No preview yet. Generate Base Ocean or Run V1 Pipeline.");
            return;
        }

        Rect drawRect = FitTextureRect(inner, _runner.PreviewTexture.width, _runner.PreviewTexture.height);

        GUI.color = Color.white;
        GUI.DrawTexture(drawRect, _runner.PreviewTexture, ScaleMode.StretchToFill);
        DrawRectOutline(drawRect, 1f, new Color(1f, 1f, 1f, 0.2f));
    }

    private void DrawStatus(Rect rect)
    {
        DrawPanelBox(rect);

        float x = rect.x + 10f;
        float y = rect.y + 10f;
        float w = rect.width - 20f;

        GUI.Label(new Rect(x, y, w, 22f), "Status");
        y += 26f;

        if (_runner == null)
        {
            GUI.Label(new Rect(x, y, w, 60f), "No pipeline runner assigned.");
            return;
        }

        WorldGenerationProgress p = _runner.Progress;

        GUI.Label(new Rect(x, y, w, 22f), $"Phase: {p.phase}");
        y += 22f;

        GUI.Label(new Rect(x, y, w, 40f), p.statusText ?? "(no status)");
        y += 42f;

        DrawProgressBar(new Rect(x, y, w, 18f), p.progress01);
        y += 24f;

        GUI.Label(new Rect(x, y, w, 22f), $"Progress: {p.progress01:P0}    Elapsed: {p.elapsedSeconds:0.0}s");
        y += 34f;

        if (_runner.HasWorkingSet)
        {
            WorldGenerationWorkingSet ws = _runner.WorkingSet;
            GUI.Label(
                new Rect(x, y, w, 74f),
                $"Working Set\nSeed: {ws.seed}\nGrid: {ws.width} x {ws.height}\nBounds: {ws.worldBounds.width:0.#} x {ws.worldBounds.height:0.#}"
            );
            y += 82f;
        }
        else
        {
            GUI.Label(new Rect(x, y, w, 24f), "Working Set: none");
            y += 32f;
        }

        GUI.Label(new Rect(x, y, w, 22f), "Log");
        y += 24f;

        Rect logRect = new Rect(x, y, w, rect.yMax - y - 10f);
        DrawLog(logRect);
    }

    private void DrawLog(Rect rect)
    {
        if (_runner == null)
            return;

        IReadOnlyList<string> log = _runner.Log;
        float lineH = 20f;
        float contentH = Mathf.Max(rect.height, log.Count * lineH + 8f);

        _logScroll = GUI.BeginScrollView(
            rect,
            _logScroll,
            new Rect(0f, 0f, rect.width - 18f, contentH)
        );

        for (int i = 0; i < log.Count; i++)
            GUI.Label(new Rect(4f, 4f + i * lineH, rect.width - 26f, lineH), log[i]);

        GUI.EndScrollView();
    }

    private void DrawFooter(Rect rect)
    {
        string text = _runner == null
            ? "WorldGen Lab: no runner"
            : "WorldGen Lab: layered generation prototype";

        GUI.Label(rect, text);
    }

    private static Rect FitTextureRect(Rect bounds, int texW, int texH)
    {
        if (texW <= 0 || texH <= 0 || bounds.width <= 0f || bounds.height <= 0f)
            return bounds;

        float texAspect = texW / (float)texH;
        float boundsAspect = bounds.width / bounds.height;

        if (texAspect > boundsAspect)
        {
            float h = bounds.width / texAspect;
            return new Rect(bounds.x, bounds.center.y - h * 0.5f, bounds.width, h);
        }

        {
            float w = bounds.height * texAspect;
            return new Rect(bounds.center.x - w * 0.5f, bounds.y, w, bounds.height);
        }
    }

    private static void DrawProgressBar(Rect rect, float progress01)
    {
        Color old = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(rect, _whiteTex);

        Rect fill = rect;
        fill.width *= Mathf.Clamp01(progress01);

        GUI.color = new Color(0.35f, 0.8f, 1f, 0.85f);
        GUI.DrawTexture(fill, _whiteTex);

        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        DrawRectOutline(rect, 1f);

        GUI.color = old;
    }

    private static void DrawPanelBox(Rect rect)
    {
        Color old = GUI.color;

        GUI.color = new Color(0.02f, 0.03f, 0.045f, 0.92f);
        GUI.DrawTexture(rect, _whiteTex);

        GUI.color = new Color(0.22f, 0.28f, 0.34f, 1f);
        DrawRectOutline(rect, 1f);

        GUI.color = old;
    }

    private static void DrawWindowBackground(Rect panel)
    {
        Color old = GUI.color;

        GUI.color = new Color(0.04f, 0.055f, 0.075f, 0.96f);
        GUI.DrawTexture(panel, _whiteTex);

        GUI.color = new Color(0.22f, 0.28f, 0.34f, 1f);
        DrawRectOutline(panel, 2f);

        GUI.color = old;
    }

    private static void DrawRectOutline(Rect r, float thickness)
    {
        DrawRectOutline(r, thickness, GUI.color);
    }

    private static void DrawRectOutline(Rect r, float thickness, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), _whiteTex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), _whiteTex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), _whiteTex);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), _whiteTex);

        GUI.color = old;
    }

    private static void EnsureWhiteTexture()
    {
        if (_whiteTex == null)
            _whiteTex = Texture2D.whiteTexture;
    }
}
