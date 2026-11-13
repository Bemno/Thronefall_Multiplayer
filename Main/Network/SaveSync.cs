using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Steamworks;
using Thronefall.SaveServices;
using UnityEngine;

namespace ThronefallMP.Network;

// Makes the host's match save authoritative. Every peer used to load and write its own local "mp_{map}.json",
// so a player who missed a dawn (or played the map with someone else) loaded a different run. The host now
// sends its save when a level is requested and after every dawn save; clients load only what the host sent
// and never write match saves themselves.
public class SaveSync : MonoBehaviour
{
    private const int Channel = 93;
    private const string Magic = "TFMPSAVE1";
    private const int MaxSize = 500 * 1024;

    // map -> host's save json, or null when the host has no save for that map.
    private static readonly Dictionary<string, string> HostSaves = new();
    private readonly IntPtr[] _messages = new IntPtr[8];

    private static bool IsClient => Plugin.Instance.Network.Online && !Plugin.Instance.Network.Server;
    private static bool IsHost => Plugin.Instance.Network.Online && Plugin.Instance.Network.Server;

    private static string FullPath(string map) =>
        SaveServicesLocator.Instance.GetFullSavePath(Patches.MatchSavePathPatch.FilePrefix + map + ".json");

    // fresh: the level is being started over, so clients must not load the existing save.
    public static void SendSave(string map, bool fresh = false)
    {
        if (!IsHost || string.IsNullOrEmpty(map))
        {
            return;
        }

        string json = null;
        try
        {
            var path = FullPath(map);
            if (!fresh && SaveServicesLocator.Instance.Exists(path))
            {
                json = SaveServicesLocator.Instance.ReadAllText(path);
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"SaveSync> could not read save for {map}: {e.Message}");
            return;
        }

        var data = Encoding.UTF8.GetBytes($"{Magic}\n{map}\n{(json == null ? 0 : 1)}\n{json}");
        if (data.Length > MaxSize)
        {
            Plugin.Log.LogWarning($"SaveSync> save for {map} too large ({data.Length} bytes), not sent");
            return;
        }

        var lobby = Plugin.Instance.Network.Lobby;
        var self = SteamUser.GetSteamID();
        for (int i = 0, count = SteamMatchmaking.GetNumLobbyMembers(lobby); i < count; ++i)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
            if (member != self)
            {
                Send(member, data);
            }
        }
        Plugin.Log.LogInfo($"SaveSync> sent {map} ({(json == null ? "no save" : $"{json.Length} chars")})");
    }

    public static void ForgetHostSave(string map)
    {
        HostSaves[map] = null;
        Plugin.Log.LogInfo($"SaveSync> fresh start of {map}, ignoring any match save");
    }

    // Called when this client becomes the host: the last match saves received from the old host become ours.
    public static void AdoptHostSaves()
    {
        foreach (var save in HostSaves)
        {
            if (save.Value == null)
            {
                continue;
            }

            try
            {
                SaveServicesLocator.Instance.WriteAllText(FullPath(save.Key), save.Value);
                Plugin.Log.LogInfo($"SaveSync> adopted host save for {save.Key}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SaveSync> could not adopt save for {save.Key}: {e.Message}");
            }
        }

        HostSaves.Clear();
    }

    private void Update()
    {
        if (!SteamManager.Initialized || !IsClient)
        {
            return;
        }

        var received = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _messages, _messages.Length);
        for (var i = 0; i < received; ++i)
        {
            try
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_messages[i]);
                if (message.m_identityPeer.GetSteamID() != Plugin.Instance.Network.Owner)
                {
                    continue;
                }

                var data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, data.Length);
                var parts = Encoding.UTF8.GetString(data).Split(new[] { '\n' }, 4);
                if (parts.Length == 4 && parts[0] == Magic)
                {
                    HostSaves[parts[1]] = parts[2] == "1" ? parts[3] : null;
                    Plugin.Log.LogInfo($"SaveSync> received {parts[1]} ({(parts[2] == "1" ? "save" : "no save")})");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SaveSync> {e}");
            }
            finally
            {
                SteamNetworkingMessage_t.Release(_messages[i]);
            }
        }
    }

    private static void Send(CSteamID target, byte[] data)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(target);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            SteamNetworkingMessages.SendMessageToUser(
                ref identity, handle.AddrOfPinnedObject(), (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, Channel);
        }
        finally
        {
            handle.Free();
        }
    }

    [HarmonyPatch(typeof(MatchSaveLoadHandler), nameof(MatchSaveLoadHandler.TryLoadRun))]
    public static class TryLoadRunPatch
    {
        private static bool Prefix(string mapName)
        {
            if (!IsClient || !HostSaves.TryGetValue(mapName, out var json))
            {
                if (IsClient)
                {
                    Plugin.Log.LogWarning($"SaveSync> no host save received for {mapName}, using local file");
                }
                return true;
            }

            if (json == null)
            {
                Traverse.Create(typeof(MatchSaveLoadHandler)).Field<string>("currentMap").Value = mapName;
                Traverse.Create(typeof(MatchSaveLoadHandler)).Field<MatchSave>("currentSave").Value = null;
                Plugin.Log.LogInfo($"SaveSync> host has no save for {mapName}, starting fresh");
                return false;
            }

            SaveServicesLocator.Instance.WriteAllText(FullPath(mapName), json);
            Plugin.Log.LogInfo($"SaveSync> loading host save for {mapName}");
            return true;
        }
    }

    [HarmonyPatch(typeof(MatchSaveLoadHandler), nameof(MatchSaveLoadHandler.SaveRun))]
    public static class SaveRunPatch
    {
        private static bool Prefix()
        {
            return !IsClient;
        }

        private static void Postfix()
        {
            if (IsHost)
            {
                SendSave(Traverse.Create(typeof(MatchSaveLoadHandler)).Field<string>("currentMap").Value);
            }
        }
    }
}
