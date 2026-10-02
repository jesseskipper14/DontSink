# Don't Sink — Multiplayer-Safe Camera Hardening Pass

**Implementation owner:** Bosun  
**Architecture/spec owner:** Keel  
**Project:** *Don't Sink*  
**Engine:** Unity 6.0, URP 2D, C#  
**Pass type:** Architecture hardening / multiplayer preparation  
**Priority:** High, prerequisite for dynamic BoatScene generation  
**Networking status:** Transport/package not implemented yet  
**Multiplayer target:** One shared boat, host-authoritative shared simulation, per-player local presentation

---

## 0. Bosun: Read This First

This is an **audit-first** pass.

Do **not** reconstruct camera classes from prior context, assumptions, filenames in this document, or remembered code. Work from the **current repository**.

Before modifying code:

1. Audit the current camera/player/piloting/spectator/scene-loading architecture.
2. Identify every class that:
   - owns or moves a gameplay camera,
   - assumes a single global camera,
   - references `Camera.main`,
   - searches globally for the player/camera,
   - changes camera target/context,
   - handles unconscious/death/spectator behavior,
   - opens/closes piloting cartridges or other local presentation overlays,
   - performs scene-load camera initialization,
   - applies or is likely to apply camera shake/effects.
3. Identify static/singleton/global assumptions that would break with multiple players.
4. Summarize the proposed modification set **before implementation**.
5. Then implement the smallest coherent architecture that satisfies this document while preserving current gameplay behavior.

### Exact-current-class rule

This project has a strict rule:

> **Never reconstruct an existing class from memory or from an older copy. Use the exact current repository version.**

New classes may be authored freely.

Preserve unrelated behavior. Do not rewrite working systems merely because a broader refactor looks aesthetically pleasing.

---

# 1. Core Camera Rule

There is exactly one primary gameplay camera **per local player**.

The camera is **always player-centric**.

The player may be:

- walking around the boat,
- inside a room,
- swimming far from the boat,
- inside/deployed with the diving bell,
- interacting with a local UI/cartridge,
- unconscious/dead,
- spectating another player,
- using free spectator mode.

The camera follows **that player's local context**.

No other player's activity may hijack, reposition, reframe, disable, or otherwise control another player's camera.

The host is not special from a camera standpoint. The host also has a local player and a local camera.

---

# 2. Design Intent

Large boats are planned.

The camera must make the player feel like they are interacting with **rooms and local spaces on a boat**, not viewing the whole vessel as one object.

Therefore:

- Do not make the camera boat-centric.
- Do not zoom out merely to keep the whole boat visible.
- Do not frame around the combined bounds of multiple players.
- Do not average player positions.
- Do not create a "party camera."
- Do not tether one player's camera to another player's movement.

The camera follows the local player and adapts only to **that player's current presentation context**.

---

# 3. Multiplayer Authority Boundary

Camera state is local presentation.

It must **not** become simulation authority.

The intended broad architecture remains:

```text
Players provide intent
Objects own state
Simulations advance state
UI/cameras observe and present
```

For networking:

```text
client input
    ->
authenticated requester intent
    ->
host-authoritative gameplay simulation
    ->
replicated gameplay state
    ->
each client renders its OWN camera locally
```

Camera transform, local look-ahead, shake, zoom, spectator free-fly position, cartridge framing, and similar presentation values should **not** be host-authoritative gameplay state.

Do not replicate a shared camera transform.

Do not make gameplay simulation depend on a camera transform.

Do not use the camera as a source of authoritative world position.

---

# 4. World Position / Infinite-World Requirement

The camera has **no practical world bounds**.

If the local player reaches:

```text
x = 12,389,123
```

the camera should simply be there with them.

The upcoming dynamic BoatScene work will allow very large / effectively unbounded travel.

Therefore the camera architecture must not assume:

- world origin is meaningful,
- the player remains near `(0,0)`,
- hardcoded scene extents,
- authored camera boundary boxes,
- a finite horizontal BoatScene,
- a special "central boat" location.

If the current implementation contains explicit camera bounds that are only legacy constraints, remove or abstract them where safe.

Do **not** implement dynamic world generation in this pass.

This pass merely ensures the camera will not fight it.

---

# 5. Required Per-Player Context Behavior

A local camera needs a clean way to respond to the local player's current context.

The implementation may use a state machine, context stack, provider/interface architecture, or another clean pattern appropriate to the current repo.

Do not force a specific class name from this document.

Conceptually, the system must support at least these contexts:

## 5.1 Normal Player Follow

Default gameplay.

Camera follows the local player.

Current camera feel, smoothing, offsets, framing, and local behavior should remain unchanged unless they are fundamentally incompatible with multiplayer.

