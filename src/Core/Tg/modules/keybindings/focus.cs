// Translated from tgstation (snapshot 1d99bd7f): code/modules/keybindings/focus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

// tg: code/modules/keybindings/focus.dm:1
public partial class mob : atom_movable
{
    // tg: code/modules/keybindings/focus.dm:1
    public virtual async DmTask set_focus(datum new_focus = null)
    {
        if (focus == new_focus)
            return;
        focus = new_focus;
        await reset_perspective(focus as atom); //Maybe this should be done manually? You figure it out, reader
    }
}
