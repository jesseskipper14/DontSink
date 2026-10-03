# Don't Sink — Multiplayer Architecture & Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Primary implementation target:** Bosun / Codex  
**Purpose:** Convert the current multiplayer design decisions and prior architectural hardening into an implementation-ready plan that culminates in a real host + client multiplayer test.

---

# 0. Critical Coding Rule

## Exact-current-class rule

If modifying an existing class:

**Only use the exact latest version supplied in the active implementation thread.**

Do not reconstruct an existing class from memory.  
Do not use an older copy from prior chats, old exports, or library history.  
Do not assume an archived version is still current.

If an existing class is required and the current implementation thread does not contain the latest version, stop that specific modification and request the current file.

New classes may be authored freely.

After a coherent implementation pass is complete, prefer full drop-in replacement files or a ZIP containing the changed/new files.

This rule exists to prevent multiplayer work from silently undoing unrelated changes made in parallel.

---

# 1. Multiplayer Product Goal

Don't Sink is intended to support cooperative multiplayer centered around **one shared boat** and **one shared world simulation**.

The game should use a **host-authoritative simulation model**:

```text
player input / interaction
        ↓
client intent / request
        ↓
authenticated requester identity
        ↓
authoritative validation
        ↓
authoritative shared-state mutation
        ↓
replicated state / presentation
```

The first practical player-count target is **4 players**.

However, do not hard-code architecture around a maximum of 4 if it can reasonably be avoided. The design should remain friendly to mods or unusual sessions with much larger crews, such as a 30-player battleship-style mod.

This does **not** mean every system must be optimized for 30 players immediately. It means player identity, collections, station occupancy, replication state, persistence records, and session logic should not assume an array of exactly four humans.

---

# 2. Networking Topology

## Recommended initial model: listen server

The initial implementation should use a **listen-server architecture**.

The host process contains:

```text
Unity Host Process
├── authoritative shared simulation/server role
└── local host player/client presentation
```

Remote players connect as clients.

The host player should not be conceptually equivalent to "authority." Authority and player identity must remain separate concepts.

Do not build gameplay systems around assumptions like:

```text
if (this is the host player's character)
    then it owns the world
```

Instead:

```text
authority owns shared simulation
player identity owns a participant/avatar
```

## Dedicated server compatibility

Do **not** require a separate dedicated-server executable/process for the first implementation.

However, authoritative simulation code should remain sufficiently separable from local presentation that a future headless/dedicated server build is possible without rewriting the gameplay model.

Conceptually, a later deployment should be able to become:

```text
Headless Server
└── authoritative simulation

Clients
├── Player A
├── Player B
├── Player C
└── Player D
```

Host disconnect ends the session.

**No host migration is required.**

---

# 3. Existing Multiplayer Hardening Foundation

The diving-bell multiplayer hardening work established several general project-wide patterns.

## GameplayAuthority

`GameplayAuthority` is the current authority spine.

`GameplayAuthorityMode.SinglePlayerOrAuthoritative` currently maps to the peer allowed to perform shared authoritative simulation.

### Important rule

Do **not** globally authority-gate generic systems such as `ForceSystem`.

Player movement and other local/non-shared systems may use the same generic infrastructure.

Shared objects should opt into authority individually.

---

# 4. Completed Diving Bell Multiplayer Hardening: Passes 14A–14F

These passes are complete and have already established reusable patterns.

## 14A — Shared simulation authority

Shared bell/tether systems were hardened so authoritative simulation owns shared state.

Examples included:

- tether payloads
- tether constraints
- docking/deployment
- winch
- bell mass aggregation
- ballast
- bell air/flooding

Clients may still render presentation.

## 14B — Requester-aware winch authority

Winch line load/unload paths preserve the actual requester.

Do not trust a client-authored player ID.

Future flow:

```text
client request
    ↓
network transport authenticates sender
    ↓
host resolves requester
    ↓
existing authority API
```

## 14C — Authoritative autonomous decisions

`DivingBellContainedItem` was changed so autonomous decisions about whether an item escaped containment are authority-owned.

Explicit state-application methods such as assignment/clear operations remain usable for persistence and future replication.

General rule:

> Gate autonomous simulation decisions, not every setter/state-application API.

## 14D — Requester-aware world drops

World-item drop infrastructure now carries `WorldItemDropContext`.

Conceptually:

```text
InventoryDragController
    ↓
requester + origin + item
    ↓
IWorldItemDropTarget
    ↓
validation / mutation
```

This is game-wide infrastructure.

The requester inside a local context is **not** equivalent to trusted network identity. Future transport must authenticate the sender before constructing an authoritative request context.

## 14E — Local presentation authority

Shared gameplay authority and local presentation authority are separate concepts.

Remote players must not be able to modify another client's:

- cutaway state
- camera behavior
- culling masks
- ocean foreground/background presentation
- bell shell visibility
- rope visibility
- local fades
- local-only audio presentation
- local UI/hover/highlight state

