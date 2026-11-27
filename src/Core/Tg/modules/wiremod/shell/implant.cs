// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/implant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the circuit implant, a tiny circuit shell implanted under the skin, with its core component and its charge action.
// The var blocks are generated.

// tg: code/modules/wiremod/shell/implant.dm:1
public partial class obj_item_implant_circuit : obj_item_implant
{
    // tg: code/modules/wiremod/shell/implant.dm:13
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(typeof(obj_item_circuit_component_implant_core)), SHELL_CAPACITY_TINY));
        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, nameof(action_comp_registered));
        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED, nameof(action_comp_unregistered));
        RegisterSignal(this, COMSIG_IMPLANT_OTHER, nameof(on_new_implant));
        return dot;
    }

    // tg: code/modules/wiremod/shell/implant.dm:20
    [SignalHandler]
    public virtual void action_comp_registered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        LAZYADD(ref actions, Dm.@new<datum_action_innate_circuit_equipment_action>(this, action_comp));
    }

    // tg: code/modules/wiremod/shell/implant.dm:24
    [SignalHandler]
    public virtual void action_comp_unregistered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        datum_action_innate_circuit_equipment_action action = action_comp.granted_to[REF(this)] as datum_action_innate_circuit_equipment_action;
        if (!(action is datum_action_innate_circuit_equipment_action))
            return;
        LAZYREMOVE(ref actions, action);
        QDEL_LIST_ASSOC_VAL(action_comp.granted_to);
    }

    // tg: code/modules/wiremod/shell/implant.dm:32
    [SignalHandler]
    public virtual object on_new_implant(obj_item_implant source = null, DmList arguments = null, obj_item_implant other_implant = null)
    {
        if (!istype(other_implant, typeof(obj_item_implant_circuit)))
            return null;
        mob_living user = arguments[2] as mob_living;
        object force = arguments[4];
        if (!Dm.truthy(force))
        {
            source.balloon_alert(user, I18n.T("duplicate implant present!"));
            return COMPONENT_STOP_IMPLANTING;
        }
        return null;
    }

    // tg: code/modules/wiremod/shell/implant.dm:42
    public override object ui_host(mob user = null)
    {
        if (istype(loc, typeof(obj_item_implantcase)))
            return loc;
        return base.ui_host(user);
    }
}

