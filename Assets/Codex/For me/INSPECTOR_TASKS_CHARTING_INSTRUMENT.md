# Charting Instrument — Inspector tasks

No prefab, scene, ItemDefinition, or catalog Inspector settings were edited in this pass. Complete these in Edit Mode, then test a freshly initialized item. Existing spawned objects do not acquire newly added prefab components automatically.

## 1. Item definition

Open `Assets/Defs/Items/Placeable/item_charting_object.asset`.

| Field | Set / verify | Purpose |
|---|---|---|
| Item Id | Keep `item_charting_object` | Compatibility with the existing item and sacred existence query |
| Display Name | `Charting Instrument` | Player-facing name |
| World Prefab | `Assets/Resources/Prefabs/Items/Placeable/ChartingObject.prefab` | Currently points to **ObservationTelescope**; this must be corrected |
| Item Categories | Tool + Sacred | Already set; preserves sacred vendor restrictions |
| World Persistence | BoatOnly | Already set; no overboard recovery registry |
| Max Stack | 1 | Already set; device is an individual item |
| Is Portable Container | Enabled | Existing ItemInstance storage architecture |
| Portable Container Slot Count / Column Count | 1 / 1 | One normal paper stack |
| Portable Container Max Quantity Per Slot | **0** | Currently 20; 0 follows Charting Paper's authored Max Stack (currently 10) |
| Portable Container Accepts Any Item | Disabled | Paper-only storage |
| Portable Container Allowed Categories | None | Avoid accepting other consumables |
| Explicitly Allowed Portable Container Items | Exactly `Assets/Defs/Items/Items/item_paper_charting.asset` | Current entry is blank |
| Portable Container Tier | 0 | Keep existing nesting rules |

Open `Assets/Defs/Items/Catalogs/itemCatalog.asset`: add `item_charting_object` exactly once. It was absent when inspected. Keep the existing Charting Paper entry. This is required for spawn lookup and save/load resolution.

## 2. Physical prefab

Open `Assets/Resources/Prefabs/Items/Placeable/ChartingObject.prefab`, root `ChartingObject`.

1. **Remove `ObservationTelescopeInteractable`.** The prefab was copied from the telescope and still contains that component. Otherwise E may select telescope behavior.
2. Keep existing `WorldItem`, `BoatOwnedItem`, Rigidbody2D, colliders, highlighting and boat visual/layer policies.
3. Keep **PlaceableBoatEquipment**. For the current centered `1.85 × 1.75` BoxCollider2D: Support Width **1.5**, Support Depth **0.2**, Foot Offset **-0.875**, Support Layers **Everything** initially. Its current foot offset is -1; -0.875 matches the actual collider bottom. Inspect the contact with a real deck.
4. Keep **SkyClearanceRequirement**: Clearance Width **1.9**, Clearance Height **3**, Origin Height **0.925**, Obstruction Layers **Everything** initially, Recheck Interval **0.15**. The selected cyan box should begin just above the physical device. Narrow the mask only for intended non-obstructions.
5. Add **ChartingInstrumentInteractable**: Max Interact Distance **2**, Interaction Priority **1100**, Runner **None** on the prefab. Runtime resolves exactly one active scene runner; explicit scene instances may use an exact runner reference.
6. Add **WorldItemContainerDropTarget**: World Item = root `WorldItem`, Max Deposit Distance **2.25**, Require Matching Boat Boarding Context = enabled. Keep its normal default access settings. This gives ordinary paper drag/drop onto the physical instrument.
7. Do **not** add a second `WorldItemContainerInteractable` to the same root. `ChartingInstrumentInteractable` already opens the normal ExternalContainerOverlayUI through T; a second E-interactable would compete for targeting.

Controls with existing default interaction bindings: **F** pickup when loose, **E** deploy then use, **T** paper storage, **X** unpin when nobody is operating, **E / Escape** exit charting. Storage is accessible while loose on the boat and while another operator charts.

## 3. Scene references

