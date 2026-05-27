using UnityEngine;

public enum WorldGenerationPreviewLayer
{
    BaseOcean,
    OceanFeatures,
    IslandMass,
    IslandDetail,
    FinalHeight
}

public enum WorldGenerationPhase
{
    Idle,
    BaseOcean,
    OceanFeatures,
    IslandMass,
    IslandDetail,
    ComposeFinal,
    BuildPreview,
    Complete,
    Cancelled,
    Failed
}

public sealed class WorldGenerationWorkingSet
{
    public int seed;
    public int width;
    public int height;
    public Rect worldBounds;

    public float[] baseOceanHeight;
    public float[] oceanFeatureDelta;
    public float[] islandMassDelta;
    public float[] islandDetailDelta;
    public float[] finalHeight01;

    public bool IsValid =>
        width > 0 &&
        height > 0 &&
        baseOceanHeight != null &&
        baseOceanHeight.Length == width * height;

    public int Length => width * height;

    public WorldGenerationWorkingSet(
        int seed,
        int width,
        int height,
        Rect worldBounds)
    {
        this.seed = seed;
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
        this.worldBounds = worldBounds;

        int len = this.width * this.height;

        baseOceanHeight = new float[len];
        oceanFeatureDelta = new float[len];
        islandMassDelta = new float[len];
        islandDetailDelta = new float[len];
        finalHeight01 = new float[len];
    }

    public int Index(int x, int y)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return y * width + x;
    }

    public float GetLayer01(WorldGenerationPreviewLayer layer, int x, int y)
    {
        int i = Index(x, y);

        return layer switch
        {
            WorldGenerationPreviewLayer.BaseOcean => baseOceanHeight[i],
            WorldGenerationPreviewLayer.OceanFeatures => oceanFeatureDelta[i],
            WorldGenerationPreviewLayer.IslandMass => islandMassDelta[i],
            WorldGenerationPreviewLayer.IslandDetail => islandDetailDelta[i],
            WorldGenerationPreviewLayer.FinalHeight => finalHeight01[i],
            _ => finalHeight01[i]
        };
    }

    public float[] CopyLayer01(WorldGenerationPreviewLayer layer)
    {
        float[] src = layer switch
        {
            WorldGenerationPreviewLayer.BaseOcean => baseOceanHeight,
            WorldGenerationPreviewLayer.OceanFeatures => oceanFeatureDelta,
            WorldGenerationPreviewLayer.IslandMass => islandMassDelta,
            WorldGenerationPreviewLayer.IslandDetail => islandDetailDelta,
            WorldGenerationPreviewLayer.FinalHeight => finalHeight01,
            _ => finalHeight01
        };

        if (src == null)
            return null;

        var copy = new float[src.Length];

        if (layer == WorldGenerationPreviewLayer.OceanFeatures ||
            layer == WorldGenerationPreviewLayer.IslandMass ||
            layer == WorldGenerationPreviewLayer.IslandDetail)
        {
            NormalizeArrayTo01(src, copy);
        }
        else
        {
            System.Array.Copy(src, copy, src.Length);
        }

        return copy;
    }

    public void ComposeFinal()
    {
        if (!IsValid)
            return;

        for (int i = 0; i < Length; i++)
        {
            finalHeight01[i] = Mathf.Clamp01(
                baseOceanHeight[i] +
                oceanFeatureDelta[i] +
                islandMassDelta[i] +
                islandDetailDelta[i]
            );
        }
    }

    public WorldMapTopographyField ToTopographyField(WorldGenerationPreviewLayer layer)
    {
        float[] layerData = CopyLayer01(layer);

        if (layerData == null)
            return null;

        return new WorldMapTopographyField(
            seed,
            width,
            height,
            worldBounds,
            layerData,
            0f,
            1f
        );
    }

    private static void NormalizeArrayTo01(float[] src, float[] dst)
    {
        if (src == null || dst == null || src.Length == 0 || dst.Length != src.Length)
            return;

        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        for (int i = 0; i < src.Length; i++)
        {
            float v = src[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        if (Mathf.Abs(max - min) < 0.000001f)
        {
            for (int i = 0; i < dst.Length; i++)
                dst[i] = 0.5f;

            return;
        }

        float range = max - min;

        for (int i = 0; i < src.Length; i++)
            dst[i] = Mathf.Clamp01((src[i] - min) / range);
    }
}