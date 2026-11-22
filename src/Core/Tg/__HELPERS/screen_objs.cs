// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/screen_objs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (drag and drop, protocol version 17): screen_loc_to_offset and cut_relative_direction, which the drag as a click reads
// (client/is_drag_clickable, drag_drop.dm:100). The other screen_loc helpers (offset_to_screen_loc, spanning_screen_loc, ...) are
// translated with the code that uses them (the action buttons' saved positions, M9).

public static partial class GlobalProcs
{
    /// Takes a screen loc string in the format
    /// "+-left-offset:+-pixel,+-bottom-offset:+-pixel"
    /// Where the :pixel is optional, and returns
    /// A list in the format (x_offset, y_offset)
    /// We require context to get info out of screen locs that contain relative info, so NORTH, SOUTH, etc
    // tg: code/__HELPERS/screen_objs.dm:6
    public static DmList screen_loc_to_offset(object screen_loc = null, object view = null)
    {
        if (!Dm.truthy(screen_loc))
            return Dm.list(64, 64);
        DmList view_size = view_to_pixels(view);
        object x = 0;
        object y = 0;
        // Time to parse for directional relative offsets
        if (Dm.truthy(findtext(screen_loc, "EAST"))) // If you're starting from the east, we start from the east too
            x = Dm.add(x, view_size[1]);
        if (Dm.truthy(findtext(screen_loc, "WEST"))) // HHHHHHHHHHHHHHHHHHHHHH WEST is technically a 1 tile offset from the start. Shoot me please
            x = Dm.add(x, ICON_SIZE_X);
        if (Dm.truthy(findtext(screen_loc, "NORTH")))
            y = Dm.add(y, view_size[2]);
        if (Dm.truthy(findtext(screen_loc, "SOUTH")))
            y = Dm.add(y, ICON_SIZE_Y);

        DmList x_and_y = splittext(screen_loc, ",");

        DmList x_pack = splittext(x_and_y[1], ":");
        DmList y_pack = splittext(x_and_y[2], ":");

        object x_coord = x_pack[1];
        object y_coord = y_pack[1];

        if (Dm.truthy(findtext(x_coord, "CENTER")))
            x = Dm.add(x, Dm.div(view_size[1], 2));

        if (Dm.truthy(findtext(y_coord, "CENTER")))
            y = Dm.add(y, Dm.div(view_size[2], 2));

        x_coord = text2num(cut_relative_direction(x_coord));
        y_coord = text2num(cut_relative_direction(y_coord));

        x = Dm.add(x, Dm.mul(x_coord, ICON_SIZE_X));
        y = Dm.add(y, Dm.mul(y_coord, ICON_SIZE_Y));

        if (Dm.length(x_pack) > 1)
            x = Dm.add(x, text2num(x_pack[2]));
        if (Dm.length(y_pack) > 1)
            y = Dm.add(y, text2num(y_pack[2]));
        return Dm.list(x, y);
    }

    // tg: code/__HELPERS/screen_objs.dm:101
    public static object cut_relative_direction(object fragment = null)
    {
        // DEVIATION(DV45): tg's `var/static/regex/regex = regex(@"([A-Z])\w+", "g")` and `regex.Replace(fragment, "")`: the shim has no
        // /regex datum (Dm.Text.cs), so .NET's Regex does the same global replace (ECMAScript classes: \w is [A-Za-z0-9_], as BYOND's).
        if (fragment is not string text)
            return fragment == null ? "" : fragment;
        return RelativeDirectionWord.Replace(text, "");
    }

    private static readonly Regex RelativeDirectionWord = new(@"([A-Z])\w+", RegexOptions.ECMAScript);
}
