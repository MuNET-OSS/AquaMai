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

    /// <summary>
    /// The source that responded faster during version info fetch.
    /// Used to decide which download URL to use (url=COS, url2=CF).
    /// </summary>
    public static PreferredSource FastestSource { get; private set; } = PreferredSource.Cos;

    /// <summary>
    /// Fetches version info from both COS and CF simultaneously using threads,
    /// uses whichever responds first. Remembers which source was faster.
    /// </summary>
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
        done.WaitOne();

        if (result == null)
        {
            throw new Exception("Failed to fetch version info from both COS and Cloudflare.");
        }

        FastestSource = source;

        JSON.MakeInto<AquaMaiVersionInfo[]>(JSON.Load(result), out var items);
        var info = items.FirstOrDefault(it => it.type == channelType);

        if (info == null)
        {
            throw new Exception($"No version info found for channel type '{channelType}'.");
        }

        return info;
    }

    /// <summary>
    /// Returns the download URL based on which source (COS/CF) was faster during version info fetch.
    /// </summary>
    public static string GetDownloadUrl(AquaMaiVersionInfo info)
    {
        if (FastestSource == PreferredSource.Cf && !string.IsNullOrEmpty(info.url2))
        {
            return info.url2;
        }
        return info.url;
    }
}
