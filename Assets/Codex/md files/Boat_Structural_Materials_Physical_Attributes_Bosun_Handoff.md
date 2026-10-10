# DON'T SINK — Boat Structural Materials & Physical Attributes Foundation
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Material-bearing boat pieces, material definitions, resized mass/capacity calculation, structural mass + center-of-mass aggregation, material swapping, and damage-property seams  
**Status:** Ready for implementation  
**Out of scope:** Projectile damage, breaches/leaks, guns, shader/wallpaper implementation, repair economy

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Inspect the current SegmentResizer/resize path, installed-piece persistence, Boat Builder piece model, Rigidbody2D/mass setup, buoyancy force system, and any existing item/module mass aggregation before changing anything.
5. Reuse current installed-piece identity/save systems rather than creating a parallel structural-piece inventory.
6. Keep this pass multiplayer-safe even though full multiplayer is not implemented here.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core design

A boat piece has three separate concepts:

```text
PIECE GEOMETRY
"What shape/size is this?"

        ↓

STRUCTURAL MATERIAL
"What is it made from?"

        ↓

SURFACE STYLE / WALLPAPER
"What does it look like?"
```

This pass implements the first two and creates a clean seam for the third.

**Structural material must never be synonymous with a sprite/material/shader choice.**

A steel wall may later use multiple steel-compatible wallpapers. A wood wall may use multiple wood-compatible wallpapers. Appearance is a later pass.

---

# 2. Material-bearing boat pieces

The following Boat Builder piece types participate:

- hull segments
- walls
- decks/floors
- end caps / arbitrary structural closure pieces
- doors
- hatches
- structural roof/ceiling pieces if the current project has them

Doors and hatches receive the same fundamental material attributes as other structural pieces.

Installed machinery/modules such as engines, generators, pumps, turrets, lockers, etc. do **not** derive their mass from structural material. They retain authored/module mass through their own systems.

---

# 3. Piece identity is independent of material

A structural piece prefab/type is not permanently “the steel version” or “the wood version.”

Example:

```text
Hull Segment
├─ Wood
├─ Aluminum
└─ Steel
```

The installed piece instance owns its selected material.

Changing material does not require destroying/rebuilding piece geometry.

This allows refits such as:

```text
wood boat
↓
player can now afford steel
↓
select existing pieces
↓
change material
↓
same geometry / same installed layout
↓
mass, capacity, friction, cost properties recalculate
```

Material changes are refit operations, not runtime transmutation.

---

# 4. Initial materials

Create three initial structural material definitions:

- **Wood**
- **Aluminum**
- **Steel**

Do not over-balance them yet. Values should be serialized/data-driven and tuned through playtesting.

Intended personalities:

| Property | Wood | Aluminum | Steel |
|---|---|---|---|
| Mass | Low | Low | High |
| Resistance | Low | Medium | High |
| Durability | Medium | Medium | High |
| Puncture resistance | Low | Medium | High |
| Ricochet tendency | Very low | Low–medium | High |
| Deformation tendency | Low | High | Medium/high |
| Fragmentation tendency | High | Very low | Very low |
| Cost | Low | High | Medium/high |

These are qualitative targets, not final numbers.

---

# 5. Structural material definition

Use a data-driven definition following the project's existing ScriptableObject/content-definition conventions where practical.

Conceptually:

```text
StructuralMaterialDefinition

Identity
- StableId
- DisplayName

Physical
- DensityCoefficient
- BaseFriction
- WetFrictionMultiplier

Durability
- DurabilityCoefficient
- Resistance

Damage response
- PunctureResistance
- RicochetTendency
- FractureTendency
- DeformationTendency
- FragmentationTendency

Economy seams
- ConstructionCostPerUnit
- RepairCostPerUnit

Presentation seam
- CompatibleSurfaceFamilyIds / default surface family reference
```

Exact names should follow live project conventions.

Do not hardcode material behavior by enum switch if authored definitions fit the existing content architecture.

---

# 6. Mass model

Use an area × thickness × density-style model:

```text
PieceMass =
Effective2DArea
× StructuralThickness
× Material.DensityCoefficient
```

This is intentionally a gameplay-scaled 2D structural model, not literal naval-engineering units.

It has one important advantage: if configurable thickness is added later, the mass model already supports it.

