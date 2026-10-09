# Four world-generation handoffs — exact-project audit

**Date:** 2026-10-07. **Status:** AUDIT ONLY — awaiting review. **Implementation:** not started.

Project: `C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink`, Unity 6000.0.65f1, URP 2D. Git HEAD: `a072435be4583a513e5e33d0267b028c2af6cdf4`. This audit includes the current working tree, not merely that commit. Existing gameplay changes from the recent interaction/throwable passes were preserved. No production scripts, settings, prefabs, scenes, bakes, saves, or handoffs were changed by this audit.

## 1. Findings that affect implementation order

1. **Reuse the packed heightfield and its gameplay consumers.** The project already has canonical sampled topography, U16/Base64 persistence, seam-aware sampling, persisted graph/node/POI state, and separate derived map textures. This is a strong foundation; replacing all of it would introduce unnecessary migration work.
2. **The live generator is monolithic, not the old staged WorldGen Lab.** `WorldGenerationPipelineRunner`, `WorldGenerationWorkingSet`, `WorldGenCartridge`, and related preview helpers are explicitly deprecated/shelved. The runner has no serialized references in the searched scene/prefab assets. Its apparent stage architecture must not be treated as the current pipeline.
3. **Generation is expensive mainly before rendering.** Two isolated runs at current settings measured about **20.5 seconds for height generation**, followed by **5.3 seconds for four eager texture builds**. Water-percent solving took about **0.05 seconds**. Detailed measurements and limitations are in section 8.
4. **Current cluster affinity is economic/settlement identity, not geology.** It is assigned after graph placement by `ArchetypeCatalog`/`WorldMapRuntimeBinder`. Feeding it into terrain generation introduces a dependency reversal unless regional geology identity is created upstream and its relationship to settlement affinity is made explicit.
5. **Draft cannot safely mean “same generator with a smaller heightResolution.”** Feature positions are seeded independently of pixel traversal, but global min/max normalization and target-water solving depend on sampled heights. Different grids can shift sea level, coastlines, connected landmasses, candidate rankings, nodes, and harbors. Shared feature decisions plus a shared interpretation policy are needed.
6. **Saved heights do not freeze every assumption.** Saved sea level, graph positions, identities, and settlement manifests are retained, but classification thresholds, biome definitions, geographic depth curves, harbor solving, and much celestial configuration still come from current code/assets. “Exact accepted world forever” requires deciding which of those are truth versus deliberately upgradeable presentation.
7. **Preview isolation is essential.** `WorldTopologyService` is global; the runtime cache is a persistent singleton; the binder mutates GameState; knowledge initialization grants starting-island coverage. Running these scene components for a preview would risk publishing rejected worlds or revealing gameplay knowledge.
8. **New Game currently does not guarantee a newly generated world.** It resets GameState and loads NodeScene. The scene contains a preexisting serialized graph, seed 2, and generation flags that permit reusing that graph. The topography cache accepts any valid cached field without checking the requested seed/settings. These lifecycle assumptions must be addressed before adding rerolls and acceptance.

Recommended approach: establish identity/freeze/ownership contracts and stage boundaries first; refine geography behind those contracts; add zoom-aware presentation and Draft; then wire the player-facing builder and acceptance. Do not finish each handoff in isolation before considering the next.

## 2. Handoffs inspected and audit boundaries

The following exact files were read as target architecture, not as evidence of implemented systems:

| Handoff | SHA-256 of inspected file |
| --- | --- |
| `Assets/Codex/md files/WorldGen_V2_Geography_Bosun_Handoff.md` | `272B27F5D6D29A5D95A3DFF2332E4EE210930724075798E9BF0BD34494094183` |
| `Assets/Codex/md files/WorldMap_Cartography_LOD_Contours_Bosun_Handoff.md` | `78AED1F16861EB27549EE87B6B0F155F423FDB9D0861BDAA76DD0540488A6DC3` |
| `Assets/Codex/md files/WorldGen_Incremental_Pipeline_Workbench_Bosun_Handoff.md` | `6BDF6F389831EE243CF39064DC263BAB64B4145A2FD4CAACF044D10BDDD24924` |
| `Assets/Codex/md files/NewGame_WorldBuilder_Preview_Accept_Bosun_Handoff.md` | `44B3571AB892FC139958A36951999984F080685339578289FD14D1F5B713C68F` |

Evidence consists of live C# source, serialized settings/scene/bake data, asset GUID resolution, file byte counts, and isolated timing calls to the existing runtime algorithms. This was not a live end-to-end New Game playtest or a player build performance capture. No saved world was regenerated. Diagnostic harness code/logs were placed only in ignored `Library/CodexThrowableChecks`; they are not gameplay implementation or deliverable assets.

## 3. Current ownership and assets

Paths below are repository-relative inventories. Selected source links in section 12 open the exact workspace files.

