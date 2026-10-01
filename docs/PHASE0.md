# Phase 0 — Foundation: status

## Exit criteria (PRD §9)
| Criterion | Status |
|---|---|
| Dummy game runs Boot → Home → Play → Win → Home | Implemented ("Tap Targets") and covered by `Game.Tests.FullFlowTests.Boot_Home_Play_Win_Home`. **Not yet run inside Unity.** |
| `/verify` passes | Command written (`.claude/commands/verify.md`) with batchmode fallback. **Not yet run inside Unity.** |
| CI green | Workflow written (`.github/workflows/ci.yml`). Needs Unity license secrets before the Unity jobs can run. |
| Unity MCP bake-off | Scorecard ready (`docs/MCP_BAKEOFF.md`); needs an Editor machine. MCP for Unity is the provisional default. |

## What was verified without a Unity Editor
- All 18 assemblies compile with **0 errors, 0 warnings** against Unity reference assemblies plus API stubs for uGUI / TMP / Input System, in two configurations: Editor+tests, and a release Android player (no `UNITY_EDITOR`, no `DEVELOPMENT_BUILD`; DevTools correctly excluded). The reference DLLs available offline are from Unity 2021.3, so Unity 6.3-only API changes would not be caught; the code avoids 6.x-only APIs and was spot-checked against the 6000.3.25f1 C# reference source.
- **132 engine-free EditMode tests pass** under .NET 8 (core, input, services, level tools, the game's level generator).
- `Tools/ci/check_architecture.py` passes (layering, no game code in packages, docs present, no `async void` / `Time.timeScale` / `Debug.Log` in runtime code).

## Not verified yet (needs Unity)
- PlayMode tests (full flow, lose/retry, rewarded multiplier, pause/back, save across reboot, settings, level select) and the UI EditMode tests that build real uGUI objects.
- Visual layout on 4 aspect ratios, placeholder particles/sounds, Input System device reading on a phone.
- Hand-written assets: `GameDefinition.asset`, `TapTargetsModule.asset`, `Boot.unity`, `EditorBuildSettings.asset`. If Unity rejects any of them, run **HyperFrame → Setup Project** and re-assign `TapTargetsModule` in the GameDefinition.

## Requirement coverage (P0 items)
| Area | Done in Phase 0 | Deferred |
|---|---|---|
| Core | CORE-01…07 | CORE-08 (P2) |
| UI | UI-01, 02, 03, 04, 06, 08, 09, 11 | UI-05, 07, 10, 12 (P1) |
| Input | IN-01, 02, 03, 04, 06 | IN-05 recording/replay (P1) |
| Audio / feedback | AU-01, 02, 03, FB-01, HP-01 presets (basic vibration) | native haptics adapter |
| Save etc. | SV-01, ST-01, EC-01, PG-01 | SV-02, EC-02, PG-02 |
| Levels | LV-01, LV-06 (loop), LV-02/03/07 as generic interfaces + batch validator/report | Kit generators/solvers (Phase 1), LV-04/05 |
| Monetization & data | AD-03 mock ads (Complete/Skip/Fail popup), AN-02 schema, IAP/consent/remote-config mocks, AN-03 log in debug console | real SDK adapters (Phase 2) |
| Dev tools | DV-01, DV-02 | DV-03…05 |
| Kits | — | Grid, Goals, Sort, Boosters (Phase 1) |
| AI-first | AI-01, AI-02, AI-03 (Phase 0 recipes), AI-06, AI-10/13/14 templates, `/verify`, `/new-popup`, `/new-sound` | AI-04/05/07–09/11/12 |
| Testing | TS-03 summarizer, TS-04 CI workflow | TS-01 Kit tests, TS-02 solver bot (Phase 1) |
