using System;
using System.Linq;
using System.Net;
using System.Threading;
using MelonLoader;
using MelonLoader.TinyJSON;
using MuMod.Models;

namespace MuMod.Utils;

public enum PreferredSource
{
    Cos,
    Cf
}

public static class VersionApi
{
    private const string CosUrl = "https://munet-version-config-1251600285.cos.ap-shanghai.myqcloud.com/aquamai.json";
    private const string CfUrl = "https://aquamai-version-config.mumur.net/api/config";

    // 拉取版本信息时响应更快的源，用于决定后续下载走哪个 URL
    public static PreferredSource FastestSource { get; private set; } = PreferredSource.Cos;

    // 同时请求 COS 和 CF 两个源，谁先返回用谁
    public static AquaMaiVersionInfo GetVersionInfo(string channelType)
    {
        string result = null;
        var source = PreferredSource.Cos;
        var lockObj = new object();
        var hasResult = false;
        var failCount = 0;
        var done = new ManualResetEvent(false);

        void Fetch(string url, PreferredSource src, string name)
        {
            try
            {
                using var client = new WebClient();
                var data = client.DownloadString(url);
                lock (lockObj)
                {
                    if (!hasResult)
                    {
                        result = data;
                        source = src;
                        hasResult = true;
                        done.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Failed to fetch from {name}: {ex.Message}");
                if (Interlocked.Increment(ref failCount) >= 2)
                {
                    done.Set();
                }
            }
        }

        var cosThread = new Thread(() => Fetch(CosUrl, PreferredSource.Cos, "COS")) { IsBackground = true };
        var cfThread = new Thread(() => Fetch(CfUrl, PreferredSource.Cf, "Cloudflare")) { IsBackground = true };

        cosThread.Start();
        cfThread.Start();
        done.WaitOne(TimeSpan.FromSeconds(15));

        if (result == null)
        {
            MelonLogger.Warning("Failed to fetch version info from both COS and Cloudflare.");
            return null;
        }

        FastestSource = source;

        try
        {
            JSON.MakeInto<AquaMaiVersionInfo[]>(JSON.Load(result), out var items);
            var info = items.FirstOrDefault(it => it.type == channelType);

            if (info == null)
            {
                MelonLogger.Warning($"No version info found for channel type '{channelType}'.");
            }

            return info;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Failed to parse version info: {ex.Message}");
            return null;
        }
    }

    public static string GetDownloadUrl(AquaMaiVersionInfo info)
    {
        if (FastestSource == PreferredSource.Cf && !string.IsNullOrEmpty(info.url2))
        {
            return info.url2;
        }
        return info.url;
    }
}
