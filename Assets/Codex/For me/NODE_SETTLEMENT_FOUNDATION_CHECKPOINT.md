# Node settlement generation — prototype foundation checkpoint

2026-10-05 — Bosun 🍌

A separate settlement pass follows the navigation fixes. The playable foundation is implemented with colored primitive facades and people. It is ready for morning testing; the incoming handoff is not marked DONE because live acceptance and the service/content integrations below remain outstanding. No saved scene/prefab/Inspector wiring changed. Not committed.

## Implemented

- Authority creates each node's permanent layout once and stores it on existing MapNodeState. Population is reused; no competing population model was added.
- Manifest version, node/archetype identity, visual capacity, terraces, traversal links, stable plots, fixed roles/families, palette/roof choices, parent/child stacks, socket IDs, development history and explicit damage are persisted in ordinary node save snapshots. Snapshot copies are detached from live state. Old saves without a manifest gain one; existing manifests are reused rather than regenerated from seed.
- Stable FNV seed selection replaces runtime GetHashCode dependence. Archetype rules distinguish farming/fishing, general towns, shipyards/trade hubs and fortress settlements. Sparse towns forbid heavy-industry/defensive roles, use more open plots and fewer/lower terraces; dense types can have five-body chains.
- Terraces and plots are relative to the physical shore. The actual NodeScene terrain is at X=-120 with local shore X=120: the generator uses that shore edge, not the far-left terrain origin. The prototype town occupies the final 29 units of land immediately inland of the quay. First raised walkway is four units above the land datum; this retains the existing authored shops below it.
- Every terrace connects to the arrival path. An explicit traversal validator rejects disconnected terraces, duplicate identities, unsupported stack links and unsafe plot footprints. Valid generated layouts have exactly one Surveyor station and market role.
- Generated one-way platforms reuse WorldLedge, PlatformEffector2D and HatchLedge conventions. Existing LadderZone supplies climb access. Roof support owns a walkable platform and ladder independently of whether its upper building is currently developed.
- Bodies have fixed shape-family dimensions, independent roof caps and seven permanent palettes. Ordinary production sprites are not stretched. Metal roofs have seams; wear fades the same underlying color. Buildings are facades without blocking interior colliders. No custom shader was introduced; the renderer uses the existing sprite-light shader where available.
- Each visit freezes occupancy, condition, activity, dressing, flags/buff identities and active socket sets from population/prosperity/stability/security/trade/food. No structural polling/live morphing occurs during ordinary play.
- Development fills stacks from the bottom, with occupancy/service hysteresis. Achieved structures remain in decline, acquire boarded windows and regain occupancy on recovery. Explicit ruin/destruction survives prosperity recovery until the authority-owned TrySetDamage API clears it. Clients cannot write layout/history/repair state through these APIs.
- Fixed family sockets activate people, food/displays, garden content and dressing in stable order. These are visual placeholder people, not individually saved agents. Civilian, worker, merchant and guard categories use separate simulation inputs.
- Service/quest role anchors and persistent semantic identity are exposed through SettlementSemanticAnchor and NodeSettlementScene.TryGetAnchor / TryGetSocket / GetAnchors. Surveyor gets a permanent station/sign/spawn anchor and a distinct placeholder figure. Event space owns its reserved plot/socket instead of replacing houses.
- Runtime startup adds NodeSettlementScene to the existing NodeGroundGenerator2D. It waits for the current node's built runtime, avoids duplicate scene generation and cleans up owned sprites, materials and generated objects.

## Morning test

1. Enter NodeScene. Look immediately inland from the dock and upward. In Play Mode, select the existing object with NodeGroundGenerator2D; it now has a runtime NodeSettlementScene component. Its generated child is named Settlement_<node ID>.
2. Walk/climb from the shore onto the first walkway, then climb terrace ladders and roof-support ladders. Test dropping through the one-way ledges and walking past facades. Check camera follow and normal boat boarding/market use.
3. On that runtime component, use its context menu: Debug Preview / Rich populated town, Poor crowded town, Rich sparse town, Trade collapse, Food crisis, Abandoned historical town, then Return to loaded visit. These previews copy the layout; they do not mutate saved stats or history.
4. Enable showDebugAnchors on the runtime component and select the terrain object in Scene view to see plots/access links. This is optional and not saved by this pass.
5. Visit a farming/fishing node, then a fortress/shipyard node. Check open yards versus density/verticality and multiple colors. Preview rich to exercise full stack potential even if normal simulation population is currently low.
6. Save/reload a node and revisit it after departure. The layout, family/color identities and plot roles must remain recognizable. Preview mode itself is intentionally not saved.
7. Load a previous game, return to menu, then start New Game. Prior settlement history must not leak. The narrow New Game snapshot reset is shared with the navigation fix.