In both **NodeScene** and **BoatScene**, verify the existing `CelestialObservationOverlayRunner`:

- Overlay: the existing `MiniGameOverlayHost` for the observation cartridge.
- Field Source: that scene's `CelestialFieldSource`.
- Projection Settings: `Assets/Defs/WorldMap/Celestial/CelestialSkyProjectionSettings.asset`, the same asset used by the sky renderer.
- Observation Settings: `Assets/Defs/WorldMap/Celestial/CelestialObservationSettings.asset`.
- Charting Paper Definition: `Assets/Defs/Items/Items/item_paper_charting.asset`.
- Sky Visual Manager: existing runtime manager or leave runtime auto-resolution as already configured.

The projection, observation and paper asset references were already present in both saved scenes. Keep exactly one active observation runner in each scene. F6 can remain a debug shortcut; its legacy pocket-paper path is deliberately separate from physical instrument use.

Verify the existing `MiniGameOverlayHost` uses global Escape routing, and `EscapeCloseRegistry` / `GlobalEscapeRouter` exist with the normal menu reference. No new camera or telescope presentation component is needed.

## 4. Optional debug/town replacement adapter

Add **ChartingInstrumentReplacementHook** to a boat child or a temporary town/debug object:

- Boat: exact current boat; a boat child can leave this blank and use its parent.
- Instrument Definition: `item_charting_object`.
- Issue Point: an existing transform just above an unobstructed boat deck. For a town object, assign the exact Boat explicitly.

In Play Mode use its component context menu:

- `Charting Instrument / Issue Missing Instrument (Play Mode)` issues only if the crew lacks a valid device.
- `Charting Instrument / FORCE Replace And Discard Pending Observation (Play Mode)` intentionally retires the old device, its paper and pending observation, then issues one clean loose device.

Final NPC dialogue can call the same API later. No NPC content, automatic starter grant, or vendor stock was added.

## 5. Live verification before commit

1. Spawn through the existing InventoryDebugSpawner / item drop path using `item_charting_object`. A raw dragged prefab still needs WorldItem initialization and boat ownership, as with the telescope.
2. F pickup / drop works. E while loose deploys only on valid upright boat support; it cannot launch charting. X releases deployment after use ends.
3. T opens paper storage. Other items are rejected; one ordinary Charting Paper stack fits. Drag/drop onto the device also accepts paper.
4. E on the deployed device opens the existing cartridge by day and night. Hidden stars remain unselectable. No boat fade or camera redesign occurs.
5. Finish a pattern with **no paper in the instrument**, even if pockets contain paper. Recording should report missing paper, retain the completed observation, and leave pockets untouched.
6. Exit with E and separately Escape; first Escape closes charting, next opens the normal menu. Return: go directly to RECORD. Add paper using T or drag/drop; record consumes one instrument paper and adds one map-table fragment.
7. Repeat the no-paper case across NodeScene ↔ BoatScene, travel, save/quit/reload. Verify item identity, pinned state, paper contents and pending observation; verify nobody loads into an open cartridge.
8. Resume the same observation at a new location or during daylight: its recorded origin/time/evidence must stay the original observation. Current sky visibility still controls new star selection, but completed work can be written onto paper later.
9. Add an overhead blocker during use: safe exit, exact `No unobstructed view of sky.` warning, no paper/evidence side effect, completed checkpoint retained. Clear the blocker and resume.
10. Use RECALIBRATE to deliberately abandon the pending observation. Exit/reopen to start a fresh survey at the current position.
11. Normal replacement refuses a crew-owned physical/carried/stored device. Forced replacement removes/inactivates it before issuing one clean replacement; old progress never transfers. After losing boat ownership, normal replacement becomes available.
12. Check map-table fragments, celestial truth, known constellations and telescope observation still behave normally.

The isolated tests exercise authority-side occupancy with two actors. Actual network transport, peer replication, crew bootstrap, and per-player HUD ownership remain future integration work; verify the shared paper-supply flow when that transport exists.

**Commit after these Inspector tasks and live checks pass.** 🍌
