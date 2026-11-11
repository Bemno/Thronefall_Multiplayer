using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using Steamworks;
using ThronefallMP.UI;
using UnityEngine;

namespace ThronefallMP.Network;

// Transfers the host's mod DLL to a joining Steam friend whose version differs. Deliberately independent of the
// packet protocol (own channels, fixed byte layout) so that it keeps working between any two mod versions.
public class ModUpdater : MonoBehaviour
{
    private const int RequestChannel = 90;
    private const int ResponseChannel = 91;
    private const string Magic = "TFMPDLL1";
    private const int MaxDllSize = 500 * 1024;
    private const float TimeoutSeconds = 30f;

    private static string DllPath => typeof(Plugin).Assembly.Location;

    [UsedImplicitly] private Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;
    private readonly IntPtr[] _messages = new IntPtr[8];
    private CSteamID _awaitingFrom = CSteamID.Nil;
    private CSteamID _awaitingLobby = CSteamID.Nil;
    private float _awaitingSince;

    private void Awake()
    {
        try
        {
            if (File.Exists(DllPath + ".old"))
            {
                File.Delete(DllPath + ".old");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Updater> could not remove old dll: {e.Message}");
        }
    }

    private void Start()
    {
        if (SteamManager.Initialized)
        {
            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        }
    }

    // Called from Network.OnLobbyEntered before connecting. Returns true when the join is taken over here.
    public bool HandleVersionMismatch(CSteamID lobby)
    {
        var hostVersion = SteamMatchmaking.GetLobbyData(lobby, "version");
        if (string.IsNullOrEmpty(hostVersion) || hostVersion == Plugin.VersionString)
        {
            return false;
        }

        var owner = SteamMatchmaking.GetLobbyOwner(lobby);
        var hostName = SteamFriends.GetFriendPersonaName(owner);
        var shownVersion = hostVersion.Replace("thronefall_mp_", "");
        Plugin.Log.LogInfo($"Updater> version mismatch, host {hostVersion}, local {Plugin.VersionString}");

        if (SteamFriends.GetFriendRelationship(owner) != EFriendRelationship.k_EFriendRelationshipFriend)
        {
            Leave(lobby);
            UIManager.CreateMessageDialog("Different mod version",
                $"{hostName} runs Multiplayer v{shownVersion}, you run v{PluginInfo.PLUGIN_VERSION}.\n" +
                "Automatic update only works from Steam friends.");
            return true;
        }

        UIManager.LobbyListPanel.CloseConnectingDialog();
        UIManager.CreateMessageDialog("Update available",
            $"{hostName} runs Multiplayer v{shownVersion}, you run v{PluginInfo.PLUGIN_VERSION}.\n" +
            "Download the host's version?",
            "Update",
            onClick: () => RequestDll(lobby, owner));
        return true;
    }

    private void RequestDll(CSteamID lobby, CSteamID owner)
    {
        _awaitingFrom = owner;
        _awaitingLobby = lobby;
        _awaitingSince = Time.realtimeSinceStartup;
        UIManager.LobbyListPanel.ShowHideConnectingDialog(true);
        Send(owner, Encoding.ASCII.GetBytes(Magic), RequestChannel);
        Plugin.Log.LogInfo($"Updater> requested dll from {owner.m_SteamID}");
    }

    private void Update()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        var network = Plugin.Instance.Network;
        if (network.Online && network.Server)
        {
            Receive(RequestChannel, HandleRequest);
        }

        if (_awaitingFrom != CSteamID.Nil)
        {
            Receive(ResponseChannel, HandleResponse);
            if (_awaitingFrom != CSteamID.Nil && Time.realtimeSinceStartup - _awaitingSince > TimeoutSeconds)
            {
                Fail("The host did not answer. Is the host running a mod version with the updater?");
            }
        }
    }

    private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
    {
        var id = request.m_identityRemote.GetSteamID();
        var network = Plugin.Instance.Network;
        if (network.Server && IsLobbyMember(network.Lobby, id))
        {
            SteamNetworkingMessages.AcceptSessionWithUser(ref request.m_identityRemote);
        }
    }

