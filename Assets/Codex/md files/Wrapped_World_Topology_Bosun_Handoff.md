# DON'T SINK — Wrapped World Topology Bosun Handoff
## Periodic World X, Finite World Y, Polar Ice Boundaries, and Seam-Safe Global Coordinates

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature family:** World topology / global map foundation  
**Scope:** Foundational world-coordinate pass  
**Purpose:** Replace east/west world-edge assumptions with a horizontally wrapped world while retaining finite north/south extent bounded by physical polar ice sheets.

---

# 0. NON-NEGOTIABLE PROJECT RULES

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before modifying existing code:
1. Open the exact current source.
2. Preserve unrelated serialized state, save compatibility, authority behavior, and current multiplayer hardening.
3. Audit every system that computes global X distance/vector or assumes "leftmost/rightmost".
4. Prefer central topology APIs over scattered `% width` math.
5. Do not confuse global world-map position with BoatScene-local physical X.

---

# 1. TOPOLOGY DECISION

The world is:

- **periodic in X**
- **finite in Y**

Conceptually:

```text
            NORTH POLAR ICE
     ===========================

     ← west/east wrap endlessly →

          ocean / islands
          trenches / towns
          routes / ruins

     ← west/east wrap endlessly →

     ===========================
            SOUTH POLAR ICE
```

The playable world has a finite circumference.

There is no meaningful east or west edge.

Crossing the left boundary returns on the right and vice versa.

North and south remain finite and are bounded by actual world terrain: polar ice sheets.

---

# 2. "INFINITE X" MEANS EDGELESS TRAVERSAL, NOT UNBOUNDED STORAGE

Do not store arbitrarily growing X coordinates forever.

Use a canonical wrapped global X domain, e.g.:

```text
0 <= x < WorldWidth
```

or the current project's equivalent normalized range.

Players may travel east/west indefinitely, but authoritative global coordinates normalize back into the finite circumference.

If an accumulated revolution count is ever needed later for analytics or special mechanics, add it separately. Do not require it for normal geography.

---

# 3. CENTRAL WORLD TOPOLOGY SERVICE

Create/reuse one authoritative topology utility/service.

Conceptually provide:

```text
NormalizeWorldX(x)

WrappedDeltaX(fromX, toX)

WrappedDelta(fromPosition, toPosition)

WrappedDistance(a, b)

NearestEquivalentX(targetX, referenceX)

NearestEquivalentPosition(target, reference)

IsWithinWorldY(y)

ClampOrValidateWorldY(y)
```

Exact names should match project style.

Critical rule:

> Gameplay systems should stop implementing raw global-X subtraction when wrapped distance is intended.

---

# 4. EXAMPLE: WHY RAW X SUBTRACTION BECOMES WRONG

World width:

```text
1000
```

Boat:

```text
x = 990
```

Island:

```text
x = 10
```

Naive difference:

```text
10 - 990 = -980
```

Actual nearest wrapped displacement:

```text
+20
```

Every global system that reasons about proximity, direction, route length, or neighborhood must use the wrapped topology API.

---

# 5. GLOBAL VS LOCAL COORDINATES

This distinction is essential.

## Global world coordinate
Used for:
- world map,
- true world position,
- POIs,
- nodes,
- towns,
- celestial ground tracks,
- fog/knowledge,
- global navigation.

Global X wraps.

## BoatScene local physical coordinate
Used for:
- local water scene,
- player physics,
- boat physics,
- local generated terrain,
- local interaction.

BoatScene X is already conceptual/local travel distance rather than literal global geography.

Do not attempt to make BoatScene Rigidbody coordinates themselves wrap around the whole world.

Instead, global travel/world-position authority updates through the navigation/travel layer.

---

# 6. WORLD Y

World Y remains finite.

Do not wrap Y.

North/south bounds are represented as physical world content rather than arbitrary invisible out-of-bounds barriers.

Primary fiction:
- northern polar ice sheet,
- southern polar ice sheet.

These may later contain:
- pack ice,
- glacial cliffs,
- polar biomes,
- rare resources,
- ruins,
- creatures,
- expedition content.

But the topology rule remains finite Y.

---

