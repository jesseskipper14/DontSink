# Ocean cruising refinements 🍌

## Player grip on moving floors

The walking cap used world-space horizontal velocity. A boat faster than walking speed therefore prevented normal forward walking even if the player was standing on its deck.

`CharacterMotor2D.TryGetMovingSupport` now requires a real upward supporting contact with a simulated dynamic/kinematic floor body. It uses that body's point velocity, including rotation; this works with the boat's existing kinematic ghost floor. A boarding volume or ground-probe overlap alone never carries the player. Ascending separation from the floor rejects stale jump contacts.

`CharacterMoveForce` measures walking/sprinting speed relative to that floor. While the same support remains contacted, its horizontal velocity changes carry the player without deleting their relative walking velocity. Bounded horizontal grip stops idle slip and corrects excessive slip during boarding/movement. It does not change vertical velocity or attach a joint. Grip and carry stop on takeoff, lost contact, climbing, disabled movement, and static terrain.

No collider friction material or prefab was changed. The new **Moving Floor Grip Acceleration** on CharacterMoveForce defaults to **60 physical units/s²**. This is gameplay traction, not a claim of physically simulated shoe friction. Existing force/input/attribute paths remain in use.

## Island texture and visibility

The terrain shader's procedural dirt/strata sampled world position. Moving/scaling a silhouette through that pattern caused texture swimming. `NodeGroundTerrain` now has an optional object-space pattern branch, disabled by default. BoatIslandVisual2D enables it only through its renderer property block, with a fixed reference scale captured when an island appears. Pattern features move and scale with the silhouette. Waterline/depth colors remain tied to the actual terrain waterline. Shared material assets and ordinary world-space seabed/NodeScene pattern behavior remain unchanged.

**Visibility Range Multiplier = 0.65** on BoatIslandVisual2D shortens the visual visibility distance by **35%**, including its proximity fade band. F4 still reports the full geographic detection range; the visual begins inside 65% of that range. Fade Seconds and Smoothing Seconds remain separate timing controls; their one-second defaults were not shortened.

## Smoother ordinary seabed descent

Every macro knot previously used quintic interpolation with zero slope at both ends, creating a flat shelf at each chunk boundary even during a sustained descent. Ordinary geographic depth now uses monotone cubic Hermite interpolation with tangents frozen when each knot commits. Sloped terrain can continue through a boundary instead of flattening there. End tangents are limited to the interval secant, preventing overshoot and retaining the existing slope budget.

Explicit terrain features retain their existing quintic shaping and frozen feature metadata, preserving the trench walls/flat floor. Already committed geometry does not change when future geography/directives change. Re-enter BoatScene for a clean comparison; a running plan is intentionally not retroactively reshaped. Truly flat geographic targets can still produce flat terrain, and rapidly changing target/history constraints can still change gradients.

## Inspector tasks — manual review

No scene, prefab, physics-material, terrain-profile asset, or material asset settings were edited by Bosun. Your existing scene/material edits were left intact.

1. On the existing **BoatIslandVisual2D**, check the newly added **Visibility Range Multiplier = 0.65**. Keep your other visual tuning. Island Material should still use **DontSink/NodeGroundTerrain**; you do not need to turn on the object-pattern toggle on the material asset.
2. On the player prefab's **CharacterMoveForce**, review **Moving Floor Grip Acceleration = 60**. No extra component/reference/physics material is required. Actual deck support must remain in the motor's Ground Mask, as before.
3. No terrain Inspector change is required. Start a fresh BoatScene visit before judging the new ordinary transition shaping.

## Live tests

- At maximum boat speed, release movement and stand on the deck, then walk forward/backward and sprint. Relative walking speed should remain usable. Repeat during acceleration and reversing the boat.
- Jump while cruising, then change boat speed while airborne. You should retain takeoff momentum but receive no further floor carry. Check ladders, walking onto/off the deck, wading/swimming, and ordinary NodeScene ground for regression.
- Watch an island while approaching, alongside, and receding: the dirt/strata should stay attached to the shape. Confirm the shorter visual range and smooth fade. Compare the island with ordinary floor/NodeScene rendering, which should retain their prior pattern behavior.
- Sail a sustained geographic descent on a fresh visit. Look for fewer repeated shelves at chunk boundaries. Test chunk reloads and bell/anchor/rope contact; run the explicit trench proof again if enabled.

Node spacing is pinned as a separate generation/scale tuning issue in `Assets/Codex/BUGS.md`; world size and node placement were not changed. The earlier sandy-shore/map-truth mismatch remains on that backlog too.

## Validation

Full production runtime C# compilation passes. The focused Unity harness passed **20,316 assertions**: 8 moving-floor physics, 6,559 ordinary terrain smoothing, 788 island projection, 657 mesh/lifecycle, 3 shader import, 6,645 geographic land, and 5,656 feature/planner. It uses production motor/movement/terrain/projection/renderer/query sources with adapted scene, input and attribute hosts. The moving-floor test uses actual Unity 2D physics, a zero-friction capsule and a 20-unit/s kinematic floor; it checks idle grip, relative forward/reverse walking, sprinting, accelerating support, jump release and probe-only overlap. Terrain checks cover seam slope during a linear descent, frozen geometry after geography changes, monotonic no-overshoot transitions and slope caps, alongside the existing explicit-feature regressions.

Renderer tests verify an object-pattern property block and a fixed pattern reference scale across movement/resizing. Shader import is checked against local copies of the project's shader libraries; this is not a rendered visual comparison. Full-game contact, ghost-floor synchronization, water ordering and texture appearance still need the live checks above.

Commit after those live checks pass. Geographic boat obstruction remains the next feature pass.
