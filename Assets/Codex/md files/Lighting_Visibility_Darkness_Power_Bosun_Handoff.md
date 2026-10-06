# DON'T SINK — Lighting, Visibility, Darkness & Power
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Foundational lighting / visibility / darkness architecture pass  
**Goal:** Make light a first-class gameplay system. Darkness must materially affect navigation, exploration, interiors, underwater survival, power management, node presentation, and player fear without turning the game into unreadable mush.

---

# 0. Non-negotiable engineering rule

## Exact-current-class rule

If modifying any existing class, inspect the exact latest live source first. Never reconstruct an existing class from memory or from an older handoff.

Before touching an existing class:
1. Open the exact current source.
2. Preserve unrelated serialized fields and behavior.
3. Preserve current power, flood, compartment, weather, celestial, camera, water, save, and multiplayer behavior.
4. Reuse current systems where they already provide the correct authority.
5. Prefer additive support classes/data over broad rewrites.
6. Stop after every checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. Core pillar

Lighting is not primarily an art pass.

It is a gameplay system for:
- visibility,
- navigation,
- power management,
- environmental danger,
- spatial understanding,
- underwater exploration,
- settlement readability,
- failure escalation.

The intended experience includes:

> “The boat is sinking.”

followed by:

> “And now the power is failing and the lights are going out.”

Darkness should matter.

---

# 2. Responsibility split

Use separate cooperating systems:

```text
Global / Celestial Lighting
= sun, moon, sky, broad ambient illumination

Visibility / Portal System
= what spaces this local player is physically allowed to see

Local Physical Lights
= lamps, flashlights, searchlights, fires, node lights

Water Attenuation
= how celestial/artificial light degrades underwater

Personal Emergency Glow
= tiny local-only anti-frustration visibility floor
```

No single subsystem should fake all five.

If room visibility and rendered lighting disagree:

> Visibility wins.

A sealed room remains hidden even if Unity technically renders a light inside it.

---

# 3. Physical sun is daylight authority

The existing physical sun / celestial time system should be the source of truth for daylight.

Sun state should drive, directly or through derived values:
- daylight intensity,
- daylight color,
- sunrise/sunset transitions,
- broad shadow direction,
- sky presentation,
- star visibility,
- outdoor artificial-light automation,
- underwater daylight input.

Do not maintain a separate arbitrary “lighting time” that can disagree with the physical sun.

---

# 4. Moonlight is real

Moonlight is not merely a fixed blue nighttime ambient.

Moon contribution should depend on:
- moon position,
- whether it is above the horizon,
- lunar phase,
- weather/cloud attenuation.

Desired behavior:

```text
Full moon + clear sky
→ useful outdoor night visibility

Crescent moon
→ some illumination

New moon
→ effectively no moonlight

Moon below horizon
→ no moonlight

Cloud cover
→ reduced moonlight
```

---

# 5. Night should be dark

Avoid “blue daytime.”

Desired rule:
- moonless clear night can be extremely dark,
- cloudy moonless night can approach functional blackness,
- full-moon clear night may be navigable without artificial light,
- unlit interiors can be black,
- deep ocean can be black,
- artificial light should materially change what the player can do.

Readability is preserved through the player's tiny local emergency glow, not generous global ambient light.

---

# 6. Day/night transitions

Transitions should be continuous.

Smoothly derive:
- sun intensity,
- sun color,
- ambient contribution,
- moon contribution,
- sky color,
- star visibility,
- node exterior-light demand,
- underwater surface-light input.

Avoid visible snapping.

---

# 7. Selective shadow casting

Use real 2D shadows where useful.

Prioritize:
- hulls,
- major walls,
- major terrain,
- buildings,
- doors/hatches,
- important structures.

Tiny clutter need not cast dynamic shadows by default.

Moon shadows may be weaker/softer than sun shadows.

---

# 8. Opaque geometry matters

Hard rule:

> Opaque physical barriers block both sight and light unless an explicit opening allows transmission.

This applies especially to:
- boat compartments,
- authored building interiors,
- caves later,
- sealed rooms,
- underwater structures.

---

# 9. Per-player visibility

Visibility is local to each player.

Each player sees only what their own physical position / line-of-sight connectivity permits.

In multiplayer:
- Player A in an engine room may see almost nothing.
- Player B on deck may see bright moonlight.
- Their screens can legitimately differ.

Do not use one global shared visibility mask.

---

# 10. Personal emergency glow

Each local player gets a tiny, weak, local-only visibility floor.

Purpose:
- prevent total control blindness,
- let the player see their own body,
- immediate floor,
- very close ladder/hatch/geometry.

Properties:
- very small radius,
- very weak,
- no meaningful exploration range,
- no electricity cost,
- invisible to other players,
- no AI/world simulation effect,
- no map-knowledge effect,
- cannot pass through opaque barriers.

This is not a physical lamp.

---

# 11. Sealed dark-space rule

Hard rule:

> A sealed interior with no incoming light and no local light source is black except for the local player's tiny emergency glow.

This is intentional, including at extreme depth with no power.

---

# 12. Visibility portals

Use a reusable opening abstraction rather than bespoke logic per door.

Conceptually a `VisibilityPortal` or equivalent may support:

```text
PassesSight
PassesLight
LightTransmission
MovementAllowed
Open/Closed
```

Examples:

```text
Open door
→ sight + light + movement

Closed opaque door
→ blocks all three

Window
→ sight + light
→ blocks movement

Grate
→ sight
→ possibly attenuated light
→ blocks movement
```

Exact names should fit the live project.

---

# 13. Doors/hatches dynamically affect light and sight

Closed opaque hatch:
- blocks direct light,
- blocks sight.

Open hatch:
- sight becomes possible,
- light may spill through.

This should update dynamically.

---

# 14. Windows

Windows:
- pass sight,
- pass light,
- block movement,
- may attenuate/tint light through authored material settings.

Future options such as dirty glass, shutters, cracks, or frosted glass are not required now.

---

# 15. Compartment-aware visibility

Prefer reusing the existing boat compartment model as part of visibility logic.

Do not rely on Light2D shadows alone to decide which rooms a player can see.

Conceptually:

```text
Player in Compartment A
→ A is visibility-eligible

Open portal to B
→ B may become eligible

Closed opaque portal to C
→ C remains hidden
```

Then actual lighting determines whether the eligible space is bright or black.

---

# 16. Multi-room light propagation

If practical, allow light/environmental illumination to propagate through multiple open portals with attenuation.

Example:

```text
Sunlight
→ deck hatch
→ Room A bright-ish
→ doorway
→ Room B dim
→ doorway
→ Room C barely lit
```

Design for this even if v1 uses a simpler approximation.

Do not build a custom ray tracer.

---

# 17. Local light spill

Openings should permit spatial spill rather than toggling whole rooms bright/dark.

Responsibility split:

```text
Portal system
= whether sight/light may cross

2D lighting/shadows
= what the spill looks like
```

---

# 18. Exterior through openings

From a dark interior, an open hatch/window should reveal the actual lit exterior through that opening.

The surrounding hull/wall stays opaque.

Sorting/layering must not expose the rest of the exterior through solid geometry.

---

# 19. Artificial lights are physical world objects

Artificial light should belong to actual things:
- ceiling lamps,
- lanterns,
- deck lights,
- streetlights,
- searchlights,
- lighthouse lamps,
- fires,
- glowing equipment,
- suit lamps,
- diving-bell lights,
- future bioluminescence.

Avoid invisible gameplay fill lights.

---

# 20. Powered-light model

Most electric lights consume electricity.

Conceptually:

```text
PoweredLight
- switch/group state
- requested power
- actual power availability
- enabled/damaged state
- light output
- emissive visual state
- direction/type
```

