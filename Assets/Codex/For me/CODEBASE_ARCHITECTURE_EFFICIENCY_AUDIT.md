# Don't Sink — Codebase Architecture & Efficiency Audit

**Audit date:** 2026-10-09  
**Project snapshot:** Git HEAD `1fe167f5107b7918289bec02ad4539e3b2aa2db4`, including the existing working tree.  
**Engine:** Unity 6000.0.65f1.  
**Requested scope:** [Official audit handoff](<C:/Users/skip_/Downloads/Dont_Sink_Codebase_Architecture_Efficiency_Audit.md>), plus LOC per system.  
**Mode:** Audit only. This report adds documentation; it does not change production code, scenes, prefabs, materials, project settings, or handoff statuses.

## A. Executive Summary

**Verdict: Healthy with manageable debt.** The project is large because it implements a substantial game: authored boats, compartment flooding, moving reference frames, inventory and nested containers, modular equipment, tethers, celestial navigation, generated geography, town simulation, surveying, persistence, and extensive authoring tools. Its size alone does not justify a rewrite.

The measured baseline is **208,025 physical C# lines across 970 files**, rather than the handoff's approximate 214,000 C# lines. Including 34 shader files adds 5,118 lines, giving **213,143 source lines across 1,004 files**. The median C# file is 91.5 lines; most files are small contracts, definitions, models, or focused components. A minority of active files carry disproportionately broad responsibilities.

An approximate **95% useful-source estimate, with a reasonable 94–97% range**, still fits the evidence. This is an engineering judgment about whether source serves current gameplay, authoring, persistence, diagnostics, or a deliberate near-term seam. It is **not** measured execution coverage, a promise that the remainder can be deleted, or an estimate of the minimum code necessary to implement the game. Design notes can be useful without being executable.

There is **no compelling evidence of systematic, project-wide AI bloat**. Provenance cannot be inferred from style or file size. There is evidence of incremental development debt: copied vendor/delivery helpers, gameplay ownership creeping into cartridges, global actor fallbacks, legacy routes and map overlays surviving alongside newer navigation, and methods that combine several independent jobs. Duplication is worth addressing locally; it is not currently the dominant source of LOC.

### Five principal concerns

1. **Transaction atomicity:** [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:176>) mutates items/offers before final money application. A late failure can leave a partial result. This is a concrete code path, although no failed transaction was reproduced during this audit.
2. **Gameplay state in UI:** [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:691>) owns finite stock in a static UI-state dictionary. The runner checks it, so this is not an assertion of unlimited purchasing today; it is an ownership, persistence-policy, and future authority problem.
3. **Incomplete multiplayer boundaries:** [GameplayAuthority](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Multiplayer/GameplayAuthority.cs:5>) is a role gate, not authenticated networking. Several shared simulation paths and module controls still assume a local authoritative scene.
4. **Change radius:** [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>), [InventoryDragController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/InventoryDragController.cs:1>), [WinchModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:1>), and scene/persistence coordination combine enough concerns that small changes can cross multiple systems.
5. **Lifecycle and runtime housekeeping:** a confirmed rain event-unsubscription omission, repeated allocation patterns in compartment flow, and some expensive-looking query paths merit contained follow-ups. Their frame costs were not measured here.

### Five principal strengths

1. The **agent definition/brain/movement/service architecture** already supports reuse instead of bespoke NPC controllers; see [AgentController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Runtime/AgentController.cs:1>) and [AgentServiceHandler](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Services/AgentServiceHandler.cs:1>).
2. **Navigation truth, player knowledge, and presentation are meaningfully separated**; see [WorldNavigationService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs:1>), [WorldTopology](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldTopology.cs:1>), and [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>). These concepts should not be collapsed merely because several are called “map.”
3. **Actor and camera ownership seams exist** in [IInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Interfaces/IInteractable.cs:1>), [LocalInteractionIntentSource](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Interactions/LocalInteractionIntentSource.cs:65>), and [CameraManager](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Camera/CameraManager.cs:183>). Recent moving-boat fixes address real complexity.
4. **Persistent instances, definitions, and snapshots are differentiated**; see [ItemInstance](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemInstance.cs:44>), [ItemDefinition](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemDefinition.cs:1>), and [GameState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/GameState.cs:1>). There is migration debt, but the basic distinction is sound.
5. **The optimized celestial rendering and authoring tooling are valuable infrastructure**, not obvious surplus. Preserve [CelestialSkyRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.cs:1>) and the boat-builder tools while improving their seams.

### Confidence and limits

Inventory and line counts are measured from current files. Dependency, method, duplicate, and reachability figures are conservative lexical estimates. Targeted semantic review validated the principal findings, but this was not a manual review of every branch in all 970 files. Unity serialization, Resources loading, callbacks, reflection, partial classes, preprocessor branches, and inactive authoring assets can defeat naive usage analysis.

No Unity play session, standalone build, test suite, two-client session, or profiler recording was executed in this audit. Runtime rankings below identify code patterns to measure; they do not assert milliseconds saved. Existing profiler recordings and historical FPS screenshots are not fresh measurements of this snapshot.

## B. Codebase Metrics

### Counting rules

Physical LOC includes blank lines, comments, declarations, braces, and directives. “Code/directive LOC” excludes blank and comment-only lines using a lexical scanner; mixed code/comment lines count as code. It is not a measure of business logic or complexity.

The scan covers project-owned C# under Assets. It excludes Library, Temp, package source, generated project files, and external SDK source. The primary requested roots, Assets/Scripts plus Assets/Editor, contain **963 files / 206,956 lines**. The broader total includes six Assets/Notes files and one definition script outside those roots.

| Metric | Result |
| --- | ---: |
| C# files | 970 |
| Physical C# LOC | 208,025 |
| Blank lines | 38,435 |
| Comment-only lines | 10,627 |
| Code/directive lines | 158,963 |
| Mean / median physical lines per file | 214.5 / 91.5 |
| 90th / 95th percentile file size | 536 / 826 |
| Files strictly over 500 / 1,000 / 2,000 / 3,000 lines | 107 / 35 / 6 / 1 |
| Editor-folder C# | 25 files / 13,179 lines |
| Notes C# | 6 files / 1,044 lines |
| Remaining C# outside Editor folders and Notes | 939 files / 193,802 lines |
| Block-bodied methods detected, approximately | 7,326 |
| Methods strictly over 100 / 200 physical lines, approximately | 130 / 15 |
| Update / FixedUpdate / LateUpdate declarations | 100 / 47 / 41 |
| StartCoroutine call sites | 17 |
| InvokeRepeating call sites | 0 |
| WaitForSeconds / WaitForSecondsRealtime construction sites | 6 |
| Scene-wide object-search call sites | 405 |
| GetComponentsInChildren / GetComponentInParent call sites | 196 / 452 |
| Debug.Log-family call sites, excluding comments | 1,113 |
| Shader files / physical LOC | 34 / 5,118 |
| Combined C# and shader physical LOC | 213,143 |

Method counts are approximate: the scan does not fully parse constructors, accessors, expression bodies, or conditional compilation. Call-site counts do not indicate execution frequency. For example, 405 searches include setup, fallback, save capture, editor tools, and deprecated code; they are not 405 searches every frame. Likewise, `+=` counts were not treated as event counts because arithmetic uses the same operator.

### LOC by system — complete, mutually exclusive accounting

This is **folder ownership**, not a perfect feature-cost attribution. Vendor UI belongs to MiniGames, vendor transactions also appear under Inventory and Boats, and authority checks are distributed across gameplay folders. The tiny Multiplayer folder therefore does not mean multiplayer concerns comprise only 178 lines.

