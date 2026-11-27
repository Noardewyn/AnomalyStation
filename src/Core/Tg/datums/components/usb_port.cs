// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/usb_port.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a machine's USB port. A USB cable that holds a shell's circuit plugs into it, the machine's own circuit components
// join that circuit, and the cable snaps out when the circuit is carried out of range. The var block is generated.

/// Opens up a USB port that can be connected to by circuits, creating registerable circuit components
// tg: code/datums/components/usb_port.dm:2
public partial class datum_component_usb_port : datum_component
{
    // tg: code/datums/components/usb_port.dm:28
    [DmParams("circuit_component_types", "extra_registration_callback", "extra_unregistration_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList circuit_component_types = Dm.arg(__extra, 0) as DmList;
        object extra_registration_callback = Dm.arg(__extra, 1);
        object extra_unregistration_callback = Dm.arg(__extra, 2);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.circuit_component_types = circuit_component_types;
        this.extra_registration_callback = extra_registration_callback;
        this.extra_unregistration_callback = extra_unregistration_callback;
        return null;
    }

    // tg: code/datums/components/usb_port.dm:36
    public virtual async DmTask set_circuit_components(DmList components = null)
    {
        float should_register = FALSE;
        if (Dm.truthy(length(circuit_components)))
        {
            await UnregisterFromParent();
            should_register = TRUE;
            QDEL_LAZYLIST(ref circuit_components);
        }

        foreach (object circuit_component in Dm.each(components))
        {
            obj_item_circuit_component component = circuit_component as obj_item_circuit_component;
            if (ispath(circuit_component))
                component = Dm.@new((Type)circuit_component, null) as obj_item_circuit_component;
            if (!Dm.truthy(should_register))
                component.register_usb_parent(parent as atom_movable);
            RegisterSignal(component, COMSIG_CIRCUIT_COMPONENT_SAVE, nameof(save_component));
            LAZYADD(ref circuit_components, component);
        }

        if (Dm.truthy(should_register))
            await RegisterWithParent();
    }

    // tg: code/datums/components/usb_port.dm:55
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_USB_CABLE_TRY_ATTACH, nameof(on_atom_usb_cable_try_attach));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_MOVABLE_CIRCUIT_LOADED, nameof(on_load));
        await AddComponentFrom(REF(parent), typeof(datum_component_shuttle_move_deferred_checks), nameof(on_moved));

        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_components))
            component.register_usb_parent(parent as atom_movable);

        if (Dm.truthy(extra_registration_callback))
        {
            if (istype(extra_registration_callback, typeof(datum_callback)))
            {
                datum_callback callback = extra_registration_callback as datum_callback;
                await callback.Invoke(this);
            }
            else
                await Dm.Call(parent, Dm.str(extra_registration_callback), this);
        }
    }

    // tg: code/datums/components/usb_port.dm:71
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_USB_CABLE_TRY_ATTACH,
            COMSIG_ATOM_EXAMINE,
            COMSIG_MOVABLE_CIRCUIT_LOADED));
        RemoveComponentSource(REF(parent), typeof(datum_component_shuttle_move_deferred_checks));

        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_components))
            component.unregister_usb_parent(parent as atom_movable);

        if (Dm.truthy(extra_unregistration_callback))
        {
            if (istype(extra_unregistration_callback, typeof(datum_callback)))
            {
                datum_callback callback = extra_unregistration_callback as datum_callback;
                await callback.Invoke(this);
            }
            else
                await Dm.Call(parent, Dm.str(extra_unregistration_callback), this);
        }

        unregister_circuit_signals();
        unregister_physical_signals();
        attached_circuit = null;
    }

    // tg: code/datums/components/usb_port.dm:93
    [SignalHandler]
    public virtual void save_component(datum source = null, DmList objects = null)
    {
        Dm.append(objects, parent);
    }

    // tg: code/datums/components/usb_port.dm:97
    [SignalHandler]
    public virtual void on_load(datum source = null, obj_item_integrated_circuit circuit = null, DmList components = null)
    {
        DmList components_in_list = Dm.list();
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(components))
            Dm.append(components_in_list, component.type);

        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_components))
        {
            if (Dm.@in(component.type, components_in_list))
                continue;
            Dm.append(components, component.type);
        }
        Waiting.NoSleep(set_circuit_components(components));
        obj_item_usb_cable cable = Dm.@new<obj_item_usb_cable>(circuit.drop_location());
        cable.attached_circuit = circuit;

        on_atom_usb_cable_try_attach(this, cable, null);
    }

    // tg: code/datums/components/usb_port.dm:113
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref circuit_components);
        QDEL_NULL(ref usb_cable_beam);

        attached_circuit = null;
        usb_cable_ref = null;

        return base.Destroy(force);
    }

    // tg: code/datums/components/usb_port.dm:122
    public virtual void unregister_circuit_signals()
    {
        if (attached_circuit == null)
            return;

        UnregisterSignal(attached_circuit, Dm.list(
            COMSIG_CIRCUIT_SHELL_REMOVED,
            COMSIG_QDELETING,
            COMSIG_CIRCUIT_SET_SHELL));
    }

    // tg: code/datums/components/usb_port.dm:132
    public virtual void unregister_physical_signals()
    {
        if (physical_object == null)
            return;

        SEND_SIGNAL(COMSIG_USB_PORT_UNREGISTER_PHYSICAL_OBJECT, Dm.list(this, physical_object));
        UnregisterSignal(physical_object, COMSIG_ATOM_EXAMINE);
        RemoveComponentSource(REF(physical_object), typeof(datum_component_shuttle_move_deferred_checks));
    }

    // tg: code/datums/components/usb_port.dm:140
    public virtual async DmTask attach_circuit_components(obj_item_integrated_circuit circuitboard = null)
    {
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_components))
        {
            await circuitboard.add_component(component);
            RegisterSignal(component, COMSIG_CIRCUIT_COMPONENT_REMOVED, nameof(on_circuit_component_removed));
        }
    }

    // tg: code/datums/components/usb_port.dm:145
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        if (attached_circuit == null)
            Dm.append(examine_text, span_notice(I18n.T("There is a USB port on the front.")));
        else
            Dm.append(examine_text, span_notice(I18n.T("[] is connected to [] by a USB port.", (object)attached_circuit.shell ?? attached_circuit, ((atom)parent).p_them())));
    }

    // tg: code/datums/components/usb_port.dm:153
    [SignalHandler]
    public virtual void on_examine_shell(datum source = null, mob user = null, DmList examine_text = null)
    {
        Dm.append(examine_text, span_notice(I18n.T("[] [] attached to [] with a USB cable.", ((atom)source).p_They(), ((atom)source).p_are(), parent)));
    }

    // tg: code/datums/components/usb_port.dm:158
    [SignalHandler]
    public virtual object on_atom_usb_cable_try_attach(datum source = null, obj_item_usb_cable connecting_cable = null, mob user = null)
    {
        if (!Dm.truthy(LAZYLEN(circuit_components)))
            Waiting.NoSleep(set_circuit_components(circuit_component_types));

        atom atom_parent = parent as atom;

        if (attached_circuit != null)
        {
            if (user != null)
                atom_parent.balloon_alert(user, I18n.T("usb already connected"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        if (connecting_cable.attached_circuit == null)
        {
            if (user != null)
                connecting_cable.balloon_alert(user, I18n.T("connect to a shell first"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        if (!IN_GIVEN_RANGE(connecting_cable.attached_circuit, parent, USB_CABLE_MAX_RANGE))
        {
            if (user != null)
                connecting_cable.balloon_alert(user, I18n.T("too far away"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        if (Dm.truthy(connecting_cable.attached_circuit.locked))
        {
            connecting_cable.balloon_alert(user, I18n.T("shell is locked!"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        usb_cable_ref = WEAKREF(connecting_cable) as datum_weakref;
        attached_circuit = connecting_cable.attached_circuit;

        Waiting.NoSleep(connecting_cable.forceMove(attached_circuit));
        Waiting.NoSleep(attach_circuit_components(attached_circuit));
        if (user != null)
            Waiting.NoSleep(attached_circuit.interact(user));

        atom_movable new_physical_object = attached_circuit.shell;
        if (new_physical_object == null)
            new_physical_object = attached_circuit;

        RegisterSignal(attached_circuit, COMSIG_CIRCUIT_SHELL_REMOVED, nameof(on_circuit_shell_removed));
        RegisterSignal(attached_circuit, COMSIG_QDELETING, nameof(on_circuit_deleting));
        RegisterSignal(attached_circuit, COMSIG_CIRCUIT_SET_SHELL, nameof(on_set_shell));
        Waiting.NoSleep(set_physical_object(new_physical_object));

        return COMSIG_USB_CABLE_ATTACHED;
    }

    // tg: code/datums/components/usb_port.dm:204
    public virtual async DmTask set_physical_object(atom_movable new_physical_object = null)
    {
        if (physical_object != null)
            unregister_physical_signals();
        if (usb_cable_beam != null)
            QDEL_NULL(ref usb_cable_beam);

        atom atom_parent = parent as atom;
        usb_cable_beam = atom_parent.Beam(new_physical_object, "usb_cable_beam", Dm.resource("icons/obj/science/circuits.dmi")) as datum_beam;

        await AddComponentFrom(REF(new_physical_object), typeof(datum_component_shuttle_move_deferred_checks), nameof(on_moved));
        RegisterSignal(new_physical_object, COMSIG_ATOM_EXAMINE, nameof(on_examine_shell));
        SEND_SIGNAL(COMSIG_USB_PORT_REGISTER_PHYSICAL_OBJECT, Dm.list(this, new_physical_object));
        physical_object = new_physical_object;
    }

    // Adds support for loading circuits without shells but with usb cables, or loading circuits with shells because the shells might not load first.
    // tg: code/datums/components/usb_port.dm:219
    [SignalHandler]
    public virtual void on_set_shell(datum source = null, atom_movable new_shell = null)
    {
        Waiting.NoSleep(set_physical_object(new_shell));
    }

    // tg: code/datums/components/usb_port.dm:223
    [SignalHandler]
    public virtual void on_moved()
    {
        if (attached_circuit == null)
            return;

        if (IN_GIVEN_RANGE(attached_circuit, parent, USB_CABLE_MAX_RANGE))
            return;

        Waiting.NoSleep(detach());
    }

    // tg: code/datums/components/usb_port.dm:234
    [SignalHandler]
    public virtual void on_circuit_deleting()
    {
        Waiting.NoSleep(detach());
        qdel(usb_cable_ref);
    }

    // tg: code/datums/components/usb_port.dm:239
    [SignalHandler]
    public virtual void on_circuit_component_removed(datum source = null)
    {
        Waiting.NoSleep(detach());
    }

    // tg: code/datums/components/usb_port.dm:243
    [SignalHandler]
    public virtual void on_circuit_shell_removed()
    {
        Waiting.NoSleep(detach());
    }

    // tg: code/datums/components/usb_port.dm:247
    public virtual async DmTask detach()
    {
        obj_item_usb_cable usb_cable = usb_cable_ref?.resolve() as obj_item_usb_cable;
        if (usb_cable == null)
            return;

        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(circuit_components))
        {
            UnregisterSignal(component, COMSIG_CIRCUIT_COMPONENT_REMOVED);
            await attached_circuit.remove_component(component);
            await component.moveToNullspace();
        }

        unregister_circuit_signals();
        unregister_physical_signals();

        atom atom_parent = parent as atom;
        await usb_cable.forceMove(atom_parent.drop_location() as atom);
        usb_cable.balloon_alert_to_hearers(I18n.T("*snap*"));

        physical_object = null;
        attached_circuit = null;
        usb_cable_ref = null;

        QDEL_NULL(ref usb_cable_beam);
    }
}