For this pass, thickness is authored per piece/type rather than player-configurable.

---

# 7. Authored structural thickness

Each material-bearing piece/type gets an effective structural thickness/material-amount value.

Examples conceptually:

```text
ordinary wall     1.0
deck              1.1
hull segment      1.4
door              1.2
heavy hatch       1.8
```

These are examples only, not prescribed values.

The point is that a hatch can represent more material than a decorative wall of identical visible area.

Do not expose thickness as a player-facing Boat Builder control in this pass.

Keep the axis available for future Light / Standard / Heavy construction if that becomes desirable.

---

# 8. Geometry source of truth

Mass/capacity calculations must use the piece's **actual resized geometry**.

Do not blindly use:

- prefab default dimensions
- sprite bounds
- transform.localScale
- arbitrary base weight multiplied by scale

unless the live resize architecture proves one is truly authoritative.

Bosun must inspect SegmentResizer and the geometry/collider representation that owns final shape.

For simple rectangles:

```text
EffectiveArea = width × height
```

For arbitrary end caps/polygons:

- prefer actual polygon area when available,
- otherwise use the closest authoritative geometry-area representation already present.

Do not introduce expensive per-frame geometry integration. Recalculate only when geometry/material inputs change.

---

# 9. Structural mass aggregation

Every material-bearing piece contributes:

```text
CalculatedMass
BoatLocalCenterOfMassPosition
```

The boat automatically aggregates structural mass:

```text
StructuralMass = Σ pieceMass
```

This is **base structural mass**, separate from:

- installed modules
- cargo/items
- flood water
- players
- diving bell
- money chest
- other dynamic masses

Do not collapse these into one opaque authored boat-mass constant.

---

# 10. Center of mass is required

Structural material placement must affect boat center of mass.

A steel-heavy port side and wood-heavy starboard side should not magically balance.

Conceptually:

```text
StructuralCOM =
Σ(pieceBoatLocalPosition × pieceMass)
────────────────────────────────────
Σ(pieceMass)
```

The overall boat COM should combine structural contribution with existing/future mass contributors through the current boat physics architecture.

Bosun must inspect current Rigidbody2D / buoyancy / mass aggregation ownership before choosing the implementation seam.

Do not duplicate an existing authoritative COM system.

---

# 11. Existing buoyancy remains authoritative

This pass does **not** add fake material buoyancy.

No:

```text
SteelBuoyancy = -0.7
WoodBuoyancy = +0.4
```

Structural material affects mass.

Existing hull geometry/displacement/buoyancy continues to determine lift.

Dependency:

```text
material + geometry
↓
structural mass / COM
↓
existing boat mass physics
↓
existing buoyancy behavior
```

Do not rewrite buoyancy formulas.

---

# 12. Resistance replaces “Strength”

Call the general damage-resistance property **Resistance**, not Strength.

Meaning:

> How strongly this material resists ordinary incoming structural damage.

Keep `Strength` available as a future term for actual load-bearing/structural engineering.

Resistance and penetration are intentionally separate.

---

# 13. Structural capacity and Integrity

Do not model pieces as ordinary RPG health bars.

A material-bearing piece has structural capacity derived from material quantity:

```text
MaxStructuralCapacity =
EffectiveArea
× StructuralThickness
× Material.DurabilityCoefficient
```

The player-facing summary is:

```text
Integrity =
RemainingStructuralCapacity
/
MaxStructuralCapacity
```

Integrity means:

> The percentage of the piece's original structural usefulness that remains.

Examples:

```text
100% = essentially sound
70%  = meaningfully battered
30%  = badly compromised
0%   = structurally failed
```

Exact UI thresholds remain future/tunable.

---

# 14. Integrity is NOT the penetration gate

Critical rule:

A piece's overall integrity does not decide whether a bullet hole can exist.

Example:

A powerful rifle may:

- make a clean puncture through healthy steel,
- create a water breach,
- reduce total integrity by only ~0.5%.

A cannonball may:

- fail to fully penetrate,
- massively deform the plate,
- remove a large amount of structural capacity.

Therefore:

```text
local penetration/failure
≠
overall integrity percentage
```

This separation is mandatory.

---

# 15. What 0% Integrity means

At 0% Integrity, the piece is **structurally failed**.

