# DISC.6 — Sounding evidence and seafloor charts checkpoint

Implemented 2026-10-07 after authorization to continue beyond DISC.5. Awaiting physical sounding / scene playtest. Nothing committed. No scenes, prefabs, catalogs or saved Inspector values edited.

## Playtest first

1. Put Charting Paper in a pocket or portable container. If it is stacked, leave a free pocket for the chart. Hold the existing sounding line in Hands, with rope loaded, and deploy it from the exterior deck in BoatScene.
2. Let the weight reach bottom. The existing HUD gains Record sounding — 1 Charting Paper. Click once: one physical unprocessed Sounding Chart is created. There is no map reveal yet. Each bottom-contact attempt can be recorded once; retrieve/release and take another reading for another chart.
3. Return to the authored Surveyor. Open Sounding Chart Processing and click Process Sounding Chart. The existing item transforms in place into a georeferenced seafloor chart. No extra inventory slot and no shared map reveal.
4. Open Mapping Table → Charts. Inspect and integrate the processed chart. It is consumed, a bathymetry-only source receipt is recorded, and the existing reveal fade includes its coverage.
5. In the World Map, Sea Floor toggles acquired depth detail. If the surface at that location is unknown, the depths remain hidden under the normal surface shroud. Acquire surface coverage later and they become visible. Unknown depth areas are ordinary plain ocean, not a second mythic shroud.
6. Save/reload or transition scenes while carrying raw evidence and while carrying processed charts. Integrated depths and receipts also persist. Confirm a basic sounding does not add wreck/resource/node markers or move the believed-position marker.

If Record is disabled after bottom contact, check that the sounder is still deployed/held, bottom contact occurred in a registered world with authoritative geographic navigation available, and this attempt has not already been recorded. Redeploy after the world/navigation setup becomes ready. Paper/full-inventory failures are retryable for the same captured reading.

## Frozen evidence and foundation scope

The existing physical sounding line captures the actual weight depth below the local wave surface at the first bottom latch. It freezes the boat's true geographic position at that time; clicking later does not relocate the reading. This foundation registers a local sounding around the boat's navigation position, not a full geometric reconstruction of the weight's horizontal rope excursion. Sloping/streamed physical terrain can differ from the canonical world topography displayed by the map.

Each sheet holds one frozen reading: evidence version, geographic position, measured depth, world seed/topography version/bounds. Evidence is carried in the existing physical chart carrier. Its item identity, notes and detached reading survive existing item snapshots and inventory/world transfer paths; no new save schema version is required. Old charts have no sounding field. The optional presence flag prevents Unity JSON from inventing empty sounding data on old/reference charts.

Processing validates the evidence and current world, transforms its kind in place, and creates a bathymetry-only local circular mask. Default coverage radius is 28 map units, configurable as Sounding Reveal Radius on WorldMapKnowledgeSource, and increased to at least half a knowledge-cell diagonal on coarse grids so the reading is representable. Source ID is tied to the physical item's identity. Raw evidence cannot integrate. Processing again fails; duplicated processed source IDs cannot integrate twice.

The seafloor overlay uses the existing registered topography and acquired bathymetry mask. Measured depth remains in the physical chart's evidence/notes before consumption. This checkpoint does not build a saved interpolated depth field from individual measurements, a multi-reading notebook, continuous swath recording, advanced sonar, POI detection, or a full sounding quest runtime.

## Paper transaction and ownership

Recording is a trusted host service called by the validated deployed sounder, never a client-supplied chart payload. It resolves only the exact requester's carried paper. Paper in pockets and nested portable containers is supported; equipment-only paper must be stowed because Hands holds the sounding instrument.

One sheet is consumed and one physical chart is created before notifications. A last sheet can reuse its slot; stacked sheets require a free pocket slot. A paper-only filtered container cannot receive the output carrier: use a permitted free pocket instead or fail without consuming paper. Reentry is blocked; observer exceptions after the committed writes do not replay the transaction. Existing normal item quantity operations retain their notifications; a narrow internal quantity-write/publish pair supports the atomic paper transaction.

