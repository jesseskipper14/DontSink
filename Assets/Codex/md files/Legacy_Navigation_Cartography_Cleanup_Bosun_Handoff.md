# DON'T SINK — Legacy Navigation / Cartography Cleanup & Migration
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Cleanup/migration pass after the newer off-rails navigation, physical cartography, harbor, Surveyor, and piloting designs  
**Goal:** Remove or quarantine obsolete player-facing navigation/cartography systems that conflict with the current design, while preserving any underlying graph/topology data still useful to world generation, economy, AI, quests, or simulation.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

If modifying or deleting an existing class, inspect the exact latest source currently in the project first.

Never reconstruct an existing class from memory, an old handoff, or assumptions.

For this cleanup pass especially:

1. Find every consumer before deleting a type or field.
2. Distinguish:
   - obsolete for player navigation,
   - obsolete for UI only,
   - obsolete everywhere,
   - still useful for simulation/generation.
3. Preserve unrelated save, economy, event, world-generation, multiplayer, and UI behavior.
4. Prefer migration/deprecation over blind deletion when old saves may contain legacy state.
5. Stop after each checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

This pass is intentionally conservative. Cleanup is good. Accidentally deleting the economy because it happened to read a route edge is less good.

---

# 1. CURRENT NAVIGATION PHILOSOPHY

The modern design is:

```text
NodeScene
→ player explicitly Embarks
→ boat leaves harbor
→ player chooses where to physically sail
→ travel is continuous/off-rails
→ nearby geography is physically observed
→ player may become lost
→ harbor is found physically
→ player explicitly Docks
→ NodeScene
```

There is no longer a player-facing concept of:

```text
Choose destination node
→ validate route
→ unlock route
→ travel along route
→ reach route end
→ arrive
```

That old model must be removed from player-facing systems.

---

# 2. EMBARK NO LONGER REQUIRES A DESTINATION

Hard rule:

> Embark means leave the harbor.

The player does not need to:
- select a destination,
- select a route,
- choose a connected node,
- pass a route-validity check,
- pass a cluster-unlock check.

After embark:
- the crew decides where to steer,
- the boat may go anywhere physically reachable,
- getting lost is legitimate gameplay.

Audit and remove any embark requirement tied to destination/route selection.

---

# 3. REMOVE PLAYER-FACING NODE-TO-NODE ROUTE TRAVEL

Legacy route systems such as the following should be audited for removal from player travel:

- TravelRequest / TravelResult style route authorization,
- selected destination node,
- legal-route validation,
- MaxRouteLength travel gating,
- graph-edge eligibility for player travel,
- node adjacency as player permission,
- route-specific departure,
- route-progress completion.

Exact class names must be confirmed from live code.

Do not assume a type is safe to delete until all consumers are found.

---

# 4. WORLD GRAPH MAY REMAIN AS SIMULATION TOPOLOGY

Do **not** blindly delete the generated node graph.

Graph/cluster relationships may remain useful for:
- world generation,
- regional organization,
- economy,
- trade simulation,
- NPC shipping,
- survey-contract generation,
- event weighting,
- AI,
- authored world structure.

The critical distinction:

> A world graph edge is not a player travel route.

If graph data remains, rename/document APIs where needed so this distinction is obvious.

Avoid APIs whose names imply player permission if they now represent only topology/simulation relationships.

---

# 5. REMOVE GENERATED ROUTE LINES FROM PLAYER MAP AUTHORITY

The World Map should no longer show generated graph edges as authoritative player routes.

Player route planning now comes from physical cartography tools:
- manual route pins,
- route string,
- manually recorded heading/distance,
- player-authored annotations.

The player is allowed to draw:
- incorrect routes,
- dangerous routes,
- routes through unknown water,
- routes based on bad interpretation.

Generated graph edges must not silently override or "correct" that.

---

# 6. LEGACY ROUTE SAVE STATE BECOMES MIGRATION TARGET

Audit persisted state such as:

- `unlockedRoutes`,
- selected route,
- selected destination,
- active route,
- graph route progress,
- route-completion state,
- route-specific travel locks,
- hard cluster unlocks used as movement gates.

Where obsolete:
- stop writing them,
- migrate old saves safely,
- remove them only after confirming no valid consumers remain.

