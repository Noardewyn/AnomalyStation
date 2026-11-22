// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/conveyor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The conveyors (the owner, 2026-11-04): the whole file but the circuit component (/obj/item/circuit_component/conveyor_switch
// stays inert: circuits, not-translated.md 114), the screentips (add_context, deferred by the owner's rule of 2026-10-29) and
// the variable editor's hooks (vv_edit_var, D275); live-partial.txt lists them. A belt moves what lies on it through its
// convey component's move loop on SSconveyors (conveyor_movement.dm); a switch (or several with one id) sets every belt of
// its id going forward, backward or off; a crowbar lifts a belt into a belt stack and a switch into a switch assembly, which
// build them again. The var blocks are generated.

// tg: code/modules/recycling/conveyor.dm:11
public partial class obj_machinery_conveyor : obj_machinery
{
    /// Maximum amount of items a conveyor can move at once.
    // tg: code/modules/recycling/conveyor.dm:2  (file-local, unused in the file)
    internal const int MAX_CONVEYOR_ITEMS_MOVE = 30;
    /// Conveyor is currently off.
    // tg: code/modules/recycling/conveyor.dm:4
    internal const int CONVEYOR_OFF = 0;
    /// Conveyor is currently configured to move items forward.
    // tg: code/modules/recycling/conveyor.dm:6
    internal const int CONVEYOR_FORWARD = 1;
    /// Conveyor is currently configured to move items backwards.
    // tg: code/modules/recycling/conveyor.dm:8
    internal const int CONVEYOR_BACKWARDS = -1;

