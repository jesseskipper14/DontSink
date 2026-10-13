# MP.4 normal personal inventory checkpoint

Implemented 2026-10-12. User presentation acceptance pending; MP.4 remains in progress.

## Behavior

The existing hotbar, equipment, drag preview, split/merge, portable-container and quick-transfer UI now submits authenticated host commands. Dragging reserves a preview without extracting the authoritative item. Cancelling or dropping outside supported UI keeps the source item. Selection reaches the host so ordinary station/module actions use the correct participant loadout.

`NetworkInventoryAddress` names hotbar/equipment/container/storage slots. `NetworkInventoryModel` plans against detached persistence snapshots, checks source/destination structural fingerprints, ownership, slot policy, stack limits, equipment footprints, portable nesting/cycles and byte/depth budgets. It constructs every replacement before writing either inventory side, then publishes notifications. Whole moves preserve identity; splits mint one new identity and divide retained water; merges preserve the receiving identity. Existing item/chart metadata follows the snapshots. Views rebind by canonical item ID after replication replaces model trees.

Another living player's loadout cannot be addressed for mutation. Nested inventories are resolved only through the requester's personal items or accessible shared storage. Deployed sounding-line items cannot be moved. Unsupported world dropping/throwing remains fenced rather than mutating a client replica.

## Verification

Editor/player-symbol compilation. Native host/client/late observer tests in the ignored `Temp/CodexNetworking/MP2ValidationProject` fixture cover normal drag preview/cancel, split/merge conservation and IDs, retained water, hotbar-to-hands, nested insertion, self-container rejection, stale fingerprints, host selection and foreign-player inventory refusal. Initial successful run: `mp4-loadout-{host,client,observer}.log`. Matching-source regression is recorded at the end of this checkpoint run.

## Manual acceptance

1. Rebuild both peers from the same code. Open the normal inventory while aboard.
2. Drag, cancel, split, merge, equip and move items into a portable container. Confirm both peers agree and full moves keep instance identity.
3. Ctrl/quick-transfer to an open supported container/storage and back. Try an incompatible wearable, occupied equipment footprint and full destination.
4. Close a panel while dragging; no item should disappear. Unsupported world drops should keep the source item.

One pending request is allowed per local transfer stream. A one-item deposit ends the drag preview after submitting. Broad usable-item actions, physical cargo pickup/drop/throw/securing, NPC/vendor interactions and unconnected container sources are still separate MP work.


## Final verification

`mp4-loadout-final-{host,client,observer}.log` all report PASS with zero runtime errors using the final storage/lifecycle build. This adds the same-container-icon insertion check: the source slot is excluded from insertion candidates, preventing a no-op self-merge. The original child identity and quantity survive the move and restoration. Late observer confirms full nested state and refusal to access another living player's inventory. Editor/player compilation and source parity checks also pass.


Transfer fingerprints track identity, definition, quantity, nested contents and chart metadata. They exclude live charge/water measurements, so drying or consumption cannot permanently prevent a move. The transaction still copies the latest authoritative measurements and preserves them during split/merge. Protocol 11 requires matching peer builds.


Final protocol-11 rerun: native loadout, shared storage and installation suites each PASS on host, client and late observer with zero runtime errors. The loadout suite additionally moves an item after its charge changes from 2 to 1 and verifies that the latest charge and original identity survive. `mp4-modules-inventory-regression-{host,client,observer}.log` also PASS: debug module replication/interior visibility, existing fuel transfer/on/off/power/range controls, late module/fuel state, remote drop-through and host/client debug item acquisition. All owned validation players exited. Changes remain uncommitted; manual presentation acceptance is pending.


## Six-pass continuation verification

The protocol-14 matching-source rerun passed on host, client and late observer with zero runtime errors, alongside helm linking, loose items, charged throwing and the existing module-control regression. See MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md for the combined checklist/evidence. Q-based ordinary world drops and authenticated L linking are now connected; general world actions and MP.5 remain pending. Manual acceptance is pending; no commits were made.
