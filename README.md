# Thronefall Multiplayer (updated for Thronefall v2.13)

Co-op multiplayer for Thronefall over Steam lobbies, kept working on the current game version.

This continues a chain of community forks. All credit for the mod itself goes to
**Rikhardur Bjarni Einarsson (BadWolf)**, author of the original
[MunWolf/thronefall_multiplayer](https://github.com/MunWolf/thronefall_multiplayer), who invited anyone to
fork and release their own version as long as they give credit. The previous fork,
[Bwarhness/thronefall_multiplayer](https://github.com/Bwarhness/thronefall_multiplayer) (released on Thunderstore as
ThronefallMultiplayerContinued 1.2.2), stopped working with Thronefall v2.13. Its full history is kept in this repository.

## Changes since 1.2.2

**Fixes for Thronefall v2.13**
- `UnitRespawnerForBuildings.RespawnAKnockedOutUnit` now returns `int`; the hook was rebuilt so the plugin loads again.
- New references (`PackageTools`, `Drawing`, URP runtime) required by the updated pathfinding and camera code.
- `CameraRig.Start` replacement now sets the URP shadow fields, removing a per-frame NullReferenceException.
- The main menu became a campaign carousel; the Multiplayer button is now found by name instead of a fixed path.
- Null guards for a remote player's manual attack and for destroyed players at dawn.

**New**
- Online matches use their own save files (`mp_<level>.json`), so multiplayer never loads or overwrites your singleplayer runs.
- The host's save is sent to clients when a level starts and at every dawn, so everyone continues the same match.
- In-lobby updater: if your build differs from the host's and the host is a Steam friend, you can download the
  host's DLL directly (SHA256-checked) and restart.
- Recent partners: people you played with are remembered, and you are told when one of them is hosting, even when
  Steam shows them as Invisible.
- Hosting copies a `steam://joinlobby/...` link to the clipboard, a fallback when the lobby list does not show the game.

## Install

Easiest: run `Thronefall-Multiplayer-Installer.exe` from the [Releases](../../releases) page. It finds Thronefall via
Steam, closes the game and installs BepInEx plus the mod. The exe is unsigned, so Windows SmartScreen will warn
("More info" -> "Run anyway").

Manual: install [BepInExPack_Thronefall](https://thunderstore.io/c/thronefall/p/BepInEx/BepInExPack_Thronefall/), then
put `com.badwolf.thronefall_mp.dll` in `BepInEx/plugins/ThronefallMultiplayer/`.

All players must run the same build.

## Building

Copy the game DLLs listed in `setup.ps1` (plus `PackageTools`, `Drawing`, `Unity.RenderPipelines.Universal.Runtime`,
`Unity.RenderPipelines.Core.Runtime`) from `Thronefall_Data/Managed` into `lib/`, and `BepInEx/plugins/MMHOOK/MMHOOK_Assembly-CSharp.dll`
from a game folder where BepInEx has run once. Then `dotnet build Main/ThronefallMP.csproj -c Release`.
The game's own DLLs are not part of this repository.

`installer/build-installer.ps1 -Game <Thronefall folder>` packs BepInEx from that game folder and the built DLL into
`dist/Thronefall-Multiplayer-Installer.exe` (uses IExpress, built into Windows).

---

## Previous README (Bwarhness fork)

> **This is a community fork of [MunWolf/thronefall_multiplayer](https://github.com/MunWolf/thronefall_multiplayer) by Rikhardur Bjarni Einarsson (BadWolf).**
> All credit for the mod itself goes to BadWolf. The original repository was archived in February 2024, and BadWolf
> generously invited anyone to *"fork it, release their own version and do whatever they want with it as long as
> they give credit."* This fork exists only to keep the mod working after a Thronefall game update broke the
> original 1.0.5 release.

## What this fork fixes (v1.0.6)

A 2024+ Thronefall update changed several `Assembly-CSharp` APIs that the mod hooks/uses, so the original 1.0.5
build crashed on load with:

```
InvalidOperationException: Parameter #0 of hook for ... TransitionToTarget(...) doesn't match, must be CameraRig or related
```

This fork recompiles the original source against the current game and adapts every broken call site. Notable changes:

- **`CameraRig.TransitionToTarget`** — gained a `float targetCameraSize` parameter, and the private `targetPosition`
  field it relied on was removed. The reimplemented camera coroutine now exits on the camera's actual position and
  eases the orthographic size toward `targetCameraSize` (matching vanilla), fixing both the load crash and a
  potential camera lock-up.
- **`CommandUnits.PlaceCommandedUnitsAndCalculateTargetPositions`** — gained a `bool _smartCommand` parameter.
- **Treasure hunter gold** — `PerkManager.treasureHunterGoldAmount` was split per-wave; rewritten to use the new
  `EnemySpawner.GetTreasureHunterBonus(wave, out amount)`.
- **`Spawn.GetRandomPointOnSpawnLine`** — now requires an `NNConstraint`; supplied with the same flying/large/ground
  priority the game uses.
- **`PlayerInteraction.EquippedWeapon` → `ManualAttack`**, **`EquipWeapon(manual, auto)`**,
  **`PerkManager.UnlockedEquippables` → `allEquippables`**, **`UpgradeCommander.moveSpeedMultiplicator` →
  `CommandUnits.commanderUnitMoveSpeedMulti`**, **`SettingsManager.UseLargeInGameUI`** removed,
  **`UnityEvent.m_PersistentCalls`** accessed via reflection, **`Lerp.Interpolate` → `Mathf.Lerp`**.

### Install (players)

1. Install [BepInEx 5 for Thronefall](https://thunderstore.io/c/thronefall/p/BepInEx/BepInExPack_Thronefall/)
   (e.g. via Thunderstore Mod Manager / r2modman, same as the original mod).
2. Download `com.badwolf.thronefall_mp.dll` from this repo's [Releases](../../releases).
3. Drop it into `BepInEx/plugins/` (replacing the old one if present) and launch the game.

> All players in a session must use the **same** build (the mod performs a version check on connect).

---

## Original README

### Information
This is the development repository for the WIP Thronefall Multiplayer mod.
It uses BepInEx to inject code into the game.

### Development Setup

This is written in CSharp so you will have to acquire an IDE that can read .sln files (for example JetBrains Rider)
You will also need an installation of Thronefall, run the setup.ps1 script to acquire all required dlls from your installation.
After that open the solution file and run the build.

### How to Run

Download BepInEx 6 and copy it into your Thronefall directory.
When you run the build it copies the ThronefallMultiplayer dlls to the correct plugin folder.
Run your executable either directly or through Steam.