| Source system/folder | C# files | Physical LOC | Code/directive LOC | Share of C# LOC |
| --- | ---: | ---: | ---: | ---: |
| `Assets/Scripts/WorldMap` | 205 | 33,538 | 26,917 | 16.1% |
| `Assets/Scripts/Modules` | 85 | 29,090 | 22,337 | 14.0% |
| `Assets/Scripts/Inventory` | 110 | 27,509 | 21,563 | 13.2% |
| `Assets/Scripts/MiniGames` | 48 | 24,560 | 19,731 | 11.8% |
| `Assets/Scripts/Boats` | 61 | 15,660 | 11,567 | 7.5% |
| `Assets/Scripts/Editor` | 24 | 13,153 | 8,628 | 6.3% |
| `Assets/Scripts/Controller` | 44 | 9,193 | 6,861 | 4.4% |
| `Assets/Scripts/Environment` | 42 | 7,111 | 5,740 | 3.4% |
| `Assets/Scripts/Parts` | 30 | 6,686 | 5,209 | 3.2% |
| `Assets/Scripts/GameState` | 25 | 5,257 | 4,090 | 2.5% |
| `Assets/Scripts/Physics` | 19 | 4,922 | 3,447 | 2.4% |
| `Assets/Scripts/MoneyChest` | 13 | 4,762 | 3,676 | 2.3% |
| `Assets/Scripts/Agents` | 56 | 4,593 | 3,597 | 2.2% |
| `Assets/Scripts/Global` | 22 | 3,415 | 2,537 | 1.6% |
| `Assets/Scripts/Survival` | 46 | 3,254 | 2,235 | 1.6% |
| `Assets/Scripts/Saves` | 11 | 3,177 | 2,544 | 1.5% |
| `Assets/Scripts/Travel` | 28 | 2,100 | 1,690 | 1.0% |
| `Assets/Scripts/Debug` | 13 | 2,008 | 1,378 | 1.0% |
| `Assets/Scripts/UI` | 16 | 1,494 | 1,138 | 0.7% |
| `Assets/Scripts/Water` | 5 | 1,285 | 968 | 0.6% |
| `Assets/Scripts/GameMessage` | 6 | 1,176 | 941 | 0.6% |
| `Assets/Notes` | 6 | 1,044 | 10 | 0.5% |
| `Assets/Scripts/Camera` | 6 | 806 | 662 | 0.4% |
| `Assets/Scripts/Menu` | 6 | 722 | 569 | 0.3% |
| `Assets/Scripts/Interfaces` | 27 | 493 | 266 | 0.2% |
| `Assets/Scripts` | 2 | 383 | 212 | 0.2% |
| `Assets/Scripts/LOD` | 5 | 256 | 199 | 0.1% |
| `Assets/Scripts/Multiplayer` | 4 | 178 | 136 | 0.1% |
| `Assets/Scripts/Rendering` | 1 | 62 | 0 | 0.0% |
| `Assets/Scripts/Equipment` | 1 | 53 | 42 | 0.0% |
| `Assets/Scripts/Materials` | 1 | 34 | 28 | 0.0% |
| `Assets/Editor` | 1 | 26 | 25 | 0.0% |
| `Assets/Defs/Weather Events` | 1 | 25 | 20 | 0.0% |
| **Total** | **970** | **208,025** | **158,963** | **100.0%** |

### Detailed subsystem LOC

The following table replaces, rather than adds to, the preceding totals. Files directly under a parent folder remain attributed to that parent; deeper descendants are grouped at the next folder level.

| Subsystem/folder | C# files | Physical LOC | Code/directive LOC | Share of C# LOC |
| --- | ---: | ---: | ---: | ---: |
| `Assets/Scripts/MiniGames/Cartridges` | 23 | 18,813 | 15,166 | 9.0% |
| `Assets/Scripts/Modules/Tethers` | 42 | 15,869 | 12,145 | 7.6% |
| `Assets/Scripts/Editor/BoatBuilder` | 18 | 9,921 | 6,072 | 4.8% |
| `Assets/Scripts/WorldMap/Celestial` | 44 | 9,250 | 7,633 | 4.4% |
| `Assets/Scripts/Inventory/Item` | 45 | 8,702 | 6,766 | 4.2% |
| `Assets/Scripts/Inventory` | 22 | 7,657 | 6,052 | 3.7% |
| `Assets/Scripts/Boats/Simulation` | 19 | 6,840 | 5,335 | 3.3% |
| `Assets/Scripts/Controller/Player` | 28 | 6,716 | 4,981 | 3.2% |
| `Assets/Scripts/WorldMap/Data` | 17 | 5,056 | 4,026 | 2.4% |
| `Assets/Scripts/MiniGames/Runners` | 14 | 4,975 | 3,989 | 2.4% |
| `Assets/Scripts/Physics` | 19 | 4,922 | 3,447 | 2.4% |
| `Assets/Scripts/Boats` | 18 | 4,845 | 3,309 | 2.3% |
| `Assets/Scripts/MoneyChest` | 13 | 4,762 | 3,676 | 2.3% |
| `Assets/Scripts/Inventory/Flotation` | 9 | 4,037 | 3,080 | 1.9% |
| `Assets/Scripts/Modules/Hardpoints` | 7 | 3,831 | 2,965 | 1.8% |
| `Assets/Scripts/Environment/GroundGeneration` | 24 | 3,624 | 3,071 | 1.7% |
| `Assets/Scripts/WorldMap/Topography` | 14 | 3,514 | 2,762 | 1.7% |
| `Assets/Scripts/Boats/Compartments` | 20 | 3,488 | 2,534 | 1.7% |
| `Assets/Scripts/Modules/Winch` | 6 | 3,383 | 2,638 | 1.6% |
| `Assets/Scripts/Editor` | 6 | 3,232 | 2,556 | 1.6% |
| `Assets/Scripts/Saves` | 11 | 3,177 | 2,544 | 1.5% |
| `Assets/Scripts/Inventory/Cargo` | 10 | 3,127 | 2,389 | 1.5% |
| `Assets/Scripts/WorldMap/Player` | 28 | 2,688 | 1,968 | 1.3% |
| `Assets/Scripts/WorldMap/UI` | 14 | 2,619 | 1,927 | 1.3% |
| `Assets/Scripts/Controller/Interactions` | 16 | 2,477 | 1,880 | 1.2% |
| `Assets/Scripts/Inventory/Tools` | 11 | 2,466 | 1,884 | 1.2% |
| `Assets/Scripts/WorldMap/Runtime` | 20 | 2,375 | 2,007 | 1.1% |
| `Assets/Scripts/Modules` | 12 | 2,183 | 1,655 | 1.0% |
| `Assets/Scripts/WorldMap/Fog` | 21 | 2,053 | 1,789 | 1.0% |
| `Assets/Scripts/Debug` | 13 | 2,008 | 1,378 | 1.0% |
| `Assets/Scripts/GameState/Scene` | 10 | 1,985 | 1,571 | 1.0% |
| `Assets/Scripts/Parts` | 6 | 1,920 | 1,491 | 0.9% |
| `Assets/Scripts/Agents/Definitions` | 29 | 1,904 | 1,484 | 0.9% |
| `Assets/Scripts/Environment/Resources` | 10 | 1,903 | 1,452 | 0.9% |
| `Assets/Scripts/Environment` | 8 | 1,584 | 1,217 | 0.8% |
| `Assets/Scripts/Travel/Travel` | 17 | 1,573 | 1,351 | 0.8% |
| `Assets/Scripts/Inventory/BoatEquipment` | 13 | 1,520 | 1,392 | 0.7% |
| `Assets/Scripts/Modules/State` | 4 | 1,454 | 1,149 | 0.7% |
| `Assets/Scripts/Agents/Spawning` | 12 | 1,437 | 1,125 | 0.7% |
| `Assets/Scripts/WorldMap/MapTable` | 6 | 1,427 | 1,198 | 0.7% |
| `Assets/Scripts/Water` | 5 | 1,285 | 968 | 0.6% |
| `Assets/Scripts/Global/Rendering` | 9 | 1,264 | 960 | 0.6% |
| `Assets/Scripts/GameMessage` | 6 | 1,176 | 941 | 0.6% |
| `Assets/Scripts/Parts/Segments` | 6 | 1,159 | 920 | 0.6% |
| `Assets/Scripts/GameState` | 4 | 1,149 | 908 | 0.6% |
| `Assets/Scripts/Parts/Hatch` | 6 | 1,122 | 851 | 0.5% |
| `Assets/Scripts/GameState/Boat` | 5 | 1,117 | 826 | 0.5% |
| `Assets/Scripts/Modules/Storage` | 6 | 1,059 | 810 | 0.5% |
| `Assets/Notes` | 6 | 1,044 | 10 | 0.5% |
| `Assets/Scripts/GameState/Inventory` | 6 | 1,006 | 785 | 0.5% |
| `Assets/Scripts/WorldMap/POI` | 6 | 1,003 | 793 | 0.5% |
| `Assets/Scripts/Agents/Runtime` | 7 | 927 | 710 | 0.4% |
| `Assets/Scripts/Parts/Door` | 4 | 891 | 677 | 0.4% |
| `Assets/Scripts/UI` | 10 | 887 | 694 | 0.4% |
| `Assets/Scripts/Parts/Walls` | 5 | 853 | 689 | 0.4% |
| `Assets/Scripts/WorldMap/Model` | 8 | 846 | 732 | 0.4% |
| `Assets/Scripts/Survival/Afflictions` | 12 | 812 | 582 | 0.4% |
| `Assets/Scripts/Camera` | 6 | 806 | 662 | 0.4% |
| `Assets/Scripts/Survival/Vitals` | 15 | 806 | 376 | 0.4% |
| `Assets/Scripts/WorldMap/Input` | 3 | 788 | 591 | 0.4% |
| `Assets/Scripts/Global/Weather` | 5 | 784 | 553 | 0.4% |
| `Assets/Scripts/Parts/Stairs` | 3 | 741 | 581 | 0.4% |
| `Assets/Scripts/Menu` | 6 | 722 | 569 | 0.3% |
| `Assets/Scripts/Modules/Helm` | 4 | 677 | 493 | 0.3% |
| `Assets/Scripts/Survival/Air` | 5 | 674 | 503 | 0.3% |
| `Assets/Scripts/Modules/Turret` | 3 | 616 | 466 | 0.3% |
| `Assets/Scripts/Global/Physics` | 3 | 606 | 433 | 0.3% |
| `Assets/Scripts/Survival/Buffs` | 8 | 593 | 483 | 0.3% |
| `Assets/Scripts/WorldMap/Events` | 6 | 582 | 452 | 0.3% |
| `Assets/Scripts/WorldMap/Biomes` | 6 | 575 | 452 | 0.3% |
| `Assets/Scripts/MiniGames/Overlay` | 5 | 563 | 434 | 0.3% |
| `Assets/Scripts/Interfaces` | 27 | 493 | 266 | 0.2% |
| `Assets/Scripts/Global/Time` | 4 | 488 | 389 | 0.2% |
| `Assets/Scripts/Boats/Vendor` | 4 | 487 | 389 | 0.2% |
| `Assets/Scripts` | 2 | 383 | 212 | 0.2% |
| `Assets/Scripts/Survival/Death` | 6 | 369 | 291 | 0.2% |
| `Assets/Scripts/Travel/StarMap` | 4 | 358 | 226 | 0.2% |
| `Assets/Scripts/UI/DynamicText` | 4 | 349 | 250 | 0.2% |
| `Assets/Scripts/WorldMap/Navigation` | 5 | 330 | 251 | 0.2% |
| `Assets/Scripts/Global/ServiceRoot` | 1 | 273 | 202 | 0.1% |
| `Assets/Scripts/WorldMap/Simulation` | 1 | 272 | 205 | 0.1% |
| `Assets/Scripts/UI/ContextHints` | 2 | 258 | 194 | 0.1% |
| `Assets/Scripts/LOD` | 5 | 256 | 199 | 0.1% |
| `Assets/Scripts/MiniGames/Core` | 6 | 209 | 142 | 0.1% |
| `Assets/Scripts/Multiplayer` | 4 | 178 | 136 | 0.1% |
| `Assets/Scripts/Travel/Route` | 7 | 169 | 113 | 0.1% |
| `Assets/Scripts/Agents/Services` | 4 | 134 | 113 | 0.1% |
| `Assets/Scripts/WorldMap/Resources` | 5 | 118 | 95 | 0.1% |
| `Assets/Scripts/Agents/Movement` | 3 | 117 | 100 | 0.1% |
| `Assets/Scripts/Agents/Core` | 1 | 74 | 65 | 0.0% |
| `Assets/Scripts/Rendering` | 1 | 62 | 0 | 0.0% |
| `Assets/Scripts/Equipment` | 1 | 53 | 42 | 0.0% |
| `Assets/Scripts/WorldMap/Messaging` | 1 | 42 | 36 | 0.0% |
| `Assets/Scripts/Materials` | 1 | 34 | 28 | 0.0% |
| `Assets/Editor` | 1 | 26 | 25 | 0.0% |
| `Assets/Defs/Weather Events` | 1 | 25 | 20 | 0.0% |
| `Assets/Scripts/Modules/Interfaces` | 1 | 18 | 16 | 0.0% |

