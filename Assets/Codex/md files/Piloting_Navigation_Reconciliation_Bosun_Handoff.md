# DON'T SINK — Piloting Navigation Reconciliation Bosun Handoff
## Local Viewscape, Analog Compass, Nearby Land Awareness, Harbor Visibility, and Reconciliation with Modern Navigation Architecture

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Critical piloting/navigation reconciliation pass  
**Goal:** Bring the existing piloting view into alignment with the newer world-position, harbor, cartography, star-navigation, and off-rails travel architecture without turning piloting into GPS/autopilot.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest source currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before changing any existing piloting/navigation class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized fields, save compatibility, and multiplayer hardening.
3. Reconcile with the current live implementation rather than replacing systems blindly.
4. Reuse existing world-position, topography, harbor, weather/visibility, piloting, boat-orientation, and UI seams where practical.
5. Prefer additive support classes/data over invasive rewrites.
6. Stop at each checkpoint, compile, test, summarize changes, and wait for playtest approval.

---

# 1. WHY THIS PASS EXISTS

The 2D side-view BoatScene cannot naturally give the pilot the same situational awareness a real captain would have.

In reality, a pilot can look around and understand things like:

> "There is an island a few miles north."

The existing side-view alone cannot communicate that reliably.

At the same time, the game must **not** solve navigation for the player with:
- a normal minimap,
- GPS,
- exact headings,
- true-position readouts,
- automatic route correction,
- autopilot.

This pass therefore adds two complementary piloting instruments:

1. **Local Viewscape**
   - a circular, boat-centered, top-down approximation of physically visible surroundings.

2. **Analog Compass**
   - a separate adjacent compass that points north as a reference only.

Together they restore the physical information a real pilot would reasonably have without revealing hidden global information.

---

# 2. CORE PILOTING PHILOSOPHY

The piloting station is for **execution and observation**.

It is not the authoritative planning system.

The intended split is:

```text
Mapping Table
= planning / belief / manually recorded routes

Piloting View
= immediate physical observation / steering execution

World Truth
= hidden authoritative simulation
```

The pilot may compare:
- local visible geography,
- the analog north reference,
- manually recorded desired heading/distance,
- star observations,
- harbor approach visuals.

The game does not perform that comparison for them.

---

# 3. LOCAL VIEWSCAPE OVERVIEW

Add a **circular local viewscape inset** to the piloting view.

It is not a normal minimap.

It represents:

> "If I could freely look around from the boat right now, what major nearby physical geography would I be able to perceive?"

The viewscape is centered on the current boat.

---

# 4. BOAT-UP ORIENTATION

The local viewscape is **boat-up**, not north-up.

Rules:

- Boat remains fixed at the center.
- Boat's forward direction always points toward the top of the circle.
- Nearby world geometry rotates smoothly around the boat as the boat turns.
- Nearby geometry translates smoothly as the boat moves.

Conceptually:

```text
             boat forward
                  ▲

         █████
      █████████

                  ▲
                [BOAT]

                         ██
                       ████
```

The visual should prioritize intuitive relative position over mathematically perfect cartographic projection.

---

# 5. LOCAL VIEWSCAPE SCALE

Initial target:

> **Approximately 10 world units circumference**, configurable.

Treat this as provisional playtest tuning.

Expose the viewscape's physical/world-space coverage size in Inspector/config.

Do not hardcode the number into rendering math such that future tuning becomes painful.

Future systems may expand effective visible range based on observer height.

---

# 6. FUTURE HEIGHT-ABOVE-WATER SEAM

Leave a clean seam for future visibility range scaling based on observer height above water.

Example future use:

```text
normal helm height
→ normal viewscape radius

raised bridge / lookout
→ greater visible radius

crow's nest
→ significantly greater visible radius
```

This gives future ship architecture and lookout roles meaningful navigation value.

Do not implement full crow's-nest mechanics in this pass.

The architecture should simply avoid assuming that local view range is permanently fixed.

---

# 7. OCEAN PRESENTATION

Open water inside the viewscape should remain visually simple.

Use:
- subtle wave marks,
- sparkles,
- light ambient ocean motion.

Do not add:
- grid,
- coordinate lines,
- range rings,
- degree markings,
- exact distance labels,
- radar sweep,
- GPS-like symbology.

The viewscape should feel like an abstract visual-surroundings instrument, not radar.

---

# 8. LAND PRESENTATION

Nearby land should render as simple filled silhouettes derived from actual local world geometry.

Show enough coastline shape to communicate:
- island nearby,
- long coastline,
- channel,
- land to port/starboard/ahead/astern.

Do not render detailed miniature terrain.

First-pass land display should **not** need:
- biome textures,
- topographic contours,
- building-level detail,
- map labels,
- node names,
- World Map icons.

Use actual nearby physical coastline truth because this represents direct visual observation.

