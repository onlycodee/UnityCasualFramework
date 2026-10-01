# AGENTS.md — com.hyperframe.devtools

- Game cheats: call `DebugCommands.Register("Game", "Fill tube", () => ...)` from anywhere (e.g. `GameplayModule.OnBoot`). The call is compiled out of release builds, so no `#if` is needed.
- Run a command by path from tests or MCP: `DebugCommands.Execute("Level/Win")`.
- Never reference `HyperFrame.DevTools` from game runtime assemblies; it does not exist in release builds.