### Dependency indicators

Lexical source-file dependencies were estimated from references to top-level declared types. Multiple types in one file are grouped together. DTO-heavy files therefore have inflated apparent fan-in; partial-class siblings and string references can also distort these numbers.

| High fan-out file | Other source files referenced, approximately | Interpretation |
| --- | ---: | --- |
| [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>) | 55 | Review boundaries, not just size |
| [HardpointInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Hardpoints/HardpointInteractable.cs:1>) | 40 | Review boundaries, not just size |
| [SurfaceSurveyAuthority](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Fog/SurfaceSurveyAuthority.cs:1>) | 36 | Small orchestrator with a wide contract surface |
| [CelestialObservationOverlayRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/CelestialObservationOverlayRunner.cs:1>) | 34 | Review boundaries, not just size |
| [BoatLooseItemPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/BoatLooseItemPersistence.cs:1>) | 33 | Review boundaries, not just size |
| [BoatBuilderWindow](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Editor/BoatBuilder/BoatBuilderWindow.cs:1>) | 33 | Review boundaries, not just size |
| [WinchModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:1>) | 29 | Review boundaries, not just size |
| [WorldMapSaveBuilder](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapSaveBuilder.cs:1>) | 28 | Review boundaries, not just size |

| High fan-in file | Referring source files, approximately | Interpretation |
| --- | ---: | --- |
| [Boat](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Boat.cs:1>) | 140 | Central contract/model; changing it has wide impact |
| [ItemInstance](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemInstance.cs:1>) | 100 | Central contract/model; changing it has wide impact |
| [GameState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/GameState.cs:1>) | 77 | Includes multiple declared types; not one class's exact fan-in |
| [ItemDefinition](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemDefinition.cs:1>) | 70 | Includes multiple declared types; not one class's exact fan-in |
| [GameplayAuthority](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Multiplayer/GameplayAuthority.cs:1>) | 69 | Central contract/model; changing it has wide impact |
| [IInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Interfaces/IInteractable.cs:1>) | 60 | Includes multiple declared types; not one class's exact fan-in |
| [WorldTopology](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldTopology.cs:1>) | 60 | Includes multiple declared types; not one class's exact fan-in |
| [InventorySlot](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/InventorySlot.cs:1>) | 58 | Includes multiple declared types; not one class's exact fan-in |

High fan-in is often desirable for Boat, ItemInstance, topology, and interaction contracts. It is a reason to stabilize APIs and test compatibility, not split them into arbitrary adapters.

## C. Architecture Map

| Area | Current owners and flow | Assessment |
| --- | --- | --- |
| Application/session/persistence | [MainMenuController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Menu/MainMenuController.cs:1>) initiates session work; [GameState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/GameState.cs:1>) stores session/save models; [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:1>) captures and serializes; [SceneTransitionController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/SceneTransitionController.cs:1>) orchestrates scene context and boat/player capture. | Keep model/service split; narrow orchestration dependencies. |
| Geography and world state | [WorldMapGraphGenerator](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapGraphGenerator.cs:1>), [WorldMapTopographyGenerator](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyGenerator.cs:1>), biome/POI generation, [WorldMapRuntimeBinder](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeBinder.cs:1>), and [WorldMapRuntimeRegistry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeRegistry.cs:1>) connect generated data to runtime node state. | Preserve until the planned WorldGen V2 contracts govern changes. |
| Navigation, surveying, knowledge | [WorldNavigationService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs:1>), [WorldTopology](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldTopology.cs:1>), [SurfaceSurveyAuthority](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Fog/SurfaceSurveyAuthority.cs:1>), celestial knowledge/chart authorities, and chart carriers. | Distinct truth, observations, and knowledge are justified. |
| Map and celestial UI | [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>), [CelestialObservationCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.cs:1>), [CelestialChartTableCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/CelestialChartTableCartridge.cs:1>), runners, render caches. | Presentation is functional but some cartridges have excessive change radius. |
| Boats and physics | [Boat](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Boat.cs:1>), [BoatPilotingSimulation](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatPilotingSimulation.cs:1>), [ForceSystem](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/ForceSystem.cs:1>), [BuoyancyPolygonForce](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyPolygonForce.cs:1>), compartment network and physical-frame helpers. | Legitimate complexity; preserve recent reference-frame fixes. |
| Modules and tethers | Hardpoints → installed modules; [WinchModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:1>) and TetherDeploymentModule partials manage deployment, payloads, links, and occupancy. | Keep separate systems; extract narrowly at stable boundaries. |
| Inventory and cargo | [ItemDefinition](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemDefinition.cs:1>) → [ItemInstance](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemInstance.cs:1>) → slots/containers; acquisition, drag, world-drop, securing, loose-item persistence and [ThrownCargoPhysics](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ThrownCargoPhysics.cs:1>) bridge physical objects. | Strong instance model; transaction/actor ownership needs consolidation. |
| Economy and treasury | [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:1>), vendor purchase services, [MoneyService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MoneyChest/MoneyService.cs:1>), [MoneyChestTreasuryService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MoneyChest/MoneyChestTreasuryService.cs:1>). | Highest correctness priority: atomic operations and canonical finite stock. |
| Agents and towns | Agent definitions, brains, movement and interaction service dispatch; NodeSettlementScene/leader service connects generated town plots and civic systems. | Reuse existing agent framework; no replacement needed. |
| Survival | Vitals, afflictions, air supply and exertion attach to actor state and force/presentation adapters. | Many small contracts are justified; inactive predecessors can be reviewed separately. |
| Environment | Time/weather services drive renderer adapters, waves, depth lighting, sky and backgrounds. | Service/presentation split is good; event and camera-global policies need attention. |
| Authoring and diagnostics | BoatBuilder, repair/geometry tools, layer reports, diagnostic overlays and deterministic verifiers. | A meaningful part of the game's productivity infrastructure. |

