# MP.4B — Complete item snapshots and shared fuel transfers

## Status

MP.4A was accepted by the user on 2026-10-11. MP.4B was manually validated by the user on 2026-10-11. The complete multiplayer handoff and MP.4 remain in progress.

The normal scene integration review found that MainMenuController still rejects multiplayer New Game, BoatSpawner locally restores an authored boat, and the scene restoration scripts bind a single authored player. Simply enabling connected scene loading would duplicate simulation/restoration and leave UI references pointing at the wrong actor. Complete shared item data is established here before wiring those bootstrap paths. Normal scene loading, world distribution and player/HUD replacement are not implemented by this checkpoint.

## Implemented

- NetworkItemData/NetworkItemCodec provide bounded host-owned snapshots, separate from 20 Hz poses. A versioned binary envelope preserves exact instance/definition IDs, quantity, charges, retained water, optional charting-instrument state, complete cartographic data, nested container layout and empty slots. Optional instrument/chart DTOs reuse their established JSON serialization and presence flags inside the binary item tree, preserving coverage masks, reference stars and sounding evidence rather than reproducing selected fields.
- NetworkPlayerActor captures the complete personal hotbar and equipment models. Inventory/equipment notifications and contained-item changes mark snapshots dirty; poses do not trigger inventory serialization. Clients validate the entire tree before restoring their passive models. Owner and remote actor inventories use their actual participant identity; no client uploads item data.
- NetworkSharedInteractionWorld publishes actual cargo and generator-container snapshots. The known-crate and invented one-slot fuel reconstruction was removed. Held equipment comes from the actor snapshot, rather than a second fixture-specific inventory writer.
- New WithdrawFuel/DepositFuel commands transfer one complete existing fuel instance between the shared generator and a specified requester hotbar slot. The development UI uses slot 1. These commands validate connected transport sender, character/actor mapping, target incarnation/revision, proximity, source ownership, slot bounds, compatible empty destination and replay/rate limits. Both model writes precede notifications; swaps, merges and partial stacks are outside this adapter.
- Conflicting withdrawals can only grant the one existing item to one actor. A repeated deposit does not recreate or move a second item. A rejected transfer leaves both stores untouched.
- Interaction replies now carry current target revisions, avoiding an immediate follow-up command racing the next state update.
- Protocol is `DontSink/shared-items/4`. All peers need a matching fresh build; old MP.4A players are deliberately incompatible.

## Bounds and remaining scope

Each data envelope permits 256 KiB, 512 items, nesting depth 8 and 64 slots per portable container. Personal hotbars use the existing 1–8-slot rule. Readers reject incompatible versions, unknown definitions, invalid identity/value/layout data, malformed lengths and trailing bytes before changing the live inventory. Oversized data is rejected explicitly; it is not silently truncated. Larger authored containers or larger chart data need a deliberate protocol extension/chunking path before normal scene use.

Definitions resolve through the existing fixture's authored definitions, the built-in cartographic chart definition, and an optional ItemDefinitionCatalog Inspector reference. This is not automatic discovery of every item definition in the project. Future scene bootstrap must provide the appropriate authoritative catalog.

The fuel adapter is one domain transfer path, not a claim that arbitrary container UI, market, harvesting, securing, item dropping from any slot or structural module installation is networked. Other non-networked interactors remain disabled in the development fixtures. Disconnected inventory retention, lockers/bodies, reconnect and durable save mirrors remain MP.5 work. No scene, original boat/player asset, world generator or save DTO version was changed in MP.4B.

## Verification

Editor-symbol and player-symbol production compilation pass. An isolated Unity 6000.0.65f1 standalone build passes including NGO postprocessing. All four changed/new runtime source files exactly match the copies compiled into that player.

Three actual Windows processes pass the inventory test with zero runtime errors: exact requester ownership, preserved item identity/charges/waterlogging, conflicting withdrawal, replayed deposit, invalid identity/revision/slot/range rejection, return of the original item, and late join with personal hotbar/equipment, nested contents and a 145,721-byte chart including its coverage mask and star reference. Codec checks cover chart round-trip, trailing-byte rejection and nesting limits. The chart size exercises real fragmented transport rather than a tiny mock message. A nested charge change after inventory insertion is also received.

MP.2 movement and MP.3 shared interaction/late-join/disconnect regressions pass. MP.4 sailing initially failed the existing short-window positive-displacement assertion; its complete recheck passed with zero runtime errors, including actual propulsion, occupancy, controls, moving cargo, late join and disconnect. This initial failure is retained as test-instability evidence rather than erased; the calm-water fixture's bobbing makes a short displacement window sensitive. There was no production physics change to make it pass.

Logs are under ignored `Temp/CodexNetworking`: `mp4-items-build.log`, `mp4-items-host.log`, `mp4-items-client.log`, `mp4-items-observer.log`, `mp4-items-regression-interaction-*.log`, `mp4-items-regression-movement-*.log`, `mp4-items-regression-boat-*.log` (initial failure), and `mp4-items-boat-recheck-*.log` (passing run). All owned test player processes exited or were stopped after failure. The user's open Unity project was not replaced by the harness.

## Manual acceptance

1. Leave any old session, make a fresh build and connect a host/client pair using the existing MainMenu development panel.
2. Start either the shared interaction test or sailing-boat test. Walk beside the generator.
3. Use **Take fuel → hotbar slot 1**. The generator should lose its fuel; only the requester panel should show that fuel and its current charge count.
4. Have the other player try taking it too; no second item should appear.
5. Return it with **Return fuel from slot 1**. Your slot should empty and the generator should regain that same fuel/charges. Start/burn fuel, take it and return it again to verify the changed charge count persists.
6. Repeat while sailing and join another player after taking fuel. It should see the empty shared generator and the carrying actor's current inventory state.

Next: connect the normal scene bootstrap with host-selected assets/world and explicit per-participant restoration/presentation, then expand the remaining MP.4 domain adapters and MP.5 lifecycle protocol.