---

# 9. PHYSICAL VISIBILITY IS NOT MAP KNOWLEDGE

The local viewscape may use true local geometry because it represents what the crew can physically see.

However:

> Seeing land in the piloting viewscape does not modify World Map coverage.

It does not:
- reveal fog/mythic shroud,
- add node markers,
- georegister POIs,
- fix believed position,
- update the map.

This must remain consistent with the Map / Node Discovery design:

> **Eyeballs provide observations. Cartography provides coordinates.**

---

# 10. WEATHER / FOG VISIBILITY

The viewscape must obey physical visibility conditions.

At minimum, effective land visibility should be affected by:
- fog,
- storms,
- bad weather,
- future visibility modifiers.

Fog/bad weather should reduce how far from the boat land can be represented.

The viewscape must **not** provide magical top-down awareness through zero-visibility conditions.

Conceptually:

```text
EffectiveViewRange
= BaseViewRange × EnvironmentalVisibilityModifier
```

Use existing weather/visibility authority if available.

Do not invent an unrelated second weather-visibility simulation if the project already exposes suitable visibility state.

---

# 11. SOFT RANGE EDGE

Do not make geography abruptly pop in/out at a hard radius.

Land near the visibility boundary should fade smoothly.

Desired behavior:
- smooth appearance as land comes into view,
- smooth fade as it leaves visibility,
- no harsh geometry popping,
- no jitter as visibility changes slightly.

Readability and smoothness matter more than perfect optical simulation.

---

# 12. NO FULL LINE-OF-SIGHT OCCLUSION PASS YET

Do not build expensive true horizon/terrain occlusion for the first version.

For v1:
- distance + weather visibility is sufficient,
- local geometry may be rendered if within effective visual range.

Later:
- larger landmasses,
- observer height,
- horizon blocking,
- lookout mechanics

can improve the model if gameplay requires it.

---

# 13. SETTLEMENT / HARBOR VISUALS IN THE VIEWSCAPE

The local viewscape **should show settlements/harbors when physically close enough to be visible**.

Do not use abstract node icons.

Instead, show a simple physical harbor/settlement representation attached to the land silhouette.

Possible first-pass representation:
- small dock/quay silhouette,
- simple harbor structure shape,
- settlement cluster mark integrated into coastline,
- simple harbor landmark.

The exact final art is not part of this pass.

---

# 14. HARBOR DOCKING AREA MUST BE VISIBLE IN THE VIEWSCAPE

This is an important functional requirement.

When the boat is near a harbor:

> **The local viewscape should show the harbor docking/berth area clearly enough for the pilot to understand where the final approach is.**

This directly solves the earlier problem of a huge physical world containing a relatively small docking target.

The pilot should be able to see:
- which side of the island/shore the harbor is on,
- approximate berth/docking-area position,
- the relationship between boat, harbor, and nearby coastline.

The viewscape becomes part of final harbor situational awareness.

---

# 15. HARBOR VIEWSCAPE VS MAIN-VIEW GUIDANCE

Do not replace the existing Harbor Guidance system.

The systems have different jobs:

```text
Local Viewscape
= "The harbor is on that side of the island."

Main BoatScene Harbor Guidance
= "Put the boat into this final approach/berth area."
```

The viewscape may show the physical docking-area footprint or clear harbor-zone representation.

The main-world guidance remains responsible for the close-range approach lane/berth guidance previously designed.

No autopilot.

---

# 16. UNKNOWN HARBORS / MAP KNOWLEDGE

Physical harbor visibility does not require prior World Map knowledge.

If the player physically approaches an unknown settlement:
- land can appear in the viewscape,
- visible harbor structures can appear,
- docking area can appear physically when close enough,
- none of that automatically adds authoritative map knowledge.

This preserves the discovery rules.

---

# 17. ANALOG COMPASS

Add a separate compass adjacent to the local viewscape.

The compass is not integrated into the circular viewscape itself.

It should be visually distinct but placed nearby in the piloting UI.

---

# 18. COMPASS FUNCTION

The compass provides one fundamental piece of information:

> **Which direction is north relative to the boat?**

It behaves like a physical compass.

The player can infer approximate cardinal/intercardinal heading by comparing:
- boat-forward,
- north pointer/card,
- desired route heading.

---

# 19. NO EXACT HEADING DISPLAY

Do **not** display:

```text
HEADING: 035°
```

Do not show:
- numeric heading,
- exact bearing,
- digital compass degrees,
- desired-heading delta,
- "turn 12° right",
- course correction arrows.

The compass must remain an analog reference.

The player performs the mental comparison.

---

# 20. COMPASS PRESENTATION

Recommended first pass:

- fixed compass housing,
- clear N reference,
- rotating compass card or north pointer driven by boat orientation.

Exact visual design can be simple.

The important behavior is smooth, stable analog orientation.

