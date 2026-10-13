# MP.4 normal module installation/removal checkpoint

Implemented 2026-10-12. User presentation acceptance pending; MP.4 remains in progress.

## Behavior

Normal E on an empty compatible hardpoint installs a supported module from the interacting owner's loadout. The existing inventory search and authored support/compatibility rules are reused. Host selection, item identity/fingerprint, access range, boarding context, inventory layout and payload budgets are validated before installation consumes the source.

The ordinary held pickup input uses host-timed holds with 120 ms heartbeats and a 300 ms freshness cutoff. Cancellation, moving away, leaving boarding context, sitting or a changed hardpoint invalidate the hold. Removal requires an empty, switched-off module and an acceptable empty hotbar slot or empty Hands, then returns the original consumed item snapshot/identity. Authored starting modules without an originating item return their linked ItemDefinition, as in single player. Existing removal guards/lifecycle still run.

Supported families: engines, generators, pumps, helms, rudders and plain installed storage. Live tethers, turrets and other unconnected module families remain deferred. Helm/station wiring remains authored; a general multiplayer link-tool adapter is still pending.

The former append-only debug manifest is now one latest-state record per changed hardpoint (maximum 64). Removal/reinstallation replaces a record and advances its generation. Passive clients apply the current desired layout before module/storage snapshots. Fuel commands and storage slot addresses reject old generations; transient manifest/data arrival differences wait for matching state. Removed roots are deactivated before deferred destruction, and simulation registries exclude superseded roots.

F6 now places module items in personal inventory, restoring the normal workflow. Shift+F6 keeps the explicit debug auto-install shortcut. Existing scripted RequestDebugSpawn calls retain their previous default auto-install behavior.

## Verification

Editor/player compilation and native host/client/late observer fixture. Tests exercise actual normal E and held pickup, three remove/reinstall cycles, original instance identity, final installation, contents blocking, stale generation refusal and late current layout/fuel. Final matching-source evidence is recorded below. No production scene/prefab authoring was changed.

## Manual acceptance

1. Matching Host/Join builds; board and use F6 for a supported module.
2. Select it or retain it in the hotbar; hover a compatible empty hardpoint and press E. Confirm both peers see one installed module and the item disappears once.
3. Empty/switch off the module, hold the normal pickup key and confirm it returns once. Cancel halfway, move away and test a full inventory.
4. Repeat installation/removal, then join late. Check module/fuel/storage visuals and normal module controls.
5. For a pre-wired helm/rudder, confirm installing at the authored hardpoint retains the intended station connection. Full BoatScene piloting acceptance remains deferred at the user's request.

## Remaining lifecycle work

Original installed-item snapshots currently live in the host session registry; durable persistence across save/reload belongs to MP.5. Authored starting modules follow existing save semantics. This checkpoint does not claim crew travel, reconnect or complete SP parity. Owner movement prediction/reconciliation is still required before real-latency acceptance.


The native fixture also caught the Hands-only module case: `CharacterPlayer` has six hotbar slots and the generator definition excludes all six. The installed module is returned to empty Hands when no hotbar destination accepts it. Tests exercise this authored policy without changing production prefabs or item definitions.


## Final verification

`mp4-installation-final-{host,client,observer}.log` all report PASS with zero runtime errors. The native driver selects Hands, uses ordinary E and held pickup, verifies three complete cycles plus a fourth installed incarnation (generation 8), waits for the host's nonempty-module refusal, rejects a stale-generation command and checks late-join layout/fuel. Test-only fixture corrections included moving the empty hardpoint away from overlapping installed-module interaction boxes, closing module UI before pickup, and waiting for the held command's response rather than using a fixed one-second pulse. Production scene/prefab authoring was untouched.

All 985 production C# runtime/editor sources match the isolated build copies. Both standalone editor-symbol and player-symbol compilation pass. Full user presentation acceptance remains pending.


Final protocol-11 rerun: native loadout, shared storage and installation suites each PASS on host, client and late observer with zero runtime errors. The loadout suite additionally moves an item after its charge changes from 2 to 1 and verifies that the latest charge and original identity survive. `mp4-modules-inventory-regression-{host,client,observer}.log` also PASS: debug module replication/interior visibility, existing fuel transfer/on/off/power/range controls, late module/fuel state, remote drop-through and host/client debug item acquisition. All owned validation players exited. Changes remain uncommitted; manual presentation acceptance is pending.


## Six-pass continuation verification

The protocol-14 matching-source rerun passed on host, client and late observer with zero runtime errors, alongside helm linking, loose items, charged throwing and the existing module-control regression. See MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md for the combined checklist/evidence. Q-based ordinary world drops and authenticated L linking are now connected; general world actions and MP.5 remain pending. Manual acceptance is pending; no commits were made.
