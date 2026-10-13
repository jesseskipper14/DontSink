# DON'T SINK — Boat Module Maintenance / Condition / Integrity Foundation
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Maintainable module condition, max-condition quality, integrity/performance, operating wear, submersion deterioration, maintenance/parts interactions, persistence, and seams for future fault/buff and player-role systems  
**Status:** Ready for implementation  
**Out of scope:** Full module fault/buff system, role/perk implementation, detailed economy integration, automatic boat maintenance, node-generator maintenance behavior beyond shared contracts

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current:
   - installed module hierarchy,
   - engine/generator/pump/turret/storage implementations,
   - module persistence,
   - module power/output APIs,
   - boat electrical reservoir/system,
   - flooding/submersion detection,
   - item/inventory model,
   - existing drill/tool hold-interaction pattern,
   - player attribute/buff architecture,
   - save/load,
   - multiplayer authority seams.
5. Reuse existing stable module identities.
6. Keep Condition and Integrity as distinct concepts.
7. Do not implement the future module fault/buff framework in this pass.
8. Keep all wear, repair, maintenance, and consumable use host-authoritative.
9. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core model

Maintainable modules have two primary durability axes:

```text
Condition
↓
maintenance state / service reserve

Integrity
↓
actual physical/mechanical soundness
```

These do different jobs.

Condition does **not** directly reduce performance.

Low Condition causes Integrity drain.

Integrity is the primary gameplay effector:

- performance,
- disabled state,
- future fault probability,
- physical damage consequences.

This is deliberate.

---

# 2. Why two axes

The system must support all of these:

```text
Well-maintained, undamaged engine
→ high Condition
→ high Integrity
→ excellent performance

Poorly maintained engine
→ low Condition
→ Integrity begins draining
→ requires frequent repair if neglected

Bullet-damaged engine
→ Condition may still be high
→ Integrity drops immediately
→ performance/fault consequences

Cheap old engine
→ low MaxCondition
→ needs maintenance frequently
→ can still perform perfectly when healthy

Exceptional engine
→ extremely high MaxCondition
→ very long maintenance interval
```

Condition is **maintenance burden**.

Integrity is **actual health/capability**.

---

# 3. Maintainability is data-driven

Do not assume every installed module wears at the same rate.

The module/item definition should decide whether it participates and how.

Conceptually:

```text
ModuleMaintainabilityDefinition
- IsMaintainable
- DefaultMaxCondition
- OperatingConditionDrainRate
- SubmergedConditionDrainRate / Multiplier
- LowConditionIntegrityDrainRates
- IntegrityPerformanceCurve
- DisabledIntegrityThreshold
- MaintenanceAmountPerKit
- RepairAmountPerPartsKit
```

Exact ownership may live directly on module definitions if that fits current architecture better.

Passive storage may be maintainable with zero operational wear, or not maintainable at all.

Machinery such as:

- Engine
- Generator
- Pump
- Turret
- future powered/specialty modules

will commonly participate.

---

# 4. Condition

Condition represents current maintenance state.

Conceptually:

```text
CurrentCondition
MaxCondition
```

Normalized maintenance fraction:

```text
ConditionFraction =
CurrentCondition / MaxCondition
```

Condition:

- drains from module use/load,
- drains rapidly from submersion,
- is restored by Maintenance Kits,
- does not directly alter output,
- does not directly disable the module,
- does not directly create faults,
- drives Integrity decay when low.

---

# 5. Max Condition

Each module instance has a persistent **MaxCondition**.

Definition provides a default, but the installed/item instance owns its actual value.

Example:

```text
Pristine engine
MaxCondition = 100

Used engine
MaxCondition = 75

Rustbucket
MaxCondition = 30

Exceptional engine
MaxCondition = 140

Alien engine
MaxCondition = 10000
```

This creates a persistent quality axis without requiring separate module prefabs/definitions.