---

# 21. COMPASS SOURCE OF TRUTH

Compass orientation should derive from authoritative boat/world orientation.

Do not derive it from:
- believed map position,
- current route,
- node destination,
- UI transform guesses.

It represents a physical directional instrument.

For this pass, do not implement:
- magnetic declination,
- compass drift,
- calibration errors,
- damage-induced compass error.

Those could become future mechanics if ever useful.

---

# 22. RECONCILE WITH MANUAL NAVIGATION LEGS

The Cartography Workbench design includes future manually recorded navigation-leg values such as:

```text
Heading: 072°
Distance: 86.4 NM
```

When that system is connected to piloting:

> The piloting UI must consume/display the player's recorded values exactly, without solving or correcting them.

The compass exists so the pilot can manually approximate the requested heading.

Do not add:
- auto-course steering,
- exact heading lock,
- heading-error meter,
- auto-leg completion.

---

# 23. OFF-RAILS WORLD TRAVEL

This pass must preserve/reinforce the newer travel model:

> BoatScene X/local travel is not a rigid node-to-node rail.

The boat may:
- wander off route,
- get lost,
- overshoot,
- approach from unexpected directions,
- physically discover unintended land.

Reaching the end of some generated local water strip is not equivalent to reaching a destination.

Destination arrival is based on the actual geographic harbor/approach region.

---

# 24. TRUE POSITION VS BELIEVED POSITION

The local viewscape is based on **true local physical surroundings** because those are observable.

The World Map believed-position block remains separate.

The piloting view must not expose:
- true world coordinates,
- true map marker,
- hidden node IDs,
- exact global location.

Therefore:

```text
true position
→ drives physically visible local surroundings

believed position
→ remains a player-managed Mapping Table object

no automatic reconciliation
```

The pilot can recognize land and still have no idea where that land belongs on the authoritative map.

That is intentional.

---

# 25. NO PASSIVE MAP DISCOVERY FROM VIEWSCAPE

Explicit hard rule:

> Viewscape rendering must never write to cartographic knowledge.

Do not allow:
- local coastline render → World Map reveal,
- harbor visibility → node discovery,
- nearby POI visibility → POI marker,
- local viewscape sample → position fix.

The viewscape is observation only.

---

# 26. VISIBILITY DATA API

Prefer a reusable local physical-visibility query seam rather than hardcoding everything inside the piloting UI.

Conceptually, something like:

```text
LocalVisualEnvironment
- observer world position
- observer orientation
- observer height
- effective visibility range
- nearby land silhouette geometry
- nearby visible harbor geometry
```

Exact names must follow live architecture.

This seam may later serve:
- crow's nest/lookout,
- binoculars,
- NPC lookout roles,
- other observation systems.

---

# 27. HARBOR DATA INTEGRATION

Reuse the Harbor / Node Transition design.

The local viewscape should consume the actual generated harbor truth where appropriate:
- HarborPosition,
- WaterwardDirection,
- berth/docking area,
- visible harbor proxy geometry.

Do not invent a second harbor coordinate system specifically for the piloting inset.

---

# 28. VISUAL SMOOTHING

Prioritize stable presentation.

Use smoothing/interpolation for:
- world rotation,
- land silhouette movement,
- harbor representation,
- visibility fades,
- compass rotation.

Avoid:
- jitter from small physics rotation,
- snapping when crossing angular boundaries,
- one-frame geometry pops.

The viewscape is an instrument. It should feel calm enough to read while the boat is moving.

---

# 29. UI PLACEMENT

The piloting view should contain:

```text
[ main piloting view ]

[ circular local viewscape ]   [ separate analog compass ]
```

Exact screen placement is tunable.

Requirements:
- both visible without obscuring essential piloting controls,
- adjacent enough to compare quickly,
- clearly distinct instruments.

Do not merge the compass into the circular viewscape.

---

# 30. FUTURE LOOKOUT / CROW'S-NEST SEAM

Future possibilities:
- observer height increases viewscape range,
- crew member assigned as lookout improves visibility,
- binoculars improve identification,
- weather resistance improves detection,
- tall masts/crow's nests become useful navigation infrastructure.

Do not implement these now.

Just avoid architecture that would make them difficult.

---

# 31. MULTIPLAYER / AUTHORITY

The underlying boat/world state remains authoritative.

Shared truth:
- boat position,
- boat orientation,
- weather visibility,
- nearby terrain truth,
- harbor geometry.

The local viewscape itself is presentation.

Clients may render locally from authoritative/shared world state.

Do not network every silhouette vertex if deterministic/local reconstruction from shared truth is available.

Compass is local presentation of authoritative boat orientation.

---

# 32. SAVE / PERSISTENCE

No special persistence is required for:
- current viewscape rotation,
- current fade states,
- current compass animation.

These are derived presentation.

