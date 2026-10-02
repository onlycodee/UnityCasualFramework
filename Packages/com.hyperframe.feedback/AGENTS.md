# AGENTS.md — com.hyperframe.feedback

```csharp
var fx = ServiceLocator.Get<IFeedbackService>();
fx.Punch(ball.transform);                       // tap response
fx.Shake(tube.transform);                       // invalid move
fx.Burst(pos, Color.yellow); fx.Haptic(HapticType.Light);
fx.CoinFly(screenPos, hud.CoinLabel, 10, onDone: () => wallet.Add(Currencies.Coins, 10, "win"));
```
- Prefer these presets over custom tweens so game feel is consistent and tunable in one place.
- Game-specific VFX: make a prefab with a ParticleSystem and call `fx.Spawn(prefab, pos)` (pooled).
- `SlowMo` changes the game clock's time scale; never write `Time.timeScale` directly.
