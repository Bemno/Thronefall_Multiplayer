using System.Collections.Generic;
using Steamworks;
using ThronefallMP.Network;
using ThronefallMP.UI.Controls;
using ThronefallMP.UI.Dialogs;
using UnityEngine;
using Image = UnityEngine.UI.Image;

namespace ThronefallMP.UI.Panels;

public partial class LobbyListPanel : BaseUI
{
    private Callback<LobbyDataUpdate_t> _lobbyUpdated;
    private readonly CallResult<LobbyMatchList_t> _onLobbyMatchListCallResult;
    
    public override string Name => "Lobby List Panel";

    public ButtonControl Host { get; private set; }

    private MessageDialog _activeConnectionDialog;
    private LobbyItem _currentlySelectedLobby;
    private GameObject _lobbyList;
    private ToggleControl _friendsOnly;
    private ToggleControl _showWithPassword;
    private ToggleControl _showFull;
    private ButtonControl _connect;
    private ButtonControl _directConnect;
    private ButtonControl _back;
    private CustomScrollRect _scrollRect;
    private GameObject _recentRow;
    private string _recentSignature;
    private bool _muteSound;

    private List<LobbyItem> _lobbies = new();
    private Dictionary<CSteamID, LobbyItem> _idToLobbies = new();

    public LobbyListPanel()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        _lobbyUpdated = new Callback<LobbyDataUpdate_t>(OnLobbyDataUpdated);
        _onLobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbiesRefreshed);
    }
    
    private void PlaySelectSound()
    {
        if (!_muteSound)
        {
            //ThronefallAudioManager.Oneshot(ThronefallAudioManager.AudioOneShot.ButtonSelect);
        }

        _muteSound = false;
    }

    private void RefreshLobbies()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }
        
        // TODO: Show loading sprite.

        if (_friendsOnly.Toggle.isOn)
        {
            ClearLobbyEntries();
            Plugin.Log.LogInfo("Refreshing list with friends games.");
            var count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (var i = 0; i < count; ++i)
            {
                FriendGameInfo_t friendGameInfo;
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (SteamFriends.GetFriendGamePlayed(friend, out friendGameInfo) && friendGameInfo.m_steamIDLobby.IsValid())
                {
                    Plugin.Log.LogInfo($"Game {friendGameInfo.m_steamIDLobby.m_SteamID}");
                    SteamMatchmaking.RequestLobbyData(friendGameInfo.m_steamIDLobby);
                }
            }
            RequestPartnerLobbies();
            _scrollRect.verticalNormalizedPosition = 1.0f;
        }
        else
        {
            //SteamMatchmaking.AddRequestLobbyListStringFilter("name", "the value", ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            if (!_showFull.Toggle.isOn)
            {
                Plugin.Log.LogInfo("Adding empty slot filter");
                SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            }

            if (!_showWithPassword.Toggle.isOn)
            {
                Plugin.Log.LogInfo("Adding no password filter");
                SteamMatchmaking.AddRequestLobbyListStringFilter("password", "yes", ELobbyComparison.k_ELobbyComparisonNotEqual);
            }
            
            _onLobbyMatchListCallResult.Set(SteamMatchmaking.RequestLobbyList());
        }
    }
    
    private void SelectLobby(LobbyItem item)
    {
        if (_currentlySelectedLobby != null)
        {
            var image = _currentlySelectedLobby.LobbyGameObject.GetComponent<Image>();
            image.color = UIManager.TransparentBackgroundColor;
        }
            
        _currentlySelectedLobby = item;
        var image2 = _currentlySelectedLobby.LobbyGameObject.GetComponent<Image>();
        image2.color = UIManager.SelectedTransparentBackgroundColor;

        _connect.SetInteractable(true);
        _directConnect.Button.navigation = _directConnect.Button.navigation with { selectOnLeft = _connect.Button };
        _back.Button.navigation = _back.Button.navigation with { selectOnRight = _connect.Button };
    }

    private void ClearLobbyEntries()
    {
        foreach (var lobby in _lobbies)
        {
            Destroy(lobby.gameObject);
        }
        
        _lobbies.Clear();
        _idToLobbies.Clear();
        // TODO: Try and keep this one on refresh.
        _currentlySelectedLobby = null;
        
        _connect.SetInteractable(false);
        _directConnect.NavLeft = _back.Button;
        _back.NavRight = _directConnect.Button;
    }
    
    private static void RequestPartnerLobbies()
    {
        foreach (var lobby in Plugin.Instance.Partners.KnownLobbies)
        {
            SteamMatchmaking.RequestLobbyData(lobby);
        }
    }

    public void Open()
    {
        Plugin.Instance.Partners.AskAll();
        Enabled = true;
        RefreshLobbies();
        Host.Button.Select();
        _recentSignature = null;
        RefreshRecent();
    }

    private void RefreshRecent()
    {
        var recent = new List<(string Name, CSteamID Lobby, bool Hosting)>();
        foreach (var (name, lobby) in Plugin.Instance.Partners.Recent)
        {
            var hosting = lobby.IsValid() && !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(lobby, "version"));
            recent.Add((name, lobby, hosting));
        }

        var signature = string.Join("|", recent.ConvertAll(r => $"{r.Name}:{r.Hosting}:{r.Lobby.m_SteamID}"));
        if (signature == _recentSignature)
        {
            return;
        }

        _recentSignature = signature;
        foreach (Transform child in _recentRow.transform)
        {
            Destroy(child.gameObject);
        }

        _recentRow.SetActive(recent.Count > 0);
        if (recent.Count == 0)
        {
            return;
        }

        var label = UIHelper.CreateText(_recentRow, "label", "Recent players:");
        label.fontSize = 24;
        UIHelper.SetLayoutElement(label.gameObject, minWidth: 200);
        foreach (var (name, lobby, hosting) in recent)
        {
            var button = UIHelper.CreateButton(_recentRow, "recent", hosting ? $"Join {name}" : $"{name} (not hosting)");
            button.Normal.Size = 24;
            button.Hover.Size = 28;
            button.NormalSelected.Size = 28;
            button.HoverSelected.Size = 28;
            button.Noninteractive.Size = 24;
            button.Reset();
            UIHelper.SetLayoutElement(button.gameObject, minWidth: 160, preferredWidth: 280);
            button.SetInteractable(hosting);
            if (!hosting)
            {
                continue;
            }

            button.OnClick += () =>
            {
                if (SteamMatchmaking.GetLobbyData(lobby, "password") == "yes")
                {
                    UIManager.CreatePasswordDialog(password => ConnectToLobby(lobby, name, password), () => {});
                }
                else
                {
                    ConnectToLobby(lobby, name, null);
                }
            };
        }
    }

    public void Close()
    {
        Enabled = false;
    }

    private void Back()
    {
        Close();
        UIManager.TitleScreen.SetActive(true);
        ThronefallAudioManager.Oneshot(ThronefallAudioManager.AudioOneShot.ButtonApply);
    }

    private const float UpdateCooldown = 2.0f;
    private float _timer;
    public void Update()
    {
        if (!UIManager.HostPanel.Enabled && !UIManager.ExitHandled && Input.GetKeyDown(KeyCode.Escape))
        {
            UIManager.ExitHandled = true;
            Back();
        }

        if (!UIManager.HostPanel.Enabled && _activeConnectionDialog == null && Input.GetKeyDown(KeyCode.V)
            && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            DirectConnect();
        }
        
        _timer += Time.deltaTime;
        if (_timer < UpdateCooldown)
        {
            return;
        }

        _timer = 0.0f;
        foreach (var lobby in _lobbies)
        {
            SteamMatchmaking.RequestLobbyData(lobby.LobbyInfo.Id);
        }

        RequestPartnerLobbies();
        RefreshRecent();
    }

    private void OnLobbiesRefreshed(LobbyMatchList_t list, bool ioFailure)
    {
        ClearLobbyEntries();
        if (!Enabled)
        {
            return;
        }

        if (ioFailure)
        {
            var reason = SteamUtils.GetAPICallFailureReason(_onLobbyMatchListCallResult.Handle);
            // TODO: Show error dialog.
            return;
        }
        
        Plugin.Log.LogInfo($"Populating Lobby list {list.m_nLobbiesMatching}");
        for (var i = 0; i < list.m_nLobbiesMatching; ++i)
        {
            var lobby = SteamMatchmaking.GetLobbyByIndex(i);
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(lobby, "version")))
            {
                continue;
            }
            
            Plugin.Log.LogInfo($"Lobby {lobby.m_SteamID} password {SteamMatchmaking.GetLobbyData(lobby, "password")}");
            AddLobbyEntry(new Lobby()
            {
                Id = lobby,
                Name = SteamMatchmaking.GetLobbyData(lobby, "name"),
                PlayerCount = SteamMatchmaking.GetNumLobbyMembers(lobby),
                MaxPlayerCount = SteamMatchmaking.GetLobbyMemberLimit(lobby),
                HasPassword = SteamMatchmaking.GetLobbyData(lobby, "password") == "yes",
            });
        }

        RequestPartnerLobbies();
        _scrollRect.verticalNormalizedPosition = 0.0f;
    }

    private void OnLobbyDataUpdated(LobbyDataUpdate_t data)
    {
        if (!Enabled)
        {
            return;
        }

        if (data.m_ulSteamIDLobby != data.m_ulSteamIDMember)
        {
            return;
        }

        if (data.m_bSuccess == 0)
        {
            return;
        }
        
        var id = new CSteamID(data.m_ulSteamIDLobby);
        if (!_idToLobbies.TryGetValue(id, out var lobby))
        {
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(id, "version")))
            {
                return;
            }
            
            AddLobbyEntry(new Lobby()
            {
                Id = id,
                Name = SteamMatchmaking.GetLobbyData(id, "name"),
                PlayerCount = SteamMatchmaking.GetNumLobbyMembers(id),
                MaxPlayerCount = SteamMatchmaking.GetLobbyMemberLimit(id),
                HasPassword = SteamMatchmaking.GetLobbyData(id, "password") == "yes",
            });
        }
        else if (data.m_bSuccess == 0)
        {
            if (lobby == _currentlySelectedLobby)
            {
                _currentlySelectedLobby = null;
                _connect.SetInteractable(false);
                _directConnect.NavLeft = _back.Button;
                _back.NavRight = _directConnect.Button;
            }
            
            Destroy(lobby.gameObject);
            _lobbies.Remove(lobby);
            _idToLobbies.Remove(lobby.LobbyInfo.Id);
        }
        else
        {
            lobby.LobbyInfo.PlayerCount = SteamMatchmaking.GetNumLobbyMembers(lobby.LobbyInfo.Id);
            lobby.LobbyInfo.MaxPlayerCount = SteamMatchmaking.GetLobbyMemberLimit(lobby.LobbyInfo.Id);
            lobby.PlayerCount.text = $"{lobby.LobbyInfo.PlayerCount}/{lobby.LobbyInfo.MaxPlayerCount}";
        }
    }
}