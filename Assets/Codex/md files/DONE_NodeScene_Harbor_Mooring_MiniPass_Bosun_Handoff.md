# DON'T SINK — NodeScene Harbor Mooring Mini-Pass
## Raised Harbor, Quay, Short Dock, Automatic Mooring Lines, and Dredged Berth

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Small NodeScene harbor infrastructure pass  
**Goal:** Establish the basic physical harbor grammar now, without expanding into the later full harbor/economy/cargo-loading system.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest source currently in the project. Never reconstruct an existing class from memory or an older handoff.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior and serialized fields.
3. Reuse the current rope/tether architecture where possible.
4. Preserve current save, scene-transition, interaction, buoyancy, and multiplayer-hardening behavior.
5. Prefer small additive support classes/data over broad rewrites.

---

# 1. TARGET HARBOR SHAPE

The NodeScene harbor should move toward this cross-section:

```text
                 RAISED TOWN / LAND
══════════════════════════════════════
                         solid quay
                         ███████
                         █     └──── short dock
~~~~~~~~~~~~~~~~~~~~~~~~~│~~~~~~~~~~~~ waterline
                         │   \   \      automatic mooring lines
                         │    \   \____ BOAT
                         │
                         │      dredged berth
                         └───────────────
                                         \
                                          \____ normal seafloor
```

This pass intentionally establishes only the basic reusable pieces.

---

# 2. RAISE THE NODE LAND / TOWN DATUM

Raise the usable NodeScene land/town surface by a configurable number of world units.

Reason:
- ordinary waves should not crash across the island/town surface;
- the harbor should visually read as a built-up quay above the waterline;
- future harbor infrastructure should have a stable elevation reference.

Prefer a single authoritative/generated **NodeScene ground datum / harbor elevation** rather than manually offsetting individual props.

Anything that depends on the town ground level should continue to place relative to that datum:
- NPC spawn surfaces,
- stalls,
- houses/buildings,
- props,
- future quest/event placements.

Do not individually hand-move unrelated scene objects as the long-term solution.

---

# 3. ADD A SOLID QUAY / HARBOR APRON

Add a new solid shoreline harbor piece where land meets water.

Visual intent:
- concrete / stone slab / quay wall,
- stable artificial harbor edge,
- visually stronger than the current raw slope.

Mechanical intent:
- real solid ground,
- valid player walking surface,
- future anchor point for harbor equipment.

Use the project's appropriate existing ground/collision layer.

Future systems may attach to this area:
- bollards,
- cranes,
- cargo handlers,
- ladders,
- fuel/water service,
- harbor NPCs.

Do not implement those future systems in this pass.

---

# 4. SHORTEN THE CURRENT DOCK

Replace/rework the existing long horizontal dock so it projects only far enough from the quay to:
- provide player access to the moored boat,
- create a believable harbor edge,
- provide physical mooring attachment locations.

The dock no longer needs to span the old shallow shoreline geometry because the berth will now be dredged.

Keep the dock geometry simple and reusable.

---

# 5. AUTOMATIC MOORING SYSTEM

The docked boat should be secured by **two automatic physical mooring lines**:
- one toward the forward portion of the boat,
- one toward the aft portion of the boat.

Conceptually:

```text
dock bollard ●────────● boat forward mooring point

dock bollard ●────────● boat aft mooring point
```

## Hard gameplay rules

- Mooring is automatic.
- Players cannot manually tie the lines.
- Players cannot manually untie the lines.
- Lines remain secured for the entire docked NodeScene stay.
- Lines release only as part of embarking on a new voyage.
- Mooring is not a cargo-securing minigame.
- Mooring is not an ordinary player interaction.

The purpose is physical presentation and stable docking behavior, not another maintenance chore.

---

# 6. REUSE THE EXISTING ROPE / TETHER SYSTEM

Do not create a new rope simulation unless the current tether system is fundamentally unsuitable.

Reuse the existing rope/tether architecture used elsewhere in the project where practical.

The mooring implementation needs:
- visible rope/tether,
- physical restraint,
- configurable length/slack/tension behavior,
- stable attachment points,
- safe cleanup on embark/scene transition.

