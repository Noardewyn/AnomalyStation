// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/reagentgrinder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the all-in-one grinder with its radial (grind, juice, mix, eject, the AI's examine), the blender and
// juicer sounds and the shake while it works. Its screentips (add_context) are a live-partial row (D264). The var block is
// generated; the radial's var/static images are per-world state (rules 10.6).

// tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:2
public partial class obj_machinery_reagentgrinder : obj_machinery
{
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(mapload))
            beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(this);

        register_context();
        update_appearance(UPDATE_OVERLAYS);

        RegisterSignal(this, COMSIG_STORAGE_DUMP_CONTENT, nameof(on_storage_dump));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:34
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:38
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        if (!QDELETED(beaker)) // tg returns while the beaker is NOT deleted, so the beaker is never thrown (tg's own inverted check)
            return;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, beaker);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, beaker);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, beaker);
                break;
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:81
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (!Dm.truthy(in_range(user, this)) && !issilicon(user) && !isobserver(user))
        {
            Dm.append(dot, span_warning(I18n.T("You're too far away to examine []'s contents and display!", this)));
            return dot;
        }

        float total_weight = 0;
        DmList to_process = Dm.list();
        foreach (obj_item target in Dm.each<obj_item>(this))
        {
            if (Dm.@in(target, component_parts) || target == beaker)
                continue;
            float amount = 1;
            if (isstack(target))
            {
                obj_item_stack target_stack = target as obj_item_stack;
                amount = Dm.num(target_stack.amount);
            }
            string key = Dm.text("[]", target.name);
            to_process[key] = Dm.num(to_process[key]) + amount;
            total_weight += Dm.num(target.w_class);
        }
        if (Dm.truthy(to_process.len))
        {
            Dm.append(dot, span_notice(I18n.T("Currently holding:")));
            foreach (object target_name in Dm.each(to_process))
                Dm.append(dot, span_notice(I18n.T("[] []", to_process[target_name], target_name)));
            Dm.append(dot, span_notice(I18n.T("Filled to <b>[]%</b> capacity.", round((total_weight / Dm.num(maximum_weight)) * 100))));
        }

        if (!QDELETED(beaker))
        {
            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            Dm.append(dot, span_notice(I18n.T("A beaker of <b>[]u</b> capacity is present. Contains:", beaker_reagents.maximum_volume)));
            if (Dm.truthy(beaker_reagents.total_volume))
            {
                foreach (datum_reagent reg in Dm.each_cast<datum_reagent>(beaker_reagents.reagent_list))
                    Dm.append(dot, span_notice(I18n.T("[]u of []", round(Dm.num(reg.volume), CHEMICAL_VOLUME_ROUNDING), reg.name)));
            }
            else
                Dm.append(dot, span_notice(I18n.T("Nothing.")));
            Dm.append(dot, span_notice(I18n.T("[] with empty hand to remove beaker.", EXAMINE_HINT("Right click"))));
        }
        else
            Dm.append(dot, span_warning(I18n.T("It's missing a beaker.")));

        Dm.append(dot, span_notice(I18n.T("You can drag a storage item to dump its contents in the grinder.")));
        if (Dm.truthy(anchored))
            Dm.append(dot, span_notice(I18n.T("It can be [] loose.", EXAMINE_HINT("wrenched"))));
        else
            Dm.append(dot, span_warning(I18n.T("Needs to be [] in place to work.", EXAMINE_HINT("wrenched"))));
        Dm.append(dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "closed" : "open")));
        if (Dm.truthy(panel_open))
            Dm.append(dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT("pried"))));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:124
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (!QDELETED(beaker))
            Dm.append(ref dot, Dm.text("[]-beaker", base_icon_state));

        if (Dm.truthy(anchored) && !Dm.truthy(panel_open) && Dm.truthy(is_operational))
            Dm.append(ref dot, Dm.text("[]-on", base_icon_state));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:133
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
        {
            beaker = null;
            update_appearance(UPDATE_OVERLAYS);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:139
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();

        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
        {
            if (istype(part, typeof(datum_stock_part_servo)))
                speed = Dm.num(part.tier);
            else if (istype(part, typeof(datum_stock_part_matter_bin)))
                maximum_weight = WEIGHT_CLASS_GIGANTIC * Dm.num(part.tier);
        }
        return dot;
    }

    /**
     * Inserts, removes or replaces the beaker present
     * Arguments
     *
     * * mob/living/user - the player performing the action
     * * obj/item/reagent_containers/new_beaker - the new beaker to replace the old, null to do nothing
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:154
    public virtual async DmTask<object> replace_beaker(mob_living user = null, obj_item_reagent_containers new_beaker = null)
    {
        if (!QDELETED(beaker))
            await try_put_in_hand(beaker, user);

        if (!QDELETED(new_beaker))
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this)))
                return null;
            beaker = new_beaker;
        }

        update_appearance(UPDATE_OVERLAYS);
        return null;
    }

    /**
     * Transfer this item or items inside if its a bag into the grinder
     * Arguments
     *
     * * mob/user - the player who is inserting these items
     * * list/obj/item/to_add - list of items to add
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:174
    public virtual async DmTask<object> load_items(mob user = null, DmList to_add = null)
    {
        //surface level checks to filter out items that can be grinded/juice
        DmList filtered_list = Dm.list();
        foreach (obj_item ingredient in Dm.each_cast<obj_item>(to_add))
        {
            // What are we trying to grind exactly?
            if (Dm.truthy(Dm.bitand(ingredient.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(ingredient.flags_1, HOLOGRAM_1)))
                continue;

            // Nothing would come from grinding or juicing
            if (!Dm.truthy(length(await ingredient.grind_results())) && !Dm.truthy((ingredient.reagents as datum_reagents)?.total_volume))
            {
                to_chat(user, span_warning(I18n.T("You cannot grind/juice [] into reagents!", ingredient)));
                continue;
            }

            // Error messages should be in the objects' definitions
            if (!Dm.truthy(ingredient.blend_requirements(this, user as mob_living)))
                continue;

            Dm.append(filtered_list, ingredient);
        }

        if (!Dm.truthy(length(filtered_list)))
            return FALSE;

        //find total weight of all items already in grinder
        float total_weight = 0;
        foreach (obj_item to_process in Dm.each<obj_item>(this))
        {
            if (Dm.@in(to_process, component_parts) || to_process == beaker)
                continue;
            total_weight += Dm.num(to_process.w_class);
        }

        //Now transfer the items 1 at a time while ensuring we don't go above the maximum allowed weight
        float items_transfered = 0;
        foreach (obj_item weapon in Dm.each_cast<obj_item>(filtered_list))
        {
            if (Dm.num(weapon.w_class) + total_weight > Dm.num(maximum_weight))
            {
                to_chat(user, span_warning(I18n.T("[] is too big to fit into [].", weapon, this)));
                continue;
            }

            //try to remove the right way
            if (!Dm.truthy(await user.transferItemToLoc(weapon, this)))
                continue;

            total_weight += Dm.num(weapon.w_class);
            items_transfered += 1;
            to_chat(user, span_notice(I18n.T("[] was loaded into [].", weapon, this)));
        }

        return items_transfered;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:222
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode) && !is_reagent_container(tool) && !Dm.truthy(tool.is_open_container()) || Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(tool.flags_1, HOLOGRAM_1)))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        //add the beaker
        if (is_reagent_container(tool) && Dm.truthy(tool.is_open_container()))
        {
            await replace_beaker(user, tool as obj_item_reagent_containers);
            to_chat(user, span_notice(I18n.T("You add [] to [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        //add items from bag
        else if (istype(tool, typeof(obj_item_storage_bag)))
        {
            DmList to_add = Dm.list();
            //list of acceptable items from the bag
            // var/static/list/accepted_items (rules 10.6: an immutable type list, rebuilt per call)
            DmList accepted_items = Dm.list(
                typeof(obj_item_grown),
                typeof(obj_item_food_grown),
                typeof(obj_item_food_honeycomb));

            //add to list of items to check for
            foreach (obj_item ingredient in Dm.each<obj_item>(tool))
            {
                if (!Dm.truthy(is_type_in_list(ingredient, accepted_items)))
                    continue;
                Dm.append(to_add, ingredient);
            }

            //add the items
            object items_added = await load_items(user, to_add);
            if (!Dm.truthy(items_added))
            {
                to_chat(user, span_warning(I18n.T("No items were added.")));
                return ITEM_INTERACT_BLOCKING;
            }
            to_chat(user, span_notice(I18n.T("[] items were added from [] to [].", items_added, tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        //add item directly
        else if (Dm.truthy(length(await tool.grind_results())) || Dm.truthy((tool.reagents as datum_reagents)?.total_volume))
        {
            if (tool.atom_storage != null && Dm.truthy(length(tool.contents))) //anything that has internal storage would be too much recursion for us to handle
            {
                to_chat(user, span_notice(I18n.T("Drag this item onto [] to dump its contents, or empty it to grind the container.", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            //add the items
            if (!Dm.truthy(await load_items(user, Dm.list(tool))))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("[] was added to [].", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        //ask player to drag stuff into grinder
        else if (tool.atom_storage != null)
        {
            to_chat(user, span_warning(I18n.T("You must drag & dump contents of [] into [].", tool, this)));
            return ITEM_INTERACT_BLOCKING;
        }

        return NONE;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:275
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(operating))
        {
            balloon_alert(user, "still operating!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
        {
            update_appearance(UPDATE_OVERLAYS);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:286
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(operating))
        {
            balloon_alert(user, "still operating!");
            return ITEM_INTERACT_BLOCKING;
        }

        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:293
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(operating))
        {
            balloon_alert(user, "still operating!");
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:300
    public virtual object on_storage_dump(datum source = null, datum_storage storage = null, mob user = null)
    {
        //SIGNAL_HANDLER
        DmList contents_to_dump = Dm.list();
        foreach (obj_item to_dump in Dm.each<obj_item>(storage.real_location))
        {
            if (to_dump.atom_storage != null) //No recursive handling of contents please
                continue;
            Dm.append(contents_to_dump, to_dump);
        }

        to_chat(user, span_notice(I18n.T("You dumped [] items from [] into [].", Waiting.NoSleep(load_items(user, contents_to_dump)), storage.parent, this)));

        return STORAGE_DUMP_HANDLED;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:313
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || !Dm.truthy(await check_interactable(user)))
            return dot;
        await replace_beaker(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:320  (a new slot: the /atom root, _onclick/cyborg.dm, is not declared)
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:323  (a new slot: the /atom root, _onclick/ai.dm, is not declared)
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:326  ui_interact's var/static radial images (rules 10.6)
    private static ref object ui_interact__radial_eject => ref TgState.Current.obj_machinery_reagentgrinder__ui_interact__radial_eject;
    private static ref object ui_interact__radial_grind => ref TgState.Current.obj_machinery_reagentgrinder__ui_interact__radial_grind;
    private static ref object ui_interact__radial_juice => ref TgState.Current.obj_machinery_reagentgrinder__ui_interact__radial_juice;
    private static ref object ui_interact__radial_mix => ref TgState.Current.obj_machinery_reagentgrinder__ui_interact__radial_mix;
    private static ref object ui_interact__radial_examine => ref TgState.Current.obj_machinery_reagentgrinder__ui_interact__radial_examine;

    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:326
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        //sanity check
        if (!Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return null;

        object radial_eject = ui_interact__radial_eject;

        //create list of options available
        DmList options = Dm.list();
        //actions to be performed on the items stored inside
        foreach (obj_item to_process in Dm.each<obj_item>(this))
        {
            if (Dm.@in(to_process, component_parts) || to_process == beaker)
                continue;

            if (Dm.truthy(is_operational) && Dm.truthy(anchored) && !QDELETED(beaker) && !Dm.truthy((beaker.reagents as datum_reagents).holder_full()))
            {
                options["grind"] = ui_interact__radial_grind;

                options["juice"] = ui_interact__radial_juice;
            }

            options["eject"] = radial_eject;
            break;
        }

        //eject action if we have a beaker
        if (!QDELETED(beaker))
        {
            options["eject"] = radial_eject;
            //mix reagents present inside
            if (Dm.truthy(is_operational) && Dm.truthy(anchored) && Dm.truthy((beaker.reagents as datum_reagents).total_volume))
                options["mix"] = ui_interact__radial_mix;
        }

        //examine action if Ai is trying to see whats up
        if (HAS_AI_ACCESS(user))
            options["examine"] = ui_interact__radial_examine;

        //display choices & perform action
        object choice = await show_radial_menu(
            user,
            this,
            options,
            custom_check: Dm.@new<datum_callback>(this, nameof(check_interactable), user),
            require_near: !HAS_SILICON_ACCESS(user) ? TRUE : FALSE);
        if (!Dm.truthy(choice))
            return null;

        //act on choice
        switch (choice)
        {
            case "eject":
                await replace_beaker(user as mob_living);
                await dump_inventory_contents();
                break;
            case "grind":
            case "juice":
                await operate_for(6 * SECONDS, Dm.eq(choice, "juice") ? TRUE : FALSE, user);
                break;
            case "mix":
                mix(5 * SECONDS, user);
                break;
            case "examine":
                to_chat(user, boxed_message(Dm.jointext(await examine(user) as DmList, "\n")));
                break;
        }
        return null;
    }

    /**
     * Checks if the radial menu can interact with this machine
     * Arguments
     *
     * * mob/user - the player interacting with this machine
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:392
    public virtual async DmTask<object> check_interactable(mob user = null)
    {
        return !Dm.truthy(operating) && Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))) ? TRUE : FALSE;
    }

    /**
     * Grinds/Juices all contents inside the grinder
     * Arguments
     *
     * * time - the duration in deciseconds to perform the operation
     * * juicing - FALSE to grind, TRUE to juice
     * * mob/user - the player who initiated this process
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:405
    public virtual async DmTask operate_for(object time = null, float? juicing = null, mob user = null)
    {
        juicing ??= FALSE;
        if (!Dm.truthy(anchored) || !Dm.truthy(is_operational) || QDELETED(beaker) || Dm.truthy((beaker.reagents as datum_reagents).holder_full()))
        {
            operating = FALSE;
            return;
        }
        operating = TRUE;

        float duration = Dm.num(time) / Dm.num(speed);

        Shake(pixelshiftx: 1, pixelshifty: 0, duration: duration);
        if (!Dm.truthy(juicing))
            playsound(this, Dm.resource("sound/machines/blender.ogg"), 50, TRUE);
        else
            playsound(this, Dm.resource("sound/machines/juicer.ogg"), 20, TRUE);

        float total_weight = 0;
        float item_weight;
        foreach (obj_item ingredient in Dm.each<obj_item>(contents))
        {
            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            if (Dm.truthy(beaker_reagents.holder_full()))
                break;
            if (Dm.@in(ingredient, component_parts) || ingredient == beaker)
                continue;

            //record item weight before & after blending
            item_weight = Dm.num(ingredient.w_class);

            if (Dm.truthy(juicing))
            {
                if (!Dm.truthy(await ingredient.juice(beaker_reagents, user)))
                {
                    to_chat(user, span_danger(I18n.T("[] shorts out as it tries to juice up [], and transfers it back to storage.", this, ingredient)));
                    continue;
                }
            }
            else if (!Dm.truthy(await ingredient.grind(beaker_reagents, user)))
            {
                if (isstack(ingredient))
                    to_chat(user, span_notice(I18n.T("[] attempts to grind as many pieces of [] as possible.", this, ingredient)));
                else
                    to_chat(user, span_danger(I18n.T("[] shorts out as it tries to grind up [], and transfers it back to storage.", this, ingredient)));
                continue;
            }

            //happens only for stacks where some of the sheets were grinded so we roughly compute the weight grinded
            if (item_weight != Dm.num(ingredient.w_class))
                total_weight += item_weight - Dm.num(ingredient.w_class);
            else
                total_weight += item_weight;
        }

        //use power according to the total weight of items grinded
        await use_energy((active_power_usage * (duration / (1 * SECONDS))) * (total_weight / Dm.num(maximum_weight)));

        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_operating)), duration, file: "code/modules/reagents/chemistry/machinery/reagentgrinder.dm", line: 452);
    }

    ///Reset the operating status of the machine
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:455
    public virtual void stop_operating()
    {
        operating = FALSE;
    }

    /**
     * Mixes the reagents inside the beaker
     * Arguments
     *
     * * time - the length of time in deciseconds to operate
     * * mob/user - the player who started the mixing process
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:467
    public virtual void mix(object time = null, mob user = null)
    {
        if (!Dm.truthy(anchored) || !Dm.truthy(is_operational) || QDELETED(beaker) || !Dm.truthy((beaker.reagents as datum_reagents).total_volume))
        {
            operating = FALSE;
            return;
        }
        operating = TRUE;

        float duration = Dm.num(time) / Dm.num(speed);

        Shake(pixelshiftx: 1, pixelshifty: 0, duration: duration);

        playsound(this, Dm.resource("sound/machines/juicer.ogg"), 20, TRUE);

        _addtimer(Dm.@new<datum_callback>(this, nameof(mix_complete), duration), duration, file: "code/modules/reagents/chemistry/machinery/reagentgrinder.dm", line: 484);
    }

    /**
     * Mix the reagents
     * Arguments
     *
     * * duration - the time spent in mixing
     */
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:489
    public virtual async DmTask mix_complete(object duration = null)
    {
        if (QDELETED(this) || !Dm.truthy(is_operational))
            return;

        datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
        //Recipe to make Butter
        float butter_amt = FLOOR(Dm.num(beaker_reagents.get_reagent_amount(typeof(datum_reagent_consumable_milk))) / MILK_TO_BUTTER_COEFF, 1);
        object purity = beaker_reagents.get_reagent_purity(typeof(datum_reagent_consumable_milk));
        await beaker_reagents.remove_reagent(typeof(datum_reagent_consumable_milk), MILK_TO_BUTTER_COEFF * butter_amt);
        for (float i = 1; i <= butter_amt; i++)
        {
            obj_item_food_butter tasty_butter = Dm.@new<obj_item_food_butter>(drop_location());
            (tasty_butter.reagents as datum_reagents).set_all_reagents_purity(Dm.num(purity));
        }

        operating = FALSE;

        if (!Dm.truthy(beaker_reagents.total_volume))
            return;

        //Recipe to make Mayonnaise
        await beaker_reagents.convert_reagent(typeof(datum_reagent_consumable_eggyolk), typeof(datum_reagent_consumable_mayonnaise));
        //Recipe to make whipped cream
        await beaker_reagents.convert_reagent(typeof(datum_reagent_consumable_cream), typeof(datum_reagent_consumable_whipped_cream));
        //power consumed based on the ratio of total reagents mixed
        await use_energy((active_power_usage * (Dm.num(duration) / (1 * SECONDS))) * (beaker_reagents.total_volume / beaker_reagents.maximum_volume));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:331  ui_interact's var/static radial_eject
    internal object obj_machinery_reagentgrinder__ui_interact__radial_eject = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_eject");
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:341  var/static radial_grind
    internal object obj_machinery_reagentgrinder__ui_interact__radial_grind = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_grind");
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:344  var/static radial_juice
    internal object obj_machinery_reagentgrinder__ui_interact__radial_juice = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_juice");
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:356  var/static radial_mix
    internal object obj_machinery_reagentgrinder__ui_interact__radial_mix = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_mix");
    // tg: code/modules/reagents/chemistry/machinery/reagentgrinder.dm:361  var/static radial_examine
    internal object obj_machinery_reagentgrinder__ui_interact__radial_examine = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_examine");
}