### Dependency direction and circularity

The principal seam is **UI/input → request/effect → domain state → presentation**, but it is not consistently enforced. ModuleCartridge looks up an actor globally; the item-vendor runner reads stock back out of its cartridge; save capture reaches through scene orchestration. These are more consequential than a syntactic cycle count. See [ModuleCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ModuleCartridge.cs:32>), [ItemVendorOverlayRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/ItemVendorOverlayRunner.cs:235>), and [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:422>).

A concrete coordination cycle is purchase UI → emitted purchase effect → runner/service → cartridge stock mutation. The effect runner must consult the view's gameplay state before applying the operation. Breaking that ownership cycle does not require replacing the cartridge framework.

Lexical file cycles are not reported as proven architectural cycles: DTOs sharing a file and partial classes can manufacture them. No runtime recursion bug is established by this audit. For future changes, domain transaction services should not require live UI objects, and persistence should receive explicit runtime participants rather than identify the requesting player through a scene-wide search.

### Persistence and resource lifetimes

The top-level save schema currently declares **CurrentVersion = 1 / MinimumSupportedVersion = 1** in [SaveSchema](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveSchema.cs:1>). This is an envelope compatibility declaration, not evidence that every nested payload can survive arbitrary model changes. [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:450>) captures world, boat, treasury, local player and keyed player records; the local mirrors are intentional compatibility paths. Future changes to generator identity, item payloads or actor records need explicit compatibility handling rather than relying on a JSON field remaining optional.

Restore is a dependency sequence: [BoatSpawner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Boat/BoatSpawner.cs:62>) reconstructs the boat and then its modules, tethers, compartments, access and loose objects; [PlayerSceneContextRestorer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/PlayerSceneContextRestorer.cs:41>) waits for the relevant boat/terrain before restoring player context. Reordering these jobs can invalidate references even if each individual snapshot deserializes successfully. Preserve that ordering until an explicit readiness contract replaces it.

Static state is not uniformly a problem. [AgentRegistry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Runtime/AgentRegistry.cs:10>) supports owner-aware unregister and Clear; [CameraManager](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Camera/CameraManager.cs:65>) has session-registration reset machinery. [WorldMapRuntimeCache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapRuntimeCache.cs:198>) destroys owned textures, and the board-local [CelestialChartRotatedTextureCache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialChartRotatedTextureCache.cs:14>) distinguishes owned rotated textures from borrowed source textures. By contrast, vendor stock in a static UI-state cache has no comparable domain-level save/reset contract in that implementation. Review lifetime per owner instead of clearing every static field or cache indiscriminately.

### Folder, namespace, and abstraction policy

Folders communicate feature ownership better than the currently mixed namespace conventions. Namespace normalization is low priority and can break serialized managed-type names or tooling. There are no project-owned asmdef/asmref files under Assets in this snapshot; adding assembly boundaries is a future compile/build discussion, not a prerequisite to cleanup.

Interfaces such as IInteractable, IForceProvider, INodeStateLookup and environmental services carry useful runtime or testing boundaries. A single implementation is not sufficient evidence that an interface is unnecessary. The missing abstraction is primarily a **shared command/transaction boundary**, not more generic service locators or base-class hierarchies.

## D. Major Findings

### Critical

**None confirmed.** This is not a security audit or a proof that all gameplay is correct.

### High

**H1 — Trade application can partially commit.** In [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:176>), item and offer mutations occur line by line. Later inventory/offer failures can return after earlier changes. Final money application occurs at [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:213>); its failure message explicitly acknowledges that items/offers were already applied. Prevalidation does not guarantee atomicity if the apply phase can fail. Make one owner validate/reserve/apply or restore the complete transaction. Verify with injected failure on the second item and on the money step, not just successful trades. Confidence: high, static path confirmed.

**H2 — Finite vendor stock is view-owned.** [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:21>) stores stock in static session UI state, keyed by offer index. [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:838>) initializes it from authored offers, and [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:703>) decrements it following purchase success. [ItemVendorOverlayRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/ItemVendorOverlayRunner.cs:235>) checks that stock before calling the purchase service, so normal local UI buying has a stock check. However, [ItemVendorPurchaseService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/Vendor/ItemVendorPurchaseService.cs:59>) does not own the corresponding runtime decrement. Save/restock/reset semantics need an explicit domain owner and stable offer identity. Confidence: high; the intended restock policy is not established.

**H3 — Authority is incomplete across shared simulation.** [GameplayAuthority](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Multiplayer/GameplayAuthority.cs:5>) defaults to authoritative operation and offers a debug role setter. It does not validate authenticated requesters or replicate state. [EngineModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/EngineModule.cs:89>), [GeneratorModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/GeneratorModule.cs:53>), and [PumpModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/PumpModule.cs:59>) perform shared fuel/power/water work without that guard in their update paths. [ForceSystem](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/ForceSystem.cs:94>) has an opt-in authority gate; that is not proof all physical bodies opt in. This is a multiplayer-readiness finding, not a claim that local single-player is broken.

**H4 — Remaining global actor resolution can route an action to the wrong actor.** [ModuleCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ModuleCartridge.cs:32>) selects the first PlayerInventory and PlayerEquipment instead of receiving the requester. [ItemVendorPurchaseService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/Vendor/ItemVendorPurchaseService.cs:211>) first tries the actor context, then falls back to a global acquisition resolver. [PlayerLoadoutPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/PlayerLoadoutPersistence.cs:21>) deliberately supplies a local default persistence key. Preserve single-player defaults, but resolve/set the actual actor identity before opening UI or restoring data in multi-actor sessions. Confidence: high; two-client consequences were not exercised.

### Medium

**M1 — Persistent wind event retains a rain renderer callback.** [RainShaderRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/RainShaderRenderer.cs:54>) subscribes to WindManager.OnWindTransitionStarted. [RainShaderRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/RainShaderRenderer.cs:69>) removes other callbacks but not that one. A longer-lived publisher can retain a destroyed scene renderer. The same Awake path logs an absent material and continues to [RainShaderRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/RainShaderRenderer.cs:66>). Follow up with lifecycle cleanup and explicit required-reference failure behavior.

**M2 — Hot flow work constructs temporary state and disabled diagnostic strings.** [CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:15>) creates a list, per-iteration dictionary/hash set, and a delegate around the surface cache. [CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:80>) builds labels and interpolated Log arguments although DEBUG_FLOW is false. Arguments are constructed before the logger checks its flag. Profile allocations first; preserve solver order and physics outcomes while reusing scratch storage or guarding string construction.

**M3 — Save and restore depend on scene discovery and staged readiness.** [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:422>) refreshes runtime state through SceneTransitionController; the missing-controller path warns and relies on cached state. [PlayerSceneContextRestorer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/PlayerSceneContextRestorer.cs:41>) uses a retrying coroutine to wait for boat/terrain readiness. These are reasonable local safeguards, but future multi-actor/bootstrap work needs an explicit participant/readiness contract. Do not replace working restore order with generic initialization magic.

**M4 — Duplicate catalog IDs can silently replace meaning.** [ItemDefinitionCatalog](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/ItemDefinitionCatalog.cs:16>) builds a dictionary with last-write-wins entries and no duplicate validation; [WorldMapRuntimeRegistry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Runtime/WorldMapRuntimeRegistry.cs:15>) likewise overwrites entries. [AgentRegistry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Runtime/AgentRegistry.cs:10>) warns but still replaces a duplicate agent stable ID. This is not evidence that current authored assets contain duplicates. Add authored-data validation before changing lookup behavior.

**M5 — Tool compatibility code uses reflection where concrete contracts now exist.** [UnderwaterResourceInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/Resources/UnderwaterResourceInteractable.cs:502>) probes Matches/HasToolCapability via reflection, and [ToolUseChargeUtility](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Tools/ToolUseChargeUtility.cs:255>) searches capability labels by string-named members. These paths allocate arguments and hide compile-time failures. Unify only verified shared contracts; retain intentional asset compatibility until it has been migrated.

**M6 — Large UI and interaction coordinators have broad responsibilities.** WorldMapCartridge has 3,360 lines and the largest estimated fan-out. InventoryDragController has 2,652 lines, WinchModule 2,390. Their risk is independent reasons to change, rather than a line-count rule. Section K proposes bounded separations.

