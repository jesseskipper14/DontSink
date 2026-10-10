# TOWN.5–6 — Civic work and adjacent intelligence

Implemented 2026-10-09. TOWN.1–4 accepted; TOWN.5–6 await playtest. User requested both checkpoints together; TOWN.7 remains separate.

## TOWN.5

NodeLeaderCartridge consumes INodeCivicQuestProvider (injectable, defaulting to NodeCivicQuestProvider), rather than assigning work to the transient NPC. NodeCivicState belongs to the canonical MapNodeState. Work availability and status are shared node state.

No general quest framework exists in the current project, so this checkpoint uses the handoff's allowed temporary/debug entry: Settlement check-in. Accept it, then confirm the check-in with this node's leader. It explicitly has no payment or item reward. This is a provider/lifecycle seam, not a delivery/combat/quest generator.

At least one pending valid temporary generic task is ensured by the authority. Repeated reads retain its identity. Completion stores history and creates the next offer using a node-qualified, invariant-format sequence ID, without RNG. Offered → Accepted → Completed transitions reject duplicate/stale actions and wrong node/quest identity. Future generators can replace the provider without replacing the agent architecture or menu.

## TOWN.6

Nearby Settlements lists social/economic contact neighbors from graph edges. It does not inspect their live NodeState when rendering. No snapshot means an explicit no-reliable-report message. Names may introduce a previously uncharted settlement; no position, terrain reveal, believed-position change or map marker is granted.

AdjacentNodeIntelSnapshot stores receiving/source node ID, observed node ID/name, observation and receipt world-hour timestamps, population, copied ratings, flags, major public events/effects, resource pressures, and a frozen readable report. Stored/query snapshots are deep copies and never hold a remote runtime-state reference. Observation/receipt ages and manual-test provenance are displayed, with a warning that values/remaining effect times describe the observation.

TryReceive is an authoritative storage/query seam. It verifies graph-neighbor eligibility and finite, ordered timestamps; duplicate receipt times and older observations cannot replace fresher intel. It does not resolve economic contact or automatically refresh information. TOWN.7 still owns the explicit abstract-trade contact adapter/hardening.

### Manual test reports

During play mode, select the generated Town Center and open its TownCenterBuilding component context menu:

**Debug Civic → Seed adjacent reports (manual test contact)**

This explicitly captures current adjacent states and receives those snapshots with the current world clock, marked Manual test report. Repeat later only when deliberately testing refresh; reading the menu never performs this action. It requires authority and a world clock, and grants no map knowledge. No inspector scene wiring changes are necessary.

## Persistence / authority

MapNodeState.civic stores quests, sequence counter and adjacent snapshots. WorldMapNodeRuntimeStateSaveSnapshot adds an optional civic field; the existing builder/restorer copy it. Old saves without that field receive empty civic state. Existing save/topography/generator versions and IDs remain unchanged; this is an additive payload, not a world regeneration.

Civic writes (fallback creation, accept, turn-in, intel capture/receipt) require GameplayAuthority.IsAuthoritative. UI selection and scrolling remain local. The leader's existing exact local requester, node, scene, on-foot and distance checks precede commands. No real two-instance network transport/session test was performed; client command routing remains the broader multiplayer integration's responsibility.

## Verification

Production runtime/editor compilation passed; two pre-existing unused travel-debug warnings remain.

Isolated Unity: 64 assertions passed, including prior leader/local-report lifecycle checks plus fallback/idempotency, copied work queries, accept/turn-in replay protection, guaranteed replacement offers, graph-only neighbor selection, no auto-fetch, frozen remote data, copied report queries, explicit refresh, stale/invalid receipt rejection, authority guards, unchanged map knowledge, actual save-builder/restorer capture, detached save/load copies, old-save defaults and JSON timestamp/list roundtrip.

## Playtest

1. Leader → Work: accept Settlement check-in, turn it in, verify a new offer exists. It pays nothing by design.
2. Leader → Nearby Settlements: choose a contact. Expect no reliable report before one has been received.
3. Seed manual reports using the component context menu above. Reopen/select a contact and see last-known stats plus ages. Let remote simulation change; the old report should stay frozen until explicitly reseeded.
4. Save/reload: quest status/history and received intel should persist. Check your chart remains unrevealed by these reports.

No quest generator, specialty quests, physical trade boats or trade simulator was implemented. Stop for this combined checkpoint's playtest before TOWN.7.
