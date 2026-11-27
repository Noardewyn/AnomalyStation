// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/shell_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file. The var block is generated (D111).

/**
 * # Shell Item
 *
 * Printed out by protolathes. Screwdriver to complete the shell.
 */
// tg: code/modules/wiremod/shell/shell_items.dm:6
public partial class obj_item_shell : obj_item
{
    // tg: code/modules/wiremod/shell/shell_items.dm:14
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        // DEVIATION(DV570): the brain-computer interface's assembly finishes into the BCI, which is not ported yet; until it is,
        // finishing that assembly is refused with "not ported yet" (D132) instead of making an inert shell. The other assemblies
        // finish into their shells as in tg.
        if (shell_to_spawn != null && InertSeam.IsInertType(shell_to_spawn))
        {
            balloon_alert(user, I18n.T("not ported yet"));
            return TRUE;
        }
        user.visible_message(span_notice(I18n.T("[] begins finishing [].", user, this)), span_notice(I18n.T("You begin finishing [].", this)));
        tool.play_tool_sound(this);
        if (!Dm.truthy(await do_after(user, screw_delay, this)))
            return null;
        user.visible_message(span_notice(I18n.T("[] finishes [].", user, this)), span_notice(I18n.T("You finish [].", this)));

        atom drop_loc = drop_location() as atom;

        qdel(this);
        if (drop_loc != null)
            Dm.@new(shell_to_spawn, drop_loc);

        return TRUE;
    }
}