Exact support-class names should follow the live project architecture.

---

# 7. THE BOAT MUST STILL BOB

Mooring must **not** hard-freeze the boat.

While docked:
- buoyancy still runs,
- waves still affect the hull,
- the boat may move slightly,
- the boat may rotate slightly,
- the mooring lines physically restrain that motion.

Desired feel:

> secured, not welded to the universe.

The two lines should prevent the hull from drifting away or freely rotating around one anchor while still allowing believable wave motion.

Do not disable buoyancy just to make docking easy.

---

# 8. MOORING POINTS

The harbor should expose stable dock-side mooring points.

The boat should expose stable boat-side mooring points.

Prefer a reusable concept such as:

```text
MooringPoint
- stable role / ID
- forward or aft role
- attachment transform
```

or the closest fit to the current tether architecture.

For this pass:
- exactly two dock-side points are sufficient,
- exactly two boat-side target roles are sufficient.

If the current boat architecture already has suitable hardpoints/transforms, reuse them rather than adding duplicates.

---

# 9. AUTOMATIC MOORING LIFECYCLE

On entering/loading NodeScene in a docked state:

1. resolve current boat;
2. resolve harbor dock mooring points;
3. resolve boat forward/aft mooring points;
4. create the two tether constraints;
5. render visible ropes;
6. leave boat buoyancy/physics active.

On Embark:

1. remove/release both mooring tethers;
2. clean up rope visuals;
3. continue the normal scene transition / departure lifecycle;
4. do not leave stale rope constraints attached to the persistent boat.

Mooring should be deterministic and automatic.

---

# 10. DREDGED BERTH / NEW SEAFLOOR PROFILE

Replace the current immediate shallow/down-slope harbor profile with a deterministic dredged berth near the dock.

Desired shape:

```text
quay
  │
  │
  └──────────────────── flat/deep harbor basin
                                         \
                                          \
                                           normal generated seafloor
```

The berth should provide enough water depth and horizontal clearance for currently supported larger boats to dock without intersecting terrain.

For this basic pass:
- use configurable baseline harbor depth,
- use configurable berth width/length,
- use configurable transition slope back to the normal seafloor.

Do **not** automatically deepen the harbor based on the currently visiting boat.

That would undermine future harbor-quality gameplay.

---

# 11. FUTURE HARBOR DEPTH / QUALITY SEAM

Leave clean seams for later node-driven variation.

Future possibilities include:

```text
poor / low-prosperity harbor
→ shallower berth
→ shorter quay/dock
→ limited vessel access

wealthy harbor
→ deeper dredged basin
→ longer dock
→ cranes
→ better cargo handling
→ larger-vessel support
```

Do not implement prosperity scaling now.

This pass only establishes geometry/data that can later be parameterized by node metadata.

---

# 12. HARBOR NO-SPAWN REGION

The dredged berth should expose a simple underwater spawn-exclusion region.

Purpose:
- no kelp growing through the docked hull,
- no resource node spawning directly beneath/inside the boat,
- no random underwater clutter occupying the mooring basin.

This should be a simple explicit seam consumed later by the seafloor/resource generation rework.

Do not rebuild the underwater spawner in this pass.

---

# 13. NODESCENE MIRRORING COMPATIBILITY

The entire harbor assembly should mirror cleanly with the future/adjacent NodeScene left-right mirror mode.

The following should mirror together:
- quay,
- short dock,
- dredged berth,
- seafloor transition,
- dock mooring points,
- player access direction,
- harbor no-spawn region.

Do not create left-side-only hardcoded assumptions that will make mirroring painful later.

---

# 14. SUGGESTED HARBOR LAYOUT DATA

Prefer a compact generated/layout concept such as:

```text
HarborLayout
├─ GroundDatum
├─ QuayGeometry
├─ DockGeometry
├─ BerthDepth
├─ BerthWidth
├─ BerthLength
├─ SeafloorTransition
├─ ForwardDockMooringPoint
├─ AftDockMooringPoint
└─ UnderwaterNoSpawnRegion
```

Exact implementation must fit live project conventions.

Do not invent a parallel node-generation architecture if suitable current data structures already exist.

---

# 15. MULTIPLAYER / AUTHORITY

