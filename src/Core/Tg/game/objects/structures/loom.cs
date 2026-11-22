// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/loom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST4 (D333): the file whole. The wooden loom, wrenched down in half a second; the cotton, durathread and wool spun on it is the
// loomable element's (datums/elements/loomable.dm). The var block is generated (D111).

///This is a loom. It's usually made out of wood and used to weave fabric like durathread or cotton into their respective cloth types.
// tg: code/game/objects/structures/loom.dm:2
public partial class obj_structure_loom : obj_structure
{
    // tg: code/game/objects/structures/loom.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_item_typechecks), TgState.Current.obj_structure_loom__Initialize__hovering_item_typechecks));
        return dot;
    }

    // tg: code/game/objects/structures/loom.dm:22
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool, time: 0.5f * SECONDS);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/loom.dm:14  /obj/structure/loom/Initialize's var/static/list/hovering_item_typechecks
    internal DmList obj_structure_loom__Initialize__hovering_item_typechecks = Dm.list(
        Dm.kv(typeof(obj_item_stack_sheet_cotton), Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Weave"))));
}
