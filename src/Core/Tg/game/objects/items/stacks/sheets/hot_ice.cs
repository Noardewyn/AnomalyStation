// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/hot_ice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole: the hot ice's suicide. The var block is generated (D111).

// tg: code/game/objects/items/stacks/sheets/hot_ice.dm:1
public partial class obj_item_stack_sheet_hot_ice : obj_item_stack_sheet
{
    // tg: code/game/objects/items/stacks/sheets/hot_ice.dm:11
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins licking \\the []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return FIRELOSS;//dont you kids know that stuff is toxic?
    }
}
