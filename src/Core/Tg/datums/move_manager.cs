// Translated from tgstation (snapshot 1d99bd7f): code/datums/move_manager.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated (M4b slice 1: the zero-gravity drift runs on a move loop). Before it the W4.0 contracts declared
// /datum/movement_packet and its processing_move_loop_flags (CHECK_MOVE_LOOP_FLAGS, __DEFINES/movement.cs).

/**
 * Acts as a namespace for movement packet/type related procs
 *
 * Exists to provide an in code implementation of movement looping
 * Replaces things like walk() or walk_to(), among others
 *
 * Because we're doing things in engine, we have a lot more control over how different operations are performed
 * We also get more say in when things happen, so we can subject movements to the whims of the master controller
 * Rather then using a fuck ton of cpu just moving mobs or meteors
 *
 * The goal is to keep the loops themselves reasonably barebone, and implement more advanced behavior and control via the signals
 *
 * This may be bypassed in cases where snowflakes are nessesary, or where performance is important. S not a hard and fast thing
 *
 * Every atom can have a movement packet, which contains information and behavior about currently active loops, and queuing info
 * Loops control how movement actually happens. So there's a "move in this direction" loop, a "move randomly" loop
 *
 * You can find the logic for this control in this file
 *
 * Specifics of how different loops operate can be found in the movement_types.dm file, alongside the [add to loop][/datum/move_manager/proc/add_to_loop] helper procs that use them
 *
**/
// tg: code/datums/move_manager.dm:22
public partial class datum_move_manager : datum
{
    ///Adds a movable thing to a movement subsystem. Returns TRUE if it all worked, FALSE if it failed somehow
    // tg: code/datums/move_manager.dm:27  (the arguments after extra_info are the loop type's setup arguments: rules 7.6,
    // args.Copy(2) is subsystem, loop_type, priority, flags, extra_info and __extra)
    public virtual object add_to_loop(atom_movable thing_to_add = null, datum_controller_subsystem_movement subsystem = null, Type loop_type = null, float? priority = null, object flags = null, datum extra_info = null, params object[] __extra)
    {
        subsystem ??= SSmovement;
        priority ??= MOVEMENT_DEFAULT_PRIORITY;
        datum_movement_packet our_data = thing_to_add.move_packet;
        if (our_data == null)
            our_data = Dm.@new<datum_movement_packet>(thing_to_add);

        return our_data.add_loop(subsystem, loop_type, priority, flags, extra_info, __extra);
    }

    ///Returns the subsystem's loop if we're processing on it, null otherwise
    // tg: code/datums/move_manager.dm:36
    public virtual datum_move_loop processing_on(atom_movable packet_owner = null, datum_controller_subsystem_movement subsystem = null)
    {
        datum_movement_packet packet = packet_owner.move_packet;
        if (packet == null)
            return null;
        datum_move_loop linked_loop = packet.existing_loops[subsystem] as datum_move_loop;
        if (linked_loop == null)
            return null;
        if (Dm.truthy(Dm.bitand(linked_loop.flags, MOVEMENT_LOOP_IGNORE_PRIORITY)))
            return linked_loop;
        if (linked_loop != packet.running_loop)
            return null;
        return linked_loop;
    }
}

// tg: code/datums/move_manager.dm:24  GLOBAL_DATUM_INIT(move_manager, /datum/move_manager, new)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/datums/move_manager.dm:24  (GLOBAL_RAW(/datum/move_manager/move_manager), __DEFINES/__globals.dm:32)
    public datum_move_manager move_manager;

    // tg: code/datums/move_manager.dm:24  (GLOBAL_MANAGED(move_manager, new), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmove_manager()
    {
        move_manager = Dm.@new<datum_move_manager>();
        Dm.append(ref gvars_datum_init_order, "move_manager");
    }
}

///A packet of information that describes the current state of a moving object
// tg: code/datums/move_manager.dm:51  (the other vars are generated, D111)
[DmPath("/datum/movement_packet")]
public partial class datum_movement_packet : datum
{
    /**
     * Flags passed from the move loop before it calls move() and unset right after.
     * Allows for properties of a move loop to be easily checked by mechanics outside of it.
     * Having this a bitfield rather than a type var means we don't get screwed over
     * if the move loop gets deleted mid-move, FYI.
     */
    // tg: code/datums/move_manager.dm:62
    public float processing_move_loop_flags = NONE;

