using System;
using UnityEngine;

/// <summary>
/// Serializable, frozen generation inputs. Existing worlds should eventually persist
/// one of these rather than reading live ScriptableObject values on every load.
/// </summary>
[Serializable]
public sealed class CelestialGenerationConfig
{
    public int configVersion = 1;
    public int generationSalt;
    public float cellSizeWorld;

    public float ambientStarsPer1000WorldArea;
    public float ambientBrightnessMin;
    public float ambientBrightnessMax;
    public float ambientProminenceMin;
    public float ambientProminenceMax;
    public int ambientVisualVariantCount;

    public float landmarkStarsPer1000WorldArea;
    public float landmarkBrightnessMin;
    public float landmarkBrightnessMax;
    public float landmarkProminenceMin;
    public float landmarkProminenceMax;
    public int landmarkVisualVariantCount;

    public float nebulaePer1000WorldArea;
    public float nebulaBrightnessMin;
    public float nebulaBrightnessMax;
    public float nebulaProminenceMin;
    public float nebulaProminenceMax;
    public float nebulaRadiusWorldMin;
    public float nebulaRadiusWorldMax;
    public int nebulaVisualVariantCount;

    public float deepSkyObjectsPer1000WorldArea;
    public float deepSkyBrightnessMin;
    public float deepSkyBrightnessMax;
    public float deepSkyProminenceMin;
    public float deepSkyProminenceMax;
    public float deepSkyRadiusWorldMin;
    public float deepSkyRadiusWorldMax;
    public int deepSkyVisualVariantCount;

    public static CelestialGenerationConfig FromSettings(CelestialGenerationSettings settings)
    {
        if (settings == null)
            return null;

        var config = new CelestialGenerationConfig
        {
            configVersion = 1,
            generationSalt = settings.generationSalt,
            cellSizeWorld = settings.cellSizeWorld,

            ambientStarsPer1000WorldArea = settings.ambientStarsPer1000WorldArea,
            ambientBrightnessMin = settings.ambientBrightnessMin,
            ambientBrightnessMax = settings.ambientBrightnessMax,
            ambientProminenceMin = settings.ambientProminenceMin,
            ambientProminenceMax = settings.ambientProminenceMax,
            ambientVisualVariantCount = settings.ambientVisualVariantCount,

            landmarkStarsPer1000WorldArea = settings.landmarkStarsPer1000WorldArea,
            landmarkBrightnessMin = settings.landmarkBrightnessMin,
            landmarkBrightnessMax = settings.landmarkBrightnessMax,
            landmarkProminenceMin = settings.landmarkProminenceMin,
            landmarkProminenceMax = settings.landmarkProminenceMax,
            landmarkVisualVariantCount = settings.landmarkVisualVariantCount,

            nebulaePer1000WorldArea = settings.nebulaePer1000WorldArea,
            nebulaBrightnessMin = settings.nebulaBrightnessMin,
            nebulaBrightnessMax = settings.nebulaBrightnessMax,
            nebulaProminenceMin = settings.nebulaProminenceMin,
            nebulaProminenceMax = settings.nebulaProminenceMax,
            nebulaRadiusWorldMin = settings.nebulaRadiusWorldMin,
            nebulaRadiusWorldMax = settings.nebulaRadiusWorldMax,
            nebulaVisualVariantCount = settings.nebulaVisualVariantCount,

            deepSkyObjectsPer1000WorldArea = settings.deepSkyObjectsPer1000WorldArea,
            deepSkyBrightnessMin = settings.deepSkyBrightnessMin,
            deepSkyBrightnessMax = settings.deepSkyBrightnessMax,
            deepSkyProminenceMin = settings.deepSkyProminenceMin,
            deepSkyProminenceMax = settings.deepSkyProminenceMax,
            deepSkyRadiusWorldMin = settings.deepSkyRadiusWorldMin,
            deepSkyRadiusWorldMax = settings.deepSkyRadiusWorldMax,
            deepSkyVisualVariantCount = settings.deepSkyVisualVariantCount
        };

        config.Sanitize();
        return config;
    }

    public CelestialGenerationConfig Clone()
    {
        string json = JsonUtility.ToJson(this);
        CelestialGenerationConfig clone = JsonUtility.FromJson<CelestialGenerationConfig>(json);
        clone?.Sanitize();
        return clone;
    }

    public void Sanitize()
    {
        configVersion = Mathf.Max(1, configVersion);
        cellSizeWorld = Mathf.Max(1f, cellSizeWorld);

        ambientStarsPer1000WorldArea = Mathf.Max(0f, ambientStarsPer1000WorldArea);
        landmarkStarsPer1000WorldArea = Mathf.Max(0f, landmarkStarsPer1000WorldArea);
        nebulaePer1000WorldArea = Mathf.Max(0f, nebulaePer1000WorldArea);
        deepSkyObjectsPer1000WorldArea = Mathf.Max(0f, deepSkyObjectsPer1000WorldArea);

        NormalizeRange(ref ambientBrightnessMin, ref ambientBrightnessMax);
        NormalizeRange(ref ambientProminenceMin, ref ambientProminenceMax);
        NormalizeRange(ref landmarkBrightnessMin, ref landmarkBrightnessMax);
        NormalizeRange(ref landmarkProminenceMin, ref landmarkProminenceMax);
        NormalizeRange(ref nebulaBrightnessMin, ref nebulaBrightnessMax);
        NormalizeRange(ref nebulaProminenceMin, ref nebulaProminenceMax);
        NormalizeRange(ref deepSkyBrightnessMin, ref deepSkyBrightnessMax);
        NormalizeRange(ref deepSkyProminenceMin, ref deepSkyProminenceMax);

        ambientVisualVariantCount = Mathf.Max(1, ambientVisualVariantCount);
        landmarkVisualVariantCount = Mathf.Max(1, landmarkVisualVariantCount);
        nebulaVisualVariantCount = Mathf.Max(1, nebulaVisualVariantCount);
        deepSkyVisualVariantCount = Mathf.Max(1, deepSkyVisualVariantCount);

        nebulaRadiusWorldMin = Mathf.Max(0.1f, nebulaRadiusWorldMin);
        nebulaRadiusWorldMax = Mathf.Max(nebulaRadiusWorldMin, nebulaRadiusWorldMax);
        deepSkyRadiusWorldMin = Mathf.Max(0.1f, deepSkyRadiusWorldMin);
        deepSkyRadiusWorldMax = Mathf.Max(deepSkyRadiusWorldMin, deepSkyRadiusWorldMax);
    }

    private static void NormalizeRange(ref float min, ref float max)
    {
        min = Mathf.Clamp01(min);
        max = Mathf.Clamp01(max);

        if (max < min)
            max = min;
    }
}
