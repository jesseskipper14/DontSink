# DON'T SINK — Module Effects / Faults / Buffs System
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Generic data-driven module effects framework supporting positive, negative, mixed, temporary, permanent, service-cleared, repair-cleared, random faults, rare traits, reusable VFX/SFX presentation, persistence, and host-authoritative application/removal  
**Status:** Ready for implementation  
**Critical precedent:** Model this system directly after the **existing Player Buff architecture** wherever that code already solves the same problem. Reuse its patterns instead of inventing a second parallel modifier framework.  
**Immediate consumers:** Engines, generators, pumps, turrets, future maintainable modules, rare module instances, damage faults, maintenance/service effects  
**Out of scope:** Player perk system, node automation, full procedural loot generation, bespoke module-specific fault code

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current:
   - PlayerBuff architecture,
   - PlayerBuffDefinition,
   - existing modifier operation types,
   - provider/application/removal flow,
   - attribute/stat resolution,
   - persistence,
   - UI presentation,
   - module definitions,
   - installed module runtime state,
   - Module Condition / Integrity implementation,
   - module power/output/flow/fire-rate stats,
   - damage hooks,
   - maintenance/service hooks,
   - VFX/SFX helpers,
   - multiplayer authority seams.
5. If the existing Player Buff system already supports a needed pattern cleanly, **reuse or generalize that pattern**.
6. Do not fork a similar-but-different modifier stack unless exact-project constraints force it.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core principle

An effect is just an effect.

It may be:

- beneficial,
- harmful,
- mixed,
- temporary,
- permanent,
- damage-induced,
- maintenance-induced,
- player-applied,
- randomly rolled,
- quest/event driven,
- rare-instance inherent.

The underlying system does **not** need separate Buff and Fault architectures.

Conceptually:

```text
Module
├─ Base authored values
├─ Integrity
├─ Condition
└─ ActiveEffects[]
```

Each effect contributes generic modifiers and optional reusable presentation.

---

# 2. Reuse the Player Buff model

Bosun must first inspect the existing Player Buff code and model the module-effect system after it.

The current player system already supports the core idea of:

```text
Definition
+
Provider/source
+
runtime application/removal
+
generic stat modification
```

The module system should follow the same architecture where practical.

Preferred outcome:

```text
Player buffs
and
Module effects
```

share concepts and possibly shared lower-level stat-modifier infrastructure, while retaining domain-specific wrappers if needed.

Do not create duplicate logic for:

- AddFlat,
- AddPercent,
- Multiply,
- Override,
- stacking,
- provider/source tracking,
- apply/remove lifecycle,

if the existing Player Buff implementation already does those cleanly.

---

# 3. ModuleEffectDefinition

Create a data-driven effect definition, following the current PlayerBuffDefinition style where appropriate.

Conceptually:

```text
ModuleEffectDefinition

Identity
- StableId
- DisplayName
- Description

Classification
- Positive
- Negative
- Mixed
- Neutral
  (UI/presentation only)

Modifiers[]
- TargetStatId
- Operation
- Value

Stacking
- StackingPolicy
- MaxStacks

Lifetime
- DurationPolicy
- DurationSeconds if timed

Removal
- RemovalPolicy

Eligibility
- RequiredTags
- ExcludedTags
- Integrity/Condition predicates if applicable
- existing-effect restrictions

Presentation
- PresentationProfile

Source metadata
- source/category tags
```

Exact naming should match project conventions.

---

# 4. Effect classification is presentation-only

Do not make gameplay depend on:

```text
Positive
Negative
Mixed
Neutral
```

Those are UI/descriptive categories only.

Example:

```text
Overclocked
+30% output
+25% power draw
+40% wear rate
```

This is naturally mixed.

The modifier system should not care.

---

# 5. Generic stat targeting

Effects target stable stat IDs through a common module stat interface.

Do not hardcode effect-specific fields in the effect system.

Conceptual stat IDs:

```text
module.output
module.power_draw
module.wear_rate
module.condition_drain
module.integrity_drain

pump.flow_rate
generator.fuel_burn
generator.output
weapon.fire_rate
weapon.accuracy
weapon.spread
engine.thrust
engine.efficiency
```

