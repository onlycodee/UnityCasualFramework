---
description: Compile, run EditMode + PlayMode tests, capture screenshots, and report (PRD §7.2)
---
Run the HyperFrame self-verification loop. Fix and repeat each step until it is clean before moving on.
$ARGUMENTS may narrow the scope (e.g. a test name filter); otherwise run everything.

0. **Architecture gate** (always, no Unity needed): `python3 Tools/ci/check_architecture.py`. Fix every violation.

1. **Compile** — Unity MCP: call `refresh_unity` (request a script compile), then `read_console` filtered to errors and warnings.
   Zero errors. Zero *new* warnings in `Assets/_Game` and `Packages/com.hyperframe.*`. Fix → repeat.

2. **EditMode tests** — Unity MCP: `run_tests` with mode `EditMode`, then poll `get_test_job` until finished.
   All must pass. Fix the code (never the assertion, unless the test is wrong and you say why) → repeat.

3. **PlayMode smoke** — `run_tests` with mode `PlayMode`. `Game.Tests.FullFlowTests` is the minimum
   (Boot → Home → Play → Win → Home). On failure read the message: `AppDriver.Describe()` shows flow state,
   open popups and input-lock holders.

4. **Screenshots** — `manage_editor` → play; wait for Home; `manage_camera` → `screenshot` (Game View) at
   1080×1920 (9:16), 1170×2532 (9:19.5), 1080×2520 (9:21), 1536×2048 (3:4). Open each image and check:
   nothing clipped by the safe area, no text overflow, buttons readable. Exit Play Mode.

5. **Report** — what changed, test totals (pass/fail per platform), screenshot findings, open issues.

### Fallback when the Unity MCP server is not connected
Close the Unity Editor (it locks the project), then run `Tools/ci/run-tests.sh all`
(Windows: `powershell -ExecutionPolicy Bypass -File Tools\ci\run-tests.ps1 all`). It compiles, runs both
test platforms in batchmode and prints `artifacts/summary.md`. Screenshots are skipped in this mode; say so in the report.

### Tool names
The tool names above are from MCP for Unity (CoplayDev, the Phase 0 default). If another Unity MCP server
wins the bake-off (`docs/MCP_BAKEOFF.md`), use its equivalents: compile/refresh, console read, test run,
play mode toggle, game-view screenshot.
