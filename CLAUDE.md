# HyperFrame (Unity 6.3 LTS) — agent guide

HyperFrame is a modular Unity framework for 2D hypercasual games. This repository holds the framework
packages (`Packages/com.hyperframe.*`) and a dummy game (`Assets/_Game`, "Tap Targets") that proves the
full flow. Read this file first, then the `AGENTS.md` of every package you touch.

## Layout
```
Packages/com.hyperframe.core       L1  boot, ServiceLocator, EventBus, StateMachine, pooling, tweens/timers, HFLog
Packages/com.hyperframe.input      L2  gestures, input lock, 2D routing, input injection
Packages/com.hyperframe.audio      L2  SoundTable, music crossfade, SFX voices
Packages/com.hyperframe.services   L2  save, settings, wallet, progression, levels, analytics, ads/IAP/consent mocks
Packages/com.hyperframe.ui         L2  screens, popups, theme, safe area, overlays
Packages/com.hyperframe.feedback   L2  shake/punch/particles/confetti/coin fly/slow-mo, haptics
Packages/com.hyperframe.app        L2  GameDefinition, HyperFrameApp boot, GameFlow, standard screens/popups, AppDriver
Packages/com.hyperframe.devtools   dev debug console (not compiled into release builds)
Assets/_Game                       L4  the ONLY per-game code and content
Tools/ci                           architecture check, batchmode test runner, result summarizer
```

## Rules (PRD §4.1)
- **AR-1** Each package is one assembly; references only point down the stack. `python3 Tools/ci/check_architecture.py` enforces it.
- **AR-2** Access services through interfaces (`ServiceLocator.Get<IAudioService>()`); every service has a mock that works without SDKs.
- **AR-3** Tuning, levels, economy and flow options are data (ScriptableObjects / JSON / `GameDefinition`), not constants.
- **AR-4** The flow is fixed: Boot → Consent → Loading → Home → Gameplay → Result → Reward → Home. A game supplies only a `GameplayModule` + `GameplayBase`.
- **AR-6** Prefer plain C# classes, typed struct events and the service locator. No DI containers, no reflection magic.
- **AR-7** No game-specific code in `Packages/com.hyperframe.*`. Reusable game logic gets promoted into a Kit by review.

## Where code goes
| You are building… | Put it in |
|---|---|
| Game rules, level types, game UI, game tests | `Assets/_Game/Scripts/...`, `Assets/_Game/Tests/{EditMode,PlayMode}` |
| A fix or feature in the framework itself (this repo only) | the owning `Packages/com.hyperframe.*` + its tests + its `AGENTS.md` |
| Generic mechanics used by 2+ games | a new `Packages/com.hyperframe.kits.<name>` (Phase 1+) |

## Conventions
- UI children: `btn_Name`, `txt_Name`, `img_Name`, `list_Name`. Bind with `Bind("btn_Name", ...)`.
- Events: `struct <Thing>Event`, published on `IEventBus`; subscribe with `SubscribeUntilDestroy` or dispose.
- Time: `TweenEngine` / `IGameClock` only. Never write `Time.timeScale`; never use coroutines for gameplay timing.
- Async: return `Task`; fire-and-forget with `.Forget("Tag")`; never `async void`.
- Logging: `HFLog.Debug/Info/Warn/Error`, never `Debug.Log` in runtime code.
- Analytics: only events in `AnalyticsSchema` (add new ones there or with `RegisterCustom` first).
- Saved data shape changed? bump `GameDefinition.saveSchemaVersion` and add an `ISaveMigration` + test.

## Workflow (PRD §7.2)
1. Read `Assets/_Game/Resources/GameDefinition.asset`, `RECIPES.md` and the relevant `AGENTS.md`.
2. Make the change, with tests.
3. Run **/verify** (architecture check → compile → EditMode → PlayMode → screenshots). Without the Unity MCP
   bridge use `Tools/ci/run-tests.sh all` (Editor closed).
4. Report: what changed, test results, screenshots, open issues.

## Forbidden without asking the user
- Editing `Packages/com.hyperframe.*` from a *game* project (only in this framework repo).
- Adding SDKs, packages or `manifest.json` dependencies.
- Disabling, skipping or weakening a test to get green.
- Shipping an asset without an `ASSET_LICENSES.md` entry.

## Useful entry points
- Boot from code (tests): `HyperFrameApp.Launch(definition, AppOptions.ForTests())`
- Drive the app: `HyperFrame.App.Testing.AppDriver` (`WaitForState`, `Click<TView>("btn_X")`, `TapWorld`, `Describe`)
- Debug commands: `DebugCommands.Execute("Level/Win")`
- Editor menu: `HyperFrame/Setup Project`, `HyperFrame/Clear Save Data`
