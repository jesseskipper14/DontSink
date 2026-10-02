# DON'T SINK — Celestial Dynamics Foundation Bosun Handoff
## Deterministic Moving Celestial Bodies, Local Sky State, Modifiers, and Event-Condition Seams

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature family:** Star-charting / celestial systems  
**Scope:** Foundational seams only. Final planet content is intentionally not authored yet.  
**Purpose:** Add a deterministic dynamic-celestial framework that can later accept planets, moons, comets, wandering bodies, eclipses, alignments, environmental modifiers, and event hooks without planet-specific hardcoding.

---

# 0. NON-NEGOTIABLE PROJECT RULES

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before modifying an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized fields, authority hooks, save compatibility, and current multiplayer hardening.
3. Prefer additive support classes and small interfaces over invasive rewrites.
4. Reuse the current celestial truth, chart evidence, naming, world-time, map-coordinate, and event-query architecture where cleanly possible.
5. Do not regress Phase 6/7 celestial chart behavior.

## This is not an astrophysics simulator

Do not implement:
- Keplerian orbital mechanics,
- n-body gravity,
- realistic planetary distances,
- astronomical ephemeris math,
- spherical-world rendering.

The system is a deterministic gameplay model.

A body may simply have a 30-day period, traverse its authored path for 15 days, be below the horizon for 15 days, then repeat.

---

# 1. DESIGN PILLAR

The sky is not decoration.

It is:
- navigation information,
- environmental information,
- event information,
- a player-observable forecasting layer.

The player should be able to notice patterns such as:

> "Whenever Grok42069 is nearly overhead, everything feels heavier."

The world reacts to celestial truth whether or not the player understands why.

The game should not automatically explain those relationships.

---

# 2. TRUTH / VISIBILITY / KNOWLEDGE / EFFECTS MUST REMAIN SEPARATE

For dynamic celestial bodies, preserve the project's broader distinction:

## Celestial truth
Where the body actually is according to:
- body definition,
- world time,
- queried world position,
- world topology.

## Visibility
Whether the player can currently see it after:
- horizon check,
- day/night sky rendering,
- cloud/fog/weather obstruction,
- other presentation rules.

## Player knowledge
What the player has:
- observed,
- charted,
- named,
- written notes about,
- inferred personally.

## Mechanical effects
What the body actually does to the world:
- gravity multiplier,
- ambient tint,
- event-condition tags,
- spawn-weight changes,
- future tide/weather/creature effects.

Mechanical effects use **truth**, not visual visibility.

Clouds can hide a planet without turning off its gravity.

---

# 3. DETERMINISTIC DYNAMIC BODY MODEL

Every dynamic celestial body has a stable definition and stable ID.

Conceptually:

```text
DynamicCelestialBodyDefinition
- StableId
- CanonicalName
- BodyType
- Period
- PhaseOffset
- VisibleDuration / visibility arc
- GroundTrack / authored closed path
- Presentation data
- Modifier definitions
- Condition tags / metadata
- Version
```

Body types should be general enough for:
- planet,
- moon,
- comet,
- wandering star,
- artificial object,
- future weird celestial object.

Do not create planet-only assumptions in core logic.

---

# 4. WORLD TIME IS THE AUTHORITATIVE ORBIT CLOCK

Orbital/body state is derived from world time.

Given:

```text
body definition
+ world time
+ query world position
```

the system should deterministically derive the body's local apparent state.

No current orbital transform needs to be saved.

No per-frame position persistence.

No offline time progression.

If the game clock does not advance while the game is closed, celestial bodies do not advance while the game is closed.

---

# 5. WRAPPED WORLD ASSUMPTION

The project is moving toward:

- periodic/wrapped world X,
- finite world Y,
- north/south bounded by physical polar ice sheets.

Dynamic celestial design should assume that future topology.

Do **not** hardcode raw non-wrapped X differences.

