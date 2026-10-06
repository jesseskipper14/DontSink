# Prosperity clutter and archetype palette

Crates and barrels now respond to the visit's prosperity snapshot (node prosperity rating divided by four). Each street has up to 2–6 cargo sites, according to width. Occupancy is rounded from `capacity × prosperity^1.35`: zero prosperity gives no cargo; full prosperity fills every safely available cargo site. One permanent bench/planter site per street remains independent of prosperity. Actual counts depend on clearance and candidate types.

All safe slots are planned and reserved in the same deterministic order regardless of prosperity. Poor-town cargo is a subset of rich-town cargo; existing props do not move as wealth changes. Empty slots remain empty during the visit. The next visit or an explicit debug preview reevaluates the snapshot. Existing building condition/occupancy and socket displays still use their original stat rules.

## Archetype desaturation

| Archetype | Blend toward grayscale |
| --- | ---: |
| Industrial IDs (fallback) | 82% |
| Fuel depot | 78% |
| Salvage yard | 72% |
| Shipyard | 60% |
| Fortress island | 50% |
| Storm refuge | 35% |
| Smuggler cove | 25% |
| Trade hub | 12% |
| Fishing hamlet | 8% |
| Lumber port | 4% |
| Farming atoll | 2% |
| Unknown archetype | 18% |

This treatment adjusts generated settlement SpriteRenderer colors once per reconstruction, including buildings, props, foliage, hills, structural details, and paving. NPC identification/skin colors are excluded. It does not alter shared materials, saved prefabs, the ocean/sky, or terrain outside the settlement. Intrinsic colors inside authored sprite textures are not converted to grayscale by this palette tint; a future material treatment can address those when production art replaces the placeholders.

No Inspector changes or setup are required. Rules live in `NodeTownPresentationRules.cs`; no new persistent or authoritative state is introduced.

## Verification and live checks

Production runtime compilation passed. Direct3D11 previews included poor/rich industrial nodes and a rich lumber node and were visually inspected. The industrial sample has two permanent amenities at zero prosperity and seven total props at full prosperity. The harness uses a compatible preview material rather than production URP lighting.

Generated-town tests cover stable prop IDs/positions, cargo counts increasing with prosperity, no cargo at zero, full/poor site subsets, clearance, repeatability, and archetype tint alpha/saturation. Existing settlement regression checks passed.

Re-enter a node, then use the existing town debug previews to compare prosperity states. Compare a fuel depot/shipyard/salvage yard with a lumber port or farming atoll. Doors, ladders, and key fronts should stay clear. Commit after the live visual checks pass. 🍌
