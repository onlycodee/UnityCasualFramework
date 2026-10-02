# Decisions (Phase 0 defaults)

Defaults picked so work could start; each can be revisited. "Open question" numbers refer to PRD §12.

| # | Topic | Decision | Why / how to change |
|---|---|---|---|
| — | Unity version | **6000.4.11f1 (Unity 6.4)**, upgraded from 6000.3.25f1 (6.3 LTS) on 2026-10-02 at Phuong's request | Phuong chose 6.4 over the PRD's LTS preference. 6.4 deprecates `Object.GetInstanceID()` (use `GetEntityId()`); nothing else the framework uses changed. To change again: `ProjectSettings/ProjectVersion.txt` and `"unity"` in package.json files. |
| 1 | Unity MCP server | **MCP for Unity (CoplayDev) v10.0.0**, HTTP on `localhost:8080/mcp` | Most complete tool set (compile, console, tests, play mode, screenshots). Provisional until the bake-off in `docs/MCP_BAKEOFF.md`. Framework code does not depend on it; `/verify` has a batchmode fallback. |
| 3 | Tweening | **Built-in `TweenEngine`** in core (no DOTween/LitMotion) | Needed pause-aware, allocation-free tick, unit-testable without Unity. LitMotion can be added later for heavy animation without changing the core API. |
| — | Async | **`System.Threading.Tasks`** + `.Forget()` | Zero dependencies and testable under plain .NET. UniTask can replace it in a later phase. Note: `Task.Delay` does not work in WebGL; revisit before the playable-ad export (P2). |
| 6 | Distribution | **Git-URL UPM packages** from this repo (`?path=Packages/com.hyperframe.core#vX.Y.Z`) | No registry to run. Move to a private registry (e.g. Verdaccio) if versions multiply. |
| — | JSON | **Newtonsoft (com.unity.nuget.newtonsoft-json 3.2.1)** for save data | Dictionaries + raw-JSON migrations (`JObject`). `JsonUtility` cannot do either. |
| — | Render pipeline | **No pipeline package in Phase 0** (Built-in) | Placeholder particles try URP shaders first, so switching to URP 2D later needs no code change. |
| — | Text | Placeholders use **legacy `Text`** with the built-in font; designed prefabs may use **TextMeshPro** (both supported by `SetText`) | Legacy Text renders in batchmode tests without importing TMP Essentials. |
| — | UI scaling | `CanvasScaler` 1080×1920, **Expand** | Guarantees the reference area is visible from 3:4 to 9:21 (UI-08). |
| 2 | Ads / analytics stack | Not chosen. Phase 0 ships interfaces + mocks only | Decide before Phase 2 (e.g. AppLovin MAX or LevelPlay; Firebase + GameAnalytics). Each SDK goes in its own adapter package. |
| 4 | Child-directed | Not decided | Needed before Phase 2 consent/ads work. |
| 5 | Image/audio generation services | Not decided | Phase 1 asset pipeline. |
| 7 | WebGL playable before Game #2 | Not decided | Affects the async choice above. |
| — | Service locator name | `ServiceLocator` (not `Services`) | `Services` collides with the `HyperFrame.Services` namespace inside framework code. |
| — | Boot without scene wiring | `HyperFrameApp` auto-boots in the scene named `Boot` from `Resources/GameDefinition` | Scenes stay empty, so agents never edit scene YAML. `GameDefinition` lives in `Assets/_Game/Resources/` (PRD shows `Assets/_Game/`). |