    // tg: code/modules/recycling/conveyor.dm:48
    [DmParams("mapload", "new_dir", "new_id")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_dir = Dm.arg(__extra, 0);
        object new_id = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_dir, new_id));
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep_override), Dm.named("priority", STEP_SOUND_CONVEYOR_PRIORITY)));
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_TURF_IGNORE_SLOWDOWN))));
        register_context();
        if (Dm.truthy(new_dir))
            Waiting.NoSleep(setDir(new_dir));
        if (Dm.truthy(new_id))
            id = (string)new_id;
        neighbors = Dm.list();
        ///Leaving onto conveyor detection won't work at this point, but that's alright since it's an optimization anyway
        ///Should be fine without it
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddElement(typeof(datum_element_force_move_pulled)));
        Waiting.NoSleep(update_move_direction());
        conveyors_by_id_add(id, this);
        if (Dm.truthy(wire_mode))
        {
            update_cable();
            START_PROCESSING(SSmachines, this);
        }
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:61  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_conveyor__Initialize__loc_connections;

    // LAZYADD(GLOB.conveyors_by_id[id], src) (__HELPERS/_lists.dm): the list is a value of the assoc list, inline (rules 11.1);
    // the switch shares it
    internal static void conveyors_by_id_add(string key, datum member)
    {
        GLOB.conveyors_by_id[key] ??= Dm.list();
        Dm.append((DmList)GLOB.conveyors_by_id[key], member);
    }

    // LAZYREMOVE(GLOB.conveyors_by_id[id], src), inline as above
    internal static void conveyors_by_id_remove(string key, datum member)
    {
        if (GLOB.conveyors_by_id[key] is DmList members)
        {
            Dm.remove(members, member);
            if (!Dm.truthy(Dm.length(members)))
                GLOB.conveyors_by_id[key] = null;
        }
    }

    // tg: code/modules/recycling/conveyor.dm:73
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(inverted))
            Dm.append(ref dot, span_notice(I18n.T("It is currently set to go in reverse.")));
        Dm.append(ref dot, I18n.T("\nLeft-click with a <b>wrench</b> to rotate clockwise."));
        Dm.append(ref dot, I18n.T("Right-click with a <b>wrench</b> to rotate counterclockwise."));
        Dm.append(ref dot, I18n.T("Left-click with a <b>screwdriver</b> to invert its direction."));
        Dm.append(ref dot, I18n.T("Right-click with a <b>screwdriver</b> to flip its belt around."));
        Dm.append(ref dot, I18n.T("Left-click with a <b>multitool</b> to toggle whether this conveyor receives power via cable. Toggling connects and disconnects."));
        Dm.append(ref dot, I18n.T("Using another <b>conveyor belt assembly</b> on this will place a <b>new conveyor belt<b> in the direction this one is pointing."));
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:134
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        build_neighbors();
    }

    // tg: code/modules/recycling/conveyor.dm:138
    public override object Destroy(float? force = null)
    {
        last_command = CONVEYOR_OFF;
        Waiting.NoSleep(set_operating(FALSE));
        conveyors_by_id_remove(id, this);
        attached_wire_ref = null;
        return base.Destroy(force);
    }

    // tg: code/modules/recycling/conveyor.dm:154
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        await update_move_direction();
    }

    // tg: code/modules/recycling/conveyor.dm:158
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (!Dm.truthy(dot))
            return dot;
        //Now that we've moved, rebuild our neighbors list
        neighbors = Dm.list();
        build_neighbors();
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:166
    public virtual void build_neighbors()
    {
        //This is acceptable because conveyor belts only move sometimes. Otherwise would be n^2 insanity
        turf our_turf = get_turf(this);
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            turf new_turf = get_step(our_turf, Dm.num(direction)) as turf;
            obj_machinery_conveyor valid = Dm.locate<obj_machinery_conveyor>(new_turf);
            if (QDELETED(valid))
                continue;
            neighbors[Dm.text("[]", direction)] = TRUE;
            valid.neighbors[Dm.text("[]", REVERSE_DIR(Dm.num(direction)))] = TRUE;
            RegisterSignal(valid, COMSIG_MOVABLE_MOVED, nameof(nearby_belt_changed), @override: TRUE);
            RegisterSignal(valid, COMSIG_QDELETING, nameof(nearby_belt_changed), @override: TRUE);
            valid.RegisterSignal(this, COMSIG_MOVABLE_MOVED, nameof(nearby_belt_changed), @override: TRUE);
            valid.RegisterSignal(this, COMSIG_QDELETING, nameof(nearby_belt_changed), @override: TRUE);
        }
    }

    // tg: code/modules/recycling/conveyor.dm:181
    [SignalHandler]
    public virtual void nearby_belt_changed(datum source = null)
    {
        // SIGNAL_HANDLER
        neighbors = Dm.list();
        build_neighbors();
    }

    /**
     * Proc to handle updating the directions in which the conveyor belt is moving items.
     */
    // tg: code/modules/recycling/conveyor.dm:189
    public virtual async DmTask update_move_direction()
    {
        object sw__1 = dir;
        if (Dm.eq(sw__1, NORTH))
        {
            forwards = NORTH;
            backwards = SOUTH;
        }
        else if (Dm.eq(sw__1, SOUTH))
        {
            forwards = SOUTH;
            backwards = NORTH;
        }
        else if (Dm.eq(sw__1, EAST))
        {
            forwards = EAST;
            backwards = WEST;
        }
        else if (Dm.eq(sw__1, WEST))
        {
            forwards = WEST;
            backwards = EAST;
        }
        else if (Dm.eq(sw__1, NORTHEAST))
        {
            forwards = EAST;
            backwards = SOUTH;
        }
        else if (Dm.eq(sw__1, NORTHWEST))
        {
            forwards = NORTH;
            backwards = EAST;
        }
        else if (Dm.eq(sw__1, SOUTHEAST))
        {
            forwards = SOUTH;
            backwards = WEST;
        }
        else if (Dm.eq(sw__1, SOUTHWEST))
        {
            forwards = WEST;
            backwards = NORTH;
        }

        if (Dm.truthy(inverted))
        {
            object temp = forwards;
            forwards = backwards;
            backwards = temp;
        }
        // We need to do this this way to ensure good functionality on corner belts.
        // Basically, this allows the conveyor belts that used a flipped belt sprite to
        // still convey items in the direction of their arrows. It's different from inverted,
        // which makes them go backwards so they need to be ran separately, so a flipped conveyor
        // can also be reversed.
        if (Dm.truthy(flipped))
        {
            object temp = forwards;
            forwards = backwards;
            backwards = temp;
        }
        if (Dm.eq(operating, CONVEYOR_FORWARD))
            movedir = forwards;
        else
            movedir = backwards;
        await update();
    }

    // tg: code/modules/recycling/conveyor.dm:239
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        // DM prints a negated stopped belt (inverted ? -operating) as "0"; C#'s -0 would print "-0"
        float shown = Dm.truthy(inverted) && Dm.num(operating) != 0 ? -Dm.num(operating) : Dm.num(operating);
        icon_state = Dm.text("[][][]", base_icon_state, shown, Dm.truthy(flipped) ? "-flipped" : "");
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/recycling/conveyor.dm:243
    public virtual async DmTask set_operating(object new_value = null)
    {
        if (Dm.eq(operating, new_value))
            return;
        operating = Dm.num(new_value);
        update_appearance();
        await update_move_direction();
        //If we ever turn off, disable moveloops
        if (Dm.eq(operating, CONVEYOR_OFF))
        {
            foreach (atom_movable movable in Dm.each<atom_movable>(get_turf(this)))
                stop_conveying(movable);
        }
    }

    // tg: code/modules/recycling/conveyor.dm:254
    public virtual async DmTask<object> update()
    {
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            await set_operating(FALSE);
            return FALSE;
        }
        if (!Dm.eq(operating, last_command))
            await set_operating(last_command);
        update_appearance();
        // If we're on, start conveying so moveloops on our tile can be refreshed if they stopped for some reason
        if (!Dm.eq(operating, CONVEYOR_OFF))
        {
            foreach (atom_movable movable in Dm.each<atom_movable>(get_turf(this)))
                await start_conveying(movable);
        }
        return TRUE;
    }

    // tg: code/modules/recycling/conveyor.dm:268
    [SignalHandler]
    public virtual void conveyable_enter(datum source = null, atom_movable convayable = null)
    {
        // SIGNAL_HANDLER
        if (convayable.loc != loc) // If we are not on the same turf (order of operations memes) go to hell
            return;
        if (Dm.eq(operating, CONVEYOR_OFF))
        {
            GLOB.move_manager.stop_looping(convayable, SSconveyors);
            return;
        }
        Waiting.NoSleep(start_conveying(convayable));
    }

    // tg: code/modules/recycling/conveyor.dm:277
    [SignalHandler]
    public virtual void conveyable_exit(datum source = null, atom convayable = null, object direction = null)
    {
        // SIGNAL_HANDLER
        object has_conveyor = neighbors[Dm.text("[]", direction)];
        if (!Dm.eq(convayable.z, z) || !Dm.truthy(has_conveyor) || !isturf(convayable.loc)) //If you've entered something on us, stop moving
            GLOB.move_manager.stop_looping(convayable as atom_movable, SSconveyors);
    }

    // tg: code/modules/recycling/conveyor.dm:283
    public virtual async DmTask start_conveying(atom_movable moving = null)
    {
        if (QDELETED(moving))
            return;
        datum_move_loop_move moving_loop = GLOB.move_manager.processing_on(moving, SSconveyors) as datum_move_loop_move;
        if (moving_loop != null)
        {
            moving_loop.direction = movedir;
            moving_loop.delay = speed * 1 * SECONDS;
            return;
        }

        if (!istype(moving, typeof(atom_movable)) || Dm.truthy(is_type_in_typecache(moving, start_conveying__unconveyables)) || moving == this)
            return;
        await moving.AddComponent(typeof(datum_component_convey), movedir, speed * 1 * SECONDS);
    }

    // tg: code/modules/recycling/conveyor.dm:292  var/static/list/unconveyables (a proc-level static, rules 10.6)
    private static DmList start_conveying__unconveyables => TgState.Current.obj_machinery_conveyor__start_conveying__unconveyables ??=
        (DmList)typecacheof(Dm.list(typeof(obj_effect), typeof(mob_dead)));

    // tg: code/modules/recycling/conveyor.dm:297
    public virtual void stop_conveying(atom_movable thing = null)
    {
        if (!ismovable(thing))
            return;
        GLOB.move_manager.stop_looping(thing, SSconveyors);
    }

    // attack with item, place item on conveyor
    // tg: code/modules/recycling/conveyor.dm:303
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_conveyor)))
        {
            // We should place a new conveyor belt machine on the output turf the conveyor is pointing to.
            turf target_turf = get_step(get_turf(this), Dm.num(forwards)) as turf;
            if (target_turf == null || isclosedturf(target_turf))
                return ITEM_INTERACT_BLOCKING;
            foreach (obj_machinery_conveyor belt in Dm.each<obj_machinery_conveyor>(target_turf))
            {
                to_chat(user, span_warning(I18n.T("You cannot place a conveyor belt on top of another conveyor belt.")));
                return ITEM_INTERACT_BLOCKING;
            }

            obj_item_stack_conveyor belt_item = (obj_item_stack_conveyor)tool;
            await belt_item.use(1);
            Dm.@new<obj_machinery_conveyor>(target_turf, forwards, id);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            await user.transfer_item_to_turf(tool, drop_location() as turf);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/recycling/conveyor.dm:323
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        user.visible_message(span_notice(I18n.T("[] struggles to pry up [] with [].", user, this, tool)),
            span_notice(I18n.T("You struggle to pry up [] with [].", this, tool)));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 40)))
            return ITEM_INTERACT_BLOCKING;
        await set_operating(FALSE);
        obj_item_stack_conveyor belt_item = Dm.@new<obj_item_stack_conveyor>(loc, 1, TRUE, null, null, id);
        if (!QDELETED(belt_item)) //God I hate stacks
            transfer_fingerprints_to(belt_item);

        to_chat(user, span_notice(I18n.T("You remove [].", this)));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:338
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await setDir(turn(dir, -45));
        to_chat(user, span_notice(I18n.T("You rotate [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:344
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        inverted = Dm.num(!Dm.truthy(inverted));
        await update_move_direction();
        to_chat(user, span_notice(I18n.T("You set []'s direction [].", this, Dm.truthy(inverted) ? I18n.T("backwards") : I18n.T("back to default"))));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:351
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        wire_mode = Dm.num(!Dm.truthy(wire_mode));
        update_cable();
        power_change();
        if (Dm.truthy(wire_mode))
            START_PROCESSING(SSmachines, this);
        else
            STOP_PROCESSING(SSmachines, this);
        to_chat(user, span_notice(I18n.T("You set []'s wire mode [].", this, Dm.truthy(wire_mode) ? I18n.T("on") : I18n.T("off"))));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:363
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;
        await user.transferItemToLoc(tool, drop_location());
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:369
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        flipped = Dm.num(!Dm.truthy(flipped));
        await update_move_direction();
        to_chat(user, span_notice(I18n.T("You flip []'s belt [].", this, Dm.truthy(flipped) ? I18n.T("around") : I18n.T("back to normal"))));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:376
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await setDir(turn(dir, 45));
        to_chat(user, span_notice(I18n.T("You rotate [].", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:382
    public override object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        chan ??= power_channel;
        ignore_use_power ??= FALSE;
        if (!Dm.truthy(wire_mode))
            return base.powered__proc(chan, ignore_use_power);
        datum_powernet powernet = get_powernet() as datum_powernet;
        if (powernet != null)
            return Dm.num(clamp(powernet.avail - Dm.num(powernet.load), 0, powernet.avail)) >= active_power_usage;
        return base.powered__proc(chan, ignore_use_power);
    }

    // tg: code/modules/recycling/conveyor.dm:390
    public override object power_change()
    {
        object dot = base.power_change();
        Waiting.NoSleep(update());
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:394
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(wire_mode))
            return PROCESS_KILL;
        if (attached_wire_ref == null)
        {
            update_cable();
            return null;
        }
        datum_powernet powernet = get_powernet() as datum_powernet;
        if (powernet == null)
            return null;
        if (Dm.truthy(powered__proc()))
        {
            powernet.load = Dm.num(powernet.load) + active_power_usage;
            if (!Dm.truthy(powered_wire))
            {
                powered_wire = TRUE;
                power_change();
            }
        }
        else
        {
            power_change();
            powered_wire = FALSE;
        }
        return null;
    }

    // tg: code/modules/recycling/conveyor.dm:413
    public virtual object update_cable()
    {
        if (!Dm.truthy(wire_mode))
        {
            attached_wire_ref = null;
            return null;
        }
        turf our_turf = get_turf(this);
        attached_wire_ref = WEAKREF(Dm.locate<obj_structure_cable>(our_turf)) as datum_weakref;
        if (attached_wire_ref != null)
            return power_change();
        return null;
    }

    // tg: code/modules/recycling/conveyor.dm:422
    public virtual object get_powernet()
    {
        if (!Dm.truthy(wire_mode))
            return null;
        // tg reads attached_wire_ref.resolve() bare: a multitool on a belt without a cable under it runtimes there (power_change
        // after update_cable found none); the port reads it as no cable
        obj_structure_cable cable = attached_wire_ref?.resolve() as obj_structure_cable;
        if (cable == null)
        {
            attached_wire_ref = null;
            return null;
        }
        return cable.powernet;
    }
}

// tg: code/modules/recycling/conveyor.dm:109
public partial class obj_machinery_conveyor_inverted : obj_machinery_conveyor
{
    // tg: code/modules/recycling/conveyor.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && !Dm.truthy(ISDIAGONALDIR(dir)))
            log_mapping(Dm.text("[] at [] spawned without using a diagonal dir. Please replace with a normal version.", this, AREACOORD(this)));
        return dot;
    }
}

// Auto conveyor is always on unless unpowered.
// tg: code/modules/recycling/conveyor.dm:116
public partial class obj_machinery_conveyor_auto : obj_machinery_conveyor
{
    // tg: code/modules/recycling/conveyor.dm:119
    [DmParams("mapload", "newdir")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        last_command = CONVEYOR_FORWARD;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_operating(last_command));
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:124
    public override async DmTask<object> update()
    {
        object dot = await base.update();
        if (Dm.truthy(dot))
            await set_operating(last_command);
        return dot;
    }
}

// Conveyor switch
// tg: code/modules/recycling/conveyor.dm:427
public partial class obj_machinery_conveyor_switch : obj_machinery
{
    // tg: code/modules/recycling/conveyor.dm:445
    [DmParams("mapload", "newid")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object newid = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, newid));
        if (Dm.truthy(newid))
            id = (string)newid;

        update_appearance();
        obj_machinery_conveyor.conveyors_by_id_add(id, this);
        set_wires(Dm.@new<datum_wires_conveyor>(this));
        // AddComponent(/datum/component/usb_port, typecacheof(list(/obj/item/circuit_component/conveyor_switch), only_root_path = TRUE))
        Dm.Deferred("code/modules/recycling/conveyor.dm:453", "Initialize: the USB port of the conveyor switch's circuit component (circuits, not-translated.md 114)");
        register_context();
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:456
    public override object Destroy(float? force = null)
    {
        obj_machinery_conveyor.conveyors_by_id_remove(id, this);
        return base.Destroy(force);
    }

    // update the icon depending on the position
    // tg: code/modules/recycling/conveyor.dm:471
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]-off", base_icon_state);
        if (Dm.num(position) < obj_machinery_conveyor.CONVEYOR_OFF)
            icon_state = Dm.text("[]-[]", base_icon_state, Dm.truthy(invert_icon) ? "fwd" : "rev");
        else if (Dm.num(position) > obj_machinery_conveyor.CONVEYOR_OFF)
            icon_state = Dm.text("[]-[]", base_icon_state, Dm.truthy(invert_icon) ? "rev" : "fwd");
        return await base.update_icon_state(__extra);
    }

    /// Updates all conveyor belts that are linked to this switch, and tells them to start processing.
    // tg: code/modules/recycling/conveyor.dm:499
    public virtual async DmTask update_linked_conveyors()
    {
        foreach (obj_machinery_conveyor belt in Dm.each<obj_machinery_conveyor>(GLOB.conveyors_by_id[id]))
        {
            belt.last_command = Dm.num(position);
            await belt.set_operating(position);
            belt.speed = conveyor_speed;
            CHECK_TICK();
        }
    }

    /// Finds any switches with same `id` as this one, and set their position and icon to match us.
    // tg: code/modules/recycling/conveyor.dm:507
    public virtual void update_linked_switches()
    {
        foreach (obj_machinery_conveyor_switch belt_switch in Dm.each<obj_machinery_conveyor_switch>(GLOB.conveyors_by_id[id]))
        {
            belt_switch.invert_icon = invert_icon;
            belt_switch.position = position;
            belt_switch.conveyor_speed = conveyor_speed;
            belt_switch.update_appearance();
            CHECK_TICK();
        }
    }

    /// Updates the switch's `position` and `last_pos` variable. Useful so that the switch can properly cycle between the forwards, backwards and neutral positions.
    // tg: code/modules/recycling/conveyor.dm:517
    public virtual void update_position(object direction = null)
    {
        if (Dm.eq(position, obj_machinery_conveyor.CONVEYOR_OFF))
        {
            playsound(this, Dm.resource("sound/machines/lever/lever_start.ogg"), 40, TRUE);

            if (Dm.truthy(oneway)) //is it a oneway switch
                position = oneway;
            else
            {
                if (Dm.eq(direction, obj_machinery_conveyor.CONVEYOR_FORWARD))
                    position = obj_machinery_conveyor.CONVEYOR_FORWARD;
                else
                    position = obj_machinery_conveyor.CONVEYOR_BACKWARDS;
            }
        }
        else
        {
            playsound(this, Dm.resource("sound/machines/lever/lever_stop.ogg"), 40, TRUE);
            position = obj_machinery_conveyor.CONVEYOR_OFF;
        }
    }

    // tg: code/modules/recycling/conveyor.dm:533
    public virtual async DmTask on_user_activation(mob user = null, object direction = null)
    {
        add_fingerprint(user);
        update_position(direction);
        update_appearance();
        await update_linked_conveyors();
        update_linked_switches();
    }

    /// Called when a user clicks on this switch with an open hand.
    // tg: code/modules/recycling/conveyor.dm:541
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        await on_user_activation(user, obj_machinery_conveyor.CONVEYOR_FORWARD);
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:545
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await on_user_activation(user, obj_machinery_conveyor.CONVEYOR_BACKWARDS);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/recycling/conveyor.dm:561
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(is_wire_tool(tool)))
            return NONE;
        await wires.interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:567
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object input_speed = await tgui_input_number(user, I18n.T("Set the speed of the conveyor belts in seconds"), I18n.T("Speed"), conveyor_speed, 20, 0.2f, round_value: FALSE);
        if (!Dm.truthy(input_speed) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await usr.can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        conveyor_speed = Dm.num(input_speed);
        to_chat(user, span_notice(I18n.T("You change the time between moves to [] seconds.", input_speed)));
        await update_linked_conveyors();
        return TRUE;
    }

    // tg: code/modules/recycling/conveyor.dm:576
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        obj_item_conveyor_switch_construct switch_construct = Dm.@new<obj_item_conveyor_switch_construct>(loc);
        switch_construct.id = id;
        transfer_fingerprints_to(switch_construct);
        to_chat(user, span_notice(I18n.T("You detach [].", this)));
        qdel(this);
        return TRUE;
    }

    // tg: code/modules/recycling/conveyor.dm:585
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        oneway = Dm.num(!Dm.truthy(oneway));
        to_chat(user, span_notice(I18n.T("You set [] to [] configuration.", this, Dm.truthy(oneway) ? I18n.T("one way") : I18n.T("default"))));
        return TRUE;
    }

    // tg: code/modules/recycling/conveyor.dm:591
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 50);
        invert_icon = Dm.num(!Dm.truthy(invert_icon));
        update_appearance();
        to_chat(user, span_notice(I18n.T("You set [] to [] position.", this, Dm.truthy(invert_icon) ? I18n.T("inverted") : I18n.T("normal"))));
        return TRUE;
    }

    // tg: code/modules/recycling/conveyor.dm:598
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("[] is set to [] configuration. It can be changed with a <b>screwdriver</b>.", this, Dm.truthy(oneway) ? I18n.T("one way") : I18n.T("default"))));
        Dm.append(ref dot, span_notice(I18n.T("[] is set to [] position. It can be rotated with a <b>wrench</b>.", this, Dm.truthy(invert_icon) ? I18n.T("inverted") : I18n.T("normal"))));
        Dm.append(ref dot, span_notice(I18n.T("[] is set to move [] seconds per belt. It can be changed with a <b>multitool</b>.", this, conveyor_speed)));
        return dot;
    }
}

