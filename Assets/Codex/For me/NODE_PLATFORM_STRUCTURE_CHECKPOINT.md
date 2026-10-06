# Town platform structure

Town platforms now use 0.4-unit-thick decks instead of 0.18, with a lighter top trim. Collider thickness follows the visual thickness downward: offset = -0.2, so the walkable top stays at the existing manifest height. One-way platforms, HatchLedge behavior, roof stacking, and ladder exit heights remain in use.

Elevated terraces now have timber railings, upright support posts, and diagonal knee braces. Railings have two horizontal rails and posts spaced no more than 2.5 units apart. They omit reserved building footprints and leave 2.5-unit-wide openings at ladder access, including support-owned ladders. Support posts are spaced roughly every eight units and land on the highest lower terrace under them, or ground if none exists. Existing single skinny support visuals were replaced.

Railings and braces are cosmetic, without colliders. They do not physically prevent a player from walking off a ledge. Structural decoration is under each terrace's `Terrace timber frame` inside NodeTown's generated settlement. The thickness applies to street and roof-slot platforms; railings and trestles decorate elevated streets.

No saved Inspector, prefab, scene, or material changes. No Inspector setup required. Saved settlement positions and history were not changed.

## Verification and live checks

Production runtime compilation, isolated settlement regressions, and Direct3D11 previews passed. Platform checks confirm the thicker colliders retain their original top datum. Additional geometry checks cover ladder gaps and building frontage. The fortress preview was visually inspected; the graphics harness uses its compatible preview material rather than production URP lighting.

In play mode, re-enter a node and check ladder exits, walking and dropping through thicker platforms, and the look of support posts/railings in a tall town. Commit after the live checks pass. 🍌
