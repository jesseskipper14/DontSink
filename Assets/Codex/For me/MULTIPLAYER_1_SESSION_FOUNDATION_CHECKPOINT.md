# MP.1 — Listen-host / client session foundation

2026-10-10. First networking implementation checkpoint. This is a connection/identity milestone, not completion of the multiplayer handoff.

Status: **MP.1 validated**. On 2026-10-10 the user confirmed the actual game build succeeds, the acceptance test cases pass, and the host session shows two participants.

## Stack choice

Netcode for GameObjects 2.7.0 with its Unity Transport 2.6.0 dependency. The Unity registry declares Unity 6000.0 compatibility; verified using the project's exact 6000.0.65f1 editor. Package versions are pinned in manifest/lock. Existing Collections, Burst, Mathematics and Mono Cecil versions satisfy the resolved dependencies without upgrades.

NGO fits the existing GameObject/MonoBehaviour project, listen-host model, custom intent messages, spawning/replication requirements and Unity debugging workflow. GameplayAuthority stays the game's simulation policy rather than becoming synonymous with a particular avatar's network ownership. FishNet also supports Unity 6 and Steam transports, but introduces a separate vendor/package/tooling path; this project's initial basic host-authoritative movement requirement does not yet justify adopting it for prediction features. This is a project-specific selection, not a universal ranking.

Initial local/LAN testing uses Unity Transport with an IP address and UDP port. No paid service, Relay account or Steam application setup is required for this checkpoint. Internet matchmaking, NAT traversal, Steam transport/authentication and stable production account identity are not implemented here. NGO connection approval is not secure user authentication.

Primary references reviewed:

- [Unity 6 package compatibility](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.netcode.gameobjects.html)
- [NGO NetworkManager and transport guidance](https://docs-multiplayer.unity3d.com/netcode/2.0.0/components/networkmanager/)
- [Connection approval and authentication limitations](https://docs-multiplayer.unity3d.com/netcode/1.9.1/basics/connection-approval/)
- [Unity community Steam Networking Sockets transport](https://github.com/Unity-Technologies/multiplayer-community-contributions/tree/main/Transports/com.community.netcode.transport.steamnetworkingsockets)
- [FishNet transport options](https://fish-networking.gitbook.io/docs/guides/high-level-overview/transports)

Steam integration requires separate compatibility/maintenance checks and verified authentication. No Steam SDK or community Steam transport was installed.

## Implementation

- `DontSinkNetworkSession`: MainMenu-only development panel installed at initial MainMenu Play startup; persistent session component owns a runtime NetworkManager/UnityTransport. Host, Join, Leave, address, port and connection/identity status are available. An unrelated existing NetworkManager is rejected rather than silently taken over.
- A protocol payload and configurable concurrent participant limit are checked in host connection approval. No client-authored player or save ID is accepted.
- `SessionParticipantRegistry`: server-assigned connection, persistence and character identities remain separate. Pending connections occupy capacity. Only connected transport sender IDs resolve to active participants. Disconnect invalidates the sender mapping while retaining the participant record for the hosted session.
- Client assignment arrives in a reliable host message, accepted only from NGO's server connection. This assigns identity, not an avatar/loadout.
- `GameplayAuthority`: a session owns the process role before transport startup; debugging components cannot override it. Failed connection/leave releases the role after shutdown. Static state resets for Play Mode, including disabled domain reload.
- MainMenu New Game / Load Game entry methods reject requests while this foundation test is connected/connecting. Leave before using ordinary single-player gameplay.

No scene, player prefab, boat prefab, authored menu canvas or Inspector asset was changed. The runtime panel can be removed/replaced when the real multiplayer menu is authored.

## Acceptance check

1. Let Unity resolve the new packages and compile.
2. Open MainMenu and enter Play Mode. Click **Multiplayer — Development** in the top-right corner.
3. Start **Host**, default port 7777. Confirm a participant ID is displayed.
4. Run a current standalone game build at MainMenu. Open the same panel and **Join** `127.0.0.1:7777`.
5. Confirm the standalone reports Connected and its participant ID differs from the host's. The host should show two reserved participants.
6. Leave the client. Host reservation count remains two. Leave the host; ordinary single-player New Game should work again.
7. Join an address/port with no host and let the connection fail; confirm the UI becomes offline and standalone role is restored.

These processes do not yet spawn two players or share gameplay. Keep this test at MainMenu. A normal build must include MainMenu as its starting scene. No manual NetworkManager prefab is needed for MP.1.

## Verification performed

- Actual project Build and Run and user acceptance tests pass. Player compilation fixes keep connection gizmos editor-only and module anchor alignment available at runtime. SurveyorNpc's internal PrefabInstance ID no longer collides with Unity's reserved prefab asset ID; an isolated Unity import/instantiation check also passes.
- Full current project source compiled against Unity references plus actual NGO/Transport assemblies.
- Twelve headless registry/role checks pass: distinct identities, pending capacity, duplicate/unknown sender rejection, disconnect reservation, connection-ID reuse, 30-participant collection behavior, session namespace separation, role ownership and debug override rejection.
- Built an isolated Unity 6000.0.65f1 validation player from the production session/registry/authority code. Two separate Windows processes connected over localhost UDP. Both received distinct identities and passed; host observed client disconnect and preserved the reservation; shutdown restored standalone authority.
- The real process test caught a runtime-created NetworkManager's initially null NetworkConfig. Explicit initialization fixes that startup path. Test success is from the rebuilt corrected version.
- Scoped whitespace checks pass.

Validation project/build/logs live under ignored `Temp/CodexNetworking`; these are not shipped game assets. This test does not prove gameplay replication, real prefab spawning, moving-boat physics, platform authentication or save/reconnect behavior.

## Next checkpoint

MP.2: audited dynamic player prefab/bootstrap, sender-to-actor mapping, host-authoritative remote movement intent, local camera/input/UI binding and a shared boat replica policy. Review concrete Inspector/prefab setup before migrating authored players.

True reconnect requires verified reoccupation credentials/account identity and the handoff's body/locker lifecycle. For now a fresh connection receives a fresh participant; it cannot steal a reserved participant's identity. Join-policy safe-spawn behavior, scene synchronization, participant saves, host save mirrors and exclusive gameplay requests remain pending. Do not mark the overall multiplayer handoff DONE_.
