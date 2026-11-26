# Roadmap: turn HOCKEY into a commercial game

## Context
HOCKEY is a Unity 6 (6000.3, URP) robot air-hockey game: 1x1 and 3x3, 5 robots defined as data
(`Assets/Resources/Robots/*.asset`, `RobotSpec.cs`), 3 arenas applied at runtime to one rink
(`ArenaThemeImpl.cs`: Clássica, Gelo, Fábrica), AI, local split screen, P2P online by IP
(`NetSession.cs`, NGO + UTP), tournament mode, uGUI menus built in code (`GameUI.cs`), ~3.4k lines
of game code plus tests in `Assets/Tests`. It works, but it ships as `productName: HOCKEY`,
`companyName: DefaultCompany`, with school-project art (PSD buttons, `Sem Título-1.psd`), the legacy
built-in font, the old Input Manager, mixed PT/EN code names (`moverp1`, `marcadorazul`), no README,
and online that needs port forwarding.

Goal: a premium game (one-time price) sold on **Steam (Windows/macOS) and mobile (iOS/Android)**,
**Portuguese (PT-BR) only**, made by **one developer on a small budget** (~US$1–5k).

Decisions from the user: platforms PC + mobile; solo, small budget; PT-BR only; premium.

Two risks to keep in view:
- Premium mobile sells poorly in Brazil; plan the mobile price low (R$10–20) and treat it as a
  second launch after Steam.
- PT-only on Steam limits reach. Keep all text in a string table from day one so English can be
  added later for little cost.

---

## 1. Name and brand

**Recommended name: ROBÓQUEI** (robô + hóquei). Short, says what the game is, easy to say in
Brazil, and a unique search term. Backups: *Disco Voltz*, *Rinque de Lata*, *Choque no Rinque*.
Before committing: search INPI (Brazil), USPTO, Steam and both app stores, and check that the
domain and @ handles are free.

Brand kit (do it yourself + one paid logo commission):
- Logo: wordmark plus an icon (the Ômega robot head or a disc with a lightning bolt). Commission
  one designer (~US$300–600) with 3 rounds. Must read at 32px (app icon, Steam capsule).
- Palette: keep the existing team red `#F24D4D` / blue `#4D94FF` and the UI cyan `#21D4ED`
  (`GameUI.cs` `Accent`), on a dark navy `#0A1221`. Add one warm "spark" yellow for highlights.
- Type: one display font (e.g. a free-license heavy sans like *Bebas Neue* or *Russo One*) and one
  UI font (*Inter* or the OpenSans already in `Assets/ThirdParty/Fonts`). Check the OFL licenses.
- Tone: sporty, loud, robot-workshop humor; robot personalities (Faísca nervous and fast,
  Trator slow and heavy).
- Deliverables: `docs/brand/brand-guide.md`, logo files, Steam capsules (header 920x430, small
  462x174, main 1232x706, vertical 748x896, library hero 3840x1240), app icons for all sizes,
  press kit page.

## 2. Better assets (solo, small budget)

Pick **one art direction and stick to it**: clean stylized low-poly with bright emissive lights
("toy robot league"). It is the cheapest to make look good and runs well on mobile.

| Area | Now | Plan | Rough cost |
|---|---|---|---|
| Robots | one shared chassis + body kits (`RobotLoadout.cs`) | 5 distinct models through `RobotSpec.model`; Asset Store robot kit, or one commissioned base model + kitbash | $150–600 |
| Rink / arenas | `arena.fbx`, `chao arena.fbx`, school-made models | Modular rink kit (boards, glass, goals, stands) so each arena reuses it | $50–200 |
| Crowd | capsule fans (`CrowdSpawner.cs`) | Low-poly crowd pack with GPU instancing | $30–80 |
| UI | PSD buttons, built-in font, code-drawn rounded rects | Redo UI in brand style with TextMeshPro; 9-sliced sprites; icons per tool and stat | Own work + icon pack $20 |
| VFX | `FxPool.cs` / `RobotFx.cs` (new) | Goal explosion, puck trail, sparks on hits, boost trail | Own work / $30 pack |
| Music | `Music.wav` (source unknown); copyrighted mp3s already deleted | 3–5 licensed tracks (menu, match, tension, result) from a royalty-free library or one commission | $100–500 |
| SFX | `ImpactSound.cs`, `GameSfx.cs` | Licensed SFX pack + crowd reactions + announcer voice in PT-BR (Fiverr) | $50–200 |

**License audit before anything else** (put results in `docs/licenses.md`): `Music.wav`, everything in
`Assets/2D`, `Assets/3D`, `Assets/ThirdParty`, `Assets/UnityTechnologies/Adam Character Pack`
(likely unused; remove), fonts. Anything without a clear commercial license gets replaced.

Remove unused content to cut build size: `New Terrain 3.asset`, `terrain/`, Adam Character Pack,
`InitTestScene*.unity`, unused PSDs (`Sem Título-1.psd`), `PUCK v1.ipt.max`.

## 3. Arenas