A low-MaxCondition engine is not necessarily weak.

It simply requires much more frequent maintenance.

---

# 6. MaxCondition is an economic-quality seam

Future economy can value modules using:

```text
Module type
Integrity
CurrentCondition
MaxCondition
Permanent modifiers/buffs later
```

A cheap engine with MaxCondition 30 may cost a fraction of a pristine equivalent because it will constantly demand maintenance.

Do not implement full buy/sell valuation in this pass.

Preserve the data needed for it.

---

# 7. Condition drain from operation

Normal wear drains Condition while the module is actively operating.

Conceptually:

```text
ConditionDrain =
OperatingConditionDrainRate
× LoadFactor
× OperatingTime
```

LoadFactor should be used if the current module already exposes a meaningful load/throttle/output fraction.

If not, V1 may use:

```text
Active
vs
Inactive
```

with one configured drain rate.

Do not invent complicated load simulation solely for maintenance.

---

# 8. No unloaded-boat wear requirement

The boat is expected to remain loaded during normal play.

If the boat ever becomes unloaded, this pass does **not** need to analytically calculate maintenance wear while unloaded.

Do not add background boat wear simulation merely for theoretical completeness.

---

# 9. Submersion condition loss

Submersion rapidly damages Condition.

Unlike normal wear, submerged deterioration does **not** require the module to be operating.

Conceptually:

```text
if submerged:
    Condition -= SubmergedConditionDrainRate × dt
```

or:

```text
base condition drain × large submerged multiplier
```

Use the cleanest current water/submersion authority.

Do not create corrosion simulation.

The abstraction is simply:

> Being underwater is extremely bad for maintainable machinery.

---

# 10. Condition thresholds drive Integrity drain

Use Condition percentage relative to MaxCondition.

Initial bands:

```text
60–100%
→ no maintenance-driven Integrity drain

30–60%
→ moderate Integrity drain

0–30%
→ severe Integrity drain
```

Exact percentages/rates should be serialized/tunable.

These thresholds do **not** directly change performance.

They only change how fast Integrity deteriorates.

Conceptually:

```text
ConditionFraction >= 0.60
→ MaintenanceIntegrityDrain = 0

0.30 <= ConditionFraction < 0.60
→ MaintenanceIntegrityDrain = Moderate

ConditionFraction < 0.30
→ MaintenanceIntegrityDrain = Severe
```

---

# 11. Integrity

Integrity represents actual mechanical/physical soundness.

Conceptually:

```text
CurrentIntegrity
MaxIntegrity
```

V1 can use a normalized 0–100 style scale if that aligns with current module architecture.

Integrity is reduced by:

- future bullet/impact damage,
- future collision/module damage,
- low-condition deterioration,
- future faults/effects,
- other explicit damage sources.

Integrity is restored by Parts Kits.

---

# 12. Integrity controls performance

Condition must not appear in the performance formula.

Conceptually:

```text
EffectivePerformance =
BasePerformance
× IntegrityPerformanceMultiplier
× FutureFaultModifier
× other existing modifiers
```

No Condition multiplier.

---

# 13. Integrity performance curve

Locked behavior:

```text
80–100% Integrity
→ perfect performance

30–80% Integrity
→ performance degrades along a configurable curve

<30% Integrity
→ disabled
```

Use module-defined/tunable performance curves where practical.

Examples:

```text
Engine:
lower thrust / acceleration

Pump:
lower pumping rate

Generator:
lower output

Turret:
future fire-rate/traverse/accuracy consequences as appropriate
```

Do not force one hardcoded curve onto every module if the current data architecture can support per-module curves.

---

# 14. Disabled threshold

Initial disabled threshold:

```text
Integrity < 30%
```

At/above threshold:

- module may operate according to performance curve.

Below threshold:

- module is disabled/nonfunctional.

Do not destroy/remove the module GameObject.

Do not delete the installed module from the boat.

Repair can bring it back.