**M7 — Legacy navigation remains intertwined with current presentation.** [NodeTravelController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/NodeTravelController.cs:141>) uses the newer departure flow, while [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:3159>) still builds route-oriented access information. [BoatPilotingRouteState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatPilotingRouteState.cs:1>) is still constructed by simulation; it is not an unused file. Retire concepts through their consumers and save compatibility, rather than deleting all “route” classes.

### Low

- Exact UI and delivery duplication exists, particularly vendor cartridges, door/hatch close logic, and lost-chest spawners. Prefer small shared helpers where behavior truly matches; section E distinguishes intentional parallels.
- Catalog caches need a deliberate authoring invalidation policy. [ItemDefinitionCatalog](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/ItemDefinitionCatalog.cs:16>) builds once; changing its authored list after the first lookup does not rebuild that dictionary in this method.
- Some optional prototype/adaptor classes have no discovered callers or asset references. Their review cost is small; no broad deletion is warranted.
- Editor-only code appears in 25 files outside Editor folders. Observed examples use UNITY_EDITOR guards, including [CompartmentLinkAuthoringEditor](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentLinkAuthoringEditor.cs:1>) and [CelestialFixedSkyProfile](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialFixedSkyProfile.cs:37>). This count is **not** 25 build leaks.

### Informational

- Historical map overlays remain in archived scenes and inactive hierarchy branches of current scenes.
- Large editor tools and snapshot/configuration copies legitimately add LOC.
- Physics collision layers, visual sorting layers, and ownership/authority roles solve different problems; a universal replacement would introduce unnecessary coupling.

### Qualitative health scorecard

| Category | Assessment | Basis |
| --- | --- | --- |
| Scope-to-size fit | Healthy | Most LOC corresponds to substantial active systems and tools. |
| Cohesion | Manageable debt | Several broad UI/interaction coordinators. |
| Dependency direction | Manageable debt | UI stock and global actor fallbacks cross intended boundaries. |
| Duplication | Localized debt | Exact repetitions and overlapping concepts, not pervasive copied subsystems. |
| Dead/legacy code | Small, identifiable minority | Commented predecessors and two deprecated islands dominate candidates. |
| Scene/player ownership | Improving, incomplete | Actor-context seams exist; first-object fallbacks remain. |
| UI/gameplay separation | Needs targeted work | Transaction and stock ownership findings. |
| Persistence | Functional, migration-sensitive | Keyed player records coexist with intentional local/legacy mirrors. |
| Authority/network readiness | Incomplete | Role gates are not transport/authentication; shared writers remain. |
| Lifecycle/event hygiene | Generally structured; one confirmed omission | Rain wind-transition callback; other reviewed pairs clean up. |
| Static state/cache ownership | Mixed | Presentation caches have cleanup; UI stock requires session policy. |
| Serialization/identity | Sensible base, validation debt | Stable identities exist; duplicate/default handling deserves tests. |
| ScriptableObjects/interfaces | Healthy for scope | Definitions and narrow capabilities are generally purposeful. |
| Runtime efficiency | Plausible hotspots; unmeasured | Flow allocations, hover queries, demand scans, lighting sort. |
| Authoring/debug tooling | Valuable | Extensive boat and layer diagnostics support current workflow. |
| Testability | Mixed | Pure state/services are testable; scene-discovered coordinators are harder. |

## E. Duplicate Concepts

The exact-block scan used normalized, comment-stripped blocks of at least 12 substantial lines and 300 characters. It found approximately **1,930 unique participating physical lines across 58 files**. Both sides of a duplicate are counted. This is neither an exhaustive semantic clone detector nor an estimate of deletable lines.

| Parallel implementations | Evidence and recommendation |
| --- | --- |
| Item and boat vendor cartridges | [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:90>) and [BoatVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/BoatVendorCartridge.cs:99>) repeat session/rect/list/purchase UI mechanics. Share presentation helpers after gameplay stock and transaction ownership are settled. Do not make one all-purpose vendor class. |
| Vendor item delivery/acquisition | [ItemVendorPurchaseService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/Vendor/ItemVendorPurchaseService.cs:178>) and [BoatVendorModuleTransactionService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Vendor/BoatVendorModuleTransactionService.cs:308>) repeat delivery checks and actor resolution. One narrow delivery/reservation seam could improve refund safety. |
| Door and hatch closing | [DoorRuntime](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Parts/Door/DoorRuntime.cs:25>) and [HatchRuntime](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Parts/Hatch/HatchRuntime.cs:28>) repeat obstruction/feedback/cache mechanics. Their physical roles differ; share close-obstruction helpers rather than merge behaviors. |
| Lost money-chest spawners | [MoneyChestLostChestSpawner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MoneyChest/MoneyChestLostChestSpawner.cs:35>) and [MoneyChestNodeLostChestSpawner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MoneyChest/MoneyChestNodeLostChestSpawner.cs:28>) share state/spawn mechanics; boat/node placement remains distinct. |
| Tool charges/resource harvest | [ToolUseChargeUtility](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Tools/ToolUseChargeUtility.cs:166>) and [UnderwaterResourceInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/Resources/UnderwaterResourceInteractable.cs:617>) duplicate capability/charge mechanics. Prefer the existing typed capability contract once compatibility is checked. |
| Current vs deprecated map colors/overlays | [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:3069>) and [WorldMapHeatmapController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/UI/WorldMapHeatmapController.cs:161>) share heatmap logic. Retire the deprecated family after asset migration rather than maintain two UIs. |
| Current vs shelved WorldGen preview | [WorldMapTopographyDebugTextureBuilder](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldMapTopographyDebugTextureBuilder.cs:179>) and [WorldGenerationPreviewTextureBuilder](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Topography/WorldGenerationPreviewTextureBuilder.cs:52>) overlap preview colors. Do not revive the shelved pipeline to deduplicate helpers. |
| Frozen celestial configuration vs authored settings | [CelestialGenerationConfig](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialGenerationConfig.cs:106>) and [CelestialGenerationSettings](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialGenerationSettings.cs:89>) intentionally distinguish generation snapshots from authoring assets. Keep the distinction; verify defaults in migration tests. |
| Ground mesh vs SpriteShape binder | [GroundFillMeshRenderer2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/GroundGeneration/GroundFillMeshRenderer2D.cs:45>) and [GroundSpriteShapeBinder2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/GroundGeneration/GroundSpriteShapeBinder2D.cs:37>) have similar provider wiring but different rendering substrates. Retain if either authoring path is supported. |
| True position, estimated position, revealed knowledge, charts | [WorldNavigationService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs:1>) and chart/survey authorities represent distinct gameplay information. These are not duplicate state merely because their coordinates overlap. |
| Player-local legacy fields vs keyed persistence | [PlayerLoadoutPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/PlayerLoadoutPersistence.cs:21>) and [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:450>) preserve single-player compatibility while supporting keyed records. Remove mirrors only through a save migration. |

No duplicate-concept finding justifies combining all physics, all maps, all NPC services, or all inventory actions into one framework.

## F. God Objects / Large Classes

Size is a triage indicator. “God object” requires excessive responsibility or ownership, not merely exceeding 1,000 lines.

| File | Physical LOC | Assessment |
| --- | ---: | --- |
| [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>) | 3,360 | Strong boundary-extraction candidate: input/view state, map modes, heatmaps, selection, route information and overlays. |
| [InventoryDragController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/InventoryDragController.cs:1>) | 2,652 | Broad operation coordinator; preserve rollback/reservation behavior while moving domain operations out of pointer/UI flow. |
| [WinchModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:1>) | 2,390 | Complex active tether/payload system. Separate stable mechanics incrementally, not a fresh winch rewrite. |
| [CelestialChartTableCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/CelestialChartTableCartridge.cs:1>) | 2,055 | Mostly complex authored interaction/presentation; first separate geometry and draw concerns. |
| [CelestialObservationCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.cs:1>) | 2,054 | Presentation/input breadth; navigation semantics should remain in existing authorities. |
| [WorldMapGraphGenerator](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapGraphGenerator.cs:1>) | 1,881 | Large algorithmic owner; upcoming WorldGen contracts are the right migration vehicle. |
| [DivingBellOccupancy](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Tethers/DivingBell/DivingBellOccupancy.cs:1>) | 1,832 | Active specialized feature complexity; not a reason to reopen the completed bell pass. |
| [WinchCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WinchCartridge.cs:1>) | 1,786 | Large draw/interaction surface; extract reusable presentation after module ownership is stable. |
| [BuoyancyPolygonForce](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyPolygonForce.cs:1>) | 1,746 | Algorithm plus substantial debug gizmos; size overstates production simulation complexity. |
| [MoneyChestTreasuryService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MoneyChest/MoneyChestTreasuryService.cs:1>) | 1,586 | Legitimately central money lifecycle, but transaction entry points need explicit invariants. |
| [PlayerLadderClimber](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Player/Ladders/PlayerLadderClimber.cs:1>) | 1,538 | Recent moving-frame hardening; preserve behavior and isolate helpers only with regression evidence. |
| [Interactor2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Interactions/Interactor2D.cs:1>) | 1,438 | Central interaction policy; query allocation and requester boundaries warrant careful extraction. |
| [ModuleCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ModuleCartridge.cs:1>) | 1,347 | UI plus module commands and actor lookup; a command/context seam is more valuable than reducing lines. |
| [BoatLooseItemPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/BoatLooseItemPersistence.cs:1>) | 1,333 | Serialization bridge spans several carriers; split codecs/resolution only while maintaining restore order. |
| [SceneTransitionController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/SceneTransitionController.cs:1>) | 1,007 | High consequence orchestration: capture, restore, scene change, actor selection and legacy paths. |
| [Boat](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Boat.cs:1>) | 1,005 | High fan-in entity root, simulation and diagnostics. Stabilize API; do not split the entity arbitrarily. |

