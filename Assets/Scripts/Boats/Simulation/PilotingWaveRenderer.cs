using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation-only virtual ocean renderer for the piloting overlay.
///
/// Wave CONDITIONS come from IWaveService. Individual virtual crests exist
/// only in navigation-space presentation so the top-down helm view can show
/// an ocean moving independently of the physical side-view BoatScene.
///
/// This class owns no authoritative boat or navigation state.
/// </summary>
public sealed class PilotingWaveRenderer
{
    private sealed class VirtualWaveBand
    {
        public float worldY;
        public float visualVariation;

        public float organicAnchorX;
        public float organicSlope;
        public float organicPhaseA;
        public float organicPhaseB;
        public float organicScale;

        public float organicDriftA;
        public float organicDriftB;
    }

    private readonly IWaveService _waves;

    private readonly float _physicalBoatWorldXAtOpen;
    private readonly bool _hasPhysicalBoatWorldXAtOpen;

    private readonly float _troughFlatFraction;
    private readonly float _waveTextureRefreshHz;

    private readonly List<VirtualWaveBand> _virtualWaves =
        new List<VirtualWaveBand>();

    private System.Random _waveRandom;

    private Vector2 _lastNavigationPosition;
    private Vector2 _lastCameraCenter;
    private float _lastVisibleWorldHeight = 90f;

    private float _organicPresentationTime;

    // Tracks live wave-condition changes while the helm remains open.
    // Existing virtual bands are continuously re-spaced around the boat rather
    // than preserving the spacing that happened to exist when Begin() ran.
    private float _lastWaveSpacing;

    private Texture2D _waveFieldTexture;
    private Color32[] _waveFieldPixels;
    private float[] _columnWaveCrestYs;
    private int _waveFieldTextureHeight;

    private bool _waveFieldTextureValid;
    private float _nextWaveTextureBuildTime;

    private float _lastWaveTextureBuildMs;
    private float _measuredWaveTextureBuildHz;
    private int _waveTextureBuildsSinceSample;
    private float _waveTextureSampleStartTime;

    private const int PhysicalCrestSampleCount = 48;

    private const int WaveFieldTextureWidth = 256;
    private const int WaveFieldTextureMinHeight = 64;
    private const int WaveFieldTextureMaxHeight = 192;

    private const float WaveAmplitudeForMaxVisual = 10f;

    public static readonly Color OceanBackgroundColor =
        new Color(
            0.0025f,
            0.009f,
            0.018f,
            1f);

    private static readonly Color WaveMaxPeakColor =
        new Color(
            0.72f,
            0.91f,
            1.00f,
            1f);

    public float Amplitude =>
        _waves != null
            ? _waves.Amplitude
            : 0f;

    public float Spacing =>
        GetWaveSpacing();

    public float WorldSpeed =>
        GetWaveWorldSpeed();

    public float VisualAmplitude01 =>
        GetWaveVisualAmplitude01();

    public float LastTextureBuildMs =>
        _lastWaveTextureBuildMs;

    public float MeasuredTextureBuildHz =>
        _measuredWaveTextureBuildHz;

    public float TroughFlatFraction =>
        _troughFlatFraction;

    public PilotingWaveRenderer(
        IWaveService waves,
        float physicalBoatWorldXAtOpen,
        bool hasPhysicalBoatWorldXAtOpen,
        float troughFlatFraction,
        float waveTextureRefreshHz)
    {
        _waves = waves;

        _physicalBoatWorldXAtOpen =
            physicalBoatWorldXAtOpen;

        _hasPhysicalBoatWorldXAtOpen =
            hasPhysicalBoatWorldXAtOpen;

        _troughFlatFraction =
            Mathf.Clamp(
                troughFlatFraction,
                0f,
                0.75f);

        _waveTextureRefreshHz =
            Mathf.Clamp(
                waveTextureRefreshHz,
                5f,
                60f);
    }

    public void Begin(
        int seed,
        Vector2 navigationPosition,
        Vector2 cameraCenter,
        float visibleWorldHeight)
    {
        _organicPresentationTime = 0f;

        _waveFieldTextureValid = false;
        _nextWaveTextureBuildTime = 0f;
        _lastWaveTextureBuildMs = 0f;
        _measuredWaveTextureBuildHz = 0f;
        _waveTextureBuildsSinceSample = 0;
        _waveTextureSampleStartTime =
            Time.unscaledTime;

        _lastNavigationPosition =
            navigationPosition;

        _lastCameraCenter =
            cameraCenter;

        _lastVisibleWorldHeight =
            Mathf.Max(
                12f,
                visibleWorldHeight);

        InitializeVirtualWaveSet(
            seed,
            navigationPosition);

        _lastWaveSpacing =
            GetWaveSpacing();
    }

