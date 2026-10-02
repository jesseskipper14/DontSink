# DON'T SINK — Phase 6C Bosun Implementation Handoff
## Cartography Workbench: Physical Drawing, Route String, Measuring Tools, and Manual Navigation Records

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Phase:** 6C  
**Primary objective:** Turn the shared map table into a persistent, physical navigation workspace where players manually draw, measure, plot, record, and manage routes without the game calculating navigation answers for them.

---

# 0. NON-NEGOTIABLE PROJECT RULES

## Exact-current-class rule

> If modifying an existing class, only modify the exact latest version currently present in the project. Never reconstruct an existing class from memory, prior handoffs, or assumptions.

Before changing an existing class:
1. Open and inspect the exact current file.
2. Reconcile this spec against what actually exists.
3. Preserve unrelated features, serialized fields, authority hooks, compatibility shims, and current multiplayer hardening.
4. Prefer additive changes over rewrites.
5. If the live code differs from this document, adapt this design to the live code rather than forcing older assumptions onto it.

New support classes may be authored freely.

## Do not touch the piloting cartridge in Phase 6C

> **Phase 6C must NOT modify the current piloting cartridge or current piloting execution logic.**

Phase 6C ends at a clean future-facing navigation-plan query seam.

A later piloting rework will consume the active route leg's player-recorded heading/distance.

## Checkpoint discipline is mandatory

Implement this phase in **five checkpoints**:

1. **6C.1 — Physical Workspace Foundation**
2. **6C.2 — Pencil, Eraser & Freehand Annotation**
3. **6C.3 — Physical Route String & Pins**
4. **6C.4 — Physical Measurement & Manual Navigation Records**
5. **6C.5 — Persistence, Authority & Freeze**

> **Implement only the current checkpoint. Stop after it compiles and its acceptance tests pass. Do not begin the next checkpoint until Jesse has playtested/refined and explicitly approved the current one.**

This is required because game feel and interaction quality need regular live refinement.

---

# 1. CORE PHILOSOPHY

The game must not hand the player navigation answers.

The intended flow is:

```text
pin route string from A to B
        ↓
drag ruler next to string
        ↓
player reads distance
        ↓
drag/rotate 180° protractor over route
        ↓
player reads angle / does simple heading math
        ↓
player manually types heading + distance
        ↓
that exact transcription becomes the navigation record
```

The game may internally know geometry because it must render the board.

It must not expose:
- calculated route distance,
- calculated bearing,
- true heading,
- measurement error,
- correction prompts,
- automatic route generation,
- automatic alignment,
- automatic snapping,
- automatic leg advancement.

If the player types `289°` instead of `29°`, the system records `289°`.

---

# 2. HIGH-LEVEL FANTASY

The map table should feel like a **busy, persistent navigation board**, not a temporary route-planning screen.

It may accumulate:
- old routes,
- abandoned routes,
- route pins from six journeys ago,
- several colors of string,
- marked-up celestial scraps,
- blank paper,
- handwritten notes and doodles,
- colored blocks,
- player boat piece,
- ruler,
- protractor,
- pencil,
- eraser,
- string spool,
- general clutter.

The player cleans up after themselves, or does not.

Nothing automatically tidies the board after:
- finishing a journey,
- changing scenes,
- opening/closing the table,
- activating another route,
- arriving somewhere.

---

# 3. EXISTING FOUNDATION TO PRESERVE

Phase 6 already established the shared physical map-table architecture.

Likely relevant existing systems include:
- `MapTableCartridge`
- World Map tab
- Star Chart tab
- shared coordinate plane / viewport registration
- finite physical table
- physical table blocks / player boat piece
- celestial scraps / folio
- fragment move / rotate / pin
- assembled fragment groups
- manual evidence-based snapping
- persistent board state
- requester-aware authority/revision patterns

Inspect exact current files before touching any of this.

Phase 6C should extend the same physical workspace, not replace it.

---

# 4. SHARED PHYSICAL-SPACE RULES

## Shared table-level objects

