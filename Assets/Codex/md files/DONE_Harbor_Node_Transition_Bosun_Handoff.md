# DON'T SINK — Harbor / Node Transition Bosun Handoff
## Boat Position, Harbor Approach, Docking, Embark Spawn, and NodeScene Transition Semantics

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature family:** World travel / node transitions / harbor approach  
**Scope:** Small focused pass  
**Purpose:** Establish a clean contract for how a boat approaches a node, docks, transitions into NodeScene, and re-embarks into BoatScene without relying on vague node-center proximity or unsafe arbitrary warps.

---

# 0. NON-NEGOTIABLE PROJECT RULES

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized state, authority hooks, save compatibility, and multiplayer hardening.
3. Reuse current travel, scene-transition, node, world-position, boat-state, and interaction systems where possible.
4. Prefer additive support classes/data over invasive rewrites.
5. Do not touch discovery behavior beyond leaving a clean seam for a future dedicated discovery pass.

---

# 1. CORE SEMANTIC CHANGE

The authoritative world-map position of a node should mean:

> **NodePosition = HarborPosition**

Not town center.  
Not some abstract settlement centroid.  
Not an offshore placeholder.

The node's world coordinate is the canonical harbor / shoreline arrival location.

This becomes the anchor for:
- harbor approach,
- harbor proxy visuals,
- docking geometry,
- departure geometry,
- transition positioning,
- future discovery logic,
- future celestial/location-aware content.

---

# 2. HIGH-LEVEL TRANSITION MODEL

## Docking

```text
BoatScene
    ↓
boat approaches node harbor
    ↓
harbor proxy becomes visible
    ↓
guidance appears at close range
    ↓
boat enters docking/berth zone
    ↓
E — Dock
    ↓
dock request accepted
    ↓
transition to NodeScene
    ↓
authoritative true world position = NodePosition / HarborPosition
    ↓
boat placed in NodeScene's dock/harbor representation
```

## Embarking

```text
NodeScene
    ↓
player chooses Embark
    ↓
transition to BoatScene
    ↓
boat appears at safe offshore DepartureAnchor
    ↓
authoritative true world position = DepartureAnchor world coordinate
    ↓
velocity = 0
angular velocity = 0
throttle = 0
```

The embark warp is intentional.

It represents converting from the detailed NodeScene harbor representation into the BoatScene travel representation with enough offshore clearance to avoid immediate re-docking, shoreline overlap, or terrain collision.

---

# 3. HARBOR GEOMETRY

Each generated node should own deterministic harbor approach data.

Conceptually:

```text
HarborDefinition
- HarborPosition        // same as NodePosition
- WaterwardDirection
- Berth / Docking Zone
- Approach Corridor
- Departure Anchor
- Guidance Range
- Proxy Visibility Range
- Boat-scale tuning multipliers
```

Exact class names should follow current project conventions.

The important point is that the harbor geometry is deterministic and belongs to the node/world truth, not recreated differently every time a scene loads.

---

# 4. PROCEDURAL WATERWARD DIRECTION

## 4.1 Why this exists

The node itself should already be generated at a valid shoreline/harbor location.

The waterward-direction calculation is primarily there to answer:

> **Which direction from this harbor provides the best continuous navigable stretch of water?**

This direction drives:
- berth placement,
- approach guidance,
- departure anchor,
- safe offshore spawn direction.

## 4.2 Do not use a single point sample

Do not merely sample one water point in each direction.

A candidate heading should be scored based on a **continuous water corridor**.

Conceptually:

```text
for candidate directions around HarborPosition:
    sample a corridor extending outward
    evaluate:
        continuous water coverage
        minimum depth
        land intersections
        major obstruction intersections
        usable width
        clearance length

choose best-scoring navigable-water direction
```

The algorithm should favor the longest/cleanest continuous open-water approach.

This prevents:
- pointing through a peninsula,
- choosing a tiny inlet/puddle,
- spawning the boat behind an island,
- sending the approach lane inland.

