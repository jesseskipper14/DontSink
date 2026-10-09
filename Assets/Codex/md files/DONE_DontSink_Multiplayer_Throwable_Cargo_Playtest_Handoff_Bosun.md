# Don't Sink — Mini Multiplayer Playtest Pass: Throwable Cargo

**Implementation owner:** Bosun  
**Architecture/spec owner:** Keel  
**Project:** *Don't Sink*  
**Engine:** Unity 6.0, URP 2D, C#  
**Pass type:** Small multiplayer/gameplay prototype  
**Goal:** Make currently hand-held/droppable items throwable so cargo can physically collide with players and create chaotic multiplayer interactions  
**Explicit non-goal:** Damage, health, hit reactions, bespoke knockback, or combat logic

---

# 0. Read This First

This is intentionally a **small pass**.

The fantasy is simple:

> Player picks up a crate, holds Q, aims at their friend with the mouse, releases Q, and Unity physics commits the crime.

Do not turn this into a combat framework.

Before changing code, audit the current repository and identify the exact current path for:

- "Press Q to Drop" while carrying an item,
- held-item ownership/state,
- world-item release/drop,
- Rigidbody2D restoration when an item leaves the player's hands,
- item collider/layer restoration,
- Player collision layers/colliders,
- player motor Rigidbody behavior,
- multiplayer requester/authority validation for held items,
- current local-player mouse/world aiming utilities,
- local camera lookup,
- item/Player collision matrix.

Use the exact current classes.

> **Never reconstruct an existing class from memory or an old handoff.**

---

# 1. Core Behavior

Anything that currently participates in the existing:

```text
Press Q to Drop
```

hand-held-item path should also be throwable.

Do NOT introduce a new `Throwable` item category unless the current repo exposes a genuine gameplay reason that requires one.

Conceptually:

```text
Q tap
-> normal existing drop

Q hold
-> enter throw charge / aim mode
-> show local trajectory preview
-> continue holding to increase throw power
-> release Q
-> authoritative throw toward mouse
```

Secured, stowed, nested, inventory-contained, or otherwise non-held items are not throw candidates until they are actually in the existing held/carry state.

---

# 2. Input Semantics

The existing Q drop behavior must survive.

Because tap and hold share Q, do NOT immediately execute Drop on Q-down.

Recommended behavior:

```text
Q DOWN
-> begin pending drop/throw input

release before HoldThreshold
-> execute existing Drop path

hold beyond HoldThreshold
-> enter Throw Aim mode
-> display throw preview
-> charge power while Q remains held

Q RELEASE after threshold
-> request throw
```

Suggested starting values, all tunable:

```text
HoldThreshold        ~0.20 sec
FullChargeDuration   ~0.80 sec after throw mode begins
MinimumThrowSpeed    configurable
MaximumThrowSpeed    configurable
```

Do not treat these exact numbers as sacred.

The user will tune feel during the playtest.

---

# 3. Aim

Throw toward the **local player's mouse cursor in world space**.

Conceptually:

```text
aimDirection =
    (mouseWorldPosition - heldItemReleasePosition).normalized
```

Use the current local-player camera/context architecture.

Do not use a global/shared camera assumption.

Do not replicate mouse position or the trajectory preview.

Only the eventual throw intent/state needs multiplayer authority.

---

# 4. Throw Strength Is Intentionally Unrealistic

This is a fun multiplayer physics feature, not a strength simulator.

A player should be able to throw a heavy crate across a meaningful portion of the boat and hit another player.

Therefore:

> **Do not use a fixed impulse that makes heavy cargo barely leave the player's hands.**

Prefer a **target launch velocity / delta-V model** whose launch speed is primarily controlled by charge, not by item mass.

Conceptually:

```text
charge01 = 0..1

throwSpeed =
    Lerp(MinimumThrowSpeed,
         MaximumThrowSpeed,
         ThrowChargeCurve(charge01))

desiredThrowVelocity =
    aimDirection * throwSpeed
```

Then apply physics in a mass-correct way, for example:

```text
impulse = rb.mass * desiredDeltaV
```

or set/add velocity through the project's current authoritative physical-item pathway.

Result:

- light item can be thrown far,
- heavy crate can ALSO be thrown far,
- heavy crate carries much more momentum because it has much more mass,
- collisions naturally become more chaotic.

That unrealistic strength is intentional.

Do not "fix" it for realism.

---

# 5. Inherited Motion

Thrown items must inherit existing world motion.

At release:

```text
final velocity =
    inherited carrier/world velocity
    + charged throw velocity
```

This matters especially aboard a moving boat.

Do not make the universe forget the boat was moving when the player releases an object.

Audit how boarded players and held objects currently obtain world velocity.

Use the current architecture rather than guessing that `playerRb.velocity` alone contains the full correct motion.

---

# 6. Release Must Use the Canonical Existing Item Path

Throwing is a specialized release, not a second item lifecycle.

Reuse the current authoritative held-item release/drop machinery for:

- item ownership release,
- container/hand state cleanup,
- hierarchy/parent cleanup,
- Rigidbody2D activation,
- collider restoration,
- world-item state,
- persistence identity,
- multiplayer ownership/state,
- any existing bookkeeping.

Then apply the throw velocity/impulse.

Do not duplicate Drop logic in a Throw implementation.

No item duplication.

No item deletion.

No stale "still held" state after throw.

---

# 7. Thrower Collision Grace

A held object's collider may overlap the throwing player's body at the release frame.

Prevent the object from immediately exploding out of its thrower due to overlap resolution.

Use a **thrower-only collision grace**.

Preferred semantics:

```text
release item
-> ignore collision ONLY between this thrown item and the throwing player
-> restore collision once the item has clearly separated
```

Include a short safety timeout so collision cannot remain ignored forever if separation detection fails.

Starting timeout may be roughly:

```text
0.15–0.30 sec
```

but tune based on current collider sizes.

Important:

- do NOT disable collision with every player,
- other players should be hittable immediately,
- do NOT globally change Player/WorldItem collision during grace.

---

# 8. Cargo vs Player Collision

The feature should use **real Unity physics collision**.

There should be no gameplay code like:

```text
OnCargoHitPlayer()
ApplyKnockback()
TakeThrowDamage()
```

for this pass.

Desired behavior:

```text
thrown Rigidbody2D item
        +
real Player collider/Rigidbody2D
        =
Unity physics response
```

No damage.

No stun.

No health loss.

No special hit state.

If a crate:

- knocks someone backward,
- pushes them off the deck,
- sends them down a ladder opening,
- bumps them into the water,

that is the intended showcase.

---

# 9. Audit Player Physics Carefully

Bosun must verify that the current player motor does not erase collision response every physics tick.

Potential failure mode:

```text
crate collides with player
-> Box2D applies impulse
-> player motor immediately overwrites Rigidbody velocity
-> visually nothing happens
```

If the existing player motor already preserves external Rigidbody impulses, leave it alone.

If it stomps them, make the **smallest architecture-safe adjustment** necessary so normal Unity collision impulses can influence the player.

Do NOT replace this with bespoke cargo knockback.

The point of the pass is specifically to let shared physics produce the interaction.

Preserve normal movement feel as much as possible.

---

# 10. Collision Layers

Audit the existing Physics2D layer matrix.

If the current world-item layer already physically collides with Player:

- use it,
- do not add another layer.

If current items intentionally do NOT collide with Player:

- choose the narrowest safe way to permit thrown-item/player physical collision,
- preserve held-item self-collision safety,
- avoid globally destabilizing every resting cargo object unless that is already compatible with current gameplay.

Do not blindly create a `ThrownItem` layer before understanding the current layer architecture.

The acceptance criterion is simply:

> A thrown physical item can physically collide with another player's real collider.

---

# 11. Throw Charge Preview

Once Q has crossed the throw-hold threshold, show a small **local-only ballistic arc preview**.

Purpose:

- communicate aim,
- communicate increasing power,
- make throwing fun and readable,
- help the user certify the physics behavior during the multiplayer playtest.

The preview is presentation only.

Do not replicate it.

## 11.1 Preview inputs

The preview should approximate the actual launch using:

```text
held item release position
mouse aim direction
current charge
minimum/maximum throw speed
inherited world velocity
Physics2D gravity
item gravityScale
```

A basic ballistic sample is sufficient:

```text
p(t) =
    origin
    + initialVelocity * t
    + 0.5 * gravity * gravityScale * t^2
```

Draw a short series of dots/segments.

No need for a sophisticated trajectory package.

## 11.2 Preview collision prediction

Collision prediction is optional.

If it is cheap and fits current architecture, the preview may stop at the first likely world collision.

If doing so becomes complicated due to moving boat geometry, rotating colliders, water, dynamic cargo, or multiplayer state, skip it.

An approximate ballistic arc is enough for v1.

🍌 Banana check: it is an aiming hint, not artillery fire-control software.

---

# 12. Charge Feedback

The arc should visually communicate increasing strength while Q is held.

Possible simple signals:

- trajectory extends farther,
- dots/line length grows,
- small charge bar near cursor/player,
- endpoint moves.

Use the simplest presentation consistent with current UI style.

Do not build a large throw HUD.

---

# 13. Multiplayer Authority

Use the CURRENT multiplayer/requester/authority architecture found in the repo.

Do not create a second networking path.

Conceptual flow:

```text
LOCAL PLAYER
Q hold + mouse aim
-> local preview / charge feedback

Q release
-> throw intent/request

AUTHORITATIVE HOST/OWNER
-> verify requester is allowed to act
-> verify requester actually holds that exact item
-> clamp/validate charge
-> release item through canonical item path
-> apply authoritative launch velocity/impulse

REPLICATED PHYSICS/ITEM STATE
-> all players see the thrown object
-> physical collisions occur
```

The local trajectory preview is not authoritative.

## 13.1 Charge authority

Do not overbuild anti-cheat for this playtest.

If current networking architecture supports authoritative press/release timing cleanly, use it.

