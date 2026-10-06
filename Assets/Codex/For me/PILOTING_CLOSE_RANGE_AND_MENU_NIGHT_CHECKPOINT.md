# Close-range piloting and night menu checkpoint

## Close-range piloting

The main piloting ocean view now draws nearby geographic land in muted green, a thin shallow-water band, and cyan visiting-boat berth outlines with their waterward approach line. The existing compass, wide circular viewscape and harbor approach inset remain.

Land samples use the viewing boat's geographic bridge and the physical viewport's existing scale. Docking outlines use the harbor geometry service, not independently invented docking bounds. X wrapping is respected; samples beyond the world latitude bounds remain empty. Overlays clip to the close viewport. Terrain updates approximately 15 times per second.

This is presentation only: no movement, docking eligibility, authority, chart discovery, route, terrain collision or saved-state changes.

## Main menu

**Withdrawn at the user's request.** The automatic MainMenuController attachment and MainMenuNightBackdrop script/metadata have been removed. No saved menu scene, prefab, material or Inspector settings had been changed, so loading the existing menu scene restores its authored presentation. Exit Play Mode first to discard the previously generated runtime canvas and panel tint. The piloting changes remain.

The replacement direction is to use the authored WaveSystem with buoyancy, an existing boat prefab with a partially flooded compartment, nearby floating crates, and the actual celestial sky system using a generated and saved fixed menu sky with landmark stars. This replacement has not been implemented yet.

The description and verification below record the withdrawn implementation.

MainMenuController creates a scene-owned background canvas behind its existing controls. It uses a private copy of the existing sky atmosphere material locked to night, a deterministic 650-star texture, dark ocean layers and a tilted half-submerged boat placeholder. The boat is decorative UI geometry with no physics or gameplay components. Generated art does not capture pointer input.

The existing MainMenuPanel is tinted dark at runtime so its former translucent white veil does not wash out the night. Its saved color remains unchanged. The backdrop never changes EnvironmentManager time, loads a gameplay world, or changes new-game/save times. Owned textures/materials are released with the menu.

## Inspector tasks

None. No saved scenes, prefabs, materials, project settings or serialized Inspector references were changed. The menu attaches automatically through MainMenuController; piloting uses the existing boat bridge and harbor presentation controller.

## Play checks

1. Open the main piloting view while departing/approaching a harbor: nearby green land and cyan berth outlines should align with the boat. Move/turn, and check that the outlines enter and leave the close viewport without sticking to its edge.
2. Check open ocean: distant land should remain in the wide view rather than appearing in the close view. Check a world seam if convenient.
3. Open MainMenu at your usual and smaller resolutions: stars, water and half-sunken boat should appear behind working New Game/Load/Quit buttons. Check the save/load panel too.
4. Start a new game and return to the menu, then load an existing save: gameplay time should retain its usual new-game/saved behavior while the title remains night.

## Verification

Production runtime C# compilation passed. An isolated Unity Direct3D11 harness rendered the menu preview and checked deterministic stars, duplicate attachment protection, non-intercepting UI, unchanged shared sky material, rotated/scaled near-field projection and X-seam projection. Existing harbor presentation regressions passed 502 assertions.

The harness adapts scene/data providers and does not run the complete game menu or interactive piloting GUI. The play checks above remain the final integration check in the actual game.

Commit checkpoint after those checks. No commit was created automatically.

## Island water-order regression correction

BoatIslandVisual2D's authored WorldBackdrop layer is in front of BackWater in this project's sorting list. The generated island renderer now resolves to BackWater with an order below the lowest scene-local backwater mesh order (including disabled meshes). Already earlier authored layers are respected. Neither ocean renderer nor the saved island component's Inspector settings are changed. The separate island silhouette, fade and motion remain intact.

Production compilation passed; isolated island mesh/lifecycle/sorting checks passed 661 assertions, including negative backwater order, foreground-water precedence, unchanged water settings and unchanged serialized island layer. In-game check: approach an island while boarded, then unboard; its submerged silhouette should sit behind the visible ocean layers. Piloting performance was explicitly left for the next discussion.

The first ordering fix did not resolve the visible slab in the user's screenshot. Transparent ocean layers still expose underwater background geometry. The decorative island mesh now terminates at its waterline instead of extruding a rectangular slab beneath it; its above-water crest retains the same shape and position. Physical coastal terrain is separate and unchanged. Production compilation passed again; 791 island regression assertions and a Direct3D11 pixel check confirm that the generated island still draws above water and draws no pixels below its waterline. The pixel preview uses a compatible simple material in the isolated harness; actual scene integration still needs the user's check. No water settings, Inspector data or piloting performance code changed in this correction.