The physical docked-boat state is shared consequential state.

Host/shared authority should own:
- whether the boat is docked,
- mooring creation/removal,
- tether endpoints,
- boat physics state,
- embark release.

Clients may render the visible rope locally from authoritative attachment state.

Prevent:
- duplicate line creation,
- stale tethers after embark,
- one client seeing moored while another sees released,
- scene-transition cleanup races.

---

# 16. SAVE / PERSISTENCE

Mooring is derived from docked NodeScene state and does not need to become a complex permanent inventory item.

On loading a docked NodeScene:
- recreate the expected automatic mooring lines from the authoritative docked state.

Do not persist transient rope solver state if it can be deterministically reconstructed.

Persist only what the existing boat/node transition system actually requires.

---

# 17. DEBUG / TUNING

Expose practical tuning for:
- NodeScene land elevation,
- quay dimensions,
- dock length,
- berth depth,
- berth width,
- berth length,
- seafloor transition slope,
- mooring-line slack/rest length,
- mooring stiffness/damping if supported,
- dock attachment offsets,
- boat forward/aft attachment offsets,
- underwater no-spawn bounds.

Useful gizmos:
- ground datum,
- waterline,
- berth rectangle/profile,
- dock mooring points,
- boat mooring targets,
- tether lines,
- no-spawn region.

---

# 18. EXPLICIT NON-GOALS

Do not implement:
- manual tying/untying,
- rope consumables,
- mooring minigame,
- docking skill checks,
- prosperity-based harbor generation,
- cranes,
- cargo loading/unloading,
- harbor workers,
- multiple berths,
- harbor fees,
- fuel/water service,
- tides,
- dock damage,
- fenders,
- boat hull damage,
- boat module maintenance,
- full visual harbor art pass,
- full Node generation,
- seafloor resource generation rework.

---

# 19. ACCEPTANCE TESTS

This mini-pass is ready when:

1. NodeScene land/town surface is raised above the old water-exposed level.
2. Ordinary waves no longer wash across the intended town surface under normal conditions.
3. A solid quay/harbor slab exists at the shoreline.
4. The dock is visibly shorter and tied into the new quay layout.
5. A docked boat receives two automatic mooring lines.
6. One line constrains the forward area and one the aft area.
7. Players cannot manually tie or untie either line.
8. Lines remain until Embark.
9. Embark removes both lines cleanly.
10. The boat continues to bob under buoyancy/waves while moored.
11. Mooring prevents meaningful drift away from the dock.
12. Mooring prevents uncontrolled large rotation around a single anchor.
13. Visible rope endpoints remain attached correctly while the boat moves.
14. Harbor seafloor has a dredged flat/deep berth near the dock.
15. Larger currently supported boats do not intersect the berth floor while moored.
16. Seafloor transitions back into normal generated terrain outside the berth.
17. Harbor exposes an underwater no-spawn region.
18. Harbor geometry is compatible with later left/right NodeScene mirroring.
19. Existing dock/NodeScene transitions still work.
20. Existing buoyancy and boat persistence remain intact.

---

# 20. FUTURE TODO — VISUAL DOCK / HARBOR REWORK

After the physical grammar is stable, revisit harbor presentation.

Future visual pass may include:
- better quay art,
- bollards,
- fenders,
- cranes,
- cargo props,
- ladders,
- harbor lights,
- prosperity-based harbor variation,
- longer/deeper docks for richer nodes,
- industrial vs ramshackle harbor sets.

Do not block this mini-pass on final art.

---

# 21. BOSUN DELIVERY EXPECTATIONS

After implementation, report:

1. Exact files modified
2. Exact new files
3. Existing classes inspected before modification
4. How land elevation is controlled
5. How quay/dock geometry is generated
6. Which existing rope/tether classes were reused
7. How automatic forward/aft mooring points are resolved
8. How bobbing is preserved
9. How the dredged berth is generated
10. No-spawn seam added
11. Multiplayer/authority implications
12. Save/load behavior
13. Inspector/debug controls added
14. Regression tests performed
15. Any discrepancy between this handoff and live project architecture

---

**End of NodeScene Harbor Mooring mini-pass.**
