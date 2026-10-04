# Physical coastal grounding prototype

2026-10-04. Replaces the previous separate background island / invisible boat barrier approach for this experiment, per Skip's new direction. Sharp evasive turns must be usable; modest map/coast mismatch is acceptable.

## Behavior

- The existing streamed ground now rises from seabed through the waterline into land along the current geographic heading. One set of surface samples drives mesh, EdgeCollider2D and ground sampling.
- Geographic sea-level height maps to exactly zero depth. The previous minimum 15m coastline depth is removed locally, blending into the existing committed seabed away from shore. Above-water height is a new local interpretation of normalized map height, with a 40-unit maximum; the map has no existing physical land-elevation calibration.
- Initial/new coastal coverage starts at its target immediately. Existing coverage follows course changes with bounded uplift (30 units/sec) and faster lowering (120 units/sec).
- Every physics step queries nearby non-static bodies that can collide with Ground. Their bounds prevent new ground uplift through the bodies, with 0.1 clearance and a 60-degree clearance envelope outside their footprint. Existing contact is not erased just because the clearance envelope overlaps it. Turning toward water may lower the ground and release the obstruction.
- Dynamic sample heights use shared keys at chunk boundaries. Only currently loaded samples are retained. The coastal surface can change with heading and does not promise exact frozen geometry on reversing/reload; deep-water terrain and feature planning retain their existing committed baseline.
- Players, the diving bell, anchors and items can now interact with the actual coastal ground. This intentionally supersedes the old boat-only barrier/free-underwater-island requirement.
- The distant approaching/receding island silhouette remains a separate visual object, with its existing fade, scale and stable presentation. The rising local seafloor supplies physical grounding independently. Only the old barrier/navigation clamp disengages automatically while physical coasts are active; no scene components were removed.
- NodeScene generation is unchanged. Queued inventory, hatch, carrier velocity, diving-bell and darkness bugs remain deferred. This is not the queued general slope-tuning pass.

## Inspector checklist

No Inspector assets, scenes, prefabs, materials or physics settings were edited by Bosun in this prototype. Existing uncommitted user asset edits were preserved.

1. On the existing BoatTerrainStreamer2D, confirm **Use Physical Coasts = true**. No new component or profile assignment is needed. The new parameters are on the streamer, not BoatTerrainProfile.
2. Confirm the actual hull collider layer collides with **Ground**, including any collider/Rigidbody layer overrides. Hull/Ground is enabled in the current project matrix; check any hull parts on other layers.
3. Set the main BoatRoot Rigidbody2D to **Continuous** collision detection for fast approaches if it is still Discrete. The prefab currently remains Discrete; no automatic change was made.
4. Keep the streamer's current material/sorting settings for the first test. The island now uses the ground's rendering settings, not BoatIslandVisual2D's WorldBackdrop settings. Water and actor sorting should be checked in the live scene.
5. Keep BoatIslandVisual2D enabled for distant approaching/receding presentation. BoatGeographicObstruction2D automatically steps aside while physical coasts are active; you may disable that component manually for clarity. BoatLandBarrier is not needed by the new terrain backend.

To roll back the experiment: turn off Use Physical Coasts and reload BoatScene so old coastal geometry is cleared. Re-enable the previous island/barrier components if you disabled them. Toggling the option mid-scene is not a full geometry reset.

## Live acceptance test

Use the world-map debug coordinates/warp to start just offshore, then test:

1. Approach land slowly. Ground should visibly reach the waterline, rise above it, and contact the hull at that same surface. F4 should report physical coasts active and barrier retired. Signed geographic target depth becomes negative on land.
2. Repeat at cruising speed. Watch for tunnelling, hull climbing, excessive pitching, chatter, or terrain forming through the boat. Real buoyancy/assembled hull behavior is not certified by the isolated tests.
3. Before impact, make a sharp 90-degree or larger course change into open water. Previously projected coast should lower promptly; there should be no invisible wall left on the old course. Small coast/map offsets are expected.
4. Turn parallel to shore, then away. Check that the terrain responds rather than continuing to enforce the previous approach.
5. After grounding, reverse and/or turn back toward water. Check that retreat remains practical.
6. Drop an item, dive with the bell, and put the player on nearby ground. Check for uplift/clipping during a turn. Lowering terrain can remove support and cause a fall; this prototype favors releasing obsolete land quickly.
7. Cross several chunk boundaries and inspect the coastline mesh/collision for gaps. Check open ocean/trench regression and save/load near the coast.

If boat handling fails, note position, heading, speed, F4 target/current ground, and whether the hull climbed the visible slope or hit something invisible. This pass should be tuned against those observations before committing.

## Validation

Production runtime compilation passed. Isolated Unity tests exercise shoreline continuity, body-clearance limits, mesh/collider/sampler agreement, physical slope contact, course-change release, reverse motion, and the production streamer with adapted navigation/save hosts. The streamer copy differs only in its class name to coexist with the earlier island-test host.

Limits: terrain is a local 2D course interpretation, not an exact top-down coastline cross-section that stays immutable while steering. Existing deep-water commits still limit exact depth matching outside the coastal band. Virtual environmental drift is no longer clamped by the retired geographic barrier; coastline accuracy under strong sideways drift needs live observation. Terrain-query capacity is 2048 nearby colliders; on overflow updates pause and existing ground remains.

Checkpoint: live-test and tune first, then commit if it feels right. Do not advance to the next feature pass yet.

## Follow-up: self-protection fix

The F4 clearance diagnostic identified SeaFloor/Seabed_-1 as the limiting "body". Streamed chunk edges inherit the scene SeaFloor Rigidbody2D. When that parent is non-static, the broad actor filter incorrectly includes the terrain itself and prevents it from rising. Streamed chunk colliders are now explicitly excluded from actor protection. A regression fixture adds a kinematic Rigidbody2D to the terrain parent and checks both above-water coastal generation and the absence of seabed in the limiting-body diagnostic.

No Inspector change is required. Reload BoatScene after recompilation. This addresses the false depth ceiling; one-time startup placement of an already inland boat above the real land remains a separate unresolved issue. The guard will still legitimately limit uplift through the actual hull until startup placement is handled.
