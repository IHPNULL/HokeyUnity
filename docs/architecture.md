# Architecture

How the game's code fits together. It covers the state as of the "Online play, peer to peer by IP"
commit plus the robot effects work (`FxPool`, `RobotFx`).

## One scene, everything at runtime

The build has one scene, `Assets/Scenes/MENU.unity`. It holds:

- **`JOGANDO`**: the arena. Includes the rink (`arena`), the stands, the scoreboard, lights, the puck
  (`PUCK`) and one robot per team (`unidadeverm` = red, `Unidadeazul` = blue). Each robot has its own
  camera, used for split screen.
- **`Game`**: the root object holding `MatchManager` and `GameUI`, plus the orbiting `Menu Camera`.
- **`Audio Source`**: the music player. `MatchManager` finds it by name.

Menus, the HUD, the extra 3x3 robots, the rink's rounded boards and floor, the robot body kits, the
particle effects and every sound effect are built in code when the scene starts. The one-shot editor
scripts in `Assets/Editor` set the scene up once: `GameSetup`, `UrpLookSetup`, `GoalBuilder`,
`ColliderFix` and `UrpMigration`.

```
MENU.unity
├── Game ── MatchManager (match flow) ── NetSession (online, added at runtime)
│        └─ GameUI (menus + HUD, uGUI built in code)
├── JOGANDO (arena; also the menu backdrop)
│   ├── arena ── Plane (floor collider), policarbonato (old boards, hidden), gol / gol (1) (goal triggers)
│   ├── unidadeverm / Unidadeazul ── moverp1, RobotTools, Camera   (+ 2 clones each for 3x3)
│   ├── PUCK ── puck.cs (scores goals)
│   └── Lighting, cenario externo/placar, stands (CrowdSpawner)
└── Audio Source (music)
```

## Match flow: `MatchManager`

A best-of-3-periods state machine (`Assets/scripts/Game/MatchManager.cs`):

```
Menu → Countdown → Playing ⇄ GoalPause
                      ↓
                 PeriodBreak → Countdown …     (any state ⇄ Paused)
                      ↓
                   Result → Rematch / next tournament match / Menu
```

- A period ends when its clock runs out or a team reaches `GameConfig.goalsPerPeriod`. A tie goes to
  sudden death. The first team to win 2 periods wins.
- Goals are counted by the puck's trigger hits. `puck.cs` increments the static counters
  `marcadorazul.pontosazul` and `marcadorverm.pontosverm`. `MatchManager.CheckGoals` compares them
  against the values it last saw.
- `moverp1.Frozen` holds every robot still during menus, countdowns and goal pauses.
- UI hooks: the `StateChanged` and `Banner` events. Banners are also sent to the online peer.

`GameConfig` (static) holds every menu choice: players, quick match or tournament, 1x1 or 3x3,
difficulty, arena, period length, goals per period, volume, robot picks and the tournament `Ladder`.

## Rink geometry: `Rink` and `RinkBuilder`

The scene's rink is stretched around its center: ×1.25/1.2 for 1x1 and ×1.6/1.45 for 3x3.
Gameplay code writes positions in the scene's original coordinates and maps them with
`Rink.W(x, z)`. Goals, pillars and round markings keep their real size through
`Rink.KeepSize` / `ApplyKeeps`. `RinkBuilder` generates rounded boards and the floor for the
current size. `MatchManager.ApplyFormat` re-runs all of this whenever the format changes.

## Robots

| Piece | File | Role |
|---|---|---|
| Data | `Robots/RobotSpec.cs`, `Resources/Robots/*.asset` | id, name, stats (speed, weight, size, strength), tool, body kit, accent color, optional model |
| Catalog | `RobotCatalog` (in `RobotSpec.cs`) | Loads every spec from `Resources/Robots`, sorted by `order`. 3x3 team weight budget (30) |
| Apply | `Robots/RobotLoadout.cs` | Takes a spec and sets the robot's mass, scale, motor multipliers, team paint, body kit, number and lights |
| Tools | `Robots/RobotTools.cs` | Claw (cradle + shoot), Booster, Plow, Kicker, Magnets. Cooldowns |
| Movement | `3D/unidade/moverp1.cs` | Tank drive with momentum, grip, impact spin and stun. Reads an `IRobotInput` |
| Input | `Game/IRobotInput.cs`, `Game/AIRobotInput.cs`, `NetSession.RemoteInput` | Keyboard, AI or network input behind one interface |
| Effects | `Robots/FxPool.cs`, `Robots/RobotFx.cs` | Shared particle pools (sparks, skid spray, kick flash) and per-robot effects |

**To add a robot**, create a new asset with *Create > Hockey > Robot* in `Assets/Resources/Robots`.
No code is needed.

The AI (`AIRobotInput`) is hand-written steering: it gets behind the puck on the line to the goal,
then charges. Difficulty sets its top speed, reaction delay and how far it lets the puck drift. In
3x3, `MatchManager.AssignRoles` makes one robot per team the attacker and the others defenders. The
human player switches robots with Tab or Shift.

## Arenas: `ArenaTheme`

`ArenaTheme.Names` (`Game/ArenaTheme.cs`) lists Clássica, Gelo and Fábrica. `ArenaThemeImpl.cs`
re-skins the one rink at runtime. It swaps the floor material and physics material, light colors,
ambient light, color grading, puck drag and robot grip (`moverp1.Grip`). It also turns the ice
lines and pillars on or off. The roadmap replaces this with data assets (`ArenaDef`).

## Online: `NetSession`

Peer to peer by IP with Netcode for GameObjects and Unity Transport, on UDP port 7777. No
NetworkObjects are used. Everything goes through named custom messages:

| Message | Direction | Content |
|---|---|---|
| Config | host → client | match settings |
| Pick | both | robot picks |
| Input | client → host | drive, turn, action |
| Snapshot | host → client, 25/s | robot and puck positions, clock, score |
| Event | host → client | banner, horn, buzzer, beep, kick, impact sounds |

The host is authoritative: it runs the physics, the AI and the score, and plays blue. The client
plays red, and between snapshots it smooths the remote state (`MatchManager.SmoothRemote`).

## UI: `GameUI`

All screens are built in code with uGUI: title, setup, robot select, online, HUD, pause and
result. They scale with a `CanvasScaler`. The text uses Unity's built-in `LegacyRuntime.ttf` font,
and the rounded sprites are generated at runtime.

## Audio

Sound effects are synthesized at startup, with no audio files. `GameSfx` makes the horn, buzzer and
beeps, `ImpactSound` makes metal hits, and `CrowdSpawner` makes the crowd murmur and roar. Music
comes from the scene's `Audio Source`.

## Tests (`Assets/Tests`)

| Test | Covers |
|---|---|
| `GameplayTest` | Puck stays on the rink, goals score |
| `MatchTest` | Menu and setup screens, best of 3 with sudden death, 1 Player vs AI, 3x3 robot switching |
| `RobotsTest` | Catalog covers every tool, loadouts change mass/size/stats, kicker, booster |
| `ArenaSizeTest` | Bigger rinks keep goals, pillars and markings at real size |
| `PhysicsProbeTest` | Puck vs robot hull, goal posts, boards; fast robots don't pass through boards |
| `ImpactSoundTest` | Puck hits and robot crashes make sounds |
| `CrowdTest` | Crowd fills the stands and cheers for the scorer |
| `NetTest` | Hosting opens the port, online screen, a bad IP is refused |
| `InputBackendTest` | The legacy Input Manager is enabled (will change with the new Input System) |
