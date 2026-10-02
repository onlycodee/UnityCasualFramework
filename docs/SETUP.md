# Setup

## 1. Open the project
1. Install **Unity 6000.4.11f1** (Unity 6.4) with Android and/or iOS build support via Unity Hub.
2. Clone with LFS: `git lfs install && git clone <repo>`.
3. Open the folder in Unity Hub. First import resolves packages (Input System, uGUI, Newtonsoft, MCP for Unity).
4. If Unity asks to enable the new Input System backend, choose **Yes** (restart). Then in
   *Project Settings → Player*: Default Orientation = Portrait. (The code also works with "Both" input backends.)
5. Run **HyperFrame → Setup Project** once. It (re)creates `Assets/_Game/Scenes/Boot.unity`, puts it first in
   Build Settings, and checks `Assets/_Game/Resources/GameDefinition.asset`.
6. Open the Boot scene and press Play: Home → Play → tap all circles → Win → Continue → Home.
   **DBG** (bottom-left) opens the debug console.

## 2. Connect Claude Code to the Editor (MCP)
1. The `com.coplaydev.unity-mcp` package is already in `Packages/manifest.json`. Install `uv` (Python ≥ 3.10).
2. In Unity: *Window → MCP for Unity* → start the server (HTTP, port 8080).
3. `.mcp.json` in the repo root already points Claude Code at `http://localhost:8080/mcp`. Run `claude` in the repo root and check `/mcp`.
4. Try `/verify`.

## 3. CI (GitHub Actions + GameCI)
`.github/workflows/ci.yml` runs the architecture check, then EditMode and PlayMode tests on every PR.
Add repository secrets (Settings → Secrets and variables → Actions):
- **Personal license**: `UNITY_LICENSE` (contents of the `.ulf` file), `UNITY_EMAIL`, `UNITY_PASSWORD`.
  See https://game.ci/docs/github/activation for how to obtain the `.ulf`.
- **Pro/Plus license**: `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
Then protect `main` and require the "Unity editmode tests" and "Unity playmode tests" checks (TS-04).

## 4. Using HyperFrame in a new game project
Reference the packages by git URL in the game's `Packages/manifest.json`:
```json
"com.hyperframe.core":     "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.core#v0.1.0",
"com.hyperframe.input":    "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.input#v0.1.0",
"com.hyperframe.ui":       "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.ui#v0.1.0",
"com.hyperframe.audio":    "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.audio#v0.1.0",
"com.hyperframe.services": "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.services#v0.1.0",
"com.hyperframe.feedback": "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.feedback#v0.1.0",
"com.hyperframe.app":      "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.app#v0.1.0",
"com.hyperframe.devtools": "https://github.com/<owner>/<repo>.git?path=Packages/com.hyperframe.devtools#v0.1.0"
```
Copy `CLAUDE.md`, `RECIPES.md`, `.claude/`, `.mcp.json`, `Tools/` and `.github/` from this repo. (A `new-game`
scaffolding command that does this is planned for Phase 3.)
