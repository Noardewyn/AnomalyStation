// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/module.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/shell/module.dm:1
public partial class obj_item_mod_module_circuit : obj_item_mod_module
{
    // tg: code/modules/wiremod/shell/module.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, nameof(action_comp_registered));
        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED, nameof(action_comp_unregistered));

        shell = Waiting.NoSleep(AddComponent(typeof(datum_component_shell),
            Dm.list(Dm.@new<obj_item_circuit_component_mod_adapter_core>()),
            Dm.named("capacity", SHELL_CAPACITY_LARGE)
        )) as datum_component_shell;
        return dot;
    }

    // tg: code/modules/wiremod/shell/module.dm:28
    [SignalHandler]
    public virtual object override_power_usage(datum source = null, object amount = null)
    {
        object dot = null;
        if (Dm.truthy(Waiting.NoSleep(drain_power(amount))))
            dot = COMPONENT_OVERRIDE_POWER_USAGE;
        return dot;
    }

    // tg: code/modules/wiremod/shell/module.dm:33
    [SignalHandler]
    public virtual void action_comp_registered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        Dm.append(action_comps, action_comp);
    }

    // tg: code/modules/wiremod/shell/module.dm:37
    [SignalHandler]
    public virtual void action_comp_unregistered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        Dm.remove(action_comps, action_comp);
        foreach (object @ref in Dm.each(action_comp.granted_to))
            unpin_action(action_comp, Dm.locate_ref(@ref) as mob);
        QDEL_LIST_ASSOC_VAL(action_comp.granted_to);
    }

    // tg: code/modules/wiremod/shell/module.dm:44
    public override async DmTask on_install()
    {
        await base.on_install();
        if (shell?.attached_circuit == null)
            return;
        RegisterSignal(shell?.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE, nameof(override_power_usage));
    }

    // tg: code/modules/wiremod/shell/module.dm:50
    public override async DmTask on_uninstall(float? deleting = null)
    {
        deleting ??= FALSE;
        await base.on_uninstall(deleting);
        if (shell?.attached_circuit == null)
            return;
        foreach (obj_item_circuit_component_equipment_action action_comp in Dm.each<obj_item_circuit_component_equipment_action>(action_comps))
        {
            foreach (object @ref in Dm.each(action_comp.granted_to))
                unpin_action(action_comp, Dm.locate_ref(@ref) as mob);
        }
        UnregisterSignal(shell?.attached_circuit, COMSIG_CIRCUIT_PRE_POWER_USAGE);
    }

    // tg: code/modules/wiremod/shell/module.dm:60
    public override async DmTask<object> on_use(mob activator = null)
    {
        object dot = await base.on_use(activator);
        if (!Dm.truthy(dot))
            return dot;
        if (shell.attached_circuit == null)
            return dot;
        if (shell.attached_circuit != null)
            await shell.attached_circuit.interact(mod.wearer);
        return dot;
    }

    // tg: code/modules/wiremod/shell/module.dm:68
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        float unnamed_action_index = 1;
        foreach (obj_item_circuit_component_equipment_action action_comp in Dm.each<obj_item_circuit_component_equipment_action>(action_comps))
            dot[REF(action_comp)] = add_ui_configuration(action_comp.button_name.value is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.text("Unnamed Action []", unnamed_action_index++), "pin", Dm.num(Dm.truthy(action_comp.granted_to[REF(user)])));
        return dot;
    }

    // tg: code/modules/wiremod/shell/module.dm:74
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        await base.configure_edit(key, value);
        obj_item_circuit_component_equipment_action action_comp = Dm.locate_ref(key) is obj_item_circuit_component_equipment_action located && Dm.@in(located, action_comps) ? located : null; // locate(key) in action_comps
        if (!(action_comp is obj_item_circuit_component_equipment_action))
            return;
        if (Dm.truthy(text2num(value)))
            pin_action(action_comp, usr);
        else
            unpin_action(action_comp, usr);
    }

    // tg: code/modules/wiremod/shell/module.dm:84
    public virtual void pin_action(obj_item_circuit_component_equipment_action action_comp = null, mob user = null)
    {
        if (!(user is mob))
            return;
        if (Dm.truthy(action_comp.granted_to[REF(user)])) // Sanity check - don't pin an action for a mob that has already pinned it
            return;
        Waiting.NoSleep(mod.add_item_action(Dm.@new<datum_action_item_action_mod_pinnable_circuit>(mod, user, this, action_comp)));
    }

    // tg: code/modules/wiremod/shell/module.dm:91
    public virtual void unpin_action(obj_item_circuit_component_equipment_action action_comp = null, mob user = null)
    {
        datum_action_item_action_mod_pinnable_circuit action = action_comp.granted_to[REF(user)] as datum_action_item_action_mod_pinnable_circuit;
        if (!(action is datum_action_item_action_mod_pinnable_circuit))
            return;
        qdel(action);
    }
}

