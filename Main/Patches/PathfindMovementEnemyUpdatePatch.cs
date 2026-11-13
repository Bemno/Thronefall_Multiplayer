using System;
using HarmonyLib;

namespace ThronefallMP.Patches;

// Leaving a level destroys the networked players at the start of the transition, while enemies keep updating
// until the scene unloads. Their Update then threw a NullReferenceException every frame (thousands after a
// defeat), which stalled the After Match screen. Disable such enemies instead while a transition runs.
[HarmonyPatch(typeof(PathfindMovementEnemy), "Update")]
public static class PathfindMovementEnemyUpdatePatch
{
    private static Exception Finalizer(Exception __exception, PathfindMovementEnemy __instance)
    {
        if (__exception is NullReferenceException
            && SceneTransitionManager.instance != null
            && SceneTransitionManager.instance.SceneTransitionIsRunning)
        {
            __instance.enabled = false;
            return null;
        }

        return __exception;
    }
}
