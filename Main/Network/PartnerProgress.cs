using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using Thronefall.SaveServices;
using ThronefallMP.UI;

namespace ThronefallMP.Network;

// Online, meta progress (perk level, XP, beaten levels) comes from a save file shared only with the current
// group of players, so a group starts from zero and keeps what it unlocked together. The singleplayer save is
// never written while a group profile is active and is reloaded when going back offline.
public static class PartnerProgress
{
    private static ConfigEntry<bool> _enabled;
    private static string _soloBase;
    private static string _soloCraaghelm;

    public static string ActiveKey { get; private set; }

    public static void Bind(ConfigFile config)
    {
        _enabled = config.Bind("Progress", "SeparateProgressPerPartner", true,
            "Online, use progress (perks, beaten levels) shared only with the players you are playing with. " +
            "Each group starts from zero. Your singleplayer save is not touched.");
    }

    // Returns true when the active profile changed.
    public static bool Use(IEnumerable<CSteamID> others)
    {
        var ids = others.Select(o => o.m_SteamID).Where(id => id != 0).Distinct().OrderBy(id => id).ToList();
        if (_enabled == null || !_enabled.Value || ids.Count == 0)
        {
            return false;
        }

        var key = string.Join("_", ids);
        var manager = SaveLoadManager.instance;
        if (key == ActiveKey || manager == null)
        {
            return false;
        }

        var traverse = Traverse.Create(manager);
        if (ActiveKey == null)
        {
            _soloBase = traverse.Field<string>("saveFileName").Value;
            _soloCraaghelm = traverse.Field<string>("saveFileNameCraaghelm").Value;
            Backup(_soloBase);
            Backup(_soloCraaghelm);
        }

        var profileBase = $"mp_{key}_{_soloBase}";
        var isNew = !File.Exists(SaveServicesLocator.Instance.GetFullSavePath(profileBase));
        traverse.Field("saveFileName").SetValue(profileBase);
        traverse.Field("saveFileNameCraaghelm").SetValue($"mp_{key}_{_soloCraaghelm}");
        ActiveKey = key;
        Reload(manager);

        var names = string.Join(", ", ids.Select(id => SteamFriends.GetFriendPersonaName(new CSteamID(id))));
        Plugin.Log.LogInfo($"Progress> using {(isNew ? "new" : "saved")} progress with {names} ({profileBase})");
        if (isNew)
        {
            UIManager.CreateMessageDialog("Shared progress",
                $"First game with {names}.\nYou start from zero and keep what you unlock together.\n" +
                "Your singleplayer progress is untouched.");
        }

        return true;
    }

    public static void Restore()
    {
        var manager = SaveLoadManager.instance;
        if (ActiveKey == null || manager == null)
        {
            return;
        }

        var traverse = Traverse.Create(manager);
        traverse.Field("saveFileName").SetValue(_soloBase);
        traverse.Field("saveFileNameCraaghelm").SetValue(_soloCraaghelm);
        Plugin.Log.LogInfo($"Progress> back to singleplayer progress (was {ActiveKey})");
        ActiveKey = null;
        Reload(manager);
    }

    // Mirrors SaveLoadManager.LoadGame without GameProgressResetter.AfterPlaytestReset, which deletes
    // Craaghelm match saves whenever the (here: profile) Craaghelm save file does not exist yet.
    public static void Reload()
    {
        if (SaveLoadManager.instance != null)
        {
            Reload(SaveLoadManager.instance);
        }
    }

    private static void Reload(SaveLoadManager manager)
    {
        PerkManager.ResetAllProgress();
        LevelProgressManager.instance.SceneNameToLevelData.Clear();

        var traverse = Traverse.Create(manager);
        traverse.Field("levelProgressManager").SetValue(LevelProgressManager.instance);
        traverse.Field("perkManager").SetValue(PerkManager.instance);
        traverse.Field("playerWardrobeManager").SetValue(PlayerWardrobeManager.instance);

        string[] lines = null;
        try
        {
            var path = SaveServicesLocator.Instance.GetFullSavePath(manager.BaseGameSaveName);
            if (File.Exists(path))
            {
                lines = SaveServicesLocator.Instance.ReadAllLinesSafe(path);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Progress> could not read {manager.BaseGameSaveName}: {e.Message}");
        }

        if (lines != null)
        {
            traverse.Field("loadData").SetValue(lines);
            // Loads the Craaghelm save afterwards through OnContentLoadedForRealm.
            traverse.Method("LoadDataFromFile", IDlcProvider.DlcId.UNKNOWN).GetValue();
        }
        else
        {
            traverse.Field<UnityEngine.Events.UnityEvent<IDlcProvider.DlcId>>("OnContentLoadedForRealm").Value
                .Invoke(IDlcProvider.DlcId.UNKNOWN);
        }

        foreach (var item in PerkManager.instance.CurrentlyEquipped.ToList())
        {
            if (item != null && !item.IsUnlocked)
            {
                PerkManager.SetEquipped(item, false);
            }
        }
    }

    private static void Backup(string name)
    {
        try
        {
            var path = SaveServicesLocator.Instance.GetFullSavePath(name);
            var backup = path + ".mp-backup";
            if (File.Exists(path) && !File.Exists(backup))
            {
                File.Copy(path, backup);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Progress> backup of {name} failed: {e.Message}");
        }
    }
}