Today an arena is a code branch inside `ArenaThemeImpl.cs` that recolors one rink. That does not
scale. Change to **data-driven arenas**:
- `ArenaDef` ScriptableObject (like `RobotSpec`): id, display name, floor material, physics material,
  puck drag, robot grip (`moverp1.Grip`), light colors, post-processing profile, music track,
  optional obstacle prefab, rink size override.
- Each arena's props live in an additive scene or prefab loaded on match start (unloaded after), so
  memory only holds the current arena (matters on mobile).
- `ArenaTheme.Apply` reads the `ArenaDef` instead of `if (Current == 1)` branches; keep the
  three current arenas as the first three assets.

Arena line-up for launch (8), each with one gameplay twist:
1. **Clássica** – wood, no twist (tutorial arena).
2. **Gelo** – existing; low grip, fast puck.
3. **Fábrica** – existing; four pillars.
4. **Ferro-Velho** (scrapyard) – bumpers on the boards that bounce the puck harder.
5. **Esteira** (conveyor) – floor strips that push robots and puck sideways.
6. **Magnética** – a center magnet that pulls the puck every few seconds.
7. **Noturna** – dark rink, light only from robot headlights and the puck glow.
8. **Praia** (Copacabana-style night league) – sand edges slow robots; Brazil flavor.

Post-launch free update: 2 more arenas + 2 robots (keeps reviews and the Steam page active).

## 4. Game content and features needed to sell

- **Career / league mode** (PT: "Liga"): build on the existing tournament ladder in `GameUI.cs`;
  divisions, unlock robots and arenas by winning, stars per match.
- **Tutorial**: a 2-minute guided first match.
- **Controls**: move to the **new Input System** (now `activeInputHandler: 0`, old Input Manager).
  Needed for gamepads on Steam (Steam Deck verified) and touch on mobile (virtual stick + action button).
- **Save system**: progress, unlocks, settings (JSON in `persistentDataPath`; Steam Cloud on PC).
- **Settings**: graphics quality, resolution, volume per channel (mixer exists: `MusicVol.mixer`),
  key rebinding, accessibility (colorblind team colors, screen shake toggle).
- **Online**: P2P by IP will not pass for a commercial release (players cannot port forward, and it
  does not work on mobile). Replace the transport with **Steam Networking + Steam lobbies** on PC
  (free, invite friends) and keep local / vs-AI only on mobile at launch. Unity Relay + Lobby is the
  cross-platform alternative if PC–mobile cross-play is wanted later (paid by usage).
- **Steam features**: achievements (~20), Steam Cloud, Remote Play Together (free couch online for
  the split-screen mode — big win for a local multiplayer game), rich presence.
- **Mobile**: one-player vs AI + league + local 2 players on one tablet; no split screen on phones.

## 5. Performance

Targets: PC 144 fps at 1080p on a mid GPU, Steam Deck 60 fps; mobile 60 fps on a 2021 mid-range
Android (e.g. Galaxy A52) and iPhone 11, under 300 MB download, <3 s warm load.

Measure first: Unity Profiler + Frame Debugger on a 3x3 match in each arena, saved as a baseline in
`docs/performance.md`. Then:
- **Scripts**: remove per-frame polling (`marcadorazul.cs` / `marcadorverm.cs` / `mostradado.cs`
  update text every `Update`; switch to events from `MatchManager`). Replace
  `Resources.FindObjectsOfTypeAll` and `FindObjectsByType` scans (`ArenaThemeImpl.cs:64`,
  `ImpactSound.cs:34`) with references set once. Check GC allocs per frame = 0 during play.
- **UI**: `GameUI.cs` labels rebuilt every frame through `dyn` list — update only on change; split
  HUD into its own Canvas so the menus do not rebuild with it; TextMeshPro instead of legacy Text.
- **Rendering (URP, `Assets/Settings/OmegaURP.asset`)**: separate quality tiers for PC / mobile;
  on mobile turn off SSAO, limit additional lights, lower shadow distance and cascades, bake
  arena lighting (lightmaps + light probes) and keep only robots and puck dynamic; enable SRP
  Batcher and GPU instancing for crowd and seats; LODs on the stands; texture compression ASTC on
  mobile; atlas UI sprites.
- **Split screen**: two cameras render the whole scene twice; lower render scale per camera and cull
  the crowd for the second camera.
- **Physics**: check fixed timestep and solver iterations; continuous collision only on the puck.
- **Audio**: compress music as streaming Vorbis, short SFX as decompress-on-load, mono 3D sounds
  (`Music.wav` is 7.7 MB uncompressed).
- **Build**: IL2CPP, managed stripping, remove unused packages from `Packages/manifest.json`
  (`com.unity.ai.inference`, `com.unity.ai.assistant`, `com.unity.2d.*`, `com.unity.analytics` legacy,
  `terrain`, `cloth`) and check the build report for the biggest assets.
- **Mobile-specific**: target 60 fps with `Application.targetFrameRate`, thermal test of a 10-minute
  session, Adaptive Performance package on Android.

## 6. Code cleanup (makes everything above easier)