However, gameplay/physics behavior that happens to share a controller with visual behavior must remain authoritative/per-player as required.

Do not accidentally fix presentation by disabling gameplay logic for remote players.

## 14F — Per-player persistence seam

`GameState` now includes keyed per-player persistence state while retaining legacy singular fields as compatibility mirrors.

Current single-player identity uses `"local"`.

Future networking should replace this with authenticated stable identity.

Old saves remain compatible.

Multiple-player persistence must not silently choose a random player using `FindAnyObjectByType`.

Ambiguity should fail loudly.

---

# 5. Identity Model

The multiplayer implementation should explicitly separate these concepts:

```text
ConnectionIdentity
    authenticated network/platform participant
    likely SteamID in production
    local generated fallback for LAN/dev testing

PlayerPersistenceIdentity
    participant's persistent record within this world/session

CharacterIdentity
    the mortal/resurrectable character

SpawnedPlayerEntity
    the current networked physical body/prefab
```

These often map 1:1 but must not be collapsed into one concept.

This separation is required for:

- reconnects
- dead-but-spectating players
- resurrection
- late joiners
- BoatScene disconnected bodies
- NodeScene locker persistence
- future character selection
- future roles
- future voice chat
- avoiding direct coupling between Steam identity and a specific `GameObject`

## Trust boundary

Never trust a client-provided arbitrary player/network ID as authoritative identity.

The network transport/session layer authenticates the sender.

Authority then resolves that sender to a server-known `ConnectionIdentity` / player record.

---

# 6. Player Scene-Object Audit and Dynamic Spawning

## Current concern

Players are currently authored as scene objects.

This must be audited before networking is introduced.

Multiplayer likely requires converting players to dynamically spawned prefabs whose:

- authenticated identity
- persistence record
- character identity
- loadout
- boarding/scene context
- connection state
- spawn location
- role
- presentation ownership

are applied at spawn time.

## Do not blindly prefab the current player

Before conversion, inspect all systems that currently assume a scene-authored player already exists.

Search for:

```text
FindFirstObjectByType<Player
FindAnyObjectByType<Player
FindObjectsByType<Player
PlayerInventory
PlayerBoardingState
GameState.I.player
camera references
UI references
spawn bootstrap references
scene bootstrap assumptions
inventory persistence bindings
interaction references
audio-listener assumptions
sorting/cutaway assumptions
```

Identify scene references that currently drag the player into unrelated systems.

## Goal

Reach a model where a player can be created dynamically using something conceptually like:

```text
SpawnPlayer(
    ConnectionIdentity,
    PlayerPersistenceIdentity,
    CharacterIdentity,
    SavedPlayerState,
    SpawnContext
)
```

Single-player behavior must continue to work.

Single-player should ideally use the same spawn/lifecycle architecture with one local authoritative participant.

---

# 7. Session Lifecycle

## Host

A player may host a multiplayer session.

The host process runs the authoritative shared simulation and also has one local player.

## Host disconnect

Host disconnect or host process failure ends the session.

No host migration.

## Reconnect reservation

A disconnected player's identity/slot remains reserved for the **entire hosted session**.

Do not allow another participant to steal that persistence identity.

This reservation applies independently of the practical target player count.

---

# 8. Late Join Policy

The host/session configuration should support these join policies:

## Open

Brand-new players may join active play immediately if a valid/safe spawn exists.

## Spectate Until Safe Spawn

A new player may connect during an active voyage but remains a spectator until a valid spawn opportunity appears.

## Reconnect Only During Voyage

Existing participants may reconnect during BoatScene.

Brand-new players cannot become active players until the session reaches a valid NodeScene opportunity.

## NodeScene Only

Brand-new active players may only enter during NodeScene.

A player who already belongs to the session may still need special reconnect handling according to the session policy.

---

# 9. Player Disconnect Model

Disconnect behavior is intentionally different between BoatScene and NodeScene.

---

# 10. BoatScene Disconnect — Temporary Flesh Vessel

A persistent disconnected body exists **only** when all of the following are true:

1. current scene is `BoatScene`
2. that participant occupied/entered the BoatScene as an active player at the beginning of that scene
3. the participant is now disconnected/logged out
4. there remains a chance for that participant to reconnect and reoccupy that body before the BoatScene ends

The body is a temporary reconnect vessel, not a general offline-character system.

## Behavior

On disconnect during BoatScene:

- the currently held item immediately drops into the world
- all other carried inventory/equipment stays with the body
- the body remains physically present
- the body may be looted
- the body is vulnerable
- the body can drown
- the body can take impact damage
- creatures may attack it
- friendly fire can hurt/kill it
- flooding/fire/environmental systems continue to affect it
- physics continues to affect it
- the body counts as living for party-wipe detection while it remains alive

If the player reconnects during the same BoatScene and the body is still valid/alive, they reoccupy **that exact body**.