// tg: code/modules/wiremod/shell/implant.dm:47
public partial class obj_item_circuit_component_implant_core : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/implant.dm:62
    public override async DmTask populate_ports()
    {
        message = add_input_port("Message", PORT_TYPE_STRING, trigger: null) as datum_port_input;
        send_message_signal = add_input_port("Send Message", PORT_TYPE_SIGNAL) as datum_port_input;
        show_charge_meter = add_input_port("Show Charge Meter", PORT_TYPE_BOOLEAN, trigger: nameof(update_charge_action)) as datum_port_input;

        user_port = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/implant.dm:70
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref charge_action);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/implant.dm:74
    [ShouldNotSleep]
    public virtual void update_charge_action()
    {
        if (Dm.truthy(show_charge_meter.value))
        {
            if (charge_action != null)
                return;
            charge_action = Dm.@new<datum_action_innate_implant_charge_action>(this);
            if (implant.imp_in != null)
                Waiting.NoSleep(charge_action.Grant(implant.imp_in));
            // DEVIATION(DV607): the circuit implant has no actions list (tg's actions_types = null), and tg's `actions += charge_action`
            // on it stores the action itself, which no list var here can hold: it is added as the list's one action instead.
            LAZYADD(ref implant.actions, charge_action);
        }
        else
        {
            if (charge_action == null)
                return;
            if (implant.imp_in != null)
                Waiting.NoSleep(charge_action.Remove(implant.imp_in));
            Dm.remove(ref implant.actions, charge_action);
            QDEL_NULL(ref charge_action);
        }
    }

    // tg: code/modules/wiremod/shell/implant.dm:91
    public override async DmTask register_shell(atom_movable shell = null)
    {
        implant = shell as obj_item_implant;

        show_charge_meter.set_value(TRUE);

        RegisterSignal(shell, COMSIG_IMPLANT_IMPLANTED, nameof(on_implanted));
        RegisterSignal(shell, COMSIG_IMPLANT_REMOVED, nameof(on_removed));
    }

    // tg: code/modules/wiremod/shell/implant.dm:99
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        implant = null;

        // tg clears implant before it reads implant.imp_in, so a shown charge meter runtimes here and the signals below stay
        // registered; kept as tg has it
        if (charge_action != null)
        {
            if (implant.imp_in != null)
                await charge_action.Remove(implant.imp_in);
            Dm.remove(ref implant.actions, charge_action);
            QDEL_NULL(ref charge_action);
        }

        UnregisterSignal(shell, Dm.list(
            COMSIG_IMPLANT_IMPLANTED,
            COMSIG_IMPLANT_REMOVED));
    }

    // tg: code/modules/wiremod/shell/implant.dm:113
    [SignalHandler]
    public virtual void on_implanted(datum source = null, mob_living owner = null)
    {
        update_charge_action();

        user_port.set_output(owner);

        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(owner, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(on_borg_charge));
        RegisterSignal(owner, COMSIG_LIVING_ELECTROCUTE_ACT, nameof(on_electrocute));
    }

    // tg: code/modules/wiremod/shell/implant.dm:124
    [SignalHandler]
    public virtual void on_removed(datum source = null, mob_living owner = null)
    {
        user_port.set_output(null);

        UnregisterSignal(owner, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_PROCESS_BORGCHARGER_OCCUPANT,
            COMSIG_LIVING_ELECTROCUTE_ACT));
    }

    // tg: code/modules/wiremod/shell/implant.dm:135
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!COMPONENT_TRIGGERED_BY(send_message_signal, port))
            return null;

        string sent_message = trim(message.value) as string;
        if (!Dm.truthy(sent_message))
            return null;

        if (implant.imp_in == null)
            return null;

        if (implant.imp_in.stat == DEAD)
            return null;

        to_chat(implant.imp_in, I18n.T("<i>You hear a strange, robotic voice in your head...</i> \"[]\"", span_robot(Dm.text("[]", html_encode(sent_message)))));
        return null;
    }

    // tg: code/modules/wiremod/shell/implant.dm:151
    [SignalHandler]
    public virtual void on_borg_charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null)
    {
        if (parent.cell == null)
            return;

        Waiting.NoSleep(charge_cell.Invoke(parent.cell, seconds_per_tick));
    }

    // tg: code/modules/wiremod/shell/implant.dm:159
    [SignalHandler]
    public virtual void on_electrocute(datum source = null, object shock_damage = null, object shock_source = null, object siemens_coefficient = null, object flags = null)
    {
        if (parent.cell == null)
            return;

        if (Dm.truthy(Dm.bitand(flags, SHOCK_ILLUSION)))
            return;

        Waiting.NoSleep(parent.cell.give(Dm.num(shock_damage) * 2));
        to_chat(source, span_notice(I18n.T("You absorb some of the shock into your []!", parent.name)));
    }

    // tg: code/modules/wiremod/shell/implant.dm:171
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob mob = null, DmList examine_text = null)
    {
        if (isobserver(mob))
        {
            atom source_atom = source as atom;
            Dm.append(examine_text, span_notice(I18n.T("[] [] <a href='byond://?src=[];open_implant=1'>\\a [] implanted in []</a>.", source_atom.p_They(), source_atom.p_have(), REF(this), parent, source_atom.p_them())));
        }
    }

    // tg: code/modules/wiremod/shell/implant.dm:177
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);

        if (!isobserver(usr))
            return null;

        if (Dm.truthy(href_list["open_implant"]))
            await parent.attack_ghost(usr as mob_dead_observer);
        return null;
    }
}

// tg: code/modules/wiremod/shell/implant.dm:186
public partial class datum_action_innate_implant_charge_action : datum_action_innate
{
    // tg: code/modules/wiremod/shell/implant.dm:194
    [DmParams("circuit_component")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_circuit_component_implant_core circuit_component = Dm.arg<obj_item_circuit_component_implant_core>(__extra, 0);
        await base.New(__extra);

        this.circuit_component = circuit_component;

        await build_all_button_icons();

        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/wiremod/shell/implant.dm:203
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext_x = 2;
        button.maptext_y = 0;
        return button;
    }

    // tg: code/modules/wiremod/shell/implant.dm:209
    public override object Destroy(float? force = null)
    {
        circuit_component.charge_action = null;
        circuit_component = null;

        STOP_PROCESSING(SSobj, this);

        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/implant.dm:217
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (!Dm.truthy(await base.Trigger(clicker, trigger_flags, __extra)))
            return null;
        obj_item_stock_parts_power_store cell = circuit_component.parent.cell;

        if (cell == null)
            to_chat(owner, span_boldwarning(I18n.T("[] has no power cell.", circuit_component.parent)));
        else
        {
            to_chat(owner, span_info(I18n.T("[]'s [] has <b>[]%</b> charge left.", circuit_component.parent, cell.name, cell.percent())));
            to_chat(owner, span_info(I18n.T("You can recharge it by using a cyborg recharging station.")));
        }
        return null;
    }

    // tg: code/modules/wiremod/shell/implant.dm:228
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
        return null;
    }

    // tg: code/modules/wiremod/shell/implant.dm:231
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        await base.update_button_status(current_button, force);
        obj_item_stock_parts_power_store cell = circuit_component.parent.cell;
        current_button.maptext = cell != null ? MAPTEXT(Dm.text("[]%", cell.percent())) : "";
    }
}