## 5.2 Boat Interior / Large Boat

Still normal player-follow.

Do not switch to whole-boat framing.

The boat may become very large later.

A player in a cabin should feel like they are in that cabin, not watching a ship-management diagram.

## 5.3 Free Swimming / Underwater Excursion

If Player A swims 100+ meters away from the boat:

- Player A's camera follows Player A.
- Player B's camera is unaffected.
- No camera attempts to keep both players visible.
- No boat-centered camera constraint pulls Player A back visually.

This must remain true at extreme navigation/world positions.

## 5.4 Diving Bell

If Player A enters or travels with the diving bell:

- Player A's camera may adopt whatever existing bell/interior presentation is currently appropriate.
- Player B, who remains aboard the boat, keeps their own ordinary player-centric camera.
- Bell state must never globally change "the camera."
- Any bell-specific camera behavior must be scoped to the local player using/occupying that context.

Do not alter diving-bell gameplay or authority behavior unless required to remove camera coupling.

## 5.5 Piloting Cartridge / Local Overlay Context

The piloting cartridge is **local presentation for the player who is interacting with it**.

Example:

```text
Player A uses helm cartridge
    ->
Player A sees piloting view

Player B is elsewhere on boat
    ->
Player B sees whatever they are locally doing
```

Opening/closing a cartridge must not alter another player's camera or UI presentation.

The cartridge itself remains presentation-only. Authoritative navigation state remains elsewhere.

No need to implement multi-viewer piloting UI in this pass unless the current repo already supports it.

---

# 6. Spectator / Death Camera

This pass should establish multiplayer-safe spectator architecture.

When the local player is dead:

They should be able to choose between:

1. **Follow Living Player**
2. **Free Spectator**

## 6.1 Follow Living Player

The dead player's local camera may follow another living player.

Requirements:

- This is local presentation only.
- Following another player does not grant gameplay control.
- It does not alter the followed player's camera.
- The spectator can switch living-player targets.
- The architecture should tolerate players dying/disconnecting/despawning while being spectated.

## 6.2 Free Spectator

The dead player may enter a free camera mode.

Requirements:

- Local only.
- No effect on simulation.
- No collision/physics authority unless the current spectator design intentionally uses one.
- Must not move or possess any gameplay object.
- Must not become a source of authoritative position.

If a full polished spectator control scheme would substantially enlarge this pass, prioritize the **architecture and clean target-switch seam** while preserving any existing spectator behavior.

But the end-state architecture must clearly support both modes.

---

# 7. Scene Loading / Player Spawn

Each player should spawn with or acquire **their own local camera**.

Clarification of intent:

- Do not assume there is one global player object.
- Do not assume there is one global gameplay camera.
- Do not bind by "first Player found in scene."
- Do not bind by arbitrary `FindObjectOfType<Player>()` style behavior when multiple players can exist.
- Do not use a shared singleton target that all cameras would overwrite.

After a scene transition or player spawn, the local camera should bind to the **correct local player** through an explicit/local-player-aware mechanism.

Since networking transport is not implemented yet, use a clean seam that works in single-player today and can later be supplied with authenticated/local-player identity by the networking layer.

Do not invent a fake networking stack just to solve this pass.

---

# 8. Local Camera Effects Seam

Create or harden a **local camera effects seam** for future physical impacts.

We are **not** implementing the large physical impulse/damage/shake pass now.

We are preparing the API boundary so future systems can request effects without touching global camera state.

Conceptually:

```text
local gameplay event / presentation event
    ->
local camera effects service/controller
    ->
that local player's camera only
```

Future examples:

```text
wave impact
collision
explosion
bell impact
damage event
```

Possible future operations may include:

```text
AddShake(...)
AddImpulse(...)
SetTemporaryOffset(...)
```

Exact API names are up to the implementation after repository audit.

### Important

A shared world event may eventually cause each nearby player to receive a **different local camera effect** based on distance/context.

Therefore do not design:

```text
GlobalCamera.Shake(...)
```

or any static mutable camera-effect state.

The seam should be instance/local-player scoped.

Do not add arbitrary shake now merely to demonstrate the API.

---

# 9. Preserve Current Camera Feel

Behavioral rule:

> **Current camera behavior should remain perceptually the same unless a behavior is fundamentally multiplayer-unsafe.**

Architecture may change substantially underneath.

Preserve where possible:

- follow smoothing,
- offsets,
- current player framing,
- underwater behavior,
- bell behavior,
- existing unconscious/spectator presentation,
- cartridge behavior,
- current look-ahead logic,
- current zoom behavior.

Do not turn this into a camera-design pass.

Do not retune camera feel unless required to preserve it after refactor.

