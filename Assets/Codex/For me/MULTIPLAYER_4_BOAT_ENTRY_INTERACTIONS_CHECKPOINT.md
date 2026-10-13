# MP.4C3 follow-up — Normal boat-entry interactions

## Status

Boat-entry interaction routing is implemented and native-tested. This closes the practical entry gap in the earlier station slice: that slice left the ordinary interactor disabled and depended on a deck zone whose selected fixture boat had no Board Point. The authored BoardingDoor_Wooden is now usable through normal mouse-hover/hold-E interaction, with its existing board/unboard points. No production boat or scene authoring was changed.

Matching fresh builds are required: protocol is now `DontSink/node-interactions/4`. Full MP.4 and the multiplayer handoff remain pending.

## Implementation

- NetworkPlayerActor restores LocalInteractionIntentSource, Interactor2D and InteractPromptDriver on the owning gameplay actor only. The scene bootstrap explicitly binds its prompt UI after scene readiness, including late join. PlayerLadderClimber is enabled on the host, consuming the existing authenticated movement/climb input; client actors remain non-simulated.
- Interactor2D keeps normal targeting/prompt queries, but connected action execution routes through NetworkSceneBoat.Interactions. This adapter registers only the adopted boat's BoardingDoor, DoorInteractable (including rail gates), HatchInteractable, LadderZone and PilotChairInteractable targets. Wider item/vendor/other world interactions are still pending rather than executing local mutations.
- Non-simulated client bodies have no Physics2D overlap-query participation. Replica hover picking therefore uses authored BoxCollider2D/CircleCollider2D shapes in rendered space. The existing hover-selection/access policy is retained so an inaccessible boarding trigger or implicit-only ladder does not hide a valid gate behind it. Other collider shapes are deliberately not approximated by a giant fallback bounds box in this adapter.
- Host requests validate transport sender, connected participant, character identity, owned actor, scene readiness, request ordering/rate, revision/index, active target, authoritative actor range and the existing target CanInteract rules. Seating uses the previous exclusive station request path. Normal E targeting now selects the authored chair; E while seated still releases that station.
- Boarding holds are timed on the host using bounded heartbeat freshness. Short holds do nothing, moving away/changing targets cancels progress, and a completed hold stays latched until release. Replayed request IDs do not execute again. No client-supplied position or elapsed hold duration is accepted.
- The existing BoatBoardingInteractable performs the domain operation on the host. Network actors retain their persistent root instead of parenting under an unnetworked boat; host snaps write the Rigidbody2D's physics position. Single-player parenting and ordinary interaction behavior remain unchanged.
- Existing boat door state replication covers rail gates/doors. A bounded 64-bit hatch snapshot now applies HatchRuntime.RestoreOpenState on replicas, including late join. Domain blocked-close checks remain in place.

## Verification

Editor and player symbol C# compilation pass. A Unity 6000.0.65f1 standalone build passes; the changed production runtime sources match the isolated build copies.

Three isolated Windows processes enter the actual NodeScene. The client uses its enabled ordinary LocalInteractionIntentSource/Interactor2D, with the harness injecting input and aiming at actual authored collider geometry. The host only supplies approach positions. It does not supply a missing deck point or invoke boarding directly in this test.

Passing checks cover opening the actual exterior rail gate, a short E hold refusing boarding, a full E hold boarding through BoardingDoor_Wooden, holding E through completion without reversing, persistent network actor root, ordinary gate/door and hatch toggles observed on the client, mouse-targeted pilot-chair claim, occupied/boarded late join, observer departure and seated-owner disconnect cleanup. All three processes report PASS with zero runtime errors. Climbing itself still needs manual gameplay acceptance; this test validates the entry/interactor path rather than claiming a complete ladder regression.

The first harness instruction was sent before the client registered its test-only message handler, causing the test to wait without driving input. An explicit harness readiness handshake fixes that sequencing. Headless test sky rendering is disabled after scene entry because the minimal validation project omits dynamically found star shaders and otherwise repeatedly logs their absence; production sky/rendering files were not changed.

Ignored successful logs: `Temp/CodexNetworking/mp4-entry-final-build.log` and `mp4-entry-final-{host,client,observer}.log`, plus the preceding `mp4-entry-ready-*.log` run. The existing native host/client movement regression also passes as `mp4-entry-movement-{host,client}.log`. The final rebuild retains the completion latch through target/access loss and fixes the temporary deck hint to use replica geometry.

## Manual test

1. Make fresh matching host/client builds, Host/Join, then use regular New Game.
2. Hover the authored rail gate and press E to open it as needed. Hover the boarding door and hold E to board; its prompt should show the ordinary hold action. Release E before holding it again to leave.
3. Use W/Up and S/Down on applicable ladders. Hover doors/hatches and press E; check their state on both peers. A blocked close should still be refused by the existing domain checks.
4. Hover the pilot chair and press E to sit. Use the existing temporary network helm buttons, and E to leave. Conflict/disconnect ownership behavior remains from the preceding station checkpoint.

The deck-zone W/Up route still retains its explicit authored Board Point requirement. It is separate from the now-working boarding door and is not required to use that door. Regular piloting-cartridge integration and the remaining transaction/world/lifecycle domains remain subsequent work.

