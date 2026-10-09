# DON'T SINK — Surveyor NPC Mini-Pass Bosun Handoff
## One Surveyor Per Node, Dynamic Station Placement, Local Charts, Position Fixes, Survey Work, and Chart Sales

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Small focused NPC/service pass  
**Goal:** Get the Surveyor into NodeScene soon as a real in-world access point for the new cartographic/discovery systems, without waiting for the later full NPC/dialogue/personality overhaul.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

> If modifying an existing class, inspect and use the exact latest source currently in the project. Never reconstruct an existing class from memory, an old handoff, or assumptions.

Before changing an existing class:
1. Open the exact current source.
2. Preserve unrelated behavior, serialized fields, save compatibility, authority hooks, interaction behavior, and multiplayer hardening.
3. Reuse the current NPC/interactable/UI patterns where practical.
4. Reuse the new Map / Node Discovery interfaces and chart pipeline rather than inventing parallel surveyor-only logic.
5. Prefer additive support classes/data over invasive rewrites.
6. Do not expand into the future full NPC dialogue/personality system.

---

# 1. CORE SCOPE

For this mini-pass:

> **Every node has exactly one Surveyor.**

The Surveyor:
- exists physically in NodeScene,
- spawns with a dedicated Surveyor Station,
- exposes cartographic services through a simple interaction menu,
- grants the local island chart,
- provides explicit authoritative position fixes,
- exposes seams for survey contracts,
- exposes seams for purchasable charts/information.

This is intentionally a functional NPC/service pass, not a character-writing pass.

---

# 2. ONE SURVEYOR PER NODE

For now:
- exactly one Surveyor per node,
- always present,
- deterministic identity/spawn association,
- no random absence,
- no death/unavailability,
- no reputation refusal,
- no faction lockout,
- no schedule.

Those possibilities are future gameplay.

Guaranteed presence now makes discovery-system testing reliable.

---

# 3. SURVEYOR STATION

The Surveyor should spawn **with a dedicated Surveyor Station**.

Important rule:

> The Surveyor's spawn location is defined by the dynamically placed Surveyor Station.

Do not hardcode:
- a fixed world-space coordinate,
- a fixed distance from harbor,
- a manually authored per-node transform.

The future Node Generation system should be able to place the Surveyor Station dynamically as part of settlement layout.

For this mini-pass:
- create the station prefab/data seam,
- place it using whatever current deterministic NodeScene placement path is safest,
- parent or associate the Surveyor with that station,
- spawn the Surveyor at a stable station-relative point.

The station is the placement authority.

---

# 4. STATION PURPOSE

The Surveyor Station is both:
- a visual/home anchor for the NPC,
- an authoring hook for future Node Generation.

It may initially be visually simple:
- desk,
- chart stand,
- sign,
- small kiosk,
- tent/booth,
- other placeholder.

Final art is not required.

Future systems may use the station for:
- chart displays,
- hydrographic tools,
- survey equipment,
- quest markers,
- assistants,
- personality-driven clutter.

Do not implement those now.

---

# 5. SURVEYOR INTERACTION MENU

Interacting with the Surveyor should open a simple functional menu.

Initial options:

```text
Surveyor

- Local Chart
- Fix Position
- Survey Work
- Charts for Sale
```

No dialogue tree is required.

No prose conversation system is required.

No unique personality system is required.

This menu is a service UI, not the future giga-pass NPC chat system.

---

# 6. LOCAL CHART

`Local Chart` provides the physical georeferenced chart for this Surveyor's own island.

The chart should use the Map / Node Discovery pipeline already designed.

Expected payload:

```text
Local Surveyor Chart
├─ this island's surface geography
├─ configured offshore buffer
├─ this node's marker / canonical location
└─ no unrelated neighboring-node information
```

The chart is:
- physical,
- georeferenced,
- integratable at the Mapping Table,
- consumed on successful integration.

The Surveyor does **not** directly reveal the map.

---

# 7. LOCAL CHART REISSUE RULE

The local chart is effectively a one-time knowledge source, but the Surveyor may reissue the physical item if needed.

Desired behavior:

```text
if local chart knowledge NOT yet integrated:
    Surveyor may issue/reissue local chart

if local chart knowledge already integrated:
    no additional local chart is needed
```

Reissue should be free or effectively free for now.

Purpose:
- avoid soft-locking the tutorial/discovery flow if the physical chart is dropped, lost, destroyed, or otherwise unavailable before integration.

