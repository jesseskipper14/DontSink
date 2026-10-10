# DON'T SINK — Town Center / Node Leader Mini-Pass
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** One civic building + one persistent civic NPC + one exterior node-status board  
**Goal:** Establish the Town Center as the dependable civic hub for generic quests, full local node information, and stale-but-useful adjacent-node intelligence.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

If modifying any existing class, inspect the exact latest live source first.

Never reconstruct an existing class from memory or from an older handoff.

Before touching an existing class:

1. Open the exact current source.
2. Preserve unrelated serialized fields and behavior.
3. Preserve current node-generation, NPC, world-map, quest, save, multiplayer, power, and interaction behavior.
4. Reuse existing semantic-node / persistent-NPC / service-menu architecture where present.
5. Prefer additive support classes/data over broad rewrites.
6. Stop after every checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. CORE ROLE

The Town Center is the primary civic-information and civic-work hub for a settlement.

Its responsibilities are intentionally narrow and strong:

1. Main source of generic civic quests
2. Main source of full local node information
3. Main source of adjacent-node information
4. Visible exterior status board for quick local-node stats

Role split:

```text
Surveyor
= where am I / where is this place / survey & charting work

Town Center / Node Leader
= how is this place doing / what does it need / what do we know about neighbors
```

Future specialized locations own their own flavor:

- Tavern / Bar → rumors, random quests, social nonsense
- Armory → weapons, armor, martial services
- Dens / Back Alleys → criminal / nefarious work
- Surveyor → charting / survey / position fix
- other specialty NPCs later

Do not let the Town Center absorb every system in the game.

---

# 2. EVERY SETTLEMENT NODE GETS ONE

Every actual settlement node gets:

- exactly one Town Center / Civic Center,
- exactly one Node Leader assigned to it,
- exactly one visible local-status board.

Visual form may vary by node archetype.

Examples:

- fishing hamlet → modest council house / civic shack
- trade town → proper administrative building
- fortress → command office
- farming settlement → communal hall
- dense city → civic hall / administrative block

The semantic role remains the same.

---

# 3. BUILDING SEMANTIC ROLE

Use a stable semantic role such as:

```text
TownCenter
or
CivicCenter
```

Do not hardcode “Mayor’s Office.”

The building’s gameplay identity is civic administration, not a specific governmental title.

Node-generation integration:

- Town Center occupies a permanent Civic/Service plot.
- Node Generator chooses and places the Town Center.
- The Town Center prefab owns its internal sockets/anchors.
- This follows the existing rule:

> The Node Generator places buildings. Buildings populate themselves.

---

# 4. NODE LEADER ROLE

The primary interactable NPC is a persistent semantic **Node Leader**.

This is not necessarily a mayor.

Possible future presentation titles:

- Mayor
- Elder
- Administrator
- Foreman
- Captain
- Warden
- Governor
- Council Speaker
- archetype-specific civic title

The system stores a semantic role, not one fixed title.

---

# 5. NODE LEADER IS PERSISTENT

The Node Leader is not an ambient NPC.

Requirements:

- stable NPC ID,
- stable semantic role,
- stable assignment to this node,
- stable Town Center association,
- saveable state where needed,
- persistent quest relationships / turn-in state where needed,
- generated identity/name later,
- not randomly respawned each visit.

If a future political/event system replaces a leader, that should be explicit.

---

# 6. DIRECT INTERACTION

Player interacts directly with the visible Node Leader.

No kiosk-only interaction.

No procedural interior required for v1.

Recommended placement:

- exterior porch,
- front civic platform,
- open civic area,
- immediately adjacent to Town Center entrance.

The NPC should be easy to find.

---

# 7. AUTHORED LEADER SOCKET

Town Center prefab should own a stable authored leader position.

Conceptually:

```text
TownCenter
├─ LeaderSocket
├─ StatusBoardSocket
├─ optional interaction/service anchor
└─ future cosmetic sockets
```

Node Generator should not randomly decide where the leader stands.

---

