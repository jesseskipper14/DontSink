# Harbor visuals and guidance — HT.4 checkpoint

2026-10-04 — Bosun 🍌

## Latest design read

Read `Piloting_Navigation_Reconciliation_Bosun_Handoff.md` and reconciled this contained harbor pass with it. The new circular local viewscape, separate analog compass, coastline sampling and piloting UI reconciliation remain their own later checkpoints. This pass supplies the main BoatScene harbor presentation that the future viewscape will consume alongside its own observations.

## What you should see

- Simple distant town/harbor silhouettes near any physically nearby node, including nodes outside the selected route. Their scale changes with true geographic distance; their side-view position moves smoothly with relative bearing as you turn or pass them.
- Playtest refinement: the settlement cluster was too large. Each node now uses one compact lighthouse-style silhouette (1.7 units wide × 3.95 units tall before distance scaling), with a tower, lantern room and pitched cap. Metadata-driven town variety is deferred for now.
- Close to a dockable harbor, a **Harbor approach** sketch appears in the upper-right of the main view. Your boat is the fixed upward-facing gold marker. The teal outline is the actual berth; the line joins it to the validated offshore departure point. Port/starboard/ahead/astern are relative to the boat.
- The sketch turns with geographic boat heading and translates the berth relative to your current true position. It shows the same rectangle used by docking, not a second target. Inside it, the caption changes to **In berth · E to dock**. The existing E interaction remains responsible for eligibility and transition; ordinary hovered interactions and tether/bell restrictions still apply.
- Fog and heavy rain shorten physical visibility and guidance range. Range boundaries and appearance fade rather than pop.

The sketch is a close harbor guide in the main view, not the future circular local viewscape. It shows harbor geometry only: no surrounding world map, coordinates, exact bearings, degree readouts, route correction, or node names. It stays non-interactive and does not steer or brake.

## Inspector tasks

No mandatory new wiring. No scenes, prefabs, materials, sorting layers or Inspector references were changed by Bosun.

In **BoatScene → BoatSceneController → Harbor Presentation**, inspect/tune if desired:

- Enabled: true.
- Sorting Layer: `WorldBackdrop`; Sorting Order: 5. These are runtime renderer choices using the existing layer.
- Silhouette Color.
- Fade Seconds: 0.8; Movement Smoothing Seconds: 0.35.
- Fade Start Fraction: 0.65.
- Bearing Spread: 90 physical scene units; controls stylized left/right placement.
- Far Scale: 0.35; Near Scale: 1.4; Distance Scale curve controls the transition.
- Approach Panel Size: 240 pixels, reduced to fit the active camera viewport. It does not extend beyond the screen on smaller resolutions.

Existing **SceneTransitionController → Harbor Settings** owns visibility range (20 map units), guidance range (8), and the assigned terrain profile. Keep the configured MainMenu controller, as established in the previous checkpoint. BoatSceneController installs the presentation component at runtime; do not add anything to boat prefabs.

## Playtest sequence

1. Embark normally. Look for the settlement silhouette and close harbor approach sketch near the departure harbor. Travel outward: the guide should fade first, then the settlement farther away.
2. Use F4 **Fill destination berth coordinates → Warp geography** for a fast approach test, then close F4. The berth sketch should place your boat inside the teal outline. Press E with no other interactable hovered; confirm the previously tested docking transition still works.
3. Move outside the berth while remaining nearby. Turn through north and make sharp turns. The sketch should rotate smoothly without flipping at 359°/0°, and moving forward should move nearby berth geometry toward/behind the boat marker.
4. Approach another node independently of the selected route. Its silhouette and close berth guide should still appear. This rendering does not reveal it on the map or update believed position.
5. Test the same approach in heavy rain/fog using your existing weather tools. Visibility should shrink, with full fog fading the representation away. Docking eligibility itself is unchanged.
6. Test a smaller Game view and different active cameras. The sketch should remain within the local active camera viewport. Check whether its default upper-right placement or the silhouette scale needs tuning around your normal HUD.
7. Dock, embark, save/load and return to menu. There should be no leftover proxy objects or new persistence payload.

## Sources and behavior

Modified: `Assets/Scripts/GameState/Scene/BoatScene/BoatSceneController.cs` — exposes presentation context/settings and installs the runtime presentation component alongside the dock interaction.

New, with Unity metadata:

- `Assets/Scripts/Travel/Travel/BoatHarborPresentation.cs` — derived settlement meshes, smoothing, weather range and close approach sketch; owns/releases its runtime material, meshes and objects.
- `Assets/Scripts/Travel/Travel/HarborPresentationMath.cs` — boat-relative projection, fade, visibility, smoothing and actual berth corners.
- `Assets/Scripts/Travel/Travel/HarborVisualObservation.cs` — reusable read-only physical observation query over the existing harbor definitions. This is a seam for later viewscape harbor integration, not a separate harbor coordinate system.

Existing classes inspected include BoatSceneController, HarborTravelService, HarborGeometryQuery, BoatSceneWorldPositionBridge, BoatIslandVisual2D, WeatherManager, FogManager, CameraManager and live MapNode metadata.

Position/heading come from WorldNavigationService and the existing scene-owned bridge's geographic heading. Wrapped deltas come from WorldTopologyService. Fog uses the current FogManager intensity when available; rain uses WeatherManager.RainDropDensity. There is no separate visibility/weather simulation. Translation/scale use exponential interpolation; heading uses shortest-angle interpolation; opacity uses timed fades.

No consequential authority behavior, docking rules, scene transitions, saved payloads, physics or discovery were changed. Clients may render this presentation from existing shared truth; this does not implement networking/replication. The single local CameraManager owns the approach overlay; ambiguous local camera ownership suppresses it.

## Validation and limits

Production runtime compilation passed. The isolated Unity suite passed 512 new presentation assertions plus the existing harbor bridge, harbor geometry, topology, coastal terrain, physics and island regressions. The new tests cover shape-preserving rotation, angle wrapping, port/starboard orientation, weather/range fade, wrapped harbor observation, berth outline agreement, production mesh creation, finite geometry, no added physics, cleanup and no navigation writes. Scene/weather providers are adapted harness hosts.

Visual scale, layering, placement against actual BoatScene art, weather feel and the OnGUI sketch need the live tests above. Final harbor art, exact shoreline alignment, full horizon occlusion, the circular piloting viewscape/analog compass, discovery and NodeScene mirroring/mooring remain separate work. The side-view proxy is a stylized bearing presentation; it is not a physical pier or a reconstruction of the island coastline.

Commit this checkpoint once the live visual/approach tests pass. No commit made by Bosun.