# 7. POLAR ICE AS TERRAIN, NOT SPECIAL FAILURE CODE

Prefer:

```text
world approaches north/south limit
        ↓
terrain generation / biome rules transition into permanent ice
        ↓
movement becomes physically blocked / effectively impassable
```

Avoid:

```text
if y > maxY:
    teleport player back
```

or arbitrary "YOU ARE OUT OF BOUNDS" behavior.

The ice sheet is world geography.

Later mechanics may permit limited polar penetration if deliberately designed.

---

# 8. TOPOGRAPHY SEAM CONTINUITY

Current topography/world generation must be audited so X=0 and X=WorldWidth are adjacent.

The seam must not produce:
- discontinuous height cliffs,
- impossible biome jumps,
- mismatched water depth,
- broken contour lines,
- duplicated/severed islands.

Generation/sampling should become periodic in X.

For procedural noise:
- use periodic/wrapped sampling,
- toroidal/noise-domain technique for X continuity,
- or another deterministic seam-safe approach.

Y need not be periodic.

---

# 9. BAKED TOPOGRAPHY

The current baked topo asset / packed height representation should remain usable if possible.

Required changes:
- X edge samples must be treated as neighbors,
- rendering/query sampling near left/right edge must wrap,
- contour/depth classification must not see the seam as a boundary.

Do not duplicate a permanent extra world strip in save data merely to fake wrapping.

---

# 10. WORLD MAP RENDERING

The physical map/table remains finite in representation, but panning across the seam should feel continuous.

Possible presentation:
- canonical map texture repeated left/right,
- nearest wrapped copy chosen relative to viewport center,
- seam crossing handled by duplicated render instances while underlying stable IDs remain singular.

Do not duplicate gameplay entities just because the renderer displays a second wrapped copy.

---

# 11. MAP OBJECT DISPLAY NEAR THE SEAM

A town at x=5 and boat at x=995 should display as nearby across the seam.

Use:

```text
NearestEquivalentPosition(entity, viewport/reference)
```

to choose which visual copy to render.

Potentially render more than one presentation copy when an entity is near a visible seam.

Stable gameplay identity remains one object.

---

# 12. WORLD MAP / STAR CHART SHARED COORDINATES

The Star Chart and World Map share the same 1:1 physical coordinate space.

Therefore the wrap topology applies equally to:
- geographic map,
- star chart,
- celestial dynamic ground tracks,
- believed boat marker,
- future routes,
- chart fragments tied to world coordinates.

Do not let one tab use wrapped coordinates while the other still treats X edges as far apart.

---

# 13. TRUE POSITION

Authoritative true world position should always normalize global X.

Conceptually:

```text
truePosition.x = NormalizeWorldX(truePosition.x)
```

Y remains finite/validated.

Do not let long travel accumulate huge global X values unless intentionally stored separately.

---

# 14. BELIEVED POSITION

The physical blue boat marker / believed-position representation should also understand wrapped X.

A player may place their believed marker near either visual copy of the seam.

Underlying belief coordinate should normalize to canonical world X.

Presentation chooses the nearest wrapped copy.

---

# 15. ROUTE / DISTANCE SEMANTICS

Any global route-length or proximity query must use wrapped distance.

A route across the seam may be the shortest route.

Example:

```text
Town A = x 990
Town B = x 20
WorldWidth = 1000

route X distance = 30
not 970
```

Do not automatically choose shortest route for the player in the future manual-navigation system if that would reveal information they should infer themselves.

But internal systems that need true physical/global proximity must use correct wrapped geometry.

Distinguish:
- truth-distance API,
- player-authored belief route.

---

# 16. EXISTING WORLD GRAPH AUDIT

The legacy graph generator currently has concepts such as:
- leftmost StartDock,
- rightmost Destination,
- cluster spacing across X,
- inter-cluster edges.

Those assumptions are incompatible with an edgeless X topology.

Audit and replace any logic relying on:
- leftmost,
- rightmost,
- "first" western cluster,
- "last" eastern cluster,
- edge-of-world progression.

Do not preserve false topology merely for compatibility.

If start/destination selection remains needed, choose by authored/seeded semantic rules rather than east/west extremes.

