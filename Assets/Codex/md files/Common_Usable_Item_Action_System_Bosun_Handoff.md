# DON'T SINK — Common Usable Item Action System
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Common action lifecycle for held/usable items, Primary + Secondary action slots, shared input/timing/presentation/movement/cancellation orchestration, executor components for gameplay logic, migration of existing usable items, and multiplayer-safe action authority seams  
**Status:** Ready for implementation  
**Immediate consumers:** Drill, Sounding Line, future Knife runtime-animation POC, Gun/Ballistics pass, Patch Applicator, future held tools/weapons  
**Out of scope:** Knife swing implementation itself, reactive seagrass, gun ballistics, module buffs/faults, full player role/perk system

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current:
   - held-item / equipped-item architecture,
   - inventory item instance model,
   - player input flow,
   - Interactor2D / targeting,
   - existing drill behavior,
   - existing sounding-line behavior,
   - tool capability checks,
   - charge/battery drain,
   - player movement modifier/state architecture,
   - animation/audio/VFX helpers,
   - interaction prompt ownership,
   - save/load,
   - multiplayer authority seams.
5. The goal is to **adapt existing usable-item behavior into a shared action lifecycle**, not rewrite working gameplay for sport.
6. Preserve existing item semantics during migration.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core problem

Usable items should not each invent their own:

- input lifecycle,
- hold/press semantics,
- duration timers,
- cancellation behavior,
- movement restrictions,
- animation timing,
- loop audio,
- progress values,
- authority request pattern.

The project needs one common action layer.

This action layer must be reusable by:

```text
Drill
Sounding Line
Knife
Gun
Patch Applicator
future tools
future melee weapons
future ranged weapons
future utility items
```

The system must remain generic enough that actions with very different gameplay logic can share the same lifecycle.

---

# 2. Architectural split

Hard design rule:

```text
ACTION DEFINITION
owns:
- action metadata
- input behavior
- duration
- cooldown
- movement policy
- presentation references
- timing windows / normalized progress metadata
- concurrency policy

ACTION EXECUTOR
owns:
- actual gameplay consequence
```

Do not build one giant action class containing booleans such as:

```text
IsDrill
IsGun
IsKnife
IsSoundingLine
```

Do not encode gameplay-type branches into the shared action host.

---

# 3. Usable item action host

A usable held item may expose two first-class action slots:

```text
UsableItemActionHost
├─ PrimaryAction
└─ SecondaryAction
```

Both slots exist architecturally from V1.

An item may leave one slot empty.

Examples:

```text
Drill
Primary   → Drill
Secondary → None

Knife
Primary   → Swing
Secondary → future stab/block

Gun
Primary   → Fire
Secondary → future aim/alternate mode
```

Do not build the architecture around exactly one action.

---

# 4. Data-only ItemActionDefinition

Use a data-driven definition, preferably ScriptableObject following current content conventions.

Conceptually:

```text
ItemActionDefinition

Identity
- StableId
- DisplayName

Input
- InputMode

Timing
- Duration
- Cooldown

Movement
- MovementPolicy
- ReducedMovementMultiplier

Concurrency
- ActionConcurrencyPolicy

Presentation
- AnimationProfile
- StartAudio
- LoopAudio
- CompleteAudio
- CancelAudio
- VFX profile / references

Optional timing metadata
- normalized active windows
- presentation event markers
```

Exact field names should follow current code conventions.

The definition must remain **data-only**.

Do not place scene references or runtime gameplay state in the ScriptableObject.

---

# 5. Input modes

V1 supports at least:

```text
Press
Hold
Toggle
```

Examples:

```text
Knife swing
→ Press

Semi-auto gun
→ Press

Automatic gun
→ Hold

Drill
→ Hold

Patch Applicator
→ Hold

Future flashlight
→ Toggle
```

If the audit discovers an existing action requiring press-and-release/charge behavior, add the smallest general extension necessary.

Do not invent more input modes without a real consumer.

---

# 6. Duration belongs to the action definition

Finite action duration is authored in the action definition.

Examples:

```text
Knife swing
Duration: 0.30 s

Sword swing
Duration: 0.65 s

Heavy hammer
Duration: 1.20 s
```

This allows multiple items to share an executor while having different pacing.

Hold/indefinite actions may use an explicit indefinite mode or executor-controlled completion while still exposing elapsed/progress semantics where meaningful.

