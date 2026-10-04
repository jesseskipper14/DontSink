# DON'T SINK — Map / Node Discovery Bosun Handoff
## Shared Cartographic Knowledge, Surveyors, Surface Surveying, Seafloor Knowledge, POI Discovery, and Mythic Uncharted Presentation

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature family:** World map / cartography / discovery / navigation  
**Scope:** Foundational discovery architecture  
**Status:** Design-vetted, ready for implementation planning  

---

# 0. NON-NEGOTIABLE ENGINEERING RULE

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, an old handoff, or assumptions.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized fields, save compatibility, authority hooks, and current UI behavior.
3. Reuse current map, node, topography, player-map-state, travel, persistence, telescope, and mapping-table systems where practical.
4. Prefer additive support classes/data over invasive rewrites.
5. Do not rebuild Mapping Table / Cartography Workbench mechanics already specified elsewhere.
6. Do not silently reintroduce the old `Unknown / Rumored / Partial / Known` geography state model.

---

# 1. CORE DISCOVERY PHILOSOPHY

The world always exists in full deterministic truth.

The player does **not** gain geographic map knowledge merely by:
- sailing through an area,
- seeing an island,
- seeing a settlement,
- docking,
- seeing a surface POI,
- finding a wreck,
- diving somewhere,
- physically standing at a node.

The governing rule is:

> **Eyeballs provide observations. Cartography provides coordinates.**

The player may physically see or visit something without having any authoritative way to place it on the shared map.

This is deliberate and central to the navigation game.

---

# 2. REMOVE THE OLD GEOGRAPHIC KNOWLEDGE-STATE MODEL

For geography, nodes, and POIs, do not use hidden progression states such as:

```text
UNKNOWN
RUMORED
PARTIAL
KNOWN
```

Instead:

> **The player's actual shared map content is the knowledge state.**

If authoritative information has been integrated, it can render. If it has not been integrated, it does not render.

Partial information exists naturally because different sources provide different payloads.

Examples:

```text
Accurate island shape known
Node marker absent

Node marker known
Surrounding coastline absent

Surface geography known
Seafloor absent

Seafloor known
Underwater POI absent
```

No enum is required to describe those combinations.

---

# 3. RUMORS ARE LITERAL RUMORS

Rumors do not mutate map state.

Example:

> "A merchant carrying timber went down about 40 miles offshore between Greyhook and Blackwater."

That may be recorded in a future Rumor Log, quest log, notebook, dialogue history, or other player-facing reference.

It does **not**:
- add a POI marker,
- add an approximate search circle,
- set a hidden POI state,
- shade a confidence region,
- alter authoritative map coverage.

Better sources should give **better clues**, not automatic uncertainty geometry.

The player can use the physical mapping tools to draw circles, infer routes, place blocks/notes, or mark guesses. Manual annotation behavior belongs to the existing Mapping Table / Cartography Workbench system and should not be duplicated here.

---

# 4. THREE KNOWLEDGE LAYERS

The project has three distinct information layers sharing one global coordinate space.

## 4.1 Star Map

Celestial knowledge:
- landmark stars,
- charted star regions,
- constellations,
- planets / future dynamic celestial bodies,
- celestial chart fragments,
- player celestial annotations.

This layer is independent from geographic discovery.

Knowing the stars above an area does not reveal the world beneath them.

## 4.2 World Map

Primary geographic reference layer:
- land / water,
- coastline,
- normal surface topography / biome presentation,
- integrated node markers,
- integrated surface POIs,
- shared manual table annotations.

## 4.3 Sea Floor / POI Overlay

Secondary geographic layer rendered on top of the World Map:
- bathymetry,
- depth contours,
- trenches,
- caves / underwater terrain when supported,
- integrated underwater POIs.

The Sea Floor layer is **not** a separate standalone map tab. It is an overlay on the World Map.

---

# 5. SURFACE KNOWLEDGE IS A PREREQUISITE FOR VIEWING SEAFLOOR KNOWLEDGE

Seafloor information may already have been acquired and stored, but it cannot render where World Map coverage is absent.

```text
Sea floor data exists
        +
World Map coverage absent
        =
Sea floor data hidden from view
```

Later, when authoritative surface coverage is integrated:

```text
World Map coverage becomes known
        ↓
existing processed bathymetry can render
        ↓
existing processed underwater POIs can render
```

This is a **display dependency**, not a requirement that surface data must always be acquired first.

Players may perform arbitrary sounding in unknown waters, but the processed result remains unviewable until the surface map in that area is known.

For quests, sounding quests should target World Map-known regions.

---

# 6. UNCHARTED WORLD MAP PRESENTATION: MYTHIC NAUTICAL SHROUD

Uncharted space should not look like ordinary black fog-of-war.