Exact IDs should follow current naming conventions.

A module exposes the stats it supports.

An effect targeting an unsupported stat should fail predictably and visibly in development/debugging.

---

# 6. Modifier operations

Use the operation set already present in the Player Buff system if equivalent.

Expected operations:

```text
AddFlat
AddPercent
Multiply
Override
```

Preferred deterministic order:

```text
Base value
→ flat additions
→ additive percentages
→ multiplicative modifiers
→ override if present
→ stat-specific clamp
```

If Player Buff already has a tested order, use that instead of duplicating this exact sequence.

`Override` should remain uncommon and deterministic.

---

# 7. No hardcoded effects

Do not create code like:

```text
if (effect == Misfiring)
if (effect == WornBearings)
if (effect == HighOutput)
```

Gameplay systems query effective stats.

Example:

```text
effectiveOutput =
ModuleStats.Resolve("module.output")
```

Then an effect changes that result through generic modifiers.

The module does not need to know why its output changed.

---

# 8. Reusable presentation effects

An effect may optionally drive shared visual/audio presentation.

Create or reuse a generic presentation layer.

Conceptually:

```text
ModuleEffectPresentationProfile
├─ SmokeProfile
├─ SparksProfile
├─ LoopAudio
├─ IntermittentAudio
├─ Flicker
├─ Shake/Jitter
├─ Tint
└─ future reusable presentation channels
```

Examples:

```text
Worn Bearings
-10% output
+15% power draw
Presentation:
  occasional sparks
  subtle grinding loop
```

```text
Overclocked
+25% output
+20% wear
Presentation:
  brighter glow
  faster animation
  occasional sparks
```

```text
Reinforced
+20% resistance
Presentation:
  none
```

Do not create one-off controllers such as:

```text
EngineSmokeController
GeneratorSmokeController
PumpSmokeController
```

when a reusable effect-presentation system can handle them.

---

# 9. Presentation attachment points

Modules should expose generic presentation anchors/attachment points where practical.

Examples:

```text
PrimaryEffectAnchor
SmokeAnchor
SparkAnchor
AudioAnchor
```

Do not require every module prefab to use every anchor.

If current module prefabs already have useful child transforms or presentation roots, reuse them.

The presentation layer should degrade gracefully if a requested anchor is absent.

---

# 10. Configurable stacking

Support configurable effect stacking.

Initial policies:

```text
RejectDuplicate
RefreshDuration
ReplaceExisting
Stack
IndependentInstances
```

Optional:

```text
MaxStacks
```

Examples:

```text
Factory Tuned
→ RejectDuplicate

Corroded
→ Stack, MaxStacks = 3

Temporary Surge
→ RefreshDuration

Damaged Regulator
→ ReplaceExisting
```

If the Player Buff code already implements equivalent policies, reuse those patterns.

---

# 11. Duration policies

Initial lifetime policies:

```text
Permanent
Timed
UntilServiced
UntilRepaired
```

Future extension may add other lifecycle rules.

Do not implement speculative extra duration types without a current use.

---

# 12. Timed effects use simulation/gameplay time

Timed effects tick using game/simulation time while the module is loaded and participating in simulation.

Do not use real-world wall-clock time.

Example:

```text
30-second temporary fault
```

does not expire because the player quit for dinner.

This should remain consistent with the current decision that boat maintenance/wear does not simulate while unloaded.

---

# 13. Removal policies

Effects may require different removal conditions.

Examples:

```text
UntilServiced
→ clears during appropriate maintenance/service action

UntilRepaired
→ clears when required repair threshold/action is satisfied

Permanent
→ does not clear through normal service/repair

Timed
→ expires normally
```

A definition may also explicitly forbid ordinary removal.

Do not assume all negative effects disappear when Condition increases.

---

# 14. Source tracking

Every runtime effect instance should retain its source where meaningful.

Conceptually:

```text
EffectSource
- Damage
- Wear
- Maintenance
- PlayerApplied
- RareTrait
- Quest
- Event
- GeneratedItem
- Debug
```

Exact implementation may use tags/IDs rather than a rigid enum if the Player Buff system already has a better provider/source model.

