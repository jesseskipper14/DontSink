# Multiplayer infrastructure audit — Bosun

2026-10-02. Analysis only, following the world-wrapping implementation. No MP runtime, networking packages, Inspector settings, scenes, or prefabs were changed. This is a focused source audit against `Assets/Codex/md files/Dont_Sink_Multiplayer_Architecture_Handoff.md`, not a security certification or a live two-client test.

## Main finding

There are useful authority, actor-context, keyed persistence, camera ownership, and edit-lock seams already. There is not yet an authenticated multiplayer session connecting them. The biggest immediate holes are Modules/Boat Power simulation, commands that lose their requester before mutation, and inventory/market operations that are locally validated rather than atomic host transactions.

The handoff's installed-engine propulsion concern is already addressed: `Physics/ThrottleForce.cs:114` discovers engines, applies throttle load, checks thrust eligibility, and sums installed thrust before applying force. Preserve that work. The next module pass should harden authority and requests, not redo propulsion.

Priorities below describe future integration risk. Most peer-duplication consequences are predictions from source inspection, since no networked session exists here today. The trade partial-commit path is an existing local correctness risk as well.

## Classification and evidence

Source paths in these tables are relative to `Assets/Scripts`. “Already MP-safe” means the inspected local seam follows the required separation; it does not mean the entire subsystem has authenticated networking or replication.