## 4.3 Determinism

Once chosen, `WaterwardDirection` must be deterministic for the generated node.

It should not vary based on scene load, current boat, random runtime state, or which player arrives first.

---

# 5. BOAT-SCALED HARBOR GEOMETRY

Harbor direction is node-authored/generated.

Actual berth/departure dimensions should scale from the **current boat's physical size**.

Use the current boat's authoritative bounds/length source if one already exists.

Avoid hardcoded fixed-world-unit zones that may fit the starter boat but fail for later hulls.

Conceptually:

```text
BoatLength = authoritative current boat length

Berth dimensions
    = BoatLength × configurable multiplier(s)

Berth seaward offset
    = BoatLength × configurable multiplier

DepartureAnchor offset
    = BoatLength × larger configurable multiplier
```

This should support:
- tiny starter vessel,
- larger upgraded hull,
- future absurd floating architecture.

Inspector tuning is preferred over hardcoded values.

---

# 6. DOCKING ZONE / BERTH

The actual docking area should be a small but forgiving **water-space berth zone** near the harbor.

It does not need to correspond to a physical collidable pier yet.

The first-pass rule is intentionally simple:

> **Boat reference point inside berth zone + player presses E = valid dock.**

Do not initially require:
- heading alignment,
- low speed,
- exact hull alignment,
- port/starboard side,
- physical hatch alignment,
- zero throttle,
- touching a dock collider.

These may be reconsidered only after playtesting.

The goal is to add some physical piloting requirement without adding mandatory tedium to routine trade runs.

---

# 7. DOCK INTERACTION

## 7.1 Explicit interaction required

Docking never triggers automatically.

Entering the berth makes a normal interaction available:

> **E — Dock**

Use the current interaction/prompt architecture.

Do not trigger a scene transition merely because the boat:
- overlaps the zone,
- touches shoreline,
- collides with something,
- passes nearby.

If the player barrels through the berth at speed and manages to hit E during the valid window, that is acceptable for the first pass.

## 7.2 Dock request lifecycle

On successful interaction:

1. lock/resolve the target NodeId;
2. prevent duplicate concurrent dock requests;
3. begin normal scene transition;
4. load NodeScene;
5. set authoritative true world position to that NodePosition / HarborPosition;
6. place the boat using NodeScene's dock/harbor spawn representation;
7. restore normal NodeScene state.

No pre-transition physical braking simulation is required.

---

# 8. EMBARK LIFECYCLE

On Embark from NodeScene:

1. resolve the current node's HarborDefinition;
2. derive current boat-scaled DepartureAnchor from HarborPosition + WaterwardDirection;
3. validate/fallback if needed;
4. transition to BoatScene;
5. place boat at DepartureAnchor;
6. set authoritative true world position to the DepartureAnchor global coordinate;
7. set linear velocity = 0;
8. set angular velocity = 0;
9. set throttle = 0;
10. restore normal BoatScene control.

This prevents:
- immediate re-docking,
- shoreline overlap,
- land spawn,
- inherited throttle launching the boat unexpectedly,
- retained angular spin across the scene transition.

---

# 9. SAFE DEPARTURE VALIDATION

The generated departure point must be validated as navigable water.

At minimum check:
- water, not land;
- sufficient depth for current boat;
- no major terrain overlap;
- adequate hull clearance;
- corridor from harbor toward departure does not cross land.

Prefer generated harbor geometry that is valid by construction.

A deterministic fallback search is acceptable for corrupted/missing edge cases, but runtime should not normally hunt randomly for somewhere to put the boat.

Useful diagnostics:

> `Departure Anchor is not in navigable water.`

or equivalent debug logging.

---

# 10. NODE VISUALS IN BOATSCENE

All nodes currently share the same detailed NodeScene.

BoatScene therefore needs a lightweight visual representation so the entire crew can see the settlement they are approaching.

Do **not** reproduce the exact NodeScene building layout.