// tg: code/modules/wiremod/shell/module.dm:97
public partial class datum_action_item_action_mod_pinnable_circuit : datum_action_item_action_mod_pinnable
{
    // tg: code/modules/wiremod/shell/module.dm:106
    [DmParams("Target", "user", "linked_module", "action_comp")]
    public override async DmTask New(params object[] __extra)
    {
        mob user = Dm.arg<mob>(__extra, 1);
        obj_item_mod_module_circuit linked_module = Dm.arg<obj_item_mod_module_circuit>(__extra, 2);
        obj_item_circuit_component_equipment_action action_comp = Dm.arg<obj_item_circuit_component_equipment_action>(__extra, 3);
        await base.New(__extra);
        module = linked_module;
        action_comp.granted_to[REF(user)] = this;
        circuit_component = action_comp;
        name = (string)action_comp.button_name.value;
        button_icon_state = Dm.text("bci_[]", replacetextEx(LOWER_TEXT(action_comp.icon_options.value), " ", "_"));
    }

    // tg: code/modules/wiremod/shell/module.dm:115
    public override object Destroy(float? force = null)
    {
        Dm.remove(circuit_component.granted_to, REF(pinner));
        circuit_component = null;

        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/module.dm:121
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        object dot = await base.do_effect(trigger_flags);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_mod_control mod = module.mod;
        if (!(mod is obj_item_mod_control))
            return FALSE;
        if (!Dm.truthy(mod.active) || Dm.truthy(mod.activating))
        {
            if (mod.wearer != null)
                module.balloon_alert(mod.wearer, I18n.T("not active!"));
            return FALSE;
        }
        circuit_component.user.set_output(owner);
        circuit_component.signal.set_output(COMPONENT_SIGNAL);
        return dot;
    }

    /// If the guy whose UI we are pinned to got deleted
    // tg: code/modules/wiremod/shell/module.dm:136
    public override void pinner_deleted()
    {
        // tg indexes the module's list of action components with the component and subtracts the pinner's ref from what it
        // finds there (nothing: the list is not associative); kept as tg has it
        if (module != null)
        {
            object granted = module.action_comps[circuit_component];
            Dm.remove(ref granted, REF(pinner));
            module.action_comps[circuit_component] = granted;
        }
        base.pinner_deleted();
    }
}

