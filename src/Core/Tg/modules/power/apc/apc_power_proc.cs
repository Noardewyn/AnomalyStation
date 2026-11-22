// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/apc/apc_power_proc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice 1: connect_to_network (the network building reaches an APC through its terminal). M7 slice 3 (the APC): the rest of
// the file.

// tg: code/modules/power/apc/apc_power_proc.dm:1
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/modules/power/apc/apc_power_proc.dm:1
    [DmParams()]
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:4
    public override object connect_to_network()
    {
        //Override because the APC does not directly connect to the network; it goes through a terminal.
        //The terminal is what the power computer looks for anyway.
        if (Dm.truthy(terminal))
            terminal.connect_to_network();
        return null;
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:10
    public virtual void make_terminal(object terminal_cable_layer = null)
    {
        terminal_cable_layer ??= cable_layer;
        //attempt to locate a terminal if mappers/map export placed it here
        terminal = Dm.locate<obj_machinery_power_terminal>(loc);
        // create a terminal object at the same position as original turf loc
        if (QDELETED(terminal))
            terminal = Dm.@new<obj_machinery_power_terminal>(loc);
        terminal.cable_layer = Dm.num(terminal_cable_layer);
        Waiting.NoSleep(terminal.setDir(dir));
        terminal.master = this;
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:20
    public virtual void toggle_nightshift_lights(mob user = null)
    {
        if (Dm.truthy(low_power_nightshift_lights))
        {
            balloon_alert(user, I18n.T("power is too low!"));
            return;
        }
        if (last_nightshift_switch > world.time - 10 * SECONDS) //~10 seconds between each toggle to prevent spamming
        {
            balloon_alert(user, I18n.T("night breaker is cycling!"));
            return;
        }
        last_nightshift_switch = world.time;
        set_nightshift(Dm.not(nightshift_lights));
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:30
    public virtual void update()
    {
        if (Dm.truthy(operating) && !Dm.truthy(shorted) && !Dm.truthy(failure_timer))
        {
            area.power_light = Dm.num(lighting > APC_CHANNEL_AUTO_OFF);
            area.power_equip = Dm.num(Dm.num(equipment) > APC_CHANNEL_AUTO_OFF);
            area.power_environ = Dm.num(environ > APC_CHANNEL_AUTO_OFF);
            playsound(this.loc, Dm.resource("sound/machines/terminal/terminal_on.ogg"), 50, FALSE);
        }
        else
        {
            area.power_light = FALSE;
            area.power_equip = FALSE;
            area.power_environ = FALSE;
            playsound(this.loc, Dm.resource("sound/machines/terminal/terminal_off.ogg"), 50, FALSE);
        }
        area.power_change();
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:43
    public virtual async DmTask toggle_breaker(mob user = null)
    {
        if (!Dm.truthy(is_operational) || Dm.truthy(failure_timer))
            return;
        operating = Dm.not(operating);
        if (Dm.truthy(user))
        {
            string enabled_or_disabled = Dm.truthy(operating) ? I18n.T("enabled") : I18n.T("disabled");
            balloon_alert(user, I18n.T("power []", enabled_or_disabled));
            user.log_message(Dm.text("turned [] the []", enabled_or_disabled, this), LOG_GAME);
            add_hiddenprint(user);
        }
        update();
        update_appearance();
    }

    /// Returns the surplus energy from the terminal's grid.
    // tg: code/modules/power/apc/apc_power_proc.dm:56
    public override object surplus()
    {
        if (Dm.truthy(terminal))
            return terminal.surplus();
        return 0;
    }

    /// Adds load (energy) to the terminal's grid.
    // tg: code/modules/power/apc/apc_power_proc.dm:62
    public override void add_load(object amount = null)
    {
        if (Dm.truthy(terminal?.powernet))
            terminal.add_load(amount);
    }

    /// Returns the amount of energy the terminal's grid has.
    // tg: code/modules/power/apc/apc_power_proc.dm:67
    public override object avail(object amount = null)
    {
        if (Dm.truthy(terminal))
            return terminal.avail(amount);
        return 0;
    }

    /// Returns the surplus energy from the terminal's grid and the cell.
    // tg: code/modules/power/apc/apc_power_proc.dm:73
    [DmParams()]
    public override object available_energy(float? consider_cell = null)
    {
        return Dm.add(charge(), surplus());
    }

    /**
     * Returns the new status value for an APC channel.
     *
     * // val 0=off, 1=off(auto) 2=on 3=on(auto)
     * // on 0=off, 1=on, 2=autooff
     * TODO: Make this use bitflags instead. It should take at most three lines, but it's out of scope for now.
     *
     * Arguments:
     * - val: The current status of the power channel.
     *   - [APC_CHANNEL_OFF]: The APCs channel has been manually set to off. This channel will not automatically change.
     *   - [APC_CHANNEL_AUTO_OFF]: The APCs channel is running on automatic and is currently off. Can be automatically set to [APC_CHANNEL_AUTO_ON].
     *   - [APC_CHANNEL_ON]: The APCs channel has been manually set to on. This will be automatically changed only if the APC runs completely out of power or is disabled.
     *   - [APC_CHANNEL_AUTO_ON]: The APCs channel is running on automatic and is currently on. Can be automatically set to [APC_CHANNEL_AUTO_OFF].
     * - on: An enum dictating how to change the channel's status.
     *   - [AUTOSET_FORCE_OFF]: The APC forces the channel to turn off. This includes manually set channels.
     *   - [AUTOSET_ON]: The APC allows automatic channels to turn back on.
     *   - [AUTOSET_OFF]: The APC turns automatic channels off.
     */
    // tg: code/modules/power/apc/apc_power_proc.dm:94
    public virtual object autoset(object val = null, object on = null)
    {
        object sw__1 = on;
        if (Dm.eq(sw__1, AUTOSET_FORCE_OFF))
        {
            if (Dm.eq(val, APC_CHANNEL_ON)) // if on, return off
                return APC_CHANNEL_OFF;
            else if (Dm.eq(val, APC_CHANNEL_AUTO_ON)) // if auto-on, return auto-off
                return APC_CHANNEL_AUTO_OFF;
        }
        else if (Dm.eq(sw__1, AUTOSET_ON))
        {
            if (Dm.eq(val, APC_CHANNEL_AUTO_OFF)) // if auto-off, return auto-on
                return APC_CHANNEL_AUTO_ON;
        }
        else if (Dm.eq(sw__1, AUTOSET_OFF))
        {
            if (Dm.eq(val, APC_CHANNEL_AUTO_ON)) // if auto-on, return auto-off
                return APC_CHANNEL_AUTO_OFF;
        }
        return val;
    }

    /**
     * Used by external forces to set the APCs channel status's.
     *
     * Arguments:
     * - val: The desired value of the subsystem:
     *   - 1: Manually sets the APCs channel to be [APC_CHANNEL_OFF].
     *   - 2: Manually sets the APCs channel to be [APC_CHANNEL_AUTO_ON]. If the APC doesn't have any power this defaults to [APC_CHANNEL_OFF] instead.
     *   - 3: Sets the APCs channel to be [APC_CHANNEL_AUTO_ON]. If the APC doesn't have enough power this defaults to [APC_CHANNEL_AUTO_OFF] instead.
     */
    // tg: code/modules/power/apc/apc_power_proc.dm:118
    public virtual object setsubsystem(object val = null)
    {
        if (cell != null && cell.charge > 0)
            return (Dm.eq(val, 1) ? (object)APC_CHANNEL_OFF : val);
        if (Dm.eq(val, 3))
            return APC_CHANNEL_AUTO_OFF;
        return APC_CHANNEL_OFF;
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:125
    public override async DmTask disconnect_terminal()
    {
        if (Dm.truthy(terminal))
        {
            terminal.master = null;
            terminal = null;
        }
    }

    /**
     * Temporarily disables all power to the room for a set duration
     *
     * Some rooms are immune to this effect due to having important machines
     *
     * * duration - the duration of the power failure in seconds (not deciseconds)
     */
    // tg: code/modules/power/apc/apc_power_proc.dm:137
    public virtual void energy_fail(object duration = null)
    {
        failure_timer = max(failure_timer, round(Dm.num(duration), Dm.num(SSmachines.wait) / 10)); // SSMACHINES_DT (__DEFINES/subsystems.dm:276)
        update();
        queue_icon_update();
    }

    // tg: code/modules/power/apc/apc_power_proc.dm:142  (set waitfor = FALSE: the body may sleep in the lights' update and
    // CHECK_TICK, so this is the wrapper of waiting-model.md 5.4)
    public virtual void set_nightshift(object on = null)
    {
        Waiting.WaitforFalse(set_nightshift__body(on));
    }

    private async DmTask set_nightshift__body(object on)
    {
        if (Dm.truthy(low_power_nightshift_lights) && !Dm.truthy(on))
            return;
        if (Dm.eq(nightshift_lights, on))
            return; //no change
        nightshift_lights = Dm.num(on);
        foreach (turf area_turf in Dm.each_cast<turf>(area.get_turfs_from_all_zlevels()))
        {
            foreach (obj_machinery_light night_light in Dm.each<obj_machinery_light>(area_turf))
            {
                if (Dm.truthy(night_light.nightshift_allowed))
                {
                    night_light.nightshift_enabled = nightshift_lights;
                    await night_light.update(FALSE);
                }
                CHECK_TICK();
            }
        }
    }
}
