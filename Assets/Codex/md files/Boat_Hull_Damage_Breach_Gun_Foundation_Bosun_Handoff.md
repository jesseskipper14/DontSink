# DON'T SINK — Boat Hull Damage, Breach & Gun Foundation
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Localized structural damage, penetration, ricochet, breaches/leaks, traversal-scale hull failure, simple patching, and a reusable gun/magazine/ammo foundation with two test-gun configurations  
**Status:** Ready for implementation  
**Depends on:** Boat Structural Materials & Physical Attributes Foundation; Boat Surface / Wallpaper Shader System  
**Out of scope:** Full combat balance, collision damage implementation, complete repair economy, full creature/player combat framework, advanced ammunition logistics, destructible mesh cutting

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current:
   - structural-piece/material implementation from the preceding pass,
   - localized surface/damage presentation seams,
   - flooding/compartment state,
   - compartment stable IDs and geometry ownership,
   - hull/end-cap/floor/wall colliders,
   - water-surface authority,
   - player carry/tool interaction patterns,
   - existing drill implementation,
   - inventory/item instance model,
   - charge/battery/tool capability systems,
   - player/creature damage interfaces if any,
   - save/load,
   - multiplayer authority seams.
5. Reuse existing stable IDs and interaction conventions.
6. Do not invent a parallel flooding system.
7. Do not physically cut hull meshes/colliders for ordinary bullet holes.
8. Keep consequential simulation host-authoritative.
9. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core target loop

The first vertical slice should support:

```text
Player equips gun
↓
Gun fires ballistic shot
↓
Shot hits structural boat piece
↓
Material evaluates structural damage + penetration
↓
Possible outcomes:
- dent / local damage
- ricochet
- clean penetration
↓
Localized damage record created/updated
↓
Possible breach created
↓
Breach position tested against real exterior water
↓
If submerged:
    water enters owning compartment
    visible leak/spurt appears inside
↓
Compartment flood level rises
↓
Existing boat mass/buoyancy consequences occur
↓
Player can patch ordinary breach
```

This pass should make the entire chain functional.

---

# 2. Common localized structural-damage model

Damage is local.

Do not make penetration depend on an entire resized piece's global Integrity percentage.

Use sparse freeform local damage records.

Conceptually:

```text
LocalDamageRecord
- StableId
- PieceId
- LocalPosition
- Radius
- AccumulatedStructuralDamage
- DamageType / Classification
- Penetrated
- VisualSeverity
- OptionalBreachId
```

Nearby impacts may merge into the same local record when appropriate.

Untouched pieces store no records.

---

# 3. Repeated weak hits can weaken one spot

A projectile that cannot penetrate healthy steel may eventually penetrate after repeatedly striking the same local region.

This is a **local weakening** effect.

Conceptually:

```text
Healthy local region:
High effective puncture resistance

Repeated clustered impacts:
↓
Local structural damage rises
↓
Effective local resistance / puncture resistance is reduced
↓
Later shot may penetrate
```

Scattered shots should not magically combine across an entire giant hull plate.

Overall Integrity remains a summary of total remaining structural capacity.

---

# 4. Overall Integrity remains separate

Keep the Structural Materials definition:

```text
Integrity =
RemainingStructuralCapacity
/
MaxStructuralCapacity
```

But local penetration/failure is independent.

Examples:

```text
High-power rifle:
tiny clean puncture
~0.5% integrity loss
but active breach exists

Large blunt impact:
major integrity loss
possibly no penetration
```

This separation is mandatory.

---

# 5. Ballistic shot model

Use a raycast / hitscan-style ballistic simulation for firearms.

Do not simulate rifle bullets as high-speed Rigidbody2D projectiles.

The shot may still have:

- muzzle flash,
- tracer,
- hit effect,
- ricochet visualization,

but consequential collision resolution uses deterministic ray/segment casts.

Slow projectiles such as cannonballs may use physical projectile simulation later.

---

# 6. Universal gun implementation

Build **one reusable gun implementation**, not separate code per weapon.

Gun behavior is data-driven.

Conceptually:

```text
GunDefinition / GunBehavior
- FireRate
- MuzzlePower
- MaxRange
- Accuracy / Spread
- FireMode
- CompatibleMagazineFamilies
- optional recoil/presentation fields
```

The same implementation should support many future guns by changing data.

The two required test guns are configurations/prefabs, not separate systems.

---

# 7. Fire modes

Support at least:

```text
SemiAutomatic
Automatic
```

in the reusable gun implementation.

Burst fire can wait.

Fire rate must be data-driven.

The two initial test guns may both use semi-auto if desired.

---

# 8. Magazine model

Magazines are real inventory item instances.

A magazine should conceptually contain:

```text
MagazineDefinition
- StableId
- Capacity
- CompatibleAmmoFamilies / AmmoIds
- CompatibleGunFamilies / MagazineFamily

MagazineInstance
- CurrentRoundCount
- LoadedAmmoId
```

The gun stores an **actual inserted magazine instance**.

Ejecting the magazine returns the same magazine with remaining rounds preserved.

Do not represent magazines as a magic integer inside the gun.

---

# 9. Loose ammunition

Ammo should exist as item/stack data eventually.

For this mini-pass:

- architecture must support compatible ammo items,
- preloaded magazines are acceptable for testing,
- manual magazine-loading UX may be deferred.

Do not let the absence of ammo-loading UI block the ballistic foundation.

---

# 10. Reloading

Provide a simple reload/eject workflow.

Suggested V1:

```text
R
→ eject/current-mag handling
→ select compatible loaded magazine from inventory
→ insert
```

Selection must be deterministic.

The underlying gun API should expose explicit:

```text
TryInsertMagazine(...)
TryEjectMagazine(...)
```

so future inventory drag/drop can drive it.

Do not hardwire reload logic to a single test magazine.

---

# 11. Ammo ballistic properties

Ammo defines the projectile-side damage characteristics.

Conceptually:

```text
AmmoDefinition
- StructuralDamage
- PenetrationPower
- NominalHoleSize
- RicochetEnergyRetention
- PenetrationEnergyRetention
- optional actor-damage seam
```

Exact naming should fit project conventions.

Gun and ammo both contribute to final shot behavior.

Conceptually:

```text
FinalBallisticPower =
Gun.MuzzlePower
× Ammo ballistic properties
```

Do not make every weapon stat live only on the gun.

---

# 12. Structural damage vs penetration

Keep these independent.

```text
StructuralDamage
```

answers:

> How much structural capacity does this hit remove?

```text
PenetrationPower
```

answers:

> How capable is this shot of punching through this material at this local condition?

This allows:

```text
high-power rifle
→ high penetration
→ small clean hole
→ low integrity loss

heavy blunt projectile later
→ huge structural damage
→ maybe poor penetration
→ large integrity loss
```

---

# 13. Material response

Consume the Structural Materials attributes:

```text
Resistance
PunctureResistance
RicochetTendency
FractureTendency
DeformationTendency
FragmentationTendency
```

For the first pass, fully use:

- Resistance
- PunctureResistance
- RicochetTendency

The remaining tendencies may influence initial visual classification/severity lightly, but do not build full fracture/deformation simulation yet.

---

# 14. Simple angle model

Impact angle should influence penetration and ricochet, but keep it intentionally gamey and simple.

Do **not** build a War Thunder-style armor simulator.

Conceptually:

```text
EffectivePenetration
=
ShotPenetration
× simple angle factor
× local-damage factor
```

And:

```text
RicochetChance
=
Material.RicochetTendency
× PenetrationFailureFactor
× AngleModifier
```

Important gameplay rule:

**Even near-perpendicular impacts may still have a non-zero, meaningful ricochet chance.**

This compensates for the game's mostly square/simple geometry and produces more interesting results.

Grazing impacts should still increase ricochet chance substantially.

Keep constants/tuning serialized.

---

# 15. Consequential ricochets

Ricochets are real continued ballistic events, not decorative sparks.

When a shot ricochets:

1. Calculate a reflected outgoing direction.
2. Reduce remaining ballistic power.
3. Continue raycast from a safely offset new origin.
4. Allow subsequent impacts.

A ricochet can potentially hit:

