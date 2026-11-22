// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/sheets/light.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the file whole: the wired glass tile, finished into light tiles with an iron sheet, cut back into cable and
// glass. The var block is generated (D111).

// tg: code/game/objects/items/stacks/sheets/light.dm:1
public partial class obj_item_stack_light_w : obj_item_stack
{
    // tg: code/game/objects/items/stacks/sheets/light.dm:17
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_silicon), 20), Dm.kv(typeof(datum_reagent_copper), 5));
    }

    // tg: code/game/objects/items/stacks/sheets/light.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_warning(I18n.T("\\The [] looks unfinished, add <b>iron</b> to complete it.", this)));
        return dot;
    }

    // tg: code/game/objects/items/stacks/sheets/light.dm:24
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_sheet_iron steel)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.truthy(await steel.use(1)))
        {
            to_chat(user, span_warning(I18n.T("You need one iron sheet to finish the light tile!")));
            return ITEM_INTERACT_BLOCKING;
        }
        var finished_tiles = Dm.@new<obj_item_stack_tile_light>(user.drop_location());
        to_chat(user, span_notice(I18n.T("You make a light tile.")));
        if (!QDELETED(finished_tiles))
            finished_tiles.add_fingerprint(user);
        await use(1);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/stacks/sheets/light.dm:39
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item I = null)
    {
        object dot = await base.wirecutter_act(user, I);
        object Tsec = user.drop_location();
        var CC = Dm.@new<obj_item_stack_cable_coil>(Tsec, 5);
        if (!QDELETED(CC))
            CC.add_fingerprint(user);
        var G = Dm.@new<obj_item_stack_sheet_glass>(Tsec);
        if (!QDELETED(G))
            G.add_fingerprint(user);
        await use(1);
        return dot;
    }
}
