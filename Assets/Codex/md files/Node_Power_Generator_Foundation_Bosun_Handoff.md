# DON'T SINK — Node Power / Generator Foundation
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Persistent node-level power providers/consumers, quantitative supply/demand, abstract generator fuel reserve/supply, deterministic load shedding, physical generator binding, unloaded analytical simulation, and semantic power-state exposure  
**Status:** Ready for implementation  
**Out of scope:** Full trade simulation, physical town fuel-can inventory, generator wear/maintenance behavior, settlement-stat penalties, electrical grids/wires, boat power rewrites

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current `MapNodeState` / persistent node-state model, NodeScene bootstrap/binding path, settlement/node generation manifests, node archetype/affinity state, world-time authority, lighting/time-of-day systems, save/load serialization, resource/trade-status state, physical interactable conventions, and multiplayer authority seams.
5. Reuse existing persistent node IDs and stable runtime binding.
6. Do not hide consequential state exclusively inside scene GameObjects.
7. Keep the system host-authoritative/multiplayer-safe from the beginning.
8. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core goal

Node power should be a real persistent settlement system, not a scripted light switch.

A node may contain any number of power providers and consumers:

```text
NODE POWER SYSTEM
│
├─ Providers[]
│   ├─ Main town generator
│   ├─ Additional generator later
│   └─ Any future provider
│
├─ Shared node fuel state
│
└─ Consumers[]
    ├─ building lighting groups
    ├─ street-light groups
    ├─ civic services
    ├─ workshops/machinery
    └─ future powered systems
```

Typical settlements will initially have **one primary generator**, but the architecture must not impose a one-generator limit.

No electrical grid topology, wire routing, poles, breakers, or circuit simulation in this pass.

---

# 2. Quantitative power

Power is quantitative.

Use abstract gameplay units, not literal watts unless the project already has a suitable unit convention.

Conceptually:

```text
Available Generation: 100
Requested Demand:      125
```

The system allocates available supply to consumers according to deterministic priority rules.

This allows a damaged/reduced-output generator to restore parts of a town organically as output increases.

Example:

```text
Damaged generator output: 55
Town requested demand:    110

↓ generator repaired

Available output: 100

↓ allocation recalculates immediately

more buildings/lights/services become powered
```

That direct visual response is a core design goal.

---

# 3. One shared node power bus

V1 uses one abstract shared electrical bus per node.

All registered providers contribute to the same node supply.

All registered consumers request from the same node supply.

No neighborhood grids or separate circuits yet.

Future local grids must be able to layer above this architecture without invalidating provider/consumer contracts.

---

# 4. Provider abstraction

Do not hardcode the power system around one `TownGenerator`.

Create a provider-facing contract/interface/model conceptually equivalent to:

```text
INodePowerProvider
- StableProviderId
- IsOperational
- RatedOutput
- AvailableOutput
- FuelDemand / FuelBurnRate
```

Exact shape should follow current code conventions.

The Node Power system must be able to aggregate:

```text
TotalAvailableGeneration =
Σ provider.AvailableOutput
```

Whether there are 1 or 1000 providers is not a special case.

Do not optimize around a hard one-provider assumption.

---

# 5. Initial physical generator

Most generated nodes initially receive one primary town generator/power-house representation.

The generator is a real physical settlement object/location, not invisible spreadsheet state.

It should eventually support:

- inspection,
- repairs,
- maintenance,
- quest hooks,
- visual/audio operating state.

For this foundation, only simple inspection/state binding is required.

Persistent authority lives in node state; the scene object binds to it.

---

# 6. Physical generator ↔ persistent state

The physical NodeScene generator must not own the only copy of meaningful state.

Conceptually:

```text
Persistent NodePowerState
        ↕
NodeScene physical generator representation
```

Scene load:

```text
NodeScene generator
→ binds by stable provider ID / generated settlement identity
→ renders/interacts from persistent state
```

Scene unload does not stop the node's economic/power state from existing.

---

# 7. Consumer abstraction

Create a consumer-facing contract/model conceptually equivalent to:

```text
INodePowerConsumer
- StableConsumerId
- RequestedDemand
- Priority
- OptionalLoadOrder
- WantsPower
- IsPowered
```

Exact names should match project conventions.

