# Mooring marker: boat builder setup

2026-10-06 — Bosun 🍌

This step adds Mooring to Boat Builder's Cargo / Securing group and a Mooring prefab field to BoatKit. Placement uses the existing preview, grid snapping, prefab linkage, parenting and Undo. The tool checks for MooringPoint2D on the prefab root. Existing tool IDs are unchanged. No saved Inspector settings, boat prefabs or scenes were modified.

## Authoring tasks

1. Create a GameObject named Mooring with MooringPoint2D on the root. Put the root origin at the desired rope attachment location.
2. Add your tie-here sprite using a SpriteRenderer, either on the root or a child. A child lets you offset the art while keeping the attachment origin precise. No dedicated Rigidbody2D or collider is needed. Use the appropriate boat sorting layer/order for your marker.
3. Save it as a prefab, for example Assets/Resources/Prefabs/BoatKit/Mooring.prefab.
4. Select the BoatKit used by the builder (currently Assets/Resources/ScriptableObjects/BoatKit.asset) and assign the new Mooring slot.
5. In Boat Builder choose Cargo / Securing > Mooring, enable Auto Parent and select the player boat root. Placement uses the existing _Deck category (ExteriorDeck). Place one marker where you want to tie the boat. If already placed directly under the boat root, move it into _Deck while preserving its world position. Save your authored boat prefab as usual.
6. Place one matching marker on the authored quay. Adjust its SpriteRenderer sorting independently if necessary.

## Next step

The follow-up controller now supports one quay marker, one boat marker and one visual rope, without physics joints. Role and Rope Length are removed. See AUTHORED_QUAY_MOORING_SETUP.md for NodeSceneMooring setup on your authored quay marker.

Validation: full production editor assembly compilation passed. Check placement preview, parenting and Undo in the live builder after assigning your prefab.
