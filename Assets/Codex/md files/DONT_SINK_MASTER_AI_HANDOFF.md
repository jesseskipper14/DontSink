# DON'T SINK — MASTER AI / CODEX HANDOFF

> **Purpose:** This document is the durable project handoff for AI-assisted development of **Don't Sink**.
>
> It is intended to let a new coding agent enter the repository with the architectural context, design intent, implementation history, workflow rules, active constraints, and collaboration style needed to make safe changes without requiring the full historical ChatGPT conversation archive.
>
> **Read this before modifying code.**
>
> Last consolidated: **2026-10-01**

---

# 0. EXECUTIVE SUMMARY

**Don't Sink** is a solo-developed **Unity 6 / URP 2D / C#** survival, exploration, navigation, trade, and boat-simulation game set in a flooded world.

The player lives on and operates a physically simulated boat, travels between locations, manages cargo and survival resources, explores underwater areas, salvages valuable items, trades with settlements, upgrades the boat, and gradually reconstructs knowledge of the world and sky.

The long-term multiplayer target is **cooperative play aboard one shared boat**, with a **host-authoritative simulation**. Networking transport/package is intentionally not yet implemented. Current work hardens architecture so later multiplayer mostly becomes:

**client intent → authenticated requester → existing authority API → replicated state → local presentation**

The codebase values:
- Runtime gameplay authority over UI-owned logic.
- Explicit state and persistent identities.
- Reusable systems instead of one-off hacks.
- Physical interaction where it improves gameplay.
- Deterministic generation where appropriate.
- Save/load correctness.
- Multiplayer-aware seams even before networking exists.
- Narrow, auditable implementation passes.
- Preserving current behavior unless a task explicitly changes it.

The most important workflow rule is:

> **Never reconstruct or replace an existing class from memory, an old chat, or an older file version. Inspect the exact current repository version first.**

This rule exists because several subsystems have evolved concurrently and old replacement files can silently erase unrelated work.

---

# 1. AGENT OPERATING RULES

These rules are not suggestions. Treat them as project constraints unless the user explicitly changes one.

## 1.1 Exact-current-class rule

Before modifying an existing class:

1. Read the **exact current file** from the repository.
2. Search its important references/callers/dependencies.
3. Preserve unrelated behavior.
4. Make the smallest coherent change needed.
5. Do not reconstruct the class from historical notes.
6. Do not assume a class seen in a prior conversation is still current.

When working outside direct repository access, the user historically supplied the newest file before any replacement was authored. With Codex/repository access, this should become automatic: inspect the file directly.

## 1.2 Prefer narrow passes

The user strongly prefers work broken into small, focused implementation passes.

Good:
- “Harden authority around bell containment.”
- “Fix flotation force limiting.”
- “Add persistence for deployed flotation bags.”
- “Implement celestial coordinate projection.”

Bad:
- “While I was here I rewrote interaction, inventory, save/load, and module architecture.”

Narrow passes make testing and regression isolation much easier.

## 1.3 No unsolicited refactors

Do not perform opportunistic cleanup unless:
- it is necessary for the task,
- the user explicitly asks for it,
- or the current structure would make the requested implementation unsafe.

If you identify a worthwhile cleanup, document it as a TODO instead of quietly folding it into unrelated work.

## 1.4 Preserve single-player behavior

Multiplayer hardening must not break or complicate current single-player behavior unnecessarily.

The project is still primarily tested as a single-player game.

## 1.5 Runtime authority, UI observer

Gameplay state belongs in runtime systems.

UI:
- reads state,
- sends intents,
- presents results.

UI should not become the authoritative owner of important gameplay data.

## 1.6 Multiplayer direction

The intended multiplayer model is:

- One shared boat.
- Host authoritative simulation.
- Clients send intents.
- Requester identity must eventually come from authenticated transport/session information.
- Never trust a client-authored requester/player ID.
- Shared authoritative state is replicated.
- Local-only presentation remains local.
- Generic systems are not globally authority-gated.
- Individual shared objects/systems opt into authority where required.

Do **not** add a networking package merely because a system is being hardened for multiplayer.

## 1.7 Avoid global player assumptions

Prefer:
- explicit player/interactor references,
- stable participant IDs,
- requester-aware APIs,
- per-player state where behavior is player-specific.

Avoid:
- `FindObjectOfType<Player>()` as authority logic,
- assumptions that only one player can exist,
- globally toggled collision or interaction state when the state should belong to one player or one bell.

## 1.8 Reusable systems over special cases

The user consistently prefers reusable systems.

Examples:
- Generic tether/winch architecture rather than “diving bell cable code.”
- Generic flotation bags rather than bell-only emergency buoyancy.
- Generic interaction filtering rather than bell-specific interaction hacks.
- Shared current/disturbance fields for vegetation and creatures.
- Persistent world-coordinate knowledge systems rather than node-unlock special cases.

## 1.9 Preserve physical/gameplay meaning

Do not move gameplay truth into presentation layers.

Examples:
- A rendered animation frame should not determine whether a knife hit occurs.
- Sprite sorting should not determine containment.
- Visual bag inflation should not be the sole source of buoyancy state.
- A UI icon should not own whether an item is equipped.
- A scene-local representation should not replace persistent item identity.

## 1.10 Use Git defensively

Before large or risky work:
- ensure the repository has a clean checkpoint,
- inspect the diff after changes,
- avoid touching unrelated files,
- make it easy to revert.

The user is comfortable using Git as a safety net and prefers transparent diffs.

---

# 2. COLLABORATION / RESPONSE STYLE

This section describes how to work effectively with the user during development.

It intentionally excludes unrelated personal information and private platform-level instructions.

## 2.1 Preferred working relationship

The user has historically called the assistant **“Keel.”**

The preferred dynamic is collaborative and technical, with enough personality to avoid sounding like documentation generated by a beige office printer.

The user is comfortable with:
- light sarcasm,
- dry humor,
- occasional profanity,
- direct disagreement when warranted.

