using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AMDaemon;
using MAI2.Util;
using Manager;
using MelonLoader;
using UnityEngine;

namespace AquaMai.Core.Helpers;

/**
 * LocalAssets 不一定只在游戏根目录，数据包 / option 包也可以各自带一份：
 *
 *   <StreamingAssets>/A###/LocalAssets            数据包
 *   <AppImage.OptionMountRootPath>/M###/LocalAssets   amdaemon 挂载的 option 包
 *
 * 目录合集直接问游戏要（DataManager.GetDirs），不自己枚举：
 * option 包的目录名每个版本字母都不一样（比赛 ROM 下还会变），交给游戏判断才不会错。
 *
 * 优先级从低到高：
 *
 *   数据包 LocalAssets（目录名升序）< option 包 LocalAssets（目录名升序）< <游戏根目录>/LocalAssets
 *
 * 也就是根目录最高——用户自己的东西总能覆盖随包分发的内容；包与包之间后加载的覆盖前面的。
 * 调用方按这个顺序读，后读到的自然覆盖先读到的。
 */
public static class LocalAssets
{
    private const string LocalAssetsDirName = "LocalAssets";

    // 游戏认定的数据目录合集，null 表示还没拿到过 amdaemon 就绪后的结果
    private static List<string> _gameDirs;

    // 用来避免重复打同一份根目录日志
    private static string _loggedRoots;

    // 游戏认定的全部数据目录（数据包 + option 包），后出现的优先级更高
    private static List<string> GetGameDirs()
    {
        // 只在 amdaemon 就绪后缓存：就绪前 option 包还没挂载，缓存下来会让后面所有调用方都看不到 option 包
        if (_gameDirs != null) return _gameDirs;

        var dirs = new List<string>();
        try
        {
            Singleton<DataManager>.Instance.GetDirs(Application.streamingAssetsPath, AppImage.OptionMountRootPath, ref dirs);

            // 只在调用成功时缓存，异常时拿到的可能只有数据包、没有 option 包，缓存下来整个会话都缺目录
            if (AmDaemonReady.IsReady) _gameDirs = dirs;
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[LocalAssets] Failed to get game data directories: {e.Message}");
        }

        return dirs;
    }

    /**
     * 所有存在的 LocalAssets 根目录，按优先级从低到高。
     */
    public static List<string> GetRoots()
    {
        var roots = new List<string>();
        foreach (var dir in GetGameDirs()) AddIfExists(roots, Path.Combine(dir, LocalAssetsDirName));
        AddIfExists(roots, FileSystem.ResolvePath(LocalAssetsDirName));

        // 用户经常搞不清自己的文件为什么没生效，把实际找到的目录打出来方便排查
        var summary = string.Join(" < ", roots);
        if (summary != _loggedRoots)
        {
            _loggedRoots = summary;
            MelonLogger.Msg($"[LocalAssets] Roots (low to high priority): {summary}");
        }

        return roots;
    }

    /**
     * 把配置里填的路径翻译成候选路径，优先级从低到高，不检查是否存在。
     *
     * - 恰好是 LocalAssets，或以 LocalAssets/ 开头（例如默认的 LocalAssets/Camouflages）：
     *   在每个 LocalAssets 根目录下找同名子目录，这样包里的内容也能被读到。
     *   LocalAssetsExtra/xxx 这种不匹配，按普通相对路径处理。
     * - 其它相对路径：按游戏根目录解析，当作单个目录。
     * - 绝对路径：原样使用，不再遍历 LocalAssets。
     */
    private static List<string> BuildCandidates(string configuredPath)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(configuredPath)) return candidates;

        var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());

        if (Path.IsPathRooted(expanded))
        {
            // 根路径要保留结尾的分隔符，否则 "C:\" 被裁成 "C:" 就变成盘符相对路径了
            var root = Path.GetPathRoot(expanded) ?? string.Empty;
            candidates.Add(expanded.Length > root.Length ? expanded.TrimEnd('\\', '/') : root);
            return candidates;
        }

        var normalized = expanded.Replace('\\', '/').Trim('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        var isUnderLocalAssets = normalized.Equals(LocalAssetsDirName, StringComparison.OrdinalIgnoreCase)
                                 || normalized.StartsWith(LocalAssetsDirName + "/", StringComparison.OrdinalIgnoreCase);

        if (!isUnderLocalAssets)
        {
            candidates.Add(FileSystem.ResolvePath(expanded));
            return candidates;
        }

        var subDir = normalized.Length > LocalAssetsDirName.Length
            ? normalized.Substring(LocalAssetsDirName.Length + 1)
            : "";
        foreach (var root in GetRoots())
        {
            candidates.Add(subDir.Length == 0 ? root : Path.Combine(root, subDir));
        }

        // 根目录的 LocalAssets 不存在时也要有候选，否则 ResolvePath 会返回 null，
        // 调用方就没法像以前一样报错或者自己建目录
        var gameRootCandidate = Path.Combine(FileSystem.ResolvePath(LocalAssetsDirName), subDir);
        if (candidates.All(it => !string.Equals(it, gameRootCandidate, StringComparison.OrdinalIgnoreCase)))
            candidates.Add(gameRootCandidate);

        return candidates;
    }

    /**
     * 配置路径实际对应的所有目录，按优先级从低到高。
     * 需要把多个目录的内容合并起来时用这个。
     */
    public static List<string> ResolveAll(string configuredPath)
    {
        return BuildCandidates(configuredPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /**
     * 配置路径实际对应的那个目录或文件（优先级最高的）。
     * 一个都不存在时返回优先级最高的候选路径，方便调用方直接拿去报错或者创建目录。
     */
    public static string ResolvePath(string configuredPath)
    {
        var candidates = BuildCandidates(configuredPath);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(candidates[i]) || File.Exists(candidates[i])) return candidates[i];
        }

        return candidates.Count == 0 ? null : candidates[candidates.Count - 1];
    }

    /**
     * 在配置路径的各个候选目录里找同名文件，返回存在且优先级最高的那个，都没有则返回 null。
     */
    public static string ResolveFile(string configuredPath, string fileName)
    {
        var candidates = BuildCandidates(configuredPath);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var path = Path.Combine(candidates[i], fileName);
            if (File.Exists(path)) return path;
        }

        return null;
    }

    /**
     * 按优先级从低到高枚举文件。同名文件会按顺序出现，调用方往字典里塞就是「后面覆盖前面」。
     */
    public static IEnumerable<string> EnumerateFiles(string configuredPath, string searchPattern = "*")
    {
        foreach (var dir in ResolveAll(configuredPath))
        {
            foreach (var file in Directory.EnumerateFiles(dir, searchPattern, SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }

    private static void AddIfExists(List<string> list, string dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        if (list.Any(it => string.Equals(it, dir, StringComparison.OrdinalIgnoreCase))) return;
        list.Add(dir);
    }
}
