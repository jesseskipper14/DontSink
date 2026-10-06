# Node foliage and island landscape

Implemented small foliage, latitude-based background trees, and two island ridge silhouettes. No saved scenes, prefabs, materials, or Inspector settings were changed. No Inspector tasks are required.

## Placement

`NodeNaturePlanner` creates stable candidate IDs, positions, variants, sizes, and populated/unpopulated decisions from the existing settlement seed. Small foliage has ground and elevated-street candidates: ferns, flowers, and bushes. Building footprints (including reserved future buildings), event reserves, and ladder access are excluded. Background trees have their own candidate row and can stand visually behind houses. These objects have no colliders or gameplay behavior.

Candidates are generated before population decisions. Hash channels isolate position, occupancy, size, and appearance, without touching Unity's global random state. Reloading the same saved settlement and geography produces the same decoration. This pass does not add save data or change settlement history. It does not yet support persistent harvesting/destruction.

The graph node's map Y is normalized against the world's map bounds, with the midpoint as the equator. Absolute normalized latitude selects palms through 0.25, deciduous trees through 0.65, and pines above 0.65, symmetrically in both hemispheres. Missing geographic data emits a warning and falls back to temperate trees.

## Presentation and future art

Runtime hierarchy: `NodeContext/NodeView/NodeTown/Settlement_<id>/NodeNature`, with `SmallFoliage`, `BackgroundTrees`, and `IslandLandscape` children. Trees and landscape use WorldBackdrop; small plants use WorldBuildings. Placeholder sprites are generated once per town build and released on rebuild/unload, along with the existing settlement presentation.

The separate `NodeNaturePlan` and stable candidate IDs are the placement seam for authored sprites later. Rendering lives in `NodeSettlementScene.Nature.cs`; placement lives in `NodeNaturePlanner.cs`. Landscape is currently two static, seed-varied ridge bands tapering toward the harbor, not a shader or parallax system.

## Verification

- Full production runtime compilation passed.
- Isolated Unity harness: 56,111 nature assertions passed, covering repeatability after manifest copy, unchanged saved data/global RNG, unique IDs, populated and empty candidates, access clearance, both hemispheres, graph-coordinate latitude, silhouette transparency, collider-free presentation, and cleanup/rebuild.
- Existing settlement and broader terrain/navigation regression checks passed.
- Direct3D11 previews of palm, pine, and deciduous settlements rendered and were visually inspected. The isolated preview uses a compatible built-in sprite material; actual project lighting and scene composition still need the usual play-mode check.
- These are local cosmetic objects derived from shared saved identity, without new authoritative state writes. A real multi-client session was not tested.

## Quick play-mode checks before committing

1. Visit a node, inspect small plants in open ground/street spaces, and check ladder entrances remain clear.
2. Leave and revisit the same node; foliage locations should repeat.
3. Visit equatorial, middle-latitude, and far north/south nodes; check palm, deciduous, and pine silhouettes respectively.
4. Confirm hills and trees stay behind buildings, with no rectangular opaque backdrop or new collision.

Commit after the live visual check passes. 🍌

## Visual refinement

Hill textures increased from 512×128 to 2048×512, with subpixel alpha along the ridge to soften pixel steps. Tree/plant silhouettes increased from 32×32 to 128×128; round canopies now use 48 outline segments instead of 12. Transparent pixels retain white RGB to avoid dark filtering fringes. Rasterization is limited to triangle bounds, and hills use a direct height-field fill.

Ferns now grow from a common ground-level base and fan upward, correcting the inverted-looking arrangement. Flowers have separate upright stems and leaves. Candidate positions, population, and sizes are unchanged. No Inspector changes are needed. Production compilation and Direct3D11 visual checks passed; an additional close-up was inspected for orientation and edge quality.

## Thin stone road preview

Latest foliage placement refinement: ordinary house fronts now accept small foliage, while doorways, ladders, key/service building fronts and event reserves stay clear. Clearance accounts for plant variant and scale. See `NODE_PLACEHOLDER_PROPS_CHECKPOINT.md` for the shared placement rule.

Added a 0.24-unit-thick stone paving veneer along the ground-level town, ending before the harbor edge. Two rows use seed-varied widths and muted gray/warm stone colors with mortar gaps. This is a runtime visual under `Settlement_<id>/Stone paved road`, on GroundFront at sorting order 1. The road root uses local Y = 0.01 to prevent clipping; mortar sits slightly behind the stones in Z. Existing sand remains below it and owns all collision. No Inspector tasks are required. Production compilation and Direct3D11 rendering passed for the initial road. Re-enter the node to check the corrected placement in game.

