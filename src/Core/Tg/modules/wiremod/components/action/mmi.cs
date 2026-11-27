// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/components/action/mmi.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/wiremod/components/action/mmi.dm:3
public partial class datum_action_innate_mmi_comp_disconnect : datum_action_innate
{
    // tg: code/modules/wiremod/components/action/mmi.dm:9
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_circuit_component_mmi mmi_comp = target as obj_item_circuit_component_mmi;
        mmi_comp.remove_occupant();
        return dot;
    }
}

/**
 * # Man-Machine Interface Component
 *
 * Allows an MMI to be inserted into a shell, allowing it to be linked up. Requires a shell.
 */
// tg: code/modules/wiremod/components/action/mmi.dm:21
public partial class obj_item_circuit_component_mmi : obj_item_circuit_component
{
    // tg: code/modules/wiremod/components/action/mmi.dm:1
    private const float MMI_MESSAGE_COOLDOWN = 0.1f * SECONDS;

    // tg: code/modules/wiremod/components/action/mmi.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        disconnect_action = Dm.@new<datum_action_innate_mmi_comp_disconnect>(this);
        return dot;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:83
    public override async DmTask populate_ports()
    {
        message = add_input_port("Message", PORT_TYPE_STRING) as datum_port_input;
        send = add_input_port("Send Message", PORT_TYPE_SIGNAL) as datum_port_input;
        eject = add_input_port("Eject", PORT_TYPE_SIGNAL) as datum_port_input;

        north = add_output_port("North", PORT_TYPE_SIGNAL) as datum_port_output;
        east = add_output_port("East", PORT_TYPE_SIGNAL) as datum_port_output;
        south = add_output_port("South", PORT_TYPE_SIGNAL) as datum_port_output;
        west = add_output_port("West", PORT_TYPE_SIGNAL) as datum_port_output;

        attack = add_output_port("Attack", PORT_TYPE_SIGNAL) as datum_port_output;
        secondary_attack = add_output_port("Secondary Attack", PORT_TYPE_SIGNAL) as datum_port_output;
        clicked_atom = add_output_port("Target Entity", PORT_TYPE_ATOM) as datum_port_output;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:97
    public override object Destroy(float? force = null)
    {
        remove_occupant_item();
        QDEL_NULL(ref disconnect_action);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:102
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (brain == null && boris == null)
            return null;

        if (COMPONENT_TRIGGERED_BY(eject, port))
            remove_occupant_item();
        if (COMPONENT_TRIGGERED_BY(send, port))
        {
            if (!Dm.truthy(message.value) || !(message_cooldown <= world.time)) // COOLDOWN_FINISHED
                return null;

            string msg_str = copytext(html_encode(message.value), 1, max_length);

            if (occupant == null)
                return null;

            if (isAI(occupant))
                to_chat(occupant, Dm.text("[][]", span_boldnotice(I18n.T("Message from remote circuit: ")), span_notice(msg_str)));
            else
                to_chat(occupant, Dm.text("[][]", span_bold(I18n.T("You hear a message: ")), msg_str));
            message_cooldown = world.time + MMI_MESSAGE_COOLDOWN; // COOLDOWN_START
        }
        return null;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:123
    public override async DmTask register_shell(atom_movable shell = null)
    {
        await base.register_shell(shell);
        RegisterSignal(shell, COMSIG_ATOM_ITEM_INTERACTION, nameof(handle_interaction));
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:127
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        UnregisterSignal(shell, Dm.list(COMSIG_ATOM_ITEM_INTERACTION));
        remove_occupant_item();
        await base.unregister_shell(shell);
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:132
    [SignalHandler]
    public virtual object handle_interaction(atom_movable shell = null, mob_living user = null, obj_item item = null)
    {
        obj_item_brain_processor target_mmi = null;
        mob_living new_occupant = null;
        obj_item_borg_upgrade_ai target_boris = null;
        if (istype(item, typeof(obj_item_brain_processor)))
        {
            target_mmi = item as obj_item_brain_processor;
            if (target_mmi.brainmob == null)
            {
                shell.balloon_alert(user, I18n.T("no consciousness detected!"));
                return ITEM_INTERACT_FAILURE;
            }
            new_occupant = target_mmi.brainmob;
        }
        else if (istype(item, typeof(obj_item_borg_upgrade_ai)))
            target_boris = item as obj_item_borg_upgrade_ai;
        else
            return null;
        datum_component_shell shell_comp = shell.GetComponent(typeof(datum_component_shell)) as datum_component_shell;
        if (Dm.truthy(shell_comp.locked))
        {
            shell.balloon_alert(user, I18n.T("locked!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (brain != null || boris != null)
        {
            shell.balloon_alert(user, I18n.T("already has brain!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (!Dm.truthy(Waiting.NoSleep(user.transferItemToLoc(item, this))))
            return ITEM_INTERACT_FAILURE;
        if (target_mmi != null)
        {
            brain = target_mmi;
            set_occupant(new_occupant);
        }
        if (target_boris != null)
        {
            boris = target_boris;
            register_boris_circuit(shell);
        }
        RegisterSignal(item, COMSIG_QDELETING, nameof(remove_occupant_item));
        RegisterSignal(item, COMSIG_MOVABLE_MOVED, nameof(occupant_item_moved));
        return null;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:167  var/static/list/connections
    private static ref DmList register_boris_circuit__connections => ref TgState.Current.obj_item_circuit_component_mmi__register_boris_circuit__connections;

    // tg: code/modules/wiremod/components/action/mmi.dm:166
    public virtual void register_boris_circuit(atom_movable shell = null)
    {
        boris_circuit_container_connections = WEAKREF(Waiting.NoSleep(AddComponent(typeof(datum_component_connect_containers), this, register_boris_circuit__connections)) as datum) as datum_weakref;
        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(shell), shell)))
        {
            Waiting.NoSleep(location.AddComponentFrom(REF(this), typeof(datum_component_boris_circuit_container)));
            Waiting.NoSleep(AddComponentFrom(REF(location), typeof(datum_component_shuttle_move_deferred_checks), nameof(post_movement_checks)));
        }
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:173
    public virtual void unregister_boris_circuit(atom_movable shell = null)
    {
        QDEL_NULL(ref boris_circuit_container_connections);
        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(shell), shell)))
        {
            location.RemoveComponentSource(REF(this), typeof(datum_component_boris_circuit_container));
            RemoveComponentSource(REF(location), typeof(datum_component_shuttle_move_deferred_checks));
        }
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:179
    [SignalHandler]
    public virtual void boris_shell_or_container_moved(atom_movable shell_or_container = null, atom old_loc = null)
    {
        if (isturf(old_loc) && isturf(shell_or_container.loc))
            return;
        object old_locs = Dm.list();
        if (ismovable(old_loc))
            old_locs = Dm.add(get_nested_locs(old_loc as atom_movable), old_loc);
        object new_locs = get_nested_locs(shell_or_container);
        foreach (atom_movable loc_exited in Dm.each_cast<atom_movable>(Dm.sub(old_locs, new_locs)))
        {
            loc_exited.RemoveComponentSource(REF(this), typeof(datum_component_boris_circuit_container));
            RemoveComponentSource(REF(loc_exited), typeof(datum_component_shuttle_move_deferred_checks));
        }
        foreach (atom_movable loc_entered in Dm.each_cast<atom_movable>(Dm.sub(new_locs, old_locs)))
        {
            Waiting.NoSleep(loc_entered.AddComponentFrom(REF(this), typeof(datum_component_boris_circuit_container)));
            Waiting.NoSleep(AddComponentFrom(REF(loc_entered), typeof(datum_component_shuttle_move_deferred_checks), nameof(post_movement_checks)));
        }
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:194
    [SignalHandler]
    public virtual void occupant_item_moved(atom_movable occupant_item = null)
    {
        if (occupant_item.loc != this)
            remove_occupant_item(occupant_item as obj_item);
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:200
    [SignalHandler]
    public virtual void remove_occupant_item(obj_item removing = null)
    {
        if (removing == null)
            removing = brain;
        if (removing == null)
            removing = boris;
        if (removing == null)
            return;
        if (istype(removing, typeof(obj_item_brain_processor)))
            brain = null;
        if (istype(removing, typeof(obj_item_borg_upgrade_ai)))
        {
            boris = null;
            unregister_boris_circuit(parent.shell);
        }
        remove_occupant();

        UnregisterSignal(removing, Dm.list(
            COMSIG_QDELETING,
            COMSIG_MOVABLE_MOVED
        ));
        if (removing.loc == this)
            Waiting.NoSleep(removing.forceMove(drop_location() as atom));
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:221
    public virtual async DmTask confirm_ai_connect(mob_living_silicon_ai user = null, atom_movable shell = null)
    {
        object confirmation = await tgui_alert(user, I18n.T("Connect to []?", shell), buttons: Dm.list("Yes", "No"));
        if (!Dm.eq(confirmation, "Yes"))
            return;
        if (QDELETED(this) || QDELETED(user) || QDELETED(shell) || parent?.shell == null || !Dm.truthy(await user.can_interact_with(shell)) || boris == null)
            return;
        do_ai_connect(user, shell);
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:229
    public virtual void do_ai_connect(mob_living_silicon_ai user = null, atom_movable shell = null)
    {
        if (occupant != null)
        {
            if (occupant != user)
                shell.balloon_alert(user, I18n.T("occupied!"));
            return;
        }
        set_occupant(user);
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:248  var/static/list/connections
    private static ref DmList set_occupant__connections => ref TgState.Current.obj_item_circuit_component_mmi__set_occupant__connections;

    // tg: code/modules/wiremod/components/action/mmi.dm:236
    public virtual void set_occupant(mob_living new_occupant = null)
    {
        new_occupant.remote_control = this;
        RegisterSignal(new_occupant, COMSIG_MOB_CLICKON, nameof(handle_occupant_attack));
        RegisterSignal(new_occupant, COMSIG_QDELETING, nameof(remove_occupant));
        occupant = new_occupant;
        if (!isAI(new_occupant))
            return;
        ADD_TRAIT(new_occupant, TRAIT_CONNECTED_TO_CIRCUIT, REF(this));
        mob_living_silicon ai = new_occupant as mob_living_silicon;
        Waiting.NoSleep(ai.reset_perspective(this));
        // Perspective gets reset whenever multicam is ended, which happens whenever an AI gets incapacitated or carded.
        // This could change in the future, so we also register other signal handlers.
        RegisterSignals(ai, Dm.list(COMSIG_MOB_RESET_PERSPECTIVE, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED)), nameof(remove_occupant));
        RegisterSignal(ai, COMSIG_SILICON_AI_SET_CONTROL_DISABLED, nameof(on_control_toggled));
        connected_ai_container_connections = WEAKREF(Waiting.NoSleep(AddComponent(typeof(datum_component_connect_containers), ai, set_occupant__connections)) as datum) as datum_weakref;
        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(ai), ai)))
            Waiting.NoSleep(AddComponentFrom(REF(location), typeof(datum_component_shuttle_move_deferred_checks), nameof(post_movement_checks)));
        Waiting.NoSleep(disconnect_action.Grant(ai));
        to_chat(ai, span_notice(I18n.T("Established connection with remote circuit.")));
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:256
    [SignalHandler]
    public virtual void occupant_or_container_moved(atom_movable occupant_or_container = null, atom old_loc = null)
    {
        if (isturf(old_loc) && isturf(occupant_or_container.loc))
            return;
        object old_locs = Dm.list();
        if (ismovable(old_loc))
            old_locs = Dm.add(get_nested_locs(old_loc as atom_movable), old_loc);
        object new_locs = get_nested_locs(occupant_or_container);
        foreach (atom_movable loc_exited in Dm.each_cast<atom_movable>(Dm.sub(old_locs, new_locs)))
            RemoveComponentSource(REF(loc_exited), typeof(datum_component_shuttle_move_deferred_checks));
        foreach (atom_movable loc_entered in Dm.each_cast<atom_movable>(Dm.sub(new_locs, old_locs)))
            Waiting.NoSleep(AddComponentFrom(REF(loc_entered), typeof(datum_component_shuttle_move_deferred_checks), nameof(post_movement_checks)));
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:269
    [SignalHandler]
    public virtual void post_movement_checks()
    {
        mob_living_silicon_ai ai = occupant as mob_living_silicon_ai;
        if (!(ai is mob_living_silicon_ai))
            return;
        if (!Dm.truthy(Waiting.NoSleep(ai.can_interact_with(parent.shell))))
            remove_occupant();
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:277
    [SignalHandler]
    public virtual void on_control_toggled(datum _source = null, object control_disabled = null)
    {
        if (Dm.truthy(control_disabled))
            remove_occupant();
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:282
    [SignalHandler]
    public virtual void remove_occupant()
    {
        if (occupant == null)
            return;
        if (isAI(occupant))
        {
            REMOVE_TRAIT(occupant, TRAIT_CONNECTED_TO_CIRCUIT, REF(this));
            mob_living_silicon_ai ai = occupant as mob_living_silicon_ai;
            if (ai.eyeobj == null)
                ai.create_eye();
            Waiting.NoSleep(disconnect_action.Remove(ai));
            UnregisterSignal(ai, Dm.list(COMSIG_MOB_RESET_PERSPECTIVE, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), COMSIG_SILICON_AI_SET_CONTROL_DISABLED));
            QDEL_NULL(ref connected_ai_container_connections);
            foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(ai), ai)))
                RemoveComponentSource(REF(location), typeof(datum_component_shuttle_move_deferred_checks));
            Waiting.NoSleep(ai.reset_perspective(null));
            to_chat(ai, span_notice(I18n.T("Disconnected from remote circuit.")));
        }
        occupant.remote_control = null;
        UnregisterSignal(occupant, Dm.list(COMSIG_MOB_CLICKON, COMSIG_QDELETING));
        occupant = null;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:302
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (user != occupant)
            return await base.relaymove(user, direction);

        object direct = direction;
        if (Dm.truthy(Dm.bitand(direct, NORTH)))
            north.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direct, WEST)))
            west.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direct, EAST)))
            east.set_output(COMPONENT_SIGNAL);
        if (Dm.truthy(Dm.bitand(direct, SOUTH)))
            south.set_output(COMPONENT_SIGNAL);

        return TRUE;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:317
    [SignalHandler]
    public virtual object handle_occupant_attack(mob_living source = null, atom target = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = null;
        if (Dm.truthy(modifiers[RIGHT_CLICK]))
        {
            clicked_atom.set_output(target);
            secondary_attack.set_output(COMPONENT_SIGNAL);
            dot = COMSIG_MOB_CANCEL_CLICKON;
        }
        else if (Dm.truthy(modifiers[LEFT_CLICK]) && !Dm.truthy(modifiers[SHIFT_CLICK]) && !Dm.truthy(modifiers[ALT_CLICK]) && !Dm.truthy(modifiers[CTRL_CLICK]))
        {
            clicked_atom.set_output(target);
            attack.set_output(COMPONENT_SIGNAL);
            dot = COMSIG_MOB_CANCEL_CLICKON;
        }
        return dot;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:328
    public override async DmTask<object> add_to(obj_item_integrated_circuit add_to = null)
    {
        object dot = await base.add_to(add_to);
        if (HAS_TRAIT(add_to, TRAIT_COMPONENT_MMI))
            return FALSE;
        ADD_TRAIT(add_to, TRAIT_COMPONENT_MMI, REF(this));
        return dot;
    }

    // tg: code/modules/wiremod/components/action/mmi.dm:334
    public override void removed_from(obj_item_integrated_circuit removed_from = null)
    {
        REMOVE_TRAIT(removed_from, TRAIT_COMPONENT_MMI, REF(this));
        remove_occupant_item();
        base.removed_from(removed_from);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/wiremod/components/action/mmi.dm:167
    internal DmList obj_item_circuit_component_mmi__register_boris_circuit__connections = Dm.list(Dm.kv(COMSIG_MOVABLE_MOVED, nameof(obj_item_circuit_component_mmi.boris_shell_or_container_moved)));

    // tg: code/modules/wiremod/components/action/mmi.dm:248
    internal DmList obj_item_circuit_component_mmi__set_occupant__connections = Dm.list(Dm.kv(COMSIG_MOVABLE_MOVED, nameof(obj_item_circuit_component_mmi.occupant_or_container_moved)));
}
