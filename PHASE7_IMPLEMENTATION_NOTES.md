# Phase 7 — celestial naming and constellation knowledge

Implemented against the current working files, including the uncommitted Phase 6 changes present at the start. No scene, prefab, material, package, inventory, or general physics edits were made by this pass.

## Architecture

- Truth stays in `CelestialField` / its constellation catalog. Default constellation generation is byte-for-byte equivalent to the previous generator. Custom tuning builds connected graphs from landmark stars only, starting with a connected path and adding unique branches.
- Evidence stays in immutable fragment marks. `CelestialKnowledgeQueries` scans persistent fragments matching the current seed, generator version, and star-field fingerprint. Sky visibility and debug presentation never grant evidence.
- Known state reuses the existing `verifiedConstellationIds` list. Eligibility is derived and requires every member. Validation is idempotent and goes through `CelestialKnowledgeAuthority`.
- Names and notes are shared crew annotations keyed by stable ID plus subject kind. Duplicate names are allowed. An expected revision rejects stale writes. Empty records retain a revision tombstone to prevent stale editors from recreating cleared data.
- Selection, text drafts, expanded occurrence labels, show-known preferences, and Look overlays are local presentation. Display labels disable rich-text parsing.
- Scraps retain their observed geometry. Known branches connect the actual marks on placed/rotated scraps. Same-scrap relationships draw on each occurrence; relationships crossing scraps use the nearest visible occurrence pair. Incorrect arrangements are not automatically corrected.
- Debug show-all can overlay truth-space branches where evidence is absent, but does not add stars to paper or unlock naming/validation.

## Modified existing files

Paths relative to the project root:

1. `Assets/Scripts/WorldMap/Celestial/CelestialGenerationSettings.cs` — Inspector constellation tuning.
2. `Assets/Scripts/WorldMap/Celestial/CelestialConstellationGenerator.cs` — tunable membership/branch counts; legacy defaults preserved.
3. `Assets/Scripts/WorldMap/Celestial/CelestialField.cs` — independent derivative-truth config/catalog invalidation.
4. `Assets/Scripts/WorldMap/Celestial/CelestialFieldGenerator.cs` — settings-based creation supplies constellation tuning.
5. `Assets/Scripts/WorldMap/Celestial/CelestialFieldSource.cs` — shared tuning restoration, render-only debug toggle, context-menu validation/reset.
6. `Assets/Scripts/WorldMap/Celestial/CelestialChartState.cs` — additive annotations, revisions, frozen derivative generation settings.
7. `Assets/Scripts/WorldMap/Celestial/CelestialChartingAuthority.cs` — freezes derivative generation tuning after successful evidence commit.
8. `Assets/Scripts/MiniGames/Cartridges/CelestialChartTableCartridge.cs` — additive partial-class hooks for selection/details/rendering; preserves scrap drag/rotate/snap behavior.
9. `Assets/Scripts/MiniGames/Runners/WorldMapOverlayRunner.cs` — passes the existing celestial field source to the table cartridge.
10. `Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.cs` — local known-constellation preference and telescope overlay hook.
11. `Assets/Scripts/MiniGames/Runners/CelestialObservationOverlayRunner.cs` — passes render-only debug preference to the telescope.
12. `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.cs` — partial-class declaration for the Look presentation extension.
13. `Assets/Scripts/WorldMap/Celestial/CelestialStarterChartBootstrap.cs` — freezes tuning when the fresh-world starter evidence is added.

## New production files

1. `Assets/Scripts/WorldMap/Celestial/CelestialConstellationGenerationConfig.cs`
2. `Assets/Scripts/WorldMap/Celestial/CelestialKnowledgeAuthority.cs`
3. `Assets/Scripts/MiniGames/Cartridges/CelestialChartTableCartridge.Knowledge.cs`
4. `Assets/Scripts/MiniGames/Cartridges/CelestialObservationCartridge.Constellations.cs`
5. `Assets/Scripts/WorldMap/Celestial/CelestialSkyRenderer.Constellations.cs`

Unity `.meta` files accompany these scripts. This document is also new. Temporary compiler and isolated test artifacts are under `Temp/CodexPhase7`; they are not production assets.

## Inspector/setup

No new components are required.

- On the existing `CelestialGenerationSettings` asset, expand **Constellations** under **Constellation Generation**. Defaults: average stars 6, star variation 1, minimum 4, maximum 7, average branches 5, branch variation 0. The unchanged default yields the historical 5–7 target members and a simple connected path. Actual branch counts are clamped to `[N-1, N*(N-1)/2]`.
- For fixed four-star tuning: minimum/maximum/average = 4; star variation = 0. For fourteen stars: minimum/maximum/average = 14; variation = 0. Set min/max before average because Inspector validation clamps the average. Raise branches for denser topology.
- **Use a fresh world to experiment.** Tuning freezes with the first successful chart/annotation/validation mutation, including starter chart creation. Existing worlds with pre-Phase-7 evidence use the historical defaults; later Inspector changes cannot silently reshape saved knowledge.
- On the existing `CelestialFieldSource`, **Debug Show All Constellations** defaults false. **Debug Requester** should be the exact player/interactor GameObject for Inspector context-menu mutations. **Debug Selected Constellation Id** can be entered manually or filled by selecting a visible debug constellation on the table.
- On `CelestialSkyRenderer`, **Look Intent Source** accepts the local player's `ICharacterIntentSource` component. Assign it explicitly for multiple-player configurations. The single-player fallback only selects a unique local `LocalCharacterIntentSource`. It reads `CharacterIntent.FocusHeld` (currently RMB through the existing input adapter), not a new mouse binding. **Show Crew Constellation Names** defaults true.
- Ensure the table/observation/sky use the existing matching celestial settings/source setup. No scene/prefab assignments were changed by this pass.