| Behavior | Current owner and exact source | Current assets / consumers |
| --- | --- | --- |
| Height generation and temporary structured feature definitions | `Assets/Scripts/WorldMap/Topography/WorldMapTopographyGenerator.cs` | `Assets/Defs/WorldMap/Topography/WorldMapTopographySettings.asset` |
| Generation knobs, interpretation, colors, contours, biome resolution | `WorldMap/Topography/WorldMapTopographySettings.cs` | One shared settings asset mixes truth and presentation parameters |
| Authoritative runtime sampling | `WorldMap/Topography/WorldMapTopographyField.cs` | Float height array, seed, dimensions, bounds, raw range, topology generation version |
| Source lifecycle, bake matching, lazy texture rebuilds, restore | `WorldMap/Topography/WorldMapTopographyDebugSource.cs` | Components in NodeScene and BoatScene; despite its name, this supplies gameplay truth |
| Editor bake format | `WorldMap/Topography/WorldMapTopographyBakeAsset.cs` | `Assets/Data/WorldMap/Topography/Baked/BakedTopography_2.asset` and `BakedTopography_2 1.asset` |
| Current bake fingerprint | `WorldMap/Topography/WorldMapTopographyFingerprint.cs` | Whole settings JSON + seed + current topology version, FNV-1a 32-bit |
| Water target and classification statistics | `WorldMap/Topography/WorldMapTopographyAnalysis.cs`, `WorldMapTopographyStats.cs` | Resolved sea level is stored separately from source settings |
| Texture/contour generation | `WorldMap/Topography/WorldMapTopographyDebugTextureBuilder.cs`, `WorldMapTopographyClassificationTextureBuilder.cs` | Base, transparent contour, full debug, classification RGBA32 textures |
| Persistent scene-to-scene terrain cache | `WorldMap/Data/WorldMapRuntimeCache.cs` | DontDestroyOnLoad singleton; field, sea, stats, textures, optional biome layer |
| Topology and shared coordinate domain | `WorldMap/Navigation/WorldTopology.cs` | Periodic X, finite Y, current generation version 3; used by terrain, graph, travel, celestial mapping |
| Nodes, landmass candidate scan, geographic groups, graph routes | `WorldMap/Data/WorldMapGraphGenerator.cs` | NodeScene topography-first; BoatScene retains legacy generation mode in serialized fallback configuration |
| Node IDs | `WorldMap/Data/WorldMapStableIdUtility.cs` | `worldSeed:localStableId`, generally `topo_cluster_CC_node_NN`; not persistent geological island IDs |
| Economic affinity and archetype plan | `WorldMap/Data/ArchetypeCatalog.cs`, `ClusterAffinityDef.cs`, `NodeArchetypeDef.cs` | `Assets/Defs/WorldMap/Node Archetypes/ArchetypeCatalog.asset`; five affinity assets under `Cluster Affinities` |
| Persistent node initialization / runtime registration | `WorldMap/Runtime/WorldMapRuntimeBinder.cs`, `MapNodeRuntime.cs`, `WorldMapRuntimeRegistry.cs` | Builds/reuses node state and calls settlement planner; changes GameState |
| Settlement layout and node appearance | `WorldMap/Model/NodeSettlementPlanner.cs`, `WorldMap/Runtime/NodeSettlementScene.cs` and presentation helpers | Saved settlement manifest; archetype-driven buildings/props; local latitude/tree presentation is separate from the regional biome overlay |
| Biome assignment | `WorldMap/Biomes/WorldMapBiomeGenerator.cs`, `WorldMapBiomeMetrics.cs`, `WorldMapBiomeDef.cs`, `WorldMapBiomeLayer.cs`, `WorldMapBiomeTextureBuilder.cs` | `Assets/Defs/WorldMap/Biomes/WorldMapBiomeCatalog.asset`; four listed biome definitions plus fallback |
| POI placement/restoration | `WorldMap/POI/WorldMapPOIGenerator.cs`, `WorldMapPOISource.cs`, `WorldMapPOILayer.cs`, `WorldMapPOIDef.cs` | `Assets/Defs/WorldMap/POIs/WorldMapPOIGenerationSettings.asset`, `WorldMapPOICatalog.asset` |
| Harbor truth/query and boat-dependent berth | `Assets/Scripts/Travel/Travel/HarborGeometryQuery.cs`, `HarborTravelService.cs` | Harbor definition reconstructed from node coordinates + field/sea; boat profile/size supplies safe berth/departure checks |
| Boat terrain and physical grounding | `Assets/Scripts/Environment/GroundGeneration/BoatTerrainStreamer2D.cs`, `BoatTerrainProfile.cs`, `BoatCoastalSurface.cs`, `BoatGeographicObstructionQuery.cs`, `BoatGeographicLandQuery.cs` | Canonical world samples become physical depth through a curve; voyage scale remains separate |
| NodeScene local quay/seabed | `Environment/GroundGeneration/NodeGroundGenerator2D.cs` | Authored land/slope/quay parameters and optional local randomization; not a slice automatically baked from global topography |
| Main world-map drawing / input | `Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs` and `.CartographicReveal.cs` | IMGUI map with whole-world textures and overlays; player map and debug controls share this cartridge |
| Shared world/star map view transform | `WorldMap/MapTable/MapTableViewportState.cs`, `MapTableCartridge.cs`, `MapTablePageLayout.cs` | Shared pan/zoom registration, compare mode and physical chart integration |
| Cartographic knowledge | `WorldMap/Fog/WorldMapKnowledgeSource.cs` and partials, `WorldMapKnowledgeState.cs`, `WorldMapBathymetryConcealment.cs`, `WorldMapMythicShroud.cs`, `LocalIslandChartBuilder.cs` | Source-driven reveal; saved surface/underwater coverage and semantic IDs; starter island only on fresh knowledge |
| Celestial domain/configuration | `WorldMap/Celestial/CelestialFieldSource.cs`, `CelestialFieldGenerator.cs`, `CelestialGenerationIdentity.cs`, `CelestialGenerationFingerprint.cs` | `Assets/Defs/WorldMap/Celestial/CelestialGenerationSettings.asset`; separate fixed decorative menu sky |
| World snapshot and codec | `WorldMap/Data/WorldMapSaveSnapshot.cs`, `WorldMapSaveBuilder.cs`, `WorldMapSaveRestorer.cs`, `WorldMapTopographyHeightCodec.cs` | Packed heights, saved graph, runtime nodes, settlements, POIs, knowledge, surveys, effects |
| Save file / compatibility / slot UI | `Assets/Scripts/Saves/SaveGameService.cs`, `SaveSchema.cs`, `SaveCompatibilityDiagnostics.cs`, `SaveLoadController.cs`, `SaveLoadPanelUI.cs` | Atomic pretty JSON writes in persistentDataPath/Saves; schema 1; currently NodeScene-only saves |
| New Game | `Assets/Scripts/Menu/MainMenuController.cs` | `Assets/Scenes/MainMenu.unity` → `NodeScene.unity`; resets GameState/time/boat/player state before loading |
| Shelved generation lab | `MiniGames/Runners/WorldGenerationPipelineRunner.cs`, `WorldGenOverlayRunner.cs`; `MiniGames/Cartridges/WorldGenCartridge.cs`; `WorldMap/Topography/WorldGenerationWorkingSet.cs`, progress/preview helpers | Existing ideas/UI can be studied, but these are not the production dependency graph |

Build settings enable MainMenu, NodeScene and BoatScene. Old prototype scenes under `Assets/Scenes/Old Scene Stuff` must not be mistaken for active startup configuration.

## 4. Current flow and actual dependencies

### 4.1 Startup, explicit regeneration, and restoration differ

**New Game today:** `MainMenuController.StartNewGame` ensures core singletons, immediately resets GameState, clears the world/celestial snapshots and travel/player/boat state, sets the start time, then loads NodeScene. There is no settings screen, Draft, Full Preview, reroll UI, accepted-settings record, or Accept step. StartNewGame itself does not create a disk save.

**Graph Awake:** tries `WorldMapSaveRestorer.TryRestoreGraphToGenerator`; otherwise `EnsureGenerated`. `EnsureGenerated` returns if the scene's serialized graph already has nodes and force-regeneration is off. NodeScene currently has such a graph. Thus New Game is not inherently equivalent to pressing Generate Map Graph.

**Topography Awake:** tries persistent runtime cache first, then raw saved topography restore, then `LoadOrGenerate`. `LoadOrGenerate` again checks runtime cache, then a matching assigned bake, then synchronous runtime generation if permitted. An explicit `GenerateRuntimeOnly` bypasses those reuse checks.

**Fresh terrain generation:**

```text
settings + graph-generator seed
→ configure global generation bounds
→ seed one System.Random sequence; draw noise offsets
→ create basin definitions, then chains/peaks, then trenches
→ evaluate base noise + all structured features across square grid
→ evaluate periodic shifted copy and blend for every grid sample
→ latitude falloffs, contrast/bias, collect raw min/max
→ global normalization, polar ice foundation, seam endpoint duplication
→ WorldMapTopographyField
→ resolve sea level to water target, then class statistics
→ build base + contour + full debug + classification textures eagerly
→ publish field/sea/stats/textures into persistent runtime cache
```

**Explicit editor bake:** independently generates a new field; resolves water target/stats; builds only textures selected by the bake flags; packs heights; writes texture subassets to a bake asset. This is an editor AssetDatabase operation, not an appropriate runtime New Game acceptance API.

**Raw save restore:** decodes saved heights and sea level; reconstructs field with version inferred from a string; builds the base texture and recomputes statistics using current settings with saved sea level substituted. Contours/classification/debug/biome layers are then lazy. Loading therefore avoids the main height-generation cost but does not freeze all interpretation settings.

**Graph placement:** field + effective sea + classification thresholds → candidate-grid landmask/connected components → regional candidate metrics/scores → one-per-landmass coverage and spacing fallback → selected node positions → geographic clusters → node IDs/stats/names → spanning trees and extra route edges → StartDock/far markers.

**Runtime binding:** graph → archetype/affinity plan → reuse saved node state or initialize new node state → ensure settlement manifest → registry → resolve player's current node/start node → runtime-built event.

**Parallel logical branches after topography:** biome metrics/assignment and POI placement do not require the economic affinity plan. Harbor definitions are queried lazily from node positions and canonical field/sea. Discovery initializes coverage using valid world bounds and later waits for the starter island/chart context. Celestial truth resolves the same field seed/bounds and its own configuration.

There is no single production orchestration object enforcing this ordering. Awake/Start/events, FindAnyObjectByType, fallback LoadOrGenerate calls, and static services cooperate. Dependency guarantees should replace this implicit coordination for a builder/workbench; ordinary scene consumers can remain adapters.

### 4.2 Current serialized values, not class defaults

