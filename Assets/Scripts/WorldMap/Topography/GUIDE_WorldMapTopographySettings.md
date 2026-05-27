# WorldMapTopographySettings Guide

This guide explains what each setting controls and what visual effect you should expect when tuning it. Because apparently we now own a tiny planet machine and must label every lever before someone launches Nebraska.

The generator has three broad layers:

1. **Base noise**: broad natural variation and fine detail.
2. **Structured ocean-floor features**: island chains, volcanoes, basins, trenches, and edge shaping.
3. **Interpretation/rendering**: sea level, classification, colors, contours, and debug overlays.

---

## World Scale and Resolution

| Field | What it does | Likely visual effect |
|---|---|---|
| `worldSize` | Graph-space dimensions covered by topography. | Bigger values make the world physically larger. Cheap by itself. |
| `heightResolution` | Resolution of generated height data. | Higher = more detail in terrain truth, slower generation. |
| `seedSalt` | Extra deterministic salt mixed with world seed. | Changes topography sequence without changing the world seed. |

## Base Noise

| Field | What it does | Likely visual effect |
|---|---|---|
| `baseNoiseScale` | Main noise frequency. | Higher = smaller/noisier shapes. Lower = broader smoother terrain. |
| `octaves` | Number of layered noise passes. | Higher = more detail and roughness, slower generation. |
| `persistence` | Strength of smaller detail octaves. | Higher = rougher terrain. Lower = smoother terrain. |
| `lacunarity` | Frequency multiplier per octave. | Higher = introduces tiny details faster. |

## Structured Ocean Floor

| Field | What it does | Likely visual effect |
|---|---|---|
| `useStructuredOceanFloor` | Enables deliberate ocean-floor features. | Off = mostly noise. On = chains, volcanoes, basins, trenches, border shaping. |
| `islandChainCount` | Number of raised island/volcanic chains. | Higher = more archipelagos. Too high can clutter or merge land. |
| `chainLengthMin / Max` | Approximate chain length. | Longer = sweeping island arcs. Shorter = compact clusters. |
| `chainCurveStrength` | How much chain paths bend. | Higher = more arcing, organic chains. |
| `chainRidgeWidth` | Width of sharp raised chain core. | Lower = narrow volcanic ridges. Higher = broader chain cores. |
| `chainRidgeStrength` | Height added by ridge. | Higher = chain more likely to rise above sea level. |
| `chainShelfWidth` | Width of broad raised shelf around chain. | Higher = wider shallow shelves and more land merging. |
| `chainShelfStrength` | Height added by shelf. | Higher = more continuous archipelago shelves. |
| `chainSampleSteps` | Curve distance accuracy for chains. | Higher = smoother curves but slower. Lower = much faster. |

## Archipelago Distribution

| Field | What it does | Likely visual effect |
|---|---|---|
| `enforceChainCenterSpacing` | Prevents chains from piling onto each other. | Helps avoid one giant super-island. |
| `chainCenterMinDistance` | Minimum distance between chain centers. | Higher = more separated island groups. |
| `chainPlacementAttempts` | Attempts to find a spaced chain location. | Higher = better spacing reliability, tiny cost increase. |

## Volcanic Peaks

| Field | What it does | Likely visual effect |
|---|---|---|
| `volcanicPeaksPerChainMin / Max` | Number of peaks along each chain. | Higher = more volcanic islands/mountains. |
| `volcanicPeakRadiusMin / Max` | Peak footprint size. | Smaller = pointy islands. Larger = broad volcanic islands. |
| `volcanicPeakStrengthMin / Max` | Height boost at peaks. | Higher = more mountains and land above sea. |
| `volcanicPeakLateralJitter` | Sideways offset from chain center. | Higher = more natural scattered island shapes. |
| `volcanicPeakTangentJitter01` | Spacing irregularity along chain. | Higher = less evenly spaced peaks. |

## Volcanic Peak Shape

| Field | What it does | Likely visual effect |
|---|---|---|
| `volcanicPeakStretchMax` | Max elliptical stretch. | Higher = less circular, more ridge-like volcanoes. |
| `volcanicPeakEdgeWarpStrength` | Distorts peak edge. | Higher = lumpier, less circular volcanoes. Too high can get weird. |
| `volcanicPeakEdgeWarpFrequency` | Number of edge lobes. | Higher = smaller/more bumps. Lower = broad lobes. |
| `volcanicPeakInteriorRoughness` | Adds roughness inside peak. | Higher = less smooth mountain interiors. |
| `volcanicPeakInteriorNoiseScale` | Scale of roughness. | Higher = smaller rough details. Lower = broader patches. |