Do not let old legacy fields continue driving gameplay simply because old saves contain them.

---

# 7. CLUSTERS MAY REMAIN, BUT NOT AS MOVEMENT LOCKS

Clusters may remain useful world-generation and simulation metadata.

They should not function as invisible walls.

Remove player-facing logic equivalent to:

```text
Cluster is locked
→ player cannot sail there
```

The player may physically sail into any reachable region.

Progression comes from:
- knowledge,
- navigation skill,
- supplies,
- environmental danger,
- equipment,
- world conditions,

not metaphysical travel permission.

---

# 8. AUDIT CURRENTNODEID SEMANTICS

`currentNodeId` or equivalent needs review.

While docked:
- the game legitimately knows which node the boat is docked at.

While underway:
- true world position is the geographic authority.

Avoid treating a node ID as the player's universal location while at sea.

Prefer semantics equivalent to:

```text
DockedNodeId
```

when that is what the value actually means.

Do not rename blindly without inspecting all current consumers/save fields.

---

# 9. REMOVE LEGACY GEOGRAPHIC KNOWLEDGE STATES

For world geography / nodes / POIs, the old abstract:

```text
UNKNOWN
RUMORED
PARTIAL
KNOWN
```

model is obsolete.

Modern geographic knowledge is explicit data:
- surface chart coverage,
- known node markers,
- known surface POIs,
- known bathymetry,
- known underwater POIs,
- literal rumor/reference text.

Rumors do not mutate geographic state.

Important:

> Do not remove celestial knowledge-state systems merely because they use similar terminology.

Constellation/celestial knowledge still has legitimate lifecycle states.

Scope this cleanup carefully to geographic/entity discovery.

---

# 10. REMOVE PASSIVE GEOGRAPHIC DISCOVERY

Audit for any code that reveals/registers geography from:

- sailing nearby,
- seeing land,
- seeing a lighthouse,
- seeing a settlement,
- entering a harbor,
- docking,
- walking into a town,
- physically finding a wreck,
- physically finding a cave,
- physically finding an underwater POI.

These observations must not automatically georegister on the World Map.

Modern rule:

> Eyeballs provide observations. Cartography provides coordinates.

---

# 11. DOCKING DOES NOT AUTOMATICALLY PROVIDE CARTOGRAPHIC KNOWLEDGE

Docking at a node may tell the player:
- they are physically in a settlement,
- what that settlement calls itself, if appropriate.

It does not automatically mean:
- they know its map coordinate,
- the node marker appears on the World Map,
- nearby terrain becomes charted,
- believed position is corrected.

The Surveyor / chart systems provide those services explicitly.

---

# 12. BELIEVED POSITION MUST NOT AUTO-TRACK TRUE POSITION

The physical believed-position marker on the Mapping Table is player-managed.

Remove any remaining behavior that automatically updates it from:
- boat movement,
- route progress,
- dead reckoning,
- node arrival,
- scene transition,
- docking.

Current special behavior:
- explicit Surveyor `Fix Position` may snap it to the authoritative location.

Otherwise it can be:
- wrong,
- rotated,
- dragged anywhere,
- left behind,
- placed over uncharted space.

This separation is intentional.

---

# 13. TRUE POSITION REMAINS AUTHORITATIVE WORLD STATE

Do not confuse cleanup of player-facing navigation with removal of true world position.

True world position is still required for:
- physical travel,
- nearby geography,
- harbor proximity,
- viewscape,
- celestial/location-sensitive systems,
- weather/environment,
- world simulation.

The rule is:

```text
TruePosition
= hidden simulation authority

BelievedPosition
= player-managed physical map object
```

No silent reconciliation.

---

# 14. REMOVE ROUTE-RELATIVE PILOTING SOLVERS

Audit the piloting system for legacy logic derived from an authoritative selected route.

Potential obsolete concepts include:

- along-track progress,
- cross-track error used as player guidance,
- automatic destination bearing,
- route completion percentage,
- exact target heading,
- "turn X degrees",
- automatic course correction,
- destination ETA based on graph route,
- steering toward a selected node,
- exact route-centerline guidance.

Modern piloting provides:
- physical boat control,
- local viewscape,
- visible nearby land/harbors,
- analog north compass,
- future manually recorded navigation-leg values.