- another hull piece,
- cargo,
- machinery,
- a player,
- a creature,
- another structural surface.

Actual actor damage may use an existing damage interface if one exists.

---

# 16. Ricochet cap

Maximum ricochets per shot:

```text
2
```

This is intentionally enough to create occasional "pinball" moments.

After the ricochet cap is reached, another failed penetration stops the ballistic continuation.

Also enforce a separate hard cap on total ballistic hit resolutions/segments, suggested around:

```text
8
```

or another small safe value after testing.

This prevents pathological loops.

---

# 17. Ricochet energy loss

Every ricochet reduces remaining ballistic power.

Use a simple retained-energy multiplier.

Conceptually:

```text
RemainingPower *= RicochetRetention
```

Example behavior:

```text
100%
↓ bounce
60%
↓ bounce
36%
```

Exact numbers are authored/tunable.

Remaining range should also be reduced by distance already traveled.

---

# 18. Penetration continues

A successful penetration does **not** end the shot.

After penetration:

1. Create/update local damage.
2. Create breach if applicable.
3. Reduce remaining power according to material/shot settings.
4. Continue the ballistic ray beyond the penetrated surface.

This allows:

```text
exterior hull
↓
cargo
↓
interior machinery
↓
opposite hull
```

or similar chains.

Use the total-hit-resolution safety cap.

---

# 19. Penetration hole size

Breach/hole size is not derived solely from structural damage.

Ammo/source carries a nominal hole size:

```text
NominalHoleSize
```

Material and impact response may modify it.

A very powerful rifle can therefore create:

```text
very high penetration
small hole
small integrity loss
```

This is required behavior.

---

# 20. Structural target resolution

The ballistic solver should resolve a structural hit into at least:

```text
StructuralDamageApplied
Penetrated?
Ricochet?
Stopped?
RemainingBallisticPower
OptionalBreach
```

The outcome must be deterministic from authoritative inputs plus any authoritative random roll.

If randomness is used for ricochet, host/authority owns it.

---

# 21. Breach record

A puncture through a watertight piece may create a gameplay breach.

Conceptually:

```text
HullBreach
- StableId
- PieceId
- PieceLocalPosition
- PieceLocalNormal
- OwningCompartmentId
- HoleArea / Severity
- IsPatched
- IsTraversalOpening
- CurrentLeakState derived
```

Do not store a simple permanent `IsLeaking` flag.

Leak status is derived from current physical conditions.

---

# 22. Compartment ownership

A breach must resolve which interior compartment it opens into.

Bosun must audit the current compartment/hull geometry.

Preferred behavior:

```text
Hit location
↓
resolve adjacent owning compartment by geometry
↓
store stable CompartmentId on breach
```

Do not assume an entire hull piece belongs to one compartment unless the current project geometry guarantees that.

If one structural piece borders multiple compartments, exact hit location decides ownership.

---

# 23. External water test

Leak flow uses the **actual current external water authority**.

Do not use a constant global water Y if the existing wave/water system provides local surface height.

At each relevant leak update:

```text
Breach world position
vs
External water surface at breach X
```

If breach is above external water:

```text
LeakRate = 0
```

If the boat lists/sinks/waves submerge it later:

```text
Leak begins naturally
```

The same hole can therefore start/stop leaking as physical conditions change.

---

# 24. Pressure-head leak model

Use a simple pressure-head concept based on outside-vs-inside water level.

Conceptually:

```text
ExternalHead =
external water level above breach

InternalHead =
compartment water level above breach

NetHead =
max(0, ExternalHead - InternalHead)
```

Then:

```text
LeakRate =
HoleArea
× PressureScale
× function(NetHead)
```

Exact function may be simple and tuned for gameplay.

The key is that incoming flow weakens as internal water rises.

---

# 25. Leak fountain/spurt visuals

Leak VFX must reflect the same net pressure head used by flooding.

Required visual behavior:

```text
fresh floor puncture
→ tall/violent upward spurt

compartment begins filling
→ internal water pushes back
→ spurt height decreases

inside/outside levels approach equality
→ spurt becomes small or disappears
```

For side-wall breaches:

- spray direction should broadly follow inward breach normal.

For floor/downward-surface breaches:

- spray/fountain should visibly rise into the compartment.

This visual is important to the intended multiplayer experience.

Do not simulate individual water particles as authoritative network state.

Clients render VFX from replicated breach/flood state.

---

# 26. Flooding integration

Do not invent a new water-volume system.

Feed leak inflow into the existing compartment flooding authority.

Required chain:

```text
Breach flow rate
↓
existing compartment water state
↓
existing flood level
↓
existing water mass / boat physics
```

If the current flooding API needs a generalized external-inflow seam, add the smallest clean one.

---

# 27. Surface damage visuals

Use the Surface/Wallpaper pass damage overlay seam.

A visual puncture/dent is attached to:

```text
PieceId + piece-local position
```

not wallpaper UV.

Initial visual states should distinguish at least:

- impact/dent,
- puncture/hole,
- severe failed opening,
- patched ordinary breach.

Do not cut the mesh for ordinary bullet holes.

---

# 28. 0% Integrity: catastrophic structural failure

For watertight structural pieces, reaching:

```text
Integrity == 0
```

means catastrophic structural failure.

Required consequences:

- piece remains as stable render/physics ownership object,
- it becomes substantially non-watertight,
- a severe/large breach/opening is created around the worst/appropriate damaged region,
- flooding becomes substantial,
- damage presentation shows major torn/open failure.

Do not simply delete the whole GameObject.

---

# 29. Traversal openings

A sufficiently large catastrophic breach can become a real traversal opening.

This is inspired by Barotrauma-style hull failure.

Required eventual behavior in this pass if current collider architecture permits a safe implementation:

- players can pass through large failed openings,
- creatures can pass through large failed openings.

Ordinary bullet punctures remain leak-only and non-traversable.

Use a configurable:

```text
TraversalSize / SeverityThreshold
```

or equivalent.

Bosun must audit current collider ownership and use the safest approach.

Do **not** perform arbitrary runtime polygon destruction unless necessary.

A localized pass-through portal/trigger/collision-bypass seam is acceptable if it preserves physics stability.

Future monsters may have special behavior allowing them to squeeze/ooze through smaller openings, but that is explicitly **not part of this pass**.

---

# 30. Collision damage forward seam

Collision damage will eventually be important.

Architect structural damage application around a generic impact request, not a gun-only API.

Conceptually:

```text
StructuralImpact
- target piece
- local hit position
- local normal
- structural damage
- penetration power
- source type
- source velocity/energy metadata as useful
```

Gun ballistics are the first producer.

Boat collisions will be a future producer.

Do not implement full collision damage now.

---

# 31. Patching lifecycle

Ordinary breaches can be temporarily patched.

Required items:

```text
Hull Patch
- consumable

Patch Applicator
- reusable holdable tool
```

The applicator should follow the existing drill/tool interaction philosophy where practical.

Player:

1. equips/holds Patch Applicator,
2. targets a reachable ordinary breach,
3. holds interaction/application for a timed duration,
4. successful completion consumes one Hull Patch,
5. breach becomes Patched.

---

# 32. Patch behavior

For an ordinary bullet-sized breach:

```text
1 patch consumable
+ applicator tool
→ patched
```

A patched breach:

- stops or nearly stops leak flow,
- persists across save/load,
- remains visibly patched,
- does **not** restore lost structural Integrity.

Proper structural repair is a later system.

---

# 33. Patch damage / reopening

If a patched local region is struck hard enough again:

```text
patch may fail
↓
breach reopens
```

Implement the simplest robust version.

No passive patch degradation over time yet.

---

# 34. Catastrophic breaches and patching

A catastrophic 0%-Integrity opening must not be fixed by one tiny Hull Patch.

For this pass, choose one of these simple behaviors after auditing implementation cost:

- catastrophic breach cannot be field-patched, or
- requires severity-scaled/multiple patch resources.

Prefer the least misleading stable option.

Do not restore a destroyed structural panel to healthy status via one consumable patch.

---

# 35. Actor-damage seam

Because ricochets/penetrations are consequential, the ballistic solver must be target-agnostic enough to hit actors.

Audit existing player/creature damage interfaces.