Routes, route pins, reusable tools, table-owned drawings, and blank paper live on the **Map Table itself**, not one tab.

They remain at the same table-space coordinates while switching between:
- WORLD MAP
- STAR CHART

Switching tabs changes what map information is underneath them, not their transforms.

## Route pins may be placed anywhere on the physical table

Pins may be placed:
- on the mapped area,
- in unknown areas,
- on wooden border,
- in tool-tray area,
- on blank paper,
- on celestial scraps,
- anywhere on the physical table.

The system does not judge whether the route is useful.

## No snapping

Route pins do not snap to:
- player boat piece,
- destinations,
- nodes,
- POIs,
- celestial objects,
- grids,
- ruler,
- protractor,
- other pins,
- route endpoints.

Placement is completely manual.

---

# 5. CHECKPOINT 6C.1 — PHYSICAL WORKSPACE FOUNDATION

## Goal

Add the reusable physical tool set, tool tray, blank paper, and shared persistence skeleton.

Do not add freehand drawing yet.  
Do not add routes yet.  
Do not add navigation records yet.

## Physical tool tray

Add a **physical table-space tool tray** beside/outside the main mapped area.

It is not screen-fixed UI.

It:
- is part of the finite physical table,
- pans/zooms with the table,
- has default/home transforms for tools,
- is the initial home for reusable tools.

## Required reusable tools

The table automatically provides:
- ruler,
- 180° protractor,
- pencil,
- eraser,
- string spool.

These are reusable table fixtures, not inventory consumables.

## Tool persistence

Tool transforms persist exactly where the player leaves them.

Persist at minimum:
- table position,
- rotation where relevant.

Closing the table or changing scenes must not reset them.

## Return Tools to Tray

Provide:

> **Return Tools to Tray**

This restores each reusable tool to its defined home transform.

No automatic reset otherwise.

## Tool controls

### Ruler / protractor
- Hold LMB = pick up / move.
- RMB-drag while carried = rotate freely.
- Release LMB = drop.
- No snap-to-route.
- No snap-to-north.
- No numeric angle readout.

### Pencil / eraser
- movable,
- fixed orientation,
- no rotation needed.

### String spool
- movable/reusable,
- no meaningful rotation needed.

## Blank paper uses existing Charting Paper

Do not invent a second paper resource unless the current inventory architecture absolutely forces it.

The existing Charting Paper item can:
- be consumed by celestial observation to create evidence,
- be placed directly as blank physical paper.

Blank paper is belief material, not evidence.

## Blank paper size

Blank paper is:
- fixed-size,
- non-resizable,
- non-cuttable.

Expose:
- blank paper width in table/world units,
- blank paper height in table/world units.

## Blank paper placement

Preferred:
- drag Charting Paper from inventory onto the table.

Before implementing, inspect the exact current `InventoryDragController`.

### If drag/drop cleanly supports this
Use it transactionally.

### If not
Do not destabilize the inventory system.

Use:
- `Add Blank Paper`
- consumes one Charting Paper
- creates a blank sheet at a sensible table/tray spawn location.

Underlying paper-instance data must be the same either way.

## Returning blank paper to inventory

Blank paper may return to inventory only if not restrained by a pin.

Preferred:
- drag back to inventory if cleanly supported.

Fallback:
- `Return to Inventory` button.

A marked sheet must preserve:
- unique paper identity,
- freehand drawing state,
- any needed metadata.

Do not collapse a marked sheet into a generic anonymous stack and lose its state.

## Paper restraint rule

Only pins restrain paper.

Restraining:
- normal paper pins,
- future route pins.

Non-restraining:
- ruler,
- protractor,
- pencil,
- eraser,
- string spool,
- colored blocks,
- player boat piece,
- route string itself.

## Blank paper visibility across tabs

Blank paper is shared across both tabs.

World Map must provide a **Hide Paper** presentation toggle.

When ON:
- blank paper invisible,
- blank-paper-owned handwriting later invisible,
- blank paper non-interactable,
- still physically present,
- still restrained by pins,
- route strings/pins/tools/blocks still visible.

