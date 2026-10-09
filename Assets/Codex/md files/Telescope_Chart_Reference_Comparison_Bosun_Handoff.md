# DON'T SINK — Telescope Chart / Reference Comparison
## Bosun Mini-Pass Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Local telescope-use overlay for physically comparing owned star-bearing charts/reference items against the real sky  
**Size:** Small, focused mini-pass  
**Goal:** Let the player hold an owned chart/reference item up into the telescope view and manually compare its stars against the physical sky, with no snapping, auto-solving, or knowledge generation.

---

# 0. Exact-current-class rule

Before modifying any existing class:

1. Inspect the exact current live source.
2. Preserve unrelated serialized fields/current behavior.
3. Never reconstruct an existing class from memory or an older handoff.
4. Find and inspect the existing debug-key implementation that already displays a star-bearing chart/reference in this kind of comparison view.
5. Reuse existing inventory/item/chart/reference systems rather than creating a parallel item-selection model.
6. Stop after the checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

This pass should be implemented as a small extension of existing systems, not as a new telescope UI framework.

---

# 1. Existing telescope assumptions

This mini-pass builds on the existing telescope design.

The telescope is already intended to be:

- a physical carryable/placeable/pinnable boat item,
- deployed before use,
- subject to sky-clearance checks,
- using the same gameplay camera rather than a separate scope camera,
- using the existing RMB Look behavior,
- local in presentation while physical telescope placement remains shared,
- usable in daylight/night/weather as allowed by the existing telescope system,
- incapable of generating chart evidence or celestial knowledge by itself.

Do not redesign telescope deployment or camera behavior here.

---

# 2. Core interaction

While actively using the telescope, the player may open an eligible star-bearing chart/reference item from their own inventory.

The selected item is presented locally in the telescope view as if the player is physically holding the paper/card up in front of their view.

The purpose is purely visual comparison.

Example player behavior:

```text
use telescope
↓
open owned star chart/reference
↓
paper slides upward from bottom of screen
↓
move / rotate / mildly scale paper
↓
compare printed stars against real sky
↓
close paper or exit telescope
```

---

# 3. Item eligibility

Do not create a special telescope-only inventory.

The source of truth is the player's existing inventory/items.

Any owned item that represents a chart/reference and actually contains star imagery/data intended for visual sky comparison should be eligible.

Examples may include:

- celestial chart fragments,
- star-bearing chart scraps,
- quest star-reference cards,
- future charts that explicitly contain stars.

Eligibility should be driven by existing chart/reference/item semantics where possible.

If no clean existing capability/tag exists, add the smallest reusable capability/interface required rather than hardcoding specific prefab names.

Conceptual examples only:

```csharp
IStarReferenceViewable
```

or

```csharp
ChartReferenceCapabilities.ContainsStars
```

Do not use exact names without first auditing current item/chart architecture.

---

# 4. Existing debug implementation

The user reports that an existing debug key already provides related star-chart/reference viewing behavior.

Bosun must locate and inspect it before writing production behavior.

The debug implementation may contain useful existing logic for:

- selecting a chart/reference,
- rendering the item,
- mapping chart visuals into screen space,
- rotation,
- positioning,
- sky comparison.

Reuse/adapt working logic where appropriate.

Do not preserve debug-only architecture merely because it exists.

Production behavior must use normal inventory ownership and telescope-use state.

---

# 5. Opening behavior

When the player opens a star-bearing reference while using the telescope:

- the chart/card begins below the visible viewport,
- it animates/slides upward into view,
- the motion should feel like physically raising a paper/card into the player's line of sight.

This is a short presentation animation only.

No gameplay delay or timing challenge is required.

Opening should not automatically position the chart over a matching star pattern.

Default opening position should simply be a sensible lower/central viewing position.

---

# 6. Manual manipulation

While the reference is open, the player may manipulate it locally.

Required controls:

- reposition freely within the telescope view,
- rotate freely,
- mildly zoom/scale the reference in and out.

No snapping.

No auto-orientation.

No auto-centering on celestial objects.

No automatic scale match.

No hidden correction.

The player is responsible for visual alignment.

---

