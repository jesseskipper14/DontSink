# DON'T SINK — Deterministic Node / Settlement Generation
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Full NodeScene settlement-generation foundation  
**Primary goal:** Every node becomes a deterministic, recognizable settlement whose permanent physical identity survives for the life of the save while population, prosperity, trade, food, security, stability, events, damage, abandonment, and recovery visibly change what the player sees on later visits.

---

# 0. Non-negotiable engineering rule

## Exact-current-class rule

If an existing class must be modified, inspect the exact latest source currently in the project first. Never reconstruct an existing class from memory, an old handoff, or assumptions.

Before touching an existing class:
1. Open the live source.
2. Preserve unrelated behavior and serialized fields.
3. Preserve save compatibility, interaction behavior, scene transitions, and multiplayer hardening.
4. Reuse existing systems where practical.
5. Prefer additive support classes/data over invasive rewrites.
6. Stop after each implementation checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core design pillar

The settlement is built from two layers:

```text
PERMANENT SETTLEMENT IDENTITY
+
CURRENT NODE CONDITION
=
CURRENT VISIT PRESENTATION
```

The player should recognize the same place across time.

Permanent:
- harbor relationship,
- terraces,
- paths/walkways,
- permanent plots,
- semantic plot roles,
- building families,
- base color choices,
- roof families,
- stack relationships,
- required service locations,
- reserved quest/event spaces.

Mutable:
- population,
- prosperity,
- trade activity,
- food availability,
- security,
- stability,
- active events/buffs/outcomes,
- occupancy,
- condition,
- activity,
- dressing,
- abandonment,
- recovery.

Target reaction:

> “I remember this city. This place went to shit.”

Not merely:

> “This is a poor-town prefab.”

---

# 2. Generate once, then persist

The initial node seed/archetype may generate the settlement skeleton, but the permanent layout must then be saved.

Do **not** depend on regenerating from seed forever.

Reason:
- generator algorithms will change,
- old saves must not rearrange towns,
- permanent town identity matters,
- development history needs stable lot IDs.

Persist a compact layout manifest rather than instantiated scene GameObjects.

Conceptually:

```text
NodeSettlementLayoutManifest
- GenerationVersion
- NodeStableId
- ArchetypeId
- VisualPopulationCapacity
- Terrace definitions
- Traversal connections
- Permanent plot records
- Semantic roles
- Building family assignments
- Base palettes
- Roof assignments
- Stack relationships
- Required station assignments
- Reserved quest/event sockets
```

---

# 3. Archetype defines urban form

Archetype strongly determines the permanent physical tendencies. Seed creates variation inside those tendencies.

### Farming Atoll
- broad horizontal spread,
- low density,
- 1–2 terraces,
- little stacking,
- many open/non-building plots.

### Fishing Hamlet
- sparse waterfront settlement,
- low verticality,
- 1–2 terraces,
- detached buildings,
- fishing/work plots,
- generous gaps.

### Shipyard
- dense waterfront,
- industrial/storage-heavy,
- compact working areas,
- moderate/high stacking,
- larger workshop/warehouse bodies.

### Fortress Island
- dense,
- strongly vertical,
- multiple terraces,
- frequent stacked structures,
- civic/defensive roles,
- deliberate chokepoints while remaining fully traversable.

Other archetypes can be added through data.

Archetype should define ranges/tendencies, not exact layouts.

---

# 4. Maximum visual settlement capacity

Every node has a fixed maximum visual capacity/footprint.

Example:

```text
VisualPopulationCapacity = 1000
```

Presentation might behave approximately as:

```text
Population 100   → sparse
Population 500   → moderately occupied
Population 1000  → visually full
Population 10000 → still visually full
```

The real population can continue beyond the visual ceiling.

This is a presentation cap, not necessarily literal housing capacity.

---

# 5. Population is explicit persistent state

Population should be a real persistent simulation value, distinct from prosperity/trade/security/etc.

Conceptually:

```text
Population = 742
```