## Ocean Basins

| Field | What it does | Likely visual effect |
|---|---|---|
| `basinCount` | Number of broad ocean depressions. | Higher = more deep ocean separation. |
| `basinRadiusMin / Max` | Basin size range. | Larger = bigger deep-ocean regions. |
| `basinStretchMax` | Basin elongation. | Higher = long troughs rather than round basins. |
| `basinStrengthMin / Max` | Basin lowering amount. | Higher = deeper water between islands. Too high can drown too much. |

## Trenches

| Field | What it does | Likely visual effect |
|---|---|---|
| `trenchCount` | Number of narrow deep cuts. | Higher = more dramatic underwater drop-offs. |
| `trenchLengthMin / Max` | Trench length range. | Longer = large tectonic scars/channels. |
| `trenchCurveStrength` | Trench bend. | Higher = more organic trench arcs. |
| `trenchWidth` | Width of trench depression. | Lower = sharper trenches. Higher = broader channels. |
| `trenchStrengthMin / Max` | Trench lowering amount. | Higher = deeper and more visible trenches. |
| `trenchSampleSteps` | Curve accuracy for trenches. | Higher = smoother but slower. Lower = faster. |

## Ocean Border and Large Shape

| Field | What it does | Likely visual effect |
|---|---|---|
| `featureEdgeInset01` | Keeps island-chain centers away from edges. | Higher = fewer large landmasses cut off by map borders. |
| `useOceanBorderFalloff` | Lowers terrain near rectangular map edge. | Makes the world feel surrounded by deep water. |
| `oceanBorderMargin01` | Width of edge lowering. | Higher = wider deep-ocean frame. |
| `oceanBorderStrength` | Strength of edge lowering. | Higher = stronger deep water around edge. |
| `oceanBorderPower` | Shape of border falloff. | Higher = sharper drop near the very edge. |
| `fadeRaisedFeaturesNearEdge` | Fades raised chains/peaks near edge. | Prevents large landmasses from touching border. |
| `raisedFeatureEdgeFadeMargin01` | Width of raised-feature edge fade. | Higher = more aggressive edge-safe map. |
| `useRadialFalloff` | Adds broad center-to-edge lowering. | Centers playable island zone. |
| `radialFalloffStrength` | Strength of radial lowering. | Higher = deeper outer world. |
| `radialFalloffPower` | Shape of radial falloff. | Higher = preserves center more, drops edges faster. |
| `contrast` | Expands highs/lows after generation. | Higher = more dramatic islands and deeps. |
| `bias` | Raises/lowers terrain before interpretation. | Positive = more high terrain, negative = more low terrain. |

## Sea Level and Base Map Colors

| Field | What it does | Likely visual effect |
|---|---|---|
| `seaLevel01` | Height threshold for land/water. | Higher = more drowned world. Lower = more land. |
| `shallowDepth01` | Distance below sea level considered shallow/shelf. | Higher = wider turquoise shelves. |
| `beachHeight01` | Distance above sea level considered beach/coast. | Higher = wider sandy coastline bands. |
| `deepWaterColor` | Deep water color. | Darker = more dramatic ocean depth. |
| `shelfWaterColor` | Intermediate shelf color. | Controls mid-depth water tone. |
| `shallowWaterColor` | Shallow/reef water color. | Brighter = clearer reef/lagoon feel. |
| `beachColor` | Coastal land color. | Controls shore/sand tone. |
| `lowlandColor` | Low terrain color. | Controls vegetation/flat island tone. |
| `highlandColor` | Higher terrain color. | Controls inland/hilly tone. |
| `mountainColor` | Highest terrain color. | Controls volcanic/mountain caps. |
| `seaLevelLineColor` | Coastline/sea-level line color. | More visible coastline debugging. |
| `seaLevelLineThickness` | Coastline line thickness. | Higher = thicker coastline line. |

## Auto Sea Level and Classification

| Field | What it does | Likely visual effect |
|---|---|---|
| `autoAdjustSeaLevelToTargetWater` | Solves sea level from target water coverage. | Keeps maps reliably ocean-heavy across seeds. |
| `targetWaterPercent` | Desired water coverage. | Higher = fewer/smaller islands. Lower = more land. |
| `seaLevelSolveIterations` | Binary-search accuracy. | Higher = more exact, slightly slower. |
| `openOceanDepth01` | Open/deep ocean threshold. | Higher = less deep ocean, more open ocean. |
| `lowlandHeight01` | Lowland/highland threshold. | Higher = more lowland. |
| `highlandHeight01` | Highland/mountain threshold. | Higher = fewer mountains. |
| `classificationOverlayAlpha` | Opacity of classification overlay. | Higher = clearer classes, less base map visibility. |
| `class*Color` fields | Classification colors. | Used by the class overlay, not the base map itself. |