    public void Tick(
        float dt,
        Vector2 navigationPosition,
        Vector2 cameraCenter,
        float visibleWorldHeight)
    {
        _lastNavigationPosition =
            navigationPosition;

        _lastCameraCenter =
            cameraCenter;

        _lastVisibleWorldHeight =
            Mathf.Max(
                12f,
                visibleWorldHeight);

        if (_waves == null ||
            dt <= 0f)
        {
            return;
        }

        _organicPresentationTime +=
            dt;

        float currentSpacing =
            GetWaveSpacing();

        ApplyLiveSpacingChange(
            currentSpacing,
            navigationPosition.y);

        float waveWorldSpeed =
            GetWaveWorldSpeed();

        if (waveWorldSpeed > 0f)
        {
            for (int i = 0;
                 i < _virtualWaves.Count;
                 i++)
            {
                _virtualWaves[i].worldY -=
                    waveWorldSpeed *
                    dt;
            }
        }

        MaintainVirtualWaveCoverage();

        _lastWaveSpacing =
            currentSpacing;
    }

    /// <summary>
    /// Weather may change frequency while the helm is already open. Re-space the
    /// existing virtual bands around the current boat position so the visible sea
    /// follows the live IWaveService instead of preserving Begin()-time spacing.
    ///
    /// Because WaveManager transitions frequency gradually, this rescale is also
    /// gradual and does not require throwing away/re-seeding the visible ocean.
    /// </summary>
    private void ApplyLiveSpacingChange(
        float currentSpacing,
        float anchorY)
    {
        if (currentSpacing <= 0.0001f)
        {
            _virtualWaves.Clear();
            return;
        }

        if (_lastWaveSpacing <= 0.0001f)
        {
            if (_virtualWaves.Count == 0)
            {
                AddVirtualWave(
                    anchorY +
                    currentSpacing);
            }

            return;
        }

        float ratio =
            currentSpacing /
            _lastWaveSpacing;

        if (Mathf.Abs(
                ratio -
                1f) <=
            0.0001f)
        {
            return;
        }

        for (int i = 0;
             i < _virtualWaves.Count;
             i++)
        {
            VirtualWaveBand wave =
                _virtualWaves[i];

            float offsetFromBoat =
                wave.worldY -
                anchorY;

            wave.worldY =
                anchorY +
                offsetFromBoat *
                ratio;
        }

        InvalidateTexture();
    }

    public void Draw(
        PilotingViewProjection view)
    {
        DrawOceanBackground(
            view.PlayArea);

        if (_virtualWaves.Count == 0)
            return;

        float spacing =
            GetWaveSpacing();

        if (spacing <= 0f)
            return;

        DrawContinuousWaveField(
            view,
            spacing);
    }

    public void InvalidateTexture()
    {
        _waveFieldTextureValid =
            false;

        _nextWaveTextureBuildTime =
            0f;
    }

    public void End()
    {
        if (_waveFieldTexture != null)
        {
            Object.Destroy(
                _waveFieldTexture);

            _waveFieldTexture = null;
        }

        _waveFieldPixels = null;
        _columnWaveCrestYs = null;
        _waveFieldTextureHeight = 0;
        _waveFieldTextureValid = false;

        _virtualWaves.Clear();
        _lastWaveSpacing = 0f;
    }

    private void InitializeVirtualWaveSet(
        int seed,
        Vector2 boatPosition)
    {
        _virtualWaves.Clear();

        _waveRandom =
            new System.Random(
                seed != 0
                    ? seed
                    : 17357);

        float spacing =
            GetWaveSpacing();

        if (spacing <= 0f)
            return;

        float firstDistanceAhead =
            GetApproximateNextPhysicalCrestDistance(
                spacing);

        float firstWaveY =
            boatPosition.y +
            firstDistanceAhead;

        float bottom =
            _lastCameraCenter.y -
            _lastVisibleWorldHeight * 0.5f;

        float top =
            _lastCameraCenter.y +
            _lastVisibleWorldHeight * 0.5f;

        float y =
            firstWaveY;

        while (y - spacing >=
               bottom - spacing)
        {
            y -= spacing;
        }

        while (y <=
               top + spacing * 1.5f)
        {
            AddVirtualWave(y);
            y += spacing;
        }
    }