The reconnect does not create a new replacement body.

If the body died while disconnected, the reconnecting player returns in the dead/spectator state.

## Held-item rule

Whatever is currently in the player's hands drops immediately at the disconnect location.

This is especially important for sacred items.

If a player disconnects while holding a sacred item over open ocean, the sacred item falls into the ocean.

That is intentional.

## Sacred items

Sacred items remain **hands-only**.

They should not move into lockers or invisible disconnected-player persistence.

Examples include systems such as:

- Money Chest
- Astrolabe
- other future sacred/world-critical hand-carried items

This avoids unique world-critical items disappearing into an offline player record.

## Future idea: player preserver

A future system may allow players to place a living/disconnected/unconscious body into a preservation/stasis device to keep it safe.

This is **not part of the current multiplayer pass**.

Do not implement it now.

However, avoid making disconnected bodies fundamentally incompatible with being safely contained by a future system.

---

# 11. BoatScene End While Player Is Disconnected

A disconnected BoatScene body is temporary and scene-scoped.

When BoatScene ends:

- it does not persist physically into the next scene as a disconnected body
- surviving eligible player state transitions back into the player's locker/persistence model
- the participant remains reserved for the session
- future reconnect happens through NodeScene/locker semantics rather than reoccupying the old BoatScene body

---

# 12. NodeScene Disconnect — Locker / Escrow Model

A logout/disconnect in NodeScene does **not** leave a body.

The character disappears from the scene.

Eligible player-owned inventory/equipment is persisted to a disconnected-player locker/escrow associated with that player's stable identity.

## Locker concept

Each participant should effectively have a world-owned player locker/escrow.

The physical locker is a presentation/gameplay interface for authoritative persistent state.

The locker prevents unique/valuable ordinary inventory from permanently disappearing because a player stopped playing.

## Host access

The host can:

- inspect a disconnected player's locker
- explicitly unlock it for other players

This intentionally allows host power to be abused.

Do not overengineer anti-host-grief systems.

The design assumes that if the host is malicious, the social problem is outside the game's responsibility.

---

# 13. Personal Inventory Rules

While a player is:

- alive
- conscious
- connected

their inventory is personal/private.

Other players cannot normally browse or manipulate it without a specific gameplay mechanic.

When a player is:

- unconscious
- dead
- a disconnected BoatScene body

their body/inventory becomes accessible to other players.

---

# 14. Loot Rules

Loot is physical and authoritative.

No instanced duplicate loot.

No per-player duplicate pickup.

First successfully validated claim wins.

Example:

```text
Player A requests pickup
Player B requests pickup nearly simultaneously
        ↓
host validates requests in authoritative order
        ↓
first valid accepted claim succeeds
        ↓
later conflicting claim fails cleanly
```

This model applies to:

- items
- resources
- containers
- station claims
- seat occupancy
- market stock where appropriate
- other exclusive shared state

---

# 15. Money

Money is shared.

All player/party funds flow through the Money Chest / Treasury model.

Do not implement per-player allowance accounts during this pass.

An allowance system may be reconsidered later, but current design intentionally avoids it.

---

# 16. Friendly Fire

Full friendly fire is desired.

This includes:

- weapons
- tools
- projectiles
- explosions
- environmental physics
- bodies
- cargo
- other dangerous physical interactions

Do not silently introduce cooperative invulnerability.

Accidental friendly-fire chaos is intentional gameplay.

---

# 17. Player-to-Player and Cargo Collision Philosophy

Normal locomotion should **not** cause players to physically shove each other around.

Normal carried/ordinary item movement should also avoid unnecessary disruptive collision behavior where appropriate.

However, high-energy externally driven motion should become dangerous.

Example:

- ordinary Steve walks through Bob: no collision
- a massive wave launches Steve across the compartment: Steve can physically slam into Bob
- ordinary cargo is handled safely
- a wave/explosion/hull impact sends cargo flying: cargo can hurt people

## Motion provenance

Do not base this only on velocity magnitude.

The system should eventually distinguish **why** something is moving.

Potential hazardous impulse sources include:

- wave impulse
- explosion
- decompression
- hull impact
- creature attack
- externally applied environmental force
- severe vessel motion

Ordinary player locomotion should not accidentally enter damaging collision mode merely because movement was fast.

This concept should be designed as reusable infrastructure for:

- players
- corpses
- unsecured cargo
- tools
- debris
- other physical objects

The first implementation may be minimal, but avoid architecture that prevents motion-source/provenance tracking later.

---

# 18. Death, Unconsciousness, Resurrection, Spectating

The multiplayer survival model should resemble Barotrauma in overall session flow.

## Unconsciousness

Unconscious players may be resuscitated by others.

Unconscious bodies remain physically present.

Their inventories may be accessed.

## Death

Dead players are dead until a resurrection mechanic is used.

