// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/robot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the robotics control console, whole, with tgui's RoboticsControlConsole window. isAdminGhostAI is the
// GAP(lead) of is_helpers.cs (no admin ghosts yet): its branches read false. The var block is generated (D111).

// tg: code/game/machinery/computer/robot.dm:1
public partial class obj_machinery_computer_robotics : obj_machinery_computer
{
    // tg: code/game/machinery/computer/robot.dm:14
    public virtual object can_control(mob user = null, mob_living_silicon_robot R = null)
    {
        object dot = FALSE;
        if (R == null)
            return dot;
        if (isAI(user))
        {
            if (R.connected_ai != user)
                return dot;
        }
        if (iscyborg(user))
        {
            if (R != user)
                return dot;
        }
        if (Dm.truthy(R.scrambledcodes))
            return dot;
        return TRUE;
    }

    // tg: code/game/machinery/computer/robot.dm:28
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "RoboticsControlConsole", name);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/robot.dm:35
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["can_hack"] = FALSE;
        if (issilicon(user))
        {
            mob_living_silicon S = (mob_living_silicon)user;
            if (Dm.truthy(S.hack_software))
                data["can_hack"] = TRUE;
        }
        // else if(isAdminGhostAI(user)) data["can_hack"] = TRUE: no admin ghosts yet (GAP(lead), is_helpers.cs)

        data["can_detonate"] = FALSE;
        if (isAI(user))
        {
            mob_living_silicon_ai ai = (mob_living_silicon_ai)user;
            data["can_detonate"] = ai.malf_picker != null ? TRUE : FALSE;
        }

        turf current_turf = get_turf(this);
        data["cyborgs"] = Dm.list();
        foreach (mob_living_silicon_robot R in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
        {
            if (!Dm.truthy(can_control(user, R)))
                continue;
            if (!Dm.truthy(is_valid_z_level(current_turf, get_turf(R))))
                continue;
            DmList cyborg_data = Dm.list(
                Dm.kv("name", R.name),
                Dm.kv("locked_down", R.lockcharge),
                Dm.kv("status", R.stat),
                Dm.kv("charge", R.cell is obj_item_stock_parts_power_store borg_cell ? round(Dm.num(borg_cell.percent())) : null),
                Dm.kv("module", R.model != null ? Dm.text("[] Model", R.model.name) : "No Model Detected"),
                Dm.kv("synchronization", R.connected_ai),
                Dm.kv("emagged", R.emagged),
                Dm.kv("ref", REF(R)));
            data["cyborgs"] = Dm.add(data["cyborgs"], Dm.list(cyborg_data));
        }

        data["drones"] = Dm.list();
        foreach (mob_living_basic_drone drone in Dm.each<mob_living_basic_drone>(GLOB.drones_list))
        {
            if (Dm.truthy(drone.hacked))
                continue;
            if (!Dm.truthy(is_valid_z_level(current_turf, get_turf(drone))))
                continue;
            DmList drone_data = Dm.list(
                Dm.kv("name", drone.name),
                Dm.kv("status", drone.stat),
                Dm.kv("ref", REF(drone)));
            data["drones"] = Dm.add(data["drones"], Dm.list(drone_data));
        }

        return data;
    }

    // tg: code/game/machinery/computer/robot.dm:85
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.eq(action, "stopbot"))
        {
            if (Dm.truthy(await allowed__proc(usr)))
            {
                mob_living_silicon_robot R = locate_ref_in(@params["ref"], GLOB.silicon_mobs) as mob_living_silicon_robot;
                if (Dm.truthy(can_control(usr, R)) && !Dm.truthy(await base.ui_act(action, @params, ui, state)))
                {
                    if (isAI(usr))
                    {
                        if (Dm.truthy(R.lockcharge))
                        {
                            if (Dm.truthy(R.ai_lockdown))
                            {
                                R.ai_lockdown = FALSE;
                                await lock_unlock_borg(R);
                            }
                            else
                                to_chat(usr, span_danger(I18n.T("Cyborg locked by an user with superior permissions.")));
                        }
                        else
                        {
                            R.ai_lockdown = TRUE;
                            await lock_unlock_borg(R);
                        }
                    }
                    else
                    {
                        if (locked_down_borg == null && !Dm.truthy(R.lockcharge)) //If there is no borg locked down by the console yet
                        {
                            await lock_unlock_borg(R, ((atom)loc.loc).name);
                            R.ai_lockdown = FALSE; //Just in case I'm stupid
                            locked_down_borg = R;
                            RegisterSignal(R, COMSIG_QDELETING, nameof(borg_destroyed));
                        }
                        else if (locked_down_borg == R) //If the borg locked down by the console is the same as the one we're trying to unlock
                            await lock_unlock_borg(R);
                        else if (Dm.truthy(R.lockcharge) && Dm.truthy(R.ai_lockdown))
                        {
                            R.ai_lockdown = FALSE;
                            await lock_unlock_borg(R);
                        }
                        else if (Dm.truthy(R.lockcharge) && locked_down_borg != R)
                            to_chat(usr, span_danger(I18n.T("The cyborg was locked by a different console.")));
                        else
                            to_chat(usr, span_danger(I18n.T("You can lock down only one cyborg at a time.")));
                    }
                }
            }
            else
                to_chat(usr, span_danger(I18n.T("Access Denied.")));
            if (locked_down_borg != null)
                use_power = ACTIVE_POWER_USE;
            else
                use_power = IDLE_POWER_USE;
        }
        else if (Dm.eq(action, "killbot")) //Malf AIs, and AIs with a combat upgrade, can detonate their cyborgs remotely.
        {
            if (!isAI(usr))
                return null;
            mob_living_silicon_ai ai = (mob_living_silicon_ai)usr;
            if (ai.malf_picker == null)
                return null;
            mob_living_silicon_robot target = locate_ref_in(@params["ref"], GLOB.silicon_mobs) as mob_living_silicon_robot;
            if (target == null)
                return null;
            if (target.connected_ai != ai)
                return null;
            await target.self_destruct(usr);
        }
        else if (Dm.eq(action, "magbot"))
        {
            mob_living_silicon S = usr as mob_living_silicon;
            // || isAdminGhostAI(usr): no admin ghosts yet (GAP(lead), is_helpers.cs)
            if (S != null && Dm.truthy(S.hack_software))
            {
                mob_living_silicon_robot R = locate_ref_in(@params["ref"], GLOB.silicon_mobs) as mob_living_silicon_robot;
                if (R != null && !Dm.truthy(R.emagged) && R.connected_ai == usr && !Dm.truthy(R.scrambledcodes) && Dm.truthy(can_control(usr, R)))
                {
                    log_silicon(Dm.text("[] emagged [] using robotic console!", key_name(usr), key_name(R)));
                    message_admins(Dm.text("[] emagged cyborg [] using robotic console!", ADMIN_LOOKUPFLW(usr), key_name_admin(R)));
                    await R.SetEmagged(TRUE);
                    await R.logevent(Dm.text("WARN: root privleges granted to PID [][].", num2hex(rand(1, 65535), -1), num2hex(rand(1, 65535), -1))); //random eight digit hex value. Two are used because rand(1,4294967295) throws an error
                }
            }
        }
        else if (Dm.eq(action, "killdrone"))
        {
            if (Dm.truthy(await allowed__proc(usr)))
            {
                mob_living_basic_drone drone = locate_ref_in(@params["ref"], GLOB.mob_list) as mob_living_basic_drone;
                if (Dm.truthy(drone.hacked))
                    to_chat(usr, span_danger(I18n.T("ERROR: [] is not responding to external commands.", drone)));
                else
                {
                    turf T = get_turf(drone);
                    message_admins(Dm.text("[] detonated [] at []!", ADMIN_LOOKUPFLW(usr), key_name_admin(drone), ADMIN_VERBOSEJMP(T)));
                    log_silicon(Dm.text("[] detonated []!", key_name(usr), key_name(drone)));
                    do_sparks(3, Dm.lt(TRUE, drone) ? TRUE : FALSE); // tg's typo: do_sparks(3, TRUE< drone)
                    drone.visible_message(span_danger(I18n.T("\\the [] self-destructs!", drone)));
                    drone.investigate_log("has been gibbed by a robotics console.", INVESTIGATE_DEATHS);
                    await drone.gib();
                }
            }
        }
        return null;
    }

    // I feel like this should be changed, but I have no idea in what way exactly, so I just extracted it to make the code less of a mess
    // tg: code/game/machinery/computer/robot.dm:166
    public virtual async DmTask lock_unlock_borg(mob_living_silicon_robot R = null, object console_location = null)
    {
        if (Dm.truthy(R.lockcharge) && locked_down_borg == R)
        {
            UnregisterSignal(locked_down_borg, COMSIG_QDELETING);
            locked_down_borg = null;
        }
        message_admins(span_notice(Dm.text("[] [] []!", ADMIN_LOOKUPFLW(usr), !Dm.truthy(R.lockcharge) ? "locked down" : "released", ADMIN_LOOKUPFLW(R))));
        log_silicon(Dm.text("[] [] []!", key_name(usr), !Dm.truthy(R.lockcharge) ? "locked down" : "released", key_name(R)));
        log_combat(usr, R, Dm.text("[] cyborg", !Dm.truthy(R.lockcharge) ? "locked down" : "released"));
        await R.SetLockdown(!Dm.truthy(R.lockcharge) ? TRUE : FALSE);
        to_chat(R, !Dm.truthy(R.lockcharge) ? span_notice(I18n.T("Your lockdown has been lifted!")) : span_alert(I18n.T("You have been locked down!")));
        if (console_location != null)
            to_chat(R, span_alert(I18n.T("The approximate location of the console that is keeping you locked down is []", console_location)));
        if (R.connected_ai != null)
            to_chat(R.connected_ai, Dm.text("[]: <a href='byond://?src=[];track=[]'>[]</a><br>", !Dm.truthy(R.lockcharge) ? span_notice(I18n.T("NOTICE - Cyborg lockdown lifted")) : span_alert(I18n.T("ALERT - Cyborg lockdown detected")), REF(R.connected_ai), html_encode(R.name), R.name));
    }

    // tg: code/game/machinery/computer/robot.dm:180
    [SignalHandler]
    public virtual void borg_destroyed()
    {
        locked_down_borg = null;
    }

    // tg: code/game/machinery/computer/robot.dm:184
    public override async DmTask on_set_machine_stat(object old_value = null) //depowering the console unlocks the borg
    {
        if (locked_down_borg != null)
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))) && Dm.truthy(locked_down_borg.lockcharge))
                await lock_unlock_borg(locked_down_borg);
        }
        await base.on_set_machine_stat(old_value);
    }

    // tg: code/game/machinery/computer/robot.dm:190
    public override async DmTask<object> atom_break(object damage_flag = null) // This shouldnt be needed, but hitting console doesnt trigger destroy apparently
    {
        if (locked_down_borg != null)
            await lock_unlock_borg(locked_down_borg);
        return await base.atom_break(damage_flag);
    }

    // tg: code/game/machinery/computer/robot.dm:195
    public override object Destroy(float? force = null)
    {
        if (locked_down_borg != null)
            Waiting.NoSleep(lock_unlock_borg(locked_down_borg));
        return base.Destroy(force);
    }
}

public static partial class GlobalProcs
{
    // locate(ref) in list: the list's thing whose REF is the given text
    internal static object locate_ref_in(object reference, DmList list)
    {
        object found = Dm.locate_ref(reference);
        return found != null && Dm.@in(found, list) ? found : null;
    }
}
