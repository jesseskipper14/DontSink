# Boarded bubbles and BackWater effects

Two rendering-order problems were found in the NodeScene configuration.

- Ambient bubbles use WaterEffects, which draws after BoatView/interior renderers. While the bubble field belongs to the boarded local player's active camera, it now sorts bubbles on Default, behind the boat. Disembarking restores the configured sorting layer/order. Already-active pooled bubbles update as well as new ones. No saved bubble Inspector data was changed.
- The opaque SeaBackground renderer using Custom/SeaDepthBackground2D is on Default, after BackWater. It paints over BackWater's shader effects while FrontWater remains visible because it draws later. WaterViewEffectsController now resolves same-scene sea-depth backdrops at runtime and places them on BackWater one order below the ocean, whenever they would otherwise cover it. This targets only the depth-background shader, not sky, boats, terrain or other sprites.

FrontWater and BackWater objects, materials, visibility rules and configured sorting were not edited. No saved Inspector, prefab or material changes were made in this detour. Production compilation passed. The actual scene visual comparison remains a live check; no automated pixel-render test was run for these two fixes.

Reload NodeScene and board the interior. Confirm bubbles disappear behind solid boat sprites and shafts/sparkles remain visible in open water below the boat. Disembark and check the existing foreground water appearance and ambient bubbles. Check BoatScene too, since the same code handles both scenes. This detour does not change the pending node-generation refinement scope.
