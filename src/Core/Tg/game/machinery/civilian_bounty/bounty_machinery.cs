// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/civilian_bounty/bounty_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file, the civilian bounty pad and its terminal (the CivCargoHoldTerminal window) and the bounty beacon.
// The var blocks are generated (D111).

///Pad for the Civilian Bounty Control.
// tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:6
public partial class obj_machinery_piratepad_civilian : obj_machinery_piratepad
{
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:14
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float T = -2;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            T += Dm.num(micro_laser.tier);

        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            T += Dm.num(scanning_module.tier);

        cooldown_reduction = T * (30 * SECONDS);
        return dot;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:25
    public virtual float get_cooldown_reduction() => cooldown_reduction;
}

///Computer for assigning new civilian bounties, and sending bounties for collection.
// tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:29
public partial class obj_machinery_computer_piratepad_control_civilian : obj_machinery_computer_piratepad_control
{
    ///Percentage of a civilian bounty the civilian will make.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:2  (file-local, #undef at the end)
    internal const float CIV_BOUNTY_SPLIT = 30;
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:3
    private const float HIGH_PRIORITY_BOUNTY_ODDS = 20;

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:45
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!isidcard(tool))
            return NONE;
        if (!Dm.truthy(await id_insert(user, tool, inserted_scan_id)))
            return ITEM_INTERACT_BLOCKING;
        inserted_scan_id = tool as obj_item_card_id;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:59
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (Dm.truthy(cargo_hold_id))
        {
            foreach (obj_machinery_piratepad_civilian C in Dm.each_cast<obj_machinery_piratepad_civilian>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_piratepad_civilian))))
            {
                if (Dm.eq(C.cargo_hold_id, cargo_hold_id))
                {
                    pad_ref = WEAKREF(C) as datum_weakref;
                    return;
                }
            }
        }
        else
        {
            obj_machinery_piratepad_civilian pad = Dm.locate<obj_machinery_piratepad_civilian>(range(4, this));
            pad_ref = WEAKREF(pad) as datum_weakref;
        }
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:70
    public override async DmTask<object> recalc()
    {
        if (Dm.truthy(sending))
            return FALSE;
        if (inserted_scan_id == null)
        {
            status_report = I18n.T("Please insert your ID first.");
            playsound(loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
            return FALSE;
        }
        if (inserted_scan_id.registered_account.civilian_bounty == null)
        {
            status_report = I18n.T("Please accept a new civilian bounty first.");
            playsound(loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
            return FALSE;
        }
        status_report = I18n.T("Civilian Bounty: ");
        var pad = pad_ref?.resolve() as obj_machinery_piratepad_civilian;
        foreach (atom_movable possible_shippable in Dm.each<atom_movable>(get_turf(pad).contents.Copy()))
        {
            if (possible_shippable == pad)
                continue;
            if (Dm.truthy(Dm.bitand(possible_shippable.flags_1, HOLOGRAM_1)))
                continue;
            if (possible_shippable is obj_item possible_shippable_item && Dm.truthy(Dm.bitand(possible_shippable_item.item_flags, ABSTRACT)))
                continue;
            if (Dm.truthy(inserted_scan_id.registered_account.civilian_bounty.applies_to(possible_shippable as obj)))
            {
                status_report += I18n.T("Target Applicable.");
                playsound(loc, Dm.resource("sound/machines/synth/synth_yes.ogg"), 30, TRUE);
                return null;
            }
        }
        status_report += I18n.T("Not Applicable.");
        playsound(loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 30, TRUE);
        return null;
    }

    /**
     * This fully rewrites base behavior in order to only check for bounty objects, and no other types of objects like pirate-pads do.
     */
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:103
    public override async DmTask<object> send(object check_global = null, mob user = null)
    {
        status_report = "";
        playsound(loc, Dm.resource("sound/machines/wewewew.ogg"), 70, TRUE);
        if (!Dm.truthy(sending))
            return FALSE;
        datum_bank_account id_account = inserted_scan_id?.registered_account;

        // To account for check_global, we're going to construct a list of all bounties we want to check.
        DmList bounty_stack = Dm.list();

        if (Dm.truthy(check_global))
            bounty_stack = GLOB.shared_crew_bounties.Copy();
        else
            Dm.append(ref bounty_stack, id_account?.civilian_bounty);

        if (!Dm.truthy(length(bounty_stack)))
        {
            stop_sending();
            return FALSE;
        }
        int active_count = 0;
        var pad = pad_ref?.resolve() as obj_machinery_piratepad_civilian;
        foreach (atom_movable possible_shippable in Dm.each<atom_movable>(get_turf(pad).contents.Copy()))
        {
            if (possible_shippable == pad)
                continue;
            if (Dm.truthy(Dm.bitand(possible_shippable.flags_1, HOLOGRAM_1)))
                continue;
            if (possible_shippable is obj_item possible_shippable_item && Dm.truthy(Dm.bitand(possible_shippable_item.item_flags, ABSTRACT)))
                continue;

            foreach (datum_bounty stack_item in Dm.each<datum_bounty>(bounty_stack))
            {
                var shipped = possible_shippable as obj;
                if (Dm.truthy(stack_item.applies_to(shipped)))
                {
                    active_count++;
                    LAZYADDASSOC(ref stack_item.contribution, id_account, stack_item.contribution_amount(shipped));
                    if (Dm.num(stack_item.contribution[id_account]) <= 0 || !Dm.truthy(stack_item.contribution[id_account]))
                        stack_item.contribution[id_account] = stack_item.contribution_amount(shipped);
                    await stack_item.ship(shipped);
                    qdel(possible_shippable);
                }
            }
        }

        if (active_count >= 1)
        {
            status_report += I18n.T("Bounty Target[] Found x[]. ", active_count > 1 ? "s" : "", active_count);

            SStgui.update_uis(this); //update Ui data to display how much of the bounty remains
        }
        else
        {
            status_report = I18n.T("No applicable target found. Aborting. ");
            stop_sending();
        }

        active_count = 0; // We'll just re-use this for the second message setter.
        foreach (datum_bounty stack_item in Dm.each<datum_bounty>(bounty_stack))
        {
            if (Dm.truthy(stack_item.can_claim()))
            {
                active_count++;
                //Pay for the bounty with the ID's department funds.
                SSblackbox.record_feedback("tally", "bounties_completed", 1, stack_item.type);
                stack_item.claimed = TRUE;
                stack_item.on_claimed(inserted_scan_id);
                // Unique case: A global bounty is completed, and you have the same bounty as a personal bounty,
                // it will complete your personal one as well. It will however only increment the tracker by one.
                if (Dm.truthy(check_global))
                {
                    SSeconomy.civ_bounty_tracker++; //This is the tracker for adding more global bounties, not for logging purposes.
                    foreach (datum_bank_account helper in Dm.each<datum_bank_account>(stack_item.contribution))
                    {
                        if (helper?.civilian_bounty != null && helper.civilian_bounty.GetType().IsAssignableTo(stack_item.GetType()))
                        {
                            helper.reset_bounty(inserted_scan_id);
                            await helper.bank_card_talk(I18n.T("Your [] bounty has been completed for matching the completed station bounty!", stack_item.name));
                        }
                    }
                }
                else
                    id_account.reset_bounty(inserted_scan_id);

                var reward = Dm.@new<obj_item_bounty_cube>(drop_location());
                await reward.set_up__proc(stack_item, inserted_scan_id);
            }
        }
        if (active_count >= 1)
        {
            status_report += I18n.T("x[] Bount[] completed! Please give your bounty cube[] to cargo for your automated payout shortly. ", active_count, active_count > 1 ? "ies" : "y", active_count > 1 ? "s" : "");
        }

        if (Dm.truthy(check_global))
            update_global_bounty_list(round(CIV_BOUNTY_BASELINE + (SSeconomy.civ_bounty_tracker / 3)), FALSE);

        pad.visible_message(span_notice(I18n.T("[] activates!", pad)));
        pad.finish_sending();
        playsound(loc, Dm.resource("sound/machines/synth/synth_yes.ogg"), 30, TRUE);
        sending = FALSE;
        return TRUE;
    }

    ///Here is where cargo bounties are added to the player's bank accounts, then adjusted and scaled into a civilian bounty.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:185
    public virtual async DmTask<object> add_bounties(mob user = null, float cooldown_reduction = 0)
    {
        datum_bank_account id_account = inserted_scan_id?.registered_account;
        if (id_account == null)
            return FALSE;
        if ((id_account.civilian_bounty != null || Dm.truthy(id_account.bounties)) && !(world.time >= id_account.bounty_timer)) // COOLDOWN_FINISHED
        {
            string time_left = DisplayTimeText(id_account.bounty_timer - world.time, round_seconds_to: 1); // COOLDOWN_TIMELEFT
            balloon_alert(user, I18n.T("try again in []!", time_left));
            return FALSE;
        }
        if (inserted_scan_id.trim == null)
        {
            await say(I18n.T("Requesting ID card has no job assignment registered!"));
            return FALSE;
        }

        DmList crumbs = ((datum_id_trim)inserted_scan_id.trim).generate_bounty_list();
        id_account.bounty_timer = world.time + ((5 * MINUTES) - cooldown_reduction); // COOLDOWN_START
        id_account.bounties = crumbs;
        return TRUE;
    }

    /**
     * Proc that assigned a civilian bounty to an ID card, from the list of potential bounties that that bank account currently has available.
     * Available choices are assigned during add_bounties, and one is locked in here.
     *
     * @param choice The index of the bounty in the list of bounties that the player can choose from.
     */
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:209
    public virtual datum_bounty pick_bounty(object choice = null)
    {
        datum_bank_account id_account = inserted_scan_id?.registered_account;
        if (!Dm.truthy(id_account?.bounties?[choice]))
        {
            playsound(loc, Dm.resource("sound/machines/synth/synth_no.ogg"), 40, TRUE);
            return null;
        }
        id_account.set_bounty(id_account.bounties[choice] as datum_bounty, inserted_scan_id);
        id_account.bounties = null;
        SSblackbox.record_feedback("tally", "bounties_assigned", 1, id_account.civilian_bounty.type);
        return id_account.civilian_bounty;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:219
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await id_eject(user, inserted_scan_id);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:223
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["points"] = points;
        data["pad"] = pad_ref?.resolve() != null ? TRUE : FALSE;
        data["sending"] = sending;
        data["status_report"] = status_report;
        data["id_inserted"] = inserted_scan_id;
        data["claimed_bounties"] = SSeconomy.civ_bounty_tracker;

        // Personal bounty data:
        datum_bank_account account = inserted_scan_id?.registered_account;
        if (account != null)
        {
            if (account.civilian_bounty != null)
            {
                data["id_bounty_info"] = account.civilian_bounty.description;
                data["id_bounty_num"] = account.bounty_num();
                data["id_bounty_value"] = Dm.num(account.civilian_bounty.get_bounty_reward()) * (CIV_BOUNTY_SPLIT / 100);
            }
            if (Dm.truthy(account.bounties))
            {
                data["picking"] = TRUE;
                DmList names = Dm.list();
                DmList infos = Dm.list();
                DmList values = Dm.list();
                foreach (datum_bounty bounty in Dm.each_cast<datum_bounty>(account.bounties))
                {
                    Dm.append(ref names, bounty.name);
                    Dm.append(ref infos, bounty.description);
                    Dm.append(ref values, Dm.num(bounty.get_bounty_reward()) * (CIV_BOUNTY_SPLIT / 100));
                }
                data["id_bounty_names"] = names;
                data["id_bounty_infos"] = infos;
                data["id_bounty_values"] = values;
            }
            else
                data["picking"] = FALSE;
        }

        // Global bounty data:
        DmList listBounty = Dm.list();
        foreach (datum_bounty global_bounty in Dm.each_cast<datum_bounty>(GLOB.shared_crew_bounties))
        {
            object ship_max = global_bounty.get_max();
            object ship_total = global_bounty.get_total(); //Present value as a percentage, we'll handle 0 and 100 as constants

            if (Dm.eq(listBounty["name"], global_bounty.name))
                continue;
            Dm.append(ref listBounty, Dm.list(Dm.list(
                Dm.kv("name", global_bounty.name),
                Dm.kv("description", global_bounty.description),
                Dm.kv("reward", global_bounty.get_bounty_reward()),
                Dm.kv("claimed", global_bounty.claimed),
                Dm.kv("shipped", ship_total),
                Dm.kv("maximum", ship_max),
                Dm.kv("priority", global_bounty.high_priority),
                Dm.kv("unique", global_bounty.unique))));
        }
        data["listBounty"] = listBounty;

        return data;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:273
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        var pad = pad_ref?.resolve() as obj_machinery_piratepad_civilian;
        if (pad == null)
            return dot;
        mob user = ui.user;
        if (!Dm.truthy(await user.can_perform_action(this)) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return dot;
        switch (Dm.str(action)) //several ui_acts are handled on parent by piratepad
        {
            case "pick":
                pick_bounty(@params["value"]);
                return TRUE;
            case "bounty":
                await add_bounties(user, pad.get_cooldown_reduction());
                return TRUE;
            case "eject":
                await id_eject(user, inserted_scan_id);
                inserted_scan_id = null;
                return TRUE;
            case "update_list":
            {
                playsound(this, Dm.resource("sound/machines/data_transmission.ogg"), 50); // Should only need to play once per round due to the list auto-updating afterwards.

                float bonus_bounties = clamp(round(length(GLOB.player_list) / 8f), 0, 5); // The number of bounties to be generated is 5 + 1-per every 8 players on the server, up to a max of 10 total.
                looped_global_update(1, CIV_BOUNTY_BASELINE + bonus_bounties, first_time: TRUE); // Just for visual flair
                return TRUE;
            }
            case "print":
                print_sheet(user as mob_living);
                break;
        }
        return dot;
    }

    /// Self explanitory, holds the ID card in the console for bounty payout and manipulation.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:302
    public virtual async DmTask<object> id_insert(mob user = null, obj_item inserting_item = null, obj_item target = null)
    {
        var card_to_insert = inserting_item as obj_item_card_id;
        bool holder_item = false;

        if (!isidcard(card_to_insert))
        {
            card_to_insert = await inserting_item.remove_id() as obj_item_card_id;
            holder_item = true;
        }

        if (card_to_insert == null || !Dm.truthy(await user.transferItemToLoc(card_to_insert, this)))
            return FALSE;

        if (target != null)
        {
            if (holder_item && Dm.truthy(await inserting_item.insert_id(target)))
                playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);
            else
                await id_eject(user, target);
        }

        user.visible_message(span_notice(I18n.T("[] inserts \\the [] into \\the [].", user, card_to_insert, this)),
            span_notice(I18n.T("You insert \\the [] into \\the [].", card_to_insert, this)));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);
        await ui_interact(user);
        return TRUE;
    }

    ///Removes A stored ID card.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:326
    public virtual async DmTask<object> id_eject(mob user = null, obj_item target = null)
    {
        if (target == null)
        {
            to_chat(user, span_warning(I18n.T("That slot is empty!")));
            return FALSE;
        }
        else
        {
            await try_put_in_hand(target, user as mob_living);
            user.visible_message(span_notice(I18n.T("[] gets \\the [] from \\the [].", user, target, this)),
                span_notice(I18n.T("You get \\the [] from \\the [].", target, this)));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);
            inserted_scan_id = null;
            return TRUE;
        }
    }

    /**
     * Updates the global bounty list: First by sorting through all completed bounties on the list and deleting them.
     * Then, adds new bounties up to the limit, defined by the update_up_to argument.
     * The bounties to be added should not share duplicates between job subtypes.
     * @param update_up_to How many new bounties to add to the list, up to the maximum defined by MAXIMUM_BOUNTY_JOBS.
     * @param enable_high_priority If enabled, means that past a prob(HIGH_PRIORITY_BOUNTY_ODDS), the created bounty will have a 1.5x value multiplier and be labeled in the UI.
     * @param running_jobs A list we generate when making multiple bounties at once, this stores the job define to prevent double dipping on the same job type.
     */
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:346
    public virtual DmList update_global_bounty_list(float update_up_to = CIV_BOUNTY_BASELINE, object enable_high_priority = null, DmList running_jobs = null)
    {
        //First, clear out completed bounties.
        foreach (datum_bounty complete_or_unique in Dm.each<datum_bounty>(GLOB.shared_crew_bounties.Copy()))
        {
            if (Dm.truthy(complete_or_unique.claimed))
                Dm.remove(ref GLOB.shared_crew_bounties, complete_or_unique);
            if (Dm.truthy(complete_or_unique.unique))
                update_up_to++; // We're doing this to ignore the quantity of unique bounties in the global list.
        }

        //Then, add new bounties up to the limit.
        DmList jobs_picked = running_jobs ?? Dm.list();
        while (length(GLOB.shared_crew_bounties) < update_up_to)
        {
            float job_code = rand(CIV_JOB_BASIC, CIV_JOB_BITRUN); //CIV_JOB_ defines taken from _DEFINES/economy.dm. If new job bounty classes are added, swap out our maximum.
            if (Dm.@in(job_code, jobs_picked))
                continue;
            Dm.append(ref jobs_picked, job_code);

            datum_bounty new_bounty = random_bounty(job_code);
            if (Dm.truthy(new_bounty.global_exempt))
                continue;

            GLOB.shared_crew_bounties.Insert(0, new_bounty);

            if (Dm.truthy(enable_high_priority) && prob(HIGH_PRIORITY_BOUNTY_ODDS))
            {
                new_bounty.high_priority = TRUE;
                new_bounty.description = Dm.text("[][]", new_bounty.description, I18n.T("</br>This bounty is marked as <b>high priority</b>, and will reward <b>1.5x</b> the normal payout!"));
            }
        }
        return jobs_picked;
    }

    /// Performs several global bounty updates in a row on a callback loop, adding one each time.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:375
    public virtual object looped_global_update(float current_count = 0, float update_to = 0, DmList inherited_list = null, object first_time = null)
    {
        DmList jobs_picked = update_global_bounty_list(current_count, enable_high_priority: TRUE, running_jobs: inherited_list);

        if (current_count == update_to)
        {
            if (Dm.truthy(first_time))
                setup_special_procs();
            return TRUE;
        }
        current_count++;
        _addtimer(Dm.@new<datum_callback>(this, nameof(looped_global_update), current_count, update_to, jobs_picked, first_time), 0.8f * SECONDS, file: "code/game/machinery/civilian_bounty/bounty_machinery.dm", line: 382);
        return FALSE;
    }

    /// Spawns the roundstart "special" bounties.
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:387
    public virtual void setup_special_procs()
    {
        foreach (Type selected_special in Dm.each_cast<Type>(subtypesof(typeof(datum_bounty_item_special))))
            Dm.append(ref GLOB.shared_crew_bounties, Dm.@new(selected_special));
    }

    /**
     * Handles cooldowns and creation of a new cargo bounty sheet.
     */
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:394
    public virtual bool print_sheet(mob_living user = null)
    {
        if (!(world.time >= sheet_printer_cooldown)) // COOLDOWN_FINISHED
        {
            balloon_alert(user, I18n.T("printer spooling!"));
            return false;
        }

        var paper = Dm.@new<obj_item_paper>(loc);
        paper.name = I18n.T("paper - Bounties");

        DmList printout_text = Dm.list();
        Dm.append(ref printout_text, I18n.T("<center><h2>Nanotrasen Cargo Bounties</h2></center>"));

        foreach (datum_bounty current_bounty in Dm.each<datum_bounty>(GLOB.shared_crew_bounties))
        {
            if (Dm.truthy(current_bounty.claimed))
                continue;
            Dm.append(ref printout_text, I18n.T("<h3>[]</h3>\n\t\t\t<ul>\n\t\t\t<li>Quantity requested: <i>[]</i>\n\t\t\t<li>Reward: <b>[]</b> cr.</li>\n\t\t\t<li>Cut: <b>[]</b> cr.</li>\n\t\t\t</ul>",
                current_bounty.name, current_bounty.print_required(), current_bounty.get_bounty_reward(), round(BOUNTY_CUT_STANDARD * Dm.num(current_bounty.get_bounty_reward()))));
        }
        paper.add_raw_text(printout_text.Join(""));
        paper.update_appearance();

        playsound(this, Dm.resource("sound/machines/printer.ogg"), 100, TRUE);
        sheet_printer_cooldown = world.time + 4 * SECONDS; // COOLDOWN_START
        return true;
    }
}

// tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:53  (defined twice on the pad terminal: the pirates' file's is the later one)
public partial class obj_machinery_computer_piratepad_control
{
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:53
    public async DmTask<object> multitool_act__d0(mob_living user = null, obj_item tool = null)
    {
        if (tool is obj_item_multitool I && I.buffer is obj_machinery_piratepad_civilian)
        {
            to_chat(user, span_notice(I18n.T("You link [] with [] in [] buffer.", this, I.buffer, I)));
            pad_ref = WEAKREF(I.buffer) as datum_weakref;
            return TRUE;
        }
        return await base.multitool_act(user, tool);
    }
}

///Beacon to launch a new bounty setup when activated.
// tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:422
public partial class obj_item_civ_bounty_beacon : obj_item
{
    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:429
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        loc.visible_message(span_warning(I18n.T("\\The [] begins to beep loudly!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(launch_payload)), 1 * SECONDS, file: "code/game/machinery/civilian_bounty/bounty_machinery.dm", line: 431);
        return null;
    }

    // tg: code/game/machinery/civilian_bounty/bounty_machinery.dm:433
    public virtual void launch_payload()
    {
        playsound(this, SFX_SPARKS, 80, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        switch ((int)uses)
        {
            case 2:
                Dm.@new<obj_machinery_piratepad_civilian>(drop_location());
                break;
            case 1:
                Dm.@new<obj_machinery_computer_piratepad_control_civilian>(drop_location());
                qdel(this);
                break;
        }
        uses--;
    }
}