| Area / priority | Classification | Evidence and consequence | Contained next change |
| --- | --- | --- | --- |
| Role/bootstrap, P1 | Needs larger architectural change | `Multiplayer/GameplayAuthority.cs:5` defaults authoritative; its setter is a debug role switch. No runtime NetworkManager/NetworkObject/RPC, authenticated connection registry, or reconnect lifecycle was found in `Assets/Scripts`. Multiplayer Center in the manifest is not that runtime. | Establish host/client role before shared initialization and a session registry; bind authenticated sender to participant and actor. Keep host authority independent of the host's character. |
| Engine, P1 | Needs authority gate | `Modules/EngineModule.cs:80` consumes power/fuel in Update; `SetOn:133` and `Toggle:154` directly mutate. No GameplayAuthority check. | Host-only simulation plus validated toggle/fuel intents; clients apply state and render. |
| Generator and pump, P1 | Needs authority gate | `GeneratorModule.cs:53` adds power/burns fuel; `PumpModule.cs:61` consumes power and removes compartment water. Both expose ungated SetOn. | Gate shared ticks and command paths together. Verify one fuel burn, one power production/withdrawal, and one water change per host tick. |
| Turret, P1 | Needs authority gate | `Modules/Turret/TurretModule.cs:148` consumes power and instantiates a projectile. `PlayerTurretController.cs:19` directly aims/fires. | Host validates occupant, cooldown, target/pitch limits, and power, then creates projectile once. Aim input is intent; resulting state/fire event is host output. |
| Power reservoir, P1 | Needs authority gate | `Boats/BoatPowerState.cs:18,24,33,41` exposes mutable power operations without role/request validation. | Separate host transactions from snapshot application. Do not gate every replication/restore setter indiscriminately; validate finite amounts and legal ranges at commands. |
| Hardpoint/module UI, P1 | Needs requester propagation | `Modules/Hardpoints/HardpointInteractable.cs:213` calls parameterless Toggle after context checks. `MiniGames/Runners/ModuleOverlayRunner.cs:21` opens without requester. `ModuleCartridge.cs:38–39` finds global inventory/equipment; buttons mutate modules and fuel directly. | Carry the originating actor through runner/cartridge; resolve their stores explicitly and route shared mutations through a host command boundary. Repeat range/context validation at execution, not just prompt display. |
| Storage deposits, P1 | Needs requester propagation | `Modules/Storage/StorageModuleWorldDropTarget.cs:25,42` receives context but inserts through ContainerPlacementUtility without validating requester, range, or role. | Validate authenticated actor, target identity, boat/context, range, slot revision, and source ownership in one transfer transaction. |
| Inventory drag/drop, P1 | Needs larger architectural change | `Inventory/InventoryDragController.cs:266,734,1152` has useful local reservations but directly edits slots. `Inventory/Item/WorldItemDropUtility.cs:5,26` accepts an actor and instantiates items locally. | Retain reservation UX; add host item/slot revisions, operation IDs, commit/reject handling, and host-only world spawn. Local reservations alone do not stop two peers claiming one item. |
| Inventory keyboard input, P1 | Needs small seam | `Inventory/PlayerInventoryInput.cs:15` reads scroll/drop keys without an explicit local-owner guard. Replicated actor copies could all respond to one keyboard. | Bind/enable the input adapter only for the local participant; preserve authoritative inventory commands separately. |
| External storage UI, P1 | Needs requester propagation | `Inventory/ExternalContainerOverlayUI.cs:48,67` uses Camera.main and first PlayerInventory fallback. | Explicit local owner/inventory binding. A remote-origin interaction must not open or mutate the local human's panel. Existing BUGS.md already tracks this family. |
| Market trades, P1 | Needs larger architectural change | `WorldMap/Player/Trade/TradeEffectApplier.cs:180–224` changes items, offers, and embargo state before final money application; failure returns without rollback. No authenticated requester/authority boundary. `TradeWorldMapRunner.cs:132` reaches this shared path. | Validate the complete bundle, reserve/commit stock, cargo, and shared money atomically with revision and operation-ID checks. Payment failure must leave every part unchanged. |
| Treasury, P1 | Needs authority gate | `MoneyChest/MoneyService.cs:37,54` already uses shared treasury. `MoneyChestTreasuryService.cs:347,397` exposes ungated mutations. | One host ledger, replicated chest/balance state, server-resolved requester transactions. Preserve the shared pool; do not replace it with individual wallets. |
| Legacy trade adapter, P2 | Needs small seam | `WorldMap/Player/Trade/TradeService.cs:62,214` mutates WorldMapPlayerState.credits; normal runners instead use TradeEffectApplier/MoneyService. | Audit remaining call sites and retire or adapt this alternate path. Its existence does not prove the current market UI uses the wrong pool. |
| Resource harvest/tools, P1 | Needs authority gate | `Environment/Resources/UnderwaterResourceInteractable.cs:162,242` validates locally, awards loot, decrements charges, and disables exhausted resources. `Inventory/Tools/ToolUseChargeUtility.cs:66` consumes held-tool charges locally. | Host validates held tool, range, resource revision, elapsed work, and remaining charges; first validated claim wins. Do not trust client-supplied elapsed time or loot. |
| Helm/navigation, P1 | Needs authority gate | `Boats/Simulation/BoatPilotingSimulation.cs:238,249` uses a process-local object control owner; `FixedUpdate:475` advances shared simulation without GameplayAuthority. | Host station lease and intent stream; host boat/nav ticks. Preserve WorldNavigationService authoritative/replicated state application distinction. |
| Shared physics, P1 | Needs small seam | `Physics/ForceSystem.cs:12,94` already provides an opt-in role gate, default off. `Boats/Boat.cs:200,229` also has direct physics paths. | Review all shared boat/cargo force producers and opt them into the host policy; clients need an appropriate replica physics mode. Do not globally disable actor-local ForceSystem physics. Inspector changes require a later reviewed task list. |
| Cargo securing, P1 | Needs authority gate | `Inventory/Cargo/BoatSecuredItem.cs:110,235,328,347` changes decay, joints, and securing state locally; `BoatSecureZone.cs:525` reserves local slots. | One host occupancy/quality ledger and validated secure/unsecure/fasten commands; separate restore/application from command eligibility. |
| Doors/hatches, P2 | Needs authority gate | `Parts/Hatch/HatchRuntime.cs:126,131` decides closure using local blockers and refreshes collider/presentation. | Host decides legal open/close state; clients apply that decision, rather than independently rejecting it using replica physics. |
| Seats/turret occupancy, P1 | Needs larger architectural change | `Controller/Player/SeatController2D.cs:128,145` and `TurretControlStation.cs:116` claim occupants locally and move actors. | Host exclusive claims with releases on exit, death, unconsciousness, disconnect, and scene invalidation; local camera/UI follows the replicated claim. |
| Boarding/ladders, P2 | Needs small seam | `Parts/BoatBoardingInteractable.cs:170` preserves actor context but directly boards. `Controller/Player/Ladders/PlayerLadderClimber.cs:238` consumes concrete LocalCharacterIntentSource and drives Rigidbody movement. | Host boarding truth and eligibility; abstract movement intent source for remote actors. Keep local presentation and actor collision handling distinct from shared scene decisions. |
| AI/fish, P1 | Needs authority gate | `Agents/Runtime/AgentController.cs:124` ticks brain/movement; `Agents/Spawning/FIshSchoolController.cs:68` advances school decisions without a role gate. | Host brains/target selection/damage. Replicated or scene-authored agents still need policy even though AgentSceneSpawner already gates spawning. |
| World simulation/events, P1 | Needs authority gate | `WorldMap/Simulation/WorldMapDriftSimulator.cs:102` and `WorldMap/Events/WorldMapEventManager.cs:73` tick shared state without a role gate. | One host clock/tick and random outcome stream; replicate resulting state/events. Seeded generation is not a substitute for authority over evolving state. |
| Spawn/identity, P1 | Needs larger architectural change | PlayerInventory is scene-authored in `Assets/Scenes/BoatScene.unity:1710` and `NodeScene.unity:18996`. `GameState/Inventory/PlayerLoadoutPersistence.cs:35` can fall back to the local key for every actor. | Dynamic participants and actor spawning; bind distinct persistence keys before restore. Do not create a fixed four-slot identity architecture. |
| Crew transitions, P1 | Needs larger architectural change | `GameState/Scene/SceneTransitionController.cs:53,77,128` captures a current player and directly loads the scene. `Boats/BoatDepartureGate.cs:11` checks deployed tether payloads, not the connected crew roster. | Host-ordered transition protocol with roster, confirmation/consequences, crew capture, autosave, load, spawn/restore, local bindings, and resume. Preserve tether blocking. |
| Saves/autosaves, P1 | Needs larger architectural change | `Saves/SaveGameService.cs:39` already rejects BoatScene saves. `RefreshRuntimeStateBeforeSave:422` captures current-player runtime data; keyed payload records exist. Autosave call sites do not implement Node-entry/pre-embark milestones. Save/load has no host-role/provenance protocol. | Host capture every reserved participant, explicit restoration provenance, required milestone autosaves and five-slot rotation; passive client mirror import must not load the live host world. |
| Death/disconnect/wipe, P1 | Needs larger architectural change | `Survival/Death/PlayerDeathSystem.cs:67` dies locally. `CorpseRespawnHandler.cs:125` creates a corpse without a participant/character/inventory transfer protocol. No session body/locker/reconnect/wipe coordinator found. | Implement the handoff's body, inventory access, sacred-hand drop, Node locker, reconnect, spectator, and party-wipe rules as an explicit lifecycle. |
| Hazard impulse provenance, P2 | Needs small seam | `Physics/ExternalImpulseForce.cs:11` accumulates a vector but carries no source/provenance model. | Distinguish normal player locomotion collision from host-issued hazardous impulses. Preserve full friendly fire; avoid blanket player invulnerability or global collision removal. |