    private void MaintainVirtualWaveCoverage()
    {
        float spacing =
            GetWaveSpacing();

        if (spacing <= 0f)
        {
            _virtualWaves.Clear();
            return;
        }

        float bottom =
            _lastCameraCenter.y -
            _lastVisibleWorldHeight * 0.5f -
            spacing;

        float top =
            _lastCameraCenter.y +
            _lastVisibleWorldHeight * 0.5f +
            spacing * 1.5f;

        for (int i =
                 _virtualWaves.Count - 1;
             i >= 0;
             i--)
        {
            if (_virtualWaves[i].worldY <
                bottom)
            {
                _virtualWaves.RemoveAt(i);
            }
        }

        float highestY =
            _lastNavigationPosition.y;

        for (int i = 0;
             i < _virtualWaves.Count;
             i++)
        {
            if (_virtualWaves[i].worldY >
                highestY)
            {
                highestY =
                    _virtualWaves[i].worldY;
            }
        }

        if (_virtualWaves.Count == 0)
        {
            highestY =
                _lastNavigationPosition.y +
                spacing;

            AddVirtualWave(
                highestY);
        }

        while (highestY <
               top)
        {
            highestY +=
                spacing;

            AddVirtualWave(
                highestY);
        }
    }

    private void AddVirtualWave(
        float worldY)
    {
        if (_waveRandom == null)
        {
            _waveRandom =
                new System.Random(17357);
        }

        float visualVariation =
            Mathf.Lerp(
                0.90f,
                1.08f,
                (float)_waveRandom.NextDouble());

        float slopeDegrees =
            Mathf.Lerp(
                -2.0f,
                2.0f,
                (float)_waveRandom.NextDouble());

        float organicSlope =
            Mathf.Tan(
                slopeDegrees *
                Mathf.Deg2Rad);

        float organicPhaseA =
            Mathf.Lerp(
                0f,
                Mathf.PI * 2f,
                (float)_waveRandom.NextDouble());

        float organicPhaseB =
            Mathf.Lerp(
                0f,
                Mathf.PI * 2f,
                (float)_waveRandom.NextDouble());

        float organicScale =
            Mathf.Lerp(
                0.75f,
                1.20f,
                (float)_waveRandom.NextDouble());

        float organicDriftA =
            Mathf.Lerp(
                -0.018f,
                0.018f,
                (float)_waveRandom.NextDouble());

        float organicDriftB =
            Mathf.Lerp(
                -0.031f,
                0.031f,
                (float)_waveRandom.NextDouble());

        _virtualWaves.Add(
            new VirtualWaveBand
            {
                worldY = worldY,
                visualVariation =
                    visualVariation,

                // Anchor organic slope around the boat's current lateral
                // neighborhood. Wave presentation no longer depends on targets
                // or on the route renderer.
                organicAnchorX =
                    _lastNavigationPosition.x,

                organicSlope =
                    organicSlope,
                organicPhaseA =
                    organicPhaseA,
                organicPhaseB =
                    organicPhaseB,
                organicScale =
                    organicScale,
                organicDriftA =
                    organicDriftA,
                organicDriftB =
                    organicDriftB
            });
    }

    private float GetApproximateNextPhysicalCrestDistance(
        float spacing)
    {
        if (_waves == null ||
            !_hasPhysicalBoatWorldXAtOpen ||
            spacing <= 0f)
        {
            return spacing * 0.65f;
        }

        float startX =
            _physicalBoatWorldXAtOpen;

        float bestX =
            startX;

        float bestHeight =
            float.NegativeInfinity;

        for (int i = 0;
             i <= PhysicalCrestSampleCount;
             i++)
        {
            float t =
                i /
                (float)PhysicalCrestSampleCount;

            float sampleX =
                startX +
                spacing *
                t;

            float height =
                _waves.SampleHeightAtWorldXWrapped(
                    sampleX);

            if (height >
                bestHeight)
            {
                bestHeight =
                    height;

                bestX =
                    sampleX;
            }
        }

        return Mathf.Clamp(
            bestX - startX,
            0f,
            spacing);
    }

