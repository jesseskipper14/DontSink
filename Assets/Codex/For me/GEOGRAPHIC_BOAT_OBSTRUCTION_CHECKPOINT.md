# Geographic boat obstruction 🍌

## Implemented

Geographic land detection and physical enforcement are separate:

- `BoatGeographicObstructionQuery` reads topography and effective sea level. It builds a periodic-X, finite-Y distance-to-water sample field once per geography context, using a linear-time squared Euclidean distance transform. This is independent of island visuals and node identity.
- Sweeps traverse crossed topography cells. Bilinear height and water distance become quadratic along each cell segment; threshold roots split the interval so an inland peak cannot hide between water endpoints. Attempted displacement is explicit/unwrapped, including across the world seam. A bounded traversal budget fails conservatively on exceptionally long paths.
- The forbidden test combines actual topography land classification with sampled distance from water. It permits configured shoreline tolerance. If already inland, it blocks increasing penetration above the starting value while allowing equal/decreasing penetration: travel along the coast or back toward water remains possible. All-land geography with no known water blocks attempted movement.
- `BoatLandBarrierProxy` translates a blocked forward/reverse probe into an invisible tall kinematic wall. It contacts only the main boat's attached hull colliders, with zero friction/bounce. It uses collision-layer overrides plus per-collider exclusion for nearby non-boat bodies, including cargo or bells that share a hull layer. Spawned/teleported nearby colliders are synchronized before filtering. The wall latches its stopping plane rather than chasing the hull farther inland during repeated thrust. Thickness expands with speed to reduce tunneling risk.
- `BoatGeographicObstruction2D` owns the authoritative query/driver and replaceable proxy. Disable, authority loss, changed boat/voyage, geographic debug warp, and unavailable context release the barrier. Missing layer/hull wiring is reported in F4; it does not silently activate a navigation-only wall.
- `BoatPilotingSimulation` applies a final continuous geographic check before accepting observed travel plus environmental drift. This closes the independent lateral-drift bypass. It limits accepted navigation displacement/velocity without relocating the Rigidbody or inventing physical voyage-strip travel. The proxy provides the physical hull stop; crew, bell, anchors and items retain their own physical movement.

No island collider is used. No damage, grounding penalties, physical arrival/docking transition, node exemptions, player/bell obstruction, or boundary punishment was added. X wrapping remains intact. Paths leaving valid Y geography are reported unavailable and left to the subsequent boundary pass.

F4 shows **Geographic obstruction**, whether the physical barrier is on, approximate initial land penetration, allowed look-ahead probe fraction, and permitted fraction for the last actual navigation/drift step.

## Inspector tasks — perform manually

No scene, prefab, layer configuration, collision matrix, physics material asset, or existing Inspector setting was edited.

1. In **Tags and Layers**, create a dedicated physics layer named **BoatLandBarrier** in an empty user-layer slot.
2. In **Physics 2D → Layer Collision Matrix**, configure **BoatLandBarrier** to collide with **Hull** only. Exclude Player, WorldItem/BoatItem/BellItem, TetherPayload, BellInterior/BellLedge, GhostCollision, Ground, Interactable, and other layers. Runtime filtering additionally excludes non-main-boat bodies even if they use Hull. Do not put crew/items onto BoatLandBarrier.
3. In **BoatScene**, create an empty root named **BoatGeographicObstruction** with zero position/rotation and unit scale. Add **BoatGeographicObstruction2D**. Do not add your own collider or Rigidbody; it creates the runtime proxy.
4. Leave **Simulation** empty to resolve the single active scene simulation after boat spawning. Set **Hull Layers = Hull**, **Barrier Layer Name = BoatLandBarrier**. With multiple simulations, use an explicit reference if available; automatic binding rejects ambiguity.
5. Initial values: **Coast Tolerance = 1.5 map units**, **Look Ahead Seconds = 0.5**, **Minimum Probe Distance = 2 physical units**, **Barrier Thickness = 1**, **Extra Barrier Height = 50**, **Hull Clearance = 0.02**. Coast tolerance is based on sampled water distance, not exact shoreline distance; tune it against your map resolution and desired overlap. Disable/re-enable the component after changing layer/hull-mask wiring in Play Mode.
6. For fast-boat testing, set the main **BoatRoot Rigidbody2D → Collision Detection = Continuous** manually. The current BoatRoot prefab is Dynamic but Discrete. The proxy thickness helps catch fast travel; Continuous adds the appropriate solver safeguard. Do not change the body's mass, constraints, interpolation, or collider geometry for this pass.
7. Keep the barrier layer out of broad interaction/drop raycast masks if those masks currently include every physics layer. It is a physics-only implementation object, not a clickable item or valid world-drop surface. The already reported inventory-drop regression predates this barrier; its diagnosis remains queued for the bug-fix pass.