Use the node's existing broad metadata to generate/select a **distant harbor/town proxy**.

Potential inputs:
- node archetype,
- prosperity,
- trade rating,
- dock rating,
- security,
- stability,
- deterministic visual seed,
- other existing node-state metadata.

Potential outputs:
- settlement silhouette family,
- approximate size,
- harbor visual style,
- cranes/towers/tanks,
- light density,
- rough material/style family,
- fortification impression,
- dock prominence.

The goal is broad continuity:

> A poor ramshackle settlement should look poor/ramshackle from offshore and still feel like the same type of place after NodeScene loads.

Exact building correspondence is unnecessary.

---

# 11. HARBOR PROXY PRESENTATION

The harbor/town proxy is presentation-only in the first pass.

It does **not** need physical collision.

## 11.1 Visibility

Any sufficiently nearby node may become visible as a physical/distant settlement proxy.

Do not make settlement visibility depend on active route selection.

Nodes should not disappear merely because the player is not navigating to them.

## 11.2 Distance source

Proxy appearance uses **true geographic distance to NodePosition / HarborPosition**.

Do not use:
- route progress,
- believed position,
- selected-route percentage.

If the player is lost, the proxy should behave according to where they actually are.

## 11.3 Scale

Use a configurable distance-to-scale curve rather than fake physical-perspective math.

Expose:
- far/min apparent scale,
- near/max apparent scale,
- visibility range,
- scaling curve.

Smoothness/readability is more important than optical realism.

## 11.4 Relative bearing

Proxy horizontal presentation should respond to the harbor's relative bearing from the boat.

The mapping may be stylized/smoothed.

Do not chase physically perfect perspective if it causes jitter.

Required property:

> As the boat turns or passes the harbor, the visible settlement should move smoothly and intuitively across the background.

Use interpolation/smoothing to avoid snapping.

---

# 12. HARBOR GUIDANCE

The player generally already knows roughly where they are going before close approach.

Guidance exists only to solve the final approach problem.

## 12.1 Guidance range

When the boat enters a configurable close **Harbor Guidance Range**, display simple approach guidance.

Nodes should be spaced far enough apart that multiple simultaneous harbor-guidance targets should not occur under normal world generation.

If two nodes qualify at once:
- treat it as a generation/configuration diagnostic;
- do not overbuild multi-harbor traffic-control UI.

## 12.2 First-pass visuals

Anything simple is acceptable.

For example:
- translucent guide centerline,
- evenly spaced chevrons,
- guide markers,
- simple berth outline.

The visual may be debug-quality initially.

The important thing is that the player can clearly answer:

> "Where exactly does the game want me to put this enormous boat?"

## 12.3 No autopilot

Guidance:
- does not steer,
- does not brake,
- does not snap,
- does not capture the boat,
- does not calculate an approach for the player beyond showing the generated corridor.

It is informational only.

---

# 13. RANGE LAYERS

The exact values should be Inspector/config driven and tuned by feel.

Conceptually:

```text
Far world visibility
    → island / terrain may already be visible

Harbor proxy range
    → metadata-driven settlement silhouette appears

Harbor guidance range
    → approach guide appears

Berth range
    → E — Dock becomes available
```

A separate formal **node discovery/recognition system is explicitly NOT part of this pass**.

Do not decide when an unknown node becomes discovered here.

Leave a clean hook for the later discovery pass.

---

# 14. UNKNOWN NODE PRESENTATION

Even if node discovery/knowledge logic is not finalized yet:

> Physical map knowledge must not make a real settlement visually cease to exist.

If current node knowledge state allows an unknown settlement to be rendered without prematurely "discovering" it, it may appear as an unidentified settlement proxy.

Do not add permanent discovery-state mutations in this pass.

If this cannot be integrated cleanly without defining discovery semantics, keep the rendering seam ready and leave actual unknown-node behavior to the dedicated discovery pass.

---

