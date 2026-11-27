// Translated from tgstation (snapshot 1d99bd7f): code/modules/wiremod/shell/brain_computer_interface.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the brain-computer interface, a circuit shell implanted in the head (its core component speaks to its owner, shows
// the cell's charge on an action button and charges from a cyborg recharger or a shock), and the manipulation chamber that implants
// and removes it. The var blocks are generated.

// tg: code/modules/wiremod/shell/brain_computer_interface.dm:1
public partial class obj_item_organ_cyberimp_bci : obj_item_organ_cyberimp
{
    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_REGISTERED, nameof(action_comp_registered));
        RegisterSignal(this, COMSIG_CIRCUIT_ACTION_COMPONENT_UNREGISTERED, nameof(action_comp_unregistered));

        obj_item_integrated_circuit circuit = Dm.@new<obj_item_integrated_circuit>(this);
        Waiting.NoSleep(circuit.add_component(Dm.@new<obj_item_circuit_component_equipment_action>(null, "One")));

        Waiting.NoSleep(AddComponent(typeof(datum_component_shell), Dm.list(
            Dm.@new<obj_item_circuit_component_bci_core>()
        ), SHELL_CAPACITY_SMALL, Dm.named("starting_circuit", circuit)));
        return dot;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:22
    public override async DmTask<object> say(object message = null, object bubble_type = null, DmList spans = null, float? sanitize = null, object language = null, float? ignore_spam = null, object forced = null, float? filterproof = null, float? message_range = null, datum_saymode saymode = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        sanitize ??= TRUE;
        ignore_spam ??= FALSE;
        filterproof ??= FALSE;
        message_range ??= 7;
        message_mods ??= Dm.list();
        if (owner != null)
        {
            // Otherwise say_dead will be called.
            // It's intentional that a circuit for a dead person does not speak from the shell.
            if (owner.stat == DEAD)
                return null;

            forced = "circuit speech";
            return await owner.say(message, bubble_type, spans, sanitize, language, ignore_spam, forced, filterproof, message_range, saymode, message_mods);
        }

        return await base.say(message, bubble_type, spans, sanitize, language, ignore_spam, forced, filterproof, message_range, saymode, message_mods);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:46
    [SignalHandler]
    public virtual void action_comp_registered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        LAZYADD(ref actions, Dm.@new<datum_action_innate_circuit_equipment_action>(this, action_comp));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:50
    [SignalHandler]
    public virtual void action_comp_unregistered(datum source = null, obj_item_circuit_component_equipment_action action_comp = null)
    {
        datum_action_innate_circuit_equipment_action action = action_comp.granted_to[REF(this)] as datum_action_innate_circuit_equipment_action;
        if (!(action is datum_action_innate_circuit_equipment_action))
            return;
        LAZYREMOVE(ref actions, action);
        QDEL_LIST_ASSOC_VAL(action_comp.granted_to);
    }
}

