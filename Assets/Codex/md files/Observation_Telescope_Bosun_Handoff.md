# DON'T SINK — Observation Telescope Bosun Handoff
## Physical Sky-Observation Interactable

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Feature:** Observation Telescope  
**Scope:** Small star-charting completion pass

## 0. Non-negotiable project rules

### Exact-current-class rule
If modifying an existing class, inspect and use the exact latest version currently present in the project. Never reconstruct an existing class from memory, old handoffs, or assumptions.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized state, compatibility shims, authority hooks, and existing multiplayer hardening.
3. Reuse current interaction, persistence, camera, input, and authority patterns where possible.
4. Prefer additive support classes over invasive rewrites.
5. Do not regress Phase 6/7 map-table or celestial systems.

### Multiplayer shape
The intended future model is a shared, host-authoritative boat. For this feature:
- physical telescope placement/deployment is shared physical state;
- telescope viewing is local presentation state;
- one player observing must not hide the boat or alter the camera for other players.

---

# 1. Feature definition

The Observation Telescope is:
- a carryable physical item;
- placeable on the boat;
- pinnable/deployable on a valid boat surface;
- usable only while properly placed/deployed;
- gated by a clear-enough sky volume above it;
- a presentation tool only.

It does **not**:
- create chart fragments;
- consume chart paper;
- create celestial evidence;
- reveal hidden celestial truth;
- start the charting minigame;
- brighten stars;
- pierce clouds/fog/weather;
- use a telescope scope/vignette;
- create a new camera.

Its purpose is simply:

> Let the player clearly see the existing sky without the boat, player, telescope, or onboard objects visually blocking it.

---

# 2. Basic player flow

```text
pick up telescope
      ↓
place it on valid boat surface
      ↓
pin/deploy it
      ↓
press E to use
      ↓
perform upward sky-clearance box check
      ↓
blocked?
   yes → "No unobstructed view of sky."
   no  → enter Observation View
      ↓
fully hide boat + onboard visuals + player + telescope
      ↓
reuse existing camera and existing RMB Look behavior
      ↓
allow subtle zoom, about 1.0x–1.5x
      ↓
E or Escape exits
      ↓
restore hidden visuals/camera state exactly
```

---

# 3. Physical item / placement architecture

## 3.1 Reusable placeable boat-equipment seam

Do not force this directly into cargo securing unless the exact current architecture clearly makes that the correct abstraction.

Preferred design intent: create/reuse a lightweight general concept for **non-module placeable boat equipment**.

Conceptually:

```text
PlaceableBoatEquipment

Carried
   ↓
Placed on valid boat surface
   ↓
Pinned / deployed
   ↓
Usable
```

The telescope is the first consumer, but the seam should support future loose-but-deployable boat equipment.

This is not:
- a BoatBuilder-installed module;
- cargo securing rope gameplay;
- a securing minigame;
- permanent welding to the boat.

## 3.2 Future impulse seam

Important future design rule:

> **Nothing in the boat is ever truly safe.**

Do not implement impulse knock-loose/knock-over behavior now.

However, deployed/pinned state must leave room for future systems to:
- loosen equipment;
- knock it over;
- unseat it;
- move it;
- potentially break deployment.

Avoid encoding `Pinned = permanently immutable forever`.

## 3.3 Multiple telescopes

Multiple telescope instances are allowed. No singleton assumption.

---

# 4. Use requirements

The telescope can be used only when:
- it is placed on the boat;
- it is in the required deployed/pinned state;
- it currently has sufficient upward sky clearance.

It cannot be used while:
- carried;
- loose/unplaced;
- improperly deployed;
- obstructed.

Use the project's current interaction/prompt system.

Expected interaction:
- `E` enters Observation View when valid;
- `E` exits while observing;
- `Escape` also exits.

No additional UI is required.

---

# 5. Sky-clearance check

## 5.1 Use an upward box volume, not a ray

A tiny sliver of visible sky is not sufficient.

Implement/reuse a configurable upward box/volume check originating from the telescope.

Expose something equivalent to:

```text
Sky Clearance
- Check Width
- Check Height
- Obstruction Layers
- Check Interval
```

Use a clean 2D physics implementation matching the current project.

## 5.2 What counts as obstruction

Anything physically occupying the configured clearance volume should count when on an included obstruction layer.