When OFF:
- paper returns exactly where it was.

Celestial evidence scraps remain Star Chart-specific.

## 6C.1 acceptance tests

1. Tool tray exists in physical table space.
2. All five reusable tools exist automatically.
3. Ruler/protractor move and rotate by mouse.
4. Pencil/eraser/spool move.
5. Tool transforms survive close/reopen.
6. Tool transforms survive scene transition.
7. Return Tools to Tray works.
8. Blank paper consumes existing Charting Paper.
9. Blank paper size is fixed and Inspector configurable.
10. Blank paper moves/rotates/stacks.
11. Unpinned blank paper can return to inventory.
12. Pinned blank paper cannot return.
13. Hide Paper works on World Map without deleting state.
14. Existing Phase 6 behavior remains intact.

> **STOP HERE FOR PLAYTEST AND APPROVAL.**

---

# 6. CHECKPOINT 6C.2 — PENCIL, ERASER & FREEHAND ANNOTATION

## Goal

Add literal physical pencil/eraser behavior and persistent editable freehand strokes.

Do not begin route work yet.

## Pencil controls

```text
Hold LMB
→ carry pencil

While LMB remains held:
    hold RMB
    → apply pencil at physical tool tip

Release RMB
→ stop drawing, keep carrying

Release LMB
→ drop pencil
```

Drawing occurs at the tool tip, not cursor center.

Pencil orientation stays fixed.

## Eraser controls

Same pattern:

```text
Hold LMB
→ carry eraser

While LMB remains held:
    hold RMB
    → erase under physical tip/radius

Release RMB
→ stop erasing

Release LMB
→ drop eraser
```

Eraser orientation stays fixed.

## World/table-consistent line width

Pencil width must be in **table/world units**, not screen pixels.

Zoom changes apparent pixel width but not physical width relative to map/table.

Expose:
- Pencil Stroke Width World Units
- Eraser Radius World Units
- Stroke Sample Spacing World Units

A line drawn at one zoom level must physically match one drawn at another.

## Pencil style

For 6C:
- one graphite color,
- one width,
- no brush palette,
- no pressure sensitivity,
- no highlighter,
- no typed text.

General board annotation is freehand only.

## Drawable surfaces

Pencil may draw on:
1. exposed table/map plane,
2. blank paper,
3. celestial evidence scraps.

## Stroke ownership

### Table-owned stroke
- anchored to table coordinates,
- visible across both tabs,
- does not move with paper.

### Blank-paper-owned stroke
- stored in paper-local coordinates,
- moves/rotates with paper,
- returns to inventory with paper,
- hidden when that blank paper is hidden on World Map.

### Celestial-scrap-owned stroke
- separate mutable annotation layer,
- moves/rotates with scrap,
- survives grouping/snapping,
- never modifies immutable evidence.

## Immutable evidence

Pencil/eraser must not affect:
- celestial object IDs,
- fragment evidence,
- observation datum,
- snapping,
- constellation queries,
- Phase 7 structured celestial names/notes.

## Drawing ownership on stacked surfaces

When a stroke begins over stacked papers, assign it to the **topmost drawable physical surface** under the pencil tip.

Stroke ownership remains fixed for that stroke.

## Annotation render layer

Important usability rule:

> **All handwritten annotations render above all paper layers, regardless of which sheet owns them.**

This deliberately sacrifices strict realism to avoid tedious paper digging.

Ownership remains correct internally.

## Eraser behavior across overlapping annotations

> **Eraser affects every handwritten stroke geometry intersecting its radius, regardless of owner.**

One eraser pass may partially erase marks from:
- table plane,
- multiple blank papers,
- multiple celestial scraps.

Surviving fragments retain their original owners.

## Partial/local erasing

Do not delete an entire stroke just because one part was touched.

Persistent data must support:
- local deletion,
- splitting strokes into surviving fragments.

Store editable point/segment geometry or equivalent source data.

Rendering may cache/bake, but the persistent source must remain editable.

## Eraser never affects non-handwriting

Never erase:
- printed evidence,
- constellation lines,
- route string,
- pins,
- blocks,
- tools,
- map geography.

