# Pinned instrument collider clearance

Date: 2026-10-02. Contained fix to `PlaceableBoatEquipment.cs`, shared by telescope and charting instruments. No serialized Inspector/prefab/scene changes and no additional Inspector tasks.

## Behavior

Before a FixedJoint2D is created, the instrument's actual enabled, non-trigger solid colliders are checked against nearby geometry. Compound child colliders attached to the instrument Rigidbody are included. Structural boat surfaces always need clearance, including real surfaces whose ordinary contacts are suppressed by the ghost system. External obstructions use the existing SkyClearanceRequirement obstruction-layer mask and ordinary layer/pair collision exclusions. An excluded PLAYER layer does not block the buffer check. Players cannot be mistaken for structural boat support merely because they are parented beneath the boat.

The instrument is lifted along boat-up in 0.01-unit increments until there is at least 0.02 units of separation. Lift is bounded to 0.25 units. A pose that already has clearance is preserved. The joint's connected anchor is configured after that final pose is established. The support probe reaches down across the bounded clearance adjustment so the instrument does not immediately unpin for losing contact with the foot probe.

If no safe supported pose exists, the instrument returns to its original position and no joint is created. A saturated nearby-collider query or trigger-only footprint also rejects pinning. The boat is never displaced to solve placement.

While pinned, `FixedJoint2D.enableCollision` is false: the attached instrument and the connected boat body cannot generate competing collision/trigger contacts. This is scoped to those connected bodies; player/other-object collision relationships remain intact. Unpin disables and removes the joint, restoring the ordinary boat collision relationship. [Unity's connected-body collision API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Joint2D-enableCollision.html).

BoatOwnedItemEscapeTracker now treats a live PlaceableBoatEquipment pin connected to the owning boat as physical containment. It resets the outside timer and preserves/restores mass contribution while pinned, even without BoatItemContainmentZone trigger membership. After unpin, joint disable/break, or component disable, the ordinary containment-zone/grace-period path resumes. No blanket exemption is applied to loose instruments.

The initial clearance fix caused two integration regressions: its all-layer scan ignored authored player exclusions, and suppressing connected-body trigger contacts made the escape tracker clear ownership after five seconds. Both are corrected by the mask handling and attachment-based containment above. No Inspector data was changed.

Both fresh pinning and `BoatLooseItemPersistence` deployment restoration call the corrected `RestoreDeployment` path. Corrected poses are subsequently captured by ordinary item pose persistence; no save format changes were needed.

## Validation

Full production runtime compilation succeeds. Isolated Unity checks pass **61 assertions** using unchanged production deployment, sky clearance, BoatOwnedItem, BoatItemRegistry, BoatItemContainmentZone, and BoatOwnedItemEscapeTracker sources with real Unity 2D physics; boat/world/policy hosts are adapters. The original 49 checks cover both instruments together, compound colliders, flat and 25-degree rotated boats, buffered gaps, supported pin retention, idempotent restoration, 150 solver steps per orientation without boat translation/rotation, unpin/redeployment, failed placement rollback, ceilings and unrelated obstacles, trigger-only footprint, and client rejection. The expanded checks cover deployment beside an excluded player, retained ownership without trigger membership, restoring direct mass contribution, resetting an already-expired outside timer, seven seconds of solver steps while pinned, ordinary escape clearing after unpin, structural clearance despite an empty external mask, and no ownership exemption for a disabled joint. Diff whitespace validation passes.

## Live checkpoint

1. Restart Play Mode so newly created/restored joints use the corrected code.
2. Pin each instrument, then both. Confirm a small clearance above the deck and no oscillation. Existing manual collider offsets may remain; the check uses the current collider geometry.
3. Deploy while standing beside/overlapping the instrument's placement area with PLAYER excluded from its obstruction mask. Leave it pinned for at least ten seconds; check BoatOwnedItem ownership and mass contribution remain intact. Sail/rotate the boat; confirm normal telescope/charting use, support retention, unpin, and pickup.
4. Save with both instruments pinned, reload, and confirm stable restored placement.
5. Try pinning where a ceiling/solid obstacle prevents the small lift. It should fail rather than create a conflicting joint.

Commit after the live checks pass. Camera safety remains the next planned pass.
