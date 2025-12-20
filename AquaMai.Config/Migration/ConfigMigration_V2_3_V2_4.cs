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

        return dst;
    }
}

