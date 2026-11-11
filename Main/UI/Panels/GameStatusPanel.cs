using ThronefallMP.UI.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThronefallMP.UI.Panels;

public class GameStatusPanel : BaseUI
{
    private static readonly Color BackgroundColor = new(0.11f, 0.11f, 0.11f, 0.95f);
    private static readonly Color EntryColor = new(0.2f, 0.2f, 0.2f, 1f);
    
    public override string Name => "Game Status Panel";

    private GameObject _playerList;
    private ButtonControl _copyInvite;
    
    public override void ConstructPanelContent()
    {
        var container = UIHelper.CreateUIObject("container", PanelRoot);
        {
            var rectTransform = container.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(0.25f, 1);
            UIHelper.SetLayoutGroup<VerticalLayoutGroup>(
                container,
                true,
                false,
                true,
                true,
                0,
                10,
                10,
                10,
                10,
                TextAnchor.UpperLeft
            );
        }
        
        _playerList = UIHelper.CreateUIObject("background", container);
        {
            _playerList.AddComponent<Mask>();
            var image = _playerList.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.color = BackgroundColor;
            var rectTransform = _playerList.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            UIHelper.SetLayoutGroup<VerticalLayoutGroup>(
                _playerList,
                true,
                false,
                true,
                true,
                5,
                10,
                10,
                10,
                10,
                TextAnchor.UpperLeft
            );
        }
        var playerEntry = UIHelper.CreateUIObject($"player_header", _playerList);
        UIHelper.SetLayoutGroup<HorizontalLayoutGroup>(
            playerEntry,
            false,
            false,
            true,
            true,
            0,
            0,
            0,
            15,
            15,
            TextAnchor.UpperCenter
        );

        var playerName = UIHelper.CreateText(playerEntry, "name", "Name", UIManager.DefaultFont);
        playerName.alignment = TextAlignmentOptions.Left;
        playerName.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIHelper.SetLayoutElement(playerName.gameObject, flexibleWidth: 1);
        
        var ping = UIHelper.CreateText(playerEntry, "ping", "Ping", UIManager.DefaultFont);
        ping.alignment = TextAlignmentOptions.Center;
        ping.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIHelper.SetLayoutElement(ping.gameObject, minWidth: 40);
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(_playerList.GetComponent<RectTransform>());

        foreach (var player in Plugin.Instance.PlayerManager.GetAllPlayers())
        {
            CreatePlayerLine(player);
        }

        Plugin.Instance.PlayerManager.OnPlayerAdded += CreatePlayerLine;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_playerList.GetComponent<RectTransform>());
        _playerList.SetActive(false);

        _copyInvite = UIHelper.CreateButton(container, "copy_invite", "Copy Invite");
        _copyInvite.Normal.Size = 24;
        _copyInvite.Hover.Size = 28;
        _copyInvite.NormalSelected.Size = 28;
        _copyInvite.HoverSelected.Size = 28;
        _copyInvite.Noninteractive.Size = 24;
        _copyInvite.Reset();
        UIHelper.SetLayoutElement(_copyInvite.gameObject, minHeight: 40);
        _copyInvite.OnClick += CopyInvite;
        _copyInvite.gameObject.SetActive(false);
    }

    private static void CopyInvite()
    {
        var network = Plugin.Instance.Network;
        if (!network.Online || !network.Lobby.IsValid())
        {
            return;
        }

        GUIUtility.systemCopyBuffer = Network.Network.InviteLink(network.Lobby);
        UIManager.CreateMessageDialog("Invite copied", "The invite link is on your clipboard. Send it to a friend: they press Direct Connect (or Ctrl+V) in the Multiplayer menu, or paste it into Win+R while the game is running.");
        ThronefallAudioManager.Oneshot(ThronefallAudioManager.AudioOneShot.ButtonApply);
    }

    private void Update()
    {
        if (Plugin.Instance.Network.Online && Input.GetKeyDown(KeyCode.BackQuote))
        {
            _playerList.SetActive(!_playerList.activeSelf);
            _copyInvite.gameObject.SetActive(_playerList.activeSelf);
            Plugin.Log.LogInfo($"List toggled {_playerList.activeSelf}");
        }
    }
    
    public void CreatePlayerLine(Network.PlayerManager.Player player)
    {
        var playerEntry = UIHelper.CreateBox(_playerList, $"player_{player.Id}", EntryColor);
        playerEntry.SetActive(false);
        UIHelper.SetLayoutGroup<HorizontalLayoutGroup>(
            playerEntry,
            false,
            false,
            true,
            true,
            5,
            5,
            5,
            15,
            15,
            TextAnchor.MiddleCenter
        );

        var isHost = UIHelper.CreateUIObject("is_host", playerEntry);
        var image = isHost.AddComponent<Image>();
        image.type = Image.Type.Filled;
        var crown = Plugin.Instance.TextureRepository.Crown;
        image.sprite = Sprite.Create(
            crown,
            new Rect(0, 0, crown.width, crown.height),
            new Vector2(0.5f, 0.5f)
        );
        UIHelper.SetLayoutElement(isHost, minWidth: 24);
        
        var playerName = UIHelper.CreateText(playerEntry, "name", "", UIManager.DefaultFont);
        playerName.alignment = TextAlignmentOptions.Left;
        playerName.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIHelper.SetLayoutElement(playerName.gameObject, flexibleWidth: 1);
        
        var ping = UIHelper.CreateText(playerEntry, "ping", "", UIManager.DefaultFont);
        ping.alignment = TextAlignmentOptions.Center;
        ping.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UIHelper.SetLayoutElement(ping.gameObject, minWidth: 40);

        var info = playerEntry.AddComponent<PlayerInfoControl>();
        info.playerId = player.Id;
        info.hostIdentifier = isHost;
        info.container = playerEntry;
        info.playerName = playerName;
        info.ping = ping;
        playerEntry.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_playerList.GetComponent<RectTransform>());
    }
}