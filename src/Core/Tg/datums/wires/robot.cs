// Translated from tgstation (snapshot 1d99bd7f): code/datums/wires/robot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the cyborg's wires, whole. The var block is generated (D111).

// tg: code/datums/wires/robot.dm:1
public partial class datum_wires_robot : datum_wires
{
    // tg: code/datums/wires/robot.dm:6
    [DmParams("holder")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        wires = Dm.list(
            WIRE_AI, WIRE_CAMERA,
            WIRE_LAWSYNC, WIRE_LOCKDOWN,
            WIRE_RESET_MODEL
        );
        add_duds(2);
        await base.New(Dm.forward(__extra, holder));
    }

    // tg: code/datums/wires/robot.dm:15
    public override bool interactable(mob user)
    {
        if (!base.interactable(user))
            return false;
        mob_living_silicon_robot R = (mob_living_silicon_robot)holder;
        if (Dm.truthy(R.wiresexposed))
            return true;
        return false;
    }

    // tg: code/datums/wires/robot.dm:22
    public override DmList get_status()
    {
        mob_living_silicon_robot R = (mob_living_silicon_robot)holder;
        DmList status = Dm.list();
        Dm.append(ref status, I18n.T("The law sync module is [].", Dm.truthy(R.lawupdate) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The intelligence link display shows [].", R.connected_ai != null ? R.connected_ai.name : "NULL"));
        Dm.append(ref status, I18n.T("The camera light is [].", R.builtInCamera != null && Dm.truthy(R.builtInCamera.camera_enabled) ? "on" : "off"));
        Dm.append(ref status, I18n.T("The lockdown indicator is [].", Dm.truthy(R.lockcharge) ? "on" : "off"));
        Dm.append(ref status, I18n.T("There is a star symbol above the [] wire.", get_color_of_wire(WIRE_RESET_MODEL)));
        return status;
    }

    // tg: code/datums/wires/robot.dm:32
    public override async DmTask on_pulse(object wire = null, mob_living user = null)
    {
        mob_living_silicon_robot R = (mob_living_silicon_robot)holder;
        if (Dm.eq(wire, WIRE_AI)) // Pulse to pick a new AI.
        {
            if (!Dm.truthy(R.emagged))
            {
                object new_ai;
                object is_a_syndi_borg = R.has_faction(ROLE_SYNDICATE);
                if (user != null)
                    new_ai = await select_active_ai(user, R.z, !Dm.truthy(is_a_syndi_borg) ? TRUE : FALSE, is_a_syndi_borg);
                else
                    new_ai = await select_active_ai(R, R.z, !Dm.truthy(is_a_syndi_borg) ? TRUE : FALSE, is_a_syndi_borg);
                R.notify_ai(AI_NOTIFICATION_CYBORG_DISCONNECTED);
                if (Dm.truthy(new_ai) && !Dm.eq(new_ai, R.connected_ai))
                {
                    await R.set_connected_ai(new_ai);
                    log_silicon(Dm.text("[] synced [] [] to []", key_name(usr), key_name(R), R.connected_ai != null ? Dm.text("from []", key_name(R.connected_ai)) : "", key_name(new_ai)));
                    if (Dm.truthy(R.shell))
                    {
                        await R.undeploy(); //If this borg is an AI shell, disconnect the controlling AI and assign ti to a new AI
                        R.notify_ai(AI_NOTIFICATION_AI_SHELL);
                    }
                    else
                        R.notify_ai(TRUE);
                }
            }
        }
        else if (Dm.eq(wire, WIRE_CAMERA)) // Pulse to disable the camera.
        {
            if (!QDELETED(R.builtInCamera) && !Dm.truthy(R.scrambledcodes))
            {
                R.builtInCamera.toggle_cam(usr, FALSE);
                R.visible_message(span_notice(I18n.T("[]'s camera lens focuses loudly.", R)), span_notice(I18n.T("Your camera lens focuses loudly.")));
                log_silicon(Dm.text("[] toggled []'s camera to [] via pulse", key_name(usr), key_name(R), Dm.truthy(R.builtInCamera.camera_enabled) ? "on" : "off"));
            }
        }
        else if (Dm.eq(wire, WIRE_LAWSYNC)) // Forces a law update if possible.
        {
            if (Dm.truthy(R.lawupdate))
            {
                R.visible_message(span_notice(I18n.T("[] gently chimes.", R)), span_notice(I18n.T("LawSync protocol engaged.")));
                log_silicon(Dm.text("[] forcibly synced []'s laws via pulse", key_name(usr), key_name(R)));
                // TODO, log the laws they gained here
                await R.lawsync();
                await R.show_laws();
            }
        }
        else if (Dm.eq(wire, WIRE_LOCKDOWN))
        {
            await R.SetLockdown(!Dm.truthy(R.lockcharge) ? TRUE : FALSE); // Toggle
            log_silicon(Dm.text("[] [] [] via pulse", key_name(usr), !Dm.truthy(R.lockcharge) ? "locked down" : "released", key_name(R)));
        }
        else if (Dm.eq(wire, WIRE_RESET_MODEL))
        {
            if (Dm.truthy(R.has_model()))
                R.visible_message(span_notice(I18n.T("[]'s model servos twitch.", R)), span_notice(I18n.T("Your model display flickers.")));
        }
    }

    // tg: code/datums/wires/robot.dm:75
    public override async DmTask on_cut(object wire = null, float? mend = null, mob_living source = null)
    {
        mob_living_silicon_robot R = (mob_living_silicon_robot)holder;
        if (Dm.eq(wire, WIRE_AI)) // Cut the AI wire to reset AI control.
        {
            if (!Dm.truthy(mend))
            {
                R.notify_ai(AI_NOTIFICATION_CYBORG_DISCONNECTED);
                log_silicon(Dm.text("[] cut AI wire on [][]", key_name(usr), key_name(R), R.connected_ai != null ? Dm.text(" and disconnected from []", key_name(R.connected_ai)) : ""));
                if (Dm.truthy(R.shell))
                    await R.undeploy();
                await R.set_connected_ai(null);
            }
            await R.logevent(Dm.text("AI connection fault []", Dm.truthy(mend) ? "cleared" : "detected"));
        }
        else if (Dm.eq(wire, WIRE_LAWSYNC)) // Cut the law wire, and the borg will no longer receive law updates from its AI. Repair and it will re-sync.
        {
            if (Dm.truthy(mend))
            {
                if (!Dm.truthy(R.emagged))
                {
                    R.lawupdate = TRUE;
                    log_silicon(Dm.text("[] enabled []'s lawsync via wire", key_name(usr), key_name(R)));
                }
            }
            else if (!Dm.truthy(R.deployed)) //AI shells must always have the same laws as the AI
            {
                R.lawupdate = FALSE;
                log_silicon(Dm.text("[] disabled []'s lawsync via wire", key_name(usr), key_name(R)));
            }
            await R.logevent(Dm.text("Lawsync Module fault []", Dm.truthy(mend) ? "cleared" : "detected"));
        }
        else if (Dm.eq(wire, WIRE_CAMERA)) // Disable the camera.
        {
            if (!QDELETED(R.builtInCamera) && !Dm.truthy(R.scrambledcodes))
            {
                float fixing_camera = !Dm.truthy(mend) ? TRUE : FALSE;
                R.builtInCamera.camera_enabled = fixing_camera;
                R.builtInCamera.toggle_cam(usr, 0);
                R.visible_message(span_notice(I18n.T("[]'s camera lens focuses loudly.", R)), span_notice(I18n.T("Your camera lens focuses loudly.")));
                await R.logevent(Dm.text("Camera Module fault []", Dm.truthy(fixing_camera) ? "cleared" : "detected"));
                log_silicon(Dm.text("[] [] []'s camera via wire", key_name(usr), Dm.truthy(fixing_camera) ? "enabled" : "disabled", key_name(R)));
            }
        }
        else if (Dm.eq(wire, WIRE_LOCKDOWN)) // Simple lockdown.
        {
            await R.SetLockdown(!Dm.truthy(mend) ? TRUE : FALSE);
            await R.logevent(Dm.text("Motor Controller fault []", Dm.truthy(mend) ? "cleared" : "detected"));
            log_silicon(Dm.text("[] [] [] via wire", key_name(usr), !Dm.truthy(R.lockcharge) ? "locked down" : "released", key_name(R)));
        }
        else if (Dm.eq(wire, WIRE_RESET_MODEL))
        {
            if (Dm.truthy(R.has_model()) && !Dm.truthy(mend))
            {
                await R.ResetModel();
                log_silicon(Dm.text("[] reset []'s module via wire", key_name(usr), key_name(R)));
            }
        }
    }

    // tg: code/datums/wires/robot.dm:115
    public override bool can_reveal_wires(mob user)
    {
        if (HAS_TRAIT(user, TRAIT_KNOW_ROBO_WIRES))
            return true;

        return base.can_reveal_wires(user);
    }

    // tg: code/datums/wires/robot.dm:121
    public override bool always_reveal_wire(object color)
    {
        // Always reveal the reset model wire.
        if (Dm.eq(color, get_color_of_wire(WIRE_RESET_MODEL)))
            return true;

        return base.always_reveal_wire(color);
    }
}
