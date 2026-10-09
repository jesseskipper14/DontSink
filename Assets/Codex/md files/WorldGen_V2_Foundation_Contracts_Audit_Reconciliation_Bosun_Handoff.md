# DON'T SINK — World Generation V2 Foundation Contracts & Audit Reconciliation
## Bosun Implementation Handoff — READ THIS BEFORE THE FOUR WORLDGEN HANDOFFS

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Status:** Architecture/reconciliation contract after exact-project audit  
**Purpose:** Resolve the live-code audit against the four world-generation handoffs before implementation begins.

This document is the **controlling reconciliation layer** for:

1. `WorldGen_V2_Geography_Bosun_Handoff.md`
2. `WorldMap_Cartography_LOD_Contours_Bosun_Handoff.md`
3. `WorldGen_Incremental_Pipeline_Workbench_Bosun_Handoff.md`
4. `NewGame_WorldBuilder_Preview_Accept_Bosun_Handoff.md`

If wording in one of those four files conflicts with this document, **this document wins**.

The audit this reconciles is `WORLD_GENERATION_FOUR_HANDOFF_AUDIT.md` dated 2026-10-07.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Preserve existing supported save/load paths unless deliberately migrated.
5. Stop at every checkpoint, compile/test, summarize exact changes, and wait for playtest approval.
6. Do not treat shelved/deprecated WorldGen Lab classes as current authority.

The audit confirmed that the live generator is **not** the old staged `WorldGenerationPipelineRunner` / `WorldGenerationWorkingSet` path.

Do not resurrect that pipeline as-is.

---

# 1. LOCKED PRODUCT DECISIONS

## 1.1 World gameplay scale stays essentially the same

Current canonical world bounds are already large enough from a gameplay/travel perspective.

Do **not** enlarge the world merely to create a grander feeling.

The goal is:

> Make the existing world LOOK and READ much larger.

The player should look at the map and feel that leaving for another island is committing to a voyage.

## 1.2 Canonical gameplay topography remains compact

The existing packed heightfield / canonical topography architecture is retained.

For V2, do **not** make a globally huge high-resolution physical heightfield a prerequisite for map quality.

Current 512×512 gameplay truth may remain the baseline unless profiling/implementation proves a compelling reason to change it.

Architecture should not hardcode 512 forever, but V2 should first improve geography planning, generation performance, cartographic rendering, and close-zoom visual detail without multiplying gameplay-truth resolution.

## 1.3 Extra close-zoom detail is CARTOGRAPHIC, not physical

When the player zooms deeply into the World Map, extra detail may be added visually.

That extra detail does **not** become:

- BoatScene collision
- BoatScene physical depth
- grounding truth
- sounding truth
- harbor truth
- POI placement truth
- resource-spawn truth

Canonical topography remains gameplay authority.

This keeps the upcoming dynamic seafloor/depth pass fully compatible.

## 1.4 Preview worlds are isolated until Accept

A generated candidate world must not mutate:

- `GameState`
- active gameplay `WorldMapRuntimeCache`
- gameplay knowledge
- live node state
- live celestial state
- current save data

Preview is an isolated builder session.

Only an explicit Accept transaction may publish the candidate into gameplay.

## 1.5 Accepted worlds are permanently frozen as gameplay worlds

Once a player accepts a world:

> Generator updates must never silently regenerate or reinterpret that save into a different physical world.

Old worlds restore their accepted canonical truth.

New generator versions apply only to new worlds unless an explicit migration is intentionally designed.

---

# 2. AUDIT CONCLUSION: KEEP THE GOOD FOUNDATION

The audit confirmed that the project already has valuable foundations:

- seam-aware canonical topography
- wrapped X / finite Y topology
- packed U16/Base64 height persistence
- saved graph/node/POI state
- separate derived map textures
- current water-percentage solving
- downstream gameplay consumers already sampling canonical field truth

Do not replace these merely because WorldGen V2 is ambitious.

The correct strategy is:

```text
KEEP canonical field/storage/query contracts

REFACTOR how geography is planned/generated

ADD semantic geology plans

ADD proper generation-stage identity/caching

REPLACE fixed low-fidelity map presentation

ADD isolated preview/accept lifecycle
```

---

# 3. TRUTH / INTERPRETATION / PRESENTATION ARE DIFFERENT THINGS

The current settings/fingerprint path mixes too many responsibilities.

V2 must separate them.

Conceptually:

```text
WORLD TRUTH
physical accepted geography and semantic world identity

INTERPRETATION
rules that turn normalized truth into gameplay categories/depth

CONTENT
nodes / POIs / biomes / settlements derived from accepted truth

PRESENTATION
map colors / line style / contour style / parchment / renderer LOD

KNOWLEDGE
what the player is allowed to see
```