Do not become patronizing, overly therapeutic, or artificially deferential.

## 2.2 Be direct

The user values:
- clear conclusions,
- concrete next steps,
- explicit reasoning about architecture,
- practical tradeoffs.

Avoid excessive hedging when the engineering answer is reasonably clear.

If there are multiple valid approaches, explain the meaningful tradeoff rather than presenting ten nearly identical options.

## 2.3 Do not repeat answered questions

The project often spans multiple long conversations.

Before asking for information:
- inspect the repository,
- inspect this handoff,
- inspect current task context.

If the answer is already available, use it.

## 2.4 Explain why, not only what

The user wants to understand the architecture, not merely paste code.

When a design choice matters, explain:
- what owns the state,
- why that ownership is correct,
- what future system it supports,
- what failure mode it prevents.

For trivial mechanical edits, keep explanation short.

## 2.5 Complete drop-in code is preferred when appropriate

Historically, when code was supplied through chat, the user preferred:
- complete replacement classes,
- complete new classes,
- or a ZIP containing the full pass,

rather than scattered fragments that require manual reconstruction.

With direct repository access, edit files in place, but still summarize:
- files changed,
- behavior changed,
- any inspector/setup steps,
- how to test.

## 2.6 Inspector/setup work must be explicit

Unity bugs are frequently caused by scene/prefab setup rather than code.

When a change requires:
- assigning a serialized field,
- changing a layer,
- adding a collider,
- adding a component,
- linking a hardpoint,
- updating a prefab,
- modifying sorting layers,
- changing a catalog,

state that clearly.

Do not hide required Inspector work in prose.

## 2.7 Prefer testable milestones

The user likes to implement a pass, test it, and then choose the next pass.

Whenever practical, end a coding pass with a compact regression checklist.

## 2.8 Keep future ideas separate from current scope

The project has many excellent future ideas. That does not mean every task should become a twelve-week systems rewrite.

Use categories such as:
- **Current pass**
- **Deferred**
- **Future**
- **Non-goal**

This has worked well in prior work.

---

# 3. PROJECT IDENTITY

## 3.1 Engine / technology

- Engine: **Unity 6**
- Known project version from diagnostics: **6000.0.65f1**
- Render pipeline: **URP 2D**
- Language: **C#**
- Target platform: primarily PC
- Core simulation style: physical 2D boat/world interactions
- Saves: JSON-based persistent game state
- Primary runtime scene seen in diagnostics: `NodeScene`
- Travel gameplay also uses `BoatScene`

## 3.2 Genre / fantasy

Broadly:

**2D side-view flooded-world survival + boat management + exploration + trade + underwater salvage + celestial navigation**

The player:
- lives aboard a modular boat,
- travels between settlements/regions,
- manages fuel/resources/cargo,
- dives or otherwise explores,
- salvages resources and artifacts,
- trades,
- upgrades,
- gradually learns the world,
- uses stars as a real navigational/progression system.

## 3.3 Tone

The world is flooded and dangerous.

Loss should matter, but the game generally aims for:
- harsh consequences,
- recoverability,
- a safety floor that prevents permanent campaign collapse.

The **Money Chest** is the clearest example of this philosophy.

---

# 4. HIGH-LEVEL GAME LOOP

The historical core loop is:

1. **NodeScene / dock / settlement**
   - trade,
   - provision,
   - repair,
   - configure boat,
   - acquire information,
   - manage cargo,
   - prepare route.

2. **BoatScene / journey**
   - operate boat,
   - navigate,
   - encounter weather/sea hazards,
   - manage power/fuel,
   - dive,
   - harvest resources,
   - salvage,
   - deal with physical cargo and flooding,
   - possibly become lost.

3. **Arrive at settlement / region**
   - sell goods,
   - recover,
   - learn more of world,
   - upgrade,
   - continue progression.

Long-term progression combines:
- economic growth,
- boat upgrades,
- survival capability,
- underwater capability,
- geographic knowledge,
- celestial/star-map knowledge.

---

# 5. WORLD MAP / TOPOGRAPHY

## 5.1 Legacy graph foundation

The project originally used a graph-oriented world generator.

Important historical components include:
- `WorldMapGraphGenerator`
- `WorldMapRuntimeBinder`
- `MapNodeRuntime`

Typical generator parameters included:
- cluster count,
- nodes per cluster min/max,
- cluster spacing,
- cluster radius,
- jitter,
- extra edges,
- inter-cluster edges.

Historically:
- `StartDock` = leftmost node
- `Destination` = rightmost node

This node graph remains useful for settlements/routes/economic structure, but it is **not** the ultimate authority for continuous geography.

## 5.2 Continuous coordinate authority

Current design direction:

> The topography field's continuous **WorldBounds / graph-space coordinates** are the authoritative global coordinate system.

Do **not** derive global geography from graph-node extents.

The topography world currently defaults to a centered rectangle around approximately:
- width: 400 graph/world-map units
- height: 250 graph/world-map units

Nodes, POIs, navigation coordinates, and knowledge systems should all refer to this shared continuous space.

## 5.3 World navigation truth

`GameState.worldNavigation` is intended to represent authoritative continuous world-position/navigation truth.

Important distinction:

**True position** and **player belief/knowledge** are separate.

The player may:
- physically be at one continuous coordinate,
- believe they are elsewhere,
- have incomplete geographic knowledge,
- have incomplete celestial knowledge.

This separation is foundational to navigation gameplay.

## 5.4 Topography-first generation

The world-map system evolved into a topography-first generator with concepts such as:
- islands,
- volcanoes,
- trenches,
- basins,
- sea-level classification,
- deep water,
- shallow water,
- land.

Known tooling/features:
- cartridge-style map window,
- drag / zoom / pan,
- debug key historically `M`,
- heatmap buttons on the left,
- node stats on the right,
- buff/event panel,
- topography texture,
- contour texture,
- classification overlay,
- toggles for topography and contours.

## 5.5 Baked topography

