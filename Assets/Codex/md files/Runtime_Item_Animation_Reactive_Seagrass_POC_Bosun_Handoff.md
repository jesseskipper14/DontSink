# DON'T SINK — Runtime Item Animation + Reactive Seagrass POC
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Two runtime-animation proof-of-concepts: (A) action-driven procedural held-item animation using the new Common Usable Item Action System, and (B) continuously reactive underwater seagrass influenced by ambient current and nearby moving bodies  
**Status:** Ready for implementation  
**Depends on:** Common Usable Item Action System  
**Immediate POC consumers:** Knife swing; individual reactive seagrass clumps  
**Out of scope:** Full hand/arm animation, full melee-combat framework, dense vegetation batching, gameplay drag/stealth from vegetation, full current simulation

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Audit the current:
   - held-item visual hierarchy,
   - usable-item/action integration,
   - player facing / sprite-flip logic,
   - sorting layers/orders for held tools,
   - physics-query conventions,
   - current underwater vegetation prefabs/materials,
   - water/current data providers if any,
   - player/creature Rigidbody2D and movement velocity access,
   - URP 2D shader/material conventions.
5. The knife POC must consume the Common Usable Item Action System rather than create a parallel timer/input system.
6. Reactive seagrass is a separate environmental animation system and must **not** depend on ItemAction.
7. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Why this POC exists

This pass intentionally proves two different runtime-animation families:

```text
A. ACTION-DRIVEN OBJECT ANIMATION
   finite action
   ↓
   normalized action progress
   ↓
   procedural held-item motion

B. CONTINUOUS ENVIRONMENTAL ANIMATION
   ambient motion + nearby disturbances
   ↓
   continuously changing deformation
```

Do not force both into one generic animation framework.

They share principles, not implementation.

---

# 2. POC A — Knife swing

The knife swing proves:

- Common ItemAction timing can drive visible motion,
- held-item motion can be data-driven,
- different items can reuse the same motion executor/player,
- left/right facing can mirror one authored profile,
- gameplay hit windows can align with the visible swing,
- fast melee motion can use swept hit detection,
- one target is hit at most once per swing by default.

---

# 3. Held-item hierarchy rule

Do not animate the authoritative held-item root through the swing path if that root also owns gameplay/inventory/interaction state.

Preferred conceptual hierarchy:

```text
HeldItemRoot
├─ item identity / instance binding
├─ action host / executor components
├─ interaction / gameplay components
└─ MotionRoot
   ├─ visual sprite
   └─ optional hit sample / hit proxy
```

The procedural motion system animates `MotionRoot`.

This prevents animation from unintentionally dragging:

- pickup logic,
- inventory identity,
- action-host transforms,
- unrelated colliders,
- authority/state ownership.

If the exact current prefab hierarchy already has a safe visual child, reuse it.

---

# 4. Runtime item motion profile

Create a data-driven motion profile, preferably following project ScriptableObject conventions.

Conceptually:

```text
RuntimeItemMotionProfile

Identity
- StableId
- DisplayName

Motion curves over Progress01
- PositionX
- PositionY
- RotationDegrees
- optional ScaleX
- optional ScaleY

Pose
- GripOffset / PivotOffset
- optional start/end local offsets

Gameplay/presentation metadata
- ActiveHitWindowStart01
- ActiveHitWindowEnd01
```

Exact names should follow current conventions.

For V1, curves are sufficient.

Do not build a custom scene-path editor unless the live Unity AnimationCurve tooling proves unusable.

---

# 5. Motion is evaluated from Action Progress01

The knife action definition owns action duration.

The animation does **not** create its own swing timer.

Conceptually:

```text
ItemActionRuntime.Progress01
↓
RuntimeItemMotionProfile evaluates curves
↓
MotionRoot local pose updates
```

Example knife profile:

```text
0.00
rest / slight windup

0.15
windup complete

0.55
fast attack arc

0.80
follow-through

1.00
return/rest completion
```

A sword may use:

- the same motion player,
- the same melee executor,
- a different action duration,
- a different motion profile.

---

# 6. Canonical facing direction

Author motion profiles in one canonical direction:

```text
Facing Right
```

At runtime, mirror them for left-facing actors.

Left-facing behavior should correctly mirror:

- local X position,
- rotation/orientation where needed,
- hit-sample path,
- sprite facing.

Do not author duplicate left/right motion profiles.

Bosun should reconcile this with the project's exact existing player sprite-flip/facing architecture.

---

# 7. Knife POC only animates the item

Do **not** animate player arms/hands in this pass.

