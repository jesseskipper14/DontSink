# Feature follow-ups

## TODO — Deterministic building-relative key NPC placement

Requested 2026-10-06. Place key service buildings organically within the deterministic node settlement layout, with each NPC's spawn anchor authored relative to its building/station. Moving the building should move the NPC's spawn location with it. Reuse the existing AgentSpawnPoint/AgentDefinition/behavior composition rather than independent world-coordinate placements.

Derive persistent NPC identities from the canonical node ID and service/building role, independently of position. Preserve repeatability across visits/save-load, one required service NPC per node, ground/platform support, door/ladder clearance and authority-controlled spawning. Surveyor placement is the first use case. This is a follow-up; current scene authoring remains manual.

## TODO — POI star-clue reference charts

Requested 2026-10-06. Keep this separate from the current Surveyor/local-island checkpoint.

1. Choose a real POI in the authoritative world map data.
2. Generate a star-map clue image from the celestial field corresponding to that POI. Include useful recognizable stars/landmarks so the player can interpret the location.
3. Store/display the generated image on a physical reference chart. Preserve its POI/celestial-field association and reproducible visual identity through save/load.
4. Let the exact player hold the chart up and view it outside the Mapping Table, using the existing Hands/item-view interaction patterns.

The chart stays non-integrating: viewing/holding/acquiring it must not reveal geography, grant a POI marker, snap believed position or display a hidden true-coordinate answer. Host-issued item data is shared/persistent; held viewing is local presentation. Verify repeatable generation, correct celestial registration, carried/dropped/restored chart identity, image readability and multiplayer requester ownership.
