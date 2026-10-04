# DON'T SINK — Charting Instrument Bosun Handoff
## Sacred Physical Charting Device + Existing Celestial Charting Cartridge Integration

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature:** Charting Instrument  
**Scope:** Small star-charting completion pass  
**Purpose:** Replace the current debug/function-key entry into the existing celestial charting cartridge with a real sacred physical boat item, add internal Charting Paper storage, add sky-clearance gating, and persist the completed-observation checkpoint so lack of paper does not force the player to redo charting work.

---

# 0. NON-NEGOTIABLE PROJECT RULES

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized state, compatibility shims, authority hooks, and current multiplayer hardening.
3. Reuse current interaction, inventory, storage, persistence, camera, and charting-cartridge architecture where possible.
4. Prefer additive support classes over invasive rewrites.
5. Do not regress Phase 6/7 map-table, celestial truth, chart evidence, or constellation systems.

## Do not redesign the existing charting cartridge

The current charting cartridge presentation/camera flow is considered sufficient.

Do **not** redesign:
- its visual presentation,
- its camera behavior,
- its core charting interaction,
- its star-selection rules,
- its existing evidence generation flow,

except where necessary to:
- launch it from the physical Charting Instrument,
- support internal paper storage,
- support the resumable chart-phase checkpoint described below,
- support E / Escape exit integration if not already present.

---

# 1. FEATURE DEFINITION

The Charting Instrument is:

- a physical carryable boat item,
- placeable on the boat,
- pinnable/deployable,
- a **sacred item**,
- limited to one valid instrument per boat,
- required to launch the existing celestial charting cartridge,
- gated by the same clear-sky rule as the Observation Telescope,
- equipped with dedicated internal storage for Charting Paper,
- capable of preserving a completed-observation checkpoint until paper is available.

The Charting Instrument is **not** the telescope.

Strict split:

```text
Observation Telescope
= look at the sky clearly

Charting Instrument
= perform actual celestial charting
```

---

# 2. BASIC PLAYER FLOW

```text
pick up Charting Instrument
      ↓
place on valid boat surface
      ↓
pin/deploy
      ↓
press E
      ↓
check upward sky-clearance box
      ↓
blocked?
   yes → "No unobstructed view of sky."
   no  → launch existing celestial charting cartridge
      ↓
player performs existing charting workflow
      ↓
if player reaches chart phase:
    completed observation checkpoint becomes persistent
      ↓
paper available in instrument?
    yes → continue and create chart evidence normally
    no  → player may exit without losing completed observation
           another crew member may load paper
           player may return later / after scene changes / after travel
      ↓
resume directly at chart phase
      ↓
successful chart creation consumes paper
      ↓
clear pending checkpoint
```

---

# 3. SACRED ITEM RULES

## 3.1 One valid Charting Instrument per boat

There should be only **one valid Charting Instrument associated with a boat/crew at a time**.

Do not support multiple simultaneously valid charting instruments on one boat.

## 3.2 Sacred replacement philosophy

The player must never permanently lose access to charting because the device fell overboard, despawned, clipped into geometry, or otherwise became unavailable.

Every town should eventually expose an NPC capable of replacing the instrument.

For this pass:
- the actual NPC may be debug/simple if town NPC content is not ready;
- expose the required replacement hook/API;
- do not overbuild narrative/vendor behavior yet.

## 3.3 Boat-persistence-only ownership rule

The intended rule is simple:

> If the Charting Instrument persists with the boat, the crew still has it. If it does not persist with the boat, the crew does not have it and may receive a replacement.

Do not invent a separate Lost/Recoverable sacred-item state machine like the Money Chest.

There is no meaningful gameplay value in recovering an obsolete lost charting device.

## 3.4 Forced replacement / stuck-object override

Provide a safe replacement path for cases where the instrument technically still exists but is unusable, for example:
- stuck inside geometry,
- inaccessible due to a bug,
- corrupted physical placement.

A forced replacement must:
1. delete/invalidate the existing Charting Instrument,
2. reset any pending charting checkpoint,
3. create/issue the new replacement instrument.

Never allow forced replacement to duplicate the sacred item.

## 3.5 Replacement resets charting progress

A newly issued Charting Instrument starts clean.

Important:

> **Pending chart-phase progress does NOT transfer to a replacement device.**

If the old device is lost or intentionally replaced:
- any stored pending chart-phase checkpoint is discarded,
- the player must perform the charting observation again on the new device.

This is intentional.

---

# 4. PHYSICAL PLACEMENT / DEPLOYMENT

Reuse the same reusable non-module placeable-boat-equipment seam established for the Observation Telescope.

Conceptually:

```text
Carried
   ↓
Placed on valid boat surface
   ↓
Pinned / deployed
   ↓
Usable
```