It should look like an old nautical map filled with mythical ocean imagery.

Possible decorative motifs:
- krakens,
- megalodons,
- sea dragons / serpents,
- volcanoes / fire islands,
- Poseidon / sea-god imagery,
- mermaids / sirens,
- ghost ships,
- maelstroms,
- leviathans,
- cursed storms,
- tridents / crowns / treasure motifs,
- decorative currents / waves,
- ominous old-map warnings and flourishes.

Krakens may be real in the actual game world. That does **not** make decorative kraken art authoritative.

## Hard rule

> **The mythic uncharted layer must never sample, inspect, encode, hint at, or statistically correlate with actual world truth.**

It must not use:
- topography,
- land,
- nodes,
- POIs,
- biome truth,
- seafloor truth,
- resources,
- event locations,
- actual kraken locations,
- actual volcanoes,
- celestial truth.

Use a separate decorative visual seed/system.

---

# 7. AUTHORITATIVE SURFACE COVERAGE REPLACES MYTH

Where authoritative World Map coverage exists:

```text
mythic shroud hidden
+
normal authoritative World Map rendering visible
```

Where coverage does not exist:

```text
mythic nautical shroud visible
+
world truth completely unreadable
```

Do not allow faint coastline, terrain, biome, or seafloor leakage beneath the shroud.

---

# 8. WORLD MAP COVERAGE IS CARTOGRAPHIC DATA, NOT PASSIVE EXPLORATION

Do not reveal World Map coverage based on:
- boat proximity,
- sailing path,
- camera view,
- physical line of sight,
- island visibility,
- docking.

Coverage comes from actual cartographic sources, such as:
- Surveyor local charts,
- completed surface-survey contracts,
- purchased georeferenced charts,
- found georeferenced charts,
- quest rewards,
- other explicit future authoritative sources.

Open ocean that has been properly charted is valid useful information. A revealed open-water region may simply render as ocean.

---

# 9. COVERAGE MUST SUPPORT ARBITRARY SHAPES

Do not build the system around only:

```text
RevealCircle(center, radius)
```

Coverage should support compositing / unioning arbitrary shapes or masks, including:
- connected landmass + offshore dilation,
- smoothed survey swaths,
- corridor/polyline + width,
- polygon,
- rectangle,
- circle,
- authored/generated arbitrary mask,
- purchased/found chart mask.

Implementation may use masks/textures internally, but the conceptual system must remain shape-agnostic.

---

# 10. LOCAL SURVEYOR CHART

The local Surveyor's baseline cartographic service reveals **his island only**.

Default reveal:

```text
connected landmass associated with Surveyor's node
+
configurable offshore buffer/dilation
```

Do not automatically reveal:
- neighboring islands,
- neighboring nodes,
- regional POIs,
- nearby trade routes,
- seafloor data.

## 10.1 Whole-island default

For normal procedural islands:

> Reveal the entire connected island plus a configurable offshore ocean buffer.

## 10.2 Procedural-world safety guardrail

Dynamic generation can produce pathological landmasses.

If the connected landmass exceeds configured safety limits, do not reveal the entire continent-sized object.

Support configurable guardrails such as:
- maximum connected-landmass area,
- maximum extent / diameter,
- maximum reveal radius,
- fallback local-region area budget.

Fallback behavior should remain centered on / associated with the Surveyor's node, still follow real coastline where practical, include normal offshore dilation, and avoid exposing absurd portions of the world from one local NPC.

---

# 11. SURVEYOR POSITION FIX

Speaking to a Surveyor at a node provides an authoritative position fix.

This is separate from map-chart integration.

On successful Surveyor interaction:

> **The physical believed-position marker snaps to the true HarborPosition / NodePosition.**

This can happen even if the player has navigated badly and placed the marker somewhere wildly wrong.

Docking alone does **not** provide this fix. Merely being physically present at a town does **not** provide this fix.

This creates meaningful gameplay for towns with no Surveyor, absent/dead Surveyors, hostile Surveyors, or Surveyors who refuse to help.

---

# 12. THE BELIEVED-POSITION MARKER IS A PHYSICAL TABLE OBJECT

Do not turn the believed-position marker into an automatic navigation system.

It may:
- be manually dragged anywhere,
- be rotated,
- be misplaced,
- sit over uncharted mythic space,
- be thrown off the table.

Its special behavior is minimal:

```text
authoritative position fix
→ snap marker to known true coordinate
```

Do not automatically move it through dead reckoning unless a separate later mechanic explicitly does so.

---

# 13. MAP SOURCE PAYLOADS ARE EXPLICIT

A cartographic source only grants the information it actually contains.

Possible payloads:

```text
surface geography coverage?
node markers?
surface POIs?
bathymetry?
underwater POIs?
position fix?
```

Do not use:

```text
coverage known
→ reveal everything inside
```

Examples:

### Shipping chart
- coastline: yes
- node markers: yes
- surface POIs: maybe
- bathymetry: no
- underwater POIs: no

### Hydrographic chart
- surface geography: maybe
- bathymetry: yes
- underwater POIs: maybe

### Node-location record
- exact node marker: yes
- surrounding geography: no

### Coastline chart
- geography: yes
- settlements: maybe not

---

# 14. NODE MARKERS AND SURFACE POIS MAY EXIST IN UNCHARTED SPACE

An authoritative source may provide a node location even when surrounding geography is not known.

Likewise an authoritative **surface POI** may be displayed in otherwise uncharted space if a source explicitly provides its exact georeferenced location.

Do not infer surrounding geography from that marker.

---

# 15. UNDERWATER POIS DO NOT RENDER WITHOUT WORLD MAP COVERAGE

Underwater POIs are part of the Sea Floor / POI overlay.

Even if their authoritative coordinates are already stored:

> They remain hidden until the World Map at that coordinate is known.

---

# 16. PHYSICAL DISCOVERY DOES NOT GEOREGISTER CONTENT

The following do **not** automatically add markers or coverage:
- seeing an island,
- seeing a lighthouse,
- seeing a giant tower,
- seeing ruins,
- docking at an unknown town,
- diving onto a wreck,
- finding a cave,
- seeing a surface-visible wreck,
- visually encountering any POI.

If players want to return later, they must establish position, use the stars, use their existing map, manually annotate the table, perform a real survey, obtain an authoritative chart, or otherwise solve the problem themselves.

Finding something once and being able to reliably relocate it are intentionally different achievements.

---

# 17. GEOREFERENCED CHARTS VS REFERENCE / KNOWLEDGE CHARTS

There are two fundamentally different chart concepts.

## 17.1 Georeferenced Integratable Charts

These contain sufficient world-coordinate registration to integrate into the master shared map.

Examples:
- Surveyor local island chart,
- official regional map,
- processed surface survey chart,
- processed bathymetric chart,
- purchased shipping chart.

They can contain any explicit payload subset.

Integration is deliberate and occurs at the Mapping Table.

## 17.2 Reference / Knowledge Charts

These **never integrate** into the master map.

They are literal evidence/reference objects the player must interpret visually.

Examples:
- treasure maps,
- star-reference cards,
- antique maps,
- old-book illustrations,
- topographic sketches with clues,
- bathymetric sketches pointing toward something interesting,
- cave diagrams,
- POI clue maps.

They may contain truthful nuggets. They do not automatically resolve world coordinates.

These are especially appropriate for juicier / higher-value POIs. Not all POIs should use this mechanism.

---

# 18. NO BAD AUTHORITATIVE CHARTS

Formal integratable charts are accurate.

Do not implement conflicting false geography in the authoritative cartographic dataset.

Old books, antique maps, folklore diagrams, and suspicious historical records may contain obsolete names, incomplete drawings, ambiguous clues, strange references, or partial truths, but those belong to the **reference / knowledge chart** category.

A future map-corruption system may deliberately alter known cartography. That is a separate later pass.

---

# 19. CHART INTEGRATION WORKFLOW

Georeferenced charts do not auto-merge on pickup.

Player deliberately integrates them at the Mapping Table.

```text
acquire physical georeferenced chart
        ↓
bring to Mapping Table
        ↓
choose Integrate
        ↓
validate source/payload
        ↓
atomically commit authoritative shared map data
        ↓
consume/destroy physical chart item
        ↓
play reveal/fade presentation
```

If integration fails:
- do not consume the chart,
- do not partially commit the payload.

Integration is a single consequential transaction.

---

# 20. INTEGRATED CHARTS ARE CONSUMED

Once a georeferenced chart is successfully integrated:

> **Destroy / consume the physical chart item.**

The knowledge remains permanently in the shared cartographic dataset.

---

# 21. FADE-IN PRESENTATION

Newly integrated authoritative knowledge should not pop into existence in one frame.

After commit:
- mythical shroud fades away,
- authoritative geography fades in,
- supplied markers/POIs appear,
- supplied bathymetry appears if currently viewable.

Use a configurable short duration, roughly in the 1–3 second range initially.

## Critical rule

> **Knowledge commits immediately. The fade is presentation only.**

Therefore:
- save during fade: knowledge is already acquired,
- multiplayer authority: state is already committed,
- reopen map: data is fully present,
- gameplay logic does not wait on the fade animation.

---

# 22. SHARED CREW / BOAT CARTOGRAPHY

Authoritative cartographic knowledge is shared at the boat/crew level.