    private float GetWaveSpacing()
    {
        if (_waves == null)
            return 0f;

        float frequency =
            Mathf.Max(
                0f,
                _waves.Frequency);

        if (frequency <= 0.0001f)
            return 0f;

        return
            2f /
            frequency;
    }

    private float GetWaveWorldSpeed()
    {
        if (_waves == null)
            return 0f;

        float frequency =
            Mathf.Max(
                0f,
                _waves.Frequency);

        float phaseSpeed =
            Mathf.Max(
                0f,
                _waves.Speed);

        if (frequency <= 0.0001f ||
            phaseSpeed <= 0f)
        {
            return 0f;
        }

        return
            phaseSpeed /
            (Mathf.PI * frequency);
    }

    private float GetWaveVisualAmplitude01()
    {
        if (_waves == null)
            return 0f;

        return Mathf.Clamp01(
            _waves.Amplitude /
            WaveAmplitudeForMaxVisual);
    }

    private static void DrawOceanBackground(
        Rect playArea)
    {
        Color previous =
            GUI.color;

        GUI.color =
            OceanBackgroundColor;

        GUI.DrawTexture(
            playArea,
            Texture2D.whiteTexture);

        GUI.color =
            previous;
    }

    private void EnsureWaveFieldTexture(
        Rect playArea)
    {
        float aspect =
            playArea.width > 0.0001f
                ? playArea.height /
                  playArea.width
                : 0.5f;

        int desiredHeight =
            Mathf.Clamp(
                Mathf.RoundToInt(
                    WaveFieldTextureWidth *
                    aspect),
                WaveFieldTextureMinHeight,
                WaveFieldTextureMaxHeight);

        bool needsRebuild =
            _waveFieldTexture == null ||
            _waveFieldTexture.width !=
                WaveFieldTextureWidth ||
            _waveFieldTextureHeight !=
                desiredHeight;

        if (!needsRebuild)
            return;

        if (_waveFieldTexture != null)
        {
            Object.Destroy(
                _waveFieldTexture);
        }

        _waveFieldTextureHeight =
            desiredHeight;

        _waveFieldTexture =
            new Texture2D(
                WaveFieldTextureWidth,
                _waveFieldTextureHeight,
                TextureFormat.RGBA32,
                false);

        _waveFieldTexture.name =
            "PilotingWaveField_Runtime";

        _waveFieldTexture.wrapMode =
            TextureWrapMode.Clamp;

        _waveFieldTexture.filterMode =
            FilterMode.Bilinear;

        _waveFieldPixels =
            new Color32[
                WaveFieldTextureWidth *
                _waveFieldTextureHeight];

        _waveFieldTextureValid =
            false;
    }

    private void DrawContinuousWaveField(
        PilotingViewProjection view,
        float spacing)
    {
        Event currentEvent =
            Event.current;

        if (currentEvent != null &&
            currentEvent.type !=
                EventType.Repaint)
        {
            return;
        }

        EnsureWaveFieldTexture(
            view.PlayArea);

        if (_waveFieldTexture == null ||
            _waveFieldPixels == null)
        {
            return;
        }

        float now =
            Time.unscaledTime;

        bool refreshDue =
            !_waveFieldTextureValid ||
            now >=
                _nextWaveTextureBuildTime;

        if (refreshDue)
        {
            RebuildWaveFieldTexture(
                view,
                spacing);

            float interval =
                1f /
                Mathf.Max(
                    1f,
                    _waveTextureRefreshHz);

            _nextWaveTextureBuildTime =
                now +
                interval;
        }

        if (!_waveFieldTextureValid)
            return;

        Color previous =
            GUI.color;

        GUI.color =
            Color.white;

        GUI.DrawTexture(
            view.PlayArea,
            _waveFieldTexture,
            ScaleMode.StretchToFill,
            false);

        GUI.color =
            previous;
    }