Dead bodies remain physical world objects with their equipment/inventory.

Death does **not** automatically escrow the player's belongings.

A corpse may be recoverable, lootable, lost, sunk, or otherwise affected by the world.

## Resurrection

Resurrection restores the **same CharacterIdentity**.

It is resurrection, not reincarnation.

Do not create a fresh unrelated character identity as part of resurrection.

## Spectating

Dead players and waiting late joiners should support:

- teammate-follow spectating
- free-camera spectating

Free-camera spectating must be configurable.

There should be a host/session toggle allowing free-camera mode to be disabled if it creates scouting/exploit problems.

---

# 19. Party Wipe

The voyage/session continues while at least one player body remains alive.

This includes a disconnected BoatScene body.

Therefore:

```text
3 players dead
1 disconnected player's body still alive
        ↓
party is NOT wiped yet
```

This is intentional.

If the final living body dies, the party wipe condition is met.

On party wipe:

**reload the last valid save.**

---

# 20. Save Rules

## No BoatScene saving

Do not allow ordinary saving during BoatScene.

Voyages are intentionally high-stakes.

Players undertaking a long voyage are expected to prepare properly.

No save-scumming during the voyage.

If this proves too punishing in testing, the rule can be revisited later.

## Autosave milestones

The game should autosave:

1. on entering NodeScene
2. after a player clicks Embark but **before** embark/scene-transition logic executes

The pre-Embark save is especially important.

If the party later dies during the voyage, reloading should preserve all town preparation/work performed before embark.

The current autosave rotation is approximately **5 saves**.

Keep the rotating model and tune the number later based on playtesting.

---

# 21. Autosave Restore-Provenance Rule

Repeated party wipes must **not** gradually replace the entire autosave history with identical copies.

Problem scenario:

```text
pre-Embark autosave A
    ↓
party dies
    ↓
load A
    ↓
NodeScene initializes
    ↓
automatic NodeScene-entry autosave B
    ↓
party embarks and dies
    ↓
load B
    ↓
NodeScene auto-saves C
    ↓
repeat until all autosave slots are clones
```

This destroys meaningful save history.

## Required behavior

Loading a save must preserve explicit **restore provenance**.

Do not solve this using a fragile time-based cooldown.

Use an explicit mechanism such as:

- load/restore generation
- one-shot autosave suppression token
- restored-save lifecycle marker
- equivalent deterministic state

Rule:

> Do not create a new autosave merely as a side effect of restoring an existing autosave.

Conceptual flow:

```text
Load autosave A
    ↓
mark current lifecycle as restored-from-save A
    ↓
NodeScene initializes
    ↓
suppress the NodeScene-entry autosave caused only by restore
    ↓
player genuinely progresses
    ↓
player clicks Embark
    ↓
normal fresh pre-Embark autosave is allowed
    ↓
restore suppression clears
```

Saving should resume at the next legitimate gameplay milestone.

## Suggested save metadata

If compatible with current save architecture, consider storing metadata such as:

```text
SaveId
CreatedUtc
SaveReason
    NodeEntered
    PreEmbark
    Manual / other future reason
LoadedFromSaveId (optional)
```

This metadata is useful for debugging and save-history behavior.

Do not force a schema expansion solely for decorative metadata if it would destabilize the current save system. Implement the minimum robust provenance seam first.

---

# 22. Client Saves

Clients should be allowed to maintain a **full mirrored copy** of the authoritative session/world save.

During the live session:

- host remains authoritative
- client copies are mirrors/backups
- clients do not independently restore state into the active host session

A client may later deliberately take a mirrored save and start a **new forked hosted world** from it.

That fork becomes a separate world/session.

This provides backup protection without creating competing authorities in one live session.

---

# 23. Scene Model

The party always travels together at the Unity scene level.

Do not allow one participant to remain in NodeScene while another is actively in BoatScene.

The session has one shared gameplay scene context.

---

# 24. NodeScene Embark Rule

Embarking from NodeScene requires **all active connected players** to be boarded.

Do not silently transition while connected players are wandering around town.

This deliberately prevents "herding kittens" from being solved by teleporting everyone without warning.

Disconnected players using NodeScene locker semantics should not permanently block embark.

Their physical character is no longer present.

---

# 25. BoatScene Transition Rule

Any player may initiate the relevant transition interaction.

If all required connected players are onboard, transition normally.

If one or more connected players are not onboard, show the transitioner a confirmation warning similar to:

> **Warning: not all players are onboard. Transition anyway?**

The transitioner may confirm.

This supports situations where rescue is impossible or impractical.

Example:

A player is trapped deep underwater in a diving bell with diminishing air and no rescue path.

The remaining crew may knowingly abandon them.

## Consequence for connected players left behind

A connected player who is left behind by a forced BoatScene transition:

- is treated as dead
- is brought to the destination NodeScene
- arrives with **zero inventory**
- their abandoned/lost items remain governed by authoritative world/death rules from the departing scene as appropriate