## Seams to retain

| Existing seam | Classification | What is still required |
| --- | --- | --- |
| `Camera/CameraManager.cs:183`, owner-scoped camera/input bindings | Already MP-safe at the local presentation seam | Session bootstrap must correctly bind local versus remote actors. Spectator camera position must not become authoritative simulation relevance. |
| `Multiplayer/SpawnClaimService.cs:40`, authored spawn claim helper | Needs small seam | Host-resolved participant key, safe/unsubmerged fallback validation, claim lifecycle, and reconnect-to-existing-body handling. Runtime Transform IDs and client strings are not network identity. |
| `GameState/GameState.cs:65,716`, keyed player records | Already MP-safe as a collection shape | Authenticate keys; capture all relevant actors; add character/session lifecycle data. No reason to replace the collection with a four-player array. |
| `WorldMap/Celestial/CelestialChartBoardAuthority.cs:824`, authority checks plus revision/edit locks | Already MP-safe at the local mutation seam | Transport must authenticate the sender and validate interaction eligibility; release leases on every lifecycle exit. Existing requester keys are not independently authenticated. |
| `Inventory/BoatEquipment/PlaceableBoatEquipment.cs:62,174`, role/context checks | Already MP-safe at the local mutation seam | Host dispatch and stable object references, replica deployment application, disconnect cleanup. Preserve the pinning buffer/support fixes. |
| `Agents/Spawning/AgentSceneSpawner.cs:58` and existing flotation/cutting authority gates | Already MP-safe at the role-gate seam | Session role initialization and replication; check the instantiated/preplaced object's own simulation as well. |
| `Modules/State/BoatModuleStatePersistence.cs:26`, module manifest with hardpoint IDs | Needs small seam | Pair hardpoint ID with stable boat ID and module incarnation/revision. Reconstruction snapshots are not validated interactive transactions. |

Sacred/category and vendor gates already exist (`Inventory/Item/ItemDefinition.cs:226–248`). Retain them. The handoff additionally requires sacred held items to drop immediately on disconnect, including NodeScene; that rule needs the missing lifecycle coordinator. A future ordinary-item escrow must not accidentally swallow sacred hands items.

## Identity and lifecycle design required by the handoff

