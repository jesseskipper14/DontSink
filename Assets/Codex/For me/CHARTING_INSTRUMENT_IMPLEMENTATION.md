# Charting Instrument — implementation delivery

This focused pass connects a sacred physical boat item to the existing CelestialObservationCartridge, supplies paper through its ordinary internal container, and saves the completed observation on the exact item instance. Inspector setup remains user-owned; see `INSPECTOR_TASKS_CHARTING_INSTRUMENT.md` before live testing.

## Architecture and controls

`ChartingInstrumentInteractable` reuses **PlaceableBoatEquipment** and **SkyClearanceRequirement** unchanged. E deploys a loose item on valid boat support; E on a deployed item acquires a single operator and launches the existing scene runner. X unpins after the operator exits. F uses ordinary WorldItem pickup when loose. No securing rope, BoatBuilder install, telescope fade, or new camera is involved. The existing BreakDeployment seam remains available for a future impulse pass.

T opens the existing ExternalContainerOverlayUI for the same ItemInstance. The standard WorldItemContainerDropTarget handles paper drag/drop after it is added in the Inspector. The container filter lives entirely on the existing ItemDefinition: explicit Charting Paper acceptance, one slot, per-slot cap 0 to follow the paper definition's normal stack limit. Paper access is independent of the operator lock.

The runner preserves the exact requester, rejects a busy overlay, and releases the operator through the cartridge's End callback. Its instrument session rechecks placement, boat/user context, distance and clearance. Obstruction interrupts the active cartridge and posts exactly `No unobstructed view of sky.`. E closes the session; the existing MiniGameOverlayHost / EscapeCloseRegistry handles Escape. Opening E cannot close the new session in the same frame. Runner disable and instrument disable clean up the active session. No load automatically opens charting.

## Paper transaction

The physical path calls CelestialChartingAuthority with the exact instrument. Authority validates that operator, deployment, clearance, canonical boat device and pending observation identity still match. It then consumes **one paper from the instrument's direct ItemContainerState**, never the operator's pockets or another container. The existing evidence/sequence transaction and rollback remain in place. Only successful evidence commit clears the checkpoint.

The separate F6/debug path remains available and retains its legacy requester-inventory paper source. It does not promise device-owned persistence. A destroyed instrument reference fails closed rather than silently changing a physical session into the pocket-paper debug path.

## Completed-observation checkpoint

Completing the correct trace now builds a CelestialObservation immediately at the ReadyToRecord boundary. This freezes the evidence ID, original observer position, date/hour, visibility, quality, calibration result, survey sequence, pattern IDs and observed object coordinates. Recording retries reuse this exact completed result rather than resampling current sky conditions or creating a new observation ID.

`ChartingInstrumentState` contains:

- version 1;
- owning/associated boat instance ID;
- revision;
- invalidation flag for a retired physical device;
- optional pending CelestialObservation.

No operator, open overlay, cursor, camera or unsolved calibration/transcription state is saved. The state is an additive ItemInstance field and an additive **ItemInstanceSnapshot v1** field; no BoatLooseItemSnapshot version bump or separate boat registry/save channel was needed. Existing container snapshots already persist paper, and existing boat equipment snapshots already persist deployment and local pose. Item-owned state also survives ordinary pickup into Hands/inventory and re-instantiation.

Snapshots deep-copy the completed result. Unity inline serialization can materialize a default object for a null class field, so empty payloads are normalized using meaningful state and observation ID checks. Cleared observations stay cleared after JSON round trips; ordinary items do not become charting devices merely because a default inline object appeared.

Resume uses the saved observation origin/sequence, enters RECORD directly, and reconstructs the existing presentation from its pattern/framing. Recording completed work is permitted during daylight, while current visibility still gates new calibration/transcription. Resume after travel does not turn the observation into evidence of the new location. RECALIBRATE explicitly discards pending work; exit/reopen starts a fresh survey at the current location. Celestial generation knowledge is frozen when a physical checkpoint is stored, using the existing authority helper.

## Sacred existence and replacement

`ChartingInstrumentReplacement` derives current validity from the boat's registered world items, installed storage, crew loadouts/nested containers, and offline crew snapshots. Live loadouts supersede their stored mirrors. An item-associated boat ID also preserves crew ownership while a carried device is ashore. No Lost/recovery state machine or persistent static cache was added.

Only one canonical device may chart per boat, chosen by item identity. Debug/corrupt physical duplicates cannot all become active operators. The existing item ID `item_charting_object` is retained as a compatibility identity; definitions whose WorldPrefab contains ChartingInstrumentInteractable also qualify.

Authority APIs:

```csharp
ChartingInstrumentReplacement.HasValidPersistedInstrument(boat);
ChartingInstrumentReplacement.TryIssueReplacement(boat, definition, position,
    out WorldItem replacement, out string error);
ChartingInstrumentReplacement.ForceReplaceChartingInstrument(boat, definition, position,
    out WorldItem replacement, out string error);
```

The boat must be live, active, identified, and have BoatItemRegistry. The definition must be sacred, BoatOnly, a container, and reference the instrument prefab. Normal issuance refuses an existing valid device. Forced issuance invalidates/deletes the old device before issuing a fresh identity; pending observation and old paper do not transfer. Obsolete unowned devices associated with that boat are retired too. Matching stale boat/module/loadout snapshots are scrubbed, while other boats' crew items and ordinary saved items are preserved. Replacement starts loose and requires normal deployment.

`ChartingInstrumentReplacementHook` is an optional MonoBehaviour adapter for a boat child or town/debug object, with explicit Boat, definition and issue-point references plus Play Mode context menus. No automatic starter grant, final NPC dialogue or vendor system was added. The normal boat escape/ownership tracker still decides when an overboard item ceases to travel with the boat.

## Shared vs local / multiplayer limits

The authoritative physical component owns the transient single-operator lock. Item identity, deployment, paper and pending checkpoint are consequential shared state; cartridge UI is local presentation. A second actor cannot acquire the same instrument but can access paper storage, and added paper is queried at commit time. The operator who later resumes need not be the original observer.

No network transport/replication was added. Future bootstrap must authenticate the requester, route the host state to peers, bind each local actor to its UI, and route GameMessageService warnings per player; the current service and GameplayInputBlocker are process-wide. The new paper-storage UI lookup prefers the exact actor hierarchy, then permits a unique scene UI only; it refuses ambiguous multi-UI fallback. Actual peer networking and live two-client supply behavior remain to be verified when that transport exists.

## Exact production files

Modified:

- `Assets/Scripts/GameState/Inventory/ItemInstanceSnapshot.cs`
- `Assets/Scripts/Inventory/Item/ItemInstance.cs`
- `Assets/Scripts/Inventory/CelestialChartPaperConsumption.cs`
- `Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.cs`
- `Assets/Scripts/MiniGames/Runners/CelestialObservationOverlayRunner.cs`
- `Assets/Scripts/WorldMap/Celestial/CelestialChartingAuthority.cs`

Added:

- `Assets/Scripts/Inventory/BoatEquipment/ChartingInstrumentState.cs`
- `Assets/Scripts/Inventory/BoatEquipment/ChartingInstrumentInteractable.cs`
- `Assets/Scripts/Inventory/BoatEquipment/ChartingInstrumentReplacement.cs`
- `Assets/Scripts/Inventory/BoatEquipment/ChartingInstrumentReplacementHook.cs`
- `Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.Checkpoint.cs`
- `Assets/Codex/CHARTING_INSTRUMENT_IMPLEMENTATION.md`
- `Assets/Codex/INSPECTOR_TASKS_CHARTING_INSTRUMENT.md`
- corresponding new Unity metadata, where absent.

No prefab, scene, ItemDefinition or catalog Inspector settings were edited. No core celestial generation, chart table, constellation, weather or telescope behavior was redesigned.

## Verification

- Full runtime sources plus the existing Editor sorting-layer drawer compile successfully against Unity 6000.0.65f1 references.
- Isolated Unity harness uses unchanged production item definition/instance/container placement, cartridge/runner/overlay, chart authority and new instrument/replacement implementations. Surrounding scene/boat/inventory/UI hosts are adapters; this is not a full live project or network test.
- Existing celestial generation, evidence/naming/constellation, branch/layer/thinning, reset/persistence, telescope and void-transition regression checks pass.
- New checks cover filtered normal stacks; quantity/whole-paper rollback; checkpoint capture at trace completion; no-paper retries; original evidence preservation; old/ordinary/cleared JSON payloads; paper persistence; successful and duplicate evidence commits; no pocket-paper fallback; loose/deployed use; two-actor occupancy/storage availability; direct runner resume by day; Escape/obstruction/runner teardown; carried/offline crew presence; duplicate validity; normal/forced replacement; ordinary/other-boat snapshot preservation; clean replacement identity, storage and deployment; and client-only authority refusal.

Final assertion count and live checks are reported in chat. Complete the separate Inspector/live checklist before committing this pass. Temporary compilation and harness artifacts remain ignored under Temp/CodexPhase7.

## Discrepancies found in authored assets

The existing ChartingObject prefab still contains ObservationTelescopeInteractable. Its definition still references the telescope WorldPrefab, has a blank explicitly allowed paper entry, and uses an extra per-slot cap of 20 while Charting Paper's authored Max Stack is 10. The instrument definition was also absent from itemCatalog. These are listed as explicit user Inspector tasks rather than silently repaired.
