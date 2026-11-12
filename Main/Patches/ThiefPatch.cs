using HarmonyLib;

namespace ThronefallMP.Patches;

// Craaghelm thieves steal through PlayerInteraction.instance.SpendCoins on every peer. The shared gold is owned
// by the server, so a client's replica must not book the theft as its own local spend.
[HarmonyPatch(typeof(ThiefController), "Steal")]
internal static class ThiefPatch
{
    public static bool Stealing { get; private set; }

    private static void Prefix() => Stealing = true;

    private static void Finalizer() => Stealing = false;
}
