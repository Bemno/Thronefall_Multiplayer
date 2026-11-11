using HarmonyLib;

namespace ThronefallMP.Patches;

// Match saves are per-machine "{map}.json" files. Online, each peer would load its own singleplayer run
// (one player gets old buildings, the other an empty map) and MP dawns would overwrite singleplayer runs.
// A separate "mp_" file starts every peer fresh and keeps both save sets apart.
[HarmonyPatch(typeof(MatchSaveLoadHandler), "get_SavePath")]
public static class MatchSavePathPatch
{
    private static void Postfix(ref string __result)
    {
        var network = Plugin.Instance?.Network;
        if (network != null && network.Online)
        {
            __result = "mp_" + __result;
        }
    }
}
