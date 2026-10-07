# DON'T SINK — New Game World Builder, Preview & Acceptance
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Player-facing New Game world setup, preview, safe parameters, deterministic acceptance, and world freeze/bake  
**Goal:** Clicking New Game should open a world-building flow where the player chooses broad safe parameters, previews the actual world they are about to inhabit, rerolls/tunes safely, and then permanently accepts that exact world for the new save.

---

# 0. Exact-current-class rule

Before modifying New Game, save creation, generator settings, preview UI, or world-bake code:

1. Inspect exact current menu/save/world-generation classes.
2. Preserve existing save-slot behavior.
3. Preserve generator determinism.
4. Preserve world/celestial coordinate assumptions.
5. Reuse current generation settings assets where possible.
6. Do not reconstruct current classes from memory.
7. Stop after every checkpoint for playtest.

---

# 1. Core New Game flow

Target flow:

```text
NEW GAME
↓
World Settings
↓
Generate Draft Preview
↓
Reroll / adjust safe settings
↓
Optional Full Preview
↓
Accept World
↓
Freeze / bake canonical world
↓
Create save
↓
Begin game
```

The previewed world and accepted world must be the same world.

---

# 2. Safe player-facing philosophy

Normal players should control broad strokes only.

They should not set:

- trench curve strength,
- octave amplitudes,
- topology repair thresholds,
- shelf-noise warp,
- cluster-repair iteration counts.

Safe parameters should be intuitive and resistant to garbage output.

---

# 3. Initial safe controls

Initial candidate set:

```text
Seed
World Preset
Island Amount
Island Clumping
Landmass Style
Ocean Depth
Danger / Harshness
Biome Variety
```

Exact names can evolve.

Do not expose a setting merely because it exists internally.

Only expose controls validated as safe.

---

# 4. Seed

Support:

- random seed,
- manual seed entry,
- reroll,
- copy/share seed.

Seed alone does not override explicit player settings.

Canonical world identity includes:

```text
Seed
+ GeneratorVersion
+ AcceptedGenerationParameters
```

---

# 5. World Presets

Presets provide sane bundles.

Examples:

```text
Standard
Sparse Ocean
Dense Archipelagos
Long Voyages
Harsh Depths
Varied World
```

Names are illustrative.

A preset sets safe parameters.

Player may then tweak those safe parameters individually.

---

# 6. Island Amount

Controls how many surface landforms/islands are targeted.

This is distinct from total land percentage.

Existing target-water-percentage logic may remain an underlying constraint.

At fixed land percentage:

- more islands → smaller average islands,
- fewer islands → larger average islands.

---

# 7. Island Clumping

Controls spatial distribution.

Examples:

```text
Loose
Balanced
Clustered
Dense Clusters
```

This should meaningfully affect voyage geography.

It should not simply scale total land.

---

# 8. Landmass Style

High-level control for broad composition.

Possible safe options/presets may bias:

- archipelagos,
- large islands,
- mixed,
- volcanic chains,
- atoll-heavy,
- drowned-plateau worlds.

Do not guarantee a monoculture unless explicitly designed.

---

# 9. Ocean Depth

Broad player control only.

Examples:

```text
Shallower
Standard
Deep
Very Deep
```

This may affect:

- depth distribution,
- prevalence/extent of deep water,
- difficulty of routine diving,
- macro seafloor profile.

Do not expose raw curve internals.

---

# 10. Danger / Harshness

This should remain a broad world-generation/gameplay control.

It may eventually influence:

- dangerous-region weighting,
- extreme trenches,
- harsh resource gaps,
- dangerous events/POIs,
- hostile-biome weighting.

Exact semantics may evolve.

Keep the safe setting abstract.

---

# 11. Biome Variety

Optional safe control.

Potential modes:

```text
Focused
Standard
Varied
Highly Varied
```

Do not let this undermine coherent regional identity.

---

# 12. Draft Preview

New Game should use fast Draft Preview by default.

Goal:

> Player can reroll/tune without waiting ~30 seconds every time.

A few-second preview is strongly preferred.

Draft may omit expensive downstream systems.

---

# 13. Draft fidelity rule

Draft must preserve actual world-defining decisions.

The draft should faithfully preview:

- land distribution,
- macro ocean geography,
- archipelago placement,
- island positions,
- major island archetype,
- major trenches/ridges/basins,
- broad depth.

Full Preview may add detail.

It must not secretly choose a different world.

---

# 14. Full Preview

Provide a Full Preview action for final inspection.

Full Preview may include:

- high-resolution geography,
- final contour/map LOD products,
- node placement,
- harbors,
- biomes,
- other expensive derived systems.

This is optional before acceptance but recommended in development.

Normal player UX may simplify this later.

---

# 15. Developer Advanced mode

Development builds/debug mode may expose an Advanced section.

This reuses the WorldGen Workbench controls.

Normal players should not see unsafe raw tuning.

---

# 16. Preview map

Preview should support:

- pan,
- zoom,
- high-level world inspection,
- relevant debug overlays in developer mode.

In player mode, keep the preview clean.

---

# 17. Development overlays

Developer-only preview may toggle:

- height,
- land/shallow/deep,
- contours,
- biomes,
- macro geography,
- regions,
- island archetypes,
- nodes,
- harbor candidates,
- harbors,
- POIs later.

---

# 18. Deterministic Preview → Accept

Hard rule:

> Accept World must not reroll geographic decisions.

Workflow:

```text
Seed + settings
→ deterministic draft/full generation
→ preview
→ Accept
→ same canonical world
```

If acceptance needs a final full bake, it must use the same stage seeds and accepted settings.

---