Keep four different concepts: authenticated connection identity, persistent player identity, character identity, and the currently spawned actor. A GameObject, persistence string, or process-local object reference cannot stand in for all four. Resolve requester identity on the host from the authenticated connection, then select the actor and its stores. Reject stale actor/object incarnations and replayed operations.

Reserve session participants through disconnect until session end. In BoatScene, the disconnected living body remains vulnerable and lootable, counts for wipe, and reconnect reclaims that body. If dead, reconnect enters spectating; resurrection preserves character identity. In NodeScene, ordinary possessions go to the required locker and the body despawns. Scene-end handling must settle disconnected BoatScene bodies into the specified locker policy. Private personal inventory access depends on alive/conscious/connected state, not merely whether a PlayerInventory component exists.

For Node departure, require all connected active crew boarded. For BoatScene transitions, obtain the requester's confirmation and apply the handoff's left-behind death/inventory consequences; disconnected bodies warn but do not veto departure. Capture all reserved participant records consistently before scene load. Host disconnect ends the session; host migration is out of scope.

Save restoration needs a reason/token carried through transition and bootstrap, so load/restore suppresses duplicate Node-entry autosave while subsequent normal pre-embark autosave remains allowed. This is a missing protocol, not a claim that today's absent milestone autosave already creates duplicate files. Five-slot rotation exists and can be reused. Client mirrors must contain the complete save, and a later offline fork needs a new world/session identity; importing that mirror must never be a live-session load command.

The separate BUGS.md New Game world-map snapshot/starting dock findings remain open. Wrapping does not fix that reset path; validate it before relying on repeated fresh-world session tests.

## Replication candidates

| Kind | Candidate data | Policy |
| --- | --- | --- |
| Client intent | Toggle/install/remove/fuel module; transfer/drop/harvest; aim/fire; helm; board/seat/hatch; embark | Authenticated sender, stable target/actor IDs, operation ID, expected revision where needed. Host validates range, context, resources, and current lifecycle state. |
| Host state | Power/fuel/on-state; water; module manifest; inventories/cargo slots; station claims; boat pose/navigation; resource depletion; treasury; chart revisions; actor health/death | Replicate accepted truth. Do not run shared simulation again on receipt. Choose rates per subsystem after measurement. |
| Host events | Fire/projectile spawn, accepted pickup/trade, damage/death, securing change, scene-transition phase | Reliable/idempotent handling where required; late join must recover from state, not replay an entire event history. |
| Local presentation | Cameras, telescope fades, panels, prompts, drag ghost, audio/view effects | Remains local. Effects follow accepted host events/state; presentation cannot spend resources. |
| Derived data | Seeded terrain/stars/constellations, map rendering, debug readouts | Reconstruct from matching host configuration, generation version, and saved identity. Do not stream every ambient star as a network object. |
| Durable save | Shared world/boat/chart/treasury plus every reserved player/character/locker record | Host-consistent capture at allowed milestones; complete passive client mirrors. Transient leases/input/drag ghosts need cleanup, not blind persistence. |

## Proposed contained passes and checkpoints

1. **Modules + Boat Power first.** Add actor-bearing commands and validation, host simulation gates, explicit replica application, stable module addressing, and local UI ownership. Keep a single-player adapter. Test client-role ticks do not burn fuel, pump water, charge power, or create shots; stale/repeated requests cannot mutate twice. Commit after live single-player checks.
2. **Minimal session identity and dynamic player bootstrap.** Add participant/character/actor mappings and early role setup. Bind local UI/input/camera once; bind persistence before restore; use existing authored spawn helpers with safety checks. Inspector/prefab tasks must be supplied for review rather than silently applied.
3. **Select transport and prove two-instance module traffic.** Make the package choice in a separate researched pass, then implement authenticated dispatch, state application, late-join module state, disconnect claim release, and host shutdown. Prove one shared fuel/power/projectile result before expanding coverage. This audit makes no package recommendation or installation.
4. **Inventory/market/resource transactions and station claims.** Host revisions and idempotency; atomic stock/cargo/treasury commit; exclusive seats/helm; first-valid loot claims. Test concurrent requests, stale revisions, resource exhaustion, failed payment, replay, and disconnect mid-operation. Preserve free interaction apart from existing exclusivity.
5. **Shared physics, AI, and world simulation.** Explicit host simulation policy; actor relevance collection rather than spectator camera; actor intent adapters; hazard provenance. Verify boat/cargo do not receive duplicate forces and world events happen once.
6. **Lifecycle, crew transitions, and save mirrors.** Implement disconnect body/locker/sacred rules, death/reconnect/spectating/wipe, roster departure rules, host-ordered scenes, restoration provenance, autosave milestones, and full client mirrors. Test both scenes with disconnect/reconnect at every transition boundary.

