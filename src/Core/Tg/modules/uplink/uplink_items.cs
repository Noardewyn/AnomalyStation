// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the sales, the entry base, its categories, the special equipment); the var blocks are
// generated but stock_key (a text, a typepath or a weakref: hand-declared here). DV343: an entry whose own procs, item or system are not
// live is hidden from the uplink (uplink_item_hidden below); it shows by itself when its package makes it live.

public static partial class GlobalProcs
{
    // TODO: Work into reworked uplinks.
    /// Selects a set number of unique items from the uplink, and deducts a percentage discount from them
    // tg: code/modules/uplink/uplink_items.dm:3
    public static DmList create_uplink_sales(object num = null, Type category = null, object limited_stock = null, DmList sale_items = null)
    {
        DmList sales = Dm.list();
        DmList per_category = Dm.list();

        foreach (datum_uplink_item possible_sale in Dm.each<datum_uplink_item>(sale_items))
        {
            if (!Dm.@in(possible_sale.category, per_category))
                per_category[possible_sale.category] = Dm.list();
            Dm.append_at(per_category, possible_sale.category, possible_sale);
        }

        for (float i = 1, i__end = min(Dm.num(length(per_category)), Dm.num(num)); i <= i__end; i++)
        {
            object item_category = pick(per_category);
            datum_uplink_item taken_item = pick(per_category[item_category] as DmList) as datum_uplink_item;
            Dm.remove(per_category, item_category);
            datum_uplink_item uplink_item = Dm.@new(taken_item.GetType()) as datum_uplink_item;
            float discount = Dm.num(uplink_item.get_discount());
            DmList disclaimer = TgState.Current.create_uplink_sales__disclaimer ??= Dm.list(
                "Void where prohibited.",
                "Not recommended for children.",
                "Contains small parts.",
                "Check local laws for legality in region.",
                "Do not taunt.",
                "Not responsible for direct, indirect, incidental or consequential damages resulting from any defect, error or failure to perform.",
                "Keep away from fire or flames.",
                "Product is provided \"as is\" without any implied or expressed warranties.",
                "As seen on TV.",
                "For recreational use only.",
                "Use only as directed.",
                "16% sales tax will be charged for orders originating within Space Nebraska.");

            //We want to limit the purchase amount of some items without adjusting the pricing.
            if (uplink_item.limited_discount_stock > 0)
                uplink_item.limited_stock = uplink_item.limited_discount_stock;

            //if stock limited is passed into the function, we'll override everything
            if (Dm.num(limited_stock) > 0)
                uplink_item.limited_stock = Dm.num(limited_stock);

            if (uplink_item.cost >= 20) //Tough love for nuke ops
                discount *= 0.5f;
            uplink_item.stock_key = WEAKREF(uplink_item);
            uplink_item.category = category;
            uplink_item.cost = max(round(uplink_item.cost * (1 - discount)), 1);
            float initial_cost = Dm.initial(uplink_item, (datum_uplink_item p) => p.cost);
            uplink_item.name += Dm.text(" ([]% off!)", round((initial_cost - uplink_item.cost) / initial_cost * 100));
            uplink_item.desc += Dm.text(" Normally costs [] TC. All sales final. []", initial_cost, pick(disclaimer));
            uplink_item.item = taken_item.item;
            uplink_item.discounted = TRUE;

            Dm.append(sales, uplink_item);
        }
        return sales;
    }

    // DEVIATION(DV343): the campaign's uplink rule (docs/ai/specs/syndicate.md section 2). tg lists and sells every entry; the port hides
    // an entry whose item is inert (an untranslated type: it would materialize as a prop), whose own procs are not all translated
    // (the entry's type is inert: its purchase would run the base's spawning instead of its own), or which waits for a system that is
    // not ported though its item is live (UplinkWaits). The uplink's data (/datum/asset/json/uplink), the traitor's sales and the
    // handler's restriction check (purchases, the surplus crate and the random item) read it; SStraitor keeps every entry, as tg.
    public static bool uplink_item_hidden(datum_uplink_item item)
    {
        if (Attribute.IsDefined(item.GetType(), typeof(DmInertAttribute), inherit: false))
            return true;
        if (item.item != null && item.item != typeof(obj_item_loot_table_maker) && Attribute.IsDefined(item.item, typeof(DmInertAttribute), inherit: false))
            return true;
        return UplinkWaits.ContainsKey(item.GetType());
    }

