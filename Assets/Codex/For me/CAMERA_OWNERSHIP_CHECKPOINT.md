# Camera ownership checkpoint — Bosun 🍌

Implemented 2026-10-02. Code is ready for live validation and a commit checkpoint. No scene, prefab, ScriptableObject, or serialized Inspector data was changed.

## What changed

`CameraManager` now owns an explicit player and its configured cameras. Existing BoatScene and NodeScene Follow Target assignments remain the initial owner binding. Another manager no longer destroys the first manager's GameObject. Camera ownership stays fixed when the view follows someone else.

`BindPlayer(playerTransform, isLocal)` is the spawn/rebind seam for a future networking bootstrap. Remote instances disable their configured cameras and listeners and cannot supply local input. Each camera set must belong to one actor; two active local managers bound to the same actor refuse ownership rather than choosing one. Ownership is local presentation, never saved/replicated gameplay authority.

Actor aiming, telescope sessions, tool hints/readouts, and inventory world-drop targeting resolve the actor's camera. Piloting, map-table, and charting entry points reject remote/dead/spectating requesters before changing UI. Remote chairs cannot register an open local Escape action. Piloting runner/host fallback requires a unique instance, rather than choosing the first one.

Boat and bell presentation use their explicit viewer override or the uniquely resolved local manager's **Viewing Player**. Their old global single-player searches are removed. A remote swimmer/bell occupant therefore cannot become this client's viewer through a fallback search. Spectator follow deliberately presents the selected target's context.

Both existing death paths (`CorpseRespawnHandler`, `DeathSpectatorCamHandler`) ask the actor's manager to change modes. Corpse/vitals/living-system behavior is retained. Legacy camera roots/behaviour arrays no longer control presentation. The managed camera remains enabled during local death; free movement is a presentation transform change only.

The existing free-camera script yields while a managed camera follows a player. Legacy boat-follow/switcher scripts also yield for managed cameras. Current offsets, exponential follow smoothing, focus-pan limits/smoothing, telescope fade/zoom settings, and default orthographic zoom values remain intact.

Telescope sessions track a camera binding version: losing ownership ends the session immediately, and an old session cannot restore its zoom/pan onto a newly bound owner. This does not change the normal telescope fade transition.

`SimulationLodTargetResolver` no longer falls back to camera position or selects an arbitrary first actor. A single actor is a compatibility default; a host with several actors must supply its gameplay relevance targets explicitly.

## Controls and API

| Context | Behavior |
| --- | --- |
| Alive | Existing player follow and C camera cycling; modal gameplay input blocks C. |
| Local death | Free spectator on the same managed camera. |
| Spectator Tab | Cycle living, active players in stable instance-ID order. No eligible targets means free mode. |
| Spectator F | Return to free mode. |
| Free spectator | Existing WASD/Shift/scroll controls when CameraWASDController is present and enabled. Otherwise manager supplies WASD/Shift movement. |
| Spectator target dies, deactivates, or despawns | Return to free mode at the current camera position. |
| Respawn | Restore owner follow and the pre-death active camera/orthographic zoom. |

Local effects seam: `manager.SetTemporaryOffset(Vector2)`, `manager.AddImpulse(Vector2, recovery)`, `manager.ClearEffects()`. Effects are instance fields; no gameplay event was wired to shake/impulse. These APIs reject remote owners.

`OwnerPlayer`/`OwnerTarget` identify the input/presentation owner. `ViewingPlayer` identifies the viewed actor and differs during spectator follow. `ForActor(...)` resolves ownership, never whichever player the camera currently watches. `SetFollowTarget(...)` remains a compatibility owner rebind; use `SpectatePlayer(...)` for spectator view changes.

`CameraManager.Instance` remains only for scene-wide presentation on a one-human-per-client process. It returns null if multiple local contexts are eligible. Actor-local code uses `ForActor`/`CameraForActor`. Split-screen, multiple humans reading one keyboard, multiple simultaneous cutaway/material presentations, and a selectable primary view are outside this pass.

## Audit and modification set

The audit read current code and checked scene references before editing. Current scenes explicitly bind Follow Target to their player; no player-spawn code was calling SetFollowTarget. Scene restoration moves/reparents the existing actor, which keeps that identity binding. Camera position was already unbounded; no world bounds were added or removed.

