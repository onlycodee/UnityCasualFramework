#!/usr/bin/env python3
"""
License-free architecture gate (runs in CI before Unity, and locally in a second).

Checks:
  AR-1  asmdef references only point down the layer stack (no cycles, no upward deps)
  AR-7  framework packages never reference game code (Assets/_Game, namespace Game)
  AI-02 every package has README.md and AGENTS.md
  hygiene: no `async void`, no direct `Time.timeScale =` outside the core runner,
           no UnityEngine.Debug.Log in framework runtime code (use HFLog)

Exit code 1 on any violation.
"""
import json, os, re, sys, glob

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# Lower rank = lower layer. A reference must point to a strictly lower rank, or to the same package.
RANKS = {
    "com.hyperframe.core": 0,                                                 # L1
    "com.hyperframe.input": 1, "com.hyperframe.audio": 1, "com.hyperframe.services": 1,  # L2
    "com.hyperframe.ui": 2, "com.hyperframe.feedback": 3,                     # L2 (UI uses input lock)
    "com.hyperframe.app": 4,                                                  # L2 app shell / flow
    "com.hyperframe.devtools": 5,                                             # dev-only, on top of app
}
KIT_RANK, GAME_RANK = 6, 7  # L3 com.hyperframe.kits.*, L4 Assets/_Game


def package_of(path):
    rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
    if rel.startswith("Packages/"):
        return rel.split("/")[1]
    if rel.startswith("Assets/_Game"):
        return "game"
    return "other"


def rank_of(pkg):
    if pkg in RANKS:
        return RANKS[pkg]
    if pkg.startswith("com.hyperframe.kits."):
        return KIT_RANK
    if pkg == "game":
        return GAME_RANK
    return None


def main():
    errors = []
    asmdefs = {}
    for p in glob.glob(os.path.join(ROOT, "Packages", "**", "*.asmdef"), recursive=True) + \
             glob.glob(os.path.join(ROOT, "Assets", "**", "*.asmdef"), recursive=True):
        with open(p, encoding="utf-8") as f:
            data = json.load(f)
        asmdefs[data["name"]] = (p, data)

    # AR-1 / AR-7
    for name, (path, data) in asmdefs.items():
        pkg = package_of(path)
        my_rank = rank_of(pkg)
        if pkg.startswith("com.hyperframe.") and my_rank is None:
            errors.append(f"{name}: package {pkg} has no layer rank in check_architecture.py")
            continue
        for ref in data.get("references", []):
            if ref not in asmdefs:
                continue  # Unity / third-party assembly
            ref_pkg = package_of(asmdefs[ref][0])
            if ref_pkg == pkg:
                continue
            ref_rank = rank_of(ref_pkg)
            if ref_pkg == "game" and pkg != "game":
                errors.append(f"AR-7 {name} ({pkg}) references game assembly {ref}")
            elif my_rank is not None and ref_rank is not None and ref_rank >= my_rank:
                errors.append(f"AR-1 {name} ({pkg}, rank {my_rank}) references {ref} ({ref_pkg}, rank {ref_rank}) — dependencies must flow down")

    # AI-02 docs + code hygiene
    for pkg_dir in sorted(glob.glob(os.path.join(ROOT, "Packages", "com.hyperframe.*"))):
        for doc in ("README.md", "AGENTS.md", "package.json"):
            if not os.path.isfile(os.path.join(pkg_dir, doc)):
                errors.append(f"AI-02 {os.path.basename(pkg_dir)} is missing {doc}")
        for cs in glob.glob(os.path.join(pkg_dir, "**", "*.cs"), recursive=True):
            rel = os.path.relpath(cs, ROOT)
            src = open(cs, encoding="utf-8").read()
            code = re.sub(r"//.*", "", src)
            if re.search(r"\basync\s+void\b", code):
                errors.append(f"{rel}: async void (use Task + .Forget())")
            if re.search(r"\bnamespace\s+Game\b|\busing\s+Game\s*;", code):
                errors.append(f"AR-7 {rel}: framework code references the Game namespace")
            if "/Runtime/" in cs.replace(os.sep, "/") and not cs.endswith("HyperFrameRunner.cs") and not cs.endswith("HyperFrameApp.cs"):
                if re.search(r"Time\.timeScale\s*=(?!=)", code):
                    errors.append(f"{rel}: writes Time.timeScale (use IGameClock)")
                if re.search(r"\bDebug\.Log(Warning|Error)?\(", code) and not cs.endswith("UnityLogSink.cs"):
                    errors.append(f"{rel}: Debug.Log in runtime code (use HFLog)")

    if errors:
        print("Architecture check FAILED:")
        for e in errors:
            print("  - " + e)
        return 1
    print(f"Architecture check passed ({len(asmdefs)} assemblies).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
