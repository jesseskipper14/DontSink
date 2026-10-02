# Don't Sink — Dynamic BoatScene Seafloor + Geographic Land Encounter Handoff

**Implementation owner:** Bosun  
**Architecture/spec owner:** Keel  
**Project:** *Don't Sink*  
**Engine:** Unity 6.0, URP 2D, C#  
**Pass type:** Major architecture / world-generation pass  
**Primary goal:** Replace finite authored BoatScene seabed with continuously generated terrain that follows authoritative world navigation context while preserving smooth 2D presentation  
**Secondary goal:** Establish the first reusable world-to-side-view projection seam for islands and future surface encounters  
**Networking target:** One shared boat, host-authoritative simulation; local cameras/presentation remain per-player  
**Important:** Networking transport is not implemented yet. Do not invent it in this pass.

---

# 0. Read This First

This is an **audit-first, phased implementation pass**.

Do not begin by writing an infinite terrain generator from scratch based on assumptions.

Before modifying code:

1. Audit the current repository.
2. Identify the exact current classes/services responsible for:
   - physical BoatScene travel,
   - top-down navigation position and heading,
   - current route / travel state,
   - current BoatScene seabed / `EdgeCollider2D`,
   - current ground rendering material/shader,
   - water rendering and waterline,
   - underwater ground/surface queries,
   - player out-of-bounds / ground snap recovery,
   - scene persistence / world-item restore,
   - deployed diving bell behavior and seabed contact,
   - anchor behavior and seabed contact,
   - resource spawning / terrain alignment,
   - world topography sampling,
   - biome/topography classifications,
   - travel seed / world seed / node identity,
   - any current systems that assume a finite BoatScene ground span,
   - any current systems that directly reference a specific authored ground object.
3. Produce a concise dependency map and exact modification plan.
4. Then implement the phases below in order.
5. If the audit exposes a foundational conflict with this spec, stop and report it rather than bulldozing forward.

### Exact-current-class rule

> **Never reconstruct an existing class from memory, old chats, prior handoffs, or guessed filenames. Use the exact current repository version.**

New classes may be authored freely.

Preserve unrelated behavior.

---

# 1. Prime Directive

This entire feature exists because the game represents a 3D/top-down navigable world through a 2D side-scrolling BoatScene.

That means exact geometric projection is not the goal.

The governing rule is:

> **World space determines gameplay truth. Side-view space communicates that truth smoothly. It is not required to geometrically reproduce 3D geography accurately.**

Priority order:

```text
1. World/gameplay correctness
2. Physical continuity
3. Visual continuity
4. Broad geographic resemblance
5. Exact 3D -> 2D spatial correspondence
```

Item 5 loses whenever it conflicts with the first four.

This is intentional design, not a bug.

---

# 2. Core Coordinate Model

The project needs three distinct coordinate concepts.

## 2.1 True Navigation Position

Authoritative top-down world position.

Conceptually:

```text
NavigationPosition = true geographic position in the world map
```

This continues to update whether or not the player knows where they are.

Player "lostness" is knowledge/presentation only.

The game itself always knows the true position.

## 2.2 Voyage Strip Position

Introduce or formalize a scalar logical coordinate representing progress through the side-view journey.

Conceptually:

```text
VoyageStripPosition = accumulated signed travel through the physical side-view journey
```

Important semantics:

- Physical forward travel increases VoyageStripPosition.
- Physical reverse travel decreases VoyageStripPosition.
- Geographically turning left/right does NOT reverse VoyageStripPosition.
- Turning 180 degrees in top-down world space while still moving physically screen-forward continues advancing the strip.

This coordinate exists because BoatScene X is not east/west or north/south.

It means:

> **distance through the continuously unfolding journey**

not a fixed world axis.

## 2.3 Local BoatScene X

Current Unity/Physics2D local physical position.

Conceptually:

```text
Local BoatScene X <-> VoyageStripPosition
```

The system should provide a clean mapping seam.

### No floating origin in this pass

Do not implement scene recentering unless the audit proves an immediate precision problem.

The playable world will ultimately have soft and hard lethal boundaries, so truly enormous coordinates should not be reachable under normal play.

