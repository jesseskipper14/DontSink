# DISC.3 — Local-island charts and explicit position fixes

2026-10-06 — Bosun. DISC.2 accepted. Stop for playtesting here. No staging/commit or existing saved Inspector/scene/prefab/material changes. Physical Surveyor NPC/station content is the next refinement after this seam is accepted.

## Implemented

- LocalIslandChartBuilder samples registered topography, associates the node with nearby land, finds its four-connected island and builds an arbitrary surface mask. X wraps, Y stays finite. Offshore dilation spreads through water only; unrelated neighboring land is excluded. No bathymetry or POIs are supplied. Chart payload includes only this node's stable ID.
- LocalIslandChartLimits is configurable on WorldMapKnowledgeSource with defaults: analysis resolution cap 512 per axis; offshore buffer 4 map units; land association radius 16; maximum land area 5000 square map units; maximum land extent 150; maximum reveal radius 100; fallback radius 28; fallback total coverage area budget 2000. Oversized connected land triggers a deterministic local fallback with bounded land and ocean coverage, instead of revealing the continent. The fallback reserves part of its budget for offshore water.
- Local chart source receipts are stable per node/world/topography version. SurveyorCartographyService.TryIssueLocalChart creates the existing physical chart carrier for the exact requester. It grants no coverage on issuance. It rejects a duplicate already carried by that requester and stops after the crew integrates that source. Lost/unavailable charts can be reissued before integration. Multiple crew copies may exist before integration; only one can integrate and later copies are rejected by the existing source receipt.
- Free local charts require a carrier vendors cannot buy, including forced vendor inclusion. The existing built-in chart carrier already meets that rule. No vendor prices/stock/currency transactions were added. Additional authored chart/reference payloads use the existing DISC.2 item/payload seam; actual Surveyor sales and contracts remain later UI/content.
- SurveyorCartographyService.TryFixPosition resolves the station's canonical map node and explicitly corrects the shared physical PlayerBoat marker. It does not move the actual boat, reveal geography, integrate a chart, reset the navigator's rotation, or unpin the marker. MapTablePhysicalPieceAuthority.TryApplyPositionFix creates a missing marker if needed, changes its position/revision, and cancels outstanding drag locks so a stale drag cannot undo the fix. Switching table pages also cancels pending physical-piece drags.
- Fresh-game starting coverage now uses this same whole-island/offshore builder, with its own starter receipt. It waits for registered topography instead of silently falling back to the old circle. Only an empty, fresh fallback knowledge grid may be re-registered when topography arrives; acquired/restored state is preserved. Existing saved starter coverage is not retroactively expanded and visits do not grant new coverage.
- CHARTS > DEBUG: chart test items now includes Issue current node's local island chart and Fix believed position to current node. These are explicit debug entry points for this checkpoint, not automatic docking/talking behavior. Production station interactions will validate requester/range and supply their own station node ID at the host service boundary.

## Test now

No Inspector assignments required. Prefer NodeScene for these debug controls: while sailing, the stored current node is a departure/docking node, not your current boat coordinates.

1. In the Mapping Table, drag the blue believed-position boat marker somewhere incorrect. Open CHARTS, enable DEBUG: chart test items, and choose Fix believed position to current node. Return to WORLD MAP: the marker should be at the canonical node, with its rotation/pin state retained. Coverage should not change and the actual boat should not move.
2. Issue current node's local island chart. It should appear physically in inventory without revealing the map. A second issuance while carrying it should be blocked. Drop/lose it before integration; a replacement should be available.
3. Inspect and Integrate the local chart. It should be consumed through the normal fade workflow, exposing the island and offshore waters plus its own node marker. Neighboring islands/nodes/POIs and depth data should remain unknown unless another explicit source already granted them.
4. Try issuance after integration: no replacement should be needed/issued. Save/reload and check this source receipt and coverage persist. A duplicate physical copy must fail integration without consumption.
5. On a disposable fresh game, check starting coverage follows the whole starting island with a water buffer. Existing saves intentionally retain their saved coverage. Check wrapped islands at the map edge if available.
6. Regression: Star Chart board and manual physical marker dragging, map viewport switching, ordinary inventory/Hands transfer, chart consumption/save paths, departure/docking. No passive reveal or position fixes should occur while sailing, docking or merely visiting a node.

Configuration tuning is optional. The generator uses a capped analysis raster, then the existing knowledge grid resamples the mask. Very small landforms/coasts may need finer resolution in a future generation/presentation pass; this does not change terrain truth. Diagonal-only land contacts are separate components by the four-connected analysis rule. Pathological fallbacks may intentionally chart only part of the local connected island.

## Validation

Production runtime and editor compilation passed. New isolated Unity checks passed 51 assertions: 26 island-builder checks, 12 position-fix checks and 13 Surveyor-service checks. They use production island builder/topography/mask/knowledge code, physical-piece authority and Surveyor service, with adapted inventory/equipment/table/GameState/graph/provider hosts. Coverage includes deterministic whole-island masks, offshore buffer, neighboring-land exclusion, wrapping seam, finite latitude, area/extent/distance fallbacks, invalid inputs, cross-resolution integration, explicit node-only data, marker creation/pin/orientation preservation, repeated fixes, authority rejection, stale-drag rejection, physical issuance without grants, carried-duplicate rejection, loss/reissue, integrated-source refusal, inventory capacity and nonsaleable free carrier requirements.

Provider/scene bootstrap wiring was compiled and reviewed; complete fresh-game startup ordering, full physical item/drop/save flow and UI geography must be verified in Play Mode. No network request/replication transport or final NPC interaction validation is implemented by this seam-only checkpoint.

## Files

New scripts plus metadata:
- Assets/Scripts/WorldMap/Fog/LocalIslandChartBuilder.cs
- Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeSource.LocalCharts.cs
- Assets/Scripts/WorldMap/Fog/SurveyorCartographyService.cs

Existing live classes inspected before editing:
- Assets/Scripts/WorldMap/Fog/WorldMapKnowledgeSource.cs (starter path now uses local builder; dependency wait)
- Assets/Scripts/WorldMap/MapTable/MapTablePhysicalPieceAuthority.cs (explicit fix with stale-lock invalidation)
- Assets/Scripts/WorldMap/MapTable/MapTableCartridge.cs (cancel piece drag on tab switch)
- Assets/Scripts/WorldMap/MapTable/CartographicChartFolio.cs (debug test entry points)

Also recorded the requested POI -> star clue image -> reference chart -> held viewing follow-up in FEATURE_TODOS.md. It stays separate from this checkpoint and never auto-integrates map data.

## Next after acceptance

Bind one real Surveyor/station per node to the settlement's deterministic Surveyor anchor, reuse NpcBase and existing interaction/menu patterns, and expose Local Chart / explicit Fix Position / Survey Work / Charts for Sale. Full survey-contract generation follows its own discovery checkpoint. Broad world-map generation redesign and reference-clue image/held viewing work remain separate.