## Hide Paper + marks

Hiding blank paper on World Map hides:
- the sheet,
- handwriting owned by that sheet.

Table-owned handwriting remains visible.

## Stationary-surface rule

Drawing/erasing assumes target surface is stationary.

In multiplayer, use edit-lock/revision patterns so one player cannot move a sheet while another commits drawing to it.

## Shared-state commit shape

Do not authority/network commit each mouse sample.

Prefer:
- local stroke preview,
- commit completed stroke as one logical mutation,
- aggregate erase mutation on release/end.

## 6C.2 acceptance tests

1. Pencil physical drag/apply works.
2. Eraser physical drag/apply works.
3. Tool-tip position is correct.
4. Stroke width is physically consistent across zoom.
5. Drawing works on table.
6. Drawing works on blank paper and follows transform.
7. Drawing works on celestial scraps and follows transform/grouping.
8. All handwriting renders above paper.
9. Eraser can affect overlapping annotations from multiple owners.
10. Erasing is partial/local.
11. Printed evidence is immutable.
12. Hidden blank paper hides its owned handwriting.
13. Table-owned handwriting remains visible across tabs.
14. Save/load preserves strokes.
15. Existing chart systems remain intact.

> **STOP HERE FOR PLAYTEST AND APPROVAL.**

---

# 7. CHECKPOINT 6C.3 — PHYSICAL ROUTE STRING & PINS

## Goal

Add persistent physical multi-pin string routes.

Do not add ruler/protractor record entry yet.

## Routes have no finished state

A route simply exists.

It may:
- be extended later,
- be ignored for many journeys,
- remain forever,
- be deleted only when player chooses.

Do not create `isFinished`.

## Route structure

Conceptually:

```text
●──────●──────●──────●
 P0     P1     P2     P3

Leg 0 = P0→P1
Leg 1 = P1→P2
Leg 2 = P2→P3
```

Store:
- stable route ID,
- string color,
- ordered pins,
- adjacent-pin legs,
- optional route name if trivial,
- revision/provenance as appropriate.

## Reusable string spool

String spool is the physical route-editing tool.

It is not an inventory rope resource.

Do not consume cargo-securing rope.

## Creating routes

With the spool:
1. place first route pin anywhere on physical table,
2. stretch string to cursor,
3. place additional pins,
4. stop interacting whenever desired.

Route remains and has no finished state.

## Extend route

Allow extending from either endpoint at any later time.

## Insert pin

Allow inserting a pin into an existing string segment.

This splits one leg into two.

## Move pin

Moving a pin moves only that pin.

No whole-route translation/rotation.

Middle pin changes two adjacent legs.  
Endpoint changes one.

## Remove pin

### Middle pin
Neighbors reconnect into one new leg.

### Endpoint
Route shortens.

### Fewer than two pins
Collapse/remove route as appropriate.

## Remove entire route

Provide explicit whole-route delete for convenience.

Do not force manual removal of dozens of pins.

## Route string colors

Initial player-selectable palette:
- red,
- blue,
- green,
- yellow,
- white,
- black.

Color has no mechanical meaning.

## Optional route name

Nice-to-have only.

If trivial:
- optional free-text name,
- duplicates allowed,
- empty allowed,
- stable ID remains actual identity.

Do not let this delay core behavior.

## Pin placement

Pins may be placed anywhere on the physical table.

No snapping.  
No map-bound restriction.  
No knowledge/fog requirement.

## Route pins restrain every paper layer underneath

> **A route pin restrains every paper sheet physically under its point, regardless of stacking order.**

If one pin pierces four overlapping papers, all four are restrained.

## Moving/removing route pins updates restraint dynamically

When a pin moves:
- recalculate papers under it,
- add/remove restraint appropriately.

A paper remains restrained if any other normal/route pin still restrains it.

## Unified paper-restraint query

Prefer one central rule:

> Paper may move/rotate only if no normal pin and no route pin restrains it.

## String itself does not restrain

Route string crossing paper does not pin it.