# 8. EXTERIOR STATUS BOARD

Town Center includes a visible exterior node-status board.

Purpose:

> Let the player walk up and simply look at the node’s current stats without entering a menu.

This board is local-node information only.

Do not put adjacent-node intelligence on it.

---

# 9. STATUS BOARD PRESENTATION

The board should be a real world-space display if practical.

The player should not need to press E just to inspect it.

Possible presentation:

```text
PORT MERCY

Population      842
Prosperity       61
Stability        74
Security         52
Food Balance    -18
Trade            67
Dock             45
```

Exact layout/art can evolve later.

Important rule:

> The underlying exact values should be available.

This also makes the board useful as an in-world simulation-debug surface.

---

# 10. LOCAL NODE INFORMATION

The Node Leader provides essentially full current node stats.

At minimum include current authoritative values for:

- Population
- Prosperity
- Stability
- Security
- Food Balance
- Trade Rating
- Dock Rating

Also expose, where currently available:

- active major node buffs,
- active major events,
- obvious shortages/surpluses,
- power status later,
- important civic conditions,
- other current NodeState fields deemed player-readable.

For the local node:

> Information is current and authoritative.

No staleness model is needed for the node the leader currently governs.

---

# 11. NODE LEADER INTERACTION MENU

Initial functional menu may be simple:

```text
[ Work ]
[ About This Settlement ]
[ Nearby Settlements ]
[ Leave ]
```

No full dialogue/personality system is required in this pass.

Display leader name/title if current NPC identity systems support it.

Later the NPC dialogue/personality pass can wrap these functions in actual conversation.

---

# 12. GENERIC CIVIC QUEST SOURCE

The Town Center / Node Leader is the dependable main source of generic quests.

Hard rule:

> At least one valid generic civic quest should be available whenever the player checks for work.

Do not allow this hub to repeatedly produce “nothing available.”

If the later quest generator has unusual edge cases, use fallback civic work.

---

# 13. QUEST CATEGORIES — HIGH-LEVEL ONLY

Do not over-design the quest system in this mini-pass.

Expected future generic civic categories include:

- Deliver X
- Acquire / find Y
- Kill / remove Z
- Transport
- Procurement
- Relief
- Repair/material supply
- Missing shipment follow-up
- Security work
- Civic errands
- Inter-node errands

Specialized work should remain with specialized services.

Examples:

- survey contracts → Surveyor
- criminal jobs → back alleys/dens
- rumor-heavy random work → Tavern
- martial commerce → Armory

The detailed quest-generation pass comes later.

---

# 14. QUEST OWNERSHIP BELONGS TO THE NODE

Strong architectural rule:

```text
Node civic need
→ generates civic quest
→ Node Leader presents / accepts / turns it in
```

Avoid:

```text
NPC randomly owns arbitrary quest forever
```

The civic need belongs to the node.

The Node Leader is its primary presentation/turn-in point.

---

# 15. QUEST PROVIDER SEAM

This mini-pass should expose a clean provider interface/service hook for future quest generation.

Bosun may use temporary/debug placeholder entries if the quest framework is not ready.

Do not build the entire quest generator here.

The important thing is:

- Node Leader can request available civic quests for this NodeStableId.
- Quest availability can later derive from NodeState/economy/events.
- At least one generic quest is guaranteed.

---

# 16. ADJACENT NODE INFORMATION

The Node Leader also provides information about nearby settlements.

This should normally use world-graph / simulation-neighbor relationships.

Important distinction:

> The graph is being used as social/economic contact topology, not as player travel rails.

This is a valid retained use of the graph.

---

# 17. ADJACENT INFO MAY INTRODUCE UNKNOWN SETTLEMENTS

The leader may tell the player about a settlement the player has never cartographically discovered.

Example:

> “Hey, you been over to Llamatown yet?”

or:

> “Last we heard, Greyhook’s been short on grain.”

This may communicate:

- settlement name,
- general relation/direction,
- trade condition,
- broad recent state.

But it must not automatically georegister the settlement.

