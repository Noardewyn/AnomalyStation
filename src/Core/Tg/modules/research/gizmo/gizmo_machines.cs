// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo machines, science's strange objects that do something when the right wire sequences are pulsed: a
// random name and look, the controller that makes their wires (and the voice gizmo's voice), the spinning beyblade gizmo, the
// toggle gizmos that hum to life with their on state (the tucker tubes Meta maps among them). The var blocks are generated (D111).
//
// GAP(types): tg's `var/datum/gizmo_controller/controller` (:18) is generated as a Type (rules 3.2 V3: its default is a path that
// tg replaces by the controller on Initialize); the controller the gizmo holds is controller_value (the same split as
// /obj/machinery/var/circuit).

/// Science object that behaves similairly to to strange objects/relics, but is activated by cracking wire sequences and other functions
// tg: code/modules/research/gizmo/gizmo_machines.dm:2
public partial class obj_machinery_gizmo : obj_machinery
{
    /// GAP(types): tg's `controller` (:18) once Initialize made it: reference to the gizmo. We dont actually need to track this for
    /// anything but ease of vv
    public datum_gizmo_controller controller_value;

    /// Possible names to pick from to keep things confusing (var/static/list/possible_names)
    // tg: code/modules/research/gizmo/gizmo_machines.dm:20
    public static DmList possible_names => TgState.Current.obj_machinery_gizmo__possible_names ??= Dm.list(
        "gizmo", "jigger", "doohickey", "particle inverter", "polarity superplexer", "flitcher poster", "natty gonk", "quantum quantum",
        "entropy nilum", "tachyon streamer", "doing device", "task operator", "interface responder", "kinetic observer", "turbo encabulator",
        "statistic responder", "possibility matrix", "toety aaier", "phase anchor", "thingamajing"
    );

    // tg: code/modules/research/gizmo/gizmo_machines.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        name = Dm.str(pick(possible_names));

        if (Dm.truthy(icon_states))
        {
            base_icon_state = Dm.str(pick(icon_states));
            icon_state = Dm.str(base_icon_state);
        }

        controller_value = Dm.@new(controller, this) as datum_gizmo_controller;
        Waiting.NoSleep(controller_value.generate_interfaces(this));

        RegisterSignal(this, COMSIG_GIZMO_START_MOVING, nameof(start_moving));
        RegisterSignal(this, COMSIG_GIZMO_STOP_MOVING, nameof(stop_moving));
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:41
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);

        if (Dm.truthy(is_wire_tool(tool)))
        {
            await attempt_wire_interaction(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:49
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = ITEM_INTERACT_BLOCKING;
        if (Dm.eq(await default_unfasten_wrench(user, tool), SUCCESSFUL_UNFASTEN))
            return ITEM_INTERACT_SUCCESS;
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:54
    [SignalHandler]
    public virtual void start_moving(datum source = null, datum_gizpulse pulse = null)
    {
        on_start_moving(pulse);
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:59
    [SignalHandler]
    public virtual void stop_moving(datum source = null, datum_gizpulse pulse = null)
    {
        on_stop_moving(pulse);
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:64
    public virtual void on_start_moving(datum_gizpulse pulse = null)
    {
        return;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:67
    public virtual void on_stop_moving(datum_gizpulse pulse = null)
    {
        return;
    }
}

/// Gizmo that comes with a gizmo interface to start moving
// tg: code/modules/research/gizmo/gizmo_machines.dm:71
public partial class obj_machinery_gizmo_beyblade : obj_machinery_gizmo
{
    // tg: code/modules/research/gizmo/gizmo_machines.dm:79
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);

        icon_state = base_icon_state + (Dm.truthy(moving) ? "_spinning" : "");
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:84
    public override void on_start_moving(datum_gizpulse pulse = null)
    {
        density = TRUE;

        moving = TRUE;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:90
    public override void on_stop_moving(datum_gizpulse pulse = null)
    {
        density = FALSE;

        moving = FALSE;
        update_appearance(UPDATE_ICON);
    }
}

/// A gizmo with some sort of "on" state. Really only for visuals
// tg: code/modules/research/gizmo/gizmo_machines.dm:97
public partial class obj_machinery_gizmo_toggle : obj_machinery_gizmo
{
    // tg: code/modules/research/gizmo/gizmo_machines.dm:104
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_GIZMO_ON_STATE, nameof(on_state__proc));
        RegisterSignal(this, COMSIG_GIZMO_OFF_STATE, nameof(off_state));
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:110
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = await base.update_icon(updates, __extra);

        icon_state = base_icon_state + (Dm.truthy(on_state) ? "_on" : "");
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:120
    [SignalHandler]
    public virtual void on_state__proc(datum source = null)
    {
        on_state = TRUE;
        update_appearance(UPDATE_ICON);
        visible_message(span_smallnoticeital(I18n.T("[] hums to life.", this)));
    }

    // tg: code/modules/research/gizmo/gizmo_machines.dm:127
    [SignalHandler]
    public virtual void off_state(datum source = null)
    {
        on_state = FALSE;
        update_appearance(UPDATE_ICON);
        visible_message(span_smallnoticeital(I18n.T("[] powers down.", this)));
    }
}

// The gizmos' static list of names (rules 10.6).
public sealed partial class TgState
{
    // tg: code/modules/research/gizmo/gizmo_machines.dm:20
    internal DmList obj_machinery_gizmo__possible_names;
}
