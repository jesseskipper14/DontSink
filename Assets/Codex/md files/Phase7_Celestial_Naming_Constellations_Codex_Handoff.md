# DON'T SINK — Phase 7 Codex Implementation Handoff
## Celestial Naming, Notes, Constellation Knowledge, Validation, and Visualization

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Phase:** 7  
**Status entering this phase:** Phase 6 physical chart table is considered complete/frozen enough to build on.  
**Primary objective:** Add player-facing celestial naming/notes and complete the constellation knowledge lifecycle without violating the project's truth/evidence/knowledge/belief architecture.

---

# 0. READ THIS FIRST — NON-NEGOTIABLE PROJECT RULES

## 0.1 Exact-current-class rule

This project has an explicit rule:

> **If modifying an existing class, only modify the exact latest version currently present in the project. Never reconstruct an existing class from memory, from an older handoff, or from assumptions.**

Before changing an existing class:
1. Open and inspect the exact current file in the project.
2. Reconcile this handoff against the actual implementation.
3. Preserve all existing features, integrations, serialized fields, compatibility shims, and multiplayer-hardening behavior unless this spec explicitly replaces them.
4. Prefer additive changes over rewrites.
5. If a class has diverged from this handoff, adapt to the current class rather than forcing an older architecture onto it.

New support classes may be authored freely.

## 0.2 Do not casually refactor unrelated systems

Phase 7 should be a targeted feature pass. Do not:
- rename unrelated public APIs,
- move large systems around for aesthetic reasons,
- rewrite save architecture,
- replace the current map-table architecture,
- replace the observation/charting minigame,
- replace the physical fragment system,
- replace the current multiplayer authority spine,
- replace current celestial generation unless necessary to expose the requested tuning.

If cleanup is genuinely necessary, keep it minimal and document it.

## 0.3 Multiplayer shape remains important

The game is still primarily being developed/tested single-player, but the intended future model is:

- one shared boat,
- host-authoritative shared simulation,
- clients send intents,
- authority APIs mutate consequential shared state,
- replicated/shared state renders on clients later.

Phase 7 must continue this architecture.

Shared mutable celestial knowledge and shared annotations must not be implemented as random UI-local data.

## 0.4 Sacred conceptual separation

The celestial/navigation architecture depends on keeping these concepts separate:

### Truth
What actually exists in the generated world:
- stars,
- celestial objects,
- constellations,
- constellation membership,
- canonical constellation branches,
- world/celestial coordinates,
- stable IDs.

### Evidence
What the crew has physically charted or legitimately acquired:
- chart fragments,
- observed celestial marks,
- legitimate chart evidence,
- found/bought/earned charts.

### Knowledge
What the crew has learned or validated:
- whether a constellation is known,
- future official/validated relationships,
- future discovered classifications.

### Belief / Annotation
What the crew calls things or writes about them:
- player-created names,
- notes,
- physical chart placement,
- physical tokens,
- future drawn annotations.

**Do not collapse these categories.**

Player naming a star does not change celestial truth.  
Validating a constellation does not create missing chart evidence.  
Debug rendering must not mutate knowledge.  
A visible constellation must not fabricate stars onto scraps where they were never observed.

---

# 1. PHASE 7 HIGH-LEVEL GOAL

Phase 7 adds two related capabilities:

1. **Player-created names and notes for meaningful celestial subjects**
2. **A real constellation knowledge lifecycle**

Naming is intentionally low-stakes. It exists because:
- players may want personal vocabulary for the sky,
- names help human spatial memory and navigation,
- some players may name everything,
- some may name nothing,
- later dialogue systems may refer to player-created celestial names.

Example future dialogue use:
> “SnakeBalls69 sure is shining bright tonight.”

Phase 7 should expose a clean query seam for future dialogue/content systems, but does **not** need to implement that dialogue.

---

# 2. CURRENT FOUNDATION TO PRESERVE

## 2.1 World map / celestial coordinate relationship

The World Map and Celestial Map share the same continuous coordinate space 1:1.

A celestial/world position maps consistently between:
- physical world map,
- star chart,
- in-scene sky projection.

## 2.2 Physical chart table

