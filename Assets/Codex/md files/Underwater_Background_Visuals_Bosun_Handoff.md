# DON'T SINK — Underwater Background Visuals
## Bosun Implementation Handoff

**Project:** Don't Sink  
**Engine:** Unity 6.0, URP 2D, C#  
**Scope:** Underwater visual-depth / ambience / environmental-background foundation  
**Goal:** Make underwater spaces feel large, regionally distinct, layered, mysterious, and occasionally terrifying through persistent distant geography, mid-distance formations, near-background flora/fauna, and rare ominous moving silhouettes.

---

# 0. NON-NEGOTIABLE PROJECT RULE

## Exact-current-class rule

If modifying any existing class, inspect the exact latest live source first.

Never reconstruct an existing class from memory or an older handoff.

Before touching an existing class:

1. Open the exact current source.
2. Preserve unrelated serialized fields and behavior.
3. Preserve current water, biome, world-position, topology, rendering, sorting-layer, camera, save, and multiplayer behavior.
4. Reuse existing depth / terrain / biome query systems where practical.
5. Prefer additive support classes/data over invasive rewrites.
6. Stop after every checkpoint, compile/test, summarize exact changes, and wait for playtest approval.

---

# 1. CORE DESIGN PILLAR

Underwater backgrounds are not merely decorative wallpaper.

They should provide:

- scale,
- depth,
- regional identity,
- biome identity,
- topographic suggestion,
- environmental storytelling,
- visual motion,
- unease,
- future seams for POIs/events/creatures.

The player should sometimes be able to look into the distance and think:

> “That looks like a huge trench wall.”

or:

> “There are thermal chimneys all through this region.”

and occasionally:

> “What the fuck was that?”

That final reaction is important.

---

# 2. BACKGROUND ACCURACY PHILOSOPHY

Background visuals should be **loosely grounded in actual local world truth**, but prioritize interesting composition over geometric accuracy.

Hard rule:

> Backgrounds should suggest the real region, not duplicate the exact seafloor mesh.

Use real inputs such as:

- world position,
- biome,
- approximate depth/topography scan,
- nearby POI influence,
- local region seed.

But the final visual composition may exaggerate, simplify, or reinterpret those inputs.

Interesting > exact.

---

# 3. PERSISTENT REGIONAL IDENTITY

Major underwater background composition should be deterministic enough that returning to the same world region feels familiar.

Conceptually:

```text
World region
+ biome
+ topology/depth hints
+ deterministic seed
→ stable background profile/composition
```

The same region should preserve its broad visual identity across visits.

Examples:

- same distant plateau family,
- same broad trench-wall direction,
- same dominant chimney-field tendency,
- same regional silhouette language.

Do not require exact pixel-perfect reconstruction.

The goal is recognizability, not a saved screenshot.

---

# 4. THREE-LAYER BACKGROUND MODEL

Use three primary depth layers.

## LAYER 1 — FAR GEOGRAPHIC FORMS

Purpose:

- huge scale,
- vague topology,
- regional identity,
- visual horizon/depth.

Examples:

- enormous cliffs,
- vague valleys,
- distant trench walls,
- plateaus,
- sloping plains,
- ridges,
- deep shelves,
- massive rock masses.

Characteristics:

- very low contrast,
- large scale,
- soft silhouette,
- slowest parallax,
- often only partially visible,
- may be barely perceptible.

These forms may imply real topology but should not try to match exact terrain.

## LAYER 2 — MID-DISTANCE ENVIRONMENTAL FORMS

Purpose:

- readable biome character,
- stronger environmental definition,
- more obvious local geography.

Examples:

- chimney stacks,
- thermal vent fields,
- rock spires,
- ledges,
- shelves,
- cliff faces,
- rock towers,
- distant ruins later,
- broken columns later,
- volcanic structures.

Characteristics:

- more readable than far layer,
- moderate contrast,
- stronger parallax,
- stable enough to support regional identity.

## LAYER 3 — NEAR BACKGROUND SCENE LIFE