---

# 17. NODE / CLUSTER PLACEMENT

Node/cluster placement should treat opposite X edges as adjacent.

Crowding/repair logic must use wrapped distance.

Otherwise two nodes at:
- x=2,
- x=998

would incorrectly be considered far apart.

Apply wrapped geometry to:
- minimum spacing,
- crowding repair,
- neighborhood queries,
- route candidate distances,
- cluster affinity spatial checks.

---

# 18. TOPOGRAPHIC / BIOME QUERIES

Any lookup using X should normalize/wrap.

Examples:
- terrain height,
- water depth,
- biome classification,
- shoreline distance,
- resource pressure location,
- POI suitability,
- deep-water bands.

Seam behavior should be indistinguishable from interior X.

---

# 19. FOG / KNOWLEDGE

Surface shroud / survey shroud / map knowledge must wrap.

Near the seam:
- revealing area at x≈0 may also visually reveal the adjacent wrapped area at x≈WorldWidth,
- not because there are two places, but because they are the same adjacent geography.

Knowledge storage should remain canonical rather than duplicated.

---

# 20. POIS / TOWNS / EVENTS

POI and town stable coordinates normalize X.

Queries for:
- nearby towns,
- event radius,
- treasure distance,
- settlement influence,
- latent POI discovery

must use wrapped displacement.

A town just over the seam is nearby, not a world away.

---

# 21. CELESTIAL DYNAMICS COMPATIBILITY

Dynamic celestial bodies will use authored closed ground tracks over the world map.

Wrapped X is essential so:
- body tracks can cross the seam cleanly,
- repeated observations form coherent closed paths,
- local altitude/zenith calculations use wrapped distance,
- alignments can be queried consistently anywhere in the world.

The topology service should be generic enough for celestial consumers.

---

# 22. SAVE COMPATIBILITY

Existing saves likely contain finite non-wrapped world X values.

Migration should:
- normalize any valid prior X into canonical domain,
- preserve Y,
- preserve stable IDs,
- preserve node/POI associations.

Do not regenerate an existing world merely because X wrapping was introduced unless absolutely unavoidable.

If topography seam continuity cannot be retrofitted to existing generated worlds, document the limitation explicitly rather than silently corrupting geography.

---

# 23. WORLD GENERATION VERSIONING

Because wrap-aware generation may alter:
- topology,
- node spacing,
- biome boundaries,
- seam continuity,

bump/version generation semantics cleanly.

New worlds should use wrapped generation from creation.

Old worlds may require:
- migration,
- compatibility mode,
- or explicit unsupported-version handling if truly necessary.

Do not silently reinterpret seeds under a different topology without versioning.

---

# 24. MAP SEAM ORIGIN

The canonical X seam is a technical coordinate artifact, not a lore boundary.

Do not attach special gameplay meaning to x=0.

Presentation may later allow choosing a different visual map center/origin.

Gameplay identity must not depend on which meridian the renderer chooses as the seam.

---

# 25. TRAVEL ACROSS THE SEAM

Travel systems must treat seam crossing as ordinary travel.

No special:
- teleport event,
- loading fiction,
- "world edge" notification.

From the player's perspective, sailing east long enough simply continues through the world.

Global position normalization happens invisibly at the topology layer.

---

# 26. NAVIGATION / PILOTING BOUNDARY

Do not use this pass to redesign piloting.

This pass only establishes correct global topology.

Future manual navigation will need:
- wrapped true-world geometry internally,
- player-authored route belief separately,
- no automatic route correction.

Do not expose shortest wrapped direction to the player merely because the truth API knows it.

---

# 27. ECONOMY / PRESSURE / EVENT SPATIAL LOGIC

Any spatially local simulation that depends on geographic distance should be audited.

Potential examples:
- market/resource pressure propagation,
- event radius,
- cluster adjacency,
- town influence,
- future faction control,
- weather cells,
- creature migration.

Do not necessarily redesign these systems now if they do not currently rely on raw global X.

But identify every raw global-X distance assumption.

---

# 28. PERFORMANCE

The topology math itself should be trivial.

Prefer:
- pure deterministic functions,
- no allocations in hot loops,
- centralized helpers.

