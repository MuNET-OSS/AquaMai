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

    /// <summary>
    /// Resolves the cache path to an absolute path based on the game directory.
    /// </summary>
    public static string GetCachePath()
    {
        var path = _config.cache_path;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = @"LocalAssets\MuMod.cache";
        }
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(Environment.CurrentDirectory, expanded);
    }

    /// <summary>
    /// Maps the user-facing channel name to the internal type string in the version API.
    /// "fast" -> "ci", "slow" -> "slow"
    /// </summary>
    public static string GetChannelType()
    {
        var channel = (_config.channel ?? "slow").Trim().ToLowerInvariant();
        return channel == "slow" ? "slow" : "ci";
    }
}