A 2,221-line BoatBuilderSceneTools_Backup.disabled.cs file accounts for the sixth raw file over 2,000 lines; it is entirely commented, not another live god object.

### Long-method examples

| Method | Approximate physical span | Why it matters |
| --- | ---: | --- |
| [ApplyForces](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyPolygonForce.cs:98>) | 427 | Algorithmic stages may be separable; verify force equivalence. |
| [DrawScrollableContent](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WinchCartridge.cs:122>) | 423 | Presentation sections have independent reasons to change. |
| [ApplyForces](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Player/CharacterMoveForce.cs:118>) | 306 | High branching complexity; moving-frame invariants matter more than cosmetic extraction. |
| [DrawDebugPanel](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/PilotingHudRenderer.cs:128>) | 295 | Debug-only complexity; low gameplay priority. |
| [OnDrawGizmosSelected](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyPolygonForce.cs:1450>) | 294 | Editor visualization; exclude from runtime cost conclusions. |
| [DrawHeatmapContent](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:698>) | 286 | Good candidate for an existing-map presenter boundary. |
| [FixedUpdate](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:182>) | 238 | Physics/lifecycle breadth; profile and test before changing order. |
| [TryApply](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:9>) | 236 | Correctness-sensitive validate/apply/money sequence; highest priority. |

The scanner observed many branch tokens in movement logic, but no formal cyclomatic-complexity value is claimed. Physical method length includes explanatory comments and local functions.

## G. Dead / Legacy Code

### Evidence levels

- **Definitely inactive source:** entire file is comment-only. Asset deletion still requires GUID/reference checks.
- **Deprecated island:** obsolete-labelled family, references limited to archived/inactive assets or its own family.
- **No discovered current consumers:** candidate for review, not proof of dead code.
- **Live compatibility behavior:** still consumed, even if it belongs to a superseded concept.

A conservative all-asset/editor/initialization-root graph potentially reaches **909 C# files / 202,372 lines (97.3% of physical C# LOC)**. The remaining **61 files / 5,653 lines** lack a discovered route in that graph. This deliberately overestimates “used”: editor roots, Resources assets, inactive authoring content and lexical references count.

A narrower enabled-build-scene/Resources/serialized-dependency graph plus editor tooling reaches approximately **199,846 lines (96.1%)**. Neither percentage measures actual runtime execution or proves every reached method is necessary.

### Review buckets

These are disjoint file buckets for review, **not an approved deletion list**.

| Bucket | Files | Physical LOC | Action |
| --- | ---: | ---: | --- |
| Entirely comment-only C# | 26 | 4,836 | Distinguish useful design notes from obsolete executable backups; archive deliberately. |
| Shelved/deprecated WorldGen Lab | 6 | 1,829 | Preserve ideas/contracts; retire stale implementation only alongside the approved V2 plan. |
| Deprecated map UI family | 16 | 3,553 | Remove serialized dependencies and compatibility consumers before deleting files. |
| Other disconnected active/prototype/adaptor files | 36 | 1,774 | Review individually; several are useful optional or planned seams. |
| **Total review surface** | **84** | **11,992** | **5.8% of C# LOC; not expected removal volume.** |

Examples of comment-only predecessors include [BuoyancyForce](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyForce.cs:1>), [BoatImpulse2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BoatImpulse2D.cs:1>), [TestBoat](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/TestBoat.cs:1>), and [WaterStencilPassFeature](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Rendering/WaterStencilPassFeature.cs:1>). Notes are documentation even if stored as .cs.

The shelved WorldGen cluster includes [WorldGenerationPipelineRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/WorldGenerationPipelineRunner.cs:1>), [WorldGenCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldGenCartridge.cs:1>), [WorldGenOverlayRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/WorldGenOverlayRunner.cs:1>), WorldGenerationWorkingSet, WorldGenerationProgress and WorldGenerationPreviewTextureBuilder. It should not become a second implementation of the approved future pipeline.

The deprecated map UI includes MapOverlayController, WorldMapHoverController, heatmap/route/marker overlays and node visualizers. Archived prototype scenes reference it. **WorldMapPlayerMarkerUI and UIEdgeGraphic also have enabled components inside inactive deprecated-map hierarchy branches in NodeScene/BoatScene.** Those references invalidate a simplistic “no C# callers → safe deletion” conclusion.

Examples without discovered consumers: [CargoBuoyancyBody](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Cargo/CargoBuoyancyBody.cs:1>) has empty Start/Update methods; [PlayerBoatSpawnOnSceneStart](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Player/PlayerBoatSpawnOnSceneStart.cs:1>) is a deprecated warning stub; [AirEscapeSolver](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/AirEscapeSolver.cs:1>) appears superseded. Conversely, WorldMapPersistenceDebugTool can be intentionally attach-on-demand tooling, and NodeTradeContactIntel is a planned trade-contact seam. Do not delete the latter just because it is presently unconsumed.

[BoatPilotingRouteState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatPilotingRouteState.cs:1>) remains active through [BoatPilotingSimulation](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatPilotingSimulation.cs:591>). [NodeTravelController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/NodeTravelController.cs:204>) contains a route restriction helper with no discovered caller, but that does not make the whole controller unused. File-level and method-level retirement must be separated.

## H. Multiplayer-Relevant Findings

There is useful groundwork, **not a completed multiplayer implementation**. The project has a gameplay role gate, actor intent interfaces, keyed persistence, authority-specific navigation/chart operations, and local camera/input ownership. The installed multiplayer-center package is not itself a gameplay transport or replication layer.

| Concern | Current evidence | Recommended boundary |
| --- | --- | --- |
| Shared boat/module simulation | [EngineModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/EngineModule.cs:89>), [GeneratorModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/GeneratorModule.cs:53>), [PumpModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/PumpModule.cs:59>), [ForceSystem](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/ForceSystem.cs:94>) | One authoritative simulation owner; clients consume presentation state. Audit actual prefab gate settings. |
| Vendor/trade operations | [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:176>), [ItemVendorOverlayRunner](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Runners/ItemVendorOverlayRunner.cs:235>) | Actor-scoped, validated, atomic transaction request with canonical stock and money state. |
| Module/UI requester | [ModuleCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ModuleCartridge.cs:32>) | Pass initiating actor/context; preserve a deliberate single-player fallback at composition root only. |
| Container world-drop | [StorageModuleWorldDropTarget](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Storage/StorageModuleWorldDropTarget.cs:25>) | Validate the provided requester/context at the mutation boundary; no trust solely because a UI called it. |
| World spawning/drop delivery | [WorldItemDropUtility](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/WorldItemDropUtility.cs:5>) | Exactly one authoritative carrier creation. Low-level replica/restore builders still need valid use paths. |
| Player persistence | [PlayerLoadoutPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/PlayerLoadoutPersistence.cs:21>) | Bind identity before restore; retain local-key compatibility. |
| Navigation truth vs replica state | [WorldNavigationService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs:55>) | Preserve explicit authoritative-write and replicated-apply separation. |
| Agent service discovery | [AgentServiceHandler](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Services/AgentServiceHandler.cs:15>) | Scene/service-domain selection before invoking handlers; interaction-time search is not automatically a CPU issue. |
| Camera and input | [CameraManager](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Camera/CameraManager.cs:183>), [LocalInteractionIntentSource](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Interactions/LocalInteractionIntentSource.cs:65>) | Preserve existing local ownership and gameplay-input gating. |

GameplayAuthority.CanRun prevents selected work for a role; it does not prove who requested an operation, authorize inventory access, protect against replay, or replicate the outcome. Avoid calling entire features “multiplayer safe” based on the presence of one guard.

Single-player should continue working throughout hardening. A future verification matrix should cover two actors on one boat, separate boats, scene change during an open UI, duplicate/replayed purchase requests, remote inventory identity, and authority transfer. Those are proposed tests, not completed checks.

## I. Performance / Runtime Risks

### Ranked measurement targets

