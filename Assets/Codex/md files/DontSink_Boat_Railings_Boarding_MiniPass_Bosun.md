# Don't Sink — Mini Pass: Boat Railings + Board/Unboard Reconciliation

**Implementation owner:** Bosun  
**Architecture/spec owner:** Keel  
**Project:** *Don't Sink*  
**Engine:** Unity 6.0, URP 2D, C#  
**Pass type:** Small gameplay / Boat Builder / traversal pass  
**Goal:** Add installable boat railings while preserving continuous board/unboard coverage along the vessel, with railings locally blocking traversal where they physically occupy the boat edge.

---

# 0. Read This First

This is intentionally a **small pass**.

The desired behavior is simple:

> The boat can have railing on the stern, bow, and the screen-facing side connecting them. Players may board/unboard anywhere along the boat edge that is not physically blocked by railing.

Do NOT solve this by manually splitting the existing board/unboard zones into many hand-authored segments.

Before modifying code, audit the exact current repository and identify:

- `DeckBoardZone` / current board-unboard implementation,
- current hold-to-board / hold-to-unboard logic,
- current deck snap logic,
- boarding-door interaction behavior,
- boat-local vs world-space traversal logic,
- current Boat Builder module system,
- structural module placement / rotation / persistence,
- collider/layer setup for installed modules,
- current interaction system used for toggles / doors / hatches,
- save/load path for installed module state,
- any current code that assumes the entire board-zone span is always traversable.

Use exact-current classes only.

> Never reconstruct an existing class from memory or an old handoff.

Preserve unrelated traversal behavior.

---

# 1. Physical Layout

The intended railing layout can include:

```text
             BOW RAIL
            ┌─────────┐
            │         │
SCREEN-FACING SIDE RAIL
════════════════════════════════
            │         │
            └─────────┘
            STERN RAIL
```

In actual side-view presentation, the important installed rail surfaces are:

- **bow rail**
- **stern rail**
- **screen-facing side rail**
- deliberate gaps in those rails for embark/disembark
- optionally a gate occupying one of those gaps

The opposite/far side does not need to be inferred merely because a near-side rail exists.

---

# 2. Core Traversal Rule

Keep the boat's broad board/unboard opportunity continuous.

Conceptually:

```text
DeckBoardZone:
==========================================================

Railing coverage:
      XXXXXXXX                XXXXXXXX

Effective crossing:
======        ================        =================
```

The broad zone still says:

> "A board/unboard interaction may be possible here."

A local railing says:

> "Not through this exact crossing point."

Do NOT resize, split, or destroy the broad zone as rail modules are installed.

---

# 3. Candidate-Crossing Validation

Boarding/unboarding should validate the actual intended crossing path or crossing point.

Do not use:

```text
"there is a railing somewhere inside the board zone"
-> block entire zone
```

Instead, determine the actual local crossing location and ask whether that crossing is blocked.

Conceptually:

```text
Player requests board/unboard
        |
        v
DeckBoardZone resolves candidate crossing point/path
        |
        v
Is this exact crossing obstructed by a traversal blocker?
        |
     yes/no
```

This is important at rail endpoints.

Example:

```text
RAILING
XXXXXXXXXXXX|
            | <- player immediately beside the endpoint
```

The player should be able to board/unboard just outside the railing footprint.

No giant invisible dead zones.

---

# 4. Railings Have Two Responsibilities

A railing should provide:

## A. Real physical collision

So:

- player cannot simply walk through it,
- thrown cargo hits it,
- loose items hit it,
- ordinary Rigidbody2D physics treats it as structure.

## B. Explicit traversal-blocking information

So the board/unboard system knows:

> this part of this boat edge cannot be crossed.

Do not rely exclusively on incidental collider overlap to decide board/unboard eligibility.

Physics collision answers physics questions.

Traversal-blocking data answers boarding questions.

These may share geometry/configuration, but should remain conceptually separate.

---

# 5. Suggested Reusable Seam

Use current project patterns and audit findings rather than blindly using this exact name.

Conceptually, a small component/interface like:

```text
BoatEdgeTraversalBlocker
```

may expose:

```text
- boat-local blocking footprint
- which crossing edge(s) it blocks
- whether currently active
```

Possible edge semantics:

```text
ScreenFacingSide
Bow
Stern
```

Do not create a complicated edge graph.

The first-pass need is only:

> "Does this installed structure block this candidate crossing?"

---

# 6. Boat-Local Geometry Rule

Railings are boat-mounted structures.

