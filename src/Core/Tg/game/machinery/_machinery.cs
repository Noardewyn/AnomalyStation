// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/_machinery.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the machine's life cycle and power (M3 P3, scope 2g): New, Initialize, LateInitialize and
// post_machine_initialize, Destroy, the area power relationship, processing, the machine_stat, is_operational and panel
// setters, the power usage procs, get_room_area, can_be_occupant, set_occupant, Exited and clear_components; M4c slice 4
// (the suit storage unit): open_machine, dump_inventory_contents, try_put_in_hand, close_machine, can_crowbar_pry_open and
// default_pry_open; M5 slice 7 (machine frames, D148): the circuit board and its default parts (New's board, Initialize),
// RefreshParts, the default screwdriver, crowbar and wrench, spawn_frame, on_construction, Exited's deconstruct; M5 slice 6
// (airlocks): dump_contents, handle_deconstruct, atom_break and the base on_deconstruction. The other procs of the file stay Dm.NotTranslated with their
// milestone: the UIs and interactions (M4a, M9), the occupant machines (M4a), exchange_parts (the part replacer, M9). EMP (emp_act, M7 EMP slice) is translated; its language tail is M9. The M7 zaps slice: zap_act. The var block is generated (D111).

// M5 slice 7 (machine frames, D148): the circuit board instance (GAP(types): /obj/machinery/var/circuit is generated as a
// Type, rules 3.2 V3; tg keeps the typepath there until Initialize and the board itself after it).
public partial class obj_machinery
{
    /// The machine's circuit board (tg's `circuit` once it holds the board instance).
    public obj_item_circuitboard circuit_value;
}

// tg: code/game/machinery/_machinery.dm:87
[DmPath("/obj/machinery")]
public partial class obj_machinery : obj
{

    ///Needed by machine frame & flatpacker i.e the named arg board
    // tg: code/game/machinery/_machinery.dm:166
    [DmParams("location", "board")]
    public override async DmTask New(params object[] __extra)
    {
        object location = Dm.arg(__extra, 0);
        // The second argument is only a hint-typed `obj/item/circuitboard/board` in DM: any other value (a windoor's set_dir)
        // is passed on unchanged by the zero-argument `..()` below, so it is read untyped first.
        object board_arg = Dm.arg(__extra, 1);
        obj_item_circuitboard board = board_arg as obj_item_circuitboard;
        if (board is obj_item_circuitboard)
        {
            // GAP(types): /obj/machinery/var/circuit is generated as a Type (rules 3.2 V3), which a board instance does not fit:
            // the board instance is circuit_value (M5 slice 7), circuit keeps the board's type.
            circuit_value = board;
            circuit = board.GetType();
            //we don't want machines that override Initialize() have the board passed as a param e.g. atmos
            await base.New(location);
            return;
        }

        await base.New(Dm.forward(__extra, location, board_arg));
    }

    // tg: code/game/machinery/_machinery.dm:174
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it.
        SSmachines?.register_machine(this);

        // GAP(types): circuit is generated as a Type (rules 3.2 V3); the board instance is circuit_value (M5 slice 7). A board
        // passed to New is already there; else, as tg's `circuit = new circuit(src)`, one is made from the type.
        if (circuit_value == null && ispath(circuit, typeof(obj_item_circuitboard)))
            circuit_value = Dm.@new(circuit, this) as obj_item_circuitboard;
        if (circuit_value is obj_item_circuitboard)
        {
            // D132 (DV27): an inert board's own apply_default_parts is not translated; its machine gets no parts from it.
            if (!InertSeam.IsInert(circuit_value))
                Waiting.NoSleep(circuit_value.apply_default_parts(this));
        }

        if (Dm.truthy(Dm.bitand(processing_flags, START_PROCESSING_ON_INIT)))
            begin_processing();

