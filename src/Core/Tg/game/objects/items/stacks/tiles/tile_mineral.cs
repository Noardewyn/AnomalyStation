// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tiles/tile_mineral.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SK1 (D333): the mineral tiles' welding back into a sheet (four tiles a sheet); the 48 mineral tile types are var-only and
// turn live with it. The var blocks are generated (D111).

// tg: code/game/objects/items/stacks/tiles/tile_mineral.dm:1
public partial class obj_item_stack_tile_mineral : obj_item_stack_tile
{
    // tg: code/game/objects/items/stacks/tiles/tile_mineral.dm:5
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(get_amount()) < 4)
        {
            to_chat(user, span_warning(I18n.T("You need at least four tiles to do this!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(mineralType))
        {
            to_chat(user, span_warning(I18n.T("You can not reform this!")));
            stack_trace(Dm.text("A mineral tile of type [] doesn't have its mineralType set.", type), "code/game/objects/items/stacks/tiles/tile_mineral.dm", 12);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 40)))
            return ITEM_INTERACT_BLOCKING;
        Type sheet_type = text2path(Dm.text("/obj/item/stack/sheet/mineral/[]", mineralType)) as Type;
        var new_item = Dm.@new(sheet_type, user.drop_location()) as obj_item_stack_sheet_mineral;
        user.visible_message(span_notice(I18n.T("[] shaped [] into [] with [].", user, this, new_item, tool)),
            span_notice(I18n.T("You shaped [] into [] with [].", this, new_item, tool)),
            span_hear(I18n.T("You hear welding.")));
        object holding = user.is_holding(this);
        await use(4);
        if (Dm.truthy(holding) && QDELETED(this))
            await user.put_in_hands(new_item);
        return ITEM_INTERACT_SUCCESS;
    }
}