Consumers are binary in V1:

```text
Powered
or
Unpowered
```

No partial-voltage lamp brightness or fractional machine performance in this pass.

The bus is quantitative; each individual consumer allocation is binary.

---

# 8. Consumer grouping

Prefer **logical consumer groups** where that is more efficient and visually coherent.

Examples:

```text
Harbor Safety Lights
Town Center Lighting
Workshop Building
Street Lights - East
Street Lights - West
```

Do not require every bulb to be a separately allocated load unless there is a clear gameplay reason.

A consumer group may fan its powered state out to several child lights/objects.

This reduces allocation churn and avoids strange patterns like every third lamp surviving a shortage.

Individual consumers remain supported where appropriate.

---

# 9. Dynamic demand

A consumer may request zero demand when not actively needed.

Example:

```text
Street-light group:
Day:   RequestedDemand = 0
Night: RequestedDemand = 12
```

Time-of-day changes should dirty/recalculate node allocation through existing time/lighting hooks rather than polling every frame if possible.

The same pattern can later support shops only while open, machinery only while running, quest machinery, and seasonal loads.

---

# 10. Deterministic priority allocation

Consumers need deterministic load-shedding priority.

Initial priority tiers may be:

```text
Critical
High
Normal
Low
Decorative
```

Exact enum names/order may follow project style.

Allocation order:

```text
Priority
↓
explicit LoadOrder if provided
↓
StableConsumerId
```

Never depend on scene hierarchy order, registration timing, dictionary iteration order, or nondeterministic Unity object enumeration.

This matters for save reproducibility and multiplayer.

---

# 11. Stable allocation / no churn

Power allocation should not arbitrarily alternate between same-priority consumers when supply is unchanged.

Once inputs are unchanged:

```text
same providers
same output
same consumers
same demand
same priorities
```

the exact same consumers must remain powered.

Recalculate only when meaningful inputs change.

---

# 12. Semantic node power state

Expose a derived semantic node state:

```text
Normal
Strained
Blackout
```

Suggested meaning:

```text
Normal:
all requesting consumers that should be served are powered

Strained:
generation exists, but one or more requesting consumers are shed

Blackout:
no meaningful generation / no eligible requesting consumers can be powered
```

Exact thresholds should remain simple and deterministic.

Do not apply Prosperity/Stability/Security modifiers in this pass.

Other systems must be able to query the semantic state later.

---

# 13. Persistent abstract fuel reserve

Town generator fuel is abstract at node scale.

Do not model it as physical jerry cans or individual inventory items.

Persistent node fuel state should include conceptually:

```text
FuelReserve
FuelCapacity
InboundFuelSupplyRate
LocalFuelProductionRate
LastSimulationTime
```

Exact fields may be split differently if a cleaner design emerges during audit.

The purpose is to represent:

> Does this settlement have enough fuel supply to keep its generators operating?

without requiring physical cargo simulation.

---

# 14. Shared fuel reserve

All fuel-burning node power providers initially draw from the same node-level fuel reserve.

Do not require individual generator tanks in V1.

Conceptually:

```text
NodeFuelReserve
↓
all active node generators consume from it
```

If future provider-specific fuels ever become necessary, extend the model then.

Do not build multiple fuel classes now.

---

# 15. Fuel supply comes from abstract node economics

The Node Power system does **not** simulate settlements purchasing fuel.

It also does not require an individually simulated trade ship to physically arrive.

Instead, node/economic state supplies an abstract replenishment rate.

Conceptually:

```text
Trade connectivity/status
+ neighboring economic conditions later
+ local production
↓
InboundFuelSupplyRate
```

Node Power consumes that rate.

This deliberately keeps ownership separated:

```text
trade/node simulation
→ tells Node Power how much fuel supply exists

Node Power
→ determines reserve/output/blackout consequences
```

The full trade simulation is future work.

---

# 16. Local fuel production

A node may generate some or all of its own fuel.

Do not create a separate “fuel-free generator” abstraction in V1.

Instead:

```text
LocalFuelProductionRate
```

adds to the same abstract node fuel supply.

Thus a settlement can be fully import dependent, partially self-sufficient, or effectively fuel independent because local production exceeds burn.

That covers the desired exception without creating a second generator category.

---

# 17. Fuel reserve simulation

