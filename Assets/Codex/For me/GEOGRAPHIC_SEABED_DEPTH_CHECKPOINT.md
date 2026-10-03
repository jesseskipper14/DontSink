# Geographic seabed depth checkpoint 🍌

## Result

Streamed BoatScene seabed now uses world topography as its broad depth signal. Forecast positions project physical X offset from the boat through its geographic heading and world/physical scale. The existing topography sampler provides horizontal wrapping and finite north/south sampling. BoatScene X remains journey progress rather than a fixed east/west world axis.

The terrain profile centralizes depth mapping. Normalized water depth is `(effective sea level - topography height) / effective sea level`, clamped to 0–1. Coast/land maps to the shallow end while retaining an underwater base floor. The new default curve requests 15 units at 0, 60 at .25, 220 at .6, and 500 at 1. Final terrain respects the existing maximum depth.

Each loaded chunk freezes its two macro depth knots. Uncommitted regions use the latest geographic forecast when they first load. Committed knots remain in session history after geometry unloads. Turning, reversing, geographic warp, and returning to an unloaded region cannot rewrite committed geometry. Neighbor and disjoint-region constraints make future gaps connect safely. Quintic interpolation smooths macro changes; half the slope budget is reserved for macro depth, half for rolling detail.

Collider adjacent points now extend the chunk's own local tangent, so changes to uncommitted neighbors cannot change its collision normals on reload. Water fill continues to extend below the maximum supported depth.

F4 shows **Geographic depth: active/fallback**, the requested target depth, and **Seabed under boat: Y**. Missing topography, invalid sea level, disabled geographic depth, or unavailable navigation projection use the existing base-depth fallback. The field and mapping are captured for the voyage; changing a profile or regenerating topography is intended to take effect on a fresh voyage.

## Inspector review — no assets edited

On your existing `Assets/Defs/WorldMap/SeaBiomeGenType/BoatTerrainProfile.asset`:

1. Confirm **Use Geographic Depth** is enabled. It defaults on.
2. Review the new **Geographic Depth** curve and **Maximum Depth**. Default mapping ends at 500; you can tune the curve.
3. Keep your current chunk width, interest radius, and maximum slope for the first regression test. They determine how far you sail before seeing a newly requested depth.

No new scene references, components, or prefab wiring are required. Existing terrain profile and streamer assignments remain in place. No NodeScene or map-table layout changes were made.

## Testing

Production runtime compilation passed. Unity harness: **5,784 assertions passed** (4,704 terrain, 21 voyage, 941 wrapping, 47 camera, 71 pinning). New checks cover shallow/deep geographic requests, immutable chunk reloads, negative/disjoint regions, shared endpoints, total slope caps, and the production topography sampling path. Unrelated navigation/actor hosts are adapted in the harness; live BoatScene projection remains a Play Mode check.

1. Start/re-enter a BoatScene voyage. F4 should report geographic depth **active**. Check actual seabed Y against the requested target; small rolling differences are expected.
2. Pick contrasting shallow/deep regions with the map coordinate debug tools. Warp geography, observe F4 target change, then sail forward into new terrain. **Warp does not rebuild the existing floor.** With default 128-wide chunks and radius 256, expect to leave a few hundred units of initially committed ground before changes begin. Large depth differences also need a substantial slope-limited transition distance.
3. Turn or warp again before entering more new chunks. Future terrain should follow the new target. Reverse to old terrain: its surface should match its prior shape, including after unload/reload.
4. Cross the horizontal world seam. Target sampling should wrap; the physical strip and seabed must continue smoothly.
5. Deploy bell/anchor and drop rope on a transition and chunk boundary. Check seabed contact and saved-object correction after load. Run a short NodeScene regression too.

## Limits / next checkpoint

Committed history is session-only. The handoff explicitly permits approximate seabed regeneration across reloads; existing restoration coverage and embedding correction remain responsible for safe restored objects. Raw collider points are not saved.

This is the macro-depth checkpoint. Terrain feature directives, island presentation, geographic land obstruction, and later resource streaming remain separate passes. No networking transport, arrival implementation, or floating origin was added. Wrapping continues to supersede the older handoff's no-wrap requirement.

Commit after the live regression checks pass.

## Startup correction

Initial terrain could commit the 120-unit fallback before the scene bridge resolved the newly spawned boat's piloting state. The bridge now exposes a synchronous projection refresh, called before the first terrain samples commit. Startup also commits the chunk beneath the boat before extending toward preload edges, so a deeper distant sample cannot pull the starting floor away from the local target. No Inspector changes are required. Re-enter BoatScene to test this fix: existing committed chunks intentionally are not rewritten. Rolling detail may place the floor a few units above/below its macro target.

## More aggressive large depth changes

Per the requested refinement, the profile now exposes **Large Depth Change Slope Degrees** (default 40°) and **Large Depth Change Threshold** (default 100 units). Larger target/history mismatches unlock more of this slope budget; smaller mismatches use less. Rolling detail retains its ordinary budget. The interpolation's peak gradient plus detail stays within the larger configured safety cap. The solver can also use that cap to bridge already committed disjoint steep regions. Committed geometry remains unchanged.

These are new code defaults, not edits to your profile asset. Review them in the Inspector; no additional references are required. Re-enter BoatScene for the startup correction and fresh transition settings. The original 20°-only transition description above is superseded by this refinement.
