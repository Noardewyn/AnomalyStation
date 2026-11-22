// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/life.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the AI's life tick, its health and stat, and the power-loss routine.

// tg: code/modules/mob/living/silicon/ai/life.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/life.dm:1
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        if (stat == DEAD)
            return null;
        //Being dead doesn't mean your temperature never changes

        if (Dm.truthy(malfhack?.aidisabled))
        {
            deltimer(malfhacking);
            // This proc handles cleanup of screen notifications and
            // messenging the client
            Waiting.NoSleep(malfhacked(malfhack));
        }

        if (isturf(loc) && (QDELETED(eyeobj) || eyeobj.loc == null))
            Waiting.NoSleep(view_core());

        // Handle power damage (oxy)
        if (battery <= 0 && lacks_power())
        {
            to_chat(this, span_warning(I18n.T("Your backup battery's output drops below usable levels. It takes only a moment longer for your systems to fail, corrupted and unusable.")));
            Waiting.NoSleep(adjust_oxy_loss(200));
        }

        if (Dm.truthy(aiRestorePowerRoutine))
        {
            // Lost power
            battery--;
        }
        else
        {
            // Gain Power
            if (battery < 200)
                battery++;
        }

        if (!lacks_power())
        {
            area home = get_area(this) as area;
            if (Dm.truthy(home.powered(AREA_USAGE_EQUIP)))
            {
                obj_machinery_power_terminal terminal = home.apc?.terminal;
                if (terminal != null)
                    Waiting.NoSleep(terminal.use_energy(500 * WATTS * Dm.num(seconds_per_tick), channel: AREA_USAGE_EQUIP));
            }

            if (aiRestorePowerRoutine >= POWER_RESTORATION_SEARCH_APC)
            {
                ai_restore_power();
                return null;
            }
        }
        else if (!Dm.truthy(aiRestorePowerRoutine))
            ai_lose_power();
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:41
    public virtual bool lacks_power()
    {
        turf T = get_turf(this);
        area A = get_area(this) as area;
        switch (power_requirement)
        {
            case NONE:
                return false;
            case POWER_REQ_ALL:
                return T == null || A == null || ((!Dm.truthy(A.power_equip) || isspaceturf(T)) && !Dm.truthy(is_type_in_list(loc, Dm.list(typeof(obj_item), typeof(obj_vehicle_sealed_mecha)))));
        }
        return false;
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:50
    public override async DmTask updatehealth()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;

        float old_health = Dm.num(health);
        set_health(Dm.num(maxHealth) - Dm.num(get_oxy_loss()) - Dm.num(get_tox_loss()) - Dm.num(get_brute_loss()) - Dm.num(get_fire_loss()));

        object old_stat = stat;
        update_stat();

        diag_hud_set_health();

        if (old_health > Dm.num(health) || !Dm.eq(old_stat, stat)) // only disconnect if we lose health or change stat
            disconnect_shell();
        if (Dm.truthy(_listen_lookup?[COMSIG_LIVING_HEALTH_UPDATE]))
            _SendSignal(COMSIG_LIVING_HEALTH_UPDATE, Dm.list(this));
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:66
    public override void update_stat()
    {
        if (HAS_TRAIT(this, TRAIT_GODMODE))
            return;
        if (stat != DEAD && Dm.num(health) <= Dm.num(dead_threshold))
            Waiting.NoSleep(death());
        else if (stat < DEAD)
            Waiting.NoSleep(set_stat(STABLE));
        diag_hud_set_status();
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:75
    public override void update_sight()
    {
        base.update_sight();
        if (Dm.truthy(aiRestorePowerRoutine))
            clear_sight(Dm.bitor(Dm.bitor(SEE_TURFS, SEE_MOBS), SEE_OBJS));
        if (dm_meson_action != null && !Dm.truthy(dm_meson_action.active))
            clear_sight(SEE_TURFS); // DEVIATION(DV424): the meson vision switched off
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:81
    public virtual async DmTask start_RestorePowerRoutine()
    {
        to_chat(this, span_notice(I18n.T("Backup battery online. Scanners, camera, and radio interface offline. Beginning fault-detection.")));
        end_multicam();
        await Dm.sleep(5 * SECONDS);
        turf T = get_turf(this);
        area AIarea = get_area(this) as area;
        if (Dm.truthy(AIarea?.power_equip))
        {
            if (!isspaceturf(T))
            {
                ai_restore_power();
                return;
            }
        }
        to_chat(this, span_notice(I18n.T("Fault confirmed: missing external power. Shutting down main control system to save power.")));
        await Dm.sleep(2 * SECONDS);
        to_chat(this, span_notice(I18n.T("Emergency control system online. Verifying connection to power network.")));
        await Dm.sleep(5 * SECONDS);
        T = get_turf(this);
        if (isspaceturf(T))
        {
            to_chat(this, span_alert(I18n.T("Unable to verify! No power connection detected!")));
            setAiRestorePowerRoutine(POWER_RESTORATION_SEARCH_APC);
            return;
        }
        to_chat(this, span_notice(I18n.T("Connection verified. Searching for APC in power network.")));
        await Dm.sleep(5 * SECONDS);
        obj_machinery_power_apc theAPC = null;

        //like ERP with the code, at least this stuff is no more 4x sametext
        for (int PRP = 1; PRP <= 4; PRP++)
        {
            T = get_turf(this);
            AIarea = get_area(this) as area;
            if (AIarea != null)
                theAPC = AIarea.apc;
            if (theAPC == null)
            {
                if (PRP == 1)
                    to_chat(this, span_alert(I18n.T("Unable to locate APC!")));
                else
                    to_chat(this, span_alert(I18n.T("Lost connection with the APC!")));
                setAiRestorePowerRoutine(POWER_RESTORATION_SEARCH_APC);
                return;
            }
            if (Dm.truthy(AIarea.power_equip))
            {
                if (!isspaceturf(T))
                {
                    ai_restore_power();
                    return;
                }
            }
            switch (PRP)
            {
                case 1:
                    to_chat(this, span_notice(I18n.T("APC located. Optimizing route to APC to avoid needless power waste.")));
                    break;
                case 2:
                    to_chat(this, span_notice(I18n.T("Best route identified. Hacking offline APC power port.")));
                    break;
                case 3:
                    to_chat(this, span_notice(I18n.T("Power port upload access confirmed. Loading control program into APC power port software.")));
                    break;
                case 4:
                    to_chat(this, span_notice(I18n.T("Transfer complete. Forcing APC to execute program.")));
                    await Dm.sleep(5 * SECONDS);
                    to_chat(this, span_notice(I18n.T("Receiving control information from APC.")));
                    await Dm.sleep(0.2f * SECONDS);
                    to_chat(this, Dm.text("<A href=byond://?src=[];emergencyAPC=[]>", REF(this), TRUE) + I18n.T("APC ready for connection.") + "</A>");
                    apc_override = theAPC;
                    await apc_override.ui_interact(this);
                    setAiRestorePowerRoutine(POWER_RESTORATION_APC_FOUND);
                    break;
            }
            await Dm.sleep(5 * SECONDS);
            theAPC = null;
        }
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:143
    public virtual void ai_restore_power()
    {
        if (Dm.truthy(aiRestorePowerRoutine))
        {
            if (aiRestorePowerRoutine == POWER_RESTORATION_APC_FOUND)
            {
                to_chat(this, span_notice(I18n.T("Alert cancelled. Power has been restored.")));
                if (apc_override != null)
                    to_chat(this, span_notice(I18n.T("APC backdoor has been closed."))); //Fluff for why we have to hack every time.
            }
            else
                to_chat(this, span_notice(I18n.T("Alert cancelled. Power has been restored without our assistance.")));
            setAiRestorePowerRoutine(POWER_RESTORATION_OFF);
            remove_status_effect(typeof(datum_status_effect_temporary_blindness));
            apc_override = null;
            update_sight();
        }
    }

    // tg: code/modules/mob/living/silicon/ai/life.dm:156
    public virtual void ai_lose_power()
    {
        disconnect_shell();
        setAiRestorePowerRoutine(POWER_RESTORATION_START);
        Waiting.NoSleep(adjust_temp_blindness(2 * SECONDS));
        update_sight();
        to_chat(this, span_alert(I18n.T("You've lost power!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(start_RestorePowerRoutine)), 2 * SECONDS, file: "code/modules/mob/living/silicon/ai/life.dm", line: 162);
    }
}
