using UnityEngine;

public static class WorldMapBiomeTextureBuilder
{
    public static Texture2D BuildTexture(
        WorldMapBiomeLayer layer,
        WorldMapTopographySettings settings)
    {
        if (layer == null || !layer.IsValid || settings == null)
            return null;

        int size = Mathf.Max(16, settings.textureResolution);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "WorldMapBiomeOverlay"
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : x / (float)(size - 1);

                int biomeIndex = layer.GetBiomeIndexUV(u, v);
                WorldMapBiomeDef biome = layer.Catalog.GetBiome(biomeIndex);

                Color c = biome != null
                    ? biome.debugColor
                    : Color.magenta;

                c.a *= settings.biomeOverlayAlpha;

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
    }
}