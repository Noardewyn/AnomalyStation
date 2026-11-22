// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/orders/order_computer/cook_order.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the produce orders console (the chef's groceries on the cargo shuttle) and its cooking voucher.
// The var blocks are generated (D111).

// tg: code/game/machinery/computer/orders/order_computer/cook_order.dm:1
public partial class obj_machinery_computer_order_console_cook : obj_machinery_computer_order_console
{
    // tg: code/game/machinery/computer/orders/order_computer/cook_order.dm:15
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_cooking_voucher)
            return await base.item_interaction(user, tool, modifiers, __extra);
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(tool)))
            return ITEM_INTERACT_BLOCKING;

        playsound(this, Dm.resource("sound/machines/card_slide.ogg"), 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        balloon_alert(user, I18n.T("redeemed voucher"));
        free_uses = Dm.num(free_uses ?? 0f) + 10;
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/computer/orders/order_computer/cook_order.dm:27
    public override async DmTask order_groceries(mob_living purchaser = null, obj_item_card_id card = null, DmList groceries = null)
    {
        await say(I18n.T("Thank you for your purchase! It will arrive on the next cargo shuttle!"));
        await aas_config_announce(typeof(datum_aas_config_entry_order_console), Dm.list(), this, Dm.list(radio_channel), capitalize(blackbox_key));
        foreach (datum_orderable_item ordered_item in Dm.each<datum_orderable_item>(groceries.Copy()))
        {
            if (!Dm.@in(ordered_item.category_index, order_categories))
            {
                groceries.Remove(ordered_item);
                continue;
            }
            if (Dm.@in(ordered_item, SSshuttle.chef_groceries))
                SSshuttle.chef_groceries[ordered_item] = Dm.add(SSshuttle.chef_groceries[ordered_item], groceries[ordered_item]);
            else
                SSshuttle.chef_groceries[ordered_item] = groceries[ordered_item];
        }
    }
}
