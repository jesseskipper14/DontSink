# Incoming handoff status

Updated 2026-10-06 — Bosun 🍌. Reviewed against the current incoming folder, implementation checkpoints, source inventory and Skip's accepted tests. DONE_ means the implemented, approved pass scope is complete; it does not include separate roadmap features or guarantee there will be no later bugs.

## Completed handoffs

- DONE_Phase7_Celestial_Naming_Constellations_Codex_Handoff.md
- DONE_Observation_Telescope_Bosun_Handoff.md
- DONE_Charting_Instrument_Bosun_Handoff.md
- DONE_DontSink_CameraSafe_Multiplayer_Handoff_Bosun.md
- DONE_Wrapped_World_Topology_Bosun_Handoff.md
- DONE_DontSink_Dynamic_BoatScene_Seafloor_Land_Handoff_Bosun.md
- DONE_Piloting_Navigation_Reconciliation_Bosun_Handoff.md
- DONE_Harbor_Node_Transition_Bosun_Handoff.md

New DONE_ renames in this review: camera hardening and dynamic BoatScene terrain. Existing .meta files moved with their documents, preserving Unity GUIDs. The previous status listed an inaccurate DONE camera filename; the list above reflects the actual folder.

Terrain is complete under the approved revised scope: streaming/voyage strip, geographic depths, feature planning, land encounters, separate island visuals, physical coastal grounding, polar boundary state, and harbor-based departure/arrival integration. X wrapping supersedes the old no-wrap checklist. Literal physical coasts supersede the old boat-only invisible obstruction model. Safe new departures replace the proposed inland-startup workaround. Existing grounded saves/debug warps are not automatically relocated. Dynamic resource/POI content, polar consequences and impact damage were explicitly outside this pass.

Piloting is complete with the approved nautical-mile viewscape radius. Automatic observer-height/bridge/crow's-nest integration remains deferred; its multiplier seam exists.

## Pending incoming implementation handoffs — eight, plus settlement follow-through

| Current filename | Status | Remaining work |
| --- | --- | --- |
| NodeScene_Harbor_Mooring_MiniPass_Bosun_Handoff.md | Completed under the user-revised scope; filename still unprefixed | Authored quay/markers, curved seabed, concrete material, builder placement under _Deck and one visual rope with moored throttle rejection/Embark release are accepted. Automatic harbor construction, two physical mooring lines and boat-scaled dredging were superseded by direct instructions. See AUTHORED_QUAY_MOORING_SETUP.md. |
| Node_Settlement_Generation_Bosun_Handoff.md | Foundation and visual refinement accepted; full service/content follow-through remains | Persisted town layouts/history, larger organic spacing/stacking, existing market/ladder/NpcBase reuse, passive wandering, foliage/background, paving, thicker platforms/rails/braces, safe props, prosperity clutter and archetype palettes are implemented/accepted. Functional Surveyor and further service/content binding remain separate follow-through. No blanket morning-playtest blocker remains. |
| Map_Node_Discovery_Bosun_Handoff.md | DISC.1–5 accepted; DISC.6 seafloor charts implemented, awaits final playtest | Replace geographic knowledge-state/passive discovery with source-driven surface/seafloor/POI coverage; mythic shroud; physical consumable charts; Surveyor local chart and explicit position fix; independent believed-position marker; telescope surface-survey contracts; sounding evidence processing; shared persistence/authority. Existing shroud/chart/sounding foundations are not this completed redesign. |
| Phase6C_Cartography_Workbench_Bosun_Handoff.md | Pending | Physical shared tabletop workspace, pencil/eraser/freehand marks, pins and route strings, physical measurement/manual navigation records, persistence/authority and freeze behavior. The completed charting instrument does not implement this workbench. Piloting already completed separately; its old future TODO is not an additional unfinished piloting pass. |
| Celestial_Dynamics_Foundation_Bosun_Handoff.md | Pending | Definition-driven moving celestial bodies, closed ground tracks and authoritative-time evaluation; local altitude/zenith/horizon queries; sky/chart evidence integration; modifiers/conditions and stable IDs/versioning/persistence. Existing static stars and day/night sun/moon animation are not this implementation. |
| Dont_Sink_Multiplayer_Architecture_Handoff.md | Audit delivered; implementation pending | Modules/boat-power authority first; participant identity and dynamic player bootstrap; transport/authenticated two-instance traffic; atomic shared transactions/claims; shared physics/AI relevance; disconnect/reconnect, crew scenes/death/wipe and host saves/client mirrors. See MULTIPLAYER_INFRASTRUCTURE_AUDIT.md. No real transport/session implementation has been completed. |
| Legacy_Navigation_Cartography_Cleanup_Bosun_Handoff.md | Audit + destination-free Embark + passive reveal removal implemented; remaining cleanup on hold | Audit KEEP/MIGRATE/REMOVE/DEFER; remove obsolete player route/cluster/range gates, auto-arrival and route UI; reconcile discovery/piloting/celestial movement permissions; migrate old saves; retain graph links required by economy/events/worldgen. Some free-sailing and harbor changes already landed, but the full cleanup audit/migration has not. |
| Surveyor_NPC_MiniPass_Bosun_Handoff.md | Surveyor NPC accepted; surface survey work implemented, awaits playtest | One functional Surveyor/station per node; local chart issuance/reissue, position fixes, survey-work and chart-sale service/quest seams. Depends on shared discovery/chart contracts rather than a separate Surveyor-only implementation. |
| Underwater_Background_Visuals_Bosun_Handoff.md | Pending | Three background layers, geographic forms/landmarks, regional flora/fauna and ominous silhouettes, depth/light integration and persistence/transition hardening. |
| Lighting_Visibility_Darkness_Power_Bosun_Handoff.md | Pending; earlier depth/lamp POC accepted | Full celestial/global illumination, powered fixtures, compartment/portal light, underwater/searchlight/node lighting, occlusion/shadows and performance/save/MP hardening. |