    private void HandleRequest(CSteamID sender, byte[] data)
    {
        var network = Plugin.Instance.Network;
        if (!IsLobbyMember(network.Lobby, sender) || Encoding.ASCII.GetString(data) != Magic)
        {
            return;
        }

        var dll = File.ReadAllBytes(DllPath);
        if (dll.Length > MaxDllSize)
        {
            Plugin.Log.LogError($"Updater> dll too large to send ({dll.Length} bytes)");
            return;
        }

        byte[] hash;
        using (var sha = SHA256.Create())
        {
            hash = sha.ComputeHash(dll);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Plugin.VersionString);
        writer.Write(hash);
        writer.Write(dll.Length);
        writer.Write(dll);
        writer.Flush();
        Send(sender, stream.ToArray(), ResponseChannel);
        Plugin.Log.LogInfo($"Updater> sent dll ({dll.Length} bytes) to {sender.m_SteamID}");
    }

    private void HandleResponse(CSteamID sender, byte[] data)
    {
        if (sender != _awaitingFrom)
        {
            return;
        }

        try
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            if (reader.ReadString() != Magic)
            {
                throw new InvalidDataException("bad header");
            }

            var version = reader.ReadString();
            var hash = reader.ReadBytes(32);
            var length = reader.ReadInt32();
            if (length <= 0 || length > MaxDllSize)
            {
                throw new InvalidDataException($"bad length {length}");
            }

            var dll = reader.ReadBytes(length);
            using (var sha = SHA256.Create())
            {
                if (dll.Length != length || !sha.ComputeHash(dll).SequenceEqual(hash) || dll[0] != 'M' || dll[1] != 'Z')
                {
                    throw new InvalidDataException("checksum mismatch");
                }
            }

            // A loaded DLL cannot be overwritten on Windows but can be renamed; the .old copy is removed on next start.
            var temp = DllPath + ".new";
            File.WriteAllBytes(temp, dll);
            if (File.Exists(DllPath + ".old"))
            {
                File.Delete(DllPath + ".old");
            }
            File.Move(DllPath, DllPath + ".old");
            File.Move(temp, DllPath);

            Plugin.Log.LogInfo($"Updater> installed {version}");
            Leave(_awaitingLobby);
            _awaitingFrom = CSteamID.Nil;
            UIManager.CreateMessageDialog("Updated",
                $"Installed Multiplayer v{version.Replace("thronefall_mp_", "")}.\nRestart Thronefall to use it.",
                "Quit game",
                onClick: Application.Quit);
        }
        catch (Exception e)
        {
            Fail($"The update failed: {e.Message}");
        }
    }

    private void Fail(string message)
    {
        Plugin.Log.LogError($"Updater> {message}");
        Leave(_awaitingLobby);
        _awaitingFrom = CSteamID.Nil;
        UIManager.CreateMessageDialog("Update failed", message);
    }

    private static void Leave(CSteamID lobby)
    {
        if (lobby != CSteamID.Nil)
        {
            SteamMatchmaking.LeaveLobby(lobby);
        }
        UIManager.LobbyListPanel.CloseConnectingDialog();
    }

    private static bool IsLobbyMember(CSteamID lobby, CSteamID id)
    {
        for (int i = 0, count = SteamMatchmaking.GetNumLobbyMembers(lobby); i < count; ++i)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, i) == id)
            {
                return true;
            }
        }
        return false;
    }

    private void Receive(int channel, Action<CSteamID, byte[]> handler)
    {
        var received = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, _messages, _messages.Length);
        for (var i = 0; i < received; ++i)
        {
            try
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_messages[i]);
                var data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, data.Length);
                handler(message.m_identityPeer.GetSteamID(), data);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Updater> {e}");
            }
            finally
            {
                SteamNetworkingMessage_t.Release(_messages[i]);
            }
        }
    }

    private static void Send(CSteamID target, byte[] data, int channel)
    {
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(target);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var result = SteamNetworkingMessages.SendMessageToUser(
                ref identity, handle.AddrOfPinnedObject(), (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Reliable, channel);
            if (result != EResult.k_EResultOK)
            {
                Plugin.Log.LogWarning($"Updater> send to {target.m_SteamID} returned {result}");
            }
        }
        finally
        {
            handle.Free();
        }
    }
}
