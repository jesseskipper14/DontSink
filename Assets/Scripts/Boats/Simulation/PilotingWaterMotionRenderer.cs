using System;
using UnityEngine;

/// <summary>
/// Presentation-only local water-motion cue for the piloting view.
///
/// Foam is generated procedurally from world/navigation-space grid cells rather
/// than stored as one finite particle list. That makes the field effectively
/// infinite in every direction and keeps the visible density consistent as the
/// camera moves or zooms.
///
/// The particles provide local motion/parallax only. They leave no trail and
/// encode no route history.
/// </summary>
public sealed class PilotingWaterMotionRenderer
{
    private int _seed;
    private float _presentationTime;

    // -----------------------------------------------------------------
    // TUNING
    // -----------------------------------------------------------------
    // Smaller cell size = more foam at normal zoom.
    // 12 world units gives roughly 150-220 visible flecks in the current
    // ~316 x 90 piloting view, depending on exact aspect/camera framing.
    private const float BaseCellWorldSize = 12f;

    // Safety cap for very large zoom-outs. Once the requested density would
    // exceed this, cell size grows automatically rather than issuing thousands
    // of IMGUI draw calls every frame.
    private const int MaxVisibleParticles = 520;

    // Slightly more alive than the previous 0.03-0.12 units/sec style drift.
    // These values describe a tiny looping wander around each particle's
    // deterministic world-space home position.
    private const float MinDriftRadiusWorld = 0.15f;
    private const float MaxDriftRadiusWorld = 0.55f;
    private const float MinDriftAngularSpeed = 0.22f;
    private const float MaxDriftAngularSpeed = 0.55f;

    private const float MinSizePixels = 1.2f;
    private const float MaxSizePixels = 2.8f;
    private const float MinStretch = 1f;
    private const float MaxStretch = 2.8f;
    private const float MinAlpha = 0.10f;
    private const float MaxAlpha = 0.27f;

    // Draw a little beyond the visible bounds so tiny drift never causes
    // particles to wink at the exact screen edge.
    private const float DrawOverscanCells = 1.25f;

    public void Begin(
        int seed,
        Vector2 cameraCenter,
        float visibleWorldHeight)
    {
        _seed =
            seed ^
            unchecked((int)0x46A0F1D3);

        _presentationTime =
            0f;
    }

    public void Tick(
        float dt,
        Vector2 cameraCenter,
        float visibleWorldHeight)
    {
        if (dt <= 0f)
            return;

        _presentationTime +=
            dt;
    }