Still keep logical strip identity separate from Unity transform X so floating-origin support remains possible later if ever needed.

---

# 3. Base Seafloor Rules

The base terrain is:

- underwater seafloor only,
- one continuous rolling seabed,
- immutable once committed,
- not destructible,
- not the island itself,
- not caves,
- not overhangs,
- not POIs,
- not wrecks,
- not volcano meshes,
- not destination structures.

The ground is effectively adamantium.

If players want tunneling, they can play Minecraft.

The current `EdgeCollider2D` style is the intended starting point.

---

# 4. Base Terrain Shape Language

Ordinary seabed should mostly be:

- smooth,
- gently rolling,
- physically continuous,
- easy to read,
- compatible with current underwater traversal,
- compatible with player grounding,
- compatible with bell contact,
- compatible with anchors,
- compatible with loose objects,
- compatible with future deterministic resources.

Special dramatic geography is layered in later via explicit feature directives.

Examples of future terrain-shaping features:

```text
trench
sinkhole
volcanic rise
massive ridge
continental-shelf drop
```

Examples of future overlay/secondary geometry:

```text
cave
ruin
wreck
rock arch
secondary terrain shelf
Dave-the-Diver-style alternate layer
```

Base terrain should remain a single-valued function of horizontal strip position.

---

# 5. Phase 0 — Repository Audit

Before code changes, identify:

- the current authored ground object(s),
- how ground rendering is performed,
- how `EdgeCollider2D` is configured,
- whether any gameplay code requires the existing ground object's exact component identity,
- how player ground queries work,
- how anchor/bell contact is detected,
- how resource spawners query terrain,
- how persistence restores objects relative to ground,
- how topography is exposed at runtime,
- how the current world-map coordinate space maps to navigation state,
- how current active travel / route seed is represented.

Deliver:

```text
current system
-> multiplayer/world-generation risk
-> proposed modification/new class
```

Do not implement further phases until this architecture map is coherent.

---

# 6. Phase 1 — Voyage Strip Coordinate

Establish the logical strip coordinate without yet replacing terrain.

Desired conceptual flow:

```text
actual signed physical BoatScene travel
             |
             +--> VoyageStripPosition
             |
             +--> projected through current heading
                     |
                     +--> NavigationPosition
```

The physical side-view journey and top-down world path must no longer be treated as the same coordinate axis.

### Acceptance tests

- Sail physically forward: strip increases.
- Reverse physically: strip decreases.
- Turn geographically while still moving screen-forward: strip keeps increasing.
- Spin top-down heading 180 degrees and continue physical forward travel: strip still increases.
- NavigationPosition responds to heading correctly.
- Existing piloting feel remains unchanged.

---

# 7. Phase 2 — Generated Seafloor Chunks

Replace the authored static BoatScene seabed as terrain authority with generated chunks.

## 7.1 Chunk identity

Each chunk should represent a stable range of VoyageStripPosition.

Use a deterministic seed hierarchy conceptually like:

```text
world/voyage seed
+ logical chunk coordinate
+ terrain channel salt
= deterministic base terrain RNG
```

Do not couple terrain RNG consumption to resources, POIs, or future systems.

Use distinct deterministic channels/salts later.

## 7.2 Chunk size

Do not hardcode based only on this document.

Audit current physical scale first.

Current visual context:

- present camera/skybox visible width is roughly 100 world units,
- future optics may increase visible distance,
- chunk size should be large enough to avoid excessive GameObjects/collider overhead,
- small enough to stream and regenerate smoothly.

Likely practical range is around tens to low hundreds of physical world units.

Make it configurable.

## 7.3 Seam continuity

Neighboring chunks must share boundary samples or otherwise guarantee continuous geometry.

No collider crack, one-pixel gap, visible vertical seam, or slope discontinuity caused by chunk ownership.

The player should not be able to identify chunk boundaries visually.

## 7.4 Existing visual shader

Reuse the existing ground-generation / seabed material/shader where practical.

A visual pass is required in this feature because the user needs visible terrain cues to certify that dynamic generation and geographic behavior are correct.

Do not redesign final ground art.

---

# 8. Interest-Source Loading

Terrain streaming is driven by relevant physical actors, NOT camera position.

Potential initial interest sources include:

- boat,
- living player,
- deployed diving bell,
- active anchor / seabed contact requirement,
- any authoritative/persistent object the audit proves must keep terrain alive.

Do not make every loose item retain chunks forever.

The project will later enforce a bounded player excursion radius via an in-universe lethal "boat aura" / sea-monster consequence, so terrain loading does not need to support arbitrary player separation across the entire world.

## Vertical rule

One large vertical ground chunk / column.

Do not vertically chunk base terrain.

The loaded local region should support surface, all water depth, seabed, and anchor/bell/player interaction from top to bottom.

---

# 9. Phase 3 — Forecast vs Committed Terrain

This is a foundational rule.

```text
HISTORY / COMMITTED                  FORECAST
===============================|~~~~~~~~~~~~~~~~~~~~
cannot change                        may be replanned
```

## 9.1 Committed terrain

Terrain becomes committed once it is sufficiently near/visible/reachable to any relevant interest source.

Committed terrain:

- does not morph,
- does not move because heading changed,
- does not get rewritten by later forecast corrections,
- becomes part of the local journey history.

## 9.2 Forecast terrain

Terrain far enough ahead to remain unseen/unreachable may change when:

- heading changes,
- macro topography target changes,
- a future terrain-shaping feature is no longer on the projected path,
- projected island/land context changes.

This is how the side-view cheats smoothly while the top-down world remains authoritative.

## 9.3 Commit horizon

Do not bind commit distance directly to camera zoom.

Use a gameplay-safe margin such as:

```text
furthest relevant actor
+ expected actor lead
+ physics safety buffer
+ hidden visual/preload buffer
```

Future telescope/optics support should be able to expand visual/preload distance without redefining terrain authority.

### Acceptance test

Approach a forecast depth change, turn away before it commits, and verify hidden future terrain replans. Repeat after some of the transition has committed and verify committed terrain stays fixed while later future terrain resolves toward the new geographic course.

---

# 10. Phase 4 — World Topography Drives Macro Depth

The existing world-map/topography system should influence generated BoatScene depth.

But it is a macro signal, not a literal side-view heightmap.

Conceptual flow:

```text
predicted true navigation position
          |
          v
world topography / broad depth context
          |
          v
desired BoatScene depth
          |
          v
smoothing + slope safety
          |
          v
generated seabed
```

## 10.1 Depth mapping

Provide one centralized configurable mapping from world topography/depth normalization to gameplay depth.

Current target:

```text
surface                ~0
shallow water          configurable
shelf/deep water       configurable
maximum base depth     ~500 m / Unity world units
```

The user wants this easy to retune later.

Do not spread hardcoded depth thresholds across multiple classes.

Use a profile/curve/data object appropriate to current project patterns.

## 10.2 Safety overrides literal topography

If macro geography says 15m and then abruptly 400m trench/deep area, the physical BoatScene should transition safely.

A configurable maximum base slope around ~45 degrees is acceptable as an emergency upper limit.

Ordinary terrain should be much gentler.

> Topography requests a target depth. It does not command an instantaneous vertical jump.

---

# 11. Phase 5 — Terrain Feature Planning Seam

Terrain-shaping features must be planned BEFORE terrain is committed.

Never use:

```text
generate floor
-> later delete floor
-> insert trench
```

Use:

```text
baseline topography
+ terrain-shaping feature directives
-> desired profile
-> smoothing / feature rules
-> committed terrain
```

Future feature types may include trench, cliff, volcano, sinkhole, ridge, shelf break.

Ordinary terrain follows normal safety/slope rules.

Explicit special features may later request steeper or near-vertical geometry if they provide sufficient warning/lead-in.

For this pass, one development-only proof feature is sufficient to validate the seam.

Do not build the entire dramatic-feature library now.

---

# 12. Base Seafloor Is Always Present Near Land

Do NOT make island land geometry replace the local underwater seafloor.

Near an island:

```text
             ISLAND BACKGROUND
            ###################
         #########################
~~~~~~~~~~~~~~~~ WATER ~~~~~~~~~~~~~~~~

________________________________________
      GENERATED LOCAL SEAFLOOR
```

The underwater base floor always remains present.

