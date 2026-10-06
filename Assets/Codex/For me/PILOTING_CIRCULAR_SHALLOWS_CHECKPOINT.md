# Circular piloting view and shallow-water indications

## Current refinement

- Main view is now a circular scope with square projection, reserving room for the existing compass, broad viewscape and harbor approach panels.
- The boat remains exactly centered; the former vertical offset, delayed follow and course/yaw camera look-ahead are removed. Heading still rotates the boat icon. Sailing physics and control input are unchanged.
- Initial physical view diameter is four times the incoming view-height setting (90 becomes 360 physical units). This is runtime presentation setup, not a saved Inspector edit. Explicit later debug zoom changes still set the actual diameter.
- Green shows above-water land. Amber shows caution shallows. Red diagonal hatching shows water near/below the hull-clearance threshold. The legend appears beneath the scope.
- Hazard shading uses the geographic depth curve and the coastal blend used by BoatCoastalSurface, with hull draft, harbor depth clearance and rolling-amplitude allowance. This makes near-sea-level water visibly hazardous even when it is not green land. These are geographic depth indications; streamed terrain can temporarily differ during slope transitions and may include additional local features.
- Existing waves, water motion and berth overlays remain inside the scope. A one-time generated circular mask clips all of them at the rim. The reference grid and route diagnostics appear only with DBG enabled.
- The existing miles-wide secondary view and GPU land rendering remain intact. There are no recurring per-pixel CPU land or depth scans. Depth-curve texture refresh occurs on profile changes or at ten-second intervals.

## Inspector tasks

None. No saved scene, prefab, material, or Inspector settings changed.

## Verification and play check

Production compilation passed. Direct3D11 checks passed 16,411 assertions including coast/wrap/projection/resource regressions, circular corners, green dry land, red near-coast grounding risk, amber shallows and deeper-water exclusion. Existing circular-view regressions passed 379 assertions; existing harbor presentation regressions also passed. The actual cartridge layout and physical grounding still need in-game verification.

Reopen piloting at the beached location, check that the boat is centered and nearby shallows are visible, then move/turn toward deeper water. Inspect the coast and berth overlay, small-window layout, and close/reopen behavior. Try this diameter before tuning it again. Commit once the feel is acceptable.

## Next checkpoint: main menu

Return to the unresolved main-menu issues after this piloting refinement. Do not move on to the next feature handoff and forget the menu. The specific menu symptoms have not yet been provided; gather them when resuming that work.
