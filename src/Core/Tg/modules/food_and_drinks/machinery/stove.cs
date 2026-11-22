// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/stove.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the stove and the soup pot. The var blocks are generated (D111).

// tg: code/modules/food_and_drinks/machinery/stove.dm:1
public partial class obj_machinery_stove : obj_machinery
{
    // tg: code/modules/food_and_drinks/machinery/stove.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_stove), Dm.named("container_x", -6), Dm.named("container_y", 16)));
        return dot;
    }
}

// Soup pot for cooking soup
// Future addention ideas:
// - Thermostat you can stick in the pot to see in examine the temperature
// - Tasting the pot to learn its exact contents w/o sci goggles (chef skillchip?)
// tg: code/modules/food_and_drinks/machinery/stove.dm:28
public partial class obj_item_reagent_containers_cup_soup_pot : obj_item_reagent_containers_cup
{
    // tg: code/modules/food_and_drinks/machinery/stove.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_cuffable_item)));
        RegisterSignal(this, COMSIG_ATOM_REAGENT_EXAMINE, nameof(reagent_special_examine));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:56
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Remove ingredient";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        else if (Dm.truthy(can_add_ingredient(held_item)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Add ingredient";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return NONE;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:67
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        datum_reagents reagents = (datum_reagents)this.reagents;
        Dm.append(ref dot, span_notice(I18n.T("There's room for <b>[]</b> more ingredients or <b>[]</b> more units of reagents in there.", max_ingredients - LAZYLEN(added_ingredients), reagents.maximum_volume - reagents.total_volume)));
        return dot;
    }

    /**
     * Override standard reagent examine with something a bit more sensible for the soup pot,
     * including the ingredients we have within as well
     */
    // tg: code/modules/food_and_drinks/machinery/stove.dm:76
    [SignalHandler]
    public virtual object reagent_special_examine(datum source = null, mob user = null, DmList examine_list = null, float? can_see_insides = null)
    {
        can_see_insides ??= FALSE;
        datum_reagents reagents = (datum_reagents)this.reagents;
        Dm.append(ref examine_list, I18n.T("Inside, you can see:"));

        if (Dm.truthy(LAZYLEN(added_ingredients)) || reagents.total_volume > 0)
        {
            DmList ingredient_amounts = Dm.list();
            foreach (obj_item ingredient in Dm.each_cast<obj_item>(added_ingredients))
                ingredient_amounts[ingredient.type] = Dm.add(ingredient_amounts[ingredient.type], 1);

            foreach (object ingredient_type in Dm.each(ingredient_amounts))
                Dm.append(ref examine_list, I18n.T("&bull; [] []\\s", ingredient_amounts[ingredient_type], Dm.initial((Type)ingredient_type, (atom a) => a.name)));

            float unknown_volume = 0;
            foreach (datum_reagent current_reagent in Dm.each_cast<datum_reagent>(reagents.reagent_list))
            {
                if (Dm.truthy(can_see_insides)
                    || current_reagent is datum_reagent_water
                    || current_reagent is datum_reagent_consumable
                )
                    Dm.append(ref examine_list, I18n.T("&bull; [] units of []", round(Dm.num(current_reagent.volume), 0.01f), current_reagent.name));
                else
                    unknown_volume += Dm.num(current_reagent.volume);
            }

            if (unknown_volume > 0)
                Dm.append(ref examine_list, I18n.T("&bull; [] units of unknown reagents", round(unknown_volume, 0.01f)));

            if (reagents.total_volume > 0)
            {
                if (Dm.truthy(can_see_insides))
                    Dm.append(ref examine_list, span_notice(I18n.T("The contents of [] have a temperature of []K.", this, reagents.chem_temp)));
                else if (reagents.chem_temp > WATER_BOILING_POINT) // boiling point
                    Dm.append(ref examine_list, span_notice(I18n.T("The contents of [] are boiling.", this)));
            }
        }
        else
            Dm.append(ref examine_list, I18n.T("Nothing."));

        if (Dm.truthy(reagents.is_reacting))
            Dm.append(ref examine_list, span_warning(I18n.T("It is currently mixing!")));

        return STOP_GENERIC_REAGENT_EXAMINE;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:116
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        LAZYREMOVE(ref added_ingredients, gone);
        return dot;
    }

    /**
     * Adds items to a soup pot without invoking any procs that call sleep() when using in a component.
     *
     * Args:
     * * transfer_from: The container that's being used to add items to the soup pot. Must not be null.
     * * user: the entity adding ingredients via a container to a soup pot. Must not be null.
     */
    // tg: code/modules/food_and_drinks/machinery/stove.dm:127
    public virtual object transfer_from_container_to_pot(obj_item transfer_from = null, mob user = null)
    {
        if (transfer_from.atom_storage == null)
            return null;

        obj_item_storage tray = transfer_from as obj_item_storage;
        float loaded = 0;

        foreach (obj tray_item in Dm.each<obj>(transfer_from.contents))
        {
            if (!Dm.truthy(can_add_ingredient(tray_item as obj_item)))
                continue;
            if (LAZYLEN(added_ingredients) >= max_ingredients)
            {
                balloon_alert(user, I18n.T("it's full!"));
                return TRUE;
            }
            if (Dm.truthy(transfer_from.atom_storage.attempt_remove(tray_item as obj_item, this)))
            {
                loaded++;
                LAZYADD(ref added_ingredients, tray_item);
            }
        }
        if (Dm.truthy(loaded))
        {
            to_chat(user, span_notice(I18n.T("You insert [] items into \\the [].", loaded, this)));
            update_appearance(UPDATE_OVERLAYS);
        }
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:148
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item item = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, item, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (Dm.truthy(item.is_open_container()))
            // (assuming they want to pour rather than add the container itself)
            // allow interaction to fall through to reagent container coed
            return NONE;
        if (!Dm.truthy(can_add_ingredient(item)))
            return ITEM_INTERACT_BLOCKING;

        // Too many ingredients
        if (LAZYLEN(added_ingredients) >= max_ingredients)
        {
            balloon_alert(user, I18n.T("too many ingredients!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(item, this)))
        {
            balloon_alert(user, I18n.T("can't add that!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Ensures that faceatom works correctly, since we can can often be in another atom's loc (a stove)
        atom_movable balloon_loc = ismovable(loc) ? (atom_movable)loc : this;
        balloon_loc.balloon_alert(user, I18n.T("ingredient added"));
        user.face_atom(balloon_loc);

        LAZYADD(ref added_ingredients, item);
        update_appearance(UPDATE_OVERLAYS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:176
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        return transfer_from_container_to_pot(tool, user);
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:179
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYLEN(added_ingredients)))
            return SECONDARY_ATTACK_CALL_NORMAL;

        obj_item removed = added_ingredients[1] as obj_item;
        await removed.forceMove(get_turf(this));
        await user.put_in_hands(removed);

        // Ensures that faceatom works correctly, since we can can often be in another atom's loc (a stove)
        atom_movable balloon_loc = ismovable(loc) ? (atom_movable)loc : this;
        balloon_loc.balloon_alert(user, I18n.T("ingredient removed"));
        user.face_atom(balloon_loc);

        update_appearance(UPDATE_OVERLAYS);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:195
    public virtual object can_add_ingredient(obj_item ingredient = null)
    {
        // Let default reagent handling take this
        if (Dm.truthy(ingredient.is_open_container()))
            return FALSE;
        // To big for the pot
        if (ingredient.w_class >= WEIGHT_CLASS_BULKY)
            return FALSE;
        if (Dm.truthy(Dm.bitand(ingredient.item_flags, ABSTRACT | DROPDEL | HAND_ITEM)))
            return FALSE;
        if (HAS_TRAIT(ingredient, TRAIT_NO_STORAGE_INSERT))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:208
    public override async DmTask<object> try_splash(mob user = null, atom target = null)
    {
        object dot = await base.try_splash(user, target);
        if (!Dm.truthy(dot) && Dm.truthy(LAZYLEN(added_ingredients)))
            await dump_ingredients();
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:213
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (!Dm.truthy(dot) && Dm.truthy(LAZYLEN(added_ingredients)))
            Waiting.NoSleep(dump_ingredients());
        return dot;
    }

    /**
     * Dumps all inside ingredients to a spot
     *
     * * drop_loc - Where to drop the ingredients, defaults to drop loc
     * * x_offset - How much pixel X offset to give every ingredient, if not set will be random
     * * y_offset - How much pixel Y offset to give every ingredient, if not set will be random
     */
    // tg: code/modules/food_and_drinks/machinery/stove.dm:225
    public virtual async DmTask dump_ingredients(atom drop_loc = null, object x_offset = null, object y_offset = null)
    {
        drop_loc ??= drop_location() as atom;
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(added_ingredients))
        {
            await ingredient.forceMove(drop_loc);
            ingredient.pixel_x += isnum(x_offset) ? Dm.num(x_offset) : rand(-4, 4);
            ingredient.pixel_y += isnum(y_offset) ? Dm.num(x_offset) : rand(-4, 4);
            ingredient.SpinAnimation(loops: 1);
        }
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/food_and_drinks/machinery/stove.dm:233
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        datum_reagents reagents = (datum_reagents)this.reagents;
        if (Dm.length(added_ingredients) <= 0 && reagents.total_volume <= 0)
            return dot;
        mutable_appearance filled_overlay = mutable_appearance(icon, Dm.text("[]_filling_overlay", base_icon_state)) as mutable_appearance;
        DmList food_reagents = Dm.list();
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(added_ingredients))
        {
            if (ingredient.reagents == null)
                continue;
            Dm.combine(ref food_reagents, ((datum_reagents)ingredient.reagents).reagent_list);
        }

        filled_overlay.color = mix_color_from_reagents(Dm.add(reagents.reagent_list, food_reagents));
        Dm.append(ref dot, filled_overlay);
        return dot;
    }
}