- Rename PT/legacy names to clear ones, one at a time with tests: `moverp1` -> `RobotMotor`,
  `marcadorazul` / `marcadorverm` -> one `ScoreBoard`, `puck` -> `Puck`, `mostradado`.
  Move `Assets/3D/unidade/moverp1.cs` into `Assets/scripts/Robots`.
- Move all player-facing text into a Unity Localization string table (PT-BR only now, EN later).
- Split `GameUI.cs` (605 lines) into one screen per class.
- Add an assembly definition for game code (faster compiles, tests already have `HockeyTests.asmdef`).
- CI: run EditMode/PlayMode tests and a build on every push (GitHub Actions + GameCI, or Unity
  Build Automation).

## 7. Documentation (`docs/`)

- `README.md` (root): what the game is, how to open, run, test, build.
- `docs/gdd.md`: game design document — rules, robots and stats, tools, arenas, modes, progression.
  Build on `docs/superpowers/specs/2026-11-26-robots-and-online-design.md`.
- `docs/architecture.md`: scene layout, `MatchManager` state machine, robot pipeline
  (`RobotSpec` -> `RobotLoadout` -> `RobotTools`), arena pipeline, networking, UI.
- `docs/how-to/add-a-robot.md`, `add-an-arena.md`, `release-build.md`.
- `docs/art-bible.md` and `docs/brand/brand-guide.md`.
- `docs/performance.md` (targets + baseline numbers), `docs/licenses.md`, `CHANGELOG.md`.
- Player-facing: in-game "Como jogar" screen, Steam page text, store FAQ, privacy policy (needed for
  the app stores even with no data collection).

## 8. Business and release

- **Company**: open an MEI or ME in Brazil to receive Steam/App Store money; Steam app fee US$100;
  Apple Developer US$99/year; Google Play US$25 once.
- **Price**: Steam ~R$27–37 (US$9.99 equivalent), mobile R$14,90; launch discount 10–15%.
- **Marketing (solo)**: Steam page up as early as possible (wishlists), short GIF/clip posts of
  robot hits and goals (TikTok, Reels, X, r/IndieDev), a free demo for Steam Next Fest, contact
  Brazilian YouTubers / streamers, BIG Festival and Gamescom Latam showcase submissions.
- **Ratings**: IARC (free through the stores) — the game should be rated Livre/E.

---

## Roadmap (solo, ~12 months; dates from Dec 2026)

| Phase | When | Goal | Done when |
|---|---|---|---|
| 0. Foundation | Dec 2026 (4 wks) | License audit, remove unused assets/packages, README + architecture doc, CI, performance baseline, name + trademark check | Clean repo, CI green, baseline in `docs/performance.md` |
| 1. Brand + art direction | Jan 2027 (4 wks) | Final name, logo commission, palette, fonts, art bible, one "beauty" arena mock | Brand guide + logo files |
| 2. Tech refactor | Feb–Mar 2027 (6 wks) | New Input System (gamepad + touch), `ArenaDef` data arenas, renames, string table, save system, settings, UI on TextMeshPro | Old 3 arenas run from data; all tests pass |
| 3. Vertical slice | Apr–May 2027 (8 wks) | 1 arena + 2 robots at final quality, new UI, VFX, music + SFX, tutorial; PC + Android builds hit perf targets | Slice looks like the final game; shown to 10 playtesters |
| 4. Steam page | Jun 2027 | Capsules, trailer from the slice, page live, start wishlists | Page public |
| 5. Content production | Jun–Sep 2027 (16 wks) | All 5 robots + 5 new arenas, league mode, achievements, Steam lobbies / Remote Play Together | Content complete (alpha) |
| 6. Demo + Next Fest | Oct 2027 | Free demo (2 arenas, 2 robots), join Steam Next Fest | Demo live; wishlist goal 5–10k |
| 7. Beta + polish | Oct–Nov 2027 | Bug fixing, balance, Steam Deck check, accessibility, PT text review | No known crash; Deck Verified submitted |
| 8. Steam launch | Dec 2027 | 1.0 on Steam (Win + macOS) | Released |
| 9. Mobile launch | Feb–Mar 2028 | Touch UI tuned, mobile quality tier, store pages, TestFlight / Play closed test | iOS + Android released |
| 10. Post-launch | 2028 | Free update (2 arenas, 2 robots), English localization if numbers justify it | — |

Budget check (~US$1–5k): logo $300–600, robot/rink/crowd packs $250–900, music $100–500,
SFX + announcer $50–200, store fees ~$225, legal/MEI setup small. Leaves room for one trailer edit
or a second art commission.

## Next concrete step
Start Phase 0: write `README.md` and `docs/architecture.md`, do the license audit, record the
performance baseline, and remove unused packages/assets (each in its own commit, tests green).

## Verification
- Each phase has a "done when" check above; record it in `CHANGELOG.md`.
- Tests: run the EditMode/PlayMode suite in `Assets/Tests` after each refactor step (CI does it).
- Performance: compare Profiler captures against the Phase 0 baseline on PC, Steam Deck (or a
  similar low-power PC) and the chosen Android/iPhone test devices.
- Brand/art: playtest feedback on the vertical slice and Steam page click-through rate.