Examples:
- ceiling;
- upper deck;
- boat wall close above/beside telescope;
- boat module;
- placed equipment;
- other nearby physical obstruction.

A wall far down the hull and outside the configured upward box must **not** block use.

## 5.3 Error message

If use is attempted without sufficient clearance:

> **No unobstructed view of sky.**

Use current prompt/message conventions.

## 5.4 Re-check while in use

Validate clearance:
1. when entering Observation View;
2. periodically/continuously while observing.

If clearance becomes obstructed:
- automatically exit Observation View;
- restore visuals/camera state;
- show `No unobstructed view of sky.`

## 5.5 Reusable clearance component/query

Prefer a small reusable sky/overhead-clearance component or query if it naturally fits current architecture. Future equipment may need similar clearance rules. Do not over-engineer a giant framework.

---

# 6. Observation View

## 6.1 Use the existing camera

Do not create:
- telescope camera;
- scope camera;
- separate celestial camera.

Reuse the current game camera.

Observation View is a local camera/presentation state.

## 6.2 What becomes hidden

While Observation View is active, fully hide visual elements belonging to the boat/onboard scene context, including:
- boat visuals;
- boat modules;
- hull/interior/exterior boat renderers;
- onboard items;
- player character;
- telescope itself;
- other visuals physically part of/on the boat that would block the sky.

Mental model:

> The player is looking at the actual sky from this position, but the boat itself is not visually blocking the view.

## 6.3 Fully hidden means fully hidden

Steady-state observation should be fully invisible, not ghosted.

A short fade transition is acceptable if simple and compatible with current presentation architecture.

## 6.4 What remains visible

Do **not** hide environmental/world obstruction.

Keep:
- islands;
- terrain;
- cliffs;
- world geometry;
- clouds;
- fog;
- weather;
- sun;
- moon if applicable;
- sky;
- celestial objects that are actually rendered;
- non-boat environmental silhouettes.

If a giant island blocks the sky, it stays.

## 6.5 Safe restoration

When Observation View ends, restore every affected renderer/visual to its exact prior state.

Do not blindly set all renderers to enabled=true.

Use a reversible local presentation snapshot/state or equivalent.

Restoration should occur on:
- E exit;
- Escape exit;
- newly obstructed clearance;
- invalidated telescope/user state;
- scene teardown if needed.

---

# 7. Look / pan behavior

Reuse the existing **hold RMB Look** functionality.

Do not invent telescope-specific panning.

The telescope does not:
- rotate celestial truth;
- change boat heading;
- change observation coordinates;
- create a bearing;
- record a direction.

---

# 8. Zoom

Allow only subtle zoom.

Target range:
- minimum around `1.0x`;
- maximum around `1.5x`.

Expose min/max in Inspector/settings instead of hardcoding.

Use current camera/input conventions where possible. Mouse wheel is acceptable if no existing zoom action fits.

The zoom is intentionally modest:
- no sniper scope;
- no extreme astronomy magnification;
- no alternate star rendering.

Preferred:
- snapshot prior camera zoom/state;
- apply Observation View zoom locally;
- restore previous camera state on exit.

No observation zoom state persists.

---

# 9. Day / night / weather

The telescope is usable at any time.

Do not night-gate it.

The telescope does not:
- make stars visible in daylight;
- remove clouds;
- remove fog;
- pierce weather;
- reveal otherwise hidden celestial objects.

Whatever the existing sky renderer would show remains what the player sees.

---

# 10. Phase 7 constellation compatibility

If Phase 7 provides knowledge-gated constellation visualization through the existing Look behavior, Observation View should remain compatible with that same system.

Do not create telescope-specific constellation logic.

Rules remain:
- Known constellation behavior may appear through normal Look;
- Hidden constellations remain hidden;
- Debug overrides follow existing celestial debug rules.

---

# 11. Local vs shared state

## Shared physical state
Use normal shared boat-item persistence/authority for:
- telescope item identity;
- position;
- rotation;
- placed/deployed/pinned state;
- future physical-condition fields.

## Local transient state
Keep local:
- whether this player is observing;
- camera look/pan;
- observation zoom;
- locally hidden renderer state.

One player's Observation View must not affect another player's presentation or camera.

---

# 12. Persistence