Fuel reserve evolves analytically:

```text
Fuel gained =
(InboundFuelSupplyRate + LocalFuelProductionRate)
× elapsed world time

Fuel consumed =
sum(active generator burn rates)
× elapsed world time
```

Then clamp:

```text
0 <= FuelReserve <= FuelCapacity
```

Do not require scene GameObjects to be loaded.

Do not run hidden generator Update loops for unloaded nodes.

---

# 18. Unloaded-node analytical simulation

Node power/fuel must continue to make sense while the player is away.

When persistent state is queried/loaded:

```text
elapsed = current world time - LastSimulationTime
```

Advance the abstract reserve analytically.

If necessary, account for meaningful state thresholds inside that interval rather than merely applying an obviously incorrect single end-state formula.

Keep the model simple and deterministic.

Do not instantiate NodeScene objects to simulate power.

---

# 19. Low-fuel output reduction

Generators begin losing available output when node fuel reserve falls below roughly **40% of capacity**.

Initial rule:

```text
Fuel fraction >= 0.40
→ full fuel-based output multiplier = 1.0

Fuel fraction between 0.40 and 0.0
→ output multiplier ramps linearly from 1.0 to 0.0

Fuel fraction == 0
→ fuel-based output multiplier = 0.0
```

Conceptually:

```text
FuelOutputMultiplier =
Clamp01(FuelFraction / 0.40)
```

Thus:

```text
40% fuel → 100% fuel-supported output
20% fuel →  50% fuel-supported output
 0% fuel →   0% fuel-supported output
```

This is a gameplay abstraction representing low reserve/supply pressure, not literal generator tank physics.

Keep the low-fuel threshold serialized/tunable rather than permanently hardcoded if practical.

---

# 20. Provider available output

Provider output should conceptually support multiple independent multipliers/seams:

```text
AvailableOutput =
RatedOutput
× OperationalMultiplier
× ConditionMultiplier (future)
× FuelOutputMultiplier
× Event/other modifiers (future)
```

This pass should not implement generator wear/condition degradation.

It only needs to leave a clean seam.

Future Module Maintenance/Failure work should decide how node-generator condition behaves.

---

# 21. Generator maintenance is deferred

Node generators should eventually behave similarly to maintainable boat modules.

Likely differences:

- slower wear,
- settlement auto-maintenance when sufficient resources exist,
- local resource/economic state influences upkeep.

Do not implement that behavior here.

Node Power only needs to tolerate:

```text
Online
Offline
Reduced output
```

from future condition/maintenance logic.

---

# 22. Immediate allocation recalculation

Whenever meaningful provider/consumer state changes, recalculate immediately.

Examples:

- generator repaired,
- provider enabled/disabled,
- provider output changes,
- fuel crosses an output threshold,
- consumer starts/stops requesting power,
- night begins and street lights request demand.

This is essential for direct visual feedback.

Example quest payoff:

```text
Player repairs generator at night
↓
provider output increases
↓
NodePowerSystem recalculates
↓
whole consumer groups receive power
↓
half the town visibly lights back up
```

No bespoke quest-script light toggles should be needed.

---

# 23. Simple generator interaction

Provide a small physical interaction/inspection experience.

Example:

```text
Main Generator

Status: Damaged / Online / Offline
Available Output: 55 / 100
Settlement Demand: 87
Fuel Reserve: Low
Power State: Strained
```

Exact presentation should follow current interaction/UI conventions.

No direct player refueling in this pass.

No generator-management control panel required.

---

# 24. Starting node reliability

The starting settlement should begin in a stable, understandable condition:

- functioning primary generator,
- healthy enough output,
- adequate fuel reserve,
- no random opening blackout.

Other generated nodes may later start strained or blacked out according to world/node generation.

---

# 25. Settlement generation integration

Power-provider identity/configuration should be generated as part of persistent settlement/node content rather than invented only when NodeScene loads.

Conceptually a generated settlement can persist:

```text
PowerProviderManifest[]
InitialFuelCapacity
InitialFuelReserve
Initial/local fuel supply parameters
```

The NodeScene binds physical representation to this persistent semantic content.

Do not let scene load order decide what power sources a settlement “has.”

---

# 26. Persistence ownership