The game does not solve the player's route against true position for them.

---

# 15. MANUAL NAVIGATION LEGS ARE NOT TRUE ROUTES

Future Cartography Workbench navigation legs may contain player-recorded values such as:

```text
Heading: 072°
Distance: 86.4 NM
```

These are:
- player-authored records,
- potentially wrong,
- not authoritative geometry,
- not automatically corrected.

Do not repurpose legacy route solver code to "helpfully" validate them against world truth.

---

# 16. REMOVE ARRIVAL-BY-ROUTE-COMPLETION

Arrival is no longer caused by:
- route progress reaching 100%,
- reaching a selected destination's expected travel distance,
- crossing an arbitrary end-of-scene X coordinate,
- completing an edge.

Arrival happens because the boat physically reaches a real harbor/docking region and the player explicitly Docks.

Audit scene-transition triggers accordingly.

---

# 17. BOATSCENE X IS NOT AUTHORITATIVE GLOBAL ROUTE PROGRESS

BoatScene local/physical travel distance must not be treated as:

```text
0 = origin node
1 = destination node
```

or equivalent.

The boat may:
- wander,
- reverse,
- overshoot,
- approach unexpected land,
- reach another node entirely,
- sail off intended course.

Any legacy code assuming BoatScene movement maps linearly to a selected node edge must be removed or rewritten.

---

# 18. HARBOR PROXIES MUST NOT REQUIRE AN ACTIVE DESTINATION

Nearby harbor/town representation should depend on actual physical proximity.

It must not require:
- selected destination,
- active route,
- route endpoint,
- unlocked route.

Any nearby node can become physically visible if conditions allow.

This applies to:
- BoatScene harbor/town proxy,
- local piloting viewscape,
- final harbor guidance.

---

# 19. HARBOR GUIDANCE IS PROXIMITY-BASED, NOT ROUTE-BASED

Close-range harbor guidance may appear for the nearby harbor.

It should not be tied to:
- planned destination,
- graph route,
- active leg completion,
- travel request.

The guidance is informational only.

The player still manually pilots into the berth/docking area.

---

# 20. REMOVE STAR-MAP ROUTE UNLOCKING

Earlier designs used celestial knowledge / star-map pieces as route/cluster unlocks.

That is obsolete.

Modern celestial knowledge helps with:
- orientation,
- charting,
- navigation,
- position determination,
- survey reference,
- celestial progression.

It does **not** grant permission to sail across a graph edge or enter a cluster.

Audit old:
- route unlock items,
- "next cluster unlocked" logic,
- celestial-route gates,
- cluster access gates.

Preserve useful celestial evidence/fragment/chart systems.

---

# 21. FOUND/BOUGHT CELESTIAL MATERIAL IS KNOWLEDGE, NOT MOVEMENT PERMISSION

Bought/found star charts and fragments may:
- reveal celestial knowledge,
- provide reference,
- support navigation,
- support quests.

They do not:
- unlock a travel edge,
- authorize a node route,
- create invisible map access.

Keep those systems conceptually separate.

---

# 22. AUDIT STARTDOCK / DESTINATION GRAPH SEMANTICS

A deterministic starting node/dock remains useful.

A globally designated "Destination = rightmost node" should not remain a core travel assumption unless a separate story/victory system explicitly needs it.

Audit:
- StartDock,
- Destination,
- origin/destination pairing,
- graph generation assumptions,
- UI assumptions.

If a future story system wants a final destination:
- let that story system own it explicitly.

Do not let core navigation architecture assume the world has one canonical endpoint.

---

# 23. WORLD MAP UI LEGACY ROUTE SCRUB

Audit World Map UI for:

- click-node-to-travel behavior,
- generated route-edge highlights,
- available destination lists,
- locked-route coloration,
- cluster-lock visuals,
- route range circles,
- "travel here" buttons,
- legal-route indicators,
- selected graph destination.

Remove or repurpose where obsolete.

The World Map is now primarily:
- cartographic knowledge,
- player planning,
- physical scraps/charts,
- manual annotations,
- physical route-string planning,
- physical believed-position marker.

---

# 24. DO NOT AUTO-CORRECT PLAYER ROUTE PLANNING

The modern map can contain:
- wrong manual marks,
- wrongly assembled celestial scraps,
- wrong route strings,
- wrong heading calculations,
- wrong believed position.

