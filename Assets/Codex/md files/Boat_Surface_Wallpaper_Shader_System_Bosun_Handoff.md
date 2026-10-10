# DON'T SINK — Boat Surface / Wallpaper Shader System
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Dynamic stylized boat-surface rendering for compartment far walls, exterior hull pieces, end caps, and other resizable structural surfaces  
**Status:** Ready for implementation  
**Out of scope:** Structural material physics, actual damage logic, projectile systems, breach creation, persistent grime history, player-facing color-wheel UI

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Inspect the current rendering path for:
   - compartment background/far-wall visuals,
   - exterior hull rendering,
   - end-cap pieces,
   - SegmentResizer / resized geometry,
   - sorting layers/orders,
   - current material/shader usage,
   - Boat Builder property editing,
   - existing flood/water-height presentation hooks if any.
5. Reuse existing geometry. Do not invent a parallel shape system just for visuals.
6. Keep the implementation compatible with the Structural Materials pass:
   - structural material = physics/data,
   - wallpaper/surface style = visual presentation.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core philosophy

These boat surfaces are **dynamic shapes with wallpaper-like visual treatment**, not rigid sprites.

The geometry answers:

> Where does this surface exist and what shape is it?

The surface system answers:

> What does that surface look like?

Target surfaces include:

- interior compartment far walls,
- exterior hull faces,
- arbitrary end caps,
- walls/decks/other resizable structural faces where the same system fits cleanly.

The final architecture should feel like:

```text
Dynamic piece geometry
        ↓
Surface mask / mesh
        ↓
Boat-local wallpaper mapping
        ↓
Style + tint + optional pattern detail
        ↓
future wetness / damage overlays
```

---

# 2. Structural material and visual style are independent

Do **not** restrict wallpaper choice by structural material.

The player may intentionally choose combinations such as:

```text
Steel structure
+ wood-plank wallpaper
```

or:

```text
Wood structure
+ shiny industrial metal wallpaper
```

That is allowed.

No compatibility filtering.

No automatic rejection.

No interior/exterior restriction unless a future style explicitly requires one for a technical reason.

This is a deliberate player-freedom decision.

---

# 3. SurfaceStyle definition

Create a data-driven surface-style definition following project conventions.

Conceptually:

```text
BoatSurfaceStyleDefinition

Identity
- StableId
- DisplayName

Visual inputs
- BaseTexture / pattern texture
- OptionalDetailTexture
- WorldScale / RepeatScale
- Tintable = true
- DefaultTint
- Optional pattern parameters

Future-ready inputs
- supports damage overlay
- supports wetness
- supports waterline/flood-height response
```

Exact names should match the live codebase.

Adding a new wallpaper later should mostly be an asset-authoring task, not a new code path.

---

# 4. Per-piece style selection

Surface style is selected **per installed piece**.

Examples:

```text
Wall A
Style: Riveted Plate
Tint: Navy

Wall B
Style: Framed Panel
Tint: Cream
```

Do not implement one global “boat skin.”

The architecture should allow future Boat Builder conveniences such as:

- multi-select,
- apply style to selected pieces,
- apply tint broadly,

but those are not required unless the current editor makes them trivial.

---

# 5. Per-piece tint

Every starting wallpaper is tintable.

This includes:

- metal,
- wood,
- paneling,
- smooth surfaces.

The player should eventually be able to use a full color-wheel/editor.

For this pass, implement the underlying per-piece tint property and a minimal developer/editor selection method if needed for testing.

The color-wheel UI itself can come later.

A piece stores something equivalent to:

```text
SurfaceStyleId
SurfaceTint
```

Tint persists with the installed piece.

---

# 6. Color channels

V1 needs **one primary tint channel**.

Design the style definition so a future second masked/secondary channel can be added without rewriting the entire system.

Do not implement secondary trim color now unless the live shader architecture makes it nearly free.

---

# 7. Fixed wallpaper scale

Each wallpaper has an authored fixed repeat scale.

The player does **not** adjust texture scale in V1.

Resizing a piece reveals more of the same pattern.

Correct:

```text
small wall:
|==|==|

larger wall:
|==|==|==|==|
```

Wrong:

```text
small wall:
|==|==|

larger wall:
|========stretched========|
```

Texture/pattern scale must remain visually stable.

---

# 8. Boat-local anchoring

Initial UV/mapping policy is **boat-local anchoring**.

The wallpaper must remain attached to the boat while the boat:

- translates,
- rotates,
- bobs,
- sinks,
- is moved between scenes.

Do not use world-space mapping that causes the pattern to swim across the hull.

Conceptually:

```text
Boat-local coordinates
→ repeated surface sampling
```

This also allows neighboring surfaces to visually align more naturally.