---

# 7. Shared normalized progress

Every finite action exposes:

```text
Progress01
0.0 → 1.0
```

This is the shared timing source for:

- procedural animation,
- animation curves,
- hit windows,
- action UI,
- sound markers,
- VFX timing,
- future charge mechanics,
- movement curves if added later.

Example:

```text
Knife action:

0.00–0.15  windup
0.15–0.65  active swing
0.65–1.00  follow-through
```

Do not create independent hidden timers for presentation if the same normalized action clock can drive them.

---

# 8. Gameplay executor components

Gameplay logic lives on item/prefab components implementing a shared executor contract.

Conceptually:

```text
IItemActionExecutor

CanBegin(context)
Begin(context)
Tick(context, deltaTime)
Release(context)
Complete(context)
Cancel(context, reason)
```

Exact signatures may differ based on current architecture.

The important separation is:

```text
ScriptableObject definition
= authored action data

Executor component
= gameplay code
```

This avoids asset definitions containing scene/runtime behavior.

---

# 9. Executor lookup/binding

The item host must bind each action definition/slot to the appropriate executor component on that usable item.

Possible clean approaches:

- stable executor key,
- typed component reference configured on prefab,
- interface discovery constrained to the held item root.

Do not use global scene searches.

Do not rely on component order.

Do not use fragile string-based class names if a safer serialized binding exists.

---

# 10. Common ItemActionContext

Executors receive a common action context rather than independently searching the scene.

Conceptually:

```text
ItemActionContext

Actor / Player
ItemInstance
HeldItem / ActionHost
ActionSlot
AimDirection
AimWorldPosition
CurrentTarget / hovered target if applicable
Boat context if available
Authority / requester identity
Action runtime state
```

Only include fields that can be supplied cleanly by current systems.

Executors may derive specialized context from these inputs without calling broad `FindObjectOfType`-style searches.

This is important for multiplayer.

---

# 11. Movement policy

Actions may alter player movement while active.

Initial policies:

```text
Free
Reduced
Locked
```

For `Reduced`, the action definition includes a tunable multiplier.

Examples:

```text
Knife swing
→ Free or mildly Reduced

Drill
→ Reduced

Heavy weapon / minigun
→ strongly Reduced or Locked
```

The action system should request movement modification through the existing player movement/state modifier seam.

Do **not** directly rewrite base movement values in item code.

Movement restrictions must be removed on:

- completion,
- release,
- cancellation,
- item unequip,
- player invalidation.

---

# 12. Future player perks must be possible

The movement/action architecture must leave room for future actor modifiers.

Examples:

```text
Heavy Weapons perk
→ less movement penalty while firing

Tool Mastery perk
→ faster action duration

Mechanic perk
→ more repair per consumable
```

Do not implement perks here.

Avoid burying final action numbers in places that future actor modifiers cannot intercept.

---

# 13. Action concurrency

Primary and Secondary exist independently, but not every pair should be allowed to run simultaneously.

Add a small data-driven concurrency policy.

Recommended starting options:

```text
Exclusive
CanRunAlongsideOtherSlot
```

Default should be `Exclusive`.

Examples:

```text
Knife swing
→ Exclusive

Drill
→ Exclusive

Future gun Aim
→ may run alongside Fire
```

Do not build an elaborate action-graph scheduler.

The goal is only to avoid hardcoding “only one action can ever exist” and then immediately breaking aim+fire later.

---

# 14. Conflict behavior

When a new action is requested while a conflicting action is active, the definition/runtime must have deterministic behavior.

Prefer a small policy such as:

```text
RejectNew
or
CancelCurrentAndStartNew
```

Default to the safer current-gameplay behavior after audit.

Do not silently run mutually exclusive actions together.

---

# 15. Cooldown

Cooldown belongs to the action definition.

The shared host tracks action availability.

Cooldown should:

- start at a clearly defined lifecycle point,
- be authoritative,
- expose remaining time if UI needs it,
- not require each executor to reinvent timer code.

Do not use cooldown as a substitute for gun fire-rate internals where repeated automatic fire has specialized cadence.

The future Gun executor may use both:

```text
Action is held
+
GunDefinition.FireRate
```

inside the active action.

---

# 16. Cancellation is mandatory

Every running action must support clean cancellation.

Cancellation can be triggered by:

- item unequipped,
- item removed from player possession,
- player unconscious/dead,
- actor state transition that invalidates the action,
- boarding/unboarding transition if incompatible,
- target disappears,
- target becomes invalid/out of range where required,
- scene transition,
- authority rejection,
- conflicting action supersedes it,
- UI/tool mode closes where relevant.

Cancellation is not an optional executor feature.

---

# 17. Cancel cleanup contract

`Cancel()` must clean up everything the action started.

Examples:

```text
looping audio
movement restrictions
temporary VFX
temporary tool pose
target reservation
charge drain
progress UI
animation state
temporary interaction ownership
```

No action may leave orphaned state after cancellation.

A drill sound continuing after the tool is unequipped is a bug, not atmosphere.

---

# 18. Complete vs Release vs Cancel

Keep lifecycle semantics distinct.

## Release

Input was released for a hold-style action.

The executor may:

- complete,
- stop cleanly,
- transition to a release phase,
- cancel depending on action semantics.

## Complete

The action reached successful normal completion.

## Cancel

The action was invalidated/interrupted and must clean up without claiming successful completion.

Do not collapse all three into one “StopAction” callback if current consumers need distinct behavior.

---

# 19. Presentation orchestration

The shared action layer owns generic presentation lifecycle.

Conceptually:

```text
Action starts
↓
start animation
play start audio
start loop audio/VFX

Action progresses
↓
presentation follows Progress01

Action completes
↓
stop loop
play completion audio/VFX
exit animation

Action cancels
↓
stop loop
play optional cancel presentation
reset pose/state
```

Gameplay executors should not need to duplicate generic audio/animation boilerplate.

---

# 20. Gameplay remains authoritative outside animation

Animation/presentation does not determine gameplay truth.

For a future knife:

```text
Action Progress01
↓
procedural animation evaluates visible knife path

Executor
↓
evaluates active hit window / damage query
```

The animation should visually agree with the gameplay window, but the position of a sprite is not network authority.

---

# 21. Audio

The action definition may provide:

```text
StartAudio
LoopAudio
CompleteAudio
CancelAudio
```

Not every action needs every event.

Loop audio must stop on:

- release,
- completion,
- cancellation,
- item destruction/unequip.

Use existing audio architecture where practical.

Do not create one AudioSource permanently per action asset if shared/current patterns are cleaner.

---

# 22. VFX

Actions may optionally provide generic VFX hooks.

Examples:

```text
start spark
loop dust
completion puff
cancel effect
```

Gameplay-specific VFX may still be triggered by executors when tied to actual consequence.

Example:

```text
gun muzzle flash
→ generic action/start presentation may own it

bullet impact spark
→ ballistic result owns it
```

Keep responsibilities sensible.

---

# 23. Targeting

Some executors need a target; others do not.

The common context may provide the current hovered/aimed target.

Executors remain responsible for validating target type/capabilities.

Examples:

```text
Drill
→ requires valid extractable with capability

Patch Applicator
→ requires reachable breach

Knife
→ may not require preselected target; uses swing hit volume

Gun
→ uses aim direction, not hovered IInteractable requirement
```

Do not make the common action host require an `IInteractable` target for every action.

---

# 24. Primary/Secondary input integration

Audit current input architecture and introduce the smallest common mapping for:

```text
PrimaryAction
SecondaryAction
```

Do not hardcode mouse buttons deep inside executors.

Input source should produce action intents.

This is required for:

- control remapping,
- controller support,
- multiplayer intents,
- AI/future actor control.

---

# 25. Multiplayer authority

Architect the action lifecycle for host-authoritative gameplay.

Conceptually:

```text
Client/player input
↓
Action intent
↓
authority validates:
- actor owns item
- action slot exists
- cooldown
- actor state
- target/range requirements
- item resources
↓
authoritative executor consequence
```

Clients may begin responsive local presentation where safe.

Do not let client-side presentation determine:

- extraction rewards,
- ammo consumption,
- structural damage,
- breach repair,
- charge consumption,
- authoritative target effects.

---

# 26. Action runtime identity

A running action should have enough runtime identity to reject stale completion/cancel messages.

Conceptually:

```text
ActionRunId / sequence
```

This is particularly useful for multiplayer and rapid cancellation/restart.

Do not overbuild a distributed job system.

A monotonically increasing local/authority sequence is sufficient if current net architecture supports it.