A palette change must not regenerate terrain.

A contour-style change must not reroll islands.

A knowledge change must not dirty generator stages.

---

# 4. VERSION AXES MUST BE EXPLICIT

Do not continue conflating every kind of version into one number/string.

At minimum distinguish conceptually:

```text
SaveSchemaVersion
Topology / CoordinateFormatVersion
WorldGenerationAlgorithmVersion
WorldInterpretationVersion
WorldContent / ManifestVersion where needed
CartographyPresentationVersion
```

Exact class names are flexible.

Important behavior:

- topology version answers coordinate/seam semantics
- generation version answers how new worlds are created
- interpretation version answers accepted sea/class/depth semantics
- presentation version invalidates visual caches only
- save schema answers serialization compatibility

Unsupported versions should fail clearly.

Do not silently treat unknown generation strings as older topology.

---

# 5. WORLD IDENTITY CONTRACT

Introduce an immutable candidate/accepted world identity.

Conceptually:

```text
WorldIdentity
- root world seed
- canonical bounds/domain
- topology version
- world-generation algorithm version
- accepted truth-setting snapshot/hash
- accepted interpretation snapshot/hash
```

A separate unique world/save GUID may be assigned on acceptance.

The important rule:

> A runtime cache artifact is valid only for the WorldIdentity it was created for.

“Any valid cached field” is no longer sufficient.

---

# 6. PREVIEW SESSION CONTRACT

Introduce a candidate-generation session that owns temporary artifacts.

Conceptually:

```text
WorldBuilderSession
- CandidateInput
- CandidateWorldIdentity
- temporary geology plan
- temporary heightfield
- temporary derived artifacts
- validation results
- timings
```

The session must not publish to normal gameplay services.

Do not use gameplay singleton state as preview scratch storage.

---

# 7. NUMERIC GENERATION MUST STOP DEPENDING ON SCENE PUBLICATION

The live project currently coordinates generation through scene lifecycle, Awake/Start, fallback calls, static/global services and runtime binders.

For World Builder use, separate:

```text
PURE/ISOLATED GENERATION
inputs → artifacts

from

GAMEPLAY PUBLICATION
accepted artifacts → runtime services / GameState / scenes
```

Existing MonoBehaviours may remain adapters/consumers.

They should not remain the only way to create world truth.

---

# 8. THE GENERATION PIPELINE IS A DAG, NOT A SIMPLE LINE

The audit found that biomes, POIs and nodes have independent branches after interpreted topography.

Do not force false dependencies simply because the original handoff showed a linear example.

Target conceptual graph:

```text
WorldIdentity / Input Snapshot
│
├─ Macro + Regional Geology Plan
│  └─ Archipelago + Island Plans
│     └─ Canonical Terrain Sampling
│        └─ Sea / Classification / Gameplay Interpretation
│           ├─ Biome Assignment
│           ├─ Node Candidates → Graph
│           │                  ├─ Economic Affinity / Archetypes
│           │                  ├─ Settlement Manifest
│           │                  └─ Harbor Definitions / Start Viability
│           ├─ POI Candidates → POI Placement
│           └─ Cartographic Derived Data
│
└─ Celestial Generation Inputs / Domain
   └─ Celestial Truth
```

Gameplay knowledge initializes **after accepted artifacts exist**.

---

# 9. REGIONAL GEOLOGY IS NEW UPSTREAM IDENTITY

Existing `ClusterAffinityDef` is economic/settlement identity.

Do not feed it backward into terrain generation.

Create a distinct upstream concept such as `GeologyRegionPlan` or equivalent.

It may contain:

- regional geology type
- island-family tendencies
- feature density
- archipelago tendency
- macro depth tendency
- volcanic/plateau/ridge tendencies
- stable region ID

Then downstream economic/settlement affinity may optionally be influenced by regional geology.

Direction is:

```text
Geology
→ terrain
→ nodes
→ economy/settlement affinity
```

Never the reverse.

---

# 10. SEMANTIC GEOLOGY PLANS SHOULD PERSIST FOR V2 WORLDS

The current generator discards its temporary chains/basins/trenches after rasterizing them.

V2 should retain a compact semantic plan for new worlds.

Useful persistent concepts include:

- macro feature IDs
- geology region IDs
- archipelago IDs
- island IDs
- island archetypes
- main/satellite relationships
- shared ridge/shelf relationships
- major basin/ridge/trench identity

Why keep this compact plan?

1. It provides stable regional identity.
2. It helps cartographic rendering.
3. It enables deterministic per-feature RNG.
4. It supports future background/POI systems.
5. It avoids trying to infer geology semantics back out of a raster later.

The physical raster remains canonical gameplay height truth.

The plan explains how that world was composed.

---

# 11. STABLE RNG STREAMS