This is a harsh intentional consequence.

## Disconnected BoatScene bodies

A disconnected BoatScene body does **not** count as onboard.

Its presence should still trigger the "not all players onboard" warning so the transitioner gets an explicit decision point before abandoning that player.

---

# 26. Spawn Rules

Use authored spawn points.

## Boat spawn

Prefer the authored boat player spawn points when safe.

## Unsafe boat spawn

If boat state indicates that the normal spawn points are unsafe, use fallback logic.

Boat submersion is one important safety signal.

Do not spawn a new/rejoining player directly into guaranteed death solely because the authored point is underwater/inaccessible.

## Node fallback

Use the authored NodeScene fallback spawn when boat spawn is unsuitable.

Spawn validation should remain server/authority owned.

---

# 27. Interactions and Shared-State Validation

Player-triggered shared operations should carry the exact requester.

Authority should validate at least:

- requester exists
- requester is connected/eligible
- requester has permission
- requester is close enough
- requester is interacting with the expected object
- requester is in the correct boat/scene/context
- required resources exist
- item/station/object is still available
- requested state transition is legal
- ownership/context requirements are satisfied
- the operation does not conflict with a prior accepted authoritative operation

Client prompt/UI validation is useful for responsiveness.

It is not authoritative validation.

---

# 28. Interactable Ownership Philosophy

The project intentionally favors cooperative chaos.

Any interactable is generally a free-for-all unless a specific locking mechanism exists.

Examples:

- helm
- stations
- winches
- pumps
- hatches
- dangerous controls
- tether actions
- cargo systems
- other shared controls

Do not add restrictive permission systems by default just because networking exists.

---

# 29. Exclusive Stations

Exclusive stations use authoritative occupancy.

Examples may include:

- helm
- pilot seat
- other one-user control stations

Behavior:

```text
station unoccupied
Player A requests use
host accepts
station occupant = A

Player B requests use
host rejects while occupied
```

Release conditions should include appropriate cases such as:

- player exits station
- player dies
- player becomes incapacitated where relevant
- player disconnects where relevant
- scene transition
- station becomes invalid/destroyed

Do not allow two peers to independently believe they own the same exclusive station.

---

# 30. Roles

Formal roles are future gameplay but should be anticipated.

Potential examples:

- Captain
- Engineer
- other crew roles

The networking foundation should support a replicated role identifier/role state associated with a player.

Actual role mechanics, role assignment UI, role perks, and detailed permissions are **not required in the first networking pass**.

The host remains ultimate session authority even if another player is Captain.

---

# 31. Boat Builder vs Future In-Game Building

The existing Boat Builder is an **editor tool only**.

It is not part of the runtime multiplayer session.

Do not waste the initial multiplayer pass networking the editor Boat Builder.

A future in-game construction/building system will receive its own design and implementation pass.

Likely future direction:

- players can build their own boat designs
- designs can be shared with the party
- the runtime building system may reuse some concepts/data from Boat Builder

Do not prematurely entangle the current editor tool with networking.

---

# 32. Multiplayer Menu / Connection UI

The game requires menu work as part of this multiplayer pass.

Add a Multiplayer entry/flow.

At minimum, provide a practical development/session UI for:

- Host session/server
- Join/connect as client
- disconnect/leave
- view connection status
- enter connection target/address/code as required by selected networking stack
- debug local host/client testing
- expose relevant join-policy settings
- expose spectator free-camera enable/disable setting where practical

This UI does not need final production polish.

It must be sufficient for real multiplayer testing.

---

# 33. Networking Package / Transport

This pass should culminate in **actual networking**, not only another abstract hardening round.

Bosun should analyze the best Unity-compatible networking solution for the current architecture.

Selection criteria should include:

- Unity 6 compatibility
- listen-server support
- authenticated player identity integration path
- likely Steam support / SteamID compatibility
- network object spawning
- RPC/request messaging
- state replication
- disconnect/reconnect lifecycle
- host-authoritative architecture
- headless/dedicated-server compatibility later
- debugging/tooling
- maintainability for a solo developer
- minimal friction with the existing `GameplayAuthority` model

Do not choose a package purely because it is fashionable.

Do not rewrite the authority architecture around the networking package if the package can instead serve the existing authority/request model.

---

# 34. First Real Multiplayer Milestone

The multiplayer pass is not considered successful until the developer can:

1. start a host session
2. start/connect a second client instance
3. have two distinct player entities in the same shared session
4. move both players independently
5. interact with shared objects
6. validate exclusive interaction conflicts
7. manipulate inventory/world items without duplication
8. verify requester identity is preserved
9. verify local presentation does not leak across clients
10. verify shared state agrees across peers
11. disconnect/reconnect and exercise the correct scene-specific behavior
12. exercise basic scene transition behavior
13. inspect obvious layering/collision regressions
14. save/reload through the intended host-authoritative save flow where relevant