| Setting / stage | Current value |
| --- | --- |
| World bounds | 720 × 450 map units; centered at origin, X wraps, Y finite |
| NodeScene seed | 2; no New Game reroll currently |
| Height grid | 512 × 512; 262,144 floats |
| Derived texture resolution | 2048 × 2048, independent of world aspect ratio |
| Base noise | Scale .025, octaves 1, persistence .201, lacunarity 1.1; broad/detail stacks still each have at least one octave |
| Structured features | Enabled; 9 chains, 10 basins, 10 trenches |
| Chain shape | Length 50–100; curve strength 50; ridge width 20/strength .15; shelf width 80/strength .02; 8 sampled curve segments |
| Chain distribution | Minimum center distance 300, 40 attempts, best-candidate fallback; hard spacing not guaranteed |
| Chain peaks | 4–8 each; radius 3.5–9; stretch up to 3; strength .3–.53; angular warp .077/frequency 2.09; interior roughness 0 |
| Basins / trenches | Basins radius 45–110; trenches length 120–220, width 30, strength .5–.9, curve strength 80, 8 curve steps |
| Water target | Enabled; .93 water; 24 binary-search iterations; authored manual sea .69 |
| Polar foundation | Band fraction .06, height .99; contributes to coverage calculation |
| Classification | shallowDepth .09, openOceanDepth .22, beachHeight .035, lowlandHeight .18, highlandHeight .38 |
| Contours | Enabled, separate overlay; count 100, thickness .08 in band-relative test; major every 5; coastline thickness .008 |
| Biome layer | Enabled; 256 × 160; sample radius 18, authored sampleGrid 6 becomes 7; noise .15; 2 smoothing iterations |
| Node candidates | 160 × 100; radius 8; 5 × 5 samples; target 25 nodes/5 geographic clusters; min spacing 9 with .55 fallback |
| POI candidates | 180 × 112; radius 10; 5 × 5 samples; global spacing 14; deterministic score noise .08; source salt 420691 |
| Knowledge | NodeScene 128 × 128, independent of height/biome grids |
| Map zoom | 1–240 pixels per map/world unit; default 32; shared with star-chart map table |

NodeScene references `BakedTopography_2.asset` (GUID `6ed88d8fb64e08d45aed07e2f124495d`). BoatScene references `BakedTopography_2 1.asset` (GUID `a25a96c0f6f7c67409c7b635ce885464`). Both sources use the same topography settings asset (GUID `53604aa955a02294ab4093c4910a6a6b`) and prefer runtime cache/bakes. Normal travel expects the persisted graph/field to override BoatScene's legacy fallback configuration.

## 5. World representation, formats, caches, and sizes

### 5.1 Authoritative versus derived

`WorldMapTopographyField` owns a private normalized float array with immutable metadata. `Get01` wraps X and clamps Y. For generation version ≥3 its unique X sample count is width−1 because the last column duplicates the first. `Sample01UV` wraps U, clamps V, and bilinearly interpolates four grid samples. World-space sampling normalizes X through `WorldTopology`.

There is **no persisted semantic macro geology / regional province / archipelago / island-archetype manifest** today. Temporary chains/peaks/basins/trenches are discarded after sampling. Connected landmasses are re-derived on the node candidate grid, not retained as canonical named features. A raster alone cannot explain which island belongs to which geological family.

Runtime gameplay depth is not stored in meters in this field. It is normalized height relative to saved sea level, mapped to physical depth through `BoatTerrainProfile.geographicDepth` and coastal conversion. Changing that curve changes sailing/grounding even if heights remain identical. Map contours currently use normalized height bands, not measured nautical depth contours.

Base/contour/debug/classification/biome textures are derived and disposable. The runtime cache shares their ownership across scene unloads. It is an in-memory current-world cache, not a persistent disk artifact cache and not a per-stage keyed cache. There are no production LOD tiles, contour polylines, mip pyramids, or dirty-stage artifacts.

### 5.2 Storage budget

| Representation | Current approximate size / exact observed bytes |
| --- | --- |
| 512² float height array | 1 MiB; generator temporarily also holds a 1 MiB raw array |
| Packed 512² U16 height bytes | 524,288 bytes = .5 MiB |
| Base64 height string in UTF-8 JSON/YAML | 699,052 characters/bytes ≈ .667 MiB; managed UTF-16 string roughly twice that payload |
| One 2048² RGBA32 texture | 16 MiB texel payload; no mipmaps; builders allocate another 16 MiB Color32 staging array |
| Four eager terrain textures | 64 MiB texel payload, plus transient buffers/native upload overhead; optional biome overlay adds another 16 MiB |
| Biome index grid 256 × 160 ints | 160 KiB excluding catalog/references and smoothing workspace |
| Two 128² bit-packed knowledge layers | 2,048 bytes each before Base64/metadata; additional semantic lists vary |
| Bathymetry concealment | Fixed 512² RGBA32 ≈ 1 MiB; rebuilt on field/state/revision/sea/show-depth changes |
| `BakedTopography_2.asset` | **67,811,216 bytes ≈ 64.67 MiB** on disk; two 2048² texture subassets plus packed height data/metadata |
| `BakedTopography_2 1.asset` | **34,255,698 bytes ≈ 32.67 MiB**; one 2048² texture plus packed heights/metadata |
| Both bake assets together | **102,066,914 bytes ≈ 97.34 MiB** excluding metas/import artifacts |

The large text bake assets contain texture byte payloads serialized as hex, approximately twice the raw RGBA size. This is distinct from save-file size: `WorldMapSaveBuilder` saves packed heights and world manifests, **not these textures**. `SaveGameService` uses pretty JSON and atomic text writes; it does not currently gzip the whole save. Actual total player save sizes were not measured because inventory/boat/node state varies and no user save was opened or modified.

At 1024², packed height Base64 alone would be ~2.67 MiB; at 2048² ~10.67 MiB. Four 4096² RGBA32 maps would have 256 MiB texel payload before transient allocations. Raising every resolution globally is a poor first solution.

### 5.3 Cache identity defects relevant to these handoffs

- Bake fingerprint hashes **all** settings, including colors, texture resolution, contours, and biome knobs. A presentation-only edit can invalidate terrain bake matching and trigger full height regeneration.
- Runtime cache reuse checks valid data, not requested world identity/settings fingerprint. It runs before saved restore in source Awake. New Game and Load must not accidentally inherit a prior field from the same process.
- No cache clear call from New Game or SaveGameService load was found in the inspected runtime code. Cache cleanup happens through its own Clear/context menu/destruction. This is a lifecycle risk, not a confirmed user-visible failure in a specific running session.
- `ArchetypeCatalog.ComputeGraphHash` uses seed/count/sample node indices; it does not hash node positions, cluster memberships, stable IDs, or catalog settings. Same-count regeneration/settings edits can incorrectly reuse an affinity/archetype plan.
- Harbor cache is keyed by field reference, sea, and node object/position. It correctly clears when field identity changes, but it is not an accepted-world harbor manifest or a complete versioned artifact key.
- The second BoatScene bake has the old fingerprint `2:8CF146C8` and no explicit serialized topology generation version, whereas current matching produces `topology_v3:...`. It cannot match the current fingerprint format. NodeScene's bake records version 3 and `topology_v3:2:7F417646`; matching still depends on exact current settings. Do not silently rebake either during this audit or use the old bake to define V2 semantics.

## 6. Geography, RNG, water target, and downstream coupling

### 6.1 Current geography algorithm

Base noise blends normal, broad, and detail fractal Perlin samples. Structured generation draws basins first, chains second, trenches last using one `System.Random(worldSeed ^ seedSalt)` sequence. Chains are quadratic Bezier ridges plus broader shelves, with stretched/rotated warped mound peaks distributed along the path. Basins are depressed ellipses. Trenches are narrow depressed Bezier corridors. Every terrain sample evaluates every feature; curve distance approximations recompute sampled segments during those evaluations.

Periodic generation evaluates the ordinary and X-shifted world positions and blends them with SmoothStep across U. The first/last columns are also made identical. This doubles expensive structured/noise evaluation and can influence feature strength along the domain. It should be preserved as the current baseline until a versioned, demonstrably seam-safe successor exists.

