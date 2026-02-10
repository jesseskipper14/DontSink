using UnityEngine;

namespace MiniGames
{
    public sealed class MiniGameOverlayView : MonoBehaviour
    {
        [Header("Refs")]
        public MiniGameOverlayHost host;

        [Header("Look")]
        [Range(0f, 0.9f)] public float dimAlpha = 0.55f;
        public float panelHalfSize = 0.38f; // normalized viewport units
        public float dotSize = 10f;

        private Texture2D _white;

        private void Awake()
        {
            if (host == null) host = GetComponent<MiniGameOverlayHost>();
            if (host == null) host = FindFirstObjectByType<MiniGameOverlayHost>();

            _white = Texture2D.whiteTexture;
        }

        private void OnGUI()
        {
            if (host == null || !host.IsOpen)
                return;

            // Fullscreen dim
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, dimAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = prev;

            // Panel rect (centered)
            var cx = Screen.width * 0.5f;
            var cy = Screen.height * 0.5f;
            var halfW = Screen.width * panelHalfSize;
            var halfH = Screen.height * panelHalfSize;

            var panel = new Rect(cx - halfW, cy - halfH, halfW * 2f, halfH * 2f);

            // Panel background
            GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.9f);
            GUI.DrawTexture(panel, _white);
            GUI.color = Color.white;

            // Border
            DrawRectOutline(panel, 2f, new Color(1f, 1f, 1f, 0.15f));

            // If the active cartridge provides a render hook, let it draw interactive UI.
            var renderable = host.ActiveCartridge as IOverlayRenderable;
            if (renderable != null)
            {
                renderable.DrawOverlayGUI(panel);
                return; // don't draw StarObs debug stuff on top
            }

            // StarObs debug (supports: target + mouseDot + moveDot + overlay shift/tilt + progress/quality)
            Vector2? targetN = null;
            Vector2? mouseDotN = null;
            Vector2? moveDotN = null;
            Vector2 overlayShiftN = Vector2.zero;
            float overlayTiltDeg = 0f;
            float progress = 0f;
            float avgQ = 0f;

            var star = host.DebugDrawable as IStarObsDebugDrawable;
            if (star != null)
            {
                targetN = star.GetTargetNorm01();
                mouseDotN = star.GetMouseDotNorm01();
                moveDotN = star.GetMoveDotNorm01();
                overlayShiftN = star.GetOverlayShiftNorm01();
                overlayTiltDeg = star.GetOverlayTiltDeg();
                progress = star.GetProgress01();
                avgQ = star.GetAverageQuality01();
            }
            else
            {
                // fallback to legacy debug drawable (target + progress/quality)
                var drawable = host.DebugDrawable;
                if (drawable != null)
                {
                    targetN = drawable.GetTargetNorm01();
                    progress = drawable.GetProgress01();
                    avgQ = drawable.GetAverageQuality01();
                }

                // fallback mouse dot = actual mouse
                var mouseN = ScreenToNorm(Event.current.mousePosition, panel);
                mouseDotN = mouseN;
            }

            // Apply overlay shift/tilt visually (does not affect simulation)
            Vector2 panelCenter = new Vector2(panel.center.x, panel.center.y);
            Vector2 shiftPx = new Vector2(overlayShiftN.x * (panel.width * 0.10f), overlayShiftN.y * (panel.height * 0.10f));
            float tiltRad = overlayTiltDeg * Mathf.Deg2Rad;

            Vector2 TransformPoint(Vector2 p)
            {
                // shift then rotate around center
                p += shiftPx;
                Vector2 d = p - panelCenter;
                float cs = Mathf.Cos(tiltRad);
                float sn = Mathf.Sin(tiltRad);
                Vector2 r = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                return panelCenter + r;
            }

            // Draw target dot (fixed object)
            if (targetN.HasValue)
            {
                var tp = NormToPanelPoint(panel, targetN.Value);
                tp = TransformPoint(tp);
                DrawDot(tp, dotSize, new Color(1f, 0.9f, 0.25f, 0.95f));
            }

            // Draw mouse-controlled dot (player-controlled)
            if (mouseDotN.HasValue)
            {
                var mp = NormToPanelPoint(panel, mouseDotN.Value);
                mp = TransformPoint(mp);
                DrawDot(mp, dotSize, new Color(0.35f, 0.95f, 1f, 0.95f));
            }

            // Draw WASD-controlled dot (player-controlled)
            if (moveDotN.HasValue)
            {
                var wp = NormToPanelPoint(panel, moveDotN.Value);
                wp = TransformPoint(wp);
                DrawDot(wp, dotSize, new Color(1f, 0.35f, 0.85f, 0.95f));
            }

            // Progress bar (also visually shifted/tilted a bit)
            var bar = new Rect(panel.x + 14, panel.yMax - 26, panel.width - 28, 10);
            Vector2 barA = TransformPoint(new Vector2(bar.xMin, bar.center.y));
            Vector2 barB = TransformPoint(new Vector2(bar.xMax, bar.center.y));
            float barLen = Vector2.Distance(barA, barB);
            float fillLen = barLen * Mathf.Clamp01(progress);

            // draw bar bg as a simple axis-aligned rect (good enough)
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(bar, _white);
            GUI.color = Color.white;

            var fill = new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height);
            GUI.color = new Color(0.35f, 0.95f, 1f, 0.85f);
            GUI.DrawTexture(fill, _white);
            GUI.color = Color.white;

            // Text
            GUI.Label(new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 22),
                "STAR OBSERVATION (debug overlay)");

            GUI.Label(new Rect(panel.x + 14, panel.y + 32, panel.width - 28, 22),
                $"Hold LMB near target. Esc to exit. Progress: {progress:0.00}  AvgQ: {avgQ:0.00}");
        }

        private static Vector2 NormToPanelPoint(Rect panel, Vector2 norm01)
        {
            float u = (norm01.x * 0.5f) + 0.5f;
            float v = (norm01.y * 0.5f) + 0.5f;

            return new Vector2(
                Mathf.Lerp(panel.x, panel.xMax, u),
                Mathf.Lerp(panel.yMax, panel.y, v)
            );
        }

        private static Vector2 ScreenToNorm(Vector2 screenPx, Rect panel)
        {
            float u = Mathf.InverseLerp(panel.x, panel.xMax, screenPx.x);
            float v = Mathf.InverseLerp(panel.y, panel.yMax, screenPx.y);

            float x = (u * 2f) - 1f;
            float y = (v * 2f) - 1f;
            return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        }

        private void DrawDot(Vector2 center, float size, Color c)
        {
            var r = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = Color.white;
        }

        private void DrawRectOutline(Rect r, float t, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _white);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _white);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _white);
            GUI.color = Color.white;
        }
    }
}