Legacy systems must not silently snap:
- routes to nodes,
- route lines to edges,
- annotations to POIs,
- believed position to true location,
- active navigation legs to valid courses.

Player interpretation is gameplay.

---

# 25. HARBOR / EMBARK LEGACY SPAWN CLEANUP

As the Harbor Mooring / Dock pass lands, audit old NodeScene/BoatScene spawn assumptions.

Potential obsolete logic:
- long-dock-specific spawn locations,
- shallow shoreline assumptions,
- selected-destination-facing spawn,
- route-relative departure direction,
- graph-edge-aligned initial heading.

Modern embark:
- automatic mooring releases,
- transition to BoatScene,
- boat spawns at valid DepartureAnchor,
- velocity reset,
- angular velocity reset,
- throttle reset,
- true world position becomes DepartureAnchor,
- player decides where to steer.

---

# 26. KEEP HARBOR DIRECTION, REMOVE ROUTE DIRECTION

Harbor `WaterwardDirection` remains valid physical world data.

It describes:
- safe harbor opening,
- departure geometry,
- berth orientation,
- harbor layout.

Do not confuse this with:
- selected route direction,
- direction to destination node.

Embark orientation should be based on harbor-safe geometry, not chosen travel destination.

---

# 27. TEMPORARY STARTING-ISLAND REVEAL MAY REMAIN

The current temporary starting-island auto-reveal may remain until the Surveyor tutorial is actually implemented.

Do not delete it prematurely if it is still required for testing.

Target future tutorial:

```text
Start game
→ visit Surveyor
→ receive physical local chart
→ take chart to Mapping Table
→ integrate
→ local surface geography becomes known
```

Mark temporary auto-grant/reveal clearly for later removal.

---

# 28. DO NOT REMOVE USEFUL GRAPH/CLUSTER METADATA JUST BECAUSE THE UI NO LONGER USES IT

Before deleting:
- ClusterId,
- adjacency data,
- graph edges,
- distance relationships,

search all consumers.

These may still serve:
- markets,
- events,
- worldgen,
- node affinity,
- survey contracts,
- AI trade,
- regional generation.

Where useful:
- preserve them,
- document them as simulation/world topology.

Where only player-route code uses them:
- remove them.

---

# 29. ROUTE NAMING / API CLEANUP

Where legacy names create dangerous confusion, consider safer terminology.

Examples conceptually:

```text
PlayerRoute
```

may need removal if it really means graph topology.

```text
WorldGraphEdge
SimulationAdjacency
RegionalLink
```

may be clearer depending on actual use.

Likewise:

```text
CurrentNodeId
```

may warrant migration toward:

```text
DockedNodeId
```

if that better matches live semantics.

Do not perform cosmetic renames with huge blast radius unless useful. Prioritize correctness.

---

# 30. TRAVEL LOCK AUDIT

Search for "travel lock" behavior.

Some locks may still be valid:
- scene transition in progress,
- boat not ready to embark,
- multiplayer authority transition,
- loading/saving lifecycle,
- explicit mechanical state.

Obsolete locks include:
- cannot travel because route is unknown,
- cannot travel because cluster locked,
- cannot travel because node too far,
- cannot travel because no graph connection.

Separate these carefully.

---

# 31. MAX ROUTE LENGTH AUDIT

`MaxRouteLength` or equivalent may no longer be a player-travel gate.

If it remains useful elsewhere:
- survey contract generation,
- AI trade edges,
- map-generation tuning,

preserve it there under clearer semantics.

Do not use it to stop the player from physically sailing farther.

---

# 32. MARKET / ECONOMY DEPENDENCY AUDIT

Before removing graph/route data, inspect economy consumers such as:
- PressureMarketPolicy,
- node trade relationships,
- resource pressure propagation,
- route-based price logic,
- trade simulation.

If economy needs abstract inter-node links, retain an internal simulation topology.

Do not make the player's freedom to sail dependent on that topology.

---

# 33. EVENT / QUEST DEPENDENCY AUDIT

Inspect event/quest systems for assumptions such as:
- "next node",
- "connected node",
- "route destination",
- "route length",
- "unlocked cluster."

