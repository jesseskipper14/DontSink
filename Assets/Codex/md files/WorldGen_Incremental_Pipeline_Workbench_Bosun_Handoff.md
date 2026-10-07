# DON'T SINK — Incremental World Generation Pipeline & Developer Workbench
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Generation-stage architecture, dirty dependency tracking, fast previews, developer tuning, profiling, and debug overlays  
**Goal:** Turn world-generation iteration from a ~30-second full rebuild ritual into a staged, inspectable, selectively regenerating development workflow.

---

# 0. Exact-current-class rule

Before modifying generation controls or generator orchestration:

1. Inspect the exact current generation entry points.
2. Preserve current raw tuning controls wherever practical.
3. Preserve existing deterministic seeds/settings.
4. Do not rewrite working generation stages blindly.
5. Add stage boundaries incrementally where current architecture allows.
6. Stop after every checkpoint and report timing improvements/regressions.

---

# 1. Core problem

Current raw world-generation tuning already exists and is useful.

The major pain point is iteration speed:

> One regeneration can take around 30 seconds.

For active generator tuning, this is too slow.

The developer needs a worldgen laboratory, not a 30-second loading screen after every parameter nudge.

---

# 2. Primary goal

Target workflow:

```text
change parameter
→ identify affected stage(s)
→ invalidate only those stages + downstream dependents
→ regenerate only necessary outputs
→ preview
```

Not:

```text
change contour interval
→ regenerate entire planet
```

---

# 3. Explicit generation stages

The generator should expose a stage model.

Conceptual example:

```text
MacroTopology
RegionalGeology
ArchipelagoLayout
IslandShape
Bathymetry
MesoDetail
MicroDetail
Classification
Biome
NodePlacement
HarborGeneration
POIGeneration
Presentation
```

Exact stage boundaries must follow live code.

Do not force artificial abstraction where the current implementation truly cannot separate work.

---

# 4. Stage inputs and outputs

Each stage should have identifiable:

- input settings,
- seed/subseed,
- upstream dependencies,
- output artifact/state,
- version/hash if useful,
- timing.

This enables dependency-aware regeneration.

---

# 5. Dirty-stage invalidation

Changing a setting should mark the earliest affected stage dirty, plus all dependents.

Examples:

### Contour presentation change
```text
Presentation only
```

### Island-shape parameter
```text
IslandShape
→ Bathymetry / detail affected as needed
→ Classification
→ Biome
→ Nodes
→ Harbors
→ POIs
→ Presentation
```

### Node-count setting
```text
NodePlacement
→ Harbor/POI/node-dependent outputs
```

Macro terrain should remain untouched.

---

# 6. Stable per-stage seeds

Use deterministic stage/subseeds so upstream-preserved stages remain identical when downstream settings change.

Conceptually:

```text
WorldSeed
→ MacroSeed
→ RegionSeed
→ IslandSeed
→ DetailSeed
→ NodeSeed
...
```

Do not let changing one downstream parameter unexpectedly reroll unrelated upstream geography.

---

# 7. Draft Preview

Add a fast **Draft Preview** mode.

Purpose:

> Show geographic decisions quickly enough for active tuning.

Desired aspiration:

- roughly a few seconds rather than ~30 seconds,
- 3 seconds would be an excellent target when practical.

Draft Preview may simplify or skip expensive downstream work.

---

# 8. Draft Preview must preserve geography decisions

Draft may reduce fidelity, but it must not invent a different world.

The same seed/settings should preserve:

- macro basins,
- major trenches/ridges,
- archipelago layout,
- island positions,
- island archetype decisions,
- major coastline intent.

Full Preview may add detail.

It should not move the islands somewhere else.

---

# 9. Draft may skip expensive derived systems

Draft may omit or simplify:

- high-resolution micro detail,
- final contours,
- expensive POI validation,
- resource placement,
- final caches,
- save compression,
- some node/harbor/POI layers unless toggled.

The simpler the better.

---

# 10. Full Preview

Full Preview runs final-resolution generation without yet committing to a save.

Use it when:

- geography looks promising,
- close zoom needs inspection,
- final contours are needed,
- downstream validation matters.

This is the pre-acceptance verification mode.

---

# 11. Accept is separate from Preview

Do not automatically create a save every time a world is previewed.

Workflow:

```text
tune
→ Draft
→ Draft
→ Draft
→ Full Preview
→ Accept later in New Game pipeline
```

---

# 12. Development workbench

Evolve the existing raw generation controls into a stronger WorldGen Workbench.

Suggested tabs:

```text
Macro
Regions
Islands
Seafloor
Detail
Biomes
Nodes
Harbors
POIs
Presentation
```

Use live architecture/terminology where better.

---

# 13. Existing controls should be reused

The project already supports raw generation parameters.

Do not throw them away merely to build a new UI.

Instead:

- reorganize,
- classify by stage,
- show which stage they dirty,
- expose useful defaults/presets,
- add profiling/debugging.

---

# 14. Unsafe parameters remain developer-only

Developer workbench can expose raw internals such as:

- feature curve strengths,
- noise frequencies,
- octave weights,
- repair thresholds,
- shelf dilation,
- trench dimensions,
- archetype weights.

These must remain separate from eventual safe New Game controls.

---

# 15. Pipeline/dirty-state visibility

In development mode, show dirty/invalidation information.

Example:

```text
DIRTY:
IslandShape
Bathymetry
Classification
Biome
Nodes
Harbors
Presentation
```

