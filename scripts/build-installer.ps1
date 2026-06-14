param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
$project = Join-Path $root 'src\GamePulseMonitor\GamePulseMonitor.csproj'
$artifacts = Join-Path $root 'artifacts'
$packageRoot = Join-Path $artifacts 'package'
$publishDir = Join-Path $packageRoot 'publish'
$stagingDir = Join-Path $packageRoot 'staging'
$payloadZip = Join-Path $stagingDir 'payload.zip'
$setupExe = Join-Path $artifacts 'GamePulseMonitorSetup.exe'
$portableZip = Join-Path $artifacts 'GamePulseMonitor-portable-win-x64.zip'
$litePackageRoot = Join-Path $artifacts 'package-lite'
$litePublishDir = Join-Path $litePackageRoot 'publish'
$liteStagingDir = Join-Path $litePackageRoot 'staging'
$litePayloadZip = Join-Path $liteStagingDir 'payload.zip'
$liteSetupExe = Join-Path $artifacts 'GamePulseMonitorSetup-Lite.exe'
$litePortableZip = Join-Path $artifacts 'GamePulseMonitor-portable-lite-win-x64.zip'

function Remove-TreeSafe {
    param(
        [string]$Path,
        [string]$AllowedRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullAllowedRoot = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullAllowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove '$fullPath' outside '$fullAllowedRoot'."
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force
}

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "Missing local dotnet runtime: $dotnet"
}

if (-not (Get-Command iexpress.exe -ErrorAction SilentlyContinue)) {
    throw 'IExpress is not available on this Windows installation.'
}

& (Join-Path $PSScriptRoot 'generate-icon.ps1')

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Remove-TreeSafe -Path $packageRoot -AllowedRoot $artifacts
New-Item -ItemType Directory -Force -Path $publishDir, $stagingDir | Out-Null

& $dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    -m:1 `
    --self-contained true `
    -o $publishDir `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:UseSharedCompilation=false

Get-ChildItem -Path $publishDir -Recurse -File -Filter '*.pdb' | Remove-Item -Force

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $payloadZip -Force
[System.IO.File]::Copy($payloadZip, $portableZip, $true)

$installCmd = @'
@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
exit /b %ERRORLEVEL%
'@

$installPs1 = @'
$ErrorActionPreference = 'Stop'
$requiresDesktopRuntime = $false
$desktopRuntimeDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/8.0'
$desktopRuntimeMissingMessage = 'R2FtZVB1bHNlTW9uaXRvciBMaXRlIOmcgOimgeWFiOWuieijhSBNaWNyb3NvZnQgLk5FVCA4IERlc2t0b3AgUnVudGltZSAoeDY0KeOAgg0KDQrngrnlh7vigJznoa7lrprigJ3lkI7lsIbmiZPlvIDlvq7ova/lrpjmlrnkuIvovb3pobXpnaLvvJoNCnswfQ0KDQrlronoo4XlrozmiJDlkI7vvIzor7fph43mlrDov5DooYwgTGl0ZSDlronoo4XljIXjgII='

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-DesktopRuntime {
    $runtimeRoots = @(
        $(if ($env:ProgramW6432) { Join-Path $env:ProgramW6432 'dotnet\shared\Microsoft.WindowsDesktop.App' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.WindowsDesktop.App' })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($runtimeRoot in $runtimeRoots) {
        if ((Test-Path -LiteralPath $runtimeRoot) -and
            [bool](Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like '8.*' } |
                Select-Object -First 1)) {
            return $true
        }
    }

    $dotnetCandidates = @(
        $(if ($env:ProgramW6432) { Join-Path $env:ProgramW6432 'dotnet\dotnet.exe' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }),
        'dotnet.exe'
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($dotnet in $dotnetCandidates) {
        try {
            $runtimes = & $dotnet --list-runtimes 2>$null
            if ($runtimes -match '^Microsoft\.WindowsDesktop\.App 8\.') {
                return $true
            }
        }
        catch {
        }
    }

    return $false
}

function Assert-DesktopRuntimeForLite {
    if (-not $requiresDesktopRuntime -or (Test-DesktopRuntime)) {
        return
    }

    $message = ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($desktopRuntimeMissingMessage)) -f $desktopRuntimeDownloadUrl)
    Write-Host ''
    Write-Host $message
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show(
        $message,
        'GamePulseMonitor Lite',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null

    Start-Process $desktopRuntimeDownloadUrl
    exit 1603
}

Assert-DesktopRuntimeForLite

function Invoke-ElevatedFromStage {
    $stage = Join-Path $env:TEMP ('GamePulseMonitorSetup-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Destination $stage -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'payload.zip') -Destination $stage -Force

    $script = Join-Path $stage 'install.ps1'
    $process = Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $script) `
        -Verb RunAs `
        -Wait `
        -PassThru

    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    exit $process.ExitCode
}