Purpose:

- immediate environmental richness,
- visual life,
- close parallax,
- make the scene feel inhabited even when gameplay space is sparse.

Examples:

- background flora,
- kelp,
- coral-like forms,
- small rocks,
- debris,
- small fish silhouettes,
- drifting jelly-like organisms,
- tiny schools,
- vent fauna,
- non-interactive environmental props.

Characteristics:

- clearly behind gameplay space,
- higher parallax,
- more detail,
- non-interactive,
- may regenerate between scene loads.

---

# 5. EXISTING SORTING / LAYERING AUTHORITY

Do not invent a parallel sorting system if existing project sorting layers/order already support the required hierarchy.

Background content should obey existing layering.

Broad intended order:

```text
Far geography
→ far ominous shadows
→ mid environmental forms
→ mid fauna / shadow forms
→ near background flora/fauna
→ playable foreground/world
```

Never allow background props to accidentally render in front of the player or interactable objects.

---

# 6. FAR / MID PERSISTENCE VS NEAR REGENERATION

Recommended persistence policy:

### Far
Stable regional composition.

### Mid
Stable regional composition.

### Near
Deterministic enough to remain coherent during a visit, but allowed to regenerate between scene loads.

Do not bloat save data with individual kelp fronds or decorative fish.

---

# 7. BIOME + DEPTH BOTH MATTER

Background visuals should be driven by both:

- biome,
- depth/topographic context.

Biome controls **what kinds** of forms are likely.

Depth/topography controls:

- scale,
- silhouette type,
- vertical relief,
- perceived distance,
- visual density,
- darkness/readability through the lighting system.

Examples:

### Coral / shallow shelf
- smoother terrain,
- more near flora,
- brighter forms,
- lower ominous weighting.

### Volcanic / thermal
- chimney stacks,
- jagged ridges,
- vents,
- darker vertical forms.

### Deep trench
- huge cliff walls,
- deep empty void,
- sparse but massive silhouettes,
- higher ominous-shadow weighting.

### Open bluewater
- distant broad plains/shelves,
- sparse hard geography,
- more negative space.

Exact biome content tables should be data-driven.

---

# 8. REUSE UPCOMING DEPTH / SCAN QUERY CODE

The project has upcoming scan/depth-query work intended to determine local depth/topographic context.

Reuse that foundation for background selection where practical.

Backgrounds should consume **coarse topographic/depth hints**, not duplicate the scan implementation.

Potential inputs:

```text
local average depth
depth gradient
slope direction
nearby cliff tendency
nearby trench tendency
plateau tendency
large depth discontinuity
```

Do not tightly couple the renderer to exact scan implementation details.

Use a small stable query interface if possible.

---

# 9. TOPOLOGY SUGGESTION, NOT TERRAIN DUPLICATION

Example:

Actual scan indicates:

```text
deepening strongly to the right
large depth discontinuity nearby
```

Background system may choose:

- huge dark slope,
- distant cliff wall,
- vague descending plateau.

It does not need to recreate the literal collision contour.

This keeps the visuals believable without becoming computationally or artistically brittle.

---

# 10. BIOME TRANSITIONS SHOULD BLEND

Do not abruptly replace one background profile with another at an invisible biome boundary.

Transition visual weights gradually over world distance.

Example:

```text
open bluewater
→ occasional dark jagged forms
→ a few chimneys
→ frequent volcanic structures
→ full thermal-field language
```

Use profile blending/crossfade where practical.

Avoid visual popping.

---

# 11. POI INFLUENCE IS WEIGHTING, NOT DUPLICATION

Nearby POIs may modify background visual weights.

Example:

```text
Volcanic cave POI nearby
→ chimney likelihood increased
→ thermal vent silhouettes increased
→ jagged volcanic forms increased
```

But generic background forms do not need to correspond to exact POI geometry.

This allows environmental foreshadowing without turning scenery into false interactable landmarks.

---

# 12. BACKGROUND CONTENT SHOULD READ AS NON-INTERACTIVE