Hard rule:

```text
Player hears that Llamatown exists
≠
World Map gets a precise Llamatown marker
```

No automatic terrain reveal.

No automatic node marker.

No automatic believed-position change.

---

# 18. ADJACENT INTELLIGENCE IS CACHED, NOT LIVE

Remote node information should not be a live reference to remote NodeState.

Instead, store an intelligence snapshot.

Conceptually:

```text
AdjacentNodeIntelSnapshot

SourceNodeId
ObservedNodeId

ObservedAtWorldTime
ReceivedAtWorldTime

Population
Prosperity
Stability
Security
FoodBalance
TradeRating
DockRating

MajorEvents
MajorBuffs
KnownTradeNeeds
KnownTradeSurpluses
```

Exact fields should fit live NodeState.

The key idea is snapshot semantics.

---

# 19. LOCAL INFO VS REMOTE INFO

Local node:

```text
Current authoritative state
```

Adjacent node:

```text
Last received intelligence snapshot
```

The remote node may have changed since that snapshot.

This is desirable.

---

# 20. FUTURE ABSTRACT TRADE SIMULATION

Remote intelligence freshness will later be driven by abstract trade contact.

No physical trade-boat simulation is required.

Future model:

```text
node distance
+ prosperity
+ security
+ trade rating
+ supply/demand
+ other economic factors
→ expected trade-contact frequency
```

Example:

> With this prosperity/security/demand profile, expect roughly one trade interaction every X days.

A simulated trade interaction may refresh adjacent intelligence.

---

# 21. TRADE CONTACT REFRESH RULE

Future intended rule:

```text
Trade interaction from adjacent node arrives
↓
trade/economic math resolves
↓
fresh snapshot of that remote node arrives
↓
local Town Center intelligence cache updates
```

No trade contact:

```text
no fresh intelligence
```

The Town Center does not simulate trade itself.

It only consumes results from the future Trade Simulation system.

---

# 22. GRAPH ADJACENCY IS ELIGIBILITY, NOT FRESHNESS

World graph / neighbor relation answers:

> Who can this node reasonably exchange information with?

Trade simulation answers:

> When did fresh information actually arrive?

Keep those responsibilities separate.

---

# 23. INITIAL IMPLEMENTATION BEFORE TRADE SIM EXISTS

Do not invent the trade simulator in this pass.

Instead:

- define storage/query seams for adjacent intel snapshots,
- allow debug/manual seeding of adjacent intel,
- provide deterministic fallback/testing data if needed,
- clearly mark future trade-refresh integration point.

If no snapshot exists, leader can report that no reliable recent information is available.

---

# 24. ADJACENT INFO UI

Recommended flow:

```text
Nearby Settlements
→ list known/contact-neighbor settlements with snapshots
→ select one
→ show last-known stats / age
```

Possible later display:

```text
Llamatown
Last report: 3.2 days ago

Population: 1,420
Prosperity: 48
Security: 33
Food Balance: -22

Known need:
Grain

Known surplus:
Timber
```

Exact UI can remain simple for this mini-pass.

---

# 25. INTEL AGE SHOULD BE VISIBLE

If adjacent intel has a timestamp, show its age.

Examples:

- “Last report: today”
- “Last report: 2.4 days ago”
- “Last report: 11 days ago”

This makes staleness explicit instead of silently lying to the player.

---

# 26. NO APPROXIMATE MAP BLOBS

Adjacent intelligence is textual/civic knowledge.

Do not create:

- approximate georeferenced blobs,
- fuzzy map circles,
- guessed node positions,
- auto-marked directions.

If later the player wants to mark “Llamatown is probably east,” they can do so manually through the cartography system.

---

# 27. TOWN CENTER BUILDING FORM

No procedural interior is required.

Recommended initial presentation:

- distinctive civic façade,
- visible entrance,
- visible leader,
- visible exterior status board,
- perhaps simple porch/platform.

Larger settlements can later use authored interior variants without changing service architecture.

---

# 28. NODE-GENERATION INTEGRATION