# 15. NODESCENE MIRRORING

The reusable NodeScene may mirror its broad left/right orientation based on the generated global harbor orientation **if this can be done cleanly**.

Desired simplification:

- reduce the 360° global `WaterwardDirection` into one of two NodeScene side orientations;
- one orientation has water/dock on the left and town inland to the right;
- the mirrored orientation has water/dock on the right and town inland to the left.

This is only for visual continuity.

Do not attempt to reproduce arbitrary global compass orientation in the 2D side-scrolling NodeScene.

## 15.1 Mirror layout, not blindly every sprite

Prefer mirroring:
- layout positions,
- spawn directions,
- harbor/town sides,
- local navigation flow.

Avoid blindly negative-scaling the entire scene if that would:
- reverse text,
- invert asymmetric art incorrectly,
- break UI,
- reverse sprites that should retain orientation.

Use a deliberate layout mirror mode.

## 15.2 If mirroring proves invasive

Mirroring is desirable, but it should not destabilize NodeScene generation.

If the current generator cannot support it cleanly:
- document the limitation,
- preserve a canonical orientation temporarily,
- do not derail the harbor-transition pass.

---

# 16. ONE HARBOR PER NODE

For this pass:

> Each node has exactly one harbor.

Do not support:
- multiple piers,
- faction-specific docks,
- north/south harbors,
- alternate berths.

The data model may be cleanly extensible later, but do not complicate the implementation now.

---

# 17. NO PHYSICAL PIER REQUIRED IN BOATSCENE

The first pass does not require a collidable physical dock/pier in BoatScene.

The harbor proxy may visually contain:
- piers,
- cranes,
- shore structures,
- lights,
- buoys,
- other dock-like presentation.

Mechanically:
- docking is governed by the water-space berth zone,
- transition occurs on explicit E interaction.

This intentionally avoids requiring BoatScene shoreline/dock geometry to exactly match detailed NodeScene geometry.

---

# 18. WORLD POSITION SEMANTICS

## While docked in NodeScene

```text
TrueWorldPosition = NodePosition = HarborPosition
```

## Immediately after Embark

```text
TrueWorldPosition = DepartureAnchorWorldPosition
```

This means the boat truth moves offshore as part of embark.

Do not keep true position pinned to the node while the physical BoatScene vessel is already offshore.

## On successful Dock

The true position becomes the exact node/harbor coordinate as part of the docking transition.

---

# 19. FUTURE WRAPPED-WORLD COMPATIBILITY

A separate future/world-topology pass may establish wrapped global X.

Harbor calculations should avoid unnecessary assumptions that:
- east/west world edges are permanent,
- raw X subtraction is always correct.

Where current topology services exist, use them.

Do not expand this pass into the full wrapped-world conversion.

---

# 20. MULTIPLAYER / AUTHORITY

Consequential state should remain host-authoritative/requester-aware.

Shared consequential actions include:
- dock request,
- target NodeId,
- transition start,
- authoritative true world position update,
- boat deployment/spawn state.

Presentation-only data such as:
- harbor proxy interpolation,
- guide rendering,
- smoothing

may be local client rendering of authoritative geometry/state.

Prevent duplicate dock requests from two players from triggering multiple transitions.

---

# 21. DEBUG / AUTHORING SUPPORT

Useful debug visualization:
- HarborPosition / NodePosition,
- WaterwardDirection arrow,
- sampled candidate waterward corridors,
- selected best corridor,
- Berth zone,
- Guidance corridor,
- DepartureAnchor,
- boat-scaled berth dimensions,
- harbor proxy range,
- guidance range.

Useful validation warnings:
- no valid waterward corridor,
- departure anchor on land,
- berth not in navigable water,
- corridor crosses land,
- overlapping harbor guidance ranges for two nodes,
- boat bounds unavailable for scaling.

---

# 22. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## HT.1 — Harbor truth / generation
- NodePosition semantic becomes HarborPosition
- procedural waterward-direction solver
- corridor scoring
- deterministic HarborDefinition
- debug gizmos