if (-not (Test-Admin)) {
    Invoke-ElevatedFromStage
}

$payload = Join-Path $PSScriptRoot 'payload.zip'
if (-not (Test-Path -LiteralPath $payload)) {
    throw "Missing installer payload: $payload"
}

function Select-InstallDirectory {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'GamePulseMonitor Setup'
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.ClientSize = [System.Drawing.Size]::new(540, 156)

    $label = New-Object System.Windows.Forms.Label
    $label.Text = 'Install folder'
    $label.AutoSize = $true
    $label.Location = [System.Drawing.Point]::new(16, 18)
    $form.Controls.Add($label)

    $textBox = New-Object System.Windows.Forms.TextBox
    $textBox.Text = 'D:\GamePulseMonitor'
    $textBox.Location = [System.Drawing.Point]::new(16, 44)
    $textBox.Size = [System.Drawing.Size]::new(410, 24)
    $form.Controls.Add($textBox)

    $browseButton = New-Object System.Windows.Forms.Button
    $browseButton.Text = 'Browse'
    $browseButton.Location = [System.Drawing.Point]::new(438, 42)
    $browseButton.Size = [System.Drawing.Size]::new(82, 28)
    $browseButton.Add_Click({
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = 'Choose the GamePulseMonitor install folder'
        $dialog.SelectedPath = $(if (Test-Path -LiteralPath $textBox.Text) { $textBox.Text } elseif (Test-Path -LiteralPath 'D:\') { 'D:\' } else { [Environment]::GetFolderPath('Desktop') })
        if ($dialog.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
            $textBox.Text = $dialog.SelectedPath
        }
        $dialog.Dispose()
    })
    $form.Controls.Add($browseButton)

    $hint = New-Object System.Windows.Forms.Label
    $hint.Text = 'The app needs administrator permission when it captures FPS.'
    $hint.ForeColor = [System.Drawing.Color]::FromArgb(90, 90, 90)
    $hint.AutoSize = $true
    $hint.Location = [System.Drawing.Point]::new(16, 78)
    $form.Controls.Add($hint)

    $installButton = New-Object System.Windows.Forms.Button
    $installButton.Text = 'Install'
    $installButton.Location = [System.Drawing.Point]::new(346, 112)
    $installButton.Size = [System.Drawing.Size]::new(82, 30)
    $installButton.Add_Click({
        $path = $textBox.Text.Trim().Trim('"')
        if ([string]::IsNullOrWhiteSpace($path)) {
            [System.Windows.Forms.MessageBox]::Show($form, 'Choose an install folder.', 'GamePulseMonitor Setup') | Out-Null
            return
        }

        $root = [System.IO.Path]::GetPathRoot($path)
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path -LiteralPath $root)) {
            [System.Windows.Forms.MessageBox]::Show($form, "Drive not found: $root", 'GamePulseMonitor Setup') | Out-Null
            return
        }

        $form.Tag = [System.IO.Path]::GetFullPath($path)
        $form.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $form.Close()
    })
    $form.Controls.Add($installButton)
    $form.AcceptButton = $installButton

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = 'Cancel'
    $cancelButton.Location = [System.Drawing.Point]::new(438, 112)
    $cancelButton.Size = [System.Drawing.Size]::new(82, 30)
    $cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $form.Controls.Add($cancelButton)
    $form.CancelButton = $cancelButton

    $result = $form.ShowDialog()
    $selected = [string]$form.Tag
    $form.Dispose()

    if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
        exit 1223
    }

    return $selected
}

$installDir = Select-InstallDirectory
$tempExtract = Join-Path $env:TEMP ('GamePulseMonitorPayload-' + [Guid]::NewGuid().ToString('N'))

foreach ($name in @('GamePulseMonitor', 'PresentMon')) {
    Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
New-Item -ItemType Directory -Force -Path $tempExtract | Out-Null
try {
    Expand-Archive -LiteralPath $payload -DestinationPath $tempExtract -Force
    Copy-Item -Path (Join-Path $tempExtract '*') -Destination $installDir -Recurse -Force
}
finally {
    Remove-Item -LiteralPath $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
}

$exe = Join-Path $installDir 'GamePulseMonitor.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Installed executable was not found: $exe"
}