If no pin pierces the paper, it may move out from under the string.

## Shared across tabs

Route strings and pins stay visible in the same physical positions across WORLD MAP and STAR CHART.

## Physical render order

Conceptually:

```text
MAP / TABLE SURFACE
        ↓
paper + celestial scraps
        ↓
handwritten annotation layer
        ↓
route string
        ↓
colored blocks / player boat piece
        ↓
ruler / protractor / pencil / eraser / spool
        ↓
route pin heads
```

## Crowded-board selection

Normal click:
- topmost selectable object.

Alt+Click:
- cycle overlapping selectable objects beneath cursor.

Freehand strokes are not individually selectable.

## Persistence

Routes persist across:
- table close/reopen,
- scene changes,
- journeys,
- save/load.

No automatic cleanup.

## 6C.3 acceptance tests

1. Create multi-pin route.
2. Extend from either endpoint.
3. Insert pin into existing leg.
4. Move individual pins.
5. Remove individual pins.
6. Remove entire route.
7. No finished state exists.
8. String colors work.
9. Pins can go anywhere on physical table.
10. No snapping occurs.
11. Route pin restrains every paper layer underneath.
12. Moving pin updates restraint.
13. Removing final restraint releases paper.
14. String itself does not restrain.
15. Routes remain across both tabs.
16. Routes survive scene transition/save-load.
17. Topmost selection / Alt+Click cycling works.
18. Existing workspace/drawing features remain intact.

> **STOP HERE FOR PLAYTEST AND APPROVAL.**

---

# 8. CHECKPOINT 6C.4 — PHYSICAL MEASUREMENT & MANUAL NAVIGATION RECORDS

## Goal

Allow physical measurement, manual transcription, manual active route/leg selection, and expose a future-facing query seam.

Do not touch piloting.

## Canonical distance unit

Use **nautical miles (NM)**.

Expose configurable conversion:

```text
World/Table Units Per Nautical Mile
```

## Fixed-scale ruler

Ruler:
- lives in table space,
- has fixed physical scale,
- scales visually with zoom exactly like the map,
- same leg measures same ruler divisions at every zoom,
- shows printed NM markings,
- never outputs calculated distance.

Expose tuning:
- ruler physical length,
- major tick interval,
- minor divisions,
- world/table units per NM.

## 180° protractor

The protractor is **180° only**.

It:
- has printed angle markings,
- is moved manually,
- is rotated manually,
- does not know north,
- does not auto-align,
- does not snap,
- does not calculate/display heading.

The player does whatever simple math is needed to convert the reading into a 0–359° heading.

## Measurement tools never reveal route geometry numerically

Selecting a leg must not display:
- calculated distance,
- calculated bearing,
- endpoint coordinates,
- error versus entered values.

## Selecting a route leg

Clicking a string segment selects that leg.

Show only player-recorded navigation values.

Example:

```text
ROUTE LEG

Heading
[ 073 ] °

Distance
[ 18.4 ] nm

[ SAVE RECORD ]
[ CLEAR RECORD ]

⚠ Measurements may be stale
```

## Manual transcription only

Player manually types heading/distance.

No capture-from-instrument button.

If actual geometry is 29° and player enters 289°, store 289°.

## Precision rules

Heading:
- integer whole degrees,
- range `0–359`.

Distance:
- positive numeric,
- one decimal place in NM.

Validate syntax/range only.

Do not compare against route geometry.

## Partial records allowed

A leg may have:
- neither field,
- heading only,
- distance only,
- both.

## Stale behavior

Any geometry change affecting a leg marks its record **stale**.

Even a tiny nudge can mark stale.

Stale is warning-only.

It does not:
- disable usage,
- erase values,
- correct values,
- measure error.

## Save Record clears stale

When player explicitly saves the leg record:
- stale becomes false,
- regardless of whether values are correct.

Saving means:
> "The player considers this current."

## Topology edits + records

### Move pin
Affected existing legs become stale.

### Insert pin
Old leg ceases to exist.
Create two new unmeasured legs.

