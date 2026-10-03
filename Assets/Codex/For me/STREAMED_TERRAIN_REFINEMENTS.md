# Streamed terrain refinements — Bosun 🍌

## Changes

- Pinned charting/telescope equipment now adopts the owning boat's Rigidbody2D interpolation. It follows changes to that setting while deployed, and restores its original interpolation on unpin, disable, pickup, or invalidated deployment. Existing joint clearance, support checks, ownership, and collision suppression remain in effect.
- Reproduced a rope-shaped compound body's tunneling through the streamed EdgeCollider2D at high falling speed with discrete collision detection. Continuous detection caught the impact.
- WorldItem now has **Physics Safety / Prevent Ground Tunneling**, enabled by default. Dynamic item bodies use continuous detection when their body is resolved during initialization or mass refresh. This covers existing world-item prefabs without editing each prefab. Specialized items can opt out; their Rigidbody collision detection then remains authored. Continuous detection has additional physics cost, so watch performance when testing large item piles.
- World map and the map table's World Map page now expose **DEBUG: Coordinates / warp** at the top of the right panel. Enable it, right-click inside the finite world map, then use **Copy X, Y** or **Warp to point**. A cyan crosshair marks the selected point; selected coordinates use invariant, round-trip float precision. Ordinary left-click selection, dragging, and zoom remain available.
- Warp delegates to the existing authoritative BoatScene geographic warp. It requires exactly one active ready projection, changes geographic navigation, and keeps the physical boat/crew/local terrain in place. It is session-only. It does not turn the map into a repeating sheet.

## Inspector changes applied with approval

1. `Assets/Resources/Prefabs/Items/Items/Rope.prefab`: Rigidbody2D **Collision Detection: Discrete → Continuous**.
2. `Assets/Resources/Prefabs/Items/Items/Strong Rope.prefab`: Rigidbody2D **Collision Detection: Discrete → Continuous**.

No scene wiring, collision matrix, terrain-profile settings, or instrument-prefab settings were edited in this refinement. Interpolation is managed while pinned at runtime. The new WorldItem checkbox defaults on; no manual wiring is needed.

## Validation and limits

Full production runtime compilation succeeded. The Unity harness passed 2,423 assertions: 1,343 streamed terrain, 21 voyage, 941 wrapping, 47 camera, and 71 pinning. New physics checks use rope's two offset circle solids and actual streamed collider geometry; discrete detection reproduced the failure, continuous detection prevented it. New pinning checks cover a 40-unit/second moving boat, inherited interpolation, changing the boat's interpolation, and restoring the original on unpin. Unrelated actor hosts are adapted in this harness, so these are not full production scene tests.

The map UI and visible shimmer need Play Mode verification. This checkpoint still has broad fixed seabed depth with small rolling variation. The geographic depth integration is the next pass; warping to a deep map region will not yet deepen the local seabed.

## Quick Play Mode checklist

1. Pin both instruments, accelerate, and watch them relative to the boat. Unpin/redeploy and save/load to check the same behavior.
2. Drop rope and a few other small items into deep water; verify they settle on the streamed seabed. A still-owned item intentionally ignores world ground until ownership is cleared, as before.
3. Open World Map, enable the coordinate debug section, right-click a point, copy it, and warp during an active BoatScene voyage. F4's true world coordinates should agree with the picked point; local physical position should not jump.
4. Check zoom/pan, node selection, map-table physical-piece dragging, and ordinary travel controls. Right-click outside the world bounds should report an invalid selection; docked/NodeScene warp should explain why unavailable.

Commit after these checks pass. The existing streamed-terrain checkpoint and your scene/material edits are also still in the working tree; this report describes only the refinement changes above.

## Follow-up: map sidebar overflow

The screenshot exposed fixed-position sidebar controls extending below the map-table window. Both World Map sidebars now use bounded scroll views with measured content heights. Expanded coordinates, celestial tools, dropdowns, node buffs/events, and travel actions remain reachable. Topography toggles now have enough vertical spacing to avoid overlapping click areas. Central map layout and shared page registration remain unchanged. No Inspector settings were changed for this follow-up.

Production compilation passes. In Play Mode, enable the checkbox at the top of the right sidebar, right-click the map to select a point, and use Copy/Warp. Scroll each sidebar independently to reach its lower controls, including at the smaller Game view size from the screenshot. Other UIs are a separate backlog item.

The launch exceptions were found during Editor assembly reload, in Unity's GameObject/MeshRenderer/Transform Inspector code. Their target is not identified by the log. Try unlocking Inspector tabs and selecting a stable scene object or asset; close/reopen an affected Inspector if needed. This pass does not claim to fix Unity's Inspector exceptions or suppress their logging.