Replace the single mutable terrain RNG chain for V2 new worlds.

Use named/versioned deterministic streams.

Conceptually:

```text
RootWorldSeed
├─ MacroGeologySeed
├─ RegionalPlanSeed
├─ ArchipelagoSeed(regionId)
├─ IslandSeed(islandId)
├─ DetailSeed(featureId)
├─ BiomeSeed
├─ NodeSeed
├─ POISeed
└─ CartographySeed
```

Adding a trench should not accidentally reroll every later island because one shared `System.Random` consumed an extra value.

Persistent IDs must not depend on:

- raster resolution
- transient list ordering
- thread/task ordering
- locale
- object hash codes

---

# 12. WATER TARGET POLICY FOR V2

Legacy worlds keep their existing accepted sea level and semantics.

For new V2 worlds, define the player/developer “target water percentage” against the **unique wrapped playable ocean domain**, not duplicate seam samples.

Recommended V2 policy:

- count unique wrapped-X samples only
- exclude duplicated seam endpoint
- treat polar boundary foundation as a separate topology/boundary system rather than letting polar-width changes consume the island/ocean land budget
- report achieved water percentage explicitly

This makes Island Amount / Island Clumping / target-water concepts easier to reason about.

Changing water target:

- does not reroll macro features
- reinterprets existing sampled heights
- dirties classification and downstream consumers

Document this as new V2 semantics rather than silently changing legacy behavior.

---

# 13. ISLAND AMOUNT, CLUMPING AND WATER PERCENT ARE SEPARATE

Keep these independent.

```text
Target Water %
= broad water/land ratio

Island Amount
= number/frequency of surface landforms

Island Clumping
= spatial concentration of landforms
```

At similar land percentage:

```text
few islands
→ larger average islands

many islands
→ smaller average islands
```

Clumping changes distribution, not total land budget.

---

# 14. DRAFT PREVIEW CONTRACT

The audit correctly found that:

> Draft cannot simply run the current generator at a lower height resolution and promise exact coastlines.

V2 Draft rules:

### Draft MUST preserve
- root seed
- macro feature IDs
- geology regions
- archipelago membership
- island positions
- island archetypes
- major ridge/trench/basin positions
- broad depth character

### Draft MAY approximate
- exact shoreline
- tiny cays
- minor contour placement
- micro detail
- final node/harbor/POI viability
- presentation caches

Draft is a **geographic decision preview**, not accepted gameplay truth.

---

# 15. DRAFT / FULL SEA POLICY

Draft and Full must share the same semantic geology plan.

Avoid using two completely independent normalization/water solves that can wildly change the world.

Implementation may use a canonical calibration/interpretation plan so Draft approximates final shoreline consistently.

Exact technique is left to Bosun after profiling.

Required acceptance evidence:

- same region IDs
- same archipelago IDs
- same major island IDs/positions
- same archetypes
- same macro feature IDs
- quantified shoreline deviation between Draft and Full

Minor shoreline differences are acceptable.

Major island appearance/disappearance is not.

---

# 16. ACCEPT FROM DRAFT MUST FINALIZE FIRST

An accepted save must never be created directly from approximate Draft output.

Preferred user flow:

```text
Draft looks good
↓
player presses Accept / Continue
↓
Full generation + final validation runs
↓
final world is shown/confirmed
↓
Accept commits exact Full artifacts
```

If a prior Full Preview already exists and is still clean/current, reuse it.

No hidden reroll.

---

# 17. PERFORMANCE TARGET

Current measured baseline from the audit:

```text
512² height generation      ~20.5 s
water target solve           ~0.05 s
four 2048² textures          ~5.3 s
packed U16/Base64 encode     ~0.005 s
```

A few-second Draft remains a strong target.

Do not promise exact 3.0 seconds before implementation/profiling.

The largest opportunity is the terrain kernel, not the water solver.

---

# 18. FIRST PERFORMANCE OPTIMIZATIONS SHOULD BE BORING AND SAFE

Before reaching for Jobs/Burst/GPU rewrites:

1. Split feature planning from raster sampling.
2. Precompute reusable Bezier/curve segments.
3. Give features conservative world-space support bounds.
4. Skip feature math for cells nowhere near that feature.
5. Consider coarse spatial indexing of macro features.
6. Avoid eager generation of debug/contour/classification textures when not requested.
7. Time each stage.
8. Add cancellation/yield boundaries where practical.

The audit found roughly ~80 million structured segment-distance checks in the current terrain path.

Stop doing obviously irrelevant work first.

---

# 19. BASELINE EQUIVALENCE GATE

Before deliberate geography changes:

- capture known baseline seeds
- capture packed height hashes
- capture sea level/water percentage
- capture node count/start node
- capture selected harbor viability
- capture representative map images if useful

