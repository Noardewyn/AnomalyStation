// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/_boulder_processing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

// tg: code/modules/mining/boulder_processing/_boulder_processing.dm:1
public partial class obj_machinery_bouldertech : obj_machinery
{
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        silo_materials = Dm.@new<datum_remote_materials>(
            this,
            mapload,
            Dm.named("mat_container_flags", MATCONTAINER_NO_INSERT));

        register_context();
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:44
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        await AddElement(typeof(datum_element_connect_loc), post_machine_initialize__loc_connections);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:46  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList post_machine_initialize__loc_connections => ref TgState.Current.obj_machinery_bouldertech__post_machine_initialize__loc_connections;

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:51
    public override object Destroy(float? force = null)
    {
        qdel(silo_materials);
        silo_materials = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:55
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(length(contents)))
        {
            foreach (obj_item_boulder boulder in Dm.each<obj_item_boulder>(contents))
                await remove_boulder(boulder);
        }
        return null;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:60
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = CONTEXTUAL_SCREENTIP_SET;

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = "Remove Boulder";
            return dot;
        }

        if (istype(held_item, typeof(obj_item_boulder)))
            context[SCREENTIP_CONTEXT_LMB] = "Insert boulder";
        else if (istype(held_item, typeof(obj_item_card_id)) && points_held > 0)
            context[SCREENTIP_CONTEXT_LMB] = "Claim mining points";
        else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] panel", Dm.truthy(panel_open) ? "Close" : "Open");
        else if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[]chor", Dm.truthy(anchored) ? "Unan" : "An");
        else if (Dm.truthy(panel_open) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;

        Dm.append(ref dot, span_suppradio(I18n.T("The machine reads that it has [] stored. Swipe an ID to claim them.", EXAMINE_HINT(I18n.T("[] mining points", points_held)))));

        float boulder_count = 0;
        foreach (obj_item_boulder potential_boulder in Dm.each<obj_item_boulder>(contents))
            boulder_count += 1;

        if (boulder_count >= 1)
            Dm.append(ref dot, span_notice(I18n.T("[] to manually remove a stored boulder.<br />", EXAMINE_HINT(I18n.T("Right Click")))));

        Dm.append(ref dot, span_info(I18n.T("Storage capacity = <b>[]/[] boulders</b>.", boulder_count, boulders_held_max)));
        Dm.append(ref dot, span_info(I18n.T("This machine can process up to [] at a time.", EXAMINE_HINT(I18n.T("[] boulders", boulders_processing_count)))));

        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's [] in place.", EXAMINE_HINT(I18n.T("anchored")))));
        else
            Dm.append(ref dot, span_warning(I18n.T("It needs to be [] to start operations.", EXAMINE_HINT(I18n.T("anchored")))));

        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("closed") : I18n.T("open"))));

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The whole machine can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:104
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;

        DmList booster_list = get_booster_reagents() as DmList;
        if (Dm.truthy(length(booster_list)))
        {
            Dm.append(ref dot, span_notice(I18n.T("This machine's output is boosted by <b>chemical intake:</b><br>")));
            foreach (object increment in Dm.each(booster_list))
                Dm.append(ref dot, span_info(I18n.T("&bull; []: Provides []% Boost", Dm.initial((System.Type)increment, "name"), Dm.num(booster_list[increment]) * 10)));
            Dm.append(ref dot, span_notice(I18n.T("<br>Upon being boosted successfully, \\the [] will produce [].", this, EXAMINE_HINT(Dm.initial(waste_chemical, "name")))));
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:115
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string suffix = "";
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            suffix = "-off";
        icon_state = Dm.text("[][]", base_icon_state, suffix);
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:122
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        if (!Dm.truthy(anchored) || !(Dm.eq(dir, border_dir) || Dm.eq(dir, REVERSE_DIR(Dm.num(border_dir)))))
            return FALSE;
        if (istype(mover, typeof(obj_item_stack_sheet)))
            return TRUE;
        if (istype(mover, typeof(obj_item_boulder)))
            return can_process_boulder(mover as obj_item_boulder);
        if (isliving(mover))
            return can_process_living_mob(mover as mob_living);
        return await base.CanAllowThrough(mover, border_dir);
    }

    /**
     * Can we process the boulder, checks only the boulders state & machines capacity
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:139
    public virtual object can_process_boulder(obj_item_boulder new_boulder = null)
    {
        //machine not operational
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            return FALSE;

        //not a valid boulder
        if (new_boulder == null || QDELETED(new_boulder))
            return FALSE;

        //someone is still processing this
        if (new_boulder.processed_by != null)
            return FALSE;

        //no space to hold boulders
        float boulder_count = 0;
        foreach (obj_item_boulder potential_boulder in Dm.each<obj_item_boulder>(contents))
            boulder_count += 1;
        if (boulder_count >= boulders_held_max)
            return FALSE;

        //did we cooldown enough to accept a boulder
        return accept_cooldown <= world.time ? TRUE : FALSE;
    }

    /**
     * Accepts a boulder into the machine. Used when a boulder is first placed into the machine.
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:172
    public virtual async DmTask<object> accept_boulder(obj_item_boulder new_boulder = null)
    {
        if (!Dm.truthy(can_process_boulder(new_boulder)))
            return FALSE;

        await new_boulder.forceMove(this);

        accept_cooldown = world.time + 1.5f * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline

        return TRUE;
    }

    /**
     * Can we maim this golem
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:190
    public virtual object can_process_living_mob(mob_living rockman = null)
    {
        //not operatinal
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            return FALSE;

        //still in cooldown
        if (!(accept_cooldown <= world.time))
            return FALSE;

        //not a mineral mob
        if (rockman == null || QDELETED(rockman))
            return FALSE;

        DmList bodyparts = rockman.get_bodyparts() as DmList;
        if (Dm.truthy(length(bodyparts))) //Has bodypart, check if any has the BIO_STONE state (we don't check MOB_MINERAL for carbon mobs cuz we don't want to gib plasmamen, just grind golem parts)
        {
            foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(bodyparts))
            {
                if (!Dm.truthy(Dm.bitand(part.biological_state, BIO_STONE)))
                    return FALSE;
            }
        }
        else if (!Dm.truthy(Dm.bitand(rockman.mob_biotypes, MOB_MINERAL)))
            return FALSE;

        //Only return true if they are lying down or are incapacitated.
        if (Dm.truthy(Dm.bitand(rockman.mobility_flags, MOBILITY_LIEDOWN)))
        {
            if (Dm.eq(rockman.body_position, LYING_DOWN))
                return TRUE;
        }
        else if (Dm.truthy(rockman.incapacitated))
            return TRUE;

        return FALSE;
    }

    /**
     * Accepts a golem to be processed, mainly for memes
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:230
    public virtual async DmTask accept_mob(mob_living rockman = null)
    {
        if (!Dm.truthy(can_process_living_mob(rockman)))
            return;

        if (!Dm.truthy(await use_energy(active_power_usage * 1.5f, force: FALSE)))
        {
            await say(I18n.T("Not enough energy!"));
            return;
        }

        await maim_mob(rockman);
        playsound(this, usage_sound, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);

        accept_cooldown = world.time + 3 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
    }

    /// What effects actually happens to a golem when it is "processed"
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:246
    public virtual async DmTask maim_mob(mob_living rockman = null)
    {
        Shake(duration: 1 * SECONDS);
        DmList bodyparts = rockman.get_bodyparts() as DmList;
        bool gibbing = true;
        if (Dm.truthy(length(bodyparts))) //Has bodypart, check if any has the BIO_STONE state and don't just gib them if you can.
        {
            DmList deleted_part_names = null;
            gibbing = false;
            foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(bodyparts))
            {
                if (!Dm.truthy(Dm.bitand(part.biological_state, BIO_STONE)))
                    continue;
                if (Dm.eq(part.body_zone, BODY_ZONE_CHEST)) //Ok, so, without a chest, we're pretty much going to be gibbed anyway.
                {
                    gibbing = true;
                    break;
                }
                else
                {
                    Dm.append(ref deleted_part_names, part.name);
                    qdel(part); //This calls force_removal() with dismembered set to TRUE
                }
            }
            if (!gibbing && Dm.truthy(length(deleted_part_names)))
            {
                object parts_text = english_list(deleted_part_names);
                rockman.investigate_log(Dm.text("had [] [] which all had BIO_STONE biological state) destroyed by a []", rockman.p_their(), parts_text, this), INVESTIGATE_CARGO);
                rockman.visible_message(span_warning(I18n.T("[] is processed by []!", rockman, this)), span_userdanger(I18n.T("Your [] are shred into bits by []!", parts_text, this)));
            }
        }

        if (gibbing)
            await gib_mob(rockman);
    }

    ///Called at the end of main_mob() if they were a bodyparts-less mob or their chest was made of stone...
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:274
    public virtual async DmTask gib_mob(mob_living rockman = null)
    {
        rockman.visible_message(span_warning(I18n.T("[] is processed by []!", rockman, this)), span_userdanger(I18n.T("You get processed into bits by []!", this)));
        rockman.investigate_log(Dm.text("was gibbed by [] for having the MOB_MINERAL mob biotype", this), INVESTIGATE_DEATHS);
        await rockman.gib(DROP_ALL_REMAINS);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:279
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable atom_movable = null)
    {
        if (istype(atom_movable, typeof(obj_item_boulder)))
        {
            Waiting.Detach(accept_boulder((obj_item_boulder)atom_movable)); // INVOKE_ASYNC
            return;
        }

        if (isliving(atom_movable))
        {
            Waiting.Detach(accept_mob((mob_living)atom_movable)); // INVOKE_ASYNC
            return;
        }
    }

    /**
     * Looks for a boost to the machine's efficiency, and applies it if found.
     * Applied more on the chemistry integration but can be used for other things if desired.
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:294
    public virtual async DmTask check_for_boosts()
    {
        refining_efficiency = Dm.num(Dm.initial(this, "refining_efficiency")); //Reset refining efficiency to 100%.
        await DmTask.CompletedTask;
    }

    ///Returns a map of reagent -> boost amount to increase this machines efficiency
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:300
    public virtual object get_booster_reagents()
    {
        return Dm.list();
    }

    /**
     * Checks if this machine can process this material
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:311
    public virtual object can_process_material(datum_material mat = null)
    {
        return FALSE;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:316
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(panel_open) || Dm.truthy(user.combat_mode))
            return NONE;

        if (istype(tool, typeof(obj_item_boulder)))
        {
            obj_item_boulder my_boulder = (obj_item_boulder)tool;
            if (!Dm.truthy(await accept_boulder(my_boulder)))
            {
                balloon_alert_to_viewers(I18n.T("cannot accept!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert_to_viewers(I18n.T("accepted"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_card_id)))
        {
            if (points_held <= 0)
            {
                balloon_alert_to_viewers(I18n.T("no points to claim!"));
                if (!(sound_cooldown <= world.time))
                    return ITEM_INTERACT_BLOCKING;
                sound_cooldown = world.time + 1.5f * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm), inline
                playsound(this, Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 30, FALSE);
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_card_id id_card = (obj_item_card_id)tool;
            object amount_answer = await tgui_input_number(user, I18n.T("How many mining points do you wish to claim? ID Balance: [], stored mining points: []", id_card.registered_account.mining_points, points_held), I18n.T("Transfer Points"), Dm.named("max_value", points_held), Dm.named("min_value", 0), Dm.named("round_value", 1));
            if (!Dm.truthy(amount_answer))
                return ITEM_INTERACT_BLOCKING;
            float amount = Dm.num(amount_answer);
            if (amount > points_held)
                amount = points_held;
            id_card.registered_account.mining_points += amount;
            points_held = round(points_held - amount);
            to_chat(user, span_notice(I18n.T("You claim [] mining points from \\the [] to [].", amount, this, id_card)));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:352
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool, time: 1.5f * SECONDS), SUCCESSFUL_UNFASTEN))
        {
            if (Dm.truthy(anchored))
                begin_processing();
            else
                end_processing();
            update_appearance(UPDATE_ICON_STATE);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:362
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:365
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:368
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.truthy(panel_open))
            return dot;
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("anchor it first!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("close panel!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        obj_item_boulder boulder = Dm.locate<obj_item_boulder>(this);
        if (boulder == null)
        {
            balloon_alert_to_viewers(I18n.T("no boulders to remove!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(await remove_boulder(boulder)))
        {
            balloon_alert_to_viewers(I18n.T("no space to remove!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    /**
     * Accepts a boulder into the machinery, then converts it into minerals.
     * If the boulder can be fully processed by this machine, we take the materials, insert it into the silo, and destroy the boulder.
     * If the boulder has materials left, we make a copy of the boulder to hold the processable materials, take the processable parts, and eject the original boulder.
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:400
    public virtual async DmTask breakdown_boulder(obj_item_boulder chosen_boulder = null)
    {
        if (QDELETED(chosen_boulder))
            return;
        if (chosen_boulder.loc != this)
            return;
        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
        {
            await say(I18n.T("Not enough energy!"));
            return;
        }

        //if boulders are kept inside because there is no space to eject them, then they could be reprocessed, lets avoid that
        if (chosen_boulder.processed_by == null)
        {
            if (Dm.truthy(length((reagents as datum_reagents)?.reagent_list)))
                await check_for_boosts(); //Handles the mineral boosting, as well as creating waste. Must have reagents in the machine.

            //here we loop through the boulder's ores
            DmList rejected_mats = Dm.list();
            foreach (datum_material possible_mat in Dm.each<datum_material>(chosen_boulder.custom_materials))
            {
                float quantity = Dm.num(chosen_boulder.custom_materials[possible_mat]) * refining_efficiency;
                if (!Dm.truthy(can_process_material(possible_mat)))
                {
                    rejected_mats[possible_mat] = quantity;
                    continue;
                }
                points_held += round(quantity * possible_mat.points_per_boulder_unit); // put point total here into machine
                if (silo_materials.silo == null || !Dm.truthy(await silo_materials.mat_container.insert_amount_mat(quantity, possible_mat)))
                    Dm.@new(possible_mat.sheet_type, drop_location(), floor(quantity / SHEET_MATERIAL_AMOUNT));
            }

            //puts back materials that couldn't be processed
            await chosen_boulder.set_custom_materials(rejected_mats, refining_efficiency);

            //break the boulder down if we have processed all its materials
            if (!Dm.truthy(length(chosen_boulder.custom_materials)))
            {
                playsound(loc, usage_sound, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
                if (istype(chosen_boulder, typeof(obj_item_boulder_artifact)))
                    points_held = round(points_held + MINER_POINT_MULTIPLIER); /// Artifacts give bonus points!
                await chosen_boulder.break_apart();
                return; //We've processed all the materials in the boulder, so we can just destroy it in break_apart.
            }

            chosen_boulder.processed_by = this;
        }

        //eject the boulder since we are done with it
        await remove_boulder(chosen_boulder);
    }

    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:443
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(anchored) || Dm.truthy(panel_open) || !Dm.truthy(is_operational) || Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)))
            return null;

        Waiting.NoSleep(process__body());
        return null;
    }

    private async DmTask process__body()
    {
        bool boulders_found = false;
        float boulders_processed = boulders_processing_count;
        foreach (obj_item_boulder potential_boulder in Dm.each<obj_item_boulder>(contents))
        {
            boulders_found = true;
            if (boulders_processed <= 0)
                break; //Try again next time
            boulders_processed--;

            if (potential_boulder.durability > 0)
            {
                potential_boulder.durability -= 1;
                if (potential_boulder.durability > 0)
                    continue;
            }

            await breakdown_boulder(potential_boulder);
            boulders_found = false;
        }

        //when the boulder is removed it plays sound and displays a balloon alert. Don't overlap when that happens
        if (boulders_found)
        {
            playsound(loc, usage_sound, 29, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
            balloon_alert_to_viewers(action);
        }
    }

    /**
     * Ejects a boulder from the machine. Used when a boulder is finished processing, or when a boulder can't be processed.
     */
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:474
    public virtual async DmTask<object> remove_boulder(obj_item_boulder specific_boulder = null)
    {
        if (QDELETED(specific_boulder))
            return TRUE;
        if (Dm.locate<obj_item_boulder>(loc) != null) //There is an boulder in our loc. it has be removed so we don't clog up our loc with even more boulders
            return FALSE;
        if (!Dm.truthy(length(specific_boulder.custom_materials)))
        {
            await specific_boulder.break_apart();
            return TRUE;
        }

        //Reset durability to little random lower value cause we have crushed it so many times
        float size = specific_boulder.boulder_size;
        if (size == BOULDER_SIZE_SMALL)
            specific_boulder.durability = Dm.rand(2, BOULDER_SIZE_SMALL - 1);
        else
            specific_boulder.durability = Dm.rand(BOULDER_SIZE_SMALL, size - 1);
        specific_boulder.processed_by = this; //so we don't take in the boulder again after we just ejected it
        await specific_boulder.forceMove(drop_location() as atom);
        specific_boulder.processed_by = null; //now since move is done we can safely clear the reference
        playsound(loc, Dm.resource("sound/machines/ping.ogg"), 50, FALSE);

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/boulder_processing/_boulder_processing.dm:46  var/static/list/loc_connections (a proc-level static, rules 10.6)
    internal DmList obj_machinery_bouldertech__post_machine_initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_bouldertech.on_entered)));
}
