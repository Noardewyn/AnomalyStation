// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/icecream_vat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the ice cream vat. The var block is generated (D111); the type-level statics (the
// flavours' radial pictures, the cone prototypes and the preinstalled reagents) are TgState fields (rules 10.6).

// tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:9
public partial class obj_machinery_icecream_vat : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:1-7  file-local #defines (#undef at :298-300)
    ///How many units of a reagent is needed to make a cone.
    private const float CONE_REAGENT_NEEDED = 1;
    ///The vat is set to dispense ice cream.
    private const string VAT_MODE_ICECREAM = "ice cream";
    ///The vat is set to dispense cones.
    private const string VAT_MODE_CONES = "cones";

    ///List of ice creams as icons used for the radial menu.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:29  var/static/list/ice_cream_icons (rules 10.6)
    public static ref DmList ice_cream_icons => ref TgState.Current.obj_machinery_icecream_vat__ice_cream_icons;
    /// List of prototypes of dispensable ice cream cones. path as key, instance as assoc.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:31  var/static/list/obj/item/food/icecream/cone_prototypes (rules 10.6)
    public static ref DmList cone_prototypes => ref TgState.Current.obj_machinery_icecream_vat__cone_prototypes;
    ///List of all reagenets the icecream vat will spawn with, if preinstall_reagents is TRUE.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:33  var/static/list/icecream_vat_reagents (rules 10.6)
    public static DmList icecream_vat_reagents => TgState.Current.obj_machinery_icecream_vat__icecream_vat_reagents ??= Dm.list(
        Dm.kv(typeof(datum_reagent_consumable_milk), 6),
        Dm.kv(typeof(datum_reagent_consumable_korta_milk), 6),
        Dm.kv(typeof(datum_reagent_consumable_flour), 6),
        Dm.kv(typeof(datum_reagent_consumable_korta_flour), 6),
        Dm.kv(typeof(datum_reagent_consumable_sugar), 6),
        Dm.kv(typeof(datum_reagent_consumable_ice), 6),
        Dm.kv(typeof(datum_reagent_consumable_coco), 6),
        Dm.kv(typeof(datum_reagent_consumable_vanilla), 6),
        Dm.kv(typeof(datum_reagent_consumable_berryjuice), 6),
        Dm.kv(typeof(datum_reagent_consumable_ethanol_singulo), 6),
        Dm.kv(typeof(datum_reagent_consumable_lemonjuice), 6),
        Dm.kv(typeof(datum_reagent_consumable_caramel), 6),
        Dm.kv(typeof(datum_reagent_consumable_banana), 6),
        Dm.kv(typeof(datum_reagent_consumable_orangejuice), 6),
        Dm.kv(typeof(datum_reagent_consumable_cream), 6),
        Dm.kv(typeof(datum_reagent_consumable_peachjuice), 6),
        Dm.kv(typeof(datum_reagent_consumable_cherryjelly), 6)
    );

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(cone_prototypes))
        {
            cone_prototypes = Dm.list();
            foreach (Type cone_path in Dm.each<Type>(typesof(typeof(obj_item_food_icecream))))
            {
                obj_item_food_icecream cone = Dm.@new(cone_path) as obj_item_food_icecream;
                if (Dm.truthy(cone.ingredients))
                    cone_prototypes[cone_path] = cone;
                else
                {
                    stack_trace(Dm.text("Ice cream cone [] (TYPE: []) has been found without ingredients, please make a bug report about this.", cone, cone_path), "code/modules/food_and_drinks/machinery/icecream_vat.dm", 66);
                    qdel(cone);
                }
            }
        }
        if (!Dm.truthy(ice_cream_icons))
        {
            ice_cream_icons = Dm.list();
            foreach (object flavor in Dm.each(GLOB.ice_cream_flavours))
            {
                datum_ice_cream_flavour flavor_datum = GLOB.ice_cream_flavours[flavor] as datum_ice_cream_flavour;
                if (Dm.truthy(flavor_datum.hidden))
                    continue;
                ice_cream_icons[flavor] = make_ice_cream_color(flavor_datum);
            }
        }

        RegisterSignal(this, COMSIG_ATOM_REAGENT_EXAMINE, nameof(allow_reagent_scan));

        object ice_cream_day = check_holidays(ICE_CREAM_DAY); //ice cream vats are more "robust" on this holiday

        create_reagents(Dm.truthy(ice_cream_day) ? 400 : 300, NO_REACT | TRANSPARENT);
        ((datum_reagents)reagents).chem_temp = T0C; //So ice doesn't melt
        register_context();

        if (Dm.truthy(preinstall_reagents))
        {
            foreach (object reagent in Dm.each(icecream_vat_reagents))
                Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent, Dm.num(icecream_vat_reagents[reagent]) * (Dm.truthy(ice_cream_day) ? 2.5f : 1), reagtemp: T0C));
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:88
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == custom_ice_cream_beaker)
            custom_ice_cream_beaker = null;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:93
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item != null)
        {
            if (is_reagent_container(held_item))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Insert beaker";
                context[SCREENTIP_CONTEXT_RMB] = "Transfer beaker reagents";
            }
            else if (held_item is obj_item_food_icecream)
                context[SCREENTIP_CONTEXT_LMB] = Dm.text("Take scoop of [] ice cream", selected_flavour);
            else if (held_item is obj_item_kitchen_spoon || held_item is obj_item_kitchen_spoon_soup_ladle)
                context[SCREENTIP_CONTEXT_RMB] = "Spill reagent";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        string sw__1 = vat_mode;
        if (sw__1 == VAT_MODE_ICECREAM)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Select flavor";
            context[SCREENTIP_CONTEXT_RMB] = "Change mode to cones";
        }
        else if (sw__1 == VAT_MODE_CONES)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Make cone";
            context[SCREENTIP_CONTEXT_RMB] = "Change mode to flavors";
        }
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:113
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("You can use a [] or [] to spill reagents.", EXAMINE_HINT(I18n.T("spoon")), EXAMINE_HINT(I18n.T("soup ladle"))));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:117
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_kitchen_spoon || tool is obj_item_kitchen_spoon_soup_ladle)
        {
            await spill_reagents(user);
            return ITEM_INTERACT_SUCCESS;
        }

        obj_item_reagent_containers beaker = tool as obj_item_reagent_containers;
        if (beaker == null || beaker.reagents == null || Dm.truthy(Dm.bitand(beaker.item_flags, ABSTRACT)) || !Dm.truthy(beaker.is_open_container()))
            return NONE;

        if (custom_ice_cream_beaker != null)
        {
            if (!Dm.truthy(await user.transferItemToLoc(beaker, this)))
            {
                balloon_alert(user, I18n.T("beaker slot full!"));
                return ITEM_INTERACT_BLOCKING;
            }

            await try_put_in_hand(custom_ice_cream_beaker, user);
            balloon_alert(user, I18n.T("beakers swapped"));
            custom_ice_cream_beaker = beaker;
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(await user.transferItemToLoc(beaker, this)))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("beaker inserted"));
        custom_ice_cream_beaker = beaker;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:143
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        obj_item_reagent_containers beaker = tool as obj_item_reagent_containers;
        if (beaker == null || beaker.reagents == null || Dm.truthy(Dm.bitand(beaker.item_flags, ABSTRACT)) || !Dm.truthy(beaker.is_open_container()))
            return NONE;

        float added_reagents = FALSE;
        foreach (datum_reagent beaker_reagents in Dm.each<datum_reagent>(((datum_reagents)beaker.reagents).reagent_list))
        {
            if (Dm.@in(beaker_reagents.type, icecream_vat_reagents))
            {
                added_reagents = TRUE;
                await ((datum_reagents)beaker.reagents).trans_to(this, Dm.num(beaker_reagents.volume), target_id: beaker_reagents.type);
            }
        }

        if (!Dm.truthy(added_reagents))
        {
            balloon_alert(user, I18n.T("no reagents to transfer!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("refilling reagents"));
        playsound(this, Dm.resource("sound/items/drink.ogg"), 25, TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:162
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await swap_modes(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await base.attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:167
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(await swap_modes(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await base.attack_robot_secondary(user, modifiers);
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:172
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (custom_ice_cream_beaker == null)
            return CLICK_ACTION_BLOCKING;
        balloon_alert(user, I18n.T("removed beaker"));
        await try_put_in_hand(custom_ice_cream_beaker, user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:179
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;

        DmList choices = Dm.list();

        string sw__1 = vat_mode;
        if (sw__1 == VAT_MODE_ICECREAM)
        {
            foreach (object flavor_key in Dm.each(ice_cream_icons))
            {
                datum_ice_cream_flavour flavor_datum = GLOB.ice_cream_flavours[flavor_key] as datum_ice_cream_flavour;
                datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
                option.image = ice_cream_icons[flavor_key];
                option.info = span_boldnotice(Dm.text("[]", flavor_datum.ingredients_text));
                choices[flavor_key] = option;
            }
        }
        else if (sw__1 == VAT_MODE_CONES)
        {
            foreach (object cone_key in Dm.each(cone_prototypes))
            {
                obj_item_food_icecream cone_item = cone_prototypes[cone_key] as obj_item_food_icecream;
                datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
                option.image = cone_prototypes[cone_key];
                option.info = span_boldnotice(Dm.text("[]", cone_item.ingredients_text));
                choices[cone_key] = option;
            }
        }

        object choice = await show_radial_menu(
            user,
            this,
            choices,
            require_near: TRUE,
            tooltips: TRUE,
            autopick_single_option: FALSE
        );

        if (!Dm.truthy(choice))
            return dot;
        datum_ice_cream_flavour flavor = GLOB.ice_cream_flavours[choice] as datum_ice_cream_flavour;
        if (flavor != null)
        {
            selected_flavour = flavor.name;
            balloon_alert(user, I18n.T("making []", selected_flavour));
        }
        obj_item_food_icecream cone = cone_prototypes[choice] as obj_item_food_icecream;
        if (cone != null)
            await make_cone(user, choice, cone.ingredients);
        return dot;
    }

    ///Lets the user select a reagent in the vat to spill out.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:222
    public virtual async DmTask spill_reagents(mob_living user = null)
    {
        datum_reagent reagent_to_remove = await tgui_input_list(user, I18n.T("Select a reagent to purge from the vat."), I18n.T("Remove reagent"), ((datum_reagents)reagents).reagent_list, ui_state: GLOB.conscious_state) as datum_reagent;
        if (reagent_to_remove == null || !Dm.truthy(await user.can_perform_action(this, action_bitflags: ALLOW_RESTING)))
            return;
        balloon_alert(user, I18n.T("spilled []", reagent_to_remove.name));
        await ((datum_reagents)reagents).remove_reagent(reagent_to_remove.type, reagent_to_remove.volume);
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:229
    public virtual object make_ice_cream_color(datum_ice_cream_flavour flavor = null)
    {
        if (!Dm.truthy(flavor.color))
            return null;
        image ice_cream_icon = image(Dm.resource("icons/obj/service/kitchen.dmi"), "icecream_custom");
        ice_cream_icon.color = flavor.color;
        return ice_cream_icon;
    }

    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:236
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        disassembled ??= TRUE;
        atom drop_location = this.drop_location() as atom;

        Dm.@new<obj_item_stack_sheet_iron>(drop_location, 4);
        if (custom_ice_cream_beaker != null)
            await custom_ice_cream_beaker.forceMove(drop_location);
        return null;
    }

    ///Makes an ice cream cone of the make_type, using ingredients list as reagents used to make it. Puts in user's hand if possible.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:243
    public virtual async DmTask make_cone(mob user = null, object make_type = null, DmList ingredients = null)
    {
        foreach (object reagents_needed in Dm.each(ingredients))
        {
            if (!Dm.truthy(((datum_reagents)reagents).has_reagent(reagents_needed, CONE_REAGENT_NEEDED)))
            {
                balloon_alert(user, I18n.T("not enough ingredients!"));
                return;
            }
        }
        Type cone_type = ((datum)cone_prototypes[make_type]).type;
        if (cone_type == null)
            return;
        obj_item_food_icecream cone = Dm.@new(cone_type, this) as obj_item_food_icecream;

        foreach (object reagents_used in Dm.each(ingredients))
            await ((datum_reagents)reagents).remove_reagent(reagents_used, CONE_REAGENT_NEEDED);
        balloon_alert_to_viewers(I18n.T("cooks up []", cone.name), I18n.T("cooks up []", cone.name));
        await try_put_in_hand(cone, user as mob_living);
    }

    ///Makes ice cream if it can, then puts it in the ice cream cone we're being attacked with.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:259
    public virtual async DmTask add_flavor_to_cone(datum_component_ice_cream_holder source = null, mob user = null, obj_item_food_icecream cone = null)
    {
        datum_ice_cream_flavour flavor = GLOB.ice_cream_flavours[selected_flavour] as datum_ice_cream_flavour;
        if (flavor == null)
            throw Dm.CRASH(Dm.text("[] was making ice cream of [] but had no flavor datum for it!", user, selected_flavour), "code/modules/food_and_drinks/machinery/icecream_vat.dm", 262);

        foreach (object reagents_needed in Dm.each(flavor.ingredients))
        {
            if (!Dm.truthy(((datum_reagents)reagents).has_reagent(reagents_needed, CONE_REAGENT_NEEDED)))
            {
                balloon_alert(user, I18n.T("not enough ingredients!"));
                return;
            }
        }

        bool should_use_custom_ingredients = Dm.truthy(flavor.takes_custom_ingredients) && custom_ice_cream_beaker != null && Dm.truthy(((datum_reagents)custom_ice_cream_beaker.reagents).total_volume);
        if (Dm.truthy(await flavor.add_flavour(source, should_use_custom_ingredients ? (datum_reagents)custom_ice_cream_beaker.reagents : null)))
        {
            foreach (object reagents_used in Dm.each(flavor.ingredients))
                await ((datum_reagents)reagents).remove_reagent(reagents_used, CONE_REAGENT_NEEDED);
            balloon_alert_to_viewers(I18n.T("scoops []", selected_flavour), I18n.T("scoops []", selected_flavour));
        }

        if (cone is obj_item_food_icecream)
        {
            if (cone.crafted_food_buff == null)
                cone.crafted_food_buff = typeof(datum_status_effect_food_chilling);
            if (user.mind != null)
                ADD_TRAIT(cone, TRAIT_HANDMADE, REF(user.mind));
        }
    }

    ///Swaps the mode to the next one meant to be selected, then tells the user who changed it.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:282
    public virtual async DmTask<object> swap_modes(mob user = null)
    {
        if (!Dm.truthy(await user.can_perform_action(this)))
            return FALSE;
        string sw__1 = vat_mode;
        if (sw__1 == VAT_MODE_ICECREAM)
            vat_mode = VAT_MODE_CONES;
        else if (sw__1 == VAT_MODE_CONES)
            vat_mode = VAT_MODE_ICECREAM;
        balloon_alert(user, I18n.T("dispensing []", vat_mode));
        return TRUE;
    }

    ///Allows any user to see what reagents are in the ice cream vat regardless of special gear.
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:294
    [SignalHandler]
    public virtual object allow_reagent_scan(datum source = null, mob user = null, DmList examine_list = null, float? can_see_insides = null)
    {
        return ALLOW_GENERIC_REAGENT_EXAMINE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/icecream_vat.dm:29, :31, :33  /obj/machinery/icecream_vat's var/static lists
    internal DmList obj_machinery_icecream_vat__ice_cream_icons;
    internal DmList obj_machinery_icecream_vat__cone_prototypes;
    internal DmList obj_machinery_icecream_vat__icecream_vat_reagents;
}