If an optimization claims to be output-equivalent, prove it against these baselines.

If an optimization deliberately changes generated truth, bump the new-world generator version.

Old saved worlds still restore packed truth.

---

# 20. CARTOGRAPHIC CLOSE-DETAIL CONTRACT

This is now resolved:

> Extra deep-zoom map detail is visual only.

Do NOT increase physical gameplay truth solely to make the map prettier.

The renderer may derive higher-detail visual representation from:

- canonical heightfield
- semantic geology plans
- stable cartography seeds
- island archetypes
- region identity

But it cannot publish those embellishments back into gameplay truth.

---

# 21. DYNAMIC SEAFLOOR COMPATIBILITY

The upcoming dynamic seafloor/depth system remains based on canonical gameplay topography.

Hard dependency direction:

```text
Canonical World Topography
├─ BoatScene dynamic seafloor
├─ physical grounding
├─ sounding/depth truth
├─ harbor geometry
├─ POI/resource truth
└─ World Map cartography
      └─ optional visual-only embellishment
```

Never:

```text
World Map embellishment
→ physical seafloor
```

This guarantees the map pass cannot destabilize BoatScene depth generation.

---

# 22. CARTOGRAPHIC EMBELLISHMENT MAY NOT CONTRADICT TOPOLOGY

Visual enhancement may refine appearance, but must preserve broad canonical meaning.

It must not create:

- a new island in canonical open water
- a land bridge across a canonical channel
- a fake navigable channel through canonical land
- a fake shoal implying grounding danger
- a fake deep trench where physical gameplay is shallow

The renderer may make a coastline line more organic.

It may not change landmass connectivity.

---

# 23. LAND RELIEF HAS MORE VISUAL FREEDOM

Global land interiors are not BoatScene physical traversal truth.

Therefore close-zoom land cartography may use strongly illustrative relief:

- hand-drawn mountain symbols
- ridge hachures
- valley lines
- plateau shading
- peak clusters

These should be grounded in:

- island archetype
- semantic geology plan
- canonical elevation tendency

They do not need to be a literal collision mesh.

This is a major tool for making islands feel huge at close zoom.

---

# 24. UNDERWATER CONTOURS MUST REMAIN TRUTHFUL

Underwater bathymetry is gameplay-relevant.

Therefore:

> Do not invent fake underwater contour extrema just to create visual complexity.

Underwater contours may be:

- smoothly interpolated
- simplified
- stylized
- given hand-drawn line character
- selectively shown by zoom

but they should still derive from canonical bathymetric truth / frozen depth interpretation.

Decorative underwater hachures/textures are allowed if they cannot be mistaken for quantitative depth contours.

---

# 25. COASTLINE STYLIZATION

Coastlines may be smoothed/stylized for cartographic presentation.

Allowed:

- anti-aliasing
- curve fitting
- small deterministic line wobble
- ink-like irregularity
- artistically cleaner headlands/bays within a tiny visual tolerance

Not allowed:

- changing island connectivity
- opening/closing meaningful channels
- adding/removing major islands

The canonical sea-level boundary remains authoritative.

---

# 26. NO FORCED "ISLAND-ONLY VIEW SWAP"

The player should ideally remain in one continuous World Map viewport while zooming.

Preferred implementation:

```text
coarse full-world representation
→ medium LOD
→ visible-region high-detail cartographic tiles/layers
```

not:

```text
world map
→ abrupt separate island map screen
```

A dedicated close-island view remains a fallback only if continuous LOD proves impractical.

It is not the first design.

---

# 27. CARTOGRAPHIC AESTHETIC — LOCKED

The target visual language is:

> A muted-color, vintage, hand-drawn fantasy expedition map with Tolkien-like simplicity and scale.

Not black-and-white.

Not modern GIS.

Not bright technical heatmap presentation.

Desired characteristics:

- pale parchment / aged-paper feel
- muted light blue/blue-green water
- muted tan/green/brown land
- soft dark-gray/brown ink rather than pure black
- illustrated ridges/mountains
- subtle irregular hand-drawn line character
- restrained bathymetric color bands
- elegant major/minor contour hierarchy
- plenty of negative space
- visually calm at whole-world zoom

The map should feel old, tactile and exploratory while still being mechanically precise where precision matters.

---

# 28. LOD SHOULD REVEAL MORE ILLUSTRATION, NOT JUST MORE PIXELS

At far zoom:

- broad island silhouettes
- major mountain-range suggestion
- major underwater features
- sparse major contours

At medium zoom:

- more ridge/valley illustration
- shelf shape
- intermediate contours
- better coastline character

At close zoom:

- rich land relief strokes
- local coastline linework
- more truthful contour detail
- local bathymetric features
- high-detail parchment/cartographic texture where useful

The player should feel:

