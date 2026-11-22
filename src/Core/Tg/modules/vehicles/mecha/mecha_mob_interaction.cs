// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_mob_interaction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:1
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:1
    public override async DmTask<object> mob_try_enter(mob M = null)
    {
        if (!ishuman(M)) // no silicons or drones in mechas.
            return null;
        if (HAS_TRAIT(M, TRAIT_PRIMITIVE)) //no lavalizards either.
        {
            to_chat(M, span_warning(I18n.T("The knowledge to use this device eludes you!")));
            return null;
        }
        log_message(Dm.text("[] tried to move into [].", M, this), LOG_MECHA);
        if (Dm.truthy(dna_lock) && Dm.truthy(M.has_dna()))
        {
            mob_living_carbon entering_carbon = M as mob_living_carbon;
            if (!Dm.eq(entering_carbon.dna.unique_enzymes, dna_lock))
            {
                to_chat(M, span_warning(I18n.T("Access denied. [] is secured with a DNA lock.", name)));
                log_message("Permission denied (DNA LOCK).", LOG_MECHA);
                return null;
            }
        }
        if (Dm.truthy(Dm.bitand(mecha_flags, ID_LOCK_ON)) && !Dm.truthy(await allowed__proc(M)))
        {
            to_chat(M, span_warning(I18n.T("Access denied. Insufficient operation keycodes.")));
            log_message("Permission denied (No keycode).", LOG_MECHA);
            return null;
        }
        object dot = await base.mob_try_enter(M);
        if (Dm.truthy(dot))
            await moved_inside(M as mob_living);
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:22
    public override object enter_checks(mob M = null)
    {
        if (Dm.truthy(M.incapacitated))
            return FALSE;
        if (Dm.num(atom_integrity) <= 0)
        {
            to_chat(M, span_warning(I18n.T("You cannot get in the [], it has been destroyed!", this)));
            return FALSE;
        }
        if (Dm.truthy(M.buckled))
        {
            to_chat(M, span_warning(I18n.T("You can't enter the exosuit while buckled.")));
            log_message("Permission denied (Buckled).", LOG_MECHA);
            return FALSE;
        }
        if (Dm.truthy(M.has_buckled_mobs()))
        {
            to_chat(M, span_warning(I18n.T("You can't enter the exosuit with other creatures attached to you!")));
            log_message("Permission denied (Attached mobs).", LOG_MECHA);
            return FALSE;
        }

        foreach (obj_item thing in Dm.each<obj_item>(M.held_items))
        {
            if (!Dm.truthy(Dm.bitand(thing.item_flags, Dm.bitor(ABSTRACT, HAND_ITEM))))
            {
                to_chat(M, span_warning(I18n.T("You can't enter the exosuit while your hands are occupied!")));
                return FALSE;
            }
        }

        return base.enter_checks(M);
    }

    ///proc called when a new non-mmi mob enters this mech
    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:45
    public virtual async DmTask<object> moved_inside(mob_living newoccupant = null)
    {
        if (newoccupant?.client == null)
            return FALSE;
        if (ishuman(newoccupant) && !Dm.truthy(await Adjacent(newoccupant)))
            return FALSE;
        Dm.mask(ref mecha_flags, Dm.bitnot(PANEL_OPEN)); //Close panel if open
        await newoccupant.forceMove(this);
        newoccupant.update_mouse_pointer();
        add_fingerprint(newoccupant);
        log_message(Dm.text("[] moved in as pilot.", newoccupant), LOG_MECHA);
        await setDir(SOUTH);
        playsound(this, Dm.resource("sound/machines/windowdoor.ogg"), 50, TRUE);
        set_mouse_pointer();
        if (!Dm.truthy(internal_damage))
            Dm.output_to(newoccupant, sound(Dm.resource("sound/vehicles/mecha/nominal.ogg"), volume: 50)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        return TRUE;
    }

    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:114
    public override async DmTask<object> mob_exit(mob M = null, object silent = null, object randomstep = null, object forced = null)
    {
        silent ??= FALSE;
        randomstep ??= FALSE;
        forced ??= FALSE;
        // FIXME: this code is really bad (shocker). Needs a refactor
        atom_movable mob_container;
        turf newloc = get_turf(this) as turf;
        if (ishuman(M))
            mob_container = M;
        else if (isbrain(M))
        {
            mob_living_brain brain = M as mob_living_brain;
            UnregisterSignal(brain, COMSIG_MOB_RETRIEVE_ACCESS);
            mob_container = brain.container;
        }
        else if (isAI(M))
        {
            Dm.NotTranslated("code/modules/vehicles/mecha/mecha_mob_interaction.dm:124", "mob_exit: an AI pilot (mecha_ai_interaction.dm; the AI's mech control comes with the AI's interactions)");
            return await base.mob_exit(M, silent, randomstep, forced);
        }
        else if (isliving(M))
            mob_container = M;
        else
            return await base.mob_exit(M, silent, randomstep, forced);
        mob_living ejector = M as mob_living;
        await mob_container.forceMove(newloc);//ejecting mob container
        log_message(Dm.text("[] moved out.", mob_container), LOG_MECHA);
        SStgui.close_user_uis(M, this);
        if (istype(mob_container, typeof(obj_item_brain_processor)))
        {
            obj_item_brain_processor mmi = mob_container as obj_item_brain_processor;
            if (mmi.brainmob != null)
            {
                await ejector.forceMove(mmi);
                await ejector.reset_perspective();
                await remove_occupant(ejector);
            }
            mmi.update_appearance();
        }
        await setDir(SOUTH);
        SEND_SIGNAL(COMSIG_MECHA_MOB_EXIT, Dm.list(this));
        return await base.mob_exit(M, silent, randomstep, forced);
    }

    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:178
    public override async DmTask<object> add_occupant(mob driver = null, object control_flags = null, object forced = null)
    {
        RegisterSignal(driver, COMSIG_MOB_CLICKON, nameof(on_mouseclick), TRUE);
        RegisterSignal(driver, COMSIG_MOB_SAY, nameof(display_speech_bubble), TRUE);
        RegisterSignal(driver, COMSIG_MOVABLE_KEYBIND_FACE_DIR, nameof(on_turn), TRUE);
        RegisterSignal(driver, COMSIG_MOB_ALTCLICKON, nameof(on_click_alt));
        object dot = await base.add_occupant(driver, control_flags, forced);
        update_appearance();
        // tg: code/modules/vehicles/mecha/_mecha.dm:364
        if (Dm.truthy(dot))
            await generate_equipment_actions(driver);
        refresh_arm_views();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:186
    public override async DmTask<object> remove_occupant(mob driver = null)
    {
        remove_all_equipment_actions(driver); // tg: code/modules/vehicles/mecha/_mecha.dm:369
        UnregisterSignal(driver, Dm.list(
            COMSIG_MOB_CLICKON,
            COMSIG_MOB_SAY,
            COMSIG_MOVABLE_KEYBIND_FACE_DIR,
            COMSIG_MOB_ALTCLICKON));
        driver.clear_alert(ALERT_CHARGE);
        driver.clear_alert(ALERT_MECH_DAMAGE);
        if (driver.client != null)
        {
            driver.update_mouse_pointer();
            driver.client.view_size?.resetToDefault();
        }
        object dot = await base.remove_occupant(driver);
        refresh_arm_views();
        update_appearance();
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_mob_interaction.dm:201
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (isAI(user))
        {
            Dm.NotTranslated("code/modules/vehicles/mecha/mecha_mob_interaction.dm:202", "container_resist_act: an AI pilot's emergency shunt (mecha_ai_interaction.dm)");
            return null;
        }
        to_chat(user, span_notice(I18n.T("You begin the ejection procedure. Equipment is disabled during this process. Hold still to finish ejecting.")));
        is_currently_ejecting = TRUE;
        if (Dm.truthy(await do_after(user, Dm.truthy(has_gravity()) ? exit_delay : 0, this)))
        {
            to_chat(user, span_notice(I18n.T("You exit the mech.")));
            if (Dm.truthy(cabin_sealed))
                await set_cabin_seal(user, FALSE);
            await mob_exit(user, silent: TRUE);
        }
        else
            to_chat(user, span_notice(I18n.T("You stop exiting the mech. Weapons are enabled again.")));
        is_currently_ejecting = FALSE;
        return null;
    }
}
