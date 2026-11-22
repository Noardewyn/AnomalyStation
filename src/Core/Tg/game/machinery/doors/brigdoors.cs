// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/brigdoors.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate brig timers, D316, added by the owner from gaps W1: the brig cells' door timers and their BrigTimer window).
// The var blocks are generated (D111).

/**
 * Brig Door control displays.
 *
 * This is a controls the timer for the brig doors, displays the timer on itself and
 * has a popup window when used, allowing to set the timer.
 */
// tg: code/game/machinery/doors/brigdoors.dm:12
public partial class obj_machinery_status_display_door_timer : obj_machinery_status_display
{
    // tg: code/game/machinery/doors/brigdoors.dm:1
    private const float MAX_TIMER = 15 * MINUTES;
    // tg: code/game/machinery/doors/brigdoors.dm:2
    private const float PRESET_SHORT = 2 * MINUTES;
    // tg: code/game/machinery/doors/brigdoors.dm:3
    private const float PRESET_MEDIUM = 3 * MINUTES;
    // tg: code/game/machinery/doors/brigdoors.dm:4
    private const float PRESET_LONG = 5 * MINUTES;

    // tg: code/game/machinery/doors/brigdoors.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (id != null)
        {
            foreach (obj_machinery_door_window_brigdoor M in Dm.each<obj_machinery_door_window_brigdoor>(urange(20, this)))
            {
                if (Dm.eq(M.id, id))
                    Dm.append(doors, WEAKREF(M));
            }

            foreach (obj_machinery_door_airlock_security M in Dm.each<obj_machinery_door_airlock_security>(urange(20, this)))
            {
                if (Dm.eq(M.id, id))
                    Dm.append(doors, WEAKREF(M));
            }

            foreach (obj_machinery_flasher F in Dm.each<obj_machinery_flasher>(urange(20, this)))
            {
                if (Dm.eq(F.id, id))
                    Dm.append(flashers, WEAKREF(F));
            }

            foreach (obj_structure_closet_secure_closet_brig C in Dm.each<obj_structure_closet_secure_closet_brig>(urange(20, this)))
            {
                if (Dm.eq(C.id, id))
                    Dm.append(closets, WEAKREF(C));
            }
        }

        if (!Dm.truthy(length(doors)) && !Dm.truthy(length(flashers)) && Dm.truthy(length(closets)))
            Waiting.NoSleep(atom_break());

