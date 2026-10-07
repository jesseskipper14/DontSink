# DON'T SINK — World Generation V2: Geography
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** World-generation refinement focused on geography itself: macro seafloor, island/archipelago structure, coastlines, regional identity, and scale  
**Goal:** Preserve the current gameplay-scale world bounds while making the world read as vastly larger, more geologically coherent, more varied, and more interesting at every scale.

---

# 0. Exact-current-class rule

Before modifying any existing generator, topology, heightfield, biome, node, harbor, POI, or persistence class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields and existing behavior.
3. Preserve the current shared world/celestial coordinate system.
4. Preserve current baked-height / packed-height behavior unless deliberately superseded after audit.
5. Reuse current target-water-percentage, cluster-affinity, biome, node, harbor, and topology systems where useful.
6. Do not reconstruct current classes from memory or an older handoff.
7. Stop after every checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core problem

The current world has an excellent foundation, but its geography is too visually uniform in scale.

Current tendencies include:

- many similarly sized islands,
- insufficient distinction between giant regional forms and local noise,
- underwater bathymetry that is too uniformly busy,
- weak geographic hierarchy,
- coastlines that become blocky/unconvincing at close inspection,
- islands that visually read as much closer together than their actual gameplay travel distance.

The world does **not** need materially larger gameplay bounds.

The world needs to **look grander**.

---

# 2. Primary design rule

> Macro geography establishes the world. Local noise only refines it.

Do not allow small-scale noise to define the overall planet.

Generation should become hierarchical.

Conceptually:

```text
GLOBAL STRUCTURE
↓
MACRO SEAFLOOR FEATURES
↓
REGIONAL LAND / ARCHIPELAGO STRUCTURE
↓
INDIVIDUAL ISLAND ARCHETYPES
↓
MESO TERRAIN
↓
LOCAL DETAIL
↓
DERIVED SYSTEMS
```

Noise is seasoning, not the chef.

---

# 3. Preserve current world scale

Do not enlarge canonical world bounds just to create visual scale.

Existing travel distances are already meaningful.

The goal is that the map presentation and geography make those distances **feel** meaningful.

A 5–6 world-unit channel should visually read as a genuine body of water requiring travel, not a creek the player could wade across.

---

# 4. Scale comes from hierarchy and negative space

Use large empty or low-detail regions deliberately.

Examples:

- huge abyssal basin,
- enormous quiet plain,
- long trench,
- broad continental/shelf slope,
- large drowned plateau,
- isolated archipelago,
- single distant island.

Not every part of the ocean should have equal contour density or equal geographic drama.

Empty water is useful.

It makes dramatic features feel large.

---

# 5. Target water percentage remains a first-class constraint

The existing target-water-percentage concept is useful and should remain.

Current development values may remain in the low-90% water range.

Important rule:

> Island count / island amount and total land percentage are not the same control.

If the player requests many islands while total land remains fixed, islands become smaller on average.

If the player requests fewer islands at the same land percentage, islands may become larger.

---

# 6. Safe high-level island controls

Player-facing generation should eventually expose broad controls rather than raw generator math.

Important safe concepts include:

```text
Island Amount
Island Clumping
Landmass Style / World Preset
```

Do not expose low-level values such as trench curve exponents, octave amplitudes, erosion warp strengths, etc. to normal players.

---

# 7. Island clumping

Island clumping is a major world-character control.

### Loose
Islands / archipelagos are spread across much of the world.

### Clustered
Land is concentrated into separated island provinces with large ocean gaps between them.

### Dense
Clusters are more spatially compact and visually obvious.

Clumping should not automatically change total land percentage.

Use it to control spatial concentration.

---

# 8. Regional identity

World generation should create recognizable geographic regions.

Regional identity may be influenced by:

- existing cluster affinities,
- biome tendencies,
- geology/archetype weighting,
- macro seafloor features,
- island-family weighting.

Current ClusterAffinity concepts may be reused as guidance if appropriate, but do not force them to carry responsibilities they were not designed for.

---

# 9. Island archetypes

Do not generate every island through one universal shape process.

Support multiple explicit archetypes.

Initial target set:

### Volcanic Cone Island
- steep
- roughly radial
- strong central relief
- narrow shelf possible

### Complex Mountainous Island
- multiple peaks/ridges
- valleys
- irregular coastline
- potentially large

### Ridge / Elongated Island
- long directional spine
- associated satellites
- useful for arcs/chains

### Atoll
- low ring / broken ring
- lagoon
- shallow shelf language

