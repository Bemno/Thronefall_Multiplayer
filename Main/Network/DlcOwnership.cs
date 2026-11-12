using System.Collections.Generic;
using Steamworks;
using ThronefallMP.UI;

namespace ThronefallMP.Network;

// Every lobby member publishes whether they own Craaghelm as lobby member data, so whoever starts a level can
// refuse Craaghelm scenes while someone in the lobby is missing the DLC. A member without the key (not yet
// published) counts as not owning it.
public static class DlcOwnership
{
    private const string Key = "dlc_craaghelm";

    public static bool LocalOwnsCraaghelm => DlcConfiguration.IsDlcAvailable(IDlcProvider.DlcId.CRAAGHELM);

    public static void Publish(CSteamID lobby)
    {
        if (lobby.IsValid())
        {
            SteamMatchmaking.SetLobbyMemberData(lobby, Key, LocalOwnsCraaghelm ? "1" : "0");
        }
    }

    public static bool RequiresCraaghelm(string scene)
    {
        if (string.IsNullOrEmpty(scene))
        {
            return false;
        }

        var manager = SceneTransitionManager.instance;
        return DlcConfiguration.IsDlcMap(IDlcProvider.DlcId.CRAAGHELM, scene)
            || (manager != null && scene == manager.levelSelectSceneCraaghelm);
    }

    public static bool IsLevelSelect(string scene)
    {
        var manager = SceneTransitionManager.instance;
        return manager != null && (scene == manager.levelSelectScene || scene == manager.levelSelectSceneCraaghelm);
    }

    public static List<string> MembersMissingCraaghelm(CSteamID lobby)
    {
        var missing = new List<string>();
        if (!lobby.IsValid())
        {
            return missing;
        }

        for (int i = 0, count = SteamMatchmaking.GetNumLobbyMembers(lobby); i < count; ++i)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
            if (SteamMatchmaking.GetLobbyMemberData(lobby, member, Key) != "1")
            {
                missing.Add(SteamFriends.GetFriendPersonaName(member));
            }
        }

        return missing;
    }

    // Returns true (and tells the local player why) when the transition must not happen.
    public static bool BlockTransition(string scene)
    {
        if (!Plugin.Instance.Network.Online || !RequiresCraaghelm(scene))
        {
            return false;
        }

        var missing = MembersMissingCraaghelm(Plugin.Instance.Network.Lobby);
        if (missing.Count == 0)
        {
            return false;
        }

        Plugin.Log.LogInfo($"Blocked '{scene}': Craaghelm missing for {string.Join(", ", missing)}");
        UIManager.CreateMessageDialog(
            "Craaghelm DLC required",
            "Everyone in the lobby needs the Craaghelm DLC to play this.\nMissing: " + string.Join(", ", missing));
        return true;
    }
}