    /// The entries whose item is live but whose purchase needs a system that is not ported yet (DV343), with the package that brings it.
    public static readonly System.Collections.Frozen.FrozenDictionary<Type, string> UplinkWaits = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(new Dictionary<Type, string>
    {
        // the kits of uplink_kits.dm are live (G3), but these hold items of later packages
        [typeof(datum_uplink_item_bundles_tc_bundle_a)] = "the silicons and later milestones: the syndi-kit's contents (the AI law modules, the portal gun)",
        [typeof(datum_uplink_item_bundles_tc_bundle_b)] = "the silicons and later milestones: the syndi-kit's contents (the AI law modules, the portal gun)",
        // the romerol kit's reagent and the zombie species are not ported (not-translated.md section 136)
        [typeof(datum_uplink_item_stealthy_weapons_romerol_kit)] = "the zombies: romerol and the zombie species",
    });
}

/**
 * Uplink Items
 *
 * Items that can be spawned from an uplink. Can be limited by gamemode.
**/
// tg: code/modules/uplink/uplink_items.dm:63
public partial class datum_uplink_item : datum
{
    /// If this value is changed on two items they will share stock, defaults to not sharing stock with any other item
    // tg: code/modules/uplink/uplink_items.dm:83  (rules 3.2, typed vars that hold wider values: a text key, the entry's typepath from
    // New, or a weakref of a sale's own entry)
    public object stock_key = UPLINK_SHARED_STOCK_UNIQUE;