// tg: code/modules/recycling/conveyor.dm:605
public partial class obj_machinery_conveyor_switch_oneway : obj_machinery_conveyor_switch
{
    // tg: code/modules/recycling/conveyor.dm:610
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dir, NORTH) || Dm.eq(dir, WEST))
            invert_icon = TRUE;
        return dot;
    }
}

// tg: code/modules/recycling/conveyor.dm:615
public partial class obj_item_conveyor_switch_construct : obj_item
{
    // tg: code/modules/recycling/conveyor.dm:625
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        id = Dm.text("[]", rand()); //this couldn't possibly go wrong
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:629
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        foreach (obj_item_stack_conveyor belt in Dm.each<obj_item_stack_conveyor>(view()))
            belt.id = id;
        to_chat(user, span_notice(I18n.T("You have linked all nearby conveyor belt assemblies to this switch.")));
        return null;
    }

    // tg: code/modules/recycling/conveyor.dm:634
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isfloorturf(interacting_with))
            return NONE;

        bool found = false;
        foreach (obj_machinery_conveyor belt in Dm.each<obj_machinery_conveyor>(view()))
        {
            if (Dm.eq(belt.id, id))
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            // [icon2html(src, user)] before the text: the assembly's picture in the chat line
            Dm.Deferred("code/modules/recycling/conveyor.dm:644", "interact_with_atom: icon2html(src, user), the assembly's icon in the chat line (chat icons, not-translated.md 76)");
            to_chat(user, span_notice(I18n.T("The conveyor switch did not detect any linked conveyor belts in range.")));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_machinery_conveyor_switch built_switch = Dm.@new<obj_machinery_conveyor_switch>(interacting_with, id);
        transfer_fingerprints_to(built_switch);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/recycling/conveyor.dm:651
public partial class obj_item_stack_conveyor : obj_item_stack
{
    // tg: code/modules/recycling/conveyor.dm:664
    [DmParams("mapload", "new_amount", "merge", "mat_override", "mat_amt", "_id")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_amount = Dm.arg(__extra, 0);
        object merge = Dm.arg(__extra, 1);
        DmList mat_override = Dm.arg<DmList>(__extra, 2);
        object mat_amt = Dm.arg(__extra, 3);
        object _id = Dm.arg(__extra, 4);
        merge ??= TRUE;
        mat_amt ??= 1;
        object dot = base.Initialize(mapload, Dm.forward(__extra, new_amount, merge, mat_override, mat_amt, _id));
        id = (string)_id;
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:668
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isfloorturf(interacting_with))
            return NONE;
        object belt_dir = get_dir(interacting_with, user);
        if (interacting_with == user.loc)
        {
            to_chat(user, span_warning(I18n.T("You cannot place a conveyor belt under yourself!")));
            return ITEM_INTERACT_BLOCKING;
        }
        obj_machinery_conveyor belt = Dm.@new<obj_machinery_conveyor>(interacting_with, belt_dir, id);
        transfer_fingerprints_to(belt);
        await use(1);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:680
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_conveyor_switch_construct)))
            return NONE;
        to_chat(user, span_notice(I18n.T("You link the switch to the conveyor belt assembly.")));
        obj_item_conveyor_switch_construct switch_construct = (obj_item_conveyor_switch_construct)tool;
        id = switch_construct.id;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/conveyor.dm:688
    public override object update_weight()
    {
        return FALSE;
    }

    // tg: code/modules/recycling/conveyor.dm:691
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Use a conveyor switch assembly on this before placing to connect to a lever.")));
        return dot;
    }

    // tg: code/modules/recycling/conveyor.dm:695
    [DmParams("used", "transfer", "check")]
    public override async DmTask<object> use(object used = null, params object[] __extra)
    {
        object dot = await base.use(used, __extra);
        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 30, TRUE);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/recycling/conveyor.dm:61  var/static/list/loc_connections
    internal DmList obj_machinery_conveyor__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_machinery_conveyor.conveyable_exit)),
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_conveyor.conveyable_enter)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(obj_machinery_conveyor.conveyable_enter)));

    // tg: code/modules/recycling/conveyor.dm:292  var/static/list/unconveyables
    internal DmList obj_machinery_conveyor__start_conveying__unconveyables;
}

// tg: code/modules/recycling/conveyor.dm:9  GLOBAL_LIST_EMPTY(conveyors_by_id)
public partial class datum_controller_global_vars
{
    public DmList conveyors_by_id;

    public virtual void InitGlobalconveyors_by_id()
    {
        conveyors_by_id = Dm.list();
        Dm.append(ref gvars_datum_init_order, "conveyors_by_id");
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/modules/recycling/conveyor.dm:549
public partial class obj_machinery_conveyor_switch : obj_machinery
{
    // tg: code/modules/recycling/conveyor.dm:549
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_hand(user);
    }

    // tg: code/modules/recycling/conveyor.dm:552
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }

    // tg: code/modules/recycling/conveyor.dm:555
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/modules/recycling/conveyor.dm:558
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user, modifiers);
    }
}