| Priority | Pattern and evidence | Why measure / practical scope |
| --- | --- | --- |
| 1 | [CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:15>) scratch collections/delegates and [CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:80>) diagnostic strings | Repeated solver work scales with compartments, links and iteration count. Inspect GC.Alloc and solver CPU; reusable scratch memory must preserve order/results. |
| 2 | [Interactor2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Interactions/Interactor2D.cs:689>) boarded hover gathers two allocating overlap results, builds a list and produces an array | Runs on a frequent interaction path. Preserve interpolated/physical-frame coordinate correctness; reuse query/dedup buffers only after profiling representative boat speed. |
| 3 | [BoatPowerState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/BoatPowerState.cs:17>) demand getter invokes a child-MonoBehaviour scan | Cost depends on readers and installed modules. Cache registration/demand only if it is materially hot; avoid stale demand correctness bugs. |
| 4 | [EnvironmentDepthLighting](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Global/Rendering/EnvironmentDepthLighting.cs:68>) light search and per-LateUpdate sorting | Discovery is throttled to roughly 0.5 seconds, **not every frame**. Sorting captures camera state each frame; measure with representative light counts. |
| 5 | Texture baking/rotated chart previews | [CelestialChartRotatedTextureCache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialChartRotatedTextureCache.cs:14>) retains one rotated visual per fragment, quantizes angle, and bounds texture dimension. Source-texture reuse and owned-texture disposal exist. Profile active rotation/large boards rather than assuming a leak. |
| 6 | Large IMGUI/map/celestial presentation | [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:1>) and [CelestialObservationCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.cs:1>) | GUI work can run for multiple event types. Measure open-cartridge costs before a UI rewrite. |
| 7 | Missing-service retries | [WaveManager](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Global/Physics/WaveManager.cs:173>), [BoatHarborPresentation](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Travel/Travel/BoatHarborPresentation.cs:50>) | Searches occur on recovery/null paths. Fix absent configuration first; do not label them unconditional searches every normal frame. |

No target above has an assigned millisecond saving. The list ranks recurrence and scale risk from code, not observed frame dominance.

### Update, timers, coroutines and ownership

The 188 Update/FixedUpdate/LateUpdate declarations are spread across many independent behaviors, including editor/debug or optional components. A global replacement scheduler would add complexity without evidence.

[MiniGameOverlayHost](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Overlay/MiniGameOverlayHost.cs:158>) tears down on disable/destroy. Some work is scoped to an open presentation, while physical deployment and scene restore must outlive a UI. Preserve that distinction. [PlayerSceneContextRestorer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/PlayerSceneContextRestorer.cs:41>) is a retrying restore coroutine: future readiness/cancellation policy should specify scene unload, missing boat and timeout behavior. The scan found only 17 StartCoroutine sites and no InvokeRepeating calls; a new async framework is not justified by those counts.

### Physics, layers and render policy

Moving-boat collision proxies, reference-frame velocity, ladder exits, seats and thrown-cargo layer restoration solve demonstrated bugs. [CharacterMoveForce](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Player/CharacterMoveForce.cs:118>), [PlayerLadderClimber](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Player/Ladders/PlayerLadderClimber.cs:1>), and [ThrownCargoPhysics](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ThrownCargoPhysics.cs:1>) should retain their behavioral invariants.

Sorting/layer strings spread through authored rendering, but collision masks are a separate domain. Add targeted authoring validation for required sorting layers, masks and trigger settings rather than a universal layer-manager singleton. [EnvironmentDepthLighting](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Global/Rendering/EnvironmentDepthLighting.cs:68>) writes camera-relative shader globals; multiple simultaneous gameplay cameras would need a per-camera rendering policy instead of last-writer-wins globals. That is a future ownership constraint, not an observed single-camera failure.

Preserve the recently optimized celestial batches. Sunrise/full-screen shader cost remains an existing profiling TODO if frame rate becomes choppy; this audit supplies no fresh evidence to reopen that rendering pass.

## J. Logging / Debug Noise

The lexical scan found **1,113 Debug.Log-family call sites**. This includes warnings, errors, authoring, optional diagnostics and legacy source. It does not establish that logging floods normal gameplay.

[CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:80>) is the clearest logging-related runtime concern: formatting occurs before a disabled helper decides not to output. Gate at the caller, use a conditional diagnostic method that removes argument evaluation, or otherwise defer formatting. Validate generated-code behavior rather than assuming a disabled boolean means no allocation.

Keep failure/recovery messages such as [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:224>), [InventoryDragController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/InventoryDragController.cs:2148>), and [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:422>). They explain partial mutation, drag recovery, and missing save participants. Silencing them would hide correctness issues.

Boat-builder diagnostics and treasury lifecycle logs deserve optional verbosity controls if they are noisy. They should include relevant actor/boat/item/transaction identity and emit at meaningful transitions. A new global logging framework is not necessary for this pass. Avoid interpolated strings in per-frame diagnostic paths when diagnostics are off.

The inactive 2,221-line backup contains many apparent log calls if comments are not stripped. It was excluded from the active log count; raw text-search totals would overstate live noise.

## K. Top Refactor Candidates

These are **recommendations for later approval**, not implementation instructions for this audit. Rank considers correctness, future change radius and cost. Scope estimates are qualitative.

| Rank | Candidate / evidence | Risk now | Benefit | Contained scope | Timing |
| --- | --- | --- | --- | --- | --- |
| 1 | Atomic trade apply — [TradeEffectApplier](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Player/Trade/TradeEffectApplier.cs:176>) | High: partial commit on late failure | Correctness and future replay safety | Medium: one transaction owner plus failure-injection checks | Before adding more trade/MP behavior |
| 2 | Canonical vendor stock — [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:838>) | High ownership debt; reset/save policy unclear | Stable stock, restock and authoritative purchase policy | Medium: runtime stock model keyed by stable offer/vendor identity | Before vendor persistence/MP |
| 3 | Actor-scoped module commands — [ModuleCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ModuleCartridge.cs:32>) | Wrong actor with multiple players | Smaller UI coupling; testable command validation | Medium: context-in and request-out seam | Next module hardening pass |
| 4 | Shared module authority audit — [EngineModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/EngineModule.cs:89>), [PumpModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/PumpModule.cs:59>) | Duplicate shared simulation on clients | Correct shared-state writer ownership | Medium: guards and explicit restore/replica exceptions | Before actual networking |
| 5 | Vendor delivery/reservation helper — [ItemVendorPurchaseService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/Vendor/ItemVendorPurchaseService.cs:178>) | Refund/delivery edge cases across parallel services | Reusable validation and compensation | Small–medium; preserve carrier-specific placement | After ranks 1–2 |
| 6 | Rain callback/reference cleanup — [RainShaderRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/RainShaderRenderer.cs:54>) | Retained listener and missing-material continuation | Predictable scene lifecycle | Small: paired listener/reference policy | Quick contained bug pass |
| 7 | Compartment scratch/debug allocation — [CompartmentNetwork](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Compartments/CompartmentNetwork.cs:15>) | GC/CPU risk, unmeasured | Potentially cheaper repeated solver work | Small–medium with equivalence checks | Profile first |
| 8 | Catalog ID validation — [ItemDefinitionCatalog](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/ItemDefinitionCatalog.cs:16>), [AgentRegistry](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Agents/Runtime/AgentRegistry.cs:10>) | Silent/warned replacement of duplicate identities | Earlier authored-data failures | Small: editor validation and precise diagnostics | Before content expands |
| 9 | Session static-state policy — [ItemVendorCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/ItemVendorCartridge.cs:827>) | UI/gameplay state can outlive intended session | Explicit reset and ownership | Small–medium; distinguish scroll state from stock | With stock migration |
| 10 | WorldMapCartridge presenters — [WorldMapCartridge](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/MiniGames/Cartridges/WorldMapCartridge.cs:698>) | Large change radius | Isolated map modes, input and draw helpers | Medium, behavior-preserving | When touching map UI; coordinate V2 |
| 11 | Inventory domain operations — [InventoryDragController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/InventoryDragController.cs:1162>) | UI is coupled to multi-carrier mutations | Testable operation boundary | Medium–large; preserve rollback and nested containers | After transaction contracts |
| 12 | Save participant/readiness contract — [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:422>), [PlayerSceneContextRestorer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Scene/PlayerSceneContextRestorer.cs:41>) | Capture defaults or restore before readiness | Explicit order and multi-actor ownership | Medium; preserve snapshot/schema compatibility | Before MP/save expansion |
| 13 | Typed harvest capability/charge path — [UnderwaterResourceInteractable](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Environment/Resources/UnderwaterResourceInteractable.cs:502>) | Runtime string/reflection mismatch | Compile-time contract and shared charge rules | Small–medium with asset compatibility review | Next resource/tool pass |
| 14 | Retire inactive executable backups — [BuoyancyForce](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Physics/BuoyancyForce.cs:1>) | Mostly maintenance confusion | Cleaner source searches | Small after GUID checks; preserve notes | Optional housekeeping |
| 15 | Retire deprecated map family — [MapOverlayController](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/UI/MapOverlayController.cs:1>) | Conflicting ownership and scene references | One current map UI | Medium: serialized cleanup then file removal | After current consumers audited |
| 16 | Legacy route transition — [BoatPilotingRouteState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/Simulation/BoatPilotingRouteState.cs:1>) | Old navigation concepts remain live | Clear free-navigation contract | Medium–large, includes consumers and saves | Planned navigation/V2 checkpoint |
| 17 | Power demand query ownership — [BoatPowerState](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Boats/BoatPowerState.cs:17>) | Repeated child scans, unmeasured | Efficient stable module demand list | Small if profiler validates | Profile first |
| 18 | Door/hatch obstruction helper — [DoorRuntime](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Parts/Door/DoorRuntime.cs:25>), [HatchRuntime](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Parts/Hatch/HatchRuntime.cs:28>) | Behavior drift between copies | Shared close rules/feedback | Small, preserve different physical roles | When either behavior changes |
| 19 | Winch mechanics boundaries — [WinchModule](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Modules/Winch/WinchModule.cs:182>) | Broad lifecycle/physics change radius | Smaller independently testable policies | Medium; avoid replacing tether system | Only with focused regression coverage |
| 20 | Interactor hover buffers — [Interactor2D](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Controller/Interactions/Interactor2D.cs:689>) | Allocation risk; coordinate regressions if careless | Lower frequent query churn | Small with moving-frame checks | Profile first |

