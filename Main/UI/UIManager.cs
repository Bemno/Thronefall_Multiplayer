using System.Collections;
using HarmonyLib;
using I2.Loc;
using Rewired.Integration.UnityUI;
using ThronefallMP.UI.Dialogs;
using ThronefallMP.UI.Panels;
using ThronefallMP.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ThronefallMP.UI;

public static class UIManager
{
    public static GameObject TitleScreen { get; private set; }
    public static LobbyListPanel LobbyListPanel { get; private set; }
    public static HostPanel HostPanel { get; private set; }
    public static ChatPanel ChatPanel { get; private set; }
    public static GameStatusPanel GameStatusPanel { get; private set; }

    public static bool ExitHandled = false;
    
    public static readonly Color DarkBackgroundColor = new(0.06f, 0.06f, 0.06f, 1.0f);
    public static readonly Color BackgroundColor = new(0.11f, 0.11f, 0.11f, 1.0f);
    public static readonly Color TransparentBackgroundColor = new(0.0f, 0.0f, 0.0f, 0.3f);
    public static readonly Color SelectedTransparentBackgroundColor = new(0.2f, 0.2f, 0.2f, 0.3f);
    public static readonly Color TextColor = new(0.78f, 0.65f, 0.46f, 1.0f);
    public static readonly Color ButtonTextColor = new(0.97f, 0.88f, 0.75f, 1.0f);
    public static readonly Color NoninteractiveButtonTextColor = new(0.3f, 0.3f, 0.4f, 1.0f);
    public static readonly Color ButtonHoverTextColor = new(0.0f, 0.41f, 0.11f);
    public static readonly Color ExitButtonColor = new(0.176f, 0.165f, 0.149f);
    public static TMP_FontAsset DefaultFont;

    private static bool _initialized;
    private static GameObject _canvas;
    private static GameObject _container;
    
    private static ThronefallUIElement FindMenuButton(Transform root, string name)
    {
        foreach (var element in root.GetComponentsInChildren<ThronefallUIElement>(true))
        {
            if (element.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return element;
            }
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var element = t.GetComponentInChildren<ThronefallUIElement>(true) ?? t.GetComponentInParent<ThronefallUIElement>();
                if (element != null) return element;
            }
        }
        Plugin.Log.LogWarning($"Main menu button '{name}' not found");
        return null;
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _canvas = GameObject.Find("UI Canvas");
        if (_canvas.GetComponent<StandaloneInputModule>() == null)
        {
            _canvas.AddComponent<StandaloneInputModule>();
        }
        
        _container = new GameObject("Mod UI", typeof(RectTransform));
        var containerTransform = _container.GetComponent<RectTransform>();
        containerTransform.SetParent(_canvas.transform, false);
        containerTransform.anchorMin = new Vector2(0, 0);
        containerTransform.anchorMax = new Vector2(1, 1);
        containerTransform.offsetMin = new Vector2(0, 0);
        containerTransform.offsetMax = new Vector2(0, 0);
        
        TitleScreen = GameObject.Find("UI Canvas/Title Frame").gameObject;
        // v2.13 moved the buttons from "Menu Items" into a per-campaign carousel page.
        var menuItems = TitleScreen.transform.Find("Menu Items") ?? TitleScreen.transform.Find("Menu Root/Menu_0_Thronefall/Menu");
        var play = FindMenuButton(menuItems, "Play");
        var settings = FindMenuButton(menuItems, "Settings");
        DefaultFont = settings.GetComponentInChildren<TextMeshProUGUI>(true).font;

        LobbyListPanel = BaseUI.Create<LobbyListPanel>(_canvas, _container);
        HostPanel = BaseUI.Create<HostPanel>(_canvas, _container);
        ChatPanel = BaseUI.Create<ChatPanel>(_canvas, _container);
        GameStatusPanel = BaseUI.Create<GameStatusPanel>(_canvas, _container);
        