Phase 6 already provides:
- World Map tab,
- Star Chart tab,
- shared viewport state,
- physical table space,
- pan/zoom,
- chart folio,
- draggable/rotatable chart scraps,
- pinned scraps,
- assembled fragment groups,
- manual evidence-based snapping,
- starter patch,
- shared physical table tokens/blocks,
- player boat piece,
- persistence of chart placements and physical pieces.

Do not replace this.

## 2.3 Physical evidence philosophy

Chart scraps contain real observed celestial evidence.

Successful observations create spatially accurate evidence. Errors should primarily come from player interpretation, placement, labeling, assembly, and navigation belief, not hidden RNG corruption.

## 2.4 Observation/charting

The current observation/charting system already produces persistent chart fragments.

Important existing concepts include:
- celestial object stable IDs,
- fragment marks,
- landmark stars,
- ambient stars,
- survey sequences,
- chart paper consumption,
- physical fragments,
- fragment visual generation.

Phase 7 must build on this evidence rather than invent a parallel celestial database.

## 2.5 Constellation truth scaffolding already exists

Constellation truth/generation already exists in some form.

Phase 7 should:
- inspect the current constellation truth structures,
- preserve their stable IDs and deterministic generation,
- expose/tune generation parameters where needed,
- add player knowledge around them,
- add rendering and annotation behavior.

Do not casually replace the generator if the current one already satisfies the truth model.

---

# 3. NAMEABLE CELESTIAL SUBJECTS

## 3.1 Anything meaningful except ambient stars

The rule is:

> **Anything except ambient stars may be named/annotated.**

Expected nameable categories include:
- landmark stars,
- nebulae,
- deep-sky objects,
- constellations,
- any future meaningful non-ambient celestial subject with a stable identity.

Ambient stars are explicitly excluded.

## 3.2 Ambient stars

Ambient stars:
- remain useful visual/context evidence,
- may contribute to chart matching/alignment,
- remain unnamed,
- do not get notes,
- do not get annotation indicators,
- should not become constellation members if that would make them impossible to fully chart under the current rules.

## 3.3 Capability-based implementation preferred

Prefer a central capability/query such as:
- `CanAnnotateCelestialSubject(...)`
- or an equivalent current-project pattern.

Intent:
- AmbientStar -> false
- meaningful celestial subject -> true
- constellation -> true through its constellation identity path

Constellations may have their own stable IDs/types, but the UI should treat them as annotatable celestial subjects.

---

# 4. CELESTIAL ANNOTATION DATA MODEL

## 4.1 Shared crew annotations

Names and notes are **shared crew state**, not per-player state.

For a nameable subject, the crew may store:
- stable subject identity,
- subject kind/type if needed,
- optional player name,
- optional note,
- revision,
- last editor/requester provenance if consistent with current architecture.

Conceptually:

```text
CelestialSubjectAnnotation
- subjectStableId
- subjectKind
- playerName
- note
- revision
- lastEditedByPlayerKey (if consistent with existing shared-state patterns)
```

Use current project naming conventions and save architecture rather than blindly copying this type name.

## 4.2 Stable ID is truth; player name is not identity

Never replace or key state by player-created name.

Correct:

```text
star_001842 -> playerName "Old Lantern"
```

Incorrect:

```text
dictionary["Old Lantern"] = ...
```

Player names:
- may be duplicated,
- may be changed,
- may be cleared,
- may be empty,
- may be silly,
- must never become authoritative identity.

## 4.3 Duplicate names are allowed

Do not enforce uniqueness.

## 4.4 Rename / clear behavior

For any nameable subject:
- name can be edited any time,
- name can be cleared,
- note can be edited any time,
- note can be cleared,
- clearing name must not clear note,
- clearing note must not clear name.

The annotation exists as long as either field contains meaningful data.

If both are empty, implementation may remove the empty record if that matches current save conventions.

## 4.5 Naming has no mechanical reward

Naming does **not**:
- improve accuracy,
- grant XP,
- reveal truth,
- validate constellations,
- modify observation,
- unlock navigation bonuses,
- affect generation.

It is player-authored vocabulary.

---

# 5. WHO MAY BE NAMED / WHEN

## 5.1 A subject must be legitimately known through evidence