---

# 27. Persistence

Active action execution should generally **not** persist across save/load.

On save/load:

```text
running action
→ restored as idle
```

Persist item state owned by the item itself, not transient action progress.

Examples:

Persist:
- drill charge/battery state,
- gun magazine/ammo state,
- item instance data.

Do not persist:
- knife swing at Progress01 0.47,
- drill loop audio state,
- temporary movement penalty,
- action cooldown unless current game/save semantics explicitly require it.

If cooldown persistence is unnecessary, reset it on load.

---

# 28. Migration target: Drill

The Drill is the first recommended migration target.

Bosun must preserve current behavior.

Expected action shape:

```text
PrimaryAction = Drill
InputMode = Hold
Duration = indefinite / executor-controlled
Movement = Reduced
Executor = DrillActionExecutor
```

Preserve:

- tool capability validation,
- extractable targeting,
- current extraction duration semantics,
- charge/battery drain,
- resource reward,
- cancellation behavior,
- inventory interaction.

The goal is to adapt current drill logic to the shared lifecycle, not rewrite the extraction system.

---

# 29. Migration target: Sounding Line

Audit the exact current sounding-line implementation.

Move its usable lifecycle into the Action system while preserving:

- deploy/retract behavior,
- measurement logic,
- current evidence/charting integration,
- physical line presentation,
- targeting/environment requirements,
- cancellation/reset rules.

The Sounding Line is intentionally a second migration target because it may have more staged behavior than the Drill.

This proves the action system supports more than one trivial hold-action pattern.

---

# 30. Immediate future consumer: Gun

The previously generated Hull Damage / Breach / Gun handoff remains valid for:

- gun data,
- magazine item instances,
- ammo compatibility,
- hitscan solver,
- ricochet,
- penetration,
- breaches,
- flooding,
- patching.

However, its gun input/lifecycle must use this Action system.

Required reconciliation:

```text
Gun usable item
PrimaryAction = Fire
Executor = GunFireActionExecutor
```

Semi-auto:

```text
InputMode = Press
```

Automatic:

```text
InputMode = Hold
```

`GunDefinition.FireRate` still controls repeated shot cadence.

Do not create a second bespoke gun input loop.

---

# 31. Immediate future consumer: Knife

The runtime-animation POC will consume this system.

Conceptually:

```text
Knife
PrimaryAction = Swing
InputMode = Press
Duration = authored in ItemActionDefinition
Movement = authored
Executor = MeleeSwingActionExecutor
Presentation = procedural swing profile
```

The POC should prove that action timing and animation timing share `Progress01`.

Knife implementation itself is outside this pass.

---

# 32. Immediate future consumer: Patch Applicator

The Hull Damage pass should use:

```text
Patch Applicator
PrimaryAction = ApplyPatch
InputMode = Hold
Executor = PatchBreachActionExecutor
```

The Action system owns:

- hold lifecycle,
- duration/progress,
- movement restriction,
- audio/presentation,
- cancellation.

Patch executor owns:

- breach validation,
- authority,
- consumable Hull Patch use,
- Patched state.

---

# 33. Other likely future migrations

Likely candidates after the foundation proves stable:

- Charting Instrument actions,
- Telescope held-use actions,
- future knife/sword/hammer tools,
- firearms,
- maintenance/repair tools,
- utility devices,
- fishing/salvage tools,
- future deployables where “use while held” applies.

Do not force non-held world interactions into this system merely because it exists.

---

# 34. No universal gameplay code in ActionHost

The shared host must not learn concepts such as:

```text
ore
breach
ammo
fish
damage
chart
sounding depth
```

Those belong to executors/domain systems.

The host knows only:

```text
action slot
input state
timing
cooldown
movement
presentation
lifecycle
authority/request
```

Protect this boundary.

---

# 35. Error/failure feedback

`CanBegin` / authoritative validation should be able to return a structured failure reason.

Examples:

```text
NoTarget
OutOfRange
NoCharge
NoAmmo
Cooldown
MovementStateBlocked
WrongToolCapability
NoConsumable
NoAuthority
```

Do not require one giant universal enum if domain-specific reasons need extension.

The interaction/UI layer should be able to display useful failure feedback without executor-specific UI hacks.

---

# 36. Action-state query

Expose lightweight state for UI/debugging:

```text
IsRunning
ActionSlot
DefinitionId
Progress01
Elapsed
CooldownRemaining
MovementPolicy
CurrentTargetId if meaningful
```

This is useful for:

- progress rings,
- tool HUD,
- debugging,
- multiplayer replication/prediction later.

---

# 37. Performance

Avoid:

- global searches every action tick,
- reflection-based executor lookup every frame,
- action ScriptableObjects holding mutable runtime state,
- duplicated Update loops on every inactive usable item if a central/current held-item tick can avoid it,
- per-frame allocation garbage from action context construction.

Only the equipped/active item's running actions should need meaningful continuous ticking.

---

# 38. Debug visibility

Provide developer inspection for the held usable item:

```text
Primary definition
Primary executor
Primary input mode
Primary running?
Primary Progress01

Secondary definition
Secondary executor
Secondary input mode
Secondary running?

Current movement modifier
Cooldowns
Current target
Last cancel reason
Authority state
```

Optional debug actions:

```text
Force Begin
Force Cancel
Print Context
```

Keep these developer-facing.

---

# 39. Non-goals

Do not implement in this pass:

- knife swing animation,
- knife damage,
- reactive seagrass,
- gun ballistic resolution,
- magazine/ammo behavior beyond reconciliation contract,
- patch breach logic,
- player perks,
- player roles/leveling,
- module faults/buffs,
- complex combo systems,
- action queues,
- animation state-machine replacement,
- arbitrary AI behavior trees,
- universal world interactions.

---

# 40. Implementation checkpoints

## ACTION.1 — Exact-project audit + common contracts

Audit:

- held-item root / equipped-item architecture,
- input,
- drill,
- sounding line,
- interaction targeting,
- movement modifiers,
- audio/animation helpers,
- network authority.

Define:

- ItemActionDefinition,
- Primary/Secondary slots,
- InputMode,
- MovementPolicy,
- ActionConcurrencyPolicy,
- ItemActionContext,
- IItemActionExecutor,
- action runtime state.

No migrations yet.

Compile/test.

**STOP and report.**

---

## ACTION.2 — Action host lifecycle

Implement:

- Primary/Secondary intent handling,
- Begin,
- Tick,
- Release,
- Complete,
- Cancel,
- Duration,
- Progress01,
- Cooldown,
- conflict/concurrency behavior,
- action run identity/sequence.

Use a trivial test executor.

Compile/test.

**STOP and report.**

---

## ACTION.3 — Movement + presentation orchestration

Implement:

- Free / Reduced / Locked movement policy,
- tunable reduced multiplier,
- cleanup on every exit path,
- start/loop/complete/cancel audio hooks,
- animation/presentation hook,
- optional generic VFX hook.

Verify cancellation cannot leave movement/audio stuck.

Compile/test.

**STOP and report.**

---

## ACTION.4 — Drill migration

Adapt exact-current Drill behavior into:

```text
UsableItemActionHost
+
DrillActionExecutor
```

Preserve all existing gameplay.

Regression-test:

- valid extraction,
- invalid target,
- charge drain,
- release,
- unequip cancel,
- target disappears,
- scene/player state interruption.

Compile/test.

**STOP and report.**

---

## ACTION.5 — Sounding Line migration

Adapt exact-current sounding-line behavior.

Preserve all existing measurement/evidence behavior.

Verify staged lifecycle and cancellation.

Compile/test.

**STOP and report.**

---

## ACTION.6 — Primary/Secondary + concurrency proof

Create a small developer/test item if needed to prove:

- Primary works,
- Secondary works,
- Exclusive conflict works,
- allowed concurrency works,
- cancellation of one slot does not corrupt the other,
- movement modifiers resolve safely.

No polished gameplay content required.

Compile/test.

**STOP and report.**

---

## ACTION.7 — Gun/Patch/Knife reconciliation seams

Without implementing those consumers yet, document/prepare the bindings required for:

```text
GunFireActionExecutor
MeleeSwingActionExecutor
PatchBreachActionExecutor
```

Ensure no Action-system limitation would force bespoke input/timing.

Compile/test.

**STOP and report.**

---

## ACTION.8 — Save/load + multiplayer-safe regression

Verify:

- active actions do not persist unexpectedly,
- held item state survives normally,
- cancellation occurs safely on scene/load transitions,
- action intent path is authority-safe,
- no client-authoritative resource/damage effects,
- no regression to inventory drag, boarding, interaction prompts, or tool handling.

