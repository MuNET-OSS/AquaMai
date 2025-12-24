using AquaMai.Config.Interfaces;
using Tomlet.Models;

namespace AquaMai.Config.Migration;

public class ConfigMigration_V2_3_V2_4 : IConfigMigration
{
    public string FromVersion => "2.3";
    public string ToVersion => "2.4";

    public ConfigView Migrate(ConfigView src)
    {
        var dst = (ConfigView)src.Clone();
        dst.SetValue("Version", ToVersion);

        if (src.TryGetValue<bool>("GameSystem.KeyMap.DisableIO4", out var disableIO4))
        {
            dst.SetValue("GameSystem.KeyMap.DisableIO4_1P", disableIO4);
            dst.SetValue("GameSystem.KeyMap.DisableIO4_2P", disableIO4);
            dst.SetValue("GameSystem.KeyMap.DisableIO4System", disableIO4);
            dst.Remove("GameSystem.KeyMap.DisableIO4");
        }

        if (src.IsSectionEnabled("GameSystem.SkipBoardNoCheck"))
        {
            dst.EnsureDictionary("GameSystem.OldCabLightBoardSupport");
            dst.Remove("GameSystem.SkipBoardNoCheck");
        }

        if (src.TryGetValue<bool>("GameSystem.MaimollerIO.P1", out var mml1p))
        {
            dst.SetValue("GameSystem.MaimollerIO.Touch1p", mml1p);
            dst.SetValue("GameSystem.MaimollerIO.Button1p", mml1p);
            dst.SetValue("GameSystem.MaimollerIO.Led1p", mml1p);
            dst.Remove("GameSystem.MaimollerIO.P1");
        }

        if (src.TryGetValue<bool>("GameSystem.MaimollerIO.P2", out var mml2p))
        {
            dst.SetValue("GameSystem.MaimollerIO.Touch2p", mml2p);
            dst.SetValue("GameSystem.MaimollerIO.Button2p", mml2p);
            dst.SetValue("GameSystem.MaimollerIO.Led2p", mml2p);
            dst.Remove("GameSystem.MaimollerIO.P2");
        }

        if (src.TryGetValue<bool>("GameSystem.AdxHidInput.Io4Compact", out var adxDisableButtons))
        {
            dst.SetValue("GameSystem.AdxHidInput.DisableButtons", adxDisableButtons);
            dst.Remove("GameSystem.AdxHidInput.Io4Compact");
        }

        return dst;
    }
}