It does **not** automatically disappear or delete its collider/geometry.

Never implement:

```text
Integrity == 0
→ Destroy(piece)
```

The piece remains for rendering/physics stability.

Future Hull Damage behavior may interpret failure by piece type:

- external hull / wall / end cap: no longer reliably watertight, severe breach potential
- door/hatch: no longer reliably seals; may jam/break
- deck: structurally failed/unsafe later
- interior wall: loses relevant structural/sealing function

This pass establishes the state/semantics only.

---

# 16. Local damage architecture seam

Future Hull Damage requires **freeform local damage**, not a fixed piece-wide percentage gate.

Preferred model:

```text
LocalDamageRecord
- Stable/local record id
- Boat-piece-local position
- Radius / affected area
- AccumulatedStructuralDamage
- Damage classification/type
- Penetrated flag/state
- Optional breach id
- Visual severity/state
```

Nearby damage may merge into an existing record when appropriate.

This must remain sparse:

- untouched pieces store no local records,
- only meaningful damaged areas exist,
- repeated nearby hits merge,
- cap/merge policy prevents thousands of tiny records.

The actual hit/merge/damage implementation belongs to the Hull Damage pass.

This pass should establish only the persistence/data seam required to support it.

---

# 17. Performance requirement for local damage

Freeform local damage is accepted only if it remains sparse and event-driven.

Never:

- generate dense pixel/grid damage maps for every structural piece,
- update all damage locations every frame,
- replicate thousands of empty cells.

A large pristine hull piece should cost essentially the same damage-state memory as a small pristine hull piece.

---

# 18. Damage response attributes

Material definitions include independent tendencies:

```text
PunctureResistance
RicochetTendency
FractureTendency
DeformationTendency
FragmentationTendency
```

These are inputs to the future Hull Damage system.

Do not implement full fracture/deformation simulation here.

They exist so later systems can distinguish material behavior without hardcoding:

```text
if steel ...
if wood ...
```

---

# 19. Resistance vs PunctureResistance

These have distinct jobs.

## Resistance
Opposes/reduces ordinary structural damage.

Future example:

```text
IncomingStructuralDamage
vs
Material.Resistance
→ lost structural capacity
```

## PunctureResistance
Opposes penetration/breach creation.

Future example:

```text
Projectile.PenetrationPower
vs
Material.PunctureResistance
→ penetrate / fail
```

This supports physically different outcomes.

---

# 20. Ricochet seam

`RicochetTendency` belongs on the material definition.

The future Hull Damage/weapon system evaluates ricochet only when penetration is not achieved or where its impact model says ricochet remains possible.

Ricochet chance should eventually account for:

- material RicochetTendency
- impact angle
- projectile penetration power
- how badly penetration failed

Do not implement projectile ricochet in this pass.

---

# 21. Future projectile model seam

The later gun/damage pass is expected to distinguish at least:

```text
Projectile StructuralDamage
Projectile PenetrationPower
```

These are not the same.

This enables:

```text
high-power rifle
→ high penetration
→ clean small hole
→ low total integrity loss

cannonball / blunt impact
→ huge structural damage
→ may not penetrate
→ large integrity loss
```

Do not collapse future damage into one `damage` float in a way that blocks this.

---

# 22. Friction

Structural materials provide:

```text
BaseFriction
WetFrictionMultiplier
```

Use current physics-material/contact architecture where practical.

Immediate intent:

- wood can be fairly grippy,
- metal can become substantially slipperier when wet,
- flooding/wetness may eventually modify contact friction.

Do not build a separate floor-covering system now.

The later Surface/Wallpaper pass may eventually provide overrides if coatings/rubber decking become real gameplay.

---

# 23. Material swapping / refit

Material swapping is allowed in Boat Builder/refit context.

Changing:

```text
Wood → Steel
```

recalculates:

- mass
- center-of-mass contribution
- structural capacity
- resistance properties
- material cost data
- compatible/default surface presentation seam

Changing material represents replacement/refit.

The replacement material begins structurally sound.

Do not preserve old local wood damage holes as magical holes in newly installed steel.

---

# 24. Damaged-piece trade-in seam

Future economy behavior:

Damaged structural pieces/material should return less trade-in/sale value.

Conceptually:

```text
TradeInValue =
MaterialValue
× MaterialAmount
× Condition/IntegrityFactor
```

Actual trading/economy integration is outside this pass.

The data model must make it straightforward later.

---

# 25. Construction / repair cost seam

Prefer per-unit material cost:

```text
ConstructionCost =
EffectiveArea
× StructuralThickness
× Material.ConstructionCostPerUnit
```

Repair may use:

```text
RepairCostPerUnit
```

and damaged quantity.

Actual payment/inventory consumption is deferred.

---

# 26. Surface-style compatibility seam

Structural material and visual style remain independent, but not visually misleading.

A future SurfaceStyle/wallpaper definition should be able to declare compatible structural material families.

Examples:

Steel:
- smooth painted steel
- riveted plate
- weathered plate
- compatible interior metal panel styles

Wood:
- planks
- painted wood
- framed timber paneling

Do not let a raw oak-plank style make a steel hull visually read as wood in normal player-facing use.

Actual shader implementation belongs to the next MD.

---

# 27. Multiplayer authority assumptions

Architect for host-authoritative multiplayer.

Consequential shared state eventually includes:

- installed piece material selection
- structural geometry/size
- structural mass
- structural capacity/integrity
- local damage records
- piece failure state

Derived values should be recomputable from authoritative inputs where practical.

Do not replicate per-frame decorative calculations.

Actual transport/replication is deferred.

---

# 28. Persistence

Material-bearing installed pieces must persist enough information to restore the exact boat:

- stable installed piece identity
- selected structural material ID
- resized geometry
- authored/effective thickness where instance-specific
- current integrity/remaining structural capacity when damage exists
- future local damage records
- future failure state

Do not persist fragile direct asset indices if stable IDs/content references are the project norm.

Old saves lacking material assignment must receive deterministic/default migration behavior.

---

# 29. Migration/default behavior

Audit current prefabs/pieces before choosing defaults.

Preferred migration:

- existing metal-looking structural pieces default to Steel unless live data clearly says otherwise,
- existing wooden pieces default Wood,
- ambiguous pieces use explicitly authored defaults rather than name-string heuristics where possible.

Bosun should report the proposed migration map before destructive serialized changes.

---

# 30. Structural mass refresh lifecycle

Structural mass / COM should update when:

- piece installed
- piece removed
- piece resized
- material changed
- thickness/material amount changed
- save/load restores pieces
- builder applies/refits changes

Do not recompute the whole boat every Update unless the live architecture genuinely requires it.

Prefer dirty/event-driven aggregation.

---

# 31. Debug/developer visibility

Provide a lightweight developer inspection path.

Per piece:
- material
- effective area
- thickness
- calculated mass
- max structural capacity
- current integrity
- local COM position

Per boat:
- total structural mass
- structural COM
- total/current Rigidbody mass if accessible
- combined COM if available

Inspector/debug UI/logging is sufficient.

Do not add player-facing HUD clutter in this pass.

---

# 32. Non-goals

Do not implement:

- projectile weapons
- gun input
- projectile penetration
- ricochet trajectory
- bullet-hole visuals
- breach creation
- water leak VFX
- compartment flooding changes
- actual hull fracture geometry
- mesh/collider cutting
- damage shader/decal rendering
- repair interactions
- construction purchasing
- trade-in economy
- player-configurable structural thickness
- load-bearing structural simulation
- wallpaper/shader selection UI
- new buoyancy formulas

Those are later consumers.

---

# 33. Implementation checkpoints

## MAT.1 — Audit + material definitions

Inspect:

- structural Boat Builder piece classes
- hatches/doors
- SegmentResizer / resizing
- installed module/piece persistence
- current Rigidbody2D mass/COM ownership
- buoyancy forces
- current friction/PhysicsMaterial2D usage
- current Boat Builder selection/property-editing UI

Then implement:

- data-driven StructuralMaterialDefinition
- Steel / Aluminum / Wood assets
- stable IDs
- default material assignment/migration plan

Compile/test.

**STOP and report.**

---

## MAT.2 — Piece geometry → mass / capacity

Implement:

- authoritative EffectiveArea query
- authored/effective StructuralThickness
- calculated piece mass
- calculated MaxStructuralCapacity
- current structural capacity/integrity scaffold
- recalculation on resize/material changes

