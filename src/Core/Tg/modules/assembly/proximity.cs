// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/proximity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3: the bots' assemblies are built from it). The icon link in the beep's line is the chat icons gap (section 76).

// tg: code/modules/assembly/proximity.dm:1
public partial class obj_item_assembly_prox_sensor : obj_item_assembly
{
    // tg: code/modules/assembly/proximity.dm:17
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 0);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:22
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        qdel(proximity_monitor);
        proximity_monitor = null;
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/proximity.dm:27
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(Dm.text("The proximity sensor is [].", Dm.truthy(timing) ? "arming" : (Dm.truthy(scanning) ? "armed" : "disarmed"))));
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:31
    public override async DmTask<object> activate(mob activator = null)
    {
        if (!Dm.truthy(await base.activate(activator)))
            return FALSE; //Cooldown check
        if (!Dm.truthy(scanning))
            timing = Dm.num(!Dm.truthy(timing));
        else
            scanning = FALSE;
        update_appearance();
        return TRUE;
    }

    // Pick the first valid object in this list: Wiring datum's owner, assembly holder's attached object, assembly holder itself, us
    private atom SensorHost() => (connected as object as datum_wires)?.holder ?? ((holder as obj_item_assembly_holder)?.master as atom) ?? holder ?? (atom)this;

    // tg: code/modules/assembly/proximity.dm:40
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (proximity_monitor != null)
            await proximity_monitor.set_host(SensorHost(), this);
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:49
    public override async DmTask on_attach()
    {
        await base.on_attach();
        await proximity_monitor.set_host(SensorHost(), this);
    }

    // tg: code/modules/assembly/proximity.dm:58
    public override async DmTask<object> on_detach()
    {
        object dot = await base.on_detach();
        if (!Dm.truthy(dot))
            return dot;
        await proximity_monitor.set_host(SensorHost(), this);
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:70
    public override async DmTask<object> toggle_secure()
    {
        secured = Dm.num(!Dm.truthy(secured));
        if (!Dm.truthy(secured))
        {
            if (Dm.truthy(scanning))
            {
                await toggle_scan();
                await proximity_monitor.set_host(this, this);
            }
            timing = FALSE;
            STOP_PROCESSING(SSobj, this);
        }
        else
        {
            START_PROCESSING(SSobj, this);
            await proximity_monitor.set_host(loc, this);
        }
        update_appearance();
        return secured;
    }

    // tg: code/modules/assembly/proximity.dm:84
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        if (proximity_check_mob is obj_effect_beam)
            return null;
        await sense();
        return null;
    }

    // tg: code/modules/assembly/proximity.dm:89
    public virtual async DmTask<object> sense()
    {
        if (!Dm.truthy(scanning) || !Dm.truthy(secured) || next_activate > world.time)
            return FALSE;
        next_activate = world.time + (3 * SECONDS); // this must happen before anything else
        await pulse();
        audible_message(span_infoplain("*beep* *beep* *beep*"), null, hearing_range);
        foreach (mob hearing_mob in Dm.each<mob>(get_hearers_in_view(hearing_range, this)))
            hearing_mob.playsound_local(get_turf(this), Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE);

        return TRUE;
    }

    // tg: code/modules/assembly/proximity.dm:100
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(timing))
            return null;
        time -= Dm.num(seconds_per_tick);
        if (time <= 0)
        {
            timing = FALSE;
            Waiting.NoSleep(toggle_scan(TRUE));
            time = Dm.initial(this, (obj_item_assembly_prox_sensor p) => p.time);
        }
        return null;
    }

    // tg: code/modules/assembly/proximity.dm:109
    public virtual async DmTask<object> toggle_scan(object scan = null)
    {
        if (!Dm.truthy(secured))
            return FALSE;
        scanning = Dm.num(scan);
        await proximity_monitor.set_range(Dm.truthy(scanning) ? sensitivity : 0);
        update_appearance();
        return null;
    }

    // tg: code/modules/assembly/proximity.dm:115
    public virtual async DmTask sensitivity_change(object value = null)
    {
        float sense_range = Math.Min(Math.Max(Dm.num(sensitivity) + Dm.num(value), 0), 5);
        sensitivity = sense_range;
        if (Dm.truthy(scanning) && Dm.truthy(await proximity_monitor.set_range(sense_range)))
            await sense();
    }

    // tg: code/modules/assembly/proximity.dm:120
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        holder?.update_appearance();
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:124
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        attached_overlays = Dm.list();
        if (Dm.truthy(timing))
        {
            Dm.append(ref dot, "prox_timing");
            Dm.append(ref attached_overlays, "prox_timing");
        }
        if (Dm.truthy(scanning))
        {
            Dm.append(ref dot, "prox_scanning");
            Dm.append(ref attached_overlays, "prox_scanning");
        }
        return dot;
    }

    // tg: code/modules/assembly/proximity.dm:134
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (Dm.truthy(is_secured(user)))
            return await base.ui_status(user, state);
        return UI_CLOSE;
    }

    // tg: code/modules/assembly/proximity.dm:139
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ProximitySensor", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/assembly/proximity.dm:145
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        object seconds = round(Dm.num(time) % 60);
        data["seconds"] = seconds;
        data["minutes"] = round((Dm.num(time) - Dm.num(seconds)) / 60);
        data["timing"] = timing;
        data["scanning"] = scanning;
        data["sensitivity"] = sensitivity;
        return data;
    }

    // tg: code/modules/assembly/proximity.dm:154
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "scanning":
                await toggle_scan(Dm.num(!Dm.truthy(scanning)));
                dot = TRUE;
                break;
            case "sense":
            {
                object value = text2num(@params["range"]);
                if (Dm.truthy(value))
                {
                    await sensitivity_change(value);
                    dot = TRUE;
                }
                break;
            }
            case "time":
                timing = Dm.num(!Dm.truthy(timing));
                update_appearance();
                dot = TRUE;
                break;
            case "input":
            {
                object value = text2num(@params["adjust"]);
                if (Dm.truthy(value))
                {
                    float adjusted = Dm.num(round(Dm.num(time) + Dm.num(value)));
                    time = Math.Clamp(adjusted, 0, 600);
                    dot = TRUE;
                }
                break;
            }
        }
        return dot;
    }
}