not merely:

```text
PopulationRating = 74
```

If the live node model lacks this, add it carefully after inspecting current save/state architecture.

Presentation may normalize it:

```text
VisualPopulationFactor =
Clamp01(CurrentPopulation / VisualPopulationCapacity)
```

Exact tuning is not locked.

---

# 6. Stats drive independent visual channels

Do not map the whole town through one global quality tier.

Different node stats influence different parts of presentation.

Use four broad conceptual channels:

```text
Occupancy
Condition
Activity
Dressing
```

### Occupancy
Often driven by:
- Population,
- development history,
- semantic role.

Controls:
- occupied/vacant,
- civilian density,
- residence use,
- active upper levels.

### Condition
Often driven by:
- Prosperity,
- Stability,
- persistent damage overrides.

Controls:
- maintained,
- worn,
- abandoned,
- ruined,
- repaired.

### Activity
Often driven by:
- Trade,
- Food Balance,
- Population,
- Security,
- semantic role.

Controls:
- merchants working,
- warehouse operation,
- market traffic,
- industrial use.

### Dressing
Often driven by:
- Prosperity,
- archetype,
- security,
- trade,
- events.

Controls:
- signs,
- lights,
- merchandise,
- clutter,
- tools,
- defensive props,
- decoration.

Individual building families decide which stats they care about.

---

# 7. Permanent plot roles

Every plot gets a permanent semantic role during initial generation.

Examples:
- Residential,
- Market,
- Commercial,
- Industrial,
- Warehouse/Storage,
- Civic/Service,
- Surveyor Station,
- future Harbormaster,
- future Tavern,
- Defensive,
- Work Plot,
- Non-Building/Open Plot,
- Authored Special Building,
- Quest/Event Reserve.

A residence does not later transform into an industrial supplier.

If an industrial supplier may exist in that node, it owns an industrial/special plot from the beginning.

---

# 8. Unlockable buildings already own their place

Late-game services should not materialize by replacing unrelated buildings.

Example:

```text
IndustrialSupplierPlot
```

Before activation:
- undeveloped foundation,
- inactive shell,
- shuttered workshop,
- abandoned industrial building.

After thresholds:
- same plot,
- same role,
- functioning supplier.

Archetype determines which special roles are eligible at all.

A tiny fishing hamlet does not need every heavy-industry service hidden inside it waiting for prosperity 80.

---

# 9. Permanent building family

Each building plot gets a permanent family.

Example:

```text
ResidentialFamily_A
IndustrialFamily_B
WarehouseFamily_C
```

Current state chooses a presentation inside that family.

The same lot should remain recognizably the same building over time.

---

# 10. Do not arbitrarily stretch sprites

Ordinary building sprites/prefabs retain authored proportions.

The layout generator fits buildings to plots.

Support authored body sizes such as:
- narrow,
- standard,
- wide,
- warehouse/civic.

A future building family using a deliberate tileable/procedural shader may expose variable width explicitly.

No general transform-stretching to make sprites fit.

---

# 11. Building body + modular roof grammar

Buildings are rectangular/square structural bodies with a flat structural top and one modular RoofSlot.

Conceptually:

```text
BuildingBody
- structural body
- entrance / interaction points
- NPC/activity sockets
- dressing sockets
- RoofSlot
```

RoofSlot states:

```text
Empty / exposed flat roof
RoofCap
UpperBuildingSupport
```

This modularity is a major art multiplier.

---

# 12. RoofCap

Roof caps are independent authored visual pieces.

They may vary by:
- archetype,
- roof family,
- region/style,
- prosperity presentation,
- color,
- shader/material.

Examples:
- patched metal,
- timber,
- tile,
- parapet,
- canvas,
- industrial roof.

Common building bodies can therefore read very differently between nodes.

---

# 13. UpperBuildingSupport owns access

If a RoofSlot becomes `UpperBuildingSupport`, that support owns:

- support/platform visuals,
- walkable roof/platform surface,
- ladder or authored stairs,
- WorldLedge or project-standard ledge support,
- landing clearance,
- next-building attachment point.

Important rule:

> The support owns access. The upper building does not.

This allows the roof/platform to remain walkable even with no upper building present.

It also means stacked buildings are reachable by construction.

---

# 14. Vertical stacking

Permanent layouts may contain stacked building chains.

Example:

```text
Ground body
→ support
→ upper body
→ support
→ upper body
→ support
→ top body
```

Low-density archetypes rarely stack.

Dense archetypes may stack heavily.

High-end settlements should support approximately **4–5 building bodies high** where authored content allows.

The highest cities may extend off the top of the initial screen. That is desirable. They should feel vertical.

The existing NodeScene camera already follows the player and should not be changed unless live inspection finds an actual problem.

---

# 15. Growth bottom-up

Development should generally fill lower levels before higher levels.

Example:

```text
Low development:
A active
B undeveloped
C undeveloped

Growth:
A active
B active
C undeveloped

Dense:
A active
B active
C active
```

Exact thresholds remain tunable.

---

# 16. Decline leaves history

Once a structure has actually existed, decline should not erase it.

States conceptually include:

```text
Never developed
→ empty / undeveloped

Developed + healthy
→ active

Developed + population/economic collapse
→ vacant / abandoned

Explicit severe damage
→ ruined / destroyed
```

This creates the visual distinction between:

> “This place was always small.”

and:

> “This used to be a major city.”

Persist lightweight development history per plot/stack level.

Current population can fall. Historical development remains.

---

# 17. Persistent structural overrides

Ordinary decay/recovery can be derived from current stats at load.

Explicit events need persistent overrides.

Examples:
- warehouse destroyed in raid,
- building burned,
- quest demolishes structure,
- condemned building,
- quest occupation,
- explicit rebuild.

Do not let prosperity automatically resurrect a building that was explicitly destroyed.

Keep:

```text
Derived decay/recovery
```

separate from:

```text
Persistent structural state
```

---

# 18. Color variation

Base building color/palette is part of permanent identity.

A building family may expose several palette variants.

Example:
- seven body colors,
- multiple roof colors.

A lot's chosen base colors remain stable across visits.

Mutable condition treatment may make the same color:
- cleaner,
- faded,
- dirty,
- rusty,
- soot-stained,
- desaturated.

A blue house should become a neglected blue house, not randomly an orange house.

---

# 19. Shader/material variation

Bodies and roofs may expose multiple materials/shaders.

Possible uses:
- rust,
- grime,
- fading,
- wetness,
- soot,
- damage masks,
- different roof materials.

Architecture should intentionally multiply a small art library.

Even a modest set such as:

```text
8 body sprites
× 7 colors
× several roofs
× roof colors
× shader treatments
× dressing
× stacking
```

can create hundreds of distinct combinations.

---

# 20. Terraces are the traversal backbone

Settlement terrain uses flat terraces, not random Y jitter.

Each terrace is a reliable horizontal traversal band containing:
- walkable surface,
- building attachment area,
- non-building plots,
- NPC/activity space,
- connections to nearby terraces.

Initial starting ranges:

```text
Fishing Hamlet     → 1–2
Farming Atoll      → 1–2
General Town       → 2–3
Dense trade city   → 3–4
Fortress           → 3–5
```

These are tuning defaults, not sacred constants.

---

# 21. Harbor anchors the settlement

Settlement generation begins from the existing harbor grammar.

Reuse:
- HarborPosition,
- WaterwardDirection,
- landward direction,
- raised town datum,
- quay/dock,
- dredged berth.

Conceptually:

```text
Harbor / quay
↓
Waterfront band
↓
Terrace 1
↓
Terrace 2
↓
Terrace 3
↓
inland extent
```

Archetype decides whether the node mostly:
- spreads laterally,
- climbs upward,
- stacks vertically,
- or combines all three.

---

# 22. Terraces may overlap horizontally

