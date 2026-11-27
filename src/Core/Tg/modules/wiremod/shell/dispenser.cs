// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/dispenser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/**
 * # Dispenser
 *
 * Immobile (but not dense) shell that can receive and dispense items.
 */
// tg: code/modules/wiremod/shell/dispenser.dm:6
public partial class obj_structure_dispenser_bot : obj_structure
{
    // tg: code/modules/wiremod/shell/dispenser.dm:21
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        foreach (object stored_item__it in Dm.each(stored_items))
            remove_item((obj_item)stored_item__it);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:25
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(stored_items);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:30
    public virtual void add_item(mob user = null, obj_item to_add = null)
    {
        balloon_alert(user, I18n.T("inserted item"));
        Dm.append(stored_items, to_add);
        Waiting.NoSleep(to_add.forceMove(this));
        RegisterSignal(to_add, COMSIG_MOVABLE_MOVED, nameof(handle_stored_item_moved));
        RegisterSignal(to_add, COMSIG_QDELETING, nameof(handle_stored_item_deleted));
        SEND_SIGNAL(COMSIG_DISPENSERBOT_ADD_ITEM, Dm.list(this, to_add));
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:38
    [SignalHandler]
    public virtual void handle_stored_item_moved(obj_item moving_item = null, atom location = null)
    {
        // tg names the parameter location, but COMSIG_MOVABLE_MOVED passes the old loc there, which for a stored item is the
        // dispenser, so an item moved out by other means stays in the list; kept as tg has it
        if (location != this)
            remove_item(moving_item);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:43
    [SignalHandler]
    public virtual void handle_stored_item_deleted(obj_item deleting_item = null)
    {
        remove_item(deleting_item);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:47
    public virtual void remove_item(obj_item to_remove = null)
    {
        UnregisterSignal(to_remove, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_QDELETING
        ));
        Waiting.NoSleep(to_remove.forceMove(drop_location() as atom));
        Dm.remove(stored_items, to_remove);
        SEND_SIGNAL(COMSIG_DISPENSERBOT_REMOVE_ITEM, Dm.list(this, to_remove));
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:58
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_dispenser_bot>()
        ), SHELL_CAPACITY_LARGE));
        return dot;
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:64
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (tool.w_class > max_weight && !istype(tool, typeof(obj_item_storage_bag)))
        {
            balloon_alert(user, I18n.T("item too big!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (length(stored_items) >= capacity)
        {
            balloon_alert(user, I18n.T("at maximum capacity!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!istype(tool, typeof(obj_item_storage_bag)))
        {
            add_item(user, tool);
            return ITEM_INTERACT_SUCCESS;
        }

        foreach (obj_item bag_item in Dm.each<obj_item>(tool.contents))
        {
            if (length(stored_items) >= capacity)
                break;
            if (bag_item.w_class > max_weight || istype(bag_item, typeof(obj_item_storage_bag)))
                continue;
            add_item(user, bag_item);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:87
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(locked))
            return null;
        await set_anchored(Dm.not(anchored));
        tool.play_tool_sound(this);
        balloon_alert(user, Dm.text("[]", Dm.truthy(anchored) ? I18n.T("secured") : I18n.T("unsecured")));
        return TRUE;
    }
}

// tg: code/modules/wiremod/shell/dispenser.dm:95
public partial class obj_item_circuit_component_dispenser_bot : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/dispenser.dm:118
    public override async DmTask populate_ports()
    {
        item_list = add_output_port("Items", PORT_TYPE_LIST(PORT_TYPE_ATOM)) as datum_port_output;

        item = add_output_port("Item", PORT_TYPE_ATOM) as datum_port_output;
        on_item_added = add_output_port("On Item Added", PORT_TYPE_SIGNAL) as datum_port_output;
        on_item_removed = add_output_port("On Item Removed", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:125
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        RegisterSignal(shell, COMSIG_DISPENSERBOT_ADD_ITEM, nameof(on_shell_add_item));
        RegisterSignal(shell, COMSIG_DISPENSERBOT_REMOVE_ITEM, nameof(on_shell_remove_item));
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:130
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(
            COMSIG_DISPENSERBOT_ADD_ITEM,
            COMSIG_DISPENSERBOT_REMOVE_ITEM
        ));
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:137
    [SignalHandler]
    public virtual void on_shell_add_item(obj_structure_dispenser_bot source = null, obj_item added_item = null)
    {
        item.set_output(added_item);
        item_list.set_output(source.stored_items);
        on_item_added.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:143
    [SignalHandler]
    public virtual void on_shell_remove_item(obj_structure_dispenser_bot source = null, obj_item added_item = null)
    {
        item.set_output(added_item);
        item_list.set_output(source.stored_items);
        // tg fires the added signal here, not on_item_removed; kept as tg has it
        on_item_added.set_output(COMPONENT_SIGNAL);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:149
    [SignalHandler]
    public virtual void remove_vendor_component(obj_item_circuit_component_vendor_component vendor_component = null)
    {
        UnregisterSignal(vendor_component, Dm.list(
            COMSIG_QDELETING,
            COMSIG_CIRCUIT_COMPONENT_REMOVED
        ));
        if (!QDELING(vendor_component))
            qdel(vendor_component);
        Dm.remove(vendor_components, vendor_component);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:159
    public override async DmTask ui_perform_action(mob user = null, object action = null)
    {
        object sw__1 = action;
        if (Dm.eq(sw__1, "add_vend_component"))
        {
            if (length(vendor_components) >= max_vendor_components)
            {
                balloon_alert(user, I18n.T("you have hit vendor component limit!"));
                return;
            }
            obj_item_circuit_component_vendor_component vendor_component = Dm.@new<obj_item_circuit_component_vendor_component>(parent);
            await parent.add_component(vendor_component, user as mob_living);
            Dm.append(vendor_components, vendor_component);
            RegisterSignals(vendor_component, Dm.list(
                COMSIG_QDELETING,
                COMSIG_CIRCUIT_COMPONENT_REMOVED
            ), nameof(remove_vendor_component));
        }
    }
}

// tg: code/modules/wiremod/shell/dispenser.dm:173
public partial class obj_item_circuit_component_vendor_component : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/dispenser.dm:188
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        if (istype(shell, typeof(obj_structure_dispenser_bot)))
            attached_bot = shell as obj_structure_dispenser_bot;
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:193
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        attached_bot = null;
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:197
    public override async DmTask populate_ports()
    {
        item_to_vend = add_input_port("Item", PORT_TYPE_ATOM, trigger: null) as datum_port_input;
        vend_item = add_input_port("Vend Item", PORT_TYPE_SIGNAL, trigger: nameof(vend_item__proc)) as datum_port_input;
    }

    // tg: code/modules/wiremod/shell/dispenser.dm:201
    [ShouldNotSleep]
    public virtual void vend_item__proc(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_bot == null)
            return;

        obj_item vending_item = Dm.@in(item_to_vend.value, attached_bot.stored_items) && item_to_vend.value is obj_item found ? found : null; // locate(item_to_vend.value) in attached_bot.stored_items

        if (vending_item == null)
            return;

        attached_bot.remove_item(vending_item);
    }
}