There is no new physics-material asset to assign: the proxy owns a transient zero-friction, zero-bounce material. Existing boat/crew collision settings are otherwise retained.

## Live checks

1. Start offshore and open F4. Expect **clear geographic path** with barrier off. A missing-layer/hull status means setup is incomplete, not that land detection failed.
2. Sail toward a clear island coast at low speed, then maximum speed. The main boat should physically stop near/within the tolerated shoreline. F4 should show a forward/reverse barrier. The first exact `LAND` pixel need not immediately stop the boat because shore tolerance is intentional.
3. Hold thrust into the coast for several seconds. Watch for creeping, hull rotation, chatter, climbing, or buoyancy fighting the wall. The isolated box test is stable; your actual assembled boat and buoyancy still require this check. If unstable, report it before broad tuning; the enforcement backend can be replaced without rewriting geography detection.
4. Reverse away. The barrier should release when attempted movement goes toward water. Test steering along the coast as well; parallel accepted navigation should not be treated as deeper penetration.
5. While the boat is stopped, jump off, deploy bell/anchor, and drop cargo. These must remain free to move and contact the existing floor. Check pinned equipment and ghost-floor contacts during the boat's stop too.
6. Warp offshore, including across a wrapping seam near land. Old barriers must disappear. Warp deliberately inland to test the equal/decreasing-penetration escape rule; deeply arbitrary test points may require choosing a heading toward water. No automatic offshore relocation or harbor exemption exists yet.
7. Compare environmental drift near shore. F4's last navigation fraction can fall below 100% even while the physical barrier is off if virtual lateral drift alone was constrained. The barrier still handles actual hull travel separately.
8. Leave/re-enter BoatScene, disable/re-enable the driver, and load another voyage. Check fresh binding/context and cleanup of `BoatGeographicBarrierRuntime`.

## Validation and limitations

Full production runtime C# compilation passes. The focused Unity harness passed **21,634 assertions**, including **1,301** obstruction/distance/sweep checks, **7** real barrier-physics checks, and **10** authority/driver checks, alongside **20,316** prior terrain/island/moving-floor/shader checks. Obstruction tests compare the distance transform with a complete periodic water-distance oracle, compare continuous sweeps with a dense independent oracle, and cover finite Y, water-to-water crossing, quadratic interior peaks, flat-height inland plateaus, coastal tolerance, parallel motion, retreat and seam wrapping. Real Unity 2D physics checks main-hull-only collision, same-layer cargo exclusion, friction/bounce, continued thrust without wall creep/rotation, reverse release, and cleanup. Driver checks cover authority gating, travel/drift guard, missing projection/wiring and debug-warp release.

Scene/navigation/authority hosts are adapted in driver tests; runtime lifecycle callbacks are explicitly invoked in edit mode. Full assembled hull buoyancy/contact behavior, live scene binding and collision matrix/raycast wiring are Play Mode checks.

Penetration is a bilinear interpolation of distance to **water samples**, not exact distance to an analytic shoreline or an expanded hull footprint. The obstruction applies to the main boat's geographic reference path; physical hull size determines the proxy placement. Legacy topography can have seam sampling differences from modern periodic fields; current generation-version-3 wrapping is the tested seam case. Failed/missing geography releases enforcement with status rather than guessing land. Proxy overlap filtering has a bounded capacity (1,024 nearby colliders); overflow releases physical enforcement and reports the problem. Strongly pathological high-speed/thrust setups still require Continuous collision detection and live tuning.

## Bug backlog and next checkpoint

All six newly reported bugs are queued in the single `Assets/Codex/BUGS.md` file: inventory world-drop regression, moving hatches, release velocity inheritance, submerged bell player motion, bell-water depth lighting, and overall deep-water darkness. More aggressive **45°+ depth mismatch transitions** are queued as a separate enhancement. None was silently fixed during this obstruction pass.

Commit after setup/live tests. Next feature checkpoint is the finite-Y boundary-state seam, preserving periodic X; new harbor transition requirements supersede the old generic arrival proposal and remain a separate focused implementation. The requested bug-fix pass follows completion of the current feature handoff.