Higher terraces may overlap lower terraces in X.

Do not force the settlement into a neat pyramid.

This is important for dense cities.

Early implementation only needs deterministic sorting/layering. No fake depth/parallax illusion is required.

---

# 23. Walkable street/path strip

Buildings generally sit slightly behind/up from a narrow walkable path.

Only modest visual separation is required.

Conceptually:

```text
BUILDING  BUILDING
████████  ████████

 NPC / props
────────────────── walkable terrace
```

The player should clearly read:
- facade,
- walkable path,
- props/NPC zone.

---

# 24. Permanent paths, mutable appearance

Traversal paths are permanent.

Their appearance may respond to archetype and condition.

Examples:

Poor:
- rough boards,
- mud,
- scrap steps,
- worn ladders.

Prosperous:
- maintained timber,
- stone/paving,
- railings,
- lights.

Same route. Different condition.

---

# 25. Terrace connections

Every adjacent terrace pair must have at least one valid connection.

Larger/dense settlements may have several.

Connection styles may include:
- stairs,
- steps,
- ladders,
- authored support ladders,
- future ramps.

Archetype can weight them.

---

# 26. Accessibility is king

Hard rule:

> A visually valid but unreachable town is invalid.

Build/validate an explicit traversal graph.

Required reachable destinations include:
- player arrival from harbor,
- Surveyor,
- market/service area,
- required semantic buildings,
- active special services,
- relevant upper structures,
- required quest/event access points.

If invalid, repair deterministically.

Suggested repair order:
1. add/fix terrace connection,
2. fix/remove invalid stack,
3. shift/compact conflicting plot,
4. simplify non-required content,
5. fall back to a simpler valid layout,
6. last resort safe minimal settlement.

---

# 27. Non-building plots are first-class

Permanent plots may intentionally contain no building.

Examples:
- open space,
- gardens,
- vegetation,
- fishing equipment,
- barrels/crates,
- work yards,
- statues,
- future livestock,
- event space.

Sparse archetypes use more of these.

Dense archetypes use fewer.

They may also own authored sockets and react to node state.

---

# 28. Archetype controls role composition

Archetype data should define:
- required roles,
- allowed roles,
- weighted roles,
- density tendency,
- open-space tendency,
- stack tendency,
- terrace tendency,
- max stack depth,
- style palettes,
- eligible special services.

Example Fishing Hamlet:
- required Surveyor,
- small market,
- housing,
- fishing/work plots,
- many open spaces,
- little heavy industry.

Example Shipyard:
- required Surveyor,
- market,
- shipyard/workshop,
- warehouses,
- dense waterfront activity,
- worker housing.

---

# 29. Required semantic content

At minimum, initial generation must guarantee valid space for:
- harbor/quay/dock,
- Surveyor Station,
- market/trade area,
- traversable settlement network.

Future required roles may include:
- Harbormaster,
- Tavern,
- Node Intelligence service,
- faction/service buildings.

Required content wins over decorative content.

---

# 30. Surveyor Station integration

The Surveyor mini-pass established that the Surveyor spawns with a dynamically placed station.

This generator should become the authoritative station-placement system.

Requirements:
- exactly one Surveyor Station per node,
- permanent plot assignment,
- persisted placement,
- Surveyor spawns relative to station-authored point,
- no hardcoded Surveyor world coordinate.

Preserve Surveyor service behavior.

---

# 31. Buildings populate themselves

Architectural rule:

> The Node Generator places the building. The building/plot populates its own internal authored sockets.

The Node Generator should not know market prop counts.

Instead, it provides a presentation context such as:

```text
Population
Prosperity
Stability
Security
Trade
FoodBalance
Active buffs/events/outcomes
```

Individual building/plot definitions decide what to show.

---

# 32. Stable socket IDs / stable fill order

Authored sockets need stable IDs or deterministic ordering.

Example market:

```text
20 merchandise sockets
8 civilian sockets
5 merchant sockets
12 clutter sockets
4 lighting sockets
3 damage sockets
```