## Debug Textures and Contours

| Field | What it does | Likely visual effect |
|---|---|---|
| `textureResolution` | Generated texture resolution. | Higher = sharper map/contours, more memory and bake time. |
| `textureFilterMode` | Texture filtering. | Bilinear = smooth. Point = pixelated/classified look. |
| `lowColor / midColor / highColor` | Legacy debug gradient colors. | Mostly older debug texture paths. |
| `drawContours` | Enables contour generation. | Off = no contour overlay or baked contour lines. |
| `drawContoursIntoBaseTexture` | Bakes contours into base map if true. | False is better for toggleable/masked player-facing contours. |
| `contourCount` | Number of contour bands. | Higher = more lines/detail. |
| `contourThickness` | Thickness of contour lines. | Higher = bolder/darker contours. |
| `contourColor` | Regular contour color. | Controls ordinary contour visibility. |
| `majorContourColor` | Major contour color. | Controls every-Nth contour visibility. |
| `majorContourEvery` | Interval for major contours. | Lower = more major/dark contours. |

## Common Tuning Goals

### Bigger, more separated island chains
- Raise `chainCenterMinDistance`
- Lower `chainShelfWidth`
- Lower `chainShelfStrength`
- Raise `basinStrengthMin/Max`
- Keep `enforceChainCenterSpacing` enabled

### More water-world feel
- Enable `autoAdjustSeaLevelToTargetWater`
- Set `targetWaterPercent` around `0.80` to `0.88`
- Raise `oceanBorderStrength`
- Raise `oceanBorderMargin01`
- Enable `fadeRaisedFeaturesNearEdge`

### More volcanic island drama
- Raise `volcanicPeakStrengthMax`
- Raise `volcanicPeakStretchMax`
- Raise `volcanicPeakEdgeWarpStrength`
- Raise `volcanicPeakInteriorRoughness`

### Less circular/artificial volcanoes
- Raise `volcanicPeakStretchMax`
- Raise `volcanicPeakEdgeWarpStrength`
- Lower `volcanicPeakEdgeWarpFrequency` for broader lobes, or raise it for smaller edge bumps

### Faster generation without reducing map size
- Lower `chainSampleSteps`
- Lower `trenchSampleSteps`
- Lower `trenchCount`
- Lower `octaves`
- Keep `heightResolution` and `textureResolution` if visual quality matters

### Avoid giant cropped landmasses at map edges
- Raise `featureEdgeInset01`
- Raise `oceanBorderStrength`
- Raise `raisedFeatureEdgeFadeMargin01`
- Lower `chainLengthMax` if chains frequently extend off-map

## Suggested Stable Starting Point

```text
worldSize = 400 x 250
heightResolution = 512
textureResolution = 1536

baseNoiseScale = 0.018
octaves = 4
persistence = 0.46
lacunarity = 2.05

islandChainCount = 6
chainLengthMin = 80
chainLengthMax = 190
chainCurveStrength = 45
chainRidgeWidth = 10
chainRidgeStrength = 0.32
chainShelfWidth = 24 to 30
chainShelfStrength = 0.09 to 0.12
chainSampleSteps = 12 to 18

enforceChainCenterSpacing = true
chainCenterMinDistance = 60 to 75
chainPlacementAttempts = 40

volcanicPeaksPerChainMin = 3
volcanicPeaksPerChainMax = 8
volcanicPeakRadiusMin = 3.5
volcanicPeakRadiusMax = 9
volcanicPeakStrengthMin = 0.30
volcanicPeakStrengthMax = 0.72
volcanicPeakStretchMax = 2.2
volcanicPeakEdgeWarpStrength = 0.32
volcanicPeakInteriorRoughness = 0.22

basinCount = 5
basinRadiusMin = 45
basinRadiusMax = 110
basinStrengthMin = 0.20
basinStrengthMax = 0.42

trenchCount = 1 to 3
trenchSampleSteps = 10 to 18

featureEdgeInset01 = 0.14
oceanBorderMargin01 = 0.16 to 0.18
oceanBorderStrength = 0.55 to 0.70
raisedFeatureEdgeFadeMargin01 = 0.18 to 0.22

autoAdjustSeaLevelToTargetWater = true
targetWaterPercent = 0.82
```
