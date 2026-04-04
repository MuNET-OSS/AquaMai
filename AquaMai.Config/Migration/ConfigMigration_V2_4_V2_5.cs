using AquaMai.Config.Interfaces;

namespace AquaMai.Config.Migration;

public class ConfigMigration_V2_4_V2_5 : IConfigMigration
{
    public string FromVersion => "2.4";
    public string ToVersion => "2.5";

    public ConfigView Migrate(ConfigView src)
    {
        var dst = (ConfigView)src.Clone();
        dst.SetValue("Version", ToVersion);

        // Migrate JudgeAdjust: global a/b → per-player a_1P/a_2P/b_1P/b_2P
        if (src.TryGetValue<double>("GameSettings.JudgeAdjust.a", out var a))
        {
            dst.SetValue("GameSettings.JudgeAdjust.a_1P", a);
            dst.SetValue("GameSettings.JudgeAdjust.a_2P", a);
            dst.Remove("GameSettings.JudgeAdjust.a");
        }

        if (src.TryGetValue<double>("GameSettings.JudgeAdjust.b", out var b))
        {
            dst.SetValue("GameSettings.JudgeAdjust.b_1P", b);
            dst.SetValue("GameSettings.JudgeAdjust.b_2P", b);
            dst.Remove("GameSettings.JudgeAdjust.b");
        }

        return dst;
    }
}
