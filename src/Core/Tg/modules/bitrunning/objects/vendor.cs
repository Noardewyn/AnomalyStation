// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/objects/vendor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the bitrunners' order console (its ProduceConsole window, paid in bitrunning points; the
// order arrives on the cargo shuttle or at once) and the order's supply pack. The var blocks are generated (D111).

// tg: code/modules/bitrunning/objects/vendor.dm:1
public partial class obj_machinery_computer_order_console_bitrunning : obj_machinery_computer_order_console
{
    // tg: code/modules/bitrunning/objects/vendor.dm:27
    public override object subtract_points(float final_cost = 0, obj_item_card_id card = null)
    {
        if (final_cost <= card.registered_account.bitrunning_points)
        {
            card.registered_account.bitrunning_points -= final_cost;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/bitrunning/objects/vendor.dm:33
    public override async DmTask order_groceries(mob_living purchaser = null, obj_item_card_id card = null, DmList groceries = null)
    {
        DmList things_to_order = Dm.list();
        foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(groceries))
            things_to_order[item.purchase_path] = groceries[item];

        var pack = await Dm.new_async<datum_supply_pack_bitrunning>(
            Dm.named("purchaser", purchaser),
            Dm.named("cost", get_total_cost()),
            Dm.named("contains", things_to_order));

        var new_order = await Dm.new_async<datum_supply_order>(
            Dm.named("pack", pack),
            Dm.named("orderer", purchaser),
            Dm.named("orderer_rank", "Bitrunning Vendor"),
            Dm.named("orderer_ckey", purchaser.ckey),
            Dm.named("reason", ""),
            Dm.named("paying_account", card.registered_account),
            Dm.named("department_destination", null),
            Dm.named("coupon", null),
            Dm.named("charge_on_purchase", FALSE),
            Dm.named("manifest_can_fail", FALSE),
            Dm.named("cost_type", credit_type),
            Dm.named("can_be_cancelled", FALSE));
        await say(I18n.T("Thank you for your purchase! It will arrive on the next cargo shuttle!"));
        await aas_config_announce(typeof(datum_aas_config_entry_order_console), Dm.list(), this, Dm.list(radio_channel), capitalize(blackbox_key));
        Dm.append(ref SSshuttle.shopping_list, new_order);
    }

    // tg: code/modules/bitrunning/objects/vendor.dm:62
    public override float retrieve_points(obj_item_card_id id_card = null) => round(id_card.registered_account.bitrunning_points);

    // tg: code/modules/bitrunning/objects/vendor.dm:65
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (!Dm.truthy(dot))
            flick("vendor_off", this);
        return dot;
    }

    // tg: code/modules/bitrunning/objects/vendor.dm:70
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(powered__proc()) ? null : "_off");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/bitrunning/objects/vendor.dm:75
public partial class datum_supply_pack_bitrunning : datum_supply_pack
{
    // tg: code/modules/bitrunning/objects/vendor.dm:81
    [DmParams("purchaser", "cost", "contains")]
    public override async DmTask New(params object[] __extra)
    {
        object purchaser = Dm.arg(__extra, 0);
        object cost = Dm.arg(__extra, 1);
        DmList contains = Dm.arg(__extra, 2) as DmList;
        await base.New(__extra);
        name = Dm.text("[]'s Bitrunning Order", purchaser);
        this.cost = Dm.num(cost);
        this.contains = contains;
    }
}
