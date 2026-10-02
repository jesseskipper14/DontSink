# Inspector tasks — Observation Telescope refinements

No Inspector data was changed by this refinement. Scripts and documentation were updated.

1. Open `Assets/Resources/Prefabs/Items/Placeable/ObservationTelescope.prefab` in Prefab Mode.
2. Select its root `ObservationTelescope` object, then the **Observation Telescope Interactable** component.
3. Under **Presentation**, set **Fade Duration** to **0.75** seconds. The new script field defaults to 0.75; select 0.5 for quicker or 1 for slower. This controls each complete entry/exit transition. Save the prefab after your chosen setting. Check scene-instance overrides if an instance has a different value.
4. In the gameplay scene, verify the existing `_GameplayUIBootstrap` (or the project's equivalent UI object) already contains **EscapeCloseRegistry** and **GlobalEscapeRouter**, with the router's **Escape Menu** reference assigned to the existing EscapeMenuUI. This is a verification task; no additional registry/router or telescope-specific Escape component should be needed. The telescope registers itself while used.
5. Play: deploy and observe. Confirm a smooth boat/player/item fade over the selected duration, and smooth zoom. Press E during entry to verify reversal without a jump. After completing entry, press Escape once: regular view returns and the pause menu stays closed. Press Escape again: the normal menu opens. Also try the second press during the return fade; that fade should still finish while paused.
6. Check an originally hidden/disabled renderer, rope and world-space label: they should restore correctly. RMB Look/constellations, terrain/cloud/weather and ordinary save/load should continue to work. Actual component/scene teardown intentionally restores immediately.

No new components or drag/drop references are required on the telescope. `BoatObservationRenderFade` is an internal helper, not an Inspector component.

Commit after the live checks pass. 🍌

## Follow-up: expanded sky and additional layers

The authorized sky-extension asset changes and the two new layer picker controls are listed in `Assets/Codex/TELESCOPE_SKY_COVERAGE_AND_LAYERS.md`. The extension fields were set to 0.30. No additional-hidden layer selections were applied to the telescope prefab; choose Agent and/or WorldBuildings there as needed, then exit/re-enter observation.

## Final refinement: void transition

`CelestialSkyProjectionSettings → Void Ambient Transition → Void Ambient Fade Distance World` was added and set to **32** world-map units. Review/tune that value and check that the world edge transitions through sparse ambient stars into empty void. All changes and checks are recorded in `Assets/Codex/CELESTIAL_VOID_AMBIENT_TRANSITION.md`. The proposed camera clamps were not implemented.