# 7. Scale range

The player may resize the reference within a modest range.

This represents physically holding the reference closer/farther from their face.

The range should be enough to make visual comparison practical, but not so large that the item becomes an arbitrary full-screen image viewer.

Exact min/max values should be serialized/tunable.

---

# 8. Opaque paper/card

The reference remains opaque.

Do not add a transparency/opacity control.

The intended interaction is:

- move the paper,
- rotate it,
- scale it,
- compare edges/features/stars by positioning it relative to the actual sky.

The player does not need to ghost the paper directly over the sky.

---

# 9. Local-only presentation

This entire comparison overlay is local to the telescope user.

Do not replicate:

- selected reference,
- reference position,
- rotation,
- scale,
- open/closed state.

Other players do not see the overlay.

Shared/authoritative state remains limited to the physical telescope/item/inventory ownership systems already used elsewhere.

---

# 10. Inventory ownership

A reference can only be opened through this mechanic if the local player actually owns/has access to that item through the normal inventory system.

Do not expose all known chart assets globally.

Do not create a debug-library browser in production.

If the item leaves the player's valid possession while open because of an authoritative inventory change, close the overlay safely.

---

# 11. Selecting the item

Use the normal inventory/item interaction path where practical.

Preferred behavior:

- player opens/selects the chart/reference item they own,
- if telescope mode is active and the item is star-viewable, it can be raised into the comparison view.

Do not require a separate telescope-specific list if the existing inventory already provides a natural item-selection interaction.

If current inventory architecture makes direct selection impractical, use the smallest possible temporary chooser backed strictly by eligible owned items.

Do not build a second inventory UI.

---

# 12. One reference at a time

Only one chart/reference item may be raised into the telescope view at once.

Opening another eligible reference should replace/close the previous one cleanly.

No multi-paper stacking in this mini-pass.

The physical cartography workbench remains the place for multi-fragment spatial assembly.

---

# 13. Session persistence

Reference transform state is temporary.

While the same reference remains open during the current telescope-use session:

- preserve its current position,
- preserve its rotation,
- preserve its scale.

When the player exits telescope use:

- close the overlay,
- discard temporary transform state.

Re-entering telescope use starts from the normal default raised position/rotation/scale.

Do not save telescope-overlay placement into world/save state.

---

# 14. Closing behavior

The reference should be closable without exiting telescope mode.

Closing may animate the paper back downward if cheap/clean.

Exiting telescope mode must always close the reference safely.

Also close safely if:

- telescope use is forcibly ended by sky obstruction,
- player loses telescope-use eligibility,
- scene changes,
- selected inventory item becomes invalid,
- UI/game state requires telescope exit.

No orphaned overlay state.

---

# 15. No gameplay solving

Hard rule:

> This system never determines whether the reference matches the sky.

Do not implement:

- pattern matching,
- tolerance checks,
- star-ID comparison,
- hidden solve score,
- “Correct” / “Incorrect” feedback,
- snap-to-sky,
- auto-rotation,
- auto-scale,
- auto-position,
- vibration/audio hints.

The player's eyes do the work.

---

# 16. No knowledge generation

Opening or aligning a reference through the telescope does not:

- chart stars,
- reveal celestial knowledge,
- validate a constellation,
- create chart evidence,
- complete charting progress,
- georegister the player's world position,
- reveal geography.

This is purely a visual comparison tool.

Other systems may later consume the player's interpretation through explicit gameplay actions, but this mini-pass does not add those actions.

---

# 17. Quest reference cards

Quest-provided star-reference cards are valid candidates if they are represented as owned items and contain star imagery/reference data.

They behave exactly like other eligible star-bearing references.

The telescope does not know or care whether a reference came from:

- charting,
- treasure,
- trader,
- quest,
- survey contract,
- future content.

Use item capability/semantics, not source-specific code.

---

# 18. Input coexistence

Audit current telescope controls and inventory controls before choosing final bindings.

The comparison manipulation controls must not break:

- existing RMB Look behavior,
- telescope zoom behavior,
- telescope exit,
- inventory item opening,
- general interaction controls.