Do not expose a searchable omniscient catalog of every generated celestial object.

A normal celestial subject should become available for naming because the crew has legitimate evidence of it.

For constellations, the constellation must be **Known**, not merely truth-generated.

## 5.2 No psychic astronomy

Normal subject progression:

```text
Truth exists
    ↓
Player obtains legitimate evidence
    ↓
Subject becomes interactable/nameable
```

Constellation progression:

```text
Truth exists
    ↓
All member stars are charted
    ↓
Constellation becomes eligible for validation
    ↓
Validation occurs
    ↓
Constellation becomes Known
    ↓
Constellation can be named/noted
```

---

# 6. STAR / CELESTIAL SUBJECT UI

## 6.1 Interaction from physical chart scraps

On a Star Chart scrap, a nameable celestial mark should be interactable.

User intent:
- click/select a meaningful celestial mark,
- open a small Name / Notes section,
- edit in ordinary UI.

This does not need fancy animation or ceremony.

## 6.2 Details panel preferred

Use the existing right-side details area if compatible with the current implementation.

Example:

```text
CELESTIAL SUBJECT

Name
[ Old Lantern              ]

Notes
[ Bright orange star.      ]
[ Useful eastern reference ]

[ Save ] [ Clear Name ] [ Clear Note ]
```

Exact layout can follow current UI conventions.

## 6.3 Selection

When selected:
- clearly highlight the selected celestial mark,
- preserve fragment interaction behavior,
- do not accidentally select a constellation line if a direct star hit is intended.

Recommended hit priority:

> **direct celestial mark hit > constellation line hit**

## 6.4 Annotation indicator

A subject with a name and/or note should have a subtle visual indicator even when its label is collapsed.

The indicator should:
- be small,
- not obscure evidence,
- communicate “this has annotation data,”
- not require the full text label to always be visible.

Ambient stars never get the indicator.

## 6.5 Hide / expand labels

Labels should be collapsible.

Default:
- chart remains relatively clean,
- annotation indicator is visible,
- full player-created name is not necessarily always shown.

The player can expand a label for a particular visible occurrence.

## 6.6 Expanded label behavior

Recommended:
- expanded label remains screen-upright,
- star/mark remains physically part of the rotated paper,
- optional subtle leader line connects label to mark,
- renaming the subject updates expanded labels immediately.

## 6.7 Local presentation state

The following should generally remain local presentation state, not shared crew state:
- currently selected subject,
- which occurrences/labels are expanded,
- current text field focus,
- local constellation render toggle,
- local open-world Look overlay state.

Do not network/synchronize cosmetic UI expansion.

---

# 7. SAME SUBJECT ON MULTIPLE SCRAPS

If the same stable celestial object appears on multiple fragments:

- naming it on one fragment updates the shared annotation,
- all other occurrences represent the same underlying subject,
- all expanded labels resolve to the same current name,
- notes remain shared by subject,
- fragment evidence remains immutable.

Do not store separate names per fragment.

This is a core acceptance requirement.

---

# 8. CONSTELLATION TRUTH GENERATION

## 8.1 Constellations exist from world creation

Constellations are generated as celestial truth at world/star-map creation.

They should have:
- stable constellation IDs,
- deterministic membership,
- deterministic canonical branch/line topology,
- stable generation under the same generation version/seed.

## 8.2 Constellation members should be chartable meaningful stars

Constellation membership should use chartable landmark-type stars, not anonymous ambient stars.

## 8.3 Inspector-configurable generation tuning

The user explicitly wants to experiment with very different constellation sizes.

Expose generation tuning in the Inspector.

At minimum expose:

```text
Constellation Generation
- Average Stars Per Constellation
- Star Count Variation
- Average Branches Per Constellation
- Branch Count Variation
- Minimum Stars Per Constellation
- Maximum Stars Per Constellation
```

If the current generator uses a different parameter model, adapt this intent cleanly rather than layering duplicate settings.

## 8.4 Why these settings exist

The user may want:
- small 4-star constellations,
- medium 7-star constellations,
- large 14-star constellations,
- sparse/simple branch topology,
- denser/more complex topology.

These are artistic/gameplay tuning knobs.

Do not hardcode “7 stars per constellation.”

