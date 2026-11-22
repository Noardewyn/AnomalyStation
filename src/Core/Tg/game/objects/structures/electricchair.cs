// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/electricchair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the ready-made electric chair, shocking whoever is buckled to it every five seconds through its
// electrified_buckle component, wrenched back into a plain chair. The var block is generated (D111).

// tg: code/game/objects/structures/electricchair.dm:1
public partial class obj_structure_chair_e_chair : obj_structure_chair
{
    // tg: code/game/objects/structures/electricchair.dm:9
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object export_to_component = mutable_appearance(Dm.resource("icons/obj/chairs.dmi"), "echair_over", OBJ_LAYER, this, appearance_flags: KEEP_APART);
        export_to_component = color_atom_overlay((mutable_appearance)export_to_component);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_electrified_buckle),
            Dm.named("overlays_to_add", Dm.list(export_to_component)),
            Dm.named("shock_flags", SHOCK_NOGLOVES)));
        return dot;
    }

    // tg: code/game/objects/structures/electricchair.dm:20
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_structure_chair non_electric_chair = Dm.@new<obj_structure_chair>(loc);
        tool.play_tool_sound(this);
        await non_electric_chair.setDir(dir);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
