using UnityEngine;

public enum BoatIslandPhase { Hidden, Approaching, Alongside, Receding }

/// <summary>Local presentation only. Geography controls proximity; signed voyage travel controls alongshore drift.
/// Heading and camera never determine screen side. One silhouette retires before another starts.</summary>
public sealed class BoatIslandProjection
{
    public int LandmassId { get; private set; }
    public BoatIslandPhase Phase { get; private set; }
    public float Opacity { get; private set; }
    public float OffsetX { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    private float _closest = float.PositiveInfinity;
    private double _alongsideStrip;
    private bool _retiring;
    public void Retire() { if (LandmassId != 0) _retiring = true; }
    public void Reset()
    {
        LandmassId = 0; Phase = BoatIslandPhase.Hidden; Opacity = 0;
        OffsetX = Width = Height = 0; _closest = float.PositiveInfinity; _retiring = false;
    }

    public void Tick(BoatLandEncounter encounter, double strip, float dt, float widthPerMapUnit,
        float minimumWidth, float maximumWidth, float maximumHeight, float approachOffset,
        float recessionMargin, float parallax, float fadeSeconds, float smoothingSeconds)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0 || double.IsNaN(strip) || double.IsInfinity(strip)) return;
        dt = Mathf.Min(dt, .1f); // Resuming a paused/blocked frame never snaps a visible silhouette.
        float fadeStep = dt / Mathf.Max(.1f, fadeSeconds);
        if (LandmassId != 0 && (!encounter.HasLandmass || encounter.LandmassId != LandmassId)) _retiring = true;
        if (_retiring)
        {
            Opacity = Mathf.MoveTowards(Opacity, 0, fadeStep);
            if (Opacity == 0) Reset();
            return;
        }
        if (!encounter.HasLandmass) return;
        float distance = Mathf.Max(0, encounter.Distance);
        float normalized = Mathf.Clamp01(distance / Mathf.Max(.0001f, encounter.VisibilityRange));
        float proximity = 1 - normalized;
        float fullWidth = Mathf.Clamp(encounter.EquivalentDiameter * Mathf.Max(.1f, widthPerMapUnit),
            Mathf.Max(1, minimumWidth), Mathf.Max(minimumWidth, maximumWidth));
        float targetWidth = fullWidth * Mathf.Lerp(.25f, 1f, Mathf.Sqrt(proximity));
        float targetHeight = Mathf.Min(Mathf.Max(1, maximumHeight), fullWidth * .12f) * Mathf.Lerp(.25f, 1, proximity);
        if (LandmassId == 0)
        {
            LandmassId = encounter.LandmassId; _closest = distance;
            Phase = normalized <= .35f ? BoatIslandPhase.Alongside : BoatIslandPhase.Approaching;
            _alongsideStrip = strip;
            OffsetX = Phase == BoatIslandPhase.Alongside ? 0 : Mathf.Max(0, approachOffset) * normalized;
            Width = targetWidth; Height = targetHeight;
        }
        _closest = Mathf.Min(_closest, distance);
        if (Phase != BoatIslandPhase.Receding && distance > _closest + Mathf.Max(.1f, recessionMargin))
            Phase = BoatIslandPhase.Receding;
        else if (Phase == BoatIslandPhase.Approaching && normalized <= .35f)
        { Phase = BoatIslandPhase.Alongside; _alongsideStrip = strip; }
        float drift = (float)System.Math.Clamp((_alongsideStrip - strip) * Mathf.Max(0, parallax), -fullWidth * .3, fullWidth * .3);
        float targetX = Phase switch
        {
            BoatIslandPhase.Approaching => Mathf.Max(0, approachOffset) * normalized,
            BoatIslandPhase.Alongside => drift,
            _ => -Mathf.Max(0, approachOffset) * normalized + Mathf.Min(0, drift)
        };
        float blend = 1 - Mathf.Exp(-dt / Mathf.Max(.1f, smoothingSeconds));
        OffsetX = Mathf.Lerp(OffsetX, targetX, blend);
        Width = Mathf.Lerp(Width, targetWidth, blend); Height = Mathf.Lerp(Height, targetHeight, blend);
        float targetAlpha = Mathf.SmoothStep(0, 1, proximity / .2f);
        Opacity = Mathf.MoveTowards(Opacity, targetAlpha, fadeStep);
    }
}