## 8.5 Graph integrity requirements

Generated constellation topology must remain valid.

At minimum:
- every member star must be connected into the constellation graph,
- no orphan members,
- no duplicate edges,
- no self-edges,
- branch count cannot be below the number required to connect all members,
- branch count should be clamped to a sane maximum,
- deterministic output for same inputs.

A connected graph with `N` stars requires at least `N - 1` branches.

Clamp and/or warn when Inspector values are nonsensical.

---

# 9. CONSTELLATION KNOWLEDGE LIFECYCLE

## 9.1 States

Conceptually:

### HIDDEN
Constellation exists in truth, but the crew does not know the relationship.

### ELIGIBLE FOR VALIDATION
Every member star has been charted, but the constellation has not been validated.

### KNOWN
Every member star has been charted and the constellation has been successfully validated.

Implementation may derive `Eligible` rather than persist it.

Recommended:
- persist Known/revealed state,
- derive eligibility from current evidence.

## 9.2 Absolute all-stars-charted rule

A constellation cannot become Known unless **every single member star is charted**.

For a 7-star constellation:

```text
0/7 -> not eligible
5/7 -> not eligible
6/7 -> not eligible
7/7 -> eligible
7/7 + successful validation -> Known
```

No partial credit.

## 9.3 Definition of “charted”

“Charted” means the crew owns legitimate persistent chart evidence for that specific member star.

Do not count:
- currently visible in the night sky,
- currently rendered by debug tools,
- theoretically known by celestial truth,
- visible on some debug overlay.

Use the actual persistent evidence system.

Create or centralize a query such as:

```text
IsCelestialObjectCharted(stableId)
```

or equivalent current-project API.

Avoid duplicating evidence-scanning logic.

## 9.4 Validation does not create evidence

Validation may reveal the relationship among stars.

It must not:
- create missing star marks,
- create chart fragments,
- inject unobserved stars into old scraps,
- mark uncharted stars as charted.

---

# 10. CONSTELLATION VALIDATION

## 10.1 Phase 7 validation is debug-only

There is no real NPC gameplay implementation in this phase.

The eventual intended behavior is:
- rare/special NPC,
- chart-validation interaction,
- possibly quests/payment/other voodoo.

Do not implement that NPC now.

## 10.2 Real validation API must exist

Debug controls should call the same real validation pipeline future gameplay will use.

Provide authority-safe reusable operations conceptually equivalent to:

```text
CanValidateConstellation(constellationId)
TryValidateConstellation(constellationId, requester)
```

Use current project authority conventions.

## 10.3 Validation must reject incomplete constellations

If one or more member stars are not charted:
- validation fails,
- state does not change,
- report useful progress such as `6 / 7 member stars charted`.

## 10.4 Successful validation

On success:
- persistent shared constellation knowledge becomes Known,
- revision increments if using revisioned state,
- normal rendering becomes available,
- constellation becomes nameable/annotatable,
- save/load preserves Known state.

## 10.5 Debug operations

Provide at least:

```text
Debug Validate Selected Constellation
Debug Validate All Eligible Constellations
Debug Reset Selected Constellation To Hidden
```

Reset exists so the lifecycle can be tested repeatedly.

Reset should:
- change knowledge back to Hidden,
- not delete chart evidence,
- not delete celestial truth.

Preferred annotation behavior on reset:
- do **not** destroy stored name/note,
- hide it while the constellation is not Known,
- re-validation restores access to the prior annotation.

---

# 11. DEBUG VISIBILITY

## 11.1 Debug Show All Constellations

Expose an Inspector-configurable development toggle:

> **Debug Show All Constellations**

This allows the developer to inspect every generated constellation while tuning generation.

## 11.2 Render-only override

This toggle must be **presentation-only**.

It must not mutate:
- save data,
- constellation Known state,
- chart evidence,
- validation eligibility,
- annotations,
- revisions.

Turning it off returns to normal knowledge-gated rendering.

## 11.3 Scope

The debug visibility override should affect:
- Star Chart constellation rendering,
- relevant celestial/open-world constellation rendering.

A runtime context-menu/debug command is optional but useful.

---

# 12. CONSTELLATION RENDERING — STAR CHART