## Inspector tasks

None required. Runtime generation wires the existing ground host automatically. No prefab, material, sorting-layer, collider-mask or scene asset was rewritten. Later authored family/roof/NPC catalogs and functional station binding will get their own setup checklist.

## Verification and limits

Production C# compilation passed. The isolated Unity harness passed 9,301 model assertions across 500 layouts and 393 scene/platform/ladder/anchor/cleanup assertions, plus the navigation/knowledge checks and the existing ocean regression suite. JSON roundtrips preserve identity/history; snapshot copies do not alias live manifests; collapse/recovery/destruction and negative authority behavior pass. Scene tests exercise actual generated platform geometry and production rendering code, with adapted node providers and ladder/ledge component hosts. They do not certify the real player's masks, collision matrix, climb exits, assembled save-slot cycle or normal scene lighting.

Direct3D11 rendered prototype fixtures for fishing, fortress and abandonment. Existing eight depth shaders and bell/lamp pixel/disable checks also pass. Those fixture images are in ignored Temp/CodexPhase7/settlement-*.png and omit the real NodeScene terrain, dock, camera and lighting.

## What still needs another contained step

- Bind real Surveyor gameplay to the generated station when that service implementation exists. No implemented Surveyor scripts were found in the current source. This pass establishes its permanent space and identity, not a chart-selling/position-fix interaction.
- Bind/relocate actual market and future special services. Existing authored shops/NPCs remain operational at their current places; generated market/industry/Harbormaster/Tavern roles currently provide stable spaces and availability metadata, not replacement vendor implementations.
- Replace shape templates/placeholder people with authored family, roof and NPC assets. There is no new ambient AI/schedule implementation or network transport; layout/history use the project's existing authority gate.
- Tune footprint, terrace height, density, capacity, development thresholds and art after live testing. Current sparse/dense geometry is deliberately a prototype. The separate mooring/quay/dredged-berth pass remains pending; its world-layout/mirroring ownership is not duplicated here.
- Connect concrete quest/event consumers and authored per-family event/damage treatments to the exposed role/socket/override seams. Snapshot flags/buff IDs are available; specific event content is not invented here.

## New production files

- [NodeSettlementManifest.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Model/NodeSettlementManifest.cs>) (+ .meta)
- [NodeSettlementPlanner.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Model/NodeSettlementPlanner.cs>) (+ .meta)
- [NodeSettlementScene.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/NodeSettlementScene.cs>) (+ .meta)
- [SettlementSemanticAnchor.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/SettlementSemanticAnchor.cs>) (+ .meta)

## Modified existing files

- [MapNodeState.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Model/MapNodeState.cs>)
- [WorldMapRuntimeBinder.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeBinder.cs>)
- [NodeGroundGenerator2D.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/GroundGeneration/NodeGroundGenerator2D.cs>)
- [WorldMapSaveBuilder.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveBuilder.cs>)
- [WorldMapSaveRestorer.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveRestorer.cs>)
- [WorldMapSaveSnapshot.cs](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveSnapshot.cs>)

The separate navigation report covers MainMenuController's necessary fresh-session snapshot reset, travel diagnostics, and other navigation files. Existing source was inspected before each edit. Incoming handoff text was not rewritten; its requirement to wait for playtest at every checkpoint was superseded by your direct instruction to proceed while you slept and test in the morning.

After live checks, commit this checkpoint. Keep the settlement handoff unprefixed until its remaining functional service/content scope is reconciled and accepted.

## Follow-up: empty legacy manifest on load

Fixed the startup rejection of empty settlement manifests. Unity inline serialization/older save data can supply an empty custom-class object instead of null; the original initialization only recognized null. Initialization now recognizes wholly empty version-0/version-1 shells and generates their first layout on authority. Any identity, layout data, or unsupported version prevents replacement, preserving existing towns and their history. Validation errors now include version, node ID, terrace and plot counts.

Inspector tasks: none. Stop Play Mode, allow compilation, then reload the same save and enter NodeScene. Confirm the town appears without the manifest error; save and reload again to check layout persistence.

Production assembly compilation passed. Unity regression harness passed, including seven added checks for legacy missing/empty JSON, valid first generation, repeat initialization, malformed identified layout preservation, unsupported version preservation, and client authority. Settlement totals: 9,308 planner checks and 393 scene checks. Actual user-save loading remains a live check.
