using System;

namespace AquaMai.Mods.UX.JudgeDisplayPro;

/// <summary>
/// GOOD 到小 PERFECT 判定显示
/// </summary>
public enum NormalDisplayMode
{
    /// <summary>只显示判定</summary>
    JudgeOnly,
    /// <summary>显示判定 + FAST / LATE</summary>
    All,
    /// <summary>只显示 FAST / LATE</summary>
    TimingOnly,
    /// <summary>显示蓝色或者红色的判定文字</summary>
    ColoredJudge,
    /// <summary>不显示</summary>
    None,
}

/// <summary>
/// 大 P 判定显示
/// </summary>
public enum CriticalDisplayMode
{
    /// <summary>不开启大P</summary>
    None,
    /// <summary>只有绝赞开启大P，并显示</summary>
    OnBreak,
    /// <summary>绝赞开启大P，但不显示</summary>
    OffBreak,
    /// <summary>所有音符开启大P，并显示</summary>
    OnAll,
    /// <summary>所有音符开启大P，只有绝赞显示</summary>
    OnAllShowBreak,
    /// <summary>所有音符开启大P，但是不显示</summary>
    OffAll,
}

public class UserSettings
{
    public bool IsEnable = false;
    public CriticalDisplayMode CriticalDisplayMode = CriticalDisplayMode.None;
    public NormalDisplayMode PerfectDisplayMode = NormalDisplayMode.JudgeOnly;
    public NormalDisplayMode GreatDisplayMode = NormalDisplayMode.JudgeOnly;
    public NormalDisplayMode GoodDisplayMode = NormalDisplayMode.JudgeOnly;

    public string Serialize()
    {
        return $"{IsEnable},{CriticalDisplayMode},{PerfectDisplayMode},{GreatDisplayMode},{GoodDisplayMode}";
    }

    public void Deserialize(string data)
    {
        var values = data.Split(',');
        IsEnable = bool.Parse(values[0]);
        CriticalDisplayMode = (CriticalDisplayMode)Enum.Parse(typeof(CriticalDisplayMode), values[1]);
        PerfectDisplayMode = (NormalDisplayMode)Enum.Parse(typeof(NormalDisplayMode), values[2]);
        GreatDisplayMode = (NormalDisplayMode)Enum.Parse(typeof(NormalDisplayMode), values[3]);
        GoodDisplayMode = (NormalDisplayMode)Enum.Parse(typeof(NormalDisplayMode), values[4]);
    }
}