# 19. Acceptance freeze

Once the player accepts a world:

- generator version is stored,
- accepted parameters are stored,
- canonical world truth is frozen/baked as required,
- later generator updates do not mutate this save's world.

This is non-negotiable.

---

# 20. Existing-save stability

A save created under GeneratorVersion N should continue using its accepted world even after the project ships GeneratorVersion N+1.

Do not regenerate existing terrain just because code changed.

---

# 21. What should be persisted

Persist enough canonical state to guarantee accepted-world stability.

Candidate persistent pieces may include:

- generator version,
- seed,
- accepted parameters,
- authoritative packed height/topography,
- stable node/POI manifests,
- other generator outputs that cannot safely be reconstructed after version changes.

Exact storage strategy depends on live audit.

---

# 22. Derived presentation should remain rebuildable

Prefer not to permanently store redundant derived assets if they can be recreated safely from canonical truth.

Examples:

- contour caches,
- map LOD textures,
- debug overlays.

This may help reduce file size.

Do not make the save depend on transient presentation caches.

---

# 23. File-size goal

World saves/assets should be smaller where practical.

Audit actual contributors before optimization.

Potential strategies:

- keep one canonical compact height representation,
- compress coherent data,
- avoid duplicate map textures,
- regenerate derived contour/presentation caches,
- store manifests rather than decorative instances.

---

# 24. Progress and cancel

For Full Preview / Accept bake:

- show current stage,
- show progress,
- allow cancel before save creation if practical.

Do not create half-valid saves.

---

# 25. Safe-setting validation

Every normal-player safe setting combination should be validated against:

- impossible land packing,
- degenerate all-water/all-land outputs,
- unusable node distribution,
- pathological worldgen failures.

If some combinations remain unsafe, constrain them rather than exposing raw failure.

---

# 26. New Game UX

Illustrative structure:

```text
NEW WORLD

Seed: [__________] [Reroll]

Preset: Standard

Island Amount      Standard
Island Clumping    Balanced
Landmass Style     Mixed
Ocean Depth        Deep
Danger             Standard
Biome Variety      Standard

[ Generate Preview ]

        MAP PREVIEW

[ Advanced ]   [ Full Preview ]   [ Accept World ]
```

Final visual styling can come later.

---

# 27. Do not conflate preview with discovered map

This is pre-game world-selection UI.

It may show the generated world for selection purposes.

Once gameplay begins, normal player map knowledge/fog rules apply.

Do not carry preview omniscience into gameplay knowledge.

---

# 28. Save creation boundary

Only after Accept World:

- create/initialize final world save,
- initialize player/boat/world starting state,
- establish starting node,
- begin normal gameplay knowledge rules.

Preview state itself should not contaminate gameplay state.

---

# 29. Re-roll semantics

Reroll should:

- change seed,
- preserve current safe settings,
- regenerate draft.

Changing safe settings with fixed seed should deterministically alter the world according to those settings.

---

# 30. Preset semantics

Selecting preset:

- applies safe parameter bundle,
- clearly updates visible controls,
- does not secretly lock them unless designed.

---

# 31. Implementation checkpoints

## NGBUILD.1 — Audit current New Game/save flow
- locate menu entry,
- save creation,
- generator invocation,
- current settings ownership,
- accepted world persistence.

**STOP FOR REVIEW.**

## NGBUILD.2 — New World settings shell
- safe parameter model,
- seed/reroll,
- preset infrastructure,
- player/developer mode split.

**STOP FOR PLAYTEST.**

## NGBUILD.3 — Draft Preview integration
- use fast Draft pipeline,
- map preview,
- pan/zoom,
- reroll/settings loop.

**STOP FOR PLAYTEST.**

## NGBUILD.4 — Full Preview
- final-resolution verification,
- progress UI,
- no reroll divergence.

**STOP FOR PLAYTEST.**

## NGBUILD.5 — Accept / freeze
- persist generator version,
- accepted params,
- canonical truth,
- create save only after acceptance.

**STOP FOR PLAYTEST.**

## NGBUILD.6 — Existing-save / migration hardening
- generator-version regression,
- file-size audit,
- rebuildable derived caches,
- cancel/failure handling.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 32. Acceptance tests

1. New Game opens world-settings flow.
2. Player-facing controls remain broad and safe.
3. Unsafe raw parameters are developer-only.
4. Seed can be rerolled/copied/entered.
5. Island Amount and Island Clumping are separate.
6. Draft Preview is much faster than current full regeneration.
7. Draft geography matches eventual Full Preview decisions.
8. Full Preview never silently rerolls the world.
9. Accept World preserves the exact accepted world.
10. Save creation occurs after acceptance.
11. Generator version is stored.
12. Existing saves do not mutate after generator upgrades.
13. Derived map/contour caches can be rebuilt where appropriate.
14. Preview omniscience does not leak into gameplay map knowledge.
15. Safe parameter combinations avoid pathological worlds.
16. Developer Advanced mode can expose raw controls/debug overlays.
17. File size is reduced where redundant derived data existed.

---

# 33. Non-goals

Do not turn this pass into:

- geography algorithm redesign,
- contour LOD implementation,
- quest setup,
- character creation,
- multiplayer lobby,
- tutorial flow,
- map discovery redesign.

Those are separate concerns.

---

# 34. Final principle

New Game should feel like:

```text
choose broad world character
→ quickly preview
→ reroll/tweak
→ inspect
→ commit
```

not:

```text
blindly click New Game
→ wait
→ hope
```

And once the player accepts a world:

> That exact world belongs to that save permanently.

---

**End of New Game World Builder / Preview / Acceptance handoff.**
