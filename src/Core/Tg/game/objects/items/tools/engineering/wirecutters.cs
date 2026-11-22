// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/engineering/wirecutters.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base wirecutters' Initialize (M4a P4a); suicide_act is a live-partial stub (M10); the abductor's and power
// wirecutters come after the core version (the cyborg's are var-only).

// tg: code/game/objects/items/tools/engineering/wirecutters.dm:1
public partial class obj_item_wirecutters : obj_item
{
    // tg: code/game/objects/items/tools/engineering/wirecutters.dm:36
    internal static ref DmList wirecutter_colors => ref TgState.Current.obj_item_wirecutters__wirecutter_colors;

    // tg: code/game/objects/items/tools/engineering/wirecutters.dm:54
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(random_color))
        {
            // set_greyscale(colors = list(pick(wirecutter_colors))) (game/atom/atom_greyscale.dm:10)
            // GAP(lead): /atom/proc/set_greyscale is not declared (atom_greyscale.cs: "translated with clothing and dyes");
            // its body for a one-colour list and no new config, inline, until it is (request in p4a.json).
            DmList colors = Dm.list(pick(wirecutter_colors));
            object joined = colors.Join("");
            if (!Dm.eq(greyscale_colors, joined)) // If you want to disable greyscale stuff then give a blank string
                greyscale_colors = joined;
            Waiting.NoSleep(update_greyscale());
        }

        Dm.Deferred("code/game/objects/items/tools/engineering/wirecutters.dm:58", "Initialize: AddElement(/datum/element/falling_hazard, ...), the wirecutters falling on a head (falls, M5)");
        Waiting.NoSleep(AddElement(typeof(datum_element_cuffsnapping), snap_time_weak_handcuffs, snap_time_strong_handcuffs));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/objects/items/tools/engineering/wirecutters.dm:62  suicide_act: live-partial (M10).
}

// tg: code/game/objects/items/tools/engineering/wirecutters.dm:36  var/static/list/wirecutter_colors (rules 10.6)
public sealed partial class TgState
{
    internal DmList obj_item_wirecutters__wirecutter_colors = Dm.list(
        COLOR_TOOL_BLUE,
        COLOR_TOOL_RED,
        COLOR_TOOL_PINK,
        COLOR_TOOL_BROWN,
        COLOR_TOOL_GREEN,
        COLOR_TOOL_CYAN,
        COLOR_TOOL_YELLOW
    );
}
