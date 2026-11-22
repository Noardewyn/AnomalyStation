// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/chemistry/machinery/chem_dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P5: the chemistry machines): the chem dispenser, the soda, booze, mutagen and botanical dispensers, the
// emagged full-upgrade ones and the abductors' synthesizer, with the ChemDispenser window. The var blocks are generated (D111) but
// cell (V3 typed it Type: tg stores the machine's power cell there); the var/static lists are per-world state (rules 10.6).
// GLOB.chemical_reagents_list is not built (the holder's GAP, holder.cs): the reagent type's prototype stands in for an entry.

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:1
public partial class obj_machinery_chem_dispenser : obj_machinery
{
    /// The cell used to dispense reagents
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:14  var/obj/item/stock_parts/power_store/cell (hand-declared:
    // the generator's V3 made it a Type)
    public obj_item_stock_parts_power_store cell;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:1  (the type's defaults: a hand-declared var leaves the
    // constructor to the hand-written part, types-converter.md 3.2)
    public obj_machinery_chem_dispenser()
    {
        name = "chem dispenser";
        desc = "Creates and dispenses chemicals.";
        density = 1;
        icon = Dm.resource("icons/obj/medical/chemical.dmi");
        icon_state = "dispenser";
        base_icon_state = "dispenser";
        interaction_flags_machine = 11;
        resistance_flags = 34;
        circuit = typeof(obj_item_circuitboard_machine_chem_dispenser);
        processing_flags = 0;
    }

    /// The default list of dispensable_reagents
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:54  var/static/list/default_dispensable_reagents (rules 10.6)
    public static ref DmList default_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser__default_dispensable_reagents;
    /// The default list of reagents upgrade_reagents
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:82
    public static ref DmList default_upgrade_reagents => ref TgState.Current.obj_machinery_chem_dispenser__default_upgrade_reagents;
    /// The default list of reagents emagged_reagents
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:91
    public static ref DmList default_emagged_reagents => ref TgState.Current.obj_machinery_chem_dispenser__default_emagged_reagents;

    /// The reagent type's prototype for GLOB.chemical_reagents_list[path] (the holder's GAP: "no entry" is the list's own test)
    private static datum_reagent glob_reagent(object path)
    {
        Type reagent_path = path as Type;
        return ispath(reagent_path, typeof(datum_reagent)) && reagent_path != typeof(datum_reagent) && !Dm.@in(reagent_path, get_abstract_types())
            ? Dm.initial(reagent_path, (datum_reagent p) => p) : null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:99
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (dispensable_reagents != null && !Dm.truthy(dispensable_reagents.len))
            dispensable_reagents = default_dispensable_reagents;
        if (dispensable_reagents != null)
            dispensable_reagents = sort_list(dispensable_reagents, "/proc/" + nameof(GlobalProcs.cmp_reagents_asc)) as DmList;

        if (upgrade_reagents != null && !Dm.truthy(upgrade_reagents.len))
            upgrade_reagents = default_upgrade_reagents;
        if (upgrade_reagents != null)
            upgrade_reagents = sort_list(upgrade_reagents, "/proc/" + nameof(GlobalProcs.cmp_reagents_asc)) as DmList;

        if (emagged_reagents != null && !Dm.truthy(emagged_reagents.len))
            emagged_reagents = default_emagged_reagents;
        if (emagged_reagents != null)
            emagged_reagents = sort_list(emagged_reagents, "/proc/" + nameof(GlobalProcs.cmp_reagents_asc)) as DmList;

        object dot = base.Initialize(mapload, __extra); // So that we call RefreshParts() after adjusting the lists

        if (Dm.truthy(is_operational))
            begin_processing();
        update_appearance();
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:121
    public override object Destroy(float? force = null)
    {
        cell = null;
        QDEL_NULL(ref beaker);
        return base.Destroy(force);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:126
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(panel_open))
            Dm.append(dot, span_notice(I18n.T("[]'s maintenance hatch is open!", this)));
        if (Dm.truthy(in_range(user, this)) || isobserver(user))
            Dm.append(dot, span_notice(I18n.T("The status display reads:\nRecharge rate: <b>[]</b>.\nEnergy cost: <b>[]</b>.", display_power(recharge_amount, convert: FALSE), siunit(power_cost, "J/u", 3))));
        Dm.append(dot, span_notice(I18n.T("Use <b>RMB</b> to eject a stored beaker.")));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:136
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (Dm.truthy(old_value)) //Turned off
            end_processing();
        else //Turned on
            begin_processing();
        await DmTask.CompletedTask;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:142
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (cell.maxcharge == cell.charge)
            return null;
        Waiting.NoSleep(use_energy(active_power_usage * Dm.num(seconds_per_tick))); //Additional power cost before charging the cell.
        Waiting.NoSleep(charge_cell__proc(recharge_amount * Dm.num(seconds_per_tick), cell)); //This also costs power.
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:149
    public virtual object display_beaker()
    {
        mutable_appearance b_o = beaker_overlay ?? mutable_appearance(icon, "disp_beaker") as mutable_appearance;
        b_o.pixel_w = -7;
        b_o.pixel_z = -4;
        return b_o;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:155
    public virtual void work_animation()
    {
        if (Dm.truthy(working_state))
            flick(working_state, this);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:159
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]", Dm.truthy(nopower_state) && !Dm.truthy(powered__proc()) ? nopower_state : base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:163
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(has_panel_overlay) && Dm.truthy(panel_open))
            Dm.append(ref dot, mutable_appearance(icon, Dm.text("[]_panel-o", base_icon_state)));

        if (beaker != null)
        {
            beaker_overlay = display_beaker() as mutable_appearance;
            Dm.append(ref dot, beaker_overlay);
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:172
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, "already emagged!");
            return FALSE;
        }
        balloon_alert(user, "safeties shorted out");
        Dm.combine(ref dispensable_reagents, emagged_reagents); //add the emagged reagents to the dispensable ones
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:181
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return Dm.num(severity) <= EXPLODE_LIGHT ? FALSE : base.ex_act(severity, target, __extra);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:184
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        await base.contents_explosion(severity, target);
        if (beaker == null)
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
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:197
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == beaker)
        {
            beaker = null;
            cut_overlays();
        }
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:203
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ChemDispenser", name);
            await ui.open();
        }