Author their blocking geometry in **boat-local space**.

Resolve current crossing geometry in world space only when needed.

Do not encode world-up/world-left assumptions that fail when the boat pitches, rolls, or capsizes.

The existing project rule applies:

> Boat-mounted traversal geometry is authored boat-local; traversal semantics resolve against current world-space orientation.

The current board/unboard behavior after capsize should remain intact except where a railing physically blocks the relevant crossing.

---

# 7. Bow and Stern Rails

Bow and stern railings are not special-case game logic.

They are just blockers covering those corresponding vessel edges.

Expected behavior:

```text
closed bow rail
-> cannot board/unboard through bow edge at that segment

closed stern rail
-> cannot board/unboard through stern edge at that segment
```

If there is a deliberate open gap at bow/stern, crossing through the gap remains possible.

Do not hardcode:

```text
bow = always blocked
stern = always blocked
```

Installed geometry determines local availability.

---

# 8. Screen-Facing Side Rail

The screen-facing side rail may run across most of the vessel length.

It should block the ordinary side board/unboard crossing wherever present.

Gaps remain usable.

Example:

```text
SIDE RAIL
XXXXXXXXXX        XXXXXXXX       XXXXXXXXXX
          GAP             GAP
==========|================|================
       board here       board here
```

The board zone remains continuous under the entire length.

The rail blocker locally suppresses the interaction.

---

# 9. Gates

The architecture should support a rail gate cleanly.

Preferred first-pass rule:

```text
Gate Closed
- physical collider active in blocking position
- traversal blocker active
- board/unboard unavailable through gate

Gate Open
- physical obstruction removed/moved out of crossing
- traversal blocker inactive
- board/unboard available through gate
```

If the current interaction/module architecture makes a simple gate inexpensive, implement a minimal toggleable gate now.

A first-pass gate does NOT need:

- elaborate animation,
- hinge physics,
- damage,
- locking,
- key requirements,
- powered operation.

A simple open/closed state is sufficient.

If adding a full gate prefab would materially enlarge this mini-pass, still ensure the railing/traversal-blocker architecture supports it without redesign.

---

# 10. Gate State Must Have One Source of Truth

Do not let physics and traversal disagree.

Bad:

```text
gate visual says open
collider says closed
board system says maybe
```

Preferred:

```text
GateState.Open
    -> visual/presentation
    -> physical blocking state
    -> traversal blocking state
```

Use the current module-state/persistence pattern if one already exists.

---

# 11. Prompt Behavior

When the player is inside the broad board/unboard zone but their crossing is blocked by railing:

- do NOT show the normal board/unboard prompt for that blocked crossing,
- do NOT let hold-to-board begin and then fail after the timer,
- avoid prompt flicker at rail endpoints.

If a gate is interactable, its own interaction prompt should follow the current interaction priority system.

Do not let:

```text
Hold E to Board
```

mask:

```text
Press E to Open Gate
```

or vice versa incorrectly.

Use the current prompt-priority architecture rather than inventing a second system.

---

# 12. Boarding Door Compatibility

The current boarding-door behavior must continue to work.

Audit whether the existing boarding door:

- overlaps the broad board zone,
- has special prompt restoration logic,
- has its own traversal permission,
- uses a separate collider or interaction path.

Do not regress it while adding generic rail blocking.

If the boarding door is itself effectively an opening in a rail/wall, prefer integrating through the same local crossing-availability concept where practical.

Do not forcibly rewrite the door system if current behavior is stable.

---

# 13. Physical Collision

Rail colliders should behave as normal boat structure.

Expected:

- player cannot walk through a closed rail,
- thrown cargo collides with it,
- loose cargo/items collide with it,
- no bespoke "cargo hit railing" behavior required,
- physics handles the result.

Use existing structural/module physics layers when appropriate.

Do not invent a special railing collision layer unless the current collision matrix requires one.

---

# 14. Boat Builder Integration

Railings should be installable structural modules using the current Boat Builder architecture.

Support at least the concepts needed for:

- side rail segment,
- bow rail segment,
- stern rail segment,
- optional gate segment.

Use current placement/grid/rotation rules.

Do not create a separate railing editor if normal module placement can express this.

A railing segment should persist like other installed modules.

If gate state is implemented, persist open/closed state using the current module-state mechanism.

---

# 15. Placement / Coverage

The traversal blocking footprint should correspond closely to the physical rail span.

Do not make the blocking footprint dramatically wider than the actual rail.

Endpoint precision matters because intended embark/disembark gaps may be narrow.

