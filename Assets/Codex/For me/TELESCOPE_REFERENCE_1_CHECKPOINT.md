# TELREF.1 — Carried telescope references

Implemented 2026-10-09. Scene playtest accepted 2026-10-09 after rotation correction; handoff marked DONE_.

## Current architecture audit

- `CelestialObservationOverlayRunner` owns the F7 debug fragment preview. It browses shared `GameState.celestialCharts.fragments`, not carried inventory. Its paper/ink rendering is supplied by `CelestialChartFragmentVisualBuilder`. The production overlay reuses that builder and the runner's visual settings; F7 remains separate.
- `BoatObservationPresentationController` owns the actor-local deployed telescope session, camera binding, clearance checks and exit cleanup. The new References partial attaches presentation to this existing lifecycle and camera viewport.
- `ItemInstance` / `CartographicChartState` own physical chart evidence. Existing `CartographicChartIntegration.Collect` traverses the requesting actor's inventory, equipment and nested portable containers. No global chart library is offered by the telescope chooser.
- Survey-job clue cards are currently UI-only, not physical owned items. They are excluded. Geography/sounding charts and blank paper are also excluded unless they carry valid star evidence.
- Existing inventory clicks select/transfer items rather than opening paper viewers. R raises an eligible selected hotbar/equipment item, otherwise opens a small carried-items chooser.

## Implemented behavior

At the Star Chart table, select a recorded fragment and choose **Make carried reference · 1 Charting Paper**. This consumes one existing Charting Paper and issues a single existing cartographic-chart carrier containing a detached star-reference payload. Failure to add the item rolls back paper consumption. The original shared fragment is retained and unchanged.

During telescope use:

- R / Raise reference opens the selected eligible owned item or chooser.
- Opaque paper slides up from below the actor camera viewport.
- Left drag repositions paper; Shift + wheel rotates; Ctrl + wheel scales (default 0.7–1.35, serialized).
- Unmodified wheel remains telescope zoom. Numeric inventory selection remains available; wheel inventory cycling is suppressed during observation. RMB look is unchanged.
- Choose another reference replaces the current paper and releases its textures.
- R / Lower reference / Escape closes just the paper. E exits telescope use. Exit, forced invalidation and component cleanup discard transient state and generated textures.
- Ownership is recollected against the exact actor while raised, including the outer container chain; dropping or transferring the chart closes it.

There is no sky matching, alignment correction, score, discovery/coverage write, saved overlay transform, replicated presentation state or extra camera.

## Data and authority

The optional `CartographicChartState.starReference` payload is additive. Detached chart copies and existing item save serialization preserve the captured stars, provenance and recorded orientation. Legacy charts without valid marks remain ineligible. Reference charts cannot be integrated as geographic coverage.

The explicit copy transaction follows the current host-authoritative cartography convention. It resolves stored evidence by ID and uses the actor's existing paper/inventory paths. Client-side issuing of new reference copies is denied; there is no new network RPC in this checkpoint. The comparison UI itself is local and requires the existing telescope local-authority/session guards. Multiplayer end-to-end behavior still requires scene testing.

## Validation and playtest

Runtime and production editor compilation checked using the existing isolated diagnostic project. All 18 focused isolated Unity assertions passed. They cover detached evidence, payload JSON round trips, legacy eligibility, actor ownership, dropped-item ownership, immutable visual generation, opaque paper and texture/session cleanup. Results recorded in ignored `Library/CodexThrowableChecks/telescope-reference-results.txt`.

Scene playtest: make a carried reference at the Star Chart, use a deployed telescope, raise it with R, manipulate it, switch references, drop/transfer it, and exit/re-enter. Check RMB look, ordinary zoom, E exit and existing Survey interactions. Inspector/scene assets were not changed. No commit made.

## Playtest correction — 2026-10-09

Raise duration increased from 0.25 to 0.75 seconds. Rotation/scaling now sample frame input once, handling both scroll axes so Shift-wheel horizontal events and IMGUI event consumption cannot suppress rotation. Modified GUI wheel events are still consumed to avoid panel scrolling. Vendor/treasure issuance can reuse this viewer by setting the carried item's starReference payload; text-only cards and geographic charts are not automatically star references.
