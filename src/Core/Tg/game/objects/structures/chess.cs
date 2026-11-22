// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/chess.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The chess pieces (the holodeck's and the crafted ones): a wrench takes one apart into two iron
// sheets in half a second, a hologram's slips through. The var blocks are generated (D111).

// tg: code/game/objects/structures/chess.dm:1
public partial class obj_structure_chess : obj_structure
{
    // tg: code/game/objects/structures/chess.dm:11
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
        {
            balloon_alert(user, I18n.T("it goes right through!"));
            return TRUE;
        }
        to_chat(user, span_notice(I18n.T("You start to take apart the chess piece.")));
        if (!Dm.truthy(await do_after(user, 0.5f * SECONDS, target: this)))
            return TRUE;
        obj_item_stack_sheet_iron metal_sheets = Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
        if (!QDELETED(metal_sheets))
            metal_sheets.add_fingerprint(user);
        tool.play_tool_sound(this);
        qdel(this);
        return TRUE;
    }
}