Despite some Inspector tooltip wording, radial falloff and ocean-border shaping are applied to **latitude**, not all four edges, in the wrapped topology path. Do not reintroduce a deep-water wall at the X seam. Polar terrain is added after raw normalization. The world is a cylinder in coordinate terms, not a finite rectangular island canvas.

No explicit island archetype selection exists. “Volcanic peak” is a mound shape, not a semantic volcanic island with crater/interior/coastal hierarchy. Existing chains already provide common direction/shelves and peak relationships, so they are useful building blocks for archipelagos. There are no canonical main/satellite island roles, atoll rings/lagoons, plateau families, regional tectonic provinces, intentional abyssal plain masks, or persistent coast/interior ridge/valley plans.

Post-shape contrast and bias are affine changes applied **before global min/max normalization**. A positive contrast and uniform bias are largely canceled by that normalization, apart from floating-point effects. They should not become supposedly meaningful player controls without changing/versioning their semantics.

### 6.2 Determinism as it exists

Terrain is repeatable for the same seed/settings/grid/algorithm/environment. Feature draws occur before pixel sampling, which is reusable for resolution-independent planning. However:

- Changing basin count changes subsequent RNG draws for chains/trenches. Peak count or placement-attempt outcomes similarly shift later features. There are no stable independent terrain stage/feature seed streams yet.
- Increasing height resolution changes sampled extrema and the water solve; same feature definitions do not guarantee identical shoreline/landmass interpretation.
- `Mathf.PerlinNoise`, floating-point arithmetic, threshold comparisons, and math-library behavior are not a formal cross-platform bit-identical contract. Freeze accepted packed truth rather than promise arbitrary future code will regenerate it exactly.
- Candidate score noise and biome noise use stable hash functions, not shared mutable Unity random. Topography-first node stats/routes use separate derived seeds. The existing graph clustering/sorted IDs are deterministic under unchanged candidates, but IDs are ordinal, so added/deleted/reclustered nodes can reassign identity.
- POIs use field seed XOR source salt and definition-specific score hashes. Catalog iteration participates in global spacing, so reordering definitions can change placements. Stable POI names/IDs are not canonical geology feature IDs; `poi_<type>_<ordinal>` may refer to a different site after regeneration. Generated display-name formatting also uses culture-sensitive numeric interpolation.
- Archetype coverage/affinity selection has derived seeds and deterministic sorting, but catalog order/weights, required archetype coverage, earlier repeat counts, and the incomplete cache key affect results.
- NodeScene's separate local ground generator supports `randomizeSeedOnGenerate`; it is not governed by the global terrain determinism contract.

Recommended seed contract: separate named/versioned seeds for macro, regional plan, archipelago plan, each island, detail, biomes, nodes, POIs, and presentation. Persistent stable feature IDs must not depend on raster resolution, transient list position, task scheduling, GetHashCode, locale, or shared global RNG state.

### 6.3 Target water percentage

`FindSeaLevelForTargetWaterPercent` binary-searches [0,1], repeatedly counting field samples with `height < sea`. It does not add/delete islands or redistribute geography. Effective sea level then changes classification, connected landmasses, shelf/depth interpretation, candidate scores, and harbor availability.

Current settings target .93; timing runs resolved **sea .6981653 and water .9300003**. Discrete samples, plateaus, and polar coverage mean some targets/settings can only be approximated. The duplicated seam column is included in the width×height count. Draft resolution can therefore alter the solved threshold even with identical features.

Keep the existing solver as a baseline/reusable implementation; decide whether V2 coverage measures the full canonical domain including ice, unique seam samples, and any excluded polar land. Do not silently change the target's denominator. Island Amount and Island Clumping must remain separate controls; neither is currently implemented as a dedicated high-level parameter. Chain count/peak count approximate amount and center spacing influences distribution, but these are not equivalent to the requested controls.

### 6.4 Biomes, nodes, harbors, POIs, and knowledge

- **Biomes:** class proportions/height range around cells → catalog scores plus hashed noise → highest-scoring biome → neighbor smoothing. They are derived from final topography; no upstream regional geology identity drives them. `MapNode.biome` is initialized to None in graph generation; the debug biome raster is not automatically an economic node-biome assignment system.
- **Nodes:** coast-weighted regional metrics, landmass coverage, spacing and clustering. Candidate landmass resolution can miss small islands or merge/separate features differently from the canonical height grid. Preserve X-wrap connectivity. Twenty-five nodes is a target, not a hard guarantee for every new geography.
- **Affinity:** five existing definitions are CalmArchipelago, FoodBelt, IndustrialRemnant, TimberChain, WreckCoast. Their authored fields describe weighted settlement archetypes/market style, not physical geology. Reuse identity/policy where appropriate, but do not equate a trade cluster with a physical archipelago.
- **Harbors:** `HarborGeometryQuery.TrySolve` scans 72 directions at 5° increments, requiring continuous waterward corridors with obstruction checks. Definitions are deterministic/reconstructible, then berth sizing checks actual boat dimensions/draft/profile/scale. Geography changes can remove all valid corridors. Higher field resolution also changes grid-derived search step/search distance, so it affects harbor semantics and cost.
- **Physical coast:** BoatScene streaming/grounding/piloting near-field samples the authoritative field. New visual-only fine detail must not create apparent safe water over physical ground or show islands that have no matching collision truth. NodeScene's authored quay profile remains a separate presentation/physical scene system.
- **POIs:** current generator depends on final field, effective sea, classification settings, sampling/scoring/count/spacing definitions; it does not consume biome or affinity artifacts. Most current definitions are underwater. Deeper macro features may move or increase candidate availability. Existing POIs should restore, not reroll.
- **Knowledge/surveys:** source-driven surface and underwater masks plus semantic node/POI/source IDs are saved. Starting island coverage is granted deliberately; travel must not regain passive reveal. Existing charts/soundings/survey clues refer to saved coordinates and IDs. Terrain rerolls invalidate these relationships in a live save.
- **Celestial:** seed/bounds are shared with topography and map-table registration. CelestialFieldSource normally builds using current celestial settings; saved constellation configuration is handled separately. Fixed decorative menu sky must remain outside the world builder. Freeze celestial generator inputs/version if accepted-world clue reproducibility is part of the world contract.

## 7. Current map zoom and contour rendering

### 7.1 Rendering and zoom

`WorldMapCartridge` draws the full base texture into a bounds-derived rectangle inside an IMGUI clipped viewport. Classification and biome overlays and a separate contour texture can be drawn on top. Pan changes center; mouse-wheel zoom changes pixels-per-world-unit, clamped 1–240. Shared `MapTableViewportState` preserves the same center/scale across world/star pages and compare mode.

There is **no zoom-aware selection of terrain resolution**. Bilinear base filtering softens resampling, but zoom still enlarges a fixed image generated from fixed truth. Classification/biome textures use point filtering. All these textures are 2048² without mipmaps. There is no tile residency/visible-tile generation or LOD contour selection.

Current height cell spacing is ~1.409 map units X and .881 Y. At 240 pixels/unit that is approximately **338 × 211 screen pixels per truth cell**. Texture texels are ~.352 × .220 units, ~84 × 53 pixels at maximum zoom. Merely doubling the texture does not provide missing terrain information; re-rendering bilinear 512² truth at 4096² still cannot reveal new genuine coastline/interior structure.

### 7.2 Contours

`WorldMapTopographyDebugTextureBuilder.IsContour` computes `scaled = height * contourCount`, finds distance to its nearest integer, compares against `contourThickness`, and marks major lines by contour index modulo `majorContourEvery`. Thus current thickness .08 is tested in scaled band coordinates, not a constant screen-space width and not simply .08 normalized height. Coastline uses a separate absolute height tolerance.

