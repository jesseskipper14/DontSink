# Harbor / node transition — final checkpoint (HT.5)

2026-10-04 — Bosun 🍌

Core harbor/departure/docking and visual/piloting integration were previously implemented and accepted. The final checkpoint reviewed current NodeScene generation, boat/player restoration and transition semantics, then reran the isolated regressions.

## Canonical NodeScene orientation retained deliberately

The handoff section 15.2 expressly allows documenting an invasive mirror and preserving canonical orientation temporarily. That option applies here: NodeGroundGenerator2D generates samples from x=0 to worldWidth, with left-side land and right-side underwater slope. Its walls follow those endpoints. BoatSpawner resolves an authored spawn transform and restores saved boat poses; PlayerSceneContextRestorer restores boarding/local context separately. The scene's dock/town/control objects are not generated from one reversible harbor layout model.

Mirroring only the terrain would mismatch dock/buildings/spawns. Negative-scaling a common scene root would risk text, asymmetric art, collider/physics and saved transform semantics. No blanket mirror was introduced. Deliberate harbor layout positions and spawn-side mapping belong with NodeScene_Harbor_Mooring_MiniPass and Node_Settlement_Generation, where the shared layout can actually own them. Global WaterwardDirection, berth and departure geometry remain authoritative independently of this side-view presentation.

## Transition checks

Reviewed SceneTransitionController.TryDockAtHarbor: authority/in-progress guard, controller requester/berth validation, departure lifecycle gate, player/loadout and boat capture, true navigation position set to the actual docked node, currentNode update, travel clear and NodeScene load. HarborDockInteraction uses the ordinary interaction system; no automatic trigger arrival was added. BoatSpawner initializes harbor navigation before streamed terrain and tether/item restoration, then clears the one-time departure reset. Existing bridge tests cover departure position, projection scale, heading/reset behavior, navigation restore and scene-owned bridge matching.

The old destination/source travel endpoints still exist alongside this modern path. They are legacy cleanup targets, not evidence that explicit proximity docking is missing. Chart knowledge and graph data were not modified by this closure.

## Verification and limits

The existing isolated Unity suite passed: 121 harbor geometry, 14 harbor bridge, 502 harbor presentation checks plus viewscape, terrain, polar, coastal, grounding and other regressions. Production component/geometry sources use adapted scene hosts. This does not certify a full assembled NodeScene save/transition cycle. Previously accepted live dock/embark tests remain relevant.

No gameplay code or Inspector assets changed. No new Inspector tasks. Optional live regression before the bookkeeping commit: dock at two different harbors, save/load NodeScene, embark again, confirm valid water departure and zero starting velocity/throttle, and confirm node/boat identity and inventory remain correct. Canonical local water/town orientation is expected at both harbors.

Harbor handoff marked DONE_ under its explicit canonical-orientation fallback. Mooring, settlement layout, discovery and final art are separate passes. Legacy cleanup begins with CLEAN.1 audit; this closure does not remove route APIs or migrate saves.
