using System.Collections;
using HarmonyLib;
using I2.Loc;
using ThronefallMP.Components;

namespace ThronefallMP.Patches;

public static class PlayerSceptPatch
{
    public static void Apply()
    {
        On.PlayerScept.Start += Start;
        On.PlayerScept.Update += Update;
    }

    private static void Start(On.PlayerScept.orig_Start original, PlayerScept self)
    {
        original(self);
        // Need to do this because the reference is wrong for dynamically instantiated players.
        // Craaghelm uses a different player rig, so the base-game path is missing there.
        var loadout = self.transform.Find("Horse_LOD1/Rig/root/body/Humanoid Model Base/Civil Loadout")
            ?? FindDescendant(self.transform, "Civil Loadout");
        if (loadout != null)
        {
            self.loadoutParent = loadout.gameObject;
        }
    }

    private static UnityEngine.Transform FindDescendant(UnityEngine.Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }
        return null;
    }
    
    private static void Update(On.PlayerScept.orig_Update original, PlayerScept self)
    {
        var data = self.transform.parent.GetComponent<PlayerNetworkData>();
        if (data == null)
        {
            return;
        }
        
        if (data.SharedData.InteractButton && !data.PlayerScepterInteractLast)
        {
            self.StopAllCoroutines();
            self.StartCoroutine(Traverse.Create(self).Method("AnimationIn").GetValue<IEnumerator>());
        }
        if (!data.SharedData.InteractButton && data.PlayerScepterInteractLast)
        {
            self.StopAllCoroutines();
            self.StartCoroutine(Traverse.Create(self).Method("AnimationOut").GetValue<IEnumerator>());
        }

        data.PlayerScepterInteractLast = data.SharedData.InteractButton;
    }
}