There is no marching-squares/polyline extraction, topology-preserving simplification, intermediate line hierarchy, zoom threshold, minimum pixel spacing, label pipeline, or independent visible-cell contour budget. Major/minor opacity exists, but all bands remain baked at every zoom. Full debug embeds contours; base optionally can embed them; separate contour overlay also includes coastline. Rendering embedded contours and an overlay together can duplicate line appearance.

### 7.3 Knowledge ordering is part of rendering correctness

Viewport order is terrain → celestial/grid → bathymetry concealment → routes/travel route → surface shroud → POIs/nodes with visibility predicates. Bathymetry concealment is a 512² point-filtered opaque plain-ocean texture where depth knowledge is missing; surface shroud draws parchment over unrevealed coverage cells. Terrain LODs must preserve this distinction and semantic marker checks.

Full truth textures existing in memory are not themselves a player knowledge grant. However, replacing the current opaque concealment with an alpha-blended/tiled system can leak shelves, coast edges, contour segments, or hidden nodes through filtering/tile borders. Every LOD and asynchronous fallback must fail closed while masks are unavailable. Preview omniscience must be a separate context, never a debug toggle that mutates gameplay knowledge.

### 7.4 Recommended representation direction, subject to review

Keep canonical sampling authoritative. Add a bounded derived cartography service with coarse full-world overview and visible-region higher-resolution presentation; cache by truth identity + interpretation + presentation version + LOD/tile. Decide during MAPLOD.2 whether close-scale truth is a denser accepted raster, frozen hierarchical tiles, or saved deterministic feature plans with a versioned sampler. That decision must precede WGEO close-scale fidelity and acceptance format finalization.

Contours can be derived segments or shader/raster contours. Choose after comparing CPU build/upload cost, draw cost, seam behavior, mask clipping, and screen-space density. Polylines are an option, not an audit finding that they already exist or a mandatory rewrite. Reuse the map-table transform and current knowledge queries regardless of technique.

## 8. Timings and expensive stages

### Method

An isolated Unity 6000.0.65f1 batch Editor project under ignored Library called the existing compiled runtime `WorldMapTopographyGenerator`, analysis, texture builders, and codec. The harness reconstructed the current settings asset's public scalar/vector/color/enum fields and used seed 2. It ran twice on this machine with D3D11. No production scene was loaded. Texture timings include creation, CPU population/upload, and immediate diagnostic destruction. Generation ran synchronously on the Editor main thread, as the production methods do.

The initial sandbox launch failed to connect to its local Package Manager server; an isolated launch with the required process access completed. These are successfully measured stage timings, not profiler guesses. They exclude Editor startup/import, node/runtime/POI/biome generation, gameplay scene initialization, serialized bake writing, and complete save serialization. The earlier piloting-lag profiler capture is not evidence for this generation path.

| Existing stage | Run 1 | Run 2 |
| --- | ---: | ---: |
| Generate 512² heightfield | 20,597.31 ms | 20,506.59 ms |
| Target-water solve, 24 passes | 48.36 ms | 45.36 ms |
| Classification statistics | 7.69 ms | 7.39 ms |
| Base texture, 2048² | 1,403.95 ms | 1,395.15 ms |
| Contour overlay, 2048² | 1,256.86 ms | 1,270.02 ms |
| Full debug texture, 2048² | 1,627.52 ms | 1,634.65 ms |
| Classification overlay, 2048² | 1,027.15 ms | 1,024.73 ms |
| Copy + U16/Base64 encode | 4.14 ms | 4.68 ms |
| Measured stages summed | ~25.97 s | ~25.89 s |

Terrain accounts for ~79% of these measured stages, textures ~20.5%. This is consistent with a roughly 30-second full regeneration once other stages are included, but **the unmeasured remainder must not be attributed to a particular subsystem without another capture**. Warm bake load and persisted-height restore take different paths and should have separate baselines.

### Cost mechanisms and priorities

1. **Repeated per-sample structured evaluation:** 512² samples × two periodic evaluations × 19 Bezier chain/trench features × 8 curve segments yields roughly 79.7 million segment-distance iterations, before basin/peak/noise work. Chain peaks additionally do rotation, Atan2, Sin, and Sqrt even for distant samples before rejecting them. Precomputed curve segments and safe support/bounds/spatial indexing could reuse current algorithms and substantially reduce wasted work. Profiling does not yet isolate each inner kernel's exact share.
2. **Eager unrequested overlays:** `GenerateRuntimeOnly` always builds four 2048² textures. Bake flags only govern the editor bake path. Base-only generation could avoid ~3.9 seconds here, with contour/debug/classification built on demand; geography still dominates.
3. **Main-thread blocking:** moving a synchronous Generate call into a coroutine does not yield inside it. Genuine chunked kernels/worker-safe numeric stages and cancellation points are required. Unity object creation/upload must remain on supported threads. Yielding can improve responsiveness without necessarily reducing total time.
4. **Biome computation:** 40,960 cells × 49 neighborhood samples ≈ 2 million height/classification samples, then scoring/smoothing and potentially another 2048² texture. Generated lazily by EnsureBiomeTexture, not necessarily part of every fresh terrain call. Not timed here.
5. **Nodes/POIs:** node candidate metrics ~400,000 neighborhood samples plus landmask/selection/clustering; POI candidate metrics up to ~504,000 before per-definition scores/sorts/spacing. These are separate reusable downstream artifacts. Not timed here.
6. **Harbor validation:** direction scans/obstruction/boat berth depth sweeps can be expensive if requested for every node repeatedly. Current service caches successful definitions. New Game should validate its start harbor and final node viability without recomputing all harbor checks every preview repaint. Not timed here.
7. **Bake/import/file work:** 97 MiB of existing text bakes and embedded texture data can add AssetDatabase/import/serialization costs. Avoid writing them during every preview. Packing height bytes itself is negligible at current resolution.

A “few-second Draft” cannot be achieved by removing textures alone. Sampling a 128² draft might reduce the raster loop roughly sixteenfold in an idealized estimate, but neither its timing nor coastline equivalence is established. Share a planned world and resolved normalization/sea policy; do not advertise exact Draft matching from this estimate.

Diagnostic evidence: `Library/CodexThrowableChecks/Assets/Editor/WorldGenAuditTiming.cs` and `Library/CodexThrowableChecks/worldgen-audit.log`. Important values/results are reproduced above so this document does not depend on retaining ignored files.

## 9. Handoff comparison and KEEP / EXTEND / REFACTOR / REPLACE / DEFER decisions

### 9.1 WGEO — Geography

**KEEP:** gameplay bounds 720×450, periodic topology, float/U16 sampling contract, existing water solver, boat depth/grounding consumers, source-driven discovery, persisted graph/runtime/POI state. Keep legacy saved worlds out of new geography generation.

**EXTEND:** chain/shelf/peak/basin/trench building blocks into explicit upstream macro features, stable regional plans, archipelago relationships, per-island archetypes, coastline/interior structure, regional density masks. Extend settings with independent high-level amount/clumping and geology controls while preserving raw developer knobs and migrated defaults.

**REFACTOR:** generator into planned feature definitions and numeric sampling stages; separate sea interpretation and presentation; move/bridge geology affinity upstream without using downstream economic cluster assignment as an input cycle; introduce world/algorithm identity distinct from topology format version.

**REPLACE:** globally uniform noise-led terrain composition as the organizing model, ordinal-only geology identity, and unconditional all-feature work per sample where profiling supports a bounded alternative. This does not require replacing Field, codec, map-table transforms, settlement planner, or boat streaming.

**DEFER:** actual new POI gameplay/economy/quests, authoring final island art, redesigned navigation, local NodeScene quay/town generation overhaul. WGEO.8 must validate these dependencies, not absorb their redesign.

Mismatch: current “structured ocean” is already more than noise, but it is not a hierarchy of persisted geological provinces. Affinity names sound geographic but their implementation is economic. Current island chains can be reused rather than introducing an unrelated second archipelago generator.

### 9.2 MAPLOD — Cartography

