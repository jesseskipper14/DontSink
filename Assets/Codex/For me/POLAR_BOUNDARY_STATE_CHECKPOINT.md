# Polar boundary state checkpoint

2026-10-04. Contained implementation of the terrain/land handoff's boundary-state seam, reconciled with the newer wrapped-world decision.

## Result

- X remains periodic. Crossing the east/west seam never creates a boundary warning.
- Y remains finite, with north/south polar approach bands inside the existing world bounds. The newer wrapping handoff's polar-ice fiction supersedes the older no-wrap rectangular-world/storm-boundary proposal.
- Default warning band: outer 6% of world height at either pole, matching WorldTopology's existing default polar ice band. Default dangerous core: outer 2%.
- WorldBoundaryQuery.Evaluate returns Unavailable, Normal, Soft or Hard; nearest pole; signed distance to the finite-Y edge; band widths; continuous severity 0–1; and inward return direction. Exact outer edge is Hard with severity 1. Outside-Y input is also Hard, without wrapping Y.
- WorldBoundaryService.TryGetCurrent derives this context from current shared navigation and WorldTopologyService bounds on each read. It retains no static hazard state across scene changes, saves or new games. Missing navigation/bounds produces Unavailable rather than stale state.
- Read-only consumers may use the service on host or clients. Future weather, damage or resource consequences must be gated by GameplayAuthority and replicated through their own systems.
- Band fractions can be supplied explicitly through either API. Invalid/nonfinite configuration returns Unavailable. No extra component or Inspector profile is required.

This checkpoint adds geographic context and debug warnings. It does not yet connect severity to weather, damage, sinking or additional ice content. Existing finite-Y navigation validation and existing polar geography remain in force. It does not change physical boat motion, X wrapping, map presentation, engine propulsion, grounding friction, harbor arrival or coastal startup placement.

## Inspector tasks

None. No scenes, prefabs, materials, profiles, collision settings or Inspector assets were changed. The existing F4 window consumes the new query automatically.

## Live test

1. In BoatScene, open F4. Note your current coordinates so you can return after testing.
2. In the lower warp section, select **Fill north warning**, then **Warp geography**. Near the top of F4, expect **Polar boundary: Soft**, nearest pole North, roughly 33% severity, and a return-south warning.
3. Select **Fill north core**, then Warp. Expect Hard, roughly 83% severity, and a dangerous-polar-core message.
4. Repeat the south warning/core presets. The classifications should match, with return direction north.
5. Warp back to an interior coordinate. Expect Normal and 0% severity immediately.
6. At the same Y, warp X by plus/minus one world width, or cross the seam normally. Polar classification and severity must remain unchanged.
7. Load another save or start a new game. Boundary state should follow that world's current position/bounds, not retain the previous test's pole/severity.

The presets fill coordinate fields only; the existing Warp geography button applies them. They preserve current X, choose positions inside finite Y and remain authority-gated. Terrain/geography changes caused by the existing warp system are expected; no boundary damage should occur in this checkpoint.

## Validation and next checkpoint

Production runtime C# compilation passes. Isolated Unity checks cover both poles, band thresholds, severity progression, edge/outside handling, invalid inputs/configuration, large wrapped-X displacements, current-context replacement and read-only service behavior. The query and facade are production sources; topology/navigation providers are adapted test hosts. Actual F4 layout/controls and save transitions still require the live checks above.

After live checks, commit this checkpoint. Remaining terrain/land handoff work: destination/arrival integration following the newer harbor handoff, and coastal startup/restore placement safety. Queued engine/submersion and boat–ground friction fixes remain in BUGS.md.