    public void Draw(
        PilotingViewProjection view)
    {
        float viewWidth =
            Mathf.Max(
                1f,
                view.VisibleWorldWidth);

        float viewHeight =
            Mathf.Max(
                1f,
                view.VisibleWorldHeight);

        float viewArea =
            viewWidth *
            viewHeight;

        // Preserve normal world-space density until it would exceed the
        // performance cap. At extreme zoom-out, increase cell size just enough
        // to remain bounded.
        float adaptiveCellSize =
            Mathf.Max(
                BaseCellWorldSize,
                Mathf.Sqrt(
                    viewArea /
                    Mathf.Max(
                        1,
                        MaxVisibleParticles)));

        float overscan =
            adaptiveCellSize *
            DrawOverscanCells;

        int minCellX =
            Mathf.FloorToInt(
                (view.Left - overscan) /
                adaptiveCellSize);

        int maxCellX =
            Mathf.FloorToInt(
                (view.Right + overscan) /
                adaptiveCellSize);

        int minCellY =
            Mathf.FloorToInt(
                (view.Bottom - overscan) /
                adaptiveCellSize);

        int maxCellY =
            Mathf.FloorToInt(
                (view.Top + overscan) /
                adaptiveCellSize);

        Color previous =
            GUI.color;

        int drawn =
            0;

        for (int cellY = minCellY;
             cellY <= maxCellY;
             cellY++)
        {
            for (int cellX = minCellX;
                 cellX <= maxCellX;
                 cellX++)
            {
                if (drawn >=
                    MaxVisibleParticles)
                {
                    GUI.color =
                        previous;

                    return;
                }

                uint hash =
                    HashCell(
                        cellX,
                        cellY,
                        _seed);

                float jitterX =
                    Hash01(
                        hash ^
                        0xA2C79D15u);

                float jitterY =
                    Hash01(
                        hash ^
                        0xC13FA9A9u);

                Vector2 homeWorldPosition =
                    new Vector2(
                        (cellX +
                         Mathf.Lerp(
                             0.16f,
                             0.84f,
                             jitterX)) *
                        adaptiveCellSize,
                        (cellY +
                         Mathf.Lerp(
                             0.16f,
                             0.84f,
                             jitterY)) *
                        adaptiveCellSize);

                float driftAngle =
                    Hash01(
                        hash ^
                        0x91E10DA5u) *
                    Mathf.PI *
                    2f;

                float driftRadius =
                    Mathf.Lerp(
                        MinDriftRadiusWorld,
                        MaxDriftRadiusWorld,
                        Hash01(
                            hash ^
                            0x5D588B65u));

                float driftSpeed =
                    Mathf.Lerp(
                        MinDriftAngularSpeed,
                        MaxDriftAngularSpeed,
                        Hash01(
                            hash ^
                            0xB5297A4Du));

                float phase =
                    Hash01(
                        hash ^
                        0x68E31DA4u) *
                    Mathf.PI *
                    2f;

                float time =
                    _presentationTime *
                    driftSpeed +
                    phase;

                // Tiny looping wander around a stable world-space home point.
                // Because the home point is deterministic from cell coordinates,
                // the field exists everywhere without storing or spawning objects.
                Vector2 primaryDrift =
                    new Vector2(
                        Mathf.Cos(
                            time +
                            driftAngle),
                        Mathf.Sin(
                            time +
                            driftAngle)) *
                    driftRadius;

                Vector2 secondaryDrift =
                    new Vector2(
                        Mathf.Sin(
                            time *
                            0.73f +
                            phase),
                        Mathf.Cos(
                            time *
                            0.61f +
                            phase)) *
                    (driftRadius *
                     0.28f);

                Vector2 worldPosition =
                    homeWorldPosition +
                    primaryDrift +
                    secondaryDrift;

                if (worldPosition.x <
                        view.Left ||
                    worldPosition.x >
                        view.Right ||
                    worldPosition.y <
                        view.Bottom ||
                    worldPosition.y >
                        view.Top)
                {
                    continue;
                }

                Vector2 panelPosition =
                    view.WorldToPanel(
                        worldPosition);

                float sizePixels =
                    Mathf.Lerp(
                        MinSizePixels,
                        MaxSizePixels,
                        Hash01(
                            hash ^
                            0x7F4A7C15u));

                float stretch =
                    Mathf.Lerp(
                        MinStretch,
                        MaxStretch,
                        Hash01(
                            hash ^
                            0xD1B54A35u));

                float alpha =
                    Mathf.Lerp(
                        MinAlpha,
                        MaxAlpha,
                        Hash01(
                            hash ^
                            0x94D049BBu));

                float width =
                    Mathf.Max(
                        1f,
                        sizePixels *
                        stretch);

                float height =
                    Mathf.Max(
                        1f,
                        sizePixels);

                GUI.color =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha);

                GUI.DrawTexture(
                    new Rect(
                        panelPosition.x -
                            width * 0.5f,
                        panelPosition.y -
                            height * 0.5f,
                        width,
                        height),
                    Texture2D.whiteTexture);

                bool hasSatelliteSpeck =
                    Hash01(
                        hash ^
                        0x369DEA0Fu) <
                    0.28f;

                if (hasSatelliteSpeck)
                {
                    float satelliteSize =
                        Mathf.Max(
                            1f,
                            sizePixels *
                            0.55f);

                    GUI.color =
                        new Color(
                            1f,
                            1f,
                            1f,
                            alpha *
                            0.62f);

                    GUI.DrawTexture(
                        new Rect(
                            panelPosition.x +
                                width * 0.70f,
                            panelPosition.y -
                                height * 0.65f,
                            satelliteSize,
                            satelliteSize),
                        Texture2D.whiteTexture);
                }

                drawn++;
            }
        }

        GUI.color =
            previous;
    }

    public void End()
    {
        _presentationTime =
            0f;
    }

    private static uint HashCell(
        int x,
        int y,
        int seed)
    {
        unchecked
        {
            uint hash =
                (uint)seed;

            hash ^=
                (uint)x *
                0x9E3779B9u;

            hash =
                RotateLeft(
                    hash,
                    16);

            hash ^=
                (uint)y *
                0x85EBCA6Bu;

            hash ^=
                hash >>
                16;

            hash *=
                0x7FEB352Du;

            hash ^=
                hash >>
                15;

            hash *=
                0x846CA68Bu;

            hash ^=
                hash >>
                16;

            return hash;
        }
    }

    private static uint RotateLeft(
        uint value,
        int count)
    {
        return
            (value << count) |
            (value >>
             (32 - count));
    }

    private static float Hash01(
        uint value)
    {
        unchecked
        {
            value ^=
                value >>
                16;

            value *=
                0x7FEB352Du;

            value ^=
                value >>
                15;

            value *=
                0x846CA68Bu;

            value ^=
                value >>
                16;

            return
                (value &
                 0x00FFFFFFu) /
                16777215f;
        }
    }
}