This is critical because players may jump off the boat near land, the bell may deploy, anchors may reach bottom, loose objects need ground, and land collision may be active for the boat while players remain locally free.

---

# 13. Phase 6 — Geographic Land Encounter Detection

Land collision is primarily a NAVIGATION/WORLD-SPACE concept.

A normal static side-view island collider cannot represent arbitrary top-down heading changes correctly.

Therefore separate:

```text
A. true geographic land/sea classification
B. side-view island visual presentation
C. physical boat obstruction enforcement
```

These must not be one monolithic system.

---

# 14. Dominant Island Encounter

First pass supports:

> **At most one dominant nearby island/landmass encounter visual at a time.**

The selection should be based on authoritative world/nav context.

It may consider:

- relevant landmass/island proximity,
- island size,
- projected path relevance,
- current active encounter stickiness.

Do not build a full skyline of every island in the region.

---

# 15. Island Size and Visibility

The world-map island size should influence:

1. visual detection distance,
2. side-view apparent length/scale.

Make both tunable.

Examples:

```text
small island
-> visible fairly late
-> occupies part of one screen

large island
-> visible from farther away
-> grows substantially
-> may occupy several screens while traveling alongside it
```

Exact map-to-side-view scale is NOT sacred.

Perceptual believability wins.

---

# 16. Phase 7 — Stable Island Projection

The island's side-view presentation should remain visually stable even when the player changes heading.

## 16.1 Core rule

> **Do not derive island screen side directly from current boat orientation.**

Otherwise a player spinning the boat could make an island whip around the screen.

That is forbidden.

## 16.2 Presentation phases

Conceptually:

```text
APPROACHING
-> island enters/grows from the right

ALONGSIDE
-> island may occupy most or all horizontal view
-> large islands may persist across multiple screens

RECEDING
-> island trails left
-> shrinks over distance
```

Once the current island encounter chooses a stable presentation side/state, keep it sticky.

Do not left/right flip because of small heading changes.

If the island becomes large enough to fill the whole horizon, left/right is no longer visually important until leading/trailing edges eventually resolve.

## 16.3 Visual smoothness outranks 3D accuracy

If exact projected world geometry would require teleporting island position, snapping scale, moving the island quickly across the screen, or visibly reordering already-established local scenery, do NOT do it.

Preserve visual continuity instead.

World truth still drives gameplay.

The visual is allowed to lie.

## 16.4 First-pass visuals

Procedural generic island silhouette is acceptable.

Reuse existing ground/land visual language and shader/material where practical.

Water should render in front of the island, consistent with the current NodeScene visual style.

Final island art is out of scope.

---

# 17. World-to-Side-View Surface Encounter Seam

Do not build a universal encounter framework now.

However, isolate the world-to-strip projection logic enough that future surface encounters can reuse it.

Future examples:

- other boat,
- derelict,
- whale,
- buoy,
- floating platform,
- storm wall,
- large floating debris.

Example future boat behavior:

```text
far right / small
-> approaches
-> grows
-> passes
-> leaves left
-> shrinks
```

This should follow the same visual-stability rules as islands.

Moving surface objects may later include true world velocity.

Do not implement them now.

Just preserve the seam.

---

# 18. Phase 8 — Geographic Boat Obstruction

The game must prevent the boat from traveling deeper into world-space land.

This is a gameplay-truth problem, not a visual-island-collider problem.

## 18.1 Semantics

Allow some coastline tolerance.

Do not treat the first exact land pixel as an inviolable forcefield.

Desired rule:

```text
movement that increases penetration into forbidden land
-> blocked

movement parallel to coastline
-> allowed

movement away from land
-> allowed
```

This lets a boat overlap land slightly in world-space representation without becoming trapped.

## 18.2 Boat only

The geographic obstruction applies to the MAIN BOAT.

It should NOT block player, diving bell, anchor, loose cargo, or random world items.

Near land, players should still be able to jump into shallow water and interact with the local seabed.

## 18.3 Physical enforcement

First implementation should use a simple boat-only **geographic barrier proxy**.

Likely first attempt:

- invisible collider,
- dedicated collision layer,
- boat hull collision only,
- zero/near-zero friction,
- zero bounciness,
- simple tall barrier,
- placed in the physical direction the boat was attempting to travel.

