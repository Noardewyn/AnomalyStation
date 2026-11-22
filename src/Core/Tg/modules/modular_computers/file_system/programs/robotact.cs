// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/robotact.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326): the cyborg's self-management app, its window NtosRobotact. The var block is generated (D111).

// tg: code/modules/modular_computers/file_system/programs/robotact.dm:1
public partial class datum_computer_file_program_robotact : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/robotact.dm:15
    public override async DmTask<object> on_start(mob_living user = null)
    {
        if (computer is not obj_item_modular_computer_pda_silicon)
        {
            to_chat(user, span_warning(I18n.T("A warning flashes across \\the []: Device Incompatible.", computer)));
            return FALSE;
        }
        object dot = await base.on_start(user);
        if (Dm.truthy(dot))
        {
            obj_item_modular_computer_pda_silicon tablet = computer as obj_item_modular_computer_pda_silicon;
            if (tablet.device_theme == PDA_THEME_SYNDICATE)
                program_open_overlay = "command-syndicate";
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Checks if we should see a specific cyborg on our "network". Arguments are our borg, and another borg
     *
     * Intended to allow borgs with the same AI to see eachother, syndicate borgs (scrambledcodes) with no AI to see eachother
     *  and not-syndicate borgs with no AI to see no one. Syndicate borgs connected to an AI will no longer see other syndicate
     *  borgs except ones also slaved to the same AI.
     */
    // tg: code/modules/modular_computers/file_system/programs/robotact.dm:33
    public virtual object evaluate_borg(mob_living_silicon_robot this_borg = null, mob_living_silicon_robot other_borg = null)
    {
        if (this_borg.connected_ai != other_borg.connected_ai)
            return FALSE;
        if (Dm.truthy(this_borg.scrambledcodes) && Dm.truthy(other_borg.scrambledcodes))
            return TRUE;
        if (this_borg.connected_ai != null)
            return TRUE;
        return null;
    }

    // tg: code/modules/modular_computers/file_system/programs/robotact.dm:41
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (!iscyborg(user))
            return data;

        //Implied, since we can't run on non tablets
        obj_item_modular_computer_pda_silicon tablet = computer as obj_item_modular_computer_pda_silicon;

        mob_living_silicon_robot cyborg = tablet.silicon_owner as mob_living_silicon_robot;

        data["borgName"] = cyborg.name;
        data["designation"] = cyborg.model;
        data["masterAI"] = cyborg.connected_ai;
        data["MasterAI_connected"] = cyborg.connected_ai != null ? TRUE : FALSE; //Need a bool for this on the other side
        data["masterAI_online"] = cyborg.connected_ai?.stat == STABLE ? TRUE : FALSE;

        object charge = 0;
        object maxcharge = 1;
        if (cyborg.cell is obj_item_stock_parts_power_store cyborg_cell)
        {
            charge = cyborg_cell.charge;
            maxcharge = cyborg_cell.maxcharge;
        }
        data["charge"] = charge; //Current cell charge
        data["maxcharge"] = maxcharge; //Cell max charge
        data["integrity"] = (Dm.num(cyborg.health) + 100) / 2; //health, as percentage
        data["lampIntensity"] = cyborg.lamp_intensity; //lamp power setting
        data["lampConsumption"] = cyborg.lamp_power_consumption; //Power consumption of the lamp per lamp intensity.
        data["sensors"] = Dm.text("[]", Dm.truthy(cyborg.sensors_on) ? "ACTIVE" : "DISABLED");
        data["printerPictures"] = cyborg.connected_ai != null ? cyborg.connected_ai.aicamera.stored.len : cyborg.aicamera.stored.len; //Number of pictures taken, synced to AI if available
        data["printerToner"] = cyborg.toner; //amount of toner
        data["printerTonerMax"] = cyborg.tonermax; //It's a variable, might as well use it
        data["thrustersInstalled"] = cyborg.ionpulse; //If we have a thruster uprade
        data["thrustersStatus"] = Dm.text("[]", Dm.truthy(cyborg.ionpulse_on) ? "ACTIVE" : "DISABLED"); //Feedback for thruster status
        data["selfDestructAble"] = Dm.truthy(cyborg.emagged) || cyborg is mob_living_silicon_robot_model_syndicate ? TRUE : FALSE;

        data["cyborg_groups"] = Dm.list();
        if (Dm.truthy(data["masterAI_online"]) || (!Dm.truthy(data["MasterAI_connected"]) && Dm.truthy(cyborg.scrambledcodes))) //If a borg isn't connected, we can just skip this all
        {
            DmList borggroup = Dm.list(); //temporary list for holding groups of borgs
            foreach (mob_living_silicon_robot other_borg in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
            {
                if (!Dm.truthy(evaluate_borg(cyborg, other_borg)))
                    continue;

                bool shell = Dm.truthy(other_borg.shell) && !Dm.truthy(other_borg.ckey);

                DmList cyborg_data = Dm.list(
                    Dm.kv("otherBorgName", other_borg.name),
                    Dm.kv("integ", round((Dm.num(other_borg.health) + 100) / 2)), //mob heath is -100 to 100, we want to scale that to 0 - 100
                    Dm.kv("locked_down", other_borg.lockcharge),
                    Dm.kv("status", other_borg.stat),
                    Dm.kv("shell_discon", shell ? TRUE : FALSE),
                    Dm.kv("charge", other_borg.cell is obj_item_stock_parts_power_store other_cell ? round(Dm.num(other_cell.percent())) : null),
                    Dm.kv("module", other_borg.model != null ? Dm.text("[]", other_borg.model.name) : "None"),
                    Dm.kv("ref", REF(other_borg))
                );
                Dm.append(ref borggroup, Dm.list(cyborg_data));
                if (length(borggroup) == 4) //grouping borgs in packs of four, since I can't do it later in js
                {
                    Dm.append(data["cyborg_groups"] as DmList, Dm.list(borggroup));
                    borggroup = Dm.list();
                }
            }
            if (Dm.truthy(length(borggroup))) //and any remainders
                Dm.append(data["cyborg_groups"] as DmList, Dm.list(borggroup));
        }

        //Cover, TRUE for locked
        data["cover"] = Dm.text("[]", Dm.truthy(cyborg.locked) ? "LOCKED" : "UNLOCKED");
        //Ability to move. FAULT if lockdown wire is cut, DISABLED if borg locked, ENABLED otherwise
        data["locomotion"] = Dm.text("[]", Dm.truthy(cyborg.wires.is_cut(WIRE_LOCKDOWN)) ? "FAULT" : Dm.text("[]", Dm.truthy(cyborg.lockcharge) ? "DISABLED" : "ENABLED"));
        //Model wire. FAULT if cut, NOMINAL otherwise
        data["wireModule"] = Dm.text("[]", Dm.truthy(cyborg.wires.is_cut(WIRE_RESET_MODEL)) ? "FAULT" : "NOMINAL");
        //DEBUG -- Camera(net) wire. FAULT if cut (or no cameranet camera), DISABLED if pulse-disabled, NOMINAL otherwise
        data["wireCamera"] = Dm.text("[]", cyborg.builtInCamera == null || Dm.truthy(cyborg.wires.is_cut(WIRE_CAMERA)) ? "FAULT" : Dm.text("[]", Dm.truthy(cyborg.builtInCamera.can_use()) ? "NOMINAL" : "DISABLED"));
        //AI wire. FAULT if wire is cut, CONNECTED if connected to AI, READY otherwise
        data["wireAI"] = Dm.text("[]", Dm.truthy(cyborg.wires.is_cut(WIRE_AI)) ? "FAULT" : Dm.text("[]", cyborg.connected_ai != null ? "CONNECTED" : "READY"));
        //Law sync wire. FAULT if cut, NOMINAL otherwise
        data["wireLaw"] = Dm.text("[]", Dm.truthy(cyborg.wires.is_cut(WIRE_LAWSYNC)) ? "FAULT" : "NOMINAL");

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/robotact.dm:118
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        if (!iscyborg(user))
            return data;
        mob_living_silicon_robot cyborg = user as mob_living_silicon_robot;
        //Implied
        obj_item_modular_computer_pda_silicon tablet = computer as obj_item_modular_computer_pda_silicon;

        data["Laws"] = cyborg.laws.get_law_list(TRUE, TRUE, FALSE);
        data["borgLog"] = tablet.borglog;
        data["borgUpgrades"] = cyborg.upgrades;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/robotact.dm:131
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        //Implied type, memes
        obj_item_modular_computer_pda_silicon tablet = computer as obj_item_modular_computer_pda_silicon;
        mob_living_silicon_robot cyborg = tablet.silicon_owner as mob_living_silicon_robot;

        switch (action as string)
        {
            case "coverunlock":
                if (Dm.truthy(cyborg.locked))
                    await cyborg.toggle_cover();
                break;

            case "lawchannel":
                await cyborg.set_autosay();
                break;

            case "lawstate":
                await cyborg.checklaws();
                break;

            case "alertPower":
                if (!IS_UNCONSCIOUS_OR_CRIT(cyborg))
                {
                    if (cyborg.cell is not obj_item_stock_parts_power_store power_cell || !Dm.truthy(power_cell.charge))
                    {
                        cyborg.visible_message(span_notice(I18n.T("The power warning light on [] flashes urgently.", span_name(Dm.text("[]", cyborg)))),
                            I18n.T("You announce you are operating in low power mode."));
                        playsound(cyborg, Dm.resource("sound/machines/buzz/buzz-two.ogg"), 50, FALSE);
                    }
                }
                break;

            case "toggleSensors":
                cyborg.toggle_sensors();
                break;

            case "viewImage":
                if (cyborg.connected_ai != null)
                {
                    if (cyborg.connected_ai.aicamera != null)
                        await cyborg.connected_ai.aicamera.viewpictures(usr);
                }
                else if (cyborg.aicamera != null)
                    await cyborg.aicamera.viewpictures(usr);
                break;

            case "printImage":
                obj_item_camera_siliconcam_robot_camera borgcam = cyborg.aicamera as obj_item_camera_siliconcam_robot_camera;
                if (borgcam != null)
                    await borgcam.borgprint(usr as mob_living_silicon_robot);
                break;

            case "toggleThrusters":
                cyborg.toggle_ionpulse();
                break;

            case "lampIntensity":
                cyborg.lamp_intensity = Dm.num(@params["ref"]);
                await cyborg.toggle_headlamp(FALSE, TRUE);
                break;

            case "selfDestruct":
                if (IS_UNCONSCIOUS_OR_CRIT(cyborg) || Dm.truthy(cyborg.lockcharge)) //No detonation while stunned or locked down
                    return dot;
                if (Dm.truthy(cyborg.emagged) || cyborg is mob_living_silicon_robot_model_syndicate) //This option shouldn't even be showing otherwise
                    await cyborg.self_destruct(cyborg);
                break;
        }
        return dot;
    }
}
