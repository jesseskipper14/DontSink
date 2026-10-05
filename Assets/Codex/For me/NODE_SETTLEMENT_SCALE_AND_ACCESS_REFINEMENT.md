# Settlement scale, layout and ladder refinement

The screenshots showed small facades and doors, a narrow town footprint, identical full-width elevated rows, excessive reserved vertical spacing, and overlap with the existing harbor market. The original generated ladders also lacked the authored climb/exit configuration.

## Changes

- Ground street now spans 98 physical units instead of 29. Ordinary house bodies are 7.2–8.6 units wide and 5.5–6.2 units tall, with 2.6-unit doors.
- Elevated streets form smaller alternating west/east districts. Their height follows the reserved buildings below them, including future stack bodies; no global five-storey row spacing. Edges, body heights and horizontal placement vary deterministically.
- New settlements stack less often, with shorter chains. Terminal ordinary houses get pitched roofs and no support ladder. Existing developed stack identities are preserved during migration.
- Market and Surveyor buildings copy the authored Market prefab's sprite hierarchy at its authored scale, including its asymmetric sell platform/sign footprint. These are facade placeholders, without cloned vendor/cargo-store/sell-zone behavior. Existing scene shops remain functional.
- The final 18 units before the harbor are reserved for authored scene shops. Generated building/socket geometry stays landward of that area in the current 120-unit NodeScene land span.
- Access uses Resources/Prefabs/BoatKit/Ladder directly. ResizableSegment2D and LadderAutoFitAuthoring resize each runtime instance while preserving prefab climb-center and exit references. No procedural ladder replacement remains.

## Saved layouts

Manifest version 2 records the revised geometry. Valid version-1 prototype layouts reflow once on authority, retaining plot IDs, family IDs, colors, development/occupancy/service history and damage. Upper rows with four columns split into two smaller districts. Terrace/connection geometry changes; existing plot/socket identities survive. Invalid or unsupported saved manifests are still rejected rather than regenerated. Normal subsequent loads do not reflow version-2 layouts.

## Inspector tasks

None. No saved prefab, scene, material, layer or Inspector settings were edited. Resource paths reference the existing Market and Ladder assets. The footprint currently assumes the authored NodeScene's 120-unit land span; supporting arbitrary differently sized node scenes remains a later configuration refinement.

## Live checks

1. Reload the existing save and enter NodeScene. Check the town spreads landward and clears the existing market's sign and sell platform.
2. Compare house door/body scale to the actual player. Walk along ground and elevated streets.
3. Climb a street-access ladder and a support ladder, exiting at both ends. The harness verifies prefab wiring, not actual player traversal.
4. Check some standalone houses have pitched roofs, with no ladder or building above them.
5. Save/reload and confirm layout, colors and any historical/damaged plots remain stable.

## Validation

Production assembly compilation passed. Unity harness passed 9,810 planner/model checks and 142 scene checks, plus existing navigation/terrain/lighting regressions. Scene checks now instantiate the actual ladder prefab with production ladder/resize/marker scripts; player movement remains an adapted host. Direct3D11 rendered fishing, fortress and abandoned previews. The isolated preview uses its built-in compatible sprite material instead of the main project's URP configuration; main-scene lighting remains a live check.

Changed production files: NodeSettlementManifest.cs, NodeSettlementPlanner.cs and NodeSettlementScene.cs. No commits were made. Commit after the live checks pass.

## Follow-up: ladder exit clearance and landward gate

Settlement ladders now extend 1.6 units above the destination platform and .2 units below their starting surface. The authored top marker is consequently about 1.55 units above the ledge, giving the player rigidbody center sufficient height when PlayerLadderClimber snaps to it. This applies to both street-access and support ladders without modifying the Ladder prefab.

A closed timber gate now marks the far-left landward boundary. Its position follows BoundaryWall_Left and the ground datum, including boundary padding. The existing wall owns collision; the generated gate adds no collider. It is only created when the corresponding boundary exists, and rebuild/cleanup follows the settlement root.

Inspector tasks: none. Reload NodeScene, climb onto a street and a roof platform, and walk to the landward edge to check the visible gate against the stopping point. Production compilation and Unity regressions passed: 149 settlement scene checks and 9,810 planner checks, plus existing regression suites. Actual player traversal remains a live check.

## Follow-up: town hierarchy and extended landward boundary

The town controller and generated hierarchy now belong to NodeContext / NodeView / NodeTown. Ground generation attaches the controller there and supplies its ground reference; the town's render root preserves the ground-relative world position when parented to NodeTown. The existing NodeContext and NodeView transforms remain untouched. NodeTown is created/reused at runtime.

Authorized Inspector changes in NodeScene:

- SeaFloor Transform X: -120 to -240.
- NodeGroundGenerator2D World Width: 240 to 360.
- Island Length: 120 to 240.
- Point Count: 240 to 360, preserving approximately one-unit terrain sampling.
- Boundary Padding: -20 to 1.5. The negative value placed the wall inside the town's available land.

The shoreline remains at world X=0, the underwater right endpoint remains X=120, and the town keeps its existing world coordinates. The normal landward boundary moves from X=-100 to approximately X=-241.5. Ground generation additionally checks the manifest's town footprint with a 120-unit landward clearance, extending only the landward end if a later layout needs more room. The existing boundary collider's near face must remain outside that clearance; the gate follows the resulting actual wall position. No building plots or spacing were changed in this follow-up.

Unity's existing editor regeneration also refreshed the serialized ground collider points, moved the boundary children to local X=-1.5 and 361.5, and widened generated background/fill scales from 100 to 150. The existing Randomize Seed On Generate setting remains enabled; its refreshed terrain seed is 1056985022 (previously -451432140). These generated-data updates occurred during scene regeneration after the five direct Inspector edits above. Unity also serialized the existing viewscapeSettings field; no new viewscape configuration was intentionally edited in this follow-up.

Production compilation and Unity regressions passed. Checks now use the production NodeGroundGenerator2D and verify shoreline preservation, boundary clearance, hierarchy placement and repeated attachment without duplicate controllers. Reload NodeScene and check NodeContext/NodeView/NodeTown, unchanged harbor alignment, and the gate's new position far beyond the town. No additional Inspector work is required.

## Follow-up: explicit -140 boundary, direct stacks and NpcBase

Per the corrected requested coordinate, NodeScene's landward boundary now uses an explicit world X=-140 override, and the gate follows it. Inspector changes: Override Town Boundary World X enabled; Town Boundary World X set to -140. The previous automatic 120-unit clearance policy is bypassed for this explicit override. The current town footprint is landward of the harbor but right of this barrier, with clear separation from generated buildings. Extended terrain remains in place; town spacing is unchanged. Both editor regeneration and runtime placement honor the explicit coordinate.

Ordinary house bodies are six units tall. A directly stacked house starts at the supporting body's exact top, and its roof-access platform uses the same increment, without the former .65-unit air gap. Terminal pitched roofs remain. Version-2 saved layouts receive a one-time version-3 stack-height migration preserving IDs, colors and history; ground-level positions remain unchanged.

All generated people, including the Surveyor placeholder, now instantiate Resources/Prefabs/Agents/NpcBase at its authored scale. Root/shirt SpriteRenderer is cyan (.12,.7,.78); other body-part colors are retained. Prefab AgentController receives the permanent socket/service ID and node ID, and is registered. No new AgentDefinition, wandering AI, schedules or service behavior was added; the prefab currently has no definition assigned. NPC center starts 1.02 units above its standing socket to clear its two-unit body collider.

Inspector tasks: none beyond the two authorized boundary fields already applied. Reload NodeScene; check world X=-140 on BoundaryWall_Left, visible gate alignment, gapless stacked bodies/platforms, cyan NPC shirts and player-relative proportions. Save/reload to check migration stability. Production compilation and Unity regressions passed, including direct stack height, prefab controllers/shirt color, and explicit boundary-coordinate checks. Harness agent-controller behavior is adapted; actual NPC interaction/collision is a live check.

## Follow-up: passive brain and bounded NPC wandering

Generated NPCs now use the new Resources/SettlementTownNpc definition and SettlementTownNpcBehavior assets. The behavior references the existing PassiveTownNpcBrain and GroundWanderWithinHomeBounds definitions. Existing definition/prefab/scene Inspector data was not edited. The shared movement's current speed is 3, pause chance .25, and decision timing 3–12 seconds.

Each plot owns a stationary AgentHomeBounds trigger. Its horizontal range uses the building footprint minus 1.6 units for body/edge clearance. Upper-house bounds also respect the supporting platform width. NPCs receive these bounds and their permanent identity during initialization. NPCs wander locally and turn at the range limits, with their current platform height preserved. They do not roam between streets, climb ladders or receive new service behavior. The Surveyor placeholder also receives this basic behavior, per the request for town NPCs.

AgentHomeBounds now exposes Configure(BoxCollider2D) for explicit runtime wiring. No saved layout/history changes are required. Inspector tasks: none; new assets are loaded automatically. Cyan shirts are retained.

Production compilation and Unity regression checks passed. Harness checks load the real new assets, verify passive-brain/wander links, configured stationary home bounds, spawn containment, and 300 production movement ticks constrained to the home range without changing platform height. Controller initialization is adapted in the harness; actual kinematic NPC/player collisions remain a live check. Reload NodeScene, watch ground and roof NPCs wander/pause/turn, then test walking past them. Commit after the live checks pass.
