using System;
using UnityEngine;

/// <summary>Decorative nautical art. Inputs deliberately exclude geography, nodes, POIs and celestial truth.</summary>
public static class WorldMapMythicShroud
{
    private static Texture2D texture;
    public static Texture2D Texture
    {
        get
        {
            if (texture != null) return texture;
            const int size = 512;
            var random = new System.Random(731947); // Independent decorative seed, never a world seed.
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float grain = (float)random.NextDouble() * .035f;
                pixels[y * size + x] = new Color(.78f + grain, .72f + grain, .56f + grain, 1);
            }
            Color32 ink = new Color(.29f, .35f, .32f, 1);
            void Dot(int x, int y, int r = 1)
            {
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    if (x + dx >= 0 && x + dx < size && y + dy >= 0 && y + dy < size && dx * dx + dy * dy <= r * r)
                        pixels[(y + dy) * size + x + dx] = ink;
            }
            void Line(float ax, float ay, float bx, float by)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)));
                for (int i = 0; i <= steps; i++) { float t = steps == 0 ? 0 : i / (float)steps; Dot(Mathf.RoundToInt(Mathf.Lerp(ax, bx, t)), Mathf.RoundToInt(Mathf.Lerp(ay, by, t))); }
            }
            // Repeating currents, wandering sea serpents, and compass roses are decorative only.
            for (int y = 12; y < size; y += 24) for (int x = 0; x < size; x++)
                if (x % 64 < 42) Dot(x, y + Mathf.RoundToInt(3 * Mathf.Sin(x * .16f)), 0);
            for (int k = 0; k < 9; k++)
            {
                int cx = random.Next(55, size - 55), cy = random.Next(40, size - 40);
                if (k % 3 == 0)
                {
                    for (int a = 0; a < 360; a++) Dot(cx + Mathf.RoundToInt(24 * Mathf.Cos(a * Mathf.Deg2Rad)), cy + Mathf.RoundToInt(24 * Mathf.Sin(a * Mathf.Deg2Rad)), 0);
                    for (int a = 0; a < 8; a++) { float angle = a * Mathf.PI / 4; Line(cx, cy, cx + 30 * Mathf.Cos(angle), cy + 30 * Mathf.Sin(angle)); }
                }
                else
                {
                    for (int x = -42; x < 42; x++) { int y = cy + Mathf.RoundToInt(11 * Mathf.Sin(x * .12f)); Dot(cx + x, y, 2); }
                    Dot(cx + 43, cy + Mathf.RoundToInt(11 * Mathf.Sin(42 * .12f)), 5);
                    for (int t = 0; t < 5; t++) Line(cx - 25 + t * 12, cy, cx - 32 + t * 12, cy - 18);
                }
            }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Mythic nautical shroud", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }
    }
}
