# Piloting land performance correction

## Changes

Removed the close-view CPU loop that measured 53.9 ms per rebuild and the circular view's CPU terrain raster that measured 10.8 ms. Both views now use `PilotingLandGpuRenderer` and the resource shader `Shaders/Piloting/PilotingLand`.

The immutable geographic height field is copied/uploaded once on first use, shared by both views, and replaced when the field changes. Boat movement changes shader projection parameters; the GPU samples the height texture. Interpolation matches the CPU geographic field, including the wrapped last column and finite latitude. The first opening or a changed field still requires an initial height upload.

The main view retains its physical viewport and raster resolution; the circular view retains its configured nautical-mile range, smoothing, weather fade, boat marker, harbor decorations and restrained wave marks. Circular decorations remain a small CPU overlay texture; there is no per-pixel CPU terrain sampling in either renderer.

Close harbor search now uses half the viewport diagonal without adding the wider guidance range, runs at most twice per second, and caches successful/failed berth solutions. Results invalidate when field, sea level, boat, scale, settings reference or meaningful hull bounds change; a ten-second expiry also permits in-place settings edits and failed solves to refresh. Berth bounding boxes must intersect the close viewport before drawing. The controller's already-solved active harbor berth remains eligible even when its shore/node is outside the viewport. Duplicate active outlines are suppressed. This does not change actual docking rules or harbor guidance range.

Profiler markers remain for the next capture. `Piloting.Land.GpuRaster` measures CPU command submission, not GPU execution time. `Piloting.Land.HeightUploadOnce` isolates initial uploads. The removed near-field CPU sampling/upload labels no longer appear.

## Inspector tasks

None. No saved scenes, prefabs, materials, Inspector references or game project settings changed. New code/shader resources load automatically.

## Verification

- Production C# compilation passed.
- Direct3D11 GPU tests passed 16,396 assertions for CPU/GPU coastline agreement, X seam, bounded latitude, rotated projection, circular clipping, shared height data and resource cleanup.
- Existing circular-view checks passed 379 assertions against the actual GPU render texture, including visible land, heading, berth/tower markings, full-fog suppression and cleanup.
- Existing harbor presentation checks passed 502 assertions.
- Checks passed in gamma mode and again in the game's linear color mode. The shader preserves the previous CPU texture's display-space colors.
- Isolated raster submission averaged approximately 0.014–0.019 ms at 512×192. This is CPU submission overhead in the harness, not an end-to-end game frame time or a GPU timing measurement. The actual game still needs a before/after check.

## Quick play check

Open piloting in open water where the original lag occurred. Then check a nearby coastline/harbor, turn the boat, and close/reopen piloting. Main-view land and docking outlines should align; the circular view should keep its broader range. New field uploads should not recur simply because the boat moves.

If lag remains, save another profiler capture to the same folder as before; no Hierarchy UI work is necessary. Commit this checkpoint once the in-game result is good.
