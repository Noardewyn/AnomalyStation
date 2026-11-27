// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/quantum_pad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the quantum pad. A pad linked to another (a multitool's buffer saved from the other pad with its panel open, or
// the mapped pad ids) sends what is loose on its tile to the linked pad when used: the flash, the charge-up by its servos, the
// sparks, the beam flick and the emitter sound on both pads, then the power drawn by its capacitors, and its cooldown. The
// quantum keycard links to a pad and opens a pad's link to it. A ghost jumps along the link. The pad's USB port takes its
// circuit component (a target pad port and a fail signal). The var blocks are generated (D111).

// tg: code/game/machinery/quantum_pad.dm:1
public partial class obj_machinery_quantumpad : obj_machinery
{
    /// mapping: var/static/list/mapped_quantum_pads
    // tg: code/game/machinery/quantum_pad.dm:18
    public static DmList mapped_quantum_pads => TgState.Current.obj_machinery_quantumpad__mapped_quantum_pads ??= Dm.list();

    // tg: code/game/machinery/quantum_pad.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(map_pad_id))
            mapped_quantum_pads[map_pad_id] = this;

        Waiting.NoSleep(AddComponent(typeof(datum_component_usb_port), typecacheof(Dm.list(typeof(obj_item_circuit_component_quantumpad)), only_root_path: TRUE)));
        return dot;
    }

    // tg: code/game/machinery/quantum_pad.dm:29
    public override object Destroy(float? force = null)
    {
        DmList pads = mapped_quantum_pads;
        Dm.remove(ref pads, map_pad_id);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/quantum_pad.dm:33
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It is [] linked to another pad.", linked_pad != null ? "currently" : "not")));
        if (!Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("The panel is <i>screwed</i> in, obstructing the linking device.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The <i>linking</i> device is now able to be <i>scanned<i> with a multitool.")));
        return dot;
    }

    // tg: code/game/machinery/quantum_pad.dm:41
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float E = 0;
        foreach (datum_stock_part_capacitor capacitor in Dm.each<datum_stock_part_capacitor>(component_parts))
            E += Dm.num(capacitor.tier);
        power_efficiency = E;
        E = 0;
        foreach (datum_stock_part_servo servo in Dm.each<datum_stock_part_servo>(component_parts))
            E += Dm.num(servo.tier);
        teleport_speed = Dm.num(Dm.initial(this, nameof(teleport_speed)));
        teleport_speed -= (E * 10);
        teleport_cooldown = Dm.num(Dm.initial(this, nameof(teleport_cooldown)));
        teleport_cooldown -= (E * 100);
        return dot;
    }

    // tg: code/game/machinery/quantum_pad.dm:55
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        obj_item_multitool multi_tool = tool as obj_item_multitool;
        if (Dm.truthy(panel_open))
        {
            multi_tool.set_buffer(this);
            balloon_alert(user, I18n.T("saved to multitool buffer"));
            to_chat(user, span_notice(I18n.T("You save the data in [] buffer. It can now be saved to pads with closed panels.", multi_tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(multi_tool.buffer, typeof(obj_machinery_quantumpad)))
        {
            if (multi_tool.buffer == (object)this)
            {
                balloon_alert(user, I18n.T("cannot link to self!"));
                return ITEM_INTERACT_BLOCKING;
            }
            linked_pad = multi_tool.buffer as obj_machinery_quantumpad;
            balloon_alert(user, I18n.T("data uploaded from buffer"));
            return ITEM_INTERACT_SUCCESS;
        }

        balloon_alert(user, I18n.T("no quantum pad data found!"));
        return NONE;
    }

    // tg: code/game/machinery/quantum_pad.dm:73
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/quantum_pad.dm:76
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/quantum_pad.dm:79
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_quantum_keycard)))
        {
            obj_item_quantum_keycard card = tool as obj_item_quantum_keycard;
            if (Dm.truthy(card.qpad))
            {
                to_chat(user, span_notice(I18n.T("You insert [] into []'s card slot, activating it.", card, this)));
                await interact(user, card.qpad);
                return ITEM_INTERACT_SUCCESS;
            }
            to_chat(user, span_notice(I18n.T("You insert [] into []'s card slot, initiating the link procedure.", card, this)));
            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this)))
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You complete the link between [] and [].", card, this)));
            await card.set_pad(this);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/machinery/quantum_pad.dm:95
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))) || Dm.truthy(state_open))
            icon_state = Dm.text("[]-idle-open", base_icon_state);
        else
            icon_state = Dm.text("[]-idle", base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/quantum_pad.dm:102
    [DmParams("user", "target_pad")]
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        obj_machinery_quantumpad target_pad = (Dm.arg(__extra, 0) ?? linked_pad) as obj_machinery_quantumpad;
        if (QDELETED(target_pad))
        {
            if (Dm.truthy(map_pad_link_id) && Dm.truthy(initMappedLink()))
                target_pad = linked_pad;
            else
            {
                to_chat(user, span_warning(I18n.T("Target pad not found!")));
                return null;
            }
        }

        if (world.time < Dm.num(last_teleport) + teleport_cooldown)
        {
            to_chat(user, span_warning(I18n.T("[] is recharging power. Please wait [].", this, DisplayTimeText(Dm.num(last_teleport) + teleport_cooldown - world.time))));
            return null;
        }

        if (Dm.truthy(teleporting))
        {
            to_chat(user, span_warning(I18n.T("[] is charging up. Please wait.", this)));
            return null;
        }

        if (Dm.truthy(target_pad.teleporting))
        {
            to_chat(user, span_warning(I18n.T("Target pad is busy. Please wait.")));
            return null;
        }

        if (Dm.truthy(Dm.bitand(target_pad.machine_stat, NOPOWER)))
        {
            to_chat(user, span_warning(I18n.T("Target pad is not responding to ping.")));
            return null;
        }
        add_fingerprint(user);
        doteleport(user, target_pad);
        return null;
    }

    // tg: code/game/machinery/quantum_pad.dm:128
    public virtual void sparks()
    {
        do_sparks(5, TRUE, this, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
    }

    // tg: code/game/machinery/quantum_pad.dm:131
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        mob_dead_observer ghost = user;
        object dot = await base.attack_ghost(ghost);
        if (Dm.truthy(dot))
            return dot;
        if (linked_pad == null && Dm.truthy(map_pad_link_id))
            initMappedLink();
        if (linked_pad != null)
            await ghost.forceMove(get_turf(linked_pad));
        return dot;
    }

    // tg: code/game/machinery/quantum_pad.dm:140
    public virtual void doteleport(mob user = null, obj_machinery_quantumpad target_pad = null)
    {
        target_pad ??= linked_pad;
        if (target_pad == null)
            return;
        playsound(get_turf(this), "sound/items/weapons/flash.ogg", 25, TRUE);
        teleporting = TRUE;

        _addtimer(Dm.@new<datum_callback>(this, nameof(teleport_contents), user, target_pad), teleport_speed, file: "code/game/machinery/quantum_pad.dm", line: 146);
    }

    // tg: code/game/machinery/quantum_pad.dm:148
    public virtual async DmTask teleport_contents(mob user = null, obj_machinery_quantumpad target_pad = null)
    {
        teleporting = FALSE;
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("[] is unpowered!", this)));
            return;
        }
        if (QDELETED(target_pad) || Dm.truthy(Dm.bitand(target_pad.machine_stat, NOPOWER)))
        {
            if (user != null)
                to_chat(user, span_warning(I18n.T("Linked pad is not responding to ping. Teleport aborted.")));
            return;
        }

        last_teleport = world.time;

        // use a lot of power
        await use_energy(Dm.num(active_power_usage) / power_efficiency);
        sparks();
        target_pad.sparks();

        flick(Dm.text("[]-beam", base_icon_state), this);
        playsound(get_turf(this), "sound/items/weapons/emitter2.ogg", 25, TRUE);
        flick(Dm.text("[]-beam", target_pad.base_icon_state), target_pad);
        playsound(get_turf(target_pad), "sound/items/weapons/emitter2.ogg", 25, TRUE);
        foreach (atom_movable ROI in Dm.each<atom_movable>(get_turf(this)))
        {
            if (QDELETED(ROI))
                continue; //sleeps in CHECK_TICK

            // if is anchored, don't let through
            if (Dm.truthy(ROI.anchored))
                continue;

            if (isliving(ROI))
            {
                mob_living living_subject = ROI as mob_living;
                //only TP living mobs buckled to non anchored items
                if (living_subject.buckled != null && Dm.truthy(living_subject.buckled.anchored))
                    continue;
            }

            await do_teleport(ROI, get_turf(target_pad), no_effects: TRUE, channel: TELEPORT_CHANNEL_QUANTUM);
            CHECK_TICK();
        }
    }

    // tg: code/game/machinery/quantum_pad.dm:187
    public virtual object initMappedLink()
    {
        object dot = FALSE;
        obj_machinery_quantumpad link = mapped_quantum_pads[map_pad_link_id] as obj_machinery_quantumpad;
        if (link != null)
        {
            linked_pad = link;
            dot = TRUE;
        }
        return dot;
    }
}

