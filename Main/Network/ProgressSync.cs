using System;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using Steamworks;
using Thronefall.SaveServices;
using ThronefallMP.Patches;
using UnityEngine;

namespace ThronefallMP.Network;

// Each player keeps their own copy of a group's progress (PartnerProgress), and the copies drifted apart: a win
// saved on one machine only, XP computed separately. On join the copy with the most progress wins; afterwards the
// host pushes its copy after every save. Copies are only swapped in level select.
public class ProgressSync : MonoBehaviour
{
    private const int Channel = 94;
    private const string Magic = "TFMPPROG1";

    private readonly IntPtr[] _messages = new IntPtr[4];
    private static (string baseSave, string craaghelmSave, CSteamID from)? _pending;

    private static bool IsHost => Plugin.Instance.Network.Online && Plugin.Instance.Network.Server;
    private static bool IsClient => Plugin.Instance.Network.Online && !Plugin.Instance.Network.Server;

    public static void Push()
    {
        if (!IsHost || PartnerProgress.ActiveKey == null)
        {
            return;
        }

        var data = Pack();
        if (data == null)
        {
            return;
        }

        var network = Plugin.Instance.Network;
        var self = SteamUser.GetSteamID();
        for (int i = 0, count = SteamMatchmaking.GetNumLobbyMembers(network.Lobby); i < count; ++i)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(network.Lobby, i);
            if (member != self)
            {
                Send(member, data);
            }
        }
        Plugin.Log.LogInfo("ProgressSync> sent group progress");
    }

    private void Update()
    {
        if (!SteamManager.Initialized || !Plugin.Instance.Network.Online)
        {
            _pending = null;
            return;
        }

        var received = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _messages, _messages.Length);
        for (var i = 0; i < received; ++i)
        {
            try
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_messages[i]);
                var data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, data.Length);
                using var reader = new BinaryReader(new MemoryStream(data));
                if (reader.ReadString() == Magic)
                {
                    _pending = (reader.ReadString(), reader.ReadString(), message.m_identityPeer.GetSteamID());
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ProgressSync> {e}");
            }
            finally
            {
                SteamNetworkingMessage_t.Release(_messages[i]);
            }
        }

        if (_pending.HasValue && SceneTransitionManagerPatch.InLevelSelect && PartnerProgress.ActiveKey != null)
        {
            var pending = _pending.Value;
            _pending = null;
            Apply(pending.baseSave, pending.craaghelmSave, pending.from);
        }
    }

    private static void Apply(string baseSave, string craaghelmSave, CSteamID from)
    {
        var theirs = Score(baseSave, craaghelmSave);
        var manager = SaveLoadManager.instance;
        var ours = Score(Read(manager.BaseGameSaveName), Read(manager.CraaghelmSaveName));
        if (IsClient && from == Plugin.Instance.Network.Owner)
        {
            if (theirs.CompareTo(ours) >= 0)
            {
                Adopt(baseSave, craaghelmSave);
            }
            else
            {
                Plugin.Log.LogInfo($"ProgressSync> our progress {ours} is ahead of the host's {theirs}, sending it");
                var data = Pack();
                if (data != null)
                {
                    Send(from, data);
                }
            }
        }
        else if (IsHost && theirs.CompareTo(ours) > 0)
        {
            Adopt(baseSave, craaghelmSave);
            Push();
        }
    }

    private static void Adopt(string baseSave, string craaghelmSave)
    {
        var manager = SaveLoadManager.instance;
        Write(manager.BaseGameSaveName, baseSave);
        Write(manager.CraaghelmSaveName, craaghelmSave);
        PartnerProgress.Reload();
        Plugin.Log.LogInfo("ProgressSync> adopted group progress");
    }

    // (total perk level, total XP) over both realms.
    private static (int level, int xp) Score(string baseSave, string craaghelmSave)
    {
        var a = Parse(baseSave);
        var b = Parse(craaghelmSave);
        return (a.level + b.level, a.xp + b.xp);
    }

    private static (int level, int xp) Parse(string save)
    {
        int level = 0, xp = 0;
        if (string.IsNullOrEmpty(save))
        {
            return (level, xp);
        }

        var lines = save.Replace("\r", "").Split('\n');
        for (var i = 0; i + 1 < lines.Length; ++i)
        {
            if (lines[i] == "Perk Level")
            {
                int.TryParse(lines[i + 1], out level);
            }
            else if (lines[i] == "Perk XP")
            {
                int.TryParse(lines[i + 1], out xp);
            }
        }
        return (level, xp);
    }

    private static byte[] Pack()
    {
        var manager = SaveLoadManager.instance;
        if (manager == null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Read(manager.BaseGameSaveName));
        writer.Write(Read(manager.CraaghelmSaveName));
        writer.Flush();
        return stream.ToArray();
    }

    private static string Read(string name)
    {
        try
        {
            var path = SaveServicesLocator.Instance.GetFullSavePath(name);
            return SaveServicesLocator.Instance.Exists(path) ? SaveServicesLocator.Instance.ReadAllText(path) : "";
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"ProgressSync> could not read {name}: {e.Message}");
            return "";
        }
    }

    private static void Write(string name, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        SaveServicesLocator.Instance.WriteAllText(SaveServicesLocator.Instance.GetFullSavePath(name), content);
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

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SaveGame))]
    private static class SaveGamePatch
    {
        private static void Postfix()
        {
            Push();
        }
    }
}