## 12.1 Hidden constellations

By default:
- no constellation lines,
- no constellation labels,
- no selectable constellation relationship.

Even if several member stars are charted, the relationship remains hidden until validation.

## 12.2 Known constellations

When Known:
- canonical constellation line segments render in Star Chart view,
- constellation becomes selectable,
- constellation may be named/noted,
- expanded label may be shown,
- annotation indicator may be shown if name/note exists.

## 12.3 Physical evidence rule

Constellation knowledge must not fabricate missing evidence.

Do not:
- print new stars onto scraps,
- rewrite fragment textures to invent previously unobserved marks.

Constellation visualization should layer over legitimate chart context.

## 12.4 Line rendering strategy

Prefer overlay/UI line rendering rather than destructively modifying fragment evidence textures.

Reason:
- constellation relationship is knowledge,
- fragment marks are immutable evidence.

## 12.5 Hit testing

Recommended selection priority:
1. direct celestial mark hit,
2. constellation label/centroid hit,
3. constellation line segment hit,
4. underlying fragment/board interactions.

Do not make constellation lines steal every click.

---

# 13. CONSTELLATION RENDERING — CHARTING VIEW

Known constellations should be available as a navigation/study overlay in the charting/telescope experience.

Recommended default:
- Known constellation lines ON in charting mode.

Expose a local presentation preference:
- `Show Known Constellations`

This preference:
- does not change shared knowledge,
- does not reveal hidden constellations.

---

# 14. CONSTELLATION RENDERING — OPEN WORLD NIGHT SKY

## 14.1 Default presentation

Known constellations should **not** permanently draw lines across the normal night sky.

Default normal view:
- natural stars,
- no constant constellation-line overlay.

## 14.2 “Look” behavior

Use the existing RMB-ish “Look” concept / input abstraction.

When the player holds Look:
- known constellation lines appear/fade in,
- known constellation names may appear if appropriate,
- hidden constellations remain hidden.

When Look is released:
- constellation overlay disappears/fades out.

Do not hardwire this to a literal mouse button if the current project already has an input abstraction.

## 14.3 Only Known constellations

Open-world Look must not reveal:
- Hidden constellations,
- merely Eligible constellations.

Debug Show All may override rendering for development only.

## 14.4 Scene projection consistency

Constellation line geometry must be based on the same stable celestial truth used by:
- chart view,
- world/celestial coordinate mapping,
- open-world star projection.

Avoid a second visual-only constellation geometry system that can disagree with map truth.

---

# 15. ANNOTATING CONSTELLATIONS

Known constellations use the same basic annotation model as other nameable subjects:

```text
Name
Note
```

Constellation names are player-authored labels, not truth identity.

The constellation truth retains:
- stable constellation ID,
- members,
- canonical branches.

---

# 16. FUTURE DIALOGUE / CONTENT QUERY SEAM

Phase 7 should expose a clean query API for later systems to retrieve player-created names.

Conceptually:

```text
TryGetCrewCelestialName(subjectStableId, out string playerName)
TryGetCrewCelestialAnnotation(subjectStableId, out annotation)
```

Exact API shape should match current architecture.

Future systems may use this for:
- NPC dialogue,
- quest text,
- chart-validation banter,
- procedural references.

Do not implement those content systems now.

---

# 17. PERSISTENCE

## 17.1 Save data

Phase 7 needs persistent shared state for:
- celestial subject annotations,
- constellation Known/revealed state,
- revisions/provenance if used by authority model.

## 17.2 Additive migration

Old saves must continue to load.

Missing Phase 7 fields should initialize safely to:
- no annotations,
- all constellations Hidden unless existing data says otherwise.

Do not invalidate old saves.

## 17.3 Deterministic truth is not duplicated unnecessarily

Do not serialize large amounts of constellation truth if that truth is already deterministic from world seed/generation version and currently reconstructed that way.

Persist mutable player state, not redundant immutable truth, unless the current persistence design already snapshots truth for version stability.

Inspect current save architecture first.

## 17.4 Annotation storage

Prefer lookup by stable subject ID.

If subject categories can collide in ID space, include subject kind or use globally unique stable IDs.

Check the current stable-ID scheme rather than assuming.