### Low Shelf / Cay Group
- low relief
- broad shallow platform
- many small cays / islets

### Drowned Plateau / Peak Archipelago
- large submerged highland
- only highest peaks break surface
- broad shared underwater shelf

Exact names are flexible.

The archetype concept is not.

---

# 10. Regional tendencies + per-island selection

Do not roll every island independently from one global bag.

Preferred model:

```text
Region / geological province
→ archetype tendencies
→ individual island selection
```

Example:

```text
Volcanic Arc Region
→ volcanic cones + ridge islands heavily weighted
```

```text
Drowned Plateau Region
→ broad shelves + low peaks + cay groups
```

This produces coherent regional identity.

---

# 11. Archipelagos are first-class generated structures

An archipelago should not merely be “several islands that spawned near one another.”

An archipelago may own:

- shared underwater shelf or ridge,
- common orientation,
- dominant geological style,
- main islands,
- satellite islands,
- cays,
- channels,
- related coastal structure.

This should strongly improve map believability and scale.

---

# 12. Macro underwater features are explicit

Do not rely on generic noise to accidentally resemble underwater geography.

Support explicit macro features such as:

- abyssal plains,
- deep basins,
- major ridges,
- trenches,
- drowned plateaus,
- seamount provinces,
- broad shelves,
- long slopes,
- major submarine valleys/canyons.

These features may span huge portions of the map.

---

# 13. Large features should be truly large

Examples:

- trench spanning roughly a quarter of the world,
- giant basin occupying a major ocean region,
- ridge crossing several regions,
- massive drowned plateau supporting multiple island groups.

This is desirable for both presentation and gameplay.

A large trench can create meaningful travel/resource consequences:

> A long region where routine shallow diving is impossible becomes a strategic obstacle.

---

# 14. Feature density varies by region

Not all biomes/regions should have equal terrain complexity.

Examples:

### Abyssal Plain
- broad smooth forms
- sparse contours
- large negative space

### Volcanic Region
- frequent relief
- seamounts
- chimney/vent-associated forms
- dramatic elevation changes

### Shelf Sea
- channels
- shoals
- ridges
- varied shallows

### Trench Margin
- steep relief
- strong slope
- dramatic contour concentration

Feature density should be data-driven.

---

# 15. Coastline hierarchy

Coastlines should be generated in stages.

Preferred conceptual order:

```text
Primary island body
↓
Major bays / peninsulas / headlands
↓
Secondary coastal structure
↓
Small roughness/detail
```

Do not let one high-frequency noise pass decide the coastline at every scale.

This is critical for close-zoom quality.

---

# 16. Island interior hierarchy

Land interiors should visibly communicate terrain identity.

A player zooming into a large island should be able to distinguish:

- mountain ranges,
- peaks,
- ridges,
- valleys,
- plateaus,
- lowlands.

A mountainous island should not merely be “green blob with contour speckles.”

---

# 17. Meso terrain

After macro geography and island bodies are established, add meso-scale terrain:

- secondary ridges,
- valleys,
- submarine canyons,
- shelf breaks,
- side slopes,
- subsidiary seamounts,
- major coastal embayments.

This is where much of the “interesting at medium zoom” geography comes from.

---

# 18. Micro detail comes last

Only after larger structures are stable should local detail be added:

- coast roughness,
- small elevation variation,
- local rocks/bumps,
- minor bathymetric texture.

Micro detail must not erase macro readability.

---

# 19. Surface and underwater geography should relate

Island and seafloor generation should not feel like two unrelated random fields.

Examples:

- volcanic island may continue downward into a seamount/ridge,
- island chain may share a submerged arc,
- plateau islands share broad underwater highland,
- atolls sit on shallow platform structures.

This relationship strongly improves geographic credibility.

---

# 20. Derived systems remain downstream

The following should remain downstream consumers:

- land/shallow/deep classification,
- biomes,
- nodes,
- harbors,
- POIs,
- resources,
- seafloor generation,
- background visual weighting,
- discovery/knowledge layers.

Do not make geography depend on later gameplay systems.

---

# 21. Determinism

Generation should remain deterministic from:

```text
WorldSeed
+ GeneratorVersion
+ WorldGenerationParameters
+ stable stage/sub-seeds
```

The same accepted inputs should reproduce the same world truth.

---

# 22. Generator versioning

Generation changes can be substantial.

Therefore:

- new worlds may use new generator versions,
- accepted existing saves must not silently mutate when generator code changes,
- generator version must be stored with the world.

Persistence/freeze details are covered more deeply in the later New Game / acceptance pass.