Each pass should end with a short report, explicit Inspector tasks, appropriate checks, live acceptance steps, and a commit reminder. Target four players without hard-coding a maximum. No networking of editor BoatBuilder, voice implementation, host migration, dedicated deployment, or elaborate prediction work is needed to close these first seams.

## Verification limits

This audit used runtime source searches, targeted reads, package manifest inspection, and read-only scene component evidence. No MP behavior was simulated, no two-client session was run, and no transport/security claims were verified. The wrapping compile and isolated Unity results are documented separately in `WRAPPED_WORLD_CHECKPOINT.md`; they do not validate this future architecture.

There are no Inspector tasks for the audit itself. Findings are deliberately left for separately authorized implementation passes.

## 2026-10-10 implementation-start reconciliation

This addendum reconciles the historical audit with the current source after the accepted atomic-trade, vendor-stock, requester-ownership and shared-module authority passes. Earlier findings remain historical evidence, not a claim those specific defects are still unfixed. No networking package, scene or prefab was changed for this review.

### Work now available for reuse

- TradeEffectApplier coordinates reversible item and treasury mutations. Reuse this transaction boundary for host-dispatched trades.
- GameState.itemVendorStock owns finite item-vendor stock. ItemVendorPurchaseService validates/reserves canonical node/vendor/item stock and compensates failed delivery.
- ModuleOverlayRunner and ModuleCartridge carry the interacting actor; vendor delivery no longer chooses another actor's global acquisition resolver.
- StorageModuleWorldDropTarget checks live requester distance and matching boat context. Loadout restore binds identity before use and rejects ambiguous/duplicate keys.
- EngineModule, GeneratorModule, PumpModule, TurretModule and BoatPowerState guard live shared resource mutation. Explicit restore paths remain usable on replicas.
- Boat force providers and Boat.AddForce/AddTorque require authority. Boat water equalization and ExternalWaterSourceSystem ticks are gated. Generic non-boat ForceSystem policy remains opt-in.
- Existing CameraManager local ownership, actor intent interfaces, keyed persistence records, winch/tether authority, chart revision locks, physical cargo, module snapshots and treasury are integration foundations.

These are local runtime seams. They do not authenticate network senders, transmit requests, replicate state, implement remote movement, or produce a playable two-instance session.

### Current prerequisites still open

1. Packages/manifest.json contains Multiplayer Center, but no gameplay networking/transport package. Select transport using current official documentation, Unity 6 compatibility, listen-server support, solo-developer maintenance and a practical Steam identity/transport path. No package is selected by this addendum.
2. Players are still scene-authored. Audit and detach their camera/UI, inventory, restoration, boarding and input references before authoring a dynamic player prefab. Instantiate inactive, assign participant/persistence/character/entity identity and local ownership, then activate and restore.
3. PlayerLadderClimber still references LocalCharacterIntentSource directly. Host-authoritative remote movement needs an intent adapter; do not bind the remote avatar to host keyboard input.
4. BoatPilotingSimulation.FixedUpdate still advances helm commands/environment/navigation paths. Existing authoritative command checks and VoyageStrip observer flags are not a complete host/replica tick split.
5. CargoSecuringMiniGameRunner retains a global PlayerInventory fallback. The recent requester pass was scoped; it was not an exhaustive migration of every interaction.
6. ModuleCartridge still invokes module operations locally. Role gates reject a client mutation; an authenticated network request dispatcher must replace those client mutation calls for connected play.
7. Save/transition entry points remain built around the current local participant. Full crew capture, host-ordered transition, restoration provenance, reserved disconnected participants, locker/body lifecycle and passive full-save client mirrors remain to implement.
8. Shared Rigidbody replica mode and snapshot interpolation remain unimplemented. Force gating does not prevent Unity's ordinary gravity/collision integration by itself.
9. AI, world events, harvesting, exclusive station claims, inventory access policies, damage/death and other audit categories still require host execution and replication integration.

### Recommended checkpoints