    // tg: code/modules/uplink/uplink_items.dm:114
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!Dm.eq(stock_key, UPLINK_SHARED_STOCK_UNIQUE))
            return;
        stock_key = type;
    }

    /// Returns by how much percentage do we reduce the price of the selected item
    // tg: code/modules/uplink/uplink_items.dm:127
    public virtual object get_discount()
    {
        DmList discount_types = TgState.Current.datum_uplink_item__discount_types ??= Dm.list(
            Dm.kv(TRAITOR_DISCOUNT_SMALL, 4),
            Dm.kv(TRAITOR_DISCOUNT_AVERAGE, 2),
            Dm.kv(TRAITOR_DISCOUNT_BIG, 1));

        return get_discount_value(pick_weight(discount_types));
    }

    /// Receives a traitor discount type value, returns the amount by which we will reduce the price
    // tg: code/modules/uplink/uplink_items.dm:138
    public virtual object get_discount_value(object discount_type = null)
    {
        switch (discount_type)
        {
            case TRAITOR_DISCOUNT_BIG:
                return 0.75f;
            case TRAITOR_DISCOUNT_AVERAGE:
                return 0.5f;
            default:
                return 0.25f;
        }
    }

    /// Spawns an item and logs its purchase
    // tg: code/modules/uplink/uplink_items.dm:148
    public virtual async DmTask<object> purchase(mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        object spawned_item = await spawn_item(item, user, uplink_handler, source);
        log_uplink(Dm.text("[] purchased [] for [] telecrystals from []'s uplink", key_name(user), this, cost, source));
        user.playsound_local(get_turf(user), Dm.resource("sound/effects/kaching.ogg"), 100, FALSE, pressure_affected: FALSE, use_reverb: FALSE);
        if (uplink_handler.purchase_log != null)
            log_purchase(spawned_item as atom, uplink_handler);
        if (Dm.truthy(lock_other_purchases))
            uplink_handler.shop_locked = TRUE;
        return spawned_item;
    }

    // tg: code/modules/uplink/uplink_items.dm:158
    public virtual void log_purchase(atom spawned_item = null, datum_uplink_handler uplink_handler = null)
    {
        uplink_handler.purchase_log.log_purchase(spawned_item, this, cost);
    }

    /// Spawns an item in the world
    // tg: code/modules/uplink/uplink_items.dm:162
    public virtual async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        if (!Dm.truthy(spawn_path))
            return null;
        atom spawned_item;
        if (ispath(spawn_path))
            spawned_item = await Dm.new_async((Type)spawn_path, get_turf(user)) as atom;
        else
            spawned_item = spawn_path as atom;
        if (Dm.truthy(refundable))
            await spawned_item.AddElement(typeof(datum_element_uplink_reimburse), Dm.truthy(refund_amount) ? refund_amount : cost);

        if (Dm.truthy(Dm.bitand(uplink_item_flags, SYNDIE_TRIPS_CONTRABAND))) // Ignore things that shouldn't be detectable as contraband on the station.
        {
            ADD_TRAIT(spawned_item, TRAIT_CONTRABAND, INNATE_TRAIT);
            foreach (obj contained in Dm.each_cast<obj>(spawned_item.get_all_contents()))
                ADD_TRAIT(contained, TRAIT_CONTRABAND, INNATE_TRAIT);
        }
        mob_living_carbon_human human_user = user as mob_living_carbon_human;
        if (human_user != null && isitem(spawned_item) && Dm.truthy(await human_user.put_in_hands(spawned_item as obj_item)))
            to_chat(human_user, span_boldnotice(I18n.T("[] materializes into your hands!", spawned_item)));
        else
            to_chat(user, span_boldnotice(I18n.T("[] materializes onto the floor!", spawned_item)));
        if (Dm.truthy(uplink_handler._listen_lookup?[COMSIG_ON_UPLINK_PURCHASE]))
            uplink_handler._SendSignal(COMSIG_ON_UPLINK_PURCHASE, Dm.list(uplink_handler, spawned_item, user));
        return spawned_item;
    }

    /// Used to create the uplink's item for generic use, rather than use by a Syndie specifically
    /// Can be used to "de-restrict" some items, such as Nukie guns spawning with Syndicate pins
    // tg: code/modules/uplink/uplink_items.dm:188
    public virtual async DmTask<object> spawn_item_for_generic_use(mob user = null)
    {
        atom_movable created = await Dm.new_async(item, user.loc) as atom_movable;
        if (Dm.truthy(Dm.bitand(uplink_item_flags, SYNDIE_TRIPS_CONTRABAND))) // Things that shouldn't be detectable as contraband on the station.
        {
            ADD_TRAIT(created, TRAIT_CONTRABAND, INNATE_TRAIT);
            foreach (obj contained in Dm.each_cast<obj>(created.get_all_contents()))
                ADD_TRAIT(contained, TRAIT_CONTRABAND, INNATE_TRAIT);
        }

        if (isgun(created))
            await replace_pin(created as obj_item_gun);
        else if (created is obj_item_storage_toolbox_guncase)
        {
            foreach (obj_item_gun gun in Dm.each<obj_item_gun>(created))
                await replace_pin(gun);
        }

        if (isobj(created))
        {
            obj created_obj = created as obj;
            LAZYREMOVE(ref created_obj.req_access, ACCESS_SYNDICATE);
            LAZYREMOVE(ref created_obj.req_one_access, ACCESS_SYNDICATE);
        }

        return created;
    }

    /// Used by spawn_item_for_generic_use to replace the pin of a gun with a normal one
    // tg: code/modules/uplink/uplink_items.dm:209
    [DmPrivate]
    public virtual async DmTask replace_pin(obj_item_gun gun_reward = null)
    {
        if (gun_reward.pin is not obj_item_firing_pin_implant_pindicate)
            return;

        QDEL_NULL(ref gun_reward.pin);
        obj_item_firing_pin pin = Dm.@new<obj_item_firing_pin>();
        await pin.gun_insert(new_gun: gun_reward, starting: TRUE);
    }

    ///For special overrides if an item can be bought or not.
    // tg: code/modules/uplink/uplink_items.dm:220
    public virtual object can_be_bought(datum_uplink_handler source = null)
    {
        return TRUE;
    }
}

// tg: code/modules/uplink/uplink_items.dm:120
[DmPath("/datum/uplink_category")]
public partial class datum_uplink_category : datum
{
    /// Weight of the category. Used to determine the positioning in the uplink. High weight = appears first
    // tg: code/modules/uplink/uplink_items.dm:124
    public float weight = 0;
}

// tg: code/modules/uplink/uplink_items.dm:246
public partial class datum_uplink_item_special_equipment : datum_uplink_item
{
    // tg: code/modules/uplink/uplink_items.dm:253
    [DmParams("user", "U")]
    public override async DmTask<object> purchase(mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        await base.purchase(user, uplink_handler, source);
        if (Dm.truthy(user?.mind?.failed_special_equipment))
            Dm.remove(user.mind.failed_special_equipment, item);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/uplink/uplink_items.dm:18  create_uplink_sales' var/static/list/disclaimer
    internal DmList create_uplink_sales__disclaimer;

    // tg: code/modules/uplink/uplink_items.dm:129  get_discount's var/static/list/discount_types
    internal DmList datum_uplink_item__discount_types;
}
