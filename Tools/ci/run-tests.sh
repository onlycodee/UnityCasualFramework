#!/usr/bin/env bash
# Batchmode fallback for /verify when the Unity MCP bridge is unavailable (PRD §3.3).
# Compiles the project, runs EditMode then PlayMode tests, and prints a Markdown summary.
#
#   Tools/ci/run-tests.sh [editmode|playmode|all]     (default: all)
#
# Unity is found via $UNITY_PATH, else the Unity Hub default install for ProjectVersion.txt.
# The Editor must NOT have this project open (Unity locks the project).
set -euo pipefail
cd "$(dirname "$0")/../.."
MODE="${1:-all}"
VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"

if [[ -z "${UNITY_PATH:-}" ]]; then
  for candidate in \
    "/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity" \
    "/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"; do
    [[ -x "$candidate" ]] && UNITY_PATH="$candidate" && break
  done
fi
[[ -x "${UNITY_PATH:-}" ]] || { echo "Unity $VERSION not found. Set UNITY_PATH."; exit 2; }

mkdir -p artifacts
python3 Tools/ci/check_architecture.py

run() {
  local platform="$1"
  echo "▶ $platform tests (Unity $VERSION)…"
  set +e
  "$UNITY_PATH" -batchmode -nographics -projectPath . -runTests -testPlatform "$platform" \
    -testResults "artifacts/$platform-results.xml" -logFile "artifacts/$platform.log"
  local code=$?
  set -e
  if [[ ! -f "artifacts/$platform-results.xml" ]]; then
    echo "No results for $platform (exit $code). Compile errors? Last log lines:"
    grep -E "error CS|Exception" "artifacts/$platform.log" | head -40 || tail -40 "artifacts/$platform.log"
    exit 1
  fi
}

case "$MODE" in
  editmode) run EditMode ;;
  playmode) run PlayMode ;;
  all) run EditMode; run PlayMode ;;
  *) echo "usage: $0 [editmode|playmode|all]"; exit 2 ;;
esac

python3 Tools/ci/summarize_results.py "artifacts/*-results.xml" | tee artifacts/summary.md