Prefer a world-topology service/query for:
- NormalizeX
- WrappedDeltaX
- WrappedDistance
- NearestEquivalentX

The wrapped-world implementation is a separate pass.

If this celestial pass lands first, isolate world-topology assumptions behind an interface/service so the later wrap pass can replace the implementation cleanly.

---

# 6. AUTHORED CLOSED GROUND TRACKS

## 6.1 Core idea

Each dynamic body follows a deterministic **closed path over the world map**.

Think of this as the body's subpoint / ground track.

At the ground-track point:
- the body is at/near zenith.

Farther away:
- the body appears lower in the sky.

Far enough away:
- the body is below the horizon.

This avoids arbitrary per-location chart coordinates.

## 6.2 Authoring model

Final content should support authored closed tracks such as:
- closed spline,
- closed polyline,
- simple ellipse/circle if useful.

The important property is direct artistic/content control.

This lets designers work backward from content:

> "At Drowned Cathedral, on this phase/day, the Ringed Planet should pass overhead."

Then author/tune the body's path and phase to satisfy that event.

## 6.3 Path evaluation

Conceptually:

```text
cycle01 = Repeat((worldTime + phaseOffset) / period)

groundTrackPoint = GroundTrack.Evaluate(cycle01)
```

The path is closed.

For a horizontally wrapped world, tracks may cross the X seam naturally.

---

# 7. SIMPLE PERIOD / VISIBILITY MODEL

A body may have:

```text
Period = 30 days
VisibleDuration = 15 days
PhaseOffset = ...
```

During the visible portion of its cycle:
- it traverses the configured visible sky/ground-track phase.

During the non-visible portion:
- it is below horizon / unavailable according to definition.

Do not force every body to use exactly 50% visibility.

Keep it data-driven.

The model should be simple enough that a designer can reason about it without orbital mechanics.

---

# 8. LOCAL APPARENT SKY STATE

Provide a query conceptually like:

```text
EvaluateBodyAt(
    bodyId,
    worldPosition,
    worldTime
)
```

Return a compact local state, e.g.:

```text
DynamicCelestialLocalState
- StableBodyId
- CyclePhase01
- GroundTrackPosition
- IsAboveHorizon
- ApparentSkyPosition / angle
- Altitude
- ZenithFactor01
- Angular size / presentation scale if needed
- Truth-active flags
```

The exact types/names should fit current celestial architecture.

---

# 9. HORIZON / ZENITH MODEL

At/near the body's current ground-track point:
- altitude is highest,
- zenith factor approaches 1.

As wrapped world distance increases:
- altitude falls,
- zenith factor approaches 0.

Past the configured horizon range:
- body is below horizon,
- not rendered,
- local above-horizon effects may become zero if their definition uses zenith/altitude.

The exact mapping should be data-driven and gameplay-friendly.

No real spherical trigonometry is required.

---

# 10. LOCALITY SCALE

The game only needs one authoritative active BoatScene at a time.

Therefore:

> Consequential local celestial state may be evaluated once at the boat's authoritative world position and treated as uniform across that local BoatScene.

Do not evaluate planet gravity independently for every crate, fish, or player.

A few hundred local meters are negligible at world scale.

Other systems may query their own world coordinates when needed:
- town event generation,
- world-map POIs,
- quest locations,
- remote simulation,
- future settlement effects.

---

# 11. DATA-DRIVEN CELESTIAL MODIFIERS

Dynamic bodies should not directly modify arbitrary gameplay systems.

Instead, evaluate data-driven modifier outputs.

Examples:

```text
CelestialModifier
- ModifierId / Tag
- SourceBodyId
- Value
- Strength01
- QueryPosition
```

Potential domains:
- global/local gravity multiplier,
- ambient color/tint influence,
- creature-spawn modifier,
- fish-spawn modifier,
- event weight,
- weather bias,
- future tide modifier,
- future economy modifier,
- future NPC behavior tag.

The celestial system publishes/query-exposes state.

