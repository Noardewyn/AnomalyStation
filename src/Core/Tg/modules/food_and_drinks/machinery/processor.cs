// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/machinery/processor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the food processor and the slime processor. The var blocks are generated (D111); the
// static processor_inputs is a TgState field (rules 10.6). The slime processor has a USB port for its circuit component.

// tg: code/modules/food_and_drinks/machinery/processor.dm:3
public partial class obj_machinery_processor : obj_machinery
{
    /*
     * Static, nested list. The first layer contains all food processor types.
     * The second layer contains input typepaths (key) and the associated food_processor_process datums (assoc) the processor can access.
     * This allows for different types of processor to produce different outputs from same input as long as the recipes require different processors.
     */
    // tg: code/modules/food_and_drinks/machinery/processor.dm:27  var/static/list/processor_inputs (rules 10.6)
    public static ref DmList processor_inputs => ref TgState.Current.obj_machinery_processor__processor_inputs;

    // tg: code/modules/food_and_drinks/machinery/processor.dm:1  #define PROCESSOR_SELECT_RECIPE(movable_input) LAZYACCESS(processor_inputs[type], movable_input.type)
    // (file-local, #undef at :327)
    internal datum_food_processor_process PROCESSOR_SELECT_RECIPE(atom movable_input) => LAZYACCESS(processor_inputs[type], movable_input.type) as datum_food_processor_process;