If a clean existing health/damage interface exists:

- adapt ballistic hit data to it.

If not:

- expose a generic ballistic-hit seam,
- do not build the full player/creature combat system here.

Structural target behavior is mandatory.

---

# 36. Multiplayer authority

Host/authority owns:

- fire validation,
- magazine/ammo state,
- ballistic resolution,
- ricochet random rolls,
- structural damage,
- local damage records,
- penetration/breach creation,
- breach patching,
- leak flow/flood contribution,
- catastrophic opening state.

Clients render:

- muzzle flash,
- tracers,
- ricochet sparks,
- hit effects,
- leak VFX,
- damage overlays.

Do not network individual particles.

Do not trust clients to authoritatively declare:

```text
"I hit steel and made a breach."
```

---

# 37. Two required test guns

Create two weapon configurations/prefabs using the same universal gun implementation.

## Gun A — ordinary / low-power test gun

Purpose:

- exercise normal firing,
- magazine/ammo handling,
- failed penetration against healthy Steel,
- local dents/damage,
- occasional ricochet,
- repeated-hit local weakening,
- easier penetration of weaker materials.

Its exact stats are tuned by playtest.

## Gun B — high-power test gun

Purpose:

- same underlying gun architecture,
- much higher ballistic/penetration power,
- reliably punctures healthy Steel,
- creates small clean breaches,
- allows leak/flood testing.

It should **not** create an absurd giant hole just because penetration is high.

Nominal hole size remains small.

---

# 38. Test magazine/ammo setup

Provide the minimum data/configurations needed to exercise compatibility.

For example:

```text
Test Magazine Family
Capacity: authored/tunable

Standard Test Ammo
High-Power Test Ammo
```

Whether the two test guns share magazine/ammo family or use separate compatible configs is Bosun's choice after auditing item architecture.

The important part is proving:

- gun compatibility is data-driven,
- magazine state is a real item instance,
- ammo compatibility is explicit,
- final ballistic shot is composed from gun + ammo data.

---

# 39. Debug/developer tools

Provide practical developer visibility.

Per shot:

```text
Gun
Ammo
Initial power
Impact target
Impact angle factor
Structural damage
Penetration value
Material resistance
Material puncture resistance
Penetrated?
Ricochet roll/chance
Remaining power
Ricochet count
Hit-resolution count
```

Per breach:

```text
PieceId
CompartmentId
HoleArea
World position
External head
Internal head
Net head
Leak rate
Patched?
Traversal?
```

Per structural piece:

```text
Integrity
Local damage record count
Active breaches
Failed?
```

Keep this developer-facing.

---

# 40. Performance requirements

Avoid:

- per-frame ballistic simulation after shot resolution,
- dense damage textures/grids,
- per-particle authoritative water simulation,
- rebuilding whole hull geometry for bullet marks,
- unbounded ricochet loops,
- unbounded penetration chains,
- scanning every compartment globally for every leak if stable ownership is known.

Leak calculations may update at a modest fixed simulation cadence consistent with existing flooding.

---

# 41. Persistence

Persist authoritative consequential state:

Gun/item:
- inserted magazine identity,
- magazine remaining rounds,
- loaded ammo identity.

Structural piece:
- remaining structural capacity / Integrity state,
- local damage records,
- failed state.

Breach:
- stable ID,
- piece-local position/normal,
- compartment ID,
- size/severity,
- patched state,
- traversal state if applicable.

Do not persist:
- tracer visuals,
- ricochet sparks,
- transient muzzle flashes,
- individual water particles.

---

# 42. Non-goals

Do not implement:

- full collision damage
- cannonball system
- advanced armor thickness simulation
- realistic ballistics/drag/trajectory drop
- full weapon skill system
- recoil skill trees
- dozens of ammo types
- polished ammo-loading UI
- full creature combat framework
- full player combat framework if no clean existing seam exists
- runtime mesh cutting for ordinary bullet holes
- full structural-collapse simulation
- automatic permanent repair
- drydock structural repair
- patch degradation over time
- oozing monster behavior through tiny holes

---

# 43. Implementation checkpoints

## HULLDMG.1 — Exact-project audit + shared damage contracts