Historical implementation:
- packed `ushort` height data,
- around 512×512 baked map,
- baked asset roughly 33 MB,
- save representation around 970 KB,
- runtime cache.

This may have evolved. Inspect current implementation before relying on exact sizes.

## 5.6 Water targeting

The generator includes or planned:
- automatic water-percentage targeting,
- deep-water bands,
- biome classification.

Known early biomes:
- Coral Shelf
- Open Bluewater
- Drowned Ruins
- Temperate Island Chain

## 5.7 Node placement

Node placement should:
- respect land/water constraints,
- avoid invalid placement,
- repair cross-cluster crowding.

## 5.8 Outer map boundary philosophy

Do not present the true world boundary as an obviously known rectangle.

Preferred future presentation:
- unknown/unmapped fog at outer extents,
- the player should not initially know where the world “ends.”

This is a knowledge/presentation refinement, not a blocker for core coordinates.

---

# 6. WORLD KNOWLEDGE / FOG / MAP DISCOVERY

## 6.1 Knowledge states

Historical route/map knowledge states include:

- Unknown
- Rumored
- Partial
- Known

## 6.2 Two-layer fog concept

The map has a conceptual split between:
- **surface/geographic shroud**
- **survey/underwater shroud**

Underwater survey knowledge may reveal:
- contours,
- seafloor information,
- POIs,
- resource information.

## 6.3 Geographic knowledge is not celestial knowledge

These are separate progression layers.

A player can:
- know the coastline but not the sky,
- recognize stars but not the terrain,
- possess fragments of one map but not the other.

Keep their persistence and reveal logic logically independent even though they share coordinates.

---

# 7. STAR MAP / CELESTIAL NAVIGATION

This is a major project pillar.

## 7.1 Core concept

The geographic world and celestial map share **one continuous 1:1 coordinate space**.

Each region of the world has a corresponding celestial region.

The star map is not merely cosmetic UI. It is intended to become one of the primary navigation/progression systems.

## 7.2 Celestial field

The sky is based on a deterministic procedural celestial field.

At a given world coordinate:
- the same persistent celestial features should be observable,
- the projected visible sky shifts subtly as the player moves,
- exact coordinates reproduce the same local celestial pattern.

The celestial field includes:
- ambient stars,
- distinctive recognizable landmarks/star patterns.

Transient sky elements include:
- sun,
- moon,
- clouds,
- weather.

Do not bake transient weather/daylight into persistent celestial identity.

## 7.3 Observation window

The visible BoatScene sky is a **projection/window into the larger celestial field**.

Desired feel:
- movement of stars should be almost imperceptible moment to moment,
- but noticeable after meaningful travel, roughly on the order of minutes.

Viewport scale should be tuned experimentally.

## 7.4 Charting

Preferred charting interaction:

The player uses a cartridge/instrument overlay over the **real visible night sky**.

Likely actions:
- pan,
- zoom,
- find distinctive stars,
- align 3–4 notable stars/features,
- record/chart the region.

Conditions affecting observation:
- daylight,
- clouds,
- haze,
- horizon obstruction,
- weather.

Night is the primary charting window.

Optional variations may include explicit target-coordinate search tasks, but they are not the core concept.

## 7.5 Map fragments

Current direction:

Fragments are **arbitrary/overlapping pieces of evidence**, not fixed grid cells and not node unlock tokens.

Recent work includes:
- persistent chart fragments,
- shared evidence rather than hotbar-style item behavior,
- deterministic torn-paper/ink visuals,
- saved recording orientation,
- fragment preview tooling,
- preserving physical alignment for later reconstruction mechanics.

## 7.6 Progression sources

Star-map knowledge can come from:
- manual night charting,
- exploration,
- ruins,
- treasure,
- traders,
- quests,
- purchased fragments.

Purchasing a fragment may effectively reveal or help reconstruct a route toward a new cluster/region, but the underlying representation should remain continuous evidence, not a simplistic node-unlock flag.

## 7.7 Lost navigation

Long-term navigation gameplay aims to allow genuine off-course travel.

The player may need to:
- compare visible sky with charted evidence,
- estimate location,
- reacquire route.

Getting lost should be emergent gameplay rather than a binary failure screen.

Consequences can include:
- longer travel,
- extra fuel consumption,
- supply depletion,
- reaching an unexpected region,
- danger,
- recovery through celestial/navigation skill.

---

# 8. BOATSCENE NAVIGATION

## 8.1 Local vs global coordinates

`BoatScene` X should be understood as **local physical travel space**, not literal global longitude.

The boat can physically move in local scene space while navigation systems project that motion into global world coordinates.

## 8.2 Off-rails travel

Routes are intended lines, not rails.

The boat should ultimately be able to:
- wander off route,
- accumulate cross-track error,
- deviate heading,
- fail to intersect the destination approach region.

Destination arrival should not simply trigger because the local scene reaches its right edge.

## 8.3 Navigation state

Design concepts include:
- along-track progress,
- cross-track error,
- heading/course deviation,
- global continuous coordinates,
- destination approach corridors/regions.

## 8.4 Future multiplayer seam

Navigation truth should be authoritative.

Clients eventually render replicated navigation state rather than independently simulating world truth.

`GameplayAuthority.IsAuthoritative` already exists as an important seam in newer architecture.

---

# 9. BOAT PHYSICS

## 9.1 Buoyancy

Important historical component:
- `BuoyancyPolygonForce`

Design:
- polygon/slice-based buoyancy,
- physical force application,
- momentum coupling.

## 9.2 Waves

Important systems:
- `WaveField`
- `WaveManager`

Water is represented as a mesh from surface toward bottom and can recenter to simulate effectively infinite travel.

## 9.3 Player pushing boat

Known deferred issue:

A boarded player can sometimes push/propel the boat by running into interior walls.

This should be investigated systematically through:
- player/boat collision response,
- friction/contact handling,
- collider configuration,
- possibly physics materials.

Do not “fix” this with a brittle one-off force cancellation unless necessary.

## 9.4 Engine authority

Deferred architectural improvement:

