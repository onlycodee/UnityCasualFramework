# com.hyperframe.feedback

`IFeedbackService` (FB-01): `Shake`, `CameraShake`, `SquashStretch`, `Punch`, `Burst`, `Spawn(vfxPrefab)`,
`Confetti`, `CoinFly(from, walletLabel)`, `SlowMo`, `Haptic`. All pause-aware through the core tween engine.
Placeholder particles and sprites are generated in code, so feedback works before any art exists.

`IHapticsService` (HP-01 presets): `DeviceHapticsService` (basic vibration) and `MockHapticsService`.