---

# 15. No catastrophic module destruction state

This pass does not permanently destroy modules.

The catastrophic part of a failed module should come from **what the module was doing or supporting**, not from erasing the object.

Examples later:

- pump disabled while flooding,
- generator disabled during power demand,
- engine disabled during escape,
- turret disabled during combat.

The consequence is systemic.

The module remains installed until repaired/replaced.

---

# 16. Direct damage can bypass Condition

A bullet or future impact can immediately reduce Integrity.

Example:

```text
Condition: 100%
Integrity: 100%

bullet strike
↓
Integrity: 68%
Condition: still 100%
```

This is correct.

Condition should not absorb or gate physical damage.

The later Hull Damage / ballistic system may call a module-damage seam directly.

---

# 17. Future faults are driven by Integrity

Do not implement the full fault system here.

However, expose a clean seam so future faults can be triggered by:

- sudden physical damage,
- low Integrity,
- operation while damaged,
- future events.

Condition does not directly create faults.

Relationship:

```text
Low Condition
↓
Integrity drains
↓
lower Integrity
↓
future fault chance / fault effects
```

A bullet can skip that chain and cause a fault directly through sudden Integrity damage.

---

# 18. Future fault system is really module buffs/debuffs

Important forward contract:

The future "fault" system should be implemented as a generalized module modifier/buff system capable of both negative and positive effects.

Conceptually:

```text
ModuleEffect / ModuleBuff
- StableId
- duration or permanent
- source
- stat modifiers
- behavior hooks
- positive / negative / neutral semantic tag
```

Examples:

Negative:
- Jammed
- Misfiring
- Reduced output
- Overheating
- Tripped

Positive:
- Tuned
- Reinforced
- Efficient
- Overclocked
- Rare-item permanent bonus

This opens the door to:

- player-applied upgrades,
- rare item variants,
- permanent buffs/nerfs,
- temporary faults.

Do **not** build that system in this pass.

Leave the seam.

---

# 19. Maintenance Kit

A **Maintenance Kit** is a consumable.

Using one on a maintainable module restores a **flat amount of Condition**.

Conceptually:

```text
CurrentCondition += MaintenanceRestoreAmount
clamped to MaxCondition
```

One kit does not automatically restore to full.

This keeps low-MaxCondition modules cheap to fill but frequent to maintain.

---

# 20. Parts Kit

A **Parts Kit** is a consumable.

Using one restores a **flat amount of Integrity**.

Conceptually:

```text
CurrentIntegrity += RepairRestoreAmount
clamped to MaxIntegrity
```

One kit does not automatically restore the module to pristine condition.

---

# 21. Maintenance vs repair

Keep the interactions separate:

```text
Maintenance Kit
→ Condition

Parts Kit
→ Integrity
```

No Parts Kit required for normal maintenance.

No Maintenance Kit required merely to restore Integrity.

However, repairing a badly maintained module without servicing it may be wasteful because low Condition will continue draining Integrity.

That is intentional.

---

# 22. Restoring disabled modules

There is no special "revive" action.

A disabled module returns to operation naturally when:

```text
Integrity rises back to >= disabled threshold
```

If Condition remains low:

- it may immediately continue losing Integrity.

So practical recovery from a neglected disabled module may involve:

```text
Parts Kit
+
Maintenance Kit
```

but the system does not require a separate resurrection mechanic.

---

# 23. Tool interaction

Use the project's existing holdable/tool interaction philosophy, especially the drill, as the primary reference.

Possible V1 pattern:

```text
Maintenance Tool / wrench-like applicator
+ Maintenance Kit consumable
→ hold to service Condition

Repair Tool / same generic tool if cleaner
+ Parts Kit consumable
→ hold to restore Integrity
```

The exact number of reusable applicator tools should be decided after auditing the current tool/item architecture.

Do not invent unnecessary tool categories if one reusable mechanic tool can safely perform both operations with different consumables.