- **MP.0 — Current-source reconciliation:** this addendum plus the original infrastructure audit. Freeze the first playable milestone around one shared boat and two independently controlled participants. Keep the full handoff's lifecycle/save requirements as subsequent acceptance criteria.
- **MP.1 — Transport choice and session identity:** research/select the networking stack, implement listen-host/client lifecycle and sender-to-participant mapping. Keep connection, persistence, character and spawned-entity identities separate. Initialize gameplay role before spawning gameplay objects. Preserve an explicit single-player path.
- **MP.2 — Dynamic actor bootstrap and movement vertical slice:** provide reviewed player-prefab/Inspector tasks, spawn two distinct actors, bind local input/camera/UI, route remote input to host movement, apply replicated poses and shared-boat replica policy. Test editor plus standalone or two builds.
- **MP.3 — Real shared interaction vertical slice:** authenticated module commands and state snapshots, one exclusive station and one physical pickup/drop conflict. Validate range, access, incarnation/revision and duplicate requests. Prove one fuel/power/item outcome across host/client and late join.
- **MP.4 — Expand shared gameplay coverage:** inventory/container/market/harvest transactions, securing, helm, hatches, ladders, AI, damage and world simulation. Reuse existing authority/domain operations; avoid networking ambient stars or cosmetic effects.
- **MP.5 — Lifecycle, transitions and durable saves:** BoatScene disconnected bodies, NodeScene lockers, sacred held-item release, reconnect/death/spectating/wipe, crew departure consequences, host scene protocol, autosave provenance and complete passive client mirrors.
- **MP.6 — Two-instance abuse/regression and closure:** repeat disconnect/reconnect, conflicting claims, inventory transfers, moving-boat interactions, scene changes and host save/reload. Target four participants without fixed four-slot collections. Mark the multiplayer handoff complete only when its full success criteria pass.

Select transport before building package-dependent player/session components; the original audit's identity design can precede selection, but package-independent scaffolding alone should not become another long hardening detour. End each coherent checkpoint with concrete playtest steps before broadening coverage.

### Updated incoming queue

Recent additions include Node_Power_Generator_Foundation_Bosun_Handoff.md, Boat_Surface_Wallpaper_Shader_System_Bosun_Handoff.md and Boat_Structural_Materials_Physical_Attributes_Bosun_Handoff.md. They remain separate feature passes. The WorldGen V2 handoffs remain deferred under the user's earlier direction; multiplayer integration should not silently rewrite world generation.

### Verification limits

Reviewed current source and package manifest and reconciled the recent compile/headless-check results. No transport was installed, no dynamic player prefab authored, no actual host/client connection launched, and no live multiplayer correctness was established by this review.

## 2026-10-10 — Networking checkpoint follow-up

The reconciliation above records the pre-implementation audit. MP.1 now uses NGO 2.7.0 / Unity Transport 2.6.0 and was validated by the user in the actual game build with two participants. See MULTIPLAYER_1_SESSION_FOUNDATION_CHECKPOINT.md.

MP.2 uses the user's CharacterPlayer prefab for a development movement fixture. Host-assigned identities, explicit local camera/input binding, sender-to-actor validation, existing host locomotion and non-simulated client pose replicas are implemented. See MULTIPLAYER_2_DYNAMIC_PLAYER_MOVEMENT_CHECKPOINT.md for verification and acceptance steps. Authored BoatScene/NodeScene player replacement, personal scene HUD binding, real shared-boat/world replication and coordinated scene loading remain open; the static movement fixture is not proof of those behaviors. The full handoff remains pending.

MP.2, including buffered pose smoothing, was subsequently validated by the user. MP.3 adds a server-owned shared-interaction fixture using existing generator/power, seat and cargo acquisition/drop implementations. Sender identity, range, per-target incarnation/revision, request replay/rate limits and exclusive claims are enforced on the host; typed state replicates resources, held cargo identities and occupancy to late joiners. Three-process runtime checks pass, including conflicting requests and disconnect cleanup. See MULTIPLAYER_3_SHARED_INTERACTION_CHECKPOINT.md for scope and manual acceptance. This establishes a contained interaction path; connected BoatScene/NodeScene loading and real sailing-boat/world replication are still open and should be the next integration checkpoint before expanding gameplay coverage broadly.

## 2026-10-11 — MP.3 acceptance and MP.4 integration ordering

The user validated MP.3 and authorized MP.4. MP.4 begins with the shared sailing-boat integration gap identified above: the authored boat, host-only physical/navigation simulation, passive pose/fluid/door replicas and authenticated helm orders. See MULTIPLAYER_4_SHARED_BOAT_CHECKPOINT.md. This is MP.4A; the full MP.4 coverage list and original handoff remain pending. Normal BoatScene/NodeScene/world bootstrap and the subsequent domain adapters must not be marked complete from the reversible MainMenu sailing test alone.


## 2026-10-11 — MP.4A acceptance and MP.4B item foundation

