using HarmonyLib;

namespace ThronefallMP.Patches;

// Energy cores (Craaghelm) are pooled for the whole team exactly like gold in PlayerInteractionPatch: picking one
// up only counts on the server, spending goes through GlobalData so clients see their spend before the server
// confirms it, and every balance read returns the pool.
[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.AddEnergyCores))]
internal static class EnergyCoreAddPatch
{
    private static bool Prefix(int amount)
    {
        if (Plugin.Instance.Network.Server)
        {
            GlobalData.EnergyCores += amount;
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.SpendEnergyCores))]
internal static class EnergyCoreSpendPatch
{
    private static bool Prefix(int amount)
    {
        GlobalData.EnergyCores -= amount;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.EnergyCoreBalance), MethodType.Getter)]
internal static class EnergyCoreBalancePatch
{
    private static bool Prefix(ref int __result)
    {
        __result = GlobalData.EnergyCores;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerInteraction), nameof(PlayerInteraction.TrueEnergyCoreBalance), MethodType.Getter)]
internal static class EnergyCoreTrueBalancePatch
{
    private static bool Prefix(ref int __result)
    {
        __result = GlobalData.EnergyCores;
        return false;
    }
}