### Remove middle pin
Two old legs cease to exist.
Create one new unmeasured combined leg.

### Remove endpoint
Removed leg record disappears.

## Multiple routes

Many routes may coexist indefinitely.

Exactly one route may be:

> **Active for Piloting**

Changing active route does not alter/delete other routes.

## Manual active leg

Within active route, player manually chooses Active Leg.

No automatic advancement.

Never advance based on:
- true boat position,
- elapsed time,
- geometry,
- reaching anything,
- scene changes.

## Structural deletion of active target

If active route is deleted:
- Active Route = None
- Active Leg = None

If active leg is destroyed by split/removal/topology change:
- Active Leg = None

Do not choose a replacement automatically.

If active leg only changes geometry:
- remains active,
- becomes stale.

## Persistence

Active Route and Active Leg are shared consequential crew state.

Persist across scenes/journeys/save-load.

## Future-facing query seam

Expose something conceptually like:

```text
TryGetActiveNavigationLeg(...)
```

Return:
- route stable ID,
- leg stable ID,
- recorded heading if present,
- recorded distance NM if present,
- stale flag,
- optional route name/color metadata.

Critically:
- return player-recorded values exactly,
- do not calculate replacements from geometry,
- do not expose true boat position here.

## No piloting changes

Do not:
- modify current piloting cartridge,
- modify current route/progress architecture,
- add heading-follow execution,
- auto-advance legs,
- inject true-position assistance.

## 6C.4 acceptance tests

1. Ruler physical scale stays constant across zoom.
2. Ruler shows NM.
3. Protractor is 180°.
4. Protractor requires manual placement/rotation.
5. No automatic numeric distance/bearing is shown.
6. Manual heading/distance entry works.
7. `289°` is accepted if valid syntax/range.
8. Heading range validation works.
9. Distance one-decimal handling works.
10. Partial records work.
11. Moving pin marks affected leg stale.
12. Stale record remains usable.
13. Save Record clears stale.
14. Split/remove topology creates new unmeasured legs correctly.
15. Multiple routes coexist.
16. Active Route selection is manual.
17. Active Leg selection is manual.
18. No automatic advancement.
19. Deleting active route clears active state.
20. Destroying active leg clears Active Leg.
21. Query seam returns exact recorded values.
22. Piloting code remains untouched.

> **STOP HERE FOR PLAYTEST AND APPROVAL.**

---

# 9. CHECKPOINT 6C.5 — PERSISTENCE, AUTHORITY & FREEZE

## Goal

Harden all 6C behavior for shared persistent board use and future multiplayer.

No new major player-facing features should be invented here.

## One persistent shared navigation board

The board state survives:
- closing/reopening table,
- NodeScene ↔ BoatScene,
- travel,
- save/load,
- future reconnect/replication.

Persist:
- tool transforms,
- routes,
- route pins,
- route color,
- optional route name,
- route topology,
- leg records,
- stale flags,
- active route,
- active leg,
- blank/marked paper instances,
- paper transforms,
- all freehand strokes,
- stroke ownership,
- revisions/provenance as appropriate.

## Shared vs local state

Shared crew state:
- routes/pins/colors,
- route leg records,
- active route/leg,
- blank paper,
- marked-paper state,
- freehand strokes,
- reusable tool transforms,
- paper placement/pickup,
- route edits/deletions.

Local-only presentation:
- hover,
- temporary selection,
- open details panel,
- drag preview,
- local stroke preview,
- Alt+Click cycle index,
- Hide Paper toggle if treated as local preference.

## Authority mutation pattern

Follow current requester-aware patterns.

Preferred:
- begin edit,
- local preview,
- commit on release/completion,
- authority validates,
- revision increments,
- shared state updates.

Do not network/authority-mutate per mouse sample.

## Edit locks / conflicts

Use current edit-lock / expected-revision patterns for:
- moving same pin,
- moving paper while another player draws,
- route topology edits while another player edits leg data,
- simultaneous tool movement.

Reject/resolve conflicts predictably.

## Old-save migration

Pre-6C saves must load.