Propulsion strength should eventually move away from boat-level `ThrottleForce` tuning and onto installed `EngineModule` components.

Installed engines should determine available thrust.

---

# 10. BOAT BUILDER / MODULE SYSTEM

## 10.1 Modular boat

Known categories historically include:
- Hull
- Wall
- Hatch
- Door
- Chair
- Ladder
- Hardpoint
- Storage
- Cargo

Grid:
- approximately 0.5 world-unit increments.

Builder supports:
- placement,
- rotation,
- repair spans,
- shell visibility,
- module anchors,
- hardpoint/controller linking.

## 10.2 Module examples

Current/known module concepts include:
- Engine
- Generator
- Pump
- Turret
- Storage
- Winch
- Diving-related modules

Boat has a shared power reservoir/system.

## 10.3 Hardpoints

Hardpoints are foundational.

Known hardpoint work includes support for:
- module attachment,
- winch/tether payloads,
- hardpoint-to-hardpoint links.

## 10.4 Helm / piloting chair

There has been work around explicitly linking helm and piloting chair/controller relationships in the BoatBuilder.

When modifying current builder tooling, inspect the exact latest editor code because this subsystem has experienced version mismatch problems in prior chats.

## 10.5 Root-object hierarchy cleanup

Pinned future cleanup:

Refactor scene “god objects” into root objects with organized child objects.

Goal:
- make systems easier to copy/apply across scenes,
- avoid unrelated child changes overwriting each other,
- reduce scene-merge friction.

Not part of unrelated current passes unless explicitly requested.

---

# 11. POWER

Power is a boat-level shared resource with modules participating in consumption/production.

Known module categories:
- Generator
- Engine
- Pump
- Turret
- powered winch/control systems

Diving bell control behavior was intentionally designed around power:
- Powered: automatic control available.
- Unpowered: manual control available.

Power architecture should remain compatible with future host-authoritative multiplayer.

A previous audit sequence identified **Modules + Boat Power** as an important next multiplayer-hardening target after the diving-bell passes.

---

# 12. PLAYER MOVEMENT / SURVIVAL

## 12.1 Ground movement

Historical player movement details:
- coyote time ~0.08 s
- input buffer ~0.08 s
- jump blocked above steep-angle threshold around 60°

Treat exact tuning as subject to current inspector/code values.

## 12.2 Swimming

Historical baseline:
- max X ~2.2
- X acceleration ~25
- upward swim ~18
- dive ~22
- max Y ~3.0

Sprint multipliers historically around:
- 1.4
- 1.3
- 1.2

Again: inspect current tuning.

## 12.3 Survival state systems

Known systems include:
- `ExertionEnergyState`
- `AirState`
- `OxygenationState`

Historical model:
- Exertion max ~100
- Air base max ~100
- SpO2-style oxygenation layer

Low oxygen can lead to:
- critical oxygenation,
- unconsciousness after sustained critical condition,
- spectator camera,
- respawn.

## 12.4 Exertion labels

Planned/used states include:
- Resting
- Calm
- Active
- Winded
- Exerted
- Redlining

---

# 13. PLAYER BUFFS / WEARABLES

## 13.1 Wearable slots

Known single slots:
- Backpack
- Belt
- Body
- Head

Multi-slot examples:
- Diving suit: Body + Head
- Combat suit: Body + Belt + Backpack

Equipment should reject invalid occupancy conflicts.

## 13.2 Diving suit

Historical design:
- external air source with charges,
- approximate swim bonus,
- air/oxygen consumption modifiers.

Exact values should remain data-driven.

## 13.3 Buff architecture

Known architecture:
- `PlayerBuffDefinition` ScriptableObject
- providers apply/remove buffs
- baseline `PlayerAttributeProfile`

Potential attributes:
- air capacity
- air drain
- swim speed
- run speed
- stamina
- charisma/trade
- concentration/minigame performance
- piloting
- salvage efficiency

## 13.4 HUD

Planned:
- Player Buff icon bar
- Affliction bar

---

# 14. INVENTORY / ITEM IDENTITY

## 14.1 Item identity

Important concept:
- `ItemInstance`

Persistent item identity matters.

Physicalization should not casually destroy/recreate semantic identity.

This is especially important for:
- sacred items,
- diving bell storage/deployment,
- cargo,
- nested containers,
- persistence.

## 14.2 Cargo

Crates/cargo are physical.

Known behaviors:
- hands-only carry,
- racks store cargo,
- crates can persist,
- nested chest/storage state persists.

## 14.3 Storage

Known module types:
- Locker: fixed storage
- Rack: physical/container storage

Drag/drop behavior:
- dragging onto rack inserts,
- dragging off removes,
- module uninstall blocked if it still contains items.

## 14.4 Inventory drag hardening TODO

Pinned end-of-list cleanup:

`InventoryDragController` needs hardening around:
- unresolved-item safety on `CancelDrag`,
- truly transactional swap rollback,
- safe displacement in `TryDepositSingleInto`,
- deterministic valid world-drop-target selection when colliders overlap,
- safe cleanup of active drag during UI/scene lifecycle changes.

A specific known issue:
- ballast drag/drop targeting can be blocked by unrelated overlapping bell colliders because the controller historically takes the first world-drop target/collider.

Fix this generically rather than adding bell-specific collider exceptions.

---

# 15. CARGO SECURING

Cargo securing is a gameplay system, not merely decoration.

Known zone types:
- `CargoBayZone` — resizable
- `BoatCleatZone` — fixed

Securing uses a timing minigame.

Historical quality thresholds:
- “Perfect” under ~5 ms
- failure above ~50 ms

Rope consumption:
- Secure: ~3
- Fasten: ~1

Securing can degrade due to:
- time,
- impacts.

Items generally require grounded/support conditions to secure.

Treat tuning values as current-data dependent.

---

# 16. INTERACTION SYSTEM

## 16.1 General philosophy

Interactions should be:
- per-interactor aware,
- context-sensitive,
- reusable,
- safe for future multiplayer.