A lit-looking sprite and actual physical light output must agree.

---

# 21. Power is a shared point of failure

Do not model flooding primarily as “each submerged bulb dies.”

Instead:

> Flooding/damage may compromise the electrical system, and that shared failure causes many downstream consequences, including lights going out.

Possible chain:

```text
Hull breach / flooding
→ power system compromised
→ available supply drops
→ pumps weaken/fail
→ normal lighting fails
→ searchlights fail
→ other equipment fails
→ faint emergency lighting remains
```

The exact flooding-to-power mechanics can belong to later damage/maintenance work, but lighting must support this cascade.

---

# 22. Switch, power, damage, render state are separate

A light may be:
- switched on + powered + healthy,
- switched off + powered,
- switched on + unpowered,
- disabled/damaged,
- emergency-only,
- logically on but render-culled.

Do not collapse all of this into one `isOn`.

---

# 23. Boat lighting groups / circuits

Prefer meaningful groups/circuits:

```text
Interior lights
Deck lights
Navigation lights
Searchlights
Special equipment lights
```

Local switches may still exist where useful.

Persist consequential player-controlled circuit/switch state.

---

# 24. Emergency lighting

Emergency lights are deliberately **not power-dependent**.

They are:
- automatic fallback,
- extremely weak,
- typically faint red,
- limited radius,
- not player-managed initially,
- not a substitute for real lighting.

When normal local lighting fails:

```text
Normal lights off
→ faint red emergency fixtures remain
```

Some spaces may have no emergency fixture.

---

# 25. Node power model

Nodes have an authoritative shared power supply.

High-level model:

```text
Node generator(s)
→ produce total power while running
→ node consumers draw from supply
```

Lights are consumers.

If fuel runs out or generation fails:

```text
streetlights off
shop/building lights off
market darkens
moon / flame / emergency remain
```

Do not build a full municipal grid unless separately designed later.

---

# 26. Automatic node exterior lights

Typical behavior:

```text
Night
→ exterior lights request power

Day
→ exterior lights stop requesting power
```

Power availability determines whether they illuminate.

Boat lighting remains more directly player-controlled.

---

# 27. Non-electric lights

Support real non-electric physical lights:
- flame lanterns,
- candles,
- fires,
- natural bioluminescence,
- future chemical lights.

Do not expand into fire/fuel micromanagement unless already present or explicitly requested.

---

# 28. Directional lights are first-class

Use one focused-light foundation for:
- player flashlight/headlamp,
- suit lamp,
- boat searchlight,
- diving-bell lamp,
- lighthouse beam,
- other focused lamps.

A searchlight is simply a stronger directional light with different authored parameters.

---

# 29. Player underwater light

A proper underwater player light should be strongly directional.

It follows aim/look direction, not merely movement direction.

Behind the player may remain black.

This is intentional: deep threats should be able to approach from unlit directions.

The tiny personal glow remains a weak circle around the player.

---

# 30. Light is not a facing-based vision cone

If another real light illuminates something behind the player and the player has physical line of sight, it may still be seen.

Visibility comes from:

```text
physical light
+
line of sight / portal eligibility
```

not from an arbitrary player-facing visibility cone.

---

# 31. Underwater lighting model

Conceptually:

```text
surface/celestial light
× depth attenuation
× weather attenuation
+ local underwater lights
```

Desired progression:

```text
sunny surface
→ bright shallows
→ dimmer depth
→ dark deep water
→ functional black
```

At night the curve begins much darker.

---

# 32. Deep ocean can be functionally black

Hard target:

> Deep enough water, especially at night, can be functionally black outside real light sources.

Actual lamps/searchlights should become survival equipment.

The private glow prevents absolute control blindness only.

---

# 33. Underwater artificial-light attenuation

Artificial light underwater should have reduced usable range/effect compared with air.

At minimum support tunable:
- underwater range multiplier,
- intensity attenuation,
- depth influence.