Persist only existing authoritative navigation/world state.

---

# 33. DEBUG / TUNING

Expose useful tuning for:

- base viewscape circumference,
- future observer-height multiplier seam,
- weather/fog visibility multiplier,
- land fade-start distance,
- land fade-end distance,
- settlement/harbor visibility threshold,
- harbor docking-area presentation size,
- rotation smoothing,
- translation smoothing,
- compass smoothing,
- ocean sparkle/wave density.

Useful debug overlays:
- effective viewscape world radius/circumference,
- observer position,
- visible-land candidate bounds,
- effective weather visibility,
- harbor/berth geometry,
- raw vs smoothed compass rotation.

---

# 34. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## PILOT.1 — Audit + orientation foundation
- inspect exact current piloting classes
- identify current piloting view architecture
- identify authoritative boat orientation source
- add separate analog compass
- no numeric heading

**STOP FOR TESTING.**

## PILOT.2 — Circular local viewscape
- centered boat-up view
- simple ocean wave/sparkle visuals
- nearby land silhouettes
- smooth world rotation/translation
- configurable ~10 world-unit circumference

**STOP FOR TESTING.**

## PILOT.3 — Visibility integration
- fog/bad-weather range reduction
- soft land-edge fades
- no truth leakage into World Map
- future observer-height seam

**STOP FOR TESTING.**

## PILOT.4 — Harbor integration
- visible settlement/harbor representation
- docking/berth area visible in viewscape
- reuse actual HarborDefinition / harbor truth
- preserve main-world harbor guidance

**STOP FOR TESTING.**

## PILOT.5 — Navigation reconciliation
- verify no exact heading readout
- verify no true-coordinate leak
- verify no auto-course correction
- verify off-rails travel assumptions
- document future active-leg connection seam
- multiplayer/save regression

**STOP FOR FINAL TESTING.**

---

# 35. ACCEPTANCE TESTS

This pass is ready when:

1. Piloting view includes a circular local viewscape.
2. Boat remains centered and points upward.
3. Nearby world geometry rotates around the boat as the boat turns.
4. Nearby geometry translates smoothly as the boat moves.
5. Open water shows only simple ambient wave/sparkle visuals.
6. No grid/range rings/coordinates appear.
7. Nearby land uses simple actual coastline silhouettes.
8. Land visibility is reduced by fog/bad weather.
9. Land fades near the visibility boundary rather than popping.
10. Viewscape coverage size is configurable.
11. Initial target is approximately 10 world units circumference.
12. Architecture leaves a seam for future observer-height/crow's-nest scaling.
13. Nearby physical settlements/harbors can appear in the viewscape.
14. Harbor docking/berth area is visible enough to aid final approach.
15. Viewscape does not replace the main-world Harbor Guidance system.
16. Unknown physical harbors may still appear if actually visible.
17. Seeing them does not modify World Map knowledge.
18. Separate analog compass is adjacent to the viewscape.
19. Compass points north relative to boat orientation.
20. Compass movement is smooth/stable.
21. No exact numeric heading is shown.
22. No heading-error/delta readout is shown.
23. No true world coordinate is shown.
24. No true map-position marker is shown.
25. No autopilot/course correction is introduced.
26. No automatic active-leg completion is introduced.
27. Viewscape uses true local physical surroundings only for observation.
28. Believed-position marker remains a separate Mapping Table object.
29. Local observation never writes to cartographic coverage.
30. Off-rails travel remains intact.
31. Harbor arrival still depends on actual harbor/approach geometry.
32. Existing piloting controls remain functional.
33. Multiplayer clients see consistent nearby-world orientation.
34. Existing save/load behavior remains intact.

---

# 36. EXPLICIT NON-GOALS

Do not implement:
- GPS,
- normal north-up minimap,
- exact heading readout,
- digital compass degrees,
- route auto-correction,
- autopilot,
- heading lock,
- automatic route-leg completion,
- true-position display,
- passive World Map reveal,
- node discovery from viewscape,
- full horizon occlusion,
- crow's nest mechanics,
- lookout crew role,
- binocular mechanics,
- magnetic declination,
- compass damage/drift,
- final viewscape art,
- full piloting minigame redesign unless required to integrate this pass safely.

---

# 37. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:

1. Exact files modified
2. Exact new files
3. Existing piloting/navigation classes inspected
4. Boat orientation source used
5. Local-world geometry source used
6. Viewscape coordinate transform
7. Visibility/weather source used
8. Harbor data source used
9. Compass implementation
10. Smoothing/interpolation approach
11. UI layout changes
12. Multiplayer implications
13. Save/load implications
14. Debug/tuning controls added
15. Regression tests performed
16. Any live-project discrepancy from this handoff

Do not silently turn the viewscape into a conventional minimap.

---

**End of Piloting Navigation Reconciliation handoff.**