// tg: code/modules/wiremod/shell/module.dm:140
public partial class obj_item_circuit_component_mod_adapter_core : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/module.dm:182
    public override void populate_options()
    {
        module_to_select = add_option_port("Module to Select", Dm.list()) as datum_port_input_option;
    }

    // tg: code/modules/wiremod/shell/module.dm:185
    public override async DmTask populate_ports()
    {
        // Input Signals
        toggle_deploy = add_input_port("Toggle Deployment", PORT_TYPE_SIGNAL) as datum_port_input;
        toggle_suit = add_input_port("Toggle Suit", PORT_TYPE_SIGNAL) as datum_port_input;
        select_module = add_input_port("Select Module", PORT_TYPE_SIGNAL) as datum_port_input;
        // States
        wearer = add_output_port("Wearer", PORT_TYPE_USER) as datum_port_output;
        deployed = add_output_port("Deployed", PORT_TYPE_BOOLEAN) as datum_port_output;
        activated = add_output_port("Activated", PORT_TYPE_BOOLEAN) as datum_port_output;
        selected_module = add_output_port("Selected Module", PORT_TYPE_STRING) as datum_port_output;
        deployed_parts = add_output_port("Deployed Parts", PORT_TYPE_LIST(PORT_TYPE_STRING)) as datum_port_output;
        // Output Signals
        on_module_selected = add_output_port("On Module Selected", PORT_TYPE_SIGNAL) as datum_port_output;
        on_deploy = add_output_port("On Deploy", PORT_TYPE_SIGNAL) as datum_port_output;
        on_toggle_finish = add_output_port("Finished Toggling", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/module.dm:202
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_item_mod_module)))
        {
            attached_module = shell as obj_item_mod_module;
            RegisterSignal(attached_module, COMSIG_MOVABLE_MOVED, nameof(on_move));
        }
    }

    // tg: code/modules/wiremod/shell/module.dm:208
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        if (attached_module != null)
        {
            UnregisterSignal(attached_module, COMSIG_MOVABLE_MOVED);
            attached_module = null;
        }
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/module.dm:214
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_module?.mod == null)
            return null;
        obj_item_mod_module module = null;
        foreach (object potential_module__it in Dm.each(attached_module.mod.modules))
        {
            obj_item_mod_module potential_module = (obj_item_mod_module)potential_module__it;
            if (Dm.eq(potential_module.name, module_to_select.value))
                module = potential_module;
        }
        if (COMPONENT_TRIGGERED_BY(toggle_suit, port))
            Waiting.Detach(attached_module.mod.toggle_activate(attached_module.mod.wearer));
        if (COMPONENT_TRIGGERED_BY(toggle_deploy, port))
            Waiting.Detach(attached_module.mod.quick_deploy(attached_module.mod.wearer));
        if (Dm.truthy(attached_module.mod.active) && module != null && COMPONENT_TRIGGERED_BY(select_module, port))
            Waiting.Detach(module.on_select());
        return null;
    }

    // tg: code/modules/wiremod/shell/module.dm:228
    [SignalHandler]
    public virtual void on_move(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null)
    {
        if (istype(source.loc, typeof(obj_item_mod_control)))
        {
            obj_item_mod_control mod = source.loc as obj_item_mod_control;
            RegisterSignal(mod, COMSIG_MOD_MODULE_SELECTED, nameof(on_module_select));
            RegisterSignal(mod, COMSIG_MOD_DEPLOYED, nameof(on_mod_part_toggled));
            RegisterSignal(mod, COMSIG_MOD_RETRACTED, nameof(on_mod_part_toggled));
            RegisterSignal(mod, COMSIG_MOD_TOGGLED, nameof(on_mod_toggled));
            RegisterSignal(mod, COMSIG_MOD_MODULE_ADDED, nameof(on_module_changed));
            RegisterSignal(mod, COMSIG_MOD_MODULE_REMOVED, nameof(on_module_changed));
            RegisterSignal(mod, COMSIG_ITEM_EQUIPPED, nameof(equip_check));
            wearer.set_output(mod.wearer);
            DmList modules_list = Dm.list();
            foreach (obj_item_mod_module module in Dm.each<obj_item_mod_module>(mod.modules))
            {
                if (module.module_type != MODULE_PASSIVE)
                    Dm.append(modules_list, module.name);
            }
            module_to_select.possible_options = modules_list;
            if (Dm.truthy(module_to_select.possible_options.len))
                module_to_select.set_value(module_to_select.possible_options[1]);
        }
        else if (istype(old_loc, typeof(obj_item_mod_control)))
        {
            UnregisterSignal(old_loc, Dm.list(COMSIG_MOD_MODULE_SELECTED, COMSIG_ITEM_EQUIPPED));
            UnregisterSignal(old_loc, COMSIG_MOD_DEPLOYED);
            UnregisterSignal(old_loc, COMSIG_MOD_RETRACTED);
            UnregisterSignal(old_loc, COMSIG_MOD_TOGGLED);
            UnregisterSignal(old_loc, COMSIG_MOD_MODULE_ADDED);
            UnregisterSignal(old_loc, COMSIG_MOD_MODULE_REMOVED);
            selected_module.set_output(null);
            wearer.set_output(null);
            deployed.set_output(FALSE);
            activated.set_output(FALSE);
        }
    }

    // tg: code/modules/wiremod/shell/module.dm:258
    [SignalHandler]
    public virtual void on_module_select(datum source = null, obj_item_mod_module module = null)
    {
        selected_module.set_output(module.name);
        on_module_selected.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/module.dm:263
    [SignalHandler]
    public virtual void on_module_changed()
    {
        DmList modules_list = Dm.list();
        foreach (obj_item_mod_module module in Dm.each<obj_item_mod_module>(attached_module.mod.modules))
        {
            if (module.module_type != MODULE_PASSIVE)
                Dm.append(modules_list, module.name);
        }
        module_to_select.possible_options = modules_list;
        if (Dm.truthy(module_to_select.possible_options.len))
            module_to_select.set_value(module_to_select.possible_options[1]);
    }

    // tg: code/modules/wiremod/shell/module.dm:273
    [SignalHandler]
    public virtual void on_mod_part_toggled()
    {
        DmList string_list = Dm.list();
        float is_deployed = TRUE;
        foreach (object part__it in Dm.each(attached_module.mod.get_parts()))
        {
            obj_item part = (obj_item)part__it;
            if (part.loc == attached_module.mod)
                is_deployed = FALSE;
            else
            {
                string part_name = "Undefined";
                if (istype(part, typeof(obj_item_clothing_head_mod)))
                    part_name = "Helmet";
                else if (istype(part, typeof(obj_item_clothing_suit_mod)))
                    part_name = "Chestplate";
                else if (istype(part, typeof(obj_item_clothing_gloves_mod)))
                    part_name = "Gloves";
                else if (istype(part, typeof(obj_item_clothing_shoes_mod)))
                    part_name = "Boots";
                else if (istype(part, typeof(obj_item_clothing_glasses_mod)))
                    part_name = "Glasses";
                else if (istype(part, typeof(obj_item_clothing_neck_mod)))
                    part_name = "Tie";
                Dm.append(string_list, part_name);
            }
        }
        deployed_parts.set_output(string_list);
        deployed.set_output(is_deployed);
        on_deploy.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/module.dm:298
    [SignalHandler]
    public virtual void on_mod_toggled()
    {
        activated.set_output(attached_module.mod.active);
        on_toggle_finish.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/module.dm:303
    [SignalHandler]
    public virtual void equip_check()
    {
        if (attached_module.mod?.wearer == null)
            return;
        wearer.set_output(attached_module.mod.wearer);
    }
}
