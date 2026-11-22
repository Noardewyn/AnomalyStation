// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/orders/order_computer/mining_order.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mining point transfer card (gaps J6, D298), the mining equipment order console with its voucher redeemer
// (gaps P4d).

// tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:3
public partial class obj_machinery_computer_order_console_mining : obj_machinery_computer_order_console
{
    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_voucher_redeemer), typeof(obj_item_mining_voucher), typeof(datum_voucher_set_mining)));
        return dot;
    }

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:34
    public override object subtract_points(float final_cost = 0, obj_item_card_id card = null)
    {
        if (final_cost <= card.registered_account.mining_points)
        {
            card.registered_account.mining_points -= final_cost;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:40
    public override async DmTask order_groceries(mob_living purchaser = null, obj_item_card_id card = null, DmList groceries = null)
    {
        DmList things_to_order = Dm.list();
        foreach (datum_orderable_item item in Dm.each_cast<datum_orderable_item>(groceries))
        {
            object purchase_path = item.get_purchased_item(purchaser, card);
            things_to_order[purchase_path] = groceries[item];
        }

        var mining_pack = Dm.@new<datum_supply_pack_custom>(purchaser, get_total_cost(), things_to_order);
        var new_order = Dm.@new<datum_supply_order_disposable>(
            mining_pack,
            purchaser,
            "Mining Vendor",
            purchaser.ckey,
            "",
            card.registered_account,
            null,
            null,
            FALSE,
            FALSE,
            credit_type,
            FALSE);
        await say(I18n.T("Thank you for your purchase! It will arrive on the next cargo shuttle!"));
        await aas_config_announce(typeof(datum_aas_config_entry_order_console), Dm.list(), this, Dm.list(radio_channel), capitalize(blackbox_key));
        Dm.append(ref SSshuttle.shopping_list, new_order);
    }

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:69
    public override float retrieve_points(obj_item_card_id id_card = null) => round(id_card.registered_account.mining_points);

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:72
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (!Dm.truthy(dot))
            Dm.flick("mining-deny", this);
        return dot;
    }

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:77
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", Dm.initial(this, nameof(icon_state)), Dm.truthy(powered__proc()) ? null : "-off");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:96
public partial class obj_item_card_mining_point_card : obj_item_card
{
    private const string TO_USER_ID = "Transfer Card → ID";
    private const string TO_POINT_CARD = "ID → Transfer Card";

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:103
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("There's [] point\\s on the card.", points)));
        return dot;
    }

    // tg: code/game/machinery/computer/orders/order_computer/mining_order.dm:107
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isidcard(tool))
            return NONE;
        obj_item_card_id attacking_id = tool as obj_item_card_id;
        // A runtime in tg: an ID with no bank account (a blank card) has no balance to read.
        if (attacking_id.registered_account == null)
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("starting transfer"));
        object point_movement = await tgui_alert(user, I18n.T("To ID (from card) or to card (from ID)?"), I18n.T("Mining Points Transfer"), Dm.list(TO_USER_ID, TO_POINT_CARD));
        if (!Dm.truthy(point_movement))
            return ITEM_INTERACT_BLOCKING;
        object amount_answer = await tgui_input_number(user, I18n.T("How much do you want to transfer? ID Balance: [], Card Balance: []", attacking_id.registered_account.mining_points, points), I18n.T("Transfer Points"), Dm.named("min_value", 0), Dm.named("round_value", 1));
        if (!Dm.truthy(amount_answer))
            return ITEM_INTERACT_BLOCKING;
        float amount = Dm.num(amount_answer);
        switch (point_movement)
        {
            case TO_USER_ID:
                if (amount > points)
                    amount = points;
                attacking_id.registered_account.mining_points = Dm.num(attacking_id.registered_account.mining_points) + amount;
                points -= amount;
                to_chat(user, span_notice(I18n.T("You transfer [] mining points from [] to [].", amount, this, attacking_id)));
                return ITEM_INTERACT_SUCCESS;
            case TO_POINT_CARD:
                if (amount > Dm.num(attacking_id.registered_account.mining_points))
                    amount = Dm.num(attacking_id.registered_account.mining_points);
                attacking_id.registered_account.mining_points = Dm.num(attacking_id.registered_account.mining_points) - amount;
                points += amount;
                to_chat(user, span_notice(I18n.T("You transfer [] mining points from [] to [].", amount, attacking_id, this)));
                return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }
}
