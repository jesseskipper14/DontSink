# TOWN.2 — Persistent Node Leader checkpoint

Implemented 2026-10-09. Ready for playtest. TOWN.1 accepted; TOWN.3–7 remain pending.

## Behavior and architecture

- TownCenterBuilding populates its authored LeaderSocket through the existing AgentSpawnPoint. NodeLeaderNpc is an NpcBase prefab variant with a purple shirt.
- Reuses PassiveTownNpcBrain, StationaryWithinHomeBounds movement and ServiceAgentInteraction. No parallel NPC movement or interaction system.
- AgentSpawnPoint now accepts building-supplied identity/bounds before spawn and registers the initialized stable ID. Repeated binding/spawning does not duplicate the leader.
- Stable identity is `<canonical node ID>/civic/node_leader`, associated with the same saved node and civic plot. The existing agent snapshot carries definition/node/identity. Returning to the node reconstructs the same semantic leader; no personality, quest or political state is introduced.
- NodeLeaderServiceDefinition dispatches to the exact leader's NodeLeaderAgentServiceHandler. Existing MiniGameOverlayHost shows Work, About This Settlement, Nearby Settlements and Leave. The first three are menu shells; their providers belong to later checkpoints.
- Guards require the owning NodeScene building, matching current node, nearby on-foot actor and exact local camera ownership. Leaving range, destroying the building, or changing presentation ownership invalidates the menu. No discovery, quest or node-state mutation occurs.

## Authoring and playtest

1. Re-enter NodeScene to rebuild the generated town. Find the purple-shirt Node Leader beside the Town Center.
2. Approach on foot and interact: Talk to Node Leader should open the four-button menu. Leave/Escape closes it; walk away or unload the node to check cleanup.
3. Revisit the same node and confirm there is one leader in the same civic role.
4. For an authored replacement building, retain TownCenterBuilding plus distinct LeaderSocket and StatusBoardSocket children. LeaderSocket marks the feet; Leader Root Height defaults to 1.02 for NpcBase. Move the socket to relocate the leader. No ground snap is applied, avoiding composite-quay collider ambiguity.

## Verification and limits

- Production runtime compilation passed; two existing unused-field warnings in WorldMapTravelDebugController remain. Production editor compilation passed during this checkpoint.
- Isolated Unity diagnostic: 24 assertions passed for composed behavior, socket/root placement, repeat binding, registration, snapshot identity roundtrip, direct service/menu dispatch, range cleanup, local/remote requester guards, ownership rebind, cancellation, destruction/revisit identity, NpcBase variant and reused behavior assets.
- Diagnostics used a separate project under Library/CodexThrowableChecks, without interrupting the active editor. They do not replace a visual playtest or a network session test.
- Save persistence here means stable identity reconstructed from the saved canonical node/manifest. Later civic state belongs to the later node-owned providers.

The handoff explicitly says **STOP FOR PLAYTEST** after TOWN.2. No exterior stats, local reports, civic quests, adjacent intel or trade simulation were implemented here.

## Prefab reference correction

The leader variant originally used reserved prefab asset FileID 100100000 for its internal PrefabInstance. Changed that instance ID and its local reference while retaining the external NpcBase asset reference. Verified actual Town Center and leader asset loading plus Town Center instantiation in the isolated Unity project without PPtr errors. Diagnostic copies remapped script references to the compiled runtime assembly; production script references were unchanged.