**STOP FOR TESTING.**

## HT.2 — Boat-scaled berth / departure
- current boat-size query
- berth generation
- departure anchor
- safe-water validation
- embark spawn reset
- true-position updates

**STOP FOR TESTING.**

## HT.3 — Dock interaction / transition
- berth overlap eligibility
- E — Dock
- explicit scene transition
- true position becomes NodePosition
- no heading/speed gate
- duplicate request protection

**STOP FOR TESTING.**

## HT.4 — Harbor proxy / guidance
- metadata-driven proxy family
- true-distance scaling
- smoothed relative-bearing placement
- close-range guidance
- simple berth visualization

**STOP FOR TESTING.**

## HT.5 — NodeScene mirror / regression
- clean left/right NodeScene mirror if practical
- save/load/transition regression
- authority regression
- document future discovery seam

**STOP FOR FINAL TESTING.**

---

# 23. NON-GOALS

Do not implement:
- node discovery rules,
- automatic discovery radius,
- multiple harbors per node,
- heading requirement for docking,
- speed requirement for docking,
- exact hull alignment,
- Barotrauma-style docking hatch alignment,
- port/starboard-specific docking,
- physical BoatScene pier collision,
- docking damage,
- docking minigame,
- autopilot,
- final harbor art,
- wrapped-world conversion,
- piloting redesign.

---

# 24. ACCEPTANCE TESTS

The pass is ready when:

1. NodePosition is treated as HarborPosition.
2. Each node has one deterministic HarborDefinition.
3. WaterwardDirection is selected from the best continuous navigable-water corridor.
4. Direction generation does not choose a tiny isolated water sliver when better open water exists.
5. Berth geometry scales from current boat size.
6. DepartureAnchor scales from current boat size.
7. Berth and departure geometry stay seaward of the harbor.
8. DepartureAnchor validates as navigable water.
9. Approach corridor does not intentionally cross land.
10. Entering berth makes E — Dock available.
11. Docking requires no heading gate.
12. Docking requires no speed gate.
13. Docking never triggers automatically.
14. Successful Dock transitions to NodeScene.
15. Successful Dock sets true world position to NodePosition.
16. Embark transitions to BoatScene at DepartureAnchor.
17. Embark sets true world position to DepartureAnchor.
18. Embark resets linear velocity to zero.
19. Embark resets angular velocity to zero.
20. Embark resets throttle to zero.
21. Nearby node harbor proxies can appear without active route selection.
22. Proxy scale uses true distance.
23. Proxy horizontal placement responds smoothly to relative bearing.
24. Proxy presentation derives from broad node metadata rather than exact NodeScene building layout.
25. Guidance appears only at close harbor range.
26. Guidance is informational only.
27. Berth is visually understandable from guidance.
28. No physical BoatScene dock collider is required.
29. NodeScene mirroring works if cleanly supported.
30. Discovery state is not silently invented or mutated by this pass.
31. Existing travel/save/world-position behavior remains intact.

---

# 25. FUTURE TODO — NODE DISCOVERY PASS

Explicitly defer:

- when an unknown node becomes discovered,
- whether visual recognition alone is sufficient,
- discovery range,
- island vs settlement knowledge,
- map-marker creation,
- rumored/partial/known relationships,
- discovery notifications,
- knowledge sharing in multiplayer.

Harbor rendering must leave a clean seam for this later pass but must not define those rules now.

---

# 26. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:
1. Exact files modified
2. Exact new files
3. Node/harbor data added
4. Inspector tuning added
5. Topography/water-corridor scoring method
6. Boat-size source used
7. True-position transition changes
8. Scene-transition changes
9. Authority changes
10. Debug gizmos/tools
11. Regression tests
12. Any live-code discrepancy from this handoff

Do not expand scope into discovery or full docking difficulty.

---

**End of Harbor / Node Transition handoff.**
