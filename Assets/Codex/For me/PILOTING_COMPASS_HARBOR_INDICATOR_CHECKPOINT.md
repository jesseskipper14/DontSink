# PILOT.1 — Analog compass and piloting-only harbor indicator

Bosun 🍌 — 2026-10-04

## Changes

The harbor approach indicator now renders only inside the piloting cartridge. Its standalone main-scene OnGUI callback was removed. The lighthouse silhouette remains visible to the crew in BoatScene.

A separate analog compass appears beside the approach sketch. Its housing stays fixed with Bow at the top; the N needle shows geographic north relative to the boat. Geographic heading comes from the existing scene-owned BoatSceneWorldPositionBridge bound to the cartridge's BoatPilotingState. This avoids confusing route-local heading with world north. Missing geographic context shows **No reference**, rather than fabricating a north direction.

Heading smoothing uses the shortest angular path through 359°/0° with a 0.35-second exponential response. Session begin/end reset derived presentation; controls and shared navigation state are not reset. There is no digital heading, bearing delta, steering correction or new navigation persistence.

The indicator checks that the piloting state belongs to its bridge before rendering. Only the currently open locally owned cartridge draws these instruments; the existing runner's ownership and offline-helm checks remain. The indicator retains the existing actual berth geometry, weather visibility, fade and boat-relative orientation. The layout stays inside the cartridge play area, beside the compass. Very small play areas hide the full sketch when it cannot fit; the compass can still render when there is room.

Docking controls are unchanged: **Escape out of piloting, then E while aboard and inside the berth**, with no other interactable hovered. The indicator now says to exit piloting when inside the berth, rather than implying E works through the overlay's input capture. Existing tethers/diving-bell restrictions still apply.

## Inspector tasks

None required. No scene, prefab, material or Inspector data was edited.

Existing BoatSceneController / Harbor Presentation / Approach Panel Size still controls the indicator's preferred size. The compass uses a 96-pixel housing, reduced to fit the play area. Broader piloting layout/tuning will be revisited with the circular local viewscape checkpoint.

## Test this checkpoint

1. In BoatScene near a harbor, confirm the main-scene approach panel is gone but the lighthouse remains.
2. Open piloting. Confirm compass and close-range harbor indicator appear inside the overlay without covering throttle/rudder controls. The sketch should disappear as you leave harbor guidance range; the compass remains.
3. Turn the boat. North should move around the fixed compass housing: heading north puts N above center, east puts N to port, south puts N astern and west puts N to starboard. F4 can provide geographic heading for this debug comparison.
4. Turn through north in both directions. The needle should take the short path with no full-circle jump.
5. Reach the berth. Exit with Escape, then use the normal E dock action. Re-enter piloting and confirm the indicator does not leak into the main scene after closing.
6. Check a smaller Game view, offline/unlinked helm, menu/load and a new piloting session. Missing context should not reuse an old heading or another boat's harbor indicator.

## Files

Modified:

- `Assets/Scripts/Travel/Travel/BoatHarborPresentation.cs` — explicit cartridge-owned drawing method, viewer-state check and updated docking caption.
- `Assets/Scripts/MiniGames/Cartridges/PilotingCartridge.cs` — ticks/draws compass and harbor helper inside existing piloting lifecycle and play area.

New:

- `Assets/Scripts/Boats/Simulation/PilotingCompassRenderer.cs` plus metadata — geographic orientation query, analog compass, smoothing and responsive instrument layout.

Exact live classes inspected: PilotingCartridge, PilotingHudRenderer, PilotingOverlayRunner, PilotChairInteractable, BoatHarborPresentation and the existing navigation bridge. The HUD's numeric navigation heading is already behind DBG; normal rudder angle is a control position, not geographic heading. Existing route renderer, water reference grid, controls, camera follow and debug readouts were preserved. Their broader reconciliation is deferred to later PILOT checkpoints.

## Validation / next

Production runtime compile passed. The isolated Unity suite covers compass cardinal orientation, shortest-path north crossing/convergence and removal of the standalone overlay, alongside existing harbor presentation/mesh, bridge, coastal and navigation regressions. Adapted scene hosts are used; live layout, ownership and Escape/E flow still need the tests above.

Next after approval/commit: the circular boat-up local viewscape, then weather visibility and physical harbor integration. No circular viewscape or analog autopilot was added here. No new discovery or save data.

Commit once live tests pass; Bosun has not committed.