// tg: code/game/machinery/quantum_pad.dm:198
public partial class obj_item_circuit_component_quantumpad : obj_item_circuit_component
{
    // tg: code/game/machinery/quantum_pad.dm:208
    public override async DmTask populate_ports()
    {
        await DmTask.CompletedTask;
        target_pad = add_input_port("Target Pad", PORT_TYPE_ATOM) as datum_port_input;
        failed = add_output_port("On Fail", PORT_TYPE_SIGNAL) as datum_port_output;
    }

    // tg: code/game/machinery/quantum_pad.dm:212
    public override void register_usb_parent(atom_movable shell = null)
    {
        base.register_usb_parent(shell);
        if (istype(shell, typeof(obj_machinery_quantumpad)))
            attached_pad = shell as obj_machinery_quantumpad;
    }

    // tg: code/game/machinery/quantum_pad.dm:217
    public override object unregister_usb_parent(atom_movable shell = null)
    {
        attached_pad = null;
        return base.unregister_usb_parent(shell);
    }

    // tg: code/game/machinery/quantum_pad.dm:221
    public override object input_received(datum_port_input port = null, DmList return_values = null)
    {
        if (attached_pad == null)
            return null;

        obj_machinery_quantumpad targeted_pad = target_pad.value as obj_machinery_quantumpad;

        if ((attached_pad.linked_pad == null || QDELETED(attached_pad.linked_pad)) && !(targeted_pad != null && istype(targeted_pad, typeof(obj_machinery_quantumpad))))
        {
            failed.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (world.time < Dm.num(attached_pad.last_teleport) + attached_pad.teleport_cooldown)
        {
            failed.set_output(COMPONENT_SIGNAL);
            return null;
        }

        if (targeted_pad != null && istype(targeted_pad, typeof(obj_machinery_quantumpad)))
        {
            if (Dm.truthy(attached_pad.teleporting) || Dm.truthy(targeted_pad.teleporting))
            {
                failed.set_output(COMPONENT_SIGNAL);
                return null;
            }

            if (Dm.truthy(Dm.bitand(targeted_pad.machine_stat, NOPOWER)))
            {
                failed.set_output(COMPONENT_SIGNAL);
                return null;
            }
            attached_pad.doteleport(target_pad: targeted_pad);
        }
        else
        {
            if (Dm.truthy(attached_pad.teleporting) || Dm.truthy(attached_pad.linked_pad.teleporting))
            {
                failed.set_output(COMPONENT_SIGNAL);
                return null;
            }

            if (Dm.truthy(Dm.bitand(attached_pad.linked_pad.machine_stat, NOPOWER)))
            {
                failed.set_output(COMPONENT_SIGNAL);
                return null;
            }
            attached_pad.doteleport(target_pad: attached_pad.linked_pad);
        }
        return null;
    }
}

// The quantum pads' static map of mapped pad ids (rules 10.6).
public sealed partial class TgState
{
    // tg: code/game/machinery/quantum_pad.dm:18
    internal DmList obj_machinery_quantumpad__mapped_quantum_pads;
}