> “This island is enormous.”

Not:

> “I zoomed into a low-resolution blob.”

---

# 29. HAND-DRAWN VARIATION MUST BE STABLE

Do not let decorative line wobble or mountain placement “swim” while panning/zooming.

Use world-space deterministic seeds / feature IDs.

LOD transitions should:

- preserve feature identity
- add detail progressively
- crossfade or transition cleanly
- avoid random re-layout per zoom level

---

# 30. MAP ASPECT RATIO SHOULD RESPECT WORLD DOMAIN

Current derived textures are square despite a 720×450 world domain.

The new cartographic renderer/cache should be world-aspect-aware.

Do not waste resolution by stretching square raster products where a tiled/aspect-correct representation is cleaner.

Canonical gameplay field layout may remain unchanged.

This is a presentation-layer concern.

---

# 31. CONTOUR PRESENTATION

Current fixed 100-band raster contour overlay is not retained as the final presentation model.

Requirements:

- zoom-aware major/minor hierarchy
- screen-space minimum spacing
- screen-space line-width control
- feature-aware simplification
- no contour spaghetti
- no duplicate embedded + overlay contour rendering
- presentation settings separated from world truth hash

Exact implementation may be:

- marching-squares/polyline
- tiled raster
- shader-derived
- hybrid

Choose based on profiling and mask correctness.

---

# 32. KNOWLEDGE / FOG MUST FAIL CLOSED

This is critical.

The new LOD renderer must never leak unknown truth through:

- tile borders
- interpolation
- async fallback
- contour overlays
- decorative relief
- cached high-detail tiles

Gameplay composition rules:

### Surface not known
No surface terrain detail.

### Surface known, bathymetry unknown
Surface map/ocean presentation only.
No hidden depth contours.

### Surface + bathymetry known
Bathymetric detail may render.

Preview mode may display all truth through a separate preview permission context.

Preview must never mutate gameplay knowledge.

---

# 33. CARTOGRAPHIC CACHE IS DISPOSABLE

Map LOD tiles, contour caches, line meshes, debug overlays, and presentation textures are not accepted world truth.

They may be regenerated from:

- canonical accepted truth
- semantic geology plan
- cartography seed
- presentation version

Do not put giant derived textures in the save merely because they are convenient.

---

# 34. FILE-SIZE POLICY

The audit found nearly 97 MiB of current editor bake assets, largely from embedded texture subassets.

Do not confuse this with player-save size.

Player saves already store compact packed heights rather than those giant textures.

V2 goals:

- keep compact packed canonical truth
- eliminate redundant persistent derived textures where practical
- make map caches disposable
- eventually consolidate/remove stale duplicate scene bake assets once migration is safe
- do not increase global physical resolution merely to solve visual zoom

---

# 35. ACCEPTED-WORLD FREEZE SCOPE

For V2 accepted worlds, freeze enough data to preserve gameplay identity.

At minimum:

- root world seed
- world bounds/domain
- topology version
- generator algorithm version
- accepted truth-setting snapshot
- canonical packed heightfield
- resolved sea level
- truth-relevant classification thresholds
- frozen physical depth interpretation/profile values or an immutable versioned equivalent
- compact semantic geology/region/archipelago/island plan
- graph node IDs/positions/geographic grouping
- starting node
- generated POI IDs/positions
- semantic biome assignments if gameplay consumes them
- persistent settlement/node state through existing systems
- enough celestial generation identity/configuration to preserve chart clues
- any other generated semantic artifact whose movement would invalidate saved player knowledge

---

# 36. HARBOR FREEZE POLICY

Canonical node/harbor identity must not move merely because the harbor solver code changes later.

For V2:

- NodePosition / HarborPosition remains frozen with the accepted node.
- Persist or version enough node-stable harbor geometry, especially waterward direction / canonical approach identity, that accepted harbors remain recognizable.
- Boat-specific berth/clearance checks may still depend on current boat dimensions.

Full acceptance must at least validate that the starting harbor is usable by the starting boat.

Do not require every future giant boat to fit every harbor.

---

# 37. PRESENTATION MAY UPGRADE

The following may evolve without changing gameplay world truth:

- parchment texture
- palette
- line thickness
- mountain icon art
- contour stroke shader
- fonts
- UI layout
- renderer performance strategy
- cartographic LOD cache format

Presentation upgrades must preserve:

- canonical geography
- knowledge boundaries
- semantic coordinates
- depth meaning where contours are quantitative

---

# 38. BIOME FREEZE POLICY

Current biomes are derived from topography + catalog rules.

For V2 accepted worlds:

- freeze stable semantic biome assignment where gameplay/background/resource systems may consume it
- do not permanently store fragile catalog indices without stable IDs
- allow biome artwork/presentation to improve later