The exact test harness may use:

- Unity multiplayer tooling if suitable
- editor + standalone build
- two standalone builds
- packaged build + debug connection UI
- another practical local test arrangement

The requirement is outcome-based:

> The developer must be able to simultaneously control one host player and one client player and actively abuse the game until multiplayer defects reveal themselves.

---

# 35. Movement Networking Strategy

Start minimal.

Prefer straightforward host-authoritative movement first.

Accept some latency in the initial implementation.

Do **not** build a large prediction/reconciliation system before testing proves it is required.

After host/client testing:

- measure feel
- identify where latency is unacceptable
- add interpolation/prediction/reconciliation selectively

Do not prematurely network-optimize systems that are not yet proven problematic.

---

# 36. Shared Physics Authority

Shared physical simulation should generally be authority-owned.

Be especially cautious around:

- boat rigidbody forces
- cargo rigidbodies
- loose world items
- waves affecting shared objects
- tethered objects
- underwater shared physics
- impacts
- destruction
- moving modules
- resource objects

However, do not globally authority-gate generic physics infrastructure used by local player movement.

Opt shared objects into authority.

---

# 37. Modules + Boat Power — First Global Hardening Target

Before or alongside networking integration, audit Modules + Boat Power using the patterns established by the diving-bell work.

Scope:

- Engine
- Generator
- Pump
- Turret
- Storage modules
- module activation
- power consumption
- boat power reservoir
- damage/repair if present
- hardpoint/controller links
- installed-module interactions

Audit questions:

- who is allowed to activate/toggle/use this?
- does UI directly mutate shared state?
- does simulation run independently on every peer?
- does power drain happen more than once?
- is the exact requester preserved?
- does authority validate distance/access/context?
- are required resources validated host-side?
- are illegal transitions rejected?
- is state separate from local presentation?
- are installed module references stable enough for replication?
- does any code assume one global player?

Goal:

```text
player interaction
    ↓
explicit requester
    ↓
module intent/request
    ↓
authority validation
    ↓
shared module/boat mutation
    ↓
replicated result
```

---

# 38. Global Multiplayer Audit Categories

For each major subsystem, classify findings into:

```text
Already MP-safe
Needs small seam
Needs authority gate
Needs requester propagation
Needs larger architectural change
Can defer until networking exists
```

Do not automatically rewrite everything found.

Prefer narrow coherent changes.

---

# 39. Recommended Global Audit Order

After Modules + Boat Power:

## 1. Inventory / Storage / Containers

Focus on:

- item ownership
- simultaneous container access
- item transfers
- stack splitting
- transactional swaps
- storage mutations
- dropped world items
- duplication/loss prevention
- disconnect/death/body interaction

Existing `InventoryDragController` already has hardening work but still has deferred cleanup.

## 2. Helm / Propulsion / Navigation

Audit:

- helm occupancy
- throttle
- installed engine authority
- propulsion forces
- piloting inputs
- navigation state
- route/course changes

Relevant existing TODO:

Move propulsion strength/throttle authority away from generic boat-level tuning and toward installed engine modules.

## 3. Trading / Markets / Treasury

Audit:

- buying
- selling
- market inventory
- offers
- treasury mutation
- Money Chest
- rollback/atomic transactions

Transactions should be authoritative and atomic.

## 4. Underwater Resources / Tools

Audit:

- resource harvesting
- extractables
- drill interactions
- batteries
- charge consumption
- spawned resources
- depletion

Two clients must not both successfully harvest the same authoritative resource.

## 5. Securing / Cargo / Physical Items

Audit:

- secure/unsecure
- rope consumption
- degradation
- cargo state
- cleats/zones
- shared physics

## 6. Doors / Hatches / Seats / Boarding / Ladders

Audit:

- open/closed state
- requester identity
- occupancy
- boarding state
- simultaneous use
- collision/presentation separation

## 7. NPC / Vendors / Creatures

Future-facing:

- AI authority
- vendor transactions
- shared NPC state
- combat
- creatures
- pirates/fish/etc.

Authority should generally own shared AI simulation.

## 8. World Map / Travel / Events

Audit:

- travel selection
- route progression
- world simulation
- node events
- buffs
- markets
- random outcomes

Clients submit requests.

Authority advances shared world state.

---

# 40. Code-Smell Searches

Search for these patterns during audits:

```text
FindFirstObjectByType<Player
FindAnyObjectByType<Player
FindObjectsByType<Player

PlayerInventory
PlayerBoardingState
GameState.I.player
CurrentMode

Input.Get
Input.GetKey
Input.GetMouse

Rigidbody2D.AddForce
velocity =

TryPlaceItem
RemoveItem
SetActive
```

These are **inspection targets**, not automatic bugs.

Also search for:

- UI directly mutating gameplay state
- singleton player assumptions
- scene-authored player references
- static mutable gameplay state
- random outcome generation happening on every peer
- local physics decisions affecting shared world
- direct save writes from arbitrary clients
- object ownership inferred from names or scene hierarchy

---

# 41. Local Presentation Rules

Client-local presentation remains separate from shared gameplay.

Examples of local-only presentation:

- camera
- cutaways
- culling masks
- local ocean visual state
- hover/highlight
- local UI
- screen fades
- post-processing
- local-only audio effects
- spectator camera
- local shell/rope visibility decisions where applicable

Remote players must not change another client's presentation merely by entering/exiting a region or state.

---

# 42. Voice Chat — Future Requirement

In-game voice chat is a strong future requirement.

Do **not** implement voice chat during this multiplayer pass.

However, do not create architecture that makes future voice integration difficult.

Future voice chat may need access to:

- authenticated player identity
- connection/session membership
- current spawned character/body
- alive/dead/spectator state
- scene
- world position
- role
- mute/block state
- local volume
- team/session membership

Possible future voice modes may include:

- proximity voice
- boat-wide/intercom voice
- radio voice
- spectator/dead voice
- role-specific channels

Voice architecture should remain separate from gameplay authority.

Gameplay systems should expose clean state/context APIs that a future voice provider can query.

Do not couple core networking design to a specific voice provider yet.

---

# 43. Save Authority

Host/world state is authoritative.

Clients may mirror saves but should not independently mutate the active world's authoritative save state.

Authoritative save ownership should include:

- shared world
- boat
- modules
- treasury
- map/travel state
- shared effects
- per-player persistent records associated with stable identity

Player-specific records should remain conceptually separate inside the authoritative world save.

---

# 44. Runtime Player Persistence

Future multiplayer persistence must stop assuming one unnamed global player.

Per-player persistence should be keyed by stable authenticated identity.

Examples of player-specific state:

- loadout
- inventory
- equipment
- scene/boarding context
- character identity
- health/death state where appropriate
- role
- reconnect/session state
- locker state
- future player progression if applicable

Shared state remains separate.

---

# 45. Disconnect / Reconnect State Machine

Bosun should explicitly model connection/player lifecycle state rather than scattering bools.

A conceptual state machine may include:

```text
ConnectedActive
ConnectedSpectating
DisconnectedBoatBodyAlive
DisconnectedBoatBodyDead
DisconnectedLockerState
DeadSpectating
WaitingForSafeSpawn
WaitingForNodeJoin
TransitioningScene
```

Exact enum/class names are not prescribed.

The important requirement is to make lifecycle explicit and debuggable.

---

# 46. Scene Transition and Persistence Ordering

Scene transitions are high-risk and must have deterministic ordering.

A transition flow should conceptually:

1. validate transition request
2. determine who is onboard/not onboard
3. present confirmation if required
4. finalize consequences for left-behind players
5. capture authoritative player/shared state
6. perform required pre-transition autosave where applicable
7. mark transition state
8. load next scene
9. rebuild authoritative shared objects
10. spawn/restore player entities or locker/spectator states
11. re-bind local presentation
12. only then resume ordinary simulation

Do not let clients independently perform scene transitions because they saw the same UI button.

---

# 47. Single-Player Compatibility

All multiplayer hardening must preserve single-player.

Single-player should ideally become a degenerate one-player authoritative session using the same core systems.

Avoid maintaining two completely separate gameplay architectures.

The networking layer itself may be inactive in single-player, but authority/requester/persistence patterns should remain compatible.

---

# 48. Deferred / Out-of-Scope Items

Do not expand this pass unnecessarily.

Explicitly deferred:

- host migration
- dedicated server deployment
- player preserver/stasis device
- full role gameplay
- polished production multiplayer browser
- voice chat implementation
- runtime in-game boat builder
- large-scale prediction/reconciliation unless testing requires it
- final anti-cheat strategy
- cross-platform account linking
- matchmaking service beyond what is needed for practical host/client testing
- advanced resurrection implementation unless required by existing gameplay
- full spectator polish
- 30-player optimization

Architect for these where cheap and sensible, but do not implement them now.

---

# 49. Existing Parallel Cleanup — Avoid Duplicating Unless Needed

A separate cleanup thread has handled or is handling diving-bell cleanup.

Do not duplicate unrelated work unless required by multiplayer implementation.

Known parallel/deferred items include:

- underwater bell-exit sorting bug
- DeckBoardZone safe deck snap
- final `InventoryDragController` hardening
- internal bell winch requiring boat electrical power
- module interaction collider Inspector cleanup
- `TetherDeploymentModule` behavior-preserving refactor
- final bell review/regression
- practical two-instance host/client development workflow

If a multiplayer pass directly depends on one of these, address only the necessary intersection.

---

# 50. Required Bosun Audit Deliverable Before Major Rewrite

Before broad code changes, Bosun should produce an audit summary covering:

## Player architecture

