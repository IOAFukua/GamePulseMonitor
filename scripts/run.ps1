param(
    [string]$ProcessName,
    [switch]$NoClickThrough,
    [double]$Left = 24,
    [double]$Top = 24,
    [string]$LogDirectory,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$project = Join-Path $root 'src\GamePulseMonitor\GamePulseMonitor.csproj'
$publishDir = Join-Path $root 'artifacts\GamePulseMonitor'
$exe = Join-Path $publishDir 'GamePulseMonitor.exe'

$appArgs = @('--left', $Left, '--top', $Top)
if ($ProcessName) {
    $appArgs += @('--process', $ProcessName)
}
if ($NoClickThrough) {
    $appArgs += '--no-clickthrough'
}
if ($LogDirectory) {
    $appArgs += @('--log-dir', $LogDirectory)
}

if ($Build -or -not (Test-Path $exe)) {
    & $dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDir
}

Start-Process -FilePath $exe -ArgumentList $appArgs -Verb RunAs