## Controls

- Star Chart: click a meaningful mark for Name/Notes. Dragging from a mark still moves a loose scrap; RMB still rotates. Ambient stars do not open an editor.
- Save Name/Notes, Clear Name, and Clear Note use the authority API. Reload Shared Annotation discards the local draft and adopts the current shared revision.
- Expand/Collapse This Label applies to that occurrence only. All occurrences resolve the same shared annotation, so renames update expanded labels.
- Known constellation lines/centroids are selectable. Direct celestial hits take priority. Hidden constellations are selectable only with debug show-all and cannot be annotated until validated.
- Folio: Show Known Constellations and Debug Validate All Eligible.
- Selected constellation details: Debug Validate Selected; Debug Reset To Hidden.
- Field source context menu exposes the same three debug mutations. Reset retains annotations and evidence.
- Telescope: Show Known Constellations defaults on. Lines use the existing rotated/zoomed telescope projection and circular clipping.
- Normal sky: no persistent lines. Hold Focus/Look for Known lines and optional crew names; release to hide. Debug show-all is an explicit development override. Visibility follows the existing star visibility and rendered-star positions.

## Persistence and authority

- Outer save schema remains version 1. Nested celestial chart snapshot becomes version 5. The existing SaveGameService serializes/restores it directly; no duplicate save route was added.
- New fields: `annotations`, `knowledgeRevision`, `lastKnowledgeEditorPlayerKey`, `constellationGeneration`, `constellationWorldSeed`, `constellationFieldHash`.
- Missing annotation data initializes empty. Existing verified IDs survive; missing verified data defaults Hidden. Existing fragments, groups, placements, and table pieces remain intact.
- Constellation generation inputs are small mutable-world identity metadata; generated constellation truth is not redundantly serialized. They are separate from the existing star config/hash, so branch tuning does not change star IDs or fragment fingerprints.
- Mutations require `GameplayAuthority.IsAuthoritative` and an exact requester GameObject. Provenance follows the existing player persistence component/local-key fallback. Future transport must authenticate and supply that requester; the current debug authority switch is not network authentication.
- Public query/authority seams include charted-object collection/query, annotation eligibility, name/annotation retrieval, revision retrieval, progress, validation eligibility, validation/reset, and separate name/note setters/clearers.

## Verification and remaining play-mode checks

- Isolated Unity checks passed 6,304 assertions. Results: `Temp/CodexPhase7/checks-result.txt`; harness: `Temp/CodexPhase7/UnityHarness/Assets/Editor/Phase7Checks.cs`.
- Full runtime C# compile succeeds with Unity 6's bundled Roslyn compiler and the project's references/defines, including new and previously untracked Phase 6 source files.
- Isolated Unity Editor checks compile unchanged production celestial sources with minimal test host adapters for GameState/requester persistence. Checks cover legacy/default equivalence, repeated deterministic output, fixed 4/7/14 members, sparse/dense graphs, graph connectivity/unique edges, landmark-only membership, unchanged star IDs/hash, 5/7 and 6/7 rejection, 7/7 validation, repeated validation, shared annotations, duplicate names, independent clear operations, stale revisions/tombstones, ambient exclusion, authority rejection, reset/revalidation, JSON roundtrip, old-save initialization, and preservation of old physical placements/groups/pieces.
- These checks are not an end-to-end campaign SaveSlot/LoadSlot test and do not exercise live UI, actual prefab wiring, or boat scenes. The headless harness is intentionally isolated from the user's open project.
- Play-mode regression still required: select/drag/rotate/pin scraps; folio paging; group snap/detach; overlapping repeated-star labels; text-field typing (C must not toggle compare); known/debug lines and hit priority; telescope toggle/calibration/transcription; Focus-held sky lines; night/cloud visibility; tab shared viewport; physical colored blocks/player boat; actual SaveSlot/LoadSlot and an existing Phase 6 save.

## Reconciliation / limitations

- The repository already persisted `verifiedConstellationIds`; it is reused rather than introducing a second knowledge list.
- Constellation truth already used landmark stars and connected paths, but member counts were hardcoded and branch tuning did not exist.
- The existing FocusHeld intent is the requested Look abstraction.
- Added presentation lives in partial support files to avoid rewriting the large, concurrently evolving Phase 6 cartridges.
- Name limit 128 characters; note limit 2048. Labels are screen-upright; expansion is session-local. Sky overlay switches immediately on Look rather than fading.
- No NPC validator, rewards, networking transport, freehand drafting, or adjacent-phase work was added.
- Visible stars alone never validate a constellation. Old mismatched-field fragments do not count as evidence for the current field.
