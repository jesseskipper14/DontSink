# DISC.2 — Physical charts and Mapping Table integration

2026-10-06 — Bosun. DISC.1 playtest accepted. Stop here for the next playtest/commit checkpoint. No commit/staging performed. No existing saved Inspector settings, scenes, prefabs or materials changed in this checkpoint.

## Implemented

- CartographicChartState is per-item evidence with georeferenced/reference kinds, title/notes, optional Resources sprite reference, registered world seed/topography generation/bounds and explicit surface/bathymetry/node/surface-POI/underwater-POI payloads. Reads, issuance and snapshots deep-copy masks and ID lists. Unity serialization presence flags preserve absent optional masks/payloads instead of reloading them as invalid empty objects.
- ItemInstance and ItemInstanceSnapshot retain chart data through ordinary inventory/Hands/container/world-item transfer and save paths. Charts cannot stack or split. Ordinary old items remain ordinary; no charts or grants are invented from missing save fields.
- Existing Mapping Table now has a CHARTS page. It lists the exact requester's carried charts, including portable containers and Hands. Inspect shows notes, optional reference art and payload contents. Reference items cannot integrate and remain carried. Acquiring/dropping/picking up a chart does not grant map knowledge.
- Host-side CartographicChartIntegration.TryIntegrate accepts table session and exact item instance ID, not a client-supplied payload. It checks active table/requester, exact current carried ownership, one-item/non-container constraints, world registration and payload/source receipt validation. No yielding occurs between map commit and chart removal. Failure preserves the item and map; success commits knowledge, removes the exact carried item, and only then publishes inventory notifications. A transaction guard blocks callback reentry; persisted source receipts reject duplicate integration.
- Existing WorldMapCartridge uses a detached prior-state presentation snapshot. Newly exposed parchment and depth concealment fade away; newly supplied node/POI colors fade in. Knowledge is already committed while this runs. Close/reopen displays acquired data immediately. Duration defaults to 2 seconds and is adjustable through the new WorldMapOverlayRunner Cartographic Reveal Seconds field (1–3 seconds). No assignment is required.
- New self-contained Resources/Cartography chart carrier definition and physical prefab reuse the existing paper visual/physics as placeholders. Existing authored assets are untouched. ItemDefinitionCatalog has a narrow built-in resolver fallback for this carrier; existing serialized catalog lists need no editing. The generic carrier is not automatically stocked by vendors.
- CHARTS > DEBUG: chart test items supplies surface-only, depth-only or reference items at the current shared map center. Each georeferenced test chart covers a radius of 28 map units and grants no inferred markers. Test issuance is host-only, inventory capacity checked and grants nothing until deliberately integrated. Production Surveyor/survey reward issuance comes next.

## Test now

No Inspector wiring is needed. Use a test save if clearing knowledge.

1. Open Mapping Table, pan the WORLD MAP to a convenient unknown ocean area, then open CHARTS. Enable DEBUG: chart test items. Give a test surface chart. It should appear in carried inventory and on this page, without any immediate map reveal.
2. Inspect it, then Integrate. It should be consumed and return you to WORLD MAP. Parchment should fade out over about two seconds to surface geography/plain ocean. Surface-only charts must not reveal depth contours or invent node/POI records.
3. At a fresh unknown ocean area, give/integrate a depth chart first. Its knowledge should be stored without surface reveal. Integrate a surface chart at the same center afterward: the stored depth detail should appear. Try surface first, then depth as well.
4. Give a reference chart. Read its notes; Integrate is disabled. It should remain carried and give no knowledge.
5. Give an unintegrated chart, move it through Hands/a portable container or drop/pick it up, and save/reload. Its identity/data should persist and it should still integrate once. Save/reopen after integration, including during the fade: consumed charts stay consumed and knowledge stays acquired.
6. Regression: World Map pan/zoom/debug warp, Star Chart board/compare, ordinary inventory stacking and physical paper pickup/drop. Returning from CHARTS must preserve the shared map viewport.

The debug charts contain circles for convenient testing; the item pipeline accepts the arbitrary registered masks from DISC.1. These test items are not Surveyor local-island coverage.

## Validation

Production runtime and editor assembly compilation passed. Isolated Unity tests passed 32 checks using production chart state, ItemInstance, InventorySlot/snapshot contracts, knowledge state/masks/codec, topography field, chart transaction and reveal lifecycle partial. Inventory/equipment/table session hosts and nonessential item-definition/container dependencies are adapted test doubles. Coverage includes deep copies, optional-null JSON masks, exact identity/save roundtrip, old save behavior, no pickup grant, reference rejection, host/session/ownership rejection, registration mismatch, invalid-payload no writes, duplicate preservation, successful exact consumption, observer ordering/reentry, nested container and Hands removal, independent depths, saved receipts, detached fade state and expiry without knowledge rollback.

Full scene UI, physical drop/reinstantiation, end-to-end save-slot changes and network transport require playtest. This is a host-authority seam; it does not implement a network request/replication transport. Reveal animation is local to the integrating table session; other sessions receive committed knowledge through the existing shared-state path.

## Changed files

Existing classes inspected before editing:
- Assets/Scripts/Inventory/Item/ItemInstance.cs
- Assets/Scripts/GameState/Inventory/ItemInstanceSnapshot.cs
- Assets/Scripts/GameState/Inventory/ItemDefinitionCatalog.cs
- Assets/Scripts/MiniGames/Runners/WorldMapOverlayRunner.cs
- Assets/Scripts/WorldMap/MapTable/MapTableCartridge.cs
- Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs

New scripts plus metadata:
- Assets/Scripts/WorldMap/Fog/CartographicChartState.cs
- Assets/Scripts/WorldMap/MapTable/CartographicChartIntegration.cs
- Assets/Scripts/WorldMap/MapTable/CartographicChartFolio.cs
- Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.CartographicReveal.cs

New assets plus metadata: Assets/Resources/Cartography/item_cartographic_chart.asset and CartographicChart.prefab.

## Next after acceptance

DISC.3 local Surveyor seam: real connected-island coverage with offshore dilation and pathological-landmass limits; explicit Fix Position API; starter-island path; chart/vendor issuance seams. Functional Surveyor NPC/station content follows that seam. No broad new world-generation redesign, Phase 6C drawing/measurement tools, or unrelated pending bugs were included here.
