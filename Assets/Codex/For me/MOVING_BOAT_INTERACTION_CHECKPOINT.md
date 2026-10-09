# Moving boat interaction / helm orders — 2026-10-07

Implemented code corrections; assembled-scene confirmation pending. No scene, prefab or Inspector edits.

- Seat pinning temporarily removes the actor from physics simulation, eliminating competing hull contacts while constrained. Ejection restores its original simulation setting at the physical seat anchor, with the boat's linear plus angular point velocity. Disabling an occupied seat also releases its actor.
- Ladder attachments use physical body poses instead of interpolated render poses. Climb casts test relative climbing rather than boat travel; movement follows carrier point velocity into the next physics step. MovePosition is no longer immediately followed by a zero-velocity write. Detachment and authored exits inherit carrier velocity.
- Mouse hover considers both static geometry and the boarded boat's physical pose, then validates candidates against their rendered position. Collider proximity/scoring uses the same frame conversion. Local mouse aim is reprojected when queried after camera follow, without resampling button pulses; local input samples before the resolver.
- Accepted throttle/rudder presets survive helm release/reclaim and finish at the normal simulation ramp rates. Manual axis adjustments still cancel that axis's preset. Command submission still requires the controlling owner and host authority; mooring still blocks propulsion.

Verification: production runtime and editor compile. Isolated Unity checks cover seat release/restoration, physical/render point conversion and preset continuation. Preset harness extracts current production control methods verbatim and supplies minimal surrounding services. Full-speed boat/collision/prompt behavior requires the live authored scene.

Live retest: sit/stand at maximum speed in both directions; hover nearby controls while cruising; climb, pause and exit moving interior/exterior ladders; command Full Stop and leave the cartridge before the ramp finishes; repeat a rudder preset. Confirm stationary ladders and ordinary interactions too.

Paper artwork: two inventory variants (blank Charting Paper, written Cartographic Chart). Written chart kinds are Reference, SoundingEvidence and Georeferenced. Coastline/surface and hydrographic/depth charts are payload variants of Georeferenced. Telescope celestial fragments are persisted separately, not additional written inventory item states. Integration consumes its chart; there is no used-paper item state.

## Follow-up: remaining flicker, activation offset and ladder release

The initial patch did not fully resolve these assembled-scene symptoms. Follow-up corrections:

- Interaction actions now resolve in LateUpdate at order 100, after camera follow and ordinary attachment updates; prompt display follows at order 200. Both use the same current camera/target frame. Input pulses are still sampled once earlier. This addresses the observed prompt versus E-activation offset without adding input delay or enlarging hitboxes.
- Ladder eligibility validates the hovered ladder directly rather than rerunning a nearest-ladder competition. Auto boat-volume classification converts its rendered center to the volume's physical frame. Ladder local climbing now writes velocity directly; exit handling occurs before movement is issued, so a queued MovePosition cannot overwrite release. Jump exits retain intended relative climb velocity rather than attachment correction velocity.
- Money chest and slot action-range checks use the shared rendered collider proximity calculation. Locked money chests follow their anchor in FixedUpdate with carrier point/angular velocity and matching interpolation, instead of teleporting graphics in LateUpdate while leaving their collider behind. Original interpolation restores when unsecured.

Verification: production runtime/editor compile; 26 isolated Unity ladder/chest checks pass, including first physics step after top exit, paused climbing at speed, jump release with artificially excessive prior velocity, chest anchor alignment across 15 physics steps and unsecuring. Ladder test harness extracts current production FixedUpdate, HandleAutoExit and EndClimb methods verbatim with minimal surrounding fixtures; chest tests use the actual component. Camera-follow/E and assembled scene collision feedback remain live-confirmation items. Earlier 22 seat/control checks passed before this follow-up.

## Accepted physics fixes; ladder prompt anchor correction and paper artwork