Do not create infinite sellable duplicate charts after knowledge is integrated.

---

# 8. POSITION FIX

`Fix Position` is an explicit Surveyor service.

It should **not** happen automatically just because the player talks to the Surveyor.

On choosing `Fix Position`:

> Snap the shared physical believed-position marker to the true NodePosition / HarborPosition.

This uses the authoritative position-fix seam from the Map / Node Discovery architecture.

The action may be reused indefinitely.

Do not:
- reveal extra map coverage,
- auto-integrate a chart,
- move the actual boat,
- alter true world position.

This only corrects the crew's physical believed-position marker.

---

# 9. SURVEY WORK

`Survey Work` exposes the seam for the surface survey contract system.

For this mini-pass, it is acceptable for this to be:
- placeholder UI,
- debug-backed contract list,
- thin integration to current quest infrastructure,
- no available contracts if the full survey-contract generator is not yet implemented.

The important requirement is architectural:

> The Surveyor becomes a legitimate quest giver / turn-in endpoint for survey contracts.

Future survey contract behavior includes:
- unknown-water targeting,
- distance weighting,
- predefined reading zones,
- telescope survey readings,
- either-endpoint turn-in for node-to-node contracts,
- physical processed chart reward.

Do not rebuild all of that here if it is not already implemented.

---

# 10. CHARTS FOR SALE

`Charts for Sale` exposes a simple vendor/information seam.

For now:
- menu may be empty,
- menu may contain debug/sample entries,
- or it may surface a minimal list of available cartographic sources.

The system should support future purchasable:
- local/regional georeferenced charts,
- node-location charts,
- shipping corridor charts,
- surface POI charts,
- bathymetric charts,
- hydrographic charts,
- non-integrating reference/knowledge charts.

Do not hardcode a giant inventory system into the Surveyor.

Reuse current item/vendor architecture if suitable.

---

# 11. NO DIALOGUE SYSTEM IN THIS PASS

Do not implement:
- branching dialogue,
- authored conversation trees,
- personality,
- voiced dialogue,
- relationship dialogue,
- procedural chatter,
- unique biographies,
- NPC memory,
- chat history.

The future NPC dialogue/personality overhaul will be a separate large pass covering all NPCs.

For now, the Surveyor is a functional service NPC.

---

# 12. VISUAL / NPC PRESENTATION

The Surveyor needs only enough presentation to be identifiable.

Acceptable first pass:
- placeholder sprite,
- simple name label,
- simple station signage/icon,
- standard interact prompt.

Do not block implementation on final art.

---

# 13. NODE GENERATION SEAM

This mini-pass must leave a clean seam for the future full Node Generation system.

That future system will decide:
- where the Surveyor Station belongs,
- what district/space it occupies,
- how it relates to prosperity/archetype,
- surrounding buildings,
- nearby props,
- NPC clustering.

Therefore:
- Surveyor placement should depend on the station,
- station placement should be callable/configurable from Node Generation,
- do not bake assumptions about permanent static scene coordinates.

---

# 14. MULTIPLAYER / AUTHORITY

Surveyor services that modify shared state must be authoritative.

Shared/consequential actions include:
- local chart issuance/reissue,
- position-fix execution,
- survey-contract acceptance/turn-in,
- purchases,
- physical chart spawning.

Prevent:
- duplicate free chart issuance races,
- duplicate quest turn-in,
- duplicate purchases,
- inconsistent position marker fixes.

Local UI can remain client presentation.

---

# 15. SAVE / PERSISTENCE

Persist or derive enough state to support:

```text
Has local chart knowledge already been integrated?
Is there an outstanding local chart item?
What survey contracts are active/completed?
What purchased chart items exist?
```

Prefer querying the shared authoritative cartographic dataset to determine whether the local chart's knowledge is already integrated rather than storing a redundant boolean if the current architecture allows that cleanly.

Reissue logic should tolerate save/load.

---

# 16. DEBUG SUPPORT

Useful debug controls:

- spawn/rebuild Surveyor Station,
- respawn Surveyor,
- force local chart issue,
- force local chart reissue,
- mark local chart knowledge integrated/not integrated for testing,
- force position fix,
- show current believed-position marker coordinate,
- inject sample Survey Work entry,
- inject sample chart-for-sale entry.

Debug tools should be removable/hidden in normal gameplay.

---

# 17. TEMPORARY STARTING-TUTORIAL RELATIONSHIP

Current implementation may still begin with starting-island coverage already available for testing.

Future intended tutorial:

```text
new game
→ tutorial points to Surveyor
→ player interacts
→ Local Chart
→ player carries chart to Mapping Table
→ integrates chart
→ starting island fades into authoritative map
```

Do not hardcode the Surveyor in a way that prevents this future tutorial flow.

---

# 18. RECOMMENDED IMPLEMENTATION CHECKPOINTS

## SURV.1 — Station + NPC presence
- Surveyor Station prefab/data seam
- deterministic dynamic station placement
- one Surveyor per node
- Surveyor spawns with station
- basic interaction prompt

**STOP FOR TESTING.**

## SURV.2 — Functional service menu
- Local Chart
- Fix Position
- Survey Work
- Charts for Sale
- no dialogue tree

**STOP FOR TESTING.**

## SURV.3 — Local chart + reissue
- physical georeferenced chart
- correct local island payload
- no direct map reveal
- reissue while knowledge not integrated
- no pointless duplicates after integration

**STOP FOR TESTING.**

## SURV.4 — Position fix + authority
- explicit Fix Position action
- snap shared believed-position marker
- save/load regression
- multiplayer duplicate-action protection

**STOP FOR TESTING.**

## SURV.5 — Quest/vendor seams
- Survey Work hook
- Charts for Sale hook
- debug/sample entries acceptable
- document future integration points

**STOP FOR FINAL TESTING.**

---

# 19. ACCEPTANCE TESTS

This mini-pass is complete when:

1. Every node has exactly one Surveyor.
2. Every Surveyor is associated with one Surveyor Station.
3. Surveyor placement follows the dynamically placed station.
4. No permanent hardcoded station world coordinate is required.
5. Surveyor can be interacted with.
6. Interaction opens a service menu.
7. Menu contains Local Chart.
8. Menu contains Fix Position.
9. Menu contains Survey Work.
10. Menu contains Charts for Sale.
11. No dialogue tree is required.
12. Local Chart creates a physical georeferenced chart item.
13. Local Chart does not directly reveal the map.
14. Chart payload covers only the Surveyor's island + configured offshore buffer + own node marker.
15. Lost/unintegrated local chart can be reissued.
16. Already-integrated local chart knowledge does not generate pointless duplicate copies.
17. Fix Position only happens through the explicit menu action.
18. Fix Position snaps the believed-position marker to NodePosition / HarborPosition.
19. Fix Position does not move true world position.
20. Survey Work exposes a clean quest seam.
21. Charts for Sale exposes a clean information/vendor seam.
22. Shared-state actions are authority-safe.
23. Save/load does not break local-chart reissue logic.
24. The station/NPC setup can later be driven by the full Node Generation system.
25. Existing NodeScene interactions remain intact.

---

# 20. EXPLICIT NON-GOALS

Do not implement in this pass:
- full procedural NPC generation,
- NPC personality,
- branching dialogue,
- reputation,
- hostility/refusal,
- Surveyor death/unavailability,
- NPC schedules,
- voice,
- full survey quest generator if not already available,
- final cartographic economy,
- Node Intelligence NPC,
- full Node Generation,
- seafloor resource/POI generation,
- final Surveyor art,
- harbor prosperity variation,
- full tutorial.

---

# 21. FUTURE FOLLOW-UPS

### Full NPC dialogue/personality giga-pass
All NPCs eventually gain:
- chat options,
- distinct personalities,
- authored/procedural dialogue,
- relationship/context awareness.

### Full Node Generation
Surveyor Station placement becomes part of the dynamic settlement layout.

### Node Intelligence
Separate NPC/service for remote trade/security/prosperity information about known nodes.

### Surveyor expansion
Later possibilities:
- unavailable/hostile Surveyors,
- reputation-gated services,
- regional specialty,
- different chart inventories,
- better/worse contract pools,
- hydrographer variants.

---

# 22. BOSUN DELIVERY EXPECTATIONS

After each checkpoint, report:

1. Exact files modified
2. Exact new files
3. Existing classes inspected before modification
4. How Surveyor Station placement works
5. How Surveyor spawns relative to station
6. Interaction/menu implementation
7. Local chart item/payload implementation
8. Reissue logic
9. Position-fix implementation
10. Survey Work seam
11. Charts-for-Sale seam
12. Authority decisions
13. Save/persistence behavior
14. Debug tools added
15. Regression tests performed
16. Any discrepancy between this handoff and live project architecture

Do not silently expand scope into the future NPC dialogue/personality system.

---

**End of Surveyor NPC mini-pass.**