Shared state includes:
- World Map coverage,
- integrated node markers,
- integrated surface POIs,
- processed/integrated bathymetry,
- integrated underwater POIs,
- shared Mapping Table state.

Do not implement per-player authoritative map knowledge in this pass.

Future exotic mechanics may grant character-specific knowledge, but those should be explicit exceptional systems.

---

# 23. SURVEYOR AS INFORMATION SOURCE / VENDOR

The Surveyor's baseline local role is:
- authoritative position fix,
- access to his own island's local chart.

He may also offer purchasable additional information, such as:
- georeferenced chart of another island,
- open-water shipping corridor,
- exact node marker,
- surface POI,
- bathymetric chart,
- hydrographic data,
- survey contract,
- non-integrating reference chart.

Purchased information still obeys explicit payload rules.

Do not assume the Surveyor knows everything nearby.

---

# 24. SURFACE SURVEY QUESTS

Surface survey quests are how large unknown open-water regions become authoritative World Map coverage.

They are predefined cartographic jobs.

A survey contract should know, before acceptance:
- intended survey region / reveal swath,
- required observation zones,
- navigation instructions,
- star-reference card(s),
- eligible turn-in Surveyor(s),
- exact cartographic reward payload.

The player's physical route does not paint the map.

---

# 25. SURVEY QUEST GENERATION KEYS INTO UNKNOWN WATER

Surface survey quests should literally inspect the shared authoritative World Map coverage.

They should generate only for currently unknown water.

Do not offer surface survey work in fully known water.

```text
shared World Map coverage
        ↓
find contiguous / valid unknown-water candidates
        ↓
score candidates
        ↓
generate contract from chosen unknown region
```

---

# 26. SURVEYOR DISTANCE WEIGHTING

Surveyors are far more likely to offer survey work in local waters.

Use a weighting system rather than a hard local-only rule.

```text
nearby unknown region
→ high candidate weight

farther unknown region
→ progressively lower weight

beyond configurable maximum reach
→ 0 weight
```

Distance should be a dominant factor.

Later modifiers may also prefer boundary expansion, filling strategic gaps, connecting disconnected known regions, or navigationally sensible routes.

---

# 27. TINY UNKNOWN-REGION CLEANUP

Do not leave tiny slivers, strips, dots, or isolated pixels of mythic unknown between otherwise mapped areas.

After legitimate World Map coverage changes:
1. Find contiguous remaining unknown components.
2. Measure their area / configured extent.
3. If below the configured cleanup threshold, automatically convert them to surface geography coverage.

This cleanup exists specifically to avoid annoying map dandruff common in fog-of-war systems.

## Cleanup grants only surface geography

Auto-filled tiny regions do **not** automatically grant:
- node markers,
- surface POIs,
- bathymetry,
- underwater POIs,
- position fixes,
- trade information.

It only removes meaningless tiny gaps in surface geography coverage.

## Future topology note

When horizontal world wrapping is implemented, contiguous-region detection must treat the left and right world edges as adjacent.

---

# 28. SURVEY CONTRACT READINGS ARE PREDEFINED

A surface survey quest defines designated reading zones.

Players are expected to survey in those locations.

Do not use the player's arbitrary successful route to determine reveal shape.

Successful readings prove that the player completed the assignment. The contract's own predefined reveal payload determines what becomes known.

---

# 29. SURVEY NAVIGATION INSTRUCTIONS

Every surface survey quest should provide enough information for a determined player to navigate without hidden GPS.

Each target should include:
- heading,
- rough distance,
- star-reference card showing the expected local sky pattern.

The star card is simply a visual reference.

It does not:
- integrate into the Star Map,
- guarantee successful interpretation,
- automatically match against the sky,
- reveal coordinates,
- correct the player.

The player may compare it against the physical night sky, their assembled Star Map, or the future telescope comparison view.

Heading + rough distance should get the player near the region. The star card provides a recovery/verification tool when blown off course.

---

# 30. TELESCOPE REUSE FOR SURFACE SURVEYING

Do not create a new dedicated Surface Survey Instrument.

Reuse the existing deployed Telescope.

When an appropriate surface survey quest is active, Telescope view gains a contextual Survey action.

The Survey option must remain available anywhere while the contract is active.

Do **not** show it only when inside a correct target zone, because that would leak position.

---

# 31. SIMPLE TELESCOPE SURVEY ACTION

Keep the interaction simple.

Recommended first pass:
- enter Telescope Survey mode,
- align reticle with 3 generated horizon circles/targets,
- confirm each,
- complete the observation,
- then validate geographic position.

After completion:

```text
Correct Survey Reading
```

or

```text
Incorrect Survey Reading
```

Incorrect feedback should not reveal distance, direction, nearest correct zone, map marker, arrow, or hidden coordinate.