Consumers decide what to do with it.

---

# 12. ZENITH-DRIVEN CONTINUOUS EFFECTS

Support continuous effects driven by local body geometry.

Example:

```text
Big Planet
ZenithFactor = 1.0
Gravity multiplier = 1.20

ZenithFactor = 0.5
Gravity multiplier = 1.10

ZenithFactor = 0.0
Gravity multiplier = 1.00
```

Use configurable curves where appropriate:

```text
EffectStrength = Curve(ZenithFactor01)
```

Do not hardcode "planet X changes gravity" in celestial core.

---

# 13. THRESHOLD / BOOLEAN EFFECTS

Also support threshold-style effects.

Examples:
- Ringed Planet above horizon → `RingedPlanetVisibleTruth`
- Planet near zenith → `RedPlanetApex`
- Eclipse active → `SolarEclipse`
- Three-body alignment → `TripleAlignment`

This supports both continuous modifiers and discrete event conditions.

---

# 14. GLOBAL QUERY SERVICE

Expose a read-only, authoritative celestial dynamics service/API.

Conceptually:

```text
GetBodyStateAt(bodyId, worldPosition, worldTime)

GetDynamicCelestialStatesAt(worldPosition, worldTime)

GetActiveCelestialConditionsAt(worldPosition, worldTime)

GetCelestialModifiersAt(worldPosition, worldTime)
```

Consumers should not implement orbital/path math themselves.

Potential consumers:
- BoatScene physics,
- lighting,
- creature spawning,
- event system,
- quest system,
- towns,
- NPC dialogue,
- economy,
- terrain/content systems.

---

# 15. AUTHORED + EMERGENT CELESTIAL CONDITIONS

Support both.

## 15.1 Authored conditions

Designed for specific content.

Example:

```text
At Drowned Cathedral:
- Ringed Planet above horizon
- Red Planet within X degrees of Moon
- local time/phase window
- player has met cultists
```

The event system can combine celestial and non-celestial requirements.

## 15.2 Emergent conditions

Generic evaluators may detect naturally occurring configurations even if no designer explicitly planned them.

Examples:
- any 3 bodies within 5 degrees,
- body near zenith,
- body near Moon,
- body near Sun,
- eclipse/overlap,
- N bodies within an angular arc.

This allows generic systems to react to surprising alignments.

---

# 16. CONDITION COMPOSITION

Prefer data-driven condition definitions over bespoke scripts per event.

Useful primitive condition types may include:

```text
BodyAboveHorizon
BodyBelowHorizon
BodyNearZenith
BodyAtCyclePhase
BodyAtApex
BodyWithinAngleOfBody
BodyWithinAngleOfSun
BodyWithinAngleOfMoon
NBodiesWithinArc
BodyOverlap / Eclipse
```

Then combine with AND/OR rules through the existing event-condition architecture if one exists.

Do not make the celestial system directly spawn content.

It should say:

> `RingedConjunction = active`

The event system may then decide whether:
- cultists are eligible,
- player has met them,
- region is correct,
- other quest/state requirements are met.

---

# 17. ECLIPSES / ALIGNMENTS

Do not hardcode a single global eclipse boolean.

Because bodies are location-sensitive, celestial conditions should be queryable **at a world coordinate**.

Conceptually:

```text
EvaluateCondition(
    conditionId,
    worldPosition,
    worldTime
)
```

This permits:
- globally meaningful conditions,
- region-specific alignments,
- events that occur only at particular locations,
- emergent configurations elsewhere.

Final eclipse/alignment content may be added later.

---

# 18. CHARTING DYNAMIC BODIES

## 18.1 Discovery rule

A dynamic celestial body becomes **discovered** when it is successfully captured in a legitimate charting fragment.

Merely seeing it through the Observation Telescope is not enough.

## 18.2 Chart evidence representation

Dynamic-body chart evidence should use the body's deterministic truth state, including:

```text
Body Stable ID
Observed World Time
Observer World Position
Body Ground-Track Coordinate at observation
Observed apparent sky state as needed
```

Do not pretend a moving body has one permanent static star-map coordinate.

## 18.3 Repeated observations

The same body may be charted any number of times.

Repeated observations should be legitimate distinct evidence.

Over time, multiple observations can literally reveal the body's path across the chart/world map.

Do not automatically:
- connect the points,
- infer the period,
- draw a predicted path,
- tell the player where it will be next.

The player can later use freehand cartography tools to draw their own expected path.

---

# 19. STATIC STAR CHART COMPATIBILITY

Static landmark stars remain fixed celestial truth.

Dynamic bodies are separate moving evidence layered into the same charting system.

Do not mutate static star IDs or static field generation.

A chart fragment may contain:
- static celestial evidence,
- one or more dynamic-body observations,
- player annotations later.

---

# 20. NAMING

## 20.1 Canonical/default name

Every authored dynamic body must have a canonical/default name so:
- NPC dialogue,
- lore,
- tomb inscriptions,
- event definitions,
- authored content

can refer to it reliably before the player renames it.

Example:

```text
Stable ID: planet_03
Canonical Name: Vesper
Player Alias: Grok42069
```

## 20.2 Player naming

Once discovered through chart evidence, the player may rename the body through the existing Phase 7 naming/annotation system.

Do not create a separate planet-naming architecture if the Phase 7 system can support dynamic celestial bodies.

Persist:
- stable ID,
- canonical name from definition,
- player alias/name,
- player note.

## 20.3 Dialogue use

Future dialogue may intentionally choose:
- canonical authored name,
- player alias,
- both.

Ancient content should not magically know a player-created alias unless designed to.

---

# 21. PLAYER KNOWLEDGE — DO NOT OVERBUILD YET

Do not implement a formal orbital-knowledge progression system in this pass.

For now:
- player sees bodies,
- charts them,
- names them,
- writes notes,
- personally notices relationships,
- NPCs/documents may hint at relationships.

Future seams may support:
- celestial lore records,
- libraries,
- tomb inscriptions,
- known relationships,
- predicted windows,
- partial historical knowledge.

But do not award automatic knowledge such as:

> "You learned this planet has a 30-day period."

The player is allowed to infer things themselves.

---

# 22. WORLD EFFECTS ARE TRUTH-DRIVEN

Examples:

## Gravity
A heavy planet may publish a local gravity multiplier driven by zenith factor.

The authoritative BoatScene gravity system consumes the modifier.

## Ambient tint
A red planet may publish an ambient-light tint influence.

Presentation/light systems consume it.

## Cult events
A ringed planet may expose condition tags.

The event system decides whether cult content is eligible.

## Fish
An alignment may expose a spawn-weight modifier.

The creature/fish system consumes it.

The celestial system does not directly spawn fish, cultists, quests, or weather.

---

# 23. MULTIPLAYER / AUTHORITY

Consequential dynamic celestial truth is authoritative and deterministic.

Host/server-authoritative systems should own:
- world time,
- body truth evaluation,
- mechanical modifiers,
- active celestial-condition truth,
- chart-evidence creation.

Clients may locally render:
- body sprites,
- colors,
- local visual interpolation,
- sky presentation.

Because truth is deterministic from definitions + time + world position, replication should favor:
- authoritative time,
- definition/version identity,
- consequential derived state when needed,

rather than streaming every body's transform every frame.

---

# 24. SAVE / PERSISTENCE

Do not save:
- current body world position,
- current apparent sky coordinate,
- current zenith factor,
- current orbit phase.

These are derived.

Persist only mutable player/world data such as:
- player alias/name,
- player notes,
- chart observations/evidence,
- any later learned lore,
- stable content version references as required.

World time already determines current body state.

No offline advancement.

---

# 25. VERSION STABILITY

Dynamic celestial definitions must be version-stable.