Some may need reinterpretation:
- physical distance,
- graph neighborhood for simulation only,
- known-node knowledge,
- survey-contract candidate generation.

Do not globally delete concepts without understanding their use.

---

# 34. SAVE MIGRATION PRINCIPLES

For obsolete saved fields:

1. old saves must continue loading where practical,
2. ignore obsolete gameplay values after migration,
3. derive modern state from authoritative current systems,
4. do not accidentally auto-reveal geography during migration,
5. do not snap believed position to true position,
6. do not invent active routes from old data,
7. preserve graph/simulation data if still valid.

Record migration version.

---

# 35. MULTIPLAYER / AUTHORITY

Modern navigation authority should own:
- true boat/world position,
- boat physics,
- docking state,
- harbor transitions,
- authoritative map knowledge,
- shared physical mapping-table state.

Clients send intents for:
- steering,
- dock/embark,
- shared interactions.

Do not retain route-authority code that assumes the host authorizes a graph edge before movement.

The host authorizes simulation, not the crew's choice of destination.

---

# 36. DEBUG TOOL CLEANUP

Audit debug UI for legacy:
- unlock route,
- unlock cluster,
- select destination,
- teleport along route,
- route progress,
- legal destination display.

Keep useful tools if renamed/reframed:
- teleport boat to world coordinate,
- dock at node for testing,
- inspect graph topology,
- inspect known map data,
- inspect true vs believed position.

Debug tools may expose world truth explicitly because they are debug tools. Keep them clearly separated from gameplay UI.

---

# 37. INITIAL CLEANUP TARGET LIST

Bosun should explicitly search for code/data/UI tied to these concepts:

```text
TravelRequest
TravelResult
MaxRouteLength
unlockedRoutes
unlockedClusters
selectedDestination
activeDestination
currentRoute
routeProgress
routeComplete
StartDock
Destination
CurrentNodeId
Unknown
Rumored
Partial
Known
TravelLock
route edge
graph edge
cluster unlock
destination bearing
cross-track
along-track
arrival
travel complete
map reveal
discover node
discover POI
believed position
```

This is a discovery list, not permission to bulk-delete matching symbols.

Inspect context first.

---

# 38. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## CLEAN.1 — Audit only
No deletions yet.

Produce:
- list of route/navigation classes,
- list of UI consumers,
- list of save fields,
- list of graph consumers,
- list of economy/event dependencies,
- list of arrival/transition dependencies,
- proposed KEEP / MIGRATE / REMOVE / DEFER classification.

**STOP FOR REVIEW.**

## CLEAN.2 — Remove player travel gating
- Embark no longer needs destination,
- remove graph-edge travel validation,
- remove cluster movement locks,
- remove max-route player gating,
- preserve valid lifecycle/authority locks.

**STOP FOR PLAYTEST.**

## CLEAN.3 — Arrival / harbor cleanup
- remove route-progress arrival,
- remove end-of-strip arrival,
- nearby harbor logic becomes proximity-based,
- Dock remains explicit,
- harbor guidance does not require active destination.

**STOP FOR PLAYTEST.**

## CLEAN.4 — World Map cleanup
- remove click-node-to-travel,
- remove generated player-route edges,
- remove lock/range indicators,
- preserve cartographic knowledge,
- preserve manual route-planning systems,
- preserve physical believed-position marker.

**STOP FOR PLAYTEST.**

## CLEAN.5 — Discovery-state cleanup
- remove geographic Unknown/Rumored/Partial/Known state usage,
- remove passive node/POI/map discovery,
- retain celestial knowledge lifecycle,
- retain literal rumor/reference data.

**STOP FOR PLAYTEST.**

## CLEAN.6 — Piloting cleanup
- remove route-relative course solver remnants,
- remove exact target bearing/course correction,
- remove selected-destination dependence,
- preserve physical piloting,
- preserve viewscape/analog compass architecture.

**STOP FOR PLAYTEST.**

## CLEAN.7 — Star/cluster route-unlock cleanup
- remove celestial route permissions,
- remove cluster unlock movement gates,
- preserve celestial knowledge/evidence,
- preserve useful cluster metadata.

**STOP FOR PLAYTEST.**

## CLEAN.8 — Save migration / naming hardening
- migrate obsolete persisted route state,
- stop writing unused fields,
- clarify DockedNode/current-node semantics where worthwhile,
- version migration,
- test old saves.