---

# 10. Global / Static Coupling Audit

Bosun should explicitly search for and review:

```text
Camera.main
FindObjectOfType<Camera>
FindFirstObjectByType<Camera>
FindAnyObjectByType<Camera>
FindObjectOfType<Player>
FindFirstObjectByType<Player>
static camera references
singleton camera managers
global camera target fields
global shake state
scene objects named "Main Camera"
code that enables/disables all cameras
code that changes AudioListener globally
code that assumes exactly one AudioListener
code that changes Cinemachine/global camera state, if present
```

Do not blindly eliminate every `Camera.main` usage in the project.

Classify each use:

- harmless single-player/editor/UI usage,
- definitely multiplayer-unsafe,
- should be routed through a local-player camera context,
- unrelated to this pass.

Only change what is appropriate.

---

# 11. AudioListener Consideration

Audit AudioListener ownership while doing this pass.

Multiple local/remote player camera prefabs can easily create the classic Unity disaster:

> "There are 2 audio listeners in the scene."

The intended direction is:

- only the active local presentation camera/listener should render local audio,
- remote player camera/listener components should not become active presentation listeners on this client.

Do not redesign the entire audio system.

Just ensure the camera architecture does not guarantee duplicate listeners later.

Document any remaining audio-listener integration requirement.

---

# 12. Split-Screen Is NOT the Current Goal

The requirement is **one camera per local player context**, but this should not be interpreted as a request to implement local split-screen unless the existing project already supports multiple local human players on one machine.

Primary multiplayer target remains networked co-op.

Architectural rule:

- camera ownership must be per local player,
- remote players must not create active local gameplay cameras.

If the current repo has no local-player abstraction yet, establish the narrowest clean seam necessary.

---

# 13. Host-Authoritative Multiplayer Compatibility

The future multiplayer model is:

```text
one shared boat
host-authoritative simulation
clients send intents
replicated state renders locally
```

Camera hardening should respect that.

Examples:

- A remote player's dive does not move the local camera.
- A remote player's cartridge use does not open local UI.
- A remote player's death does not enter local spectator mode.
- A remote player's bell boarding does not change local camera context.
- A replicated impact event may later result in a local shake request, but the camera transform itself is not replicated.

---

# 14. Recommended Architectural Shape

Use current repository patterns where possible.

A likely healthy conceptual split is:

```text
Local Player Identity / Presentation Owner
        |
        v
Local Camera Controller
        |
        +--> current follow target
        +--> current presentation context
        +--> camera effects
        +--> spectator state
```

Potential context providers:

```text
normal player
diving bell
cartridge
spectator-follow
spectator-free
```

This is conceptual only.

Do not create interfaces/classes just because they appear in a diagram.

Prefer the smallest architecture that creates the correct ownership boundary.

🍌 **Banana check:** if this starts becoming a general-purpose cinematic camera framework, stop.

---

# 15. Dynamic BoatScene Compatibility

The next major feature after this pass is dynamic BoatScene generation.

This camera pass should leave that system with a simple assumption:

> "Generate/recycle world around relevant players. Cameras will follow their local players wherever they are."

Avoid adding camera architecture that depends on:

- a fixed BoatScene center,
- a finite world rectangle,
- one shared player position,
- a single global camera's viewport as authoritative simulation input.

It is acceptable for dynamic generation to later query local or authoritative player positions.

It should not need to query camera position to determine gameplay truth.

---

# 16. Out of Scope

Do **not** implement these unless a tiny compatibility fix is unavoidable:

- networking transport/package,
- network spawning,
- RPCs,
- replication,
- dynamic BoatScene chunk generation,
- dynamic resource generation,
- real star-map UI,
- celestial matching puzzle,
- weather/cloud obstruction for star observation,
- physical storm impulse pass,
- damage-driven camera shake,
- cinematic camera system,
- split-screen support,
- camera tuning/polish,
- whole-boat framing,
- party/group camera.

---

# 17. Required Validation Scenarios

At minimum, validate behavior conceptually and in available play-mode/editor tests.

If the repository has no multiplayer runtime yet, simulate the architecture with multiple player/camera instances where practical.

## Scenario A — Two Players, Same Boat

Player A walks left.

Player B walks right.

Expected:

```text
Camera A follows A
Camera B follows B
```

Neither camera reframes toward the other player or whole boat.

## Scenario B — One Player Dives

Player A swims far underwater / far from the boat.

Player B remains aboard.

Expected:

```text
Camera A follows A
Camera B remains with B
```

No global context change.

## Scenario C — Diving Bell

Player A boards/uses deployed diving bell.

Player B remains elsewhere.

Expected:

```text
A receives bell-appropriate local camera context
B remains unchanged
```

