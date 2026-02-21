using System;
using System.Globalization;
using System.Threading;
using AquaMai.Config.Attributes;
using HarmonyLib;
using JetBrains.Annotations;
using Manager;
using MelonLoader;

namespace AquaMai.Mods.Fix;

// Fixes various locale issues that pop up if the game runs in a different locale than ja-JP.
[ConfigSection(exampleHidden: true, defaultOn: true)]
public class FixLocaleIssues
{
    private static readonly CultureInfo JapanCultureInfo = new("ja-JP");
    [CanBeNull] private static TimeZoneInfo _tokyoStandardTime;

    public static void OnBeforePatch()
    {
        try
        {
            _tokyoStandardTime = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _tokyoStandardTime = TimeZoneInfo.CreateCustomTimeZone(
                "Tokyo Standard Time",
                TimeManager.JpTime,
                "(UTC+09:00) Osaka, Sapporo, Tokyo",
                "Tokyo Standard Time",
                "Tokyo Daylight Time",
                null,
                true);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"Could not get JST timezone, DateTime.Now patch will not work: {e.StackTrace}");
        }
    }

    // This covers all calls to T.Parse(String) or T.Parse(String, NumberStyles)
    // where T is a number. Sets the current thread's culture to Japan before calling
    // the original getter, since that's what the original getter depends on.
    //
    // While we already set the thread's culture on the OnBeforePatch lifecycle hook,
    // it seems to not apply to the game at all for some reason.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NumberFormatInfo), "get_CurrentInfo")]
    public static bool NumberFormatInfo_get_CurrentInfo(ref NumberFormatInfo __result)
    {
        __result = JapanCultureInfo.NumberFormat;
        return false;
    }

    // Same for datetimes.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DateTimeFormatInfo), "get_CurrentInfo")]
    public static bool DateTimeFormatInfo_get_CurrentInfo(ref DateTimeFormatInfo __result)
    {
        __result = JapanCultureInfo.DateTimeFormat;
        return false;
    }
    
    // Forces local timezone to be UTC+9, since segatools didn't patch it properly until recent versions,
    // which doesn't actually work well with maimai.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DateTime), "get_Now")]
    private static bool DateTime_get_Now(ref DateTime __result)
    {
        if (_tokyoStandardTime == null)
        {
            return true;
        }

        __result = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tokyoStandardTime!);
        return false;
    }
}