Growth should behave like:

```text
6 displays
→ 9
→ 13
→ 17
```

The original six remain in place. New sockets activate.

Do not reshuffle the whole building every visit.

Sockets may have authored conditions/tags such as:
- MinimumPopulation,
- MinimumProsperity,
- MinimumTrade,
- MinimumSecurity,
- SocketType,
- AllowedContentTags,
- FillPriority.

Use whatever data model best matches live architecture.

---

# 33. Different building types consume different stats

Examples:

### Residence
Primarily:
- Population,
- Prosperity,
- Stability.

### Market
Primarily:
- Trade,
- Population,
- Food Balance,
- Prosperity.

### Warehouse
Primarily:
- Trade,
- Prosperity,
- Stability.

### Defensive building
Primarily:
- Security,
- Population,
- Stability.

### Surveyor Station
Mostly protected from normal availability logic because Surveyor presence is mandatory for now.

Keep this data-driven.

---

# 34. Causality should be visible

Different failures should look different.

Food crisis:
- empty food displays,
- fewer civilians,
- shuttered food activity,
- abandoned residences.

Trade collapse:
- idle warehouses,
- fewer merchants/workers,
- sparse market displays.

Security collapse:
- disorder,
- defensive/barricade visuals later,
- fewer ordinary civilians.

Do not make every negative state use the same generic “poor” presentation.

---

# 35. Structural presentation updates once on NodeScene load

Hard rule:

> Structural settlement presentation is evaluated once when that node loads.

At load:

```text
Permanent Layout Manifest
+
Current NodeState
+
Development History
+
Persistent Overrides
+
Active Events/Buffs
=
Current Visit Presentation Snapshot
```

During the visit:
- buildings do not live-morph tiers,
- roofs do not disappear,
- structures do not rearrange,
- structural condition stays stable.

On the next visit/load:
- rebuild presentation from the latest node state.

This is where the player sees consequences.

---

# 36. Automatic development from simulation

The player does not manually upgrade towns.

When NodeScene loads:
- evaluate eligible undeveloped permanent potential,
- if thresholds are crossed, mark development as historically achieved,
- persist it,
- render current condition.

The world simulation writes settlement history.

---

# 37. Hysteresis

Avoid threshold flicker.

Example:

```text
Activate at Prosperity >= 60
Remain active until Prosperity < 50
```

Use authored hysteresis where useful for:
- services,
- occupancy tiers,
- market activity,
- repair tiers,
- development.

Do not make every 0.1-point stat fluctuation rebuild civilization.

---

# 38. Most buildings have no interiors

Default generated buildings are exterior shells/façades with:
- doors,
- service interactions,
- vendor points,
- NPC sockets,
- quest hooks.

No procedural interiors.

Very important buildings may use authored special prefabs with:
- walls,
- ceiling,
- door openings,
- bespoke internal traversal,
- bespoke collision.

Node Generation merely places them.

---

# 39. NPC categories

### Persistent semantic NPCs
Examples:
- Surveyor,
- future named merchants,
- Harbormaster,
- quest NPCs,
- important service NPCs.

Need:
- stable identity,
- stable semantic station/socket,
- persistent state as needed.

### Ambient NPCs
Examples:
- civilians,
- shoppers,
- dockworkers,
- laborers.

Need:
- visit-level presentation,
- count/density from current node state,
- no individual long-term save unless gameplay promotes one.

---

# 40. Ambient NPCs spawn from authored sockets

Do not scatter humans at arbitrary physics-valid coordinates.

Use:
- CivilianSocket,
- WorkerSocket,
- MerchantSocket,
- GuardSocket,
- IdleSocket,
- etc.

Node stats activate deterministic subsets.

Later AI may move them.

---

# 41. Stats affect NPC categories differently

Population:
- overall civilian density.

Trade:
- merchants,
- dockworkers,
- warehouse workers.

Security:
- guards/patrol-type presence.

Food Balance:
- food-market activity.

