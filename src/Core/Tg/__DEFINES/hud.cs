// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the action palette's function-like macros (M4c slice 3: the action buttons); the constants of this file
// are generated (Defines), and its other function-like macros come with the code that uses them.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/hud.dm:253  #define ui_action_palette_offset(north_offset) ("WEST+0:23,NORTH-[1+north_offset]:5")
    public static string ui_action_palette_offset(float north_offset) => Dm.text("WEST+0:23,NORTH-[]:5", 1 + north_offset);

    // tg: code/__DEFINES/hud.dm:256  #define ui_palette_scroll_offset(north_offset) ("WEST+1:8,NORTH-[6+north_offset]:28")
    public static string ui_palette_scroll_offset(float north_offset) => Dm.text("WEST+1:8,NORTH-[]:28", 6 + north_offset);
}
