using HarmonyLib;
using Steamworks;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.PlayerCommand;

namespace ThronefallMP.Patches;

[HarmonyPatch(typeof(BuildSlot), nameof(BuildSlot.ApplyBoost))]
internal static class BuildingBoostPatch
{
    private static bool _applyingFromNetwork;

    private static void Prefix(BuildSlot __instance)
    {
        var network = Plugin.Instance.Network;
        if (_applyingFromNetwork || !network.Online || !__instance.IsBoostAvailable)
        {
            return;
        }

        var identifier = __instance.GetComponent<Identifier>();
        if (identifier == null)
        {
            return;
        }

        network.Send(new BoostBuildingPacket
        {
            PlayerID = Plugin.Instance.PlayerManager.LocalId,
            BuildingId = identifier.Id
        });
    }

    public static void Handle(CSteamID sender, BoostBuildingPacket packet)
    {
        var slot = Identifier.GetGameObject(IdentifierType.BuildSlot, packet.BuildingId)?.GetComponent<BuildSlot>();
        if (slot == null)
        {
            return;
        }

        var network = Plugin.Instance.Network;
        if (network.Server)
        {
            if (!slot.IsBoostAvailable)
            {
                return;
            }

            var boost = Traverse.Create(slot).Field<BuildSlot.Upgrade>("boostUpgrade").Value;
            GlobalData.EnergyCores -= boost?.energyCoreCost ?? 0;
            network.Send(packet);
        }
        else if (packet.PlayerID == Plugin.Instance.PlayerManager.LocalId)
        {
            GlobalData.LocalEnergyCoreDelta = 0;
        }

        _applyingFromNetwork = true;
        try
        {
            slot.ApplyBoost();
        }
        finally
        {
            _applyingFromNetwork = false;
        }
    }
}
