# MP.4 shared installed storage checkpoint

Implemented 2026-10-12. User presentation acceptance pending; MP.4 remains in progress.

## Behavior

Plain installed lockers/racks use the existing module cartridge, Add Item/Remove Item controls and external slot overlay. The Open slots button exposes normal drag and quick transfers. Actor binding is explicit, and open views rebind when replicated container state changes.

`NetworkSceneBoat.Storage` publishes bounded host-owned container snapshots, at most four times per second, independently of boat poses. Snapshot equality suppresses unchanged network writes. The registry contains only current installed plain storage modules, at most 64, with a combined 256 KiB item budget. Portable children retain the same item codec and nesting limits.

Transfers share `NetworkInventoryModel` with personal inventory. The host checks authored hardpoint access, matching boarding context, collider range, storage slot policy, current hardpoint generation and both structural item fingerprints. The complete personal/shared transaction is prepared before writes and listeners. Two requests using the same source fingerprint cannot both consume that source. Tether payload storage is deliberately excluded pending its deployment lifecycle adapter.

`StorageModule` has a narrow silent replacement/publication seam; single-player placement policy remains unchanged. The rack visual already detects replacement state and rebinds its subscriptions. No production scene or prefab provisioning was added.

## Verification

Native fixture uses an authored locker hardpoint only in the ignored validation project. Checks: normal E opens the module cartridge; existing slot overlay opens; client deposit/withdrawal reaches both models; serialized competing source requests have one winner; wrong generation and out-of-range requests are rejected; late observer receives current contents. The first run passed these behaviors but its client assertion raced a later host write. The corrected fixture waits for the withdrawal snapshot before proceeding; final matching-source logs are recorded below.

## Manual acceptance

Board, press E on an installed locker/rack, use Add/Remove and Open slots. Drag and quick-transfer items, including a filled portable container. Have both peers attempt the same item. Leave access range and try a transfer from a panel left open; it must fail or close. Join late and confirm contents/rack visuals.

Shared fuel tanks continue using the existing dedicated module adapter. Shared world chests, loose cargo and live tether payloads are not enabled by this checkpoint.


## Final verification

`mp4-final-build.log` reports a successful native development build. `mp4-storage-final-{host,client,observer}.log` all report PASS with zero runtime errors. The final run includes the module lifecycle implementation and current-generation storage addresses. The earlier withdrawal assertion race was corrected in the test driver by allowing the client snapshot to arrive before a later host mutation.


Transfer fingerprints track identity, definition, quantity, nested contents and chart metadata. They exclude live charge/water measurements, so drying or consumption cannot permanently prevent a move. The transaction still copies the latest authoritative measurements and preserves them during split/merge. Protocol 11 requires matching peer builds.


Final protocol-11 rerun: native loadout, shared storage and installation suites each PASS on host, client and late observer with zero runtime errors. The loadout suite additionally moves an item after its charge changes from 2 to 1 and verifies that the latest charge and original identity survive. `mp4-modules-inventory-regression-{host,client,observer}.log` also PASS: debug module replication/interior visibility, existing fuel transfer/on/off/power/range controls, late module/fuel state, remote drop-through and host/client debug item acquisition. All owned validation players exited. Changes remain uncommitted; manual presentation acceptance is pending.


## Six-pass continuation verification

The protocol-14 matching-source rerun passed on host, client and late observer with zero runtime errors, alongside helm linking, loose items, charged throwing and the existing module-control regression. See MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md for the combined checklist/evidence. Q-based ordinary world drops and authenticated L linking are now connected; general world actions and MP.5 remain pending. Manual acceptance is pending; no commits were made.
