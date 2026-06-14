param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$project = Join-Path $root 'src\GamePulseMonitor\GamePulseMonitor.csproj'
$output = Join-Path $root 'artifacts\GamePulseMonitor'

& $dotnet publish $project -c $Configuration -r win-x64 --self-contained false -o $output
Write-Host "Published to $output"
