using UnityEngine;

[CreateAssetMenu(
    menuName = "WorldMap/Topography/Topography Settings",
    fileName = "WorldMapTopographySettings")]
public sealed class WorldMapTopographySettings : ScriptableObject
{
    #region World Scale and Resolution

    [Header("World Bounds")]
    [Tooltip("Graph-space size covered by the generated topography. This changes the physical map extent, not texture cost by itself.")]
    public Vector2 worldSize = new Vector2(400f, 250f);

    [Header("Height Field")]
    [Tooltip("Resolution of the generated height sample grid. Higher values preserve more terrain detail but increase generation cost.")]
    [Range(32, 2048)]
    public int heightResolution = 512;

    [Tooltip("Extra salt combined with the world seed so this generator can have an independent deterministic sequence.")]
    public int seedSalt = 817263;

    #endregion

    #region Base Noise

    [Header("Base Noise")]
    [Tooltip("Higher = smaller/noisier terrain features. Lower = broader smoother terrain.")]
    [Min(0.0001f)]
    public float baseNoiseScale = 0.018f;

    [Tooltip("Number of layered Perlin noise passes. Higher = more detail and slower generation.")]
    [Range(1, 8)]
    public int octaves = 5;

    [Tooltip("How much each smaller octave contributes. Higher = rougher terrain.")]
    [Range(0.1f, 0.95f)]
    public float persistence = 0.46f;

    [Tooltip("Frequency multiplier per octave. Higher = faster shift into tiny details.")]
    [Range(1.1f, 4f)]
    public float lacunarity = 2.05f;

    #endregion

    #region Structured Ocean Floor

    [Header("Structured Ocean Floor")]
    [Tooltip("If true, adds deliberate island chains, volcanic peaks, basins, trenches, and ocean-border shaping on top of base noise.")]
    public bool useStructuredOceanFloor = true;

    [Tooltip("How many raised island/volcanic chains to generate.")]
    [Range(0, 24)]
    public int islandChainCount = 6;

    [Tooltip("Minimum approximate chain length in graph units.")]
    [Min(1f)]
    public float chainLengthMin = 80f;

    [Tooltip("Maximum approximate chain length in graph units.")]
    [Min(1f)]
    public float chainLengthMax = 190f;

    [Tooltip("How strongly chain paths bend. Higher values create more arcing island chains.")]
    [Min(0f)]
    public float chainCurveStrength = 45f;

    [Tooltip("Width of the sharper raised ridge along each chain.")]
    [Min(0.1f)]
    public float chainRidgeWidth = 10f;

    [Tooltip("Height added by the sharper chain ridge.")]
    public float chainRidgeStrength = 0.32f;

    [Tooltip("Width of the broad raised shelf around each chain.")]
    [Min(0.1f)]
    public float chainShelfWidth = 30f;

    [Tooltip("Height added by the broad chain shelf.")]
    public float chainShelfStrength = 0.12f;

    [Tooltip("Accuracy of curve distance checks for chains. Lower is faster; higher is smoother.")]
    [Range(8, 96)]
    public int chainSampleSteps = 36;

    #endregion

    #region Archipelago Distribution

    [Header("Archipelago Distribution")]
    [Tooltip("If true, later chains try not to pile directly on top of earlier chains.")]
    public bool enforceChainCenterSpacing = true;

    [Tooltip("Minimum distance between island chain centers. Higher = looser, more separated island groups.")]
    [Min(0f)]
    public float chainCenterMinDistance = 55f;

    [Tooltip("How many attempts the generator gets to place chain centers with spacing before accepting the best fallback.")]
    [Range(1, 200)]
    public int chainPlacementAttempts = 40;

    #endregion

    #region Volcanic Peaks

    [Header("Volcanic Peaks")]
    [Tooltip("Minimum number of volcanic peaks per island chain.")]
    [Range(0, 30)]
    public int volcanicPeaksPerChainMin = 3;

    [Tooltip("Maximum number of volcanic peaks per island chain.")]
    [Range(0, 30)]
    public int volcanicPeaksPerChainMax = 8;

