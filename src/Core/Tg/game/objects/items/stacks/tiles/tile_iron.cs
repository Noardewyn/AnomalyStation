// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/stacks/tiles/tile_iron.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 2 (materials and stacks): the whole file (the iron floor tile's procs; its many variants are var-only). The var
// blocks are generated (D111). The welder screentips are a deferred leaf (M9).

// tg: code/game/objects/items/stacks/tiles/tile_iron.dm:1
[DmPath("/obj/item/stack/tile/iron")]
public partial class obj_item_stack_tile_iron : obj_item_stack_tile
{
    // tg: code/game/objects/items/stacks/tiles/tile_iron.dm:103
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // var/static/list/tool_behaviors and AddElement(/datum/element/contextual_screentip_tools, tool_behaviors) (:105-111)
        Dm.Deferred("code/game/objects/items/stacks/tiles/tile_iron.dm:111", "Initialize: AddElement(/datum/element/contextual_screentip_tools, tool_behaviors): screentips (M10+)");
        return dot;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_iron.dm:113
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(get_amount()) < 4)
        {
            balloon_alert(user, I18n.T("not enough tiles!"));
            return null;
        }
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_sheet_iron new_item = Dm.@new<obj_item_stack_sheet_iron>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into sheets with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(4);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/objects/items/stacks/tiles/tile_iron.dm:129
    public override async DmTask<object> welder_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.num(get_amount()) < 2)
        {
            balloon_alert(user, I18n.T("not enough tiles!"));
            return null;
        }
        if (Dm.truthy(await tool.use_tool(this, user, delay: 0, volume: 40)))
        {
            obj_item_stack_rods new_item = Dm.@new<obj_item_stack_rods>(user.loc);
            user.visible_message(
                span_notice(I18n.T("[] shaped [] into rods with [].", user.name, this, tool)),
                blind_message: span_hear(I18n.T("You hear welding.")),
                vision_distance: COMBAT_MESSAGE_RANGE,
                ignored_mobs: user
            );
            await use(2);
            await user.put_in_inactive_hand(new_item);
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }
}