---

# 18. AUTHORITY / MULTIPLAYER HARDENING

## 18.1 Shared state

Shared crew state:
- player-created celestial names,
- notes,
- constellation Known state.

Mutations should go through authority-aware APIs.

## 18.2 Requester awareness

Follow the project's established requester pattern:
- UI submits intent/request,
- authority validates,
- authority mutates,
- revision/state changes,
- UI observes result.

Do not trust future client-authored identity when the project already has authenticated requester patterns.

## 18.3 Revision safety

Use revision/conflict patterns consistent with current board state if appropriate.

At minimum:
- annotation edits should not silently overwrite newer shared changes in a future MP context,
- constellation validation should be idempotent or safely reject repeats.

## 18.4 Local-only presentation

Do not authority-gate:
- label expansion,
- selection,
- hover,
- local show/hide toggle,
- open-world Look overlay,
- debug render-only visibility.

---

# 19. EXPECTED CURRENT FILES / SYSTEMS TO INSPECT

These names come from current project history. Locate and inspect exact current versions before editing.

Likely relevant:

### Star chart / map table
- `CelestialChartTableCartridge`
- `MapTableCartridge`
- `WorldMapOverlayRunner`
- current map-table viewport/shared state classes

### Chart fragment evidence / visuals
- `CelestialChartFragmentVisualBuilder`
- `CelestialChartFragmentSnapshot`
- current celestial chart save/container state

### Observation / charting
- `CelestialObservationCartridge`
- `CelestialObservationOverlayRunner`
- `CelestialSurveySequenceTracker`
- current celestial observation/projection settings

### Celestial truth
- current celestial generator/catalog classes
- current constellation truth classes
- current star/object stable ID structures
- current constellation member/branch structures

### Persistence
- `GameState`
- relevant save snapshots/containers
- current celestial chart/knowledge save structures

### Authority
- `GameplayAuthority`
- current chart-board authority classes/patterns
- requester/player persistence key helpers

### Open-world celestial rendering
- current sky projection / celestial scene renderer
- current Look/input handling
- current night/day visibility systems

### UI
- current right-side Star Chart details UI
- current hit-testing/selectable mark logic
- current GUI style/settings structures

If file/class names have changed, follow the actual project.

---

# 20. RECOMMENDED NEW SUPPORT TYPES

Conceptual suggestions only:

```text
CelestialSubjectAnnotationSnapshot
CelestialConstellationKnowledgeSnapshot
CelestialAnnotationAuthority
CelestialKnowledgeAuthority
CelestialKnowledgeQueries
CelestialAnnotationQueries
CelestialConstellationRenderSettings
```

Do not create unnecessary micro-services merely because this list exists.

---

# 21. CENTRAL QUERIES / APIS PHASE 7 SHOULD PROVIDE

End with centralized reusable queries rather than scattered logic.

Conceptual API set:

```text
CanAnnotateSubject(subjectId / subjectRef)
TryGetAnnotation(...)
SetSubjectName(...)
SetSubjectNote(...)
ClearSubjectName(...)
ClearSubjectNote(...)

IsCelestialObjectCharted(stableId)

IsConstellationKnown(constellationId)
GetConstellationChartProgress(constellationId)
CanValidateConstellation(constellationId)
TryValidateConstellation(constellationId, requester)

TryGetCrewCelestialName(...)
TryGetCrewCelestialAnnotation(...)
```

Exact names/signatures should follow current architecture.

---

# 22. PHASE 7 DEBUG TOOLING

Required:

### Inspector
- Debug Show All Constellations

### Debug actions
- Validate Selected Constellation
- Validate All Eligible Constellations
- Reset Selected Constellation To Hidden

Useful optional additions:
- log selected constellation stable ID,
- log charted member count,
- log missing member stable IDs,
- context menu to print constellation topology,
- generation diagnostics showing member/branch counts.

Debug tools must clearly distinguish render-only operations from state-changing operations.

---

# 23. IMPLEMENTATION ORDER

## Step 1 — Audit current celestial truth/evidence structures
Locate:
- stable IDs,
- constellation truth,
- chart evidence storage,
- persistence,
- authority patterns,
- map/chart/world render paths.