The player body remains on its current animation/presentation system.

Only the held item visual moves.

Leave a future attachment seam so body/arm pose animation could later synchronize with the same action progress if desired.

---

# 8. Melee action integration

The knife should be a usable held item.

Conceptually:

```text
Knife
└─ UsableItemActionHost
   └─ PrimaryAction = Swing
      ├─ InputMode = Press
      ├─ Duration = authored
      ├─ MovementPolicy = authored
      ├─ MotionProfile = KnifeSwing
      └─ Executor = MeleeSwingActionExecutor
```

Do not create a separate knife input handler.

---

# 9. Active hit window

Gameplay damage is active only during an authored normalized window.

Example:

```text
0.20–0.65
→ hit detection active
```

Outside the window:

- visual motion may continue,
- no melee damage is applied.

This allows:

- windup,
- active swing,
- follow-through

to share one motion profile.

---

# 10. Swept hit detection

Do not rely only on a single overlap sample at the current knife position.

Fast motion can tunnel through targets between frames.

During the active hit window:

1. record previous hit-sample position,
2. evaluate current hit-sample position,
3. sweep/query between them using an appropriate 2D cast/shape test,
4. resolve valid targets.

Exact shape may be:

- small circle cast,
- capsule cast,
- line segment plus radius,
- another existing project-safe query.

Choose the simplest robust current-project approach.

---

# 11. One hit per target per swing

Default rule:

```text
one target
→ max one successful melee hit per action run
```

Maintain a per-action-run set of already-hit stable/runtime target identities.

Clear it when the action ends/cancels.

Do not apply full knife damage on six consecutive frames simply because the blade remains overlapping the same creature.

Future special weapons may override this behavior.

---

# 12. Gameplay hit logic remains separate from visual motion

The visual item path should line up with the damaging path, but visual sprite position is not gameplay authority.

Conceptually:

```text
Action Progress01
├─ RuntimeItemMotionPlayer → visible pose
└─ MeleeSwingActionExecutor → authoritative hit query
```

For multiplayer:

- authority resolves consequential hits,
- clients can render the swing locally.

Do not let client-only visual motion declare authoritative damage.

---

# 13. Cancellation

If the action is cancelled:

- MotionRoot returns/reset to its idle local pose,
- active hit detection stops immediately,
- per-swing hit cache clears,
- action audio/VFX cleanup follows the Common Action System contract.

Cancellation cases include:

- item unequipped,
- player state invalidated,
- scene transition,
- action superseded,
- authority rejection.

No knife should remain visually frozen halfway through a swing after cancellation.

---

# 14. Runtime motion player should be reusable

The knife is the POC consumer, but the runtime motion system should be generic enough for future uses such as:

```text
Sword swing
Hammer overhead attack
Spear thrust
Wrench smack
Tool jab
Gun recoil kick
Scanner sweep
```

Do not hardcode a circular knife arc.

The curves/profile define the motion.

---

# 15. POC B — Reactive seagrass

Reactive seagrass proves continuously simulated environmental animation.

Target behavior:

```text
Ambient current
+
organic sway
+
stable per-instance variation
+
nearby moving bodies
↓
responsive plant bend
↓
smooth recovery back into ambient motion
```

The grass remains rooted to the seabed.

---

# 16. Initial representation

Start with:

```text
one seagrass clump
=
one deformable rendered plant object
```

This is intentionally the simplest POC representation.

Do not solve dense vegetation batching yet.

If the visual result is successful, a later optimization pass may combine many blades/clumps into batched patch renderers.

---

# 17. Rooted deformation

The base/root of the grass should move very little or not at all.

The upper part should bend substantially more.

Conceptually:

```text
root       0% bend
lower      20–30%
middle     50–70%
tip        100%
```

Prefer a shader/vertex deformation path if the current sprite/mesh representation supports it cleanly.

If sprite geometry is too sparse for useful deformation, generate/use the smallest suitable subdivided mesh representation rather than moving the whole sprite as one rigid object.

---

# 18. Stylized underwater sway

Base motion should feel:

- slow,
- organic,
- underwater,
- not synchronized,
- not mechanically sinusoidal across the whole field.

Conceptually:

```text
BaseBend =
CurrentBias
+
SlowSway
+
StablePerInstancePhaseVariation
```

Use subtle layered sine/noise-like motion as appropriate.

Do not create expensive fluid simulation.

---

# 19. Current inputs

The seagrass animation should support:

```text
CurrentDirection
CurrentStrength
```

If the game already has a clean authoritative/environmental current provider, consume it.