**KEEP:** map/star coordinate registration, clipping/input/shared viewport, canonical field sampling, knowledge predicates and chart semantics. Keep masks separate from full terrain truth.

**EXTEND:** overview/close LODs, visible-region presentation, screen-space contour density/hierarchy, depth shading and scale cues. Define meaningful fidelity limits for old 512² saves.

**REFACTOR:** split map drawing/data access from the large WorldMapCartridge so world builder/workbench can reuse presentation without requiring a live graph or player GameState. Split contour/palette keys from geography hash; bounded cache ownership/eviction and no synchronous generation in repaint.

**REPLACE:** one fixed raster as the sole zoom representation; fixed baked contour set at every scale. Replace redundant full-debug caches where separate reusable overlays suffice.

**DEFER:** global truth algorithm changes to WGEO, discovery redesign, full GIS labels/vector editing, celestial-map overhaul. Do not invent unseen gameplay geography solely to satisfy map zoom.

Mismatch: contours are threshold raster pixels, not a vector extraction pipeline. Major/minor differentiation exists already, but intermediate bands and spacing caps do not. U16 truth and separable overlays already satisfy part of the intended truth/presentation split; there is no need for a second truth store solely for LOD.

### 9.3 WPIPE — Incremental pipeline/workbench

**KEEP:** existing production generator functions, analysis/biome/POI kernels, compact codec, source/bake interfaces as consumers, developer access to raw parameters. Reuse diagnostic concepts from the shelved lab only after checking them against production semantics.

**EXTEND:** explicit immutable artifact identity, scoped stage seeds, timing/progress/cancel, dependency keys, cached results, presets and a workbench over the shared orchestrator. Add Draft as a mode of that same planned world.

**REFACTOR:** source MonoBehaviour lifecycle into an adapter rather than the owner of generation orchestration; graph generation separate from scene/runtime binding; split settings hashes by responsibility; independent dirty branches for nodes/biomes/POIs/presentation.

**REPLACE:** full settings → whole bake invalidation, shared mutable terrain RNG across unrelated stages, global current-world cache as a preview scratchpad, and row-yield UI that merely wraps blocking production calls.

**DEFER:** resurrecting the deprecated runner as-is, player New Game controls, GPU/Jobs/Burst rewrite before identifying compatible kernels, persistent reuse of every intermediate float layer regardless of memory cost.

Mismatch: there is no live incremental production stage graph. Biomes/POIs depend directly on interpreted terrain and do not inherently require nodes/harbors; a literal linear pipeline from the example handoff would introduce false dependencies and excess invalidation. World map debug toggles are not a generation workbench.

### 9.4 NGBUILD — New Game builder

**KEEP:** existing menu/save-slot/load flows, start time/player/boat defaults, compatibility diagnostics and atomic file writes, starter-island-only coverage. Keep decorative menu scene/sky separate.

**EXTEND:** a draft builder session with safe broad controls, seed/preset entry, isolated previews, Full verification, Accept transaction and persisted accepted inputs/algorithm versions/coordinate domain. Runtime save construction can reuse snapshots/codec without editor bake APIs.

**REFACTOR:** delay destructive GameState reset until acceptance; make rejection/cancel leave the prior world/session intact; adopt exact accepted artifacts into gameplay rather than let Awake regenerate them; reconcile current NodeScene-only save validation with creating a new save at acceptance.

**REPLACE:** direct New Game→NodeScene as the only route, reliance on scene seed/serialized graph, unchecked single-current-world cache reuse for rerolls. Do not replace the whole SaveGameService simply to add an acceptance boundary.

**DEFER:** safe controls with no actual implementation such as a promised danger distribution model; character creation, multiplayer lobby, tutorial, quest setup. Advanced raw controls must use the same validated parameter model, not separate generation logic.

Mismatch: Accept cannot just invoke `BakeTopographyAsset` (editor-only), or `SaveSlot` unchanged while in MainMenu (NodeScene gate). No accepted parameter snapshot exists. WorldSave's existing modes include BakedAssetReference and RegenerateFromSeed, but current raw restoration does not make those enums a functioning immutable runtime acceptance strategy.

## 10. Risks, invalidations, and dependency policy

### 10.1 Save / identity risks to settle before implementation

| Risk | Current evidence | Required design response |
| --- | --- | --- |
| Algorithm version conflated with topology | WorldTopology.GenerationVersion=3; saved generator string is only `topography_v3_wrapped_x` or v2 | Separate generator algorithm/stage versions, sampling/topology version, save schema and presentation version |
| New version strings read as old topology | TryRestoreFromSnapshot selects version 3 only for one exact string, else 2 | Preserve explicit topology metadata; dispatch known legacy formats, reject unsupported formats rather than silently reinterpret |
| Settings not frozen | Snapshot stores settings ID/hash, field/sea; restored settings clone current asset | Persist accepted truth-relevant settings/interpretation and content IDs; explicitly decide upgradeable cosmetics |
| Misleading hash metadata | CaptureTopography uses assigned bakedAsset fingerprint even when runtime field differs; runtime path may omit current settings hash | Capture the actual accepted/generated identity, not whichever bake happens to be assigned |
| Node identity/seed divergence | UseRestoredGraph does not assign generator.seed; binder uses generator.seed for stable IDs and affinity plan | Single authoritative seed/identity from restored/accepted graph/field, validated across all consumers |
| Empty POI manifest regenerated | SaveRestorer skips restore when POI list count is zero, even though source supports empty list restore | Distinguish “saved empty result” from “artifact missing”; never reroll accepted empty output |
| Partial/corrupt height payload | Decoder uses available byte count and leaves missing samples zero; IsValid tests resulting array length | Validate exact packed byte count/encoding/checksum before publishing accepted/restored truth |
| Recomputed harbors/physical depth | Harbors are NonSerialized and rebuilt from current solver/profile; meters-depth mapping is current asset | Freeze/version harbor solver inputs or accepted definitions and validate boat clearance; policy for physical-depth upgrades |
| Biomes/content changes | Biome indices are derived against current catalog; node archetypes and settlement manifests use IDs | Store stable semantic identity when gameplay depends on it; avoid catalog index as permanent truth |
| Celestial clue drift | Celestial source uses current star config plus world seed/bounds; saved constellation config has its own handling | Audit/freeze celestial generation inputs/version in acceptance; preserve chart clues at saved coordinates |
| Scene fallback silently used | Missing topography can fall back to legacy graph generation | Accepted/loaded-world paths should fail clearly on missing truth, not quietly create a different world |

World snapshot version is 4; topography capture version is 2; outer SaveSchema remains 1; graph has its own topology generation version. These are distinct. Keep legacy decode/read paths and add explicit migration rules instead of incrementing one number and assuming all consumers adapt.

### 10.2 Proposed dependency graph and dirty scope

```text
world identity / fixed bounds / accepted input snapshot
├─ macro + regional geology plan
│  └─ archipelago + island plans
│     └─ canonical terrain/detail sampling
│        └─ resolved normalization / sea / classification interpretation
│           ├─ biome assignment
│           ├─ node candidates → graph → affinity/archetype → settlement manifests
│           │                    └─ harbor definitions / start-node viability
│           ├─ POI candidates → POI placement
│           └─ derived cartography / contour / class presentation caches
└─ celestial generation inputs + domain → star truth / related clue identity

accepted artifacts → separate gameplay knowledge initialization
```

Upstream regional geology may use an explicitly planned affinity identity; downstream economy can inherit/bias from it. If “biome influences geology detail” is required, use an upstream regional climate/geology descriptor, not the final biome raster fed back into its own terrain input.