Background objects must visually remain distinct from gameplay-space objects.

Use combinations of:

- reduced contrast,
- depth tint,
- scale,
- blur/softness where appropriate,
- layer placement,
- parallax behavior,
- reduced animation intensity.

Do not make a distant chimney look like a harvestable resource node.

Do not make scenic rocks look like collision geometry.

---

# 13. BACKGROUND READABILITY RANGE

Visual strength may range from:

- barely perceptible,
- vague silhouette,
- moderately readable.

Not every background element should be equally visible.

Far forms may sometimes be nearly subconscious.

Mid forms may be recognizable.

Near-background props can be clearer while still reading as non-interactive.

---

# 14. NO WATER-CONDITION SYSTEM REQUIRED YET

Current design assumes one basic water condition.

Do not implement full:

- turbidity,
- sediment,
- regional visibility water types.

The architecture may leave a future seam, but current background readability does not need a water-condition system.

Lighting/depth darkness can still affect what is visible.

---

# 15. NEAR-BACKGROUND AMBIENT FAUNA

Near-background layer may contain non-interactive moving life.

Examples:

- small fish silhouettes,
- schooling dots,
- jelly-like drifting forms,
- vent worms/fauna later,
- tiny crustacean-like movement,
- drifting organisms.

These are visual ambience only.

No:
- collision,
- combat,
- loot,
- AI threat logic,
- map discovery.

---

# 16. AMBIENT MOTION STYLE

Background motion should generally be slow.

Near flora/fauna can move more than far geography.

Possible motion:

- gentle sway,
- slow drift,
- schooling motion,
- minor vertical bob,
- lazy directional swimming.

Avoid excessive busy motion that competes with gameplay.

---

# 17. OMINOUS SHADOW SYSTEM

Add a dedicated system for rare large moving background silhouettes.

Purpose:

- scale,
- fear,
- ambiguity,
- creature foreshadowing,
- biome danger flavor.

This is mostly presentation.

Some future real creatures/events may inject or reuse these visuals.

---

# 18. AMBIENT / REAL THREAT MIX

The shadow system should support both:

### Ambient-only shadows
Pure atmosphere. Nothing follows from them.

### Future event/creature-driven shadows
A real gameplay system may deliberately trigger or control one.

For v1:

> Most shadows are presentation only.

Do not make every shadow correspond to a hidden enemy.

Not everything in the ocean wants to eat the player.

Some things might.

---

# 19. SHADOW CADENCE SHOULD BE RARE AND EPISODIC

Do **not** run ominous shadows on a predictable short timer.

Desired cadence:

- perhaps 1–3 passes during a loose episode,
- then potentially no large shadow for 10+ minutes,
- sometimes considerably longer,
- biome/danger weighting affects likelihood.

The player should not learn:

> “Kraken shadow happens every 120 seconds.”

That destroys the entire effect.

---

# 20. SHADOW EPISODE MODEL

Prefer an episode/burst scheduler rather than simple periodic probability.

Conceptually:

```text
Long quiet period
↓
Shadow episode begins
↓
1–3 possible passes with irregular spacing
↓
Episode ends
↓
Long cooldown / silence
```

Exact timings are tunable.

Use deterministic/random authority appropriate to current game architecture.

Do not make this save-important unless needed.

---

# 21. BIOME / DANGER WEIGHTING

Some biomes should have a higher chance of ominous activity.

Examples:

- deep trench → higher weighting,
- abyssal region → higher,
- dangerous POI region → possibly higher,
- shallow safe shelf → lower.

Do not make high weighting mean constant occurrence.

Rarity remains important even in dangerous areas.

---

# 22. SHADOWS SHOULD FEEL MASSIVE

Large ominous shadows should move slowly enough to communicate scale.

Avoid:

- darting,
- twitching,
- rapid fish-like swimming.

Preferred motion:

- slow lateral traversal,
- long diagonal drift,
- subtle rise/fall,
- barely perceptible translation,
- occasional downward-following motion.

