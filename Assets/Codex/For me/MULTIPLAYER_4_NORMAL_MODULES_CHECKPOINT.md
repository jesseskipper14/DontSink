# MP.4C4 — Normal engine, generator and pump interactions

## Status and scope

The user accepted boarding and authorized continued coverage of the missing normal interactions. This slice connects installed engine/generator/pump hardpoints to the existing ModuleOverlayRunner/ModuleCartridge window, with authenticated commands and replicated module/fuel state. It does not mark full MP.4 or the multiplayer handoff complete.

Protocol is `DontSink/node-modules/4`; use fresh matching builds.

## Normal UI and commands

- The boat's registered interaction targets now include installed engine, generator and pump HardpointInteractables. Picking respects their IInteractionColliderScope and existing hover/access policy, so only the installed module's authored interaction geometry is selectable.
- E opens the existing module window locally for the owning actor. This presentation path is restricted to currently supported installed modules; it never invokes an empty-hardpoint installation locally. Connected ModuleCartridge rendering uses its network command panel instead of the ordinary local mutation buttons. Single-player retains its existing panel and operations.
- Each peer can read running state and complete fuel contents. Turn on/off uses the existing EngineModule/GeneratorModule/PumpModule.SetOn domain methods. Invalid operating conditions remain domain-owned; networking does not supply fuel, invent power, bypass mooring or force a module to run.
- Fuel buttons select an explicit requester hotbar slot. Transfers move an exact whole ItemInstance between an eligible empty hotbar/container slot. No swaps, merges, partial splits, cross-player references, client-created item data, equipment transfers or arbitrary slot drag are introduced. Quantity, instance ID, charges, retained water and the existing codec's other nested item state travel with the instance.
- Deposit checks the expected live source instance ID; withdrawal checks the destination remains empty/eligible. Both slots are validated before mutation. Notifications run after both writes; actor loadout replication and module snapshots then report the resulting state.
- RPCs validate transport sender, connected participant, character identity, exact owned actor, scene readiness, active supported target, authoritative range/boarding context, module revision, enum/index and replay/rate limits. A stale/invalid request does not mutate state; replies are targeted to the requester. Accepted operations continue independently of panel lifetime.

## State and replica policy

NetworkSceneBoat.Modules uses a bounded typed NetworkModuleSnapshot: at most 64 records, hardpoint ID, kind, revision, running flag and the existing complete NetworkItemCodec fuel tree. Combined fuel payload is capped at 256 KiB; no client request accepts snapshot bytes.

Running state is checked at 4 Hz, with fuel invalidation driven by item change events. Prefab-deserialized fuel containers explicitly restore their container/change bindings before watching, so slot edits and child fuel changes publish correctly. Commands refresh state before revision validation and immediately after accepted mutation. Module state uses a separate low-frequency channel from boat/actor pose streams.

Clients decode changed item trees before applying domain RestorePersistentState methods. Hardpoint IDs must match the restored boat layout. Covered engines use this module snapshot instead of competing with the older engine-running bit application; that generic path also preserves existing fuel when handling uncovered engines. Module availability checks fail closed if the fixed layout no longer contains a supported module. Live install/remove/replacement manifests and damage-driven structural lifecycle are subsequent work.

## Fresh New Game correction

Fresh New Game previously created an empty module manifest, which RestoreModuleManifest correctly interpreted as an authoritative empty boat and removed authored modules. Unity inline serialization can also recreate a null DTO as an empty object, so null alone is insufficient.

BoatSaveState now has optional useAuthoredStartingModules, set only for the initial fresh game. BoatSpawner explicitly skips module/power restoration while that marker is true, preserving the prefab's authored starting contents and power. The first SetBoatModuleStates capture clears the marker and supplies the real manifest. Existing saves default the added bool to false, and an explicit saved empty manifest remains authoritative. No generator version, world geography or save format version was changed.

## Verification

The isolated Unity 6000.0.65f1 build uses the actual NodeScene and actual fallback TestBoat_12_Tethers, equipped only in the ignored test project with three existing authored hardpoint/module prefabs through the normal builder install API. No production scene, boat prefab, item definition, shader or material was edited for this slice. The test supplies approach positions and a compatible exact fuel item; fuel/power simulation and commands use production methods.

Native coverage includes normal New Game preserving the authored modules, existing gate/boarding input, normal mouse/E opening of the generator cartridge, complete fuel-tree replication, exact withdrawal/deposit without duplication and with charges retained, host generator start and real power generation from a zero-power baseline, client running state, remote range rejection, normal reboarding after leaving the boat volume, shutdown and live module/fuel state on a late observer. The preceding successful run is `Temp/CodexNetworking/mp4-modules-return-{host,client,observer}.log`; final matching-source build and native logs use `mp4-modules-final-*`; all three processes pass with zero runtime errors. The existing host/client movement regression also passes as `mp4-modules-movement-*.log`. All ten checked runtime sources match the final isolated build copies.

Earlier runs exposed an empty starter fixture, the fresh-manifest issue, a harness root-versus-child inventory lookup, the missing prefab container event binding, and a harness that forgot to reboard after its range test. Those checks were corrected without weakening range/boarding or item placement rules. Editor/player-symbol production compilation and runtime source hash comparison are also used. Engine and pump operate through the same validated adapter but still need manual presentation/domain acceptance against the user's actual authoring.

## Manual acceptance