Document discrepancies between current code and this handoff.

## Step 2 — Add/centralize charted-object query
Implement one reliable definition of:
> “Is this stable celestial object legitimately charted by the crew?”

## Step 3 — Add persistent annotation state
Add save structures for:
- player name,
- note,
- revision/provenance if appropriate.

Add authority/query path.

## Step 4 — Add basic Star Chart selection/editor
Support meaningful mark selection.
Open Name / Notes UI.
Persist edits.
Add annotation indicator.
Add expanded-label presentation.

Test same stable star across multiple scraps.

## Step 5 — Tune constellation generation settings
Expose Inspector controls around the current deterministic generator.
Preserve generation validity and stability.

## Step 6 — Add constellation knowledge state
Add Hidden/Known persistence.
Derive eligibility from all member stars charted.

## Step 7 — Add validation authority/API
Implement:
- progress query,
- validation eligibility,
- debug validation,
- reset.

## Step 8 — Add Star Chart constellation rendering
Known-only under normal mode.
Debug Show All override.
Selectable lines.
Constellation annotation UI.

## Step 9 — Add charting-view known-constellation overlay
Known only.
Local toggle.

## Step 10 — Add open-world Look overlay
Known only.
Use current Look/input abstraction.
Debug visibility override.

## Step 11 — Save/load + regression
Exercise all acceptance tests.

## Step 12 — Cleanup
Remove temporary spam.
Keep intentional development controls.
Document APIs and serialized fields.

---

# 24. ACCEPTANCE TESTS

## 24.1 Annotation basics

### Test A
Chart a landmark star.
Select it from a scrap.
Name it.
Close/reopen table.
Name persists.

### Test B
Add a note.
Clear name only.
Note remains.

### Test C
Clear note only.
Name remains.

### Test D
Clear both.
Annotation indicator disappears.

### Test E
Name two different stars the same exact name.
Both remain valid and independently editable.

## 24.2 Same stable star across fragments

Create/obtain two fragments containing the same landmark star.

Name the star on fragment A.

Expected:
- fragment B resolves the same name,
- same shared note,
- no duplicate independent annotation.

Rename through fragment B.

Expected:
- fragment A updates.

## 24.3 Ambient exclusion

Interact with ambient stars.

Expected:
- no Name/Notes editor,
- no annotation indicator,
- no persisted annotation.

## 24.4 Constellation generation tuning

Test small constellation tuning around 4 members.

Then test large tuning around 14 members.

Expected:
- generation remains valid,
- deterministic for same seed/settings/version,
- no disconnected member stars,
- branch constraints respected.

## 24.5 Eligibility strictness

For a 7-member constellation:

- 5/7 charted -> not eligible
- 6/7 charted -> not eligible
- 7/7 charted -> eligible

No partial threshold.

## 24.6 Debug validation

At 6/7:
- Debug Validate Selected fails,
- reports useful progress,
- state remains Hidden.

At 7/7:
- validation succeeds,
- state becomes Known.

## 24.7 Known constellation Star Chart rendering

Before validation:
- lines hidden under normal mode.

After validation:
- lines visible,
- constellation selectable,
- Name/Notes available.

## 24.8 Debug Show All

Enable Debug Show All Constellations on a Hidden constellation.

Expected:
- lines render,
- save/knowledge state unchanged,
- eligibility unchanged,
- turning debug off hides it again.

## 24.9 Reset Known -> Hidden

Validate a constellation.
Name it.
Reset to Hidden with debug.

Expected:
- normal rendering hides,
- chart evidence remains,
- preferred: annotation data remains stored but inaccessible while hidden.

Revalidate:
- annotation returns.

## 24.10 Open-world Look

Known constellation:
- normal night sky has no persistent overlay,
- hold Look -> known lines appear,
- release -> disappear.

Hidden constellation:
- never appears through normal Look.

## 24.11 Save/load

Save with:
- named stars,
- notes,
- Known constellations,
- constellation names/notes.

Reload.

Expected:
- all shared persistent state restored.

## 24.12 Old save migration

Load a pre-Phase-7 save.

Expected:
- no errors,
- annotations empty,
- constellations Hidden by default,
- previous Phase 6 chart/table state intact.

