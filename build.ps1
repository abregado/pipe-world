<#
.SYNOPSIS
  Headless Unity build of pipe-world for Windows. No editor window needed.
.EXAMPLE
  .\build.ps1                # build only
  .\build.ps1 -Run           # build, then launch the game
  .\build.ps1 -Setup         # regenerate the World scene and URP assets first, then build
  .\build.ps1 -Check         # only compile scripts (fast, no player build)
#>
param(
    [switch]$Run,
    [switch]$Setup,
    [switch]$Check,
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"
$project = $PSScriptRoot
$versionLine = Select-String -Path "$project\ProjectSettings\ProjectVersion.txt" -Pattern "m_EditorVersion:\s*(\S+)"
$version = $versionLine.Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity" }

$open = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -notmatch "-batchmode" }
if ($open) { throw "A Unity editor is open (PID $($open.ProcessId)). Close it first; Unity can't build a project that's open." }

New-Item -ItemType Directory -Force "$project\Builds" | Out-Null
$log = "$project\Builds\build.log"

function Invoke-Unity([string]$label, [string]$method) {
    $unityArgs = @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$project`"", "-logFile", "`"$log`"")
    if ($method) { $unityArgs += @("-executeMethod", $method) }

    Write-Host "Unity ${version}: $label..."
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
    if (-not $p.WaitForExit($TimeoutMinutes * 60000)) {
        $p.Kill()
        throw "Timed out after $TimeoutMinutes min. See $log"
    }

    Select-String -Path $log -Pattern "error CS\d+|Shader error|\[Build\] .*Result|\[WorldSetup\]|Build Finished, Result" |
        ForEach-Object { $_.Line.Trim() } | Select-Object -Unique | ForEach-Object { Write-Host $_ }

    if ($p.ExitCode -ne 0) {
        Write-Host "FAILED (exit $($p.ExitCode)) after $([int]$sw.Elapsed.TotalSeconds)s. Full log: $log" -ForegroundColor Red
        exit $p.ExitCode
    }
    Write-Host "OK in $([int]$sw.Elapsed.TotalSeconds)s." -ForegroundColor Green
}

if ($Setup) { Invoke-Unity "world setup" "PipeWorld.EditorTools.WorldSetup.Run" }

if ($Check) {
    if (-not $Setup) { Invoke-Unity "compile check" $null }
    exit 0
}

Invoke-Unity "building Windows64" "PipeWorld.EditorTools.BuildScript.BuildWindows"

if ($Run) {
    $exe = "$project\Builds\Windows\PipeWorld.exe"
    Write-Host "Launching $exe"
    Start-Process $exe
}
exit 0
