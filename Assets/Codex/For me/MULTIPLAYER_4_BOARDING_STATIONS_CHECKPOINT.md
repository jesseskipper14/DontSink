# MP.4C3 — Deck boarding and authored pilot stations

## Status

Implemented and native-tested for boarding, shared chair claims, late join and disconnect cleanup. Awaiting the user's presentation/control acceptance. MP.4 and the overall multiplayer handoff remain in progress. MP.4C2 normal NodeScene entry was accepted by the user before this slice.

Use matching fresh builds: protocol is now `DontSink/node-boarding/4`.

## Runtime path

- CharacterNetworkInputGate and NetworkPlayerActor extend the existing authenticated input packet with ClimbUpHeld/ClimbDownHeld. The host's existing DeckBoardZone consumes that actor's ICharacterIntentSource: W/Up hold boards; Down held followed by a new Jump press unboards. The domain's hold durations, trigger overlap, closest-player selection, hull/interior rejection and rail/gate checks remain in place. Client deck zones do not execute transitions.
- Host publishes the actor's boarded state. Each ready client binds that state to its corresponding normal BoatSpawner boat through PlayerBoardingState.Board/Unboard, retaining the existing view/mass/mask presentation rules. Clients do not simulate actor bodies.
- NetworkSceneBoat.Stations discovers the authored PilotChairInteractable/SeatController2D pairs in the shared boat. Ordering derives from the matching restored boat hierarchy. Capacity is bounded at 64; matching assets and the existing boat bootstrap are required. No separate network chair prefab is required.
- The ordinary chair Update is disabled only on the adopted multiplayer boat. The network adapter owns occupancy/control, while the existing SeatController2D owns pinning, underwater ejection and carrier-velocity restoration on exit. Server pose capture resolves the actual seat point when the actor's body is non-simulated.
- Requests authenticate transport sender, connected participant, character identity, owned actor and scene readiness. The host checks revision, request ordering/rate, enum/index, finite bounded values, boarded boat context, the chair's existing interaction/range rules and exclusive occupancy. Only the actual occupant can stand or issue helm commands. Duplicate last requests return their recorded result; older requests cannot execute again.
- PilotChairInteractable exposes IsHelmControlAvailable using its existing CanReceiveHelmControl logic. This preserves authored helm linkage, boat, registration, installed helm health and station capacity rules. A valid seat can remain usable while its helm is unlinked/offline.
- Occupancy, owner and helm-control availability replicate to all peers and late joiners. Ejection, actor despawn and disconnect release the station and simulation claim. Disconnecting an unrelated observer leaves the occupant's claim intact.
- Full stop / half ahead and rudder presets call BoatPilotingSimulation's target APIs, retaining normal ramping and accepted target continuation after leaving the chair. Engine buttons use the existing engine operation. Mooring continues to reject nonzero throttle through the domain API; full stop is still permitted.

## Presentation and authoring

A small temporary multiplayer panel shows deck hints and E to sit/leave a nearby authored pilot chair. While seated at an online controlling station it offers stop/half-ahead, engines on/off and left/straight/right rudder buttons. The regular piloting cartridge is deliberately not enabled through its ordinary local mutation path; adapting that cartridge is subsequent MP.4 work.

DeckBoardZone still requires its authored Board Point with snapping enabled. An unset point rejects boarding instead of inventing a destination or bypassing hull/interior safety. The selected boat in the isolated validation project had an unset point: the test alone supplies a safe deck point and repositions actors to approach the zone/chair. Production scene and prefab authoring were not changed by this slice. Configure the intended zone point in the boat builder/Inspector for manual boarding acceptance.

## Verification

- Editor-symbol and player-symbol production C# compilation pass; Unity 6000.0.65f1 standalone build passes.
- Input gate regression: 18 checks pass, including the complete supported button mask, rejecting unknown bits, nonfinite/out-of-range axes, duplicate/stale input, timeout, rate limiting and sequence wrap.
- Three isolated Windows processes enter the actual authored NodeScene. Host authority executes remote W-hold boarding, client boarding presentation, shared chair claims/pinning, constrained actor positions, competing-owner rejection, stand restoring host physics, Down-then-Jump unboarding, reboarding/reclaim, occupied late-join snapshots, unrelated observer disconnect and owner disconnect cleanup. All three report PASS with zero runtime errors.
- The existing native host/client movement regression also passes, including walking, jumping, owner camera, physics authority and buffered pose playback.
- Native setup supplies only the missing deck point and actor approach positions. It does not replace the boat, water or normal scene generation. The first attempt exposed the missing point; a second attempt moved the actor to the chair before post-board validation finished. The fixture now waits for that domain check instead of weakening it.
- The online-helm/moored-control presentation still requires manual acceptance against the user's configured helm/mooring. The native boarding test proves occupancy/ownership rather than assuming all authored chairs have a functioning installed helm.

Ignored logs: `Temp/CodexNetworking/mp4-boarding-verified-build.log`, `mp4-boarding-verified-{host,client,observer}.log`, `mp4-boarding-movement-{host,client}.log`. Earlier boarding attempts remain separate. Runtime sources in the isolated build are checked against production hashes.

## Manual acceptance

1. Make fresh matching builds, Host/Join in MainMenu, and start the regular New Game. Check the intended DeckBoardZone has a Board Point assigned in the boat prefab.
2. Approach the zone and hold W/Up to board. Check boarded boat visuals on both peers. Hold Down, then press Jump to leave; Down alone should not unboard. Closed rail/gate paths and unsafe hull/interior destinations should remain rejected.
3. Board and approach the pilot chair. Press E to sit, then have the other participant attempt the same occupied chair. Only one actor should be seated; E releases it.
4. With the authored chair correctly linked to an online helm, try the temporary control buttons. Confirm targets ramp and continue after leaving. Nonzero throttle should be refused while moored. An offline/unlinked helm should allow sitting without granting controls.
5. Join while someone is seated, then leave that observer. The seat should remain occupied. Disconnect the seated owner; another participant should be able to claim it.

## Remaining scope

Ordinary piloting-cartridge network commands, hatches/ladders and general interactors, normal inventory/container/market/harvest/securing operations, live agents/damage/world changes and module manifests remain MP.4 work. Travel, durable saves, reconnect and crew lifecycle remain MP.5. No full handoff is marked DONE from this checkpoint.


## Practical entry follow-up — 2026-10-11

The user could not reach the boat with ordinary interactables disabled. MULTIPLAYER_4_BOAT_ENTRY_INTERACTIONS_CHECKPOINT.md connects normal boarding-door/gate/hatch and chair targeting, restores owner prompts and host ladder input, and advances the protocol to DontSink/node-interactions/4. The earlier checkpoint scope above remains historical; the boat's ordinary boarding door now works without adding a deck-zone point.