This should make:

```text
push forward into land
-> boat physically stops

reverse away
-> boat escapes
```

Important:

> **Geographic detection and physical enforcement must be separate systems.**

Conceptually:

```text
GeographicObstructionDetector
-> says movement is forbidden

BoatBarrierProxy
-> currently translates that truth into Physics2D
```

If Unity responds with hull rotation, chatter, wall climbing, buoyancy fighting contacts, or unstable impulses, the enforcement implementation must be replaceable later without rewriting world-space detection.

---

# 19. Grounding Consequences Are Deferred

For this pass, land obstruction is mostly a physical stop.

Future pass may add grounding damage, stuckness, scraping, prop/rudder damage, escalating consequences for continuing throttle, and explicit recovery actions.

Do not implement those now unless a tiny seam is needed.

---

# 20. Phase 9 — World Boundary States

The world has a finite intended playable geographic extent.

No wraparound.

No normal endless terrain beyond the intended world.

The player should not hit a hard invisible wall immediately.

Use graduated boundary bands.

Conceptual model:

```text
NORMAL WORLD
    |
    v
SOFT BOUNDARY
- increasingly severe weather/environment
- warning cues
- "TURN BACK" buoy or equivalent
- still recoverable
    |
    v
HARD BOUNDARY
- extreme conditions
- persistent ship damage
- unmistakable visual/audio warning
    |
    v
continued stupidity
- ship eventually sinks
```

The user is allowed to sail some distance into the warning area before punishment becomes severe.

The underlying mathematical boundary may be based on existing baked topography/world extents.

Do not expose the map as an obvious known rectangle.

For this pass, implement the minimal boundary state/seam needed to stop the architecture from assuming infinite safe world continuation.

Full final storm spectacle and damage balancing may remain later depending on existing APIs.

---

# 21. Destination Arrival Seam

Do not require exact physical docking in BoatScene.

Future node/destination arrival should use a configurable zone in TRUE WORLD/NAVIGATION SPACE.

Conceptually:

```text
destination node
+ configurable arrival radius / region
-> boat enters zone
-> destination becomes available
-> future "ARRIVE AT DESTINATION" interaction appears
```

The user can tune difficulty through arrival-zone size.

The destination interaction/transition itself is a later pass.

Do not couple destination arrival to island sprite overlap or physical BoatScene dock position.

---

# 22. Island Collision and Destination Arrival Are Separate

Land remains land.

Boat cannot simply drive through it.

A destination node does not need to create a special hole in the land mask.

Instead:

```text
approach island / destination
-> normal land obstruction still applies

enter valid offshore world-space arrival zone
-> destination becomes available

choose ARRIVE later
-> transition to NodeScene / dock experience
```

This is intentionally simpler and more robust than requiring exact side-view harbor geometry.

---

# 23. Persistence Philosophy

Exact seabed geometry across reloads is NOT sacred.

Do not save every collider point.

It is acceptable for the broad seafloor to regenerate somewhat differently across separate visits or reloads, as long as macro theme/depth remains appropriate, scene-persistent objects are restored safely, and nothing remains embedded below terrain.

On restore:

```text
generate current local seabed
-> restore scene-persistent object
-> validate against terrain
-> if below/invalid
-> push/snap to legal surface
```

The project already has at least one player ground-snap / out-of-bounds correction path.

Audit whether it can be generalized or reused.

Slightly above-ground restored objects are acceptable; gravity can settle them.

Below-ground restore must be corrected.

---

# 24. Resource and POI Integration Seam

Resources and POIs are OUT OF SCOPE for actual spawning in this pass.

However each committed terrain chunk should expose enough context for the next pass.

Useful chunk context may include:

```text
chunk identity
voyage-strip range
associated/predicted true navigation samples
macro topography classification
broad depth
biome/world context if available
surface height query
surface normal / slope
load / unload
forecast / commit events
```

The next resource pass should be able to consume terrain context without becoming part of terrain generation.

Future deterministic resource seed concept:

```text
world/voyage seed
+ logical chunk coordinate
+ resource channel salt
= resource RNG
```

Terrain RNG and resource RNG must not share consumption order.

---

# 25. Multiplayer / Authority Rules

