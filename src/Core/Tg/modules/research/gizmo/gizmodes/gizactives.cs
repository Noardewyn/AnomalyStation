// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizactives.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo modes and their pulses' base, and the mode controls, which decide how the puzzle's solutions reach a
// mode's pulses: one solution per pulse to select it plus one to fire the selected one, one to cycle and one to fire, one per pulse
// to fire it at once, or one that cycles and fires. A mode fires its current pulse once per cooldown. The var blocks are generated
// (D111).

/// You can imagine a /datum/gizmodes as remote and a TV
/// The mode_controles is the remote. Maybe there's 9 buttons for 1-9, and pressing the buttons just goes to that channel!
/// Maybe there's 10 buttons, 1-9 for selecting a channel and an extra for going to the selected channel
/// Or maybe there's two buttons, one that cycles to the next number and one that then goes to that channel]
/// The giz_pulse is essentually the TV. Generally the functioning of the TV is themed, happy image :) or sad image :(
// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:6
public partial class datum_gizmodes : datum
{
    /// Pick the paths to generate and instantiate them
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:41
    public virtual async DmTask generate_modes(DmList trigger_callbacks = null, datum_gizmo_interface @interface = null)
    {
        this.@interface = @interface;

        DmList modes_to_spawn = Dm.list();

        foreach (object path in Dm.each(guaranteed_active_gizmodes))
        {
            if (Dm.eq(path, GIZMO_PICK_ONE))
            {
                DmList nested_list = guaranteed_active_gizmodes[path] as DmList;
                Dm.append(ref modes_to_spawn, pick_weight(nested_list));
            }
            else
                Dm.append(ref modes_to_spawn, path);
        }

        for (float i = 1, i__end = rand(min_modes, max_modes); i <= i__end; i++)
        {
            object path = pick_weight_take(possible_active_modes);
            if (!Dm.truthy(path))
                break;
            Dm.append(ref modes_to_spawn, path);
        }

        foreach (object path in Dm.each(modes_to_spawn))
            Dm.append(ref active_gizmodes, Dm.@new(path as Type));

        current_active = pick(active_gizmodes) as datum_gizpulse;

        Type mode_path = pick(mode_pulses) as Type;
        mode_pulse = Dm.@new(mode_path) as datum_gizpulse_mode_controle;
        mode_pulse.setup_mode_controle(this, active_gizmodes, trigger_callbacks);
        await DmTask.CompletedTask;
    }

    /// Activate this gizmode which in turn activates the active gizpulse (you following me here?)
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:69
    public virtual async DmTask activate(atom_movable holder = null)
    {
        if (Dm.truthy(current_active.affect_timer))
        {
            if (!(cooldown_timer <= world.time)) // COOLDOWN_FINISHED(src, cooldown_timer), inline (rules 11.1 (a))
                return;

            cooldown_timer = world.time + Dm.num(cooldown_time); // COOLDOWN_START(src, cooldown_timer, cooldown_time), inline
        }
        await current_active.activate(holder, this, @interface);
    }
}

/// Holds some functionaly that is activated and selected by the /gizmodes
// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:78
public partial class datum_gizpulse : datum
{
    /// Activate it do so stuff
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:83
    public virtual async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        return;
    }
}

/// Changes the currently activate gizpulse and adds a way to activate gizpulses
// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:87
public partial class datum_gizpulse_mode_controle : datum_gizpulse
{
    /// Select how activating the pulzes interacts with the gizpulses
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:91
    public virtual void setup_mode_controle(datum_gizmodes master = null, DmList active_gizmodes = null, DmList trigger_callbacks = null)
    {
        return;
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:95
public partial class datum_gizpulse_mode_controle_select_mode : datum_gizpulse_mode_controle
{
    /// Adds a puzzle for every possible made to select it, and a single wire to activate the selected mode
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:95
    public override void setup_mode_controle(datum_gizmodes master = null, DmList active_gizmodes = null, DmList trigger_callbacks = null)
    {
        foreach (object active in Dm.each(active_gizmodes))
            Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(this, nameof(select_mode), master, active));
        Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(master, nameof(datum_gizmodes.activate)));
    }

    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:100
    public virtual object select_mode(datum_gizmodes master = null, object new_active_mode = null, atom_movable holder = null)
    {
        master.current_active = new_active_mode as datum_gizpulse;
        // Give a hint to the user that something DID happen
        return GIZMO_PUZZLE_SOLVED_MODE_CONTROL;
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:106
public partial class datum_gizpulse_mode_controle_cycle_mode : datum_gizpulse_mode_controle
{
    /// Adds a puzzle to cycle to the next gizpulse, and a puzzle to activate the currently active mode
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:106
    public override void setup_mode_controle(datum_gizmodes master = null, DmList active_gizmodes = null, DmList trigger_callbacks = null)
    {
        Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(this, nameof(cycle_mode), master));
        Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(master, nameof(datum_gizmodes.activate)));
    }

    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:110
    public virtual async DmTask<object> cycle_mode(datum_gizmodes master = null, atom_movable holder = null)
    {
        await DmTask.CompletedTask;
        // Move to the next mode in the list (and loop back to 1 if needed)
        master.current_active = master.active_gizmodes[(Dm.num(master.active_gizmodes.Find(master.current_active)) % Dm.num(master.active_gizmodes.len)) + 1] as datum_gizpulse;
        // Give a hint to the user that something DID happen
        return GIZMO_PUZZLE_SOLVED_MODE_CONTROL;
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:117
public partial class datum_gizpulse_mode_controle_direct_activate : datum_gizpulse_mode_controle
{
    /// Adds a puzzle for every gizpulse that just immediately activates that gizpulse
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:117
    public override void setup_mode_controle(datum_gizmodes master = null, DmList active_gizmodes = null, DmList trigger_callbacks = null)
    {
        foreach (object active in Dm.each(active_gizmodes))
            Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(this, nameof(switch_and_activate), master, active));
    }

    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:121
    public virtual async DmTask switch_and_activate(datum_gizmodes master = null, datum_gizpulse active = null, atom_movable holder = null)
    {
        master.current_active = active;
        await master.activate(holder);
    }
}

// tg: code/modules/research/gizmo/gizmodes/gizactives.dm:126
public partial class datum_gizpulse_mode_controle_cycle_mode_activate : datum_gizpulse_mode_controle_cycle_mode
{
    /// Adds a single wire, that cycles and then activates
    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:126
    public override void setup_mode_controle(datum_gizmodes master = null, DmList active_gizmodes = null, DmList trigger_callbacks = null)
    {
        Dm.append(ref trigger_callbacks, Dm.@new<datum_callback>(this, nameof(cycle_mode), master));
    }

    // tg: code/modules/research/gizmo/gizmodes/gizactives.dm:129
    public override async DmTask<object> cycle_mode(datum_gizmodes master = null, atom_movable holder = null)
    {
        await base.cycle_mode(master, holder);
        await master.activate(holder);
        return null;
    }
}