Town Center should consume the permanent node-generation skeleton.

Requirements:

- permanent Civic/Service plot,
- stable Town Center assignment,
- stable leader socket,
- stable status-board socket,
- accessible from node traversal graph,
- not buried in unreachable vertical stacks.

Town Center placement should be reasonably central / accessible, though exact aesthetics remain archetype-driven.

---

# 29. ROLE / TITLE VARIATION LATER

Do not require full procedural identity now.

But architecture should allow:

```text
NodeLeaderRole = semantic stable role
DisplayTitle = archetype/culture/state-derived later
```

Examples:

```text
Fishing hamlet → Elder
Fortress → Warden
Trade hub → Administrator
Dense city → Governor
```

The role remains functionally Node Leader.

---

# 30. MULTIPLAYER / SHARED STATE

Town Center civic information is shared crew-world information.

Shared authoritative state includes:

- local NodeState,
- civic quest state,
- adjacent intel snapshots,
- accepted/completed civic quest state,
- persistent Node Leader identity/role where applicable.

UI is client-local presentation of shared state.

Avoid per-player contradictory civic state unless a later special mechanic explicitly requires it.

---

# 31. SAVE / LOAD

Persist as needed:

- Town Center permanent plot assignment via Node Generation manifest,
- Node Leader stable ID/role,
- civic quest persistent state,
- adjacent-node intelligence snapshots,
- intel timestamps,
- any leader-specific persistent state.

Do not persist transient UI/menu state.

---

# 32. DEBUG / TESTING

Recommended debug controls:

### Town Center
- show TownCenter plot ID,
- show Leader stable ID,
- show LeaderSocket,
- show StatusBoardSocket.

### Local stats
- force node-stat values,
- refresh status board,
- inspect all displayed values.

### Adjacent intel
- inject snapshot,
- age snapshot,
- clear snapshot,
- compare snapshot vs live remote NodeState,
- show source/observed/received timestamps.

### Quest provider
- force fallback generic quest,
- list available civic quests,
- verify guaranteed minimum of one.

---

# 33. IMPLEMENTATION CHECKPOINTS

## TOWN.1 — Audit + Town Center semantic building
- inspect exact current Node Generation architecture,
- inspect semantic plot roles,
- inspect persistent NPC architecture,
- create/assign Town Center semantic role,
- establish Town Center prefab/socket contract,
- confirm accessibility.

**STOP FOR PLAYTEST.**

## TOWN.2 — Persistent Node Leader
- stable Node Leader role/ID,
- direct interaction,
- LeaderSocket placement,
- basic service menu shell,
- no dialogue-personality system yet.

**STOP FOR PLAYTEST.**

## TOWN.3 — Exterior status board
- real world-space local node stats,
- current authoritative values,
- readable at practical distance,
- no interaction required for basic inspection,
- update correctly from NodeState.

**STOP FOR PLAYTEST.**

## TOWN.4 — Local node information menu
- About This Settlement,
- essentially full local node stats,
- current events/buffs where available,
- current shortages/surpluses where available.

**STOP FOR PLAYTEST.**

## TOWN.5 — Civic quest-provider seam
- Work menu,
- node-owned civic quest provider,
- guarantee at least one valid generic quest,
- use temporary/debug quest entries if full quest generator is not ready,
- no specialty quest creep.

**STOP FOR PLAYTEST.**

## TOWN.6 — Adjacent intelligence cache
- snapshot data model,
- graph-neighbor eligibility,
- Nearby Settlements menu,
- last-known remote stats,
- visible intel age,
- unknown settlement introduction without map reveal.

**STOP FOR PLAYTEST.**

## TOWN.7 — Future trade-simulation seam + hardening
- explicit refresh hook for abstract trade contact,
- no trade simulation implemented here,
- multiplayer/save review,
- node-generation regression,
- map-knowledge regression,
- quest fallback test.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 34. MINIMUM PLAYTEST SCENARIOS

### Small settlement
- Town Center exists,
- leader easy to find,
- board readable.