    // tg: code/modules/food_and_drinks/machinery/processor.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(processor_inputs))
            return dot;
        processor_inputs = Dm.list();
        foreach (Type recipe__type in Dm.each<Type>(subtypesof(typeof(datum_food_processor_process))))
        {
            if (Dm.initial(recipe__type, (datum_food_processor_process r) => r.input) == null)
                continue;
            datum_food_processor_process recipe = Dm.@new(recipe__type) as datum_food_processor_process;
            DmList typecache = Dm.list();
            DmList bad_types = null;
            foreach (object bad_type in Dm.each(recipe.blacklist))
                LAZYADD(ref bad_types, typesof(bad_type));
            foreach (object input_type in Dm.each(Dm.sub(typesof(recipe.input), bad_types)))
                typecache[input_type] = recipe;
            foreach (object machine_type in Dm.each(typesof(recipe.required_machine)))
            {
                DmList machine_inputs = processor_inputs[machine_type] as DmList;
                LAZYADD(ref machine_inputs, typecache);
                processor_inputs[machine_type] = machine_inputs;
            }
        }
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:47
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            rating_amount = Dm.num(matter_bin.tier);
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            rating_speed = Dm.num(servo.tier);
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(ref dot, span_notice(I18n.T("The status display reads: Outputting <b>[]</b> item(s) at <b>[]%</b> speed.", rating_amount, rating_speed * 100)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:59
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        LAZYREMOVE(ref processor_contents, gone);
        return dot;
    }

    /*
    *	Spawns the items based on the passed processor recipes, and transfers materials and reagents
    *	If the input was a mob, gibs them, otherwise, deletes the item
    */
    // tg: code/modules/food_and_drinks/machinery/processor.dm:67
    public virtual async DmTask process_food(datum_food_processor_process recipe = null, atom_movable what = null)
    {
        if (recipe.output != null && loc != null && !QDELETED(this))
        {
            DmList cached_mats = (Dm.truthy(recipe.preserve_materials) ? what.custom_materials : null) as DmList; // recipe.preserve_materials && what.custom_materials
            float cached_multiplier = recipe.food_multiplier * rating_amount;
            for (float i = 1, i__end = cached_multiplier; i <= i__end; i++)
            {
                atom processed_food = Dm.@new(recipe.output, drop_location()) as atom;
                if (processed_food.reagents != null && what.reagents != null)
                {
                    await ((datum_reagents)processed_food.reagents).clear_reagents();
                    await ((datum_reagents)what.reagents).trans_to(processed_food, ((datum_reagents)what.reagents).total_volume, multiplier: Dm.num(Dm.div(1, cached_multiplier)), copy_only: TRUE);
                }
                if (Dm.truthy(cached_mats))
                    await processed_food.set_custom_materials(cached_mats, Dm.num(Dm.div(1, cached_multiplier)));
                if (Dm.truthy(what._listen_lookup?[SIGNAL_USED_IN_FOOD_PROCESSOR]))
                    what._SendSignal(SIGNAL_USED_IN_FOOD_PROCESSOR, Dm.list(what, processed_food, recipe));
            }
        }

        if (isliving(what))
        {
            mob_living themob = (mob_living)what;
            await themob.gib();
        }
        else
            qdel(what);
        LAZYREMOVE(ref processor_contents, what);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:87
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(processing))
        {
            to_chat(user, span_warning(I18n.T("[] is in the process of processing!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:95
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(processing))
        {
            to_chat(user, span_warning(I18n.T("[] is in the process of processing!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:102
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(processing))
        {
            to_chat(user, span_warning(I18n.T("[] is in the process of processing!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_pry_open(user, tool, close_after_pry: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:109
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (Dm.truthy(processing))
        {
            to_chat(user, span_warning(I18n.T("[] is in the process of processing!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (tool is obj_item_storage_bag_tray)
        {
            obj_item_storage attacking_storage = (obj_item_storage)tool;
            float loaded = 0;
            foreach (obj content_item in Dm.each<obj>(attacking_storage.contents))
            {
                if (!IS_EDIBLE(content_item))
                    continue;
                datum_food_processor_process recipe__1 = PROCESSOR_SELECT_RECIPE(content_item);
                if (recipe__1 != null && Dm.truthy(attacking_storage.atom_storage.attempt_remove(content_item as obj_item, this)))
                {
                    LAZYADD(ref processor_contents, content_item);
                    loaded++;
                }
            }
            if (Dm.truthy(loaded))
            {
                to_chat(user, span_notice(I18n.T("You insert [] items into [].", loaded, this)));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }

        datum_food_processor_process recipe = PROCESSOR_SELECT_RECIPE(tool);
        if (recipe != null && Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            user.visible_message(
                span_notice(I18n.T("[] put [] into [].", user, tool, this)),
                span_notice(I18n.T("You put [] into [].", tool, this))
            );
            LAZYADD(ref processor_contents, tool);
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_warning(I18n.T("That probably won't blend!")));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:144
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]_open", base_icon_state) : base_icon_state as string;
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:148
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(processing))
        {
            to_chat(user, span_warning(I18n.T("[] is in the process of processing!", this)));
            return TRUE;
        }
        if (ismob(user.pulling) && PROCESSOR_SELECT_RECIPE(user.pulling) != null)
        {
            if (user.grab_state < GRAB_AGGRESSIVE)
            {
                to_chat(user, span_warning(I18n.T("You need a better grip to do that!")));
                return null;
            }
            mob_living pushed_mob = user.pulling as mob_living;
            visible_message(span_warning(I18n.T("[] stuffs [] into []!", user, pushed_mob, this)));
            await pushed_mob.forceMove(this);
            LAZYADD(ref processor_contents, pushed_mob);
            await user.stop_pulling();
            return null;
        }
        if (!Dm.truthy(LAZYLEN(processor_contents)))
        {
            to_chat(user, span_warning(I18n.T("[] is empty!", this)));
            return TRUE;
        }
        user.visible_message(span_notice(I18n.T("[] turns on [].", user, this)),
            span_notice(I18n.T("You turn on [].", this)),
            span_hear(I18n.T("You hear a food processor.")));
        await processing__proc();
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:171
    public virtual async DmTask processing__proc()
    {
        processing = TRUE;
        playsound(this.loc, Dm.resource("sound/machines/blender.ogg"), 50, TRUE);
        await use_energy(active_power_usage);
        float total_time = 0;
        foreach (atom_movable movable_input in Dm.each_cast<atom_movable>(processor_contents))
        {
            datum_food_processor_process recipe = PROCESSOR_SELECT_RECIPE(movable_input);
            if (recipe == null)
            {
                log_admin(Dm.text("DEBUG: [] in processor doesn't have a suitable recipe. How did it get in there? Please report it immediately!!!", movable_input));
                continue;
            }
            total_time += Dm.num(recipe.time);
        }

        if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(make_vegan_wellington) is FALSE in production (rules 1.7)
        {
            await complete_processing(); //don't waste time, we need that uncooked bacon strip right away
            return;
        }

        float duration = Dm.num(Dm.div(total_time, rating_speed));
        Waiting.DetachSync(() => Shake(1, 0, duration));
        _addtimer(Dm.@new<datum_callback>(this, nameof(complete_processing)), duration, file: "code/modules/food_and_drinks/machinery/processor.dm", line: 189);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:191
    public virtual async DmTask complete_processing()
    {
        foreach (atom_movable content_item in Dm.each<atom_movable>(processor_contents))
        {
            datum_food_processor_process recipe = PROCESSOR_SELECT_RECIPE(content_item);
            if (recipe == null)
            {
                log_admin(Dm.text("DEBUG: [] in processor doesn't have a suitable recipe. How do you put it in?", content_item));
                continue;
            }
            await process_food(recipe, content_item);
        }
        processing = FALSE;
        visible_message(span_notice(I18n.T("\\The [] finishes processing.", this)));
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:201  GAME_VERB_SRC(/obj/machinery/processor, eject, oview(1), "Eject Contents", null)
    // The verb hands its arguments to SSverbs.invoke_verb, which runs the body for a caller with a client (observer.cs's
    // reenter_corpse); the verb panel and SSverbs are M9.
    public virtual void eject(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_eject(Dm.list()));
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:201  (the verb's body, __gvb_eject)
    public virtual async DmTask __gvb_eject(DmList structured_args = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(usr) || HAS_TRAIT(usr, TRAIT_HANDS_BLOCKED))
            return;
        if (!Dm.truthy(await usr.can_perform_action(this)))
            return;
        if (isliving(usr))
        {
            mob_living L = (mob_living)usr;
            if (!Dm.truthy(Dm.bitand(L.mobility_flags, MOBILITY_UI)))
                return;
        }
        await dump_inventory_contents();
        add_fingerprint(usr);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:214
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        await user.forceMove(drop_location() as atom);
        user.visible_message(span_notice(I18n.T("[] crawls free of the processor!", user)));
        return null;
    }
}

// tg: code/modules/food_and_drinks/machinery/processor.dm:218
public partial class obj_machinery_processor_slime : obj_machinery_processor
{
    // tg: code/modules/food_and_drinks/machinery/processor.dm:229
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_slime_processor)), only_root_path: TRUE)));
        return dot;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:234  adjust_item_drop_location's var/static/list/slimecores (rules 10.6)
    private static DmList adjust_item_drop_location__slimecores => TgState.Current.obj_machinery_processor_slime__adjust_item_drop_location__slimecores ??= subtypesof(typeof(obj_item_slime_extract)) as DmList;

    // tg: code/modules/food_and_drinks/machinery/processor.dm:233
    public override object adjust_item_drop_location(atom_movable atom_to_drop = null)
    {
        DmList slimecores = adjust_item_drop_location__slimecores;
        float i = 0;
        if (!Dm.truthy(i = Dm.num(slimecores.Find(atom_to_drop.type)))) // If the item is not found
            return null;
        if (i <= 16) // If in the first 12 slots
        {
            atom_to_drop.pixel_x = atom_to_drop.base_pixel_x - 12 + (Dm.num(Dm.mod(i, 4)) * 8);
            atom_to_drop.pixel_y = atom_to_drop.base_pixel_y - 12 + (round(i / 4) * 8);
            return i;
        }
        float ii = i - 16;
        atom_to_drop.pixel_x = atom_to_drop.base_pixel_x - 8 + (Dm.num(Dm.mod(ii, 3)) * 8);
        atom_to_drop.pixel_y = atom_to_drop.base_pixel_y - 8 + (round(ii / 3) * 8);
        return i;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:247
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(processing))
            return null;
        DmList picked_slimes = null;
        /// We pick up a number of slimes equal to the rating of the matter bin
        float slimes_picked = 0;
        foreach (mob_living_basic_slime slime in Dm.each<mob_living_basic_slime>(range(1, this)))
        {
            if (!Dm.truthy(Waiting.NoSleep(slime.IsReachableBy(this)))) //don't take slimes behind glass panes or somesuch; also makes it ignore slimes inside the processor
                continue;
            if (IS_UNCONSCIOUS_OR_CRIT(slime))
            {
                datum_food_processor_process recipe = PROCESSOR_SELECT_RECIPE(slime);
                if (recipe == null)
                    continue;
                LAZYADD(ref picked_slimes, slime);
                slimes_picked += 1;
            }
            if (slimes_picked >= rating_amount)
                break;
        }
        if (!Dm.truthy(LAZYLEN(picked_slimes)))
            return null;
        visible_message(span_notice(I18n.T("[] [] sucked into [].", Dm.jointext(picked_slimes, ", "), LAZYLEN(picked_slimes) > 1 ? I18n.T("are") : I18n.T("is"), this)));
        foreach (mob_living_basic_slime slime_to_add in Dm.each<mob_living_basic_slime>(picked_slimes))
        {
            LAZYADD(ref processor_contents, slime_to_add);
            Waiting.NoSleep(slime_to_add.forceMove(this));
        }
        return null;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:271
    public override async DmTask process_food(datum_food_processor_process recipe = null, atom_movable what = null)
    {
        mob_living_basic_slime processed_slime = what as mob_living_basic_slime;
        if (processed_slime == null)
            return;

        if (processed_slime.stat != DEAD)
        {
            await processed_slime.forceMove(drop_location() as atom);
            processed_slime.balloon_alert_to_viewers(I18n.T("crawls free"));
            return;
        }

        float core_count = processed_slime.cores;
        float extra_cores = rating_amount - 1; // 0-3 bonus cores above what slime already has with upgraded parts
        for (float i = 1, i__end = core_count + extra_cores; i <= i__end; i++)
        {
            atom_movable item = Dm.@new(processed_slime.slime_type.core_type, drop_location()) as atom_movable;
            adjust_item_drop_location(item);
            SSblackbox.record_feedback("tally", "slime_core_harvested", 1, processed_slime.slime_type.colour);
        }
        await base.process_food(recipe, what);
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/food_and_drinks/machinery/processor.dm:27  /obj/machinery/processor's var/static/list/processor_inputs
    internal DmList obj_machinery_processor__processor_inputs;
    // tg: code/modules/food_and_drinks/machinery/processor.dm:234  /obj/machinery/processor/slime/adjust_item_drop_location's var/static/list/slimecores
    internal DmList obj_machinery_processor_slime__adjust_item_drop_location__slimecores;
}

// tg: code/modules/food_and_drinks/machinery/processor.dm:289
public partial class obj_item_circuit_component_slime_processor : obj_item_circuit_component
{
    // tg: code/modules/food_and_drinks/machinery/processor.dm:301
    public override async DmTask populate_ports()
    {
        active = add_input_port("Activate", PORT_TYPE_SIGNAL, trigger: nameof(activate)) as datum_port_input;
        amount = add_output_port("Amount", PORT_TYPE_NUMBER) as datum_port_output;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:305
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_processor_slime)))
            attached_processor = shell as obj_machinery_processor_slime;
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:310
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_processor = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:314
    [SignalHandler]
    public virtual void activate()
    {
        input_received();
        if (Dm.truthy(attached_processor.processing))
            return;
        if (!Dm.truthy(LAZYLEN(attached_processor.processor_contents)))
            return;
        Waiting.NoSleep(attached_processor.processing__proc());
    }

    // tg: code/modules/food_and_drinks/machinery/processor.dm:322
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        DmList contents = attached_processor.processor_contents;
        amount.set_output(LAZYLEN(contents));
        return null;
    }
}