This is useful for UI, save/load, removal rules, debugging, and future analytics/quests.

---

# 15. Effect instances

Runtime state should be separate from the immutable definition.

Conceptually:

```text
ModuleEffectInstance
- DefinitionId
- SourceId / ProviderId
- StackCount
- RemainingDuration
- AppliedAtSequence / order if needed
- instance-specific metadata only where necessary
```

Do not mutate ScriptableObject definitions at runtime.

---

# 16. Fault generation uses weighted effect pools

Random faults should come from data-driven weighted pools.

Conceptually:

```text
ModuleEffectPool
├─ Effect A, weight 5
├─ Effect B, weight 3
├─ Effect C, weight 1
```

Pools may also apply eligibility filters.

Examples:

```text
Engine Wear Fault Pool
Engine Impact Fault Pool
Generator Electrical Fault Pool
Pump Flooding Fault Pool
Rare Positive Trait Pool
Rare Mixed Trait Pool
```

Do not hardcode random fault tables inside module scripts.

---

# 17. Eligibility rules

Effect pool entries may be eligible based on:

- module tags/type,
- Integrity band,
- Condition band,
- damage source/type,
- whether module is submerged,
- existing active effects,
- source/event tags.

Keep this generic/data-driven.

Do not create dedicated code branches for specific named effects.

---

# 18. Integrity-driven fault checks

Integrity is the main driver of actual mechanical consequence.

Fault checks should happen on meaningful events or bounded intervals.

Examples:

```text
significant direct damage
crossing an Integrity threshold
operation while damaged
bounded operating interval
major impact
submersion event
```

Do **not** roll faults every frame.

Sudden bullet/impact damage may trigger an immediate fault roll.

---

# 19. Condition interaction

Condition remains maintenance burden.

Low Condition may:

```text
drain Integrity
increase eligibility/chance for wear-related faults
```

But Condition itself should not become a second hidden performance-stat stack.

Actual performance consequences should come from:

```text
Integrity
+
ActiveEffects
```

consistent with the Maintenance handoff.

---

# 20. Positive effects use the same system

Positive traits are not a separate architecture.

Examples:

```text
Tuned
Efficient
Reinforced
High Output
Quiet Running
Low Wear
```

They use the same definitions, modifiers, stacking, duration, source tracking, persistence, presentation, and UI.

---

# 21. Mixed effects use the same system

Mixed examples:

```text
Overclocked
+30% output
+25% power draw
+40% wear rate
```

```text
Lightweight Rotor
+20% efficiency
-15% durability
```

The system must support these without special logic.

---

# 22. Permanent module traits

Permanent effects may represent:

- rare-item qualities,
- factory traits,
- defects,
- alien technology,
- quest rewards,
- unique salvaged characteristics.

These belong to the module/item **instance**, not just the definition.

Two instances of the same module definition may have different permanent effects.

---

# 23. Generated-item support

Leave a clean seam for item/module generation to assign permanent effects during creation.

Example:

```text
new salvaged engine
↓
roll Rare Engine Trait Pool
↓
Tuned + Worn Bearings
```

Do not implement a full loot generator here.

Only ensure the module instance can receive those effects safely.

---

# 24. Player-applied effects

Future player actions may intentionally apply effects.

Examples:

```text
Tune Engine
Reinforce Pump
Temporary Overclock
Quiet-running modification
```

This should use the same application API as all other effect sources.

Do not create a separate "upgrade modifier" stack.

---

# 25. Effect application API

Provide a common authority-owned application seam.

Conceptually:

```text
TryApplyEffect(
    moduleInstance,
    effectDefinition,
    source,
    optionalContext
)
```

The application path should:

1. validate eligibility,
2. enforce stacking policy,
3. create/update effect instance,
4. dirty/recompute affected stats,
5. update presentation,
6. replicate/persist consequential state.

---

# 26. Effect removal API

Provide a common removal seam.

Conceptually:

```text
TryRemoveEffect(
    moduleInstance,
    effectInstance,
    removalReason
)
```

Removal should:

- respect removal policy,
- update stat resolution,
- stop presentation,
- replicate/persist state,
- avoid leaving orphaned VFX/audio.

---