Audit:

- structural material implementation,
- piece geometry/IDs,
- integrity/capacity,
- surface damage hook,
- flooding/compartment APIs,
- water-surface API,
- drill/tool item patterns,
- inventory/magazine feasibility,
- actor damage seams,
- multiplayer ownership.

Implement/define:

- generic StructuralImpact contract,
- sparse LocalDamageRecord model,
- HullBreach model,
- generic ballistic result structures.

No gun firing yet.

Compile/test.

**STOP and report.**

---

## HULLDMG.2 — Universal gun + magazine/ammo foundation

Implement:

- reusable gun behavior,
- SemiAutomatic + Automatic trigger modes,
- gun definition/configuration,
- magazine item instances,
- inserted-magazine state,
- ammo definition,
- compatible magazine/ammo contracts,
- reload/eject operations,
- simple deterministic `R` reload path,
- preloaded test magazines.

Use drill/tool architecture where appropriate rather than inventing unrelated holdable logic.

Compile/test inventory/save behavior.

**STOP and report.**

---

## HULLDMG.3 — Ballistic solver + structural response

Implement:

- hitscan/raycast shot,
- range,
- spread/accuracy,
- structural damage,
- penetration power,
- simple angle modifier,
- Resistance,
- PunctureResistance,
- local weakening,
- consequential ricochet,
- max 2 ricochets,
- bounded total hit resolutions,
- penetration continuation,
- retained power.

Create both test gun configurations.

Compile/test against Wood / Aluminum / Steel.

**STOP and report.**

---

## HULLDMG.4 — Localized visual damage + breach creation

Implement:

- local dent/puncture damage records,
- damage overlay binding,
- clean bullet-hole visualization,
- watertight-piece penetration → breach creation,
- compartment ownership resolution,
- breach persistence.

Verify changing wallpaper does not move local damage.

Compile/test.

**STOP and report.**

---

## HULLDMG.5 — Leak physics + compartment flooding

Implement:

- live breach world position,
- external wave/water-height lookup,
- internal compartment water head,
- NetHead,
- leak-rate calculation,
- existing flooding-system inflow,
- side spray / floor fountain VFX,
- spray height/intensity declining as inside water rises.

Test:

- above-waterline hole,
- barely submerged hole,
- deep hole,
- boat listing into/out of leak,
- rising compartment water suppressing spurt.

Compile/test.

**STOP and report.**

---

## HULLDMG.6 — Catastrophic failure + traversal opening

Implement:

- 0% Integrity severe structural failure for relevant watertight pieces,
- major breach/opening,
- substantial flooding,
- severe damage presentation,
- safe localized traversal opening where current collider architecture permits,
- player/creature passage for sufficiently large breach.

Do not destabilize unrelated collision behavior.

Compile/test.

**STOP and report.**

---

## HULLDMG.7 — Patch Applicator + Hull Patch

Implement:

- reusable Patch Applicator holdable,
- consumable Hull Patch,
- breach targeting,
- timed hold-to-apply,
- consume one patch for ordinary breach,
- persistent Patched state,
- leak stopped/nearly stopped,
- visible patch,
- structural Integrity unchanged,
- re-hit can reopen/destroy patch.

Handle catastrophic breaches conservatively.

Compile/test.

**STOP and report.**

---

## HULLDMG.8 — Save/load + multiplayer-safe regression

Verify persistence of:

- gun magazine state,
- ammo count,
- local damage,
- breaches,
- patches,
- failed/traversal state,
- compartment ownership.

Review host-authoritative seams.

Regression-test:

- buoyancy,
- flooding,
- inventory drag,
- tool handling,
- interaction prompts,
- sorting,
- boarding/unboarding,
- compartment stable IDs.

**STOP and report.**

---

# 44. Acceptance tests

## Gun foundation

1. One universal gun implementation supports two different gun configs.
2. Semi-auto works.
3. Automatic mode exists and obeys fire rate.
4. Gun accepts only compatible magazines.
5. Inserted magazine is a real item instance.
6. Ejected magazine retains remaining rounds.
7. Reload selects a compatible loaded magazine deterministically.
8. Save/load preserves inserted magazine and rounds.

