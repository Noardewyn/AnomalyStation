// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CI1 (D339, D341): the whole file, the shell component: a circuit put into a shell (an item, a structure, a machine), its
// unremovable components, its lock with the owner's ID, the multitool that opens the editor, the screwdriver that takes the
// circuit out, an anchored shell's power from the area's APC with the overheat, and the USB cable's connection to the circuit.
// The var block is generated (D111).

/// Makes an atom a shell that is able to take in an attached circuit.
// tg: code/datums/components/shell.dm:2
public partial class datum_component_shell : datum_component
{
    // tg: code/datums/components/shell.dm:30
    [DmParams("unremovable_circuit_components", "capacity", "shell_flags", "starting_circuit")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList unremovable_circuit_components = Dm.arg<DmList>(__extra, 0);
        object capacity = Dm.arg(__extra, 1);
        object shell_flags = Dm.arg(__extra, 2);
        obj_item_integrated_circuit starting_circuit = Dm.arg<obj_item_integrated_circuit>(__extra, 3);
        await base.Initialize(__extra);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        this.shell_flags = Dm.truthy(shell_flags) ? Dm.num(shell_flags) : this.shell_flags;
        this.capacity = Dm.truthy(capacity) ? Dm.num(capacity) : this.capacity;
        set_unremovable_circuit_components(unremovable_circuit_components);

        if (starting_circuit != null)
            await attach_circuit(starting_circuit);
        return null;
    }