Mass is communicated through slowness.

---

# 23. MOST SHADOW MOVEMENT IS HORIZONTAL

Default movement should mostly be horizontal across the scene.

Allowed exceptions:

- diagonal descent,
- slow rise,
- deepening parallel movement,
- follow-the-player-downward presentation,
- unusual event-authored paths.

---

# 24. DEPTH-FOLLOWING OMINOUS SHADOW

Support a presentation pattern where a large vague shape seems to accompany the player deeper.

Example:

```text
player descends
→ vague shape remains at distant background depth
→ partially visible for a while
→ darkness increases
→ silhouette fades / vanishes into black
```

This should be presentation-only in v1.

No chase logic.

No automatic attack.

The implication is the point.

---

# 25. DO NOT SHOW THE WHOLE MONSTER

Most major ominous silhouettes should remain incomplete and ambiguous.

Preferred progression:

```text
amorphous mass
→ partial recognizable motion
→ maybe fluke / tail
→ maybe tentacle
→ gone
```

Avoid clean full-body creature reveals.

Better examples:

- huge curved mass,
- partial tail,
- fluke,
- trailing limb,
- tentacle,
- broad body segment,
- silhouette obscured by haze,
- only a fraction of something enormous.

The player should often never know what they saw.

---

# 26. SCALE RANGE

Support multiple “large” categories.

### Clearly large but understandable
- large shark,
- whale-like silhouette,
- manta-like form.

### Extremely large / ambiguous
- several boats long,
- larger than the visible view,
- only one body segment visible,
- enormous dark mass crossing far behind terrain.

The system should support ridiculous scale.

---

# 27. RECOGNIZABLE LARGE FAUNA IS ALLOWED

Not every large shadow needs to be mysterious.

It is fine to show:

- clearly identifiable large sharks,
- large fish,
- whale-like animals,
- other normal “big ocean thing” silhouettes.

These help normalize large life.

That makes the genuinely impossible-scale silhouette more disturbing when it eventually appears.

---

# 28. SHADOWS SHOULD NOT REACT TO THE PLAYER IN V1

Ambient shadows:

- do not chase,
- do not turn toward the player,
- do not respond to flashlight,
- do not start combat,
- do not alter navigation,
- do not trigger AI.

If a future real creature/event wants reactive behavior, that system may explicitly take control.

Presentation system itself remains passive.

---

# 29. LIGHTING INTEGRATION

Underwater background visibility should naturally inherit the lighting/depth system.

As depth increases:

- distant layers become harder to perceive,
- ominous forms may become partial or disappear,
- deep-black conditions can fully swallow background silhouettes.

Do not create a separate arbitrary brightness model if the lighting system already provides suitable underwater visibility/darkness values.

---

# 30. SHADOWS IN DARKNESS

Very deep darkness should make ominous shadows less visible, not magically clearer.

The best case may be:

- a vague mass appears,
- a fluke becomes momentarily readable,
- darkness swallows it.

Do not outline creatures merely so the player cannot miss them.

Missing the sighting is acceptable.

Rarity and ambiguity are part of the design.

---

# 31. PARALLAX

Each layer should use distinct parallax behavior.

Suggested relative behavior:

```text
Far geography
→ minimal movement

Mid forms
→ moderate movement

Near background
→ strongest movement
```

Exact ratios are tunable.

Do not use parallax so aggressively that regional geography appears to slide unnaturally.

---

# 32. WORLD-RELATIVE CONTINUITY

Where practical, background composition should use world position rather than only camera-local random spawning.

This allows:

- recognizable regions,
- broad continuity while traveling,
- smoother transitions,
- deterministic regional profiles.

Do not require exact continuous rendering of every background object across arbitrary scene boundaries.

Use region/chunk/profile continuity rather than obsessive exactness.

---

# 33. BACKGROUND REGIONS / CELLS

Consider dividing global underwater space into coarse background regions/cells.

Conceptually:

```text
BackgroundRegionId
derived from world position
```

Each region can own:

- far profile seed,
- mid profile seed,
- dominant formation weights,
- ominous-shadow weighting,
- palette/profile blend.

Avoid overly small cells that create obvious repetition.

Exact region size must be tuned against world scale.

---

# 34. DATA-DRIVEN BACKGROUND PROFILES

Prefer ScriptableObject-style definitions or equivalent data assets.

Conceptually:

```text
UnderwaterBackgroundProfile
- biome tags
- depth range
- far formation weights
- mid formation weights
- near prop weights
- flora/fauna weights
- parallax parameters
- contrast/fade ranges
- ominous weighting
```

And perhaps:

```text
BackgroundFormationDefinition
- layer eligibility
- sprite/prefab
- scale range
- horizontal repeat restrictions
- topology tags
- biome tags
- animation style
- rarity
```

Exact names should fit live architecture.

---

# 35. TOPOLOGY TAGS

Coarse topology/depth scan output may translate into tags or normalized weights such as:

```text
Flat
SlopeUp
SlopeDown
Cliff
Trench
Plateau
Valley
DeepOpen
Volcanic
```

Background definitions may respond to those tags.

Do not hardwire exact sprites directly to raw scan arrays.

---

# 36. POI MODIFIER SEAM

POIs may contribute a background modifier.

Conceptually:

```text
BackgroundInfluence
- formation tag
- weight multiplier
- radius / falloff
```

Example:

```text
Undersea volcanic cave
→ Volcanic +++
→ ChimneyField +++
→ ThermalVent ++
```

This does not guarantee literal geometry alignment.

---

# 37. NO GAMEPLAY COLLISION

Background layers are presentation.

They should not:

- collide,
- block player movement,
- act as harvestables,
- become physical obstacles,
- affect buoyancy,
- receive interaction prompts.

A future specific POI/creature system may use visually related content in gameplay space, but this pass does not.

---

# 38. NO CARTOGRAPHIC KNOWLEDGE LEAK

Seeing a background cliff/chimney/shadow does not:

- reveal map coverage,
- register a POI,
- fix position,
- reveal bathymetry,
- create a node marker.

It is physical environmental observation only.

---

# 39. PERFORMANCE

This system may eventually contain many large sprites and ambient movers.

Prefer:

- pooling,
- region-based activation,
- camera-distance culling,
- deterministic composition,
- limited simultaneously animated objects,
- simple shaders,
- low-cost far-layer sprites.

Avoid:

- spawning hundreds of independent Update loops,
- per-frame global terrain rescans,
- saving decorative instances,
- unnecessary physics components.

---

# 40. LARGE SHADOW PERFORMANCE

Large ominous shadows can often be cheap:

- simple large silhouette,
- low animation complexity,
- slow transform movement,
- no physics,
- no AI,
- no collision.

Do not accidentally build a full monster simulation for a shape whose job is to make the player uncomfortable.

---

# 41. MULTIPLAYER

Background presentation is mostly client-side.

Shared authoritative inputs may include:

- world position,
- biome,
- depth/topology truth,
- active gameplay-event-driven shadow injection if future systems use it.

Ambient decorative composition may be deterministic per region without networking every prop.

Purely atmospheric shadow sightings do not necessarily need strict cross-client synchronization unless desired.

If future real creature/event uses the shadow system, that event remains authoritative.

---

# 42. SAVE / LOAD

Persist:

- no individual near-background prop instances,
- no ambient fish,
- no ordinary ambient-shadow instance.

Derive them from:

- world region,
- biome,
- topology hints,
- deterministic seeds,
- current presentation scheduler.

Persistent regional composition should be reproducible from stable background-region seeds or saved coarse profile assignments if generator-version stability requires it.

If algorithm changes would significantly alter regional identity, consider persisting lightweight regional profile identity/version rather than every sprite instance.

---

# 43. DEBUG / TUNING

Recommended debug tools:

### Region
- show current BackgroundRegionId,
- show biome/profile blend,
- show depth/topology tags,
- show POI modifiers.

### Layers
- toggle Far,
- toggle Mid,
- toggle Near,
- show parallax factor,
- show selected formation IDs.

### Shadows
- force shadow episode,
- force specific silhouette,
- adjust rarity/cooldown,
- show danger/biome weighting,
- show current episode state.

### Composition
- regenerate current region in editor/debug only,
- preview profile,
- visualize formation spawn bounds.

---

# 44. IMPLEMENTATION CHECKPOINTS

## UVIS.1 — Audit + three-layer foundation
- inspect current underwater rendering/sorting,
- inspect biome query,
- inspect world-position authority,
- inspect upcoming/current depth/topology scan seam,
- establish Far/Mid/Near background roots,
- basic parallax,
- basic fade/contrast behavior,
- world-region identity.

**STOP FOR PLAYTEST.**

Acceptance focus:
- layers clearly read at different depths,
- no foreground sorting bugs,
- broad regional persistence works.

## UVIS.2 — Far topology suggestion
- consume coarse depth/topology hints,
- generate vague cliffs/valleys/slopes/plateaus/trench forms,
- prioritize interesting composition over exact terrain,
- stable regional layout,
- biome blending.

**STOP FOR PLAYTEST.**

## UVIS.3 — Mid landmark formations
- chimney fields,
- spires,
- shelves,
- cliffs,
- rock towers,
- volcanic structures,
- POI weighting seam,
- stronger definition/parallax.

**STOP FOR PLAYTEST.**

## UVIS.4 — Near flora/fauna dressing
- background flora,
- rocks/debris,
- small ambient fauna,
- simple motion,
- no interaction,
- visit-level regeneration/pooling.

**STOP FOR PLAYTEST.**

## UVIS.5 — Ominous shadow foundation
- shadow silhouette definitions,
- large-scale slow movement,
- partial/amorphous forms,
- horizontal default movement,
- recognizable large fauna + huge ambiguous forms,
- no player reaction.

**STOP FOR PLAYTEST.**

## UVIS.6 — Shadow cadence / episodes
- 1–3 possible passes per episode,
- irregular timing,
- long quiet periods,
- biome/danger weighting,
- rare overall cadence,
- force/debug controls.

**STOP FOR PLAYTEST.**

Acceptance focus:
- shadow system does not feel periodic,
- sightings remain surprising,
- long silence is common.

## UVIS.7 — Deep-follow / lighting integration
- optional downward-follow presentation path,
- silhouettes fade into darkness,
- underwater lighting/depth values affect readability,
- no magical outline compensation,
- performance pass.

**STOP FOR PLAYTEST.**

## UVIS.8 — Persistence / transition / hardening
- region continuity,
- biome transition blending,
- generator-version considerations,
- multiplayer presentation review,
- pooling/culling,
- save/load regression,
- final tuning.

**STOP FOR FINAL PLAYTEST / FREEZE.**

---

# 45. MINIMUM PLAYTEST SCENARIOS

### Shallow shelf
- broad distant terrain,
- more near flora/fauna,
- low ominous frequency.

### Open bluewater
- sparse geography,
- broad distant forms,
- strong negative space.

### Volcanic region
- chimney fields,
- jagged mid forms,
- POI influence can increase vent language.

### Deep trench
- huge vague walls,
- very slow far parallax,
- higher ominous weighting,
- darkness swallows distant forms.

### Return visit
- broad Far/Mid regional identity remains recognizable.

### Biome crossing
- background profile changes gradually, not abruptly.

### Recognizable large shark
- clearly reads as a large shark,
- remains background-only.

### Huge ambiguous shadow
- only partial body visible,
- very slow motion,
- may exceed screen size.

### Shadow episode
- 1–3 irregular passes,
- then long quiet period.

### Depth-following shadow
- vague large form appears to continue downward,
- eventually disappears because darkness becomes too strong.

---

# 46. ACCEPTANCE TESTS

