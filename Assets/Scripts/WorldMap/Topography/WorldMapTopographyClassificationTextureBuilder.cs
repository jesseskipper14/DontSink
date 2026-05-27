using UnityEngine;

public static class WorldMapTopographyClassificationTextureBuilder
{
    public static Texture2D BuildTexture(
        WorldMapTopographyField field,
        WorldMapTopographySettings settings)
    {
        if (field == null || !field.IsValid || settings == null)
            return null;

        int size = Mathf.Max(16, settings.textureResolution);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = $"TopographyClassification_{field.Seed}"
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = size <= 1 ? 0f : y / (float)(size - 1);

            for (int x = 0; x < size; x++)
            {
                float u = size <= 1 ? 0f : x / (float)(size - 1);
                float h = field.Sample01UV(u, v);

                WorldMapTopographyClass cls =
                    WorldMapTopographyAnalysis.Classify(h, settings);

                Color c = GetClassColor(cls, settings);
                c.a = settings.classificationOverlayAlpha;

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        return tex;
    }

    private static Color GetClassColor(
        WorldMapTopographyClass cls,
        WorldMapTopographySettings settings)
    {
        return cls switch
        {
            WorldMapTopographyClass.DeepOcean => settings.classDeepOceanColor,
            WorldMapTopographyClass.OpenOcean => settings.classOpenOceanColor,
            WorldMapTopographyClass.ShelfWater => settings.classShelfWaterColor,
            WorldMapTopographyClass.ShallowWater => settings.classShallowWaterColor,
            WorldMapTopographyClass.Beach => settings.classBeachColor,
            WorldMapTopographyClass.Lowland => settings.classLowlandColor,
            WorldMapTopographyClass.Highland => settings.classHighlandColor,
            WorldMapTopographyClass.Mountain => settings.classMountainColor,
            _ => Color.magenta
        };
    }
}