---

# 24. Flat restoration now, skill/perk modifiers later

Restoration amounts are flat base values.

However, architecture must allow future player skill/perk modifiers.

Examples:

```text
Perk A
+50% Condition restored per Maintenance Kit

Perk B
+50% Integrity restored per Parts Kit
```

Future role/leveling systems may also affect:

- repair speed,
- maintenance speed,
- item efficiency,
- fault handling,
- chance to preserve consumables,
- max recoverable integrity,
- diagnostics.

Do not implement roles/perks now.

Expose a clean modifier/request seam rather than burying restoration constants deep inside module code.

---

# 25. Suggested service request seam

Conceptually:

```text
ModuleServiceRequest
- ModuleId
- ServiceType
- BaseRestoreAmount
- ActorId / requester
- Tool context
- Consumable context
```

Then future player stats/perks can modify the outcome before authoritative application.

Do not require this exact class shape if existing action/modifier architecture already provides a better seam.

---

# 26. Visual/audio feedback

Module state should expose presentation-friendly states.

Examples:

```text
Healthy
Worn
Damaged
Critical
Disabled
```

These may be derived from Integrity/Condition.

Future/optional presentation:

- smoke,
- sparks,
- sputtering audio,
- slower animation,
- warning light,
- changed interaction text.

V1 only needs enough presentation feedback to clearly communicate:

- low Condition,
- degraded Integrity,
- disabled state,
- servicing/repair progress.

Do not require elaborate VFX for every module.

---

# 27. Node-generator relationship

Boat modules do **not** auto-maintain.

Future settlement/node generators may use the same underlying Condition/Integrity concepts, but settlements can automatically maintain them when sufficient resources exist.

That behavior belongs to a future Node Maintenance/Fault pass.

Do not add automatic maintenance to boat modules.

---

# 28. Persistence

Persist authoritative module state:

```text
Stable Module Instance ID
CurrentCondition
MaxCondition
CurrentIntegrity
MaxIntegrity if instance-specific
active future ModuleBuff/Fault IDs when that system exists
```

Derived values should not be redundantly serialized if they can be recomputed safely:

- integrity performance multiplier,
- current output,
- warning band.

Old saves need deterministic defaults.

---

# 29. Instance quality

MaxCondition should live on the module/item instance.

A module definition provides:

```text
DefaultMaxCondition
```

but an individual item may override it.

This supports future generated qualities/rarities without duplicating definitions.

Example:

```text
Engine Model X definition:
DefaultMaxCondition = 100

Instance A:
MaxCondition = 30

Instance B:
MaxCondition = 100

Instance C:
MaxCondition = 145
```

Do not infer quality from prefab name.

---

# 30. Future item rarity/permanent modifiers

The module instance model should not block future permanent modifiers.

A rare item might later carry:

```text
+20% pump output
-15% power draw
+40 MaxCondition
-10% Integrity
```

or permanent negative traits.

These should eventually use the generalized ModuleBuff/ModuleEffect architecture.

Do not implement rarity generation now.

---

# 31. Multiplayer authority

Host/authority owns:

- Condition drain,
- Integrity drain,
- direct damage application,
- maintenance-kit consumption,
- parts-kit consumption,
- restore amounts,
- disabled state,
- future fault/effect rolls.

Clients render:

- module animation,
- sparks/smoke/audio,
- UI condition/integrity bars,
- service progress presentation.

Do not trust client-reported repair totals.

---

# 32. Performance requirements

Avoid:

- per-frame expensive scans across all modules,
- repeated inventory lookup every Update,
- rebuilding module state from scene hierarchy,
- random fault rolls every frame,
- background wear calculations for unloaded boats.

Condition wear can update at a modest simulation cadence or through existing module operating ticks.

Submersion deterioration may use the current water-contact cadence.

---

# 33. Debug visibility

Provide developer inspection for each maintainable module:

```text
ModuleId
Maintainable?
CurrentCondition / MaxCondition
ConditionFraction
OperatingConditionDrainRate
Submerged?
CurrentIntegrity / MaxIntegrity
IntegrityPerformanceMultiplier
Disabled?
Current output/effective performance
```

Also expose active condition band:

```text
Healthy-maintenance band
Moderate-maintenance drain
Severe-maintenance drain
```

No player-facing engineering dashboard required.

---

# 34. Non-goals

Do not implement in this pass:

- full module fault system
- generalized ModuleBuff implementation
- role/level/perk system
- player mechanic skill
- automatic boat maintenance
- complex spare-part quality tiers
- oil/lubricant consumables
- corrosion simulation
- permanent module destruction
- node-generator auto-maintenance
- full trade-in valuation
- rarity generation
- maintenance while boat unloaded
- per-module bespoke repair minigames

---

# 35. Implementation checkpoints

## MODMAINT.1 — Audit + maintainability data model

Audit:

- installed module base classes,
- engine/generator/pump/turret/storage,
- persistence,
- module output APIs,
- water/submersion state,
- drill/tool interactions,
- inventory consumables,
- current player-buff seams.

Implement/define:

- maintainability configuration,
- CurrentCondition,
- MaxCondition,
- CurrentIntegrity,
- default migration values,
- disabled threshold,
- integrity-performance curve seam.

Compile/test.

**STOP and report.**

---

## MODMAINT.2 — Operating wear + submersion

Implement:

- operating Condition drain,
- optional load-scaled wear where current module data supports it cleanly,
- rapid submerged Condition drain,
- no unloaded-boat analytical wear.

Verify:

- idle dry module does not wear,
- active dry module wears,
- submerged inactive module still deteriorates,
- values remain tunable.

Compile/test.

**STOP and report.**

---

## MODMAINT.3 — Condition → Integrity decay

Implement percentage bands:

```text
60–100% Condition:
no maintenance-driven Integrity drain

30–60%:
moderate drain

0–30%:
severe drain
```

Keep rates configurable.

Verify Condition itself does not alter output.

Compile/test.

**STOP and report.**

---

## MODMAINT.4 — Integrity → performance / disabled

Implement:

```text
80–100% Integrity:
100% performance

30–80%:
module-defined degradation curve

<30%:
disabled
```

Integrate with at least:

- Engine
- Pump
- Generator

and Turret if current architecture makes it clean.

Do not modify structural-hull Integrity semantics.

Compile/test.

**STOP and report.**

---

## MODMAINT.5 — Maintenance Kit + Parts Kit

Implement:

- Maintenance Kit consumable,
- Parts Kit consumable,
- hold-to-service interaction using existing tool conventions,
- flat Condition restoration,
- flat Integrity restoration,
- clamp to maximums,
- authoritative item consumption,
- service-request seam for future perk modifiers.

No role/perk implementation.

Compile/test.

**STOP and report.**

---

## MODMAINT.6 — Instance MaxCondition + persistence

Verify:

- same module definition can create different MaxCondition instances,
- MaxCondition persists,
- CurrentCondition persists,
- Integrity persists,
- save/load preserves disabled/performance state,
- old saves receive deterministic defaults.

Test at least:

```text
MaxCondition 30
MaxCondition 100
MaxCondition 10000
```

Compile/test.

**STOP and report.**

---

## MODMAINT.7 — Direct-damage seam + future buff/fault contract

Add the smallest generic module-damage input required so future bullets/collisions can reduce Integrity.

Do not implement the full fault system.

Document/expose the future ModuleBuff/ModuleEffect integration point.

Ensure:

```text
physical damage
→ Integrity
```

can bypass Condition cleanly.

Compile/test.

**STOP and report.**

---

# 36. Acceptance tests

## Condition