Future seam:
- turbidity,
- biome water type,
- sediment,
- storm-driven murkiness.

Water-condition simulation is not required now.

---

# 34. Light crossing the waterline

If practical, light from air can continue into water but becomes subject to underwater attenuation.

Examples:
- sunlight,
- moonlight,
- deck searchlights,
- lights near the waterline.

Accurate optical refraction is not required.

---

# 35. Reconcile with existing water shader

The project already has useful depth-related behavior in the water material.

Audit the live shader/material first.

Goal:
- preserve useful current depth scaling,
- distinguish shader presentation from actual lighting,
- avoid double-darkening,
- avoid contradictory depth curves.

Do not blindly stack a second depth-darkness system on top.

---

# 36. Weather attenuation

At minimum:
- clouds reduce sunlight,
- clouds reduce moonlight,
- fog/bad weather reduce visibility,
- storms can significantly darken the world.

Reuse current weather state where possible rather than inventing a second weather-light simulation.

---

# 37. Light color language

Keep colors simple and functional:
- sunlight → time-derived daylight/sunset,
- moonlight → cool-neutral,
- normal electric → warm/neutral,
- emergency → faint red,
- flame → warm,
- underwater artificial → source color filtered/attenuated by water.

No elaborate cinematic grading is required.

---

# 38. Simple art, strong lighting gameplay

The system must pair well with:
- simple sprites,
- flat/modular building art,
- modest color palettes,
- reusable materials.

Lighting should multiply simple art rather than require master-level painted assets.

---

# 39. Shared physical light vs private glow

### Shared physical light
Examples:
- flashlight,
- ceiling lamp,
- searchlight,
- lantern.

Other players see it. World is physically illuminated. Power/fuel rules apply.

### Personal emergency glow
Only the owning local player sees it. No world simulation or power effect.

Never confuse the two.

---

# 40. Logical light state vs render state

A distant lamp may be:

```text
On
Powered
Consuming power
```

while its expensive dynamic Light2D renderer is culled because it is off-camera.

Do not stop power consumption merely because rendering is culled.

---

# 41. Light rendering LOD / culling

Dense nodes may contain many logical lights.

Possible degradation:

```text
Near / on-screen
→ full Light2D + selective shadows

Mid distance
→ simplified glow / emissive

Far
→ emissive-only or culled
```

Underlying simulation remains correct.

---

# 42. Visibility masking has final authority

If a space is not physically visible to the local player:
- do not reveal NPCs,
- props,
- lights,
- interior contents,

even if the camera rectangle includes them.

Opening a valid portal makes it visibility-eligible. Lighting then determines brightness.

---

# 43. Light does not reveal map knowledge

Illuminating unknown land, a wreck, a harbor, cave mouth, or POI is still only physical observation.

It does not:
- chart terrain,
- reveal World Map coverage,
- register nodes/POIs,
- fix believed position.

---

# 44. Searchlights

Searchlights are strong directional lights with authored:
- beam angle,
- range,
- intensity,
- aim,
- power draw,
- shadow behavior,
- underwater attenuation,
- render LOD.

Use the same directional-light foundation as other focused lights.

---

# 45. Switch persistence

Persist consequential player-controlled lighting state where appropriate:
- boat lighting groups,
- important switches,
- searchlight on/off.

Automatic node streetlights may derive from:
- time of day,
- node power,
- damage/service state.

---

# 46. Future electrical damage seam

Later systems may add:
- shorts,
- breaker trips,
- flicker,
- fixture damage,
- partial circuit loss,
- flooding-induced power failures.

Do not fully implement those here.

Keep switch, power, damage, and render state separate so later systems can attach cleanly.

---

# 47. Sun/moon through interiors

Exterior celestial light only enters interiors through actual openings:
- open doors,
- open hatches,
- windows,
- breaches.

Sealed opaque hull/walls block it.

No hidden ambient cheat should illuminate deep sealed compartments.

---

# 48. Background / sky visibility