---

# 23. Debug overlays

Worldgen debugging should become stronger.

Recommended development overlays:

- macro basin regions,
- trench paths,
- ridge paths,
- drowned plateaus,
- archipelago regions,
- island archetype labels,
- regional geology/profile labels,
- land/shallow/deep classification,
- depth gradient,
- slope,
- biome,
- cluster affinity,
- node candidates,
- harbor candidates,
- POI candidates later.

Debug information is explicitly desirable for this system.

---

# 24. Suggested stage model

Exact classes/names depend on live architecture, but conceptually:

```text
MacroTopologyStage
↓
RegionalGeologyStage
↓
ArchipelagoLayoutStage
↓
IslandShapeStage
↓
BathymetryStage
↓
MesoDetailStage
↓
MicroDetailStage
↓
ClassificationStage
↓
BiomeStage
↓
Node/Harbor/POI downstream stages
```

Do not force this exact class breakdown if current code suggests a cleaner equivalent.

---

# 25. Performance requirement

Generation quality matters, but this pass should remain compatible with the later fast-preview / incremental-generation pipeline.

Each stage should have clear inputs/outputs where practical.

Avoid unnecessary full-world recomputation inside local-detail operations.

---

# 26. Implementation checkpoints

## WGEO.1 — Audit + stage map
- inspect exact current generator pipeline,
- identify current macro, cluster, island, topo, classification, biome, node dependencies,
- identify where local noise currently dominates macro structure,
- document current data artifacts and timing.

**STOP FOR PLAYTEST / REVIEW.**

## WGEO.2 — Macro ocean structure
- basins,
- plains,
- major ridges,
- trenches,
- broad gradients,
- large-scale negative space.

**STOP FOR PLAYTEST.**

## WGEO.3 — Regional geology + clumping
- island-clumping model,
- regional profiles,
- cluster-affinity reuse where appropriate,
- stable region identity.

**STOP FOR PLAYTEST.**

## WGEO.4 — Archipelago structures
- shared shelves/ridges,
- main/satellite islands,
- regional orientation,
- coherent island families.

**STOP FOR PLAYTEST.**

## WGEO.5 — Island archetypes
- volcanic,
- mountainous,
- ridge,
- atoll,
- cay/shelf,
- drowned plateau peaks.

**STOP FOR PLAYTEST.**

## WGEO.6 — Coast/interior hierarchy
- bays,
- peninsulas,
- ridge/valley structure,
- better close-scale island form.

**STOP FOR PLAYTEST.**

## WGEO.7 — Meso/micro detail + regional density
- biome/region detail-density variation,
- local roughness,
- submarine canyons/seamounts,
- preserve macro readability.

**STOP FOR PLAYTEST.**

## WGEO.8 — Derived-system regression
- biomes,
- nodes,
- harbors,
- POIs,
- discovery,
- save compatibility,
- deterministic reproduction.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 27. Acceptance tests

1. Current gameplay-scale world bounds remain usable.
2. World visually feels larger than current map.
3. Large ocean gaps read as meaningful voyages.
4. Target water percentage still works.
5. Island amount and island clumping are distinct concepts.
6. Loose vs clustered land distributions are visibly different.
7. Multiple island archetypes are supported.
8. Regions bias island archetypes coherently.
9. Archipelagos are generated as related structures.
10. Macro underwater features are explicit, not accidental noise.
11. Trenches/ridges/basins may span large map fractions.
12. Abyssal plains can remain intentionally simple.
13. Feature density varies by region/biome.
14. Coastline structure has scale hierarchy.
15. Island interiors visibly show ridges/valleys/peaks/plateaus.
16. Surface land and submerged geology visually relate.
17. Local noise no longer overwhelms macro geography.
18. Downstream node/harbor/biome systems still function.
19. Generation remains deterministic/versioned.
20. Debug overlays clearly expose generated geography.

---

# 28. Non-goals

Do not turn this pass into:

- world-map UI redesign,
- contour-rendering redesign,
- New Game UI,
- quest generation,
- trade simulation,
- full seafloor POI generation,
- underwater visual-background pass,
- player navigation redesign.

Those have their own handoffs.

---

# 29. Final principle

The world should stop reading as:

```text
lots of similarly sized islands
over a uniformly noisy seafloor
```

and start reading as:

```text
huge ocean provinces
with distinct geological histories
containing meaningful island systems
separated by real stretches of water
```

The player should feel that choosing to leave one island for another is choosing to undertake a voyage.

---

**End of World Generation V2: Geography handoff.**