## 16.2 Prompt refresh bug

Known historical issue:

After an interaction such as “Press E to open,” prompt text can disappear and fail to refresh until the player exits and re-enters interaction range.

This was pinned for cleanup.

## 16.3 Multiple interactables on one transform

Pinned architectural cleanup:

`Interactor2D` historically assumed one `IInteractable` per transform.

This caused real failures when a flotation-bag prefab contained:
- `WorldItemContainerInteractable`
- `DeployableFlotationBag`

The container interactable masked the bag’s Inflate interaction.

Required direction:
- safely support multiple `IInteractable` implementations on the same hovered object,
- evaluate/filter/select among them,
- do not solve this with a flotation-specific exception.

## 16.4 Module interaction colliders

Pinned Inspector cleanup:

Fix interaction colliders on modules.

This is known largely as setup work and has been repeatedly deferred/forgotten.

---

# 17. UNDERWATER RESOURCES / SALVAGE

## 17.1 Design goal

Baseline underwater resources should exist in every BoatScene.

POIs should:
- weight/enrich resource placement,
- not hard-gate all resources.

## 17.2 Risk / reward

Underwater loot should:
- scale value/risk with depth and rarity,
- be physical,
- be carryable,
- persist appropriately,
- add mass,
- create greed-driven risk.

Large salvage may require:
- cranes,
- winches,
- other boat systems.

Dangerous high-value objects are desirable.

## 17.3 Diving is optional progression

Diving should not be mandatory for basic star-map progression.

Alternative progression routes should exist.

Diving should offer:
- strong rewards,
- unique rewards,
- shortcuts,
- optional power.

## 17.4 Resource spawning

Known components:
- `ResourceCatalog`
- `UnderwaterResourceSceneSpawner`

Spawner aligns resources with ground.

Spawn area historically derived from an `EdgeCollider2D`.

Resources can be:
- Collectables: click harvest
- Extractables: drill hold

Harvest interaction was unified around time-based coroutine behavior.

## 17.5 Tools / capabilities

Known:
- `ToolCapabilityDefinition`

A drill checks tool capability rather than hard-coded item name.

## 17.6 Battery

Known battery concept:
- utility slot,
- around 100 charge,
- drill drains charge,
- UI charge bars.

Future tools may include:
- knife,
- pump,
- harvester,
- salvage tools.

---

# 18. MONEY CHEST

The Money Chest is a “sacred” item and receives special continuity guarantees.

## 18.1 Core rules

- Hands-only.
- Dedicated secure slot.
- Associated mini-game overlay.
- Multiple coin denominations/sprites.
- Only one **Active** chest pays.

## 18.2 Loss / recovery loop

If the player leaves a scene without the chest:
- the chest becomes **Lost**.

Death can count as loss.

If only a Lost chest exists:
- a replacement can be offered in a later scene.

The Lost chest remains meaningful.

If the player physically recovers the lost chest and brings it into the boat zone:
- interaction can merge/recover it into the active treasury.

Only one Active chest should exist.

## 18.3 Persistence

Treasury authority lives under `GameState`.

Known completed concerns:
- trade integration,
- lost/replacement/recovery,
- secure-slot preference,
- secured save/load,
- floor snap behavior,
- dynamic weight/buoyancy.

## 18.4 Follow-up concerns

Potential future improvements:
- friction/drag on slopes,
- buoyancy based on money load,
- robust lost-chest placement bounds.

---

# 19. TETHER / WINCH ARCHITECTURE

This architecture was designed to be reusable beyond the diving bell.

## 19.1 Principles

- Payloads are physical Rigidbody objects.
- Lines are minimally simulated and typically non-colliding.
- Tether constraints expose meaningful tension/load.
- Overload can be measured before snapping.
- Cut payloads remain recoverable.
- Travel can require deployables to be stowed.
- Line itself can be represented as inventory/resource with:
  - slots,
  - length,
  - mass,
  - strength.

## 19.2 Hardpoint support

Known additions include:
- `HardpointType.Winch`
- tether payload handling
- hardpoint↔hardpoint linking

## 19.3 Sounding line

A handheld sounding-line V1 was completed previously using segmented line inventory.

Inspect current implementation before touching it.

---

# 20. DIVING BELL

The diving bell has undergone substantial implementation and multiplayer hardening.

## 20.1 General behavior

The bell is a physical object:
- while docked,
- while deploying,
- while deployed.

It supports:
- boarding,
- internal operation,
- auto-redock behavior,
- sorting/presentation changes,
- BellItems,
- collision proxy behavior (“Ghost Bell” historically).

## 20.2 Ownership invariant

A particularly important invariant:

**Storage ItemInstance → deployed WorldItem → physical dock capture → same ItemInstance back to storage**

The deployment lifecycle should not duplicate or replace the semantic bell item.

During deployment:
- storage slot remains logically reserved as appropriate.

During docking:
- mass/state handoff should occur carefully,
- world item should be cleared at the correct time.

## 20.3 Docking states

Known lifecycle includes concepts such as:
- Free
- Capturing
- Docked

Smooth authoritative capture was implemented.

Transactional rollback matters if docking aborts.

## 20.4 Boarding / unboarding

Historical improvements:
- snap player cleanly to deck/interior,
- remove presentation flashes,
- block wall-phase cases,
- fix ladder-down/drop behavior,
- require deliberate deck unboard,
- restore boarding-door prompts.

## 20.5 Bell power / controls

Internal controls support:
- Lower
- Stop
- Raise
- Quick Release
- Cut Line

Behavior:
- Powered: automatic control.
- Unpowered: manual hold control.
- Stop locks.
- Releasing without lock can quick-release.
- Quick release/cut are mechanical/emergency behaviors.

## 20.6 Authority hardening passes

Diving-bell multiplayer hardening passes **14A–14F** were completed.

Important outcomes:
- shared bell/tether simulation authority-gated,
- requester-aware winch operations,
- explicit authority boundaries,
- autonomous containment decisions authority-only,
- local presentation separated from authoritative state,
- keyed per-player persistence,
- future transport must authenticate requester identity.

