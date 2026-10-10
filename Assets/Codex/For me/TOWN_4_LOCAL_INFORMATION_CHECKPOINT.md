# TOWN.4 — Local node information menu

Implemented 2026-10-09; ready for playtest. TOWN.1–3 accepted. TOWN.5–7 remain pending.

## Behavior

About This Settlement now opens a scrollable current local report inside the existing NodeLeaderCartridge. Back to services returns to the four-button service menu; Leave/Escape and the existing distance/scene/local-owner guards retain their behavior.

The report reads the canonical node's current GameState.worldMap state and refreshes every half second while open. State objects are resolved anew after loading/rebinding. It includes:

- Population and all six ratings, with their actual 0–4 or −4–4 ranges. Population is a whole number and ratings one decimal by default. Show precise values exposes the underlying float values without changing simulation data.
- Current civic flags, with human-readable names.
- Active, unexpired buffs/effects, stack count, remaining hours, target and current influence per hour, using existing TimedBuffInstance behavior.
- Unresolved public events belonging to the current graph node index. Hidden/resolved/other-node events are excluded. Missing event infrastructure is reported as unavailable rather than claiming no events exist.
- Significant resource conditions from current ResourcePressureState values, sorted by resource ID: pressure ≤−1 is shortage and ≥+1 is surplus. Signs follow the existing market policy. This is pressure information, not an inventory/stock guarantee. ResourceCatalog display names are used when the existing local trade runner supplies a catalog; otherwise IDs receive readable spacing/capitalization.

No new NPC, dialogue, simulation, save structure, discovery reveal, quest or adjacent-node intelligence system. Optional power status remains deferred because the node civic power integration is not available. Report formatting rejects a state whose NodeId differs from the requested canonical node.

## Verification

Runtime/editor compilation passed, with two existing unused travel-debug field warnings. An isolated Unity run passed 37 assertions: 24 existing leader composition/identity/menu/lifecycle checks plus 13 local-report checks for seven stats, rounded/precise output, negative food, flags/buff expiry/stacks, event visibility/locality, resource signs, non-mutation, wrong/missing node state, live changes and state replacement.

Diagnostics did not interrupt the active Unity editor. GUI scrolling/layout still requires a visual playtest.

## Playtest

Talk to the Node Leader and select About This Settlement. Scroll through the report, toggle precise values, then return using Back to services. Current simulation changes should appear within half a second while the report remains open.

The handoff says **STOP FOR PLAYTEST** at TOWN.4. TOWN.5 civic work is the next checkpoint; Work and Nearby Settlements remain shells.
