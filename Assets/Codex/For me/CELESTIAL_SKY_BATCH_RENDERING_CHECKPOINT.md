# Celestial sky rendering checkpoint — 2026-10-08

## Problem and change

The user's same-scene comparison measured 287.2 FPS / 3.5 ms main CPU with the celestial renderer disabled, versus 47.8 FPS / 20.9 ms enabled. The October 8 profiler capture attributed about 17.8 ms per frame to `CelestialSkyRenderer.Update`.

Replaced the per-object GameObject/SpriteRenderer pool with meshes grouped by existing sprite style: at most twelve celestial sprite batches (ambient, six landmark styles, nebula, four deep-sky styles). Constellation lines and labels remain separate.

Query refreshes rebuild the meshes using the existing deterministic celestial objects and sprite library. Per-frame work updates a small set of material uniforms and renderer bounds. Shaders project object positions, wrap longitude, cull against the existing sky envelope, preserve pixel sizes and rotations, and apply each star's deterministic twinkle/glow. Existing sorting, daylight visibility, telescope observation frame, and material alpha overrides are retained. Pixel-resolution changes refresh mesh sizes. Runtime-only resources are rebuilt after Editor domain reload, and orphaned celestial render children are retired.

No celestial generation, chart knowledge, star IDs, survey clues, saves, authored scenes, or Inspector assignments changed. The five world-generation handoffs remain on hold.

## Validation

- Full current runtime assembly compiled with Unity's compiler and references; only the two existing unused-field warnings in `WorldMapTravelDebugController`.
- Isolated Unity 6000.0.65f1 diagnostic using the project's `UniversalRP` asset and `Renderer2D` configuration.
- 20,000 synthetic celestial objects retained all 80,000 quad vertices with seven batches for that sample, no per-star SpriteRenderers. Other variant distributions can use up to twelve batches.
- Batch uniform update averaged approximately 0.01–0.02 ms per iteration in that isolated diagnostic, including reflection invocation overhead. This does **not** measure whole-game frame time, query rebuilds, field validation, or GPU cost.
- GPU-rendered pixels compared against the previous SpriteRenderer/shader path for all twelve sprite styles, including rotation. Mean RGB byte-channel differences below 0.001 in the 640×360 test images.
- Longitude seam, below-horizon culling, disable visibility, and daylight hiding checks passed.

## User play check

Stop and restart Play Mode, enable `CelestialSkyRenderer`, and compare Game view Stats in the same location. Confirm normal sky appearance and telescope/chart reference alignment. Full-scene FPS improvement remains to be measured in the user's active game.

## User confirmation

Skip confirmed normal visible stars and a night-time improvement from 47.8 to 300.2 FPS at 2253×1000. Sunrise remains 86.6 FPS at that resolution and 77 FPS at native 3440×1256. Additional sunrise/daytime optimization is deferred in BUGS.md, to resume immediately if performance becomes choppy again.

