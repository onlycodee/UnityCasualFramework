# AGENTS.md — com.hyperframe.core

## Public API in one screen
```csharp
// Services (register at boot, resolve anywhere)
ServiceLocator.Register<IWallet>(wallet);
var wallet = ServiceLocator.Get<IWallet>();          // throws with a clear message if missing
if (ServiceLocator.TryGet<IAdsService>(out var ads)) { }

// Events: define a struct, publish, subscribe
public struct BallDroppedEvent { public int Tube; }
var bus = ServiceLocator.Get<IEventBus>();
bus.Publish(new BallDroppedEvent { Tube = 2 });
this.SubscribeUntilDestroy<BallDroppedEvent>(e => Debug.Log(e.Tube)); // in a MonoBehaviour

// Tweens / timers (pause-aware)
var tweens = ServiceLocator.Get<TweenEngine>();
tweens.To(0f, 1f, 0.3f, v => group.alpha = v, Ease.OutQuad, TimeMode.Unscaled);
tweens.Delay(1f, () => Next());                // stops while the game is paused

// Pause
ServiceLocator.Get<IGameClock>().Pause("pause_popup"); /* ... */ .Resume("pause_popup");

// Logging
HFLog.Debug("Gameplay", $"moves={moves}");     // removed from release builds
```

## Rules
- Events are `struct`s named `<Thing>Event`. Never subscribe without disposing (use `SubscribeUntilDestroy` or `EventSubscriptions`).
- Use `TweenEngine` timers instead of coroutines/`Invoke` so pause works. UI animations use `TimeMode.Unscaled`.
- Pause with `IGameClock.Pause(reason)` / `Resume(reason)`, never by writing `Time.timeScale`.
- Do not `async void`. Use `task.Forget("Tag")` for fire-and-forget.
- Register cheats with `DebugCommands.Register("Category", "Name", action)` — it compiles out of release builds.

## Pitfalls
- `Services` is static: tests must call `ServiceLocator.Reset()` in TearDown.
- `StateMachine.ChangeState` called inside `Enter` is queued, not immediate.
- `TweenHandle` is a struct; keep it to `Kill()` later. A default handle is inactive and safe to Kill.