### Local stat changes
- modify Prosperity / Food Balance / Security,
- status board updates,
- leader reports current values.

### Generic work
- Work always returns at least one valid civic quest.

### Unknown neighbor introduction
- leader mentions Llamatown,
- no World Map marker appears,
- no terrain revealed.

### Stale intel
- inject 8-day-old remote snapshot,
- leader shows old values and age,
- live remote node changes do not mutate cached snapshot.

### Fresh intel
- simulate future trade-refresh hook,
- snapshot replaces old intelligence,
- age resets correctly.

### Save/load
- Node Leader remains stable,
- adjacent snapshots remain,
- civic quest state remains.

### Multiplayer
- clients see same authoritative local stats/intel/quest state.

---

# 35. ACCEPTANCE TESTS

1. Every settlement node can host exactly one Town Center.
2. Every Town Center can host exactly one persistent Node Leader.
3. Node Leader uses stable semantic role rather than hardcoded “Mayor.”
4. Town Center uses a permanent Civic/Service plot.
5. Leader uses an authored stable socket.
6. Status board uses an authored stable socket.
7. Status board shows current local node stats.
8. Board can be read without opening a normal service menu.
9. Node Leader directly interacts with player.
10. Local node info is current/authoritative.
11. Local info includes essentially full player-readable NodeState stats.
12. Work menu guarantees at least one generic civic quest.
13. Civic quest ownership belongs to the node, not arbitrarily to the NPC.
14. Specialized quest categories remain out of scope.
15. Adjacent-node info uses cached snapshots.
16. Remote snapshots do not live-bind to remote NodeState.
17. Intel age is visible.
18. Graph adjacency/contact relation determines eligible neighbor pool.
19. Future trade simulation refreshes intel through an explicit hook.
20. Town Center does not implement the trade simulator.
21. Unknown neighboring settlements may be introduced textually.
22. Hearing of a settlement does not create a precise map marker.
23. Adjacent intel does not reveal terrain.
24. Adjacent intel does not fix believed position.
25. Save/load preserves persistent leader/intel/quest state.
26. Multiplayer clients share the same authoritative civic state.
27. Existing Surveyor role remains distinct.
28. Existing node-generation system remains intact.

---

# 36. EXPLICIT NON-GOALS

Do not turn this pass into:

- full quest-generation system,
- full NPC dialogue/personality framework,
- trade simulation,
- physical merchant-boat simulation,
- political simulation,
- elections,
- leader replacement logic,
- procedural building interiors,
- rumor system,
- criminal quest system,
- armory system,
- Tavern system,
- cartographic map reveal,
- generic Node Intelligence overhaul beyond this civic consumer seam.

---

# 37. FUTURE EXTENSIONS

Leave clean seams for:

- full civic quest generation,
- simulation-driven shortages/jobs,
- abstract trade simulation,
- leader personality/dialogue,
- political events,
- leader replacement,
- reputation,
- taxation/fees,
- civic construction requests,
- node diplomacy,
- service availability,
- Town Hall interior variants,
- visible clerks/citizens,
- richer public notice boards,
- Town Bar,
- Armory,
- Dens / Back Alleys,
- broader Node Intelligence system.

---

# 38. FINAL DESIGN SUMMARY

The intended civic flow is:

```text
Town Center
├─ Exterior Status Board
│  └─ current local node stats
│
└─ Persistent Node Leader
   ├─ Work
   │  └─ guaranteed generic civic quest source
   │
   ├─ About This Settlement
   │  └─ full current local node information
   │
   └─ Nearby Settlements
      └─ cached adjacent-node intelligence
         refreshed later by abstract trade contact
```

And the core information rule is:

```text
Local node
= live authoritative information

Adjacent node
= last received trade/contact snapshot
```

A leader can absolutely say:

> “Hey, you been over to Llamatown yet?”

without the game magically drawing Llamatown onto the player’s map.

That separation is intentional.

---

**End of Town Center / Node Leader mini-pass handoff.**