    [Tooltip("Minimum volcanic peak radius in graph units.")]
    [Min(0.1f)]
    public float volcanicPeakRadiusMin = 3.5f;

    [Tooltip("Maximum volcanic peak radius in graph units.")]
    [Min(0.1f)]
    public float volcanicPeakRadiusMax = 9f;

    [Tooltip("Minimum height added at volcanic peaks.")]
    public float volcanicPeakStrengthMin = 0.30f;

    [Tooltip("Maximum height added at volcanic peaks.")]
    public float volcanicPeakStrengthMax = 0.72f;

    [Tooltip("How far peaks can wander sideways from the chain centerline.")]
    [Min(0f)]
    public float volcanicPeakLateralJitter = 8f;

    [Tooltip("How far peak spacing can jitter along the chain, normalized 0..1.")]
    [Range(0f, 0.25f)]
    public float volcanicPeakTangentJitter01 = 0.045f;

    [Header("Volcanic Peak Shape")]
    [Tooltip("How stretched volcanic peaks can be. 1 = circular, 2 = twice as long in one axis.")]
    [Min(1f)]
    public float volcanicPeakStretchMax = 2.2f;

    [Tooltip("How strongly the volcanic peak edge is warped. Higher = less circular.")]
    [Range(0f, 0.75f)]
    public float volcanicPeakEdgeWarpStrength = 0.32f;

    [Tooltip("How many lobes/bumps the volcanic peak edge tends to have.")]
    [Range(1f, 12f)]
    public float volcanicPeakEdgeWarpFrequency = 5.5f;

    [Tooltip("Small local roughness added inside volcanic peaks.")]
    [Range(0f, 0.75f)]
    public float volcanicPeakInteriorRoughness = 0.22f;

    [Tooltip("Noise scale for volcanic peak interior roughness. Higher = smaller rough details.")]
    [Min(0.0001f)]
    public float volcanicPeakInteriorNoiseScale = 0.075f;

    #endregion

    #region Depressed Ocean Features

    [Header("Ocean Basins")]
    [Tooltip("Large depressed ocean regions between islands/chains.")]
    [Range(0, 20)]
    public int basinCount = 5;

    [Tooltip("Minimum basin radius in graph units.")]
    [Min(0.1f)]
    public float basinRadiusMin = 45f;

    [Tooltip("Maximum basin radius in graph units.")]
    [Min(0.1f)]
    public float basinRadiusMax = 110f;

    [Tooltip("Basin ellipse stretch. 1 = round, higher = elongated.")]
    [Min(1f)]
    public float basinStretchMax = 2.2f;

    [Tooltip("Minimum depth/lowering applied by basins.")]
    public float basinStrengthMin = 0.16f;

    [Tooltip("Maximum depth/lowering applied by basins.")]
    public float basinStrengthMax = 0.36f;

    [Header("Trenches")]
    [Tooltip("Narrow deep cuts, useful for steep ocean drop-offs and future danger routes.")]
    [Range(0, 12)]
    public int trenchCount = 3;

    [Tooltip("Minimum trench length in graph units.")]
    [Min(1f)]
    public float trenchLengthMin = 90f;

    [Tooltip("Maximum trench length in graph units.")]
    [Min(1f)]
    public float trenchLengthMax = 220f;

    [Tooltip("How strongly trench paths bend.")]
    [Min(0f)]
    public float trenchCurveStrength = 55f;

    [Tooltip("Trench width in graph units.")]
    [Min(0.1f)]
    public float trenchWidth = 6f;

    [Tooltip("Minimum depth/lowering applied by trenches.")]
    public float trenchStrengthMin = 0.24f;

    [Tooltip("Maximum depth/lowering applied by trenches.")]
    public float trenchStrengthMax = 0.48f;

    [Tooltip("Accuracy of curve distance checks for trenches. Lower is faster; higher is smoother.")]
    [Range(8, 96)]
    public int trenchSampleSteps = 36;

    #endregion

    #region Ocean Border and Large Shape

    [Header("Ocean Border")]
    [Tooltip("Keeps important island-chain features away from the map edge. This is a percentage of map size.")]
    [Range(0f, 0.35f)]
    public float featureEdgeInset01 = 0.12f;

