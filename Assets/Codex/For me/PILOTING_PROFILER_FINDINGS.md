# Piloting profiler findings — 2026-10-05

Read `Assets/Codex/Profiler/profiler_2026_10_05.data` through Unity 6000.0.65f1's profiler APIs in the isolated harness. The binary capture was not modified. Extracted per-frame timings and inclusive marker aggregates are in ignored `Temp/CodexPhase7/piloting-capture-frames.csv` and `piloting-capture-summary.csv`.

## Result

The added close-range CPU land raster is the measured bottleneck. The initial suspicion that berth solving dominated was wrong for this capture.

| Work | Average per recorded piloting frame | Maximum |
| --- | ---: | ---: |
| Close land sampling | 53.865 ms | 55.559 ms |
| Existing circular view build | 10.773 ms | 11.188 ms |
| Close texture upload | 0.266 ms | 0.469 ms |
| Close harbor search | 0.207 ms | 0.331 ms |
| Close berth solve block | 0.00038 ms | 0.00170 ms |

There are 300 main-thread frames in the loaded capture. 86 contain the new piloting markers; those average 112.56 ms and peak at 149.25 ms. The other 214 average 6.25 ms. These are recorded CPU frame durations, not a GPU measurement or a controlled standalone-build benchmark. Instrumented near-field rebuilding and circular rebuilding occur on every one of those 86 piloting frames: the slow frame already exceeds their nominal refresh interval, so the intended refresh throttling provides no relief.

Markers are inclusive; parent and child times must not be added together. Additional simulation/terrain/physics work is present, but its whole-capture aggregates alone do not establish whether it causes the slowdown or increases because long frames require more fixed-step work. GC allocation samples also occur throughout the capture; their global count does not attribute them to the raster.

The almost-empty berth timing does not establish that solving is cheap near every harbor. It does establish that it is not the cause of this particular lag capture.

## Recommended correction

Replace the close-view per-pixel CPU loop with a cached geographic height texture sampled by the GPU. Supply the current viewport-to-geographic projection, sea level and visibility as draw parameters; preserve horizontal wrapping and bounded latitude. Build/upload the source height texture when the field changes, rather than rebuilding a colored texture as the boat moves. Apply the same approach to the circular view's land mask, preserving its range/weather fade and harbor decorations.

Keep harbor geometry caching and tighter visibility filtering as a separate small cleanup; they should not distract from the measured raster bottleneck. Compare a new capture in the same scene after implementation, using the current labels as baseline. No rendering behavior, Inspector data or performance implementation was changed during capture analysis.
