# Stable island visual projection 🍌

## What changed

`BoatIslandVisual2D` is a separate, optional BoatScene presentation component. It consumes the existing geographic land encounter and the boat's signed voyage strip. It renders one procedural mesh silhouette at the terrain waterline using an assigned transparent terrain material. It adds no colliders or Rigidbody, writes no navigation/discovery state, and leaves the streamed underwater floor intact.

`BoatIslandProjection` keeps local presentation separate from geographic truth. An approaching island enters from the right, grows with proximity, then settles alongside. Signed physical travel creates slow alongshore drift. Moving sufficiently farther from the closest sampled approach puts it in recession, where it trails left and shrinks. Heading and camera orientation are not inputs to screen side. Large map landmasses can fill several screens; scale is deliberately perceptual rather than exact map-to-physics geometry.

Position and scale use exponential smoothing; opacity uses a bounded fade. Default fade and smoothing times are one second. Selection changes fade the old silhouette out completely before starting the next, keeping at most one visible island. Geographic debug warps and topography-context changes retire the current presentation before restarting. Scene/voyage changes and component disable clear presentation. The runtime mesh and child are owned and cleaned up by this component; shared material assets are not modified.

Recession is sticky for this encounter: steering back toward the same island does not immediately flip it from left to right. A new encounter starts after it is released/retired. This is an initial continuity rule to evaluate in playtesting, not a full encounter-history system. A loaded boat already very close to land starts alongside rather than forcing a fake approach.

The shoreline-color mismatch you reported is pinned in `Assets/Codex/BUGS.md`. Map colors and gameplay classification were not changed in this pass.

## Inspector setup — perform manually

No scenes, prefabs, materials, or existing Inspector asset values were edited. Until you wire this component, there will be no new island visual.

1. Open **BoatScene** outside Play Mode. Create an empty root GameObject named **BoatIslandVisuals**. Keep position/rotation zero and scale **(1,1,1)**. Use a root object, not a boat child; it should remain upright independently of boat pitch.
2. Add **BoatIslandVisual2D** to it. No MeshFilter, MeshRenderer, collider, or Rigidbody needs to be added manually; the component creates its own mesh child at runtime.
3. Assign **Island Material** to the same material used by your streamed terrain: `Assets/Resources/Materials/Ground Materials/GroundMaterial_Style_1.mat` (the BoatScene streamer's current ground-material reference). It uses `DontSink/NodeGroundTerrain`, which supports alpha tint. Reuse the material without editing its asset settings.
4. Leave **Simulation** empty: the component resolves the single active BoatPilotingSimulation in BoatScene after boat spawning. With multiple simulations, assign the intended one explicitly if available; automatic resolution fails closed on ambiguity.
5. Keep **Sorting Layer Name = WorldBackdrop**, **Sorting Order = 0**, **Tint = white** initially. This layer is behind boats/world objects and the foreground water layers. Verify your actual water covers the submerged silhouette portion in Play Mode; do not move it onto a foreground/boat layer to fix water ordering.
6. Initial tuning: **Width Per Map Unit = 4**, **Minimum Width = 36**, **Maximum Width = 360**, **Maximum Height = 30**, **Approach Offset = 50**, **Recession Margin Map Units = 2**, **Alongside Parallax = 0.15**, **Fade Seconds = 1**, **Smoothing Seconds = 1**. These are visual settings on the new component, separate from geographic detection ranges on the terrain profile.

The runtime child is on Ignore Raycast and contains only rendering components. The waterline comes from the streamer's terrain profile. There is no new shader or material asset to configure.

## Live checks

- In open water with no detected land, there should be no silhouette. Use F4 to verify encounter availability and distance; the visual only runs with the streamed terrain and active voyage.
- Warp just inside a detected island's visibility range, then sail closer. Expect a soft appearance on the right and gradual growth, without a position/scale blink.
- Near land, turn/spin the geographic heading while stationary. The silhouette should not orbit or jump sides. Sail alongshore: its detail should drift slowly rather than whip around with heading.
- Move away: after distance grows beyond the closest approach by the recession margin, expect it to trail left, shrink and fade. Test reversing and returning as well; note whether the sticky recession rule needs refinement.
- Compare small and large islands. Large landmasses may fill the horizon and span several screens; tune width/height only after this comparison.
- Warp between different islands while watching the old one. Expect old-out, then new-in, rather than a visible mesh swap. Leave/re-enter BoatScene and load a different voyage to check cleanup and fresh context.
- Check normal view and telescope view, including a larger monitor: silhouette ordering, water overlay, fade, and sky readability. No camera bounds or telescope controls were changed.
- Deploy a bell/anchor or drop rope near an island. The original floor still supports them; the island is visual only and has no geographic/physical obstruction yet.

## Validation

Full production runtime C# compilation passes. The isolated Unity harness passed **13,744 assertions**: **788** island projection, **655** mesh/lifecycle, **6,645** geographic land, and **5,656** terrain feature/planner. Projection checks cover gradual approach/growth, alongshore drift, recession hysteresis, leftward recession, bounded fades, selection retirement, stationary convergence, pause/long-frame behavior, clearing, and large-island size. Mesh checks cover finite geometry, valid triangles/bounds, deterministic silhouette, terrain waterline/property blocks, material preservation, absence of physics components, and owned-object cleanup.

Scene/boat/navigation hosts are adapted in the isolated renderer tests; edit-mode tests invoke runtime lifecycle callbacks explicitly. The real NodeGroundTerrain shader appearance, water sorting, telescope integration, and large-monitor framing require the live checks above. The mesh test uses a built-in transparent material, not a rendered full-game visual comparison.

Geographic detection retains its previously documented cell-center approximation. This silhouette is not a geographic coastline mesh or a hull-clearance guarantee. Current network clients still need the future replicated encounter/navigation context work; this pass adds presentation, not an MP implementation.

Commit after Inspector setup and live tests. Next contained pass: **geographic boat obstruction**, with continuous world-space checks and no reliance on the visual mesh as collision authority. Harbor transitions remain a separate pass.