Skip confirms all reported bugs solved except the ladder prompt rapidly switching between two positions. LadderZone now provides an explicit IInteractPromptProvider anchor at ClimbCenter (or its own transform), instead of the UI falling back to physics collider bounds. The generic collider fallback is also converted to rendered coordinates. This is a presentation correction; verify the ladder prompt in play.

Applied Skip's supplied sprites from Assets/Sprites/Piskel/Paper: PaperCharting for blank paper (existing binding), PaperWriting for reference and georeferenced surface charts, PaperSoundingUnprocessed for raw sounding evidence, PaperSoundingProcessed for georeferenced charts carrying bathymetry. The written-chart definition now carries explicit variant sprite references; its prefab defaults to PaperWriting. Instance-resolved artwork is used by inventory slots, drag icons, held items, dropped WorldItems/highlights, storage-rack visuals and winch item icons. Held/world visuals refresh when the same instance changes state, including raw-to-processed sounding.

Runtime/editor compile; five artwork-state checks plus the existing 179 focused cartography/sounding regression checks and cartography foundation suite passed. The isolated definition fixture uses the exact production artwork resolver. No new item IDs, extra paper consumption, or saved chart-state changes.

## Other prefab anchor audit

Scanned 136 prefab files and their referenced interaction scripts, plus runtime-created pickup components. Checked 146 serialized promptAnchor/boardPoint/seatPoint/climbCenter references for missing local transform targets; none found. Null optional fields are safe where the provider falls back to its own transform.

Found and fixed one additional interface hookup: RackStoredItemInteractable had GetPromptAnchor but did not implement IInteractPromptProvider. StorageRackVisualSlots generates this component at runtime, so its configured anchor was previously ignored. It now exposes that anchor through the expected interface and delegates the generic verb to its existing pickup verb. No prefab/Inspector changes needed.

Other audited authored interaction types—including doors/hatches, chest/slot, pilot chair, agents, boarding, telescope/charting instrument, hardpoints/installed zones, lamps and diving-bell controls—already expose stable transform anchors. DebugInteractable has no authored prefab reference and uses the corrected generic fallback. Deployed flotation bags intentionally return null to put the prompt at the hovered balloon or attachment collider; retain that behavior with the corrected rendered-position fallback. Harbor docking uses its dedicated actor-position prompt path. Runtime/editor compilation passes; this was a static prefab/reference audit, not an exhaustive live prefab test.

## Boat-relative jump control and repeated jump input

Reported: jumping forward on a fast boat had no effect; jumping backward accelerated toward the stern; intermittent jumps reached approximately four times normal height.

CharacterMoveForce previously used ground support velocity only while contacted. Airborne movement therefore compared inherited world speed against the walking speed cap. It now retains the last genuinely contacted moving support as the horizontal airborne reference until another ground contact, swimming, climbing, disabled simulation/component or carrier invalidation clears it. Horizontal carrier velocity changes preserve relative velocity in flight; no airborne stopping grip or position pinning is applied. Ground/static movement tuning is unchanged.

LocalCharacterIntentSource.ConsumeJumpPressed previously cleared only its private latch, leaving published Current.JumpPressed true until Update. Several physics evaluations before the next rendered frame could reuse the edge while the ground probe still overlapped the deck. It now clears the published edge immediately, preserving held jump and other intent fields. CharacterMoveForce additionally accepts one edge until the intent returns false, covering persistent nonlocal intent too, and rejects duplicate movement components. Jump impulse magnitude/energy tuning is unchanged.

Production runtime/editor compilation and 16 isolated Unity checks pass. Checks use actual movement/input/motor source with real physics contacts and minimal surrounding services; they cover repeated evaluation of one press, unchanged jump impulse, forward/reverse air control and relative speed caps, carrier acceleration/invalidation, a fresh press after release, static-ground landing and published local-input consumption. The test fixture emitted a warning from assigning velocity after changing its deck to Static; no production warning or failed assertion occurred. Live retest: jump idle/right/left at full forward and reverse boat speed, repeatedly jump under varying frame rates, and compare ordinary stationary quay jumps. Do not mark the reported intermittent height symptom fully resolved before that confirmation.
