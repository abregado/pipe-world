<#
.SYNOPSIS
  Launches the built game, captures one frame to a PNG, and exits.
.EXAMPLE
  .\shot.ps1 -Time 0.5 -Out Builds\Shots\noon.png
  .\shot.ps1 -Pos "0,1.7,-8" -Look "0,10"     # yaw,pitch in degrees
  .\shot.ps1 -Keys "W,LeftShift" -Hold 2      # hold keys for 2s; final position is logged to Player.log
  .\shot.ps1 -Build -Setup -Time 0            # rebuild first
#>
param(
    [string]$Out = "Builds\Shots\shot.png",
    [double]$Time = -1,
    [string]$Pos,
    [string]$Look,
    [string]$Keys,
    [int]$Frames = 0,
    [double]$Hold = 0,
    [int]$Width = 1280,
    [int]$Height = 720,
    [string]$Respawn,
    [string]$Click,
    [double]$Grade = -1,
    [double]$Ambient = -1,
    [double]$ShadowRange = -1,
    [double]$LightScale = -1,
    [switch]$LightsOnly,
    [switch]$Probe,
    [int]$DebugView = 0,
    [switch]$LitTest,
    [switch]$NoShadowedGrade,
    [switch]$HardShadows,
    [switch]$LightTest,
    [string]$GraphicsApi,
    [string]$DisableFeature,
    [string[]]$Extra,
    [switch]$Menu,
    [switch]$Build,
    [switch]$Setup
)

$ErrorActionPreference = "Stop"
$project = $PSScriptRoot
if ($Build -or $Setup) {
    $buildArgs = @{}
    if ($Setup) { $buildArgs.Setup = $true }
    & "$project\build.ps1" @buildArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$exe = "$project\Builds\Windows\PipeWorld.exe"
if (-not (Test-Path $exe)) { throw "No build at $exe. Run .\build.ps1 first." }

$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $project $Out }
New-Item -ItemType Directory -Force (Split-Path $outPath) | Out-Null
Remove-Item $outPath -ErrorAction SilentlyContinue

$gameArgs = @("-screen-fullscreen", "0", "-screen-width", $Width, "-screen-height", $Height, "-shot", "`"$outPath`"")
if ($Time -ge 0) { $gameArgs += @("-shotTime", $Time.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($Pos) { $gameArgs += @("-shotPos", $Pos) }
if ($Look) { $gameArgs += @("-shotLook", $Look) }
if ($Ambient -ge 0) { $gameArgs += @("-shotAmbient", $Ambient.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($Grade -ge 0) { $gameArgs += @("-shotGrade", $Grade.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($Click) { $gameArgs += @("-shotClick", $Click) }
if ($Respawn) { $gameArgs += @("-shotRespawn", $Respawn) }
if ($ShadowRange -ge 0) { $gameArgs += @("-shotShadowRange", $ShadowRange.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($LightScale -ge 0) { $gameArgs += @("-shotLightScale", $LightScale.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($DisableFeature) { $gameArgs += @("-shotDisableFeature", $DisableFeature) }
if ($GraphicsApi) { $gameArgs += "-force-$GraphicsApi" }
if ($LightTest) { $gameArgs += "-shotLightTest" }
if ($HardShadows) { $gameArgs += "-shotHardShadows" }
if ($NoShadowedGrade) { $gameArgs += "-shotNoShadowedGrade" }
if ($LitTest) { $gameArgs += "-shotLitTest" }
if ($LightsOnly) { $gameArgs += "-shotLightsOnly" }
if ($Probe) { $gameArgs += "-shotProbe" }
if ($DebugView -gt 0) { $gameArgs += @("-shotDebugView", $DebugView) }
if ($Menu) { $gameArgs += "-shotMenu" }
if ($Keys) { $gameArgs += @("-shotKeys", $Keys) }
if ($Hold -gt 0) { $gameArgs += @("-shotHold", $Hold.ToString([Globalization.CultureInfo]::InvariantCulture)) }
if ($Frames -gt 0) { $gameArgs += @("-shotFrames", $Frames) }
if ($Extra) { $gameArgs += $Extra }

$p = Start-Process -FilePath $exe -ArgumentList $gameArgs -PassThru
if (-not $p.WaitForExit(60000)) { $p.Kill(); throw "Game did not exit within 60s." }
if (-not (Test-Path $outPath)) { throw "No screenshot written. Check %USERPROFILE%\AppData\LocalLow\DefaultCompany\pipe-world\Player.log" }
Write-Host "Saved $outPath"
