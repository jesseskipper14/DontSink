# DON'T SINK — Boat Ballast System
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Installed ballast-tank modules, continuous ballast fill, local and centralized control, mass/COM integration, powered fill/drain, emergency dump, rupture behavior, persistence, and multiplayer-safe authority  
**Status:** Ready for implementation  
**Depends on / should integrate with:** Existing boat mass/buoyancy, Boat Structural Materials & Physical Attributes, boat power, flooding/compartment water, installed modules, Module Maintenance/Integrity seams  
**Out of scope:** Free-surface slosh, automated stabilization, full submarine systems, compressed-air simulation, ballast-control autopilot, advanced tank grouping

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current installed-module base classes, boat mass aggregation, Rigidbody2D center-of-mass ownership, buoyancy force system, boat power-provider/consumer APIs, flooding/compartment water state, module submersion/damage state, Boat Builder module placement, module persistence, interaction patterns, stable module IDs, and multiplayer authority seams.
5. Ballast must plug into existing physics. Do not invent a separate ballast force.
6. All consequential ballast state is host/authority owned.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core principle

Ballast is **movable contained mass**.

There is no special "ballast buoyancy force."

A ballast tank stores water.

That water contributes:

```text
mass
+
boat-local center-of-mass contribution
```

The existing boat physics then decide draft, list, trim, whether the vessel remains positively buoyant, whether it becomes neutrally-ish buoyant, and whether it sinks.

---

# 2. Gameplay goals

The system should naturally support:

```text
Boat listing too far?
→ add ballast to the opposite side

Bow riding too high?
→ add ballast forward

Boat sinking?
→ emergency dump ballast

Want to deliberately submerge?
→ carefully add enough ballast to overcome reserve buoyancy

Want crude submarine-like control?
→ fine-tune ballast manually
```

Do not implement a special submarine mode.

Submersion should emerge from existing mass + buoyancy physics.

---

# 3. Ballast tanks are real installed modules

Ballast tanks are Boat Builder modules.

They:

- occupy real boat space,
- have real boat-local position,
- have capacity,
- have current fill,
- have mass,
- consume power during normal active operation,
- can be damaged/disabled through existing/future module systems.

A tank's placement therefore matters.

A tank on port contributes ballast mass on port.

A tank near the bow contributes ballast mass near the bow.

---

# 4. Continuous fill level

Ballast is continuous, not discrete Empty/Half/Full state.

Conceptually:

```text
CurrentBallast
MaxBallastCapacity
FillFraction = CurrentBallast / MaxBallastCapacity
```

Clamp:

```text
0 <= CurrentBallast <= MaxBallastCapacity
```

The player can stop at any intermediate amount.

---

# 5. Ballast mass

Use one shared ballast-water density/mass conversion consistent with existing boat mass units.

Conceptually:

```text
BallastMass =
CurrentBallastVolume
× BallastWaterMassPerUnit
```

If the existing game already treats contained/flood water in appropriate mass units, reuse that convention.

Do not create an incompatible second water-mass scale.

---

# 6. Center-of-mass contribution

Each tank contributes its current ballast mass at the tank's boat-local center.

Conceptually:

```text
TankBallastCOMContribution =
TankLocalPosition × BallastMass
```

Ballast must feed the same overall boat mass/COM aggregation used by structural pieces, modules, cargo, flood water, occupants, and other dynamic masses.

No fake list/trim modifiers.

---

# 7. V1 partial-fill assumption

For V1, all ballast mass is treated as concentrated at the tank's module-local center regardless of fill fraction.

Do not model changing water centroid, free-surface effect, or internal slosh.

---

# 8. Tank commands

Normal tank command state:

```text
Fill
Drain
Hold
```

Fill takes water from the surrounding environment into the tank.

Drain expels ballast water from the tank at a controlled powered rate.

Hold maintains the current ballast amount and does not consume pumping power merely for existing.

---

# 9. Normal filling

Normal Fill:

- requires tank/module operational state,
- requires available boat electrical power,
- runs at authored `FillRate`,
- increases ballast continuously,
- stops automatically at MaxCapacity,
- contributes increasing mass/COM continuously.

Conceptually:

```text
CurrentBallast += FillRate × dt
```

No physical water item is created.

---

# 10. Normal draining

Normal Drain:

- requires tank/module operational state,
- requires available boat electrical power,
- runs at authored `DrainRate`,
- decreases ballast continuously,
- can be stopped at any fill percentage,
- leaves tank fully usable.

Conceptually:

```text
CurrentBallast -= DrainRate × dt
```

Normal Drain and Emergency Dump are separate intents.

---

# 11. Water source/sink abstraction

Filling takes surrounding water "for free."

Draining returns it to the surrounding environment.

Do not create water inventory items, spawn physical water objects, or track individual liters.

Only contained ballast amount matters.

---

# 12. Power integration

Each ballast tank is a normal boat power consumer while actively Fill/Drain pumping.

Conceptually:

```text
if Command == Fill:
    RequestedPower = FillPowerDemand

if Command == Drain:
    RequestedPower = DrainPowerDemand

if Command == Hold:
    RequestedPower = 0
```

If multiple tanks operate simultaneously, their power demands add normally.

Use the existing boat power allocation system.

Do not invent ballast-specific power rationing.

---

# 13. Power loss behavior

If Fill/Drain is active but required power is unavailable:

- ballast amount does not change,
- current command may remain selected or show "No Power" according to existing module conventions,
- once power returns, behavior should follow the simplest consistent current module-control pattern.

Prefer existing powered-module semantics.

---

# 14. Local direct controls

Every ballast tank can be controlled directly at the tank.

Local controls are intentionally crude:

```text
FILL
DRAIN
HOLD
```

The tank can therefore still be operated if the centralized ballast monitor is damaged/unavailable.

Local interaction should expose tank name, fill percentage, current state, and obvious no-power/disabled/emergency-released status.

---

# 15. Ballast Control Monitor

Add a physical **Ballast Control Monitor / Ballast Control Station** module or interactable.

It discovers/binds all ballast tanks installed on the same boat using stable module identity.

It should show, at minimum:

```text
Tank Name
Current Fill %
Current Command
Operational/Disabled state
Emergency-Released state
```

and allow selecting an individual tank and issuing:

```text
Fill
Drain
Hold
```

---

# 16. Ballast monitor power

The Ballast Control Monitor requires power for centralized control.

If the monitor loses power or becomes disabled:

- centralized ballast UI/control is unavailable,
- ballast tanks themselves remain locally controllable,
- tank physics/state continue to exist normally.

Do not make monitor failure disable all ballast valves.

---

# 17. Monitor instrumentation

The ballast monitor should additionally display:

```text
Total Ballast Mass
Boat Roll/List angle
Boat Trim/Pitch angle or equivalent fore/aft tilt
```

Use real current boat physics values where available.

Do not fake a trim/readout number if the current boat physics does not expose a meaningful one.

A future net-buoyancy readout may be added if current physics supports it cleanly.

---

# 18. Tank naming

Each ballast tank receives a simple default name:

```text
Ballast Tank 1
Ballast Tank 2
```

Player-renaming should be supported if the current module naming/persistence system makes it straightforward.

Do not implement automatic Port/Bow/Starboard/Stern naming logic.

No grouping system is required.

---

# 19. Simultaneous operation

Multiple tanks can Fill/Drain simultaneously.

Their power demands, mass changes, and COM changes resolve independently through existing systems.

Do not serialize commands through one global ballast queue.

---

# 20. Emergency Dump

Ballast tanks support a separate **Emergency Dump** action.

Emergency Dump:

- requires no electrical power,
- overrides the current normal command,
- rapidly empties ballast,
- is much faster than normal controlled Drain,
- sacrifices continued use of that tank until reset at a Node.

---

# 21. Emergency Dump All

The centralized ballast monitor should expose:

```text
EMERGENCY DUMP ALL
```

This initiates Emergency Dump on every eligible ballast tank.

It must work without normal tank pumping power.

If the monitor cannot provide this mechanical emergency action while unpowered due to implementation constraints, provide a separate physical emergency control associated with the station.

Gameplay requirement:

> Electrical failure must not prevent emergency ballast dumping.

---

# 22. Emergency Released state

After Emergency Dump completes, that tank enters:

```text
EmergencyReleased
/
NeedsReset
```

While in this state:

- tank cannot Fill,
- tank cannot controlled-Drain,
- tank remains installed,
- tank remains visible/manageable,
- the condition persists across save/load.

The emergency mechanism must be reset at a Node.

---

# 23. Resetting emergency-released tanks

V1 reset rule:

- boat must be docked / in the appropriate Node service context,
- player performs a reset/service action,
- reset is free in V1,
- tank returns to normal usable state.

No economy/material cost yet.

---

# 24. Save/load command behavior

Persist current ballast amount, tank name, EmergencyReleased/NeedsReset state, and rupture/damage state through the relevant systems.

Do **not** resume active Fill/Drain automatically after load.

On load:

```text
normal command → Hold
```

EmergencyReleased remains EmergencyReleased.

---

# 25. Normal disabled-tank behavior

If a ballast tank is mechanically disabled but otherwise intact:

- it keeps its current ballast,
- it cannot actively Fill,
- it cannot actively Drain,
- current mass remains in the boat.

Do not automatically empty a disabled tank.

---

# 26. Ruptured ballast tanks

A ballast tank can eventually receive a rupture state from damage.

A rupture creates a physical connection between:

```text
tank contents
→ owning compartment
```

Contained ballast leaks into the compartment according to rupture severity/rate.

This converts controlled contained water into uncontrolled compartment flooding.

Mass remains aboard the vessel; it simply changes subsystem ownership.

Do not double-count mass during transfer.

---

# 27. Ruptured-but-operational tank

A ruptured tank may still be operational.

If the player commands:

```text
Fill
```

on a ruptured operational tank:

```text
outside water
→ ballast intake
→ tank
→ rupture
→ compartment
```

The ballast module effectively becomes a powered pump feeding water into the boat.

Do not special-case this away.

This is desired gameplay.

---

# 28. Rupture flow model

Integrate rupture/leak rate with the Hull Damage/Breach system rather than creating an unrelated leak simulator.

Preferred ownership:

```text
Damage system:
- determines rupture existence/severity/local opening

Ballast system:
- exposes contained ballast amount
- transfers tank water through rupture

Flooding system:
- receives leaked water into owning compartment
```

If the tank is filling while ruptured, incoming ballast may continuously replenish the leaking volume.

---

# 29. Mass conservation

Avoid water-mass duplication.

## Tank leaking into compartment

```text
TankBallastMass decreases
CompartmentFloodMass increases
```

Net boat mass should remain essentially unchanged except for simultaneous external inflow/outflow.

## Tank normal Drain

```text
TankBallastMass decreases
Boat total mass decreases
```

## Tank Fill

```text
TankBallastMass increases
Boat total mass increases
```

---

# 30. Damage / maintenance integration

Ballast tanks are normal installed modules for Integrity, Condition if configured maintainable, disabled state, and future module buffs/faults.

The ballast system should not own generic module wear.

It reacts to module operational state.

Future damage may cause disabled pump, rupture, stuck valve, reduced rates, or other module effects.

Only rupture behavior is explicitly anticipated here.

---

# 31. No automatic stabilization

V1 has no auto-level, auto-trim, PID controller, keep-upright mode, or auto-neutral-buoyancy mode.

The player chooses tank commands manually.

A future automation/control module can add those conveniences later.

---

# 32. No group controls

Do not implement Port, Starboard, Bow, Stern groups or tank presets.

Most boats are expected to use at most one or two tanks.

The ballast monitor simply exposes each installed tank.

---

# 33. Submarine behavior is emergent

Do not add:

```text
IsSubmarineMode
```

or a special downward ballast force.

If boat total mass exceeds buoyant support, the vessel sinks according to existing physics.

With careful ballast tuning, the player may approach neutral buoyancy and use the boat like a crude submarine.

---

# 34. Free-surface/slosh explicitly deferred

Do not simulate partially filled tank slosh.

V1 assumption:

```text
ballast mass stays at tank center
```

---

# 35. Persistence

Persist per ballast tank:

```text
Stable Module Instance ID
Player-facing Tank Name
CurrentBallast
MaxBallastCapacity if instance-specific
EmergencyReleased / NeedsReset
rupture state through damage system
Integrity/Condition through module systems
```

Do not persist derived ballast mass, derived COM contribution, transient monitor selection, or ordinary active Fill/Drain command.

On load, normal command is Hold.

---

# 36. Multiplayer authority

Host/authority owns Fill/Drain/Hold command acceptance, ballast amount, power validation, Emergency Dump, EmergencyReleased state, reset-at-Node action, rupture transfer, and mass/COM consequences.

