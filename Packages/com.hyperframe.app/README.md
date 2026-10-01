# com.hyperframe.app

The app shell every game uses.

- **`GameDefinition`** (AI-06): one asset describing the game: gameplay module, levels, theme, sounds, gestures, economy numbers, flow options, save version. Lives at `Assets/_Game/Resources/GameDefinition.asset`.
- **`HyperFrameApp`** (CORE-01): boots services in declared order with per-step timeouts (`BootReport`), auto-starts in the scene named `Boot`, or `HyperFrameApp.Launch(definition, options)` from code/tests.
- **`GameFlow`** (AR-4): Boot → Consent → Loading → Home → Gameplay → Result(Win/Lose) → Reward → Home, with Pause, Restart, level select, rewarded multiplier on win.
- **Standard views** (UI-03/04): `HomeScreen`, `GameplayScreen` (HUD), `LevelSelectScreen`, `WinPopup`, `LosePopup`, `PausePopup`, `SettingsPopup`, `MockAdPopup`. All have code-built placeholders and accept prefabs via `ViewRegistry`.
- **Plug-in point**: subclass `GameplayModule` (asset) and `GameplayBase` (one level). That is the only gameplay code a game writes.
- **`AppDriver`**: wait for states/views, press buttons by name, tap world positions; used by PlayMode tests, bots and the agent.
- **Editor**: `HyperFrame/Setup Project` creates the Boot scene, build settings entry and GameDefinition.