Otherwise it is acceptable for the client request to include:

```text
aimDirection
charge01
heldItemIdentity
```

with the authoritative side:

- validating the item is actually held by that requester,
- clamping charge to `0..1`,
- applying server-configured throw speed limits.

Trusted co-op playtest > competitive anti-cheat engineering.

---

# 14. Input / Lifecycle Safety

Throw charge/preview must clean up safely if:

- item stops being held,
- player dies,
- player is disconnected/despawned,
- scene changes,
- UI/context steals the input,
- cartridge/interaction mode disables normal player controls,
- authoritative request is rejected.

Do not leave:

- trajectory preview stuck on screen,
- collision grace permanently active,
- an item locally released but authoritatively still held,
- stale charge state.

Follow existing input/context ownership patterns.

---

# 15. No Damage Yet

Absolutely out of scope:

- HP loss,
- injury,
- concussion,
- stun,
- ragdoll state,
- cargo damage,
- item durability loss,
- friendly-fire settings,
- kill attribution,
- damage numbers,
- combat logging.

The item may move the player only because physics moved the player.

That is enough.

---

# 16. Suggested Tunables

Use the current project's normal configuration style.

Likely useful tunables:

```text
throwHoldThreshold
fullChargeDuration
minimumThrowSpeed
maximumThrowSpeed
throwChargeCurve
throwerCollisionGraceMaxSeconds
previewDuration
previewSampleCount
```

Do not expose twenty knobs for a mini playtest feature.

---

# 17. Required Playtest Scenarios

## A. Tap Q still drops

Hold a normal item.

Tap Q quickly.

Expected:

- current ordinary drop behavior remains materially unchanged,
- no accidental throw.

## B. Hold Q enters throw mode

Hold Q past threshold.

Expected:

- item remains held,
- local trajectory preview appears,
- preview points toward mouse,
- continued hold increases predicted range until capped.

## C. Release throws toward mouse

Aim forward/up/down.

Release Q.

Expected:

- item leaves hands once,
- physical object launches along expected arc,
- no duplicated item,
- no stale held state.

## D. Heavy crate goes satisfyingly far

Throw a high-mass crate at full charge.

Expected:

- crate can cross a meaningful portion of the boat,
- mass does not reduce launch to a pathetic two-foot plop,
- high mass still produces appropriately large momentum on impact.

This intentional unrealism is a feature.

## E. Moving boat inheritance

Throw while boat is moving.

Expected:

- item inherits relevant existing world motion,
- trajectory does not behave as if the boat were stationary.

## F. Thrower grace

Throw from close to body.

Expected:

- no instant violent self-collision,
- collision with thrower restores shortly after separation.

## G. Hit another player

Player A throws crate at Player B.

Expected:

- crate physically contacts B,
- Unity physics produces movement/response,
- no damage occurs,
- Player A's collision grace does not protect Player B.

## H. Chaotic environmental consequence

Throw cargo at a player near a deck edge/opening.

Expected:

- normal physics may move them into/out of local geometry,
- no special scripted cargo-hit response is required.

## I. Multiplayer authority

Client throws an item they are legitimately holding.

Expected:

- host-authoritative release/launch,
- other players observe same thrown item,
- no duplicate/desynced held copy.

Attempt request for item not held by requester.

Expected:

- authoritative rejection,
- no state corruption.

## J. Cancel/lifecycle

Begin charging, then lose held item/context.

Expected:

- preview disappears,
- charge clears,
- no ghost throw.

---

# 18. Explicitly Out of Scope

Do NOT bundle:

- damage,
- health,
- combat system,
- ragdolls,
- stun,
- cargo breakage,
- object durability,
- throw animations beyond any tiny existing-compatible presentation,
- catch mechanic,
- inventory throw-from-slot,
- AI throwing,
- throw skill/stat system,
- item-specific throw classes,
- realistic human strength,
- trajectory networking,
- complex collision-aware arc solving,
- aim assist,
- weapons architecture.

---

# 19. Architectural Smells to Avoid

Avoid:

```text
if (crateHitsPlayer)
    player.Knockback(...)
```

Avoid:

```text
if (item.mass > arbitraryValue)
    itemCannotBeThrown
```

unless the CURRENT carry system already prohibits holding that object.

Avoid duplicating the current Drop lifecycle.

Avoid global camera assumptions for mouse aiming.

Avoid locally releasing an item before authoritative multiplayer acceptance if that can create desync.

Avoid making trajectory preview authoritative.

Avoid a new combat/damage subsystem.

---

# 20. Final Design Statement

This feature is deliberately simple:

> **If the player can currently hold it and press Q to drop it, they can hold Q to charge a throw and release it toward the mouse.**

The throw should feel playful and stronger than a real human.

Heavy cargo should still fly far enough to hit a friend across the boat.

Its high mass then makes the resulting collision naturally more consequential.

The game does not need to understand that someone was "hit by cargo."

Unity physics already understands that two masses collided.

Let it.

---

**End of mini Bosun handoff.**