Clients may request commands and render fill gauges, monitor UI, tank sounds, valves/pump animation, and warnings.

Do not let clients directly set ballast state.

---

# 37. Performance

Avoid per-frame global searches for tanks, recalculating all boat mass from scratch if dirty aggregation exists, spawning water entities, per-liter simulation, or per-frame monitor rediscovery.

Prefer stable registered tank sets, dirty/event-driven mass/COM aggregation, and a fixed simulation cadence consistent with power/flooding systems.

---

# 38. Debug visibility

Provide developer inspection.

Per tank:

```text
StableId
Name
CurrentBallast / Capacity
FillFraction
BallastMass
BoatLocalPosition
CurrentCommand
Powered?
Operational?
EmergencyReleased?
Ruptured?
LeakRate if ruptured
```

Per boat:

```text
Total Ballast Mass
Structural/Dynamic Total Mass
Current COM
Roll/List
Trim
Vertical velocity
```

Useful developer controls may include setting tank to 0/50/100%, triggering Emergency Dump, and toggling rupture for testing.

---

# 39. Non-goals

Do not implement:

- free-surface slosh
- automatic trim
- automatic list correction
- neutral-buoyancy autopilot
- ballast tank grouping
- compressed air tanks
- realistic submarine blow systems
- dedicated submarine mode
- depth-pressure hull damage
- fluid particles/entities for ballast
- advanced valve-failure simulation
- reset/service costs
- background unloaded-boat ballast simulation
- per-liter network replication

---

# 40. Implementation checkpoints

## BALLAST.1 — Exact-project audit + tank data contract

Audit installed module base, module IDs/persistence, boat mass aggregation, Rigidbody2D COM, power consumer API, flooding/compartment APIs, module damage/Integrity, Boat Builder, and interactables.

Implement/define BallastTankModule data/state, capacity, current ballast, Fill/Drain/Hold command, fill/drain rates, power demand, and EmergencyReleased state.

No broad physics integration yet.

Compile/test.

**STOP and report.**

## BALLAST.2 — Mass + center-of-mass integration

Implement:

```text
BallastMass =
CurrentBallast × water-mass coefficient
```

Feed ballast into existing total boat mass and COM aggregation.

Test one centered tank, one off-center tank, port vs starboard placement, bow vs stern placement, and 0/50/100% fill.

Verify the boat lists/trims through actual physics.

No fake torque/list code.

Compile/test.

**STOP and report.**

## BALLAST.3 — Powered local controls

Implement local tank interaction:

```text
Fill
Drain
Hold
```

Integrate with existing boat power.

Verify Fill adds ballast/mass, Drain removes ballast/mass, Hold freezes amount, simultaneous tanks work, no-power state prevents powered movement, and tank remains locally controllable.

Compile/test.

**STOP and report.**

## BALLAST.4 — Central ballast monitor

Implement physical Ballast Control Monitor.

Features:

- discover/bind boat ballast tanks,
- list each tank,
- simple default names,
- optional rename if current naming path supports it,
- show fill %,
- show command/state,
- individual Fill/Drain/Hold,
- total ballast mass,
- roll/list readout,
- trim readout if meaningful,
- monitor requires power.

If monitor fails/unpowers, local tank controls continue to work.

Compile/test.

**STOP and report.**

## BALLAST.5 — Emergency dump + Node reset

Implement:

- per-tank Emergency Dump,
- monitor Emergency Dump All,
- no normal electrical power required,
- fast ballast removal,
- transition to EmergencyReleased/NeedsReset,
- unusable until Node reset,
- free reset action while docked/in appropriate Node service context.

Verify EmergencyReleased persists through save/load.

Compile/test.

**STOP and report.**

## BALLAST.6 — Rupture/flooding integration

Integrate ballast tanks with hull/module damage seam.

Implement:

- ruptured tank leaks stored ballast into owning compartment,
- mass transfers rather than duplicates,
- disabled intact tank holds ballast,
- ruptured operational tank may still Fill/Drain,
- Fill on ruptured tank can continuously pump outside water into compartment,
- leak severity uses shared damage/flooding abstractions where possible.

Compile/test.

**STOP and report.**

## BALLAST.7 — Persistence + multiplayer/regression

Verify current ballast persists, active normal commands load as Hold, EmergencyReleased persists, Node reset persists, tank names persist if rename implemented, COM rebuild matches saved ballast, rupture transfer remains correct, authority seams are host-owned, and no regression occurs to boat power/buoyancy/flooding.