---

# 9. Piece-local orientation

The wallpaper belongs to the surface and rotates with the piece.

Example:

- an angled end cap rotates,
- its wallpaper orientation rotates with it.

Do not keep the pattern locked to global/world horizontal.

For V1 there is no user-exposed manual wallpaper rotation control.

If the shader needs an internal orientation basis, derive it from the piece transform/geometry.

---

# 10. No manual offset / no manual rotation in V1

Do not add:

- per-piece UV offset controls,
- per-piece wallpaper rotation sliders,
- arbitrary player pattern scale controls.

Boat-local anchoring plus piece-local orientation is the starting behavior.

These can be added later only if real player needs emerge.

---

# 11. Mixture of organic texture and construction pattern

Surface styles may combine:

### Organic/base texture
Examples:

- painted metal mottling,
- subtle scratches,
- wood grain,
- stained surface variation.

### Repeating construction pattern
Examples:

- rivet rows,
- plate seams,
- industrial panel divisions,
- plank divisions,
- framed panel cadence.

This can be implemented through:

- authored tile textures,
- secondary detail textures,
- procedural shader patterning,
- or a hybrid.

Do not over-engineer the first version.

---

# 12. Macro visual details remain real objects

Do not bake major structural/interactive details into wallpaper.

These remain sprites/objects/geometry layered above the surface:

- windows,
- doors,
- hatches,
- ladders,
- beams,
- frames,
- furniture,
- structural supports,
- other interactive pieces.

Wallpaper should provide surface character, not replace actual boat structure.

---

# 13. Compartment far wall

The compartment background/far wall should become a dynamically filled surface.

Conceptually:

```text
Compartment geometry
+ selected SurfaceStyle
+ per-piece tint
```

No separate rigid background sprite should be required for every possible compartment dimension.

The compartment wall may be resized or shaped dynamically and the wallpaper simply fills the resulting visible surface.

---

# 14. Exterior hull

Exterior hull pieces use the same visual architecture.

Requirements:

- arbitrary resized hull shape supported,
- pattern does not stretch,
- boat-local anchoring,
- piece-local orientation,
- per-piece style,
- per-piece tint,
- future damage/wetness hooks.

The exterior visual silhouette still comes from real geometry.

The shader does not invent hull shape.

---

# 15. End caps

Arbitrary end-cap shapes are a major reason for this system.

Any supported polygon/mesh/end-cap geometry should be able to render with the same wallpaper pipeline.

Do not require custom sprite artwork for every possible end-cap silhouette.

---

# 16. Edge treatment

Preserve/extend the project's existing clean edge style rather than baking borders into wallpaper textures.

The wallpaper should meet the geometry edge cleanly.

If current visuals already use a border/outline treatment, keep it consistent.

Do not introduce a heavy generic procedural bevel unless it matches current art direction.

Important structural borders/frames remain real art/geometry where appropriate.

---

# 17. Stylized, not photorealistic

Target look:

- stylized,
- clean,
- game-art coherent,
- visually readable,
- not full-on pixel art,
- not realistic PBR material simulation.

Avoid:

- photorealistic wood scans,
- realistic metallic reflection stacks,
- heavy normal-map realism,
- visually noisy procedural detail.

The result should feel authored and graphic rather than physically rendered.

---

# 18. Starting wallpaper set

Implement approximately six initial styles to exercise the system:

1. Smooth / lightly mottled surface
2. Large riveted plate
3. Small industrial panel
4. Horizontal plank
5. Vertical plank
6. Framed panel

All six:

- fixed-scale,
- tintable,
- unrestricted by structural material,
- selectable per piece.

Exact art can be placeholder-quality enough to prove the renderer, but the styles should be visually distinct.

---

# 19. Damage overlay hook

The shader/surface architecture must leave a clean local overlay seam for future localized damage such as:

- dents,
- bullet holes,
- scratches,
- torn metal,
- patched holes,
- structural failure.

Do **not** implement actual damage logic in this pass.

Important rule:

Damage is attached to:

```text
PieceId + piece-local position
```

not to wallpaper UV coordinates.

If a player changes the wallpaper later, the damage should remain in the same physical piece-local location.

If the structural material is replaced/refit, the Structural Materials pass says the damage may reset because the piece has effectively been replaced.

---

# 20. Damage presentation must not require geometry cutting

Future bullet holes and dents should be visual/local overlays.

Do not require:

- cutting meshes,
- cutting colliders,
- literal holes in hull geometry.

The later Hull Damage system will create gameplay breach records independently from the visual mark.

The surface system only needs to be capable of showing the mark.

---

# 21. Interior live wetness hook

Prepare a shader/input seam for current compartment water height.

