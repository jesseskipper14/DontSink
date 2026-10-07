# DON'T SINK — World Map Cartography, Zoom Fidelity & Contour LOD
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** World-map rendering and cartographic fidelity across zoom levels  
**Goal:** Make zooming in reveal more meaningful geography instead of merely enlarging pixels, while keeping contours readable, stylized, performant, and derived from authoritative world truth.

---

# 0. Exact-current-class rule

Before modifying existing map, texture, contour, topography, shader, world-map UI, cache, or baked-data classes:

1. Inspect exact current source.
2. Audit current contour and map-texture generation before replacing anything.
3. Preserve world/celestial 1:1 coordinate authority.
4. Preserve current topography truth and save compatibility unless deliberately migrated.
5. Reuse current packed heightfield/runtime-cache systems where sensible.
6. Do not reconstruct current classes from memory.
7. Stop after each checkpoint for compile/test/playtest.

---

# 1. Core requirement

Hard rule:

> Zooming in should reveal more geography, not just bigger pixels.

At close zoom the player should continue to see:

- meaningful coastlines,
- mountain/highland structure,
- valleys/ridges,
- shelf edges,
- underwater slopes,
- local bathymetric features,
- readable contours.

Close zoom should make the world feel **larger**.

It must not make islands look thirty feet apart.

---

# 2. Stylized cartography, smoother behavior

The target is:

> Stylized in spirit, but smooth/cartographic in behavior.

Do not require photoreal GIS rendering.

Do require:

- visually coherent curves,
- clean silhouettes,
- legible contour hierarchy,
- good depth/color separation,
- strong feature readability.

Blocky enlargement is not acceptable at deep zoom.

---

# 3. Authoritative truth vs derived map presentation

World topography is truth.

Map imagery is derived presentation.

Conceptually:

```text
Canonical height/depth truth
↓
Map rendering / tiling / LOD
↓
Contours / shading / color ramp / coastlines
```

Changing map presentation must not regenerate world geography.

---

# 4. Multi-resolution rendering

Do not rely on one fixed-resolution raster map that is simply magnified indefinitely.

Audit and implement an appropriate multi-resolution approach.

Possible implementation families include:

- tiled raster LOD,
- mip/LOD pyramid,
- vector/mesh-derived coastlines and contours,
- zoom-regenerated derived textures,
- hybrid approaches.

Do not prescribe one before auditing live code.

Required behavior matters more than technique.

---

# 5. Zoom-level content hierarchy

Suggested behavior:

### Whole-world view
- major coastline shape,
- large elevation/depth bands,
- only major contour intervals,
- major underwater features,
- low clutter.

### Medium zoom
- additional terrain structure,
- intermediate contours,
- clearer shelves/ridges/valleys,
- better island relief.

### Close zoom
- detailed coastlines,
- local mountain/ridge/valley structure,
- local bathymetry,
- more contour detail,
- no pixel-block enlargement.

### Very close zoom
- detail may increase only while remaining screen-space legible,
- contour density must be capped.

---

# 6. Screen-space contour rule

Hard rule:

> Never draw contour lines more densely than the screen can visually resolve.

Contour presentation should adapt to zoom.

This may require:

- interval changes,
- simplification changes,
- line suppression,
- major/minor contour rules,
- screen-space spacing thresholds.

---

# 7. Major / minor contours

Support a hierarchy such as:

```text
Major contour
Intermediate contour
Minor contour
```

At far zoom:
- major only.

At medium zoom:
- major + intermediate.

At close zoom:
- minor contours appear where useful.

Do not render all contour intervals at all zoom levels.

---

# 8. Contours are derived, not baked world truth

Changing:

- contour interval,
- major/minor cadence,
- line thickness,
- simplification,
- zoom thresholds,

must not regenerate geography.

Contours should be a derived cache/presentation product.

---

# 9. Contour simplification

Contour geometry should be simplified appropriately per zoom level.

Too much simplification:
- destroys geography.

Too little:
- creates spaghetti/noise.

Use scale-aware simplification.

Preserve:
- large ridges,
- major valleys,
- shelf edges,
- major depth changes.

Suppress:
- sub-pixel wiggle,
- tiny loops,
- meaningless high-frequency contour chatter.

---

# 10. Close-zoom island interiors

At deep zoom, island interiors should remain legible.

A player should be able to identify:

- mountainous island,
- ridge-dominated island,
- broad plateau,
- central volcanic peak,
- low flat/cay island.

The map should communicate topography, not merely land color.

---

# 11. Close-zoom underwater fidelity

At deep zoom, water should retain meaningful structure:

- shallow shelf,
- shelf break,
- descending slope,
- basin,
- trench edge,
- seamount,
- valley/canyon.

A 5–6 world-unit channel should read as a substantial channel.

---

# 12. Geographic scale cues

Use cartographic cues that reinforce distance without altering coordinates.

Examples:

- broad open water areas,
- clear shelf widths,
- contour spacing that implies slope scale,
- island internal relief,
- visible bathymetric transitions,
- fewer arbitrary tiny features at whole-world zoom.

The map should visually support the same travel scale the simulation already uses.

---

# 13. Color / classification

Retain clear land / shallow / deep differentiation, but refine color ramps if useful.

Potentially support:

- land-elevation ramp,
- shallow-water ramp,
- deep-water ramp,
- subtle depth shading.

Do not let color banding obscure contour readability.

---

