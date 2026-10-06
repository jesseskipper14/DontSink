# Piloting palette, layout and control orders

## Changes

- Main scope uses a depth-driven deep blue → pale teal → sandy shoals → green land palette. Amber fill and red warning stripes are removed.
- Shallow-water tint stays translucent so the existing wave texture and foam remain visible beneath it. Foam flecks are brighter and slightly larger; wave crest intensity is increased by 65%, capped at the existing peak color. Land remains opaque. Particle density and texture resolution/refresh rates are unchanged.
- Follow-up foam tuning: 20% lower alpha, 25% shorter and 20% wider flecks. Independent 8–16 second cell cycles provide 1.25 second fades, quiet gaps, new deterministic positions on each appearance and a subtle brightness oscillation while visible. No particle GameObjects or persistent state are added.
- Berth outlines and approach lines render in the same GPU pass as the circular scope, so the circle mask clips them. Up to six nearby berth outlines are shown without extra terrain queries.
- Main scope is centered in the cartridge. A larger mini-map and matching compass stack on its right. Harbor approach uses its own square panel in the gap beside the scope (up to 320 pixels); narrower windows reserve a third panel below the instruments.
- When there is no active voyage, the main scope displays DOCKED instead of the sailing view, matching the existing helm voyage context. Main waves, water motion and near-field drawing stop while docked; the sidebar instruments remain available.
- Left panel shows signed physical speed in scene units per second and geographic heading (north = 0°, clockwise).
- Left panel offers forward/stop/reverse engine orders and full/half/straight rudder orders. Engine orders select throttle fractions, not guaranteed speeds.
- Presets use the existing throttle and rudder travel rates inside the authoritative simulation. Keyboard adjustment cancels the pending target on that axis. Releasing the helm clears pending commands while preserving lever positions.
- Preset controls require the source pilot chair to own the simulation controls. They do not claim authority on the player's behalf.

## Inspector work

None. No saved scene, prefab, material or Inspector settings were changed.

## Validation

- Full runtime C# compilation passed.
- Isolated Unity D3D11 rendering checks passed: coastline matching, wrapping, latitude limits, natural depth colors, circular masking, dock rendering, shared height data and cleanup (16,416 assertions).
- Existing viewscape and harbor presentation regression checks passed.
- 20 control checks passed using the production control methods with isolated physics/profile hosts: gradual forward/stop/reverse orders, gradual rudder orders, limits, keyboard overrides, helm release and authority rejection.
- The complete cartridge layout still needs a visual check in the running game at your normal and smaller window sizes.

## Quick play check

1. Visit shallow water: deep water should lighten toward shore, becoming sandy near land without warning stripes.
2. Approach a harbor: cyan outlines should stop cleanly at the circular edge.
3. Click Full Speed, then Full Stop, then 1/4 Reverse. Watch the bottom lever move gradually; boat acceleration/deceleration remains governed by the existing engine/physics.
4. Click Full Rudder Left then Straight Ahead. Watch gradual movement. W/S and A/D should take over from the corresponding presets.
5. Confirm centered scope, larger right mini-map, compass beneath it, and accessible left buttons at smaller sizes (the order panel scrolls if needed).
6. Confirm the harbor approach has a readable square panel beside the scope. At a dock in NodeScene, the main scope should show DOCKED; after embarking it should resume the normal sailing view.

## Next checkpoint

Main-menu issues remain pending. Return to those after this piloting refinement is accepted. Commit a checkpoint after the play check passes.