DONT_SINK_MASTER_AI_HANDOFF.md is a living project reference, not a pass to mark DONE_. Incoming documents are design inputs; this review did not begin any of their implementations.

## Separate remaining work and backlog

These are not additional unfinished incoming files. They remain in BUGS.md, ENHANCEMENTS.md, implementation caveats or explicit user deferrals.

- New Game world-map snapshot and stale starting-node reset are now implemented as a dependency of starter-only coverage and settlement identity isolation. The assembled menu/load/new-game regression still needs morning testing.
- NodeScene water-bottom binder wiring/provider compatibility. BoatScene now has a streamed fill-bottom provider; do not treat the older audit as proof that the new BoatScene streaming integration is absent.
- Map shore coloration versus actual land/water boundary; node spacing/voyage-duration balance.
- More aggressive ordinary geographic depth transitions (45 degrees or steeper for large mismatches). Feature/trench slope tweaks and removal of stepped shelves did not finish this ordinary-transition tuning request.
- Major lighting overhaul, deliberately deferred after acceptance of the depth/lamp POC: correct renderer/light integration, shadows/occlusion, consistent water/bell/actor lighting. Lamp power/fuel, persistence and network state are not included in its POC.
- Automatic observer-height visibility integration for bridge/crow's-nest design, deliberately deferred.
- Dynamic resource generation, secondary terrain/POIs, richer terrain features and future moving surface encounters; current terrain has integration seams, not completed content.
- Polar boundary weather/ice/hazard consequences; current boundary query/debug state exists.
- Physical impulse/damage/equipment release and camera-effect consumers. Pin-release and local camera-effect seams exist; consequential systems remain deferred.
- Remaining inventory/build/cartridge requester ownership, authoritative multi-actor simulation LOD relevance, module/power gates, trade rollback/atomic transactions and station claims. These overlap the MP audit and should not become duplicate implementations.
- Legacy saves missing usable world bounds and remaining seam/migration checks; preserve saved geography rather than inventing dimensions.
- Telescope renderer discovery caching/performance follow-up.
- Other non-cartridge UI/small-window checks; shared cartridge and map-table scrolling fixes are implemented. Unity Inspector null-target exceptions need investigation only if they recur with stable, unlocked targets.
- Earlier inventory/treasury reset and first-chest fixes are implemented; their listed save/menu/slot regressions remain useful verification checks, not missing implementations.

## Latest bug-patch checkpoint

Skip accepted the focused ocean fixes and the bell readout/lamp/depth-lighting prototype as sufficient to commit. The former "awaits live tests" blanket status is stale. Retain the individual regression checklists/history, but do not list inventory dropping, moving hatches, carrier velocity, bell-relative motion, propulsion eligibility or grounding resistance as unimplemented. Broader lighting remains explicitly deferred.

No gameplay scripts, scene/prefab/Inspector assets or original handoff contents were changed by this inventory review. Only status bookkeeping, two incoming filename/meta renames and the bug acceptance note were updated.


