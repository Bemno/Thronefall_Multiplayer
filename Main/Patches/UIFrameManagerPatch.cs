using HarmonyLib;
using I2.Loc;
using ThronefallMP.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThronefallMP.Patches;

public static class UIFrameManagerPatch
{
    private static RectTransform[] _buttonTransforms;
    private static VerticalLayoutGroup _menuItems;
    private static TFUITextButton _resignButton;
    private static TFUITextButton _disconnectButton;
    private static PauseUILoadoutHelper _pauseUI;
    private static ThronefallUIElement _copyInviteButton;
    
    public static void Apply()
    {
        On.UIFrameManager.OpenMenu += OpenMenu;
        On.UIFrameManager.ProcessFrameChange += ProcessFrameChange;
    }

    private static void OpenMenu(On.UIFrameManager.orig_OpenMenu original, UIFrameManager self)
    {
        if (self.ActiveFrame == null && SceneTransitionManagerPatch.InLevelSelect)
        {
            if (_copyInviteButton == null)
            {
                _copyInviteButton = CreateCopyInviteButton(self);
            }

            if (_copyInviteButton != null)
            {
                _copyInviteButton.gameObject.SetActive(Plugin.Instance.Network.Online);
                var text = _copyInviteButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null)
                {
                    text.text = "Copy Invite";
                }
            }
        }

        if (self.ActiveFrame == null && !SceneTransitionManagerPatch.InLevelSelect)
        {
            if (_disconnectButton == null)
            {
                _pauseUI = GameObject.Find("UI Canvas").transform.Find("InMatch Pause Frame").GetComponent<PauseUILoadoutHelper>();
                var menu = _pauseUI.transform.Find("Title /Menu Items");
                _menuItems = menu.GetComponent<VerticalLayoutGroup>();

                _resignButton = menu.Find("Back To Menu").GetComponent<TFUITextButton>();
                
                var disconnect = Helpers.InstantiateDisabled(_resignButton.gameObject, _resignButton.gameObject.transform.parent);
                disconnect.name = "Disconnect";
                _disconnectButton = disconnect.GetComponent<TFUITextButton>();
                var textMesh = disconnect.GetComponent<TextMeshProUGUI>();
                Object.DestroyImmediate(disconnect.GetComponent<Localize>());
                
                Traverse.Create(_disconnectButton).Field<string>("originalString").Value = "Disconnect";
                textMesh.text = "Disconnect";
                textMesh.SetAllDirty();
                Traverse.Create(_disconnectButton.onApply).Field("m_PersistentCalls").Method("Clear").GetValue();
                _disconnectButton.onApply.AddListener(() =>
                {
                    Plugin.Instance.Network.Local();
                    SceneTransitionManagerPatch.Transition("_StartMenu", SceneTransitionManagerPatch.CurrentScene);
                });
                
                _disconnectButton.topNav = _resignButton;
                _disconnectButton.botNav = _resignButton.botNav;

                var transform = _disconnectButton.GetComponent<RectTransform>();
                transform.sizeDelta = transform.sizeDelta with { y = 80 };
                _buttonTransforms = new[]
                {
                    menu.Find("Continue").GetComponent<RectTransform>(),
                    menu.Find("Settings").GetComponent<RectTransform>(),
                    _resignButton.GetComponent<RectTransform>()
                };
            }

            if (Plugin.Instance.Network.Online)
            {
                foreach (var transform in _buttonTransforms)
                {
                    transform.sizeDelta = transform.sizeDelta with { y = 80 };
                }
                
                _disconnectButton.gameObject.SetActive(true);
                _resignButton.botNav = _disconnectButton;
                _pauseUI.botmostButton = _disconnectButton;
                _menuItems.spacing = 5;
                _pauseUI.Refresh();
            }
            else
            {
                foreach (var transform in _buttonTransforms)
                {
                    transform.sizeDelta = transform.sizeDelta with { y = 100 };
                }
                
                _disconnectButton.gameObject.SetActive(false);
                _resignButton.botNav = _disconnectButton.botNav;
                _pauseUI.botmostButton = _resignButton;
                _menuItems.spacing = 10;
                _pauseUI.Refresh();
            }
        }

        original(self);
    }

    private static ThronefallUIElement CreateCopyInviteButton(UIFrameManager self)
    {
        var frame = Traverse.Create(self).Field<OverworldPauseFrame>("overworldPauseMenuFrame").Value;
        if (frame == null)
        {
            return null;
        }

        ThronefallUIElement mainMenu = null;
        foreach (var element in frame.GetComponentsInChildren<ThronefallUIElement>(true))
        {
            if (element.name.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mainMenu = element;
                break;
            }
        }

        if (mainMenu == null)
        {
            var names = string.Join(", ", System.Linq.Enumerable.Select(frame.GetComponentsInChildren<ThronefallUIElement>(true), e => e.name));
            Plugin.Log.LogWarning($"Copy Invite: no main menu button in the overworld pause menu ({names})");
            return null;
        }

        Plugin.Log.LogInfo($"Copy Invite: placed next to '{mainMenu.name}' in the overworld pause menu");
        var copy = Helpers.InstantiateDisabled(mainMenu.gameObject, mainMenu.transform.parent);
        copy.name = "Copy Invite";
        copy.transform.SetSiblingIndex(mainMenu.transform.GetSiblingIndex() + 1);
        foreach (var localize in copy.GetComponentsInChildren<Localize>(true))
        {
            Object.DestroyImmediate(localize);
        }

        var button = copy.GetComponent<ThronefallUIElement>();
        if (button is TFUITextButton textButton)
        {
            Traverse.Create(textButton).Field<string>("originalString").Value = "Copy Invite";
        }

        var textMesh = copy.GetComponentInChildren<TextMeshProUGUI>(true);
        if (textMesh != null)
        {
            textMesh.text = "Copy Invite";
            textMesh.SetAllDirty();
        }

        Traverse.Create(button.onApply).Field("m_PersistentCalls").Method("Clear").GetValue();
        button.onApply.RemoveAllListeners();
        button.onApply.AddListener(() =>
        {
            var network = Plugin.Instance.Network;
            if (network.Online && network.Lobby.IsValid())
            {
                GUIUtility.systemCopyBuffer = Network.Network.InviteLink(network.Lobby);
                if (textMesh != null)
                {
                    textMesh.text = "Copied!";
                }
            }
        });

        button.leftNav = mainMenu;
        button.rightNav = mainMenu.rightNav;
        button.topNav = mainMenu.topNav;
        button.botNav = mainMenu.botNav;
        if (mainMenu.rightNav != null && mainMenu.rightNav.leftNav == mainMenu)
        {
            mainMenu.rightNav.leftNav = button;
        }
        mainMenu.rightNav = button;
        return button;
    }

    private static void ProcessFrameChange(
        On.UIFrameManager.orig_ProcessFrameChange original,
        UIFrameManager self,
        UIFrame nextframe,
        bool writeoldframetostack,
        bool keepoldframegameobjectactive,
        ThronefallUIElement firstselected)
    {
        if (nextframe != null)
        {
            nextframe.freezeTime = false;
        }
        
        original(
            self,
            nextframe,
            writeoldframetostack,
            keepoldframegameobjectactive,
            firstselected
        );
    }
}