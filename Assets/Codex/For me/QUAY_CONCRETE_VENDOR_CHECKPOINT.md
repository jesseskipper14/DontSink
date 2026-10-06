# Quay concrete and vendor grounding

2026-10-06 — Bosun 🍌. No saved scene or prefab Inspector values were changed.

## Inspector task

On **BoatVendorSpawn → AgentSpawnPoint**, enable **Snap To Ground After Spawn**, then restart Play mode. The saved NodeScene currently has this disabled, so the initial embedded-spawn correction never runs. The wandering ground query still runs afterward, but intentionally cannot step upward through a tall quay. For diagnosis, enable **Debug Snap** on the spawned vendor's AgentGroundSnapper: it logs the collider providing support and the vertical correction. No Inspector settings were changed automatically.

Assign **Assets/Resources/Materials/QuayConcrete2D.mat** to the SpriteRenderer material slots on your authored Quay pieces, including QuayTop. Keep their existing sorting layers/orders and colliders. The shader is **DontSink/QuayConcrete2D**: muted grey concrete with subtle static grain, mottling and small pores. Color, grain strength and pore amount are adjustable. It respects sprite alpha/tint and the existing depth/daylight lighting globals; this is not a new lighting pass.

## Vendor fix

GroundWanderWithinHomeBounds previously moved NPCs horizontally at an unchanged Y and only grounded them at initial spawn. NpcBase is kinematic, so gravity never brought a wandering vendor down to a lower dock. It now resolves support at the proposed position before MovePosition, including when paused. If support is absent, it stops/turns back rather than walking over a gap at a fixed height.

AgentGroundSnapper now synchronizes spawn transforms before its initial query, resolves a missing body collider, ignores its own colliders/triggers/side walls/start-inside hits, and avoids snapping onto overhead surfaces above its feet. New Maximum Step Up defaults to 0.5. Existing Ground Mask still governs allowed support; your quay is already on WorldLedge, included by NpcBase's mask. No vendor prefab setting change is required.

Initial spawning is separate from walking: the 0.5 step-up limit applies only to movement. If the spawn anchor starts inside overlapping quay pieces or a CompositeCollider2D, the initial ray starts above the containing solids and resolves the actual surface at that X. This lifts the NPC's feet onto the quay instead of leaving it buried. Home bounds are unchanged.

## Check before committing

- Apply the concrete material and check the look above/below water.
- Watch the vendor walk from quay to lower dock, pause, and turn at gaps. Its feet should follow the actual collision surface.
- If it still hovers in one place, check that the visible surface's collider matches its artwork and is in the vendor Ground Mask.

Modified: AgentGroundSnapper.cs and GroundWanderWithinHomeBoundsMovementDefinition.cs. New: QuayConcrete2D.shader and QuayConcrete2D.mat plus .meta files.

Full production runtime compilation passed. All 15 isolated Unity assertions passed, including embedded spawns in overlapping quay pieces and a real CompositeCollider2D, spawn foot alignment, following lower surfaces, paused grounding, ignoring overhead ledges, missing-support behavior, and concrete shader compilation on D3D11. Reload NodeScene to verify your authored quay and vendor placement. The mooring implementation was not changed by this small pass.