The Charting Instrument should not:
- require BoatBuilder installation,
- consume securing rope,
- use the cargo-securing minigame,
- become permanently welded to reality.

## Future impulse seam

Preserve the broader rule:

> **Nothing in the boat is ever truly safe.**

Do not implement impulse knock-loose behavior now.

But future physical impulse systems should be able to:
- loosen it,
- knock it over,
- unseat it,
- shift it.

Sacred means recoverable/replacable, not physically invulnerable.

---

# 5. SKY-CLEARANCE REQUIREMENT

Use the **same sky-clearance architecture and behavior as the Observation Telescope**.

Do not create a second incompatible implementation.

## Required behavior

Use an upward box/volume check.

Configurable concepts should include:
- Clearance Width
- Clearance Height
- Obstruction LayerMask
- Recheck Interval if applicable

Anything inside the configured upward clearance volume may block use.

Examples:
- ceiling,
- overhanging deck,
- nearby upper hull/wall,
- module,
- placed object,
- other physical obstruction.

Distant geometry outside the upward box should not block use.

## Error text

On failed use:

> **No unobstructed view of sky.**

## Re-check while cartridge is active

If the clearance becomes obstructed while charting:
- safely exit/cancel the active cartridge,
- preserve pending chart-phase progress if the player had already reached the persistent checkpoint,
- show `No unobstructed view of sky.`

Do not fabricate chart evidence because the session was interrupted.

---

# 6. CHARTING CARTRIDGE ENTRY

## 6.1 Replace debug-only entry

The Charting Instrument becomes the normal physical entry point into the existing celestial charting cartridge.

Current function-key/debug launch may remain as a development/debug shortcut if useful, but it is no longer the intended gameplay path.

## 6.2 Interaction

When:
- instrument is deployed,
- sky clearance is valid,
- instrument is not already in use by another operator,

pressing `E` launches the existing charting cartridge.

## 6.3 No new telescope-view behavior

Do not hide the boat or reuse the Observation Telescope's presentation.

The current charting cartridge presentation is already acceptable.

No additional boat-fade work is required here.

---

# 7. DAY / NIGHT / WEATHER RULES

## 7.1 Cartridge may be launched at any time

Do not night-gate opening the Charting Instrument.

The player may launch the cartridge:
- day,
- night,
- bad weather,
- useless conditions.

## 7.2 Existing visibility rules determine selectable stars

During daylight:
- stars that are not actually visible should not be selectable/chartable.

During bad weather:
- clouds/fog/weather continue to limit visibility according to current celestial rendering/charting rules.

The instrument does not:
- brighten hidden stars,
- pierce clouds,
- remove fog,
- override day/night visibility,
- fabricate chartable targets.

---

# 8. INTERNAL CHARTING PAPER STORAGE

## 8.1 Dedicated internal storage

The Charting Instrument has dedicated internal storage that accepts **Charting Paper**.

Prefer using the existing item-definition/container/internal-storage architecture.

Do not invent a custom one-off paper inventory system if the project already supports item instances with internal storage.

## 8.2 Storage contents

The storage is intended for:
- Charting Paper only.

Use existing item/category/filter definitions where possible.

## 8.3 Stack size

Use the existing Charting Paper item definition / existing stack-size system.

Do not hardcode a telescope-specific or charting-instrument-specific paper capacity unless the current storage architecture requires one.

## 8.4 Normal inventory interaction

Load/unload paper the same way other current internal-storage items work.

Examples may include:
- drag/drop,
- container UI,
- existing internal-storage interactions.

Follow the exact current architecture.

## 8.5 Shared storage during active use

This is important for future multiplayer.

While Player A is actively operating the Charting Instrument:
- Player B may access the instrument's paper storage,
- Player B may add Charting Paper,
- newly added paper becomes immediately available to Player A's active charting workflow.

Example intended fantasy:

```text
Steve: actively charting
Steve: "We're out of paper."
Bob: grabs paper from nearby chest
Bob: loads paper into Charting Instrument
Steve: continues without restarting
```

Do not lock the paper inventory merely because the charting cartridge is in use.

---

# 9. PAPER CONSUMPTION SOURCE

The charting workflow should consume Charting Paper from the instrument's dedicated internal storage.

The instrument is the paper source for this workflow.

Do not require the current operator to personally carry paper once this feature is implemented.

If legacy code currently checks player inventory directly:
- refactor the paper-source query minimally and cleanly,
- preserve existing evidence-generation authority,
- avoid broader inventory rewrites.

---

# 10. RESUMABLE CHART-PHASE CHECKPOINT

This is the main cartridge behavior change.

## 10.1 What persists

Only the meaningful completed-observation checkpoint persists.