# 27. Stat resolution should be queryable

Modules/systems should obtain effective values through one interface.

Conceptually:

```text
IModuleStatProvider

GetBaseValue(statId)
GetEffectiveValue(statId)
GetModifiers(statId)
```

Exact interface may be shared/generalized with the Player Buff attribute system.

Prefer reuse over invention.

---

# 28. Caching / dirty recomputation

Do not recompute every effect modifier for every stat every frame if the Player Buff system already uses efficient cached resolution.

Prefer:

```text
effect added/removed/stack changed
↓
mark affected stat(s) dirty
↓
recompute cached effective values
```

Continuous systems like pumps/generators then read cached effective values.

---

# 29. UI presentation

Module inspection should show:

```text
Module Name
Integrity
Condition

Effects:
+ Tuned
- Misfiring
± Overclocked
```

Exact symbols/colors should follow project UI conventions.

Keep presentation compact.

Do not build a giant RPG tooltip system.

---

# 30. Visual effect presentation in UI

Effects with presentation profiles may also show a small icon if current UI supports it cleanly.

Examples:

```text
smoke icon
sparks icon
warning icon
```

This is optional.

The important requirement is that world-space smoke/sparks/audio can be attached generically.

---

# 31. Example definitions

## Worn Bearings

```text
Classification: Negative

Modifiers:
- module.output       AddPercent -10%
- module.power_draw   AddPercent +15%

Duration:
UntilServiced

Stacking:
RejectDuplicate

Presentation:
- subtle grinding loop
- occasional sparks
```

## Overclocked

```text
Classification: Mixed

Modifiers:
- module.output       AddPercent +30%
- module.power_draw   AddPercent +25%
- module.wear_rate    AddPercent +40%

Duration:
Permanent or Timed depending content definition

Presentation:
- brighter glow
- occasional sparks
```

## Reinforced

```text
Classification: Positive

Modifiers:
- module.resistance   AddPercent +20%

Duration:
Permanent

Presentation:
none
```

## Corroded

```text
Classification: Negative

Modifiers per stack:
- module.output       AddPercent -5%
- module.wear_rate    AddPercent +10%

Stacking:
Stack
MaxStacks: 3

Duration:
UntilRepaired / serviced according to definition
```

These are examples only.

Do not hardcode them into runtime logic.

---

# 32. Module-specific stat support

Different modules may expose different stat sets.

Examples:

```text
Engine
- thrust
- power draw
- wear rate

Pump
- flow rate
- power draw

Generator
- output
- fuel burn

Turret/Gun module
- fire rate
- accuracy
- power draw
```

The common effect system must not require every module to support every stat.

---

# 33. Presentation intensity may scale with stacks/severity

Allow a presentation profile to optionally derive intensity from:

```text
StackCount
EffectMagnitude
Integrity state
```

Examples:

```text
1 stack Corroded → light smoke
3 stacks Corroded → heavy smoke
```

Keep the presentation contract generic.

Do not bind this behavior to named effects.

---

# 34. Fault + damage interaction

A direct hit can:

```text
reduce Integrity
+
trigger fault roll
```

The selected fault is just a normal ModuleEffectDefinition.

Example:

```text
bullet impact
↓
Integrity 92% → 71%
↓
impact fault check
↓
Damaged Regulator applied
↓
-20% output
+10% power draw
+sparks
```

No separate fault-state machine.

---

# 35. Fault + maintenance interaction

Maintenance/service may remove effects tagged/policy-bound as:

```text
UntilServiced
```

Repair may remove:

```text
UntilRepaired
```

Do not automatically clear permanent traits or unrelated effect categories.

---

# 36. Persistence

Persist consequential active effect instances.

At minimum:

```text
Definition StableId
Source/provider identity if required
StackCount
RemainingDuration if Timed
instance-specific metadata if needed
```

Permanent module traits persist.

Timed effects persist their remaining simulation duration.

Do not persist derived effective values if they can be recomputed.

---

# 37. Save/load safety

On load:

1. resolve effect definitions by stable ID,
2. rebuild effect instances,
3. recompute effective stats,
4. restore presentation from current active effects.

Fail safely if an old/missing definition cannot be resolved.

