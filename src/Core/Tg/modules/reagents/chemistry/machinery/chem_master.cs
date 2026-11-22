// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_master.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5): the ChemMaster 3000 and the CondiMaster 3000 with their buffer, the tubes, pills, patches, bottles
// and condiments they print, and the ChemMaster window. Its screentips (add_context) are a live-partial row (D264); the
// lightmask's glow is DV12's (emissive_appearance). The var block is generated but selected_container (V2 typed it as the
// container class: tg stores the selected container's typepath there).

// tg: code/modules/reagents/chemistry/machinery/chem_master.dm:3
public partial class obj_machinery_chem_master : obj_machinery
{
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:1  #define MAX_CONTAINER_PRINT_AMOUNT 50 (file-local)
    private const float MAX_CONTAINER_PRINT_AMOUNT = 50;

    /// Selected printable container type
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:24  var/obj/item/reagent_containers/selected_container
    // (hand-declared: a typepath, never an instance)
    public Type selected_container;

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:3  (the type's defaults: a hand-declared var leaves the
    // constructor to the hand-written part, types-converter.md 3.2)
    public obj_machinery_chem_master()
    {
        name = Dm.text("\\improper ChemMaster 3000");
        desc = "Used to separate chemicals and distribute them in a variety of forms.";
        icon = Dm.resource("icons/obj/medical/chemical.dmi");
        icon_state = "chemmaster";
        base_icon_state = "chemmaster";
        density = 1;
        idle_power_usage = 20;
        active_power_usage = 200;
        resistance_flags = 34;
        circuit = typeof(obj_item_circuitboard_machine_chem_master);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:38
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        create_reagents(100);

        printable_containers = load_printable_containers() as DmList;
        default_container = ((DmList)printable_containers[printable_containers[1]])[1] as Type;
        selected_container = default_container;
        pill_layers = Dm.initial(typeof(obj_item_reagent_containers_applicator_pill), (obj_item_reagent_containers_applicator_pill p) => p.layers_remaining);

        register_context();

        object dot = base.Initialize(mapload, __extra);

        // GAP(types): the board instance is circuit_value (_machinery.cs)
        obj_item_circuitboard_machine_chem_master board = circuit_value as obj_item_circuitboard_machine_chem_master;
        board.build_path = type;
        board.name = name;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:54
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:83
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
        {
            Dm.append(dot, span_notice(I18n.T("The status display reads:<br>Reagent buffer capacity: <b>[]</b> units.<br>Printing speed: <b>[]%</b>.", (reagents as datum_reagents).maximum_volume, 0.75f * SECONDS / printing_speed * 100)));
            if (!QDELETED(beaker))
            {
                Dm.append(dot, span_notice(I18n.T("[] of <b>[]u</b> capacity inserted.", beaker, (beaker.reagents as datum_reagents).maximum_volume)));
                Dm.append(dot, span_notice(I18n.T("Right click with empty hand to remove beaker.")));
            }
            else
                Dm.append(dot, span_warning(I18n.T("Missing input beaker.")));

            Dm.append(dot, span_notice(I18n.T("It can be [] [].", EXAMINE_HINT("wrenched"), Dm.truthy(anchored) ? "loose" : "in place")));
            Dm.append(dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT("screwed"), Dm.truthy(panel_open) ? "close" : "open")));
            if (Dm.truthy(panel_open))
                Dm.append(dot, span_notice(I18n.T("The machine can be [] apart.", EXAMINE_HINT("pried"))));
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:98
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (Dm.truthy(panel_open) || !Dm.truthy(is_operational))
            Waiting.NoSleep(set_light(0));
        else
            Waiting.NoSleep(set_light(1, 1, "#fffb00"));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:105
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (beaker != null)
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_overlay_container"));
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_overlay_broken"));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_overlay_panel"));

        if (Dm.truthy(is_printing))
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_overlay_extruder_active"));
        else
            Dm.append(ref dot, mutable_appearance(icon, base_icon_state + "_overlay_extruder"));

        // Screen overlay
        if (!Dm.truthy(panel_open) && Dm.truthy(is_operational))
        {
            string screen_overlay = base_icon_state + "_overlay_screen";
            if (Dm.truthy(is_printing))
                screen_overlay += "_active";
            else if ((reagents as datum_reagents).total_volume > 0)
                screen_overlay += "_main";
            Dm.append(ref dot, mutable_appearance(icon, screen_overlay));
            // DEVIATION(DV12): . += emissive_appearance(icon, base_icon_state + "_overlay_lightmask", src, alpha = src.alpha), the
            // screen's glow on the emissive plane (the machine's light, set_light above, still lights the room)
        }

        // Buffer reagents overlay
        if (Dm.truthy((reagents as datum_reagents).total_volume))
        {
            // var/static/list/fill_icon_thresholds (rules 10.6: an immutable list, rebuilt per call)
            DmList fill_icon_thresholds = Dm.list(10, 20, 30, 40, 50, 60, 70, 80, 90, 100);
            mutable_appearance filling = reagent_threshold_overlay(reagents as datum_reagents, Dm.resource("icons/obj/medical/reagent_fillings.dmi"), "chemmaster", fill_icon_thresholds) as mutable_appearance;
            if (filling != null)
                Dm.append(ref dot, filling);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:136
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

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:142
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (!Dm.truthy(is_operational))
            is_printing = FALSE;
        update_appearance(UPDATE_OVERLAYS);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:147
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        datum_reagents buffer = reagents as datum_reagents;
        buffer.maximum_volume = 0;
        foreach (obj_item_reagent_containers_cup_beaker part_beaker in Dm.each<obj_item_reagent_containers_cup_beaker>(component_parts))
            buffer.maximum_volume += (part_beaker.reagents as datum_reagents).maximum_volume;

        //Servo tier determines printing speed
        printing_speed = 1 * SECONDS;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            printing_speed -= Dm.num(servo.tier) * 0.25f * SECONDS;
        printing_speed = Dm.max(printing_speed, 0.25f * SECONDS);
        return dot;
    }

    ///Return a map of category->list of containers this machine can print
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:160
    public virtual object load_printable_containers()
    {
        // var/static/list/containers (rules 10.6)
        ref DmList containers = ref TgState.Current.obj_machinery_chem_master__load_printable_containers__containers;
        if (!Dm.truthy(length(containers)))
        {
            containers = Dm.list(
                Dm.kv(CAT_TUBES, GLOB.reagent_containers[CAT_TUBES]),
                Dm.kv(CAT_PILLS, GLOB.reagent_containers[CAT_PILLS]),
                Dm.kv(CAT_PATCHES, GLOB.reagent_containers[CAT_PATCHES]));
        }
        return containers;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:173
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await tool.can_insert_container(user, this)))
            return NONE;
        if (!Dm.truthy(await replace_beaker(user, tool as obj_item_reagent_containers)))
            return ITEM_INTERACT_BLOCKING;

        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:182
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(is_printing))
        {
            balloon_alert(user, "still printing!");
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:191
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(is_printing))
        {
            balloon_alert(user, "still printing!");
            return ITEM_INTERACT_BLOCKING;
        }

        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:198
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(is_printing))
        {
            balloon_alert(user, "still printing!");
            return ITEM_INTERACT_BLOCKING;
        }

        return await default_deconstruction_crowbar(user, tool);
    }

    /**
     * Insert, remove, replace the existig beaker. Returns TRUE on success.
     * Arguments
     *
     * * mob/living/user - the player trying to replace the beaker
     * * obj/item/reagent_containers/new_beaker - the beaker we are trying to insert, swap with existing or remove if null
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:212
    public virtual async DmTask<object> replace_beaker(mob_living user = null, obj_item_reagent_containers new_beaker = null)
    {
        if (!QDELETED(beaker))
            await try_put_in_hand(beaker, user);
        if (!QDELETED(new_beaker))
        {
            if (!Dm.truthy(await user.transferItemToLoc(new_beaker, this)))
            {
                update_appearance(UPDATE_OVERLAYS);
                return FALSE;
            }
            beaker = new_beaker;
        }

        update_appearance(UPDATE_OVERLAYS);

        return TRUE;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:227
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return dot;
        await replace_beaker(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:236  (a new slot: the /atom root, _onclick/cyborg.dm, is not declared)
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:239  (a new slot: the /atom root, _onclick/ai.dm, is not declared)
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:242
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemMaster", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:248
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["maxPrintable"] = MAX_CONTAINER_PRINT_AMOUNT;
        data["maxPillDuration"] = PILL_MAX_PRINTABLE_LAYERS;
        data["categories"] = Dm.list();
        foreach (object category in Dm.each(printable_containers))
        {
            //make the category
            DmList category_list = Dm.list(
                Dm.kv("name", category),
                Dm.kv("containers", Dm.list()));

            //add containers to this category
            foreach (Type container in Dm.each_cast<Type>(printable_containers[category]))
            {
                Dm.append((DmList)category_list["containers"], Dm.list(Dm.list(
                    Dm.kv("ref", REF(container)),
                    Dm.kv("name", Dm.initial(container, (obj_item_reagent_containers c) => c.name)),
                    Dm.kv("icon", Dm.initial(container, (obj_item_reagent_containers c) => c.icon)),
                    Dm.kv("icon_state", Dm.initial(container, (obj_item_reagent_containers c) => c.icon_state)),
                    Dm.kv("volume", Dm.initial(container, (obj_item_reagent_containers c) => c.volume)))));
            }

            //add the category
            Dm.append((DmList)data["categories"], Dm.list(category_list));
        }

        await DmTask.CompletedTask;
        return data;
    }

    /// One reagent's row of the beaker's and the buffer's contents (tg writes it out at both sites, chem_master.dm:296 and :318)
    private DmList reagent_row(datum_reagent reagent) => Dm.list(
        Dm.kv("ref", Dm.text("[]", reagent.type)),
        Dm.kv("name", reagent.name),
        Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING)),
        Dm.kv("pH", reagent.ph),
        Dm.kv("color", reagent.color),
        Dm.kv("description", reagent.description),
        Dm.kv("purity", reagent.purity),
        Dm.kv("metaRate", reagent.metabolization_rate),
        Dm.kv("overdose", reagent.overdose_threshold),
        Dm.kv("addictionTypes", (reagents as datum_reagents).parse_addictions(reagent)));

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:276
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        datum_reagents buffer = reagents as datum_reagents;

        //printing statictics
        dot["isPrinting"] = is_printing;
        dot["printingProgress"] = printing_progress;
        dot["printingTotal"] = printing_total;
        dot["selectedPillDuration"] = pill_layers;
        obj_item held_item = user.get_active_held_item() as obj_item;
        dot["hasBeakerInHand"] = held_item?.is_chem_container() is var held && Dm.truthy(held) ? held : FALSE;
        dot["customTransferAmount"] = custom_transfer_amount;

        //contents of source beaker
        DmList beaker_data = null;
        if (!QDELETED(beaker))
        {
            datum_reagents beaker_reagents = beaker.reagents as datum_reagents;
            beaker_data = Dm.list();
            beaker_data["maxVolume"] = beaker.volume;
            beaker_data["currentVolume"] = beaker_reagents.total_volume;
            DmList beakerContents = Dm.list();
            if (Dm.truthy(length(beaker_reagents.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(beaker_reagents.reagent_list))
                    Dm.append(beakerContents, Dm.list(reagent_row(reagent)));
            }
            beaker_data["contents"] = beakerContents;
        }
        dot["beaker"] = beaker_data;

        //contents of buffer
        beaker_data = Dm.list();
        beaker_data["maxVolume"] = buffer.maximum_volume;
        beaker_data["currentVolume"] = buffer.total_volume;
        DmList bufferContents = Dm.list();
        if (Dm.truthy(length(buffer.reagent_list)))
        {
            foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(buffer.reagent_list))
                Dm.append(bufferContents, Dm.list(reagent_row(reagent)));
        }
        beaker_data["contents"] = bufferContents;
        dot["buffer"] = beaker_data;

        //is transfering or destroying reagents. applied only for buffer
        dot["isTransfering"] = is_transfering;

        //container along with the suggested type
        object suggested_container = default_container;
        if (buffer.total_volume > 0)
        {
            datum_reagent master_reagent = buffer.get_master_reagent() as datum_reagent;
            float container_found = FALSE;
            suggested_container = master_reagent.default_container;
            foreach (object category in Dm.each(printable_containers))
            {
                foreach (Type container in Dm.each_cast<Type>(printable_containers[category]))
                {
                    if (Dm.eq(container, suggested_container))
                    {
                        suggested_container = REF(container);
                        container_found = TRUE;
                        break;
                    }
                }
            }
            if (!Dm.truthy(container_found))
                suggested_container = REF(default_container);
        }
        dot["suggestedContainerRef"] = suggested_container;

        //selected container
        dot["selectedContainerRef"] = REF(selected_container);
        dot["selectedContainerVolume"] = Dm.initial(selected_container, (obj_item_reagent_containers c) => c.volume);

        foreach (object category in Dm.each(printable_containers))
        {
            if (Dm.@in(selected_container, printable_containers[category]))
            {
                dot["selectedContainerCategory"] = category;
                break;
            }
        }
        await DmTask.CompletedTask;
        return dot;
    }

    /**
     * Transfers a single reagent between buffer & beaker
     * Arguments
     *
     * * datum/reagents/source - the holder we are transferring from
     * * datum/reagents/target - the holder we are transferring to
     * * datum/reagent/path - the reagent typepath we are transfering
     * * amount - volume to transfer
     * * do_transfer - transfer the reagents else destroy them
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:372
    // path is object (Appendix A, gaps P5): ui_act passes the reagent's typepath as text (params["reagentRef"]), which text2path reads
    public virtual async DmTask<object> transfer_reagent(datum_reagents source = null, datum_reagents target = null, object path = null, object amount = null, object do_transfer = null)
    {
        //sanity checks for transfer amount
        if (amount == null || Dm.num(amount) <= 0)
            return FALSE;
        //sanity checks for reagent path
        Type reagent = text2path(path as string);
        if (reagent == null)
            return FALSE;

        //use energy
        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
            return FALSE;

        //do the operation
        object dot = FALSE;
        if (Dm.truthy(do_transfer))
        {
            if (Dm.truthy(target.is_reacting))
                return FALSE;
            if (Dm.truthy(await source.trans_to(target, Dm.num(amount), target_id: reagent)))
                dot = TRUE;
        }
        else if (Dm.truthy(await source.remove_reagent(reagent, amount)))
            dot = TRUE;
        if (Dm.truthy(dot) && !QDELETED(this)) //transferring volatile reagents can cause a explosion & destory us
            update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:399
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "eject":
                if (Dm.truthy(is_printing))
                {
                    await say("The buffer is locked while printing.");
                    return null;
                }

                await replace_beaker(ui.user as mob_living);
                return TRUE;

            case "insert":
            {
                obj_item_reagent_containers container = ui.user.get_active_held_item() as obj_item_reagent_containers;
                if (container != null && Dm.truthy(await container.can_insert_container(ui.user as mob_living, this)))
                    await replace_beaker(ui.user as mob_living, container);

                return TRUE;
            }

            case "setCustomTransfer":
            {
                object target = @params["target"];
                if (target == null)
                    return FALSE;

                float? number = text2num(target);
                if (number == null)
                    return FALSE;

                custom_transfer_amount = Dm.clamp(number.Value, 0, Dm.num(beaker.volume));
                return TRUE;
            }

            case "transfer":
            {
                if (Dm.truthy(is_printing))
                {
                    await say("The buffer is locked while printing.");
                    return null;
                }

                object reagent_ref = @params["reagentRef"];
                object amount = @params["amount"];
                object target = @params["target"];

                if (Dm.eq(amount, -1)) // Set custom amount
                {
                    mob user = ui.user; //Hold a reference of the user if the UI is closed
                    amount = round(Dm.num(await tgui_input_number(user, "Enter amount to transfer", "Transfer amount", round_value: FALSE)), CHEMICAL_VOLUME_ROUNDING);
                    if (!Dm.truthy(amount) || !Dm.truthy(await user.can_perform_action(this)))
                        return FALSE;
                }

                float should_transfer = Dm.truthy(is_transfering) || Dm.eq(target, "buffer") ? TRUE : FALSE; // we should always transfer if target is the buffer
                if (Dm.truthy(should_transfer) && beaker == null) // if there's no beaker, we cannot transfer
                {
                    await say("No reagent container is inserted.");
                    return FALSE;
                }

                datum_reagents reagents_from = null;
                datum_reagents reagents_to = null;
                if (Dm.eq(target, "buffer"))
                {
                    reagents_from = beaker.reagents as datum_reagents;
                    reagents_to = reagents as datum_reagents; // buffer
                }
                else if (Dm.eq(target, "beaker"))
                {
                    reagents_from = reagents as datum_reagents; // buffer
                    if (Dm.truthy(should_transfer))
                        reagents_to = beaker.reagents as datum_reagents;
                }
                return await transfer_reagent(reagents_from, reagents_to, reagent_ref, amount, should_transfer);
            }

            case "toggleTransferMode":
                is_transfering = Dm.truthy(is_transfering) ? FALSE : TRUE;
                return TRUE;

            case "stopPrinting":
                is_printing = FALSE;
                update_appearance(UPDATE_OVERLAYS);
                return TRUE;

            case "setPillDuration":
                pill_layers = Dm.clamp(Dm.num(@params["duration"]), 0, PILL_MAX_PRINTABLE_LAYERS);
                return TRUE;

            case "selectContainer":
            {
                object target = Dm.locate_ref(@params["ref"]);

                //is this even a valid type path
                if (!ispath(target))
                    return FALSE;

                //are we printing a valid container
                float container_found = FALSE;
                foreach (object category in Dm.each(printable_containers))
                {
                    //container found in previous iteration
                    if (Dm.truthy(container_found))
                        break;

                    //find for matching typepath
                    foreach (Type container in Dm.each_cast<Type>(printable_containers[category]))
                    {
                        if (Dm.eq(target, container))
                        {
                            container_found = TRUE;
                            break;
                        }
                    }
                }
                if (!Dm.truthy(container_found))
                    return FALSE;

                //set the container
                selected_container = target as Type;
                return TRUE;
            }

            case "create":
            {
                datum_reagents buffer = reagents as datum_reagents;
                if (!Dm.truthy(buffer.total_volume) || Dm.truthy(is_printing))
                    return FALSE;

                //validate print count
                object item_count_param = @params["itemCount"];
                if (item_count_param == null)
                    return FALSE;
                float? item_count = text2num(item_count_param);
                if (item_count == null || item_count <= 0)
                    return FALSE;
                item_count = Dm.min(item_count.Value, MAX_CONTAINER_PRINT_AMOUNT);
                float volume_in_each = Dm.min(round(buffer.total_volume / item_count.Value, CHEMICAL_VOLUME_ROUNDING), Dm.num(Dm.initial(selected_container, (obj_item_reagent_containers c) => c.volume)));

                // Generate item name
                object item_name_default = Dm.initial(selected_container, (obj_item_reagent_containers c) => c.name);
                datum_reagent master_reagent = buffer.get_master_reagent() as datum_reagent;
                if (selected_container == default_container) // Tubes and bottles gain reagent name
                    item_name_default = Dm.text("[] []", master_reagent.name, item_name_default);
                if (!Dm.truthy(Dm.bitand(Dm.initial(selected_container, (obj_item_reagent_containers c) => c.initial_reagent_flags), OPENCONTAINER))) // Closed containers get both reagent name and units in the name
                    item_name_default = Dm.text("[] [] ([]u)", master_reagent.name, item_name_default, volume_in_each);
                object item_name = await tgui_input_text(
                    usr,
                    "Container name",
                    "Name",
                    item_name_default,
                    max_length: MAX_NAME_LEN);

                if (!Dm.truthy(item_name) || Dm.truthy(is_printing))
                    return FALSE;

                //start printing
                is_printing = TRUE;
                printing_progress = 0f;
                printing_total = item_count;
                update_appearance(UPDATE_OVERLAYS);
                await create_containers(ui.user, item_count, item_name, volume_in_each, selected_container);
                return TRUE;
            }
        }
        return dot;
    }

    /**
     * Create N selected containers with reagents from buffer split between them
     * Arguments
     *
     * * mob/user - the player printing these containers
     * * item_count - number of containers to print
     * * item_name - the name for each container printed
     * * volume_in_each - volume in each container created
     * * chosen_container - type of the container we're going to print
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:552
    public virtual async DmTask create_containers(mob user = null, object item_count = null, object item_name = null, object volume_in_each = null, object chosen_container = null)
    {
        //lost power or manually stopped
        if (!Dm.truthy(is_printing))
            return;

        //use power
        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            is_printing = FALSE;
            update_appearance(UPDATE_OVERLAYS);
            return;
        }

        //print the stuff
        obj_item_reagent_containers item = Dm.@new((Type)chosen_container, drop_location()) as obj_item_reagent_containers;
        adjust_item_drop_location(item);
        item.name = Dm.str(item_name);
        await (item.reagents as datum_reagents).clear_reagents();
        await (reagents as datum_reagents).trans_to(item, Dm.num(volume_in_each), transferred_by: user);
        if (istype(item, typeof(obj_item_reagent_containers_applicator_pill)))
        {
            obj_item_reagent_containers_applicator_pill pill = item as obj_item_reagent_containers_applicator_pill;
            pill.layers_remaining = pill_layers;
        }
        printing_progress = Dm.num(printing_progress) + 1;
        update_appearance(UPDATE_OVERLAYS);

        //print more items
        item_count = Dm.num(item_count) - 1;
        if (Dm.num(item_count) > 0)
            _addtimer(Dm.@new<datum_callback>(this, nameof(create_containers), user, item_count, item_name, volume_in_each, chosen_container), printing_speed, file: "code/modules/reagents/chemistry/machinery/chem_master.dm", line: 581);
        else
        {
            is_printing = FALSE;
            update_appearance(UPDATE_OVERLAYS);
        }
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_master.dm:586
public partial class obj_machinery_chem_master_condimaster : obj_machinery_chem_master
{
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:590
    public override object load_printable_containers()
    {
        // var/static/list/containers (rules 10.6)
        ref DmList containers = ref TgState.Current.obj_machinery_chem_master_condimaster__load_printable_containers__containers;
        if (!Dm.truthy(length(containers)))
            containers = Dm.list(Dm.kv(CAT_CONDIMENTS, GLOB.reagent_containers[CAT_CONDIMENTS]));
        return containers;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:164  load_printable_containers' var/static/list/containers
    internal DmList obj_machinery_chem_master__load_printable_containers__containers;

    // tg: code/modules/reagents/chemistry/machinery/chem_master.dm:591  the CondiMaster's own var/static/list/containers
    internal DmList obj_machinery_chem_master_condimaster__load_printable_containers__containers;
}