Future target:

```text
one shared boat
host-authoritative simulation
clients send intents
replicated state
local presentation
```

Dynamic terrain/world truth should be compatible with that.

Authoritative/shared concepts include true NavigationPosition, VoyageStripPosition, committed terrain identity/state, land obstruction truth, destination arrival truth, world boundary state, and future authoritative resource/POI state.

Local-only concepts include per-player camera and presentation smoothing such as island visual interpolation, provided that local presentation does not alter shared gameplay truth.

Do not implement networking transport or RPCs.

Do not make camera position terrain authority.

---

# 26. Visual Smoothness Requirements

This deserves explicit acceptance criteria.

Never visibly:

- teleport island visual due to heading change,
- snap island from right to left,
- make a nearby island orbit the player because the boat rotates,
- morph committed ground under an actor,
- instantly drop seabed hundreds of units,
- slide committed terrain around to satisfy new cartographic math,
- expose a chunk seam,
- despawn terrain still needed by an interest source.

If exact geographic projection conflicts with a smooth local representation:

> **keep world-space gameplay truth correct and let the visual cheat smoothly.**

---

# 27. Required Validation Scenarios

## A. Strip coordinate

Forward, reverse, geographic turn, and 180-degree heading change.

Verify strip semantics remain consistent.

## B. Horizontal streaming

Travel across many chunk boundaries.

Expected: no seams, no ground gaps, no falling through, no camera dependence.

## C. Multiple interest sources

Boat + swimmer + deployed bell + active anchor.

Expected: all required horizontal regions stay loaded, terrain exists top-to-bottom, chunks unload once truly no longer relevant.

## D. Shallow -> deep topography

Travel from shallow world region toward a deep region.

Expected: seabed transitions smoothly, maximum slope respected, no sudden collapse.

## E. Turn away before commit

Forecast a large depth change and turn away while still hidden.

Expected: future terrain replans.

## F. Turn away after partial commit

Allow some terrain to become visible/physical, then turn away.

Expected: committed terrain remains, later future terrain resolves smoothly.

## G. Development trench directive

Inject one test terrain feature.

Expected: feature influences planned profile; no destructive hole-cutting into committed terrain.

## H. Small island encounter

Approach a small world-map island.

Expected: background island appears from appropriate stable side, grows smoothly, remains visually stable while steering, recedes/shrinks smoothly.

## I. Large island encounter

Approach a large island closely.

Expected: island can occupy most/all of screen, may persist across multiple screens, reads as traveling alongside land rather than scaling a tiny sprite absurdly.

## J. Spin / turn near island

Change heading aggressively.

Expected: island does not orbit the screen, presentation stays sticky, world-space land logic still updates.

## K. Boat obstruction

Drive geographically into land.

Expected: boat-only barrier engages, boat physically stops, player/bell/anchor remain free on local seabed.

Reverse.

Expected: barrier permits escape.

Turn parallel / away.

Expected: obstruction releases appropriately.

## L. World boundary

Travel beyond intended playable region.

Expected: soft warning state first, no immediate invisible wall, hard danger state later.

## M. Persistence restore near seabed

Restore player/object below generated terrain.

Expected: recovery/snap pushes it to legal position.

Restore slightly above floor.

Expected: normal physics settles it.

---

# 28. Explicitly Out of Scope

Do NOT bundle these into this pass:

- final island art,
- multiple simultaneous island skyline system,
- caves,
- full secondary terrain layer,
- destructible terrain,
- mining/tunneling,
- final trench/volcano feature library,
- dynamic resource spawning,
- POI spawning,
- moving NPC boats,
- whales/derelicts/etc.,
- final destination arrival interaction,
- NodeScene transition implementation,
- grounding damage,
- stuck-boat recovery systems,
- final world-edge storm VFX/audio,
- final world-edge hull-damage balancing,
- networking transport,
- replication/RPC implementation,
- floating origin,
- split-screen,
- final camera work,
- star-map puzzle.

---

# 29. Architecture Smells to Avoid

Do not create an `InfiniteWorldManagerGodObject` that owns terrain, islands, land collision, resources, destinations, weather, save/load, UI, cameras, and network authority.

Prefer narrow responsibilities and data flow.

