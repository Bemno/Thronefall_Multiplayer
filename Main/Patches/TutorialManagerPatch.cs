using HarmonyLib;
using Pathfinding.RVO;

namespace ThronefallMP.Patches;

// TutorialManager holds serialized references to the scene's own player, which the mod replaces with networked
// players. The stale Hp reads as knocked out, so the tutorial showed its "dead" tip for everyone and never
// noticed movement. Point it at the local networked player instead.
[HarmonyPatch(typeof(TutorialManager), "TutorialUpdate")]
public static class TutorialManagerPatch
{
    private static void Prefix(TutorialManager __instance)
    {
        var local = Plugin.Instance?.PlayerManager?.LocalPlayer?.Object;
        if (local == null)
        {
            return;
        }

        var traverse = Traverse.Create(__instance);
        var player = traverse.Field<UnityEngine.Transform>("player");
        if (player.Value == local.transform)
        {
            return;
        }

        player.Value = local.transform;
        traverse.Field<Hp>("playerHp").Value = local.GetComponent<Hp>();
        traverse.Field<RVOController>("playerRvo").Value = local.GetComponent<RVOController>();
    }
}