Conceptually:

```text
InteriorWaterHeightBoatLocal
```

The surface renderer should be able to darken or otherwise visually wet the portion of the interior wall below current flood height.

For this pass:

- implementing the live effect is desirable if the current flooding system already exposes a clean value,
- otherwise establish the seam without forcing flooding-system refactors.

Do not implement persistent historical flood stains yet.

---

# 22. Exterior waterline wetness

Prepare/support an exterior waterline response.

Conceptually:

```text
CurrentWorldWaterHeight
```

The exterior surface may render the submerged portion:

- darker,
- wetter,
- subtly different in tone.

A narrow waterline transition is acceptable.

This should react as the boat:

- rolls,
- lists,
- sinks,
- rises.

If implementation is cheap and clean, include it in V1.

Do not turn this into a full reflection/refraction/wave-rendering pass.

---

# 23. Wetness is presentation only

Wetness does not alter structural material properties in this pass.

Any future gameplay effect such as:

- rust,
- waterlogged wood,
- corrosion,
- rot,

belongs elsewhere.

This pass only handles visual response.

---

# 24. Geometry owns shape; shader owns fill

Hard architectural rule:

```text
GEOMETRY / MASK
owns:
- shape
- silhouette
- clipping
- openings
- collision

SHADER / SURFACE STYLE
owns:
- base appearance
- repeat pattern
- tint
- local variation
- future wetness
- future damage overlay
```

Do not ask shader math to recreate arbitrary structural geometry that already exists elsewhere.

---

# 25. Sorting / occlusion

Preserve current rendering semantics:

- compartment far wall remains behind interior objects,
- exterior hull remains in correct exterior sorting,
- windows/doors/hatches/props still appear above wallpaper where intended,
- player sorting behavior is unaffected.

Bosun must inspect current sorting layers/orders before replacing any SpriteRenderer/material path.

---

# 26. Persistence

Each installed surface-bearing piece should persist at least:

- SurfaceStyle stable ID
- primary tint

No need to persist:

- derived UVs,
- temporary wetness,
- world water height,
- shader cache data.

Those are regenerated.

Old saves should receive deterministic/default style + tint values.

---

# 27. Boat Builder integration

Implement the smallest practical editing path required to test:

- cycle/select SurfaceStyle,
- set a tint,
- see changes immediately.

Do not build the final polished color-wheel/editor UI here unless existing editor tooling makes it trivial.

Future UX can include:

- full color wheel,
- eyedropper,
- multi-select paint,
- paint-all,
- saved palettes.

Architecture should not block those.

---

# 28. Performance requirements

The surface system must remain cheap enough for many visible boat pieces.

Avoid:

- unique runtime material instances per piece if MaterialPropertyBlock or equivalent per-renderer property path can solve it,
- generating a large texture per piece,
- per-frame UV mesh rebuilds when geometry is static,
- per-frame CPU recoloring,
- unnecessary RenderTextures.

Prefer shared materials/styles plus per-instance properties.

Any wetness/damage data path should remain bounded and event-driven.

---

# 29. Shader/material architecture

Exact implementation is Bosun's choice after auditing current URP2D rendering.

Possible forms include:

- one shared BoatSurface shader with style assets,
- a small family of closely related shaders,
- texture-array or atlas-backed styles,
- multiple material assets sharing common shader code.

Avoid a giant fragile UberShader if separate simple variants are cleaner.

Equally, avoid six copy-pasted shaders that differ only by texture.

Choose the simplest architecture that keeps:

- fixed-scale repeat,
- boat-local anchoring,
- tint,
- optional repeating detail,
- wetness hook,
- damage hook.

---

# 30. Suggested data split

Conceptually:

```text
BoatSurfaceStyleDefinition
- style identity
- base texture(s)
- repeat scale
- default tint
- pattern/detail parameters

InstalledBoatPiece
- SurfaceStyleId
- SurfaceTint
```

StructuralMaterialDefinition remains separate.

Do not merge the two assets.

---

# 31. Multiplayer behavior

Surface style/tint are consequential shared customization state because all players should see the same boat appearance.

Authoritative state eventually includes:

- piece SurfaceStyleId
- piece SurfaceTint

Clients derive rendering locally.

Do not network:

- UV coordinates,
- wetness pixels,
- procedural rivet positions,
- shader internals.

Actual network transport is deferred, but the data model should be replication-friendly.

---

# 32. Non-goals

Do not implement:

- actual bullet damage
- breach creation
- water leak VFX
- mesh/collider destruction
- structural-material restrictions on wallpaper
- interior/exterior wallpaper restrictions
- player UV scale controls
- player UV offset controls
- player wallpaper rotation controls
- polished final color-wheel UI
- historical flood stains
- rust simulation
- corrosion gameplay
- photorealistic PBR boat materials
- final art for every future wallpaper

