// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/core/usb_cable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the USB cable: it takes a shell's circuit and then a machine's USB port (the machines'
// USB ports and their components are CI3's: a cable on a machine is refused by the machine's own code until then). The var block
// is generated (D111).

/// A cable that can connect integrated circuits to anything with a USB port, such as computers and machines.
// tg: code/modules/wiremod/core/usb_cable.dm:2
public partial class obj_item_usb_cable : obj_item
{
    // tg: code/modules/wiremod/core/usb_cable.dm:20
    public override object Destroy(float? force = null)
    {
        attached_circuit = null;
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_containers), this, Initialize__connections));
        return dot;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:28  var/static/list/connections (a proc-level static of Initialize, rules 10.6)
    private static ref DmList Initialize__connections => ref TgState.Current.obj_item_usb_cable__Initialize__connections;

    // tg: code/modules/wiremod/core/usb_cable.dm:35
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (attached_circuit != null)
            Dm.append(ref dot, span_notice(I18n.T("It is attached to [].", (object)attached_circuit.shell ?? attached_circuit)));
        return dot;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:41
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.pre_attack(target, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;

        if (prob(1))
        {
            balloon_alert(user, I18n.T("wrong way, god damnit"));
            return TRUE;
        }

        object signal_result = target.SEND_SIGNAL(COMSIG_ATOM_USB_CABLE_TRY_ATTACH, Dm.list(target, this, user));

        if (Dm.truthy(Dm.bitand(signal_result, COMSIG_USB_CABLE_CONNECTED_TO_CIRCUIT)))
        {
            if (attached_circuit == null)
                throw Dm.CRASH("Producers of COMSIG_USB_CABLE_CONNECTED_TO_CIRCUIT must set attached_circuit", "code/modules/wiremod/core/usb_cable.dm", 54);
            balloon_alert(user, I18n.T("connected to circuit\nconnect to a port"));

            playsound(this, "sound/machines/pda_button/pda_button1.ogg", 20, TRUE);

            return TRUE;
        }

        if (Dm.truthy(Dm.bitand(signal_result, COMSIG_USB_CABLE_ATTACHED)))
        {
            // Short messages are better to read
            string connection_description = I18n.T("port");
            if (istype(target, typeof(obj_machinery_computer)))
                connection_description = I18n.T("computer");
            else if (ismachinery(target))
                connection_description = I18n.T("machine");

            balloon_alert(user, I18n.T("connected to []", connection_description));
            playsound(this, "sound/items/tools/screwdriver2.ogg", 20, TRUE);

            return TRUE;
        }

        if (Dm.truthy(Dm.bitand(signal_result, COMSIG_CANCEL_USB_CABLE_ATTACK)))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:79
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is wrapping [] around [] neck! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        return OXYLOSS;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:83
    [SignalHandler]
    public virtual void on_moved()
    {
        if (Dm.truthy(defer_range_checks))
            return;
        check_in_range();
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:90
    public override async DmTask<object> beforeShuttleMove(turf newT = null, object rotation = null, object move_mode = null, obj_docking_port_mobile moving_dock = null)
    {
        object dot = await base.beforeShuttleMove(newT, rotation, move_mode, moving_dock);
        before_shuttle_move();
        return dot;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:94
    [SignalHandler]
    public virtual void before_shuttle_move()
    {
        defer_range_checks = TRUE;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:99
    public override async DmTask<object> afterShuttleMove(turf oldT = null, DmList movement_force = null, object shuttle_dir = null, object shuttle_preferred_direction = null, object move_dir = null, object rotation = null)
    {
        object dot = await base.afterShuttleMove(oldT, movement_force, shuttle_dir, shuttle_preferred_direction, move_dir, rotation);
        after_shuttle_move();
        return dot;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:103
    [SignalHandler]
    public virtual void after_shuttle_move()
    {
        defer_range_checks = FALSE;
        check_in_range();
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:109
    public virtual object check_in_range()
    {
        if (attached_circuit == null)
            return FALSE;

        if (!IN_GIVEN_RANGE(attached_circuit, this, USB_CABLE_MAX_RANGE))
        {
            balloon_alert_to_viewers(I18n.T("detached, too far away"));
            attached_circuit = null;
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/wiremod/core/usb_cable.dm:120
    [SignalHandler]
    public virtual void on_circuit_qdeling()
    {
        attached_circuit = null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/core/usb_cable.dm:28  (var/static/list/connections in /obj/item/usb_cable/Initialize)
    internal DmList obj_item_usb_cable__Initialize__connections = Dm.list(
        Dm.kv(COMSIG_MOVABLE_MOVED, nameof(obj_item_usb_cable.on_moved)),
        Dm.kv(COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, nameof(obj_item_usb_cable.before_shuttle_move)),
        Dm.kv(COMSIG_ATOM_AFTER_SHUTTLE_MOVE, nameof(obj_item_usb_cable.after_shuttle_move)));
}
