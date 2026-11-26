# HOCKEY (working title)

A robot hockey game made in Unity: small robots push a puck around a rink. You can play 1x1 or 3x3,
on your own against the AI, with two players on one keyboard (split screen), or online with two
players by IP address. Five robots each have their own stats and one tool. There are three arenas
and a tournament mode. The in-game text is in Brazilian Portuguese.

Roadmap to a commercial release: [docs/roadmap.md](docs/roadmap.md).

## Requirements

- Unity **6000.3.29f1** (Unity 6.3), with the Universal Render Pipeline (URP)
- macOS or Windows. The release build script targets macOS.

## Open and play

1. Open the project folder in Unity Hub with the version above.
2. Open `Assets/Scenes/MENU.unity` (the only scene in the build).
3. Press Play. Menus, the HUD and the match are all built at runtime from that scene.

### Controls

| | Blue (player 1) | Red (player 2) |
|---|---|---|
| Drive / turn | WASD | Arrow keys |
| Tool (kick, boost, release claw) | Space | Enter / Right Ctrl |
| Switch robot (3x3) | Tab / Left Shift | Right Shift |
| Pause | Esc / P | Esc / P |

In 1 Player mode, WASD and the arrow keys both drive the blue robot, and Space or Enter uses its tool.

## Tests

The EditMode and PlayMode tests are in `Assets/Tests` (assembly `HockeyTests`).
Run them in **Window > General > Test Runner**, or from the command line with the editor closed:

```bash
unity test --project-path . --mode PlayMode
```

## Build

With the editor closed, run in batch mode:

```bash
Unity -batchmode -quit -projectPath . -executeMethod BuildMac.Release       # Builds/HockeyUnity.app
Unity -batchmode -quit -projectPath . -executeMethod BuildMac.Development   # Builds/HockeyUnity-dev.app
```

## Project layout

| Path | What's there |
|---|---|
| `Assets/scripts/Game` | Match flow (`MatchManager`), settings (`GameConfig`), rink geometry (`Rink`, `RinkBuilder`), arenas (`ArenaTheme*`), AI, sounds |
| `Assets/scripts/Robots` | Robot data (`RobotSpec`), how a spec is applied to a robot (`RobotLoadout`), tools (`RobotTools`), effects (`FxPool`, `RobotFx`) |
| `Assets/3D/unidade/moverp1.cs` | Robot movement (tank drive with momentum) |
| `Assets/scripts/Net` | Online play (`NetSession`: Netcode for GameObjects, host is authoritative) |
| `Assets/scripts/UI` | All menus and the HUD, built in code (`GameUI`) |
| `Assets/scripts/Crowd` | Instanced crowd in the stands |
| `Assets/Resources/Robots` | One `RobotSpec` asset per playable robot |
| `Assets/Editor` | Editor tools: scene setup, URP look, goal builder, crowd baker, build script |
| `Assets/Tests` | Automated tests |
| `docs/` | Design, architecture, roadmap, licenses, performance |

## Documentation

- [Architecture](docs/architecture.md): how the code fits together
- [Roadmap](docs/roadmap.md): plan for the commercial release
- [Licenses](docs/licenses.md): where each asset came from and what must be replaced before sale
- [Performance](docs/performance.md): targets and measurements
- [Robots and online design](docs/superpowers/specs/2026-11-26-robots-and-online-design.md)

## License

The original 2019 shared files in `ArquivosCompatilhados/` are MIT licensed. Third-party assets are
listed in [docs/licenses.md](docs/licenses.md).