The intended boundary is:

> Once the player successfully reaches the **chart-recording/chart-production phase**, the observation work is considered complete and may be resumed later without repeating the earlier minigame.

Before that point:
- no special persistence is required.

## 10.2 No-paper behavior

If the player reaches the chart phase but the instrument has no Charting Paper:
- do not restart the cartridge,
- do not erase the completed observation,
- do not force the player to repeat the observation,
- allow the player to exit and obtain paper.

The player may:
- fetch paper from the boat,
- buy/acquire paper later,
- travel to another town,
- save and quit,
- reload,
- return later.

The completed observation remains pending on that same instrument.

## 10.3 Persistence strength

The pending chart-phase checkpoint should survive:
- closing the cartridge,
- walking away,
- scene transition,
- NodeScene ↔ BoatScene,
- travel,
- save/load.

It should survive essentially anything **as long as that same sacred physical Charting Instrument remains the boat's valid persisted device**.

## 10.4 What clears the checkpoint

Clear the pending checkpoint when:
- chart evidence is successfully committed to paper,
- the player explicitly abandons/resets the pending observation if such a command already exists or is simple to add,
- the Charting Instrument is deleted/replaced.

## 10.5 Replacement does not inherit checkpoint

A replacement Charting Instrument starts with:
- no pending chart-phase checkpoint,
- no carried-over observation.

The player must re-observe/chart.

## 10.6 Pending observation ownership

Pending chart-phase progress belongs to the **physical Charting Instrument**, not permanently to the player who created it.

The instrument/crew may later resume it.

However:
- only one operator may actively use the charting cartridge at a time.

---

# 11. ACTIVE-USE EXCLUSIVITY

The boat has one Charting Instrument.

Only one player may actively operate its charting cartridge at a time.

If another player attempts to use it while occupied, use an existing interaction-busy pattern or a concise message such as:

> **Charting instrument in use.**

The exact player-facing text may follow current conventions.

Important distinction:

### Exclusive
- active cartridge operator

### Not exclusive
- internal paper storage access

This allows another crew member to supply paper to the active operator.

---

# 12. EXIT / CANCEL

Support:
- `E` to exit,
- `Escape` to exit.

Follow existing cartridge lifecycle patterns.

On exit:

### Before persistent chart phase
No special resume requirement.

### After persistent chart phase
Keep pending observation intact until successfully committed, explicitly reset, or device replaced.

Do not accidentally consume paper or generate evidence merely because the cartridge closes.

---

# 13. PERSISTENCE MODEL

## Physical sacred item state

Persist through the existing boat-item persistence system:
- item identity,
- transform,
- deployment/pinned state,
- internal Charting Paper storage,
- pending chart-phase checkpoint.

## Transient active-session state

Do not persist:
- currently active operator,
- current UI focus,
- transient pre-checkpoint minigame progress,
- temporary cursor state,
- open/closed cartridge presentation.

Nobody should load directly into an active cartridge session.

## Boat-persistence-only sacred existence

Use the boat's persistence as the primary truth for whether the crew has a valid Charting Instrument.

Avoid a parallel "lost sacred object registry" unless the live architecture absolutely requires one.

---

# 14. REPLACEMENT HOOK / TOWN NPC SEAM

Actual final NPC content is not required yet.

Provide a clean gameplay/API seam that lets a town NPC or debug interaction ask:

```text
Does this boat currently have a valid persisted Charting Instrument?
```

If no:
- issue/create replacement.

If yes:
- normal replacement unavailable.

Also expose a forced replacement/debug-safe path:

```text
ForceReplaceChartingInstrument()
```

Conceptual behavior:
1. find current valid instrument if one exists,
2. delete/invalidate it,
3. clear pending checkpoint,
4. create/issue exactly one replacement,
5. avoid duplication.

Use project naming/authority conventions rather than these literal names if appropriate.

---

# 15. SHARED VS LOCAL STATE

## Shared consequential state
- Charting Instrument existence,
- transform,
- placement/deployment,
- internal paper inventory,
- pending chart-phase checkpoint,
- current occupancy/interaction lock.

## Local presentation state
- cartridge UI presentation,
- cursor/selection state,
- local transient pre-checkpoint interaction details.

Chart evidence remains shared through the existing celestial/charting authority architecture.

---

# 16. IMPLEMENTATION GUIDANCE

Inspect exact current systems before coding, especially:
- current charting-cartridge launch path,
- current debug/function-key entry,
- chart-phase transition,
- current Charting Paper consumption,
- current chart evidence creation,
- current item internal-storage definitions,
- `InventoryDragController`,
- item/container persistence,
- boat-item persistence,
- sacred-item patterns where useful,
- placement/deployment code from Observation Telescope pass,
- sky-clearance component from Observation Telescope pass,
- `Interactor2D` / `IInteractable`,
- requester/authority/revision patterns.