Changing:
- period,
- phase offset,
- ground track,
- visibility window

can reinterpret every historical observation.

Therefore:
- definitions should have stable IDs,
- released content should not casually mutate orbital parameters,
- save/data migration strategy should exist if definitions ever materially change.

Use the same deterministic/version-awareness philosophy as the static celestial field.

---

# 26. DEBUG / DEVELOPMENT SUPPORT

Since final planet content does not exist yet, provide development tooling.

Recommended debug body:
- obvious temporary canonical name,
- 30-day period,
- visible for 15 days,
- simple authored closed ground track,
- obvious sprite/color,
- one continuous gravity modifier driven by zenith,
- optionally one tint modifier,
- no final lore significance.

Useful debug display:
- body stable ID,
- cycle phase,
- current ground-track point,
- local altitude,
- zenith factor,
- above/below horizon,
- active modifiers,
- active generic celestial conditions.

Debug UI must not mutate player knowledge unless explicitly using real charting.

---

# 27. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## CD.1 — Dynamic body truth
- body definitions,
- period/phase,
- closed ground tracks,
- deterministic time evaluation,
- local altitude/zenith/horizon query,
- debug body.

**STOP for validation.**

## CD.2 — Rendering + chart evidence seam
- dynamic body sky rendering,
- discovery via chart fragment,
- moving-body evidence payload,
- Phase 7 naming compatibility.

**STOP for validation.**

## CD.3 — Modifier + condition API
- continuous modifiers,
- boolean/threshold conditions,
- authored/emergent condition primitives,
- local query service.

**STOP for validation.**

## CD.4 — Authority / save / regression hardening
- host-authoritative consequential queries,
- no orbital-state persistence,
- version stability,
- save/load regression,
- event-system consumption seam.

**STOP for final validation.**

---

# 28. NON-GOALS

Do not implement:
- final planet roster,
- final planet sprites,
- final cultist content,
- final fish events,
- final eclipse quest content,
- final libraries/lore systems,
- automatic orbital prediction UI,
- realistic astrophysics,
- full spherical world,
- offline time progression,
- wrapped-world conversion itself,
- route/piloting changes,
- Cartography Workbench.

---

# 29. ACCEPTANCE TESTS

The foundation is ready when:

1. A debug dynamic body has a stable ID and canonical name.
2. Its position/state is deterministic from definition + world time.
3. Reloading the same world time reproduces the same body state.
4. No body transform persistence is required.
5. The body follows a closed authored ground track.
6. The track can cross the future wrapped-X seam without conceptual breakage.
7. At the local ground-track point, zenith factor approaches 1.
8. Farther away, altitude/zenith decrease.
9. Beyond configured horizon range, body is below horizon.
10. Body rendering follows local truth.
11. Clouds/daylight may hide it visually without changing truth effects.
12. Body can be captured in a legitimate chart fragment.
13. First legitimate chart capture marks it discovered.
14. Once discovered, Phase 7 naming can assign a player alias/note.
15. Canonical name remains available independently of player alias.
16. Repeated observations create repeated legitimate moving-body evidence.
17. The system does not auto-connect observations or infer period.
18. A continuous zenith-driven modifier can be queried.
19. A boolean celestial condition can be queried.
20. Event/consumer code can query conditions at an arbitrary world coordinate.
21. The celestial system does not directly spawn content.
22. One BoatScene can consume one authoritative local celestial modifier state.
23. Current static stars/constellations remain intact.
24. No offline celestial advancement occurs.
25. Save/load preserves player names/notes/evidence but not derived orbit transforms.

---

# 30. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:
1. Exact files modified
2. Exact new files
3. Data assets/definitions added
4. Inspector fields
5. Query APIs
6. Save-schema changes
7. Authority decisions
8. Debug tools
9. Acceptance tests run
10. Any deviation required by current live architecture

Do not expand scope into wrapped-world conversion or final planet content.

---

**End of Celestial Dynamics Foundation handoff.**