| Parameter change | Minimum truthful invalidation |
| --- | --- |
| Seed or bounds/topology | All truth, domain-dependent celestial data, all derived artifacts; never apply to existing live save |
| Macro basin/ridge/trench/region structure | Affected geometry plans/sampling onward; normalization may currently cause global effects |
| Island amount/clumping/style | Regional/archipelago/island plan and terrain onward; macro oceans reusable only if placement doesn't feed back into them |
| Local roughness/detail | Affected terrain onward; resolved sea/candidates still dirty unless interpretation is intentionally fixed |
| Water target/manual sea | Reinterpret existing heights; classes, biomes, landmasses, nodes, harbors, POIs and textures; no raw feature resampling required |
| Classification thresholds | Classification/statistics, metrics-driven nodes/biomes/POIs and presentation; harbor water test unaffected unless coastal/depth policy changes |
| Node target/spacing/scoring | Nodes/graph/affinity/settlements/harbors and node overlays; terrain/biomes/POIs remain reusable under current direct dependencies |
| Affinity/archetype catalog | Affinity plan/node initialization/settlement appearance; terrain unaffected today; upstream geology link would broaden this |
| POI count/spacing/scoring/catalog | POI output/overlays; candidate metrics reusable when its sampling/threshold inputs match |
| Biome scoring/catalog/smoothing | Biome assignment/overlay; sampling metrics reusable if terrain/threshold/radius match |
| Contour density/major cadence/style | Contour representation/render only; base redraw too only when contours deliberately embedded |
| Palette / texture filtering / LOD display | Presentation/cache only; no terrain or node reroll |
| Knowledge integration | Knowledge masks/visible marker state only; never generator stages |
| Boat dimensions/draft | Boat berth validation only; no canonical world/harbor reroll |

A scope button must either include dirty prerequisites or explain why it cannot reuse them. Cache keys must include artifact hashes/versions and relevant content settings; flag toggles should not invalidate truth.

### 10.3 Where checkpoints can invalidate one another

| Interaction | Consequence and sequencing guard |
| --- | --- |
| WGEO.2–7 versus early WPIPE stage extraction | New geology stages change dependencies/artifact formats. Define plan/sampler seams early, then evolve the same graph; do not freeze the monolithic old generator into a new permanent pipeline |
| WGEO.3 affinity versus existing node clustering | Current affinity comes after nodes. Reusing it upstream unchanged creates a cycle and makes node-count edits reroll geography; introduce separate upstream region identity |
| MAPLOD.2/3 versus WGEO.6/7 | Higher-resolution presentation cannot invent missing authoritative coasts. Decide close-scale truth fidelity before final island-detail tuning |
| WPIPE.4 Draft versus target-water solve | Low-resolution extrema/quantile changes can move Full shores. Shared feature RNG alone is insufficient; use shared interpretation/canonical calibration policy |
| NGBUILD.5 acceptance versus later generator/save changes | Persisted heights alone omit interpretation/semantic inputs. Define freeze schema now, finalize it after geography/LOD truth decision, before shipping Accept |
| MAPLOD.4 versus WPIPE.3 keys | Current whole-settings hash makes contour edits regenerate terrain; split keys before calling contour work independent |
| WGEO.8 versus final resolution changes | Node landmass candidates and harbor search are resolution-sensitive. Repeat downstream viability on final canonical resolution, not only Draft |
| Biome-density geography versus derived biome layer | Feedback loop if final biomes control terrain that determines those biomes; separate planned region characteristics from derived classification |
| Workbench/preview versus runtime singleton publication | Generating a candidate can replace gameplay cache/bounds/state before Accept. Session-scoped artifacts and transaction-only publication are prerequisites |
| LOD/mask changes versus completed discovery pass | Filtering or asynchronous fallback can expose unknown details. Knowledge regression is required at every rendering checkpoint, not only MAPLOD.6 |
| Additional macro randomness versus compatibility | Stable seed no longer yields prior terrain after new draws; store old truth and use scoped versioned streams for new worlds |

## 11. Recommended combined implementation order and review gates

This audit covers the audit checkpoint of each handoff. It does **not** declare their implementation complete or rename these handoffs DONE_. Every later checkpoint retains its requested stop/compile/report/playtest gate.

1. **Review this audit and choose contracts.** Confirm no world-bound/sailing-scale change; full-domain water target policy; upstream geology versus economic affinity; acceptable Draft approximation; close-scale truth representation; exact accepted-world freeze scope; NodeScene-only save integration. These decisions can be made during review without reopening unrelated gameplay passes.
2. **WPIPE.2 + minimum NGBUILD.5/MAPLOD.2 contracts.** Add session-scoped immutable input identity and artifacts; separate numeric generator from scene publication/binding; independent seeds; truth/interpretation/presentation version boundaries. Preserve existing-world decode and current baseline before changing appearance.
3. **WPIPE.3 dirty graph and bounded baseline optimizations.** Split hashes/caches, precompute reusable feature data where equivalent, avoid unrequested textures, instrument stages and cancellation. Preserve baseline output where the optimization claims equivalence; version deliberate output changes.
4. **WGEO.2 → .3 → .4 → .5.** Macro provinces, regional identity, archipelago plans, island archetypes; run node/start-harbor/knowledge checks along the way rather than postpone all viability until the end.
5. **MAPLOD.2 truth-fidelity decision + WGEO.6/.7.** Agree close-scale accepted truth/sampling/storage, then refine coasts/interiors/detail with an overview and close preview. Finish MAPLOD.3 against those artifacts.
6. **WPIPE.4 Draft.** Sample the same plans with resolved interpretation policy, show approximate detail honestly, prove Full never rerolls features. Benchmark several representative seeds and settings, not just seed 2.
7. **WPIPE.5/.6 + MAPLOD.4/.5.** Workbench tabs/scoped regeneration/overlays; contour hierarchy, density and underwater presentation. Keep a shared renderer and distinct viewer permissions.
8. **WGEO.8 + WPIPE.7 + MAPLOD.6.** Final-resolution node/harbor/POI viability, repeatability, seam/polar/knowledge regressions, save/memory/eviction/cancel budgets and presets. Safe player defaults must come from validated combinations.
9. **NGBUILD.2 → .3 → .4 → .5 → .6.** Player settings shell, Draft, Full, atomic acceptance/adoption/save creation, old-save regression. The freeze contract was designed earlier, but this is where actual Accept behavior ships. No runtime world regeneration after acceptance.

The settings shell could be built earlier in isolation, but its controls should not claim functionality before the geography/preset constraints exist. An Accept action with no prior Full Preview should still materialize and validate final truth before saving; optional visual inspection does not mean optional final generation.

### Suggested acceptance evidence for these passes

- Snapshot/packed hashes match repeated seed/settings runs; changed presentation knobs do not change truth hashes.
- Draft/Full share feature IDs, region membership and island archetype/positions; quantify any preview shoreline error.
- Different water targets reinterpret rather than resample raw features; report actual coverage and viable nodes/harbors.
- Seam/polar tests cover field, landmass connectivity, map/contours, sky registration and sailing.
- Load pre-pass raw U16 and legacy-float worlds without generator rerolls; unsupported/corrupt inputs fail explicitly.
- Accepted worlds restore without latest assets moving nodes/POIs/start harbor or invalidating chart clues under the agreed freeze policy.
- Cancel/reroll cannot mutate GameState, active terrain cache, knowledge, save files or decorative menu scene.
- Map zoom/masks hide unknown contours/shelves/POIs at every LOD; rendering never starts expensive synchronous generation on repaint.
- Timings distinguish fresh generation, cache reuse, Draft, Full, old-save load and disk save; record peak memory and missing-artifact recovery.

## 12. Source navigation

