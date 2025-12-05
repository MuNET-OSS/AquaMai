using System;
using System.Linq;
using System.Net;
using System.Reflection;
using MuMod.Utils;
using MelonLoader;

[assembly: MelonInfo(typeof(MuMod.Main), "MuMod Loader", MuMod.Main.LoaderVersion, "MuNET Team")]

namespace MuMod;

public class Main : MelonMod
{
    public const string LoaderVersion = "1.0.0";

    public override void OnEarlyInitializeMelon()
    {
        MelonLogger.Msg("Loading version information...");
        var versionInfo = VersionApi.GetVersionInfo();
        MelonLogger.Msg($"Loading {versionInfo.version} Build {versionInfo.createdAt}...");

        using var client = new WebClient();
        var data = client.DownloadData(versionInfo.url2);
        if (AquaMaiSignatureV2.VerifySignature(data).Status != AquaMaiSignatureV2.VerifyStatus.Valid)
        {
            MelonLogger.Error("Invalid signature");
            return;
        }
        var asm = Assembly.Load(data);
        var masm = MelonAssembly.LoadMelonAssembly(asm.GetName().Name, asm, true);
        foreach (var melon in masm.LoadedMelons)
        {
            melon.Register();
        }
    }
}