Node power belongs with persistent node/world state.

Conceptually:

```text
MapNodeState
└─ NodePowerState
   ├─ ProviderStates[]
   ├─ FuelState
   ├─ allocation/derived state as needed
   └─ LastSimulationTime
```

Persist authoritative inputs.

Prefer deriving/rebuilding transient allocation state on load rather than serializing unnecessary redundant caches.

---

# 27. Stable IDs

Providers and persistent consumers that matter across load must have stable semantic IDs.

Examples:

```text
node-17:main-generator
node-17:harbor-lights
node-17:town-center
```

Do not use runtime instance IDs.

The exact ID scheme should reuse current stable node/settlement identity conventions.

---

# 28. Multiplayer authority

Power is consequential shared state.

Host/authority owns:

- provider state,
- fuel reserve,
- fuel-supply rates,
- consumer allocation,
- semantic power state.

Clients render results:

- lights on/off,
- generator visual state,
- local UI.

Do not replicate individual light shader frames or decorative effects.

Provider/consumer IDs and deterministic allocation should make replication compact.

---

# 29. Performance

The system should be event/dirty-driven.

Avoid:

- recomputing every node every frame,
- checking every lamp individually every Update,
- simulating unloaded nodes with GameObjects,
- per-frame sorting/allocation when inputs are unchanged.

Loaded NodeScene may update presentation from authoritative state, but the simulation itself should remain compact.

---

# 30. Debug visibility

Provide useful developer inspection.

At minimum:

```text
Node:
- FuelReserve / FuelCapacity
- InboundFuelSupplyRate
- LocalFuelProductionRate
- TotalRatedGeneration
- TotalAvailableGeneration
- TotalRequestedDemand
- ServedDemand
- PowerState

Provider:
- StableId
- RatedOutput
- AvailableOutput
- Operational state
- burn rate
- fuel multiplier

Consumer:
- StableId
- priority
- requested demand
- powered?
```

This can be Inspector/debug UI/logging.

Do not build a player-facing engineering dashboard.

---

# 31. Non-goals

Do not implement in this pass:

- electrical wiring
- separate town grids
- physical fuel cans/tanks
- player generator refueling
- autonomous settlement purchasing
- simulated individual trade ships carrying fuel
- full inter-node trade simulation
- generator wear
- generator repair mechanics beyond state seam/simple interaction if already available
- module-maintenance system
- settlement Prosperity/Stability/Security consequences
- partial-power consumer states
- variable lamp brightness from low voltage
- boat power rewrite
- advanced renewable/provider types
- multiple fuel commodities
- detailed NPC behavior around blackouts

---

# 32. Implementation checkpoints

## NPOWER.1 — Exact-project audit + persistent data contracts

Audit:

- MapNodeState persistence
- NodeScene bootstrap/binding
- settlement generation manifests
- world time
- trade/connectivity data
- lighting/time-of-day hooks
- save schema
- interaction UI
- network authority seams

Then define:

- NodePowerState
- provider model/contract
- consumer model/contract
- semantic PowerState
- abstract NodeFuelState
- stable IDs

No broad visual conversion yet.

Compile/test.

**STOP and report.**

---

## NPOWER.2 — Provider aggregation + fuel model

Implement:

- arbitrary provider collection
- primary town generator state
- shared node fuel reserve
- inbound supply rate
- local production rate
- burn accounting
- 40%-to-0 low-fuel output ramp
- analytical elapsed-time update

Verify unloaded simulation mathematically.

Compile/test.

**STOP and report.**

---

## NPOWER.3 — Deterministic consumer allocation

Implement:

- consumer registration/binding
- requested demand
- priority tiers
- stable load order
- binary powered/unpowered allocation
- stable deterministic resolution
- Normal / Strained / Blackout derivation

Use a few test consumers before converting every light.

Compile/test.

**STOP and report.**

---

## NPOWER.4 — Physical generator + grouped lighting integration

Bind a real NodeScene generator representation.

Implement simple inspect interaction.

Integrate a small representative set of logical power consumers, preferably:

- town-center/building light group
- street-light group
- harbor/safety-light group if available

Night/day demand should use existing time-of-day hooks.

Compile/test at day and night.

**STOP and report.**

---

## NPOWER.5 — Settlement persistence/generation + save/load

