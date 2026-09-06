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

    private MiniGameContext _ctx;
    private bool _requestedClose;
    private bool _debugMenuOpen;

    private Vector2 _cameraCenter;
    private Vector2 _cameraCourseLookAhead;

    private float _visibleWorldHeight;
    private bool _zoomLocked;

    private const float DefaultVisibleWorldHeight = 90f;
    private const float DefaultTroughFlatFraction = 0.30f;
    private const float DefaultWaveTextureRefreshHz = 20f;

    private const float DesiredBoatScreenY01 = 0.72f;
    private const float CameraFollowSharpnessX = 0.75f;
    private const float CameraFollowSharpnessY = 2.75f;

    // Presentation-only course reveal. This is deliberately NOT shake.
    // The camera looks toward the boat's actual unwanted lateral trajectory
    // and anticipates strong yaw so the pilot can immediately read
    // "that is where I am actually going."
    private const float CameraCourseLookAheadSharpness = 3.5f;
    private const float CameraLateralRevealDeadzoneSpeed = 0.35f;
    private const float CameraLateralRevealFullSpeed = 6f;
    private const float CameraMaxLateralRevealFractionOfHeight = 0.22f;
    private const float CameraYawRevealDeadzoneDegreesPerSecond = 4f;
    private const float CameraYawRevealFullDegreesPerSecond = 42f;
    private const float CameraYawPredictionSeconds = 1.15f;
    private const float CameraMaxYawRevealFractionOfHeight = 0.10f;
    private const float CameraMaxCombinedRevealFractionOfHeight = 0.24f;

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
        PilotChairInteractable sourceStation = null)
    {
        _state =
            state;

        _simulation =
            simulation;

        _sourceStation =
            sourceStation;

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

        _cameraCourseLookAhead =
            Vector2.zero;

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
            _waveRenderer.Tick(
                dt,
                _state.NavigationPosition,
                _cameraCenter,
                _visibleWorldHeight);

            UpdateCamera(
                dt);

            _waterMotionRenderer.Tick(
                dt,
                _cameraCenter,
                _visibleWorldHeight);
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
        _waterMotionRenderer.End();
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

        _waterMotionRenderer.Draw(
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

    private void UpdateCamera(
        float dt)
    {
        Vector2 position =
            _state.NavigationPosition;

        // Keep the old follow behavior as the calm baseline. Course reveal is a
        // separate presentation offset so it can react much faster than the
        // deliberately lazy lateral camera follow.
        Vector2 baseCameraCenter =
            _cameraCenter -
            _cameraCourseLookAhead;

        Vector2 desiredCourseLookAhead =
            CalculateCourseRevealLookAhead();

        float courseAlpha =
            1f -
            Mathf.Exp(
                -CameraCourseLookAheadSharpness *
                dt);

        _cameraCourseLookAhead =
            Vector2.Lerp(
                _cameraCourseLookAhead,
                desiredCourseLookAhead,
                courseAlpha);

        float xAlpha =
            1f -
            Mathf.Exp(
                -CameraFollowSharpnessX *
                dt);

        baseCameraCenter.x =
            Mathf.Lerp(
                baseCameraCenter.x,
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

        baseCameraCenter.y =
            Mathf.Lerp(
                baseCameraCenter.y,
                desiredCameraY,
                yAlpha);

        _cameraCenter =
            baseCameraCenter +
            _cameraCourseLookAhead;
    }

    private Vector2 CalculateCourseRevealLookAhead()
    {
        if (_state == null)
            return Vector2.zero;

        Vector2 headingForward =
            HeadingToForward(
                _state.HeadingDegrees);

        Vector2 navigationVelocity =
            _state.NavigationVelocity;

        // Only the sideways component relative to the bow gets the large
        // trajectory reveal. Ordinary forward motion keeps the familiar framing.
        float alongHeadingSpeed =
            Vector2.Dot(
                navigationVelocity,
                headingForward);

        Vector2 lateralCourseVelocity =
            navigationVelocity -
            headingForward *
            alongHeadingSpeed;

        float lateralSpeed =
            lateralCourseVelocity.magnitude;

        float lateral01 =
            Mathf.InverseLerp(
                CameraLateralRevealDeadzoneSpeed,
                CameraLateralRevealFullSpeed,
                lateralSpeed);

        lateral01 =
            Mathf.SmoothStep(
                0f,
                1f,
                lateral01);

        float maxLateralReveal =
            _visibleWorldHeight *
            CameraMaxLateralRevealFractionOfHeight;

        Vector2 lateralReveal =
            lateralSpeed > 0.0001f
                ? lateralCourseVelocity.normalized *
                  maxLateralReveal *
                  lateral01
                : Vector2.zero;

        float angularVelocity =
            _state.AngularVelocityDegrees;

        float yaw01 =
            Mathf.InverseLerp(
                CameraYawRevealDeadzoneDegreesPerSecond,
                CameraYawRevealFullDegreesPerSecond,
                Mathf.Abs(
                    angularVelocity));

        yaw01 =
            Mathf.SmoothStep(
                0f,
                1f,
                yaw01);

        float predictedHeading =
            _state.HeadingDegrees +
            angularVelocity *
            CameraYawPredictionSeconds;

        Vector2 predictedForward =
            HeadingToForward(
                predictedHeading);

        Vector2 yawDirectionDelta =
            predictedForward -
            headingForward;

        float maxYawReveal =
            _visibleWorldHeight *
            CameraMaxYawRevealFractionOfHeight;

        Vector2 yawReveal =
            yawDirectionDelta *
            maxYawReveal *
            yaw01;

        Vector2 combined =
            lateralReveal +
            yawReveal;

        float maxCombinedReveal =
            _visibleWorldHeight *
            CameraMaxCombinedRevealFractionOfHeight;

        return Vector2.ClampMagnitude(
            combined,
            maxCombinedReveal);
    }

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