Do not crash a save because one obsolete effect asset disappeared.

Log development warnings.

---

# 38. Multiplayer authority

Host/authority owns:

- effect application,
- effect removal,
- random pool rolls,
- stack changes,
- duration expiration,
- service/repair removal,
- effective consequential stat state.

Clients render:

- effect list,
- smoke,
- sparks,
- sound,
- icons,
- local presentation.

Random effect selection must be authoritative/deterministic enough for replication/save consistency.

---

# 39. No client-authored faults

Do not allow a client to:

```text
apply Tuned
remove Misfiring
roll rare trait
clear fault
```

locally.

Clients request actions; authority validates and mutates state.

---

# 40. Debug tools

Provide developer inspection per module:

```text
Base stats
Effective stats
Active effects
Source
Stack count
Remaining duration
Removal policy
Presentation profile
```

Useful debug actions:

```text
Apply Effect
Remove Effect
Add Stack
Expire Timed Effect
Roll Effect Pool
Print Effective Stat Breakdown
```

A stat breakdown is especially useful:

```text
generator.output

Base: 100
Tuned: +10%
Overclocked: +30%
Damaged Regulator: -20%
Final: ...
```

---

# 41. Performance

Avoid:

- per-frame full effect-stack recomputation,
- per-frame allocation-heavy stat queries,
- unique particle-system logic per effect type,
- broad scene searches for module presentation,
- duplicate Player Buff-style infrastructure.

Use cached/dirty stat resolution where practical.

---

# 42. Non-goals

Do not implement here:

- player perk tree,
- player leveling,
- full procedural loot generator,
- crafting/upgrading UX,
- node auto-maintenance,
- maintenance minigames,
- advanced particle editor,
- bespoke effect code for individual named faults,
- real-world-time expiration,
- per-frame fault RNG.

---

# 43. Implementation checkpoints

## EFFECTS.1 — Exact-project audit + Player Buff reuse plan

Audit:

- PlayerBuffDefinition,
- player modifier operations,
- stat resolution,
- provider/source handling,
- stacking,
- persistence,
- UI,
- module runtime stats.

Document explicitly:

```text
what can be reused directly
what should be generalized
what remains module-specific
```

Do not build a second system before this reconciliation.

Compile/test any required low-level generalization.

**STOP and report.**

---

## EFFECTS.2 — Module effect definitions + runtime instances

Implement:

- ModuleEffectDefinition,
- ModuleEffectInstance,
- classification metadata,
- duration/removal policy,
- source tracking,
- stacking policy,
- stable IDs.

No random fault generation yet.

Compile/test.

**STOP and report.**

---

## EFFECTS.3 — Generic module stat interface

Implement/reuse:

- stable stat IDs,
- base/effective query,
- modifier resolution,
- dirty/cached recomputation,
- deterministic operation order.

Create one or two trivial test module stats.

Compile/test.

**STOP and report.**

---

## EFFECTS.4 — Apply/remove/stack lifecycle

Implement authority-owned:

- TryApplyEffect,
- TryRemoveEffect,
- stacking rules,
- timed expiration,
- UntilServiced,
- UntilRepaired,
- Permanent.

Test:

- duplicate rejection,
- refresh,
- replacement,
- stacking,
- independent instances,
- max stacks.

Compile/test.

**STOP and report.**

---

## EFFECTS.5 — Reusable presentation layer

Implement/reuse:

- smoke,
- sparks,
- loop audio,
- flicker/jitter/tint seam,
- presentation anchors,
- cleanup on removal.

Test the same presentation profile on at least two different module types.

Compile/test.

**STOP and report.**

---

## EFFECTS.6 — Weighted effect pools + fault generation

Implement:

- ModuleEffectPool,
- weights,
- eligibility,
- authority-owned roll,
- direct-damage trigger seam,
- low-Integrity/operation trigger seam.

Do not roll every frame.

Compile/test.

**STOP and report.**

---

## EFFECTS.7 — Maintenance/repair integration

Integrate:

- UntilServiced removal,
- UntilRepaired removal,
- Integrity-driven fault checks,
- Condition-driven eligibility/chance where appropriate.

Verify:

- improving Condition does not magically clear permanent effects,
- repairing Integrity clears only eligible effects,
- servicing clears only eligible effects.

Compile/test.

**STOP and report.**

---

## EFFECTS.8 — Persistence + UI + multiplayer regression

Implement/verify:

- effect instance save/load,
- permanent traits,
- timed remaining duration,
- UI effect list,
- host-authoritative rolls/removal,
- client presentation,
- stat breakdown debug.

Regression-test:

- generator output,
- pump flow,
- engine performance,
- power draw,
- maintenance,
- damage,
- save/load.

**STOP and report.**

---

# 44. Acceptance tests

## Generic effects

1. Positive effect modifies a module stat.
2. Negative effect modifies a module stat.
3. Mixed effect applies both positive and negative modifiers.
4. No named-effect gameplay branch is required.
5. Same effect architecture works across multiple module types.

## Modifier operations

6. AddFlat works.
7. AddPercent works.
8. Multiply works.
9. Override works if supported by Player Buff precedent.
10. Resolution order is deterministic.

## Stacking

11. RejectDuplicate works.
12. RefreshDuration works.
13. ReplaceExisting works.
14. Stack works.
15. IndependentInstances works.
16. MaxStacks is enforced.

## Duration/removal

17. Permanent persists.
18. Timed expires on simulation time.
19. UntilServiced survives until service.
20. UntilRepaired survives until repair.
21. Permanent trait is not cleared by ordinary service.
22. Quit/reload does not consume real-world timed duration.

## Fault generation

23. Direct damage can trigger an effect-pool roll.
24. Low Integrity can influence eligibility/chance.
25. Condition can influence wear-fault eligibility/chance.
26. Fault RNG is not evaluated every frame.
27. Weighted pools are data-driven.

## Positive/rare traits

28. Rare positive trait can be assigned to one module instance.
29. Another instance of same definition can lack it.
30. Mixed permanent trait persists.

## Presentation

31. Effect can start smoke.
32. Effect removal stops smoke cleanly.
33. Effect can start sparks.
34. Same sparks profile works on two module types.
35. Optional loop audio starts/stops with effect.
36. Missing presentation anchor does not crash module.

## Persistence

37. Active effect IDs persist.
38. Stack count persists.
39. Timed remaining duration persists.
40. Permanent traits persist.
41. Effective stats rebuild correctly after load.
42. Missing obsolete definition fails safely.

## Multiplayer

43. Host owns effect application.
44. Host owns random rolls.
45. Client cannot clear/apply effects authoritatively.
46. Client presentation reflects replicated effect state.

## Player Buff alignment

47. Existing Player Buff infrastructure is reused/generalized where practical.
48. No unnecessary duplicate modifier framework exists.
49. Existing player buffs remain functional after any shared low-level refactor.

---

# 45. Forward contract: future module loot

A future item-generation system may:

```text
create module instance
↓
roll one or more effect pools
↓
persist resulting permanent effects
```

This system should already support that.

---

# 46. Forward contract: future player perks

Future player perks may modify:

```text
fault chance
repair-clears-fault chance
effect duration
effect application strength
service outcome
```

Do not implement those now.

Leave clean authority-side hooks.

---

# 47. Forward contract: future upgrades

Player-applied permanent upgrades should use the same effect application path.

Example:

```text
Reinforced Rotor Upgrade
↓
apply permanent ReinforcedRotor effect
```

Do not add a second module-upgrade modifier stack later.

---

# 48. Final principle

This pass should make these statements true:

> Module effects are generic stat modifiers, not hardcoded named behaviors.

> Faults, buffs, rare traits, permanent defects, and mixed tradeoffs are all the same underlying system.

> The existing Player Buff code is the architectural precedent and should be reused or generalized instead of duplicated.

> Smoke, sparks, sound, flicker, and similar consequences are reusable presentation profiles, not module-specific scripts.

> Integrity and Condition determine when effects are likely to appear or be removed, but the effect itself remains data-driven.

> A module can be excellent, terrible, weird, damaged, tuned, cursed, or all of those at once without adding a new code path for each adjective.

---

**End of Module Effects / Faults / Buffs System handoff.**
