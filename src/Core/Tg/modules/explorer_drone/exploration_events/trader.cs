// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/trader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the traders a drone meets, who take a thing from its cargo for one of theirs (some only with the translator), and
// the ID card artisan. The var blocks are generated (D111).

/// Trader events - If drone is loaded with X exchanges it for Y, might require translator tool.
// tg: code/modules/explorer_drone/exploration_events/trader.dm:2
public partial class datum_exploration_event_simple_trader : datum_exploration_event_simple
{
    // tg: code/modules/explorer_drone/exploration_events/trader.dm:13
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (islist(required_path))
            required_path = pick(required_path);
        if (islist(traded_path))
            traded_path = pick(traded_path);
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:20
    public override object get_discovery_message(obj_item_exodrone drone = null)
    {
        if (Dm.truthy(requires_translator) && !Dm.truthy(drone.has_tool(EXODRONE_TOOL_TRANSLATOR)))
            return I18n.T("You encountered [] but could not understand what they want without a translator.", name);
        Type want = required_path as Type;
        Type gives = traded_path as Type;
        return I18n.T("Encountered [] willing to trade [] for []", name, Dm.initial(gives, (obj o) => o.name), Dm.initial(want, (obj o) => o.name));
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:27
    public override object get_description(obj_item_exodrone drone = null)
    {
        if (Dm.truthy(requires_translator) && !Dm.truthy(drone.has_tool(EXODRONE_TOOL_TRANSLATOR)))
            return I18n.T("You encounter [] but cannot understand what they want without a translator.", name);
        Type want = required_path as Type;
        Type gives = traded_path as Type;
        return I18n.T("You encounter [] willing to trade [] for [] [].", name, Dm.initial(want, (obj o) => o.name), Dm.initial(gives, (obj o) => o.name), Dm.num(amount) > 1 ? I18n.T("[] times", amount) : "");
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:34
    public override object is_targetable()
    {
        return Dm.num(Dm.truthy(visited) && Dm.num(amount) > 0);
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:37
    public override object action_enabled(obj_item_exodrone drone = null)
    {
        obj trade_good = Dm.locate_type(required_path as Type, drone.contents) as obj;
        return Dm.num(Dm.num(amount) > 0 && Dm.truthy(trade_good) && (!Dm.truthy(requires_translator) || Dm.truthy(drone.has_tool(EXODRONE_TOOL_TRANSLATOR))));
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:41
    public override async DmTask fire(obj_item_exodrone drone = null)
    {
        if (!Dm.truthy(action_enabled(drone)))
        {
            end(drone);
            return;
        }
        amount--;
        await trade(drone);
        end(drone);
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:49
    public virtual async DmTask trade(obj_item_exodrone drone = null)
    {
        obj trade_good = Dm.locate_type(required_path as Type, drone.contents) as obj;
        obj loot = await Dm.new_async((Type)traded_path) as obj;
        drone.drone_log__proc(I18n.T("Traded [] for [].", trade_good, loot));
        qdel(trade_good);
        await drone.try_transfer(loot);
    }
}

// tg: code/modules/explorer_drone/exploration_events/trader.dm:105
public partial class datum_exploration_event_simple_trader_flame_card : datum_exploration_event_simple_trader
{
    // tg: code/modules/explorer_drone/exploration_events/trader.dm:116
    public override object get_discovery_message(obj_item_exodrone drone = null)
    {
        return I18n.T("Encountered [] willing to customise any ID card you bring them.", name);
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:119
    public override object get_description(obj_item_exodrone drone = null)
    {
        return I18n.T("You encounter a local craftsman willing to customise an ID card for you, free of charge.");
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:122
    public override async DmTask trade(obj_item_exodrone drone = null)
    {
        obj_item_card_id card = Dm.locate_type(required_path as Type, drone.contents) as obj_item_card_id;
        card.icon_state = (string)pick(possible_card_states ??= Dm.list("card_flames", "card_carp", "card_rainbow"));
        await card.update_icon(); //Refresh cached helper image
        drone.drone_log__proc(I18n.T("Let artisan work on [].", card.name));
    }

    // tg: code/modules/explorer_drone/exploration_events/trader.dm:114  var/static/list/possible_card_states
    public static ref DmList possible_card_states => ref TgState.Current.datum_exploration_event_simple_trader_flame_card__possible_card_states;
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/exploration_events/trader.dm:114  var/static/list/possible_card_states
    internal DmList datum_exploration_event_simple_trader_flame_card__possible_card_states;
}
