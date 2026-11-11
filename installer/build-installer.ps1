param(
    [string]$Game = 'C:\Program Files (x86)\Steam\steamapps\common\Thronefall',
    [string]$Out
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here
if (-not $Out) { $Out = Join-Path $repo 'dist\Thronefall-Multiplayer-Installer.exe' }
New-Item -ItemType Directory (Split-Path -Parent $Out) -Force | Out-Null
$work = Join-Path $env:TEMP 'tfmp_installer'
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory "$work\payload\BepInEx\plugins\ThronefallMultiplayer" -Force | Out-Null

foreach ($f in 'winhttp.dll', 'doorstop_config.ini', 'MonoMod.exe', 'MonoMod.RuntimeDetour.HookGen.exe') { Copy-Item "$Game\$f" "$work\payload\" }
foreach ($d in 'core', 'patchers') { Copy-Item "$Game\BepInEx\$d" "$work\payload\BepInEx\" -Recurse }
New-Item -ItemType Directory "$work\payload\BepInEx\config" -Force | Out-Null
Copy-Item "$Game\BepInEx\config\BepInEx.cfg" "$work\payload\BepInEx\config\"
Copy-Item "$Game\BepInEx\plugins\ConfigurationManager.dll" "$work\payload\BepInEx\plugins\"
Copy-Item "$repo\bin\Main\Release\netstandard2.1\com.badwolf.thronefall_mp.dll" "$work\payload\BepInEx\plugins\ThronefallMultiplayer\"
Compress-Archive "$work\payload\*" "$work\payload.zip"
Copy-Item "$here\install.ps1", "$here\install.cmd" $work

# recent.txt is optional and kept out of git: it pre-fills the recent partners list with the builder's SteamID.
$files = @('install.cmd', 'install.ps1', 'payload.zip')
if (Test-Path "$here\recent.txt") {
    Copy-Item "$here\recent.txt" $work
    $files += 'recent.txt'
}
$fileStrings = ($files | ForEach-Object -Begin { $i = 0 } -Process { "FILE$i=`"$_`""; $i++ }) -join "`r`n"
$fileEntries = (0..($files.Count - 1) | ForEach-Object { "%FILE$_%=" }) -join "`r`n"

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$Out
FriendlyName=Thronefall Multiplayer
AppLaunched=cmd /c install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
$fileStrings
[SourceFiles]
SourceFiles0=$work\
[SourceFiles0]
$fileEntries
"@
Set-Content "$work\installer.sed" $sed -Encoding ASCII
Remove-Item $Out -ErrorAction SilentlyContinue
Start-Process iexpress.exe -ArgumentList '/N', '/Q', "$work\installer.sed" -Wait
if (-not (Test-Path $Out)) { throw 'IExpress did not create an exe.' }
Get-Item $Out | Select-Object Name, Length
