# MP.4 six-pass acceptance checklist

2026-10-12. Six contained adapters implemented; user play-testing pending. MP.4 is not complete. All changes remain uncommitted.

## Setup

Build host and client from this same revision (protocol 14: DontSink/node-loose-items-throw/14). Host a new game, join the client, and board the shared boat on both. Keep both views visible. Perform each action as the client first, then repeat as the host. Use F6 to acquire supported module items; Shift+F6 explicitly retains the debug auto-install shortcut. The generator may require empty Hands because the authored six hotbar slots do not accept it. Use the normal displayed controls if any authored binding differs; defaults are E interact, F held pickup, L link, Q drop/throw.

Use an authored compatible empty hardpoint, a plain installed locker/rack, an installed helm and pilot chair, ordinary loose cargo and a portable container. Do not change the boat's support/wiring rules merely to make a test pass. Full BoatScene execution/piloting acceptance remains on hold; the first six tests can run in NodeScene. Skip moving-boat subtests until that dependency is available.

## 1. Personal inventory / equipment / portable containers

- Open the existing inventory. Drag, cancel, split and merge a stack. Total quantity must stay constant; a whole move preserves its identity.
- Move an allowed item into Hands/equipment and back; try an incompatible slot, occupied footprint and full destination. Refusal keeps the source.
- Insert into and withdraw from a portable container; test a filled container and charting paper with metadata. Contents/paper state must survive. A container cannot go inside itself.
- Close a panel mid-drag or release outside supported UI. Nothing disappears or drops into the world accidentally.
- Another player's inventory must not become a shared editable inventory.

## 2. Installed plain storage

- E on a plain locker/rack. Use the existing Add/Remove and Open slots controls; drag and quick-transfer items both ways.
- Transfer a filled portable container; its contents must survive. Both peers must agree on the locker contents/rack visuals.
- Have both players try to take the same remaining item. Exactly one receives it.
- Leave range with a panel open, then try a transfer. It must refuse or the panel must close; no remote withdrawal.
- Have a third build join late and inspect current contents.

## 3. Normal module installation / held removal

- Acquire a supported engine/generator/pump/helm/rudder/plain storage item with F6. E on its compatible empty hardpoint: one module appears on both peers and one item is consumed.
- Switch it off and empty it, then hold F to remove it. It returns once to an accepting inventory slot or empty Hands. Original item identity/metadata should survive a session install/remove cycle.
- Cancel halfway, move away, and try an on/nonempty module or full inventory. These must not remove it.
- Repeat removal/reinstallation several times. No duplicate roots, phantom power/fuel or stale locker windows.
- Join late and verify the final installed layout. Existing fuel/on-off controls must still work.

## 4. Helm / chair linking

- Stand near the installed helm, hover it and press L. Hover an unoccupied chair and press L. Both peers should see the completed link; normal station interaction should use that helm.
- With no pending helm selected, L on the linked chair unlinks it. Relink it. L on the same selected helm cancels selection.
- Have each player select a different helm where available: selections must stay personal.
- Try linking an occupied chair, an out-of-range target, or a helm removed after selection. No invalid link should be applied.
- Join late and confirm the final chair link. Full console travel/piloting acceptance remains deferred.

## 5. Loose world-item pickup / drop

- Hover ordinary cargo and hold F for the normal pickup duration. It disappears once from both world views and enters only the picking player's loadout.
- Tap Q on the selected item to drop it. Both peers see one item at the actor; pick it back up. Repeat several times.
- Try an interrupted hold, full inventory, secured/attached item, distant item and simultaneous pickups. Nothing duplicates or disappears on refusal.
- Drop/re-pick a filled portable container and processed/chart paper. Their contents and represented paper state must survive.
- Join late with cargo on deck; inspect its location and appearance. Leave/rejoin should not add duplicate authored crates.
- Later, on a moving boat, drop cargo and check that it inherits carrier velocity rather than being left behind.

## 6. Charged cargo throwing

- Put ordinary cargo in Hands and select Hands. Hold Q, aim, and release. Longer charge should throw farther, with the existing trajectory preview. A Q tap drops normally.
- Bowl into a cargo stack and a second player. Both peers should see the same authoritative result; cargo/player collision must be active during flight.
- Let it settle. Its normal collision behavior/layers should return; settled cargo must not retain the throw-only player collision override.
- Change selection, open a blocking UI, or lose focus mid-charge. Charging cancels without losing or throwing the item.
- Pick up thrown cargo and repeat; one release must create only one world item.
- Later, repeat on a moving boat and check inherited velocity and throw direction. Existing owner-movement latency is a separate prediction follow-up.

## Remaining scope

General world-container/treasury/vendor/NPC actions, securing/unsecuring, live tether/bell/other module families, crew travel, durable module-origin/link persistence, reconnect/save and owner movement prediction remain pending. These six passes restore the listed workflows; they do not claim complete multiplayer/single-player parity.

## Automated evidence

All five native suites passed on host, client and late observer with zero runtime errors:

- `mp4-six-items-{host,client,observer}.log`: normal L select/unlink/relink, ordinary F cargo pickup, Q-path drop commands, persistent item identity, passive client poses, host charged impulse, no-begin/cancelled/expired charge rejection, release replay without duplication, stale world-incarnation refusal, collision override restoration at rest, late current chair/cargo and leave cleanup.
- `mp4-six-loadout-{host,client,observer}.log`: normal inventory preview/cancel, split/merge conservation, retained water, latest live charges, equipment/Hands, nested containers, same-container icon insertion, self-container/stale/foreign-owner refusals and late loadouts.
- `mp4-six-storage-{host,client,observer}.log`: existing cartridge/slot UI, deposit/withdrawal, one-winner contention, generation/range refusal and late contents.
- `mp4-six-installation-{host,client,observer}.log`: normal E install and held pickup, originating identity, repeated incarnations, nonempty-module refusal, stale generation and late final layout/fuel.
- `mp4-six-modules-{host,client,observer}.log`: existing debug/module visibility, fuel/on-off/power/range commands, late module state, remote drop-through and host/client debug acquisition.

`mp4-six-verified-build.log` records a successful native development build. Standalone editor-symbol and player-symbol compilation pass. All 988 production C# files match the isolated build copies. The first exploratory run exposed a test logger cast; a second exposed a test waiting on the old world-item incarnation. Both fixture issues were corrected before these final runs. Throwing feel/bowling, production UI and moving-boat behavior still require the manual checks above. Production scene/prefab authoring is unchanged; test fixture provisioning lives only in ignored Temp/CodexNetworking/MP2ValidationProject.
