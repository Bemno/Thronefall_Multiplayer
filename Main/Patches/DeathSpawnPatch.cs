using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.Game;
using UnityEngine;

namespace ThronefallMP.Patches;

// Every death spawn goes through EnemySpawner.SpawnDeathSpawn. Online, each peer would otherwise spawn its own
// identifier-less copy when its replica of the enemy dies, so the host rolls the spawn and broadcasts it, and
// clients only spawn what the host sends.
[HarmonyPatch(typeof(EnemySpawner), "SpawnDeathSpawn")]
internal static class DeathSpawnPatch
{
    private static readonly Dictionary<string, GameObject> KnownPrefabs = new();
    private static bool _spawningFromNetwork;

    private static bool Prefix(GameObject prefab, Vector3 position, Quaternion rotation, bool elite)
    {
        if (prefab != null)
        {
            KnownPrefabs[prefab.name] = prefab;
        }

        var network = Plugin.Instance.Network;
        if (_spawningFromNetwork || !network.Online)
        {
            return true;
        }

        if (network.Server && prefab != null)
        {
            network.Send(new DeathSpawnPacket
            {
                PrefabName = prefab.name,
                Id = EnemySpawnerPatch.AllocateEnemyId(),
                Position = position,
                Rotation = rotation,
                Elite = elite
            }, true);
        }

        return false;
    }

    public static void Handle(DeathSpawnPacket packet)
    {
        var prefab = FindPrefab(packet.PrefabName);
        if (prefab == null || EnemySpawner.instance == null)
        {
            Plugin.Log.LogWarning($"Death spawn prefab '{packet.PrefabName}' not found");
            return;
        }

        var before = new HashSet<int>();
        foreach (var hp in Object.FindObjectsOfType<Hp>())
        {
            before.Add(hp.transform.root.gameObject.GetInstanceID());
        }

        _spawningFromNetwork = true;
        try
        {
            Traverse.Create(EnemySpawner.instance)
                .Method("SpawnDeathSpawn", prefab, packet.Position, packet.Rotation, packet.Elite)
                .GetValue();
        }
        finally
        {
            _spawningFromNetwork = false;
        }

        foreach (var hp in Object.FindObjectsOfType<Hp>())
        {
            var go = hp.transform.root.gameObject;
            if (!before.Contains(go.GetInstanceID()) && go.GetComponent<Identifier>() == null)
            {
                go.AddComponent<Identifier>().SetIdentity(IdentifierType.Enemy, packet.Id);
                return;
            }
        }
    }

    private static GameObject FindPrefab(string name)
    {
        if (KnownPrefabs.TryGetValue(name, out var known) && known != null)
        {
            return known;
        }

        foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (candidate.name == name && !candidate.scene.IsValid())
            {
                KnownPrefabs[name] = candidate;
                return candidate;
            }
        }

        return null;
    }
}