    // tg: code/datums/move_manager.dm:66
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable parent = Dm.arg<atom_movable>(__extra, 0);
        this.parent = parent;
        parent.move_packet = this;
    }

    // tg: code/datums/move_manager.dm:70
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        parent.move_packet = null;
        parent = null;
        foreach (datum_controller_subsystem processor in Dm.each<datum_controller_subsystem>(existing_loops))
        {
            datum_move_loop loop = existing_loops[processor] as datum_move_loop;
            if (QDELETED(loop))
                continue;
            qdel(loop);
        }
        existing_loops.Cut();
        existing_loops = null; //Catch anyone modifying this post del
        return base.Destroy(force);
    }

    ///Adds a loop to our parent. Returns the created loop if a success, null otherwise
    // tg: code/datums/move_manager.dm:83  (args.Copy(2) and args.Copy(6): the loop type's arguments, rules 7.6)
    public virtual datum_move_loop add_loop(datum_controller_subsystem_movement subsystem = null, Type loop_type = null, float? priority = null, object flags = null, datum extra_info = null, params object[] __extra)
    {
        datum_move_loop existing_loop = existing_loops[subsystem] as datum_move_loop;

        if (existing_loop != null && existing_loop.priority > Dm.num(priority))
        {
            if (!Dm.truthy(Dm.bitand(existing_loop.flags, MOVEMENT_LOOP_IGNORE_PRIORITY)) && !Dm.truthy(Dm.bitand(flags, MOVEMENT_LOOP_IGNORE_PRIORITY)))
                return null; //Give up
        }

        if (existing_loop != null && Dm.truthy(existing_loop.compare_loops(loop_type, priority, flags, extra_info, Dm.arg<object>(__extra, 0), Dm.arg<object>(__extra, 1), __extra == null || __extra.Length <= 2 ? new object[1] : __extra[2..])))
            return existing_loop; //it already exists stop trying to make the same moveloop

        datum_move_loop new_loop = (datum_move_loop)Dm.@new(loop_type, this, subsystem, parent, priority, flags, extra_info); //Pass the mob to move and ourselves in via new

        object worked_out = new_loop.setup(Dm.arg<object>(__extra, 0), Dm.arg<object>(__extra, 1), __extra == null || __extra.Length <= 2 ? new object[1] : __extra[2..]); //Here goes the rest
        if (!Dm.truthy(worked_out))
        {
            qdel(new_loop);
            return null;
        }

        existing_loops[subsystem] = new_loop;
        if (existing_loop != null)
            qdel(existing_loop); //We need to do this here because otherwise the packet would think it was empty, and self destruct
        contest_running_loop(new_loop);
        return new_loop;
    }

    ///Attempts to contest the current running move loop. Returns TRUE if the loop is active, FALSE otherwise
    // tg: code/datums/move_manager.dm:106
    public virtual object contest_running_loop(datum_move_loop contestant = null)
    {
        datum_controller_subsystem_movement contesting_subsystem = contestant.controller;

        if (Dm.truthy(Dm.bitand(contestant.flags, MOVEMENT_LOOP_IGNORE_PRIORITY)))
        {
            contesting_subsystem.add_loop(contestant);
            return TRUE;
        }
        if (running_loop == null)
        {
            running_loop = contestant;
            contesting_subsystem.add_loop(running_loop);
            return TRUE;
        }
        if (running_loop.priority > contestant.priority)
            return FALSE;

        datum_controller_subsystem_movement current_subsystem = running_loop.controller;

        datum_move_loop current_running_loop = running_loop;
        running_loop = contestant;
        current_subsystem.remove_loop(current_running_loop);
        if (running_loop != contestant) // A signal registrant could have messed with things
            return FALSE;
        contesting_subsystem.add_loop(contestant);
        return TRUE;
    }

    ///Tries to figure out the current favorite loop to run. More complex then just deciding between two different loops, assumes no running loop currently exists
    // tg: code/datums/move_manager.dm:130
    public virtual void decide_on_running_loop()
    {
        if (running_loop != null)
            return;
        if (!Dm.truthy(length(existing_loops))) //Die
        {
            qdel(this);
            return;
        }
        datum_move_loop favorite = null;
        foreach (datum_controller_subsystem_movement owner in Dm.each<datum_controller_subsystem_movement>(existing_loops))
        {
            datum_move_loop checking = existing_loops[owner] as datum_move_loop;
            if (Dm.truthy(Dm.bitand(checking.flags, MOVEMENT_LOOP_IGNORE_PRIORITY)))
                continue;
            if (favorite != null && favorite.priority > checking.priority)
                continue;
            favorite = checking;
        }

        if (favorite == null) //This isn't an error state, since some loops ignore the concept of a running loop
            return;

        datum_controller_subsystem_movement favorite_subsystem = favorite.controller;

        running_loop = favorite;
        favorite_subsystem.add_loop(running_loop);
    }

    // tg: code/datums/move_manager.dm:154
    public virtual void remove_loop(datum_controller_subsystem_movement remove_from = null, datum_move_loop loop_to_remove = null)
    {
        if (loop_to_remove == running_loop)
        {
            running_loop = null;
            remove_from.remove_loop(loop_to_remove);
        }
        if (Dm.truthy(Dm.bitand(loop_to_remove.flags, MOVEMENT_LOOP_IGNORE_PRIORITY)))
            remove_from.remove_loop(loop_to_remove);
        if (QDELETED(this))
            return;
        if (existing_loops[remove_from] == loop_to_remove)
            Dm.remove(ref existing_loops, remove_from);
        decide_on_running_loop();
        return;
    }

    // tg: code/datums/move_manager.dm:167
    public virtual object remove_subsystem(datum_controller_subsystem_movement remove = null)
    {
        datum_move_loop our_loop = existing_loops[remove] as datum_move_loop;
        if (our_loop == null)
            return FALSE;
        qdel(our_loop);
        return TRUE;
    }
}
