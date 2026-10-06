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
    private readonly PilotChairInteractable _sourceStation;

    private readonly PilotingWaveRenderer _waveRenderer;
    private readonly PilotingWaterMotionRenderer _waterMotionRenderer;
    private readonly PilotingRouteRenderer _routeRenderer;
    private readonly PilotingHudRenderer _hudRenderer;
    private readonly PilotingCompassRenderer _compassRenderer = new();

    private MiniGameContext _ctx;
    private bool _requestedClose;
    private bool _debugMenuOpen;

    private Vector2 _cameraCenter;
    private Texture2D _scopeMask;
    private GUIStyle _dockedLabel;

    // Match the helm's voyage-context distinction: no active voyage means docked.
    private static bool IsDocked => GameState.I != null && GameState.I.activeTravel == null;

    private float _visibleWorldHeight;
    private bool _zoomLocked;

    private const float DefaultVisibleWorldHeight = 90f;
    private const float DefaultTroughFlatFraction = 0.30f;
    private const float DefaultWaveTextureRefreshHz = 20f;

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
        float waveTextureRefreshHz,
        PilotChairInteractable sourceStation = null,
        PilotingViewscapeSettings viewscapeSettings = null)
    {
        _state =
            state;

        _simulation =
            simulation;

        _sourceStation =
            sourceStation;
        _compassRenderer.ConfigureViewscape(viewscapeSettings);

        _visibleWorldHeight =
            Mathf.Max(
                12f,
                visibleWorldHeight) * 4f;

        _zoomLocked =
            zoomLocked;

        _waveRenderer =
            new PilotingWaveRenderer(
                waves,
                physicalBoatWorldXAtOpen,
                hasPhysicalBoatWorldXAtOpen,
                troughFlatFraction,
                waveTextureRefreshHz);

        _waterMotionRenderer =
            new PilotingWaterMotionRenderer();

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
        _compassRenderer.Reset();
        _compassRenderer.Tick(_state, 0);

        _debugMenuOpen =
            false;

        Vector2 position =
            _state != null
                ? _state.NavigationPosition
                : Vector2.zero;

        _cameraCenter = position;

        _waveRenderer.Begin(
            _ctx.seed,
            position,
            _cameraCenter,
            _visibleWorldHeight);

        _waterMotionRenderer.Begin(
            _ctx.seed,
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
            UpdateCamera(dt);
            _compassRenderer.Tick(_state, dt);
            if (!IsDocked)
            {
                _waveRenderer.Tick(
                    dt,
                    _state.NavigationPosition,
                    _cameraCenter,
                    _visibleWorldHeight);

                _waterMotionRenderer.Tick(
                    dt,
                    _cameraCenter,
                    _visibleWorldHeight);
            }
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
        _compassRenderer.Reset();
        _ctx =
            null;

        _waveRenderer.End();
        _waterMotionRenderer.End();
        if (_scopeMask != null) Object.Destroy(_scopeMask);
        _scopeMask = null;
    }

    public void DrawOverlayGUI(
        Rect panel)
    {
        const float pad =
            14f;

        GUI.Label(
            new Rect(
                panel.x + pad,
                panel.y + 8f,
                panel.width - 80f,
                24f),
            "PILOTING");

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

        bool renderPilotingView =
            DrawHelmStatusLine(
                panel,
                pad);

        if (!renderPilotingView)
            return;

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

        if (_state == null ||
            _simulation == null)
        {
            GUI.Label(
                new Rect(
                    panel.x + pad,
                    panel.y + 64f,
                    panel.width - pad * 2f,
                    24f),
                "Piloting state unavailable.");

            return;
        }

        Rect playArea =
            new Rect(
                panel.x + pad,
                panel.y + 66f,
                panel.width - pad * 2f,
                panel.height - 186f);

        float sidebar = Mathf.Clamp(playArea.width*.19f,150,300);
        Rect left = new Rect(playArea.x,playArea.y,sidebar,playArea.height);
        Rect right = new Rect(playArea.xMax-sidebar,playArea.y,sidebar,playArea.height);
        float scopeSize = Mathf.Max(1,Mathf.Min(playArea.height-24,playArea.width-sidebar*2-24));
        Rect scope = new Rect(playArea.center.x-scopeSize*.5f,playArea.center.y-scopeSize*.5f,scopeSize,scopeSize);
        Rect approachArea = new Rect(scope.xMax+12f,playArea.y,Mathf.Max(0f,right.x-scope.xMax-24f),playArea.height);
        PilotingViewProjection view = new PilotingViewProjection(scope,_state.NavigationPosition,_visibleWorldHeight);
        GUI.BeginGroup(scope);
        var localView = new PilotingViewProjection(new Rect(0,0,scopeSize,scopeSize),_state.NavigationPosition,_visibleWorldHeight);
        if (IsDocked)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.035f,.065f,.085f,1f);
            GUI.DrawTexture(localView.PlayArea,Texture2D.whiteTexture);
            GUI.color = previous;
            _dockedLabel ??= new GUIStyle(GUI.skin.label)
                { alignment = TextAnchor.MiddleCenter, fontSize = 32, fontStyle = FontStyle.Bold };
            GUI.Label(localView.PlayArea,"DOCKED",_dockedLabel);
        }
        else
        {
            _waveRenderer.Draw(localView);
            _waterMotionRenderer.Draw(localView);
            _compassRenderer.DrawNearField(localView,_state);
            if (_debugMenuOpen)
            {
                _routeRenderer.Draw(localView,_simulation.RouteGuidance);
                DrawWaterReferenceGrid(localView);
            }
            DrawBoat(localView);
        }
        DrawScopeMask(localView.PlayArea);
        GUI.EndGroup();
        if (!IsDocked)
            GUI.Label(new Rect(scope.x,scope.yMax+3,scope.width,20),
                "Deep blue → light water → sand → land");
        _compassRenderer.Draw(right, _state, approachArea);
        _hudRenderer.DrawOrders(left,_state,_simulation,_sourceStation,_compassRenderer.GeographicHeading);

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

    /// <summary>
    /// Returns false only for an explicitly linked-but-offline helm. In that
    /// state the cartridge intentionally renders no piloting world/HUD below
    /// the red OFFLINE status line.
    /// </summary>
    private bool DrawHelmStatusLine(
        Rect panel,
        float pad)
    {
        if (_sourceStation == null)
            return true;

        PilotChairInteractable.PilotingHelmStatus status =
            _sourceStation.CurrentPilotingHelmStatus;

        bool hasControl =
            _sourceStation.HasPilotControlAuthority;

        string text;
        Color previous =
            GUI.color;

        switch (status)
        {
            case PilotChairInteractable.PilotingHelmStatus.Online:
                text = hasControl
                    ? "STATUS: ONLINE"
                    : "STATUS: ONLINE - NO CONTROL";
                break;

            case PilotChairInteractable.PilotingHelmStatus.Damaged:
                text = hasControl
                    ? "STATUS: DAMAGED"
                    : "STATUS: DAMAGED - NO CONTROL";

                GUI.color =
                    new Color(
                        1f,
                        0.72f,
                        0.2f,
                        1f);
                break;

            case PilotChairInteractable.PilotingHelmStatus.Offline:
                text =
                    "STATUS: OFFLINE";

                GUI.color =
                    Color.red;
                break;

            default:
                // An unlinked chair should never have opened this cartridge.
                text =
                    "STATUS: UNLINKED";
                break;
        }

        GUI.Label(
            new Rect(
                panel.x + pad,
                panel.y + 34f,
                panel.width - pad * 2f,
                24f),
            text);

        GUI.color =
            previous;

        return status !=
               PilotChairInteractable.PilotingHelmStatus.Offline;
    }

    private void UpdateCamera(float dt)
    { _cameraCenter = _state.NavigationPosition; }

    private static Vector2 HeadingToForward(
        float headingDegrees)
    {
        float radians =
            headingDegrees *
            Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(
                radians),
            Mathf.Cos(
                radians));
    }

    private void DrawScopeMask(Rect scope)
    {
        if (_scopeMask == null)
        {
            const int size=256;
            _scopeMask=new Texture2D(size,size,TextureFormat.RGBA32,false) { name="Piloting circular viewport mask",wrapMode=TextureWrapMode.Clamp };
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float radius=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
                pixels[y*size+x]=new Color(.055f,.065f,.08f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.987f,1,radius)));
            }
            _scopeMask.SetPixels(pixels);_scopeMask.Apply(false,true);
        }
        Color old=GUI.color;GUI.color=Color.white;GUI.DrawTexture(scope,_scopeMask);GUI.color=old;
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

