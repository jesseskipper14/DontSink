# TOWN.3 — Exterior status board

Implemented 2026-10-09; awaiting visual playtest. TOWN.1–2 accepted; TOWN.4–7 pending.

## Implementation

- TownCenterBuilding binds a TownCenterStatusBoard on its authored StatusBoardSocket. Repeated binding reuses the component and its four TextMesh renderers.
- The enlarged existing board face displays the settlement name, one stat label, a large value and a trend arrow. A projected Next stat button cycles through all seven values when a local gameplay player is within six units. No menu is needed. Text uses GroundFront order 6, the builtin font material and width/height fitting. The button is hidden when gameplay input is blocked or the board is outside the active local camera.
- Reads GameState.worldMap.byNodeStableId using the building's canonical node ID. Resolves the current state object every refresh, so loading/rebinding cannot retain stale state.
- Shows population, Prosperity, Stability, Security, Food Balance, Trade Rating and Dock Rating. Population is displayed as a whole number; other stats use one decimal. Display rounding never modifies simulation values. No percentage conversion, normalization or artificial stat values. Most ratings are 0–4; Food Balance is −4–4.
- Refreshes at 4 Hz and changes text only when content changes. Missing state displays dashes; it does not create a state or show another node's information.
- No simulation, discovery, save-format, RNG, leader-menu or networking mutations. Settlement previews still display the actual saved node stats rather than invented preview statistics.

Trend arrows show the net change relative to recent samples observed during this visit (ten-second sampling windows, approximately 10–20 seconds once established). Green up means rising, red down means falling, grey dash means steady or not yet sampled. Replacing the loaded state or losing it clears trend history. Trend is presentation-only and is not persisted.

## Verification

Production runtime/editor compilation passed. The runtime retains two pre-existing unused debug-field warnings.

Isolated Unity checks loaded the actual Town Center/leader assets and verified rounded population/ratings, seven-stat cycling/wrap, negative food, live edits, green rise/red fall arrows, replaced-state trend reset, repeated-bind duplicate prevention, material/sorting/width fitting and missing-state non-mutation. The actual prefab loaded without the prior PPtr error. Checks used copied assets with script references remapped to the compiled diagnostic runtime assembly, outside the active editor.

## Playtest

Re-enter NodeScene, approach the board on the left side of the Town Center, and check readability at normal gameplay zoom. It should show a large population value. Click Next stat to cycle through the other six readings. Simulation changes should appear within a quarter second.

An authored replacement Town Center can retain its own board art; keep StatusBoardSocket and adjust the board component's Readable Size, Text Color and sorting fields as needed. This checkpoint stops at the handoff's **STOP FOR PLAYTEST** instruction. TOWN.4 local reports is next.