    // tg: code/datums/components/shell.dm:42
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_GHOST, nameof(on_attack_ghost));
        if (!Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNMODIFIABLE)))
        {
            RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL), nameof(on_multitool_act));
            RegisterSignal(parent, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interaction));
        }
        if (!Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNREMOVABLE)))
        {
            RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER), nameof(on_screwdriver_act));
            RegisterSignal(parent, COMSIG_OBJ_DECONSTRUCT, nameof(on_object_deconstruct));
        }
        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_REQUIRE_ANCHOR)))
            RegisterSignal(parent, COMSIG_MOVABLE_SET_ANCHORED, nameof(on_set_anchored));
        RegisterSignal(parent, COMSIG_ATOM_USB_CABLE_TRY_ATTACH, nameof(on_atom_usb_cable_try_attach));
        RegisterSignal(parent, COMSIG_MOVABLE_CIRCUIT_LOADED, nameof(on_load));
    }

    // tg: code/datums/components/shell.dm:56
    public virtual void set_unremovable_circuit_components(DmList components = null)
    {
        if (unremovable_circuit_components != null)
            QDEL_LIST(unremovable_circuit_components);

        unremovable_circuit_components = Dm.list();

        foreach (object circuit_component in Dm.each(components))
            add_unremovable_circuit_component(circuit_component);
    }

    // tg: code/datums/components/shell.dm:65
    public virtual void add_unremovable_circuit_component(object component = null)
    {
        if (ispath(component))
            component = Dm.@new((Type)component);
        obj_item_circuit_component circuit_component = component as obj_item_circuit_component;
        circuit_component.removable = FALSE;
        circuit_component.set_circuit_size(0);
        RegisterSignal(circuit_component, COMSIG_CIRCUIT_COMPONENT_SAVE, nameof(save_component));
        Dm.append(unremovable_circuit_components, circuit_component);
    }

    // tg: code/datums/components/shell.dm:73
    [SignalHandler]
    public virtual void save_component(datum source = null, DmList objects = null)
    {
        Dm.append(objects, parent);
    }

    // tg: code/datums/components/shell.dm:77
    [SignalHandler]
    public virtual void on_load(datum source = null, obj_item_integrated_circuit circuit = null, DmList components = null)
    {
        DmList components_in_list = Dm.list();
        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(components))
            Dm.append(components_in_list, component.type);

        foreach (obj_item_circuit_component component in Dm.each_cast<obj_item_circuit_component>(unremovable_circuit_components))
        {
            if (Dm.@in(component.type, components_in_list))
                continue;
            Type new_type = component.type;
            Dm.append(components, Dm.@new(new_type));
        }
        set_unremovable_circuit_components(components);
        Waiting.NoSleep(attach_circuit(circuit));
    }

    // tg: code/datums/components/shell.dm:92
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ITEM_INTERACTION,
            COMSIG_ATOM_TOOL_ACT(TOOL_SCREWDRIVER),
            COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL),
            COMSIG_OBJ_DECONSTRUCT,
            COMSIG_MOVABLE_SET_ANCHORED,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_ATTACK_GHOST,
            COMSIG_ATOM_USB_CABLE_TRY_ATTACH,
            COMSIG_MOVABLE_CIRCUIT_LOADED));

        QDEL_NULL(ref attached_circuit);
    }

    // tg: code/datums/components/shell.dm:107
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(unremovable_circuit_components);
        return base.Destroy(force);
    }

    // tg: code/datums/components/shell.dm:111
    [SignalHandler]
    public virtual void on_object_deconstruct()
    {
        if (attached_circuit == null)
            return;
        if (Dm.truthy(attached_circuit.admin_only))
            return;
        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNREMOVABLE)))
            return;
        Waiting.NoSleep(remove_circuit());
    }

    // tg: code/datums/components/shell.dm:121
    [SignalHandler]
    public virtual void on_attack_ghost(datum source = null, mob_dead_observer ghost = null)
    {
        if (!Dm.truthy(is_authorized(ghost)))
            return;

        if (attached_circuit != null)
            Waiting.Detach(attached_circuit.ui_interact(ghost));
    }

    // tg: code/datums/components/shell.dm:129
    [SignalHandler]
    public virtual void on_examine(atom_movable source = null, mob user = null, DmList examine_text = null)
    {
        if (!Dm.truthy(is_authorized(user)))
            return;

        if (attached_circuit == null)
        {
            Dm.append(examine_text, span_notice(I18n.T("There is no integrated circuit attached.")));
            return;
        }

        Dm.append(examine_text, span_notice(I18n.T("There is an integrated circuit attached. Use a multitool to access the wiring. Use a screwdriver to remove it from [].", source)));
        Dm.append(examine_text, span_notice(Dm.truthy(locked) ? I18n.T("The cover panel to the integrated circuit is locked.") : I18n.T("The cover panel to the integrated circuit is unlocked.")));
        obj_item_stock_parts_power_store cell = attached_circuit.cell;
        Dm.append(examine_text, span_notice(I18n.T("The charge meter reads []%.", cell != null ? round(Dm.num(cell.percent()), 1) : 0)));

        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_USB_PORT)))
            Dm.append(examine_text, span_notice(I18n.T("There is a <b>USB port</b> on the front.")));

        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_REQUIRE_ANCHOR)))
        {
            Dm.append(examine_text, span_notice(I18n.T("The shell does not require a battery to function and will draw from the area's APC whenever possible.")));
            if (!Dm.truthy(source.anchored))
                Dm.append(examine_text, span_danger(I18n.T("<b>The integrated circuit is non-functional whilst the shell is unanchored.</b>")));
        }
    }

    /**
     * Called when the shell is anchored.
     *
     * Only applies if the shell has SHELL_FLAG_REQUIRE_ANCHOR.
     * Disables the integrated circuit if unanchored, otherwise enable the circuit.
     */
    // tg: code/datums/components/shell.dm:158
    [SignalHandler]
    public virtual void on_set_anchored(atom_movable source = null, object previous_value = null)
    {
        attached_circuit?.set_on(source.anchored);
    }

    /**
     * Called when interacting with the parent using an item. This is the method to add the circuitboard to the component.
     */
    // tg: code/datums/components/shell.dm:165
    [SignalHandler]
    public virtual object on_item_interaction(atom source = null, mob_living user = null, obj_item item = null, DmList modifiers = null)
    {
        if (!Dm.truthy(is_authorized(user)))
            return null;

        if (istype(item, typeof(obj_item_stock_parts_power_store_cell)))
        {
            source.balloon_alert(user, I18n.T("can't put cell in directly!"));
            return null;
        }

        if (istype(item, typeof(obj_item_inducer)))
        {
            obj_item_inducer inducer = item as obj_item_inducer;
            Waiting.Detach(inducer.interact_with_atom((atom)attached_circuit ?? parent as atom, user, Dm.list()));
            return ITEM_INTERACT_SUCCESS;
        }

        if (attached_circuit != null)
        {
            if (attached_circuit.owner_id != null && item == attached_circuit.owner_id.resolve())
            {
                set_locked(Dm.num(!Dm.truthy(locked)));
                source.balloon_alert(user, Dm.truthy(locked) ? I18n.T("locked []", source) : I18n.T("unlocked []", source));
                return ITEM_INTERACT_SUCCESS;
            }

            if (attached_circuit.owner_id == null && isidcard(item))
            {
                source.balloon_alert(user, I18n.T("owner id set for []", item));
                attached_circuit.owner_id = WEAKREF(item) as datum_weakref;
                return ITEM_INTERACT_SUCCESS;
            }

            if (istype(item, typeof(obj_item_circuit_component)))
            {
                if (Dm.truthy(Waiting.NoSleep(attached_circuit.add_component_manually(item as obj_item_circuit_component, user))))
                    return ITEM_INTERACT_SUCCESS;
                return ITEM_INTERACT_BLOCKING;
            }
        }

        if (!istype(item, typeof(obj_item_integrated_circuit)))
            return null;
        obj_item_integrated_circuit logic_board = item as obj_item_integrated_circuit;

        if (logic_board.shell != null) // I'll be surprised if this ever happens
            return ITEM_INTERACT_BLOCKING;

        if (attached_circuit != null)
        {
            source.balloon_alert(user, I18n.T("there is already a circuitboard inside!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (logic_board.current_size > capacity)
        {
            source.balloon_alert(user, I18n.T("this is too large to fit into []!", parent));
            return ITEM_INTERACT_BLOCKING;
        }

        logic_board.inserter_mind = WEAKREF(user.mind) as datum_weakref;
        Waiting.NoSleep(attach_circuit(logic_board, user));
        return ITEM_INTERACT_SUCCESS;
    }

    /// Sets whether the shell is locked or not
    // tg: code/datums/components/shell.dm:215
    public virtual void set_locked(object new_value = null)
    {
        locked = Dm.num(new_value);
        attached_circuit?.set_locked(new_value);
    }

    // tg: code/datums/components/shell.dm:220
    [SignalHandler]
    public virtual object on_multitool_act(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(is_authorized(user)))
            return null;

        if (attached_circuit == null)
            return null;

        if (Dm.truthy(locked))
        {
            if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_ALLOW_FAILURE_ACTION)))
                return null;
            source.balloon_alert(user, I18n.T("it's locked!"));
            return ITEM_INTERACT_BLOCKING;
        }

        Waiting.NoSleep(attached_circuit.interact(user));
        return ITEM_INTERACT_BLOCKING;
    }

    /**
     * Called when a screwdriver is used on the parent. Removes the circuitboard from the component.
     */
    // tg: code/datums/components/shell.dm:240
    [SignalHandler]
    public virtual object on_screwdriver_act(atom source = null, mob user = null, obj_item tool = null)
    {
        if (!Dm.truthy(is_authorized(user)))
            return null;

        if (attached_circuit == null)
            return null;

        if (Dm.truthy(locked))
        {
            if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_ALLOW_FAILURE_ACTION)))
                return null;
            source.balloon_alert(user, I18n.T("it's locked!"));
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(parent as atom);
        source.balloon_alert(user, I18n.T("you unscrew [] from [].", attached_circuit, parent));
        Waiting.NoSleep(remove_circuit());
        return ITEM_INTERACT_BLOCKING;
    }

    /**
     * Checks for when the circuitboard moves. If it moves, removes it from the component.
     */
    // tg: code/datums/components/shell.dm:262
    [SignalHandler]
    public virtual void on_circuit_moved(obj_item_integrated_circuit circuit = null, atom old_loc = null)
    {
        if (circuit.loc != parent)
            Waiting.NoSleep(remove_circuit());
    }

    /**
     * Checks for when the circuitboard deletes so that it can be unassigned.
     */
    // tg: code/datums/components/shell.dm:270
    [SignalHandler]
    public virtual void on_circuit_delete(datum source = null)
    {
        Waiting.NoSleep(remove_circuit());
    }

    // tg: code/datums/components/shell.dm:274
    [SignalHandler]
    public virtual object on_circuit_add_component_manually(atom source = null, obj_item_circuit_component added_comp = null, mob_living user = null)
    {
        if (Dm.truthy(locked))
        {
            source.balloon_alert(user, I18n.T("it's locked!"));
            return COMPONENT_CANCEL_ADD_COMPONENT;
        }

        if (attached_circuit.current_size + added_comp.circuit_size > capacity)
        {
            source.balloon_alert(user, I18n.T("it won't fit!"));
            return COMPONENT_CANCEL_ADD_COMPONENT;
        }
        return null;
    }

    // tg: code/datums/components/shell.dm:284
    [SignalHandler]
    public virtual object override_power_usage(datum source = null, object power_to_use = null)
    {
        if (power_used_cooldown <= Dm.num(world.time)) // COOLDOWN_FINISHED(src, power_used_cooldown), inline (rules 11.1 (a))
        {
            power_used_cooldown = Dm.num(world.time) + 1 * MINUTES; // COOLDOWN_START(src, power_used_cooldown, 1 MINUTES), inline (rules 11.1 (a))
            power_used_in_minute = 0;
        }

        area location = get_area(parent as atom) as area;
        if (!Dm.truthy(location.powered(AREA_USAGE_EQUIP)))
            return null;

        if (power_used_in_minute > max_power_use_in_minute)
        {
            Waiting.Detach(explosion(parent as atom, light_impact_range: 1, explosion_cause: attached_circuit));
            if (attached_circuit != null)
                Waiting.NoSleep(remove_circuit());
            return null;
        }
        if (location.apc?.terminal != null)
            Waiting.NoSleep(location.apc.terminal.use_energy(power_to_use, channel: AREA_USAGE_EQUIP));
        power_used_in_minute += Dm.num(power_to_use);

        return COMPONENT_OVERRIDE_POWER_USAGE;
    }

    /**
     * Attaches a circuit to the parent. Doesn't do any checks to see for any existing circuits so that should be done beforehand.
     */
    // tg: code/datums/components/shell.dm:307
    public virtual async DmTask attach_circuit(obj_item_integrated_circuit circuitboard = null, mob_living user = null)
    {
        atom_movable parent_atom = parent as atom_movable;
        if (user != null && !Dm.truthy(await user.transferItemToLoc(circuitboard, parent_atom)))
            return;
        locked = FALSE;
        attached_circuit = circuitboard;
        SEND_SIGNAL(COMSIG_SHELL_CIRCUIT_ATTACHED, Dm.list(this));
        if (!Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNREMOVABLE)) && !Dm.truthy(circuitboard.admin_only))
            RegisterSignal(circuitboard, COMSIG_MOVABLE_MOVED, nameof(on_circuit_moved));
        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_REQUIRE_ANCHOR)))
            RegisterSignal(circuitboard, COMSIG_CIRCUIT_PRE_POWER_USAGE, nameof(override_power_usage));
        RegisterSignal(circuitboard, COMSIG_QDELETING, nameof(on_circuit_delete));
        foreach (obj_item_circuit_component to_add in Dm.each_cast<obj_item_circuit_component>(unremovable_circuit_components))
        {
            await to_add.forceMove(attached_circuit);
            await attached_circuit.add_component(to_add);
        }
        RegisterSignal(circuitboard, COMSIG_CIRCUIT_ADD_COMPONENT_MANUALLY, nameof(on_circuit_add_component_manually));
        if (attached_circuit.display_name != "")
            parent_atom.name = Dm.text("[] ([])", Dm.initial(parent_atom, (atom a) => a.name), strip_html(attached_circuit.display_name));
        attached_circuit.set_locked(FALSE);

        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNREMOVABLE)) || Dm.truthy(circuitboard.admin_only))
            await circuitboard.moveToNullspace();
        else if (circuitboard.loc != parent_atom)
            await circuitboard.forceMove(parent_atom);
        await attached_circuit.set_shell(parent_atom);

        // call after set_shell() sets on to true
        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_REQUIRE_ANCHOR)))
            attached_circuit.set_on(parent_atom.anchored);
    }

    /**
     * Removes the circuit from the component. Doesn't do any checks to see for an existing circuit so that should be done beforehand.
     */
    // tg: code/datums/components/shell.dm:340
    public virtual async DmTask remove_circuit()
    {
        // remove_current_shell() also turns off the circuit
        attached_circuit.remove_current_shell();
        UnregisterSignal(attached_circuit, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_QDELETING,
            COMSIG_CIRCUIT_ADD_COMPONENT_MANUALLY,
            COMSIG_CIRCUIT_PRE_POWER_USAGE));
        if (attached_circuit.loc == parent || (!QDELETED(attached_circuit) && attached_circuit.loc == null))
        {
            atom parent_atom = parent as atom;
            atom drop_location = parent_atom.drop_location() as atom;
            if (drop_location != null)
                await attached_circuit.forceMove(drop_location);
            else
                await attached_circuit.moveToNullspace();
        }

        foreach (obj_item_circuit_component to_remove in Dm.each_cast<obj_item_circuit_component>(unremovable_circuit_components))
        {
            await attached_circuit.remove_component(to_remove);
            await to_remove.moveToNullspace();
        }
        attached_circuit.set_locked(FALSE);
        SEND_SIGNAL(COMSIG_SHELL_CIRCUIT_REMOVED, Dm.list(this));
        attached_circuit = null;
    }

    // tg: code/datums/components/shell.dm:364
    [SignalHandler]
    public virtual object on_atom_usb_cable_try_attach(atom source = null, obj_item_usb_cable usb_cable = null, mob user = null)
    {
        if (!Dm.truthy(is_authorized(user)))
            return null;

        if (!Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_USB_PORT)))
        {
            source.balloon_alert(user, I18n.T("this shell has no usb ports"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        if (attached_circuit == null)
        {
            source.balloon_alert(user, I18n.T("no circuit inside"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        if (Dm.truthy(attached_circuit.locked))
        {
            source.balloon_alert(user, I18n.T("circuit is locked!"));
            return COMSIG_CANCEL_USB_CABLE_ATTACK;
        }

        usb_cable.attached_circuit = attached_circuit;
        return COMSIG_USB_CABLE_CONNECTED_TO_CIRCUIT;
    }

    /**
     * Determines if a user is authorized to see the existence of this shell. Returns false if they are not
     *
     * Arguments:
     * * user - The user to check if they are authorized
     */
    // tg: code/datums/components/shell.dm:390
    public virtual object is_authorized(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNREMOVABLE)) && Dm.truthy(Dm.bitand(shell_flags, SHELL_FLAG_CIRCUIT_UNMODIFIABLE)))
            return FALSE;

        if (Dm.truthy(attached_circuit?.admin_only))
        {
            if (Dm.truthy(check_rights_for(user.client, R_VAREDIT)))
                return TRUE;
            return FALSE;
        }

        return TRUE;
    }
}
