# MP.4C5 — Normal piloting cartridge

Status: implemented; manual acceptance pending. Full multiplayer handoff remains in progress.

## Result

The existing `PilotingOverlayRunner`, `PilotingCartridge` and `PilotingHudRenderer` now present the owner's host-approved pilot seat in hosted NodeScene. The temporary network helm buttons are removed. The circular view, minimap, compass, harbor approach, DOCKED presentation and authored layout remain unchanged.

An authored, correctly registered, online HelmModule is still required to control the boat. An unlinked chair remains a seat. This pass does not add a helm, change chair wiring, or edit a production scene/boat prefab.

## Ownership and lifecycle

NetworkSceneBoat still owns authenticated seat claims and host SeatController simulation. The disabled ordinary PilotChair.Update is not re-enabled: it would submit local simulation input and assume a locally simulated seat. Instead, an explicit local presentation requester binds the existing runner to the owning actor when its claim appears. Other participants and late joiners never open that actor's console.

Closing the existing overlay requests Stand through the same host validation boundary. The adapter waits for the claim to release rather than reopening the cartridge while the response is in flight. E still leaves the seat. Claim loss, scene/session teardown and loss of local presentation ownership close the owner's overlay. Accepted persistent throttle/rudder targets remain simulation-owned and continue after leaving.

## Existing controls

The original engine-order and rudder-order buttons use the validated station command adapter in multiplayer and their original direct simulation calls in single-player. Controls are disabled without the local seat/helm claim or while a discrete request is pending. Request refusals and pending state appear in the existing status line.

A 10 Hz typed read-only snapshot supplies accepted throttle/rudder targets, physical forward speed, maximum rudder angle and mooring state. It drives the original button highlights and speed readout; actual lever/rudder positions still use the existing shared boat state. Helm status and control ownership are replicated per seat.

The owner's existing LocalBoatControlIntentSource is enabled as a local input sampler. A separate 10 Hz authenticated stream carries bounded W/S and A/D adjustments plus a latched X stop edge to the host. Sender/participant/actor identity, scene readiness, exact occupant, station revision, control authority, finite normalized axes, sequence and rate are checked. Held intent expires after 300 ms without fresh input. Leaving/reclaiming a station cannot reuse an earlier claim's held input. The adapter avoids repeatedly claiming the same helm, which would clear its current intent each tick.

Keyboard input overrides the matching preset axis through the existing SubmitIntent method. X uses a ramped Stop target. The owner sees the mooring refusal for keyboard throttle using host-published mooring state; the host retains the actual mooring gate for all control paths. The cartridge's old debug celestial-fix mutation is disabled in multiplayer until its authoritative gameplay path is connected.

## Compatibility and boundaries

Matching builds now require `DontSink/node-piloting/6`. No save/generator version changes. Single-player controls use their existing path. Travel into BoatScene, replicated voyage/route-guidance history, normal module installation/removal, broader inventory/storage/cargo/vendor and other MP.4 coverage remain pending. F6 module auto-install remains a temporary testing adapter.

The user's report of local-client movement latency is recorded as required owner prediction/reconciliation work in `MULTIPLAYER_2_DYNAMIC_PLAYER_MOVEMENT_CHECKPOINT.md`. This slice does not change player movement authority or the observer interpolation buffer.

## Verification

Editor-symbol and player-symbol compilation pass. Native final-source host/client/observer tests use the existing NodeScene and an ignored fixture boat with existing Helm/Rudder hardpoint/module prefabs wired through normal authoring APIs. The first attempt correctly refused control on an unlinked fixture chair; only the isolated test prefab was then equipped and wired.

Coverage: normal boarding/seat interaction, original cartridge opening for the exact local requester, host-owned claim, ramped throttle target, rudder target and late-join target replication, original HUD command seam, keyboard takeover, heartbeat expiration, forged/non-owner rejection, observer UI isolation, normal overlay close releasing the seat, and Stop/rudder targets surviving release. Native logs: `Temp/CodexNetworking/MP2ValidationProject/mp4-piloting-final-{build,host,client,observer}.log`. All three processes pass with zero runtime errors. Manual visual/button-click acceptance is still needed; the automated test invokes the same order method used by the HUD buttons.

## Manual acceptance

1. Rebuild both peers, Host/Join and start normal New Game.
2. Board and use an authored chair linked to an installed online helm. E should open your existing piloting console for the owner only. Unlinked chairs should not acquire controls.
3. Click throttle/rudder presets; confirm host/client agree, selected buttons reflect accepted targets, and levers ramp. Try W/S, A/D and X. Confirm the helm obeys its mooring restriction.
4. Click Full Stop and close the console before the lever reaches zero. The player should leave the seat and the lever should continue to Stop.
5. Have another participant try the occupied chair; join a third peer while piloting. Neither should receive the occupant's local console or control claim. The observer should see current boat/control state.
6. Recheck generator fuel transfers/on/off, debug spawning, client interior visibility and S/Down drop-through.

The final-source module/debug regression also passes for host, client and late observer: generator fuel transfers/on/off, real power generation, range rejection/reboarding, debug generator reconstruction, debug fuel creation/charges for both peers, client interior visibility and actual remote ledge drop-through. Logs: `Temp/CodexNetworking/MP2ValidationProject/mp4-piloting-modules-{host,client,observer}.log`. All eight changed runtime files match the isolated build copies; all six final native processes pass with zero runtime errors. All owned test processes have exited.

## 2026-10-12 — F6 helm/rudder testing blocker fixed

The F6 adapter's engine/generator/pump-only whitelist prevented installing the helm needed for normal console acceptance. It now also accepts HelmModule and RudderModule through the same host-authoritative compatibility, support, boarding, range and additive replica-manifest path. Chair wiring stays authored; no production scene or boat prefab was edited. Matching builds now require `DontSink/node-piloting/7`.

Native verification starts with empty pre-wired helm and empty rudder hardpoints in the ignored fixture. The client requests both installs through the F6 command API; host and client confirm online installed modules, then exercise original console opening, throttle/rudder orders, keyboard takeover/expiry, ownership rejection and close-to-stand/persistent Stop. A late observer reconstructs both modules and current helm orders without opening the owner's UI. All three processes pass with zero runtime errors. Player-symbol compilation and the isolated editor/player build pass. Logs: `Temp/CodexNetworking/MP2ValidationProject/mp4-debug-helm-{build,host,client,observer}.log`.

Manual: rebuild both peers, board, stand within 6 metres of an empty compatible Helm hardpoint, select Basic Helm and press F6. Do the same for Basic Rudder if needed. A chair must be authored as linked to that helm hardpoint to acquire piloting controls.