1. Underwater scenes support Far/Mid/Near background layers.
2. Far layer communicates huge geographic scale.
3. Mid layer communicates readable environmental forms.
4. Near layer provides non-interactive flora/fauna scene life.
5. Existing sorting rules remain intact.
6. Far/Mid composition has deterministic regional identity.
7. Near decoration may regenerate between visits.
8. Biome influences background content.
9. Depth/topography influences background content.
10. Backgrounds use coarse topology suggestion, not exact terrain duplication.
11. Biome transitions blend rather than pop.
12. POIs can modify visual weighting without requiring exact alignment.
13. Background objects remain visibly non-interactive.
14. Ambient near fauna can move without gameplay behavior.
15. Ominous-shadow system supports rare episodes.
16. Shadow cadence is not a predictable short timer.
17. Episodes may contain roughly 1–3 passes.
18. Long quiet periods of 10+ minutes are possible/common.
19. Dangerous biomes may increase weighting without making shadows constant.
20. Most major ominous forms remain partial/ambiguous.
21. Clearly recognizable large fauna is also supported.
22. Extremely large silhouettes may exceed screen dimensions.
23. Shadow movement is slow and massive.
24. Most shadow movement is horizontal.
25. Exceptional vertical/diagonal/depth-following paths are possible.
26. Ambient shadows do not react to the player.
27. Deep darkness may swallow shadows completely.
28. Background visuals do not reveal cartographic knowledge.
29. Background visuals have no collision or interaction.
30. System remains performant through pooling/culling/low-cost far visuals.
31. Existing water/biome/world-position systems remain intact.

---

# 47. EXPLICIT NON-GOALS

Do not turn this pass into:

- full underwater creature AI,
- Kraken implementation,
- combat,
- POI generation,
- seafloor resource generation,
- exact terrain mirroring,
- volumetric fog simulation,
- water-condition/turbidity simulation,
- background collision,
- map discovery,
- dynamic ecosystem simulation.

This pass is visual depth, regional identity, ambience, and fear.

---

# 48. FUTURE EXTENSIONS

Leave clean seams for:

- real creatures injecting shadow passes,
- Kraken tentacle/event silhouettes,
- whale migrations,
- giant fauna,
- underwater ruins,
- distant submarine/boat silhouettes,
- dynamic POI foreshadowing,
- biome-specific schools,
- vent plumes,
- sediment clouds,
- turbidity,
- event-driven background changes,
- large deep-sea organisms reacting to light.

---

# 49. BOSUN DELIVERY EXPECTATIONS

After every checkpoint report:

1. Exact files inspected
2. Exact files modified
3. Exact new files
4. Existing sorting/layering system used
5. Existing biome query used
6. World-position source used
7. Depth/topology query used
8. Background-region strategy
9. Far/Mid/Near hierarchy
10. Parallax approach
11. Formation/profile data design
12. POI weighting seam
13. Ambient fauna implementation
14. Shadow definition system
15. Episode scheduler
16. Lighting/depth integration
17. Pooling/culling/performance
18. Multiplayer assumptions
19. Save/version implications
20. Regression tests
21. Any live-project discrepancy from this handoff

Do not silently create a second terrain, biome, or depth-authority system.

---

# 50. FINAL DESIGN SUMMARY

The intended underwater visual stack is:

```text
FAR
huge vague geography
cliffs / valleys / trench walls / plateaus

MID
readable environmental forms
chimneys / spires / shelves / rock towers

NEAR BACKGROUND
flora / small fauna / rocks / environmental life

PLAYABLE WORLD
actual interactive terrain / resources / POIs / creatures
```

And the ominous-shadow philosophy is:

```text
Rare
Slow
Massive
Mostly partial
Often ambiguous
Usually harmless
Sometimes maybe not
```

The best sighting may be:

```text
vague dark mass
→ something like a fluke
→ a trailing tentacle
→ gone
```

The player should not always know what they saw.

That uncertainty is the feature.

---

**End of Underwater Background Visuals handoff.**