| Group | Exact existing classes changed | Purpose |
| --- | --- | --- |
| Camera ownership | CameraManager, CameraWASDController, CameraFollow, CameraSwitcher | Owner/view separation, remote gating, local effects/spectator/listeners, one managed movement writer. |
| Local actor input | LocalCharacterIntentSource, LocalInteractionIntentSource, LocalBoatControlIntentSource, LocalFlotationPlacementIntentSource, LocalHandheldSoundingLineIntentSource | Reject remote/dead/spectator local adapters; aim through owned camera. |
| Actor presentation | BoatObservationPresentationController, InteractPromptDriver, InteractPromptUI, ContextHintOverlay, HandheldSoundingLineHUD, InventoryDragController | Owned camera routing; stale telescope binding cleanup; remote prompt drivers do not hide the local driver's UI. |
| Client scene presentation | BoatVisualStateController, BoatBoardingInteractable, DivingBellVisualPresentation, DivingBellOceanPresentationBridge | Resolve the intended viewer without searching scene players. |
| Local overlay entry | PilotChairInteractable, PilotingOverlayRunner, WorldMapOverlayRunner, CelestialObservationOverlayRunner | Scope opening/closing/context changes to the local requester. |
| Death | CorpseRespawnHandler, DeathSpectatorCamHandler | Route camera modes through the actor owner without altering corpse/vitals simulation. |
| Simulation boundary | SimulationLodTargetResolver | Camera transforms cannot become the fallback fish simulation relevance target. |

No new production component was introduced.

### Camera.main / global lookup classification

| Finding | Classification and disposition |
| --- | --- |
| Player character/interaction/flotation aiming, telescope manager lookup | Definitely unsafe actor-local presentation; replaced with owned camera/context. |
| Sounding HUD, interaction prompts, hints, inventory drag world-target projection | Owned-camera routing added. InteractPromptUI retains its initial fallback, but the authorized driver supplies the current owned camera before showing it. Explicit camera Inspector overrides remain supported. |
| SimulationLodTargetResolver | Simulation authority risk; camera fallback removed. Multiplayer actor relevance policy deferred. |
| Boat/bell viewer lookups and boat camera fallback | Local scene presentation; global actor guessing removed. Unique local context remains the compatibility seam. |
| CelestialSkyRenderer, UnderwaterAmbientBubbleField, WaveRenderer_Line, WaterMeshRenderer, WaterViewEffectsController | Existing explicit-target/single-client environmental presentation. Unchanged; these do not assign player ownership or camera follow targets. A future simultaneous multi-view renderer needs separate materials/anchors or per-camera render handling. |
| WorldMapHoverController, WorldMapClickController | Legacy world-map UI camera lookup; unchanged. These are not the actor gameplay camera owner. |
| ExternalContainerOverlayUI, HardpointSupportHoverPreview | Additional inventory/build presentation debt: global player/camera selection remains. Recorded in BUGS.md for a focused presentation pass. |
| WaveImpulseDebug, CompartmentFloodGameClickTool, CompartmentBoundedSpaceDebugClickTest, commented FallingSquareSpawner | Debug/editor click projection; unchanged. |
| Scene transitions/context restoration | Existing player persistence/scene machinery; no camera target chosen by a first-player lookup. Transport spawn must call BindPlayer explicitly. |
| Unconsciousness | Actor-owned affliction/intent behaviour; no global camera mover found. Normal follow remains with the unconscious actor. |
| Other global UI/cartridge player searches | Audited as future UI/persistence ownership work, not rewritten into a general UI framework here. |

Audio audit: NodeScene has one enabled listener on its managed main camera. BoatScene has one enabled listener on its managed main camera; the two legacy camera listeners are serialized disabled. Managed camera switching now enables only the active camera's listener for the unique local context. Unmanaged listeners remain an Inspector/bootstrap responsibility.

## Inspector tasks — you control these

No new assignments are required for the existing single-player scenes. Verify these before the live check:

1. In **BoatScene and NodeScene**, inspect the existing CameraManager. **Local Presentation = true**, Main Camera is the current gameplay camera, and Follow Target is the intended player. **Presentation Owner may stay empty** to use that existing target; assigning it explicitly is optional. Leave follow/pan/zoom tuning unchanged.
2. Keep CameraManager enabled while the player is dead. CorpseRespawnHandler's old Alive/Dead Cam Behaviours lists and DeathSpectatorCamHandler's old camera-root references are now unused. You may clear them for clarity; do not add a second handler just for this pass. Both current scenes already contain CorpseRespawnHandler.
3. Existing CameraWASDController can stay enabled on the gameplay camera. It now runs only in free spectator mode for a managed camera. Keep its present movement/zoom values. A camera without it gets basic manager free movement; add it yourself only if you want its configurable speed/scroll behavior there.
4. Verify the PilotingOverlayRunner still references the intended MiniGameOverlayHost. Its new **Presentation Owner** may stay empty while exactly one local manager exists. For explicit multi-instance fixtures, assign the matching manager and a distinct overlay host; chairs must reference the matching runner.
5. Keep the gameplay listener on the Camera GameObject itself; keep legacy/unmanaged camera listeners disabled. The manager controls listeners on its configured cameras, not arbitrary listeners elsewhere in the scene.
6. Future remote player spawn: create/configure the camera hierarchy inactive, set its camera fields and call `BindPlayer(exactActorTransform, false)` before activation. For the authenticated local actor use true. Never infer local ownership from host simulation authority. Existing true defaults preserve today's authored single-player scenes.
7. If camera assignments/listeners change dynamically after activation, call `RebuildCameraCache()` and then `ActivateCamera(...)`. Binding/enable already refresh those caches. Do not share Camera component references between managers.

## Validation

- Full runtime C# compile using the project's Unity references: **passed**.
- Isolated Unity 6000.0.65f1 harness: **47 camera assertions passed**, plus the existing **61 pinning assertions**. Production CameraManager, camera controllers, local character/interaction adapters, death system and both death handlers, and piloting runner are copied unchanged into the harness. Boat/vitals/overlay/cartridge hosts are adapted; this does not test the full piloting cartridge drawing or navigation simulation.
- Checks exercise real Unity Camera/AudioListener instances, explicit actor binding, two independent actor/camera pairs, diver separation, an actor moving with a bell-like parent context, x=12,389,123, local effect separation, remote activation/input/helm/death rejection, target cycling, dead/inactive/despawned target fallback, free movement without actor movement, respawn, stale-intent repair, binding version/zoom reset, foreign-camera rejection, listener switching, disable/re-enable, and duplicate owner refusal.
- Huge-coordinate check uses immediate follow to verify no bounds/origin reset. Existing smoothing formula is preserved by code review; this does not solve Unity float precision at huge physical coordinates.
- **Actual boat/bell cutaways, underwater shaders, telescope render suppression/fades, piloting cartridge interaction, and menu/scene transitions are reviewed but not live-tested by this harness.** Multiple active local cameras in a test fixture are not a working split-screen game. No real network runtime exists yet.
- Harness sources/logs/results are ignored under Temp/CodexPhase7. `git diff --check` passed.

### Live checkpoint checklist

1. New Game and load a save in each scene. Walk, swim away from the boat, and enter/exit the diving bell; compare follow, cutaways, underwater view, and focus pan with the previous build.
2. Use/exit telescope with E and Escape; check fade, zoom, stars/constellations, panning, and normal view restoration. Open/close charting and map table too.
3. Enter/exit helm and close its cartridge. Check the chair still releases control normally and the camera remains player-centric.
4. Trigger the existing death path: free WASD/Shift/scroll, Tab target selection if another living actor exists, F back to free, then respawn. No second actor means Tab stays free. Check the Console for duplicate listeners.
5. Return to menu, start/load again, and check the intended player owns the camera with normal zoom/pan and no stale spectator/telescope session.

Commit after that live checkpoint passes. The next implementation pass remains world wrapping, followed by dynamic terrain; no world generation or topology was bundled here.

## Remaining integration boundaries

Local/remote identity must eventually come from an authenticated networking bootstrap; this pass provides the seam, not a transport. Shared modal input/Escape/scene materials are still one-local-human-per-process systems. The host's future simulation/terrain relevance policy must use gameplay actor positions, never spectator camera position. Related deferred UI/LOD work is recorded in the single BUGS.md backlog.