Do not repeat this work by adding global authority gates around generic simulation systems.

## 20.7 Known historical restore concern

There was a tether-load spike/snapping concern immediately after restore.

If still relevant in current code, solutions should use:
- restore-time protection,
- spike filtering,
- sane break evaluation,
rather than disabling break behavior globally.

## 20.8 Sorting bug

Historical bug:
exiting a submerged bell left player sorting presentation wrong:
- BoatForehull / order ~146
instead of:
- WorldPlayer / order ~1000

Boat boarding/unboarding could reset it.

A focused sorting/presentation cleanup was pinned.

Check whether current repo has already resolved it.

## 20.9 Emergency flotation

Deferred concept:
- diving bell emergency flotation belongs with non-powered emergency controls.

Generic flotation bags now provide much of the needed systemic foundation.

Do not create a second incompatible buoyancy system unnecessarily.

---

# 21. FLOTATION / LIFT BAGS

This system reached a relatively mature state in late September 2026.

## 21.1 Prefab / visuals

Bag prefab includes:
- packed sprite/state,
- inflated sprite/state,
- packed collider,
- scalable inflated collider,
- visible tether.

Interaction historically included:
- inflate,
- pick up,
- hold to remove,
- cut tether.

Placement preview / better HUD hint remained desirable.

## 21.2 Buoyancy limiter history

Initial buoyancy used a hard acceleration cap around 40 m/s².

This caused oscillation, especially with small masses.

A tension-based limiter was tried and failed in blocked-bag situations.

The more successful final direction:

> If fully submerged **and tethered**, allow full lift; otherwise scale buoyancy by submersion until a near-full threshold.

This preserved:
- usable tethered lift,
- reasonable surface behavior,
- less oscillation.

## 21.3 Geometry fix

Inflated bag force geometry needed to track actual inflated dimensions.

Updating force-body width/height to match inflated shape significantly improved behavior.

## 21.4 Multi-bag behavior

Multiple bags should sum forces correctly.

A bag need not be at maximum tether extension for its lift to contribute.

This was important for lifting the bell.

## 21.5 Angular inertia

Additional angular inertia/damping work reduced unrealistic spinning of lifted payloads.

## 21.6 Deflation behavior

Detached bags deflate relatively quickly to avoid clutter.

Partial deflation around 80% was used to ensure they lose enough buoyancy to sink.

## 21.7 Persistence

Deployed flotation bags gained explicit Save→Load persistence.

Important behavior:
- explicit save/load persists them,
- ordinary scene transitions do not necessarily preserve them the same way.

Persistence captures:
- pose,
- lifecycle,
- inflation,
- tether/attachment targets.

Two-pass restoration supports bag chains.

Missing targets should fail soft.

## 21.8 Flotation non-goals

At the time the feature stabilized, non-goals included:
- decompression sickness,
- tether breakage complexity,
- refill/reuse mechanics,
- dedicated bell-only flotation,
- authored attachment sockets.

## 21.9 Seafloor clipping

Known generic safety issue:

Exiting the bell on the seafloor could place the player into terrain.

Preferred direction:
- a generic player/seafloor out-of-bounds push-up safety,
- not a bell-specific teleport.

---

# 22. FLOODING / INTERIOR WATER

Interior flooding is physically meaningful.

Known architecture:
- `BoatWaterContextResolver`
- compartment-specific interior water
- interior buoyancy/swim behavior
- head-submersion checks
- persistent flood levels using stable compartment IDs

Boat flooding and module damage are part of existing broader systems.

---

# 23. AUDIO / MUSIC

## 23.1 Music philosophy

All main music tracks are intended to share:
- the exact same length,
- the same harmonic/key framework.

Historical base-theme discussion identified **A minor**, though the structural synchronization is more important than the label.

This makes layered transitions much easier.

## 23.2 Scene assignment

Tracks should be **data/config assigned to scenes or states**, not hard-coded in scene-specific logic.

## 23.3 Underwater treatment

Going underwater should:
- muffle/filter music,
- progressively reduce/fade music with depth.

A deeper underwater layer may include:
- heartbeat or pressure-like ambience.

## 23.4 Time of day

A future time-of-day hook is important because the project has a physical sun position.

Audio/lighting/world presentation should eventually respond to the shared day/night system.

---

# 24. WATER SHADER / VISUALS

Historical water shader discussion:

The existing shader’s **depth scaling** was considered useful.

Other effects such as:
- foam,
- sparkles

were less successful and not core.

Preferred direction:
- preserve useful depth behavior,
- consolidate rather than blindly discard,
- improve motion cues if needed.

Navigation/world-motion visual ideas have included:
- disappearing wake,
- infinite foam/motion cues,
- waves/disturbance,
- keels and water interaction.

Do not assume the old foam/sparkle implementation is desired just because it still exists.

---

# 25. ANIMATION PHILOSOPHY

## 25.1 Knife

Preferred proof of concept:
- procedural knife swing,
- pivot/arc trajectory,
- animation curve,
- gameplay hit timing separate from visuals.

Do not infer hit state purely from sprite/transform presentation.

## 25.2 Hatch

Frame animation/Piskel is appropriate for authored mechanical sprite states.

However, many final sprite animations are intentionally deferred because art is not final.

## 25.3 Sea grass

Preferred system:
- reusable URP 2D shader/procedural motion,
- ambient sway,
- currents,
- local disturbances from player/creatures/bell.

Avoid individually authored sprite-frame animation for dynamic vegetation response.

## 25.4 Kraken

Kraken/tentacle implementation has been discussed but is intentionally parked.

Likely future direction:
- multiple procedural tentacle chains,
- shared root/body authority,
- local targets/currents,
- visual motion separate from gameplay collision authority.

Do not spend current scope on it without explicit request.

---

# 26. NPC / AGENT / CREATURE FOUNDATIONS

The project has existing prototypes/foundations for:
- NPC agents,
- fish,
- world relevance systems.

