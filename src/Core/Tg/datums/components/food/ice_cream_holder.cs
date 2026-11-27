// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food/ice_cream_holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the ice cream holder (cones, waffles, sundaes) and every ice cream flavour, with
// GLOB.ice_cream_flavours. The var blocks are generated (D111). Selling ice cream to a restaurant's robot customers is translated as
// tg has it (the sale pays but reports no transaction: the cone is not eaten and no customer is counted served, as in tg).

/// Ice Cream Holder: Allows the edible parent object to be used as an ice cream cone (or cup... in a next future).
// tg: code/datums/components/food/ice_cream_holder.dm:5
public partial class datum_component_ice_cream_holder : datum_component
{
    // tg: code/datums/components/food/ice_cream_holder.dm:1  #define SWEETENER_PER_SCOOP 10 (file-local, #undef at :368)
    internal const float SWEETENER_PER_SCOOP = 10;
    // tg: code/datums/components/food/ice_cream_holder.dm:2  #define EXTRA_MAX_VOLUME_PER_SCOOP 20 (file-local, #undef at :369)
    internal const float EXTRA_MAX_VOLUME_PER_SCOOP = 20;

    // tg: code/datums/components/food/ice_cream_holder.dm:36
    [DmParams("max_scoops", "change_name", "filled_name", "change_desc", "x_offset", "y_offset", "sweetener", "prefill_flavours")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object max_scoops = Dm.arg(__extra, 0) ?? DEFAULT_MAX_ICE_CREAM_SCOOPS;
        object change_name = Dm.arg(__extra, 1) ?? TRUE;
        object filled_name = Dm.arg(__extra, 2);
        object change_desc = Dm.arg(__extra, 3) ?? FALSE;
        object x_offset = Dm.arg(__extra, 4) ?? 0;
        object y_offset = Dm.arg(__extra, 5) ?? 0;
        Type sweetener = Dm.arg(__extra, 6) as Type ?? typeof(datum_reagent_consumable_sugar);
        DmList prefill_flavours = Dm.arg(__extra, 7) as DmList;
        if (!IS_EDIBLE(parent)) /// There is no easy way to add servings to those non-item edibles, but I won't stop you.
            return COMPONENT_INCOMPATIBLE;

        atom owner = (atom)parent;

        this.max_scoops = Dm.num(max_scoops);
        this.change_name = Dm.num(change_name);
        this.filled_name = filled_name;
        this.change_desc = Dm.num(change_desc);
        this.x_offset = Dm.num(x_offset);
        this.y_offset = Dm.num(y_offset);
        this.sweetener = sweetener;

        RegisterSignal(owner, COMSIG_ITEM_ATTACK_ATOM, nameof(on_item_attack_obj));
        RegisterSignal(owner, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        if (Dm.truthy(change_name))
            RegisterSignal(owner, COMSIG_ATOM_UPDATE_NAME, nameof(on_update_name));
        if (!Dm.truthy(change_desc))
            RegisterSignal(owner, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine_more));
        else
            RegisterSignal(owner, COMSIG_ATOM_UPDATE_DESC, nameof(on_update_desc));

        RegisterSignal(owner, COMSIG_ITEM_IS_CORRECT_CUSTOM_ORDER, nameof(check_food_order));

        RegisterSignal(owner, COMSIG_ITEM_SOLD_TO_CUSTOMER, nameof(sell_ice_cream));

        if (Dm.truthy(prefill_flavours))
        {
            foreach (object entry in Dm.each(prefill_flavours))
            {
                DmList flavour_args = Dm.add(Dm.list(this), prefill_flavours[entry]) as DmList;
                datum_ice_cream_flavour flavour = GLOB.ice_cream_flavours[entry] as datum_ice_cream_flavour;
                if (flavour != null)
                    await Dm.Call(flavour, nameof(datum_ice_cream_flavour.add_flavour), Dm.arglist(flavour_args));
            }
        }
        return null;
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:79
    [SignalHandler]
    public virtual void on_update_name(atom source = null, object updates = null)
    {
        if (HAS_TRAIT(source, TRAIT_WAS_RENAMED))
            return;
        float scoops_len = Dm.length(scoops);
        if (!Dm.truthy(scoops_len))
            source.name = Dm.initial(source, (atom a) => a.name);
        else
        {
            object name_to_use = (filled_name is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.initial(source, (atom a) => a.name));
            DmList unique_list = GlobalProcs.unique_list(scoops) as DmList;
            if (scoops_len > 1 && Dm.length(unique_list) == 1) // multiple flavours, and all of the same type
                source.name = I18n.T("[] [] []", make_tuple(scoops_len), scoops[1], name_to_use); // "double vanilla" sounds cooler than just "vanilla"
            else
                source.name = I18n.T("[] []", english_list(unique_list), name_to_use);
        }
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:94
    [SignalHandler]
    public virtual void on_update_desc(atom source = null, object updates = null)
    {
        if (HAS_TRAIT(source, TRAIT_WAS_RENAMED))
            return;
        float scoops_len = Dm.length(scoops);
        if (!Dm.truthy(scoops_len))
            source.desc = Dm.initial(source, (atom a) => a.desc);
        else if (scoops_len == 1 || Dm.length(unique_list(scoops)) == 1) /// Only one flavour.
        {
            object key = scoops[1];
            datum_ice_cream_flavour flavour = GLOB.ice_cream_flavours[LAZYACCESS(special_scoops, key) is var or__1 && Dm.truthy(or__1) ? or__1 : key] as datum_ice_cream_flavour;
            if (!Dm.truthy(flavour?.desc)) //I scream.
                source.desc = Dm.initial(source, (atom a) => a.desc);
            else
                source.desc = replacetext(replacetext(I18n.T("[] []", flavour.desc_prefix, flavour.desc), "$CONE_NAME", Dm.initial(source, (atom a) => a.name)), "$CUSTOM_NAME", key);
        }
        else /// Many flavours.
            source.desc = I18n.T("A delicious [] filled with scoops of [] ice cream. That's as many as [] scoops!", Dm.initial(source, (atom a) => a.name), english_list(scoops), scoops_len);
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:111
    [SignalHandler]
    public virtual void on_examine(atom source = null, mob mob = null, DmList examine_list = null)
    {
        if (Dm.length(scoops) < max_scoops)
            Dm.append(ref examine_list, span_tinynoticeital(I18n.T("you could use a ice cream vat to fill it with yummy ice cream...")));
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:116
    [SignalHandler]
    public virtual void on_examine_more(atom source = null, mob mob = null, DmList examine_list = null)
    {
        float scoops_len = Dm.length(scoops);
        if (!Dm.truthy(scoops_len))
            return;
        if (scoops_len == 1 || Dm.length(unique_list(scoops)) == 1) /// Only one flavour.
        {
            object key = scoops[1];
            datum_ice_cream_flavour flavour = GLOB.ice_cream_flavours[LAZYACCESS(special_scoops, key) is var or__1 && Dm.truthy(or__1) ? or__1 : key] as datum_ice_cream_flavour;
            if (Dm.truthy(flavour?.desc)) //I scream.
                Dm.append(ref examine_list, I18n.T("[] filled with scoops of [] ice cream.", source.p_Theyre(), flavour != null ? flavour.name : I18n.T("broken, unhappy")));
            else
                Dm.append(ref examine_list, replacetext(replacetext(I18n.T("[] []", source.p_Theyre(), flavour.desc), "$CONE_NAME", Dm.initial(source, (atom a) => a.name)), "$CUSTOM_NAME", key));
        }
        else /// Many flavours.
            Dm.append(ref examine_list, I18n.T("[] filled with scoops of [] ice cream. That's as many as [] scoops!", source.p_Theyre(), english_list(scoops), scoops_len));
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:131
    [SignalHandler]
    public virtual void on_update_overlays(atom source = null, DmList new_overlays = null)
    {
        if (!Dm.truthy(scoops))
            return;
        float added_offset = 0;
        for (float i = 1, i__end = Dm.length(scoop_overlays); i <= i__end; i++)
        {
            object overlay__it = scoop_overlays[i];
            if (istext(overlay__it))
                overlay__it = image(Dm.resource("icons/obj/service/kitchen.dmi"), overlay__it);
            image overlay = (image)overlay__it;
            overlay.pixel_w = x_offset;
            overlay.pixel_z = y_offset + added_offset;
            Dm.append(ref new_overlays, overlay);
            added_offset += ICE_CREAM_SCOOP_OFFSET;
        }
    }

    /// Attack the ice cream vat to get some ice cream. This will change as new ways of getting ice cream are added.
    // tg: code/datums/components/food/ice_cream_holder.dm:146
    [SignalHandler]
    public virtual object on_item_attack_obj(obj_item source = null, obj target = null, mob user = null)
    {
        if (target is not obj_machinery_icecream_vat)
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        if (Dm.length(scoops) >= max_scoops)
        {
            target.balloon_alert(user, I18n.T("too many scoops!"));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        obj_machinery_icecream_vat dispenser = (obj_machinery_icecream_vat)target;
        Waiting.NoSleep(dispenser.add_flavor_to_cone(this, user, source as obj_item_food_icecream));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:157
    [SignalHandler]
    public virtual object check_food_order(obj_item source = null, datum_custom_order our_order = null)
    {
        if (our_order is not datum_custom_order_icecream)
            return FALSE;
        datum_custom_order_icecream icecream_order = (datum_custom_order_icecream)our_order;
        if (parent.type != icecream_order.cone_type) //check that the cone type matches
            return FALSE;

        // We don't want to stop ice creams from being sold because of their order. we aren't that finnicky.
        DmList our_scoops = scoops.Copy();
        sortTim(our_scoops, cmp: "/proc/" + nameof(GlobalProcs.cmp_text_asc));

        //Make sure the flavors and number of scoops match.
        if (Dm.truthy(compare_list(our_scoops, icecream_order.wanted_flavors)))
            return COMPONENT_CORRECT_ORDER;
        return null;
    }

    // tg: code/datums/components/food/ice_cream_holder.dm:173
    [SignalHandler]
    public virtual void sell_ice_cream(obj_item source = null, mob_living_basic_robot_customer sold_to = null)
    {
        //the price of ice cream scales with the number of scoops. Yummy.
        float venue_price = Dm.length(scoops) * FOOD_PRICE_TRASH * 2;

        datum_venue venue_to_pay = (sold_to.ai_controller as datum_ai_controller)?.blackboard[BB_CUSTOMER_ATTENDING_VENUE] as datum_venue;

        Dm.@new<obj_item_holochip>(get_turf(source), venue_price);
        venue_to_pay.total_income += venue_price;
        playsound(get_turf(source), Dm.resource("sound/effects/cashregister.ogg"), 60, TRUE);
    }
}

/////ICE CREAM FLAVOUR DATUM STUFF

// tg: code/datums/components/food/ice_cream_holder.dm:188
public partial class datum_controller_global_vars
{
    // tg: code/datums/components/food/ice_cream_holder.dm:188  GLOBAL_LIST_INIT_TYPED(ice_cream_flavours, /datum/ice_cream_flavour, init_ice_cream_flavours())
    public DmList ice_cream_flavours;

    // tg: code/datums/components/food/ice_cream_holder.dm:188  (GLOBAL_MANAGED(ice_cream_flavours, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalice_cream_flavours()
    {
        ice_cream_flavours = init_ice_cream_flavours() as DmList;
        Dm.append(ref gvars_datum_init_order, "ice_cream_flavours");
    }
}

// tg: code/datums/components/food/ice_cream_holder.dm:190
public static partial class GlobalProcs
{
    // tg: code/datums/components/food/ice_cream_holder.dm:190
    public static object init_ice_cream_flavours()
    {
        DmList dot = Dm.list();
        foreach (Type flavour__type in Dm.each<Type>(subtypesof(typeof(datum_ice_cream_flavour))))
        {
            datum_ice_cream_flavour flavour = Dm.@new(flavour__type) as datum_ice_cream_flavour;
            dot[flavour.name] = flavour;
        }
        return dot;
    }
}

/*
 * The ice cream flavour datum. What makes these digital, frozen snacks so yummy.
 * They are singletons, so please bear with me if they feel a little tortous to use at time.
 */
// tg: code/datums/components/food/ice_cream_holder.dm:200
public partial class datum_ice_cream_flavour : datum
{
    // tg: code/datums/components/food/ice_cream_holder.dm:228
    public override async DmTask New(params object[] __extra)
    {
        if (Dm.truthy(ingredients))
            ingredients_text = Dm.text("Requires: []", reagent_paths_list_to_text(ingredients, ingredients_text));
    }

    /// Adds a new flavour to the ice cream cone.
    // tg: code/datums/components/food/ice_cream_holder.dm:233
    public virtual async DmTask<object> add_flavour(datum_component_ice_cream_holder target = null, datum_reagents custom_ingredients = null)
    {
        atom owner = (atom)target.parent;
        LAZYADD(ref target.scoops, name);
        if (!Dm.truthy(takes_custom_ingredients) && Dm.truthy(color))
        {
            image flavoring = image(Dm.resource("icons/obj/service/kitchen.dmi"), "icecream_custom");
            flavoring.color = color;
            LAZYADD(ref target.scoop_overlays, flavoring);
        }

        datum_reagents owner_reagents = (datum_reagents)owner.reagents;
        owner_reagents.maximum_volume += datum_component_ice_cream_holder.EXTRA_MAX_VOLUME_PER_SCOOP;
        if (reagent_type != null)
            await owner_reagents.add_reagent(reagent_type, reagent_amount, reagtemp: T0C);
        // Add some sugar/sweetener to make it a more substantial snack.
        await owner_reagents.add_reagent(target.sweetener, datum_component_ice_cream_holder.SWEETENER_PER_SCOOP, reagtemp: T0C);

        float update_flags = UPDATE_ICON;
        if (Dm.truthy(target.change_name))
            Dm.combine(ref update_flags, UPDATE_NAME);
        if (Dm.truthy(target.change_desc))
            Dm.combine(ref update_flags, UPDATE_DESC);
        owner.update_appearance(update_flags);
        return TRUE;
    }
}

// tg: code/datums/components/food/ice_cream_holder.dm:338
public partial class datum_ice_cream_flavour_custom : datum_ice_cream_flavour
{
    // tg: code/datums/components/food/ice_cream_holder.dm:352
    public override async DmTask<object> add_flavour(datum_component_ice_cream_holder target = null, datum_reagents custom_ingredients = null)
    {
        if (custom_ingredients == null || custom_ingredients.total_volume < 4) //consumable reagents have stronger taste so higher volume are required to allow non-food flavourings to break through better.
            return await ((datum_ice_cream_flavour)GLOB.ice_cream_flavours[ICE_CREAM_BLAND]).add_flavour(target); //Bland, sugary ice and milk.
        image flavoring = image(Dm.resource("icons/obj/service/kitchen.dmi"), "icecream_custom");
        datum_reagent master = custom_ingredients.get_master_reagent() as datum_reagent;
        flavoring.color = master.color;
        LAZYADD(ref target.scoop_overlays, flavoring);
        object dot = await base.add_flavour(target, custom_ingredients); // Make some space for reagents before attempting to transfer some to the target.
        await custom_ingredients.trans_to(target.parent, 4);
        return dot;
    }
}