1. Make fresh matching builds, Host/Join and use normal New Game. Authored starting modules should remain installed. Existing loaded empty manifests should still represent empty hardpoints.
2. Board normally, hover an installed generator/engine/pump and press E. Check the module window opens for the interacting owner.
3. Take fuel into an empty hotbar slot, then add it back from that slot. Check both peers agree on contents and quantities; try an incompatible item or occupied destination and confirm refusal.
4. Turn generator/engine/pump on/off as their authored fuel, power and target rules permit. Check state on the other peer; a late joiner should see current module/fuel state.
5. Walk out of range/leave the boat, then issue a command from an open panel. It should be refused. Board again to regain the appropriate module access context.

## Remaining work

Regular piloting-cartridge routing, storage/container UI transactions, module installation/removal and live manifests, cargo pickup/drop/securing/throwing, broader inventory/equipment actions, NPC/vendor and other world interactions, live agents/damage/world simulation remain MP.4 coverage. Crew travel, durable saves, reconnect and lifecycle remain MP.5. The broader list remains pending even though these three module types now have a normal interaction path.


## 2026-10-12 usability corrections (acceptance pending)

Three testing blockers are addressed:

- Client boat visibility no longer depends on `IsTouching`/trigger callbacks from passive replicas. The local client's viewer resolves authored box/circle visibility zones against rendered player collider centres as the player moves. Exterior/interior and ocean presentation remain local to that viewer; host collision policy remains authoritative. Unchanged modes do not repeatedly rebuild rendering state.
- `PlayerHatchLedgeDropper` joins the host simulation allowlist. It already consumes `LocalCharacterIntentSource.Current`, so both participants use the validated ClimbDownHeld input stream and the existing collision-ignore/nudge/restoration routine. Clients do not apply their own collision mutations.
- The owner-authored `InventoryDebugSpawner` UI is enabled on each participant. F6 submits an authenticated host debug request; F8 and selection/quantity/charge controls remain local. The host requires the actor's authored spawner to have been enabled, a ready connected owning participant, a catalog definition, bounded quantity/charges, and an ordered rate-limited request. Feedback returns only to the requester and appears in the spawner HUD/Console.

In this multiplayer testing adapter, engine/generator/pump debug items install directly at the nearest compatible empty existing hardpoint within 6 metres while boarded. Normal host compatibility/support checks and module lifecycle still run. This is deliberately different from the single-player spawner's loose-item workflow because general loose cargo and ordinary module installation transactions remain pending. Other debug items use the requesting actor's existing acquisition resolver; if inventory/equipment cannot receive the item, the request refuses instead of dropping unreplicated cargo. Unsupported module kinds refuse explicitly.

A bounded additive hardpoint/item manifest reconstructs debug-installed modules on passive clients and late joiners. A client-only Hardpoint replica seam retains compatibility checks and applies the host-approved placement without rerunning authoritative support raycasts against absent client physics. Module/fuel watches, engine registry, interaction targets and visual caches refresh after installation. Renderer and collision caches are refreshed for current viewers/actors. Removal/replacement and general structural lifecycle remain pending.

Matching protocol is `DontSink/node-debug-modules/5`; rebuild both peers.

Verification: editor-symbol and player-symbol compilation pass. The ignored native test fixture adds one empty authored utility hardpoint. Initial host/client/observer tests passed debug generator installation, late reconstruction, client interior mode, existing fuel/state/power/range operations and a remote player's actual drop through a WorldLedge. The final matching-source run passes explicit host and client debug fuel creation/charge checks as well. All three native processes pass with zero runtime errors, and all nine changed runtime files match the build copies. A test-only race was corrected by waiting for the client to receive the ledge approach pose before injecting Down; production input/drop logic was not changed to accommodate the test. No production scene/prefab/material was changed for these corrections. Test logs remain ignored under `Temp/CodexNetworking/MP2ValidationProject/mp4-usability-sync-*`.

Manual acceptance: rebuild matching peers; board and move between deck/interior on the client; confirm exterior presentation changes for that client's viewer; open a hatch and hold S/Down to drop through; use PageUp/PageDown to select Basic Generator and F6 while within 6 metres of an empty utility hardpoint; spawn compatible fuel, then use the installed module panel. Confirm a late joiner sees the generator. General cargo/world debug dropping and other unreplicated gameplay commands remain outside this correction.


## 2026-10-12 inventory/storage/module lifecycle continuation

Normal personal inventory, plain shared installed storage, and E installation/held removal now have host adapters; see the three new checkpoint notes. The final protocol-11 existing-module regression PASSes on host/client/late observer. It identifies the original generator by hardpoint ID and recognizes the nearest valid debug install target, avoiding assumptions about module array order or one specific empty fixture hardpoint. General module linking, physical cargo actions and unconnected module/NPC/world families remain pending. Full BoatScene/piloting user acceptance is still on hold.


## 2026-10-12 inventory/storage/module lifecycle continuation

Normal personal inventory, plain shared installed storage, and E installation/held removal now have host adapters; see the three new checkpoint notes. The final protocol-11 existing-module regression PASSes on host/client/late observer. It identifies the original generator by hardpoint ID and recognizes the nearest valid debug install target, avoiding assumptions about module array order or one specific empty fixture hardpoint. General module linking, physical cargo actions and unconnected module/NPC/world families remain pending. Full BoatScene/piloting user acceptance is still on hold.