Harbor closure: HT.5 retained canonical NodeScene orientation under section 15.2's explicit invasive-mirroring fallback; mirroring is deferred to shared mooring/settlement layout. Existing harbor regressions pass. See HARBOR_TRANSITION_FINAL_CHECKPOINT.md. Legacy cleanup CLEAN.1 audit is delivered in LEGACY_NAVIGATION_CLEANUP_AUDIT.md; CLEAN.2 destination-free Embark and later behavior/migration checkpoints remain pending.


2026-10-05 overnight checkpoint: destination-free Embark and passive reveal removal are implemented; other legacy cleanup is explicitly on hold. Settlement prototype foundation is implemented in a separate pass, with no saved Inspector/scene/prefab changes. See DESTINATION_FREE_EMBARK_AND_REVEAL_CHECKPOINT.md and NODE_SETTLEMENT_FOUNDATION_CHECKPOINT.md for exact files, tests and morning checks. No additional DONE_ renames were made for these unfinished full handoffs.


2026-10-06 reconciliation: main menu's authored waves/buoyant props and fixed star sky were accepted; piloting palette/layout/controls/wave/sparkle mini-pass was accepted. Quay/mooring is accepted under the revised authored, single-visual-line design. Static-ground player grip correction is reported better; keep quay/dock and full-speed boat stopping as regression checks. No new gameplay implementation or incoming-file renames were performed by this inventory review. The master handoff remains a reference rather than a queued feature pass.


Discovery/Surveyor implementation started 2026-10-06: DISC.1 independent coverage/marker records, arbitrary masks, truth-independent mythic shroud and bathymetry display gating are implemented. See DISCOVERY_1_FOUNDATION_CHECKPOINT.md. DISC.1 playtest accepted. DISC.2 physical charts, reference distinction, deliberate Mapping Table integration, consume-on-success and local reveal fade are implemented; see DISCOVERY_2_CHART_INTEGRATION_CHECKPOINT.md. Surveyor presence/local chart generation remain next after this checkpoint is accepted.

2026-10-06: DISC.2 accepted. DISC.3 whole-island/offshore charts, pathological-landmass limits, physical local issuance/reissue, explicit believed-position fixes and fresh starter-island path are implemented. See DISCOVERY_3_LOCAL_SURVEYOR_SEAM_CHECKPOINT.md. Stop for playtest before physical Surveyor/station binding. POI star-clue reference imagery and held viewing are logged in FEATURE_TODOS.md.

2026-10-06: DISC.3 playtest accepted. Placeable NpcBase-derived Surveyor variant, existing passive brain/stationary movement/service interaction/dispatcher, functional Local Chart and explicit Fix Position menu, and honest empty work/sale states are implemented. User will author/place the station and NPC; no automatic scene generation added. See SURVEYOR_NPC_AUTHORING_CHECKPOINT.md. Surface contract lifecycle and timed telescope readings are next, with existing coverage/chart/celestial foundations available.

2026-10-06: Surveyor NPC playtest accepted; its building will be authored by the user. DISC.4 surface survey work now connects deterministic unknown-water offers, frozen accepted jobs, saved independent readings, sky-reference cards, the existing Telescope's timed contextual Survey action, eligible endpoint turn-in and physical surface-chart rewards. See DISCOVERY_4_SURFACE_SURVEY_CHECKPOINT.md. Production runtime/editor compilation and 66 isolated Unity checks pass. Stop for scene playtest before DISC.5 tiny surface-gap cleanup and DISC.6 sounding chart processing. No scene/Inspector edits or commit in this checkpoint. Deterministic building-relative key NPC placement remains in FEATURE_TODOS.md.



2026-10-07: DISC.5 implemented: small connected surface-gap cleanup within legitimate host commits, wrapped longitude/finite latitude, surface-only mutation, configurable eight-cell default. Runtime/editor compilation, 147 focused cleanup/chart/position-fix/survey checks and the existing foundation suite pass. See DISCOVERY_5_CLEANUP_AUTHORITY_CHECKPOINT.md for manual playtest and authority limits. No scene/Inspector edits or commit. Stop for testing before DISC.6.


2026-10-07: User authorized DISC.6 after DISC.5. Sounding line records physical paper-backed evidence; Surveyor processing transforms it into a physical bathymetry-only chart; existing Mapping Table integration consumes/reveals it. Added Sea Floor toggle and known-surface/missing-depth quest eligibility seam. See DISCOVERY_6_SEAFLOOR_CHART_CHECKPOINT.md for playtest, evidence footprint and multiplayer/quest limits. No scene/Inspector edits or commit. Stop for final discovery testing.