Stability:
- ordinary civilian presence vs abandonment/disorder.

This allows:
- dense but commercially dead towns,
- wealthy but lightly populated towns,
- poor but militarized towns,
- starving and depopulated towns.

---

# 42. Ambient NPC persistence

Ambient NPCs are regenerated each visit from:
- current snapshot,
- stable sockets,
- deterministic selection.

Do not serialize hundreds of meaningless civilians.

Persistent semantic NPCs remain persistent.

---

# 43. Quest/event space seams

Node Generation provides semantic space.

Quest/event systems consume it.

Expose anchors/tags such as:
- Harbor,
- Market,
- TownSquare,
- Residential,
- Industrial,
- Warehouse,
- ExteriorEvent,
- AuthoredInterior,
- NPCSpawn.

Later content can request compatible spaces.

Do not let quests arbitrarily place buildings anywhere.

---

# 44. Harbor integration

Preserve the NodeScene Harbor Mooring / Dock Rework design:
- raised land datum,
- solid quay,
- shortened dock,
- dredged berth,
- automatic mooring area,
- harbor no-spawn region,
- future mirroring compatibility.

The settlement grows from that harbor grammar.

Do not build a parallel harbor system.

---

# 45. Seafloor seam only

Do not perform the future full seafloor resource/POI generation pass here.

Only preserve/provide:
- dredged berth,
- harbor no-spawn region,
- valid shore/town geometry,
- useful metadata for the later pass.

---

# 46. Sorting/layering

Dense stacks + horizontally overlapping terraces require deterministic sorting.

Do not solve this with arbitrary magic numbers per instance.

Prefer a stable scheme based on:
- terrace,
- structural sublayer,
- existing project sorting rules.

Must remain compatible with:
- player sorting,
- WorldLedge,
- roof/support traversal,
- current NodeScene presentation.

---

# 47. Multiplayer / authority

Permanent settlement layout and consequential history are authoritative shared state.

Authority owns:
- initial layout generation,
- persisted manifest,
- development history,
- persistent structural overrides,
- semantic NPC identity/state,
- consequential service activation.

Clients may reconstruct decorative presentation from shared state.

Prevent:
- divergent layouts,
- duplicate Surveyors,
- duplicate special stations,
- inconsistent destroyed/rebuilt states.

---

# 48. Save/load requirements

Persist the equivalent of:
- layout manifest,
- generation version,
- permanent plot IDs,
- semantic roles,
- building families,
- base styles/colors,
- roof/stack relationships,
- development history,
- persistent overrides,
- semantic NPC state where needed.

Do not necessarily persist:
- every ambient NPC,
- animation state,
- transient shader interpolation.

Load flow:

```text
Load manifest
→ load current NodeState
→ load development/overrides
→ build current visit snapshot
→ instantiate NodeScene presentation
```

---

# 49. Debug / authoring tools

Recommended:
- force-generate selected node settlement,
- visualize terraces,
- visualize permanent plots,
- show semantic roles,
- show stack links,
- show RoofSlots,
- show traversal graph,
- highlight unreachable required content,
- show NPC sockets,
- show quest/event anchors,
- preview arbitrary node stats,
- preview poor/modest/rich conditions,
- simulate population collapse/recovery,
- force development history,
- force persistent damage override,
- display hysteresis/threshold state.

Any destructive “regenerate saved settlement” debug action should be clearly labeled.

---

# 50. Recommended data-driven definitions

Prefer ScriptableObject definitions or equivalent current-project assets for:

### NodeSettlementArchetypeDefinition
Possible data:
- terrace count range,
- horizontal spread,
- verticality,
- lot density,
- open-space frequency,
- max stack depth,
- required roles,
- allowed/weighted roles,
- allowed building families,
- roof palettes,
- color/material palettes,
- traversal preferences,
- visual population capacity range.

