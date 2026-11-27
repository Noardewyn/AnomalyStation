// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mass driver launch control (the testing range's, the chapel's, the disposals' and the shack's wall
// controllers): it finds its mass driver in range at init, opens the pod doors of its id, fires the drivers and closes the doors,
// now or when its countdown ends, with its MassDriverControl window (power, timer, doors, test fire). The var block is generated
// (D111).

// tg: code/game/machinery/computer/pod.dm:1
public partial class obj_machinery_computer_pod : obj_machinery_computer
{
    // tg: code/game/machinery/computer/pod.dm:18
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        foreach (obj_machinery_mass_driver M in Dm.each<obj_machinery_mass_driver>(range(range, this)))
        {
            if (Dm.eq(M.id, id))
            {
                connected = M;
                break;
            }
        }
        return dot;
    }

    // tg: code/game/machinery/computer/pod.dm:25
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // set waitfor = FALSE, inherited from /datum/process (waiting-model.md 5.4)
    }

    private async DmTask<object> process__body()
    {
        if (massdriver_countdown <= Dm.num(world.time)) // COOLDOWN_FINISHED(src, massdriver_countdown), inline (rules 11.1 (a))
        {
            timing = FALSE;
            // alarm() sleeps, so we want to end processing first and can't rely on return PROCESS_KILL
            end_processing();
            await alarm();
        }
        return null;
    }

    /**
     * Initiates launching sequence by checking if all components are functional, opening poddoors, firing mass drivers and then closing poddoors
     */
    // tg: code/game/machinery/computer/pod.dm:35
    public virtual async DmTask alarm()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
            return;

        if (connected == null)
        {
            await say(I18n.T("Cannot locate mass driver connector. Cancelling firing sequence!"));
            return;
        }

        foreach (obj_machinery_door_poddoor M in Dm.each<obj_machinery_door_poddoor>(range(range, this)))
        {
            if (Dm.eq(M.id, id))
                await M.open();
        }

        await sleep(2 * SECONDS);
        foreach (obj_machinery_mass_driver M in Dm.each<obj_machinery_mass_driver>(range(range, this)))
        {
            if (Dm.eq(M.id, id))
            {
                M.power = connected.power;
                await M.drive();
            }
        }

        await sleep(5 * SECONDS);
        foreach (obj_machinery_door_poddoor M in Dm.each<obj_machinery_door_poddoor>(range(range, this)))
        {
            if (Dm.eq(M.id, id))
                await M.close();
        }
    }

    // tg: code/game/machinery/computer/pod.dm:58
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "MassDriverControl", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/computer/pod.dm:65
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        // If the cooldown has finished, just display the time. If the cooldown hasn't finished, display the cooldown.
        float display_time = massdriver_countdown <= Dm.num(world.time) ? Dm.num(time) : max(0, massdriver_countdown - Dm.num(world.time)) * 0.1f; // COOLDOWN_FINISHED, COOLDOWN_TIMELEFT
        data["connected"] = connected != null ? TRUE : FALSE;
        data["seconds"] = round(display_time % 60);
        data["minutes"] = round((display_time - Dm.num(data["seconds"])) / 60);
        data["timing"] = timing;
        data["power"] = connected != null ? connected.power : 0.25f;
        data["poddoor"] = FALSE;
        foreach (obj_machinery_door_poddoor door in Dm.each<obj_machinery_door_poddoor>(range(range, this)))
        {
            if (Dm.eq(door.id, id))
            {
                data["poddoor"] = TRUE;
                break;
            }
        }
        return data;
    }

    // tg: code/game/machinery/computer/pod.dm:81
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(await allowed__proc(usr)))
        {
            to_chat(usr, span_warning(I18n.T("Access denied.")));
            return dot;
        }

        if (Dm.eq(action, "set_power"))
        {
            if (connected == null)
                return dot;
            float? value = text2num(@params["power"]);
            if (!Dm.truthy(value))
                return dot;
            value = clamp(Dm.num(value), 0.25f, 16);
            connected.power = value;
            return TRUE;
        }
        else if (Dm.eq(action, "launch"))
        {
            await alarm();
            return TRUE;
        }
        else if (Dm.eq(action, "time"))
        {
            timing = Dm.num(!Dm.truthy(timing));
            if (Dm.truthy(timing))
            {
                massdriver_countdown = Dm.num(world.time) + Dm.num(time) * SECONDS; // COOLDOWN_START(src, massdriver_countdown, time SECONDS)
                begin_processing();
            }
            else
            {
                time = max(0, massdriver_countdown - Dm.num(world.time)) * 0.1f; // COOLDOWN_TIMELEFT(src, massdriver_countdown) * 0.1
                massdriver_countdown = 0; // COOLDOWN_RESET(src, massdriver_countdown)
                end_processing();
            }
            return TRUE;
        }
        else if (Dm.eq(action, "input"))
        {
            float? value = text2num(@params["adjust"]);
            if (!Dm.truthy(value))
                return dot;
            value = round(Dm.num(time) + Dm.num(value));
            time = clamp(Dm.num(value), 0, 120);
            return TRUE;
        }
        else if (Dm.eq(action, "door"))
        {
            foreach (obj_machinery_door_poddoor M in Dm.each<obj_machinery_door_poddoor>(range(range, this)))
            {
                if (Dm.eq(M.id, id))
                {
                    if (Dm.truthy(M.density))
                        await M.open();
                    else
                        await M.close();
                }
            }
            return TRUE;
        }
        else if (Dm.eq(action, "driver_test"))
        {
            foreach (obj_machinery_mass_driver M in Dm.each<obj_machinery_mass_driver>(range(range, this)))
            {
                if (Dm.eq(M.id, id))
                {
                    M.power = connected?.power;
                    await M.drive();
                }
            }
            return TRUE;
        }
        return dot;
    }
}
