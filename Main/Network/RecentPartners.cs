using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx;
using JetBrains.Annotations;
using Steamworks;
using ThronefallMP.UI;
using UnityEngine;

namespace ThronefallMP.Network;

// Remembers who we played with and lets hosts and joiners find each other directly over Steam P2P.
// Lobby search and friends' rich presence both fail when players are Invisible; direct messages to a
// SteamID still arrive, so the host announces its lobby to recent partners and joiners ask them for one.
public class RecentPartners : MonoBehaviour
{
    private const int Channel = 92;
    private const string HostMagic = "TFMPHOST1";
    private const string AskMagic = "TFMPASK1";
    private const int MaxPartners = 8;
    private const float AskInterval = 15f;

    private class Partner
    {
        public ulong Id;
        public string Name;
        public long LastPlayed;
    }

    private static string StorePath => Path.Combine(Paths.ConfigPath, "thronefall_mp_recent.txt");

    [UsedImplicitly] private Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;
    private readonly List<Partner> _partners = new();
    private readonly Dictionary<ulong, CSteamID> _hostedLobbies = new();
    private readonly HashSet<ulong> _announcedLobbies = new();
    private readonly IntPtr[] _messages = new IntPtr[8];
    private CSteamID _lastHostedLobby = CSteamID.Nil;
    private float _nextMemberScan;
    private float _nextAsk;

    public IEnumerable<CSteamID> KnownLobbies => _hostedLobbies.Values;

    private void Start()
    {
        Load();
        if (SteamManager.Initialized)
        {
            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
        }
    }

    private void Update()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        var network = Plugin.Instance.Network;
        Receive();

        if (network.Online && Time.realtimeSinceStartup >= _nextMemberScan)
        {
            _nextMemberScan = Time.realtimeSinceStartup + 5f;
            RecordLobbyMembers(network.Lobby);
        }

        if (network.Online && network.Server && network.Lobby != _lastHostedLobby)
        {
            _lastHostedLobby = network.Lobby;
            foreach (var partner in _partners)
            {
                SendHost(new CSteamID(partner.Id));
            }
        }
        else if (!network.Online)
        {
            _lastHostedLobby = CSteamID.Nil;
            if (Time.realtimeSinceStartup >= _nextAsk && _partners.Count > 0)
            {
                AskAll();
            }
        }
    }

    public void AskAll()
    {
        _nextAsk = Time.realtimeSinceStartup + AskInterval;
        foreach (var partner in _partners)
        {
            Send(new CSteamID(partner.Id), AskMagic);
        }
    }

    private void RecordLobbyMembers(CSteamID lobby)
    {
        var self = SteamUser.GetSteamID();
        var changed = false;
        for (int i = 0, count = SteamMatchmaking.GetNumLobbyMembers(lobby); i < count; ++i)
        {
            var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
            if (member == self || !member.IsValid())
            {
                continue;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var partner = _partners.FirstOrDefault(p => p.Id == member.m_SteamID);
            if (partner == null)
            {
                partner = new Partner { Id = member.m_SteamID };
                _partners.Add(partner);
            }
            else if (now - partner.LastPlayed < 600)
            {
                continue;
            }

            partner.Name = SteamFriends.GetFriendPersonaName(member);
            partner.LastPlayed = now;
            changed = true;
        }

        if (changed)
        {
            _partners.Sort((a, b) => b.LastPlayed.CompareTo(a.LastPlayed));
            if (_partners.Count > MaxPartners)
            {
                _partners.RemoveRange(MaxPartners, _partners.Count - MaxPartners);
            }
            Save();
        }
    }

    private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t request)
    {
        var id = request.m_identityRemote.GetSteamID().m_SteamID;
        if (_partners.Any(p => p.Id == id))
        {
            SteamNetworkingMessages.AcceptSessionWithUser(ref request.m_identityRemote);
        }
    }

    private void Receive()
    {
        var received = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, _messages, _messages.Length);
        for (var i = 0; i < received; ++i)
        {
            try
            {
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_messages[i]);
                var data = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, data, 0, data.Length);
                Handle(message.m_identityPeer.GetSteamID(), System.Text.Encoding.ASCII.GetString(data));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Partners> {e}");
            }
            finally
            {
                SteamNetworkingMessage_t.Release(_messages[i]);
            }
        }
    }

    private void Handle(CSteamID sender, string text)
    {
        var partner = _partners.FirstOrDefault(p => p.Id == sender.m_SteamID);
        if (partner == null)
        {
            return;
        }

        var network = Plugin.Instance.Network;
        if (text == AskMagic)
        {
            if (network.Online && network.Server)
            {
                SendHost(sender);
            }
            return;
        }

        var parts = text.Split(' ');
        if (parts.Length < 2 || parts[0] != HostMagic || !ulong.TryParse(parts[1], out var lobbyId))
        {
            return;
        }

        var lobby = new CSteamID(lobbyId);
        _hostedLobbies[sender.m_SteamID] = lobby;
        if (network.Online)
        {
            return;
        }

        Plugin.Log.LogInfo($"Partners> {partner.Name} hosts lobby {lobbyId}");
        SteamMatchmaking.RequestLobbyData(lobby);
        if (_announcedLobbies.Add(lobbyId) && UIManager.TitleScreen != null && UIManager.TitleScreen.activeInHierarchy)
        {
            UIManager.CreateMessageDialog("Friend is hosting",
                $"{partner.Name} is hosting a game.\nOpen Multiplayer to join.");
        }
    }

    private void SendHost(CSteamID target)
    {
        Send(target, $"{HostMagic} {Plugin.Instance.Network.Lobby.m_SteamID}");
    }

    private static void Send(CSteamID target, string text)
    {
        var data = System.Text.Encoding.ASCII.GetBytes(text);
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(target);
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            SteamNetworkingMessages.SendMessageToUser(
                ref identity, handle.AddrOfPinnedObject(), (uint)data.Length,
                Constants.k_nSteamNetworkingSend_Reliable, Channel);
        }
        finally
        {
            handle.Free();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return;
            }

            foreach (var line in File.ReadAllLines(StorePath))
            {
                var parts = line.Split(new[] { ';' }, 3);
                if (parts.Length == 3 && ulong.TryParse(parts[0], out var id) && long.TryParse(parts[1], out var time))
                {
                    _partners.Add(new Partner { Id = id, LastPlayed = time, Name = parts[2] });
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Partners> could not read {StorePath}: {e.Message}");
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllLines(StorePath, _partners.Select(p => $"{p.Id};{p.LastPlayed};{p.Name}"));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Partners> could not write {StorePath}: {e.Message}");
        }
    }
}