Inside sealed spaces, the player should not see sky/background through opaque walls.

Actual openings may reveal the exterior.

Coordinate this with current sorting and shell visibility.

---

# 49. Boat shell / sorting compatibility

Audit existing hull-shell and sorting logic.

Lighting/visibility must not break:
- interior/exterior presentation,
- forehull/backhull ordering,
- submerged-player sorting,
- doors/hatches,
- boarding/unboarding.

Do not solve room visibility by globally hiding the wrong shell layers.

---

# 50. Important authored building interiors

Most generated buildings remain exterior-only.

Rare important authored interiors should reuse the same:
- opaque-wall,
- portal,
- powered-light,
- visibility rules.

Do not invent a second town-interior lighting system.

---

# 51. Node blackout experience

A blackout should be dramatic:

```text
Generator out of fuel
↓
streetlights off
shops dark
building windows dark
market dark
↓
weak emergency red / flame sources remain
↓
moonlight suddenly matters
```

Same settlement geometry, radically different gameplay/readability.

---

# 52. Node-generation lighting hooks

Generated building/plot prefabs should own authored light sockets, e.g.:

```text
StreetLightSocket
InteriorWindowGlowSocket
MarketLightSocket
EmergencyLightSocket
FlameLightSocket
```

Node Generator places buildings. Buildings/plots own their internal lighting layout.

---

# 53. Building condition and lighting

Prosperity/condition may affect light presentation:
- prosperous areas may have more functioning decorative fixtures,
- poor areas may have fewer/dimmer fixtures,
- blackout overrides both.

Prosperity never creates free electricity.

---

# 54. Local terror requirement

Do not add a generous underwater/global ambient floor that defeats darkness.

Desired deep-water feel:

```text
strong beam ahead
+
almost nothing behind
+
tiny private glow
```

This is intentional.

---

# 55. Performance priorities

Prefer:
- room/portal eligibility,
- selective ShadowCaster2D,
- light culling,
- grouped power simulation,
- cached/static geometry,
- local reconstruction.

Avoid:
- per-pixel custom visibility across whole scenes,
- hundreds of always-active shadow lights,
- networking decorative light geometry every frame,
- duplicate lighting simulations.

---

# 56. Multiplayer / authority

Shared authoritative state:
- world time,
- celestial inputs,
- weather,
- boat/node power,
- physical light switch/circuit state,
- damage/disabled state,
- door/hatch/portal state,
- consequential physical-light aim/transform.

Client-local presentation:
- personal emergency glow,
- local visibility mask,
- render culling/LOD,
- interpolation.

---

# 57. Save/load

Persist consequential state as needed:
- boat circuit state,
- important switches,
- damage/disabled fixture state if implemented,
- node power/generator state through node power system.

Do not persist:
- local visibility masks,
- per-frame fades,
- personal-glow rendering,
- render culling state.

---

# 58. Debug / tuning

Useful debug controls:

### Celestial
- force time,
- sun position,
- moon phase,
- moon above/below horizon,
- cloud attenuation.

### Visibility
- show compartment IDs,
- show portal graph,
- show portal state,
- show current visible-space set,
- show light-passing portals.

### Power
- force boat blackout,
- force node blackout,
- set supply/demand,
- show emergency-only state.

### Underwater
- force depth,
- display attenuation,
- preview waterline crossing,
- preview beam ranges.

### Rendering
- show active dynamic lights,
- show culled logical lights,
- show shadow casters,
- show lighting LOD.

---

# 59. Suggested architectural seams

Exact names are not prescribed.

```text
GlobalLightingService
- derives sun/moon/global ambient state
```

```text
VisibilityPortal
- sight/light transmission
- state
```

```text
LocalVisibilityResolver
- determines visible spaces for one local player
```

```text
PoweredLight
- demand
- switch
- power
- damage
- Light2D/emissive control
```

```text
DirectionalLightSource
- aim
- cone
- range
- intensity
```