## Physical telescope persists

Use the existing physical item / boat-item persistence architecture.

Persist as appropriate:
- item identity;
- position;
- deployment/pinned state.

Do not invent a separate telescope save system if existing item persistence already covers this.

## Observation session does not persist

Do not save:
- observing/not observing;
- pan/look offset;
- observation zoom;
- temporary hidden-visual state.

Nobody should load directly into Observation View.

---

# 13. Inspector tuning

Expose at least:

## Sky clearance
- Clearance Width
- Clearance Height
- Obstruction LayerMask
- Recheck Interval if not checked every frame

## Observation zoom
- Minimum Zoom
- Maximum Zoom
- Zoom Speed if required

## Presentation
- Fade Duration if a fade transition is implemented

Use sensible defaults.

---

# 14. Implementation guidance

Inspect exact current systems before coding:
- carry/world-item interaction;
- boat-item persistence;
- placing/deployment patterns;
- `Interactor2D` / `IInteractable`;
- camera/look controller;
- existing RMB Look implementation;
- celestial sky renderer;
- player visibility/presentation;
- boat hierarchy and renderer ownership;
- requester/authority patterns.

Possible small reusable responsibilities may resemble:
- `PlaceableBoatEquipment`;
- `SkyClearanceRequirement`;
- `ObservationTelescopeInteractable`;
- `BoatObservationPresentationController`.

Names are suggestions only.

Avoid one giant class owning physics, placement, camera, renderer hiding, persistence, and authority.

---

# 15. Non-goals

Do not implement:
- charting minigame activation;
- chart-paper consumption;
- fragment creation;
- star evidence recording;
- found/bought charts;
- route planning;
- cartography ruler/protractor tools;
- new constellation logic;
- telescope reticle;
- special magnified star rendering;
- telescope-specific sky shader;
- power requirement;
- impulse knock-over behavior;
- damage/degradation;
- NPC behavior.

The Charting Instrument prefab is a separate follow-up pass.

---

# 16. Acceptance tests

The feature is complete when:

1. Telescope can be picked up.
2. Telescope can be placed on a valid boat surface.
3. Telescope can be pinned/deployed.
4. Telescope cannot be used while carried.
5. Telescope cannot be used while loose/unplaced.
6. Clear upward box allows use.
7. Nearby overhead obstruction blocks use.
8. Distant hull geometry outside clearance box does not block use.
9. Failed use shows `No unobstructed view of sky.`
10. Observation View uses the existing camera.
11. Boat visuals become fully hidden locally.
12. Onboard items become fully hidden locally.
13. Player becomes fully hidden locally.
14. Telescope becomes fully hidden locally.
15. Islands/world terrain remain visible.
16. Clouds/fog/weather remain visible.
17. Existing RMB Look works while observing.
18. Subtle configured zoom works.
19. No extra telescope UI is required.
20. E exits.
21. Escape exits.
22. Exit restores exact prior visual visibility state.
23. Exit restores prior camera state.
24. New obstruction while observing exits safely and shows message.
25. Telescope works during daytime.
26. Telescope does not alter celestial visibility.
27. Phase 7 known-constellation Look behavior remains compatible.
28. Multiple telescopes can coexist.
29. One player's viewing state does not alter another player's presentation.
30. Physical placement/deployment persists through existing item persistence.
31. Observation pan/zoom/session state does not persist.
32. No chart evidence/progress is created.
33. Existing boat/camera/celestial behavior remains functional.

---

# 17. Future-proofing TODO

Pin for later impulse/boat-physics work:

> **Nothing in the boat is ever truly safe.**

Placeable boat equipment must allow future strong impulses to potentially:
- loosen it;
- knock it over;
- unseat it;
- shift it.

Do not implement those effects now.

---

# 18. Bosun delivery expectations

When complete, report:

1. Summary of architecture added
2. Exact files modified
3. Exact new files
4. Prefab / Inspector setup required
5. New serialized fields/defaults
6. Placement/deployment state model
7. Sky-clearance implementation
8. Local-vs-shared state decisions
9. Persistence behavior
10. Controls
11. Known limitations
12. Compile/test status
13. Regression tests performed
14. Any live-code discrepancy from this handoff

Do not expand scope into the Charting Instrument pass.

---

**End of Observation Telescope handoff.**