    [Tooltip("Extra lowering near the rectangular map border. Helps keep the playable world surrounded by deep water.")]
    public bool useOceanBorderFalloff = true;

    [Tooltip("Width of rectangular ocean-border lowering as a percentage of the map.")]
    [Range(0f, 0.4f)]
    public float oceanBorderMargin01 = 0.16f;

    [Tooltip("Strength of the rectangular ocean-border lowering.")]
    [Min(0f)]
    public float oceanBorderStrength = 0.55f;

    [Tooltip("Curve/power of the rectangular border falloff. Higher = stronger drop near the very edge.")]
    [Min(0.1f)]
    public float oceanBorderPower = 2.4f;

    [Tooltip("Fades raised chain/peak influence near map edges. Prevents large landmasses from touching the border.")]
    public bool fadeRaisedFeaturesNearEdge = true;

    [Tooltip("Width of raised-feature fade near map edges as a percentage of the map.")]
    [Range(0f, 0.4f)]
    public float raisedFeatureEdgeFadeMargin01 = 0.18f;

    [Header("Large Shape")]
    [Tooltip("Keeps edges lower/deeper so the generated world reads more like an ocean map.")]
    public bool useRadialFalloff = true;

    [Tooltip("Strength of radial/center-to-edge lowering.")]
    [Range(0f, 1f)]
    public float radialFalloffStrength = 0.36f;

    [Tooltip("Power of radial falloff. Higher preserves the center more and drops the outside faster.")]
    [Min(0.1f)]
    public float radialFalloffPower = 2.2f;

    [Header("Post Shape")]
    [Tooltip("Expands highs and lows after generation. Higher = more dramatic terrain.")]
    [Range(0.25f, 3f)]
    public float contrast = 1.18f;

    [Tooltip("Raises or lowers the terrain before interpretation. Positive = more high terrain, negative = more low terrain.")]
    [Range(-0.5f, 0.5f)]
    public float bias = 0f;

    #endregion

    #region Sea Level and Visual Colors

    [Header("Sea Level")]
    [Tooltip("Normalized height threshold. Above = land, below = water.")]
    [Range(0f, 1f)]
    public float seaLevel01 = 0.56f;

    [Tooltip("How far below sea level counts as shallow/shelf water.")]
    [Range(0.001f, 0.25f)]
    public float shallowDepth01 = 0.09f;

    [Tooltip("How far above sea level counts as beach/low coastal land.")]
    [Range(0.001f, 0.2f)]
    public float beachHeight01 = 0.035f;

    [Header("Base Map Colors")]
    public Color deepWaterColor = new Color(0.015f, 0.045f, 0.12f, 1f);
    public Color shelfWaterColor = new Color(0.05f, 0.36f, 0.55f, 1f);
    public Color shallowWaterColor = new Color(0.16f, 0.62f, 0.72f, 1f);

    public Color beachColor = new Color(0.72f, 0.63f, 0.42f, 1f);
    public Color lowlandColor = new Color(0.20f, 0.42f, 0.24f, 1f);
    public Color highlandColor = new Color(0.34f, 0.35f, 0.28f, 1f);
    public Color mountainColor = new Color(0.78f, 0.74f, 0.66f, 1f);

    public Color seaLevelLineColor = new Color(0.8f, 0.95f, 1f, 0.95f);

    [Tooltip("Thickness of the coastline/sea-level line in height-space.")]
    [Range(0.001f, 0.04f)]
    public float seaLevelLineThickness = 0.008f;

    #endregion

    #region Auto Sea Level and Classification

    [Header("Auto Sea Level")]
    [Tooltip("If true, solves seaLevel01 from targetWaterPercent after topography generation.")]
    public bool autoAdjustSeaLevelToTargetWater = false;

    [Tooltip("Desired water coverage. Useful for ensuring the generated world remains an ocean adventure map.")]
    [Range(0.5f, 0.98f)]
    public float targetWaterPercent = 0.82f;

    [Tooltip("Binary-search iterations used to solve target water percentage. Higher = more exact, slightly slower.")]
    [Range(4, 40)]
    public int seaLevelSolveIterations = 24;