Preserve existing resize/collider behavior.

Compile/test.

**STOP and report.**

---

## MAT.3 — Boat structural mass + COM aggregation

Implement:

- event/dirty-driven structural mass aggregation
- structural COM aggregation
- integration into existing boat mass/COM authority
- no buoyancy rewrite

Test deliberately asymmetric boats.

Compile/test.

**STOP and report.**

---

## MAT.4 — Boat Builder material refit

Implement the smallest practical material-selection workflow.

Requirements:

- selected structural piece can cycle/select valid material
- same geometry remains installed
- mass/capacity update immediately
- multi-select only if current builder architecture makes it safe/simple
- refit resets structural damage for replacement material
- compatible/default surface family seam updates for future shader pass

No construction payment yet.

Compile/test.

**STOP and report.**

---

## MAT.5 — Persistence / regression

Verify:

- save/load preserves selected materials
- resized pieces restore same calculated mass/capacity
- hatches/doors participate correctly
- structural mass/COM rebuild correctly on load
- old boat saves migrate deterministically
- modules/cargo/flood water remain separate mass contributors
- existing buoyancy still works
- no per-frame structural recomputation regression

Compile/test.

**STOP and report.**

---

# 34. Acceptance tests

## Material identity
1. Steel, Aluminum, and Wood definitions exist as data assets.
2. Hull/wall/deck/end-cap/door/hatch can hold material identity independent of prefab identity.
3. Changing material does not replace installed geometry.

## Resizing
4. Resize a wall to twice the relevant area; calculated mass scales accordingly.
5. Structural capacity scales with material quantity.
6. Arbitrary end-cap area uses authoritative geometry rather than sprite-scale guesswork.
7. Resizing does not require per-frame recalculation.

## Material differences
8. Same geometry in Steel weighs more than Aluminum/Wood according to authored values.
9. Same geometry has different resistance/capacity values by material.
10. Material swap updates all derived values immediately.

## Center of mass
11. Symmetric boat with symmetric materials has centered structural COM.
12. Convert only one side to Steel; COM shifts toward that side.
13. Existing buoyancy reacts through existing mass/COM physics, not a new material-buoyancy stat.

## Hatches / doors
14. Door/hatch mass changes with material.
15. Door/hatch structural capacity changes with material.
16. Door/hatch resize, if supported, recalculates correctly.

## Integrity scaffold
17. Fresh/replaced material begins at 100% Integrity.
18. Integrity represents remaining structural capacity, not penetration chance.
19. 0% state can exist without deleting the piece GameObject.

## Persistence
20. Save/load restores exact material identity.
21. Save/load restores geometry and therefore same derived mass/capacity.
22. Old saves receive deterministic defaults.
23. Structural mass/COM after load matches pre-save state.

## Regression
24. Existing modules still use module mass.
25. Cargo mass remains independent.
26. Flood-water mass remains independent.
27. Existing buoyancy behavior still functions.
28. Boat Builder undo/redo/save workflows, if present, remain valid.
29. No new continuous structural-mass polling appears in Update unless justified.

---

# 35. Forward contract for Hull Damage

The later Hull Damage + Breach pass consumes this foundation.

Expected flow:

```text
Projectile / collision / other damage source
↓
local impact on material-bearing piece
↓
Material.Resistance → structural damage
Material.PunctureResistance → penetration test
Material.RicochetTendency → failed-penetration response
other material tendencies → visual/failure behavior
↓
sparse freeform LocalDamageRecord
↓
remaining structural capacity changes
↓
overall Integrity summary changes
↓
possible local puncture/breach independent of Integrity %
```

The first weapon test will use one gun implementation with two configurations/prefabs:

- ordinary/low-power gun to exercise failed penetration, dents/ricochet
- high-power version to reliably puncture Steel and create a small breach

Do not implement either gun in this material pass.

---

# 36. Final principle

The system should make this statement true:

> The boat is made from actual material-bearing pieces whose size, material, and position determine how heavy and structurally capable the boat is.

And also:

> A huge steel hull plate can be extremely durable overall while still receiving a tiny clean bullet puncture at one local point.

That distinction is the foundation for the later gun → bullet hole → water leak → compartment flooding showcase.

---

**End of Boat Structural Materials & Physical Attributes Foundation handoff.**