- [Current terrain generator](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyGenerator.cs:57>)
- [Live settings asset](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Defs/WorldMap/Topography/WorldMapTopographySettings.asset:17>)
- [Fresh source generation / eager textures](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyDebugSource.cs:146>)
- [Raw saved-height restoration](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyDebugSource.cs:493>)
- [Whole-settings fingerprint](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyFingerprint.cs:6>)
- [Field / seam sampling](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyField.cs:47>)
- [Topology / global bounds](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldTopology.cs:7>)
- [Water-target solver](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyAnalysis.cs:105>)
- [Current topography-first graph](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapGraphGenerator.cs:378>)
- [NodeScene generation configuration](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scenes/NodeScene.unity:11786>)
- [Runtime cache reuse and lifetime](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapRuntimeCache.cs:33>)
- [Affinity plan and cache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/ArchetypeCatalog.cs:55>)
- [Runtime node binding](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeBinder.cs:38>)
- [Harbor waterward solver](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/HarborGeometryQuery.cs:65>)
- [Contour band raster test](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyDebugTextureBuilder.cs:251>)
- [Map draw ordering](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:356>)
- [Shared world/star view transform](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/MapTable/MapTableViewportState.cs:9>)
- [Depth-knowledge concealment](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Fog/WorldMapBathymetryConcealment.cs:12>)
- [World snapshot](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveSnapshot.cs:15>)
- [Height persistence capture](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveBuilder.cs:92>)
- [U16 codec](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapTopographyHeightCodec.cs:9>)
- [Current New Game entry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Menu/MainMenuController.cs:147>)
- [Save gate and atomic JSON path](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:39>)
- [Deprecated pipeline warning](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/WorldGenerationPipelineRunner.cs:7>)

## 13. Proposed mapping to every handoff checkpoint

These are implementation mappings and recommended review boundaries, not work already performed.

### WGEO sequence

| Checkpoint | Existing code/assets to map | Proposed scope / dependencies |
| --- | --- | --- |
| WGEO.1 Audit/stage map | Generator/Source/Settings/Field, graph/affinity/biome/POI/harbor/save consumers | This document; baseline timed, no implementation. Review first |
| WGEO.2 Macro ocean | Generator.BuildFeatureSet, BasinFeature/TrenchFeature/EvaluateBaseNoise, settings | EXTEND macro plans/plains/ridges/negative space; REFACTOR sampling behind WPIPE identity/stages; preserve bounds/seams/coverage policy |
| WGEO.3 Regional geology | Generator feature placement + ArchetypeCatalog/ClusterAffinityDef | EXTEND stable upstream region geology; REFACTOR affinity bridge; avoid graph→terrain dependency cycle |
| WGEO.4 Archipelagos | ChainFeature, chain centers/Bezier shelves, PeakFeature | EXTEND persistent family/main/satellite/orientation/shared shelf plans; independent per-family seeds |
| WGEO.5 Island archetypes | PeakFeature/EvaluatePeak, settings | EXTEND/REPLACE uniform mound-only morphology with explicit archetype plan and versioned samplers |
| WGEO.6 Coast/interior | Field sampling, island samplers, node landmass candidate logic | EXTEND coast/interior hierarchy under chosen canonical fidelity; coordinate with MAPLOD.2/.3 |
| WGEO.7 Meso/micro/regional density | FractalNoise, peak roughness, biome metrics | EXTEND local detail keyed by upstream region/island; preserve macro identity; do not create biome feedback loop |
| WGEO.8 Regression/freeze | BiomeGenerator, GraphGenerator/Binder/settlement, HarborGeometryQuery, POIGenerator, knowledge, save/codec/celestial | Validate final-resolution derived systems and all legacy/freeze invariants; checks also run at preceding geography gates |

### MAPLOD sequence

| Checkpoint | Existing code/assets to map | Proposed scope / dependencies |
| --- | --- | --- |
| MAPLOD.1 Audit | DebugTextureBuilder, Source, Field, Cartridge/Viewport, mask/bake assets | This document: fixed raster, raster contours, sizes/zoom traced |
| MAPLOD.2 Multires foundation | Field, texture builders, Source/RuntimeCache, shared viewport | KEEP truth API; EXTEND bounded zoom-aware derived service; settle authoritative close-scale detail/storage first |
| MAPLOD.3 Close fidelity | Height/base color evaluation, geography sampling, map viewport | EXTEND actual accepted coast/relief/shelf fidelity; no extra resolution invented from old 512² saves |
| MAPLOD.4 Contour LOD | IsContour/BuildContourTexture, contour settings, map overlay draw | REPLACE fixed all-zoom overlay with hierarchy/zoom thresholds/pixel budget/simplification; split presentation key first |
| MAPLOD.5 Underwater/cartography | Height color evaluation, class thresholds, depth profile semantics, mask ordering | EXTEND shading/scale/feature readability and regional display density without rerolling truth or leaking depth knowledge |
| MAPLOD.6 Cache/size/performance | Bake subassets, RuntimeCache ownership, derived/mask caches, save exclusion of textures | REFACTOR disposable bounded caches; reduce redundant text texture bakes safely; timed zoom/knowledge/legacy regression |

### WPIPE sequence

| Checkpoint | Existing code/assets to map | Proposed scope / dependencies |
| --- | --- | --- |
| WPIPE.1 Audit/profile | Live generator/source/builders, downstream kernels, deprecated lab comparison | This document; measured terrain/builders, explicitly unmeasured stages identified |
| WPIPE.2 Stage boundaries | Generator feature planning/sampling; Source lifecycle; GraphGenerator versus Binder | REFACTOR immutable session artifacts, seeds/versioning, computation versus publication; reuse kernels rather than shelved runner behavior |
| WPIPE.3 Dirty graph | Fingerprint, RuntimeCache, ArchetypeCatalog cache, downstream direct inputs | REPLACE whole-settings/unchecked reuse with relevant dependency keys; preserve independent branches and correct prerequisites |
| WPIPE.4 Draft | Generator feature definitions, water solve/normalization, preview texture builder concepts | EXTEND same-plan Draft with shared interpretation policy; exclude unnecessary downstream/render/file work; prove no Full reroll |
| WPIPE.5 Workbench | Existing raw Settings/graph/POI fields; WorldGenCartridge UI ideas; current map view | EXTEND developer tabs/raw knobs/scoped regeneration over new shared orchestrator; don't revive alternate generator |
| WPIPE.6 Overlays | Base/contour/class/biome maps, graph/POI overlays plus new region plans | KEEP independent toggles; EXTEND macro/region/family/identity/dirty diagnostics; never alter truth merely by toggling visibility |
| WPIPE.7 Presets/profile/cache hardening | Settings assets, stage timings, cache ownership, progress/cancel | EXTEND versioned input presets, multi-seed benchmarks, bounded memory, cancel/failure atomicity; validate safe combinations for NGBUILD |

### NGBUILD sequence

| Checkpoint | Existing code/assets to map | Proposed scope / dependencies |
| --- | --- | --- |
| NGBUILD.1 Audit | MainMenuController, scene startup/cache, GameState, SaveGameService/snapshots | This document: current direct-load/reset, saved truth and lifecycle gaps traced |
| NGBUILD.2 Settings shell | MainMenu button/defaults, Settings and validated preset model | EXTEND isolated builder session and safe broad controls/dev access; delay current state reset; no unsupported controls |
| NGBUILD.3 Draft integration | WPIPE.4 artifacts + shared map transform/presentation | EXTEND pan/zoom/reroll preview without graph-required/player-knowledge side effects; preserve same plans |
| NGBUILD.4 Full Preview | WPIPE final pipeline + MAPLOD derived renderer | EXTEND final truth generation/progress/viability; don't silently regenerate different features after Draft |
| NGBUILD.5 Accept/freeze | SaveSnapshot/Builder/HeightCodec, GameState reset, scene adapters, SaveGameService gate | EXTEND accepted input/version/semantic manifest; adopt exact validated artifacts atomically; runtime save transaction after acceptance, not editor asset bake |
| NGBUILD.6 Migration/hardening | Restorer, CompatibilityDiagnostics, SaveSchema, cache lifecycle, celestial/knowledge persistence | KEEP supported legacy worlds; EXTEND strict validation/version dispatch/size budgets/cancel; ensure accepted inputs don't follow new Inspector defaults |

**Review stop:** no checkpoint beyond the four audits has been implemented. The next authorized implementation should begin only after this document is reviewed and the contract decisions above are agreed.
