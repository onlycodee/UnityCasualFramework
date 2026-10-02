# Pixel Loop

A conveyor-shooter puzzle in the style of "Pixel Flow", built on HyperFrame with original name, art,
sounds and levels. All game code is in `Assets/_Game`; the framework only gained one generic hook
(`GameplayModule.ConfigureSounds`).

## How it plays
- A pixel-art picture sits in the middle, inside a conveyor belt.
- Shooters wait in queue columns at the bottom. Tap a column to send its front shooter onto the belt.
- The belt carries the shooter once around the picture. Each time it lines up with a row or column it
  looks inward: if the first pixel there has its colour, it fires and the pixel is destroyed.
- Out of ammo, the shooter pops. Back at the entry with ammo left, it parks in the tray; tap it there to
  send it again. A shooter that comes back to a full tray loses the level.
- The belt holds a limited number of shooters (lights next to the entry). Clear every pixel to win.
- Stars: 3 if no shooter ever came back with ammo, 2 for one or two returns, 1 otherwise.

## Code map
| File | What |
|---|---|
| `Scripts/Model/PixelLevel.cs` | Level JSON (picture rows, palette, belt/tray/queue knobs) |
| `Scripts/Model/BeltGeometry.cs` | Rounded-rectangle belt path and the stops where shooters line up with lanes |
| `Scripts/Model/BeltSim.cs` | The rules, engine-free and frame-rate independent |
| `Scripts/Model/ShooterPlanner.cs` | Builds a queue that always clears the picture when played one shooter at a time |
| `Scripts/Gameplay/PixelLoopGameplay.cs` | 2D stage, input, and all feedback (bullets, pops, shake, slow-mo, reveal) |
| `Scripts/Gameplay/ShooterView.cs` | The shooter visual and its blended movement |
| `Scripts/Gameplay/PixelLoopArt.cs` | Procedural sprites (bevelled tiles, glossy discs, glows, rounded panels) |
| `Scripts/Gameplay/PixelLoopSounds.cs` | Procedural SFX and the music loop, under the sound IDs the game uses |
| `Resources/PixelLoopModule.asset` | Level list (play order), loop point, stage colours |
| `Levels/*.json` | 21 levels |

## 3D version (Pixel Loop 3D)
Same rules, levels and sounds, presented in 3D: a voxel picture that rains onto a lit board, a conveyor rail
with moving treads, toy turrets with eyes and floating ammo badges, real-time shadows, a perspective camera
that flies in, breathes and shakes, bouncing voxel debris, flashes, sparks and shockwave rings, and a win
"rebuild" wave. Everything is generated in code (meshes in `Mesh3D`) plus two small Built-in-pipeline shaders.

**Switching between 2D and 3D:** set `gameplay` on `Resources/GameDefinition.asset` to `PixelLoop3DModule`
(the default now) or `PixelLoopModule`. Both read the same level list from `PixelLoopModuleBase`.

| File | What |
|---|---|
| `Scripts/Gameplay3D/PixelLoop3DGameplay.cs` | 3D stage, tap → ray against the stage plane, sim events → feel |
| `Scripts/Gameplay3D/Stage3DLayout.cs` | Where the tray and queue sit, and what a tap hits (engine-free, tested) |
| `Scripts/Gameplay3D/ShooterView3D.cs` | The toy turret and its blended movement |
| `Scripts/Gameplay3D/CameraRig3D.cs` | Fits the stage on any aspect ratio under the HUD; intro, idle sway, trauma shake, win push-in |
| `Scripts/Gameplay3D/Fx3D.cs` | Pooled debris / flash / spark / ring effects on game time (pause and slow-mo apply) |
| `Scripts/Gameplay3D/Mesh3D.cs`, `Look3D.cs`, `Environment3D.cs` | Procedural meshes, materials, ambient/fog/shadow settings (restored after the level) |
| `Art/Shaders/PixelLoopToy.shader`, `PixelLoopUnlit.shader` | Glossy toy plastic with rim light; unlit glow/trail/badge |
| `Resources/PixelLoop3DModule.asset` | Level list, shader references, `style` (colours, light, camera pitch/FOV, HUD margin) |

The project uses the Built-in render pipeline and has no 3D physics module, so taps are resolved with a
ray–plane intersection and `Stage3DLayout.HitTest`, not colliders. The framework restores the camera
(`CameraSnapshot`) when a level is left, so the 3D rig never leaks into Home.

## Adding a level
1. Add `Assets/_Game/Levels/NNN_name.json`:
   ```json
   { "id": "pl_022", "name": "Kite", "colors": ["#EF476F", "#FFD166"],
     "rows": ["..0..", ".000.", "01110", ".000.", "..0.."],
     "queueColumns": 3, "beltCapacity": 4, "traySlots": 5, "maxAmmo": 20, "loopSeconds": 4.5,
     "seed": 122, "shooters": [] }
   ```
   Rows are top first; `.` is empty, `0`–`9` / `a`–`z` index into `colors`. Leave `shooters` empty to plan
   the queue from the picture, or list `{ "color": 0, "ammo": 12 }` entries to design it by hand (queue
   column *i* gets shooters *i*, *i + columns*, …).
2. Add the file to `levels` on `Resources/PixelLoopModule.asset` (the catalog test checks the order).
3. Run the EditMode tests: `PixelLevelCatalogTests` checks the picture, the palette and that the queue
   clears it.

Difficulty knobs: fewer `beltCapacity` / `traySlots`, more `queueColumns` (more choice, more ways to go
wrong), lower `maxAmmo` (more shooters), shorter `loopSeconds` (faster belt).

## Debug console
`Game/Auto play on/off` lets a bot play (handy for recording), `Game/Belt speed x1 / x3` speeds the belt up.
