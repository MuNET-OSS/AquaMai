using System;
using System.Threading.Tasks;
using MuMod.Models;
using System.Net;
using MelonLoader.TinyJSON;
using System.Linq;

namespace MuMod.Utils;

public static class VersionApi
{
    public static AquaMaiVersionInfo GetVersionInfo()
    {
        using var client = new WebClient();
        var result = client.DownloadString("https://munet-version-config-1251600285.cos.ap-shanghai.myqcloud.com/aquamai.json");
        JSON.MakeInto<AquaMaiVersionInfo[]>(JSON.Load(result), out var items);
        return items.FirstOrDefault(it => it.type == "ci");
    }
}