## Scenario D — Piloting Cartridge

Player A engages helm/piloting cartridge.

Player B continues normal gameplay.

Expected:

```text
A gets cartridge/piloting presentation
B's presentation remains untouched
```

## Scenario E — Huge Coordinates

Place/move local player to a very large coordinate, e.g.:

```text
x = 12,389,123
```

Expected:

- local camera remains correctly attached,
- no camera bounds fight the player,
- no return-to-origin behavior,
- no overflow/obvious precision bug introduced by camera code.

Do not solve general Unity floating-point world-origin problems in this pass.

## Scenario F — Death / Spectator

Kill local Player A.

Expected architecture:

- A can enter spectator-follow mode and select a living player.
- A can switch to free spectator mode.
- B's camera is unaffected.
- spectating B does not control B.

## Scenario G — Camera Effects Isolation

Trigger a test/local camera effect if a safe development-only path exists.

Expected:

```text
effect requested for Player A camera
    ->
A camera affected
B camera unaffected
```

If no safe test trigger exists, unit-test/inspect the seam without adding permanent gameplay behavior.

## Scenario H — Scene Reload / Transition

Transition scenes or recreate player objects using the project's current lifecycle.

Expected:

- local camera binds to the correct local player,
- no "first player found" ambiguity,
- no duplicate active local camera ownership,
- no stale target from previous scene.

---

# 18. Performance Expectations

This pass should not add meaningful per-frame global searches.

Avoid:

```text
FindObjectOfType every Update
FindObjectsOfType every Update
repeated hierarchy scans every frame
```

Target/context changes should be event/state-driven where practical.

Camera following itself can of course update every frame.

---

# 19. Deliverables

Bosun should provide:

1. **Audit summary**
   - current camera architecture,
   - current player-binding approach,
   - multiplayer-unsafe assumptions found,
   - affected classes.

2. **Implementation plan**
   - exact existing classes to modify,
   - any new classes/interfaces/services,
   - why each change is necessary.

3. **Implementation**
   - preserve current behavior,
   - remove global camera ownership assumptions,
   - establish per-local-player camera context,
   - establish spectator target/free-camera seam,
   - establish local camera-effects seam,
   - remove inappropriate world-bound assumptions.

4. **Validation**
   - compile,
   - run relevant tests/play-mode checks available in repo,
   - explicitly report which acceptance scenarios were actually tested versus reasoned about.

5. **Post-pass notes**
   - any remaining multiplayer risks,
   - anything intentionally deferred,
   - anything dynamic BoatScene generation should know.

---

# 20. Acceptance Criteria

This pass is complete when all of the following are true:

- [ ] Every local player owns/receives an independent gameplay camera context.
- [ ] The camera remains player-centric.
- [ ] No camera tries to frame the whole boat or all players.
- [ ] Remote player actions cannot hijack local camera state.
- [ ] Free swimming is independently camera-safe.
- [ ] Diving-bell context is independently camera-safe.
- [ ] Piloting cartridge presentation is local-player scoped.
- [ ] Camera logic has no practical world-position bounds.
- [ ] Camera/player binding does not rely on "first player in scene."
- [ ] Camera transform is not gameplay authority.
- [ ] Camera transform is not intended replicated shared state.
- [ ] A local camera-effects seam exists for future shake/impacts.
- [ ] Dead-player camera architecture supports following another living player.
- [ ] Dead-player camera architecture supports free spectator mode.
- [ ] Spectator behavior remains local presentation.
- [ ] Scene transitions/rebinding are compatible with multiple players.
- [ ] AudioListener ownership has been audited for future multiple-camera safety.
- [ ] Current camera feel remains materially unchanged.
- [ ] No dynamic world generation was bundled into this pass.
- [ ] No fake networking layer was created.
- [ ] No unrelated gameplay systems were refactored without necessity.

---

# 21. Architectural Invariants to Preserve

The following project-wide rules still apply:

```text
Players provide intent.
Objects own state.
Simulations advance state.
UI observes it.
```

For camera specifically:

```text
player gameplay state
    !=
camera presentation state
```

And:

```text
local player context
    ->
local camera

never:

remote/global activity
    ->
everyone's camera
```

Finally:

> **The camera follows the player. The world does not revolve around the camera.**

That distinction becomes extremely important once dynamic BoatScene generation, very large boats, multiplayer, diving, and celestial navigation all coexist.

---

# 22. What Comes After This Pass

Planned major sequence:

```text
Multiplayer-safe camera
        ↓
Dynamic BoatScene generation
        ↓
Dynamic resource generation
```

The camera pass should make the next step easier, not attempt to implement it.

---

**End of handoff.**
