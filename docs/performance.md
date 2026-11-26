# Performance

## Targets (commercial release)

| Platform | Target |
|---|---|
| PC, mid GPU, 1080p | 144 fps (6.9 ms) |
| Steam Deck | 60 fps locked (16.6 ms) |
| Mobile: mid-range Android 2021 (Galaxy A52 class), iPhone 11 | 60 fps, no thermal throttling over a 10-minute session |
| Download size | under 300 MB (mobile) |
| Load to menu | under 3 s, warm start |

## Baseline: 2026-11-26 (Editor Play Mode, Apple M5, 2611x756 Game view, quality "Very High")

These numbers come from the Editor, not a player build, so they include Editor overhead. They are
for comparing before and after, not for comparing against the targets. Take player-build numbers
with the Profiler attached during Phase 3.

| Measure | Menu | 1x1 match, 1st | after 1 rematch |
|---|---|---|---|
| Frame time (smoothed) | 12.8 ms* | 2.6 ms | 5.1 ms |
| Batches (draw calls) | 280 | 375 | 764 |
| SetPass calls | 100 | 51 | 51 |
| Triangles per frame | 6.5 M | 6.5 M | 6.7 M |
| Shadow casters | 133 | — | — |

\* During the countdown while the Editor was busy. Not representative.

Scene contents: 166 renderers (1x1), 7 lights (3 with shadows), 3 rigidbodies, 59 colliders,
2 cameras in split screen, **1,821 crowd fans at 1,732 triangles each**, 1 canvas.
Managed heap: about 530 MB allocated, 357 MB for the graphics driver (Editor).

URP asset `OmegaURP`: render scale 1, MSAA 4x, HDR on, SRP Batcher on, **shadow distance 4000
and 4 cascades**, up to 8 additional lights. VSync on and no target frame rate.

## Findings, worst first

1. **Fixed: robot parts piled up every match.** `RobotLoadout.Apply` destroyed the old `Loadout`
   at the end of the frame, but `Detail()` ran in the same frame and copied the old copies again.
   Each robot went from 9 body copies to 29 and then 49, adding 20 per match. Draw calls doubled
   (375 to 764) after a single rematch, and kept growing in long sessions. The fix detaches the old
   loadout first. Regression test: `Assets/Tests/LoadoutReapplyTest.cs`.
2. **The crowd is about 95% of the triangles.** 1,821 fans × 1,732 tris ≈ 3.2 M per pass. It's
   drawn for the main view and the shadow map, and the shadow map has 4 cascades. In split screen
   it's drawn again for the second camera. Fixes, in order:
   - Fans don't cast shadows (`ShadowCastingMode.Off` in `Graphics.RenderMeshInstanced`'s `RenderParams`).
   - Add a low LOD for fans: about 300 tris, or an impostor card for the back rows.
   - Lower the fan density on mobile.
3. **The shadow settings are far bigger than the rink.** The rink is about 2,000 units long, but
   the shadow distance is 4,000 with 4 cascades. Fitting the distance to the rink with 2 cascades
   should save a lot of GPU time on every platform.
4. **MSAA 4x + HDR at render scale 1** in split screen. On mobile, use FXAA or MSAA 2x, no HDR,
   and render scale 0.8.
5. **Per-frame CPU work that should be event-driven.** `marcadorazul` / `marcadorverm` /
   `mostradado` update text every frame, and `GameUI` refreshes its dynamic labels every frame.
   `MatchManager.Find`, `ArenaTheme.Cache` and `ImpactSound` scan with
   `Resources.FindObjectsOfTypeAll` / `FindObjectsByType`. Those scans only run at startup, so
   they're fine for now, but they should become direct references.

## How to measure

- Editor: Window > Analysis > Profiler and Frame Debugger. Play a 1x1 and a 3x3 match in each arena.
- Player: build with `BuildMac.Development`, attach the Profiler, and take a 60-second capture
  of a 3x3 match.
- Record every new capture in this file: date, build, device, and the same columns as above.