### BuildingFamilyDefinition
Possible data:
- compatible semantic roles,
- body prefabs,
- authored dimensions,
- roof compatibility,
- color palettes,
- material/shader variants,
- internal sockets,
- condition rules,
- development thresholds,
- service hooks.

### RoofStyleDefinition
Possible data:
- compatible body/support widths,
- sprite/prefab,
- style tags,
- color palette,
- shader/material.

### Persistent Plot Record
Possible data:
- StablePlotId,
- terrace,
- position,
- semantic role,
- family,
- base palette,
- stack parent/child,
- development history,
- persistent override.

Exact class names are not prescribed.

---

# 51. Implementation checkpoints

## NODEGEN.1 — Audit + data foundation
- inspect exact live node-state/NodeScene/generator/save classes,
- inspect harbor implementation,
- inspect Surveyor implementation,
- define persistent layout manifest,
- add generation version seam,
- define archetype settlement parameters,
- add explicit Population only if truly absent.

**STOP FOR COMPILE / SAVE TEST / REVIEW.**

## NODEGEN.2 — Harbor-anchored terraces + traversal
- generate deterministic flat terraces,
- permanent path bands,
- vertical connections,
- traversal graph,
- accessibility validation/repair,
- persist manifest.

**STOP FOR PLAYTEST.**

## NODEGEN.3 — Permanent plots + semantic roles
- building and non-building plots,
- archetype-driven density/open space,
- permanent role assignment,
- required Surveyor/market slots,
- special/unlockable reserved roles.

**STOP FOR PLAYTEST.**

## NODEGEN.4 — Bodies / roofs / stacking
- authored body-family placement,
- no arbitrary stretching,
- RoofSlot,
- RoofCap,
- UpperBuildingSupport,
- ladder/WorldLedge access,
- permanent stack relationships,
- 1–5 level archetype-driven verticality.

**STOP FOR PLAYTEST.**

## NODEGEN.5 — Style multiplication
- base colors,
- roof colors/types,
- shader/material seams,
- archetype palettes,
- deterministic stable identity,
- sorting/layering.

**STOP FOR PLAYTEST.**

## NODEGEN.6 — Dynamic visit presentation
- Occupancy / Condition / Activity / Dressing,
- evaluate once on NodeScene load,
- populate internal sockets,
- stable fill order,
- visual population saturation,
- no structural live morphing.

**STOP FOR PLAYTEST.**

## NODEGEN.7 — Development history + abandonment
- threshold-based development,
- persist historical development,
- bottom-up growth,
- abandonment on decline,
- hysteresis,
- reoccupation/recovery,
- persistent damage override seam.

**STOP FOR PLAYTEST.**

## NODEGEN.8 — NPC population
- semantic NPC sockets,
- generated Surveyor Station becomes placement authority,
- ambient authored sockets,
- stat-driven NPC categories,
- no ambient save bloat.

**STOP FOR PLAYTEST.**

## NODEGEN.9 — Quest/event/service seams + hardening
- semantic quest/event anchors,
- special service activation seam,
- persistent destruction/rebuild,
- multiplayer authority review,
- save/version review,
- debug tools,
- regression pass.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 52. Minimum playtest scenarios

### Sparse farming/fishing node
- 1–2 terraces,
- broad horizontal spread,
- many open plots,
- little stacking.

### Dense shipyard/fortress
- multiple terraces,
- overlapping X ranges,
- dense permanent plot layout,
- 4–5-high stack potential,
- all upper areas reachable.

### Poor high-population node
- crowded,
- shabby,
- active occupancy,
- low-quality dressing.

### Rich low-population node
- well maintained,
- sparse NPC count,
- clean/rich presentation.

### Food crisis
- food visuals collapse,
- population drops,
- abandoned residences/upper development appear.

### Trade collapse
- market/warehouse activity drops,
- merchant/worker presence falls.

### Recovery
- abandoned historical structures remain recognizable,
- occupancy/activity returns,
- ordinary condition improves,
- explicitly destroyed structures remain destroyed unless repaired.

---

# 53. Acceptance tests

This pass is complete when:

1. Each node has a persisted permanent settlement skeleton.
2. Old saves do not depend on rerunning the generator.
3. Archetype strongly changes spread/density/terraces/verticality.
4. Harbor anchors the settlement.
5. Terraces are flat and traversable.
6. Adjacent terraces have valid access.
7. Required destinations are reachable.
8. Plot roles are permanent.
9. Special services own permanent eligible plots.
10. Archetypes can forbid inappropriate roles.
11. Building families remain stable.
12. Ordinary sprites are not arbitrarily stretched.
13. Modular roofs work.
14. UpperBuildingSupport provides walkable access even without an upper building.
15. Dense nodes can stack ~4–5 buildings high.
16. Horizontally overlapping terraces layer correctly.
17. Non-building plots exist and react to state.
18. Surveyor Station is generated as part of the permanent settlement.
19. Buildings/plots populate authored sockets.
20. Socket activation is stable/deterministic.
21. Population is explicit persistent state.
22. Visual population saturates at a configurable capacity.
23. Different node stats drive different visual channels.
24. Structural presentation refreshes once on NodeScene load.
25. Structural presentation does not live-morph during a visit.
26. Growth can develop permanent potential.
27. Development history persists.
28. Decline leaves abandoned historical structures.
29. Recovery can reoccupy ordinary abandoned structures.
30. Persistent destruction does not auto-repair from prosperity alone.
31. Base colors/style remain recognizable.
32. Mutable shader/material treatment can show decay/recovery.
33. Ambient NPCs spawn from authored sockets.
34. Ambient NPCs do not require individual permanent saves.
35. Semantic NPCs retain stable identity.
36. Quest/event systems can request semantic spaces.
37. Multiplayer clients cannot diverge on layout/history.
38. Existing harbor, Surveyor, market, interaction, save, and scene-transition systems remain intact.

---

# 54. Explicit non-goals

Do not turn this pass into:
- procedural building interiors,
- full NPC dialogue/personality,
- final NPC schedules,
- final city art,
- full seafloor generation,
- underwater POI generation,
- dynamic harbor prosperity/depth,
- cranes/cargo labor,
- player city-building,
- arbitrary building-role swapping,
- infinite vertical generation,
- procedural freeform architecture,
- continuous structural morphing while the player stands in town.

---

# 55. Final architecture summary

```text
NODE ARCHETYPE
defines settlement tendencies
        ↓

INITIAL GENERATION
creates a permanent harbor-anchored settlement skeleton
        ↓

PERSISTED MANIFEST
freezes town identity for this save
        ↓

CURRENT NODE STATE
population / prosperity / trade / food / security / stability / events
        ↓

NODE LOAD SNAPSHOT
derives occupancy / condition / activity / dressing
        ↓

PLAYER SEES HISTORY
growth, prosperity, abandonment, decay, recovery
without the town losing its identity
```

Building grammar:

```text
Permanent Plot
  ↓
Permanent Building Family
  ↓
Stable body style/color
  ↓
RoofSlot
  ├─ Empty
  ├─ RoofCap
  └─ UpperBuildingSupport
         ↓
      next building
```

The intended result is a relatively small authored art library producing a huge number of coherent settlements that remain recognizable while visibly carrying the consequences of the simulation.

---

# 56. Bosun delivery expectations

After every checkpoint, report:
- exact files modified,
- exact new files,
- existing classes inspected first,
- save/schema changes,
- Population changes,
- layout-manifest design,
- archetype data,
- terrace/traversal approach,
- accessibility repair,
- plot-role assignment,
- building/roof/stack implementation,
- color/shader system,
- dynamic load-snapshot evaluation,
- development-history persistence,
- persistent structural overrides,
- NPC sockets,
- Surveyor integration,
- quest/event seams,
- multiplayer authority,
- debug tools,
- regression tests,
- any discrepancy between this handoff and live project architecture.

Do not silently expand scope.

---

**End of Node / Settlement Generation handoff.**