        ChatPanel.Enabled = true;
        GameStatusPanel.Enabled = true;
        
        var multiplayer = Helpers.InstantiateDisabled(settings.gameObject, settings.transform.parent);
        multiplayer.name = "Multiplayer";
        multiplayer.transform.SetSiblingIndex(1);
        foreach (var localize in multiplayer.GetComponentsInChildren<Localize>(true))
        {
            Object.DestroyImmediate(localize);
        }
        var textMesh = multiplayer.GetComponentInChildren<TextMeshProUGUI>(true);
        textMesh.text = "Multiplayer";
        var button = multiplayer.GetComponentInChildren<TFUITextButton>(true);
        // game/Unity update made UnityEventBase.m_PersistentCalls inaccessible to external asms; clear the
        // inspector-wired persistent listeners via reflection (PersistentCallGroup.Clear()).
        Traverse.Create(button.onSelectionStateChange).Field("m_PersistentCalls").Method("Clear").GetValue();
        Traverse.Create(button.onApply).Field("m_PersistentCalls").Method("Clear").GetValue();
        button.onApply.AddListener(() =>
        {
            LobbyListPanel.Open();
            TitleScreen.SetActive(false);
        });
 
        button.rightNav = settings;
        if (SteamManager.Initialized)
        {
            play.rightNav = button;
            settings.leftNav = button;
        }
        else
        {
            button.ignoreMouse = true;
            button.cannotBeSelected = true;
            textMesh.color = NoninteractiveButtonTextColor;
        }
        
        multiplayer.SetActive(true);

        // Show the mod version on the main menu so players can verify both machines are on the same
        // build at a glance (the MP version check requires identical builds).
        var versionObject = new GameObject("MP Version", typeof(RectTransform));
        versionObject.transform.SetParent(TitleScreen.transform, false);
        var versionRect = (RectTransform)versionObject.transform;
        versionRect.anchorMin = new Vector2(1f, 0f);
        versionRect.anchorMax = new Vector2(1f, 0f);
        versionRect.pivot = new Vector2(1f, 0f);
        versionRect.anchoredPosition = new Vector2(-18f, 14f);
        versionRect.sizeDelta = new Vector2(500f, 30f);
        var versionText = versionObject.AddComponent<TextMeshProUGUI>();
        versionText.font = DefaultFont;
        versionText.fontSize = 18;
        versionText.alignment = TextAlignmentOptions.BottomRight;
        versionText.color = new Color(1f, 1f, 1f, 0.5f);
        versionText.raycastTarget = false;
        versionText.text = $"Multiplayer v{PluginInfo.PLUGIN_VERSION}";

        _initialized = true;
    }

    public static void CloseAllPanels()
    {
        LobbyListPanel.Enabled = false;
        HostPanel.Enabled = false;
    }

    public static WeaponDialog CreateWeaponDialog()
    {
        var dialog = BaseUI.Create<WeaponDialog>(_canvas, _container);
        dialog.Enabled = true;
        return dialog;
    }

    public static PasswordDialog CreatePasswordDialog(PasswordDialog.Confirm confirm, PasswordDialog.Cancel cancel)
    {
        var dialog = BaseUI.Create<PasswordDialog>(_canvas, _container);
        dialog.OnConfirm += confirm;
        dialog.OnCancel += cancel;
        dialog.Enabled = true;
        return dialog;
    }

    public static MessageDialog CreateMessageDialog(string title, string message, string button = null, Color? color = null, MessageDialog.ClickDelegate onClick = null)
    {
        var dialog = BaseUI.Create<MessageDialog>(_canvas, _container);
        dialog.Title = title;
        dialog.Message = message;
        if (color.HasValue)
        {
            dialog.Color = color.Value;
        }

        if (button != null)
        {
            dialog.ButtonText = button;
        }

        if (onClick != null)
        {
            dialog.OnClick += onClick;
        }
        
        dialog.Enabled = true;
        return dialog;
    }
}