Do not turn these into a single “cleanup everything” branch. Each pass should have a concrete invariant and narrow acceptance checks.

## L. Things That Should NOT Be Refactored

1. **Do not consolidate all agent behavior into one NPC script.** Keep definitions, brains, movement modules and service handlers. The current 56-file / 4,593-line agent system is modest and reused.
2. **Do not collapse authoritative world position into the player's chart knowledge or estimates.** [WorldNavigationService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Navigation/WorldNavigationService.cs:1>) explicitly supports truth and replication; charts and survey observations intentionally reveal subsets.
3. **Do not replace optimized celestial batching with per-star GameObjects or a new all-purpose rendering framework.** [CelestialSkyRenderer](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.cs:1>) solved a measured user-visible bottleneck.
4. **Do not remove keyed persistence defaults or legacy snapshot mirrors without a migration.** [PlayerLoadoutPersistence](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Inventory/PlayerLoadoutPersistence.cs:21>) and [SaveGameService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Saves/SaveGameService.cs:450>) support current single-player and historical records.
5. **Do not redo moving-frame locomotion, ladder, seat or interaction geometry as code cleanup.** Preserve fixes and isolate any new change with speed/boarding/exit regression checks.
6. **Do not merge collision layering and visual sorting policies.** Thrown cargo's temporary physical policy and water/sky draw order are independent.
7. **Do not remove narrow interfaces solely because they have one implementation.** Environmental services, force providers and interactables are valid composition boundaries.
8. **Do not merge ScriptableObject authoring definitions with mutable instance/save state.** [ItemDefinition](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemDefinition.cs:1>) vs [ItemInstance](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemInstance.cs:1>), and celestial settings vs frozen configs, have distinct lifetime requirements.
9. **Do not purge attach-on-demand diagnostics or future seams as “dead.”** Preserve world persistence tools and NodeTradeContactIntel until their support intent is decided.
10. **Do not reopen diving-bell or authored boat snap systems to reduce LOC.** Their complexity follows real authoring/physics constraints.
11. **Do not casually change deterministic generation order, seed derivation, stable IDs, or cached/baked formats.** The approved V2 foundation handoff, migration versioning and deterministic fingerprints should govern those changes.
12. **Do not add namespace sweeps, asmdefs, new event buses, generic repositories or global service locators as prerequisites.** They do not resolve the confirmed ownership defects on their own.

## M. Recommended Cleanup Sequence

### 1. Establish correctness boundaries first

A contained trade failure/rollback pass, then canonical vendor stock and its explicit session/save/restock policy. Reuse the existing services, carriers and money system. Add tests that fail mid-operation and assert inventory, offers, money and stock remain mutually consistent. Normal-success-only checks are insufficient.

### 2. Fix small confirmed lifecycle/data-validation issues

Rain event pairing and required-reference handling; duplicate authored identity validation; catalog cache invalidation where live authoring is supported. Do not make lookup behavior silently stricter without checking authored content.

### 3. Finish actor and shared-writer ownership at touched seams

Pass actors into cartridges and service calls. Bind persistence identity before restore. Keep local defaults at composition roots. Audit module updates, storage/drop paths and physical role settings before introducing network clients. Role checks and request validation must remain distinct.

### 4. Measure representative runtime work

Use representative compartments, cargo stacks, moving boats, open map/observation UIs and lights. Collect CPU/GC evidence for the ranked targets. Optimize individual hot paths without changing physical-frame logic, solver iteration semantics or owned-resource cleanup.

### 5. Reduce large-file change radius incrementally

Extract existing presentation/helper boundaries from WorldMapCartridge and winch/chart cartridges. Move inventory domain operations out of pointer handling only once transaction contracts are stable. Preserve serialized names and save formats unless migration is part of the pass.

### 6. Retire legacy islands deliberately

Start with comment-only executable backups, preserving useful design notes. For deprecated map UI, remove or migrate serialized scene references first. For route concepts, inspect current consumers and saved assumptions before removal. Treat shelved WorldGen work as part of the planned V2 migration, not an independent cleanup revival.

### 7. Coordinate with WorldGen V2, not ahead of it

No implementation of the geography/cartography/workbench/New Game/foundation handoffs occurs here. The later foundation contract review should define authoritative representation, generator versions, stable IDs, caches and invalidation. Avoid duplicate architecture investments that its checkpoints would immediately supersede.

### Validation and compatibility gates for future approved passes

- Preserve prefab/scene script GUIDs, serialized field names, managed-type names and Resources paths unless an explicit migration updates their consumers.
- Test old and new saves when changing GameState, keyed player records, item payloads, boat state or generated IDs.
- Keep ItemInstance.InstanceId, BoatIdentity, node/celestial stable IDs and hardpoint-local IDs as distinct scopes. [ItemInstance](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Inventory/Item/ItemInstance.cs:44>), [BoatIdentity](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/GameState/Boat/BoatIdentity.cs:52>), [WorldMapStableIdUtility](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapStableIdUtility.cs:7>) and [CelestialStableId](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialStableId.cs:32>) are not interchangeable. Authored hardpoint IDs need boat identity when used outside one boat.
- Keep generated identities deterministic; do not replace them with incidental Unity instance IDs or runtime GUIDs. [SpawnClaimService](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/Multiplayer/SpawnClaimService.cs:1>) uses process-local claim identity and is not a persistent/network identity service.
- Preserve owned-texture disposal and event ownership. [WorldMapRuntimeCache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Data/WorldMapRuntimeCache.cs:198>) and [CelestialChartRotatedTextureCache](<C:/Users/skip_/OneDrive/Desktop/Don't Sink/Don't Sink/Assets/Scripts/WorldMap/Celestial/CelestialChartRotatedTextureCache.cs:14>) demonstrate useful ownership patterns.
- Treat warnings as evidence of explicit fallback behavior; decide whether a fallback is valid before making it fatal or removing it.
- Keep current single-player functional throughout multiplayer hardening.

## N. Final Verdict

**Healthy with manageable debt.**

The project is not close to a tiny prototype, so roughly 208k physical C# lines is plausible for its implemented gameplay, content generation, persistence and tooling. Most source appears purposeful. The strongest evidence against “mostly generated fluff” is the breadth of actual systems, the small median file size, extensive serialized consumers, and the limited size of the identifiable legacy islands.

The debt is concentrated in **ownership and coordination**, especially transactions, finite vendor stock, global actor fallbacks, scene/bootstrap assumptions, and broad UI/interaction orchestrators. Addressing those will improve reliability and make future work easier more than a campaign to reduce file or line count.

A sensible expectation is a small amount of removable legacy source and a larger amount of active code that can be made easier to change. **No blanket deletion list, major rewrite, or claimed exact useful-code percentage is justified.**

### Audit completion notes

- Full source inventory, LOC accounting, dependency indicators and candidate scans completed.
- Principal findings validated against current source; intentional duplication and Unity reachability pitfalls explicitly accounted for.
- Runtime performance, save round-trips, build behavior and multiplayer outcomes remain unverified in this report.
- Existing material and MainMenu scene changes were present before the audit and were left untouched.
- The report itself is the only new artifact from this pass. No refactor or implementation has started.
