using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AquaMai.ErrorReport;

public partial class CrashForm : Form
{
    public CrashForm()
    {
        InitializeComponent();
        // 找那个竖着的屏幕
        var screen = Screen.AllScreens.FirstOrDefault(s => s.Bounds.Width < s.Bounds.Height);
        if (screen == null)
        {
            screen = Screen.PrimaryScreen;
        }

        // 把窗口大小设置成屏幕最短边的三分之二
        var minSize = Math.Min(screen.Bounds.Width, screen.Bounds.Height) * 2 / 3;
        Size = new Size(minSize, minSize);
        // 如果屏幕是竖的，就放在屏幕下方那个正方形 1:1 位置的中间
        if (screen.Bounds.Width < screen.Bounds.Height)
        {
            Location = new Point(screen.Bounds.Left + (screen.Bounds.Width - minSize) / 2, screen.Bounds.Top + (screen.Bounds.Height - screen.Bounds.Width) + (screen.Bounds.Width - minSize) / 2);
        }
    }

    private string? zipFile = null;

    private async void CrashForm_Load(object sender, EventArgs e)
    {
        labelStatus.Text = "正在生成错误报告... Gathering error log...";
        var exePath = Path.GetDirectoryName(Application.ExecutablePath);
        var gameDir = Path.GetDirectoryName(exePath);
        if (!File.Exists(Path.Combine(gameDir, "Sinmai.exe")))
        {
            gameDir = Environment.CurrentDirectory;
        }

        if (!File.Exists(Path.Combine(gameDir, "Sinmai.exe")))
        {
            labelStatus.Text = "未找到游戏文件夹 Game directory not found";
            return;
        }

        var errorLogPath = Path.Combine(gameDir, "Errorlog");
        if (!Directory.Exists(errorLogPath))
        {
            Directory.CreateDirectory(errorLogPath);
        }

        try
        {
            labelVersion.Text = "AquaMai v" + FileVersionInfo.GetVersionInfo(Path.Combine(gameDir, "Mods", "AquaMai.dll")).ProductVersion;
        }
        catch
        {
            labelVersion.Text = "AquaMai (Version Unknown)";
        }

        try
        {
            var logFiles = Directory.GetFiles(errorLogPath, "*.log");
            zipFile = Path.Combine(errorLogPath, $"AquaMaiErrorReport_{DateTime.Now:yyyyMMddHHmmss}.zip");
            using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Create);

            long latestLogTime = 0;
            foreach (var logFile in logFiles)
            {
                zip.CreateEntryFromFile(logFile, Path.GetFileName(logFile));
                if (long.TryParse(Path.GetFileNameWithoutExtension(logFile), out var time) && time > latestLogTime)
                {
                    latestLogTime = time;
                }
            }

            if (latestLogTime != 0)
            {
                var latestLogFile = Path.Combine(errorLogPath, $"{latestLogTime}.log");
                var latestLogPng = Path.Combine(errorLogPath, $"{latestLogTime}.png");
                if (File.Exists(latestLogFile))
                {
                    textLog.Text = File.ReadAllText(latestLogFile).Replace("\r\n", "\n").Replace("\n", "\r\n");
                }

                // if (File.Exists(latestLogPng))
                // {
                //     zip.CreateEntryFromFile(latestLogPng, Path.GetFileName(latestLogPng));
                // }
            }

            await CreateZipTxtFromDirContent(zip, Path.Combine(gameDir, "Sinmai_Data", "StreamingAssets"));
            await CreateZipTxtFromDirContent(zip, Path.Combine(gameDir, "Mods"), true);
            await CreateZipTxtFromDirContent(zip, Path.Combine(gameDir, "UserLibs"));
            await CreateZipTxtFromDirContent(zip, Path.Combine(gameDir, "LocalAssets"));
            await CreateZipTxtFromDirContent(zip, gameDir, true);

            AddFileToZipIfExist(zip, Path.Combine(gameDir, "AquaMai.toml"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "mai2.ini"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "segatools.ini"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "daemon/segatools.ini"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../AMDaemon/segatools.ini"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "DEVICE/aime.txt"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "daemon/DEVICE/aime.txt"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../AMDaemon/DEVICE/aime.txt"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "start.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "start.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "launch.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "launch.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "启动.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "启动.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "！启动.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "！启动.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../start.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../start.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../launch.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../launch.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../启动.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../启动.cmd"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../！启动.bat"));
            AddFileToZipIfExist(zip, Path.Combine(gameDir, "../！启动.cmd"));

            var melonLog = Path.Combine(gameDir, "MelonLoader", "Latest.log");
            if (File.Exists(melonLog))
            {
                // zip.CreateEntryFromFile(melonLog, Path.GetFileName(melonLog));
                var entry = zip.CreateEntry("MelonLoader.txt");
                using var stream = entry.Open();
                using var fs = new FileStream(melonLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                await fs.CopyToAsync(stream);
            }

            labelStatus.Text = $"{zipFile}";
        }
        catch (Exception ex)
        {
            labelStatus.Text = $"生成错误报告失败 Failed to generate error report";
            if (string.IsNullOrWhiteSpace(textLog.Text))
            {
                textLog.Text = ex.ToString();
            }
        }
        finally
        {
            if (HasOtherMods(gameDir))
            {
                textLog.Text = $"如果你同时应用了其他非 MuNET 制作的 Mod，请先将它们移除后并尝试重新复现该问题，否则请不要主动向 MuNET 管理员反馈。\r\n\r\n{textLog.Text}";
            }
            textLog.Select(0, 0);
        }
    }

    private static bool HasOtherMods(string gameDir)
    {
        var modsDir = Path.Combine(gameDir, "Mods");
        if (!Directory.Exists(modsDir))
        {
            return false;
        }

        return Directory.EnumerateFiles(modsDir, "*.dll", SearchOption.TopDirectoryOnly).Any(file =>
        {
            var fileName = Path.GetFileName(file);
            return !fileName.Equals("AquaMai.dll", StringComparison.OrdinalIgnoreCase)
                && !fileName.Equals("MuMod.dll", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void AddFileToZipIfExist(ZipArchive zip, string file)
    {
        if (File.Exists(file))
        {
            zip.CreateEntryFromFile(file, Path.GetFileName(file));
        }
    }

    private async Task CreateZipTxtFromDirContent(ZipArchive zip, string dir, bool includeMd5 = false)
    {
        if (!Directory.Exists(dir)) return;
        var subFiles = Directory.GetFileSystemEntries(dir);
        using var subZip = zip.CreateEntry($"{Path.GetFileName(dir)}.txt").Open();
        using var writer = new StreamWriter(subZip);
        foreach (var subFile in subFiles)
        {
            if (includeMd5 && File.Exists(subFile))
            {
                await writer.WriteLineAsync($"{subFile} {GetFileMD5(subFile)}");
            }
            else
            {
                await writer.WriteLineAsync(subFile);
            }
        }

        await writer.FlushAsync();
    }

    private static string GetFileMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = md5.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    private void CrashForm_KeyDown(object sender, KeyEventArgs e)
    {
        Application.Exit();
    }
}