Missing 6C data defaults to:
- default tool home transforms,
- no new blank paper,
- no freehand strokes,
- no routes,
- no active route/leg.

Do not invalidate Phase 6/7 state.

## No hidden true-position dependency

Audit 6C to confirm:
- routes are player-authored table state,
- leg records are player-entered,
- active route/leg never query true boat position,
- no hidden route correction exists.

## Regression tests

Must not break:
- World Map / Star Chart registration,
- viewport pan/zoom,
- physical table bounds,
- celestial scraps,
- folio,
- fragment drag/rotate/pin,
- assembled groups,
- manual Attempt Snap,
- starter patch,
- player boat piece,
- colored blocks,
- Phase 7 celestial annotation state/UI,
- charting minigame,
- inventory flows,
- save/load,
- authority behavior.

## 6C.5 acceptance tests

1. Board survives table close/reopen.
2. Board survives scene transitions.
3. Board survives save/load.
4. Tool transforms persist.
5. Blank paper + handwriting round-trips through inventory.
6. Celestial-scrap handwriting persists.
7. Table-owned handwriting persists.
8. Routes persist indefinitely.
9. Route colors persist.
10. Optional route names persist if implemented.
11. Leg records persist.
12. Stale flags persist.
13. Active route/leg persist.
14. Route-pin restraint reconstructs correctly.
15. Old saves load safely.
16. Authority conflicts do not silently overwrite state.
17. Piloting remains untouched.
18. No true-position dependency exists.

> **STOP HERE FOR FINAL PLAYTEST / FREEZE APPROVAL.**

---

# 10. IMPORTANT FUTURE TODO — PILOTING REWORK

Pin this prominently:

> **PILOTING REWORK TODO:** Replace the current route/progress-oriented piloting architecture with manual heading execution consuming the active route leg's player-recorded heading and distance.

Future piloting should:
- query active navigation leg,
- display/use exact player-recorded heading,
- display/use exact player-recorded distance,
- present compass/current-heading feedback,
- let player steer manually,
- never calculate a route for the player,
- never correct transcription,
- never auto-advance route legs,
- never use true position to "help" execute the plan.

The player manually changes active leg.

True position remains simulation/arrival authority only.

Do not implement this in 6C.

---

# 11. IMPORTANT FUTURE TODO — TRUE VS BELIEVED POSITION

Later, formalize:

```text
True Position
= authoritative simulation position

Believed Position
= player boat physical table piece position

Planned Route
= player-authored physical table/string state
```

Do not create a second hidden believed-position variable if the blue boat piece already owns that meaning.

Trusted-location fixes can be handled later.

---

# 12. IMPORTANT FUTURE TODO — IMPULSE EFFECTS

Later during the impulse/boat-physics pass:

> Sufficiently large boat/table impulse can disturb loose physical table objects.

Possible future effects:
- loose colored blocks slide,
- loose tools shift,
- loose paper moves,
- pinned paper resists.

Do not implement impulse behavior in 6C.

---

# 13. REQUIRED INSPECTOR TUNING

At minimum expose sensible tuning for:

## Blank paper
- Width World Units
- Height World Units
- default spawn/home transform if needed

## Pencil / eraser
- Pencil Stroke Width World Units
- Eraser Radius World Units
- Stroke Sample Spacing World Units

## Ruler
- World/Table Units Per Nautical Mile
- Ruler Length
- Major Tick Interval
- Minor Divisions

## Tool tray
- tray bounds/home transforms as needed

## Visuals
- route string thickness in table/world units
- route pin size in table/world units
- tool sizes if not asset-driven

All physically meaningful sizes must be table/world-space consistent, not screen-pixel dependent.

---

# 14. RECOMMENDED DATA MODEL SHAPE

Names are conceptual only. Match current project conventions.

Possible structures:

```text
MapTableToolState
- toolId / kind
- tablePosition
- rotationDegrees
- revision

BlankChartPaperInstance
- paperInstanceId
- tableTransform
- strokeIds / owned strokes
- inventory/container state
- revision

FreehandStroke
- strokeId
- ownerKind
- ownerStableId
- localPoints[]
- widthWorldUnits
- revision

NavigationRoute
- routeId
- optionalName
- stringColor
- orderedPins[]
- revision

NavigationRoutePin
- pinId
- tablePosition
- revision

NavigationRouteLeg
- legId
- startPinId
- endPinId
- recordedHeadingDegrees?
- recordedDistanceNm?
- isStale
- revision

NavigationBoardActivePlan
- activeRouteId?
- activeLegId?
- revision
```

Do not create these exact classes blindly if the current architecture suggests cleaner ownership.

---

# 15. QUICK RULE SUMMARY

## Paper
- blank paper visible across tabs unless hidden on World Map,
- blank paper fixed-size,
- marked paper can return to inventory preserving marks,
- celestial scraps remain immutable evidence underneath freehand annotation,
- only pins restrain paper.

## Handwriting
- all handwritten marks render above all paper,
- marks stay owned by originating surface,
- eraser affects all handwritten marks under radius regardless of owner,
- erasing is partial/local,
- no typed general annotations.

## Routes
- persistent physical string,
- no finished state,
- multiple routes forever,
- player cleanup only,
- pins anywhere on table,
- no snapping,
- no whole-route movement,
- route colors selectable,
- route pins restrain all paper beneath,
- string itself does not restrain.

## Measurement
- ruler fixed-scale in NM,
- protractor 180°,
- both manual,
- no automatic numeric answer,
- player manually types heading/distance.

## Records
- heading whole degrees 0–359,
- distance one decimal NM,
- stale is warning-only,
- Save Record clears stale,
- active route manual,
- active leg manual,
- no auto advancement.

## Piloting
- untouched in 6C.

---

# 16. EXPLICIT NON-GOALS

Do not implement:
- piloting rework,
- automatic route generation,
- automatic route following,
- true-position correction,
- automatic leg advancement,
- numeric ruler output,
- numeric protractor output,
- tool auto-alignment,
- route snapping,
- typed freehand labels,
- pencil colors,
- multiple brush types,
- variable paper sizes,
- paper cutting,
- 360° protractor,
- automatic tool reset,
- automatic board cleanup,
- route completion state,
- whole-route translation/rotation,
- physical impulse disturbance,
- full networking transport.

---

# 17. BOSUN DELIVERY FORMAT AFTER EACH CHECKPOINT

At the end of each checkpoint provide:

1. Summary of behavior implemented
2. Exact files modified
3. Exact new files
4. Inspector fields/settings added
5. Save-schema additions
6. Authority/shared-state changes
7. Controls added
8. Known limitations
9. Compile/test status
10. Specific playtest checklist for Jesse
11. Any deviation from this handoff due to actual current code

Then STOP.

Do not continue without approval.

---

# 18. PHASE 6C DEFINITION OF DONE

Phase 6C is complete only after all five checkpoints are approved and:

- reusable physical tools exist on a persistent table-space tray,
- tool transforms persist,
- Return Tools to Tray works,
- blank Charting Paper becomes persistent blank sheets,
- marked sheets can return to inventory preserving marks,
- blank paper can be hidden on World Map,
- pencil/eraser behave as physical dragged tools,
- stroke width/eraser radius are table-scale consistent,
- drawing works on table, blank paper, celestial scraps,
- handwriting renders above paper stacks,
- partial cross-owner erasing works,
- routes are persistent multi-pin string objects,
- routes may remain indefinitely,
- route colors are selectable,
- route pins restrain every paper layer underneath,
- only pins restrain paper,
- route pins never snap,
- route pins may be placed anywhere on physical table,
- ruler is fixed-scale in NM,
- protractor is 180° and manually aligned,
- game never gives distance/bearing answers,
- player manually records heading/distance,
- stale logic works,
- active route/leg are manual and persistent,
- query seam returns exact player-recorded navigation data,
- current piloting cartridge remains untouched,
- old saves load,
- shared board survives scenes/journeys/save-load,
- existing Phase 6/7 systems remain intact.

---

**End of Phase 6C handoff.**
