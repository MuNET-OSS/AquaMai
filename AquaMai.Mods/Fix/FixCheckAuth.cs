using System;
using System.Net;
using System.Text.RegularExpressions;
using AMDaemon.Allnet;
using AquaMai.Config.Attributes;
using AquaMai.Core.Attributes;
using AquaMai.Core.Helpers;
using AquaMai.Core.Resources;
using HarmonyLib;
using Manager;
using Manager.Operation;
using MelonLoader;
using Net;
using Net.Packet;

namespace AquaMai.Mods.Fix;

[ConfigSection(exampleHidden: true, defaultOn: true)]
public class FixCheckAuth
{
    [ConfigEntry(name: "允许 HTTPS 升级")]
    private static readonly bool allowHttpsUpgrade = true;

    private static OperationData operationData;
    private static bool tlsFailed = false;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OperationManager), "CheckAuth_Proc")]
    private static void PostCheckAuthProc(ref OperationData ____operationData)
    {
        operationData = ____operationData;
        if (Auth.GameServerUri.StartsWith("http://") || Auth.GameServerUri.StartsWith("https://"))
        {
            ____operationData.ServerUri = Auth.GameServerUri;

            // Host is used only for "CheckServerHash" and it is disabled now
            // it originally contains server's tls cert hash
            // So this can be used to transfer ambiguous data
            // we use this to notify that we can upgrade the link to https
            // as if we originally pass a https link to game, games without CheckServerHash will reject the link because ssl pinning
            if (upgradePort.IsMatch(Auth.GameServerHost) && allowHttpsUpgrade && !tlsFailed && GameInfo.GameVersion >= 22000)
            {
                var match = upgradePort.Match(Auth.GameServerHost);
                var builder = new UriBuilder(Auth.GameServerUri)
                {
                    Port = int.Parse(match.Groups[1].Value),
                    Scheme = Uri.UriSchemeHttps,
                };
                ____operationData.ServerUri = builder.ToString();
            }
        }
    }

    [HarmonyPrefix]
    [EnableIf(nameof(allowHttpsUpgrade))]
    [HarmonyPatch(typeof(Packet), "ProcImpl")]
    public static void PreProcImpl(Packet __instance, ref string ___BaseUrl)
    {
        if (__instance.State == PacketState.Process &&
            Traverse.Create(__instance).Field("Client").GetValue() is NetHttpClient client)
        {
            if (client.State == NetHttpClient.StateError && client.WebException == WebExceptionStatus.TrustFailure &&
                Auth.GameServerUri.StartsWith("http://") && operationData.ServerUri.StartsWith("https://"))
            {
                tlsFailed = true;
                operationData.ServerUri = Auth.GameServerUri;
            }
        }
        if (tlsFailed && ___BaseUrl.StartsWith("https://"))
        {
            ___BaseUrl = Auth.GameServerUri;
        }
    }

    private static readonly Regex upgradePort = new Regex(@"_AquaMai_UpgradV3_(\d+)_", RegexOptions.Compiled);
}