1. Maintainable module has CurrentCondition and MaxCondition.
2. MaxCondition is instance-specific.
3. Operating module drains Condition.
4. Idle dry module does not drain normal operating Condition.
5. Submerged module drains Condition rapidly even while inactive.
6. Maintenance Kit restores a flat Condition amount.
7. Maintenance cannot exceed MaxCondition.

## MaxCondition quality

8. MaxCondition 30 module fills to 30 and no higher.
9. MaxCondition 100 module fills to 100.
10. MaxCondition 10000 module behaves correctly without overflow/assumption bugs.
11. Same module definition supports all three instances.

## Condition → Integrity

12. Above 60% Condition, no maintenance-driven Integrity loss.
13. 30–60% Condition produces moderate Integrity drain.
14. Below 30% Condition produces severe Integrity drain.
15. Condition does not directly alter engine thrust/pump rate/generator output.

## Integrity

16. 80–100% Integrity produces full performance.
17. 30–80% follows configured degradation curve.
18. Below 30% module disables.
19. Module remains installed when disabled.
20. Parts Kit restores flat Integrity.
21. Repairing above disabled threshold allows operation again.
22. Bad Condition can immediately begin degrading the repaired module again.

## Direct damage

23. Direct module damage reduces Integrity without requiring Condition loss.
24. High Condition does not shield the module from direct damage.
25. Future bullet/collision producer can use a generic module-damage seam.

## Service interactions

26. Maintenance Kit affects only Condition.
27. Parts Kit affects only Integrity.
28. Service amount is flat before future modifiers.
29. Consumable is spent authoritatively.
30. Hold-to-service follows existing interaction/tool patterns.

## Performance

31. Engine performance responds to Integrity only.
32. Pump performance responds to Integrity only.
33. Generator output responds to Integrity only.
34. Condition itself is absent from performance formula.

## Persistence

35. CurrentCondition survives save/load.
36. MaxCondition survives save/load.
37. Integrity survives save/load.
38. Disabled state reconstructs correctly from Integrity.
39. Old saves receive deterministic defaults.

## Multiplayer architecture

40. Client cannot authoritatively add Condition or Integrity.
41. Consumable use belongs to host/authority.
42. Presentation remains client-derived.

---

# 37. Forward contracts

## Future module fault / buff system

Future pass should implement generalized positive/negative module effects.

Examples:

```text
Jammed
Misfiring
Overheating
Tuned
Efficient
Reinforced
Rare permanent bonus
Rare permanent defect
```

Integrity/damage events can trigger effects.

The system should not be restricted to "faults" semantically.

## Future roles/perks

Future player progression may modify service requests:

```text
+50% Condition restored
+50% Integrity restored
faster service time
reduced consumable use
better diagnostics
```

This pass exposes the seam but does not implement progression.

## Future economy

Future valuation can account for:

```text
module model
Integrity
CurrentCondition
MaxCondition
permanent modifiers
```

Low-MaxCondition used machinery can therefore be cheap but maintenance-heavy.

## Future node maintenance

Node generators may reuse the same semantics but allow settlement auto-maintenance when local resources/economics permit.

Boat modules remain manual unless explicitly changed later.

---

# 38. Final principle

The system should make these statements true:

> A perfectly maintained engine with high Integrity runs perfectly.

> A poorly maintained engine does not immediately run worse; instead it begins damaging itself and becomes expensive to keep alive.

> A bullet can damage an engine instantly even if it was serviced five seconds ago.

> A cheap old engine with MaxCondition 30 can be a great bargain if the player is willing to constantly maintain it.

> A rare or alien engine can have absurdly high MaxCondition and therefore require service extremely rarely.

> Performance depends on Integrity, not on an arbitrary second health bar pretending to be the same thing.

Condition creates the maintenance problem.

Integrity creates the mechanical consequence.

Future buffs/faults create the weird stories.

---

**End of Boat Module Maintenance / Condition / Integrity Foundation handoff.**