- current scene-authored player dependencies
- singleton assumptions
- spawn/bootstrap logic
- persistence hooks
- camera/UI bindings
- interaction bindings
- boarding/scene-context hooks

## Authority

- existing `GameplayAuthority` usage
- shared simulation that still runs on all peers
- direct UI → shared mutation paths

## Identity/requester

- hidden player searches
- interaction methods that lack requester context
- APIs that currently accept an arbitrary player ID/string

## Persistence

- singular player assumptions
- save/load lifecycle
- autosave triggers
- reconnect-sensitive state

## Replication candidates

Identify which state should be:

- replicated state
- authoritative event
- client intent/request
- local presentation only
- reconstructible from save
- derived rather than replicated

Classify each issue using:

```text
Already MP-safe
Needs small seam
Needs authority gate
Needs requester propagation
Needs larger architectural change
Can defer until networking exists
```

---

# 51. Implementation Philosophy

Prefer:

- narrow passes
- explicit request contexts
- stable identity
- host validation
- atomic shared transactions
- local presentation separation
- deterministic lifecycle
- clear state ownership
- debug visibility
- drop-in class replacements

Avoid:

- giant rewrites
- hidden global-player searches
- client-trusted identity
- client-owned shared physics
- UI directly changing shared world state
- scene hierarchy as identity
- hard-coded four-player arrays
- networking every cosmetic effect
- premature prediction
- overcomplicated host-abuse prevention
- dedicated-server complexity before the listen-server works

---

# 52. Success Criteria for This Pass

This multiplayer pass is successful when the developer can reliably:

- launch a host session
- launch/connect at least one client
- spawn distinct players dynamically
- move independently
- interact with shared systems
- resolve exclusive conflicts authoritatively
- transfer/pick up/drop items without duplication
- preserve requester identity
- disconnect/reconnect
- observe BoatScene body behavior
- observe NodeScene locker behavior
- test death/spectating basics
- transition scenes correctly
- verify forced-left-behind consequences
- verify client-local presentation isolation
- verify authoritative shared state
- create/use host-authoritative saves
- retain client save mirrors
- avoid autosave-history collapse after reload
- run repeatable two-instance regression tests

At that point the project has crossed from "multiplayer-ready architecture" into **actual playable multiplayer infrastructure**.

---

# 53. Final Non-Negotiable Summary

If implementation details conflict with these rules, preserve these rules unless explicitly changed by the project owner:

1. Shared world/boat simulation is host authoritative.
2. Host disconnect ends the session.
3. Target is 4 players, but architecture should not hard-limit to 4.
4. Clients submit intents; authenticated transport identity resolves the requester.
5. Do not trust client-authored player IDs.
6. Local presentation is separate from shared gameplay.
7. Player identity, character identity, and spawned body are separate concepts.
8. Players likely need dynamic prefab spawning instead of permanent scene-authored player objects.
9. BoatScene disconnect leaves a temporary vulnerable reoccupiable body.
10. NodeScene disconnect resolves to locker/escrow.
11. Held item drops immediately on disconnect.
12. Sacred items are hands-only and never disappear into locker persistence.
13. Disconnected BoatScene bodies can be looted and killed.
14. Disconnected living BoatScene bodies count as living for wipe detection.
15. At BoatScene end, surviving disconnected state resolves back into locker/persistence.
16. Personal inventory is private while alive/conscious/connected; unconscious/dead/disconnected BoatScene bodies can be accessed.
17. Loot is authoritative and non-instanced.
18. Money is shared through the Money Chest/Treasury.
19. Full friendly fire is intentional.
20. Ordinary locomotion should not cause disruptive player collision; externally driven dangerous motion may.
21. Resurrection restores the same CharacterIdentity.
22. Party wipe reloads the last save.
23. No BoatScene saving.
24. Autosave on NodeScene entry and pre-Embark.
25. Loading a save must not generate a duplicate autosave solely because the restored NodeScene initialized.
26. Clients may mirror saves and later fork them into new hosted worlds.
27. Party always shares the same Unity gameplay scene.
28. NodeScene embark requires all active connected players boarded.
29. BoatScene forced transition warns if anyone is not onboard.
30. Connected players left behind by forced transition are treated as dead and arrive at destination with zero inventory.
31. Any player may initiate a transition interaction.
32. Exclusive stations are authority-owned one-user claims.
33. Cooperative chaos is intentional; dangerous controls are generally free-for-all unless explicitly locked.
34. Roles are future-facing but replicated role identity should be anticipated.
35. First pass must end with a real host + client test.
36. Movement starts simple/host-authoritative; prediction comes later if needed.
37. Existing Boat Builder is editor-only and should not be networked.
38. Multiplayer menu/host/join UI is part of this pass.
39. Voice chat is a future requirement and should influence clean identity/session architecture, but is not implemented now.
40. Exact-current-class rule applies to every existing class modification.

---

**End of handoff.**