The player may attempt again elsewhere.

---

# 32. CORRECT / INCORRECT FEEDBACK IS REQUIRED

Do not make players sail home for an hour before learning that a reading was invalid.

Immediate correct/incorrect feedback is intentional.

This slight leakage is acceptable because the player must deliberately perform a survey reading, the action takes time, and there is no directional feedback.

---

# 33. SURVEY READINGS DO NOT NEED STRICT ORDER

Each required reading zone can be completed independently.

If the player reaches Survey Point 3 before Survey Point 1:
- allow Point 3 to count,
- persist it,
- do not force arbitrary sequence.

Valid completed readings should persist through save/load, scene changes, unrelated exploration, and detours.

---

# 34. NODE-TO-NODE SURVEY CONTRACT TURN-IN

For survey contracts conceptually connecting two nodes:

> Allow completion/turn-in at the Surveyor at either endpoint.

Natural loop:

```text
accept at Node A
→ survey along route
→ arrive at Node B
→ turn in at Node B Surveyor
```

Do not force the player to return to the original NPC solely for paperwork.

---

# 35. ACCEPTED CONTRACTS REMAIN VALID IF KNOWLEDGE CHANGES

Surface survey quests should be generated only from unknown water at offer time.

However, once accepted, preserve the contract.

If another crew member later integrates a chart that reveals the same region:
- do not invalidate the active expedition,
- allow completed readings to remain valid,
- allow turn-in/reward.

The cartographic payload may be redundant by then, but the quest itself should not collapse due to multiplayer timing.

---

# 36. SURFACE SURVEY PAYOUT IS A PHYSICAL CHART

Turning in a completed surface survey does not directly update the shared World Map.

```text
complete required telescope readings
        ↓
turn in to eligible Surveyor
        ↓
Surveyor processes observations
        ↓
receive physical georeferenced survey chart
        ↓
bring chart to Mapping Table
        ↓
integrate
        ↓
chart consumed
        ↓
predefined survey coverage fades in
```

This keeps the physical information pipeline consistent across cartographic sources.

---

# 37. SURVEY SWATH MAY REVEAL LAND INSIDE IT

Surface survey quests primarily target unknown water.

If the predefined legitimate survey swath contains previously uncharted land/coastline:

> Reveal that surface geography too.

Do not leave mythic art floating over a landmass that lies inside the successfully charted region.

The payload still does not grant unrelated node markers or POIs unless explicitly included.

---

# 38. OPEN-WATER SURVEY REVEAL

A successful open-water survey may reveal nothing but ocean.

That is expected and valid.

The reward is:
- removal of mythic uncertainty,
- confirmation of georeferenced open water,
- expansion of practical navigational chart coverage.

Good mythic-shroud visuals and satisfying fade presentation should make this feel rewarding.

---

# 39. SOUNDING: PLAYER-GENERATED SEAFLOOR EVIDENCE

Players may perform sounding / bathymetric survey work anywhere.

Sounding should not directly paint the Sea Floor map.

It creates physical survey evidence using paper.

```text
use sounding equipment
        ↓
consume / use Charting Paper
        ↓
create / add to Sounding Chart
        ↓
physical chart exists
        ↓
no shared Sea Floor map update yet
```

The exact sounding equipment mechanics belong to the later Seafloor Resource / Survey rework if not already present.

---

# 40. SOUNDING CHART PROCESSING

A competent NPC such as a Surveyor, Hydrographer, or future specialist can process sounding data.

```text
Sounding Chart
        ↓
NPC processing
        ↓
physical georeferenced bathymetric chart
        ↓
Mapping Table integration
        ↓
chart consumed
        ↓
bathymetric data added to shared Sea Floor dataset
        ↓
fade in if World Map coverage at those coordinates exists
```

The NPC does not directly update the boat map.

---

# 41. SOUNDING QUESTS REQUIRE KNOWN WORLD MAP COVERAGE

Sounding / bathymetry quests should only target areas where the World Map surface coverage is already known.

Reason:

> First know the world. Then learn its finer underwater details.

Do not use sounding quests as a backdoor mechanism for revealing surface geography.

The quest generator should key into:

```text
World Map known
+
Sea Floor data missing
```

---

# 42. ARBITRARY NON-QUEST SOUNDING IS STILL ALLOWED

Players may choose to sound unknown waters on their own.

The resulting data can be collected, processed, and stored, but it remains unviewable on the Sea Floor overlay until surface World Map coverage for that region is later acquired.

Do not discard the work.

---

# 43. SEA FLOOR OVERLAY HAS NO SECOND MYTHIC SHROUD