        RegisterSignal(SSdcs, COMSIG_GLOB_GREY_TIDE, nameof(grey_tide));
        return dot;
    }

    //Main door timer loop, if it's timing and time is >0 reduce time by 1.
    // if it's less than 0, open door, reset timer
    // update the door_timer window and the icon
    // tg: code/game/machinery/doors/brigdoors.dm:65
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            // No power, no processing.
            update_appearance();
            return PROCESS_KILL;
        }

        if (!Dm.truthy(timing))
            return PROCESS_KILL;

        if (world.time - activation_time >= timer_duration)
            await timer_end(); // open doors, reset timer, clear status screen
        update_content();
        return null;
    }

    /**
     * Update the display content.
     */
    // tg: code/game/machinery/doors/brigdoors.dm:81
    public virtual void update_content()
    {
        float time_left = this.time_left(seconds: TRUE);

        if (time_left == 0)
        {
            set_messages("", "");
            return;
        }

        object disp1 = name;
        string disp2 = Dm.text("[]:[]", add_leading(num2text(Dm.mod(time_left / 60, 60)), 2, "0"), add_leading(num2text(Dm.mod(time_left, 60)), 2, "0"));
        set_messages(disp1, disp2);
    }

    /**
     * Starts counting down the timer and closes linked the door.
     * The timer is expected to have already been set by set_timer()
     */
    // tg: code/game/machinery/doors/brigdoors.dm:96
    public virtual async DmTask<object> timer_start()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return 0;

        activation_time = world.time;
        timing = TRUE;
        begin_processing();

        foreach (datum_weakref door_ref in Dm.each_cast<datum_weakref>(doors))
        {
            var door = door_ref.resolve() as obj_machinery_door;
            if (door == null)
            {
                Dm.remove(doors, door_ref);
                continue;
            }
            if (Dm.truthy(door.density))
                continue;
            Waiting.Detach(door.close());
        }

        foreach (datum_weakref closet_ref in Dm.each_cast<datum_weakref>(closets))
        {
            var closet = closet_ref.resolve() as obj_structure_closet_secure_closet_brig;
            if (closet == null)
            {
                Dm.remove(closets, closet_ref);
                continue;
            }
            if (Dm.truthy(closet.broken))
                continue;
            if (Dm.truthy(closet.opened) && !Dm.truthy(await closet.close()))
                continue;
            closet.locked = TRUE;
            closet.update_appearance();
        }
        return 1;
    }

    /**
     * Stops the timer and resets the timer to 0, and opens the linked door.
     * Arguments:
     * * forced - TRUE if it was forced to stop rather than timing out. Will skip radioing, etc.
     */
    // tg: code/game/machinery/doors/brigdoors.dm:131
    public virtual async DmTask<object> timer_end(object forced = null)
    {
        forced ??= FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return 0;

        if (!Dm.truthy(forced))
            await aas_config_announce(typeof(datum_aas_config_entry_brig_cell_release_announcement), Dm.list(Dm.kv("CELL", name)), this, Dm.list(broadcast_channel));

        timing = FALSE;
        activation_time = 0;
        set_timer(0);
        end_processing();

        foreach (datum_weakref door_ref in Dm.each_cast<datum_weakref>(doors))
        {
            var door = door_ref.resolve() as obj_machinery_door;
            if (door == null)
            {
                Dm.remove(doors, door_ref);
                continue;
            }
            if (!Dm.truthy(door.density))
                continue;
            Waiting.Detach(door.open());
        }

        foreach (datum_weakref closet_ref in Dm.each_cast<datum_weakref>(closets))
        {
            var closet = closet_ref.resolve() as obj_structure_closet_secure_closet_brig;
            if (closet == null)
            {
                Dm.remove(closets, closet_ref);
                continue;
            }
            if (Dm.truthy(closet.broken))
                continue;
            if (Dm.truthy(closet.opened))
                continue;
            closet.locked = FALSE;
            closet.update_appearance();
        }

        return 1;
    }

    /**
     * Return time left.
     * Arguments:
     * * seconds - Return the time in seconds if TRUE, else deciseconds.
     */
    // tg: code/game/machinery/doors/brigdoors.dm:171
    public virtual float time_left(object seconds = null)
    {
        float dot = max(0, timer_duration + (Dm.truthy(activation_time) ? activation_time - world.time : 0));
        if (Dm.truthy(seconds))
            dot /= 1 * SECONDS;
        return dot;
    }

    /**
     * Set the timer. Does NOT automatically start counting down, but does update the display.
     *
     * returns FALSE if no change occurred
     *
     * Arguments:
     * value - time in deciseconds to set the timer for.
     */
    // tg: code/game/machinery/doors/brigdoors.dm:184
    public virtual object set_timer(object value = null)
    {
        float new_time = clamp(Dm.num(value), 0, MAX_TIMER);
        object dot = new_time != timer_duration ? TRUE : FALSE; //return 1 on change
        timer_duration = new_time;
        update_content();
        return dot;
    }

    // tg: code/game/machinery/doors/brigdoors.dm:190
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "BrigTimer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/doors/brigdoors.dm:196
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        float time_left = this.time_left(seconds: TRUE);
        data["seconds"] = round(Dm.mod(time_left, 60));
        data["minutes"] = round((time_left - Dm.num(data["seconds"])) / 60);
        data["timing"] = timing;
        data["flash_charging"] = FALSE;
        foreach (datum_weakref flash_ref in Dm.each_cast<datum_weakref>(flashers))
        {
            var flasher = flash_ref.resolve() as obj_machinery_flasher;
            if (flasher == null)
            {
                Dm.remove(flashers, flash_ref);
                continue;
            }
            if (!(flasher.flash_cooldown <= world.time)) // COOLDOWN_FINISHED(flasher, flash_cooldown)
            {
                data["flash_charging"] = TRUE;
                break;
            }
        }
        return data;
    }

    // tg: code/game/machinery/doors/brigdoors.dm:213
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        dot = TRUE;

        mob user = usr;

        if (!Dm.truthy(await allowed__proc(usr)))
        {
            to_chat(usr, span_warning(I18n.T("Access denied.")));
            return FALSE;
        }

        var sw__1 = action;
        if (Dm.eq(sw__1, "time"))
        {
            object value = text2num(@params["adjust"]);
            if (Dm.truthy(value))
            {
                dot = set_timer(timer_duration + Dm.num(value));
                user.investigate_log(Dm.text("modified the timer by [] seconds for cell [], currently []", Dm.num(value) / 10, id, time_left(seconds: TRUE)), INVESTIGATE_RECORDS);
                user.log_message(Dm.text("modified the timer by [] seconds for cell [], currently []", Dm.num(value) / 10, id, time_left(seconds: TRUE)), LOG_ATTACK);
            }
        }
        else if (Dm.eq(sw__1, "start"))
        {
            await timer_start();
            user.investigate_log(Dm.text("has started []'s timer of [] seconds", id, time_left(seconds: TRUE)), INVESTIGATE_RECORDS);
            user.log_message(Dm.text("has started []'s timer of [] seconds", id, time_left(seconds: TRUE)), LOG_ATTACK);
        }
        else if (Dm.eq(sw__1, "stop"))
        {
            user.investigate_log(Dm.text("has stopped []'s timer of [] seconds", id, time_left(seconds: TRUE)), INVESTIGATE_RECORDS);
            user.log_message(Dm.text("has stopped []'s timer of [] seconds", id, time_left(seconds: TRUE)), LOG_ATTACK);
            await timer_end(forced: TRUE);
        }
        else if (Dm.eq(sw__1, "flash"))
        {
            user.investigate_log(Dm.text("has flashed cell []", id), INVESTIGATE_RECORDS);
            user.log_message(Dm.text("has flashed cell []", id), LOG_ATTACK);
            foreach (datum_weakref flash_ref in Dm.each_cast<datum_weakref>(flashers))
            {
                var flasher = flash_ref.resolve() as obj_machinery_flasher;
                if (flasher == null)
                {
                    Dm.remove(flashers, flash_ref);
                    continue;
                }
                await flasher.flash();
            }
        }
        else if (Dm.eq(sw__1, "preset"))
        {
            object preset = @params["preset"];
            float preset_time = time_left();
            if (Dm.eq(preset, "short"))
                preset_time = PRESET_SHORT;
            else if (Dm.eq(preset, "medium"))
                preset_time = PRESET_MEDIUM;
            else if (Dm.eq(preset, "long"))
                preset_time = PRESET_LONG;
            dot = set_timer(preset_time);
            user.investigate_log(Dm.text("set cell []'s timer to [] seconds", id, preset_time / 10), INVESTIGATE_RECORDS);
            user.log_message(Dm.text("set cell []'s timer to [] seconds", id, preset_time / 10), LOG_ATTACK);
            if (Dm.truthy(timing))
                activation_time = world.time;
        }
        else
            dot = FALSE;
        return dot;
    }

    // tg: code/game/machinery/doors/brigdoors.dm:268
    [SignalHandler]
    public virtual void grey_tide(datum source = null, DmList grey_tide_areas = null)
    {
        if (!Dm.truthy(is_station_level(z)))
            return;

        foreach (object area_type in Dm.each(grey_tide_areas))
        {
            if (!istype(get_area(this), area_type as Type))
                continue;
            Waiting.Detach(timer_end(forced: TRUE));
        }
    }
}