Roadmap material has included future work around:
- relevance registry,
- more efficient activation,
- vendor polish.

Inspect current code before making assumptions about maturity.

---

# 27. MARKET / ECONOMY

## 27.1 Node state

Known `MapNodeState` dimensions:
- DockRating
- TradeRating
- Prosperity
- Stability
- Security
- FoodBalance

Nodes can have:
- active buffs,
- market state,
- resource pressures.

## 27.2 Market generation

Known components/concepts:
- `ResourceCatalog`
- `PressureMarketPolicy`

Offers are influenced by resource pressures.

Trade/prosperity can affect slot counts.

Generation aims to be deterministic where appropriate.

Historical rule:
- avoid same item appearing simultaneously as both buy and sell offer.

## 27.3 Player map/economic state

Known state historically includes:
- `currentNodeId`
- credits
- unlocked routes/clusters
- star map
- market cache

Some of these older node-centric concepts may be evolving as continuous navigation becomes more central.

Do not delete legacy graph state merely because continuous navigation now exists; reconcile intentionally.

---

# 28. EVENTS / BUFFS

Known world/node event structures:
- `NodeBuff`
- `EventOutcome`
- `StormEventDefinition`
- `NodeArchetypeDef`
- `ClusterAffinityDef`

These influence settlements/world state.

Persistence work previously included:
- buffs,
- events,
- outcomes,
- seeds/settings.

---

# 29. SAVE / LOAD

Save/load correctness is a major priority.

Known persistent areas include:
- topography,
- nodes,
- POIs,
- player world-map state,
- travel lock,
- buffs/events/outcomes,
- seeds/settings,
- boat modules,
- compartment flooding,
- sacred-item state,
- deployed flotation bags on explicit saves,
- star-map knowledge/fragments.

Important principle:

**Persist semantic state and stable identity, not merely scene GameObjects.**

When restoring:
- reconstruct runtime representation from authoritative saved state,
- fail soft if optional references are missing,
- preserve stable IDs.

---

# 30. MULTIPLAYER ARCHITECTURE

## 30.1 Goal

One shared boat cooperative multiplayer.

Host authoritative.

## 30.2 Current phase

Networking transport/package is **not implemented**.

Current goal is architectural preparation.

## 30.3 Desired request flow

Future shared actions should naturally fit:

1. Client chooses action.
2. Client sends intent.
3. Transport authenticates sender/requester.
4. Host authority validates.
5. Host mutates state.
6. State replicates.
7. Clients render presentation.

## 30.4 Security rule

Never trust:
- requester IDs supplied by the client,
- client claims about ownership,
- client-computed final authoritative results.

## 30.5 Audit classification

A useful historical audit vocabulary:

- MP-safe
- small seam
- authority gate
- requester propagation
- larger change
- defer

This makes multiplayer-hardening reviews easier to prioritize.

---

# 31. LAYERS / SORTING

Known physics layers historically consolidated around:

- Hull
- Ground
- Player
- Interactable
- WorldItem
- BoatItem
- InternalSensor
- HatchLedge
- WorldLedge
- UnderwaterResource
- Water
- UI

Visual sorting layers have also been deliberately structured.

Known deferred issue:
- `BoatItemZone` layering bug.

When changing player/bell/boat presentation, do not use global layer changes that accidentally affect all players or all bells.

---

# 32. CURRENT / PINNED TODO INDEX

This list contains durable TODOs mentioned in prior work. Some may already have been completed in the repo, so **verify before implementing**.

## Interaction / inventory
- Harden `Interactor2D` for multiple `IInteractable`s per hovered object.
- Fix prompt refresh after interaction.
- Fix module interaction colliders in Inspector.
- Harden `InventoryDragController`.
- Fix world-drop target selection under overlapping colliders.
- Verify ballast drop targeting.
- Fix `BoatItemZone` layering issue.

## Boat / physics
- Move propulsion authority from boat-level throttle tuning onto installed engines.
- Investigate boarded player pushing boat through interior collision/friction.
- Continue hierarchy/root-object cleanup later.

## Diving bell
- Verify submerged bell exit sorting fix.
- Verify restore-time tether spike protection.
- Add/define emergency flotation if still desired.
- Keep power/manual-control semantics intact.
- Make sure button sprites/UI cleanup is not forgotten.

## Flotation
- Placement indicator.
- HUD interaction hint polish.
- Generic player/seafloor out-of-bounds push-up.
- Final balance/regression if needed.

## World map / navigation
- Continue continuous-navigation integration.
- Treat topography `WorldBounds` as global coordinate authority.
- Do not expose map boundary as known rectangle.
- Continue star-map/celestial implementation.
- Time-of-day integration after celestial/sun foundation.

## Water presentation
- Consolidate depth scaling with improved water shader.
- Foam/sparkles are nonessential.
- Preserve useful depth behavior.

## Project architecture
- Refactor scene god objects into root + children later.
- Relevance registry / runtime hierarchy cleanup.
- World cache reset/topography clustering items from roadmap if still current.
- Save diagnostics/catalog-backed validation where useful.

---

# 33. TESTING PHILOSOPHY

Each pass should have a focused regression list.

Typical areas:
- scene load/unload,
- explicit save/load,
- death/respawn,
- boat boarding/unboarding,
- underwater vs above-water behavior,
- item ownership,
- deployment/stow,
- duplicated objects,
- missing references,
- authority vs presentation.

For multiplayer-hardened systems, think through:
- host action,
- future client intent,
- two players interacting with same object,
- per-player presentation,
- reconnect/restore semantics.

A later explicit test milestone is to run **two local instances** as host + client once networking exists.

---

# 34. DESIGN PRINCIPLES THAT SHOULD SURVIVE IMPLEMENTATION DETAILS

## 34.1 The world is physical where that produces stories

Cargo can move.
Flooding matters.
Diving has risk.
Heavy objects affect the boat.
Lost items may be recoverable.
Deployables remain physical.

This is part of the game’s identity.

## 34.2 Information is progression

