using UnityEngine;
using MiniGames;

/// <summary>
/// Piloting overlay coordinator.
///
/// Authoritative piloting/navigation state remains outside this class.
/// The cartridge now coordinates focused presentation helpers instead of
/// owning the ocean, route, HUD, and every future maritime invention itself.
/// </summary>
public sealed class PilotingCartridge :
    IMiniGameCartridge,
    IOverlayRenderable
{
    private readonly BoatPilotingState _state;
    private readonly BoatPilotingSimulation _simulation;

    private readonly PilotingWaveRenderer _waveRenderer;
    private readonly PilotingRouteRenderer _routeRenderer;
    private readonly PilotingHudRenderer _hudRenderer;

    private MiniGameContext _ctx;
    private bool _requestedClose;
    private bool _debugMenuOpen;

    private Vector2 _cameraCenter;

    private float _visibleWorldHeight;
    private bool _zoomLocked;

    private const float DefaultVisibleWorldHeight = 90f;
    private const float DefaultTroughFlatFraction = 0.30f;
    private const float DefaultWaveTextureRefreshHz = 20f;

    private const float DesiredBoatScreenY01 = 0.72f;
    private const float CameraFollowSharpnessX = 0.75f;
    private const float CameraFollowSharpnessY = 2.75f;

    private const float BoatWorldWidth = 1.5f;
    private const float BoatWorldHeight = 2.6f;

    private static readonly Color BoatOutlineColor =
        new Color(
            0.20f,
            1f,
            0.34f,
            0.95f);

    public float VisibleWorldHeight =>
        _visibleWorldHeight;

    public bool ZoomLocked =>
        _zoomLocked;

    public PilotingCartridge(
        BoatPilotingState state,
        BoatPilotingSimulation simulation)
        : this(
            state,
            simulation,
            null,
            0f,
            false,
            DefaultVisibleWorldHeight,
            true,
            DefaultTroughFlatFraction,
            DefaultWaveTextureRefreshHz)
    {
    }

    public PilotingCartridge(
        BoatPilotingState state,
        BoatPilotingSimulation simulation,
        IWaveService waves)
        : this(
            state,
            simulation,
            waves,
            0f,
            false,
            DefaultVisibleWorldHeight,
            true,
            DefaultTroughFlatFraction,
            DefaultWaveTextureRefreshHz)
    {
    }

    public PilotingCartridge(
        BoatPilotingState state,
        BoatPilotingSimulation simulation,
        IWaveService waves,
        float physicalBoatWorldXAtOpen,
        bool hasPhysicalBoatWorldXAtOpen)
        : this(
            state,
            simulation,
            waves,
            physicalBoatWorldXAtOpen,
            hasPhysicalBoatWorldXAtOpen,
            DefaultVisibleWorldHeight,
            true,
            DefaultTroughFlatFraction,
            DefaultWaveTextureRefreshHz)
    {
    }

    public PilotingCartridge(
        BoatPilotingState state,
        BoatPilotingSimulation simulation,
        IWaveService waves,
        float physicalBoatWorldXAtOpen,
        bool hasPhysicalBoatWorldXAtOpen,
        float visibleWorldHeight,
        bool zoomLocked,
        float troughFlatFraction,
        float waveTextureRefreshHz)
    {
        _state =
            state;

        _simulation =
            simulation;

        _visibleWorldHeight =
            Mathf.Max(
                12f,
                visibleWorldHeight);

        _zoomLocked =
            zoomLocked;

        _waveRenderer =
            new PilotingWaveRenderer(
                waves,
                physicalBoatWorldXAtOpen,
                hasPhysicalBoatWorldXAtOpen,
                troughFlatFraction,
                waveTextureRefreshHz);

        _routeRenderer =
            new PilotingRouteRenderer();

        _hudRenderer =
            new PilotingHudRenderer();
    }

    public bool TrySetVisibleWorldHeight(
        float height)
    {
        if (_zoomLocked)
            return false;

        _visibleWorldHeight =
            Mathf.Max(
                12f,
                height);

        _waveRenderer.InvalidateTexture();

        return true;
    }

    public bool TrySetVisibleWorldSize(
        float width,
        float height)
    {
        return TrySetVisibleWorldHeight(
            height);
    }

    public void SetZoomLocked(
        bool locked)
    {
        _zoomLocked =
            locked;
    }

    public void Begin(
        MiniGameContext context)
    {
        _ctx =
            context ??
            new MiniGameContext();

        _requestedClose =
            false;

        _debugMenuOpen =
            false;

        Vector2 position =
            _state != null
                ? _state.NavigationPosition
                : Vector2.zero;

        float boatWorldY01FromBottom =
            1f -
            DesiredBoatScreenY01;

        float cameraYOffset =
            (0.5f -
             boatWorldY01FromBottom) *
            _visibleWorldHeight;

        _cameraCenter =
            new Vector2(
                position.x,
                position.y +
                cameraYOffset);

        _waveRenderer.Begin(
            _ctx.seed,
            position,
            _cameraCenter,
            _visibleWorldHeight);
    }

    public MiniGameResult Tick(
        float dt,
        MiniGameInput input)
    {
        if (_requestedClose)
            return Cancelled("Closed");

        if (_state == null ||
            _simulation == null)
        {
            return Cancelled(
                "Piloting state missing");
        }

        if (dt > 0f)
        {
            _waveRenderer.Tick(
                dt,
                _state.NavigationPosition,
                _cameraCenter,
                _visibleWorldHeight);

            UpdateCamera(
                dt);
        }

        return Running();
    }

    public MiniGameResult Cancel()
    {
        return Cancelled(
            "Left helm");
    }

    public MiniGameResult Interrupt(
        string reason)
    {
        return Cancelled(
            $"Interrupted: {reason}");
    }

    public void End()
    {
        _ctx =
            null;

        _waveRenderer.End();
    }

    public void DrawOverlayGUI(
        Rect panel)
    {
        const float pad =
            14f;

        GUI.Label(
            new Rect(
                panel.x + pad,
                panel.y + 10f,
                panel.width - 80f,
                24f),
            "PILOTING - AUTHORITATIVE STATE PROTOTYPE");

        if (GUI.Button(
                new Rect(
                    panel.xMax - 92f,
                    panel.y + 8f,
                    44f,
                    24f),
                _debugMenuOpen
                    ? "DBG*"
                    : "DBG"))
        {
            _debugMenuOpen =
                !_debugMenuOpen;
        }

        if (GUI.Button(
                new Rect(
                    panel.xMax - 40f,
                    panel.y + 8f,
                    28f,
                    24f),
                "X"))
        {
            _requestedClose =
                true;

            return;
        }

        if (_state == null ||
            _simulation == null)
        {
            GUI.Label(
                new Rect(
                    panel.x + pad,
                    panel.y + 44f,
                    panel.width - pad * 2f,
                    24f),
                "Piloting state unavailable.");

            return;
        }

        Rect playArea =
            new Rect(
                panel.x + pad,
                panel.y + 42f,
                panel.width - pad * 2f,
                panel.height - 162f);

        GUI.Box(
            playArea,
            GUIContent.none);

        PilotingViewProjection view =
            new PilotingViewProjection(
                playArea,
                _cameraCenter,
                _visibleWorldHeight);

        _waveRenderer.Draw(
            view);

        _routeRenderer.Draw(
            view,
            _simulation.RouteGuidance);

        DrawWaterReferenceGrid(
            view);

        DrawBoat(
            view);

        _hudRenderer.Draw(
            panel,
            playArea,
            view,
            _state,
            _simulation,
            _waveRenderer,
            _zoomLocked,
            _debugMenuOpen);
    }

    private void UpdateCamera(
        float dt)
    {
        Vector2 position =
            _state.NavigationPosition;

        float xAlpha =
            1f -
            Mathf.Exp(
                -CameraFollowSharpnessX *
                dt);

        _cameraCenter.x =
            Mathf.Lerp(
                _cameraCenter.x,
                position.x,
                xAlpha);

        float boatWorldY01FromBottom =
            1f -
            DesiredBoatScreenY01;

        float cameraYOffset =
            (0.5f -
             boatWorldY01FromBottom) *
            _visibleWorldHeight;

        float desiredCameraY =
            position.y +
            cameraYOffset;

        float yAlpha =
            1f -
            Mathf.Exp(
                -CameraFollowSharpnessY *
                dt);

        _cameraCenter.y =
            Mathf.Lerp(
                _cameraCenter.y,
                desiredCameraY,
                yAlpha);
    }

    private void DrawWaterReferenceGrid(
        PilotingViewProjection view)
    {
        Color previous =
            GUI.color;

        float spacing =
            GetReferenceGridSpacing();

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.09f);

        float firstY =
            Mathf.Floor(
                view.Bottom /
                spacing) *
            spacing;

        for (float y = firstY;
             y <= view.Top;
             y += spacing)
        {
            Vector2 a =
                view.WorldToPanel(
                    new Vector2(
                        view.Left,
                        y));

            Vector2 b =
                view.WorldToPanel(
                    new Vector2(
                        view.Right,
                        y));

            DrawAxisAlignedLine(
                a,
                b,
                2f);
        }

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                0.045f);

        float firstX =
            Mathf.Floor(
                view.Left /
                spacing) *
            spacing;

        for (float x = firstX;
             x <= view.Right;
             x += spacing)
        {
            Vector2 a =
                view.WorldToPanel(
                    new Vector2(
                        x,
                        view.Bottom));

            Vector2 b =
                view.WorldToPanel(
                    new Vector2(
                        x,
                        view.Top));

            DrawAxisAlignedLine(
                a,
                b,
                1f);
        }

        // The old hard-coded x=0 "course centerline" is intentionally gone.
        // The wake corridor is now the only route guidance.

        GUI.color =
            previous;
    }

    private float GetReferenceGridSpacing()
    {
        float rough =
            Mathf.Max(
                0.5f,
                _visibleWorldHeight /
                9f);

        float power =
            Mathf.Pow(
                10f,
                Mathf.Floor(
                    Mathf.Log10(
                        rough)));

        float normalized =
            rough /
            power;

        float nice;

        if (normalized <= 1f)
            nice = 1f;
        else if (normalized <= 2f)
            nice = 2f;
        else if (normalized <= 5f)
            nice = 5f;
        else
            nice = 10f;

        return
            nice *
            power;
    }

    private void DrawBoat(
        PilotingViewProjection view)
    {
        Vector2 center =
            view.WorldToPanel(
                _state.NavigationPosition);

        float worldUnitsPerPixel =
            Mathf.Max(
                0.0001f,
                view.WorldUnitsPerPixelY);

        float boatW =
            Mathf.Clamp(
                BoatWorldWidth /
                worldUnitsPerPixel,
                10f,
                54f);

        float boatH =
            Mathf.Clamp(
                BoatWorldHeight /
                worldUnitsPerPixel,
                16f,
                90f);

        Rect boatRect =
            new Rect(
                center.x -
                    boatW * 0.5f,
                center.y -
                    boatH * 0.5f,
                boatW,
                boatH);

        Matrix4x4 previousMatrix =
            GUI.matrix;

        GUIUtility.RotateAroundPivot(
            _state.HeadingDegrees,
            center);

        Color previousColor =
            GUI.color;

        float outline =
            Mathf.Clamp(
                Mathf.Min(
                    boatW,
                    boatH) *
                0.11f,
                1.5f,
                3f);

        GUI.color =
            BoatOutlineColor;

        GUI.DrawTexture(
            new Rect(
                boatRect.x - outline,
                boatRect.y - outline,
                boatRect.width +
                    outline * 2f,
                outline),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(
                boatRect.x - outline,
                boatRect.yMax,
                boatRect.width +
                    outline * 2f,
                outline),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(
                boatRect.x - outline,
                boatRect.y,
                outline,
                boatRect.height),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(
                boatRect.xMax,
                boatRect.y,
                outline,
                boatRect.height),
            Texture2D.whiteTexture);

        GUI.color =
            previousColor;

        GUI.Box(
            boatRect,
            "▲");

        GUI.matrix =
            previousMatrix;
    }

    private static void DrawAxisAlignedLine(
        Vector2 a,
        Vector2 b,
        float thickness)
    {
        float x =
            Mathf.Min(
                a.x,
                b.x);

        float y =
            Mathf.Min(
                a.y,
                b.y);

        float w =
            Mathf.Max(
                thickness,
                Mathf.Abs(
                    b.x -
                    a.x));

        float h =
            Mathf.Max(
                thickness,
                Mathf.Abs(
                    b.y -
                    a.y));

        GUI.DrawTexture(
            new Rect(
                x,
                y,
                w,
                h),
            Texture2D.whiteTexture);
    }

    private static MiniGameResult Running()
    {
        return new MiniGameResult
        {
            outcome =
                MiniGameOutcome.None,
            quality01 =
                1f,
            note =
                null,
            hasMeaningfulProgress =
                false
        };
    }

    private static MiniGameResult Cancelled(
        string note)
    {
        return new MiniGameResult
        {
            outcome =
                MiniGameOutcome.Cancelled,
            quality01 =
                0f,
            note =
                note,
            hasMeaningfulProgress =
                false
        };
    }
}