using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using MuMod.Utils;
using MelonLoader;
using System.Runtime.InteropServices;


namespace MuMod;

public class Main : MelonMod
{
    public const string LoaderVersion = "1.0.0";
    public const string Description = "MuMod Loader";
    public const string Author = "MuNET Team";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    public override void OnEarlyInitializeMelon()
    {
        SetConsoleOutputCP(65001);

        // Load config from mumod.toml
        ConfigManager.Load();
        var channelType = ConfigManager.GetChannelType();

        // Fetch version info with dual-source racing
        var versionInfo = VersionApi.GetVersionInfo(channelType);
        MelonLogger.Msg($"Latest version: {versionInfo.version} (type: {versionInfo.type})");

        // Try loading from cache first
        var data = TryLoadFromCache(versionInfo.version);

        if (data != null)
        {
            MelonLogger.Msg("Loaded from cache.");
        }
        else
        {
            // Download from network, selecting source based on racing result
            var downloadUrl = VersionApi.GetDownloadUrl(versionInfo);
            var sourceName = VersionApi.FastestSource == PreferredSource.Cos ? "COS" : "Cloudflare";
            MelonLogger.Msg($"Downloading {versionInfo.version} from {sourceName}...");

            using var client = new WebClient();
            data = client.DownloadData(downloadUrl);

            if (AquaMaiSignatureV2.VerifySignature(data).Status != AquaMaiSignatureV2.VerifyStatus.Valid)
            {
                MelonLogger.Error("Invalid signature on downloaded data.");
                return;
            }

            MelonLogger.Msg("Signature verified.");

            // Try to cache the downloaded data
            TrySaveToCache(data);
        }

        var asm = Assembly.Load(data);
        var masm = MelonAssembly.LoadMelonAssembly(asm.GetName().Name, asm, true);
        foreach (var melon in masm.LoadedMelons)
        {
            melon.Register();
        }
    }

    /// <summary>
    /// Normalizes a version string for comparison by stripping the "v" prefix.
    /// API returns "v1.7.5-19-g0d4b39e", DLL embeds "1.7.5-19-g0d4b39e".
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        if (version != null && version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            return version.Substring(1);
        }
        return version ?? "";
    }

    /// <summary>
    /// Tries to load DLL data from cache.
    /// Reads the version from the cached DLL's FileVersionInfo and compares with latest.
    /// Returns null if cache is unavailable, version mismatch, or signature verification fails.
    /// </summary>
    private byte[] TryLoadFromCache(string latestVersion)
    {
        try
        {
            var cachePath = ConfigManager.GetCachePath();

            if (!File.Exists(cachePath))
            {
                return null;
            }

            // Read version directly from the cached DLL's embedded file version info
            var fileInfo = FileVersionInfo.GetVersionInfo(cachePath);
            var cachedVersion = fileInfo.ProductVersion;

            if (NormalizeVersion(cachedVersion) != NormalizeVersion(latestVersion))
            {
                MelonLogger.Msg($"Cache version mismatch (cached: {cachedVersion}, latest: {latestVersion}), will re-download.");
                DeleteCache(cachePath);
                return null;
            }

            var data = File.ReadAllBytes(cachePath);
            var verifyResult = AquaMaiSignatureV2.VerifySignature(data);
            if (verifyResult.Status != AquaMaiSignatureV2.VerifyStatus.Valid)
            {
                MelonLogger.Warning($"Cache signature verification failed ({verifyResult.Status}), will re-download.");
                DeleteCache(cachePath);
                return null;
            }

            return data;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Failed to load from cache: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tries to save DLL data to cache. Silently fails if the path is not writable.
    /// </summary>
    private void TrySaveToCache(byte[] data)
    {
        try
        {
            var cachePath = ConfigManager.GetCachePath();

            var dir = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(cachePath, data);
            MelonLogger.Msg($"Cached to {cachePath}");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Failed to write cache: {ex.Message}");
            // Continue without caching - this is a non-critical failure
        }
    }

    /// <summary>
    /// Deletes cache file, silently ignoring errors.
    /// </summary>
    private static void DeleteCache(string cachePath)
    {
        try { File.Delete(cachePath); } catch { }
    }
}