```text
EmergencyLight
- automatic weak non-powered fallback
```

```text
UnderwaterLightAttenuation
- depth/media modifier
```

```text
NodePowerService
- generation / supply / demand
```

Reuse existing equivalents where present.

---

# 60. Implementation checkpoints

## LIGHT.1 — Audit + celestial/global lighting
- inspect exact current sun/time classes,
- weather/fog inputs,
- water shader,
- current URP 2D lights/shadow casters,
- establish sun/moon/global ambient authority,
- smooth day/night,
- real moon contribution.

**STOP FOR PLAYTEST.**

## LIGHT.2 — Physical lights + power
- reusable powered-light component,
- emissive + Light2D synchronization,
- switches/groups,
- boat power integration,
- node-power consumer seam,
- faint red non-powered emergency lights.

**STOP FOR PLAYTEST.**

## LIGHT.3 — Compartment visibility
- local per-player visibility,
- opaque rooms hidden,
- reuse boat compartments,
- visibility portals,
- door/hatch/window rules,
- personal emergency glow.

**STOP FOR PLAYTEST.**

This is a critical checkpoint.

## LIGHT.4 — Portal light transmission
- open portals allow light,
- windows allow light/sight,
- closed opaque portals block,
- exterior through openings,
- local light spill,
- simplified multi-room attenuation if practical.

**STOP FOR PLAYTEST.**

## LIGHT.5 — Underwater lighting
- reconcile existing depth shader,
- daylight attenuation,
- night darkness,
- artificial underwater attenuation,
- directional player lamp,
- waterline-crossing light if practical,
- future water-condition seam.

**STOP FOR PLAYTEST.**

## LIGHT.6 — Directional/searchlight foundation
- common focused-light implementation,
- player lamp,
- boat searchlight seam,
- diving-bell/lighthouse compatibility,
- power draw,
- aim,
- underwater attenuation.

**STOP FOR PLAYTEST.**

## LIGHT.7 — Node lighting
- generated light sockets,
- night auto-request,
- node power,
- blackout behavior,
- emergency/flame exceptions,
- prosperity/condition hooks.

**STOP FOR PLAYTEST.**

## LIGHT.8 — Shadows / sorting / shell hardening
- selective shadow-caster pass,
- hull/wall/building compatibility,
- exterior-through-opening behavior,
- boat shell/sorting regression,
- authored-interior compatibility.

**STOP FOR PLAYTEST.**

## LIGHT.9 — Performance / multiplayer / save hardening
- logical vs render-active separation,
- culling/LOD,
- authority review,
- per-player visibility validation,
- save/load regression,
- dense-node stress test,
- debug cleanup.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 61. Minimum playtest scenarios

### Clear noon
Strong daylight. Closed interior remains blocked/darker.

### Sunset
Smooth transition. Node exterior lights begin requesting power.

### Clear full-moon night
Useful outdoor visibility while remaining clearly nighttime.

### New-moon cloudy night
Exterior approaches black. Artificial light required.

### Sealed unlit boat compartment
Black except tiny local glow.

### Open hatch into daylight
Exterior visible only through opening. Light spills inward.

### Multiplayer split
One player on deck, one below. Shared flashlight works physically. Private glows remain private.

### Boat blackout
Normal lights fail from shared power loss. Faint red emergency remains.

### Deep daytime dive
Progressively dark with depth.

### Deep night dive
Functionally black outside focused light.

### Directional-threat test
Behind-player space remains dark unless physically illuminated elsewhere.

### Node blackout
Electric city lights disappear. Moon/flame/emergency remain.

### Dense city stress
Many logical lights, limited dynamic render set, correct power demand.

---

# 62. Acceptance tests