The user accepted the sailing-boat slice. Normal scene bootstrap inspection still finds local single-player boat/player restoration and the MainMenu session guard. Before enabling those paths, MP.4B replaces fixture-only item reconstruction with bounded complete item/loadout snapshots and authenticated generator-to-requester fuel transfers. See MULTIPLAYER_4_SHARED_ITEMS_CHECKPOINT.md for native tests, protocol limits, and manual acceptance. Normal connected scenes/world/HUD, the wider domain adapters and MP.5 lifecycle/save protocol remain pending; neither MP.4 nor the original handoff is DONE.

## 2026-10-11 — MP.4B acceptance and host world preparation

The user accepted MP.4B. NetworkWorldBootstrap now transfers a detached authoritative WorldMapSaveSnapshot with portable terrain, version/session/scene/epoch checks, chunk/checksum bounds, connected-participant readiness, late join and teardown. Native three-process staging and inventory/movement regressions pass. See MULTIPLAYER_4_WORLD_PREPARATION_CHECKPOINT.md. This is preparation only: actual scene loading, GameState application, authored actor/boat replacement, owner HUD binding and the MP.5 transition/save protocol remain open. Multiplayer New/Load remains gated; MP.4 and the handoff remain in progress.

## 2026-10-11 — Authored NodeScene entry connected

MP.4C2 now connects fresh host New Game to the normal NodeScene: host generation/capture, prepared-world client application before Awake, no client terrain/graph generation, catalog-validated passive boat spawning, shared boat adoption/replicas, dynamic participant actors after scene readiness, owner camera/inventory display, late join and Leave to MainMenu. Actual NodeScene standalone host/client/observer checks pass. See MULTIPLAYER_4_NODE_SCENE_ENTRY_CHECKPOINT.md for scope and manual acceptance. Ordinary scene interactions/boarding, live agents/world stats, other HUD/tool state, crew travel and durable saves remain pending. Travel and live-session save/load are gated; full MP.4 and the handoff remain in progress.

## 2026-10-11 — MP.4C2 acceptance and MP.4C3 boarding/stations

The user validated hosted New Game entering the normal NodeScene and authorized continuation. MP.4C3 extends the authenticated actor input stream with deck boarding gestures and adopts existing authored pilot chairs into host-owned occupancy/control. Client boarding presentation, station state and ownership replicate, including late join; actor despawn/disconnect releases station claims. Existing boat context, range, hull/interior/rail and installed helm capacity/health rules remain domain-owned. See MULTIPLAYER_4_BOARDING_STATIONS_CHECKPOINT.md for scope, isolated native verification, the test-only missing deck-point setup and manual acceptance.

The temporary network helm panel uses existing ramped simulation target APIs; the ordinary piloting cartridge still needs its adapter. Normal inventory/general interactions, hatches/ladders and the remaining MP.4 domains stay open, followed by MP.5 lifecycle/travel/save work. Schema now requires matching `DontSink/node-boarding/4` builds. No production authored scene/boat asset was changed for this slice. Full MP.4 and the original handoff remain pending.

## 2026-10-11 — Normal boat-entry follow-up

The user flagged the practical entry gap: ordinary interactables were still disabled, preventing boarding to test the station slice. The boat-entry adapter now restores owner targeting/prompts, host-authenticated boarding-door holds and door/gate/hatch actions, mouse-targeted pilot-chair claims, and host-side ladder intent. Non-simulated replicas use rendered box/circle shapes for hover picking. See MULTIPLAYER_4_BOAT_ENTRY_INTERACTIONS_CHECKPOINT.md for the actual authored-door three-process test, remaining scope and manual checks. Matching protocol is now DontSink/node-interactions/4; broader item/vendor/world commands and full MP.4/MP.5 coverage remain pending.


## 2026-10-12 — Boarding acceptance and normal modules

The user confirmed boarding works and authorized continued normal interaction coverage. MP.4C4 registers installed engine/generator/pump hardpoints, opens the existing owner module window, routes on/off and exact hotbar/fuel transfers to the host and replicates complete fuel/module state. Fresh New Game now explicitly preserves authored starting modules/power until the first capture; saved empty manifests remain authoritative. See MULTIPLAYER_4_NORMAL_MODULES_CHECKPOINT.md for bounds, native verification, save compatibility and manual checks. Protocol is now DontSink/node-modules/4. Storage, wider inventory/cargo, NPC/vendor, regular piloting UI and the remaining gameplay/lifecycle coverage remain pending.


## 2026-10-12 — Client visibility, debug spawning and drop-through blockers

