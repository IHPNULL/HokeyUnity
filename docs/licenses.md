# Asset licenses audit

Audited 2026-11-26. **Rule for a commercial release:** every file that ships in the build must
come from one of three sources: our own work, a license that allows commercial use (Asset Store
EULA, CC0, OFL, MIT…), or a paid commission with rights transferred. Anything without a known
source gets replaced before sale.

"Ships" means the build references it, either from `MENU.unity`, from a prefab or material the
scene uses, or from `Resources/`. Unity leaves unreferenced assets out of builds.

## Status legend

- ✅ OK: our own work or clearly licensed
- ⚠️ Check: probably fine, but write down the license or source
- ❌ Replace: unknown or web-downloaded source, must not ship in a paid game

## Code

| Item | Status | Notes |
|---|---|---|
| `Assets/scripts`, `Assets/Editor`, `Assets/Tests`, `Assets/3D/unidade/moverp1.cs` | ✅ | Own code |
| `ArquivosCompatilhados/` (2019 shared files) | ✅ | MIT, © italo haas pascoli. Not part of the build |
| Unity packages (`Packages/manifest.json`) | ✅ | Unity Companion / package licenses. Netcode and Transport are fine for commercial use |

## Audio

All game audio now comes from free Unity Asset Store packs (Asset Store EULA, fine for commercial
use inside the game). The packs are **not in git**, because the EULA doesn't allow redistributing
them as loose files in a public repository. `tools/import-audio.sh` re-imports them, and
`Assets/Resources/Audio/SoundBank.asset` points at them. Without the packs the game falls back to
its synthesized sounds.

| Use | Clip | Asset Store pack | Status |
|---|---|---|---|
| Menu / match music | Free Action Track 2 / 1 (Loop) | Free Game Music Collection | ✅ |
| Crowd ambience / cheer | 10 - Ambience, 01 - Strong cheering | Gregor Quendel - Free Crowd Cheering Sounds | ✅ |
| Goal horn, period buzzer, countdown | Horn_1_a, Horn_1_c, beep_2_a, beep_3_c | Score and Time (Game Sound Solutions) | ✅ |
| Robot crashes, kick | Mech Game Item hit 1/5/12/14 | Epic Game Hits SFX | ✅ |
| Robot on boards | Metal_Hit-013/014 | FREE SOUND COLLECTION | ✅ |
| Puck hits | Metal_46, Metal_41 | TOOLKIT vol.2 Sound Pack Bundle | ✅ |
| Fallback SFX (`GameSfx`, `ImpactSound`, `CrowdSpawner`) | synthesized in code | own | ✅ |
| `Le Castle Vania – John Wick Medley.mp3`, `MC Nando e MC Luanzinho – Garupa 2.mp3` (Assets and ArquivosCompatilhados), `Assets/Music.wav` | — | — | ❌ removed from the project and the scene |

## Fonts

| Item | Ships? | Status | Notes |
|---|---|---|---|
| Unity `LegacyRuntime.ttf` (built in, used by `GameUI`) | yes | ✅ | Comes with Unity. Replace it with the brand font (roadmap §1) |
| `Assets/ThirdParty/Fonts/OpenSans` | no | ✅ | Open Font License / Apache 2.0. Fine to use |

## Textures and images

| Item | Ships? | Status | Notes |
|---|---|---|---|
| `Assets/3D/unidade/download.jpg`, `download (1).jpg` | yes (robot materials) | ❌ | Web downloads with no known source |
| `Assets/3D/unidade/ce0ece26ee1348f2b1c453f314dc0a6e_18.jpg` | yes | ❌ | Web download (hashed file name) |
| `Assets/3D/unidade/Chrome-red-painted-swatch-203-FB.jpg` | yes | ❌ | Web download (looks like a paint swatch catalog image) |
| `Assets/3D/unidade/ESF-6438T-Orange-Red.jpg` | yes | ❌ | Web download (looks like a manufacturer swatch) |
| `Assets/Materials/Wallpaper_Omega.jpg`, `images.jpg` | yes (scene materials) | ❌ | Web downloads. "Omega" may be a team logo; check that it's ours |
| `Assets/2D/87-872592_glass-texture-png-transparent-png.png` | yes (`glass.mat`) | ❌ | Downloaded from a free PNG site. Those sites' licenses usually forbid commercial use |
| `Assets/2D/*.psd` (old menu: buttons, background, logo) | no | ⚠️ | Own school-project art. Unreferenced since the menu was rebuilt in code. Archive or delete |
| `Assets/2D/ButtonArrowOverSprite.png` | no | ⚠️ | Likely from Unity Standard Assets |
| `Assets/Materials/Goal/NetGrid.png` | yes | ✅ | Made for this project (`GoalBuilder`) |
| `Assets/ThirdParty/CrossPlatformInput`, `Assets/ThirdParty/Prototyping` | no / via material | ✅ | Unity Standard Assets (Unity Companion License) |

Easiest fix: replace all of the ❌ textures with materials from one paid or CC0 pack (e.g.
ambientCG, Poly Haven, both CC0) during the art pass. Plain colored URP materials are a quick
stand-in.

## 3D models

| Item | Ships? | Status | Notes |
|---|---|---|---|
| `Assets/3D/*.fbx` (arena, stands, chair, scoreboard, boards, guard) | yes | ✅ | Own models (from `ArquivosCompatilhados/MODELOS` and the CAD in `Assets/terrain`) |
| `Assets/3D/unidade` robot model | yes | ✅ | Own model. Check that no part came from a downloaded model |
| `Assets/3D/PUCK v1.ipt.max` | yes (via prefab) | ✅ | Own model (Inventor source in `ArquivosCompatilhados`) |
| `Assets/UnityTechnologies/Adam Character Pack` | yes (baked into the crowd) | ⚠️ | Unity's *Adam* demo character (Unity Companion License, allowed in Unity projects). `CrowdBaker` bakes it into `Assets/Crowd/FanIdle.asset` and `FanCheer.asset`. Keep the license notice, or replace it with a crowd pack in the art pass |
| `Assets/terrain/Arena Hockey/*.SLDPRT` (SolidWorks CAD) | no | ✅ | Own CAD source. Not shipped; could move out of `Assets/` to cut import time |

## To do before release

1. Point the music `Audio Source` at a licensed track (none right now).
2. ~~Delete the `Garupa 2` mp3 from `ArquivosCompatilhados/`, and remove `Assets/Music.wav`~~ Done 2026-11-26.
3. Replace the 8 ❌ textures.
4. Write the license of every purchased asset in this file as it's added (pack name, store link, license type, date).
5. Put a credits screen in the game listing third-party licenses (OFL fonts, Unity Companion
   assets, CC-BY items if any).