Surveyor UI rechecks its existing station range/actor context before processing. Processing re-resolves exact item ownership and world registration, preserves physical identity, and requires no additional free slot. Mapping Table integration retains the existing current-table, exact-item, host-only, source receipt and callback reentry guards.

These are current authoritative gates and shared persistence seams. Multiplayer request/replication transport and a live multi-client session remain future infrastructure work.

## Rendering and quest seam

Sea Floor is a presentation toggle; it never deletes acquired data. Changing it invalidates the cached ocean concealment mask. Underwater POI icons are also hidden while Sea Floor is off. With it on, explicit underwater POI information plus known surface remains required; basic sounding payloads contain no POI or surface grants.

SoundingChartBuilder.IsQuestCandidate requires charted surface, actual water and missing bathymetry in the same registered bounds. Future sounding quest generators can use this rule. Arbitrary player recording/processing does not call it and remains allowed in unknown surface areas; acquired hidden depths persist until surface coverage arrives.

## Validation

Production runtime/editor compilation passes. The isolated Unity checkpoint group tests physical raw/processed chart JSON persistence, exact requester/host ownership, one-paper recording, no paper/full inventory, atomic notification/reentry handling, nested paper/evidence, filtered containers, in-place processing, raw-chart integration rejection, duplicate processing, registered/invalid measurements, longitude wrapping, hidden depth storage, later surface visibility, quest eligibility, Sea Floor cache behavior, and unchanged surface/POI knowledge.

It also reruns DISC.5 cleanup, physical chart pipeline, position-fix, surface-survey and foundation/bit-codec checks. Log: Library/CodexQuayChecks/discovery-6.log. Intentional observer exceptions verify post-commit duplicate protection; they are not runtime regressions.

Tests exercise the actual evidence/building/service/integration/state code with isolated host and inventory fixtures. Physical weight/contact capture, HUD layout, authored Surveyor interaction and final visual fade require the manual playtest above.

## Remaining

Stop for final discovery playtest. After acceptance, review the two incoming handoffs for DONE status and retain their explicitly deferred work in FEATURE_TODOS: physical held POI/star clue images, deterministic building-relative key NPC placement, authored Surveyor building, sale inventory/economy as later scope, and broader UI polish. Do not add new stations, lights, terrain, or Inspector assignments for this checkpoint.

Final validation: 32 sounding checks plus the 147 prior focused regression checks pass (179 focused checks total), along with the 262,174-assertion foundation suite. Production runtime/editor compilation passes.

## Playtest refinement — 2026-10-07

User found the Record action and requested a larger sounding reveal plus a fix for the overlapping Press G hint. Newly processed charts now default to radius 28 map units rather than 2. Already processed/integrated charts preserve their saved footprint; process a fresh raw Sounding Chart to test the new size. The sounding HUD includes its deploy/retrieve/release key instruction in its status area. LocalHandheldSoundingLineIntentSource suppresses its duplicate floating context hint while that HUD is visible, retaining the hint fallback when the HUD is disabled. No scene/Inspector changes.

For authored written-chart visuals, the existing distinct carrier is Assets/Resources/Cartography/item_cartographic_chart.asset. Assign its Icon to the scribbled-paper sprite and change the SpriteRenderer on its already separate World Prefab, Assets/Resources/Cartography/CartographicChart.prefab (or assign a replacement authored prefab). Blank paper remains Assets/Defs/Items/Items/item_paper_charting.asset with its original prefab. Written evidence and processed/georeferenced charts all use the carrier, so no new catalog or service assignment is required. Art authoring remains with the user.

Runtime/editor compilation and 179 focused sounding/discovery regression checks plus the foundation suite still pass. Log: Library/CodexQuayChecks/discovery-6-radius.log. Physical panel/hint layout and the tuned reveal remain for visual playtest.