Also avoid:

- camera-driven terrain truth,
- island visuals as collision authority,
- side-view sprite overlap as destination authority,
- deleting committed terrain to place features,
- storing raw collider vertices as primary persistence,
- random generation dependent on frame order,
- RNG shared between terrain and resources,
- per-frame global object searches,
- full 2D/3D projection math whose only purpose is "accuracy" that harms visual continuity.

🍌 **Banana check:** if a subsystem is becoming more sophisticated solely to maintain an exact 3D representation in the side-view, simplify it.

---

# 30. Suggested Conceptual Responsibility Split

Use current project patterns and audit results rather than blindly creating these exact names.

Conceptually healthy responsibilities are:

```text
Voyage Strip State
- scalar strip position
- strip/local conversion

Terrain Planner
- forecast
- topography sampling
- desired depth profile
- feature directives

Terrain Streamer
- chunk lifecycle
- interest sources
- commit/unload

Terrain Chunk
- generated visual/collider data
- queries

Geographic Land Query
- water/land truth
- coastline / penetration checks

Surface Encounter Projection
- stable side-view encounter planning
- island visual progression
- future reusable seam

Boat Geographic Barrier
- physical enforcement only

World Boundary State
- normal / soft / hard boundary

Destination Acquisition
- future world-space arrival-zone seam
```

Do not create interfaces/classes unless they materially improve ownership.

---

# 31. Acceptance Criteria

This pass is complete when:

- [ ] BoatScene no longer depends on a finite authored seabed as terrain authority.
- [ ] A logical VoyageStripPosition exists independently from true NavigationPosition.
- [ ] Physical forward/reverse travel drives the strip coherently.
- [ ] Geographical turning does not rotate/rebuild committed side-view history.
- [ ] Terrain streams around relevant actors, not the camera.
- [ ] Ground chunks connect without visible or physical seams.
- [ ] Base terrain is immutable once committed.
- [ ] Hidden forecast terrain can replan.
- [ ] World topography influences broad seabed depth.
- [ ] Depth mapping is centralized/configurable.
- [ ] Ordinary depth transitions are slope-limited and visually safe.
- [ ] A future terrain-feature directive seam exists.
- [ ] Base seabed remains present near land.
- [ ] Land detection is authoritative world-space logic.
- [ ] Island visual presentation is separate from land collision.
- [ ] First-pass island visuals are smooth and stable.
- [ ] Island size affects visibility distance and apparent span.
- [ ] Large islands can convincingly occupy multiple screens.
- [ ] Heading changes do not make island visuals whip around the screen.
- [ ] Boat-only geographic obstruction exists.
- [ ] Reversing away from land is always possible.
- [ ] Parallel/away movement can release land obstruction.
- [ ] Players/bell/anchor still interact with shallow local seabed near land.
- [ ] Soft/hard world-boundary architecture exists.
- [ ] No world wraparound exists.
- [ ] Destination arrival can later use a true-world configurable zone.
- [ ] A reusable seam exists for future projected surface encounters.
- [ ] Exact terrain persistence is not overengineered.
- [ ] Resources/POIs are not implemented yet.
- [ ] Networking transport is not implemented.
- [ ] Visual smoothness wins over unnecessary projection accuracy.

---

# 32. Final Design Statement

The BoatScene is not a literal orthographic slice through the world map.

It is:

> **a continuous, physically interactive, visually smooth side-view dramatization of the boat's true journey through authoritative world space.**

World space answers:

```text
Where are we?
What geography is nearby?
Are we in water or land?
Are we near a destination?
Are we leaving the playable world?
```

The BoatScene answers:

```text
What should this journey look and feel like right now?
```

Those answers must agree on gameplay truth.

They do NOT need perfect geometric correspondence.

That concession is deliberate and fundamental to the game.

---

# 33. Post-Pass Roadmap

After this pass:

```text
Dynamic BoatScene terrain
        |
        v
Dynamic resource generation
        |
        v
POI / secondary terrain / richer feature generation
        |
        v
Destination-arrival implementation
        |
        v
Future moving surface encounters
```

Large physical impulse/damage work and the real star-map puzzle remain separate roadmap features.

---

**End of Bosun handoff.**