        if (Dm.truthy(occupant_typecache))
            occupant_typecache = typecacheof(occupant_typecache) as DmList;

        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)) && Dm.truthy(component_parts)) // This is needed to prevent indestructible machinery still blowing up. If an explosion occurs on the same tile as the indestructible machinery without the PREVENT_CONTENTS_EXPLOSION_1 flag, /datum/controller/subsystem/explosions/proc/propagate_blastwave will call ex_act on all movable atoms inside the machine, including the circuit board and component parts. However, if those parts get deleted, the entire machine gets deleted, allowing for INDESTRUCTIBLE machines to be destroyed. (See #62164 for more info)
            Dm.combine(ref flags_1, PREVENT_CONTENTS_EXPLOSION_1);

        if (Dm.truthy(critical_machine))
            Waiting.NoSleep(AddElement(typeof(datum_element_block_area_power_fail)));

        if (HAS_TRAIT(SSstation, STATION_TRAIT_MACHINES_GLITCHED) && Dm.truthy(mapload))
            randomize_language_if_on_station();

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NEW_MACHINE, src) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NEW_MACHINE]))
            SSdcs._SendSignal(COMSIG_GLOB_NEW_MACHINE, Dm.list(SSdcs, this));

        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/_machinery.dm:202
    // set waitfor = FALSE (inherited from /atom/LateInitialize): this body may sleep through post_machine_initialize, so it
    // is the wrapper of waiting-model.md 5.4.
    [ShouldNotOverride]
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.WaitforFalse(LateInitialize__body(__extra));
    }

    private async DmTask LateInitialize__body(object[] __extra)
    {
        await post_machine_initialize();
    }

    /**
     * Called in LateInitialize meant to be the machine replacement to it
     * This sets up power for the machine and requires parent be called,
     * ensuring power works on all machines unless exempted with NO_POWER_USE.
     * This is the proc to override if you want to do anything in LateInitialize.
     */
    // tg: code/game/machinery/_machinery.dm:212
    [DmProtected, ShouldCallParent]
    public virtual async DmTask post_machine_initialize()
    {
        await find_and_mount_on_atom(late_init: TRUE);

        power_change();
        if (use_power == NO_POWER_USE)
            return;
        update_current_power_usage();
        setup_area_power_relationship();
    }


    // tg: code/game/machinery/_machinery.dm:225
    public override object Destroy(float? force = null)
    {
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it. An inert machine ran no
        // /obj/machinery/Initialize (types-converter.md 7), so it never registered and does not unregister (M5x slice 6: the
        // singularity eats inert machines, whose Destroy reaches here).
        if (!InertSeam.IsInert(this))
            SSmachines?.unregister_machine(this);
        end_processing();

        clear_components();
        unset_static_power();

        return base.Destroy(force);
    }

    /**
     * proc to call when the machine starts to require power after a duration of not requiring power
     * sets up power related connections to its area if it exists and becomes area sensitive
     * does not affect power usage itself
     *
     * Returns TRUE if it triggered a full registration, FALSE otherwise
     * We do this so machinery that want to sidestep the area sensitiveity optimization can
     */
    // tg: code/game/machinery/_machinery.dm:242
    public virtual object setup_area_power_relationship()
    {
        area our_area = get_area(this) as area;
        if (Dm.truthy(our_area))
            RegisterSignal(our_area, COMSIG_AREA_POWER_CHANGE, nameof(power_change));

        if (HAS_TRAIT_FROM(this, TRAIT_AREA_SENSITIVE, INNATE_TRAIT)) // If we for some reason have not lost our area sensitivity, there's no reason to set it back up
            return FALSE;

        become_area_sensitive(INNATE_TRAIT);
        RegisterSignal(this, COMSIG_ENTER_AREA, nameof(on_enter_area));
        RegisterSignal(this, COMSIG_EXIT_AREA, nameof(on_exit_area));
        return TRUE;
    }

    /**
     * proc to call when the machine stops requiring power after a duration of requiring power
     * saves memory by removing the power relationship with its area if it exists and loses area sensitivity
     * does not affect power usage itself
     */
    // tg: code/game/machinery/_machinery.dm:260
    public virtual void remove_area_power_relationship()
    {
        area our_area = get_area(this) as area;
        if (Dm.truthy(our_area))
            UnregisterSignal(our_area, COMSIG_AREA_POWER_CHANGE);

        if (Dm.truthy(always_area_sensitive))
            return;

        lose_area_sensitivity(INNATE_TRAIT);
        UnregisterSignal(this, COMSIG_ENTER_AREA);
        UnregisterSignal(this, COMSIG_EXIT_AREA);
    }

    // tg: code/game/machinery/_machinery.dm:272
    [SignalHandler]
    public virtual void on_enter_area(datum source = null, area area_to_register = null)
    {
        // If we're always area sensitive, and this is called while we have no power usage, do nothing and return
        if (Dm.truthy(always_area_sensitive) && use_power == NO_POWER_USE)
            return;
        update_current_power_usage();
        power_change();
        RegisterSignal(area_to_register, COMSIG_AREA_POWER_CHANGE, nameof(power_change));
    }

    // tg: code/game/machinery/_machinery.dm:281
    [SignalHandler]
    public virtual void on_exit_area(datum source = null, area area_to_unregister = null)
    {
        // If we're always area sensitive, and this is called while we have no power usage, do nothing and return
        if (Dm.truthy(always_area_sensitive) && use_power == NO_POWER_USE)
            return;
        unset_static_power();
        UnregisterSignal(area_to_unregister, COMSIG_AREA_POWER_CHANGE);
    }

    // tg: code/game/machinery/_machinery.dm:289
    [ShouldCallParent]
    public virtual void set_occupant(atom_movable new_occupant = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_SET_OCCUPANT]))
            this._SendSignal(COMSIG_MACHINERY_SET_OCCUPANT, Dm.list(this, new_occupant));
        occupant = new_occupant;
    }

    /// Helper proc for telling a machine to start processing
    // tg: code/game/machinery/_machinery.dm:296
    public virtual void begin_processing()
    {
        // var/datum/controller/subsystem/processing/subsystem: object (rules 3.4, wider stored values: SSmachines, the
        // default subsystem_type, is no processing subsystem).
        object subsystem = Dm.locate_type(subsystem_type, Master?.subsystems);
        // rules 10.6: tg's Master holds the machine's subsystem before the map; a test harness world without the Master
        // has none to process on (a booted world always has it).
        if (subsystem == null)
            return;
        START_PROCESSING(subsystem, this);
    }

    /// Helper proc for telling a machine to stop processing
    // tg: code/game/machinery/_machinery.dm:301
    public virtual void end_processing()
    {
        // object: as in begin_processing (rules 3.4)
        object subsystem = Dm.locate_type(subsystem_type, Master?.subsystems);
        // rules 10.6: as in begin_processing
        if (subsystem == null)
            return;
        STOP_PROCESSING(subsystem, this);
    }

    ///Early process for machines added to SSmachines.processing_early to prioritize power draw
    // tg: code/game/machinery/_machinery.dm:306
    // set waitfor = FALSE: this body cannot sleep, so it needs no wrapper (waiting-model.md 5.4).
    public virtual object process_early(params object[] __extra)
    {
        return PROCESS_KILL;
    }

    // tg: code/game/machinery/_machinery.dm:310
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra) //If you dont use process or power why are you here
    {
        return PROCESS_KILL;
    }

    ///Late process for machines added to SSmachines.processing_late to gather accurate recordings
    // tg: code/game/machinery/_machinery.dm:314
    // set waitfor = FALSE: this body cannot sleep, so it needs no wrapper (waiting-model.md 5.4).
    public virtual object process_late()
    {
        return PROCESS_KILL;
    }

    /**
     * Process but for machines interacting with atmospherics.
     * Like process, anything sensitive to changes in the wait time between process ticks should account for seconds_per_tick.
    **/
    // tg: code/game/machinery/_machinery.dm:322
    // set waitfor = FALSE: this body cannot sleep, so it needs no wrapper (waiting-model.md 5.4).
    public virtual object process_atmos(object seconds_per_tick = null) //If you dont touch atmos why are you here
    {
        return PROCESS_KILL;
    }

    ///Called when we want to change the value of the machine_stat variable. Holds bitflags.
    // tg: code/game/machinery/_machinery.dm:327
    [ShouldNotOverride]
    public virtual async DmTask<object> set_machine_stat(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, machine_stat))
            return dot;
        dot = machine_stat;
        machine_stat = Dm.num(new_value);
        await on_set_machine_stat(dot);
        return dot;
    }


    ///Called when the value of `machine_stat` changes, so we can react to it.
    // tg: code/game/machinery/_machinery.dm:338
    [DmProtected]
    public virtual async DmTask on_set_machine_stat(object old_value = null)
    {
        //From off to on.
        if (Dm.truthy(Dm.bitand(old_value, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))) && !Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))))
        {
            await set_is_operational(TRUE);
            return;
        }
        //From on to off.
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(Dm.bitor(NOPOWER, BROKEN), MAINT))))
            await set_is_operational(FALSE);
    }

    // tg: code/game/machinery/_machinery.dm:350
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (!Dm.truthy(use_power) || Dm.truthy(machine_stat) || Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        await use_energy(7.5f * KILO * JOULES / Dm.num(severity));
        Dm.@new<obj_effect_temp_visual_emp>(loc);

        if (!prob(70 / Dm.num(severity)))
            return dot;
        if (!Dm.truthy(length(GLOB.uncommon_roundstart_languages)))
            return dot;
        if (QDELETED(this))
            return dot; // DEVIATION(DV594): a machine being deleted keeps its languages (tg's get_language_holder CRASHes on a QDELING atom)
        remove_all_languages(source: LANGUAGE_EMP);
        grant_random_uncommon_language(source: LANGUAGE_EMP);
        return dot;
    }

    /**
     * Opens the machine.
     *
     * Will update the machine icon and any user interfaces currently open.
     * Arguments:
     * * drop - Boolean. Whether to drop any stored items in the machine. Does not include components.
     * * density - Boolean. Whether to make the object dense when it's open.
     */
    // tg: code/game/machinery/_machinery.dm:372
    public virtual async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        state_open = TRUE;
        set_density(density_to_set);
        if (Dm.truthy(drop))
            await dump_inventory_contents();
        update_appearance();
        return null;
    }

    /**
     * Drop every movable atom in the machine's contents list, including any components and circuit.
     */
    // tg: code/game/machinery/_machinery.dm:382
    public override async DmTask dump_contents(params object[] __extra)
    {
        // Start by calling the dump_inventory_contents proc. Will allow machines with special contents
        // to handle their dropping.
        await dump_inventory_contents();

        // Then we can clean up and drop everything else.
        turf this_turf = get_turf(this);
        foreach (atom_movable movable_atom in Dm.each<atom_movable>(contents.Copy()))
            await movable_atom.forceMove(this_turf);

        // We'll have dropped the occupant, circuit and component parts as part of this.
        set_occupant(null);
        circuit = null;
        circuit_value = null; // GAP(types): the board instance (M5 slice 7)
        LAZYCLEARLIST(component_parts);
    }

    /**
     * Drop every movable atom in the machine's contents list that is not a component_part.
     *
     * Proc does not drop components and will skip over anything in the component_parts list.
     * Call dump_contents() to drop all contents including components.
     * Arguments:
     * * subset - If this is not null, only atoms that are also contained within the subset list will be dropped.
     */
    // tg: code/game/machinery/_machinery.dm:405
    public virtual async DmTask dump_inventory_contents(DmList subset = null)
    {
        turf this_turf = get_turf(this);
        foreach (atom_movable movable_atom in Dm.each<atom_movable>(contents))
        {
            //so machines like microwaves dont dump out signalers after cooking
            if (Dm.truthy(wires) && Dm.@in(movable_atom, assoc_to_values(wires.assemblies)))
                continue;

            if (Dm.truthy(subset) && !Dm.@in(movable_atom, subset))
                continue;

            if (Dm.@in(movable_atom, component_parts))
                continue;

            await movable_atom.forceMove(this_turf);

            if (occupant == movable_atom)
                set_occupant(null);
        }
    }

    /**
     * Puts passed object in to user's hand
     *
     * Puts the passed object in to the users hand if they are adjacent.
     * If the user is not adjacent then place the object on top of the machine.
     *
     * Vars:
     * * object (obj) The object to be moved in to the users hand.
     * * user (mob/living) The user to recive the object
     */
    // tg: code/game/machinery/_machinery.dm:433
    public virtual async DmTask try_put_in_hand(obj_item @object = null, mob_living user = null)
    {
        if (!issilicon(user) && in_range(this, user))
        {
            // tg passes the machine as the source, a turf parameter in the port (slots.tsv): its turf, the same tile and direction
            @object.do_pickup_animation(user, get_turf(this));
            await user.put_in_hands(@object);
        }
        else
            await @object.forceMove(drop_location() as atom);
    }

    // tg: code/game/machinery/_machinery.dm:440
    public virtual object can_be_occupant(atom_movable occupant_atom = null)
    {
        return (Dm.truthy(occupant_typecache) ? is_type_in_typecache(occupant_atom, occupant_typecache) : Dm.num(isliving(occupant_atom)));
    }

    // tg: code/game/machinery/_machinery.dm:443
    public virtual async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        state_open = FALSE;
        set_density(density_to_set);
        if (!Dm.truthy(density))
        {
            update_appearance();
            return null;
        }

        if (!Dm.truthy(target))
        {
            foreach (object atom in Dm.each(loc as atom))
            {
                if (!Dm.truthy(can_be_occupant(atom as atom_movable)))
                    continue;
                atom_movable current_atom = atom as atom_movable;
                if (Dm.truthy(current_atom.has_buckled_mobs()))
                    continue;
                if (isliving(current_atom))
                {
                    mob_living current_mob = atom as mob_living;
                    if (Dm.truthy(current_mob.buckled) || current_mob.mob_size >= MOB_SIZE_LARGE)
                        continue;
                }
                target = atom as atom_movable;
            }
        }

        mob_living mobtarget = target as mob_living;
        if (Dm.truthy(target) && !Dm.truthy(target.has_buckled_mobs()) && (!isliving(target) || !Dm.truthy(mobtarget.buckled)))
        {
            set_occupant(target);
            await target.forceMove(this);
        }
        update_appearance();
        return null;
    }

    ///updates the use_power var for this machine and updates its static power usage from its area to reflect the new value
    // tg: code/game/machinery/_machinery.dm:470
    [ShouldCallParent]
    public virtual object update_use_power(object new_use_power = null)
    {
        if (Dm.eq(new_use_power, use_power))
            return FALSE;

        unset_static_power();

        float new_usage = 0;
        object sw__1 = new_use_power;
        if (Dm.eq(sw__1, IDLE_POWER_USE))
            new_usage = idle_power_usage;
        else if (Dm.eq(sw__1, ACTIVE_POWER_USE))
            new_usage = active_power_usage;

        if (use_power == NO_POWER_USE)
            setup_area_power_relationship();
        else if (Dm.eq(new_use_power, NO_POWER_USE))
            remove_area_power_relationship();

        static_power_usage = new_usage;

        if (Dm.truthy(new_usage))
        {
            area our_area = get_area(this) as area;
            our_area?.addStaticPower(new_usage, DYNAMIC_TO_STATIC_CHANNEL(power_channel));
        }

        use_power = Dm.num(new_use_power);

        if (Dm.truthy(use_power))
            power_change();

        return TRUE;
    }

    ///updates the power channel this machine uses. removes the static power usage from the old channel and readds it to the new channel
    // tg: code/game/machinery/_machinery.dm:503
    [ShouldCallParent]
    public virtual object update_power_channel(object new_power_channel = null)
    {
        if (Dm.eq(new_power_channel, power_channel))
            return FALSE;

        object usage = unset_static_power();

        area our_area = get_area(this) as area;

        if (Dm.truthy(our_area) && Dm.truthy(usage))
            our_area.addStaticPower(usage, DYNAMIC_TO_STATIC_CHANNEL(Dm.num(new_power_channel)));

        power_channel = Dm.num(new_power_channel);

        return TRUE;
    }

    ///internal proc that removes all static power usage from the current area
    // tg: code/game/machinery/_machinery.dm:520
    [ShouldNotOverride]
    public virtual object unset_static_power()
    {
        float old_usage = static_power_usage;

        area our_area = get_area(this) as area;

        if (Dm.truthy(our_area) && Dm.truthy(old_usage))
        {
            our_area.removeStaticPower(old_usage, DYNAMIC_TO_STATIC_CHANNEL(power_channel));
            static_power_usage = 0;
        }

        return old_usage;
    }

    /**
     * sets the power_usage linked to the specified use_power_mode to new_usage
     * e.g. update_mode_power_usage(ACTIVE_POWER_USE, 10) sets active_power_use = 10 and updates its power draw from the machines area if use_power == ACTIVE_POWER_USE
     *
     * Arguments:
     * * use_power_mode - the use_power power mode to change. if IDLE_POWER_USE changes idle_power_usage, ACTIVE_POWER_USE changes active_power_usage
     * * new_usage - the new value to set the specified power mode var to
     */
    // tg: code/game/machinery/_machinery.dm:541
    [ShouldCallParent]
    public virtual object update_mode_power_usage(object use_power_mode = null, object new_usage = null)
    {
        if (Dm.eq(use_power_mode, NO_POWER_USE))
        {
            stack_trace("trying to set the power usage associated with NO_POWER_USE in update_mode_power_usage()!", "code/game/machinery/_machinery.dm", 544);
            return FALSE;
        }

        unset_static_power(); //completely remove our static_power_usage from our area, then readd new_usage

        object sw__1 = use_power_mode;
        if (Dm.eq(sw__1, IDLE_POWER_USE))
            idle_power_usage = Dm.num(new_usage);
        else if (Dm.eq(sw__1, ACTIVE_POWER_USE))
            active_power_usage = Dm.num(new_usage);

        if (Dm.eq(use_power_mode, use_power))
            static_power_usage = Dm.num(new_usage);

        area our_area = get_area(this) as area;

        if (Dm.truthy(our_area))
            our_area.addStaticPower(static_power_usage, DYNAMIC_TO_STATIC_CHANNEL(power_channel));

        return TRUE;
    }

    ///Get a valid powered area to reference for power use, mainly for wall-mounted machinery that isn't always mapped directly in a powered location.
    // tg: code/game/machinery/_machinery.dm:566
    public virtual object get_room_area()
    {
        area machine_area = get_area(this) as area;
        if (machine_area == null)
            return null; // ??

        // check our own loc first to see if its a powered area
        if (!Dm.truthy(machine_area.always_unpowered))
            return machine_area;

        // loc area wasn't good, checking adjacent wall for a good area to use
        turf mounted_wall = get_step(this, dir);
        if (isclosedturf(mounted_wall))
        {
            area wall_area = get_area(mounted_wall) as area;
            if (!Dm.truthy(wall_area.always_unpowered))
                return wall_area;
        }

        // couldn't find a proper powered area on loc or adjacent wall, defaulting back to loc and blaming mappers
        return machine_area;
    }

    ///makes this machine draw power from its area according to which use_power mode it is set to
    // tg: code/game/machinery/_machinery.dm:586
    public virtual object update_current_power_usage()
    {
        if (Dm.truthy(static_power_usage))
            unset_static_power();

        area our_area = get_area(this) as area;
        if (!Dm.truthy(our_area))
            return FALSE;

        float sw__1 = use_power;
        if (sw__1 == IDLE_POWER_USE)
            static_power_usage = idle_power_usage;
        else if (sw__1 == ACTIVE_POWER_USE)
            static_power_usage = active_power_usage;
        else if (sw__1 == NO_POWER_USE)
            return null;

        if (Dm.truthy(static_power_usage))
            our_area.addStaticPower(static_power_usage, DYNAMIC_TO_STATIC_CHANNEL(power_channel));

        return TRUE;
    }

    ///Called when we want to change the value of the `is_operational` variable. Boolean.
    // tg: code/game/machinery/_machinery.dm:608
    [ShouldNotOverride]
    public virtual async DmTask<object> set_is_operational(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(new_value, is_operational))
            return dot;
        dot = is_operational;
        is_operational = Dm.num(new_value);
        await on_set_is_operational(dot);
        return dot;
    }


    ///Called when the value of `is_operational` changes, so we can react to it.
    // tg: code/game/machinery/_machinery.dm:619
    [DmProtected]
    public virtual async DmTask on_set_is_operational(object old_value = null)
    {
        return;
    }

    ///Called when we want to change the value of the `panel_open` variable. Boolean.
    // tg: code/game/machinery/_machinery.dm:625
    [ShouldNotOverride]
    public virtual void set_panel_open(object new_value = null)
    {
        if (Dm.eq(panel_open, new_value))
            return;
        float old_value = panel_open;
        panel_open = Dm.num(new_value);
        on_set_panel_open(old_value);
        update_appearance();
        // if this is a machine that cares about whether the panel is open for UIs, force an update
        if (Dm.truthy(Dm.bitand(interaction_flags_machine, Dm.bitor(INTERACT_MACHINE_OPEN_SILICON, INTERACT_MACHINE_OPEN))))
            SStgui?.update_uis(this); // rules 10.6: a test harness world may lack SStgui
    }

    ///Called when the value of `panel_open` changes, so we can react to it.
    // tg: code/game/machinery/_machinery.dm:639
    [DmProtected]
    public virtual void on_set_panel_open(object old_value = null)
    {
        return;
    }

    /// Toggles the panel_open var. Defined for convienience
    // tg: code/game/machinery/_machinery.dm:645
    [ShouldNotOverride]
    public virtual void toggle_panel_open()
    {
        set_panel_open(Dm.not(panel_open));
    }

    // tg: code/game/machinery/_machinery.dm:650
    public override async DmTask<object> can_interact(mob user = null, float? require_adjacent_turf = null)
    {
        if (QDELETED(user))
            return FALSE;

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))) && !Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_OFFLINE))) // Check if the machine is broken, and if we can still interact with it if so
            return FALSE;

        float try_use_signal = 0;
        if (Dm.truthy(user._listen_lookup?[COMSIG_TRY_USE_MACHINE]))
            try_use_signal = Dm.num(user._SendSignal(COMSIG_TRY_USE_MACHINE, Dm.list(user, this)));
        if (Dm.truthy(_listen_lookup?[COMSIG_TRY_USE_MACHINE]))
            try_use_signal = Dm.bitor(try_use_signal, Dm.num(_SendSignal(COMSIG_TRY_USE_MACHINE, Dm.list(this, user))));
        if (Dm.truthy(Dm.bitand(try_use_signal, COMPONENT_CANT_USE_MACHINE_INTERACT)))
            return FALSE;

        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet (as airlock.cs).
        bool isAdminGhostAI__gap = false;
        if (isAdminGhostAI__gap)
            return TRUE; //the Gods have unlimited power and do not care for things such as range or blindness

        if (user is not mob_living)
            return FALSE; //no ghosts allowed, sorry

        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178), with isAdminGhostAI as above
        bool silicon_access = HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap;
        if (!silicon_access && !Dm.truthy(user.can_hold_items()))
            return FALSE; //spiders gtfo

        if (silicon_access) // If we are a silicon, make sure the machine allows silicons to interact with it
        {
            if (!Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_ALLOW_SILICON)))
                return FALSE;

            if (Dm.truthy(panel_open) && !Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_OPEN)) && !Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_OPEN_SILICON)))
                return FALSE;

            return await user.can_interact_with(this); //AIs don't care about petty mortal concerns like needing to be next to a machine to use it, but borgs do care somewhat
        }

        object dot = await base.can_interact(user, require_adjacent_turf);
        if (!Dm.truthy(dot))
            return FALSE;

        if (Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_REQUIRES_SIGHT)) && Dm.truthy(user.is_blind()))
        {
            to_chat(user, span_warning(I18n.T("This machine requires sight to use.")));
            return FALSE;
        }

        // machines have their own lit up display screens and LED buttons so we don't need to check for light
        if (Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_REQUIRES_LITERACY)) && !Dm.truthy(user.can_read(this, READING_CHECK_LITERACY)))
            return FALSE;

        if (Dm.truthy(panel_open) && !Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_OPEN)))
            return FALSE;

        if (Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_REQUIRES_SILICON))) //if the user was a silicon, we'd have returned out earlier, so the user must not be a silicon
            return FALSE;

        if (Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_REQUIRES_STANDING)))
        {
            var living_user = (mob_living)user;
            if (!Dm.truthy(Dm.bitand(living_user.mobility_flags, MOBILITY_MOVE)))
                return FALSE;
        }

        return TRUE; // If we passed all of those checks, woohoo! We can interact with this machine.
    }

    // tg: code/game/machinery/_machinery.dm:732 (gaps package C5: a monkey's paw)
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        mob_living living_user = user as mob_living;
        if (!Dm.truthy(living_user.combat_mode))
            return await attack_hand(user);

        living_user.changeNext_move(CLICK_CD_MELEE);
        await living_user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        object damage = await take_damage(damage_amount: 4, damage_type: BRUTE, damage_flag: MELEE, sound_effect: TRUE, attack_dir: get_dir(user, this));

        object hit_with_what_noun = "paws";
        if (living_user.get_active_hand() is obj_item_bodypart_arm arm)
        {
            hit_with_what_noun = arm.appendage_noun; // hit with "their hand"
            if (Dm.num(living_user.usable_hands) > 1)
                hit_with_what_noun = Dm.text("[][]", hit_with_what_noun, plural_s(hit_with_what_noun)); // hit with "their hands"
        }

        string ending = Dm.truthy(damage) ? "." : Dm.text(", []!", no_damage_feedback);
        living_user.visible_message(
            span_danger(I18n.T("[] smashes [] with [] [][]", living_user, this, living_user.p_their(), hit_with_what_noun, ending)),
            span_danger(I18n.T("You smash [] with your [][]", this, hit_with_what_noun, ending)),
            span_hear(I18n.T("You hear a [].", Dm.truthy(damage) ? "smash" : "thud")),
            COMBAT_MESSAGE_RANGE);
        return TRUE;
    }

    // tg: code/game/machinery/_machinery.dm:762  (silicons SI4: the cyborg's and the AI's clicks on a machine)
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        // isAdminGhostAI: no admin ghosts yet (the GAP above)
        if (!Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_ALLOW_SILICON)))
            return FALSE;

        if (!Dm.truthy(await Adjacent(user)) || !Dm.truthy(can_buckle) || !Dm.truthy(has_buckled_mobs())) //so that borgs (but not AIs, sadly (perhaps in a future PR?)) can unbuckle people from machines
            return await _try_interact(user);

        if (length(buckled_mobs) <= 1)
        {
            if (Dm.truthy(await user_unbuckle_mob(buckled_mobs[1] as mob_living, user)))
                return TRUE;
        }

        object unbuckled = await tgui_input_list(user, "Who do you wish to unbuckle?", "Unbuckle", sort_names(buckled_mobs) as DmList);
        if (unbuckled == null)
            return FALSE;
        if (Dm.truthy(await user_unbuckle_mob(unbuckled as mob_living, user)))
            return TRUE;

        return await _try_interact(user);
    }

    // tg: code/game/machinery/_machinery.dm:781  (silicons SI4)
    public override async DmTask<object> attack_ai(mob user = null)
    {
        // isAdminGhostAI: no admin ghosts yet (the GAP above)
        if (!Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_ALLOW_SILICON)))
            return FALSE;
        if (!Dm.truthy(user.has_faction(ROLE_SYNDICATE)))
        {
            if (Dm.@in(ACCESS_SYNDICATE, req_access) || Dm.@in(ACCESS_SYNDICATE_LEADER, req_access) || Dm.@in(ACCESS_SYNDICATE, req_one_access) || Dm.@in(ACCESS_SYNDICATE_LEADER, req_one_access))
                return FALSE;
            if (Dm.truthy(onSyndieBase()) && loc != user)
                return FALSE;
        }
        if (iscyborg(user)) // For some reason attack_robot doesn't work
            return await attack_robot(user);
        return await _try_interact(user);
    }

    //Return a non FALSE value to interrupt attack_hand propagation to subtypes.
    // tg: code/game/machinery/_machinery.dm:707
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        update_last_used(user);
        return await base.interact(user, __extra);
    }

    // tg: code/game/machinery/_machinery.dm:711
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        mob user = ui.user;
        // tg: code/game/machinery/_machinery.dm:713
        add_fingerprint(user);
        update_last_used(user);
        if (isAI(user) && !SScameras.is_visible_by_cameras(get_turf(this))) //We check if they're an AI specifically here, so borgs/adminghosts/human wand can still access off-camera stuff.
        {
            to_chat(user, span_warning(I18n.T("You can no longer connect to this device!")));
            return FALSE;
        }
        return await base.ui_act(action, @params, ui, state);
    }

    // tg: code/game/machinery/_machinery.dm:793
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby(attacking_item, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        update_last_used(user);
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:799
    public override async DmTask<object> attackby_secondary(obj_item weapon = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attackby_secondary(weapon, user, modifiers, attack_modifiers);
        if (Dm.truthy(dot))
            return dot;
        update_last_used(user);
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:818
    public override async DmTask<object> _try_interact(mob user = null)
    {
        if (Dm.truthy(Dm.bitand(interaction_flags_machine, INTERACT_MACHINE_WIRES_IF_OPEN)) && Dm.truthy(panel_open))
        {
            if (Dm.eq(await attempt_wire_interaction(user), WIRE_INTERACTION_BLOCK))
                return TRUE;
        }
        if (Dm.truthy(user._listen_lookup?[COMSIG_TRY_USE_MACHINE])
            && Dm.truthy(Dm.bitand(Dm.num(user._SendSignal(COMSIG_TRY_USE_MACHINE, Dm.list(user, this))), COMPONENT_CANT_USE_MACHINE_INTERACT)))
            return TRUE;
        return await base._try_interact(user);
    }

    // tg: code/game/machinery/_machinery.dm:829
    [ShouldCallParent]
    public virtual async DmTask<object> RefreshParts()
    {
        //reset to baseline
        idle_power_usage = Dm.initial(this, (obj_machinery m) => m.idle_power_usage);
        active_power_usage = Dm.initial(this, (obj_machinery m) => m.active_power_usage);
        if (!Dm.truthy(component_parts) || !Dm.truthy(component_parts.len))
            return null;
        float parts_energy_rating = 0;

        foreach (datum_stock_part part in Dm.each<datum_stock_part>(component_parts))
            parts_energy_rating += Dm.num(part.energy_rating());

        foreach (obj_item_stock_parts part in Dm.each<obj_item_stock_parts>(component_parts))
            parts_energy_rating += Dm.num(part.energy_rating);

        idle_power_usage = Dm.initial(this, (obj_machinery m) => m.idle_power_usage) * (1 + parts_energy_rating);
        active_power_usage = Dm.initial(this, (obj_machinery m) => m.active_power_usage) * (1 + parts_energy_rating);
        update_current_power_usage();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_REFRESH_PARTS]))
            this._SendSignal(COMSIG_MACHINERY_REFRESH_PARTS, Dm.list(this));
        return null;
    }

    // tg: code/game/machinery/_machinery.dm:853
    [DmProtected]
    public virtual object can_crowbar_pry_open()
    {
        return Dm.num(!Dm.truthy(state_open) && !Dm.truthy(panel_open) && !Dm.truthy(is_operational));
    }

    // tg: code/game/machinery/_machinery.dm:872
    [DmProtected]
    public virtual async DmTask<object> default_pry_open(mob_living user = null, obj_item crowbar = null, float? close_after_pry = null, float? open_density = null, float? closed_density = null, float? deconstruct_on_fail = null)
    {
        close_after_pry ??= FALSE;
        open_density ??= FALSE;
        closed_density ??= TRUE;
        deconstruct_on_fail ??= FALSE;

        if (!Dm.eq(crowbar.tool_behaviour, TOOL_CROWBAR))
            return NONE;
        if (!Dm.truthy(can_crowbar_pry_open()))
            return Dm.truthy(deconstruct_on_fail) ? await default_deconstruction_crowbar(user, crowbar) : ITEM_INTERACT_BLOCKING;

        crowbar.play_tool_sound(this, 50);
        user.visible_message(span_notice(I18n.T("[] pries open [].", user, this)), span_notice(I18n.T("You pry open [].", this)));
        await open_machine(density_to_set: open_density);
        if (Dm.truthy(close_after_pry)) //Should it immediately close after prying? (If not, it must be closed elsewhere)
            await close_machine(density_to_set: closed_density);
        return ITEM_INTERACT_SUCCESS;
    }

    /**
     * Checks if the machine is in a state where it can be deconstructed with a crowbar,
     * which is used by the default crowbar deconstruction method.
     */
    // tg: code/game/machinery/_machinery.dm:898
    [DmProtected]
    public virtual object can_crowbar_deconstruct()
    {
        return panel_open;
    }

    /**
     * Default method of deconstructing a machine with a crowbar
     * Requires panel be open to work, unless ignore_panel is set to TRUE.
     *
     * * crowbar - The crowbar being used to deconstruct the machine.
     * You do not have to assert the crowbar is a crowbar, it is checked for you.
     *
     * Returns NONE on failure, or if custom_deconstruct is set to TRUE.
     * Returns ITEM_INTERACT_SUCCESS on success.
     */
    // tg: code/game/machinery/_machinery.dm:912
    [DmProtected]
    public virtual async DmTask<object> default_deconstruction_crowbar(mob_living user = null, obj_item crowbar = null, params object[] __extra)
    {
        if (!Dm.eq(crowbar.tool_behaviour, TOOL_CROWBAR))
            return NONE;
        if (!Dm.truthy(can_crowbar_deconstruct()))
            return ITEM_INTERACT_BLOCKING;

        crowbar.play_tool_sound(this, 50);
        // user.visible_message(span_notice("[user] deconstructs [src]."), span_notice("You deconstruct [src]."))
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/_machinery.dm:925
    [ShouldNotOverride]
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(Dm.bitand(obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION)))
        {
            await dump_inventory_contents(); //drop stuff we consider important
            return; //Just delete us, no need to call anything else.
        }

        await on_deconstruction(disassembled);

        // GAP(D112): an inert machine never ran Initialize, so its board is still the circuit type: it is made here, as
        // _machinery.dm:179's `circuit = new circuit(src)` would have, so the machine leaves its frame and board as in tg
        if (circuit_value == null && InertSeam.IsInert(this) && ispath(circuit, typeof(obj_item_circuitboard)))
        {
            circuit_value = Dm.@new(circuit, this) as obj_item_circuitboard;
            if (InertSeam.IsInert(circuit_value))
                component_parts = Dm.list(circuit_value); // circuitboard.dm:46, an inert board's own parts aside (D132)
            else
                await circuit_value.apply_default_parts(this);
        }
        // GAP(types): the board instance is circuit_value (circuit is generated as a Type)
        if (circuit_value != null)
            await spawn_frame(disassembled);

        if (!Dm.truthy(LAZYLEN(component_parts)))
        {
            await dump_contents(); //drop everything inside us
            return; //we don't have any parts.
        }

        foreach (object part in Dm.each(component_parts.Copy()))
        {
            if (part is datum_stock_part datum_part)
                Dm.@new(datum_part.physical_object_type, loc);
            else
            {
                obj_item obj_part = part as obj_item;
                Dm.remove(ref component_parts, part);
                await obj_part.forceMove(loc);
                if (obj_part is obj_item_circuitboard_machine board)
                {
                    foreach (object component in Dm.each(board.req_components)) //loop through all stack components and spawn them
                    {
                        if (!ispath(component, typeof(obj_item_stack)))
                            continue;
                        Type stack_path = (Type)component;
                        Dm.@new(stack_path, loc, board.req_components[component]);
                    }
                }
            }
        }
        LAZYCLEARLIST(component_parts);

        //drop everything inside us. we do this last to give machines a chance
        //to handle their contents before we dump them
        await dump_contents();
    }

    /**
     * Spawns a frame where this machine is. If the machine was not disassmbled, the
     * frame is spawned damaged. If the frame couldn't exist on this turf, it's smashed
     * down to metal sheets.
     *
     * Arguments:
     * * disassembled - If FALSE, the machine was destroyed instead of disassembled and the frame spawns at reduced integrity.
     */
    // tg: code/game/machinery/_machinery.dm:970
    public virtual async DmTask<object> spawn_frame(object disassembled = null)
    {
        obj_structure_frame_machine new_frame = Dm.@new<obj_structure_frame_machine>(loc);

        new_frame.state = FRAME_STATE_WIRED;

        // If the new frame shouldn't be able to fit here due to the turf being blocked, spawn the frame deconstructed.
        if (isturf(loc))
        {
            turf machine_turf = loc as turf;
            // We're spawning a frame before this machine is qdeleted, so we want to ignore it. We've also just spawned a new frame, so ignore that too.
            if (Dm.truthy(await machine_turf.is_blocked_turf(TRUE, source_atom: new_frame, ignore_atoms: Dm.list(this))))
            {
                await new_frame.deconstruct(Dm.num(disassembled));
                return null;
            }
        }

        new_frame.update_appearance(UPDATE_ICON_STATE);
        object dot = new_frame;
        await new_frame.set_anchored(anchored);
        if (!Dm.truthy(disassembled))
            new_frame.update_integrity(new_frame.max_integrity * 0.5f); //the frame is already half broken
        // tg: code/game/machinery/_machinery.dm:988
        transfer_fingerprints_to(new_frame);
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:991
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            await set_machine_stat(Dm.bitor(machine_stat, BROKEN));
            if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_BROKEN]))
                this._SendSignal(COMSIG_MACHINERY_BROKEN, Dm.list(this, damage_flag));
            update_appearance();
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:999  (M5x slice 1: explosions)
    [DmProtected]
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        if (!Dm.truthy(occupant))
            return;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, occupant);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, occupant);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, occupant);
                break;
        }
    }

    // tg: code/game/machinery/_machinery.dm:1011
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (gone == occupant)
        {
            set_occupant(null);
            update_appearance();
        }

        // The circuit should also be in component parts, so don't early return.
        // GAP(types): the board instance is circuit_value (circuit is generated as a Type, rules 3.2 V3)
        if (gone == circuit_value)
            circuit_value = null;
        if (Dm.@in(gone, component_parts) && !QDELETED(this))
        {
            Dm.remove(ref component_parts, gone);
            // It would be unusual for a component_part to be qdel'd ordinarily.
            await deconstruct(FALSE);
        }
        return dot;
    }

    /**
     * This should be called before mass qdeling components to make space for replacements.
     * If not done, things will go awry as Exited() destroys the machine when it detects
     * even a single component exiting the atom.
     */
    // tg: code/game/machinery/_machinery.dm:1030
    public virtual void clear_components()
    {
        if (!Dm.truthy(component_parts))
            return;
        DmList old_components = component_parts;
        circuit_value = null; // GAP(types): tg's `circuit = null`, the board instance
        component_parts = null;
        foreach (atom atom_part in Dm.each<atom>(old_components))
            qdel(atom_part);
    }

    /**
     * Default method of opening a machine's maintenance panel with a screwdriver
     *
     * * user - The mob using the screwdriver
     * * screwdriver - The screwdriver being used to open the panel.
     * You do not have to assert the screwdriver is a screwdriver, it is checked for you.
     *
     * Returns NONE on failure
     * Returns ITEM_INTERACT_SUCCESS on success
     */
    // tg: code/game/machinery/_machinery.dm:1049
    public virtual object default_deconstruction_screwdriver(mob user = null, obj_item screwdriver = null)
    {
        if (!Dm.eq(screwdriver.tool_behaviour, TOOL_SCREWDRIVER))
            return NONE;

        screwdriver.play_tool_sound(this, 50);
        toggle_panel_open();
        balloon_alert(user, I18n.T("maintenance hatch []", Dm.truthy(panel_open) ? "opened" : "closed"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/_machinery.dm:1069
    public virtual async DmTask<object> default_change_direction_wrench(mob user = null, obj_item wrench = null)
    {
        if (!Dm.truthy(panel_open) || !Dm.eq(wrench.tool_behaviour, TOOL_WRENCH))
            return NONE;

        wrench.play_tool_sound(this, 50);
        await setDir(turn(dir, -90));
        to_chat(user, span_notice(I18n.T("You rotate [].", this)));
        if (Dm.truthy(this._listen_lookup?[COMSIG_MACHINERY_DEFAULT_ROTATE_WRENCH]))
            this._SendSignal(COMSIG_MACHINERY_DEFAULT_ROTATE_WRENCH, Dm.list(this, user, wrench));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/_machinery.dm:1079
    public virtual async DmTask<object> exchange_parts(mob user = null, obj_item_storage_part_replacer replacer_tool = null)
    {
        // gaps OD3: translated with the RPED (modules/research/part_replacer.cs)
        if (!istype(replacer_tool, typeof(obj_item_storage_part_replacer)) || !Dm.truthy(component_parts))
            return FALSE;

        bool works_from_distance = istype(replacer_tool, typeof(obj_item_storage_part_replacer_bluespace));
        if (!Dm.truthy(panel_open) && !works_from_distance)
        {
            to_chat(user, display_parts(user));
            return FALSE;
        }

        obj_item_circuitboard_machine machine_board = Dm.locate_type(typeof(obj_item_circuitboard_machine), component_parts) as obj_item_circuitboard_machine;
        if (works_from_distance)
            to_chat(user, display_parts(user));
        if (machine_board == null)
            return FALSE;
        /**
         * sorting is very important especially because we are breaking out when required part is found in the inner for loop
         * if the rped first picked up a tier 3 part AND THEN a tier 4 part
         * tier 3 would be installed and the loop would break and check for the next required component thus
         * completly ignoring the tier 4 component inside
         * we also ignore stack components inside the RPED cause we dont exchange that
         */
        bool shouldplaysound = false;
        DmList part_list = replacer_tool.get_sorted_parts(ignore_stacks: TRUE);
        if (!Dm.truthy(part_list.len))
            return FALSE;

        replacer_tool.atom_storage.block_insert_remove_updates = TRUE;
        bool update_storage = false;
        foreach (object primary_part_base in Dm.each(component_parts))
        {
            //we exchanged all we could time to bail
            if (!Dm.truthy(part_list.len))
                break;

            float current_rating = 0;
            Type required_type = null;

            //we dont exchange circuitboards cause thats dumb
            if (istype(primary_part_base, typeof(obj_item_circuitboard)))
                continue;
            else if (primary_part_base is datum_stock_part primary_stock_part)
            {
                current_rating = Dm.num(primary_stock_part.tier);
                required_type = primary_stock_part.physical_object_base_type;
            }
            else
            {
                obj_item primary_stock_part_item = primary_part_base as obj_item;
                current_rating = Dm.num(primary_stock_part_item.get_part_rating());
                foreach (object design_type in Dm.each(machine_board.req_components))
                {
                    if (ispath(primary_stock_part_item.GetType(), design_type as Type))
                    {
                        required_type = design_type as Type;
                        break;
                    }
                }
            }

            foreach (object o in Dm.each(part_list))
            {
                if (o is not obj_item secondary_part)
                    continue;
                if (!istype(secondary_part, required_type))
                    continue;
                // If it's a corrupt or rigged cell, attempting to send it through Bluespace could have unforeseen consequences.
                if (istype(secondary_part, typeof(obj_item_stock_parts_power_store_cell)) && works_from_distance)
                {
                    obj_item_stock_parts_power_store_cell checked_cell = (obj_item_stock_parts_power_store_cell)secondary_part;
                    // If it's rigged or corrupted, max the charge. Then explode it.
                    if (Dm.truthy(await checked_cell.try_explode(max_charge: TRUE)))
                        break;
                }
                if (Dm.num(secondary_part.get_part_rating()) <= current_rating)
                    continue;
                //store name of part incase we qdel it below
                object secondary_part_name = secondary_part.name;
                if (Dm.truthy(replacer_tool.atom_storage.attempt_remove(secondary_part, this, silent: TRUE, visual_updates: FALSE)))
                {
                    update_storage = true;
                    if (istype(primary_part_base, typeof(datum_stock_part)))
                    {
                        object stock_part_datum = GLOB.stock_part_datums_per_object[secondary_part.GetType()];
                        if (stock_part_datum == null)
                            throw CRASH(Dm.text("[] ([]) did not have a stock part datum (was trying to find [])", secondary_part, secondary_part.GetType(), primary_part_base), "code/game/machinery/_machinery.dm", 1149);
                        Dm.append(ref component_parts, stock_part_datum);
                        Dm.remove(ref part_list, secondary_part); //have to manually remove cause we are no longer refering replacer_tool.contents
                        qdel(secondary_part);
                    }
                    else
                    {
                        Dm.append(ref component_parts, secondary_part);
                        await secondary_part.forceMove(this);
                        Dm.remove(ref part_list, secondary_part); //have to manually remove cause we are no longer refering replacer_tool.contents
                    }
                }

                Dm.remove(ref component_parts, primary_part_base);

                obj physical_part;
                if (primary_part_base is datum_stock_part stock_part_datum_of_primary)
                {
                    Type physical_object_type = stock_part_datum_of_primary.physical_object_type;
                    physical_part = Dm.@new(physical_object_type) as obj;
                }
                else
                    physical_part = primary_part_base as obj;

                replacer_tool.atom_storage.attempt_insert(physical_part as obj_item, user, @override: TRUE, force: STORAGE_SOFT_LOCKED, messages: FALSE);
                to_chat(user, span_notice(I18n.T("[] replaced with [].", capitalize(physical_part.name), secondary_part_name)));
                shouldplaysound = true; //Only play the sound when parts are actually replaced!
                break;
            }
        }

        await RefreshParts();

        if (shouldplaysound)
            replacer_tool.play_rped_effect();
        if (update_storage)
        {
            await replacer_tool.atom_storage.refresh_views();
            replacer_tool.update_appearance();
        }
        replacer_tool.atom_storage.block_insert_remove_updates = FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/_machinery.dm:1183
    public virtual object display_parts(mob user = null)
    {
        DmList part_count = Dm.list();

        foreach (object component_part in Dm.each(component_parts))
        {
            obj_item component_ref = null;

            if (component_part is datum_stock_part stock_part)
                component_ref = stock_part.physical_object_reference;
            else
            {
                component_ref = component_part as obj_item;
                foreach (obj_item counted_part in Dm.each<obj_item>(part_count))
                {
                    //e.g. 2 beakers though they have the same type are still 2 different objects so component_ref wont keep them unique so we look for that type ourselves and increment it
                    if (Dm.istype(counted_part, component_ref.type))
                    {
                        part_count[counted_part] = Dm.num(part_count[counted_part]) + 1;
                        component_ref = null;
                        break;
                    }
                }
                //looks like we already counted an type of this obj reference, time to bail
                if (component_ref == null)
                    continue;
            }

            if (Dm.truthy(part_count[component_ref]))
            {
                part_count[component_ref] = Dm.num(part_count[component_ref]) + 1;
                continue;
            }
            part_count[component_ref] = 1;

            // we infer the required stack stuff inside the machine from the circuitboards requested components
            if (component_ref is obj_item_circuitboard_machine board)
            {
                foreach (object component in Dm.each(board.req_components))
                {
                    if (!Dm.ispath(component, typeof(obj_item_stack)))
                        continue;
                    part_count[component] = board.req_components[component];
                }
            }
        }

        string text = span_notice(I18n.T("It contains the following parts:"));
        foreach (object component_part in Dm.each(part_count))
        {
            string part_name;
            //infer name & icon of part. stacks are just type paths so we have to get their initial values
            if (Dm.ispath(component_part, typeof(obj_item_stack)))
                part_name = Dm.str(Dm.initial(component_part, (obj_item_stack p) => p.singular_name));
            else
            {
                obj_item part = (obj_item)component_part;
                part_name = part.name;
            }
            //merge icon & name into text
            // [icon2html(html_icon, user, icon_state)] before the count: the part's picture in the chat line
            Dm.Deferred("code/game/machinery/_machinery.dm:1235", "display_parts: icon2html(html_icon, user, icon_state), the part's icon in the chat line (chat icons, later by the owner)");
            text += span_notice(I18n.T("[] []\\s.", part_count[component_part], part_name));
        }

        return text;
    }

    // tg: code/game/machinery/_machinery.dm:1239
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            Dm.append(ref dot, span_notice(I18n.T("It looks broken and non-functional.")));
        if (!Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
        {
            float healthpercent = Dm.num(Dm.div(atom_integrity, max_integrity)) * 100;
            if (healthpercent >= 50 && healthpercent <= 99)
                Dm.append(ref dot, I18n.T("It looks slightly damaged."));
            else if (healthpercent >= 25 && healthpercent <= 50)
                Dm.append(ref dot, I18n.T("It appears heavily damaged."));
            else if (healthpercent >= 0 && healthpercent <= 25)
                Dm.append(ref dot, span_warning(I18n.T("It's falling apart!")));
        }
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:1252
    public override object examine_descriptor(mob user = null)
    {
        return I18n.T("machine");
    }

    // tg: code/game/machinery/_machinery.dm:1256
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (HAS_TRAIT(user, TRAIT_RESEARCH_SCANNER) && Dm.truthy(component_parts))
            Dm.append(ref dot, display_parts(user));
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:1262
    public virtual async DmTask on_construction(mob user = null, params object[] __extra)
    {
        return;
    }

    /**
     * called on deconstruction before the final deletion
     * Arguments
     *
     * * disassembled - if TRUE means we used tools to deconstruct it, FALSE means it got destroyed by other means
     */
    // tg: code/game/machinery/_machinery.dm:1271
    [DmProtected]
    public virtual async DmTask<object> on_deconstruction(object disassembled = null)
    {
        return null;
    }

    // tg: code/game/machinery/_machinery.dm:1275  (the M7 zaps slice)
    public override async DmTask<object> zap_act(object power = null, object zap_flags = null)
    {
        // DEVIATION(DV27): the interaction guard (D132) on a machine whose own tg zap_act is not translated (the nuke, the
        // abductor RTG)
        if (InertSeam.MissingOverride(this, "zap_act"))
            return null;
        if (prob(85) && Dm.truthy(Dm.bitand(zap_flags, ZAP_MACHINE_EXPLOSIVE)) && !Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            await explosion(this, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 4, flame_range: 2, adminlog: TRUE, smoke: FALSE);
        else if (Dm.truthy(Dm.bitand(zap_flags, ZAP_OBJ_DAMAGE)))
        {
            await take_damage(Dm.mul(power, 2.5e-4f), BURN, ENERGY);
            if (prob(40))
                await emp_act(EMP_LIGHT);
            power = Dm.sub(power, Dm.mul(power, 5e-4f));
        }
        return await base.zap_act(power, zap_flags);
    }

    // tg: code/game/machinery/_machinery.dm:1285
    // Adjust item drop location to a 3x3 grid inside the tile, returns slot id from 0 to 8 (gaps P5: the ChemMaster's prints)
    public virtual object adjust_item_drop_location(atom_movable dropped_atom = null)
    {
        string md5_hash = md5(dropped_atom.name); // Oh, and it's deterministic too. A specific item will always drop from the same slot.
        float dot = 0;
        for (int i = 1; i <= 32; i++)
            dot += Dm.num(hex2num(copytext(md5_hash, i, i + 1)));
        dot = dot % 9;
        dropped_atom.pixel_x = -8 + ((dot % 3) * 8);
        dropped_atom.pixel_y = -8 + (round(dot / 3) * 8);
        return dot;
    }

    // tg: code/game/machinery/_machinery.dm:1326
    public virtual void AI_notify_hack()
    {
        string alertstr = span_userdanger(I18n.T("Network Alert: Hacking attempt detected[].", get_area(this) != null ? I18n.T(" in []", get_area_name(this, TRUE)) : I18n.T(". Unable to pinpoint location")));
        foreach (mob_living_silicon_ai AI in Dm.each<mob_living_silicon_ai>(GLOB.player_list))
            to_chat(AI, alertstr);
    }

    // tg: code/game/machinery/_machinery.dm:1331
    public virtual void update_last_used(mob user = null)
    {
        if (user is mob_living)
        {
            last_used_time = Dm.world.time;
            // last_user_mobtype = user.type: tg declares the var /mob/living (_machinery.dm:150), so the generated field is a
            // mob_living and cannot hold a type; tg never reads it, so it is left unset (hand-declaring it would make the
            // class's whole var block hand-written, types-converter.md 4.1)
        }
    }

    // tg: code/game/machinery/_machinery.dm:1337
    public virtual void add_as_sabotage_target()
    {
        Dm.NotTranslated("code/game/machinery/_machinery.dm:1337", "add_as_sabotage_target: antagonist objectives (M10+)");
    }
}