---

# 25. REGRESSION TESTS

Phase 7 must not break:

- physical chart fragment creation,
- chart paper consumption,
- fragment visual generation,
- fragment colors,
- observation datum / paper boat marker,
- folio behavior,
- drag/rotate/pin,
- assembled fragment groups,
- manual Attempt Snap,
- table panning/zoom,
- World Map / Star Chart shared coordinate registration,
- starter patch,
- physical colored table blocks,
- Player Boat physical piece,
- save/load of Phase 6 board state,
- comparison/world geography overlays,
- existing charting minigame,
- existing celestial truth debug views,
- existing map-table authority behavior.

---

# 26. EXPLICIT NON-GOALS FOR PHASE 7

Do **not** implement these now unless required as tiny supporting hooks.

## Real NPC validator
Future feature.

## Mechanical reward for names
None.

## Automatic official names
Not required.

## Name uniqueness
Do not enforce.

## Arbitrary freehand drawing
Deferred to Phase 6C Cartography Workbench.

## Blank player paper placement
Deferred to Phase 6C.

## Distance / angle drafting tools
Deferred to Phase 6C.

## Physical impulse movement of table objects
Deferred to future impulse pass.

## Full networking transport
Still later.

## Full broader chart verification gameplay
Later phase.

---

# 27. DEFERRED PINNED FUTURE WORK

## Phase 6C — Cartography Workbench

Later add:
- freehand drawing on table/paper,
- erasing,
- blank paper consumed from inventory,
- blank paper follows normal physical paper rules,
- manual notes/drawings in unknown areas,
- distance measurement tool,
- bearing/angle tool.

These marks are belief/annotation, not celestial truth/evidence.

## Future impulse pass

Pinned TODO:

> Sufficiently large boat/table impulse can disturb loose physical table objects.

Eventually:
- colored blocks may slide,
- loose paper may shift,
- pinned/secured pieces resist appropriately.

Not part of Phase 7.

---

# 28. DESIGN PHILOSOPHY

The desired player thought process is:

> “I know this star. I called it Old Lantern. It appears on these scraps. Those seven stars form a constellation that the validator confirmed. When I hold Look, I can see that shape in the real sky.”

Avoid turning this into:
- abstract omniscient menus,
- auto-solved navigation,
- hidden scoring,
- systems that know more than the player's evidence.

The player's map can be wrong.  
The player's names can be ridiculous.  
The player's physical arrangement can be mistaken.  
Truth remains stable underneath.

---

# 29. FINAL CODEX DELIVERABLE EXPECTATIONS

When implementation is complete, provide:

1. Summary of architecture added
2. Exact list of files modified
3. Exact list of new files
4. Inspector setup changes
5. New serialized fields/defaults
6. Save schema additions/migration behavior
7. Authority/multiplayer considerations
8. Debug controls added
9. Known limitations
10. Regression tests performed
11. Any project assumptions that were necessary
12. Any place where current code differed from this handoff

If existing class changes are substantial, prefer complete exact-current-file replacements rather than partial snippets.

Do not silently omit requested features because they were inconvenient.

Do not expand scope into adjacent phases without explicit approval.

---

# 30. PHASE 7 DEFINITION OF DONE

Phase 7 is done when all of the following are true:

- non-ambient meaningful celestial subjects can be named/noted,
- annotations are shared crew state keyed by stable identity,
- duplicate names are allowed,
- annotations persist,
- same subject across scraps resolves the same annotation,
- subtle annotation indicators exist,
- labels can be expanded/collapsed locally,
- constellation generation size/branching is Inspector-tunable,
- constellation truth remains deterministic,
- constellations are hidden by default,
- eligibility requires every member star to be charted,
- validation uses a real authority/query pipeline,
- debug validation exists,
- debug show-all exists and is render-only,
- Known constellation lines render on Star Chart,
- Known constellation lines are available in charting view,
- Known constellation lines are available in open-world Look mode,
- Hidden constellations never leak through normal gameplay rendering,
- old saves load,
- Phase 6 chart-table behavior remains intact,
- future NPC/content systems have clean APIs to validate constellations and query player-created celestial names.

---

**End of Phase 7 handoff.**
