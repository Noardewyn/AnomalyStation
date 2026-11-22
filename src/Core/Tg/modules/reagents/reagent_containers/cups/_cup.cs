// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/_cup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: M8 slice C: the open container base (drinking and feeding, pouring into and drawing from containers, the
// lid, the egg, the attached cell's wiring) and the beaker; gaps S1 (D291): the drinker's food taste reaction; gaps AS1: an
// assembly holder attached to the lid, its igniter heating the contents or setting off the wired cell. The bucket's procs are later (rows in docs/ai/specs/live-partial.txt);
// gaps P5: the mortar and the test tube. The var blocks are generated.

// tg: code/modules/reagents/reagent_containers/cups/_cup.dm:1
public partial class obj_item_reagent_containers_cup : obj_item_reagent_containers
{
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(heatable))
            Waiting.NoSleep(AddElement(typeof(datum_element_reagents_item_heatable)));
        register_context();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:49
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref lid_assembly);
        QDEL_NULL(ref attached_cell);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:54
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(cell_wired) && Dm.eq(held_item?.tool_behaviour, TOOL_WIRECUTTER))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Cut wires";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (!Dm.truthy(can_lid))
            return dot;

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_ALT_LMB] = lid_assembly != null ? "Detach assembly" : "Toggle lid";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (lid_assembly == null && istype(held_item, typeof(obj_item_assembly_holder)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Attach assembly";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (attached_cell == null && lid_assembly != null && istype(held_item, typeof(obj_item_stock_parts_power_store_cell)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Attach cell";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:72
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(drink_type))
        {
            DmList types = bitfield_to_list(drink_type, FOOD_FLAGS) as DmList;
            Dm.append(ref dot, span_notice(I18n.T("The label says it contains [] ingredients.", LOWER_TEXT(english_list(types)))));
        }
        if (Dm.truthy(can_lid))
        {
            if (Dm.truthy(has_lid))
                Dm.append(ref dot, span_notice(I18n.T("Its sealed with a bright orange rubber lid[].", lid_assembly != null ? I18n.T("with an assembly attached ontop of it") : "")));
            else
                Dm.append(ref dot, span_notice(I18n.T("It can be sealed with a lid using [].", EXAMINE_HINT("Alt-Click"))));
        }
        return dot;
    }

    /**
     * Checks if the mob actually liked drinking this cup.
     *
     * This is a bunch of copypaste from the edible component, consider reworking this to use it!
     */
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:88
    public virtual async DmTask<object> checkLiked(object fraction = null, mob eater = null)
    {
        if (Dm.num(last_check_time) + 5 * SECONDS > world.time)
            return FALSE;
        if (!ishuman(eater))
            return FALSE;
        mob_living_carbon_human gourmand = (mob_living_carbon_human)eater;
        //Bruh this breakfast thing is cringe and shouldve been handled separately from food-types, remove this in the future (Actually, just kill foodtypes in general)
        if (Dm.truthy(Dm.bitand(drink_type, BREAKFAST)) && world.time - SSticker.round_start_time < STOP_SERVING_BREAKFAST)
            await gourmand.add_mood_event("breakfast", typeof(datum_mood_event_breakfast));
        last_check_time = world.time;
        object food_taste_reaction = gourmand.get_food_taste_reaction(this, drink_type);
        // switch(food_taste_reaction)
        if (Dm.eq(food_taste_reaction, FOOD_TOXIC))
        {
            to_chat(gourmand, span_warning(I18n.T("What the hell was that thing?!")));
            gourmand.adjust_disgust(25 + 30 * Dm.num(fraction));
            await gourmand.add_mood_event("toxic_food", typeof(datum_mood_event_disgusting_food));
        }
        else if (Dm.eq(food_taste_reaction, FOOD_DISLIKED))
        {
            to_chat(gourmand, span_notice(I18n.T("That didn't taste very good...")));
            gourmand.adjust_disgust(11 + 15 * Dm.num(fraction));
            await gourmand.add_mood_event("gross_food", typeof(datum_mood_event_gross_food));
        }
        else if (Dm.eq(food_taste_reaction, FOOD_LIKED))
        {
            to_chat(gourmand, span_notice(I18n.T("I love this taste!")));
            gourmand.adjust_disgust(-5 + -2.5f * Dm.num(fraction));
            await gourmand.add_mood_event("fav_food", typeof(datum_mood_event_favorite_food));
        }
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:114
    public virtual async DmTask<object> try_drink(mob_living target_mob = null, mob_living user = null)
    {
        if (!Dm.truthy(canconsume(target_mob, user)))
            return ITEM_INTERACT_BLOCKING;

        user.changeNext_move(CLICK_CD_MELEE);

        if (target_mob != user)
        {
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, target_mob)))
                return ITEM_INTERACT_BLOCKING;
            target_mob.visible_message(
                span_danger(I18n.T("[] attempts to feed [] something from [].", user, target_mob, this)),
                span_userdanger(I18n.T("[] attempts to feed you something from [].", user, this))
            );
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target_mob)))
                return ITEM_INTERACT_BLOCKING;
            if (reagents == null || !Dm.truthy(((datum_reagents)reagents).total_volume))
                return ITEM_INTERACT_BLOCKING; // The drink might be empty after the delay, such as by spam-feeding
            target_mob.visible_message(
                span_danger(I18n.T("[] feeds [] something from [].", user, target_mob, this)),
                span_userdanger(I18n.T("[] feeds you something from [].", user, this))
            );
            if (Dm.truthy(target_mob.is_blind()))
                to_chat(target_mob, span_notice(I18n.T("You feel someone feed you something.")));
            log_combat(user, target_mob, "fed", ((datum_reagents)reagents).get_reagent_log_string());
        }
        else
        {
            if (Dm.truthy(loop_drink))
            {
                if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, user)))
                    return ITEM_INTERACT_BLOCKING;
                user.visible_message(
                    span_notice(I18n.T("[] attempts to drink from [].", user, this)),
                    ignored_mobs: Dm.list(user)
                );
                to_chat(user, span_notice(I18n.T("You attempt to drink from [].", this)));
                if (!Dm.truthy(await do_after(user, 1.25f * SECONDS, user)))
                    return ITEM_INTERACT_BLOCKING;
                if (reagents == null || !Dm.truthy(((datum_reagents)reagents).total_volume))
                    return ITEM_INTERACT_BLOCKING;
                user.visible_message(
                    span_notice(I18n.T("[] drinks from [].", user, this)),
                    ignored_mobs: Dm.list(user)
                );
            }
            to_chat(user, span_notice(I18n.T("You swallow a gulp of [].", this)));
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_GLASS_DRANK]))
            _SendSignal(COMSIG_GLASS_DRANK, Dm.list(this, target_mob, user));
        datum_reagents holder = (datum_reagents)reagents;
        float fraction = min(gulp_size / holder.total_volume, 1);
        await holder.trans_to(target_mob, gulp_size, transferred_by: user, methods: reagent_consumption_method);
        if (user.hud_used?.screen_objects?[HUD_MOB_HUNGER] is atom_movable_screen_hunger hunger_bar)
            hunger_bar.update_hunger_bar();
        await checkLiked(fraction, target_mob);
        playsound(target_mob, consumption_sound, rand(10, 50), TRUE);
        DmList diseases_to_add = null;
        foreach (datum_disease malady in Dm.each_cast<datum_disease>(await target_mob.get_static_viruses()))
        {
            if (Dm.truthy(Dm.bitand(malady.spread_flags, DISEASE_SPREAD_CONTACT_FLUIDS)))
                LAZYADD(ref diseases_to_add, malady);
        }
        if (Dm.truthy(LAZYLEN(diseases_to_add)))
            await AddComponent(typeof(datum_component_infective), diseases_to_add);
        if (Dm.truthy(loop_drink))
            return Dm.bitor(await try_drink(target_mob, user), ITEM_INTERACT_SUCCESS);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:176
    public override async DmTask<object> interact_with_atom(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(target, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!Dm.truthy(is_open_container()))
            return NONE;

        if (Dm.truthy(target.is_refillable())) //Something like a glass. Player probably wants to transfer TO it.
            return await try_refill(target, user);

        if (Dm.truthy(target.is_drainable())) //A dispenser. Transfer FROM it TO us.
            return await try_drain(target, user);

        if (isliving(target))
            return await try_drink((mob_living)target, user);

        if (Dm.truthy(is_drainable()))
            return await pour_concrete(target, user);

        return NONE;
    }

    /// QoL helper for pouring concrete directly from container just by clicking.
    /// Easier than splashing onto turf or object, but requires a little delay.
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:199
    public virtual async DmTask<object> pour_concrete(atom target = null, mob_living user = null)
    {
        if (((datum_reagents)reagents).has_reagent(typeof(datum_reagent_concrete), check_subtypes: TRUE) is not datum_reagent_concrete)
            return NONE;
        Dm.NotTranslated("code/modules/reagents/reagent_containers/cups/_cup.dm:203", "pour_concrete: the concrete reagent's pour_amount (concrete, M10+ engineering odds and ends)");
        return NONE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:225
    public override async DmTask<object> interact_with_atom_secondary(atom target = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom_secondary(target, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (!Dm.truthy(is_open_container()))
            return NONE;
        if (Dm.truthy(target.is_drainable())) //A dispenser. Transfer FROM it TO us.
            return await try_drain(target, user);
        return NONE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:237
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(has_lid) && istype(tool, typeof(obj_item_assembly_holder)))
        {
            if (lid_assembly != null)
            {
                to_chat(user, span_warning(I18n.T("[]'s lid already has an assembly attached to it!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await attach_assembly((obj_item_assembly_holder)tool, user)))
            {
                playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        if (lid_assembly != null && istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
        {
            if (attached_cell != null)
            {
                to_chat(user, span_warning(I18n.T("[] already has \\a [] attached to it!", this, attached_cell)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.locate<obj_item_assembly_igniter>(lid_assembly) == null)
            {
                to_chat(user, span_warning(I18n.T("[] doesn't have an igniter to connect [] to!", lid_assembly, this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You attach [] underneath []'s lid.", tool, this)));
            add_fingerprint(user);
            log_bomber(user, Dm.text("attached [] to", tool.name), this);
            attached_cell = (obj_item_stock_parts_power_store_cell)tool;
            attached_cell.pixel_y = 0;
            attached_cell.pixel_z = -4;
            update_appearance();
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (attached_cell != null && istype(tool, typeof(obj_item_stack_cable_coil)))
        {
            if (Dm.truthy(cell_wired))
            {
                to_chat(user, span_warning(I18n.T("[] is already wired to []!", attached_cell, lid_assembly)));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_stack_cable_coil cable = (obj_item_stack_cable_coil)tool;
            if (!Dm.truthy(await cable.use(5)))
            {
                to_chat(user, span_warning(I18n.T("You need at least 5 cable pieces to wire []!", attached_cell)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You wire [] to [].", attached_cell, lid_assembly)));
            add_fingerprint(user);
            cell_wired = TRUE;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(is_open_container()))
            return NONE;

        if (istype(tool, typeof(obj_item_food_egg))) //breaking eggs
        {
            if (Dm.truthy(((datum_reagents)reagents).holder_full()))
            {
                to_chat(user, span_notice(I18n.T("[] is full.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("You break [] in [].", tool, this)));
            await ((datum_reagents)tool.reagents).trans_to(this, ((datum_reagents)tool.reagents).total_volume, transferred_by: user);
            qdel(tool);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    /*
     * On accidental consumption, make sure the container is partially glass, and continue to the reagent_container proc
     */
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:304
    public override async DmTask<object> on_accidental_consumption(mob_living_carbon M = null, mob_living_carbon user = null, obj_item source_item = null, float? discover_after = null)
    {
        discover_after ??= TRUE;
        if (Dm.truthy(isGlass) && !Dm.truthy(custom_materials))
            await set_custom_materials(Dm.list(Dm.kv(await SSmaterials.get_material(typeof(datum_material_glass)), 5)));//sets it to glass so, later on, it gets picked up by the glass catch (hope it doesn't 'break' things lol)
        return await base.on_accidental_consumption(M, user, source_item, discover_after);
    }

    /// Callback for [datum/component/takes_reagent_appearance] to inherent style footypes
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:310
    public virtual void on_cup_change(datum_glass_style_has_foodtype style = null)
    {
        if (style == null)
            return;
        drink_type = Dm.num(style.drink_type);
    }

    /// Callback for [datum/component/takes_reagent_appearance] to reset to no foodtypes
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:316
    public virtual void on_cup_reset()
    {
        drink_type = NONE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:319
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_lid))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_lid", icon_state)));
        if (lid_assembly != null)
            Dm.append(ref dot, lid_assembly);
        if (attached_cell != null)
        {
            Dm.append(ref dot, attached_cell);
            if (Dm.truthy(cell_wired))
                Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/machines/cell_charger.dmi"), Dm.text("ccharger-[]-on", attached_cell.connector_type)));
        }
        return dot;
    }

    // For player convinience, assume that the lids are rubber and can be pierced with a syringe
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:331
    public override object is_refillable()
    {
        return base.is_refillable() is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)(!Dm.truthy(has_lid) ? TRUE : FALSE);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:334
    public override object is_drainable()
    {
        return base.is_drainable() is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)(!Dm.truthy(has_lid) ? TRUE : FALSE);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:337
    public override object is_dunkable()
    {
        return base.is_dunkable() is var and__1 && !Dm.truthy(and__1) ? and__1 : (object)(!Dm.truthy(has_lid) ? TRUE : FALSE);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:340
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (lid_assembly == null)
            return await base.attack_self(user, modifiers, __extra);
        await lid_assembly.attack_self(user);
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:346
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(can_lid))
            return NONE;

        if (Dm.truthy(cell_wired))
        {
            balloon_alert(user, I18n.T("cut the wiring first!"));
            return CLICK_ACTION_BLOCKING;
        }

        if (attached_cell != null)
        {
            obj_item our_cell = attached_cell;
            // Exited() automatically clears it
            await our_cell.forceMove((atom)drop_location());
            await user.put_in_hands(our_cell);
            balloon_alert(user, I18n.T("cell detached"));
            update_appearance();
            return CLICK_ACTION_SUCCESS;
        }

        if (lid_assembly != null)
        {
            obj_item our_assembly = lid_assembly;
            await our_assembly.forceMove((atom)drop_location());
            await user.put_in_hands(our_assembly);
            balloon_alert(user, I18n.T("assembly detached"));
            update_appearance();
            return CLICK_ACTION_SUCCESS;
        }

        has_lid = Dm.not(has_lid);
        update_appearance();
        balloon_alert(user, I18n.T("lid []", Dm.truthy(has_lid) ? I18n.T("sealed") : I18n.T("unsealed")));
        if (Dm.truthy(has_lid))
            add_container_flags(SEALED_CONTAINER);
        else
            reset_container_flags();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:380
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(cell_wired))
            return NONE;
        Dm.@new<obj_item_stack_cable_coil>(drop_location(), 5);
        cell_wired = FALSE;
        update_appearance();
        balloon_alert(user, I18n.T("wiring cut"));
        tool.play_tool_sound(this, 50);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:391
    public virtual async DmTask<object> attach_assembly(obj_item_assembly_holder assembly = null, mob_living user = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(assembly, this)))
        {
            to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", assembly)));
            return FALSE;
        }

        to_chat(user, span_notice(I18n.T("You attach [] to []'s lid.", assembly, this)));
        add_fingerprint(user);
        lid_assembly = assembly;
        lid_assembly.master = this;
        lid_assembly.pixel_y = 0;
        lid_assembly.pixel_z = assembly_pixel_y;
        await lid_assembly.on_attach();
        RegisterSignal(this, COMSIG_IGNITER_ACTIVATE, nameof(on_igniter_activate));
        log_bomber(user, Dm.text("attached [] to", lid_assembly.name), this);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:408
    public override async DmTask<object> on_found(mob finder = null)
    {
        object dot = await base.on_found(finder);
        if (lid_assembly != null)
            await lid_assembly.on_found(finder);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:412
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == lid_assembly)
        {
            lid_assembly = null;
            UnregisterSignal(this, COMSIG_IGNITER_ACTIVATE);
            update_appearance();
        }
        else if (gone == attached_cell)
        {
            attached_cell = null;
            cell_wired = FALSE;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:423
    [SignalHandler]
    public virtual void on_igniter_activate(datum source = null, obj_item_assembly_igniter igniter = null)
    {
        // We've got an attached cell wired up, so we'll try to spend all of its current first
        if (attached_cell != null && Dm.truthy(cell_wired))
        {
            object power_spent = Waiting.NoSleep(attached_cell.use(attached_cell.charge__proc()));
            // Power cell was rigged
            if (QDELETED(this))
                return;

            // We'll be nerfing plasma and welding fuel as they're very easy to get and make for boring bombs
            if (Dm.num(power_spent) > 0 && Dm.truthy(Dm.bitand(Waiting.NoSleep(((datum_reagents)reagents).spark_act(power_spent, Dm.bitor(SPARK_ACT_ENCLOSED, SPARK_ACT_WEAKEN_COMMON))), SPARK_ACT_DESTRUCTIVE)))
            {
                qdel(this);
                return;
            }
        }

        // Igniters heat, condensers chill
        object igniter_temp = igniter.get_temperature();
        if (Dm.num(igniter_temp) > 0)
            Waiting.NoSleep(((datum_reagents)reagents).expose_temperature(igniter_temp));
    }
}

// tg: code/modules/reagents/reagent_containers/cups/_cup.dm:440
public partial class obj_item_reagent_containers_cup_beaker : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:459
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:463
    public override object get_part_rating()
    {
        return ((datum_reagents)reagents).maximum_volume;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/_cup.dm:585
public partial class obj_item_reagent_containers_cup_bucket : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:616
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/reagents/reagent_containers/cups/_cup.dm:618", "Initialize: the cuffable item element (cuffing a bucket to the wrist, section 107)");
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:637
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        datum_reagents our_reagents = reagents as datum_reagents;
        if (tool is obj_item_mop)
        {
            if (Dm.num(our_reagents.total_volume) < 1)
            {
                user.balloon_alert(user, "empty!");
                return ITEM_INTERACT_BLOCKING;
            }
            await our_reagents.trans_to(tool, 5, transferred_by: user);
            user.balloon_alert(user, Dm.text("doused []", tool));
            playsound(this, Dm.resource("sound/effects/slosh.ogg"), 25, TRUE);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_assembly_prox_sensor) //This works with wooden buckets for now. Somewhat unintended, but maybe someone will add sprites for it soon(TM)
        {
            to_chat(user, span_notice(Dm.text("You add [] to [].", tool, this)));
            qdel(tool);
            obj_item_bot_assembly_cleanbot new_cleanbot_ass = await Dm.new_async<obj_item_bot_assembly_cleanbot>(null, this);
            await user.put_in_hands(new_cleanbot_ass);
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:655
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
        {
            datum_reagents our_reagents = reagents as datum_reagents;
            if (Dm.truthy(our_reagents.total_volume))
            {
                to_chat(user, span_userdanger(Dm.text("[]'s contents spill all over you!", this)));
                await our_reagents.expose(user, TOUCH);
                await our_reagents.clear_reagents();
            }
            update_container_flags(NONE);
        }
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:664
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        reset_container_flags();
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:668
    public override async DmTask<object> equip_to_best_slot(mob user = null)
    {
        if (Dm.truthy((reagents as datum_reagents).total_volume)) //If there is water in a bucket, don't quick equip it to the head
        {
            float index = Dm.num(slot_equipment_priority.Find(ITEM_SLOT_HEAD));
            slot_equipment_priority.Remove(ITEM_SLOT_HEAD);
            object dot = await base.equip_to_best_slot(user);
            slot_equipment_priority.Insert(index, ITEM_SLOT_HEAD);
            return dot;
        }
        return await base.equip_to_best_slot(user);
    }
}

// tg: code/modules/reagents/reagent_containers/cups/_cup.dm:686
public partial class obj_item_reagent_containers_cup_mortar : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:699
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (grinded == null)
            return CLICK_ACTION_BLOCKING;
        await grinded.forceMove(drop_location() as atom);
        grinded = null;
        balloon_alert(user, "ejected");
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:707
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (istype(tool, typeof(obj_item_pestle)))
        {
            if (grinded == null)
            {
                to_chat(user, span_warning(I18n.T("There is nothing to grind!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.num(user.get_stamina_loss()) > 50)
            {
                to_chat(user, span_warning(I18n.T("You are too tired to work!")));
                return ITEM_INTERACT_BLOCKING;
            }
            DmList choose_options = Dm.list(
                Dm.kv("Grind", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_grind")),
                Dm.kv("Juice", image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_juice")));
            object picked_option = await show_radial_menu(user, this, choose_options, radius: 38, require_near: TRUE);
            if (grinded == null || !Dm.truthy(in_range(this, user)) || !Dm.truthy(user.is_holding(tool)) || !Dm.truthy(picked_option))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You start grinding...")));
            if (!Dm.truthy(await do_after(user, 2.5f * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            await user.adjust_stamina_loss(40);
            switch (picked_option)
            {
                case "Juice":
                    await juice_item(grinded, user as mob_living_carbon_human);
                    return ITEM_INTERACT_SUCCESS; // juice_item returns null: tg's ternary always takes ITEM_INTERACT_SUCCESS here
                case "Grind":
                    await grind_item(grinded, user as mob_living_carbon_human);
                    return ITEM_INTERACT_BLOCKING; // grind_item returns null: tg's ternary always takes ITEM_INTERACT_BLOCKING here
            }
            to_chat(user, span_notice(I18n.T("You try to grind the mortar itself instead of []. You failed.", grinded)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (grinded != null)
        {
            to_chat(user, span_warning(I18n.T("There is something inside already!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(tool.blend_requirements(this, user)))
            return ITEM_INTERACT_BLOCKING;
        if ((Dm.truthy(length(await tool.grind_results())) || Dm.truthy((tool.reagents as datum_reagents)?.total_volume)) && Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            grinded = tool;
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:746
    public override async DmTask<object> blended(obj_item blended_item = null, object grinded = null)
    {
        this.grinded = null;

        return await base.blended(blended_item, grinded);
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:751
    public virtual async DmTask grind_item(obj_item item = null, mob_living_carbon_human user = null)
    {
        if (Dm.truthy(Dm.bitand(item.flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_notice(I18n.T("You try to grind [], but it fades away!", item)));
            qdel(item);
            return;
        }

        if (!Dm.truthy(await item.grind(reagents as datum_reagents, user)))
        {
            if (isstack(item))
                to_chat(user, span_notice(I18n.T("[] attempts to grind as many pieces of [] as possible.", this, item)));
            else
                to_chat(user, span_danger(I18n.T("You fail to grind [].", item)));
            return;
        }

        to_chat(user, span_notice(I18n.T("You grind [] into a nice powder.", item)));
    }

    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:766
    public virtual async DmTask juice_item(obj_item item = null, mob_living_carbon_human user = null)
    {
        if (Dm.truthy(Dm.bitand(item.flags_1, HOLOGRAM_1)))
        {
            to_chat(user, span_notice(I18n.T("You try to juice [], but it fades away!", item)));
            qdel(item);
            return;
        }

        if (!Dm.truthy(await item.juice(reagents as datum_reagents, user)))
        {
            to_chat(user, span_notice(I18n.T("You fail to juice [].", item)));
            return;
        }

        to_chat(user, span_notice(I18n.T("You juice [] into a fine liquid.", item)));
    }
}

///Test tubes created by chem master and pandemic and placed in racks
// tg: code/modules/reagents/reagent_containers/cups/_cup.dm:797
public partial class obj_item_reagent_containers_cup_tube : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/_cup.dm:809
    public override async DmTask<object> attach_assembly(obj_item_assembly_holder assembly = null, mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("[]'s lid is too small to fit []!", this, assembly)));
        await DmTask.CompletedTask;
        return FALSE;
    }
}