# 14. Coastline fidelity

Close zoom must avoid visibly blocky coast edges.

Options may include:

- higher-resolution derived coastline,
- vector/simplified coastline path,
- tiled higher-resolution map sections,
- shader interpolation where appropriate.

Do not simply blur a low-resolution coastline until it stops looking blocky.

---

# 15. Feature density by region

Cartographic detail should reflect actual geography.

Examples:

### Abyssal plain
- sparse contours,
- large calm shapes.

### Volcanic region
- dense relief where justified.

### Trench margin
- dramatic concentrated contours.

### Shelf sea
- complex shallow structure.

Do not normalize contour/detail density across the whole world.

---

# 16. File-size objective

Smaller files are desirable where possible.

Audit current size contributors first.

Likely principles:

- canonical truth stored once,
- derived map imagery regenerated/cached rather than redundantly persisted,
- avoid storing multiple full-resolution duplicate textures if derivable,
- tile/compress derived assets where useful,
- use existing packed/quantized height data where it remains appropriate.

Do not sacrifice accepted-world determinism merely to save disk space.

---

# 17. Heightfield / map-data audit

Before changing representation, report:

- authoritative height resolution,
- packed format,
- baked asset size,
- runtime cache size,
- contour texture size,
- classification texture size,
- duplicated derived artifacts,
- what is regenerated vs serialized.

This audit drives optimization.

---

# 18. Map LOD cache

Consider a derived cache that can hold:

- map tiles,
- contour geometry,
- simplification levels,
- classification layers.

Cache must be disposable/rebuildable where possible.

World truth must not depend on it.

---

# 19. Debug cartography views

Add development overlays/toggles such as:

- raw height,
- classified land/shallow/deep,
- contour LOD level,
- major/minor contours,
- coastline source geometry,
- tile boundaries,
- map texel resolution,
- current zoom level,
- active simplification threshold.

This is explicitly desirable.

---

# 20. Preserve knowledge/fog architecture

This pass changes map rendering, not what the player knows.

Do not accidentally:

- reveal hidden terrain,
- bypass fog/knowledge masks,
- reveal POIs,
- reveal nodes,
- alter star-map truth.

Rendering fidelity and knowledge remain separate.

---

# 21. Interaction with discovery

Unknown areas should remain unknown regardless of how detailed the renderer becomes.

Known coverage may render at higher fidelity as the player zooms.

Do not leak hidden world truth through LOD edges or cached tiles.

---

# 22. Performance

Close zoom may use more detailed assets, but rendering should remain bounded.

Prefer:

- visible-tile generation,
- cached derived products,
- zoom-triggered LOD swaps,
- asynchronous/prewarmed derivation where safe.

Avoid:

- rebuilding the entire world map every camera tick,
- regenerating all contours on tiny zoom changes,
- excessive allocations.

---

# 23. Implementation checkpoints

## MAPLOD.1 — Audit current rendering
- exact map texture path,
- contour path,
- classification overlays,
- topography data resolution,
- zoom mechanics,
- file-size contributors.

**STOP FOR REVIEW.**

## MAPLOD.2 — Multi-resolution map foundation
- separate truth from presentation,
- introduce zoom-aware derived representation,
- eliminate simple giant-pixel enlargement.

**STOP FOR PLAYTEST.**

## MAPLOD.3 — Coastline / terrain close-zoom fidelity
- smoother coastline behavior,
- readable island interior relief,
- shelf/depth fidelity.

**STOP FOR PLAYTEST.**

## MAPLOD.4 — Contour LOD
- major/minor hierarchy,
- zoom thresholds,
- screen-space spacing cap,
- simplification.

**STOP FOR PLAYTEST.**

## MAPLOD.5 — Underwater/cartographic refinement
- regional density behavior,
- depth shading,
- feature readability,
- scale cues.

**STOP FOR PLAYTEST.**

## MAPLOD.6 — Cache / size / performance
- derived cache strategy,
- file-size reduction where safe,
- render timing,
- regression with knowledge masks.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 24. Acceptance tests

1. Zooming in reveals meaningful additional geography.
2. Close zoom does not become visibly blocky.
3. Islands retain readable internal terrain.
4. Water gaps visually read at believable scale.
5. Shelf/slope/trench/seamount structure remains legible.
6. Whole-world view is not overwhelmed by contour noise.
7. Close zoom supports more contours without unreadable density.
8. Major/minor contour hierarchy works.
9. Screen-space contour density is capped.
10. Contour settings can change without world regeneration.
11. Map rendering remains stylized rather than sterile GIS output.
12. Knowledge/fog masks still hide unknown terrain.
13. No hidden POI/node leakage occurs.
14. Derived map assets are rebuildable from canonical truth.
15. File size is reduced where redundant derived data existed.
16. Performance remains practical across zoom changes.
17. World/celestial coordinate mapping remains unchanged.

---

# 25. Non-goals

Do not turn this pass into:

- world geography generation,
- New Game UI,
- player route planning,
- cartography workbench feature redesign,
- fog/knowledge-system redesign,
- POI generation,
- celestial-map redesign.

---

# 26. Final principle

The map must stop behaving like:

```text
low-resolution image
× bigger zoom
= bigger pixels
```

and start behaving like:

```text
same authoritative world
+ deeper cartographic LOD
= more geography revealed
```

The closer the player looks, the larger the world should feel.

---

**End of World Map Cartography / LOD / Contours handoff.**