The client now resolves boat interior visibility against rendered zones instead of passive Physics2D contacts. The host enables the existing hatch/ledge dropper for both actors using authenticated down input. Each owner's authored inventory debug UI now routes bounded catalog-item requests to the host; engine/generator/pump debug items install at a nearby compatible empty authored hardpoint with an additive client/late-join manifest. Other items use requester-owned acquisition; no unreplicated loose-world drop is permitted. See the usability correction section of MULTIPLAYER_4_NORMAL_MODULES_CHECKPOINT.md. Matching protocol is DontSink/node-debug-modules/5. Broader MP.4 gameplay and MP.5 lifecycle scope remain pending; user acceptance is still required.

## 2026-10-12 — MP.4C5 normal piloting and required prediction follow-up

The user accepted the visibility/debug/drop-through fixes and reported owner movement latency. Owner prediction/reconciliation is now required follow-up in the MP.2 checkpoint, preserving host shared-state authority and observer interpolation. MP.4C5 reconnects the original piloting runner/cartridge/HUD to replicated seat ownership, validated preset orders, bounded keyboard intent with expiry, host target/readout/status snapshots, and normal close-to-stand behavior. Temporary helm buttons are removed; F6 auto-install remains a testing bridge. See MULTIPLAYER_4_NORMAL_PILOTING_CHECKPOINT.md. Matching protocol is DontSink/node-piloting/6. Full multiplayer parity and the handoff remain pending.

## 2026-10-12 — F6 helm/rudder installation support

The user could not test normal piloting because debug auto-install excluded helms. F6 now supports HelmModule and RudderModule in addition to the existing engine/generator/pump whitelist, preserving host install rules and late-join replication. Native three-process testing starts with empty pre-wired helm/rudder hardpoints and passes installation followed by normal console controls and cleanup. No production boat wiring/prefab was changed. Matching protocol is DontSink/node-piloting/7; see the normal piloting checkpoint follow-up.


## 2026-10-12 sequential MP.4 inventory/storage/installation checkpoints

Three additional contained adapters are implemented; user acceptance is pending. See MULTIPLAYER_4_NORMAL_INVENTORY_CHECKPOINT.md, MULTIPLAYER_4_SHARED_STORAGE_CHECKPOINT.md and MULTIPLAYER_4_NORMAL_INSTALLATION_CHECKPOINT.md. They connect the normal personal inventory, installed plain storage, and install/held-remove controls to host-owned models. MP.4 is still in progress: physical cargo actions, NPC/vendor interactions, broader world actions and live module families remain. MP.5 travel/save/reconnect remains pending. The user paused full piloting acceptance until BoatScene execution is available; owner movement prediction remains a required follow-up.


The sequential checkpoints have now passed final protocol-11 host/client/late-join native suites plus the existing module/fuel/debug/drop-through regression. Editor/player compilation is clean and all 985 C# sources match the isolated native build. Protocol 11 changes transfer fingerprints to exclude live charge/water measurements while preserving the latest authoritative values. User presentation acceptance remains pending; no commits were made. Durable originating module-item persistence and general link commands remain follow-up scope.


## 2026-10-12 second three-checkpoint continuation

The user authorized three more checkpoints and a test list for all six. Normal helm/station L linking, ordinary loose-world-item F pickup/Q drop, and charged Hands cargo Q throwing are implemented. They reuse existing link, acquisition, release and throw domain behavior with authenticated per-owner commands. World-item replicas are passive/interpolated; only the host simulates collisions. Selections/holds have generation, identity, access, expiry and replay guards. Final protocol is DontSink/node-loose-items-throw/14; both peers must rebuild.

See MULTIPLAYER_4_HELM_LINKING_CHECKPOINT.md, MULTIPLAYER_4_WORLD_ITEMS_CHECKPOINT.md, MULTIPLAYER_4_CHARGED_THROWING_CHECKPOINT.md and MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md. Five native suites (new combined checkpoints, inventory, installed storage, install/remove, existing module controls) all pass on host/client/late observer with zero runtime errors. Editor/player compilation and all 988 source-copy comparisons pass. Validation players exited; production scene/prefab authoring was unchanged by this continuation. Changes remain uncommitted and user presentation acceptance is pending.

MP.4 remains in progress: general world containers, treasury/securing, NPC/vendor actions and live module/tether/bell families still need adapters. MP.5 travel/save/reconnect, durable originating module/link persistence and owner movement prediction/reconciliation remain pending. Full BoatScene/piloting manual acceptance is still on hold at the user's request. No seventh checkpoint was started.
