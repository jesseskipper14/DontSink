# MP.4 ordinary loose-world-item checkpoint

Implemented 2026-10-12. Manual acceptance pending; MP.4 remains in progress.

Normal F pickup and Q tap-drop now run through authenticated host commands. Pickup duration is timed on the host with short owner heartbeats; the host rechecks range, seating, attachment/securing policy, inventory capacity and structural item fingerprint throughout the hold. Normal WorldItem acquisition and ownership/release rules are reused. Drop plans the personal inventory replacement before spawning; failed validation preserves the source. Hotbar drops release one unit; equipment/Hands releases the held quantity. Complete item snapshots preserve portable contents, chart metadata, charges and retained water.

World incarnation IDs are separate from persistent item IDs, so dropping the same item again cannot revive stale pickup requests. The host discovers ordinary active-scene items, owns their real physics, and publishes bounded snapshots (maximum 128 items, 256 KiB combined item data). Pose snapshots run at 20 Hz; item encoding refreshes at 4 Hz or immediately for actions. Clients instantiate the definition's WorldPrefab, retain authoritative scale/boat ownership, disable simulation and interpolate poses. Initial client-authored duplicates are removed from the runtime scene. Late join receives current items; leave destroys client replicas.

This adapter supports ordinary single-root-body or bodyless WorldItem prefabs. Compartment-bearing items, diving bells, tether deployment items, treasury objects and multiple-body prefabs are excluded. Attached items cannot be picked up through this route. General world-container opening, securing/unsecuring, salvage and vendor actions remain separate adapters. World dropping uses Q; releasing a UI drag outside supported inventory UI still cancels the drag.

Implementation: NetworkSceneBoat.WorldItems.cs, PlayerInventoryInput, WorldItem and WorldItemDropUtility. Protocol 14 requires matching builds. Production scenes/prefabs were not provisioned or changed for this checkpoint.

Native validation evidence is recorded in MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md.
