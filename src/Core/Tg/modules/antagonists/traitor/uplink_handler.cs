// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/uplink_handler.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file. The var block is generated.

/**
 * Uplink Handler
 *
 * The uplink handler, used to handle a traitor's TC and the uplink UI.
**/
// tg: code/modules/antagonists/traitor/uplink_handler.dm:6
public partial class datum_uplink_handler : datum
{
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:37
    public override object Destroy(float? force = null)
    {
        can_replace_objectives = null;
        replace_objectives = null;
        return base.Destroy(force);
    }

    /// Called whenever an update occurs on this uplink handler. Used for UIs
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:43
    public virtual void on_update()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_UPLINK_HANDLER_ON_UPDATE]))
            _SendSignal(COMSIG_UPLINK_HANDLER_ON_UPDATE, Dm.list(this));
    }

    /// Checks if there are enough joined players to purchase an item
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:48
    public virtual object not_enough_population(datum_uplink_item to_purchase = null)
    {
        return Dm.num(Dm.num(length(GLOB.joined_player_list)) < to_purchase.population_minimum);
    }

    /// Checks for uplink flags as well as items restricted to roles and species
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:52
    public virtual object check_if_restricted(datum_uplink_item to_purchase = null)
    {
        // DEVIATION(DV343): an entry whose item, procs or system are not live cannot be bought, nor come from the surplus crate or the
        // random item (GlobalProcs.uplink_item_hidden)
        if (uplink_item_hidden(to_purchase))
            return FALSE;
        if (!Dm.truthy(to_purchase.can_be_bought(this)))
            return FALSE;
        if (Dm.@in(to_purchase, extra_purchasable))
            return TRUE;
        if (!Dm.truthy(Dm.bitand(to_purchase.purchasable_from, uplink_flag)))
            return FALSE;
        if (Dm.truthy(length(to_purchase.restricted_roles)) && !Dm.@in(assigned_role, to_purchase.restricted_roles))
            return FALSE;
        if (Dm.truthy(length(to_purchase.restricted_species)) && !Dm.@in(assigned_species, to_purchase.restricted_species))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/antagonists/traitor/uplink_handler.dm:65
    public virtual object can_purchase_item(mob user = null, datum_uplink_item to_purchase = null)
    {
        if (Dm.truthy(debug_mode))
            return TRUE;

        if (Dm.truthy(shop_locked))
            return FALSE;

        if (Dm.truthy(to_purchase.lock_other_purchases))
        {
            // Can't purchase an uplink item that locks other purchases if you've already purchased something
            if (Dm.num(length(purchase_log.purchase_log)) > 0)
                return FALSE;
        }

        if (!Dm.truthy(check_if_restricted(to_purchase)))
            return FALSE;

        if (Dm.truthy(not_enough_population(to_purchase)))
            return FALSE;

        if (telecrystals < to_purchase.cost)
            return FALSE;

        object current_stock = item_stock[to_purchase.stock_key];
        float stock = current_stock != null ? Dm.num(current_stock) : INFINITY;
        if (stock <= 0)
            return FALSE;

        return TRUE;
    }

    // tg: code/modules/antagonists/traitor/uplink_handler.dm:93
    public virtual async DmTask<object> purchase_item(mob user = null, datum_uplink_item to_purchase = null, atom_movable source = null)
    {
        if (!Dm.truthy(can_purchase_item(user, to_purchase)))
            return null;

        if (to_purchase.limited_stock != -1 && !Dm.@in(to_purchase.stock_key, item_stock))
            item_stock[to_purchase.stock_key] = to_purchase.limited_stock;

        telecrystals -= to_purchase.cost;
        await to_purchase.purchase(user, this, source);

        if (Dm.@in(to_purchase.stock_key, item_stock))
            item_stock[to_purchase.stock_key] = Dm.num(item_stock[to_purchase.stock_key]) - 1;

        SSblackbox.record_feedback("nested tally", "traitor_uplink_items_bought", 1, Dm.list(Dm.text("[]", Dm.initial(to_purchase, (datum_uplink_item p) => p.name)), Dm.text("[]", to_purchase.cost)));
        on_update();
        return TRUE;
    }

    // tg: code/modules/antagonists/traitor/uplink_handler.dm:110
    public virtual async DmTask<object> purchase_raw_tc(mob user = null, object amount = null, atom_movable source = null)
    {
        if (Dm.truthy(shop_locked))
            return FALSE;
        if (telecrystals < Dm.num(amount))
            return FALSE;

        telecrystals -= Dm.num(amount);
        obj_item_stack_telecrystal tcs = await Dm.new_async<obj_item_stack_telecrystal>(get_turf(user), amount);
        await user.put_in_hands(tcs);

        log_uplink(Dm.text("[] purchased [] raw telecrystals from []'s uplink", key_name(user), amount, source));
        on_update();
        return TRUE;
    }

    ///Helper to add telecrystals to the uplink handler, calling set_telecrystals.
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:124
    public virtual void add_telecrystals(object amount = null)
    {
        set_telecrystals(telecrystals + Dm.num(amount));
    }

    ///Sets how many telecrystals the uplink handler has, then updates the UI for any players watching.
    // tg: code/modules/antagonists/traitor/uplink_handler.dm:128
    public virtual void set_telecrystals(object amount = null)
    {
        telecrystals = Dm.num(amount);
        on_update();
    }
}
