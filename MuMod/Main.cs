using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using MuMod.Utils;
using MuMod.Models;
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

        ConfigManager.Load();
        var channelType = ConfigManager.GetChannelType();

        byte[] data;

        var versionInfo = VersionApi.GetVersionInfo(channelType);

        if (versionInfo != null)
        {
            data = LoadWithVersionInfo(versionInfo);
        }
        else
        {
            // API 不可用，尝试从缓存加载
            MelonLogger.Warning("Version API unavailable, trying cache...");
            data = TryLoadFromCache();

            if (data != null)
            {
                MelonLogger.Msg("API unavailable, loaded from cache.");
            }
            else
            {
                ErrorOverlay.SetError(
                    "MuMod: 无法获取版本信息，且本地无有效缓存\n" +
                    "请检查网络连接后重试\n\n" +
                    "MuMod: Failed to fetch version info and no valid cache available\n" +
                    "Please check your network connection and try again");
                return;
            }
        }

        if (data == null) return;

        LoadAssembly(data);
    }

    /// <summary>
    /// 有版本信息时的加载流程：先查缓存，缓存没有或版本不匹配则下载
    /// </summary>
    private byte[] LoadWithVersionInfo(AquaMaiVersionInfo versionInfo)
    {
        MelonLogger.Msg($"Latest version: {versionInfo.version} (type: {versionInfo.type})");

        var data = TryLoadFromCache(versionInfo.version);

        if (data != null)
        {
            MelonLogger.Msg("Loaded from cache.");
            return data;
        }

        // 下载
        try
        {
            var downloadUrl = VersionApi.GetDownloadUrl(versionInfo);
            var sourceName = VersionApi.FastestSource == PreferredSource.Cos ? "COS" : "Cloudflare";
            MelonLogger.Msg($"Downloading {versionInfo.version} from {sourceName}...");

            using var client = new WebClient();
            data = client.DownloadData(downloadUrl);
        }
        catch (Exception ex)
        {
            ErrorOverlay.SetError(
                $"MuMod: 下载失败\n{ex.Message}\n\n" +
                $"MuMod: Failed to download\n{ex.Message}");
            return null;
        }

        if (AquaMaiSignatureV2.VerifySignature(data).Status != AquaMaiSignatureV2.VerifyStatus.Valid)
        {
            ErrorOverlay.SetError(
                "MuMod: 签名校验失败，文件可能已损坏\n\n" +
                "MuMod: Invalid signature, file may be corrupted or tampered");
            return null;
        }

        MelonLogger.Msg("Signature verified.");

        // 写入缓存
        TrySaveToCache(data);
        return data;
    }

    private void LoadAssembly(byte[] data)
    {
        try
        {
            var asm = Assembly.Load(data);
            var masm = MelonAssembly.LoadMelonAssembly(asm.GetName().Name, asm, true);
            foreach (var melon in masm.LoadedMelons)
            {
                melon.Register();
            }
        }
        catch (Exception ex)
        {
            ErrorOverlay.SetError(
                $"MuMod: 加载程序集失败\n{ex.Message}\n\n" +
                $"MuMod: Failed to load assembly\n{ex.Message}");
        }
    }

    public override void OnInitializeMelon()
    {
        if (ErrorOverlay.HasError)
        {
            ErrorOverlay.BlockGame(HarmonyInstance);
        }
    }

    public override void OnGUI()
    {
        ErrorOverlay.Render();
    }

    // 去掉版本号前面的 "v" 前缀，方便比较
    private static string NormalizeVersion(string version)
    {
        if (version != null && version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            return version.Substring(1);
        }
        return version ?? "";
    }

    /// <summary>
    /// 从缓存加载 DLL。指定 expectedVersion 时会校验版本是否匹配，不指定则只校验签名。
    /// </summary>
    private byte[] TryLoadFromCache(string expectedVersion = null)
    {
        try
        {
            var cachePath = ConfigManager.GetCachePath();

            if (!File.Exists(cachePath))
            {
                return null;
            }

            if (expectedVersion != null)
            {
                // 读缓存 DLL 的版本号比对
                var fileInfo = FileVersionInfo.GetVersionInfo(cachePath);
                var cachedVersion = fileInfo.ProductVersion;

                if (NormalizeVersion(cachedVersion) != NormalizeVersion(expectedVersion))
                {
                    MelonLogger.Msg($"Cache version mismatch (cached: {cachedVersion}, latest: {expectedVersion}), will re-download.");
                    DeleteCache(cachePath);
                    return null;
                }
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
        }
    }

    private static void DeleteCache(string cachePath)
    {
        try { File.Delete(cachePath); } catch { }
    }
}