    [Header("Classification Thresholds")]
    [Tooltip("Water deeper than shallowDepth01 but shallower than this counts as open ocean. Deeper than this is deep ocean.")]
    [Range(0.02f, 0.9f)]
    public float openOceanDepth01 = 0.22f;

    [Tooltip("Land above beachHeight01 but below this counts as lowland.")]
    [Range(0.01f, 0.6f)]
    public float lowlandHeight01 = 0.18f;

    [Tooltip("Land above lowlandHeight01 but below this counts as highland. Above this is mountain.")]
    [Range(0.02f, 0.9f)]
    public float highlandHeight01 = 0.38f;

    [Header("Classification Overlay")]
    [Tooltip("Opacity of the classification overlay texture.")]
    [Range(0f, 1f)]
    public float classificationOverlayAlpha = 0.72f;

    public Color classDeepOceanColor = new Color(0.02f, 0.04f, 0.16f, 1f);
    public Color classOpenOceanColor = new Color(0.02f, 0.14f, 0.36f, 1f);
    public Color classShelfWaterColor = new Color(0.04f, 0.42f, 0.62f, 1f);
    public Color classShallowWaterColor = new Color(0.18f, 0.78f, 0.82f, 1f);

    public Color classBeachColor = new Color(0.86f, 0.72f, 0.44f, 1f);
    public Color classLowlandColor = new Color(0.22f, 0.58f, 0.24f, 1f);
    public Color classHighlandColor = new Color(0.46f, 0.42f, 0.28f, 1f);
    public Color classMountainColor = new Color(0.82f, 0.80f, 0.72f, 1f);

    #endregion

    #region Biome Generation

    [Header("Biome Generation")]
    [Tooltip("If true, generates a regional biome layer from topography/classification data.")]
    public bool generateBiomeLayer = true;

    [Tooltip("Biome grid width. Lower = bigger smoother regions. Higher = more detailed biome boundaries.")]
    [Range(16, 512)]
    public int biomeGridWidth = 128;

    [Tooltip("Biome grid height. Should roughly match the world aspect ratio.")]
    [Range(16, 512)]
    public int biomeGridHeight = 80;

    [Tooltip("World-space radius sampled around each biome cell to decide regional character.")]
    [Min(0.1f)]
    public float biomeSampleRadiusWorld = 18f;

    [Tooltip("Sample grid per biome cell. Odd values like 5 or 7 work best. Even values are allowed but will be treated as odd by the generator.")]
    [Range(3, 11)]
    public int biomeSampleGrid = 5;

    [Tooltip("Small deterministic noise added to biome scores to prevent overly perfect borders.")]
    [Range(0f, 0.35f)]
    public float biomeScoreNoise = 0.045f;

    [Tooltip("Neighbor smoothing passes after biome assignment. Higher = larger/smoother regions, but too high can erase small biome pockets.")]
    [Range(0, 4)]
    public int biomeSmoothingIterations = 1;

    [Tooltip("Opacity of the biome visualizer overlay.")]
    [Range(0f, 1f)]
    public float biomeOverlayAlpha = 0.55f;

    #endregion

    #region Debug Textures and Contours

    [Header("Debug Texture")]
    [Tooltip("Resolution of generated debug/base/overlay textures. Higher = sharper visual layers, more memory and bake time.")]
    [Range(64, 4096)]
    public int textureResolution = 1536;

    [Tooltip("Texture filtering used by generated debug/base map textures.")]
    public FilterMode textureFilterMode = FilterMode.Bilinear;

    [Header("Legacy Debug Colors")]
    [Tooltip("Legacy low elevation color used by older debug rendering paths.")]
    public Color lowColor = new Color(0.03f, 0.09f, 0.18f, 1f);

    [Tooltip("Legacy middle elevation color used by older debug rendering paths.")]
    public Color midColor = new Color(0.22f, 0.34f, 0.28f, 1f);

    [Tooltip("Legacy high elevation color used by older debug rendering paths.")]
    public Color highColor = new Color(0.78f, 0.74f, 0.62f, 1f);

