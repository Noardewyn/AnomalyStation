// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/laborcamp/laborstacker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2b (D304): the whole file. The var blocks and the release announcement's entry are generated (D111).

/**********************Prisoners' Console**************************/

// tg: code/modules/mining/laborcamp/laborstacker.dm:3
public partial class obj_machinery_mineral_labor_claim_console : obj_machinery_mineral
{
    // tg: code/modules/mining/laborcamp/laborstacker.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        locate_stacking_machine();
        if (SSshuttle != null) // a host-built room without the Master has no SSshuttle
        {
            if (!Dm.truthy(SSshuttle.initialized))
                RegisterSignal(SSshuttle, COMSIG_SUBSYSTEM_POST_INITIALIZE, nameof(register_shuttle_signal));
            else
                register_shuttle_signal();
        }
        //If we can't find a stacking machine end it all ok?
        if (stacking_machine == null)
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:27
    [SignalHandler]
    public virtual void register_shuttle_signal(datum source = null)
    {
        obj_docking_port_mobile laborshuttle = SSshuttle.getShuttle("laborcamp") as obj_docking_port_mobile;
        if (laborshuttle != null)
            RegisterSignal(laborshuttle, COMSIG_SHUTTLE_SHOULD_MOVE, nameof(on_laborshuttle_can_move));
        UnregisterSignal(SSshuttle, COMSIG_SUBSYSTEM_POST_INITIALIZE);
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:34
    public override object Destroy(float? force = null)
    {
        if (stacking_machine != null)
        {
            stacking_machine.labor_console = null;
            stacking_machine = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:43
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LaborClaimConsole", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:49
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        bool can_go_home = false;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            can_go_home = true;
        obj_item_card_id worn_id = null;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            worn_id = living_user.get_idcard(TRUE) as obj_item_card_id;
        }
        if (worn_id is obj_item_card_id_advanced_prisoner worn_prisoner_id)
        {
            data["id_points"] = worn_prisoner_id.points;
            if (!Dm.truthy(worn_prisoner_id.goal))
                data["status_info"] = "No goal set!";
            else if (worn_prisoner_id.points >= Dm.num(worn_prisoner_id.goal))
            {
                can_go_home = true;
                data["status_info"] = "Goal met!";
            }
            else
                data["status_info"] = Dm.text("You are [] points away.", Dm.num(worn_prisoner_id.goal) - worn_prisoner_id.points);
        }
        else
        {
            data["status_info"] = "No Prisoner ID detected.";
            data["id_points"] = 0;
        }

        if (stacking_machine != null)
            data["unclaimed_points"] = stacking_machine.points;

        data["can_go_home"] = can_go_home ? TRUE : FALSE;
        return data;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:79
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        data["shuttle_exists"] = SSshuttle?.getShuttle("laborcamp") != null ? TRUE : FALSE; // a host-built room without the Master has no SSshuttle
        return data;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:84
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user_mob = GlobalProcs.usr;

        switch (action as string)
        {
            case "claim_points":
            {
                obj_item_card_id worn_id = null;
                if (isliving(user_mob))
                {
                    mob_living living_mob = (mob_living)user_mob;
                    worn_id = living_mob.get_idcard(TRUE) as obj_item_card_id;
                }
                if (worn_id is obj_item_card_id_advanced_prisoner worn_prisoner_id)
                {
                    worn_prisoner_id.points += stacking_machine.points;
                    stacking_machine.points = 0;
                    await say("Points transferred.");
                    return TRUE;
                }
                else
                {
                    if (say_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1)
                    {
                        await say("No valid id for point transfer detected.");
                        say_cooldown = world.time + 2 * SECONDS;
                    }
                }
                break;
            }
            case "move_shuttle":
            {
                if (SSshuttle.getShuttle("laborcamp") == null)
                {
                    if (say_cooldown <= world.time)
                    {
                        await say("Shuttle not found.");
                        say_cooldown = world.time + 2 * SECONDS;
                    }
                    return dot;
                }

                DmList labor_shuttle_mobs = find_labor_shuttle_mobs();
                if (length(labor_shuttle_mobs) > 1 || labor_shuttle_mobs[1] != user_mob)
                {
                    if (say_cooldown <= world.time)
                    {
                        await say("Prisoners may only be released one at a time.");
                        say_cooldown = world.time + 2 * SECONDS;
                    }
                    return dot;
                }

                switch (Dm.num(await SSshuttle.moveShuttle("laborcamp", "laborcamp_home", TRUE)))
                {
                    case 1:
                        if (say_cooldown <= world.time)
                        {
                            await say("Shuttle not found.");
                            say_cooldown = world.time + 2 * SECONDS;
                        }
                        break;
                    case 2:
                        if (say_cooldown <= world.time)
                        {
                            await say("Shuttle already at station.");
                            say_cooldown = world.time + 2 * SECONDS;
                        }
                        break;
                    case 3:
                        if (say_cooldown <= world.time)
                        {
                            await say("No permission to dock could be granted.");
                            say_cooldown = world.time + 2 * SECONDS;
                        }
                        break;
                    default:
                        if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                        {
                            datum_record_crew target = find_record(user_mob.real_name) as datum_record_crew;
                            if (target != null)
                                target.wanted_status = WANTED_PAROLE;

                            await aas_config_announce(typeof(datum_aas_config_entry_security_labor_stacker), Dm.list(Dm.kv("PERSON", user_mob.real_name)), this, Dm.list(RADIO_CHANNEL_SECURITY));
                        }
                        user_mob.log_message("has completed their labor points goal and is now sending the gulag shuttle back to the station.", LOG_GAME);
                        await say("Labor sentence finished, shuttle returning.");
                        initiated_launch = TRUE;
                        return TRUE;
                }
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:157
    public virtual DmList find_labor_shuttle_mobs()
    {
        DmList prisoners = mobs_in_area_type(Dm.list(get_area(this))) as DmList;

        // security personnel and nonhumans do not count towards this
        foreach (mob_living mob in Dm.each<mob_living>(prisoners))
        {
            obj_item_card_id card = mob.get_idcard(FALSE) as obj_item_card_id;
            if (!ishuman(mob) || Dm.@in(ACCESS_BRIG, card?.GetAccess()))
                prisoners.Remove(mob);
        }

        return prisoners;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:168
    [SignalHandler]
    public virtual object on_laborshuttle_can_move(obj_docking_port_mobile source = null)
    {
        if (Dm.truthy(initiated_launch) && length(find_labor_shuttle_mobs()) > 1)
        {
            initiated_launch = FALSE;
            Waiting.NoSleep(say("Takeoff aborted. Prisoners may only be released one at a time."));
            return BLOCK_SHUTTLE_MOVE;
        }
        return null;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:176
    public virtual void locate_stacking_machine()
    {
        stacking_machine = Dm.locate<obj_machinery_mineral_stacking_machine>(dview(2, get_turf(this))) as obj_machinery_mineral_stacking_machine_laborstacker;
        if (stacking_machine != null)
            stacking_machine.labor_console = this;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:181
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;

        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, "id authenticator short-circuited");
        visible_message(span_warning(I18n.T("[] lets out a few sparks!", this)));
        do_sparks(2, TRUE, this);
        await DmTask.CompletedTask;
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/mining/laborcamp/laborstacker.dm:40
    public static object cmp_sheet_list(DmList a = null, DmList b = null)
    {
        return Dm.sub(a["value"], b["value"]);
    }
}

/**********************Prisoner Collection Unit**************************/

// tg: code/modules/mining/laborcamp/laborstacker.dm:185
public partial class obj_machinery_mineral_stacking_machine_laborstacker : obj_machinery_mineral_stacking_machine
{
    // tg: code/modules/mining/laborcamp/laborstacker.dm:192
    public override object Destroy(float? force = null)
    {
        if (labor_console != null)
        {
            labor_console.stacking_machine = null;
            labor_console = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:198
    public override async DmTask process_stack(obj_item_stack input = null)
    {
        if (!istype(input, typeof(obj_item_stack_sheet)))
        {
            await base.process_stack(input);
            return;
        }
        obj_item_stack_sheet sheet = (obj_item_stack_sheet)input;
        if (Dm.truthy(sheet.manufactured) && Dm.truthy(sheet.gulag_value))
            points += sheet.gulag_value * Dm.num(sheet.amount);
        await base.process_stack(input);
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:206
    public override async DmTask<object> base_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(is_type_in_typecache(tool, accepted_types)))
        {
            await process_stack((obj_item_stack)tool);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.base_item_interaction(user, tool, modifiers);
    }
}

/**********************Point Lookup Console**************************/

// tg: code/modules/mining/laborcamp/laborstacker.dm:213
public partial class obj_machinery_mineral_labor_points_checker : obj_machinery_mineral
{
    // tg: code/modules/mining/laborcamp/laborstacker.dm:220
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || Dm.truthy(user.is_blind()))
            return dot;
        user.examinate(this);
        return dot;
    }

    // tg: code/modules/mining/laborcamp/laborstacker.dm:226
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_card_id_advanced_prisoner prisoner_id)
            return NONE;

        if (!Dm.truthy(prisoner_id.goal)) //no goal to reach
        {
            await say("No goal required for this ID.");
            return ITEM_INTERACT_BLOCKING;
        }

        await say(Dm.text("ID: [].", prisoner_id.registered_name));
        await say(Dm.text("Points Collected: [] / [].", prisoner_id.points, prisoner_id.goal));
        await say("Collect points by bringing smelted minerals to the Labor Shuttle stacking machine. Reach your quota to earn your release.");
        return ITEM_INTERACT_SUCCESS;
    }
}