Compile/test.

**STOP and report.**

---

# 41. Acceptance tests

## Module basics

1. Ballast Tank is an installed Boat Builder module.
2. It occupies physical placement space.
3. It has continuous CurrentBallast and capacity.
4. Two tanks can coexist and operate independently.

## Mass / buoyancy

5. Empty tank contributes no ballast mass.
6. Half-full tank contributes half maximum ballast mass.
7. Full tank contributes full ballast mass.
8. Off-center ballast shifts COM toward the tank.
9. Filling a port tank causes physically consistent list.
10. Fore/aft placement affects trim through physics.
11. Sufficient total ballast causes the boat to sit lower.
12. Sufficient ballast can make the vessel sink without special submarine code.

## Local control

13. Player can Fill locally.
14. Player can Drain locally.
15. Player can Hold locally.
16. Normal Fill requires power.
17. Normal Drain requires power.
18. Hold does not consume pumping power.
19. Two tanks can pump simultaneously if power permits.

## Central control

20. Powered monitor lists all ballast tanks.
21. Monitor can command each tank individually.
22. Monitor shows current fill.
23. Monitor shows total ballast mass.
24. Monitor shows roll/list.
25. Monitor shows trim if available.
26. Unpowered/disabled monitor does not disable local tank controls.

## Emergency dump

27. Emergency Dump works without normal pump power.
28. Emergency Dump empties tank rapidly.
29. Emergency Dump changes tank to EmergencyReleased.
30. EmergencyReleased tank cannot resume normal use.
31. Dump All triggers every eligible tank.
32. Node reset restores tank usability.
33. Node reset is free in V1.
34. Save/load does not clear EmergencyReleased.

## Save/load

35. Ballast amount survives save/load.
36. Normal Fill command loads as Hold.
37. Normal Drain command loads as Hold.
38. Tank name persists if rename support exists.
39. Rebuilt mass/COM after load matches expected ballast state.

## Damage / rupture

40. Disabled intact tank keeps its contained ballast.
41. Disabled intact tank cannot pump.
42. Ruptured tank leaks stored ballast into its compartment.
43. Tank ballast decreases as compartment flood water increases.
44. Transfer does not double-count boat mass.
45. Ruptured operational tank may still be commanded.
46. Fill on ruptured operational tank can increase compartment flooding.
47. Damage/flooding system remains authoritative for rupture/leak semantics.

## Multiplayer

48. Client request cannot directly set ballast amount.
49. Host/authority validates commands.
50. Emergency dump state is authoritative.
51. Monitor UI may be rendered locally from replicated state.

## Regression

52. Existing buoyancy formulas are unchanged.
53. Existing module power continues to work.
54. Existing compartment flooding continues to work.
55. Boat Builder placement remains stable.
56. No per-frame tank rediscovery/global scan is introduced.

---

# 42. Forward contracts

## Future auto-trim

A future automation module may read roll, trim, tank positions, and tank fill and issue the same Fill/Drain/Hold commands.

Do not give it privileged physics access.

## Future submarine systems

A future dedicated submarine may add pressure hull, compressed air, dive planes, depth instrumentation, and auto-neutral-buoyancy control.

Those systems should still use ballast mass rather than replacing it with fake forces.

## Future faults/buffs

Module effects may later cause stuck valves, slow pumps, reduced fill/drain rate, uncommanded leakage, or improved pump efficiency.

Ballast should expose appropriate modifier seams.

## Future repair/economy

Emergency reset may later cost money, parts, or service fees.

Ruptured tanks may require structural repair.

Not part of V1.

---

# 43. Final principle

The ballast system should make these statements true:

> Ballast is just water mass stored at a real place on the boat.

> A tank on one side can make the vessel list because the center of mass actually moved.

> Dumping ballast can save a sinking boat.

> Filling enough ballast can deliberately sink the boat without entering a magical submarine mode.

> A broken central monitor is inconvenient, not fatal, because tanks retain crude local controls.

> An emergency dump can save the vessel now but sacrifices ballast capability until the player reaches a Node.

> A ruptured tank can become a horrifying powered mechanism for pumping seawater directly into the boat if the player keeps Fill active.

That combination creates a large amount of gameplay from a deliberately small physical system.

---

**End of Boat Ballast System handoff.**