When Sea Floor overlay is enabled:
- World Map remains visible normally.
- Known bathymetry renders where available.
- Unknown bathymetry simply does not render.
- Do not cover the surface map with a second mystery/fog layer.

Unsounded known ocean should look like ordinary mapped ocean with no extra seabed data.

---

# 44. SOUNDING DOES NOT AUTOMATICALLY REVEAL UNDERWATER POIS

Bathymetry and underwater POI information are separate payloads.

A basic sounder may reveal depth, contours, and terrain.

It does not automatically label wrecks, ruins, caves, submerged buildings, or rare resource sites.

Advanced sonar may later provide both bathymetry and POI detection, but only where explicitly designed.

---

# 45. SURFACE POI AND NODE INFORMATION ARE SOURCE-SPECIFIC

Authoritative geography coverage does not automatically reveal every node or POI in the covered region.

Node markers and surface POIs require their own explicit sources.

Examples:
- Surveyor local chart may include his own node marker.
- Purchased shipping chart may include multiple ports.
- Specialized chart may include a lighthouse.
- Ancient reference map may show something visually but never integrate.
- Physical sight alone does not georegister anything.

---

# 46. LOCAL SURVEYOR BASELINE NODE INFORMATION

The local Surveyor chart should include:
- his island surface geography,
- configured offshore buffer,
- his own node marker / canonical location,
- whatever minimal node identification is required for the current map UI.

Do not automatically include neighboring node markers.

---

# 47. NODE DETAIL / INTELLIGENCE: DEBUG FOR NOW

The current map/node UI may expose all node details in debug/testing once the node marker exists.

Do not attempt to solve final node-intelligence progression in this pass.

Potential future node intelligence includes:
- prosperity,
- security,
- trade state,
- dock quality,
- services,
- imports/exports,
- faction/allegiance,
- current events.

Future likely solution:
- separate Node Intelligence NPC/service such as Harbormaster, Broker, Merchant Guild clerk, Newskeeper, etc.

For now:

> debug/full info is acceptable.

---

# 48. STARTING GAME TEMPORARY BEHAVIOR

For implementation/testing now:

> Starting island coverage may begin already integrated.

This is temporary convenience so discovery systems can be tested immediately.

Final intended tutorial flow:

```text
new game
→ map mostly mythic/unmapped
→ tutorial points player to local Surveyor
→ Surveyor provides position fix + local physical chart
→ player integrates chart at Mapping Table
→ starting island fades into practical map
```

Architect this so the temporary auto-grant uses the same underlying cartographic-source/integration pipeline where practical.

Do not create a permanent special-case architecture for starting knowledge.

---

# 49. INTEGRATED AUTHORITATIVE KNOWLEDGE IS PERMANENT

Once authoritative cartographic information is successfully integrated:

> It is not normally lost.

Do not implement decay, forgetting, time-based map loss, or random chart erasure.

A future dedicated corruption mechanic may deliberately damage or alter map information. That is a separate pass.

---

# 50. MANUAL PLAYER ANNOTATIONS

Manual player annotations are not authoritative.

They can exist anywhere, including mapped space, uncharted mythic space, wrong locations, and empty ocean.

They do not snap to truth and do not reveal truth.

If the player's position estimate is wrong, their manual notes may also be wrong.

This system is already substantially specified in the Mapping Table / Cartography Workbench handoff. Do not duplicate/rebuild it here.

---

# 51. TELESCOPE REFERENCE COMPARISON — ADJACENT TODO / SEAM

Future telescope enhancement:

While in Telescope view, a key should allow the player to bring up one chart fragment or reference card for direct visual comparison with the sky.

Desired behavior:
- show one owned celestial fragment or active quest reference card,
- rotate freely,
- reposition enough for comparison,
- no automatic matching,
- no snap-to-stars,
- no alignment success indicator,
- no knowledge generation merely from comparison.

This is especially useful for survey-contract star cards.

It is adjacent to this pass but should remain a small Telescope extension rather than expanding discovery architecture.

---

# 52. MULTIPLAYER / AUTHORITY

Shared cartographic state is consequential and must be authoritative.

Host/shared authority should own:
- integrated World Map coverage,
- integrated node markers,
- integrated surface POIs,
- integrated bathymetry,
- integrated underwater POIs,
- survey quest acceptance/completion state,
- completed reading states,
- chart integration transactions,
- Surveyor position-fix results if shared marker snapping is authoritative.

Local client presentation may own:
- fade animation,
- shroud dissolve visuals,
- map rendering interpolation,
- telescope reticle graphics.

Prevent duplicate chart consumption, duplicate integration, duplicate survey turn-in, race conditions between two players using the same chart, and inconsistent shared map results.

---

# 53. SAVE / PERSISTENCE

