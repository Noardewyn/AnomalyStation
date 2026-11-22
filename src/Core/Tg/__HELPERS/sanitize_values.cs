// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/sanitize_values.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F: the preferences): the general sanitizers and the hex colour. sanitize_gender and the ooc colour come with the
// preferences that read them.

public static partial class GlobalProcs
{
    //general stuff
    // tg: code/__HELPERS/sanitize_values.dm:2
    public static object sanitize_integer(object number = null, object min = null, object max = null, object @default = null)
    {
        min ??= 0;
        max ??= 1;
        @default ??= 0;
        if (Dm.isnum(number))
        {
            number = Dm.round(Dm.num(number));
            if (Dm.le(min, number) && Dm.le(number, max))
                return number;
        }
        return @default;
    }

    // tg: code/__HELPERS/sanitize_values.dm:9
    public static object sanitize_float(object number = null, object min = null, object max = null, object accuracy = null, object @default = null)
    {
        min ??= 0;
        max ??= 1;
        accuracy ??= 1;
        @default ??= 0;
        if (Dm.isnum(number))
        {
            number = Dm.round(Dm.num(number), Dm.num(accuracy));
            if (Dm.le(Dm.round(Dm.num(min), Dm.num(accuracy)), number) && Dm.le(number, Dm.round(Dm.num(max), Dm.num(accuracy))))
                return number;
        }
        return @default;
    }

    // tg: code/__HELPERS/sanitize_values.dm:16
    public static object sanitize_text(object text = null, object @default = null)
    {
        @default ??= "";
        if (Dm.istext(text))
            return text;
        return @default;
    }

    // tg: code/__HELPERS/sanitize_values.dm:21
    public static object sanitize_islist(object value = null, object @default = null)
    {
        if (Dm.islist(value) && Dm.truthy(length(value)))
            return value;
        if (Dm.truthy(@default))
            return @default;
        return null;
    }

    // tg: code/__HELPERS/sanitize_values.dm:27
    public static object sanitize_inlist(object value = null, DmList List = null, object @default = null)
    {
        if (List != null && Dm.@in(value, List))
            return value;
        if (Dm.truthy(@default))
            return @default;
        if (Dm.truthy(List?.len))
            return pick(List);
        return null;
    }
}

// tg: code/__HELPERS/sanitize_values.dm:54 (M10 slice F: the colour preferences)
public static partial class GlobalProcs
{
    // tg: code/__HELPERS/sanitize_values.dm:54
    public static object sanitize_hexcolor(object color = null, object desired_format = null, object include_crunch = null, object @default = null)
    {
        desired_format ??= DEFAULT_HEX_COLOR_LEN;
        include_crunch ??= TRUE;
        string crunch = Dm.truthy(include_crunch) ? "#" : "";
        if (!Dm.istext(color))
            color = "";

        float start = 1 + (text2ascii(color, 1) == 35 ? 1 : 0);
        float len = length(color);
        string @char = "";
        // Used for conversion between RGBA hex formats.
        string format_input_ratio = Dm.text("[]:[]", desired_format, length_char(color) - (start - 1));

        string dot = "";
        float i = start;
        while (i <= len)
        {
            @char = Dm.textchar(color, i);
            i += length(@char);
            float ascii = text2ascii(@char);
            if (ascii >= 48 && ascii <= 57) //numbers 0 to 9
                dot += @char;
            else if (ascii >= 97 && ascii <= 102) //letters a to f
                dot += @char;
            else if (ascii >= 65 && ascii <= 70) //letters A to F
            {
                @char = LOWER_TEXT(@char);
                dot += @char;
            }
            else
                break;
            if (format_input_ratio is "3:8" or "4:8" or "3:6" or "4:6") //skip next one. RRGGBB(AA) -> RGB(A)
                i += length(Dm.textchar(color, i));
            else if (format_input_ratio is "6:4" or "6:3" or "8:4" or "8:3") //add current char again. RGB(A) -> RRGGBB(AA)
                dot += @char;
        }

        if (Dm.eq(length_char(dot), desired_format))
            return crunch + dot;
        if (format_input_ratio is "3:8" or "3:4" or "6:4") //add or remove alpha channel depending on desired format.
            return crunch + copytext(dot, 1, Dm.num(desired_format) + 1);
        if (format_input_ratio is "4:6" or "4:3" or "8:3")
            return crunch + dot + (Dm.eq(desired_format, 4) ? "f" : "ff");
        //not a supported hex color format.
        return Dm.truthy(@default) ? @default : crunch + repeat_string(desired_format, "0");
    }
}