Do not create duplicated gameplay worlds to simulate wrapping.

Only presentation may temporarily render repeated visual copies.

---

# 29. DEBUG TOOLS

Add useful debug support:

- show canonical world X,
- show nearest wrapped equivalent,
- teleport/test across seam,
- draw seam location,
- visualize wrapped neighbor copies,
- report raw vs wrapped delta between selected points,
- seam continuity topography probe.

Debug should make it easy to test x≈0 / x≈WorldWidth cases.

---

# 30. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## WW.1 — Core topology math
- world width source of truth,
- NormalizeX,
- WrappedDeltaX,
- WrappedDistance,
- nearest equivalent helpers,
- unit tests/debug probes.

**STOP for validation.**

## WW.2 — Global position + spatial queries
- true position normalization,
- nodes/POIs/towns,
- distance/proximity queries,
- save migration,
- legacy leftmost/rightmost audit.

**STOP for validation.**

## WW.3 — Topography / biome / generation seam
- periodic X sampling,
- seam-safe height/depth/biome,
- wrapped node crowding,
- generation versioning.

**STOP for validation.**

## WW.4 — Map / Star Chart presentation
- map repeated/wrapped rendering,
- seam-aware markers,
- fog/knowledge,
- believed-position presentation.

**STOP for validation.**

## WW.5 — Regression / downstream audit
- travel,
- market/event spatial logic,
- celestial-dynamics compatibility,
- old-save checks,
- documentation.

**STOP for final validation.**

---

# 31. NORTH/SOUTH ICE IMPLEMENTATION SEAM

The topology pass should establish finite Y limits and expose them cleanly.

Actual final polar content may be a separate biome/content pass.

At minimum provide hooks/parameters such as:
- North Ice Start Y
- South Ice Start Y
- hard world Y extents
- biome override/bias toward permanent ice near limits

Do not rely solely on invisible colliders at the exact edge.

The desired long-term result is a physical polar barrier.

---

# 32. NON-GOALS

Do not implement:
- toroidal Y wrapping,
- spherical-world geometry,
- latitude/longitude UI,
- globe rendering,
- final polar biome art,
- late-game icebreaker mechanics,
- new piloting model,
- full dynamic celestial content,
- route-planning workbench,
- artificial world-edge teleport fiction.

---

# 33. ACCEPTANCE TESTS

The wrapped topology foundation is ready when:

1. Global X has one canonical normalized range.
2. `NormalizeWorldX` behaves correctly for negative and >width values.
3. Wrapped delta chooses nearest displacement.
4. Wrapped distance is correct across the seam.
5. Boat true global position normalizes after east/west crossing.
6. World Y does not wrap.
7. North/south bounds remain finite.
8. A point at x≈0 and point at x≈WorldWidth are treated as neighbors.
9. Node crowding uses wrapped distance.
10. POI/town proximity uses wrapped distance.
11. Global route/travel truth can cross the seam normally.
12. Existing local BoatScene physics coordinates are not incorrectly wrapped.
13. Topography sampling at left/right seam is continuous for new wrapped worlds.
14. Biome/depth queries wrap correctly.
15. World Map can display seam-adjacent geography coherently.
16. Star Chart uses the same wrapped coordinate semantics.
17. Fog/knowledge behaves correctly across seam.
18. Stable gameplay entities are not duplicated by repeated visual copies.
19. Old-save X values migrate/normalize safely where supported.
20. Legacy leftmost/rightmost world-progression assumptions are removed or explicitly replaced.
21. Dynamic celestial ground tracks can consume the topology service.
22. No east/west out-of-bounds gameplay state remains.
23. North/south limits are exposed for future physical polar ice terrain.
24. Save/load preserves normalized positions and stable IDs.

---

# 34. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:
1. Exact files modified
2. Exact new files
3. Topology APIs added
4. Systems audited for raw X subtraction
5. Save/generation version changes
6. Debug tools added
7. Inspector/configuration changes
8. Regression tests
9. Known old-save limitations
10. Any deviations required by current live architecture

Do not expand scope into final polar content or piloting redesign.

---

**End of Wrapped World Topology handoff.**
