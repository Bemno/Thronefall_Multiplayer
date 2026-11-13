# Thronefall Multiplayer (updated for Thronefall v2.13)

Co-op multiplayer for Thronefall over Steam lobbies, kept working on the current game version.

This continues a chain of community forks. All credit for the mod itself goes to
**Rikhardur Bjarni Einarsson (BadWolf)**, author of the original
[MunWolf/thronefall_multiplayer](https://github.com/MunWolf/thronefall_multiplayer), who invited anyone to
fork and release their own version as long as they give credit. The previous fork,
[Bwarhness/thronefall_multiplayer](https://github.com/Bwarhness/thronefall_multiplayer) (released on Thunderstore as
ThronefallMultiplayerContinued 1.2.2), stopped working with Thronefall v2.13. Its full history is kept in this repository.

This fork was developed with the help of Claude (Anthropic), an AI assistant, which wrote much of the code
and documentation under human direction and in-game testing.

## 1.3.6: walls, wins and unit commands

- Walls built by a client are built and paid for as one group, like the host's own builds. Before, every wall piece was
  requested and charged separately, the gold ran out halfway and parts of the wall were missing.
- A win is saved for every player: a level change started by another player now waits until your own end screen has
  saved the beaten level, score and XP.
- Calling units: an empty commanding state no longer blocks calling new units, and knocked-out units are dropped
  from a player's group so they can be called again after respawning.

## 1.3.5: gates, host migration and restarts

- Gates in walls built by another player are now built for everyone. Before, the host could end up with a hole in
  the wall, and enemies walked straight to the castle.
- Starting a level over now starts fresh for everyone, also when the host presses New game.
- The Defeat/Victory screen now closes for every player when the level changes.
- If the host leaves, another player becomes host and lands in level select; Continue goes on from the last dawn,
  and the old host can join again.
- After an update the game restarts by itself.

## 1.3.3: fixes and updates from GitHub

- The host's match save now reaches the other players again (broken in 1.3.0), so Retry Day and continuing a run
  load the same buildings for everyone. Starting a level over now starts fresh for everyone.
- The mod checks GitHub for a newer release when the game starts and can install it from the title screen
  (turn off with `Update.CheckGitHub`).
- The host window remembers name, password, player count and options.
- Units can be called again after being placed; previously they could only be called once.
- Building health is synced from each slot's own building, which should fix new walls looking partly destroyed.
- The tutorial no longer shows its "dead" tip for living players online.
- Leaving a level after a defeat no longer floods errors and stalls the After Match screen.

## 1.3.0: progress per group of players

- Online, progress (perk level, XP, beaten levels, highscores) is kept per group of players. The first game with a
  friend starts from zero, and the next time you play together you keep what you unlocked together. Your singleplayer
  save is never written while playing online and is loaded again when you return to the main menu.
- Match saves are kept per group too, so a run with one friend does not continue with another.
- The original save files are copied once to `*.sav.mp-backup`. Turn the feature off with
  `Progress.SeparateProgressPerPartner` in the mod's config.

## 1.2.9: joining works again

- Fixed joining hanging on "Connecting". The host ignored the join request when a Steam connection to that player
  was already open (the recent-partners list opens one), and did not read any messages before the first player was
  approved. A Steam session that failed once also refused all later messages; sessions now restart automatically.
- The Multiplayer window shows a **Recent players** row: everyone you played with, with a **Join** button for those
  currently hosting.

## Craaghelm DLC support (1.2.7, experimental)

1.2.7 adds support for the Craaghelm DLC. It has only been tested in singleplayer so far; online play on
Craaghelm maps is untested. The same build works with and without the DLC.

- Energy cores are a shared currency like gold, building boosts are synced, thieves steal once, and enemies that
  split on death spawn the same copies for everyone.
- DLC weapons and perks are synced in the loadout.
- The host is told which player is missing the DLC when a Craaghelm map is started; base-game maps still work.
- Build slots and their units are now numbered in a stable traversal order, because Craaghelm maps nest build slots
  in groups and the old numbering put purchases on the wrong building.
- Fixes for errors with the Craaghelm player rig and the barrage weapon's steering.

Known gaps: boss phase changes and randomness, energy objects charged at certain waves, and both ends of tunnels
may not be synced yet.

## Changes since 1.2.2 (up to 1.2.6)

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
- Invites: the host gets a popup with the invite after loading in, **Copy Invite** in the Esc menu copies it again, and
  **Direct Connect** (or Ctrl+V) in the Multiplayer menu joins the lobby from an invite on the clipboard.

## Install

Easiest: run `Thronefall-Multiplayer-Installer.exe` from the [Releases](../../releases) page. It finds Thronefall via
Steam, closes the game and installs BepInEx plus the mod. The exe is unsigned, so Windows SmartScreen will warn
("More info" -> "Run anyway").

Manual:

1. Find the game folder: in Steam, right-click Thronefall -> Manage -> Browse local files. It is usually
   `C:\Program Files (x86)\Steam\steamapps\common\Thronefall\` or `<your Steam library>\steamapps\common\Thronefall\`.
2. Extract [BepInExPack_Thronefall](https://thunderstore.io/c/thronefall/p/BepInEx/BepInExPack_Thronefall/) into that
   folder, so that `winhttp.dll` and the `BepInEx` folder sit next to `Thronefall.exe`.
3. Start the game once and close it, so BepInEx creates its folders.
4. Put `com.badwolf.thronefall_mp.dll` in `Thronefall\BepInEx\plugins\ThronefallMultiplayer\` (create the
   `ThronefallMultiplayer` folder if it does not exist).

Resulting layout:

```
Thronefall\
  Thronefall.exe
  winhttp.dll
  BepInEx\
    plugins\
      ThronefallMultiplayer\
        com.badwolf.thronefall_mp.dll
```

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

---

## Original README

### Information
This is the development repository for the WIP Thronefall Multiplayer mod.
It uses BepInEx to inject code into the game.

### Development Setup

This is written in CSharp so you will have to acquire an IDE that can read .sln files (for example JetBrains Rider)
You will also need an installation of Thronefall, run the setup.ps1 script to acquire all required dlls from your installation.
After that open the solution file and run the build.