1. Sun drives daylight.
2. Day/night transitions are smooth.
3. Moonlight depends on phase and position.
4. New moon contributes effectively no light.
5. Clouds reduce sun/moon contribution.
6. Night can become genuinely dark.
7. Opaque barriers block sight.
8. Opaque barriers block light except through valid openings.
9. Visibility is local per player.
10. Private glow is tiny and local-only.
11. Other players do not see another player's private glow.
12. Shared physical lights illuminate shared world.
13. Closed doors/hatches block sight/light.
14. Open doors/hatches permit sight/light.
15. Windows permit sight/light while blocking movement.
16. Sealed unlit rooms can be black.
17. Exterior is visible through actual openings.
18. Normal electric lights consume power.
19. Node lights consume node power.
20. Node blackout kills normal electric lights.
21. Boat power failure kills normal boat lights.
22. Emergency lights remain weakly active without power.
23. Switch and power state are distinct.
24. Damage/disabled state remains distinct.
25. Directional lights are supported.
26. Underwater player light is directional.
27. Searchlights reuse the same focused-light foundation.
28. Deep water can become functionally black.
29. Artificial underwater light attenuates.
30. Existing water depth behavior is reconciled, not double-darkened.
31. Physical light does not reveal cartographic knowledge.
32. Visibility masking overrides stray renderer/light visibility.
33. Authored interiors can reuse the same system.
34. Logical lights may stay powered while expensive renderers are culled.
35. Dense node lighting remains performant.
36. Multiplayer players may have different visibility while sharing physical light state.
37. Save/load preserves consequential light/circuit state.
38. Existing boat sorting/shell presentation remains intact.
39. Existing power/flood architecture remains compatible with future sinking→blackout escalation.

---

# 63. Explicit non-goals

Do not turn this pass into:
- full physically based global illumination,
- custom ray tracing,
- physically accurate refraction,
- real-time volumetric fluid optics,
- complete breaker/short simulation,
- fire propagation,
- candle-fuel micromanagement,
- water-turbidity simulation,
- full maintenance system,
- cinematic color-grading pipeline,
- procedural interiors,
- AI perception rewrite,
- map discovery rewrite.

---

# 64. Future extensions

Leave clean seams for:
- electrical shorts,
- circuit breakers,
- flicker,
- damaged fixtures,
- water-induced generator/electrical failure,
- repairable lights,
- flashlight batteries,
- lighthouse systems,
- crow's-nest visibility bonuses,
- turbidity/water types,
- bioluminescence,
- flame fuel,
- creatures reacting to light,
- stealth in darkness,
- emergency strobes,
- node-grid damage,
- sabotage,
- portable lamps.

---

# 65. Bosun delivery expectations

After each checkpoint report:
1. Exact files inspected
2. Exact files modified
3. Exact new files
4. Current sun/time authority used
5. Current weather inputs used
6. Current water shader/depth behavior found
7. Existing power architecture used
8. Visibility/portal architecture
9. Compartment integration
10. Private-glow implementation
11. Powered-light state model
12. Emergency-light behavior
13. Moonlight calculation
14. Underwater attenuation approach
15. Directional-light implementation
16. Node-power/light integration
17. Shadow/sorting decisions
18. Render culling/LOD
19. Multiplayer authority/local split
20. Save/load behavior
21. Performance tests
22. Regression tests
23. Any live-project discrepancy

Do not silently invent a second power system, time-of-day system, or water-depth model.

---

# 66. Final design summary

```text
Sun / Moon / Weather
        ↓
Global available light
        ↓
Physical geometry + portals decide where light/sight may travel
        ↓
Local physical lights add illumination
        ↓
Water attenuates light underwater
        ↓
Per-player visibility decides what this player may actually see
        ↓
Tiny private glow prevents absolute control blindness
```

And the desired failure cascade is:

```text
Hull breach
↓
Flooding
↓
Power generation/system failure
↓
Normal lights die
↓
Only faint emergency red remains
↓
Deep water outside is black
↓
Actual lamps/searchlights become survival equipment
```

That is not an edge case. It is part of the intended identity of the game.

---

**End of Lighting, Visibility, Darkness & Power handoff.**
