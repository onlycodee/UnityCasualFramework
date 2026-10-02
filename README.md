# HyperFrame — AI-first Unity framework for 2D hypercasual games

Unity **6000.3.25f1 (6.3 LTS)** · Android / iOS · built to be driven by Claude Code + Unity MCP.

HyperFrame provides the 70–80% of a hypercasual game that is not gameplay (boot, flow, screens, popups,
input, audio, save, economy, progression, analytics/ads mocks, debug console), so a new game only writes a
`GameplayModule`. This repo contains the framework packages and **Tap Targets**, a dummy game that runs the
whole flow.

- **Status:** Phase 0 (Foundation). See [docs/PHASE0.md](docs/PHASE0.md) for what is done and what still needs a Unity run.
- **Setup:** [docs/SETUP.md](docs/SETUP.md) · **Decisions:** [docs/DECISIONS.md](docs/DECISIONS.md) · **Agent guide:** [CLAUDE.md](CLAUDE.md) · **Recipes:** [RECIPES.md](RECIPES.md)

```
Packages/com.hyperframe.core      L1  boot, ServiceLocator, EventBus, StateMachine, pooling, tweens, HFLog
Packages/com.hyperframe.input     L2  gestures, input lock, 2D routing, injection
Packages/com.hyperframe.audio     L2  sound table, music crossfade, SFX voices
Packages/com.hyperframe.services  L2  save+migrations, settings, wallet, progression, levels, analytics, mocks
Packages/com.hyperframe.ui        L2  screen stack, popup queue, theme, safe area, toast/loading
Packages/com.hyperframe.feedback  L2  shake, punch, particles, confetti, coin fly, slow-mo, haptics
Packages/com.hyperframe.app       L2  GameDefinition, boot, game flow, standard screens/popups, AppDriver
Packages/com.hyperframe.devtools      debug console (dev builds only)
Assets/_Game                      L4  Tap Targets (the only game code)
```

Quick check without Unity: `python3 Tools/ci/check_architecture.py`.
With Unity (Editor closed): `Tools/ci/run-tests.sh all`.
