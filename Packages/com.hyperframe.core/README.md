# com.hyperframe.core (L1)

Engine-agnostic building blocks every other HyperFrame package uses. No dependencies.

| Area | Type | PRD |
|---|---|---|
| Boot | `BootSequence` — ordered async steps, per-step timeout, critical/optional, `BootReport` | CORE-01 |
| Services | `ServiceLocator` (static), `ServiceScope`, `SceneServiceScope` | CORE-02 |
| Events | `IEventBus` / `EventBus` (struct events), `EventSubscriptions`, `SubscribeUntilDestroy` | CORE-03 |
| State machine | `StateMachine<TKey>`, `State`, `DelegateState`, transition table | CORE-04 |
| Pooling | `IPoolService` / `PoolService`, `IPoolable`, `PooledParticles` | CORE-05 |
| Time | `GameClock` (ref-counted pause, time scale), `TweenEngine` (tweens, Delay, Repeat), `Ease` | CORE-06 |
| Logging | `HFLog` (Verbose/Debug/Info stripped from release builds) | CORE-07 |
| Data | `Observable<T>` | — |
| Debug | `DebugCommands` registry (calls compiled out of release) | DV-02 |
| Unity glue | `HyperFrameRunner` ticks clock + tweens, forwards app pause/quit | — |

Most of this package is plain C# so it is unit-testable without entering Play Mode.
See `AGENTS.md` for usage rules.