Do not let a catalog rebalance silently transform an existing island from one semantic biome into another if gameplay depends on it.

---

# 39. CELESTIAL FREEZE POLICY

World/celestial coordinate registration remains 1:1.

Accepted world must retain enough celestial generation identity/configuration that:

- chart clues remain valid
- named celestial objects do not move because an asset default changed
- constellation evidence remains coherent

Decorative Main Menu sky remains separate.

---

# 40. NEW GAME ACCEPTANCE TRANSACTION

Preferred architecture:

```text
Main Menu
↓
isolated WorldBuilderSession
↓
Draft(s)
↓
Full finalization + validation
↓
AcceptedWorldPackage created in memory
↓
explicit Accept
↓
transition to NodeScene
↓
scene adopts package BEFORE fallback generation/cache reuse
↓
gameplay state/knowledge initialized
↓
initial save written after successful adoption
```

This preserves the current NodeScene-centric save gate where practical.

If live code makes a safe MainMenu save transaction cleaner, Bosun may propose it before implementation.

Hard rule either way:

> Reject/cancel must never mutate current gameplay world or create a half-valid save.

---

# 41. SCENE FALLBACK POLICY

Accepted/loaded worlds must not quietly fall back to:

- serialized seed-2 NodeScene graph
- stale runtime cache
- unrelated assigned bake
- BoatScene legacy generation mode

For production accepted/load paths:

> Missing/incompatible accepted truth should fail clearly.

Scene-authored fallback generation may remain for explicit development/test workflows.

Make that distinction explicit.

---

# 42. RUNTIME CACHE IDENTITY

`WorldMapRuntimeCache` or its successor must be keyed/validated by world identity.

Required behaviors:

- New Game candidate preview never publishes to gameplay cache.
- Accept swaps/publishes exact accepted artifacts.
- Load rejects cache entries from another world.
- New Game/load lifecycle explicitly clears or changes identity.
- A valid-but-wrong field is never accepted merely because `IsValid == true`.

---

# 43. FINGERPRINT / HASH SPLIT

Current whole-settings fingerprint invalidates terrain for presentation-only changes.

Replace with responsibility-scoped keys.

Conceptually:

```text
TruthKey
InterpretationKey
BiomeKey
NodeKey
POIKey
CartographyPresentationKey
KnowledgeRevision
```

Each cache/stage key includes only inputs that truly affect that artifact.

Example:

Changing contour color:

```text
dirty CartographyPresentation only
```

Changing water target:

```text
reuse raw sampled terrain
dirty sea interpretation
→ classification
→ biomes/nodes/harbors/POIs
→ map presentation
```

---

# 44. ECONOMIC AFFINITY CACHE MUST BE HARDENED

The audit found the current archetype/affinity plan hash is too weak.

When touched as part of WPIPE/node integration, include sufficient identity such as:

- node stable IDs
- positions/geographic groups as relevant
- graph/world identity
- catalog/settings version/hash

Do not reuse a same-count plan for a different regenerated graph.

---

# 45. EMPTY IS A VALID ACCEPTED RESULT

Distinguish:

```text
artifact missing
```

from:

```text
artifact generated successfully and contains zero items
```

This specifically matters for POIs and may matter elsewhere.

Do not regenerate an intentionally empty accepted artifact just because count == 0.

---

# 46. STRICT HEIGHT PAYLOAD VALIDATION

Accepted/restored packed height data must validate:

- encoding
- expected byte count
- dimensions
- checksum/hash where practical

Do not decode partial payload and silently fill missing samples with zero.

Corrupt accepted truth should fail clearly.

---

# 47. PLAYER-FACING SAFE SETTINGS — REVISED INITIAL SET

Initial safe New Game controls should remain broad.

Recommended initial set:

```text
Seed
World Preset
Island Amount
Island Clumping
Landmass Style
Ocean Depth
Biome Variety
```

`Danger / Harshness` is **deferred** until it maps to real implemented world/game systems.

World Scale is fixed for now.

Raw target-water percent and generator math remain Developer/Advanced controls unless later wrapped in a safe preset.

---

# 48. FULL VALIDATION BEFORE ACCEPT

Full accepted generation should validate at least:

- canonical field valid
- requested water ratio within tolerance
- wrapped seam valid
- polar boundaries valid
- minimum viable settlement/node distribution
- valid starting node
- usable starting harbor for starting boat
- graph/node stable IDs valid
- POI artifact valid even when empty
- knowledge initialization can start cleanly
- celestial identity/domain valid

Do not make legacy “rightmost Destination” a required acceptance concept.

---

# 49. NODE / POI / HARBOR VALIDATION MUST USE FULL TRUTH

Draft is not enough.

Final validation occurs using accepted Full canonical truth.

This is especially important because:

- node landmass candidates are resolution-sensitive
- harbor search depends on field sampling
- water interpretation may differ slightly from Draft

---

# 50. NO BIOME FEEDBACK LOOP

Derived biome raster must not feed backward into terrain that determines that same biome raster.

If geography needs regional climate/flavor:

- generate an upstream region descriptor
- use it to influence terrain
- derive final biome assignment afterward

---

# 51. LEGACY SAVE POLICY

Legacy supported saves continue to load their packed accepted truth.

Do not run V2 terrain generation over them.

New Tolkien-style map presentation may be applied to old worlds as a renderer upgrade, subject to:

- same canonical field
- same knowledge masks
- no invented gameplay bathymetry

Legacy worlds without V2 semantic geology plans may use a deterministic visual fallback derived from their field/seed.

Do not fabricate new gameplay semantic world features in an old save.

---

# 52. MAP VIEWER CONTEXTS

Map rendering should support explicit viewer contexts.

Conceptually:

```text
GameplayMapContext
- obeys player knowledge

WorldBuilderPreviewContext
- can show candidate truth
- no gameplay state mutation

DeveloperDebugContext
- can show internal overlays
```

Do not implement preview omniscience as “temporarily reveal everything in gameplay knowledge.”

---

# 53. CORRECTED IMPLEMENTATION ORDER

Do not finish the four original handoffs one-by-one in isolation.

Use this interleaved sequence.

## FOUNDATION.1 — Identity / isolation / baseline

Before geography changes:

- capture repeatable baseline seeds/hashes/timings
- introduce candidate session/world identity contracts
- separate generator computation from publication enough for isolated generation
- add explicit version axes
- harden cache identity

This corresponds mainly to WPIPE.2 plus minimal NGBUILD/MAPLOD contracts.

**STOP FOR REVIEW/PLAYTEST.**

## FOUNDATION.2 — Dirty graph / safe baseline optimization

- scoped artifact keys
- independent RNG streams
- feature-plan/sampling split
- precompute feature geometry
- feature support bounds/spatial rejection
- lazy derived textures
- timing instrumentation

Preserve baseline output where claiming equivalence.

**STOP FOR REVIEW/PLAYTEST.**

## GEOGRAPHY PHASE — WGEO.2 → WGEO.5

Implement:

- macro ocean provinces
- regional geology
- archipelago plans
- island archetypes

Run viability checks incrementally.

Do not wait until the end to discover that every beautiful island has no harbor.

## CARTOGRAPHY FOUNDATION + GEOGRAPHY DETAIL

Proceed with:

- MAPLOD.2 derived multires renderer
- visual-only close-detail contract from this file
- WGEO.6 coast/interior hierarchy
- WGEO.7 meso/micro detail
- MAPLOD.3 close fidelity

Canonical physical resolution need not increase for the map.

## DRAFT PHASE — WPIPE.4

Build fast Draft from the same semantic plans.

Benchmark multiple seeds/settings.

Document Draft↔Full shoreline error.

## WORKBENCH + MAP STYLE

Proceed with:

- WPIPE.5 workbench
- WPIPE.6 overlays
- MAPLOD.4 contour LOD
- MAPLOD.5 Tolkien/vintage cartographic presentation

## HARDENING

Proceed with:

- WGEO.8
- WPIPE.7
- MAPLOD.6

Validate:

- node/harbor/POI viability
- seam/poles
- knowledge
- old saves
- memory
- cache eviction
- cancellation
- repeatability
- file sizes

## NEW GAME BUILDER LAST

Then:

- NGBUILD.2 settings shell
- NGBUILD.3 Draft integration
- NGBUILD.4 Full
- NGBUILD.5 Accept/adopt/save
- NGBUILD.6 migration/hardening

The freeze contract is designed early but the UI ships after the generator/pipeline is trustworthy.

---

# 54. ORIGINAL HANDOFF OVERRIDES / AMENDMENTS

## `WorldGen_V2_Geography_Bosun_Handoff.md`

Keep its geography goals.

Amend:

- do not use current economic ClusterAffinity as upstream geology
- create separate geology-region identity
- preserve compact canonical field
- add semantic geology plans
- use named RNG streams
- use V2 water-target policy from this document

## `WorldMap_Cartography_LOD_Contours_Bosun_Handoff.md`

Major clarification:

- higher close-zoom detail is **derived visual cartography**, not new gameplay terrain truth
- no global high-resolution physical raster is required
- land relief may be highly illustrative
- underwater quantitative contours remain canonical
- adopt vintage muted-color hand-drawn fantasy-map aesthetic
- continuous zoom/LOD preferred over separate island-only view

## `WorldGen_Incremental_Pipeline_Workbench_Bosun_Handoff.md`

Keep its staged/dirty goals.

Amend:

- stage graph is a DAG
- do not revive shelved lab pipeline
- establish session-scoped artifacts and publication boundary first
- Draft shares semantic feature plans but may approximate shoreline
- a few-second Draft is a target, not a guarantee
- optimize the 20.5-second terrain kernel before premature platform rewrites

## `NewGame_WorldBuilder_Preview_Accept_Bosun_Handoff.md`

Amend:

- preview session must be isolated from runtime singletons/GameState
- Accept from Draft must run/verify Full first
- exact accepted physical world freezes
- presentation may upgrade separately
- initial `Danger/Harshness` safe setting is deferred
- avoid current direct New Game → NodeScene fallback semantics
- adopt accepted package before scene fallback generation

---

# 55. PERFORMANCE ACCEPTANCE EVIDENCE

At relevant checkpoints Bosun should report:

- fresh Full generation timing
- Draft timing
- cache-reuse timing
- old-save load timing
- per-stage timings
- peak memory where practical
- number of generated/culled feature evaluations if instrumented
- texture/cache memory
- accepted save size
- generated project bake/cache size where relevant

Benchmark more than seed 2.

Use representative settings:

- sparse/loose
- dense/clumped
- trench-heavy
- archipelago-heavy
- high island amount

---

# 56. VISUAL ACCEPTANCE EVIDENCE

For MAPLOD / vintage cartography checkpoints, provide screenshots at:

- whole-world zoom
- medium regional zoom
- close island/channel zoom
- very close zoom

Test at least:

- large mountainous island
- atoll/cay region
- archipelago
- giant trench
- abyssal plain
- narrow but gameplay-meaningful channel

Goal:

> Every zoom level should preserve or increase the sense of world scale.

---

# 57. KNOWLEDGE REGRESSION AT EVERY MAP CHECKPOINT

Do not postpone knowledge testing until final hardening.

At every new LOD/contour/render stage verify:

- unknown surface stays hidden
- known surface / unknown bathymetry does not leak depth
- underwater POIs remain hidden until allowed
- tile transitions do not leak
- async fallback does not leak
- preview context does not mutate gameplay knowledge

---

# 58. DYNAMIC SEAFLOOR REGRESSION

After geography changes, verify:

- BoatScene samples canonical accepted field
- wrapped X seam remains correct
- physical depths remain compatible with saved interpretation
- close-map embellishments never affect BoatScene
- dynamic seafloor scan/sounding systems consume canonical depth only

This is a mandatory regression whenever MAPLOD or visual cartography changes.

---

# 59. BOSUN REPORTING FORMAT

After each checkpoint, report:

1. Exact files inspected
2. Exact files modified
3. New files/classes
4. World identity/artifact model
5. Seed/RNG model
6. Cache keys invalidated
7. What remained reusable
8. Truth vs presentation changes
9. Draft/Full behavior
10. Timings
11. Save/load impact
12. Knowledge/fog regression
13. Dynamic seafloor regression
14. Node/harbor/POI regression
15. Any deviation from this reconciliation contract

Do not continue automatically past a requested playtest gate.

---

# 60. FINAL ARCHITECTURAL SUMMARY

The target system is:

```text
SAFE/RAW GENERATION INPUTS
        ↓
ISOLATED WORLD BUILDER SESSION
        ↓
SEMANTIC GEOLOGY PLAN
        ↓
CANONICAL COMPACT HEIGHTFIELD
        ↓
FROZEN GAMEPLAY INTERPRETATION
        ↓
├─ BIOMES
├─ NODES / HARBORS
├─ POIs
├─ DYNAMIC BOAT-SEAFLOOR DEPTH
├─ CELESTIAL DOMAIN
└─ CARTOGRAPHIC RENDERER
       ↓
       muted hand-drawn vintage fantasy-map visual LOD
       + visual-only close detail
       + truthful bathymetric contours
       + gameplay knowledge masks

ACCEPT
        ↓
IMMUTABLE ACCEPTED WORLD PACKAGE
        ↓
GAMEPLAY PUBLICATION
        ↓
SAVE
```

The most important separation is:

```text
THE WORLD
is compact canonical gameplay truth.

THE MAP
is a beautiful, progressively detailed interpretation of that truth.

THE MAP MAY LOOK MORE DETAILED THAN THE PHYSICAL RASTER,
but it may never become a second contradictory world.
```

---

# 61. AUTHORIZATION BOUNDARY

The exact-project audit is complete.

This reconciliation resolves the major contract decisions required before implementation.

The next implementation work should begin with:

**FOUNDATION.1 — identity / isolation / baseline**

not WGEO visual changes directly.

Bosun should stop after FOUNDATION.1 for review/playtest before proceeding.

---

**End of World Generation V2 Foundation Contracts & Audit Reconciliation handoff.**