// tg: code/modules/wiremod/shell/brain_computer_interface.dm:58
public partial class obj_item_circuit_component_bci_core : obj_item_circuit_component
{
    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:73
    public override async DmTask populate_ports()
    {
        message = add_input_port("Message", PORT_TYPE_STRING, trigger: null) as datum_port_input;
        send_message_signal = add_input_port("Send Message", PORT_TYPE_SIGNAL) as datum_port_input;
        show_charge_meter = add_input_port("Show Charge Meter", PORT_TYPE_BOOLEAN, trigger: nameof(update_charge_action)) as datum_port_input;

        user_port = add_output_port("User", PORT_TYPE_USER) as datum_port_output;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:81
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref charge_action);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:85
    [ShouldNotSleep]
    public virtual void update_charge_action()
    {
        if (Dm.truthy(show_charge_meter.value))
        {
            if (charge_action != null)
                return;
            charge_action = Dm.@new<datum_action_innate_bci_charge_action>(this);
            if (bci.owner != null)
                Waiting.NoSleep(charge_action.Grant(bci.owner));
            Dm.append(ref bci.actions, charge_action);
        }
        else
        {
            if (charge_action == null)
                return;
            if (bci.owner != null)
                Waiting.NoSleep(charge_action.Remove(bci.owner));
            Dm.remove(ref bci.actions, charge_action);
            QDEL_NULL(ref charge_action);
        }
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:102
    public override async DmTask register_shell(atom_movable shell = null)
    {
        bci = shell as obj_item_organ_cyberimp_bci;

        show_charge_meter.set_value(TRUE);

        RegisterSignal(shell, COMSIG_ORGAN_IMPLANTED, nameof(on_organ_implanted));
        RegisterSignal(shell, COMSIG_ORGAN_REMOVED, nameof(on_organ_removed));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:110
    public override async DmTask unregister_shell(atom_movable shell = null)
    {
        bci = shell as obj_item_organ_cyberimp_bci;

        if (charge_action != null)
        {
            if (bci.owner != null)
                await charge_action.Remove(bci.owner);
            Dm.remove(ref bci.actions, charge_action);
            QDEL_NULL(ref charge_action);
        }

        UnregisterSignal(shell, Dm.list(
            COMSIG_ORGAN_IMPLANTED,
            COMSIG_ORGAN_REMOVED));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:124
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (!COMPONENT_TRIGGERED_BY(send_message_signal, port))
            return null;

        string sent_message = trim(message.value) as string;
        if (!Dm.truthy(sent_message))
            return null;

        if (bci.owner == null)
            return null;

        if (bci.owner.stat == DEAD)
            return null;

        to_chat(bci.owner, I18n.T("<i>You hear a strange, robotic voice in your head...</i> \"[]\"", span_robot(Dm.text("[]", html_encode(sent_message)))));
        return null;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:140
    [SignalHandler]
    public virtual void on_organ_implanted(datum source = null, mob_living_carbon owner = null)
    {
        update_charge_action();

        user_port.set_output(owner);

        RegisterSignal(owner, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(owner, COMSIG_PROCESS_BORGCHARGER_OCCUPANT, nameof(on_borg_charge));
        RegisterSignal(owner, COMSIG_LIVING_ELECTROCUTE_ACT, nameof(on_electrocute));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:151
    [SignalHandler]
    public virtual void on_organ_removed(datum source = null, mob_living_carbon owner = null)
    {
        user_port.set_output(null);

        UnregisterSignal(owner, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_PROCESS_BORGCHARGER_OCCUPANT,
            COMSIG_LIVING_ELECTROCUTE_ACT));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:162
    [SignalHandler]
    public virtual void on_borg_charge(datum source = null, datum_callback charge_cell = null, object seconds_per_tick = null)
    {
        if (parent.cell == null)
            return;

        Waiting.NoSleep(charge_cell.Invoke(parent.cell, seconds_per_tick));
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:170
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

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:182
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob mob = null, DmList examine_text = null)
    {
        if (isobserver(mob))
        {
            atom source_atom = source as atom;
            Dm.append(examine_text, span_notice(I18n.T("[] [] <a href='byond://?src=[];open_bci=1'>\\a [] implanted in []</a>.", source_atom.p_They(), source_atom.p_have(), REF(this), parent, source_atom.p_them())));
        }
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:188
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);

        if (!isobserver(usr))
            return null;

        if (Dm.truthy(href_list["open_bci"]))
            await parent.attack_ghost(usr as mob_dead_observer);
        return null;
    }
}

// tg: code/modules/wiremod/shell/brain_computer_interface.dm:197
public partial class datum_action_innate_bci_charge_action : datum_action_innate
{
    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:205
    [DmParams("circuit_component")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_circuit_component_bci_core circuit_component = Dm.arg<obj_item_circuit_component_bci_core>(__extra, 0);
        await base.New(__extra);

        this.circuit_component = circuit_component;

        await build_all_button_icons();

        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:214
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext_x = 2;
        button.maptext_y = 0;
        return button;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:220
    public override object Destroy(float? force = null)
    {
        circuit_component.charge_action = null;
        circuit_component = null;

        STOP_PROCESSING(SSobj, this);

        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:228
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

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:239
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
        return null;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:242
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        await base.update_button_status(current_button, force);
        obj_item_stock_parts_power_store cell = circuit_component.parent.cell;
        current_button.maptext = cell != null ? MAPTEXT(Dm.text("[]%", cell.percent())) : "";
    }
}

// tg: code/modules/wiremod/shell/brain_computer_interface.dm:247
public partial class obj_machinery_bci_implanter : obj_machinery
{
    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:267
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        occupant_typecache = typecacheof(typeof(mob_living_carbon)) as DmList;
        return dot;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:271
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        await drop_stored_bci();
        return null;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:274
    public override object Destroy(float? force = null)
    {
        qdel(bci_to_implant);
        return base.Destroy(force);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:278
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (bci_to_implant == null)
            Dm.append(ref dot, span_notice(I18n.T("There is no BCI inserted.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("Right-click to remove current BCI.")));
        return dot;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:285
    public virtual void set_busy(object status = null, object working_icon = null)
    {
        busy = Dm.num_or_null(status);
        busy_icon_state = working_icon;
        update_appearance();
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:290
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (occupant != null)
        {
            icon_state = Dm.truthy(busy) ? Dm.str(busy_icon_state) : Dm.text("[]_occupied", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:297
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList overlays = (await base.update_overlays(__extra)) as DmList;

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
        {
            Dm.append(overlays, "maint");
            return overlays;
        }

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return overlays;

        if (Dm.truthy(busy) || Dm.truthy(locked))
        {
            Dm.append(overlays, "red");
            if (Dm.truthy(locked))
                Dm.append(overlays, "bolted");
            return overlays;
        }

        Dm.append(overlays, "green");

        return overlays;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:317
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;

        if (!Dm.truthy(await user.Adjacent(this)))
            return null;

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("it's locked!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        if (bci_to_implant == null)
            balloon_alert(user, I18n.T("no bci inserted!"));
        else
        {
            await user.put_in_hands(bci_to_implant);
            balloon_alert(user, I18n.T("ejected bci"));
        }

        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:337
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_organ_cyberimp_bci)))
            return NONE;

        obj_item_organ_cyberimp_bci new_bci = tool as obj_item_organ_cyberimp_bci;
        if (Dm.locate_type(typeof(obj_item_integrated_circuit), new_bci) == null)
        {
            balloon_alert(user, I18n.T("bci has no circuit!"));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_organ_cyberimp_bci previous_bci_to_implant = bci_to_implant;
        await user.transferItemToLoc(new_bci, this);
        bci_to_implant = new_bci;
        if (previous_bci_to_implant == null)
            balloon_alert(user, I18n.T("inserted bci"));
        else
        {
            balloon_alert(user, I18n.T("swapped bci"));
            await user.put_in_hands(previous_bci_to_implant);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:356
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return occupant == null ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:359
    public override async DmTask<object> crowbar_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, close_after_pry: FALSE, open_density: FALSE, closed_density: TRUE, deconstruct_on_fail: TRUE);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:362
    public virtual void start_process()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
            return;
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
            return;
        if (occupant == null || Dm.truthy(busy))
            return;

        update_use_power(ACTIVE_POWER_USE);

        float locked_state = locked;
        locked = TRUE;

        string initial_icon_state = Dm.str(Dm.initial(this, (obj_machinery_bci_implanter m) => m.icon_state));
        set_busy(TRUE, Dm.text("[]_raising", initial_icon_state));
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_busy), TRUE, Dm.text("[]_active", initial_icon_state)), 1 * SECONDS, file: "code/modules/wiremod/shell/brain_computer_interface.dm", line: 375);
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_busy), TRUE, Dm.text("[]_falling", initial_icon_state)), 2 * SECONDS, file: "code/modules/wiremod/shell/brain_computer_interface.dm", line: 376);
        _addtimer(Dm.@new<datum_callback>(this, nameof(complete_process), locked_state), 3 * SECONDS, file: "code/modules/wiremod/shell/brain_computer_interface.dm", line: 377);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:380
    public virtual async DmTask complete_process(object locked_state = null)
    {
        update_use_power(IDLE_POWER_USE);
        locked = Dm.num(locked_state);
        set_busy(FALSE);

        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
        if (!(carbon_occupant is mob_living_carbon))
            return;

        playsound(loc as atom, "sound/machines/ping.ogg", 30, FALSE);

        obj_item_organ_cyberimp_bci bci_organ = carbon_occupant.get_organ_by_type(typeof(obj_item_organ_cyberimp_bci)) as obj_item_organ_cyberimp_bci;

        if (bci_organ != null)
        {
            await bci_organ.Remove(carbon_occupant);

            if (bci_to_implant == null)
            {
                await say(I18n.T("Occupant's previous brain-computer interface has been transferred to internal storage unit."));
                await carbon_occupant.transferItemToLoc(bci_organ, this);
                bci_to_implant = bci_organ;
            }
            else
            {
                await say(I18n.T("Occupant's previous brain-computer interface has been ejected."));
                await bci_organ.forceMove(drop_location() as atom);
            }
        }
        else if (bci_to_implant != null)
        {
            await say(I18n.T("Occupant has been injected with [].", bci_to_implant));
            await bci_to_implant.Insert(carbon_occupant);
        }
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:407
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        if (Dm.truthy(state_open))
            return FALSE;

        await base.open_machine(drop, density_to_set);

        return TRUE;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:415
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        if (!Dm.truthy(state_open))
            return FALSE;

        await base.close_machine(target, density_to_set);

        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
        if (occupant is atom_movable) // istype(occupant) tests the machine's var against its own declared type, /atom/movable
        {
            obj_item_organ_cyberimp_bci bci_organ = carbon_occupant.get_organ_by_type(typeof(obj_item_organ_cyberimp_bci)) as obj_item_organ_cyberimp_bci;
            if (bci_organ == null && bci_to_implant == null)
            {
                await say(I18n.T("No brain-computer interface inserted, and occupant does not have one. Insert a BCI to implant one."));
                playsound(this, "sound/machines/buzz/buzz-sigh.ogg", 30, TRUE);
                return FALSE;
            }
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(start_process)), 1 * SECONDS, file: "code/modules/wiremod/shell/brain_computer_interface.dm", line: 429);
        return TRUE;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:432
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        string message = null;

        if (Dm.truthy(locked))
            message = I18n.T("it won't budge!");
        else if (IS_UNCONSCIOUS_OR_CRIT(user))
            message = I18n.T("you don't have the energy!");

        if (message != null)
        {
            if (message_cooldown <= world.time) // COOLDOWN_FINISHED
            {
                message_cooldown = world.time + 5 * SECONDS; // COOLDOWN_START
                balloon_alert(user, message);
            }

            return null;
        }

        await open_machine();
        return null;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:449
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(state_open))
        {
            // tg calls close_machine(null, user): the user lands in density_to_set, which only reads as true
            await close_machine(null, TRUE);
            return null;
        }
        else if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("it's locked!"));
            return null;
        }

        await open_machine();
        return null;
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:459
    public virtual async DmTask drop_stored_bci()
    {
        if (bci_to_implant == null)
            return;
        await bci_to_implant.forceMove(drop_location() as atom);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:464
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        // Prevents opening the machine dropping the BCI.
        // "dump_contents()" still drops the BCI.
        await base.dump_inventory_contents(Dm.sub(contents, bci_to_implant) as DmList);
    }

    // tg: code/modules/wiremod/shell/brain_computer_interface.dm:469
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == bci_to_implant)
            bci_to_implant = null;
        return await base.Exited(gone, direction);
    }
}
