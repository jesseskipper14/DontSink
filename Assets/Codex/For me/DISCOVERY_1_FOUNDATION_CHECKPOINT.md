# DISC.1 — Shared cartographic foundation

2026-10-06 — Bosun. First checkpoint of the paired Discovery / Surveyor implementation. No saved Inspector settings, scenes, prefabs or materials were changed by this checkpoint. No commit was made. Existing quay/friction/user asset edits remain separate.

## Implemented

- Reuses WorldMapKnowledgeState and its existing packed surface/underwater save grids. Surface and bathymetry remain independent. Underwater circle surveys and Survey All no longer imply surface grants.
- Explicit integrated node IDs, surface-POI IDs, underwater-POI IDs and source receipts are saved independently. Coverage never grants markers. Physical POI discovered/surveyed flags no longer authorize map presentation. Unknown nodes cannot be selected through hit-testing or the selected-details query.
- Surface POI records may display over uncharted parchment. Underwater POI records require surface coverage; bathymetry visibility requires both surface and underwater coverage. Plain opaque ocean hides uncharted depth colors/contours on otherwise charted water. There is no second underwater mystery shroud.
- WorldMapCoverageMask supports arbitrary raster masks plus circle, rectangle, polygon and corridor helpers. X wraps; Y stays finite. Polygon/corridor edges cross the short side of the wrapping seam. Masks in a different grid resolution are sampled into the existing grid; their registered world bounds must match exactly.
- WorldMapCartographicPayload explicitly separates optional surface/bathymetry masks and marker ID lists. Trusted host-side TryCommitCartographicSource rejects replica writes. Model validation runs before mutation; malformed masks/IDs/registration and duplicate source receipts are rejected without partial grants. This is a source-commit seam, not a physical chart inventory transaction yet.
- The opaque mythic shroud uses its own constant decorative seed and cached procedural parchment/waves/serpents/compass roses. Its builder has no world-truth inputs. Art is deliberately placeholder. Normal map and Star Chart's known-world reference both use it; explicit supplied markers draw above it.
- Bathymetry concealment is cached by world field, knowledge state/revision and sea level; it is not generated every GUI frame. Cartridge End releases that owned texture. Shroud art is shared cached presentation.
- Starter-only coverage exception is retained, including the explicit starter-node marker. The preexisting starter radius remains for this checkpoint; whole connected-island coverage and safety-limited offshore dilation belong to DISC.3. Restored saves never gain visit-based coverage or invented neighboring markers.
- Existing saved surface/bathymetry bits remain intact. Existing knownNodeStableIds are retained as explicit records. Missing new fields are empty. No conversion of physical visits/discovered flags into new cartographic records. This intentionally means legacy charts with coverage but no marker records will show fewer markers.

## Playtest before next checkpoint

Use a test save if clearing knowledge. No Inspector wiring is needed.

1. Open Map Table > World Map. Leave Show Mythic Shroud, DEBUG: Mask Uncharted Depths, and Hide Unintegrated POIs enabled. Unknown areas should be fully opaque parchment rather than grey fog; pan/zoom and check no faint coastline/contours or hidden clickable nodes leak through.
2. On the left Map Knowledge controls, Clear Fog resets test coverage/marker records/receipts. Reveal Node adds surface coverage around the current node only. It should not add node/POI markers automatically.
3. Press DEBUG: Add Current Node Marker. Your current node marker should appear, including over parchment if geography is still unknown. You should be able to select it. It should not reveal surrounding terrain. A mere node visit/docking does not create it.
4. After Clear Fog, Survey Node adds only underwater data: it should remain hidden. Then Reveal Node adds surface coverage; the previously stored depth detail becomes visible in the overlap. Revealed water outside surveyed coverage should be plain ocean, without depth contours.
5. Save/reload test knowledge. Coverage, explicit current-node marker and source receipts should persist. Visit/sail elsewhere without a cartographic source: no automatic grants. Check map pan/zoom, debug coordinate warp, Star Chart comparison and normal piloting remain functional.

Turning off the masking toggles is an explicit debug answer key, not acquisition of knowledge. Current-node highlight rings are limited to that debug answer-key view; a normal shared believed-position marker is a later implementation, not automatic tracking.

## Files inspected and modified

Existing live classes read before changes:
- Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeState.cs
- Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeSource.cs
- Assets/Scripts/WorldMap/Data/WorldMapSaveSnapshot.cs
- Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs

New files under Assets/Scripts/WorldMap/Fog, plus .meta:
- WorldMapCoverageMask.cs (mask helpers and source payload)
- WorldMapKnowledgeState.Cartography.cs (integrated records, payload validation/union, snapshots)
- WorldMapMythicShroud.cs (truth-independent procedural decoration)
- WorldMapBathymetryConcealment.cs (cached opaque depth concealment)

Also inspected save builder/restorer, POI instance/definition/source paths, topology, topography field/debug source/texture builder, both incoming handoffs and settlement checkpoints. Existing capture/restore already calls the knowledge snapshot hooks, so save builder/restorer needed no rewrite. No Surveyor/item/quest/transport scripts changed yet.

## Validation / limits

Production runtime and editor assembly compilation passed. Isolated graphics-enabled Unity tests passed 30 behavioral/render checks plus an opacity check for every pixel of the 512x512 shroud. Tests use production state, mask, codec, extracted production knowledge snapshot, topology struct, topography field and presentation builders. They cover layer separation, marker-only sources, no inferred markers, duplicate rejection, invalid payload rollback, world-registration rejection, detached JSON snapshot roundtrips, legacy fields, arbitrary masks, wrapped shapes, finite-Y/invalid queries, debug resets, opaque depth concealment, cache reuse and visibility after surface grant/removal. The rendered shroud preview was visually inspected.

Authority guards were reviewed/compiled; this is not a network transport test. Full in-game UI/input, performance, save-slot transitions and marker render placement still need your playtest. At this checkpoint there are no physical charts/consume transaction/reveal fade, functional Surveyor, position-marker table object, survey contract generator/telescope hook, sounding paper processing or tiny-gap cleanup. These belong to the later checkpoints. Full removal/migration of legacy navigation data stays on hold; its enum fields are not used as new geographic acquisition states here.

## Next

After this checkpoint is accepted/committed: DISC.2 physical georeferenced/reference chart pipeline and atomic Mapping Table integration, then Surveyor station/NPC/service placement via the existing settlement semantic anchor. The Surveyor handoff's explicit Fix Position button overrides the discovery document's broader wording about fixes on successful interaction.