This is useful and explicitly desired.

---

# 16. Stage timing

Show timing per stage.

Example:

```text
MacroTopology        0.18s
RegionalGeology      0.23s
IslandShape          0.72s
Bathymetry           0.61s
Biome                 0.14s
Nodes                 0.31s
Presentation          0.42s

Total Draft           2.61s
```

Full generation timing should also be visible.

This identifies optimization targets.

---

# 17. Cache reuse visibility

Development UI should make it obvious whether a stage was:

- regenerated,
- reused,
- skipped,
- loaded from cache.

Do not make incremental generation impossible to reason about.

---

# 18. Debug overlays

Workbench should expose map overlays for:

- height,
- land/shallow/deep,
- contours,
- macro basins,
- trenches,
- ridges,
- plateaus,
- region IDs,
- archipelago IDs,
- island archetypes,
- biome,
- cluster affinity,
- node candidates,
- placed nodes,
- harbor candidates,
- harbors,
- POIs later.

This is a development/debug feature.

---

# 19. Overlay independence

Changing overlays should not dirty world generation.

Debugging must be cheap.

---

# 20. Regeneration scope controls

Developer mode may support explicit actions such as:

```text
Regenerate Dirty
Regenerate Current Stage
Regenerate From Here
Full Regenerate
```

These are development tools.

Normal players should never see them.

---

# 21. Seed controls

Workbench should support:

- current world seed,
- reroll seed,
- fixed seed,
- copy/paste seed,
- stable stage/subseed diagnostics if useful.

---

# 22. Presets

Developer should be able to save/load named generation presets.

Examples:

```text
Current Standard
Dense Archipelago Test
Huge Trench Test
Sparse Ocean Test
Atoll Stress Test
```

Preset stores parameters, not generated world truth.

---

# 23. No side-by-side compare required

A dual-preview comparison view is explicitly **not required**.

Keep the workbench focused.

---

# 24. Generation errors and validation

Show validation warnings such as:

- target water percentage not achieved,
- impossible island packing,
- node crowding repair failures,
- harbor candidate failures,
- invalid topology seams,
- degenerate region,
- stage timing anomalies.

Do not silently repair everything without debug visibility.

---

# 25. Cancellation

If practical, long Full Preview / Full Regenerate operations should be cancellable in development mode.

Do not corrupt retained upstream cache when cancelled.

---

# 26. Progress

Show:

- current stage,
- stage progress if available,
- elapsed time,
- total time.

A 30-second operation is less offensive when it at least admits what it is doing.

---

# 27. Cache / artifact lifetime

Upstream stage outputs may be retained between tweaks.

Derived preview caches should be disposable.

Do not allow stale derived artifacts to masquerade as current generation.

Use dependency/version validation.

---

# 28. Generator-version awareness

Workbench should display current generator version.

Saved presets may include version metadata.

A preset from an older generator may be:

- migrated if supported,
- loaded with warning,
- rejected if incompatible.

---

# 29. Implementation checkpoints

## WPIPE.1 — Audit and profile
- locate all generation stages,
- time them,
- identify biggest costs,
- map parameter → affected systems.

**STOP FOR REVIEW.**

## WPIPE.2 — Stage boundaries
- explicit stage orchestration,
- deterministic stage seeds,
- identifiable outputs.

**STOP FOR PLAYTEST.**

## WPIPE.3 — Dirty dependency graph
- parameter invalidation,
- dependent-stage invalidation,
- stage reuse.

**STOP FOR PLAYTEST.**

## WPIPE.4 — Draft Preview
- fast simplified path,
- preserve macro geography decisions,
- timing display.

**STOP FOR PLAYTEST.**

## WPIPE.5 — Workbench organization
- stage tabs,
- raw parameter reuse,
- dirty-state UI,
- regenerate-scope controls.

**STOP FOR PLAYTEST.**

## WPIPE.6 — Debug overlays
- regions,
- features,
- archetypes,
- nodes/harbors,
- classification.

**STOP FOR PLAYTEST.**

## WPIPE.7 — Presets / profiling / cache hardening
- named presets,
- stage timing,
- cache validation,
- cancellation/progress if practical.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 30. Acceptance tests

1. Current raw tuning settings remain accessible in developer mode.
2. Parameters are organized by generation stage.
3. Changing a downstream setting does not always rebuild upstream geography.
4. Dirty-stage propagation is visible.
5. Per-stage deterministic seeds prevent unrelated rerolls.
6. Draft Preview preserves macro geography.
7. Draft Preview is dramatically faster than current full regeneration.
8. A few-second Draft target is pursued where practical.
9. Full Preview remains available.
10. Debug overlays do not trigger regeneration.
11. Stage timings are visible.
12. Cache reuse/regeneration state is understandable.
13. Named parameter presets can be saved/loaded.
14. Full regenerate remains available for validation.
15. Existing generator determinism remains intact.

---

# 31. Non-goals

Do not turn this pass into:

- player-facing New Game UI,
- geography redesign itself,
- map contour LOD redesign,
- save creation/acceptance flow,
- gameplay map discovery.

Those have separate handoffs.

---

# 32. Final principle

Worldgen tuning should become:

```text
change
→ quick preview
→ inspect
→ change
→ quick preview
```

not:

```text
change
→ wait 30 seconds
→ forget what you changed
→ age visibly
```

The workbench exists to make world-generation iteration cheap enough that the generator can actually be refined properly.

---

**End of Incremental World Generation Pipeline & Developer Workbench handoff.**