    [Header("Contours")]
    [Tooltip("If true, contour lines are generated.")]
    public bool drawContours = true;

    [Tooltip("If true, contour lines are drawn directly into the base map texture. If false, contours are generated as a separate transparent overlay.")]
    public bool drawContoursIntoBaseTexture = false;

    [Tooltip("Number of contour bands across the normalized height range.")]
    [Range(4, 120)]
    public int contourCount = 34;

    [Tooltip("Thickness of contour lines in height-space.")]
    [Range(0.002f, 0.08f)]
    public float contourThickness = 0.014f;

    public Color contourColor = new Color(0f, 0f, 0f, 0.58f);
    public Color majorContourColor = new Color(0f, 0f, 0f, 0.82f);

    [Tooltip("Every Nth contour line is drawn as a major contour.")]
    [Range(2, 10)]
    public int majorContourEvery = 5;

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        ValidateWorldScale();
        ValidateNoise();
        ValidateStructuredOceanFloor();
        ValidateVolcanicPeaks();
        ValidateBasins();
        ValidateTrenches();
        ValidateClassification();
        ValidateBiomes();
        ValidateTextureAndContours();
    }

    private void ValidateWorldScale()
    {
        worldSize.x = Mathf.Max(1f, worldSize.x);
        worldSize.y = Mathf.Max(1f, worldSize.y);

        heightResolution = Mathf.Max(8, heightResolution);
        textureResolution = Mathf.Max(16, textureResolution);
    }

    private void ValidateNoise()
    {
        baseNoiseScale = Mathf.Max(0.0001f, baseNoiseScale);
        lacunarity = Mathf.Max(1.01f, lacunarity);
    }

    private void ValidateStructuredOceanFloor()
    {
        chainLengthMax = Mathf.Max(chainLengthMin, chainLengthMax);
        chainSampleSteps = Mathf.Max(4, chainSampleSteps);
    }

    private void ValidateVolcanicPeaks()
    {
        volcanicPeaksPerChainMax = Mathf.Max(volcanicPeaksPerChainMin, volcanicPeaksPerChainMax);

        volcanicPeakRadiusMax = Mathf.Max(volcanicPeakRadiusMin, volcanicPeakRadiusMax);
        volcanicPeakStrengthMax = Mathf.Max(volcanicPeakStrengthMin, volcanicPeakStrengthMax);

        volcanicPeakStretchMax = Mathf.Max(1f, volcanicPeakStretchMax);
        volcanicPeakInteriorNoiseScale = Mathf.Max(0.0001f, volcanicPeakInteriorNoiseScale);
    }

    private void ValidateBasins()
    {
        basinRadiusMax = Mathf.Max(basinRadiusMin, basinRadiusMax);
        basinStrengthMax = Mathf.Max(basinStrengthMin, basinStrengthMax);
        basinStretchMax = Mathf.Max(1f, basinStretchMax);
    }

    private void ValidateTrenches()
    {
        trenchLengthMax = Mathf.Max(trenchLengthMin, trenchLengthMax);
        trenchStrengthMax = Mathf.Max(trenchStrengthMin, trenchStrengthMax);
        trenchSampleSteps = Mathf.Max(4, trenchSampleSteps);
    }

    private void ValidateClassification()
    {
        openOceanDepth01 = Mathf.Max(openOceanDepth01, shallowDepth01 + 0.001f);
        lowlandHeight01 = Mathf.Max(lowlandHeight01, beachHeight01 + 0.001f);
        highlandHeight01 = Mathf.Max(highlandHeight01, lowlandHeight01 + 0.001f);
    }


    private void ValidateBiomes()
    {
        biomeGridWidth = Mathf.Max(1, biomeGridWidth);
        biomeGridHeight = Mathf.Max(1, biomeGridHeight);
        biomeSampleRadiusWorld = Mathf.Max(0.1f, biomeSampleRadiusWorld);
        biomeSampleGrid = Mathf.Max(1, biomeSampleGrid);
    }

    private void ValidateTextureAndContours()
    {
        majorContourEvery = Mathf.Max(1, majorContourEvery);
    }
#endif
}
