// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/bundle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks). Syndicate G4c: the
// surplus crate's spawn_item (its supply pod).

// tg: code/modules/uplink/uplink_items/bundle.dm:14
public partial class datum_uplink_item_bundles_tc_random : datum_uplink_item_bundles_tc
{
    // tg: code/modules/uplink/uplink_items/bundle.dm:20
    [DmParams("user", "handler", "source")]
    public override async DmTask<object> purchase(mob user = null, datum_uplink_handler handler = null, atom_movable source = null)
    {
        DmList possible_items = Dm.list();
        foreach (datum_uplink_item uplink_item in Dm.each<datum_uplink_item>(SStraitor.uplink_items))
        {
            if (this == uplink_item || uplink_item.item == null)
                continue;
            if (!Dm.truthy(handler.can_purchase_item(user, uplink_item)))
                continue;
            Dm.append(possible_items, uplink_item);
        }

        if (Dm.truthy(possible_items.len))
        {
            datum_uplink_item uplink_item = pick(possible_items) as datum_uplink_item;
            log_uplink(Dm.text("[] purchased a random uplink item from []'s uplink with [] telecrystals remaining", key_name(user), handler.owner, handler.telecrystals));
            SSblackbox.record_feedback("tally", "traitor_random_uplink_items_gotten", 1, Dm.initial(uplink_item, (datum_uplink_item p) => p.name));
            await handler.purchase_item(user, uplink_item);
        }
        return null;
    }
}

// tg: code/modules/uplink/uplink_items/bundle.dm:36
public partial class datum_uplink_item_bundles_tc_telecrystal : datum_uplink_item_bundles_tc
{
    // Don't add telecrystals to the purchase_log since
    // it's just used to buy more items (including itself!)
    // tg: code/modules/uplink/uplink_items/bundle.dm:44
    public override void log_purchase(atom spawned_item = null, datum_uplink_handler uplink_handler = null)
    {
        return;
    }
}

// tg: code/modules/uplink/uplink_items/bundle.dm:72
public partial class datum_uplink_item_bundles_tc_surplus : datum_uplink_item_bundles_tc
{
    // tg: code/modules/uplink/uplink_items/bundle.dm:69  #define TC_VALUE_SURPLUS "%TC_VALUE_SURPLUS%" (file-local, #undef at :191)
    private const string TC_VALUE_SURPLUS = "%TC_VALUE_SURPLUS%";

    // tg: code/modules/uplink/uplink_items/bundle.dm:84
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desc = replacetext(desc, TC_VALUE_SURPLUS, crate_tc_value);
    }

    /// generates items that can go inside crates, edit this proc to change what items could go inside your specialized crate
    // tg: code/modules/uplink/uplink_items/bundle.dm:89
    public virtual object generate_possible_items(mob user = null, datum_uplink_handler handler = null)
    {
        DmList possible_items = Dm.list();
        foreach (datum_uplink_item uplink_item in Dm.each<datum_uplink_item>(SStraitor.uplink_items))
        {
            if (this == uplink_item || uplink_item.item == null)
                continue;
            if (!Dm.truthy(handler.check_if_restricted(uplink_item)))
                continue;
            if (!Dm.truthy(uplink_item.surplus))
                continue;
            Dm.append(possible_items, uplink_item);
        }
        return possible_items;
    }

    /// picks items from the list given to proc and generates a valid uplink item that is less or equal to the amount of TC it can spend
    // tg: code/modules/uplink/uplink_items/bundle.dm:102
    public virtual object pick_possible_item(DmList possible_items = null, object tc_budget = null)
    {
        datum_uplink_item uplink_item = pick(possible_items) as datum_uplink_item;
        if (prob(100 - uplink_item.surplus))
            return null;
        if (Dm.num(tc_budget) < uplink_item.cost)
            return null;
        return uplink_item;
    }

    /// fills the crate that will be given to the traitor, edit this to change the crate and how the item is filled
    // tg: code/modules/uplink/uplink_items/bundle.dm:111
    public virtual async DmTask fill_crate(obj_structure_closet_crate surplus_crate = null, DmList possible_items = null)
    {
        float tc_budget = crate_tc_value;
        while (Dm.truthy(tc_budget))
        {
            datum_uplink_item uplink_item = pick_possible_item(possible_items, tc_budget) as datum_uplink_item;
            if (uplink_item == null)
                continue;
            tc_budget -= uplink_item.cost;
            await Dm.new_async(uplink_item.item, surplus_crate);
        }
    }

    /// overwrites item spawning proc for surplus items to spawn an appropriate crate via a podspawn
    // tg: code/modules/uplink/uplink_items/bundle.dm:121
    public override async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        obj_structure_closet_crate surplus_crate = Dm.@new(crate_type) as obj_structure_closet_crate;
        if (surplus_crate == null)
            throw new DmRuntimeException("crate_type is not a crate"); // CRASH
        DmList possible_items = generate_possible_items(user, uplink_handler) as DmList;

        await fill_crate(surplus_crate, possible_items);

        await podspawn(Dm.list(
            Dm.kv("target", get_turf(user)),
            Dm.kv("style", typeof(datum_pod_style_syndicate)),
            Dm.kv("spawn", surplus_crate)));
        return surplus_crate; //For log icon
    }

    // tg: code/modules/uplink/uplink_items/bundle.dm:136
    public override void log_purchase(atom spawned_item = null, datum_uplink_handler uplink_handler = null)
    {
        base.log_purchase(spawned_item, uplink_handler);
        DmList entries = Dm.list();
        foreach (obj_item loot in Dm.each<obj_item>(spawned_item))
        {
            if (Dm.truthy(entries[loot.type]))
            {
                datum_uplink_purchase_entry existing_entry = entries[loot.type] as datum_uplink_purchase_entry;
                existing_entry.amount_purchased += 1;
                continue;
            }

            datum_uplink_purchase_entry custom_entry = Dm.@new<datum_uplink_purchase_entry>();
            custom_entry.set_item(loot);
            custom_entry.base_cost = 0;
            custom_entry.spent_cost = 0;
            custom_entry.amount_purchased = 1;
            custom_entry.name = Dm.text("Surplus Item: []", format_text(loot.name));
            custom_entry.desc = "Obtained via surplus crate.";
            entries[loot.type] = custom_entry;
        }

        foreach (object entry_type in Dm.each(entries))
            uplink_handler.purchase_log.log_purchase_custom(entries[entry_type] as datum_uplink_purchase_entry);
    }
}

// tg: code/modules/uplink/uplink_items/bundle.dm:158
public partial class datum_uplink_item_bundles_tc_surplus_united : datum_uplink_item_bundles_tc_surplus
{
    /// edited version of fill crate for super surplus to ensure it can only be unlocked with the syndicrate key
    // tg: code/modules/uplink/uplink_items/bundle.dm:170
    public override async DmTask fill_crate(obj_structure_closet_crate surplus_crate = null, DmList possible_items = null)
    {
        if (surplus_crate is not obj_structure_closet_crate_secure_syndicrate syndicrate)
            return;
        float tc_budget = crate_tc_value;
        while (Dm.truthy(tc_budget))
        {
            datum_uplink_item uplink_item = pick_possible_item(possible_items, tc_budget) as datum_uplink_item;
            if (uplink_item == null)
                continue;
            tc_budget -= uplink_item.cost;
            Dm.append(syndicrate.unlock_contents, uplink_item.item);
        }
    }
}