Where input overlap exists, define telescope-reference manipulation as an explicit modal layer only while a reference is open.

Do not silently repurpose existing controls globally.

---

# 19. Camera behavior

Do not introduce a second camera.

The reference is a local presentation element layered over the existing telescope view.

It should follow the current telescope camera/look behavior naturally.

The sky/world remains physical and live behind the paper.

Weather/clouds/day-night/celestial motion continue normally.

---

# 20. UI / visual style

The reference should look like the actual chart/card item, not a generic UI panel.

Preserve:

- its paper/card visual identity,
- printed star layout,
- item-specific markings,
- orientation.

Avoid:

- modern HUD chrome,
- alignment grids,
- reticles dedicated to solving,
- bounding-box gizmos in player mode.

Developer/debug gizmos may exist behind explicit debug toggles if useful.

---

# 21. Multiplayer authority

This feature adds no new network-authoritative gameplay state.

Relevant split:

```text
Shared:
- physical telescope existence/deployment
- authoritative inventory ownership/item state

Local:
- telescope comparison overlay
- selected display reference
- paper transform
- animation
```

Do not send high-frequency transform replication for a purely local paper overlay.

---

# 22. Save/load

No new save persistence is required for comparison transform state.

Existing inventory/chart items save normally through their current systems.

If telescope mode is somehow active during a lifecycle transition, restore to a safe closed-overlay state rather than trying to save UI manipulation state.

---

# 23. Suggested implementation structure

Exact class names depend on live code.

Prefer a small local controller/presenter such as:

```text
TelescopeUse
    ↓
TelescopeReferenceComparisonController
    ↓
reads eligible owned item
    ↓
TelescopeReferencePresenter
```

This is conceptual only.

Reuse an existing debug/reference presenter if it already cleanly provides the needed behavior.

Do not create a giant new framework.

---

# 24. Single implementation checkpoint

## TELREF.1 — Productionize telescope reference comparison

Bosun should:

1. Audit existing telescope use.
2. Locate the existing debug-key implementation.
3. Audit current chart/reference item classes.
4. Audit current inventory item-open/select behavior.
5. Identify/reuse the smallest capability for “this item contains a star reference.”
6. Add local raised-paper presentation.
7. Add move/rotate/mild-scale interaction.
8. Keep paper opaque.
9. Support one reference at a time.
10. Close/reset safely with telescope/session lifecycle.
11. Verify no celestial/geographic knowledge changes.
12. Verify no new replication.
13. Compile/test.
14. Stop and report before any further expansion.

---

# 25. Acceptance tests

1. Player enters telescope mode.
2. Player can open an eligible owned star-bearing chart/reference.
3. Paper/card slides upward from bottom into the view.
4. Paper is opaque.
5. Player can reposition it freely.
6. Player can rotate it freely.
7. Player can mildly scale it.
8. No snapping occurs.
9. No match feedback occurs.
10. No auto-position/rotation/scale occurs.
11. One reference can be open at a time.
12. Another eligible reference can replace the current one cleanly.
13. Closing the reference leaves telescope mode active.
14. Exiting telescope mode always closes the reference.
15. Re-entering telescope resets temporary transform state.
16. Losing/removing the owned item closes the display safely.
17. Weather/day-night/sky movement remains live behind the reference.
18. Existing telescope controls still work.
19. No chart/celestial/world-map knowledge changes.
20. No telescope-reference transform/state is network replicated.
21. Another multiplayer client does not see the local overlay.
22. Existing debug functionality is either cleanly reused or remains safely isolated.

---

# 26. Non-goals

Do not add:

- celestial solve detection,
- star-pattern recognition,
- automatic chart validation,
- position-fixing,
- charting progress,
- constellation validation,
- multi-reference stacking,
- transparency controls,
- permanent overlay transform persistence,
- new telescope camera,
- inventory redesign,
- cartography-board behavior.

---

# 27. Final principle

The interaction should feel like:

> The player physically raises a chart into their telescope view and uses their own eyes and brain to compare it against the real sky.

Not:

> The game opens a puzzle UI and tells them when they have solved it.

---

**End of Telescope Chart / Reference Comparison mini-pass.**