function New-Shortcut {
    param(
        [string]$Path,
        [string]$TargetPath,
        [string]$WorkingDirectory
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Description = 'GamePulseMonitor'
    $shortcut.Save()
}

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'GamePulseMonitor.lnk'
$programsDir = [Environment]::GetFolderPath('CommonPrograms')
$startMenuDir = Join-Path $programsDir 'GamePulseMonitor'
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null
$startMenuShortcut = Join-Path $startMenuDir 'GamePulseMonitor.lnk'

New-Shortcut -Path $desktopShortcut -TargetPath $exe -WorkingDirectory $installDir
New-Shortcut -Path $startMenuShortcut -TargetPath $exe -WorkingDirectory $installDir

Write-Host ''
Write-Host 'GamePulseMonitor installed successfully.'
Write-Host "Install path: $installDir"
Write-Host 'Use the desktop or Start Menu shortcut to launch it.'
'@

Set-Content -LiteralPath (Join-Path $stagingDir 'install.cmd') -Value $installCmd -Encoding ASCII
Set-Content -LiteralPath (Join-Path $stagingDir 'install.ps1') -Value $installPs1 -Encoding UTF8

$sedPath = Join-Path $packageRoot 'GamePulseMonitorSetup.sed'
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=GamePulseMonitor installed.
TargetName=$setupExe
FriendlyName=GamePulseMonitor Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=install.cmd
UserQuietInstCmd=install.cmd
SourceFiles=SourceFiles

[Strings]
FILE0="install.cmd"
FILE1="install.ps1"
FILE2="payload.zip"

[SourceFiles]
SourceFiles0=$stagingDir\

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@

Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

if (Test-Path -LiteralPath $setupExe) {
    Remove-Item -LiteralPath $setupExe -Force
}

$packageStart = Get-Date
$iexpress = Start-Process -FilePath 'iexpress.exe' -ArgumentList @('/N', '/Q', $sedPath) -PassThru
$iexpress.WaitForExit()

$deadline = (Get-Date).AddMinutes(10)
do {
    $activePackagers = Get-Process -Name iexpress, makecab -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $packageStart }

    if (-not $activePackagers) {
        break
    }

    Start-Sleep -Seconds 1
} while ((Get-Date) -lt $deadline)

if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "IExpress did not create the installer: $setupExe"
}

$iconPath = Join-Path $root 'src\GamePulseMonitor\Assets\GamePulseMonitor.ico'
& (Join-Path $PSScriptRoot 'set-exe-icon.ps1') -ExePath $setupExe -IconPath $iconPath

Remove-TreeSafe -Path $litePackageRoot -AllowedRoot $artifacts
New-Item -ItemType Directory -Force -Path $litePublishDir, $liteStagingDir | Out-Null

& $dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    -m:1 `
    --self-contained false `
    -o $litePublishDir `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:UseSharedCompilation=false

Get-ChildItem -Path $litePublishDir -Recurse -File -Filter '*.pdb' | Remove-Item -Force

Compress-Archive -Path (Join-Path $litePublishDir '*') -DestinationPath $litePayloadZip -Force
[System.IO.File]::Copy($litePayloadZip, $litePortableZip, $true)

$liteInstallPs1 = $installPs1.Replace('$requiresDesktopRuntime = $false', '$requiresDesktopRuntime = $true')
Set-Content -LiteralPath (Join-Path $liteStagingDir 'install.cmd') -Value $installCmd -Encoding ASCII
Set-Content -LiteralPath (Join-Path $liteStagingDir 'install.ps1') -Value $liteInstallPs1 -Encoding UTF8

$liteSedPath = Join-Path $litePackageRoot 'GamePulseMonitorSetup-Lite.sed'
$liteSed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=1
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=GamePulseMonitor Lite installed.
TargetName=$liteSetupExe
FriendlyName=GamePulseMonitor Lite Setup
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=install.cmd
UserQuietInstCmd=install.cmd
SourceFiles=SourceFiles

[Strings]
FILE0="install.cmd"
FILE1="install.ps1"
FILE2="payload.zip"

[SourceFiles]
SourceFiles0=$liteStagingDir\

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@

Set-Content -LiteralPath $liteSedPath -Value $liteSed -Encoding ASCII

if (Test-Path -LiteralPath $liteSetupExe) {
    Remove-Item -LiteralPath $liteSetupExe -Force
}

$litePackageStart = Get-Date
$liteIexpress = Start-Process -FilePath 'iexpress.exe' -ArgumentList @('/N', '/Q', $liteSedPath) -PassThru
$liteIexpress.WaitForExit()

$liteDeadline = (Get-Date).AddMinutes(10)
do {
    $activePackagers = Get-Process -Name iexpress, makecab -ErrorAction SilentlyContinue |
        Where-Object { $_.StartTime -ge $litePackageStart }

    if (-not $activePackagers) {
        break
    }

    Start-Sleep -Seconds 1
} while ((Get-Date) -lt $liteDeadline)

if (-not (Test-Path -LiteralPath $liteSetupExe)) {
    throw "IExpress did not create the lite installer: $liteSetupExe"
}

& (Join-Path $PSScriptRoot 'set-exe-icon.ps1') -ExePath $liteSetupExe -IconPath $iconPath

Write-Host "Installer: $setupExe"
Write-Host "Portable zip: $portableZip"
Write-Host "Lite installer: $liteSetupExe"
Write-Host "Lite portable zip: $litePortableZip"
