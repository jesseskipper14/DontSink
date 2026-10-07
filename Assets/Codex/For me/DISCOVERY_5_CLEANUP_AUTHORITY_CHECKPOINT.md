# DISC.5 — Tiny surface gaps and shared-authority checkpoint

Implemented 2026-10-07 after the user accepted DISC.4. Awaiting scene playtest. Nothing committed; no scenes, prefabs or saved Inspector values edited.

## Behavior

After a host-side cartographic source adds genuinely new surface coverage, fill remaining four-neighbor connected unknown components of at most eight knowledge-grid cells. Left/right neighbors wrap; north/south edges do not. A component crossing the horizontal seam is measured as a whole. Larger regions stay unknown.

WorldMapKnowledgeSource has a new Surface Chart Gap Cleanup setting: Maximum Unknown Component Cells (default 8, range 0–64; zero disables). This is a cell-area threshold, so eight cells means eight times the current grid-cell world area; it does not depend on the rendering texture resolution. The default is deliberately conservative and can be tuned after playtest.

Cleanup is part of the same synchronous host commit as the chart payload and source receipt. It changes only surface bits. It never adds node/POI markers, bathymetry, underwater POIs, position fixes or trade data. Existing chart-specific markers still come from that chart's explicit payload. Inventory consumption and the existing presentation fade see the complete committed result, including cleanup.

No passive travel scan or per-frame work. No cleanup for bathymetry-only, marker-only, invalid, duplicate, or unchanged-surface commits. Save restoration does not reinterpret existing gaps: it restores exactly the saved bits. Future legitimate surface expansion may clean eligible older gaps. Debug reveal helpers remain explicit operations and do not invoke cleanup.

Traversal is iterative O(grid cells), with a reusable queue per commit and no recursive flood fill or allocation per component. Grid size is already bounded to 1,048,576 cells by the knowledge-state validation.

## Authority and regression

The production TryCommitCartographicSource seam was moved into WorldMapKnowledgeSource.SurfaceCleanup.cs and retains its host guard. The state object remains pure trusted state; every production payload call routes through that host seam. Client requests still cannot provide a chart grant: integration resolves the exact item in the requester inventory, validates world registration and current table, then consumes it synchronously after the commit. Existing receipt and reentry guards remain in force.

This checkpoint verifies current host/replica gates and shared save state. It does not implement multiplayer RPC transport/replication or claim a live network-session test.

Production runtime/editor compilation passes. Isolated Unity validation passes:

- 29 cleanup checks: threshold behavior, disabling, wrapped component sizing, finite latitude edges, large unknown areas, layer isolation, invalid/replayed/unchanged commits, snapshot JSON roundtrips, detached copies, actual production source host rejection and host cleanup, and a 512×512 nonrecursive traversal.
- 33 physical chart pipeline checks: requester ownership (including another actor), carried/nested/equipped item consumption, duplicate sources, closed tables, wrong-world/reference charts, reentrant callback rejection, receipts, and detached/expiring presentation snapshots.
- 12 position-fix checks: canonical marker, preserved pin/orientation, stale drag invalidation, replica rejection, coordinate validation/wrapping, and unique marker identity.
- 73 surface survey checks: offers, accepted job stability after an actual legitimate later coverage commit, independent readings, contract JSON persistence, exact requester/host checks, full-inventory retry, and duplicate/reentrant reward protection.
- Existing foundation/bit-codec suite passes (262,174 assertions, including exhaustive codec checks).

Log: Library/CodexQuayChecks/discovery-5.log. An intentional post-inventory observer exception tests payout protection; all suites pass after it. Existing edit-mode visual cleanup warnings in the foundation fixture do not indicate a runtime failure.

## Manual playtest

1. Integrate a surface chart where small enclosed unknown specks remain. They should disappear with the chart reveal; larger unknown areas should remain.
2. Confirm cleanup does not expose node/POI icons, soundings, or move your believed-position marker.
3. Save/reload or change scenes: cleaned surface and integration receipts should persist; uncharted larger regions remain uncharted.
4. An accepted survey should retain its readings and reward when another chart covers its area. A duplicate-source chart should remain carried and fail cleanly when integrated again.

Optional: set Maximum Unknown Component Cells to zero in the runtime Inspector to compare subsequent integrations. No Inspector assignment is required for the default behavior.

## Next

Stop for DISC.5 playtest. DISC.6 remains: sounding evidence processing into physical georeferenced bathymetric charts and the associated rendering/quest/POI gates. UI polish, physical held POI sky-reference charts and deterministic building-relative NPC placement remain separate TODOs.

2026-10-07: User authorized proceeding to DISC.6. DISC.5 accepted as the preceding checkpoint.
