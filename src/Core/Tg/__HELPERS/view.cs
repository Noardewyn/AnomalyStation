// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/view.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: getviewsize (M5 slice 3: tgui's ui_status for observers) and view_to_pixels (drag and drop, protocol version 17:
// screen_loc_to_offset reads it for the drag as a click, client/is_drag_clickable, drag_drop.dm:100). maxviewdist and the sight
// helpers (can_see's viewers approach) are translated with the code that uses them.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/view.dm:1
    [ShouldBePure]
    public static DmList getviewsize(object view = null)
    {
        view ??= world.view;

        if (isnum(view))
        {
            //resetting back to 0- this is the same as just checking !view but we want to be clear the point of the check.
            if (Dm.eq(view, 0))
                return Dm.list(0, 0);
            float totalviewrange = (Dm.num(view) < 0 ? -1 : 1) + 2 * Dm.num(view);
            return Dm.list(totalviewrange, totalviewrange);
        }
        else
        {
            DmList viewrangelist = splittext(view, "x");
            return Dm.list(text2num(viewrangelist[1]), text2num(viewrangelist[2]));
        }
    }

    /// Takes a string or num view, and converts it to pixel width/height in a list(pixel_width, pixel_height)
    // tg: code/__HELPERS/view.dm:22
    public static DmList view_to_pixels(object view = null)
    {
        if (!Dm.truthy(view))
            return Dm.list(0, 0);
        DmList view_info = getviewsize(view);
        view_info[1] = Dm.mul(view_info[1], ICON_SIZE_X);
        view_info[2] = Dm.mul(view_info[2], ICON_SIZE_Y);
        return view_info;
    }
}
