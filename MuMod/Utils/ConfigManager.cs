using System;
using System.IO;
using MelonLoader;
using MuMod.Models;

namespace MuMod.Utils;

public static class ConfigManager
{
    private static MuModConfig _config;

    public static MuModConfig Config => _config;

    public static void Load()
    {
        var configPath = Path.Combine(Environment.CurrentDirectory, "MuMod.toml");

        if (!File.Exists(configPath))
        {
            _config = new MuModConfig();
            return;
        }

        try
        {
            var toml = File.ReadAllText(configPath);
            _config = TomletShim.To<MuModConfig>(toml);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Failed to parse MuMod.toml: {ex.Message}");
            MelonLogger.Warning("Using default settings.");
            _config = new MuModConfig();
        }
    }

    // 将配置中的相对路径解析为基于游戏目录的绝对路径
    public static string GetCachePath()
    {
        var path = _config.CachePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = @"LocalAssets\MuMod.cache";
        }
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Environment.CurrentDirectory, expanded);
    }

    // 将用户配置的 channel 名映射到 API 用的 type：fast → ci, slow → slow
    public static string GetChannelType()
    {
        var channel = (_config.Channel ?? "slow").Trim().ToLowerInvariant();
        switch (channel)
        {
            case "fast":
                return "ci";
            case "slow":
                return "slow";
            default:
                MelonLogger.Warning($"Unknown channel '{_config.Channel}' in MuMod.toml, defaulting to 'slow'.");
                return "slow";
        }
    }
}
