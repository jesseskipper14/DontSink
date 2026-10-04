# Terrain feature planning seam 🍌

## Implemented

`BoatTerrainFeatureDirective` is an immutable request identified by a stable ID, a signed voyage-strip center, half-width, and depth delta. Positive delta requests a trench; negative requests a rise. The offset has finite support with smooth entry/exit and center. It is applied to desired macro depth **before** depth clamps, slope safety, and chunk commitment. It never deletes or replaces the physical floor.

`BoatTerrainPlan` copies and validates requests. IDs must be unique; coordinates/depth must be finite; half-width must be at least the plan's chunk width. Invalid replacements leave the previous forecast intact. Existing slope constraints may attenuate or extend the requested shape; requested amplitude is not a promise to violate slope or depth limits.

`BoatTerrainStreamer2D.TrySetForecastFeatures` is the host planning entry point for future geographic/feature planners. It replaces all pending requests and rejects non-authoritative/unprepared calls. Already committed depth knots do not change. Cancelled hidden features disappear from later uncommitted ground; committed portions remain in journey history and reproduce when chunks reload. Requests do not consume any RNG channel.

The existing profile has one development proof feature, disabled by default and accepted only in Editor/development builds. Its default center is strip 1024, half-width 512, depth delta +100. It is anchored to the voyage's spawn-relative physical strip, independent of camera movement and geographic debug warp. Profile settings are captured when the voyage plan is created. F4 shows the current forecast directive count; geographic target depth remains the baseline request, while actual seabed Y includes committed feature shaping.

No island, resource, networking, or terrain destruction implementation was added. No Inspector assets were edited.

## Inspector tasks

On your existing `Assets/Defs/WorldMap/SeaBiomeGenType/BoatTerrainProfile.asset`:

1. Under **Development Proof Feature**, enable **Enable Debug Feature**.
2. For the first proof, keep **Debug Feature Center = 1024**, **Debug Feature Half Width = 512**, and **Debug Feature Depth Delta = 100**. With 128-wide chunks this is broad enough to read clearly, and starts beyond the initial preload region. A half-width smaller than a chunk is raised to a chunk for this profile-driven debug request.
3. Start/re-enter BoatScene so the plan captures those settings. F4 should show **Terrain feature directives: 1**.
4. After testing, disable the toggle and re-enter BoatScene to return to ordinary generation.

There are no new references, prefabs, components, or scene wiring to assign.

## Live test

- Choose a shallow/moderate geographic region, then sail physically forward through strip 512–1536. Expect a descent toward a trench around strip 1024 and a smooth return. Committed nearby geography can influence the exact shape, and slope safety can reduce the requested peak.
- If the baseline is already at maximum depth, a +100 trench is clamped and may be invisible. Use a shallow region, or test **Depth Delta = -100** on a fresh visit for a rise instead. Rises stay underwater (minimum depth 1).
- For an isolated shape check, you may temporarily disable geographic depth and use base depth 120 with rolling amplitude 0; restore those settings afterward. This is optional and requires your manual Inspector changes.
- Reverse to the feature after unloading/reloading its chunks: the visited shape must remain identical. Deploy a bell/anchor or drop rope at the feature and a chunk seam to verify contact.
- The cancellation/replacement API is tested in the harness; no extra runtime feature-editing UI was added. Changing the profile in Play Mode intentionally does not edit the current plan.

## Validation

Production C# compilation and focused whitespace checks passed. Unity harness passed **7,086 assertions**: 6,006 terrain, 21 voyage, 941 wrapping, 47 camera, 71 pinning. New checks cover copied requests, supported span/center, slope safety, shared edges, preserving committed surfaces after directive removal, cancelling hidden features, excessive-rise clamping, invalid/duplicate requests, and host/client planning permissions. The harness uses production terrain code with adapted unrelated hosts; live visuals and contact remain Play Mode checks.

Existing handoff persistence policy remains: commitment history is session-only and approximate geometry regeneration across visits is allowed, with safe actor restore. Future geographic planners can supply directives through the new entry point; automatic geographic feature selection is not implemented here.

Commit after the live checks pass. The next contained pass is geographic land encounter detection, before island presentation and boat obstruction.

## Refinement: trench walls rather than a rounded valley

The directive now supports **Edge Fraction** and **Maximum Slope Degrees**. Edge fraction 1 retains the original rounded feature. A smaller fraction concentrates depth change at the outer walls and leaves the middle at full requested depth. The profile-driven debug feature defaults to fraction **0.15** and a separate **75°** wall cap. Ordinary geographic transitions retain their existing limits; the larger cap is available where explicit features require it. Committed slope metadata retains only the rate actually needed by history, avoiding propagation of an unused steep cap into the rest of the voyage.

Inspector tasks (apply manually): on the existing terrain profile set **Debug Feature Edge Fraction = 0.15** and **Debug Feature Maximum Slope Degrees = 75**. Keep your existing center, half-width, and depth delta for the comparison. Re-enter BoatScene: existing committed valley geometry is intentionally unchanged. No Inspector values were edited by Bosun.

With 128-unit chunks and a 100-unit trench, the isolated shape test measured peak walls of **55.68°** with a flat bottom. The cap is not a guarantee of an exact angle: chunk resolution and feature depth/span also determine the resulting shape. Smaller chunks can resolve narrower walls if a later visual test calls for it; no chunk settings were changed here.

Current refinement validation: full production runtime compilation passes. A rebuilt focused Unity shape harness passed **5,656 assertions**, covering sharp finite-support walls, flat physical bottom, shared endpoints, underwater/depth limits, feature wall cap, history after removing the directive, ordinary 40° transitions away from the feature, and invalid wall settings. This is a focused planner test, not a rerun of the earlier complete regression harness. The finite topology guard is adapted; profile/math/planner/directive sources are production. Live bell/anchor/rope contact on the steeper walls remains a Play Mode check.
