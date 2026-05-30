using System;
using System.Globalization;
using DB;

namespace AquaMai.Mods.Types;

public static class OptionSpeedParser
{
    public static bool TryParse(string input, out OptionNotespeedID noteSpeed)
    {
        noteSpeed = OptionNotespeedID.Invalid;
        if (!TryParseCommonSpeed(input, out var speed)) return false;

        noteSpeed = speed == SonicSpeed ? OptionNotespeedID.Speed_Sonic : (OptionNotespeedID)speed;
        return true;
    }

    public static OptionNotespeedID ParseOrDefault(string input, OptionNotespeedID fallback)
    {
        return TryParse(input, out OptionNotespeedID noteSpeed) ? noteSpeed : fallback;
    }

    public static bool TryParse(string input, out OptionTouchspeedID touchSpeed)
    {
        touchSpeed = OptionTouchspeedID.Invalid;
        if (!TryParseCommonSpeed(input, out var speed)) return false;

        touchSpeed = speed == SonicSpeed ? OptionTouchspeedID.Speed_Sonic : (OptionTouchspeedID)speed;
        return true;
    }

    public static OptionTouchspeedID ParseOrDefault(string input, OptionTouchspeedID fallback)
    {
        return TryParse(input, out OptionTouchspeedID touchSpeed) ? touchSpeed : fallback;
    }

    public static bool TryParse(string input, out OptionSlidespeedID slideSpeed)
    {
        slideSpeed = OptionSlidespeedID.Invalid;
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value)) return false;

        if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
        {
            slideSpeed = OptionSlidespeedID.Normal;
            return true;
        }

        if (TryParseRelativeValue(value, "fast", out var fastValue)) return TrySetSlideSpeed(-fastValue, out slideSpeed);
        if (TryParseRelativeValue(value, "late", out var lateValue)) return TrySetSlideSpeed(lateValue, out slideSpeed);

        return decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numericValue)
               && TrySetSlideSpeed(numericValue, out slideSpeed);
    }

    public static OptionSlidespeedID ParseOrDefault(string input, OptionSlidespeedID fallback)
    {
        return TryParse(input, out OptionSlidespeedID slideSpeed) ? slideSpeed : fallback;
    }

    private const int SonicSpeed = 37;

    private static bool TryParseCommonSpeed(string input, out int speed)
    {
        speed = (int)OptionNotespeedID.Invalid;
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value)) return false;

        if (string.Equals(value, "sonic", StringComparison.OrdinalIgnoreCase))
        {
            speed = SonicSpeed;
            return true;
        }

        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numericValue)) return false;
        if (numericValue < 1m || numericValue > 10m || numericValue * 4m != decimal.Truncate(numericValue * 4m)) return false;

        speed = (int)((numericValue - 1m) * 4m);
        return true;
    }

    private static bool TryParseRelativeValue(string input, string prefix, out decimal value)
    {
        value = 0m;
        if (!input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var suffix = input.Substring(prefix.Length).TrimStart(' ', '_');
        return decimal.TryParse(suffix, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static bool TrySetSlideSpeed(decimal numericValue, out OptionSlidespeedID slideSpeed)
    {
        slideSpeed = OptionSlidespeedID.Invalid;
        if (numericValue < -1m || numericValue > 1m || numericValue * 10m != decimal.Truncate(numericValue * 10m)) return false;

        slideSpeed = (OptionSlidespeedID)((int)((numericValue + 1m) * 10m));
        return true;
    }
}
