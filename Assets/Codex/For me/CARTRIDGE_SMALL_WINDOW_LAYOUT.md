# Cartridge small-window layout pass 🍌

## Changed

- The shared MiniGameOverlayView now supplies interactive cartridges a minimum 1040 × 800 logical layout. If the existing overlay panel is smaller in either dimension, it becomes a bounded scroll view with horizontal/vertical access. Text stays at native size. Existing larger layouts are retained; no global UI scale or Inspector settings were changed.
- The fallback applies to interactive cartridges drawn through this shared view: Helm, Winch, Module, Money Chest, Boat Vendor, Item Vendor, Trade, Piloting, World Generation, World Map/Map Table, Star Chart, and the other IOverlayRenderable hooks using this view. Existing internal inventory/list scroll views remain available.
- A separate close button remains visible just above the scrolled panel, so the cartridge's own X cannot become the only way out when it is horizontally offscreen. It uses the existing host close path. Escape handling remains in place.
- Scroll position resets when the active cartridge changes.
- Winch has a measured internal vertical scroll view: additional rope-slot rows, status notes, motor controls, quick release, and Cut Line remain inside its panel and reachable.
- Star Chart's right details pane now scrolls independently. Fragment controls and celestial annotation controls remain bounded without moving the shared central map registration. World Map's independent sidebars were already fixed in the preceding refinement.

## Scope and validation

Full production runtime C# compilation passed; focused whitespace checks passed. No physics/gameplay/save/network code or Inspector assets were edited in this pass. No new automated tests were added for this reversible presentation change. Actual IMGUI scrolling, focus, and drag interaction still require Play Mode verification.

The separate full-screen telescope/observation runner does not use MiniGameOverlayView and is outside this shared fallback. This pass is not a complete redesign of every UI. Long labels or unbounded content in individual cartridges may still merit a focused follow-up.

## Play Mode checks

1. Use a smaller Game view (e.g. 1024 × 768 and 1280 × 720). Open Helm docked and underway. Scroll to the command terminal, type/submit a command, and close with Escape and the visible X.
2. Open Winch with several rope slots. Scroll to the last slot and lower controls. Verify press/release motor input stops correctly, including after scrolling/closing.
3. Check Module, vendors, Money Chest, and Trade: lower buttons, inventory cells, tabs, notes, and existing list scrolling should remain reachable. On a narrow window the horizontal scrollbar reaches the right columns.
4. Check Map Table's World Map and Star Chart pages. Verify map pan/zoom, physical-piece dragging, folio-to-board dragging, annotation focus, and fragment controls after scrolling. World Map coordinate picking/warp should still work.
5. Return to your usual larger view. Verify layout is unchanged whenever the overlay panel is at least 1040 × 800. Switching cartridges should start at the top-left.

Commit after the small-window regression checks pass. Earlier streamed-terrain and UI refinement changes remain in the working tree too.
