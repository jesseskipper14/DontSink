using UnityEngine;

/// <summary>
/// Immutable presentation-space projection shared by piloting renderers.
/// Converts navigation-world coordinates into the current overlay play area.
/// </summary>
public readonly struct PilotingViewProjection
{
    public readonly Rect PlayArea;
    public readonly Vector2 CameraCenter;
    public readonly float VisibleWorldHeight;

    public PilotingViewProjection(
        Rect playArea,
        Vector2 cameraCenter,
        float visibleWorldHeight)
    {
        PlayArea = playArea;
        CameraCenter = cameraCenter;
        VisibleWorldHeight =
            Mathf.Max(
                0.0001f,
                visibleWorldHeight);
    }

    public float VisibleWorldWidth
    {
        get
        {
            if (PlayArea.height <= 0.0001f)
                return VisibleWorldHeight;

            return
                VisibleWorldHeight *
                (PlayArea.width /
                 PlayArea.height);
        }
    }

    public float WorldUnitsPerPixelY
    {
        get
        {
            if (PlayArea.height <= 0.0001f)
                return 1f;

            return
                VisibleWorldHeight /
                PlayArea.height;
        }
    }

    public float Left =>
        CameraCenter.x -
        VisibleWorldWidth * 0.5f;

    public float Right =>
        CameraCenter.x +
        VisibleWorldWidth * 0.5f;

    public float Bottom =>
        CameraCenter.y -
        VisibleWorldHeight * 0.5f;

    public float Top =>
        CameraCenter.y +
        VisibleWorldHeight * 0.5f;

    public Vector2 WorldToPanel(
        Vector2 world)
    {
        float u =
            (world.x - Left) /
            VisibleWorldWidth;

        float v =
            (world.y - Bottom) /
            VisibleWorldHeight;

        return new Vector2(
            Mathf.Lerp(
                PlayArea.x,
                PlayArea.xMax,
                u),
            Mathf.Lerp(
                PlayArea.yMax,
                PlayArea.y,
                v));
    }
}