# Windows (PowerShell) version of run-tests.sh: batchmode fallback for /verify when the Unity MCP
# bridge is unavailable (PRD 3.3). Compiles the project, runs EditMode then PlayMode tests, and
# prints a Markdown summary.
#
#   powershell -ExecutionPolicy Bypass -File Tools\ci\run-tests.ps1 [editmode|playmode|all]   (default: all)
#
# Unity is found via $env:UNITY_PATH, else the Unity Hub default install for ProjectVersion.txt.
# The Editor must NOT have this project open (Unity locks the project).
param([ValidateSet('editmode', 'playmode', 'all')][string]$Mode = 'all')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..\..')
$ProjectPath = (Get-Location).Path
$Version = ((Get-Content 'ProjectSettings\ProjectVersion.txt' | Select-String '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value).Trim()

$UnityPath = $env:UNITY_PATH
if (-not $UnityPath) {
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $root) { continue }
        $candidate = Join-Path $root "Unity\Hub\Editor\$Version\Editor\Unity.exe"
        if (Test-Path $candidate) { $UnityPath = $candidate; break }
    }
}
if (-not $UnityPath -or -not (Test-Path $UnityPath)) {
    Write-Host "Unity $Version not found. Set UNITY_PATH to Unity.exe (Unity Hub > Installs > Show in Explorer)."
    exit 2
}

# Prefer the 'py' launcher: 'python'/'python3' may be the Microsoft Store stub that only opens the Store.
$Python = @('py', 'python', 'python3') | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
if (-not $Python) { Write-Host 'Python 3 not found (needed for the architecture check and summary).'; exit 2 }

New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
& $Python Tools/ci/check_architecture.py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Invoke-UnityTests([string]$Platform) {
    Write-Host "> $Platform tests (Unity $Version)..."
    $results = Join-Path $ProjectPath "artifacts\$Platform-results.xml"
    $log = Join-Path $ProjectPath "artifacts\$Platform.log"
    Remove-Item $results -ErrorAction SilentlyContinue
    # Unity.exe is a GUI app, so PowerShell would not wait for it without Start-Process -Wait.
    $proc = Start-Process -FilePath $UnityPath -Wait -PassThru -NoNewWindow -ArgumentList @(
        '-batchmode', '-nographics', '-projectPath', "`"$ProjectPath`"", '-runTests', '-testPlatform', $Platform,
        '-testResults', "`"$results`"", '-logFile', "`"$log`"")
    if (-not (Test-Path $results)) {
        Write-Host "No results for $Platform (exit $($proc.ExitCode)). Compile errors? Last log lines:"
        $errors = Select-String -Path $log -Pattern 'error CS|Exception' -ErrorAction SilentlyContinue | Select-Object -First 40
        if ($errors) { $errors | ForEach-Object { $_.Line } } else { Get-Content $log -Tail 40 -ErrorAction SilentlyContinue }
        exit 1
    }
}

switch ($Mode) {
    'editmode' { Invoke-UnityTests 'EditMode' }
    'playmode' { Invoke-UnityTests 'PlayMode' }
    'all' { Invoke-UnityTests 'EditMode'; Invoke-UnityTests 'PlayMode' }
}

& $Python Tools/ci/summarize_results.py 'artifacts/*-results.xml' | Tee-Object -FilePath 'artifacts\summary.md'
exit $LASTEXITCODE
