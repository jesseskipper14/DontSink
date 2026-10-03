# Voyage strip coordinate — checkpoint 1

2026-10-03. Implements the first dynamic-terrain checkpoint only: signed voyage distance, local/strip mapping, and F4 diagnostics. Terrain generation, NodeScene, map-table presentation, boat handling, and Inspector settings were not changed. Changes are uncommitted.

## What changed

`Assets/Scripts/Boats/Simulation/BoatVoyageStripState.cs` is a plain runtime state object owned by BoatPilotingSimulation. It accumulates signed physical displacement in a double, independently from geographic heading and longitude. Forward increases the strip; reverse decreases it. Local origin plus the configured physical scene axis provide local-to-strip and strip-to-local-axis-coordinate conversion for the upcoming terrain planner.

The existing piloting motion sample remains the source of truth. BoatPilotingSimulation observes it immediately after SampleActualBoatMotion, in the same FixedUpdate flow. No second component reads a last-tick delta and no camera/window owns the distance. New strip state only changes under GameplayAuthority. This does not implement the separate MP authority-hardening pass.

New voyage identity starts at zero with its seed and a new revision. No active voyage clears old state. Initial body placement is a baseline rather than travel. A missing/replaced body preserves this voyage's history and rebases its local mapping on return. Disable/enable resets the physical sample and rebases the strip mapping without inventing distance during suspension.

`BoatPilotingSimulation.RebasePhysicalTravelAfterTeleport()` is the explicit integration hook for future physical relocation: call after moving the Rigidbody and before the next piloting tick. It suppresses that relocation as both physical/navigation travel and preserves strip history while remapping the local origin. Existing automatic spawn/initialization establishes a baseline. This pass does not attempt to guess teleports from velocity or wire every unrelated debug relocation tool; arbitrary direct Rigidbody moves during an active tick still count as movement unless their caller uses the hook.

Geographic F4 warp does not move the physical body, change local navigation, or call the physical rebase. It therefore leaves strip distance and mapping alone. Terrain remains unchanged by either ordinary sailing or geographic warp in this checkpoint.

## Inspector tasks

None. No new component needs adding. The new state is owned by the existing piloting component. The existing F4 window shows:

- Voyage strip signed distance and last physical delta.
- Local origin coordinate and physical scene axis.
- Voyage revision, seed, accepted sample count, and rebase count.

The existing “Reset distance measurement” button resets debug measurement totals only; it does not reset authoritative strip history.

## Verification

Production runtime compilation and source whitespace checks passed. The isolated Unity harness uses the exact new production state class for 21 assertions covering forward/reverse, vertical-independent axis conversion, authority rejection, invalid input, spawn baseline, missing/replaced bodies, new-voyage/menu reset, physical teleport rebase/resume, and small-increment double accumulation. Existing 941 wrapping, 47 camera, and 61 pinning assertions also pass.

This is state-level automated verification, not a live test of production BoatPilotingSimulation tick ordering, actual steering, scene bootstrap, or Inspector presentation. Those require the checks below. Test artifacts remain ignored under Temp/CodexPhase7.

## Live checkpoint

1. Embark and open F4. Strip starts near zero at the initial physical baseline; subsequent ordinary motion counts even with F4 closed.
2. Sail forward, then reverse. Strip increases and decreases respectively; last delta follows direction. Stop: distance should stop apart from actual physical drift.
3. Change geographic heading, including 180 degrees, while moving physically screen-forward. Strip should still increase; true world position follows the changed heading.
4. Warp geography with F4. Strip and local origin must not jump. Sail again and confirm normal increments.
5. Reset the debug distance measurement. Strip history must remain intact.
6. Return to NodeScene/menu and start another voyage. Check clean strip origin/seed/revision for the new simulation; verify ordinary docking and NodeScene ground remain unchanged.

Commit after these checks. Next contained checkpoint is streamed BoatScene seabed, compatible multi-chunk ground queries, readiness/restore handling, and chunk-event isolation, with a separate reviewed Inspector task list.