## Material response

9. Ordinary test gun frequently fails to penetrate healthy Steel.
10. Failed Steel impacts can dent/damage locally.
11. Steel may ricochet even at near-perpendicular hits.
12. Grazing Steel hits ricochet more often.
13. Wood is easier to penetrate and ricochets much less.
14. Repeated hits in one location can eventually weaken/puncture material.
15. Widely scattered hits do not unfairly combine as one local weakness.

## Ricochet / penetration

16. Ricochet continuation can hit another target.
17. First ricochet loses power.
18. Second ricochet loses further power.
19. Maximum ricochets = 2.
20. Total hit resolutions are bounded.
21. Penetrated shots continue beyond the first surface.
22. Remaining ballistic power decreases after penetration.

## High-power test gun

23. High-power gun reliably punches healthy Steel after tuning.
24. It creates a small clean hole, not a giant breach.
25. Integrity loss can remain small despite successful penetration.

## Breaches

26. A penetrating hit on a watertight hull piece creates a stable breach.
27. Breach stores piece-local position.
28. Correct owning compartment is resolved.
29. Wallpaper changes do not move the breach/damage visual.

## Water behavior

30. Above-waterline breach does not leak.
31. Same hole begins leaking if boat lists/sinks until submerged.
32. Barely submerged hole leaks slowly.
33. Deeper hole leaks faster.
34. Floor puncture produces visible upward fountain/spurt.
35. As compartment water rises, fountain height/intensity drops.
36. Leak feeds the existing flooding system.
37. Added flood water affects existing boat physics normally.

## Structural failure

38. Integrity can reach 0 without deleting the piece GameObject.
39. 0% watertight structural piece creates a major flooding opening.
40. Major opening floods substantially.
41. Large enough failure opening permits player passage.
42. Large enough failure opening permits creature passage.
43. Ordinary bullet hole is not traversable.

## Patching

44. Patch Applicator is a reusable holdable/tool.
45. Hull Patch is consumable.
46. Ordinary breach requires one patch.
47. Timed patch completion consumes one patch.
48. Patched breach stops/nearly stops flow.
49. Patching does not restore Integrity.
50. Patched state persists.
51. Re-damaging the same location can reopen it.

## Multiplayer architecture

52. Client cannot authoritatively create a breach.
53. Ricochet/random ballistic outcome belongs to authority.
54. Clients can render transient VFX from authoritative result/state.
55. No particle-level replication is required.

---

# 45. Forward contracts

## Collision damage

Future collisions should call the same generic StructuralImpact path.

Examples:

```text
boat hits rock
↓
impact energy → StructuralDamage
shape/contact → local radius
possible puncture/tear depending material/source
```

No gun-specific assumptions should block this.

## Module damage

Future machinery/module damage may use a related impact contract while keeping module health/condition semantics separate.

## Full structural repair

Future drydock/repair systems may:

- restore structural capacity,
- replace material,
- remove local damage,
- remove permanent breach records.

Temporary patches do not perform those operations.

## Creature behaviors

Future creatures may interpret breach/traversal geometry differently.

A future "oozing" monster may squeeze through openings that are too small for normal actors.

Do not encode “not traversable by player” as “nobody can ever traverse.”

## Combat expansion

Future guns should primarily require new data/configuration:

```text
gun definition
magazine compatibility
ammo definition
sprites/audio/VFX
```

not new gun code.

---

# 46. Final principle

The damage system should make all of these statements true:

> A powerful rifle can make a tiny clean hole through healthy Steel while barely changing overall Integrity.

> A weak gun can bounce off Steel, then ricochet into something else.

> Repeated hits can weaken one exact patch without making a giant hull plate globally paper-thin.

> A hole above the waterline is harmless until the boat physically puts it underwater.

> A fresh floor puncture can blast a fountain into the compartment, then visibly lose pressure as the compartment fills.

> A catastrophic hull failure can become a real opening that water, players, and creatures can pass through.

> A player can slap a temporary patch over an ordinary breach, stop the leak, and keep going without magically repairing the structure.

That is the target vertical slice.

---

**End of Boat Hull Damage, Breach & Gun Foundation handoff.**
