# RECIPES.md — step-by-step procedures (AI-03)

Each recipe ends with **/verify**. Phase 0 covers the recipes the framework supports today; Kit-based recipes
(new game from a Kit, new booster, new level type, gen-levels) arrive with Phase 1 and the scaffolding of Phase 3.

## 1. Plug in a new game's gameplay
1. `Assets/_Game/Scripts/Gameplay/<Name>Module.cs`: subclass `GameplayModule` with `[CreateAssetMenu]`; override `CreateGameplay()` and `CreateLevelProvider()` (or fill `GameDefinition.levels`).
2. `<Name>Gameplay.cs`: subclass `GameplayBase`; build the level under `Context.WorldRoot` in `OnBegin`; call `Finish(...)` once.
3. Create the module asset (Assets/Create/Game/...) and assign it to `GameDefinition.gameplay`.
4. Copy `Assets/_Game/Tests/PlayMode/FullFlowTests.cs` and adapt the "play a level" helper.

## 2. New popup
Use **/new-popup <Name>Popup**. Pattern: `Packages/com.hyperframe.app/Runtime/Views/StandardPopups.cs`.

## 3. New screen
Same as a popup but derive from `UIScreen`; show with `ShowScreen<T>()` (replace) or `PushScreen<T>()` (back returns).

## 4. Replace a placeholder view with a designed prefab
1. Build the prefab with the view component (e.g. `WinPopup`) on its root and children named exactly like the placeholder (`txt_Title`, `btn_Continue`, …).
2. Add the prefab to the `ViewRegistry` asset referenced by `GameDefinition.views`. No code changes.

## 5. New analytics event
1. Add the name and required params to `AnalyticsSchema.Required` (framework) or call `AnalyticsSchema.RegisterCustom(name, params)` in `GameplayModule.OnBoot()` (game).
2. Add a typed helper next to the call site; log through `IAnalyticsService`.
3. EditMode test with `AnalyticsDispatcher { StrictSchema = true }`.

## 6. New persisted data
1. `[Serializable]` class with defaults; `save.Get<T>("section")` / `save.Set("section", data)`.
2. If an existing section changes shape: bump `saveSchemaVersion`, add an `ISaveMigration` in `GetSaveMigrations()`, and an EditMode test that migrates an old JSON string.

## 7. New sound
Use **/new-sound <id>**.

## 8. New cheat
`DebugCommands.Register("Game", "Fill board", () => ...)` in `GameplayModule.OnBoot()`. Compiled out of release builds.

## 9. Theme / reskin
Create a `UITheme` asset (Assets/Create/HyperFrame/UI/Theme), set colors/fonts/sprites, assign to `GameDefinition.theme`.