The player does not merely unlock numeric upgrades.

They learn:
- the map,
- routes,
- seafloor,
- stars,
- settlements,
- hazards.

Knowledge itself is gameplay.

## 34.3 Getting lost should be interesting

Navigation should allow failure without always becoming a hard reset.

A bad course should create:
- resource cost,
- risk,
- uncertainty,
- recovery opportunities.

## 34.4 Recovery floors are preferable to campaign-killing dead ends

The Money Chest is the clearest precedent.

Consequences should matter, but the player should often have a difficult path back rather than a permanently bricked save.

## 34.5 Optional systems should be rewarding, not mandatory

Diving is the clearest example.

It can provide powerful rewards and shortcuts without becoming the only viable progression path.

---

# 35. CODING-AGENT CHECKLIST BEFORE ANY CHANGE

Before editing:

1. Read this handoff.
2. Read the exact current target file.
3. Search all important references.
4. Determine authoritative owner of the state being changed.
5. Identify persistence implications.
6. Identify multiplayer implications.
7. Identify scene/prefab/Inspector implications.
8. Keep the pass narrow.
9. Avoid unrelated cleanup.

After editing:

1. Review diff for accidental rewrites.
2. Check compile-facing API consistency.
3. Check save/load implications.
4. Check null/missing-reference behavior.
5. Check single-player behavior.
6. Check future multiplayer seam.
7. Provide a concise test checklist.
8. Update this handoff only if a durable architectural rule changed.

---

# 36. HOW TO RESPOND DURING PROJECT WORK

A useful answer structure for significant coding tasks:

## Assessment
- What the current system appears to do.
- What is actually wrong / missing.
- Relevant architectural constraint.

## Implementation
- Files changed.
- New files.
- Important behavior changes.
- Any migration/setup.

## Why this shape
- Ownership/authority rationale.
- Persistence implications.
- Multiplayer implications if relevant.

## Inspector / Unity steps
- Exact scene/prefab actions required.

## Test checklist
- Small set of concrete cases.

Avoid bloated generic explanations after the design has already been agreed.

---

# 37. DO NOT ASSUME

Do not assume:
- a historical class file is current,
- an old serialized field still exists,
- an old enum ordering is unchanged,
- a TODO is still unresolved,
- a scene reference is globally unique,
- only one player will ever exist,
- the UI is authoritative,
- local BoatScene coordinates equal world coordinates,
- the node graph defines continuous world bounds,
- every physical object can safely be recreated instead of retaining identity,
- every multiplayer seam requires networking code today.

Verify.

---

# 38. USER-SPECIFIC COLLABORATION NOTES

These are intentionally limited to durable, project-relevant preferences.

- The user is the solo developer and has extensive knowledge of the codebase.
- They often work on multiple project threads in parallel.
- This makes stale file versions a real risk.
- They prefer the AI to inspect current code rather than confidently guessing.
- They value architecture but dislike architecture theater.
- They prefer reusable systems when the reuse is real.
- They dislike broad refactors hidden inside feature work.
- They are comfortable postponing nonessential polish.
- They frequently prototype first, then harden.
- They want systems to remain understandable enough to maintain themselves.
- They prefer decisive technical recommendations with rationale.
- Humor is welcome; clarity wins over performance.
- Profanity does not need to be sanitized when used naturally in discussion.
- Do not bring unrelated personal history into project conversations.
- Do not infer emotional, political, medical, or other sensitive personal traits as a basis for technical advice.
- Do not repeatedly ask for information already present in repository/context.
- When something is Inspector-only, say so instead of inventing code.
- When a change is risky, be explicit about what could regress.
- When a task is complete, do not invent extra work just to appear productive.

---

# 39. IMPORTANT HISTORICAL IMPLEMENTATION LANDMARKS

Approximate timeline of meaningful system evolution:

## Early 2026
- World-map generation and runtime binding.
- Market/resource pressure systems.
- Modular boat and buoyancy foundations.
- Player movement/survival foundations.
- Inventory and underwater-resource architecture.

## Spring / early summer 2026
- Topography-first world generation.
- Contours, biomes, classification overlays.
- Fog/knowledge concepts.
- Interior flooding.
- Cargo securing.
- Wearables and buff architecture.
- Underwater resource spawning/tools.

## June 2026
- Money Chest system stabilized with loss/recovery/persistence.

## August 2026
- Star-map navigation elevated into a central pillar.
- Continuous/off-rails navigation direction strengthened.
- Boat module/piloting architecture work.

## September 2026
- Major diving-bell implementation.
- Winch/tether architecture.
- Multiplayer-hardening passes.
- Flotation/lift-bag system.
- Procedural animation discussions.
- Celestial continuous-coordinate design and fragment work.

## October 2026
- Transition toward direct repository-aware AI/Codex workflow.
- This handoff created to replace reliance on scattered chat memory.

---

# 40. SOURCE OF TRUTH PRIORITY

When information conflicts, use this priority order:

1. **Current repository code and serialized project state**
2. **Explicit current user instruction**
3. **Current design/roadmap files in repo**
4. **This handoff document**
5. **Historical chat assumptions**

This document is context, not permission to overwrite current reality.

---

# 41. HANDOFF MAINTENANCE RULE

Update this file only for information that is durable.

Good additions:
- architectural ownership changes,
- new global invariants,
- completed major subsystem milestones,
- durable workflow rules,
- major deferred roadmap items.

Bad additions:
- every bug encountered,
- transient compile errors,
- one-off Inspector values,
- temporary experimental tuning,
- emotional commentary from a debugging session.

Keep this useful enough that a future coding agent can read it without needing archaeology.

---

# 42. FINAL OPERATING PRINCIPLE

The AI is not the project architect of record.

The AI is a high-context engineering collaborator operating inside an existing project.

Its job is to:
- understand before changing,
- preserve intent,
- make changes auditable,
- avoid stale assumptions,
- help the user move faster without quietly taking ownership away from them.

If a clever solution violates a known project invariant, it is not clever.