    private void RebuildWaveFieldTexture(
        PilotingViewProjection view,
        float spacing)
    {
        if (_waveFieldTexture == null ||
            _waveFieldPixels == null)
        {
            return;
        }

        float halfSpacing =
            spacing * 0.5f;

        if (halfSpacing <= 0.0001f)
            return;

        double startedAt =
            Time.realtimeSinceStartupAsDouble;

        float amplitude01 =
            GetWaveVisualAmplitude01();

        int width =
            WaveFieldTextureWidth;

        int height =
            _waveFieldTextureHeight;

        int waveCount =
            _virtualWaves.Count;

        if (_columnWaveCrestYs == null ||
            _columnWaveCrestYs.Length <
                waveCount)
        {
            _columnWaveCrestYs =
                new float[
                    Mathf.Max(
                        1,
                        waveCount)];
        }

        for (int px = 0;
             px < width;
             px++)
        {
            float u =
                (px + 0.5f) /
                width;

            float worldX =
                view.Left +
                u *
                view.VisibleWorldWidth;

            for (int i = 0;
                 i < waveCount;
                 i++)
            {
                VirtualWaveBand wave =
                    _virtualWaves[i];

                _columnWaveCrestYs[i] =
                    wave.worldY +
                    GetWaveOrganicYOffset(
                        wave,
                        worldX,
                        spacing);
            }

            for (int py = 0;
                 py < height;
                 py++)
            {
                float v =
                    (py + 0.5f) /
                    height;

                float worldY =
                    view.Bottom +
                    v *
                    view.VisibleWorldHeight;

                float strongest =
                    0f;

                for (int i = 0;
                     i < waveCount;
                     i++)
                {
                    float distanceFromCrest =
                        Mathf.Abs(
                            worldY -
                            _columnWaveCrestYs[i]);

                    if (distanceFromCrest >=
                        halfSpacing)
                    {
                        continue;
                    }

                    float progressTroughToCrest =
                        1f -
                        Mathf.Clamp01(
                            distanceFromCrest /
                            halfSpacing);

                    float spatialIntensity;

                    if (progressTroughToCrest <=
                        _troughFlatFraction)
                    {
                        spatialIntensity =
                            0f;
                    }
                    else
                    {
                        spatialIntensity =
                            (progressTroughToCrest -
                             _troughFlatFraction) /
                            Mathf.Max(
                                0.0001f,
                                1f -
                                _troughFlatFraction);
                    }

                    float combined =
                        Mathf.Clamp01(
                            spatialIntensity *
                            amplitude01 *
                            _virtualWaves[i].
                                visualVariation);

                    if (combined >
                        strongest)
                    {
                        strongest =
                            combined;
                    }
                }

                Color color =
                    Color.Lerp(
                        OceanBackgroundColor,
                        WaveMaxPeakColor,
                        strongest);

                _waveFieldPixels[
                    py * width +
                    px] =
                    (Color32)color;
            }
        }

        _waveFieldTexture.SetPixels32(
            _waveFieldPixels);

        _waveFieldTexture.Apply(
            false,
            false);

        _waveFieldTextureValid =
            true;

        double finishedAt =
            Time.realtimeSinceStartupAsDouble;

        _lastWaveTextureBuildMs =
            (float)(
                (finishedAt -
                 startedAt) *
                1000.0);

        RecordWaveTextureBuild();
    }

    private void RecordWaveTextureBuild()
    {
        _waveTextureBuildsSinceSample++;

        float now =
            Time.unscaledTime;

        float elapsed =
            now -
            _waveTextureSampleStartTime;

        if (elapsed < 1f)
            return;

        _measuredWaveTextureBuildHz =
            _waveTextureBuildsSinceSample /
            Mathf.Max(
                0.0001f,
                elapsed);

        _waveTextureBuildsSinceSample =
            0;

        _waveTextureSampleStartTime =
            now;
    }

    private float GetWaveOrganicYOffset(
        VirtualWaveBand wave,
        float worldX,
        float spacing)
    {
        if (wave == null)
            return 0f;

        float localX =
            worldX -
            wave.organicAnchorX;

        float slopeOffset =
            localX *
            wave.organicSlope;

        float broadCurve =
            Mathf.Sin(
                worldX * 0.035f +
                wave.organicPhaseA +
                _organicPresentationTime *
                wave.organicDriftA) *
            _lastVisibleWorldHeight *
            0.022f *
            wave.organicScale;

        float secondaryCurve =
            Mathf.Sin(
                worldX * 0.085f +
                wave.organicPhaseB +
                _organicPresentationTime *
                wave.organicDriftB) *
            _lastVisibleWorldHeight *
            0.009f *
            wave.organicScale;

        float maxOffset =
            _lastVisibleWorldHeight *
            0.065f;

        return Mathf.Clamp(
            slopeOffset +
            broadCurve +
            secondaryCurve,
            -maxOffset,
            maxOffset);
    }
}