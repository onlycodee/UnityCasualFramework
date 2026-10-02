# AGENTS.md — com.hyperframe.app

## The gameplay contract
```csharp
[CreateAssetMenu(menuName = "Game/My Module")]
public sealed class MyModule : GameplayModule
{
    public override IGameplay CreateGameplay() => new MyGameplay();
    // Optional: CreateLevelProvider, ParseLevel, GetSaveMigrations, ConfigureSounds, OnBoot, CalculateReward, DescribeDifficulty
}

public sealed class MyGameplay : GameplayBase
{
    protected override void OnBegin()
    {
        var level = (MyLevel)Context.Level;          // build the level under Context.WorldRoot
        Context.Hud.SetStatus("Moves: 20");
    }
    void OnSolved() => Finish(new LevelOutcome { Won = true, Stars = 3 });
    void OnOutOfMoves() => Finish(new LevelOutcome { Won = false, Reason = "out_of_moves" });
    public override void Dispose() { /* unsubscribe from Context.Input etc. */ }
}
```
The flow handles everything else: HUD, pause, win/lose popups, rewards, progression, analytics, save.

## Rules
- Spawn level objects under `Context.WorldRoot` (it is destroyed when the level ends).
- Use `Context.Tweens` / `Context.Clock` so pause works; never `Time.timeScale`.
- `Context.Camera` may be reconfigured freely (e.g. a perspective 3D rig): the flow captures it with `CameraSnapshot` before `Begin` and restores it when the level is left. Restore any other global you change (RenderSettings, QualitySettings) in `Dispose`.
- Call `Finish` exactly once. `ForceFinish(win)` is used by the debug console and tests; keep the base behaviour unless the game needs special handling.
- Flow commands: `ServiceLocator.Get<GameFlow>().Play() / Pause() / Restart() / GoHome() / ForceFinish(win)`.

## Testing a full flow (PlayMode)
```csharp
HyperFrameApp.Launch(definition, AppOptions.ForTests());
yield return AppDriver.WaitForState(GameFlowState.Home);
yield return AppDriver.Click<HomeScreen>("btn_Play");
AppDriver.TapWorld(target.position);
yield return AppDriver.Click<WinPopup>("btn_Continue");
```
On failure, `AppDriver.Describe()` explains state, open popups and input-lock holders.