Integrate power-provider manifest/default state into generated settlements.

Ensure:

- starting node gets healthy reliable power,
- generated provider identity is persistent,
- fuel reserve/supply survives save/load,
- analytical time advancement survives unload/reload,
- old saves receive deterministic defaults.

Compile/test.

**STOP and report.**

---

## NPOWER.6 — Quest-ready output-change regression

Provide a developer/debug way to alter provider output/operational state.

Test the target vertical behavior:

```text
night
↓
town demand > available generation
↓
some groups dark
↓
increase/repair generator output
↓
allocation reruns immediately
↓
additional town groups visibly power on
```

No bespoke lighting script.

Run multiplayer-safe/state-authority review.

**STOP and report.**

---

# 33. Acceptance tests

## Provider architecture

1. Node can contain one provider.
2. Node can contain multiple providers without code changes.
3. Total generation is sum of available provider output.
4. Provider registration order does not change outcome.

## Fuel

5. Fuel reserve persists.
6. Incoming abstract supply increases reserve.
7. Local production contributes to same reserve.
8. Active provider burn reduces reserve.
9. Reserve never exceeds capacity or falls below zero.
10. At >=40% reserve, fuel multiplier is full.
11. Below 40%, available output ramps downward.
12. At zero fuel, fuel-supported generator output reaches zero.

## Unloaded simulation

13. Leave node, advance world time, reload: reserve reflects elapsed supply and burn.
14. No NodeScene GameObjects were required to perform that simulation.
15. Save/reload preserves last simulation timestamp/state correctly.

## Consumers

16. Consumer receives either Powered or Unpowered.
17. Quantitative shortage sheds lower-priority loads first.
18. Same inputs produce same allocation every time.
19. Scene hierarchy/registration timing does not alter winners.
20. Same-priority consumers resolve deterministically.
21. Grouped lighting turns on/off coherently.

## Time of day

22. Street-light group requests zero demand during day.
23. Night transition causes demand to appear and allocation to rerun.
24. Day transition removes that demand cleanly.

## Semantic state

25. Sufficient supply → Normal.
26. Some requested consumers shed → Strained.
27. No meaningful generation → Blackout.

## Physical generator

28. Physical generator binds to persistent provider state.
29. Inspect UI shows generator/node power facts.
30. Scene reload does not reset generator state.

## Quest-ready behavior

31. At night, intentionally constrain provider output so part of town is dark.
32. Increase provider output.
33. Allocation recalculates immediately.
34. Additional whole consumer groups visibly switch on.
35. No quest-specific direct calls to individual lights are required.

## Regression

36. Existing NodeScene lighting still sorts/renders correctly.
37. Existing world-time behavior remains intact.
38. Existing node save/load remains intact.
39. Boat power systems are unchanged.
40. No per-frame simulation is added for unloaded nodes.

---

# 34. Forward contracts

## Future trade simulation

Future abstract trade can supply:

```text
InboundFuelSupplyRate
```

without Node Power knowing whether that rate came from neighboring trade connectivity, route security, regional production, simulated shipments, player actions, or events.

Node Power only consumes the resulting rate.

## Future maintenance/failure

Future Node/Module Maintenance can modify provider state:

```text
OperationalMultiplier
ConditionMultiplier
```

Node Power reacts without owning wear mechanics.

## Future settlement simulation

Future settlement simulation can query:

```text
Normal
Strained
Blackout
ServedDemandFraction
FuelReserveFraction
```

and decide what economic/social consequences occur.

Not part of this pass.

## Future quests

Quest code should manipulate meaningful provider/fuel state rather than directly controlling presentation.

Correct:

```text
repair generator
→ provider output changes
→ NodePower recalculates
→ town visuals respond naturally
```

Wrong:

```text
quest complete
→ manually turn 37 lamps on
```

---

# 35. Final principle

Node power should behave as a compact persistent simulation:

> Settlement providers generate quantitative power, consumers request it, shortages shed deterministic loads, and abstract fuel economics determine whether generation can be sustained.

The physical town should visibly reflect that simulation.

The system should be simple enough to reason about, but rich enough that repairing a generator at night can make half a settlement come alive without a single bespoke lighting script.

---

**End of Node Power / Generator Foundation handoff.**
