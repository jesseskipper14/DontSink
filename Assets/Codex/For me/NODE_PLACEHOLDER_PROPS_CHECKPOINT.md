# Placeholder town props

Latest refinement: cargo now scales with the visit's prosperity, using stable reserved slots. Bench/planter amenities remain independent of wealth. Generated palette colors also respond to archetype. See `NODE_PROSPERITY_AND_ARCHETYPE_PALETTE_CHECKPOINT.md`; earlier counts below describe the preceding placeholder pass.

Added sparse cosmetic benches, stacked crates, barrels, and wooden planters. No signs were added. Their runtime hierarchy is `NodeTown/Settlement_<id>/TownProps`, with stable names derived from plot ID and candidate side.

Each permanent street now offers candidate sites every 1.5 units. The saved settlement seed ranks candidates and selects prop type and timber shade. Up to four safe sites per street are populated, depending on its width. Bench clearance is 1.05 units on each side; smaller props use 0.65. Placement rejects reserved buildings, event reserves, terrace edges, ladder approaches, nearby foliage, service/population sockets, and already placed props. Roof slots remain reserved for supported buildings and access. Props have no colliders, inventory, interactions, or authoritative state; they are placeholder decoration.

No Inspector changes or setup required. No saved layout data changed. These decorations repeat on revisits rather than supporting persistent destruction or looting.

The initial two-candidates-per-plot approach could reject every site, especially with bench-sized clearance applied to small props and a random population roll before clearance. It was replaced with the ranked street pool. Console output now reports `[Settlement] Town props placed: <count> | node=<id>` and warns if no safe sites exist.

Production runtime compilation passed. Direct3D11 settlement previews passed: the sample fishing town now has four props and the fortress has eight. A bench close-up was inspected. Across 200 generated towns in four archetypes, 2,800 placement assertions passed, including nonzero presence, repeatability, no added colliders, and access/foliage/socket clearance. Minimum sample count was four. Existing settlement regressions passed. Preview materials differ from production URP lighting.

Re-enter the node and check prop scale, spacing, ladder access, and any overlap with authored scene content. Commit if the live preview looks good. 🍌

## House frontage refinement

Flowers, other small foliage, and props may now sit in front of ordinary residences. Placement excludes the actual placeholder door at house center + width × 0.23 (door width 1.15), plus access clearance and the decoration's full width. Ladders retain clearance sized to the decoration, including support-owned ladders. Key/service building fronts (every enclosed non-residence role, including market and surveyor) remain fully reserved. Event reserves remain clear. Prop spacing and socket/foliage avoidance still apply.

Foliage uses its selected variant and scale to calculate clearance. No Inspector changes needed. New direct checks cover allowed house frontage, excluded doorways, wide props beside doors, key building fronts, and ladders; the generated-town placement regressions also pass. Re-enter a node to rebuild the decoration.