---

# 33. Implementation checkpoints

## SURF.1 — Audit current render path

Inspect:

- compartment far-wall/background classes
- exterior hull renderers
- end caps
- SegmentResizer geometry
- current materials/shaders
- sorting layers/orders
- Boat Builder property UI
- save/persistence representation
- flooding/water-level APIs

Document which geometry representation will feed the surface renderer.

No visual rewrite yet.

**STOP and report.**

## SURF.2 — Shared surface rendering foundation

Implement:

- shared BoatSurface rendering path,
- fixed-scale repeating texture,
- boat-local anchoring,
- piece-local orientation,
- per-piece tint,
- persistence fields/seams.

Use one basic test wallpaper first.

Verify:

- resize does not stretch,
- movement does not cause texture swimming,
- rotation moves pattern with piece.

**STOP and report.**

## SURF.3 — Apply to target surfaces

Migrate/enable:

- compartment far wall,
- exterior hull,
- end caps,
- any obvious shared structural surface.

Preserve:

- geometry,
- collision,
- sorting,
- interaction.

**STOP and report.**

## SURF.4 — Initial wallpaper library

Add six initial styles:

1. Smooth / lightly mottled
2. Large riveted plate
3. Small industrial panel
4. Horizontal plank
5. Vertical plank
6. Framed panel

All tintable.

No compatibility restrictions.

Add minimal style/tint editing support for testing.

**STOP and report.**

## SURF.5 — Wetness hooks

If current water/flood APIs permit clean integration:

- add interior live flood-height wetness,
- add exterior waterline wetness.

If they do not, add clean shader/property seams and report the blocker rather than coupling unrelated systems.

No persistent staining.

**STOP and report.**

## SURF.6 — Damage-overlay seam + persistence/regression

Add/confirm local damage overlay interface/seam without implementing actual damage.

Verify:

- style/tint save/load,
- old-save defaults,
- style changes preserve local damage coordinates conceptually,
- no unique-material explosion,
- no per-frame geometry rebuilding,
- sorting and resize regression clean.

**STOP and report.**

---

# 34. Acceptance tests

## Mapping / resizing
1. Small wall shows wallpaper at authored scale.
2. Resize wall wider; more pattern appears, pattern does not stretch.
3. Resize wall taller; more pattern appears vertically.
4. Arbitrary end cap fills correctly.
5. Angled/rotated piece carries wallpaper orientation with it.
6. Boat translation does not move pattern across the piece.
7. Boat rotation does not cause world-space swimming.

## Selection / tint
8. Two neighboring pieces can use different styles.
9. Two pieces with same style can use different tints.
10. Wood pattern can be applied to Steel structure.
11. Metal pattern can be applied to Wood structure.
12. Every starter wallpaper accepts tint.

## Surface targets
13. Compartment far wall uses dynamic wallpaper fill.
14. Exterior hull uses same core system.
15. End cap does not require bespoke shape sprite.
16. Windows/doors/hatches remain independent objects above surface where intended.

## Persistence
17. Style survives save/load.
18. Tint survives save/load.
19. Old saves receive deterministic/default style and tint.

## Wetness
20. If implemented, interior wall below flood height visibly wets/darkens.
21. If implemented, exterior submerged hull responds to waterline.
22. Wetness does not modify structural material physics.

## Performance
23. Shared style/material path does not create runaway unique material instances.
24. Resized static pieces do not rebuild UV geometry every frame.
25. Many visible surfaces remain stable in normal BoatScene play.

## Regression
26. Current collision unchanged.
27. Current sorting behavior unchanged.
28. Boat Builder resize behavior unchanged.
29. Existing interaction colliders unchanged.
30. Existing buoyancy/physics unaffected.

---

# 35. Forward contract for Hull Damage

The later Hull Damage pass will be able to say:

```text
PieceId + local impact position
↓
LocalDamageRecord / Breach
↓
Surface renderer
↓
draw dent / bullet hole / tear / patch at that local position
```

The visual damage mark is independent of wallpaper UV.

Changing:

```text
Riveted Plate
→ Purple Wood Grain
```

does not move the bullet hole.

Changing/replacing the structural material through refit may reset damage according to the Structural Materials pass.

---

# 36. Final principle

The player should experience boat surfaces as:

> Any shape I build can wear any visual skin I want, at a stable visual scale, without stretching or texture swimming.

The system should make dynamic boat construction visually coherent while preserving total creative freedom.

Geometry defines the boat.

Wallpaper defines its character.

---

**End of Boat Surface / Wallpaper Shader System handoff.**
