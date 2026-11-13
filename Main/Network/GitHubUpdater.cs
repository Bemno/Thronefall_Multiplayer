using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using BepInEx.Configuration;
using ThronefallMP.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace ThronefallMP.Network;

// Checks the GitHub releases of this fork once per start and offers to install a newer DLL from the title screen.
public class GitHubUpdater : MonoBehaviour
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Bemno/Thronefall_Multiplayer/releases/latest";
    private const string DllAssetName = "com.badwolf.thronefall_mp.dll";

    [Serializable]
    private class Release
    {
        public string tag_name;
        public Asset[] assets;
    }

    [Serializable]
    private class Asset
    {
        public string name;
        public string browser_download_url;
        public string digest;
    }

    private ConfigEntry<bool> _enabled;

    private void Start()
    {
        _enabled = Plugin.Instance.Config.Bind("Update", "CheckGitHub", true,
            "Check GitHub for a newer version of the mod when the game starts.");
        if (_enabled.Value)
        {
            StartCoroutine(Check());
        }
    }

    private IEnumerator Check()
    {
        using var request = UnityWebRequest.Get(LatestReleaseUrl);
        request.SetRequestHeader("User-Agent", "ThronefallMultiplayer");
        request.SetRequestHeader("Accept", "application/vnd.github+json");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            Plugin.Log.LogInfo($"GitHub> update check failed: {request.error}");
            yield break;
        }

        Release release;
        try
        {
            release = JsonUtility.FromJson<Release>(request.downloadHandler.text);
        }
        catch (Exception e)
        {
            Plugin.Log.LogInfo($"GitHub> could not read release: {e.Message}");
            yield break;
        }

        var latest = release?.tag_name?.TrimStart('v');
        var asset = release?.assets?.FirstOrDefault(a => a.name == DllAssetName);
        if (latest == null || asset == null || !Version.TryParse(latest, out var latestVersion)
            || !Version.TryParse(PluginInfo.PLUGIN_VERSION, out var currentVersion) || latestVersion <= currentVersion)
        {
            Plugin.Log.LogInfo($"GitHub> up to date (latest {latest ?? "unknown"})");
            yield break;
        }

        Plugin.Log.LogInfo($"GitHub> v{latest} available");
        while (UIManager.TitleScreen == null || !UIManager.TitleScreen.activeInHierarchy || Plugin.Instance.Network.Online)
        {
            yield return new WaitForSeconds(1f);
        }

        var dialog = UIManager.CreateMessageDialog("Update available",
            $"Multiplayer v{latest} is available on GitHub, you run v{PluginInfo.PLUGIN_VERSION}.\nInstall it now?",
            "Update",
            onClick: () => StartCoroutine(Download(asset, latest)));
        dialog.SecondButtonText = "Later";
    }

    private IEnumerator Download(Asset asset, string version)
    {
        using var request = UnityWebRequest.Get(asset.browser_download_url);
        request.SetRequestHeader("User-Agent", "ThronefallMultiplayer");
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            Fail($"Download failed: {request.error}");
            yield break;
        }

        var dll = request.downloadHandler.data;
        if (dll == null || dll.Length < 2 || dll.Length > ModUpdater.MaxDllSize || dll[0] != 'M' || dll[1] != 'Z')
        {
            Fail("The downloaded file is not a valid mod DLL.");
            yield break;
        }

        if (!string.IsNullOrEmpty(asset.digest) && asset.digest.StartsWith("sha256:"))
        {
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", "").ToLowerInvariant();
            if (hash != asset.digest.Substring("sha256:".Length).ToLowerInvariant())
            {
                Fail("The downloaded file does not match the release checksum.");
                yield break;
            }
        }

        try
        {
            ModUpdater.InstallDll(dll, version);
        }
        catch (Exception e)
        {
            Fail($"The update failed: {e.Message}");
        }
    }

    private static void Fail(string message)
    {
        Plugin.Log.LogError($"GitHub> {message}");
        UIManager.CreateMessageDialog("Update failed", message);
    }
}