If not, expose debug/authored fallback values for the POC.

Do not block the POC on building a full ocean-current simulation.

---

# 20. Stable per-instance variation

Multiple nearby grass clumps must not sway in perfect synchronization.

Each clump should derive stable variation such as:

```text
PhaseOffset
FrequencyVariation
AmplitudeVariation
```

Use stable seeded/object-derived values.

Do not use frame-dependent random values.

The same clump should not randomly change its personality every scene frame.

---

# 21. Generic VegetationInfluencer

Create a generic component/concept for nearby moving objects that can disturb vegetation.

Conceptually:

```text
VegetationInfluencer

- InfluenceRadius
- InfluenceStrength
- CurrentVelocity
- Stable identity if useful
```

Potential future users:

- player,
- fish,
- creatures,
- diving bell,
- other moving bodies,
- large machinery,
- environmental movers.

The diving bell should eventually be able to influence vegetation using the same system.

Do not create `PlayerOnlyGrassInteraction`.

---

# 22. Influence depends on movement

A stationary body inside the grass should create far less disturbance than a fast-moving one.

Conceptually:

```text
Influence =
DistanceFalloff
× InfluencerVelocity
× InfluenceStrength
```

Clamp the result.

Examples:

```text
slow swimmer
→ gentle bend

fast swimmer
→ strong bend

large fast creature / bell
→ wider / stronger disturbance
```

Exact response should remain stylized and tunable.

---

# 23. Influence direction

Nearby moving bodies should push/bend grass broadly in their direction of motion.

Example:

```text
player swims right
↓
nearby grass receives rightward reactive bend
```

Do not require physically exact collision normals.

The goal is convincing responsive vegetation.

---

# 24. Recovery / spring behavior

Reactive bend should not snap instantly back to zero.

Maintain a smoothed reactive state conceptually equivalent to:

```text
ReactiveBendCurrent
ReactiveBendTarget
ReactiveVelocity
```

Then use a spring/damping model or another smooth equivalent.

Desired feel:

```text
body passes through
↓
grass bends
↓
slight lingering motion / optional small overshoot
↓
settles back into ambient current sway
```

Do not build a cloth solver.

---

# 25. Ambient and reactive bend combine

Final deformation should conceptually combine:

```text
FinalBend =
AmbientCurrentBend
+
OrganicSway
+
ReactiveInfluenceBend
```

Clamp to sane visual limits.

Reactive influence should layer on top of the existing underwater sway rather than replacing it entirely.

---

# 26. Influence manager

Do not make every grass clump perform broad scene searches or physics overlap queries every frame.

Preferred architecture:

```text
VegetationInfluenceManager
├─ registered influencers
└─ provides nearby influencer data / compact shared state
```

Grass/clumps query or receive only relevant nearby influence.

For the POC, optimize for correctness first but preserve this boundary.

---

# 27. Initial influencer count

A grass clump only needs to respond to a small number of nearby meaningful influencers.

Recommended starting limit:

```text
nearest 1–2 relevant influencers
```

This is enough for:

- one player,
- a nearby creature,
- a bell passing through.

Do not sum hundreds of world bodies.

---

# 28. Update cadence

Continuous shader sway may run every rendered frame.

Expensive CPU-side influencer selection/reactive-target calculation does **not** need to run every frame.

A starting cadence around:

```text
10–20 Hz
```

is acceptable for nearby influence sampling, with smoothed shader/runtime interpolation between updates.

Bosun should tune after profiling.

---

# 29. Visual-only in this pass

Reactive seagrass has no gameplay effect in this POC.

Do not add:

- player slowdown,
- drag,
- stealth modifiers,
- collision blocking,
- harvesting,
- creature pathing changes.

It is purely visual/reactive.

Future systems may use the same vegetation objects for gameplay.

---

# 30. No dense-patch batching yet

Individual clumps are accepted for the POC.

Do not prematurely implement:

- instanced vegetation fields,
- giant combined meshes,
- GPU influence textures,
- compute-shader grass simulation.

After visual approval and profiling, decide whether batching is needed.

---

# 31. Multiple influencers

The POC should prove at least:

```text
player influence
+
one second influencer
```

simultaneously.

The combined result should remain bounded and stable.

No violent infinite bend when two influencers overlap.

---

# 32. Large-object influence

The architecture must support large influencers such as the diving bell.

A large influencer may simply use:

```text
larger radius
higher strength
```

No separate bell-specific grass code.

---

# 33. Current-vs-player conflict

If current bends grass left while a player swims strongly right through it:

```text
ambient left bias
+
strong temporary right reactive bend
```

should produce a convincing temporary override, then return toward the current after the player passes.

Do not force one source to completely replace the other.

---

# 34. Presentation style

Target:

- stylized,
- smooth,
- readable,
- soft underwater motion,
- responsive but not rubbery,
- coherent with existing 2D art.

Avoid:

- hyper-real plant physics,
- jitter,
- rigid sprite rotation,
- synchronized metronome motion,
- excessive oscillation.

---

# 35. Multiplayer behavior

Knife:

- authority resolves actual melee hits,
- clients may render action-driven motion locally,
- action runtime identity prevents duplicate/stale hit resolution.

Seagrass:

- purely visual in this pass,
- does not need authoritative state replication,
- each client may derive grass deformation from replicated/local actor transforms/velocities.

Do not network per-grass bend state.

---

# 36. Performance guardrails

Knife:

- no allocation-heavy hit list every frame,
- no scene-wide target searches,
- clear per-swing hit cache efficiently.

Seagrass:

- no broad physics overlap per plant per frame,
- no unique runtime material per clump if shared shader/property mechanisms suffice,
- no CPU vertex rewriting for hundreds of plants every frame if shader deformation is available,
- no networking of vegetation state.

---

# 37. Debug visibility

## Knife

Expose developer info:

```text
Current Action Progress01
Motion Profile
Facing
Active Hit Window?
Previous Hit Sample
Current Hit Sample
Targets Hit This Swing
```

Optional Scene gizmos:

- motion sample path,
- active hit arc/window,
- current/previous hit sample.

## Seagrass

Expose:

```text
CurrentDirection
CurrentStrength
Ambient Bend
Reactive Bend
Selected Influencers
Influence Radius
Influence Strength
Recovery parameters
```

Optional gizmos:

- influencer radius,
- bend direction,
- selected influencer links.

---

# 38. Non-goals

Do not implement:

- arm/hand animation,
- full melee combo system,
- stamina cost for knife,
- blocking/parrying,
- creature melee reactions,
- procedural ragdolls,
- full ocean-current simulation,
- gameplay drag from seagrass,
- seagrass harvesting,
- dense field batching,
- compute-shader vegetation,
- collision-based cloth simulation,
- full animation-authoring editor.

---

# 39. Implementation checkpoints

## ANIMPOC.1 — Exact-project audit

Audit:

- Common Usable Item Action System implementation,
- held-item visual hierarchy,
- player facing/mirroring,
- sorting,
- current vegetation rendering,
- current/current-like environmental data,
- actor velocity access.

Document proposed motion child and grass deformation path.

No broad implementation yet.

**STOP and report.**

---

## ANIMPOC.2 — Generic runtime item motion player

Implement:

- `RuntimeItemMotionProfile`,
- reusable motion player,
- curve-driven local position,
- curve-driven rotation,
- optional scale,
- evaluation from `Progress01`,
- canonical right-facing authoring,
- left-facing mirroring,
- clean idle reset/cancel.

Use a non-damaging test swing first.

Compile/test.

**STOP and report.**

---

## ANIMPOC.3 — Knife action + swept melee POC

Implement:

```text
Knife
PrimaryAction = Swing
```

Add:

- `MeleeSwingActionExecutor`,
- authored active hit window,
- swept hit query,
- one-hit-per-target-per-swing,
- authority-safe hit seam,
- local visual action motion.

Damage amount may be a simple serialized test value.

Do not build the whole combat framework.

Compile/test.

**STOP and report.**

---

## ANIMPOC.4 — Ambient seagrass deformation

Implement one-clump reactive seagrass renderer with:

- rooted deformation,
- ambient sway,
- current direction/strength input,
- stable phase variation,
- shared shader/material path.

Test many nearby clumps for synchronized-motion regressions.

Compile/test.

**STOP and report.**

---

## ANIMPOC.5 — Vegetation influencer system

Implement:

- generic `VegetationInfluencer`,
- player registration,
- second test influencer,
- influence radius,
- velocity-driven bend,
- capped strength,
- manager/registry path.

No gameplay collision effects.

Compile/test.

**STOP and report.**

---

## ANIMPOC.6 — Recovery / multiple influence / large-object proof

Implement/tune:

- spring/damped recovery,
- ambient + reactive combination,
- multiple influencers,
- large-radius/high-strength influencer suitable for future diving bell use,
- bounded behavior.

Test player + second influencer passing through same clump.

Compile/test.

**STOP and report.**

---

## ANIMPOC.7 — Profiling + regression

