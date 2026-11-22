// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/health.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the health sensor, scanning the mob it is carried by (or the holder of its wires) on SSobj
// and pulsing with the triple beep once that mob's health falls to crit or death (its target), the "HP: x%" maptext it shows
// while a living mob holds it, its window ("HealthSensor"). The beep line's chat icon (icon2html) is left out, as at the other
// icon2html sites (the chat icons, not-translated.md section 76). The var block is generated (D111).

// tg: code/modules/assembly/health.dm:1
public partial class obj_item_assembly_health : obj_item_assembly
{
    // tg: code/modules/assembly/health.dm:14
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, I18n.T("[].", Dm.truthy(scanning) ? I18n.T("The sensor is on and you can see [] displayed on the screen", health_scan) : I18n.T("The sensor is off")));
        return dot;
    }

    // tg: code/modules/assembly/health.dm:18
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (isliving(old_loc))
        {
            UnregisterSignal(old_loc, COMSIG_LIVING_HEALTH_UPDATE);
            maptext = null;
        }
        if (isliving(loc))
        {
            RegisterSignal(loc, COMSIG_LIVING_HEALTH_UPDATE, nameof(on_health_changed));
            on_health_changed((mob_living)loc);
        }
        return dot;
    }

    // tg: code/modules/assembly/health.dm:27
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(await base.activate(activator)))
            return FALSE; //Cooldown check
        toggle_scan();
        return TRUE;
    }

    // tg: code/modules/assembly/health.dm:33
    public override async DmTask<object> toggle_secure()
    {
        secured = Dm.num(!Dm.truthy(secured));
        if (Dm.truthy(secured) && Dm.truthy(scanning))
            START_PROCESSING(SSobj, this);
        else
        {
            scanning = FALSE;
            STOP_PROCESSING(SSobj, this);
        }
        update_appearance();
        return secured;
    }

    // tg: code/modules/assembly/health.dm:43
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body()
    {
        //not ready yet
        if (!Dm.truthy(scanning) || !Dm.truthy(secured))
            return null;

        //look for a mob in either our location or in the connected holder
        atom @object = this;
        if (connected?.holder != null)
            @object = connected.holder;
        while (!ismob(@object))
        {
            @object = @object.loc;
            if (@object == null) //we went too far
                return null;
        }

        //only do the pulse if we are within alarm thresholds
        mob_living target_mob = (mob_living)@object;
        health_scan = target_mob.health;
        if (Dm.num(health_scan) > health_target)
            return null;

        //do the pulse & the scan
        await pulse();
        // [icon2html(src, hearers(src))]: the chat icons (section 76)
        audible_message(span_infoplain(I18n.T("*beep* *beep* *beep*")));
        playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE);
        toggle_scan();
        return null;
    }

    // tg: code/modules/assembly/health.dm:69
    public virtual object toggle_scan()
    {
        if (!Dm.truthy(secured))
            return 0;
        scanning = Dm.num(!Dm.truthy(scanning));
        if (Dm.truthy(scanning))
            START_PROCESSING(SSobj, this);
        else
            STOP_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/modules/assembly/health.dm:79
    public virtual object toggle_target()
    {
        if (health_target == HEALTH_THRESHOLD_CRIT)
            health_target = HEALTH_THRESHOLD_DEAD;
        else
            health_target = HEALTH_THRESHOLD_CRIT;
        return null;
    }

    // tg: code/modules/assembly/health.dm:86
    [SignalHandler]
    public virtual void on_health_changed(mob_living source = null)
    {
        maptext = MAPTEXT(I18n.T("HP: []%", round((source.health / source.maxHealth) * 100)));
    }

    // tg: code/modules/assembly/health.dm:90
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return Dm.truthy(is_secured(user)) ? await base.ui_status(user, state) : UI_CLOSE;
    }

    // tg: code/modules/assembly/health.dm:93
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "HealthSensor", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/assembly/health.dm:99
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["health"] = health_scan;
        data["scanning"] = scanning;
        data["target"] = health_target;
        return data;
    }

    // tg: code/modules/assembly/health.dm:106
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "scanning"))
        {
            toggle_scan();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "target"))
        {
            toggle_target();
            return TRUE;
        }
        return dot;
    }
}
