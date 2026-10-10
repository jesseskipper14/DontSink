# TOWN.7 — Trade-contact integration and hardening

Implemented 2026-10-09. TOWN.1–6 accepted. All seven checkpoints and final playtest/freeze were accepted by the user 2026-10-09. The overall handoff is marked DONE_.

## Explicit integration hook

NodeTradeContactIntel is the future abstract-trade adapter:

1. The authoritative contact system calls TryObserve with a stable, unique contact ID, sending node, receiving node and observation world-hour timestamp. It receives a detached observation payload; the receiver cache is not updated.
2. The future system owns/persists that in-flight payload as appropriate for its contacts. After resolving an actual arrival/contact, it calls TryDeliver with the same contact/node identities, the frozen payload and receipt timestamp.
3. Delivery validates those identities and passes a copied receipt to the existing NodeAdjacentIntelService. It never queries current remote state at delivery, so travel delay cannot silently produce fresh remote truth.

No timer, contact-frequency model, price/stock transaction, trade route restriction, physical boat or trade simulator was added. Normal UI reads remain read-only. The existing manual Inspector seeding remains explicitly manual test data.

## Hardening

- Authority is required at both contact stages and at receipt storage. Mismatched/reversed node identities, missing contact IDs, non-neighbors, invalid times, receipt before observation, stale observations and duplicate contacts are rejected.
- A per-node receivedIntelContactIds ledger remembers receipts after newer snapshots replace earlier ones. It is persisted with civic state; replays remain rejected after save/load. Contact IDs must be unique per contact, including across different senders to the same receiver.
- Restoring civic state now ensures its collections and sequence counter exist. Older payloads without the receipt ledger learn the contact IDs present in their cached reports. Saves without any civic state still restore empty defaults.
- Existing NodeScene authors WorldMapRuntimeBinder, and SaveGameService refreshes WorldMapSaveBuilder before payload capture. Civic state continues through the existing runtime-node snapshot builder/restorer. No generator/topography/save version changes or regeneration.
- Existing provider tests confirm valid temporary work remains available, repeated queries preserve IDs, accept/turn-in transitions reject replay, and completion guarantees the next offer. No reward/payment behavior is invented.
- UI requester guards remain local actor/node/scene/range checks; snapshots are shared node-owned state. Real transport/authenticated two-instance command routing is not implemented by this mini-pass, and was not tested here.

## Verification

Production runtime/editor compilation passed. Two pre-existing unused fields in WorldMapTravelDebugController still warn.

Isolated Unity: **724 assertions passed**:

- 64 prior leader, local-information, civic-work, intel, authority and save tests;
- 17 contact/hardening checks for capture without receipt, frozen delayed data, contact/direction validation, time ordering, receipt replay, non-authority refusal, unchanged map knowledge/navigation/player money/economy, save/load contact identity/ledger, replay after cache replacement and old/incomplete payload defaults;
- 643 town regressions: 160 layouts across eight archetypes, each checked for validation, exactly one unstacked ground Town Center, determinism and poor-town civic development; plus version-3 migration preserving IDs/positions/sockets and remaining idempotent.

Tests ran in Library/CodexThrowableChecks without touching the active editor or user save slots. No real two-instance network or full UI playthrough was performed.

## Final playtest / freeze

- Board cycles through readable stats/trends.
- Leader local report shows current effects/events/conditions.
- Temporary Work accepts and turns in, then leaves another offer.
- Nearby Settlements displays received reports/ages, not live remote values; manual seeding changes the cache only when invoked.
- Save/reload retains work and intel, and civic reports do not add chart markers/reveal coverage.

The final playtest/freeze is accepted. The overall handoff is marked DONE_. Full quest generation, trade simulation, transport integration, leader dialogue/personalities, power integration and authored building art remain intentionally deferred outside this mini-pass.