Persist at minimum:
- World Map authoritative coverage,
- integrated node markers,
- integrated surface POIs,
- bathymetric data coverage,
- integrated underwater POIs,
- active survey contracts,
- completed survey-reading IDs,
- produced but not yet integrated physical charts,
- current physical believed-position marker state via existing Mapping Table persistence,
- shared Mapping Table state.

Do not persist presentation-only fade progress.

If saved during a reveal:
- knowledge is already committed,
- reload displays final revealed state.

---

# 54. DEBUG / AUTHORING SUPPORT

Provide debug controls to:
- reveal selected surface coverage shape,
- clear/rebuild test coverage,
- grant sample georeferenced chart,
- grant sample reference chart,
- integrate chart via debug,
- show coverage-mask bounds,
- show contiguous unknown components,
- show tiny-hole cleanup candidates,
- show survey quest candidate regions,
- show Surveyor distance weights,
- show predefined survey swaths,
- show required reading zones,
- force correct/incorrect reading test,
- grant bathymetry,
- toggle Sea Floor overlay,
- test node marker without geography,
- test surface POI without geography,
- test underwater POI with/without surface coverage.

Debug-only visualizations must not leak into normal gameplay.

---

# 55. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## DISC.1 — Core cartographic state + mythic shroud
- replace geography knowledge-state assumptions with actual integrated map-data queries
- establish World Map coverage representation
- mythic nautical shroud
- no truth leakage
- arbitrary-shape coverage union
- node/surface-POI payload separation
- Sea Floor overlay dependency rules

**STOP FOR TESTING.**

## DISC.2 — Georeferenced chart pipeline
- physical integratable chart data model
- reference/non-integrating chart distinction
- Mapping Table integration
- atomic consume-on-success
- fade-in reveal
- node marker in uncharted space support
- surface POI in uncharted space support

**STOP FOR TESTING.**

## DISC.3 — Local Surveyor cartographic seam
- local whole-island + offshore-buffer chart generation
- pathological-landmass guardrails
- Surveyor position-fix API
- temporary starting-island grant path
- additional-info/vendor seams
- do not build final NPC content yet

**STOP FOR TESTING.**

## DISC.4 — Surface survey contract foundation
- unknown-water candidate extraction
- distance-weighted Surveyor candidate scoring
- predefined survey swath
- designated reading zones
- heading + rough-distance + star-reference-card data
- Telescope contextual survey action
- correct/incorrect feedback
- persistent independent readings
- either-endpoint turn-in
- physical processed survey chart reward

**STOP FOR TESTING.**

## DISC.5 — Tiny unknown cleanup + shared authority
- contiguous unknown-region cleanup threshold
- shared multiplayer authority
- accepted-contract stability if coverage changes later
- save/load regression
- position-fix regression
- chart race-condition protection

**STOP FOR TESTING.**

## DISC.6 — Seafloor cartographic foundation
- Sea Floor overlay on World Map
- hidden-until-surface-known rendering rule
- Sounding Chart evidence seam
- processing → georeferenced bathymetric chart
- integrate/consume/fade flow
- known-surface-only sounding-quest candidate rule
- arbitrary player sounding outside quests remains possible
- underwater POI rendering gate

**STOP FOR FINAL TESTING.**

---

# 56. ACCEPTANCE TESTS

The discovery pass is ready when all of the following are true.

## Core map knowledge
1. Sailing through unknown space reveals nothing automatically.
2. Seeing land reveals nothing automatically.
3. Docking reveals nothing automatically.
4. Seeing a surface POI reveals nothing automatically.
5. Finding an underwater POI reveals nothing automatically.
6. Geography no longer depends on a hidden Unknown/Rumored/Partial/Known state machine.
7. Shared map content itself determines what the crew can consume.

## Mythic shroud
8. Uncharted World Map regions show nautical mythic art.
9. Mythic art never samples actual world truth.
10. No coastline/topography/biome leakage exists under the shroud.
11. Surface coverage cleanly replaces mythic art.
12. Open-water reveal can show simply charted ocean.

## Coverage
13. Arbitrary coverage shapes can be unioned.
14. Local Surveyor reveal follows connected island geometry.
15. Normal local reveal includes configurable offshore dilation.
16. Oversized procedural landmasses trigger configured safety fallback.
17. Tiny contiguous unknown scraps auto-fill below threshold.
18. Auto-fill grants only surface geography.

## Surveyor / position
19. Talking to Surveyor can authoritatively snap the believed-position marker.
20. Docking alone does not snap the marker.
21. Believed marker can exist anywhere, including uncharted space.
22. Believed marker does not auto-follow true position.
23. Surveyor local knowledge is delivered through a physical chart, not direct map telepathy.