Profile:

- dozens/hundreds of individual POC clumps as practical,
- influencer selection cadence,
- material instance counts,
- CPU update cost,
- shader cost.

Do not optimize into batching unless profiling proves necessary.

Regression-test:

- held item sorting,
- item equip/unequip,
- action cancellation,
- player facing,
- underwater player movement,
- existing water rendering.

**STOP and report.**

---

# 40. Acceptance tests

## Knife / runtime motion

1. Knife uses Common ItemAction Primary slot.
2. Knife swing duration comes from action definition.
3. Motion profile reads the shared `Progress01`.
4. No independent knife swing timer exists.
5. Knife visually follows authored local curves.
6. Right-facing profile works.
7. Left-facing player mirrors the same profile correctly.
8. Only the item moves; player arm/body is unchanged.
9. Cancel returns item to idle pose.
10. Unequip mid-swing leaves no frozen pose.

## Melee hit logic

11. Hit detection only runs in authored active window.
12. Fast swing uses swept query rather than point overlap only.
13. Target intersected during sweep is detected.
14. Same target receives at most one full hit per swing.
15. A new swing can hit the same target again.
16. Visual path and gameplay hit path broadly agree.
17. Authority, not client-only animation, owns consequential damage.

## Reusable motion architecture

18. A second profile can reuse the same motion player with different curves.
19. Different action duration changes playback speed without rewriting the profile/executor.
20. System does not contain knife-specific arc math in the generic motion player.

## Seagrass ambient motion

21. Grass root remains visually anchored.
22. Tip bends more than root.
23. Current direction biases grass.
24. Current strength scales bend.
25. Nearby clumps do not move in perfect synchronization.
26. Motion remains stylized/smooth.

## Reactive interaction

27. Player swimming near/through grass bends it.
28. Faster player movement creates stronger response than slow movement.
29. Stationary player produces much less reactive disturbance.
30. Grass gradually recovers after player passes.
31. Recovery does not snap.
32. Ambient sway resumes underneath/after reactive motion.

## Multiple influencers

33. A second influencer affects the same grass system.
34. Player + second influencer can overlap without instability.
35. Large influencer radius/strength works for a bell-like object.
36. No player-specific logic is required inside grass.

## Performance

37. Grass does not perform scene-wide searches every frame.
38. Individual clumps do not require unique material instances unnecessarily.
39. CPU influence work may run below render-frame cadence.
40. Vegetation state is not replicated over network.

## Regression

41. Existing held-item sorting remains correct.
42. Player facing remains correct.
43. Item equip/unequip remains correct.
44. Common action cancellation remains correct.
45. Existing underwater movement remains unchanged.
46. Existing water visuals remain unchanged.

---

# 41. Forward contract: melee weapons

Future melee weapons should primarily require:

```text
ItemActionDefinition
RuntimeItemMotionProfile
Melee weapon stats
Sprite/audio/VFX
```

Possible examples:

```text
knife
sword
hammer
spear
wrench
```

Do not require a unique animation script for each weapon.

---

# 42. Forward contract: body animation

Future player arms/hands may consume the same action progress.

Conceptually:

```text
Action Progress01
├─ held-item motion
└─ future arm/body pose layer
```

The current POC does not implement that layer.

---

# 43. Forward contract: vegetation batching

If individual clumps become too expensive later, preserve these semantics:

```text
ambient current
stable phase variation
local influencer response
spring recovery
```

A future batched/instanced implementation may move influence evaluation onto:

- shared patch data,
- material property arrays,
- influence textures,
- GPU/instancing paths.

Do not sacrifice behavior merely to batch prematurely.

---

# 44. Forward contract: environmental disturbance

The generic influencer concept may later be fed by:

- players,
- creatures,
- diving bell,
- moving machinery,
- current surges,
- explosions,
- large environmental impulses.

This POC only proves moving-body interaction.

---

# 45. Final principle

This pass should make these statements true:

> A knife swing can be authored as runtime motion driven entirely by the Common ItemAction clock.

> The same motion system can animate a sword, hammer, thrust, recoil, or tool gesture without becoming knife-specific.

> A left-facing player does not require duplicate animation profiles.

> A fast blade cannot tunnel through a target simply because it moved too far between frames.

> Seagrass looks alive even when nobody is touching it.

> When a player, creature, or diving bell moves through the grass, it responds to their movement and then naturally settles back into the ambient current.

> Neither POC needs a giant universal animation framework to prove its value.

---

**End of Runtime Item Animation + Reactive Seagrass POC handoff.**
