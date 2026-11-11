$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Thronefall {
    $steamPaths = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        $p = Get-ItemProperty $key -ErrorAction SilentlyContinue
        if ($p.SteamPath) { $steamPaths += $p.SteamPath }
        if ($p.InstallPath) { $steamPaths += $p.InstallPath }
    }
    $libraries = @()
    foreach ($steam in $steamPaths | Select-Object -Unique) {
        $steam = $steam -replace '/', '\'
        $libraries += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($line in Get-Content $vdf) {
                if ($line -match '"path"\s+"(.+)"') { $libraries += ($matches[1] -replace '\\\\', '\') }
            }
        }
    }
    foreach ($lib in $libraries | Select-Object -Unique) {
        $dir = Join-Path $lib 'steamapps\common\Thronefall'
        if (Test-Path (Join-Path $dir 'thronefall.exe')) { return $dir }
    }
    return $null
}

Write-Host ''
Write-Host '  Thronefall Multiplayer - installation' -ForegroundColor Yellow
Write-Host ''

$game = Find-Thronefall
if (-not $game) {
    Write-Host 'Could not find Thronefall automatically.'
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Select the folder that contains thronefall.exe'
    if ($dlg.ShowDialog() -ne 'OK') { throw 'Cancelled.' }
    $game = $dlg.SelectedPath
    if (-not (Test-Path (Join-Path $game 'thronefall.exe'))) { throw "thronefall.exe not found in $game" }
}
Write-Host "Game folder: $game"

if (Get-Process thronefall -ErrorAction SilentlyContinue) {
    Write-Host 'Closing Thronefall...'
    Stop-Process -Name thronefall -Force
    Start-Sleep 2
}

$tmp = Join-Path $env:TEMP ('tfmp_' + [guid]::NewGuid().ToString('N'))
Expand-Archive (Join-Path $here 'payload.zip') $tmp
Copy-Item (Join-Path $tmp '*') $game -Recurse -Force
Remove-Item $tmp -Recurse -Force

$recent = Join-Path $game 'BepInEx\config\thronefall_mp_recent.txt'
if (-not (Test-Path $recent) -and (Test-Path (Join-Path $here 'recent.txt'))) {
    Copy-Item (Join-Path $here 'recent.txt') $recent
}

$dll = Join-Path $game 'BepInEx\plugins\ThronefallMultiplayer\com.badwolf.thronefall_mp.dll'
foreach ($f in $dll, (Join-Path $game 'winhttp.dll'), (Join-Path $game 'MonoMod.exe'), (Join-Path $game 'MonoMod.RuntimeDetour.HookGen.exe')) {
    if (-not (Test-Path $f)) { throw "Missing after install: $f (antivirus?)" }
}

Write-Host ''
Write-Host 'Done!' -ForegroundColor Green
Write-Host 'Start Thronefall. The first launch takes a little longer.'
Write-Host 'The main menu now has a "Multiplayer" button, and the mod version is shown in the bottom right.'