        float is_hallucinating = FALSE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            is_hallucinating = Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination))) ? TRUE : FALSE;
        }
        ui.set_autoupdate(!Dm.truthy(is_hallucinating) ? TRUE : FALSE); //to not ruin the immersion by constantly changing the fake chemicals
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:215
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["amount"] = amount;
        dot["energy"] = Dm.truthy(cell.charge) ? cell.charge : 0; //To prevent NaN in the UI.
        dot["maxEnergy"] = cell.maxcharge;
        dot["displayedUnits"] = Dm.truthy(cell.charge) ? (cell.charge / power_cost) : 0;
        dot["displayedMaxUnits"] = cell.maxcharge / power_cost;
        dot["showpH"] = recording_recipe == null ? show_ph : FALSE; //virtual beakers have no ph to compute & display
        obj_item held_item = user.get_active_held_item() as obj_item;
        dot["hasBeakerInHand"] = held_item?.is_chem_container() is var held && Dm.truthy(held) ? held : FALSE;

        DmList chemicals = Dm.list();
        float is_hallucinating = FALSE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            is_hallucinating = Dm.truthy(living_user.has_status_effect(typeof(datum_status_effect_hallucination))) ? TRUE : FALSE;
        }

        foreach (object re in Dm.each(dispensable_reagents))
        {
            datum_reagent temp = glob_reagent(re);
            if (temp != null)
            {
                object chemname = temp.name;
                object chemcolor = temp.color;
                if (Dm.truthy(is_hallucinating) && prob(5))
                {
                    chemname = Dm.text("[]", pick_list_replacements("hallucination.json", "chemicals"));
                    chemcolor = random_colour();
                }
                Dm.append(chemicals, Dm.list(Dm.list(Dm.kv("title", chemname), Dm.kv("id", temp.name), Dm.kv("pH", temp.ph), Dm.kv("color", chemcolor), Dm.kv("pHCol", convert_ph_to_readable_color(temp.ph)))));
            }
        }
        dot["chemicals"] = chemicals;
        dot["recipes"] = saved_recipes;

        dot["recordingRecipe"] = recording_recipe;
        dot["recipeReagents"] = Dm.list();
        datum_reagents beaker_reagents = beaker?.reagents as datum_reagents;
        if (Dm.truthy(beaker_reagents?.ui_reaction_id))
        {
            datum_chemical_reaction reaction = GLOB.chemical_reactions_list[beaker_reagents.ui_reaction_id] as datum_chemical_reaction;
            foreach (Type reagent in Dm.each_cast<Type>(reaction.required_reagents))
                Dm.append((DmList)dot["recipeReagents"], Dm.initial(reagent, (datum_reagent r) => r.name));
        }

        DmList beaker_data = null;
        if (!QDELETED(beaker))
        {
            beaker_data = Dm.list();
            beaker_data["maxVolume"] = beaker.volume;
            beaker_data["transferAmounts"] = beaker.possible_transfer_amounts;
            beaker_data["pH"] = round(beaker_reagents.ph, 0.01f);
            beaker_data["currentVolume"] = round(beaker_reagents.total_volume, CHEMICAL_VOLUME_ROUNDING);
            DmList beakerContents = Dm.list();
            if (Dm.truthy(length(beaker_reagents.reagent_list)))
            {
                foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(beaker_reagents.reagent_list))
                    Dm.append(beakerContents, Dm.list(Dm.list(Dm.kv("name", reagent.name), Dm.kv("volume", round(Dm.num(reagent.volume), CHEMICAL_VOLUME_ROUNDING))))); // list in a list because Byond merges the first list...
            }
            beaker_data["contents"] = beakerContents;
        }
        dot["beaker"] = beaker_data;
        await DmTask.CompletedTask;
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:265
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();

        data["reaction_list"] = get_reaction_list();
        data["all_bitflags"] = Dm.list();
        DmList readable = REACTION_TAG_READABLE;
        foreach (object readable_flag in Dm.each(readable))
        {
            float real_flag = Dm.num(readable[readable_flag]);
            if (Dm.truthy(Dm.bitand(real_flag, hidden_reaction_tags)) || !Dm.truthy(Dm.bitand(real_flag, shown_reaction_tags)))
                continue;
            ((DmList)data["all_bitflags"])[readable_flag] = real_flag;
        }

        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:277
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "amount":
            {
                if (!Dm.truthy(is_operational) || QDELETED(beaker))
                    return null;
                float? target = text2num(@params["target"]);
                if (Dm.@in(target, beaker.possible_transfer_amounts))
                {
                    amount = target;
                    work_animation();
                    return TRUE;
                }
                break;
            }

            case "dispense":
            {
                if (!Dm.truthy(is_operational) || QDELETED(cell))
                    return null;
                object reagent_name = @params["reagent"];
                if (recording_recipe == null)
                {
                    object reagent = GLOB.name2reagent[reagent_name];
                    if (beaker != null && Dm.truthy(dispensable_reagents.Find(reagent)))
                    {
                        datum_reagents holder = beaker.reagents as datum_reagents;
                        float to_dispense = Dm.max(0, Dm.min(Dm.num(amount), holder.maximum_volume - holder.total_volume));
                        if (!Dm.truthy(to_dispense))
                        {
                            await say("The container is full!");
                            return null;
                        }
                        if (!Dm.truthy(await cell.use(to_dispense * power_cost)))
                        {
                            await say("Not enough energy to complete operation!");
                            return null;
                        }
                        beaker.add_hiddenprint(ui.user);
                        await holder.add_reagent(reagent, to_dispense, reagtemp: dispensed_temperature, added_purity: base_reagent_purity);

                        work_animation();
                    }
                }
                else
                    recording_recipe[reagent_name] = Dm.num(recording_recipe[reagent_name]) + Dm.num(amount);
                return TRUE;
            }

            case "remove":
            {
                if (!Dm.truthy(is_operational) || recording_recipe != null)
                    return null;
                float? amount = text2num(@params["amount"]);
                if (beaker != null && Dm.@in(amount, beaker.possible_transfer_amounts))
                {
                    await (beaker.reagents as datum_reagents).remove_all(amount);
                    work_animation();
                    return TRUE;
                }
                break;
            }

            case "eject":
                await replace_beaker(ui.user as mob_living);
                return TRUE;

            case "insert":
            {
                obj_item_reagent_containers container = ui.user.get_active_held_item() as obj_item_reagent_containers;
                if (container != null && Dm.truthy(await container.can_insert_container(ui.user as mob_living, this)))
                    await replace_beaker(ui.user as mob_living, container);

                return TRUE;
            }

            case "dispense_recipe":
            {
                if (!Dm.truthy(is_operational) || QDELETED(cell))
                    return null;

                DmList chemicals_to_dispense = saved_recipes[@params["recipe"]] as DmList;
                if (!Dm.truthy(LAZYLEN(chemicals_to_dispense)))
                    return null;
                foreach (object key in Dm.each(chemicals_to_dispense))
                {
                    object reagent = GLOB.name2reagent[key];
                    object dispense_amount = chemicals_to_dispense[key];
                    if (!Dm.truthy(dispensable_reagents.Find(reagent)))
                        return null;
                    if (recording_recipe == null)
                    {
                        if (beaker == null)
                            return null;
                        datum_reagents holder = beaker.reagents as datum_reagents;
                        float to_dispense = Dm.max(0, Dm.min(Dm.num(dispense_amount), holder.maximum_volume - holder.total_volume));
                        if (!Dm.truthy(to_dispense))
                            continue;
                        if (!Dm.truthy(await cell.use(to_dispense * power_cost)))
                        {
                            await say("Not enough energy to complete operation!");
                            return null;
                        }
                        beaker.add_hiddenprint(ui.user);
                        await holder.add_reagent(reagent, to_dispense, reagtemp: dispensed_temperature, added_purity: base_reagent_purity);
                        work_animation();
                    }
                    else
                        recording_recipe[key] = Dm.num(recording_recipe[key]) + Dm.num(dispense_amount);
                }
                return TRUE;
            }

            case "clear_recipes":
                if (Dm.truthy(is_operational) && Dm.eq(await tgui_alert(ui.user, "Clear all recipes?", "Clear?", Dm.list("Yes", "No")), "Yes"))
                {
                    saved_recipes = Dm.list();
                    return TRUE;
                }
                break;

            case "record_recipe":
                if (Dm.truthy(is_operational))
                {
                    recording_recipe = Dm.list();
                    return TRUE;
                }
                break;

            case "save_recording":
            {
                if (!Dm.truthy(is_operational))
                    return null;
                object name = await tgui_input_text(ui.user, "What do you want to name this recipe?", "Recipe Name", max_length: MAX_NAME_LEN, encode: FALSE);
                if (!Dm.truthy(await ui.user.can_perform_action(this, ALLOW_SILICON_REACH)))
                    return null;
                if (Dm.truthy(saved_recipes[name]) && Dm.eq(await tgui_alert(ui.user, Dm.text("\"[]\" already exists, do you want to overwrite it?", name), null, Dm.list("Yes", "No")), "No"))
                    return null;
                if (Dm.truthy(name) && recording_recipe != null)
                {
                    foreach (object reagent in Dm.each(recording_recipe))
                    {
                        object reagent_id = GLOB.name2reagent[reagent];
                        if (!Dm.truthy(dispensable_reagents.Find(reagent_id)))
                        {
                            visible_message(span_warning(I18n.T("[] buzzes.", this)), span_hear(I18n.T("You hear a faint buzz.")));
                            to_chat(ui.user, span_warning(I18n.T("[] cannot find <b>[]</b>!", this, reagent)));
                            playsound(this, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, TRUE);
                            return null;
                        }
                    }
                    saved_recipes[name] = recording_recipe;
                    recording_recipe = null;
                    return TRUE;
                }
                break;
            }

            case "cancel_recording":
                if (Dm.truthy(is_operational))
                {
                    recording_recipe = null;
                    return TRUE;
                }
                break;

            case "reaction_lookup":
                if (beaker != null)
                    await (beaker.reagents as datum_reagents).ui_interact(ui.user);
                break;
        }

        object result = await handle_ui_act(action, @params, ui, state);
        if (result == null)
            result = FALSE;
        return result;
    }

    /// Same as ui_act() but to be used by subtypes exclusively
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:409
    public virtual async DmTask<object> handle_ui_act(object action = null, object @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:412
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:417
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:420
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:423
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await tool.can_insert_container(user, this)))
            return NONE;
        if (!Dm.truthy(await replace_beaker(user, tool as obj_item_reagent_containers)))
            return ITEM_INTERACT_BLOCKING;

        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:432
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:435
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        DmList R = Dm.list();
        float total = Dm.min(rand(7, 15), FLOOR(cell.charge * INVERSE(power_cost), 1));
        datum_reagents Q = Dm.@new<datum_reagents>(total * 10);
        if (beaker?.reagents != null)
            Dm.append(R, beaker.reagents);
        for (float i = 1; i <= total; i++)
            await Q.add_reagent(pick(dispensable_reagents), 10, reagtemp: dispensed_temperature, added_purity: base_reagent_purity);
        Dm.append(R, Q);
        await chem_splash(get_turf(this), null, 3, R);
        if (beaker?.reagents != null)
            await (beaker.reagents as datum_reagents).remove_all();
        await cell.use(total * power_cost);
        await cell.emp_act(severity);
        work_animation();
        visible_message(span_danger(I18n.T("[] malfunctions, spraying chemicals everywhere!", this)));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:455
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        recharge_amount = Dm.initial(this, (obj_machinery_chem_dispenser p) => p.recharge_amount);
        float new_power_cost = Dm.initial(this, (obj_machinery_chem_dispenser p) => p.power_cost);
        float parts_rating = 0;
        foreach (obj_item_stock_parts_power_store stock_cell in Dm.each<obj_item_stock_parts_power_store>(component_parts))
            cell = stock_cell;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
        {
            new_power_cost -= (Dm.num(matter_bin.tier) * 0.25f * KILO * WATTS);
            parts_rating += Dm.num(matter_bin.tier);
        }
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
        {
            recharge_amount *= Dm.num(capacitor.tier);
            parts_rating += Dm.num(capacitor.tier);
        }
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
        {
            if (Dm.num(servo.tier) > 3)
                Dm.combine(ref dispensable_reagents, upgrade_reagents);
            else
                Dm.remove(ref dispensable_reagents, upgrade_reagents);
            parts_rating += Dm.num(servo.tier);
        }
        power_cost = Dm.max(new_power_cost, 0.1f * KILO * WATTS);
        return dot;
    }

    /**
     * Insert, remove, replace the existig beaker. Returns TRUE on success.
     * Arguments:
     *
     * * mob/living/user - the player trying to replace the beaker
     * * obj/item/reagent_containers/new_beaker - the beaker we are trying to insert, swap with existing or remove if null
     */
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:483
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

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:497
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        cell = null;
        if (beaker != null)
        {
            await beaker.forceMove(drop_location() as atom);
            beaker = null;
        }
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:504
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

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:513
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:516
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:519
    public virtual object get_reaction_list()
    {
        // var/static/list/reaction_list (rules 10.6)
        ref DmList reaction_list = ref TgState.Current.obj_machinery_chem_dispenser__get_reaction_list__reaction_list;
        if (Dm.truthy(reaction_list?[type]))
            return reaction_list[type];

        reaction_list ??= Dm.list();
        reaction_list[type] = Dm.list();

        DmList new_reaction_list = Dm.list();
        DmList product_index = Dm.sub(GLOB.chemical_reactions_list_product_index, dispensable_reagents) as DmList;
        foreach (object result in Dm.each(product_index))
        {
            DmList reactions = product_index[result] as DmList;
            datum_reagent result_datum = glob_reagent(result);
            foreach (datum_chemical_reaction reaction in Dm.each_cast<datum_chemical_reaction>(reactions))
            {
                if (!Dm.truthy(Dm.bitand(reaction.reaction_tags, shown_reaction_tags)))
                    continue;
                if (Dm.truthy(Dm.bitand(reaction.reaction_tags, hidden_reaction_tags)))
                    continue;
                if (reaction.required_container != null)
                    continue;

                object index = result_datum.name;
                DmList new_info = get_reaction_info(reaction) as DmList;
                new_info["description"] = result_datum.description;
                new_info["color"] = result_datum.color;

                float num_alts = 0;
                while (Dm.truthy(new_reaction_list[index]))
                {
                    num_alts++;
                    index = Dm.text("[] (Alt[])", result_datum.name, num_alts == 1 ? "" : Dm.text(" #[]", num_alts));
                }

                new_reaction_list[index] = new_info;
            }
        }

        reaction_list[type] = new_reaction_list;
        return reaction_list[type];
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:553
    public virtual object get_reaction_info(datum_chemical_reaction reaction = null)
    {
        DmList info = Dm.list();
        info["id"] = reaction.type;
        info["lower_temperature"] = reaction.required_temp;
        info["upper_temperature"] = reaction.optimal_temp;
        info["lower_ph"] = reaction.optimal_ph_min;
        info["upper_ph"] = reaction.optimal_ph_max;
        info["bitflags"] = reaction.reaction_tags;
        info["required_reagents"] = reagent_list_to_info(reaction.required_reagents);
        info["required_catalysts"] = reagent_list_to_info(reaction.required_catalysts);
        return info;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:565
    public virtual object reagent_list_to_info(DmList reagent_list = null)
    {
        DmList info = Dm.list();
        foreach (Type reagent_typepath in Dm.each_cast<Type>(reagent_list))
        {
            Dm.append(info, Dm.list(Dm.list(
                Dm.kv("name", Dm.initial(reagent_typepath, (datum_reagent r) => r.name)),
                Dm.kv("amount", reagent_list[reagent_typepath]),
                Dm.kv("typepath", reagent_typepath))));
        }
        return info;
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:576
public partial class obj_machinery_chem_dispenser_drinks : obj_machinery_chem_dispenser
{
    /// The default list of reagents dispensable by the soda dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:593  var/static/list/drinks_dispensable_reagents (rules 10.6)
    public static ref DmList drinks_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser_drinks__drinks_dispensable_reagents;
    /// The default list of emagged reagents dispensable by the soda dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:622
    public static ref DmList drink_emagged_reagents => ref TgState.Current.obj_machinery_chem_dispenser_drinks__drink_emagged_reagents;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:630
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (dispensable_reagents != null && !Dm.truthy(dispensable_reagents.len))
            dispensable_reagents = drinks_dispensable_reagents;
        if (emagged_reagents != null && !Dm.truthy(emagged_reagents.len))
            emagged_reagents = drink_emagged_reagents;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:638
    public override async DmTask setDir(object newdir = null)
    {
        object old = dir;
        await base.setDir(newdir);
        if (!Dm.eq(dir, old))
            update_appearance(); // the beaker needs to be re-positioned if we rotate
    }

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:644
    public override object display_beaker()
    {
        mutable_appearance b_o = beaker_overlay ?? mutable_appearance(icon, "disp_beaker") as mutable_appearance;
        switch (Dm.num(dir))
        {
            case NORTH:
                b_o.pixel_w = rand(-9, 9);
                b_o.pixel_z = 7;
                break;
            case EAST:
                b_o.pixel_w = 4;
                b_o.pixel_z = rand(-5, 7);
                break;
            case WEST:
                b_o.pixel_w = -5;
                b_o.pixel_z = rand(-5, 7);
                break;
            default: //SOUTH
                b_o.pixel_w = rand(-9, 9);
                b_o.pixel_z = -7;
                break;
        }
        return b_o;
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:662
public partial class obj_machinery_chem_dispenser_drinks_fullupgrade : obj_machinery_chem_dispenser_drinks
{
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:666
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.combine(ref dispensable_reagents, emagged_reagents); //adds emagged reagents
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:671
public partial class obj_machinery_chem_dispenser_drinks_beer : obj_machinery_chem_dispenser_drinks
{
    /// The default list of reagents dispensable by the beer dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:679  var/static/list/beer_dispensable_reagents (rules 10.6)
    public static ref DmList beer_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser_drinks_beer__beer_dispensable_reagents;
    /// The default list of emagged reagents dispensable by the beer dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:708
    public static ref DmList beer_emagged_reagents => ref TgState.Current.obj_machinery_chem_dispenser_drinks_beer__beer_emagged_reagents;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:716
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        dispensable_reagents = beer_dispensable_reagents;
        emagged_reagents = beer_emagged_reagents;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:722
public partial class obj_machinery_chem_dispenser_drinks_beer_fullupgrade : obj_machinery_chem_dispenser_drinks_beer
{
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:726
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.combine(ref dispensable_reagents, emagged_reagents); //adds emagged reagents
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:731
public partial class obj_machinery_chem_dispenser_mutagen : obj_machinery_chem_dispenser
{
    /// The default list of reagents dispensable by mutagen chem dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:734  var/static/list/mutagen_dispensable_reagents (rules 10.6)
    public static ref DmList mutagen_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser_mutagen__mutagen_dispensable_reagents;
    /// The default list of emagged reagents dispensable by mutagen chem dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:737
    public static ref DmList mutagen_emagged_reagents => ref TgState.Current.obj_machinery_chem_dispenser_mutagen__mutagen_emagged_reagents;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:739
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        dispensable_reagents = mutagen_dispensable_reagents;
        emagged_reagents = mutagen_emagged_reagents;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:745
public partial class obj_machinery_chem_dispenser_mutagensaltpeter : obj_machinery_chem_dispenser
{
    /// The default list of dispensable reagents available in the mutagensaltpeter chem dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:752  var/static/list/mutagensaltpeter_dispensable_reagents (rules 10.6)
    public static ref DmList mutagensaltpeter_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser_mutagensaltpeter__mutagensaltpeter_dispensable_reagents;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:770
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        dispensable_reagents = mutagensaltpeter_dispensable_reagents;
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:775
public partial class obj_machinery_chem_dispenser_fullupgrade : obj_machinery_chem_dispenser
{
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:779
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.combine(ref dispensable_reagents, emagged_reagents); //adds emagged reagents
        return dot;
    }
}

// tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:784
public partial class obj_machinery_chem_dispenser_abductor : obj_machinery_chem_dispenser
{
    /// The default list of dispensable reagents available in the abductor chem dispenser
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:796  var/static/list/abductor_dispensable_reagents (rules 10.6)
    public static ref DmList abductor_dispensable_reagents => ref TgState.Current.obj_machinery_chem_dispenser_abductor__abductor_dispensable_reagents;

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:839
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        dispensable_reagents = abductor_dispensable_reagents;
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:54
    internal DmList obj_machinery_chem_dispenser__default_dispensable_reagents = Dm.list(
        typeof(datum_reagent_aluminium),
        typeof(datum_reagent_bromine),
        typeof(datum_reagent_carbon),
        typeof(datum_reagent_chlorine),
        typeof(datum_reagent_copper),
        typeof(datum_reagent_consumable_ethanol),
        typeof(datum_reagent_fluorine),
        typeof(datum_reagent_hydrogen),
        typeof(datum_reagent_iodine),
        typeof(datum_reagent_iron),
        typeof(datum_reagent_lithium),
        typeof(datum_reagent_mercury),
        typeof(datum_reagent_nitrogen),
        typeof(datum_reagent_oxygen),
        typeof(datum_reagent_phosphorus),
        typeof(datum_reagent_potassium),
        typeof(datum_reagent_uranium_radium),
        typeof(datum_reagent_silicon),
        typeof(datum_reagent_sodium),
        typeof(datum_reagent_stable_plasma),
        typeof(datum_reagent_consumable_sugar),
        typeof(datum_reagent_sulfur),
        typeof(datum_reagent_toxin_acid),
        typeof(datum_reagent_water),
        typeof(datum_reagent_fuel)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:82
    internal DmList obj_machinery_chem_dispenser__default_upgrade_reagents = Dm.list(
        typeof(datum_reagent_acetone),
        typeof(datum_reagent_ammonia),
        typeof(datum_reagent_ash),
        typeof(datum_reagent_diethylamine),
        typeof(datum_reagent_fuel_oil),
        typeof(datum_reagent_saltpetre)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:91
    internal DmList obj_machinery_chem_dispenser__default_emagged_reagents = Dm.list(
        typeof(datum_reagent_toxin_carpotoxin),
        typeof(datum_reagent_medicine_mine_salve),
        typeof(datum_reagent_medicine_morphine),
        typeof(datum_reagent_drug_space_drugs),
        typeof(datum_reagent_toxin)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:593
    internal DmList obj_machinery_chem_dispenser_drinks__drinks_dispensable_reagents = Dm.list(
        typeof(datum_reagent_consumable_coffee),
        typeof(datum_reagent_consumable_space_cola),
        typeof(datum_reagent_consumable_cream),
        typeof(datum_reagent_consumable_dr_gibb),
        typeof(datum_reagent_consumable_grenadine),
        typeof(datum_reagent_consumable_ice),
        typeof(datum_reagent_consumable_icetea),
        typeof(datum_reagent_consumable_lemonjuice),
        typeof(datum_reagent_consumable_lemon_lime),
        typeof(datum_reagent_consumable_limejuice),
        typeof(datum_reagent_consumable_melon_soda),
        typeof(datum_reagent_consumable_menthol),
        typeof(datum_reagent_consumable_orangejuice),
        typeof(datum_reagent_consumable_pineapplejuice),
        typeof(datum_reagent_consumable_pwr_game),
        typeof(datum_reagent_consumable_shamblers),
        typeof(datum_reagent_consumable_spacemountainwind),
        typeof(datum_reagent_consumable_sodawater),
        typeof(datum_reagent_consumable_sol_dry),
        typeof(datum_reagent_consumable_space_up),
        typeof(datum_reagent_consumable_sugar),
        typeof(datum_reagent_consumable_tea),
        typeof(datum_reagent_consumable_tomatojuice),
        typeof(datum_reagent_consumable_tonic),
        typeof(datum_reagent_water)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:622
    internal DmList obj_machinery_chem_dispenser_drinks__drink_emagged_reagents = Dm.list(
        typeof(datum_reagent_consumable_ethanol_thirteenloko),
        typeof(datum_reagent_consumable_ethanol_whiskey_cola),
        typeof(datum_reagent_toxin_mindbreaker),
        typeof(datum_reagent_toxin_staminatoxin)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:679
    internal DmList obj_machinery_chem_dispenser_drinks_beer__beer_dispensable_reagents = Dm.list(
        typeof(datum_reagent_consumable_ethanol_absinthe),
        typeof(datum_reagent_consumable_ethanol_ale),
        typeof(datum_reagent_consumable_ethanol_applejack),
        typeof(datum_reagent_consumable_ethanol_beer),
        typeof(datum_reagent_consumable_ethanol_coconut_rum),
        typeof(datum_reagent_consumable_ethanol_cognac),
        typeof(datum_reagent_consumable_ethanol_creme_de_cacao),
        typeof(datum_reagent_consumable_ethanol_creme_de_coconut),
        typeof(datum_reagent_consumable_ethanol_creme_de_menthe),
        typeof(datum_reagent_consumable_ethanol_curacao),
        typeof(datum_reagent_consumable_ethanol_gin),
        typeof(datum_reagent_consumable_ethanol_hcider),
        typeof(datum_reagent_consumable_ethanol_kahlua),
        typeof(datum_reagent_consumable_ethanol_beer_maltliquor),
        typeof(datum_reagent_consumable_ethanol_navy_rum),
        typeof(datum_reagent_consumable_ethanol_rice_beer),
        typeof(datum_reagent_consumable_ethanol_rum),
        typeof(datum_reagent_consumable_ethanol_sake),
        typeof(datum_reagent_consumable_ethanol_tequila),
        typeof(datum_reagent_consumable_ethanol_triple_sec),
        typeof(datum_reagent_consumable_ethanol_vermouth),
        typeof(datum_reagent_consumable_ethanol_vodka),
        typeof(datum_reagent_consumable_ethanol_whiskey),
        typeof(datum_reagent_consumable_ethanol_wine),
        typeof(datum_reagent_consumable_ethanol_yuyake)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:708
    internal DmList obj_machinery_chem_dispenser_drinks_beer__beer_emagged_reagents = Dm.list(
        typeof(datum_reagent_consumable_ethanol),
        typeof(datum_reagent_iron),
        typeof(datum_reagent_consumable_mintextract),
        typeof(datum_reagent_consumable_ethanol_atomicbomb),
        typeof(datum_reagent_consumable_ethanol_fernet)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:734
    internal DmList obj_machinery_chem_dispenser_mutagen__mutagen_dispensable_reagents = Dm.list(
        typeof(datum_reagent_toxin_mutagen)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:737
    internal DmList obj_machinery_chem_dispenser_mutagen__mutagen_emagged_reagents = Dm.list(
        typeof(datum_reagent_toxin_plasma)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:752
    internal DmList obj_machinery_chem_dispenser_mutagensaltpeter__mutagensaltpeter_dispensable_reagents = Dm.list(
        typeof(datum_reagent_toxin_mutagen),
        typeof(datum_reagent_saltpetre),
        typeof(datum_reagent_plantnutriment_eznutriment),
        typeof(datum_reagent_plantnutriment_left4zednutriment),
        typeof(datum_reagent_plantnutriment_robustharvestnutriment),
        typeof(datum_reagent_plantnutriment_endurogrow),
        typeof(datum_reagent_plantnutriment_liquidearthquake),
        typeof(datum_reagent_water),
        typeof(datum_reagent_toxin_plantbgone),
        typeof(datum_reagent_toxin_plantbgone_weedkiller),
        typeof(datum_reagent_toxin_pestkiller),
        typeof(datum_reagent_medicine_cryoxadone),
        typeof(datum_reagent_ammonia),
        typeof(datum_reagent_ash),
        typeof(datum_reagent_diethylamine)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:796
    internal DmList obj_machinery_chem_dispenser_abductor__abductor_dispensable_reagents = Dm.list(
        typeof(datum_reagent_aluminium),
        typeof(datum_reagent_bromine),
        typeof(datum_reagent_carbon),
        typeof(datum_reagent_chlorine),
        typeof(datum_reagent_copper),
        typeof(datum_reagent_consumable_ethanol),
        typeof(datum_reagent_fluorine),
        typeof(datum_reagent_hydrogen),
        typeof(datum_reagent_iodine),
        typeof(datum_reagent_iron),
        typeof(datum_reagent_lithium),
        typeof(datum_reagent_mercury),
        typeof(datum_reagent_nitrogen),
        typeof(datum_reagent_oxygen),
        typeof(datum_reagent_phosphorus),
        typeof(datum_reagent_potassium),
        typeof(datum_reagent_uranium_radium),
        typeof(datum_reagent_silicon),
        typeof(datum_reagent_silver),
        typeof(datum_reagent_sodium),
        typeof(datum_reagent_stable_plasma),
        typeof(datum_reagent_consumable_sugar),
        typeof(datum_reagent_sulfur),
        typeof(datum_reagent_toxin_acid),
        typeof(datum_reagent_water),
        typeof(datum_reagent_fuel),
        typeof(datum_reagent_acetone),
        typeof(datum_reagent_ammonia),
        typeof(datum_reagent_ash),
        typeof(datum_reagent_diethylamine),
        typeof(datum_reagent_fuel_oil),
        typeof(datum_reagent_saltpetre),
        typeof(datum_reagent_medicine_mine_salve),
        typeof(datum_reagent_medicine_morphine),
        typeof(datum_reagent_drug_space_drugs),
        typeof(datum_reagent_toxin),
        typeof(datum_reagent_toxin_plasma),
        typeof(datum_reagent_uranium),
        typeof(datum_reagent_consumable_liquidelectricity),
        typeof(datum_reagent_medicine_c2_synthflesh)
    );

    // tg: code/modules/reagents/chemistry/machinery/chem_dispenser.dm:520  get_reaction_list's var/static/list/reaction_list
    internal DmList obj_machinery_chem_dispenser__get_reaction_list__reaction_list;
}