**STOP and report.**

---

# 41. Acceptance tests

## Action slots

1. Usable item may define Primary.
2. Usable item may define Secondary.
3. Either slot may be empty.
4. Primary and Secondary use the same common lifecycle.

## Input

5. Press action begins once per press.
6. Hold action remains active while held.
7. Toggle action enters/exits correctly.
8. Executors do not hardcode physical input keys/buttons.

## Timing

9. Finite action uses definition-authored Duration.
10. `Progress01` advances 0→1.
11. Different definitions using the same executor can have different durations.
12. Cooldown blocks restart correctly.

## Movement

13. Free action does not alter movement.
14. Reduced action applies authored multiplier.
15. Locked action prevents movement through the existing movement seam.
16. Completion restores movement.
17. Cancel restores movement.
18. Unequip restores movement.

## Cancellation

19. Unequip cancels.
20. Item removal cancels.
21. player invalid/unconscious state can cancel.
22. target invalidation can cancel target-dependent action.
23. scene transition cancels.
24. authority rejection cancels/does not commit gameplay.
25. loop audio/VFX do not remain orphaned.

## Concurrency

26. Exclusive action rejects/cancels conflicting action according to policy.
27. Concurrent-compatible Primary/Secondary can coexist.
28. Ending one concurrent action does not corrupt the other.

## Context

29. Executor receives actor/item/aim/target context without global scene search.
30. Context supports targetless actions.
31. Context supports target-dependent actions.

## Drill migration

32. Drill still extracts valid resources.
33. Drill still rejects invalid targets/capabilities.
34. Drill still drains charge correctly.
35. Drill release/cancel stops cleanly.
36. Existing drill behavior is otherwise unchanged.

## Sounding Line migration

37. Existing sounding-line measurement works.
38. Existing chart/evidence consequences remain intact.
39. Sounding-line cancellation leaves no orphaned line/state.

## Multiplayer

40. Client action request does not authoritatively grant extraction/rewards.
41. Authority validates action start/consequence.
42. Stale action completion can be rejected by runtime sequence/identity.

## Persistence

43. Saving/loading does not resume a half-finished swing/drill action.
44. Persistent item state remains intact.
45. Transient movement/audio/action state resets cleanly.

---

# 42. Forward contract: Runtime Animation POC

The next animation POC consumes this system.

Knife:

```text
PrimaryAction = Swing
↓
Progress01
↓
procedural held-item path
↓
authored active hit window
```

The POC should not invent a separate knife timer.

The same action lifecycle drives:

- visible motion,
- sound timing,
- hit timing,
- movement restrictions,
- completion/cancellation.

Reactive seagrass is a separate environmental animation technique and does not use ItemAction.

---

# 43. Forward contract: Gun pass

The Gun/Breach handoff must treat the Action system as its input/lifecycle foundation.

Required architecture:

```text
UsableItemActionHost
↓
Primary Fire action
↓
GunFireActionExecutor
↓
GunDefinition + inserted Magazine + Ammo
↓
Ballistic solver
```

Do not create a duplicate gun-specific input lifecycle.

Automatic fire remains a Hold action whose executor honors the gun's authored FireRate.

---

# 44. Forward contract: Player roles/perks

Future actor progression may modify action/service outcomes.

Possible modifier seams:

```text
Action duration multiplier
Movement penalty multiplier
Cooldown multiplier
Repair amount multiplier
Maintenance amount multiplier
Recoil/accuracy modifiers later
```

The Action system does not implement progression.

It must merely avoid making those future modifiers impossible.

---

# 45. Final principle

The system should make these statements true:

> A knife, drill, sounding line, gun, and repair tool can all share the same action lifecycle without sharing gameplay logic.

> The action definition controls how an action behaves in time and presentation.

> The executor controls what that action actually does.

> A sword can use the same melee executor as a knife but have a slower authored duration.

> A minigun can lock or heavily reduce player movement without weapon code rewriting the player motor.

> An action can always be cleanly cancelled without leaving audio, movement, charge drain, VFX, or temporary state behind.

> Guns can adopt this system before their ballistic implementation, instead of becoming another bespoke input island.

That is the foundation this pass should deliver.

---

**End of Common Usable Item Action System handoff.**
