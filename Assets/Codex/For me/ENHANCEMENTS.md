# Enhancement ideas

Keep enhancements separate from reproducible bugs. Entries should state the benefit, scope, and acceptance checks before implementation.

## Candidate: explicit fresh-session reset

- Motivation: New Game has accumulated separate reset calls, and newly added persistent state is easy to omit.
- Possible scope: a single GameState fresh-session entry point plus narrow reset hooks for persistent runtime caches/services. Preserve existing scene setup and save-loading paths.
- Acceptance: one reset covers the intended world, player, chart, calendar, and treasury state; loading saves remains unchanged; repeated load/menu/new-game sequences start cleanly.
- Status: idea only, not approved or implemented. Address the concrete open bugs first.

## Future pass: boat equipment impulse release

- Rule: nothing in the boat is ever truly safe.
- Scope: host-side strong impulses can loosen, unseat, knock over or shift deployed equipment. Use `PlaceableBoatEquipment.BreakDeployment()`; keep physical rules separate from telescope presentation.
- Acceptance: the host releases physical deployment, observation invalidates/restores safely, resulting loose pose persists, and ordinary pickup becomes available.
- Status: seam added during Observation Telescope pass; impulse/damage behavior deferred as requested.

## Future multiplayer integration: deployed equipment and local warnings

- Scope: transport host deployment/unpin requests and replicate equipment state; bind each local actor to its existing camera and route observation warnings to that actor.
- Evidence: deployment is host-gated and persisted, but the current project has no telescope-specific network transport. Existing GameMessageService warnings are process-wide.
- Acceptance: separate viewers observe independently, non-viewers retain their presentation, clients cannot mutate deployment directly, and blocked-view warnings reach only their requester.
- Status: future integration; current single-player path and camera-scoped presentation implemented.