However, exact sub-pixel matching is unnecessary.

Prefer a small, clear authored footprint over complicated runtime geometry inference.

---

# 16. Multiplayer / Authority

Use the current multiplayer authority architecture.

Shared/authoritative state includes:

- installed rail modules,
- gate open/closed state if implemented,
- resulting physical collider state,
- resulting traversal availability.

A client should not locally decide that a closed authoritative gate is open for boarding.

Board/unboard remains an authenticated gameplay intent under existing rules.

Do not implement new networking infrastructure for railings.

---

# 17. Save / Load

Installed rail modules should restore through the current boat/module persistence system.

After load:

```text
rail exists
-> physical collider correct
-> traversal blocker correct
```

If gate state is implemented:

```text
saved gate open
-> reload open
-> no physical block
-> no traversal block

saved gate closed
-> reload closed
-> physical + traversal block active
```

No separate railing save database.

---

# 18. Required Validation Scenarios

## A. No rails

Boat has no railing.

Expected:

- existing board/unboard behavior remains materially unchanged along the full valid boat length.

## B. Side rail segment

Install one screen-facing side rail.

Expected:

- walking through it is physically blocked,
- board/unboard through that exact section is unavailable,
- board/unboard immediately outside the rail footprint still works.

## C. Long side rail with gap

Install rail segments with a deliberate gap.

Expected:

```text
rail -> blocked
gap  -> board/unboard works
rail -> blocked
```

No need to manually create multiple board zones.

## D. Bow rail

Install bow rail.

Expected:

- physical crossing through installed bow rail is blocked,
- board/unboard candidate through that covered bow edge is rejected,
- nearby uncovered crossing remains available if geometry permits.

## E. Stern rail

Same expectations as bow.

## F. Rail endpoint

Stand near the endpoint of a side rail.

Expected:

- blocked when candidate crossing intersects rail,
- allowed as soon as candidate crossing is outside blocker footprint,
- no oversized invisible restriction.

## G. Gate closed

If gate implemented:

Expected:

- collider active,
- crossing unavailable,
- gate interaction available according to normal prompt priority.

## H. Gate open

Expected:

- crossing becomes available,
- player can board/unboard through gate,
- no stale invisible blocker remains.

## I. Capsized / rotated boat

Test railing + board/unboard while vessel is significantly rotated/capsized.

Expected:

- blockers follow boat-local geometry,
- no world-up assumption,
- existing capsize traversal semantics remain correct.

## J. Thrown cargo

Throw cargo at railing.

Expected:

- ordinary Unity physics collision,
- no pass-through caused by traversal logic,
- no special scripted impact required.

## K. Save/load

Save with installed rails and, if applicable, an open/closed gate.

Reload.

Expected:

- rail placement restored,
- collider state correct,
- traversal blocking correct,
- gate state restored if supported.

## L. Multiplayer

Player A opens/closes gate or uses a rail gap.

Player B observes the same structural state.

Expected:

- authoritative/shared state agrees,
- no client-only traversal hole.

---

# 19. Explicitly Out of Scope

Do NOT bundle:

- climbing over railings,
- vaulting,
- jumping-over-rail detection,
- rail damage,
- rail breakage,
- cargo damaging rails,
- player damage from rails,
- procedural rail generation,
- fancy gate animation,
- hinge physics,
- locks/keys,
- electrical gates,
- AI traversal,
- full deck-edge navmesh,
- replacing the existing board/unboard system.

---

# 20. Architecture Smells to Avoid

Avoid:

```text
rail installed
-> resize DeckBoardZone
```

Avoid:

```text
rail installed
-> create a new board zone on each side
```

Avoid:

```text
if any railing overlaps broad zone
-> disable whole broad zone
```

Avoid using current world orientation as the authored rail-edge identity.

Avoid physics-collider overlap as the ONLY source of traversal semantics if a small explicit blocker footprint is cleaner.

Avoid gate visual state, collider state, and traversal state becoming independent booleans.

---

# 21. Final Design Statement

The boat should have a broad continuous opportunity to board/unboard along its usable perimeter.

Installed structure locally determines whether the player can actually cross there.

In short:

> **Board/unboard zone says "you may cross somewhere here."  
> Railing says "not through me."  
> Gap/gate says "cross here."**

Railings are ordinary physical boat structure.

Board/unboard remains one coherent traversal system.

Do not fracture the boat into manually maintained boarding slices just because humans invented fences.

---

**End of mini Bosun handoff.**