Do not assume old class layouts.

## Prefer reuse from Observation Telescope pass

The Charting Instrument should reuse:
- placeable/deployable boat-equipment seam,
- upward sky-clearance requirement,
- shared physical persistence conventions.

Do not duplicate those implementations.

---

# 17. CONCEPTUAL DATA SHAPE

Names are illustrative only.

A clean shape may resemble:

```text
ChartingInstrumentState
- itemInstanceId
- deploymentState
- internalStorage
- pendingChartObservation?
- revision
```

Pending checkpoint might contain only the exact authoritative data already required to resume at the chart-production phase.

Do **not** serialize the whole live cartridge UI/controller state if a compact observation-result payload is sufficient.

Prefer:

```text
completed observation result
+ exact data needed to generate the pending chart
```

rather than:

```text
save every button/cursor/minigame variable
```

---

# 18. SACRED-ITEM FAILURE RULE SUMMARY

### Instrument on boat and persisted
Crew has instrument.

### Instrument thrown overboard / absent from persisted boat state
Crew does not have it.
Town replacement hook may issue a new one.

### Instrument technically exists but is inaccessible/stuck
Forced replacement may:
- destroy old one,
- clear pending progress,
- issue one new instrument.

### Old device lost after reaching chart phase
Pending progress is lost with it.

### New replacement device
Starts fresh.

No Lost state.  
No recovery quest.  
No merging.  
No duplicate sacred instruments.

---

# 19. NON-GOALS

Do not implement in this pass:
- Observation Telescope behavior,
- boat visual hiding for charting,
- redesigned charting camera,
- acquired/found/bought charts,
- POI chart data,
- message-in-bottle generation,
- vendor generation,
- treasure-chest chart spawning,
- route plotting,
- ruler/protractor workbench,
- piloting rework,
- impulse knock-over physics,
- detailed town NPC dialogue/content,
- new celestial truth generation,
- new constellation mechanics.

---

# 20. ACCEPTANCE TESTS

The feature is ready when:

1. Charting Instrument can be picked up.
2. It can be placed on the boat.
3. It can be pinned/deployed.
4. It cannot be used while carried/loose.
5. It reuses the same upward sky-clearance check as telescope.
6. Obstructed use shows `No unobstructed view of sky.`
7. Clear placement allows `E` to launch the existing charting cartridge.
8. Current charting presentation remains otherwise unchanged.
9. Cartridge can be launched during daytime.
10. Daylight-hidden stars remain unselectable according to existing visibility.
11. Clouds/weather continue to matter.
12. E exits cartridge.
13. Escape exits cartridge.
14. Instrument has dedicated internal Charting Paper storage.
15. Storage uses existing internal-storage/item-definition rules.
16. Paper stack size follows existing item definition behavior.
17. Charting consumes paper from instrument storage, not operator pockets.
18. Another player can add paper while one player is actively charting.
19. Newly added paper becomes immediately usable by the active operator.
20. Only one player can operate the cartridge at a time.
21. Only one valid Charting Instrument exists per boat.
22. Reaching chart phase creates a persistent pending checkpoint.
23. Reaching chart phase with no paper does not restart/erase progress.
24. Player may exit, fetch paper, return, and resume directly at chart phase.
25. Pending checkpoint survives scene transition.
26. Pending checkpoint survives travel.
27. Pending checkpoint survives save/load.
28. Successful chart creation consumes paper and clears pending checkpoint.
29. Replacing/deleting the instrument clears pending checkpoint.
30. Replacement device does not inherit old pending progress.
31. Physical placement/deployment/internal paper storage persist with boat.
32. Active cartridge session itself does not persist.
33. Town/debug replacement hook can create a replacement if no valid instrument persists.
34. Forced replacement deletes old instrument before issuing new one.
35. Forced replacement cannot duplicate the sacred item.
36. Existing celestial evidence generation remains authoritative and correct.
37. Existing map-table/star-chart systems remain functional.

---

# 21. BOSUN DELIVERY EXPECTATIONS

When complete, report:

1. Summary of architecture added/changed
2. Exact files modified
3. Exact new files
4. Prefab/Inspector setup required
5. Reused Observation Telescope placement/clearance components
6. Internal-storage integration
7. Exact paper-consumption source change
8. Pending checkpoint data model
9. Save-schema additions
10. Replacement API/hook
11. Multiplayer occupancy behavior
12. Shared-vs-local state decisions
13. Compile/test status
14. Regression tests performed
15. Any live-code discrepancy from this handoff

Do not expand scope into acquired charts or the Cartography Workbench pass.

---

**End of Charting Instrument handoff.**
