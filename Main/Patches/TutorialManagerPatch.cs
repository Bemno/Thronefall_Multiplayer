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
        if (player.Value != local.transform)
        {
            player.Value = local.transform;
            traverse.Field<Hp>("playerHp").Value = local.GetComponent<Hp>();
            traverse.Field<RVOController>("playerRvo").Value = local.GetComponent<RVOController>();
            traverse.Field<PlayerInteraction>("playerInteraction").Value = local.GetComponent<PlayerInteraction>();
        }

        // The "melee 0/3" counter reads the scene player's serialized StabMA, which never attacks. The weapon is
        // equipped after spawn, so keep following whatever StabMA the local player currently has.
        if (local.GetComponent<PlayerInteraction>()?.ManualAttack is StabMA stab)
        {
            traverse.Field<StabMA>("activeStabAttack").Value = stab;
        }
    }
}