**STOP FOR PLAYTEST.**

## CLEAN.9 — Dependency hardening / final removal
- revisit graph data still used by economy/events/worldgen,
- delete truly dead classes/fields/assets,
- remove stale Inspector fields,
- remove dead debug controls,
- remove dead UI,
- document remaining graph topology purpose.

**STOP FOR FINAL TESTING.**

---

# 39. ACCEPTANCE TESTS

The cleanup pass is successful when:

1. Player can Embark without choosing a destination.
2. Player can sail in any physically reachable direction.
3. No graph edge is required for player movement.
4. No cluster unlock is required for player movement.
5. No MaxRouteLength gate blocks physical sailing.
6. No generated graph route is presented as the player's route.
7. Manual cartography remains authoritative for player route planning.
8. BoatScene movement is not interpreted as normalized node-to-node progress.
9. Route progress cannot trigger arrival.
10. Crossing a generated scene boundary does not automatically mean destination arrival.
11. Nearby harbors can appear without being the active destination.
12. Docking is based on actual harbor proximity + explicit Dock action.
13. Harbor guidance does not require a selected route.
14. Seeing land does not reveal the World Map.
15. Docking does not reveal geography automatically.
16. Seeing/finding a POI does not georegister it automatically.
17. Believed position does not auto-follow true position.
18. Surveyor Fix Position still works explicitly.
19. Geographic Unknown/Rumored/Partial/Known state is no longer used as the main map-knowledge model.
20. Celestial knowledge states remain intact where valid.
21. Star-map knowledge does not unlock physical movement.
22. Cluster metadata remains available to simulation where useful.
23. Economy/event/worldgen systems still compile and behave.
24. Old saves migrate without accidental map reveal or route invention.
25. Existing harbor, mooring, Surveyor, piloting, cartography, and NodeScene systems still work.
26. Dead route UI/Inspector/debug elements are removed.
27. Remaining graph APIs are clearly simulation/world-topology concepts, not player travel permissions.

---

# 40. LIVING CLEANUP DOCUMENT

This is intentionally a starting cleanup inventory.

As obsolete systems are encountered during later development/playtesting:
- add them to this pass or a follow-up cleanup list,
- classify whether they are obsolete everywhere or only in one role,
- remove them deliberately.

Do not attempt to predict every stale feature now.

The project has undergone major navigation-design evolution; finding additional fossils is expected.

---

# 41. EXPLICIT NON-GOALS

Do not use this pass to:
- redesign the entire economy,
- delete the world graph without dependency analysis,
- implement full Cartography Workbench,
- implement full survey quests,
- implement the piloting rework itself,
- redesign harbor visuals,
- regenerate nodes,
- perform unrelated code style cleanup,
- rewrite working systems merely because they are old.

This is a semantic cleanup/migration pass.

---

# 42. BOSUN DELIVERY EXPECTATIONS

After every checkpoint, report:

1. Exact files inspected
2. Exact files modified
3. Exact files deleted
4. Legacy systems discovered
5. For each discovered system: KEEP / MIGRATE / REMOVE / DEFER
6. Graph consumers found
7. Economy dependencies found
8. Event/quest dependencies found
9. Save fields migrated
10. UI removed or repurposed
11. Arrival behavior changes
12. Embark behavior changes
13. Discovery behavior changes
14. Believed-position behavior verified
15. Multiplayer implications
16. Regression tests
17. Any additional obsolete feature discovered during the pass

Do not silently delete shared infrastructure.

---

# 43. FINAL TARGET

After this cleanup, the player-facing navigation model should be conceptually simple:

```text
Embark
↓
Sail anywhere
↓
Observe physical world
↓
Use compass / sky / charts / local viewscape
↓
Reason about where you are
↓
Find a harbor physically
↓
Dock explicitly
```

And the map model should be:

```text
Physical observation
≠ cartographic knowledge

Cartographic source
→ physical chart/evidence
→ deliberate integration
→ shared map knowledge

Player-authored belief
can remain wrong
```

Anything still forcing the old:

```text
pick node
→ unlock edge
→ travel route
→ arrive automatically
```

model is a cleanup candidate.

---

**End of Legacy Navigation / Cartography Cleanup & Migration handoff.**