## Chart sources
24. Georeferenced charts integrate.
25. Reference charts never integrate.
26. Integratable payloads are explicit by information type.
27. Successful integration atomically commits knowledge.
28. Successful integration consumes the chart.
29. Failed integration preserves the chart.
30. Reveal presentation fades in after commit.
31. Knowledge remains permanent after integration.

## Nodes / POIs
32. Node marker can exist without surrounding geography.
33. Surface POI can exist without surrounding geography.
34. Underwater POI does not render without World Map coverage.
35. Geography coverage does not automatically grant node markers.
36. Geography coverage does not automatically grant POIs.
37. Current debug node detail panel may show full node information for testing.

## Surface surveys
38. Survey quests generate only from unknown water at offer time.
39. Local unknown-water candidates receive higher weight.
40. Candidate weight drops toward zero with distance.
41. Contract owns predefined survey region and reveal payload.
42. Player route does not determine reveal shape.
43. Each target has heading + rough distance + star reference.
44. Telescope Survey action can be attempted anywhere while contract is active.
45. Survey minigame itself does not reveal whether target zone is nearby.
46. Completed reading returns Correct/Incorrect only.
47. Incorrect feedback gives no directional/location correction.
48. Readings may complete out of order.
49. Valid readings persist.
50. Node-to-node contracts may be turned in at either endpoint.
51. Accepted contracts remain valid if another chart later reveals the area.
52. Turn-in produces a physical georeferenced survey chart.
53. Integrating that chart consumes it and reveals the predefined swath.
54. Surface survey swath may reveal land contained inside it.

## Seafloor
55. Sea Floor is an overlay on World Map.
56. Unsurveyed known ocean has no second mystery shroud.
57. Bathymetry can exist in storage outside known surface coverage.
58. Bathymetry does not render there until World Map coverage exists.
59. Sounding generates physical paper evidence rather than directly updating map.
60. Processed sounding becomes a physical georeferenced bathymetric chart.
61. Integrating the bathymetric chart consumes it.
62. Sounding quests only target World Map-known regions with missing seafloor data.
63. Arbitrary non-quest sounding remains possible.
64. Basic sounding does not automatically reveal underwater POIs.

## Multiplayer / persistence
65. Integrated cartography is shared crew/boat state.
66. Duplicate chart integration is prevented.
67. Duplicate survey turn-in is prevented.
68. Save/load preserves coverage and survey progress.
69. Saving during fade reloads to final committed knowledge.
70. Existing Star Map / Mapping Table behavior remains intact.

---

# 57. EXPLICIT NON-GOALS

Do not implement in this pass:
- final Surveyor NPC dialogue/art/behavior,
- full NPC procedural spawning,
- final quest framework overhaul,
- final Node Intelligence economy,
- rumor log,
- map corruption,
- false authoritative charts,
- player-specific normal map knowledge,
- final telescope chart/reference comparison UX,
- final sounding equipment mechanics if they belong to later Seafloor pass,
- advanced sonar,
- underwater POI generation,
- wreck/cave/building procedural generation,
- full NodeScene generation,
- boat hull damage,
- boat module maintenance,
- mooring line system,
- dock visual rework,
- wrapped-world conversion itself.

---

# 58. FOLLOW-UP PASSES / TODO SEAMS

### Near-term
- **Surveyor NPC mini-pass**
  - physically spawn Surveyor in NodeScene
  - basic interaction
  - local chart handoff
  - position fix
  - survey-contract hook
  - purchasable information seam

### Later
- **Node Intelligence NPC/pass**
  - remote trade/security/prosperity/services/current-state information for known nodes

- **Telescope comparison mini-pass**
  - directly compare rotatable star fragment/reference card against physical sky

- **Cartographic corruption pass**
  - exceptional mechanics that deliberately damage/alter known map information

- **Seafloor / Resource / POI generation rework**
  - dynamic underwater POIs
  - wrecks
  - ruins
  - caves
  - underwater buildings
  - events
  - resource distribution
  - survey/sonar hooks

- **Actual Node Generation pass**
  - NPCs
  - houses/buildings
  - quest/event seams
  - seafloor/depth alignment around settlements

---

# 59. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:
1. Exact files modified
2. Exact new files
3. Existing classes inspected before modification
4. New authoritative data structures
5. Coverage-mask representation
6. Mythic-shroud implementation
7. Chart-source payload model
8. Chart consume/integration transaction
9. Surveyor seams
10. Survey-quest generation/scoring
11. Telescope survey hook
12. Seafloor visibility gating
13. Multiplayer authority decisions
14. Save/persistence changes
15. Debug tooling added
16. Regression tests performed
17. Any discrepancy between live project architecture and this handoff

Do not silently broaden scope into Node Generation or Seafloor POI generation.

---

**End of Map / Node Discovery handoff.**
