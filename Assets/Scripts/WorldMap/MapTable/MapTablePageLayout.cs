using UnityEngine;

/// <summary>
/// Shared three-column page layout used by both World Map and Star Chart.
/// Keeping the central viewport rectangle identical is part of the navigation mechanic:
/// a point under the registration reticle on one page stays under it on the other.
/// </summary>
public readonly struct MapTablePageLayout
{
    public readonly Rect Left;
    public readonly Rect Viewport;
    public readonly Rect Right;
    public readonly Rect Footer;

    public MapTablePageLayout(Rect left, Rect viewport, Rect right, Rect footer)
    {
        Left = left;
        Viewport = viewport;
        Right = right;
        Footer = footer;
    }

    public static MapTablePageLayout Compute(Rect panel)
    {
        const float pad = 14f;
        const float footerH = 24f;
        const float leftW = 240f;
        const float rightW = 275f;

        Rect content = new Rect(
            panel.x + pad,
            panel.y + 8f,
            panel.width - pad * 2f,
            panel.height - footerH - 22f);

        Rect left = new Rect(
            content.x,
            content.y,
            leftW,
            content.height);

        Rect viewport = new Rect(
            left.xMax + pad,
            content.y,
            Mathf.Max(80f, content.width - leftW - rightW - pad * 2f),
            content.height);

        Rect right = new Rect(
            viewport.xMax + pad,
            content.y,
            rightW,
            content.height);

        Rect footer = new Rect(
            panel.x + pad,
            panel.yMax - footerH - 4f,
            panel.width - pad * 2f,
            footerH);

        return new MapTablePageLayout(left, viewport, right, footer);
    }
}
