# Unity MCP bake-off (PRD §3.3)

Status: **not run yet** (needs a machine with the Unity Editor). MCP for Unity is the provisional default.

Score each candidate 0–2 per criterion (0 = no, 1 = partial/flaky, 2 = solid) on Unity 6000.4.11f1 with this repo.

| # | Criterion | How to test | MCP for Unity (CoplayDev) | Unity official MCP (Unity AI) | Unity-MCP (IvanMurzak) | mcp-unity (CoderGamester) |
|---|---|---|---|---|---|---|
| 1 | Recompile + return compile errors | Introduce a typo in `Assets/_Game/Scripts`, ask for a compile | | | | |
| 2 | Read console logs | `HFLog.Warn` from a menu item, read it back | | | | |
| 3 | Run EditMode/PlayMode tests + results | Run `Game.Tests.FullFlowTests` | | | | |
| 4 | Create/modify GameObjects, components, prefabs, ScriptableObjects | Create a ViewRegistry asset and a prefab with `WinPopup` | | | | |
| 5 | Game View screenshot | Capture Home at 1080×1920 | | | | |
| 6 | Enter/exit Play Mode | Play, wait for Home, stop | | | | |
| 7 | Stable across domain reloads | 10 compile cycles in a row without reconnecting | | | | |
| 8 | License + maintenance | License file; commits/releases in last 90 days | | | | |
| | **Total (max 16)** | | | | | |

Decision rule: highest total wins; ties go to the one with better test running (criterion 3). Record the result in
`docs/DECISIONS.md` and update tool names in `.claude/commands/verify.md`.
