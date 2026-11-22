// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/firedoor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M4b slice 2: the firelocks): /obj/machinery/door/firedoor and its thin border_only variant: the init (the area,
// the merge group of a row of firelocks, the adjacent turfs' signals), the atmos check (a hot or cold neighbour turf
// closes the row), activation and reset with the area's fire effect and fire alarm, crack_open and its release, the power
// signals, the knock on a closed firelock, the weld, the crowbar hold and toggle, the pictures, the door animation timing,
// open, close and Moved; the knock and bash sounds (M4b audio slice 1); the fire alarm siren (/datum/looping_sound/firealarm,
// M4b audio slice 5). Deferred leaves: the hazard lights' emissive (DEVIATION DV12) and the weld log line. Generated
// live-partial stubs: add_context (M9), the AI's and cyborgs' clicks and the pseudocircuit (since silicons SI2; the alien's since gaps X5) and the ninja's
// drain (M10+). M5 slice 10: the bolt lock (screwdriver) and the unbolting (wrench) of a welded firelock, on_deconstruction
// (the frame or the electronics) and the firelock frame (/obj/structure/firelock_frame and its heavy and border_only
// variants): the plasteel reinforcement, the electronics, the crowbar, the wrench that finishes the firelock and the welder
// that cuts the frame into iron; E13: its RCD procs. The var blocks are generated.

// tg: code/game/machinery/doors/firedoor.dm:1-2  CONSTRUCTION_NO_CIRCUIT and CONSTRUCTION_PANEL_OPEN: constants of obj_structure_firelock_frame

// tg: code/game/machinery/doors/firedoor.dm:6
public partial class obj_machinery_door_firedoor : obj_machinery_door
{
    /// default time for each step
    // tg: code/game/machinery/doors/firedoor.dm:3  #define DEFAULT_STEP_TIME 20 (file-local)
    protected const float DEFAULT_STEP_TIME = 20;
    // Delay on reactivation, used to prevent dumb crowbar things. Just trust me
    // tg: code/game/machinery/doors/firedoor.dm:4  #define REACTIVATION_DELAY (3 SECONDS) (file-local)
    protected const float REACTIVATION_DELAY = 3 * SECONDS;

    // tg: code/game/machinery/doors/firedoor.dm:52  var/static/list/merger_typecache (rules 10.6: a TgState field)
    public static ref DmList merger_typecache => ref TgState.Current.obj_machinery_door_firedoor__merger_typecache;

    // tg: code/game/machinery/doors/firedoor.dm:75
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        id_tag = assign_random_name();
        soundloop = Dm.@new<datum_looping_sound_firealarm>(this, FALSE);
        CalculateAffectingAreas();
        my_area = get_area(this) as area;
        if (Dm.eq(name, Dm.initial(this, (atom p) => p.name)))
            update_name();
        if (merger_typecache == null)
            merger_typecache = typecacheof(typeof(obj_machinery_door_firedoor)) as DmList;

        if (prob(0.004f) && Dm.eq(icon, Dm.resource("icons/obj/doors/doorfireglass.dmi")))
        {
            base_icon_state = "sus";
            desc = Dm.text("[] This one looks a bit sus...", desc);
        }

        RegisterSignal(this, COMSIG_MACHINERY_POWER_RESTORED, nameof(on_power_restore));
        RegisterSignal(this, COMSIG_MACHINERY_POWER_LOST, nameof(on_power_loss));
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/machinery/doors/firedoor.dm:94
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        RegisterSignal(this, COMSIG_MERGER_ADDING, nameof(merger_adding));
        RegisterSignal(this, COMSIG_MERGER_REMOVING, nameof(merger_removing));
        await GetMergeGroup(merger_id, merger_typecache);
        register_adjacent_turfs();

        if (Dm.truthy(alarm_type)) // Fucking subtypes fucking mappers fucking hhhhhhhh
            await start_activation_process(alarm_type as string);
    }

    /**
     * Sets the offset for the warning lights.
     *
     * Used for special firelocks with light overlays that don't line up to their sprite.
     */
    // tg: code/game/machinery/doors/firedoor.dm:109
    public virtual void adjust_lights_starting_offset()
    {
        return;
    }

    // tg: code/game/machinery/doors/firedoor.dm:112
    public override object Destroy(float? force = null)
    {
        remove_from_areas();
        unregister_adjacent_turfs(loc);
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/doors/firedoor.dm:118
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(density))
            Dm.append(ref dot, span_notice(I18n.T("It is open, but could be <b>pried</b> closed.")));
        else if (!Dm.truthy(welded))
        {
            Dm.append(ref dot, span_notice(I18n.T("It is closed, but could be <b>pried</b> open.")));
            Dm.append(ref dot, span_notice(I18n.T("Hold the firelock temporarily open by prying it with <i>left-click</i> and standing next to it.")));
            Dm.append(ref dot, span_notice(I18n.T("Prying by <i>right-clicking</i> the firelock will open it permanently.")));
            Dm.append(ref dot, span_notice(I18n.T("Deconstruction would require it to be <b>welded</b> shut.")));
        }
        else if (Dm.truthy(boltslocked))
            Dm.append(ref dot, span_notice(I18n.T("It is <i>welded</i> shut. The floor bolts have been locked by <b>screws</b>.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("The bolt locks have been <i>unscrewed</i>, but the bolts themselves are still <b>wrenched</b> to the floor.")));
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:182
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (my_area == null || !Dm.truthy(id_tag))
            return dot;
        name = Dm.text("[] [] []", get_area_name(my_area), Dm.initial(this, (atom p) => p.name), id_tag);
        return dot;
    }

    /**
     * Calculates what areas we should worry about.
     *
     * This proc builds a list of areas we are in and areas we border
     * and writes it to affecting_areas.
     */
    // tg: code/game/machinery/doors/firedoor.dm:194
    public virtual void CalculateAffectingAreas()
    {
        DmList new_affecting_areas = Dm.bitor(get_adjacent_open_areas(this), get_area(this)) as DmList;
        if (Dm.truthy(compare_list(new_affecting_areas, affecting_areas)))
            return; //No changes needed

        remove_from_areas();
        affecting_areas = new_affecting_areas;
        foreach (area place in Dm.each<area>(affecting_areas))
            LAZYADD(ref place.firedoors, this);
        if (Dm.truthy(active))
            add_as_source();
    }

    // tg: code/game/machinery/doors/firedoor.dm:206
    public virtual void remove_from_areas()
    {
        remove_as_source();
        foreach (area place in Dm.each<area>(affecting_areas))
            LAZYREMOVE(ref place.firedoors, this);
    }

    // tg: code/game/machinery/doors/firedoor.dm:211
    [SignalHandler]
    public virtual void merger_adding(obj_machinery_door_firedoor us = null, datum_merger new_merger = null)
    {
        if (!Dm.eq(new_merger.id, merger_id))
            return;
        RegisterSignal(new_merger, COMSIG_MERGER_REFRESH_COMPLETE, nameof(refresh_shared_turfs));
    }

    // tg: code/game/machinery/doors/firedoor.dm:217
    [SignalHandler]
    public virtual void merger_removing(obj_machinery_door_firedoor us = null, datum_merger old_merger = null)
    {
        if (!Dm.eq(old_merger.id, merger_id))
            return;
        UnregisterSignal(old_merger, COMSIG_MERGER_REFRESH_COMPLETE);
    }

    // tg: code/game/machinery/doors/firedoor.dm:223
    [SignalHandler]
    public virtual void refresh_shared_turfs(datum source = null, DmList leaving_members = null, DmList joining_members = null)
    {
        datum_merger temp_group = source as datum_merger;
        if (temp_group.origin != this)
            return;
        DmList shared_problems = Dm.list(); // We only want to do this once, this is a nice way of pulling that off
        foreach (obj_machinery_door_firedoor firelock in Dm.each_cast<obj_machinery_door_firedoor>(temp_group.members))
        {
            firelock.issue_turfs = shared_problems;
            foreach (object dir in Dm.each(GLOB.cardinals))
            {
                turf checked_turf = get_step(get_turf(firelock), Dm.num(dir));
                if (checked_turf == null)
                    continue;
                if (isclosedturf(checked_turf))
                    continue;
                process_results(checked_turf);
            }
        }
    }

    // tg: code/game/machinery/doors/firedoor.dm:239
    public virtual void register_adjacent_turfs()
    {
        if (loc == null)
            return;

        turf our_turf = get_turf(loc);
        RegisterSignal(our_turf, COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, nameof(process_results));
        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            turf checked_turf = get_step(our_turf, Dm.num(dir));

            if (checked_turf == null)
                continue;

            RegisterSignal(checked_turf, COMSIG_TURF_CHANGE, nameof(adjacent_change));
            RegisterSignal(checked_turf, COMSIG_TURF_EXPOSE, nameof(process_results));
            if (!isopenturf(checked_turf))
                continue;
            process_results(checked_turf);
        }
    }

    // tg: code/game/machinery/doors/firedoor.dm:257
    public virtual void unregister_adjacent_turfs(atom old_loc = null)
    {
        if (loc == null)
            return;

        turf our_turf = get_turf(old_loc);
        UnregisterSignal(our_turf, COMSIG_TURF_CALCULATED_ADJACENT_ATMOS);
        foreach (object dir in Dm.each(GLOB.cardinals))
        {
            turf checked_turf = get_step(our_turf, Dm.num(dir));

            if (checked_turf == null)
                continue;

            UnregisterSignal(checked_turf, COMSIG_TURF_CHANGE);
            UnregisterSignal(checked_turf, COMSIG_TURF_EXPOSE);
        }
    }

    // If a turf adjacent to us changes, recalc our affecting areas when it's done yeah?
    // tg: code/game/machinery/doors/firedoor.dm:273
    [SignalHandler]
    public virtual void adjacent_change(turf changed = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(CalculateAffectingAreas)));
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(process_results), changed)); //check the atmosphere of the changed turf so we don't hold onto alarm if a wall is built
    }

    // tg: code/game/machinery/doors/firedoor.dm:278
    public virtual object check_atmos(turf checked_turf = null)
    {
        datum_gas_mixture environment = Waiting.NoSleep(checked_turf.return_air()) as datum_gas_mixture;
        if (environment == null)
            throw Dm.CRASH("We tried to check a gas_mixture that doesn't exist for its firetype, what are you DOING", "code/game/machinery/doors/firedoor.dm", 281);

        if (environment.temperature >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            return FIRELOCK_ALARM_TYPE_HOT;
        if (Dm.num(environment.moles[typeof(datum_gas_antinoblium)]) > MINIMUM_MOLE_COUNT)
            return FIRELOCK_ALARM_TYPE_HOT;
        if (environment.temperature <= BODYTEMP_COLD_DAMAGE_LIMIT)
            return FIRELOCK_ALARM_TYPE_COLD;
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:291
    [SignalHandler]
    public virtual void process_results(datum source = null)
    {
        foreach (area place in Dm.each<area>(affecting_areas))
        {
            if (!Dm.truthy(place.fire_detect)) //if any area is set to disable detection
                return;
        }

        turf checked_turf = source as turf;
        if (!Dm.truthy(Dm.bitand(checked_turf.flags_1, INITIALIZED_1))) // uninitialized turfs won't have atmos setup anyways, so check_atmos would just complain and not work
            return;
        object result = check_atmos(checked_turf);

        if (Dm.truthy(result) && Dm.truthy(TURF_SHARES(checked_turf)))
        {
            Dm.combine(ref issue_turfs, checked_turf);
            if (Dm.truthy(alarm_type)) // If you've already got an alarm, go away
                return;
            // Store our alarm type, in case we can't activate for some reason
            alarm_type = result;
            if (!Dm.truthy(ignore_alarms))
                Waiting.NoSleep(start_activation_process(result as string));
        }
        else if (Dm.truthy(Dm.length(issue_turfs)))
        {
            Dm.remove(ref issue_turfs, checked_turf);
            if (Dm.truthy(Dm.length(issue_turfs)) && !Dm.eq(alarm_type, FIRELOCK_ALARM_TYPE_GENERIC))
                return;
            alarm_type = null;
            if (!Dm.truthy(ignore_alarms))
                Waiting.NoSleep(start_deactivation_process());
        }
    }

    /**
     * Begins activation process of us and our neighbors.
     *
     * This proc will call activate() on every fire lock (including us) listed
     * in the merge group datum. Returns without doing anything if we're already active, cause of course
     *
     * Arguments:
     * code should be one of three defined alarm types, or can be not supplied. Will dictate the color of the fire alarm lights, and defaults to "firelock_alarm_type_generic"
     */
    // tg: code/game/machinery/doors/firedoor.dm:329
    public virtual async DmTask start_activation_process(string code = null)
    {
        code ??= FIRELOCK_ALARM_TYPE_GENERIC;
        if (Dm.truthy(active))
            return; //We're already active
        soundloop.start();
        is_playing_alarm = TRUE;
        my_area.fault_status = AREA_FAULT_AUTOMATIC;
        my_area.fault_location = name;
        datum_merger merge_group = await GetMergeGroup(merger_id, merger_typecache) as datum_merger;
        foreach (obj_machinery_door_firedoor buddylock in Dm.each_cast<obj_machinery_door_firedoor>(merge_group.members))
            buddylock.activate(code);
    }

    /**
     * Begins deactivation process of us and our neighbors.
     *
     * This proc will call reset() on every fire lock (including us) listed
     * in the merge group datum. sets our alarm type to null, signifying no alarm.
     */
    // tg: code/game/machinery/doors/firedoor.dm:345
    public virtual async DmTask start_deactivation_process()
    {
        soundloop.stop();
        is_playing_alarm = FALSE;
        my_area.fault_status = AREA_FAULT_NONE;
        my_area.fault_location = null;
        datum_merger merge_group = await GetMergeGroup(merger_id, merger_typecache) as datum_merger;
        foreach (obj_machinery_door_firedoor buddylock in Dm.each_cast<obj_machinery_door_firedoor>(merge_group.members))
            buddylock.reset();
    }

    /**
     * Proc that handles activation of the firelock and all this details
     *
     * Sets active and alarm type to properly represent our state.
     * Also calls set_status() on all fire alarms in all affected areas, tells
     * the area the firelock sits in to report the event (AI, alarm consoles, etc)
     * and finally calls correct_state(), which will handle opening or closing
     * this fire lock.
     */
    // tg: code/game/machinery/doors/firedoor.dm:363
    [SignalHandler]
    public virtual void activate(string code = null)
    {
        code ??= FIRELOCK_ALARM_TYPE_GENERIC;
        if (Dm.truthy(active))
            return; //Already active
        if (Dm.truthy(ignore_alarms) && code != FIRELOCK_ALARM_TYPE_GENERIC)
            return;
        if (code != FIRELOCK_ALARM_TYPE_GENERIC && !(Dm.num(activation_cooldown) <= world.time)) // Non generic activation, subject to crowbar safety; COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            // Properly activate once the timeleft's up
            _addtimer(Dm.@new<datum_callback>(this, nameof(activate), code), max(Dm.num(activation_cooldown) - world.time, 0), file: "code/game/machinery/doors/firedoor.dm", line: 371); // COOLDOWN_TIMELEFT (__DEFINES/cooldowns.dm:115), inline (rules 11.1 (a))
            return;
        }
        active = TRUE;
        alarm_type = code;
        add_as_source();
        correct_state();
        update_appearance(UPDATE_ICON); //Sets the door lights even if the door doesn't move.
    }

    /// Adds this fire door as a source of trouble to all of its areas
    // tg: code/game/machinery/doors/firedoor.dm:380
    public virtual void add_as_source()
    {
        foreach (area place in Dm.each<area>(affecting_areas))
        {
            LAZYADD(ref place.active_firelocks, this);
            if (LAZYLEN(place.active_firelocks) != 1)
                continue;
            //if we're the first to activate in this particular area
            place.set_fire_effect(TRUE, AREA_FAULT_AUTOMATIC, name); //bathe in red
            if (place == my_area)
            {
                // We'll limit our reporting to just the area we're on. If the issue affects bordering areas, they can report it themselves
                place.alarm_manager.send_alarm(ALARM_FIRE, place);
            }
        }
    }

    /**
     * Proc that handles reset steps
     *
     * Clears the alarm state and attempts to open the firelock.
     */
    // tg: code/game/machinery/doors/firedoor.dm:396
    [SignalHandler]
    public virtual void reset()
    {
        alarm_type = null;
        active = FALSE;
        remove_as_source();
        soundloop.stop();
        is_playing_alarm = FALSE;
        correct_state();
        update_appearance(UPDATE_ICON); //Sets the door lights even if the door doesn't move.
    }

    /**
     * Open the firedoor without resetting existing alarms
     *
     * * delay - Reconsider if this door should be open or closed after some period
     *
     */
    // tg: code/game/machinery/doors/firedoor.dm:412
    public virtual void crack_open(object delay = null)
    {
        active = FALSE;
        ignore_alarms = TRUE;
        if (!Dm.truthy(Dm.length(issue_turfs))) // Generic alarms get out
            alarm_type = null;

        soundloop.stop();
        is_playing_alarm = FALSE;
        remove_as_source();
        correct_state();
        update_appearance(UPDATE_ICON); //Sets the door lights even if the door doesn't move.

        /// Please be called 3 seconds after the LAST open, rather then 3 seconds after the first
        _addtimer(Dm.@new<datum_callback>(this, nameof(release_constraints)), 3 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/game/machinery/doors/firedoor.dm", line: 425);
    }

    /**
     * Reset our temporary alarm ignoring
     * Consider if we should close ourselves/our neighbors or not
     */
    // tg: code/game/machinery/doors/firedoor.dm:431
    public virtual async DmTask release_constraints()
    {
        ignore_alarms = FALSE;
        if (!Dm.truthy(alarm_type) || Dm.truthy(active)) // If we have no alarm type, or are already active, go away
            return;
        // Do we even care about temperature?
        foreach (area place in Dm.each<area>(affecting_areas))
        {
            if (!Dm.truthy(place.fire_detect)) // If any area is set to disable detection
                return;
        }
        // Otherwise, reactivate ourselves
        await start_activation_process(alarm_type as string);
    }

    /// Removes this firedoor from all areas it's serving as a source of problems for
    // tg: code/game/machinery/doors/firedoor.dm:443
    public virtual void remove_as_source()
    {
        foreach (area place in Dm.each<area>(affecting_areas))
        {
            if (!Dm.truthy(LAZYLEN(place.active_firelocks))) // If it has no active firelocks, do nothing
                continue;
            LAZYREMOVE(ref place.active_firelocks, this);
            if (Dm.truthy(LAZYLEN(place.active_firelocks))) // If we were the last firelock still active, clear the area effects
                continue;
            place.set_fire_effect(FALSE, AREA_FAULT_NONE, name);
            if (place == my_area)
                place.alarm_manager.clear_alarm(ALARM_FIRE, place);
        }
    }

    // tg: code/game/machinery/doors/firedoor.dm:454
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        if (emag_card is obj_item_card_emag_doorjack digital_crowbar) //Skip doorjack-specific code
            digital_crowbar.use_charge(user);
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        Waiting.Detach(open()); // INVOKE_ASYNC(src, PROC_REF(open))
        return TRUE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:464
    public override object Bumped(atom_movable AM = null)
    {
        if (Dm.truthy(panel_open) || Dm.truthy(operating))
            return null;
        if (!Dm.truthy(density))
            return base.Bumped(AM);
        return FALSE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:471
    [SignalHandler]
    public virtual void on_power_loss()
    {
        soundloop.stop();
    }

    // tg: code/game/machinery/doors/firedoor.dm:476
    [SignalHandler]
    public virtual void on_power_restore()
    {
        correct_state();

        if (Dm.truthy(is_playing_alarm))
        {
            soundloop.start();
        }
    }

    // tg: code/game/machinery/doors/firedoor.dm:485
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(operating) || !Dm.truthy(density))
            return dot;
        mob_living living_user = user as mob_living;
        user.changeNext_move(CLICK_CD_MELEE);

        if (!Dm.truthy(living_user.combat_mode))
        {
            user.visible_message(span_notice(Dm.text("[] knocks on [].", user, this)),
                span_notice(Dm.text("You knock on [].", this)));
            playsound(this, knock_sound, 50, TRUE);
        }
        else
        {
            user.visible_message(span_warning(Dm.text("[] bashes []!", user, this)),
                span_warning(Dm.text("You bash []!", this)));
            playsound(this, bash_sound, 100, TRUE);
        }
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:502
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        // tg: code/game/machinery/doors/firedoor.dm:503
        add_fingerprint(user);
        if (Dm.truthy(operating) || !Dm.truthy(welded))
            return FALSE;

        if (Dm.truthy(boltslocked))
        {
            to_chat(user, span_notice(I18n.T("There are screws locking the bolts in place!")));
            return ITEM_INTERACT_SUCCESS;
        }
        tool.play_tool_sound(this);
        user.visible_message(span_notice(I18n.T("[] starts undoing []'s bolts...", user, this)),
            span_notice(I18n.T("You start unfastening []'s floor bolts...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, DEFAULT_STEP_TIME)))
            return ITEM_INTERACT_SUCCESS;
        playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:515
        user.visible_message(span_notice(I18n.T("[] unfastens []'s bolts.", user, this)),
            span_notice(I18n.T("You undo []'s floor bolts.", this)));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/firedoor.dm:521
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(operating) || !Dm.truthy(welded))
            return FALSE;
        user.visible_message(span_notice(I18n.T("[] [] []'s bolts.", user, Dm.truthy(boltslocked) ? "unlocks" : "locks", this)),
            span_notice(I18n.T("You [] []'s floor bolts.", Dm.truthy(boltslocked) ? "unlock" : "lock", this)));
        tool.play_tool_sound(this);
        boltslocked = Dm.truthy(boltslocked) ? FALSE : TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/firedoor.dm:530
    public override async DmTask<object> try_to_activate_door(mob user = null, float? access_bypass = null, float? bumped = null)
    {
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:533
    public override async DmTask try_to_weld_secondary(obj_item_weldingtool W = null, mob user = null)
    {
        if (!Dm.truthy(W.tool_start_check(user as mob_living, amount: 1)))
            return;
        user.visible_message(span_notice(Dm.text("[] starts [] [].", user, Dm.truthy(welded) ? "unwelding" : "welding", this)), span_notice(Dm.text("You start welding [].", this)));
        if (Dm.truthy(await W.use_tool(this, user as mob_living, DEFAULT_STEP_TIME, volume: 50)))
        {
            welded = Dm.truthy(welded) ? FALSE : TRUE;
            user.visible_message(span_danger(Dm.text("[] [] [].", user, Dm.truthy(welded) ? "welds" : "unwelds", this)), span_notice(Dm.text("You [] [].", Dm.truthy(welded) ? "weld" : "unweld", this)));
            user.log_message(Dm.text("[] firedoor [] with [].", Dm.truthy(welded) ? "welded" : "unwelded", this, W), LOG_GAME);
            update_appearance();
            correct_state();
        }
    }

    /// We check for adjacency when using the primary attack.
    // tg: code/game/machinery/doors/firedoor.dm:545
    public override async DmTask try_to_crowbar(obj_item acting_object = null, mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        if (Dm.truthy(welded) || Dm.truthy(operating))
            return;

        atom crowbar_owner = (acting_object?.loc is atom owner__1 && Dm.truthy(owner__1)) ? owner__1 : user; // catches mechs and any other non-mob using a crowbar

        if (Dm.truthy(density))
        {
            being_held_open = TRUE;
            crowbar_owner.balloon_alert_to_viewers("holding firelock open", "holding firelock open");
            activation_cooldown = world.time + REACTIVATION_DELAY; // COOLDOWN_START (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
            await open(opener: user as mob_living);
            if (QDELETED(crowbar_owner))
            {
                being_held_open = FALSE;
                return;
            }
            RegisterSignal(crowbar_owner, COMSIG_MOVABLE_MOVED, nameof(handle_held_open_adjacency));
            RegisterSignal(crowbar_owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(handle_held_open_adjacency));
            RegisterSignal(crowbar_owner, COMSIG_QDELETING, nameof(handle_held_open_adjacency));
            handle_held_open_adjacency(crowbar_owner);
        }
        else
            await close();
    }

    /// A simple toggle for firedoors between on and off
    // tg: code/game/machinery/doors/firedoor.dm:567
    public override async DmTask try_to_crowbar_secondary(obj_item acting_object = null, mob user = null)
    {
        if (Dm.truthy(welded) || Dm.truthy(operating))
            return;

        if (Dm.truthy(density))
        {
            await open(opener: user as mob_living);
            if (Dm.truthy(active))
                _addtimer(Dm.@new<datum_callback>(this, nameof(correct_state)), 2 * SECONDS, TIMER_UNIQUE, file: "code/game/machinery/doors/firedoor.dm", line: 574);
        }
        else
            await close();
    }

    // tg: code/game/machinery/doors/firedoor.dm:578
    [SignalHandler]
    public virtual void handle_held_open_adjacency(atom crowbar_owner = null)
    {
        if (!QDELETED(crowbar_owner) && Dm.truthy(Waiting.NoSleep(IsReachableBy(crowbar_owner as atom_movable))))
        {
            if (!ismob(crowbar_owner))
                return;
            mob_living mob_user = crowbar_owner as mob_living;
            if (isliving(mob_user) && (mob_user.body_position == STANDING_UP))
                return;
        }
        being_held_open = FALSE;
        correct_state();
        UnregisterSignal(crowbar_owner, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(crowbar_owner, COMSIG_LIVING_SET_BODY_POSITION);
        UnregisterSignal(crowbar_owner, COMSIG_QDELETING);
        if (crowbar_owner != null)
            crowbar_owner.balloon_alert_to_viewers("released firelock", "released firelock");
    }

    // tg: code/game/machinery/doors/firedoor.dm:620
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            icon_state = Dm.text("[]_opening", base_icon_state);
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            icon_state = Dm.text("[]_closing", base_icon_state);
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
            icon_state = Dm.text("[]_deny", base_icon_state);
        else
            icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(density) ? "closed" : "open");
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:632
    public override object animation_length(object animation = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_ANIMATION))
            return 1.2f * SECONDS;
        else if (Dm.eq(animation, DOOR_CLOSING_ANIMATION))
            return 1.2f * SECONDS;
        else if (Dm.eq(animation, DOOR_DENY_ANIMATION))
            return 0.3f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:641
    public override object animation_segment_delay(object animation = null)
    {
        if (Dm.eq(animation, DOOR_OPENING_PASSABLE))
            return 1.0f * SECONDS;
        else if (Dm.eq(animation, DOOR_OPENING_FINISHED))
            return 1.2f * SECONDS;
        else if (Dm.eq(animation, DOOR_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(animation, DOOR_CLOSING_FINISHED))
            return 1.2f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:652
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(welded))
            Dm.append(ref dot, Dm.truthy(density) ? "welded" : "welded_open");
        if (Dm.truthy(alarm_type) && Dm.truthy(powered__proc()) && !Dm.truthy(ignore_alarms))
        {
            mutable_appearance hazards;
            hazards = mutable_appearance(icon, Dm.text("[]", Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) ? "firelock_alarm_type_emag" : alarm_type)) as mutable_appearance;
            hazards.pixel_w = light_xoffset;
            hazards.pixel_z = light_yoffset;
            Dm.append(ref dot, hazards);
            // DEVIATION(DV12): emissive_appearance (__HELPERS/lighting.dm:2), the hazard lights' glow on the emissive plane, is
            // not created (lighting planes and emissives are not ported).
            Dm.Deferred("code/game/machinery/doors/firedoor.dm:662", "update_overlays: emissive_appearance of the hazard lights (DEVIATION DV12)");
        }
        return dot;
    }

    /**
     * Corrects the current state of the door, based on its activity.
     *
     * This proc is called after weld and power restore events. Gives the
     * illusion that the door is constantly attempting to move without actually
     * having to process it. Timers also call this, so that if activity
     * changes during the timer, the door doesn't close or open incorrectly.
     */
    // tg: code/game/machinery/doors/firedoor.dm:675
    public virtual void correct_state()
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) || Dm.truthy(being_held_open) || QDELETED(this))
            return; //Unmotivated, indifferent, we have no real care what state we're in anymore.
        if (Dm.truthy(active) && !Dm.truthy(density)) //We should be closed but we're not
        {
            Waiting.Detach(close());
            return;
        }
        if (!Dm.truthy(active) && Dm.truthy(density)) //We should be open but we're not
        {
            Waiting.Detach(open());
            return;
        }
    }

    // tg: code/game/machinery/doors/firedoor.dm:685
    public override async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(welded))
            return null;
        object old_activity = active;
        object dot = await base.open(forced, opener);
        if (!Dm.eq(old_activity, active)) //Something changed while we were sleeping
            correct_state(); //So we should re-evaluate our state
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:693
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        if (HAS_TRAIT(loc, TRAIT_FIREDOOR_STOP))
            return null;
        object old_activity = active;
        object dot = await base.close(forced, __extra);
        if (!Dm.eq(old_activity, active)) //Something changed while we were sleeping
            correct_state(); //So we should re-evaluate our state
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:701
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf targetloc = get_turf(this);
        if (Dm.truthy(disassembled) || Dm.truthy(prob(40)))
        {
            var unbuilt_lock = (obj_structure_firelock_frame)Dm.@new(assemblytype, targetloc);
            if (Dm.truthy(disassembled))
                unbuilt_lock.constructionStep = obj_structure_firelock_frame.CONSTRUCTION_PANEL_OPEN;
            else
            {
                unbuilt_lock.constructionStep = obj_structure_firelock_frame.CONSTRUCTION_NO_CIRCUIT;
                unbuilt_lock.update_integrity(Dm.num(unbuilt_lock.max_integrity) * 0.5f);
            }
            await unbuilt_lock.setDir(dir);
            unbuilt_lock.update_appearance();
        }
        else
            Dm.@new<obj_item_electronics_firelock>(targetloc);
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:715
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        unregister_adjacent_turfs(old_loc);
        register_adjacent_turfs();
        return dot;
    }
}

// tg: code/game/machinery/doors/firedoor.dm:725
public partial class obj_machinery_door_firedoor_border_only : obj_machinery_door_firedoor
{
    // tg: code/game/machinery/doors/firedoor.dm:742  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_door_firedoor_border_only__Initialize__loc_connections;

    // tg: code/game/machinery/doors/firedoor.dm:737
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        flags_1 = Dm.num(Dm.bitand(flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1)));
        adjust_lights_starting_offset();
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:746
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        object dot = await base.close(forced, __extra);
        flags_1 = Dm.num(Dm.bitand(flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1)));
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:750
    public override void adjust_lights_starting_offset()
    {
        light_xoffset = 0;
        light_yoffset = 0;
        float sw__1 = dir;
        if (sw__1 == NORTH)
            light_yoffset = 2;
        else if (sw__1 == SOUTH)
            light_yoffset = -2;
        else if (sw__1 == EAST)
            light_xoffset = 2;
        else if (sw__1 == WEST)
            light_xoffset = -2;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/machinery/doors/firedoor.dm:764
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        adjust_lights_starting_offset();
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:768
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.eq(border_dir, dir)) //Make sure looking at appropriate border
            return TRUE;
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:773
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        return !Dm.truthy(density) || !Dm.eq(dir, to_dir) ? TRUE : FALSE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:776
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;
        if (leaving == this)
            return null; // Let's not block ourselves.

        if (Dm.eq(direction, dir) && Dm.truthy(density))
        {
            Waiting.NoSleep(leaving.Bump(this));
            return COMPONENT_ATOM_BLOCK_EXIT;
        }
        return null;
    }

    // tg: code/game/machinery/doors/firedoor.dm:787
    public override object can_atmos_pass__proc(turf T = null, float? vertical = null)
    {
        vertical ??= FALSE;
        if (Dm.eq(get_dir(loc, T), dir))
            return !Dm.truthy(density) ? TRUE : FALSE;
        else
            return TRUE;
    }
}

// tg: code/game/machinery/doors/firedoor.dm:808
public partial class obj_structure_firelock_frame : obj_structure
{
    // tg: code/game/machinery/doors/firedoor.dm:1  #define CONSTRUCTION_NO_CIRCUIT 1 (file-local, #undef at :1022)
    internal const int CONSTRUCTION_NO_CIRCUIT = 1;
    // tg: code/game/machinery/doors/firedoor.dm:2  #define CONSTRUCTION_PANEL_OPEN 2 (file-local, #undef at :1021)
    internal const int CONSTRUCTION_PANEL_OPEN = 2;
    // tg: code/game/machinery/doors/firedoor.dm:3  #define DEFAULT_STEP_TIME 20 (file-local, #undef at :1024)
    private const float DEFAULT_STEP_TIME = 20;

    // tg: code/game/machinery/doors/firedoor.dm:821
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        float sw__1 = constructionStep;
        if (sw__1 == CONSTRUCTION_PANEL_OPEN)
        {
            Dm.append(ref dot, span_notice(I18n.T("It is <i>unbolted</i> from the floor. The circuit could be removed with a <b>crowbar</b>.")));
            if (!Dm.truthy(reinforced) && !Dm.truthy(directional))
                Dm.append(ref dot, span_notice(I18n.T("It could be reinforced with plasteel.")));
        }
        else if (sw__1 == CONSTRUCTION_NO_CIRCUIT)
            Dm.append(ref dot, span_notice(I18n.T("There are no <i>firelock electronics</i> in the frame. The frame could be <b>welded</b> apart .")));
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:831
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, constructionStep);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/doors/firedoor.dm:835
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        float sw__1 = constructionStep;
        if (sw__1 == CONSTRUCTION_PANEL_OPEN)
        {
            if (!istype(tool, typeof(obj_item_stack_sheet_plasteel)))
                return NONE;
            if (Dm.truthy(directional))
            {
                to_chat(user, span_warning(I18n.T("[] can not be reinforced.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            var plasteel_sheet = (obj_item_stack_sheet_plasteel)tool;
            if (Dm.truthy(reinforced))
            {
                to_chat(user, span_warning(I18n.T("[] is already reinforced.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.num(plasteel_sheet.get_amount()) < 2)
            {
                to_chat(user, span_warning(I18n.T("You need more plasteel to reinforce [].", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] begins reinforcing []...", user, this)),
                span_notice(I18n.T("You begin reinforcing []...", this)));
            playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:852
            if (!Dm.truthy(await do_after(user, DEFAULT_STEP_TIME, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (constructionStep != CONSTRUCTION_PANEL_OPEN || Dm.truthy(reinforced) || Dm.num(plasteel_sheet.get_amount()) < 2 || !Dm.truthy(plasteel_sheet))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(span_notice(I18n.T("[] reinforces [].", user, this)),
                span_notice(I18n.T("You reinforce [].", this)));
            playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:859
            await plasteel_sheet.use(2);
            reinforced = 1;
            return ITEM_INTERACT_SUCCESS;
        }
        else if (sw__1 == CONSTRUCTION_NO_CIRCUIT)
        {
            if (istype(tool, typeof(obj_item_electronics_firelock)))
            {
                user.visible_message(span_notice(I18n.T("[] starts adding [] to []...", user, tool, this)),
                    span_notice(I18n.T("You begin adding a circuit board to []...", this)));
                playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:868
                if (!Dm.truthy(await do_after(user, DEFAULT_STEP_TIME, target: this)))
                    return ITEM_INTERACT_BLOCKING;
                if (constructionStep != CONSTRUCTION_NO_CIRCUIT)
                    return ITEM_INTERACT_BLOCKING;
                qdel(tool);
                user.visible_message(span_notice(I18n.T("[] adds a circuit to [].", user, this)),
                    span_notice(I18n.T("You insert and secure [].", tool)));
                playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:876
                constructionStep = CONSTRUCTION_PANEL_OPEN;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            // tg: code/game/machinery/doors/firedoor.dm:881  (silicons SI2: the cyborg's electroadaptive pseudocircuit)
            if (tool is obj_item_electroadaptive_pseudocircuit raspberrypi)
            {
                if (!Dm.truthy(await raspberrypi.adapt_circuit(user as mob_living_silicon_robot, circuit_cost: DEFAULT_STEP_TIME * 0.0005f * STANDARD_CELL_CHARGE)))
                    return ITEM_INTERACT_BLOCKING;
                user.visible_message(span_notice(I18n.T("[] fabricates a circuit and places it into [].", user, this)),
                    span_notice(I18n.T("You adapt a firelock circuit and slot it into the assembly.")));
                constructionStep = CONSTRUCTION_PANEL_OPEN;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return NONE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:892
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (constructionStep != CONSTRUCTION_PANEL_OPEN)
            return NONE;
        tool.play_tool_sound(this);
        user.visible_message(span_notice(I18n.T("[] begins removing the circuit board from []...", user, this)),
            span_notice(I18n.T("You begin prying out the circuit board from []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, DEFAULT_STEP_TIME)))
            return ITEM_INTERACT_BLOCKING;
        if (constructionStep != CONSTRUCTION_PANEL_OPEN)
            return ITEM_INTERACT_BLOCKING;
        playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:902
        user.visible_message(span_notice(I18n.T("[] removes []'s circuit board.", user, this)),
            span_notice(I18n.T("You remove the circuit board from [].", this)));
        Dm.@new<obj_item_electronics_firelock>(drop_location());
        constructionStep = CONSTRUCTION_NO_CIRCUIT;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/firedoor.dm:910
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (constructionStep != CONSTRUCTION_PANEL_OPEN)
            return NONE;
        if (Dm.truthy(Dm.locate<obj_machinery_door_firedoor>(get_turf(this))))
        {
            to_chat(user, span_warning(I18n.T("There's already a firelock there.")));
            return ITEM_INTERACT_BLOCKING;
        }
        tool.play_tool_sound(this);
        user.visible_message(span_notice(I18n.T("[] starts bolting down []...", user, this)),
            span_notice(I18n.T("You begin bolting []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, DEFAULT_STEP_TIME)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(Dm.locate<obj_machinery_door_firedoor>(get_turf(this))))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] finishes the firelock.", user)),
            span_notice(I18n.T("You finish the firelock.")));
        playsound(get_turf(this), Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/machinery/doors/firedoor.dm:925
        if (Dm.truthy(reinforced))
            Dm.@new<obj_machinery_door_firedoor_heavy>(get_turf(this));
        else if (Dm.truthy(directional))
        {
            var new_firedoor = Dm.@new<obj_machinery_door_firedoor_border_only>(get_turf(this));
            await new_firedoor.setDir(dir);
            new_firedoor.adjust_lights_starting_offset();
        }
        else
            Dm.@new<obj_machinery_door_firedoor>(get_turf(this));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/firedoor.dm:937
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (constructionStep != CONSTRUCTION_NO_CIRCUIT)
            return NONE;
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] begins cutting apart []'s frame...", user, this)),
            span_notice(I18n.T("You begin slicing [] apart...", this)));

        if (!Dm.truthy(await tool.use_tool(this, user, DEFAULT_STEP_TIME, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        if (constructionStep != CONSTRUCTION_NO_CIRCUIT)
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] cuts apart []!", user, this)),
            span_notice(I18n.T("You cut [] into metal.", this)));
        turf targetloc = get_turf(this);
        Dm.@new<obj_item_stack_sheet_iron>(targetloc, Dm.truthy(directional) ? 2 : 3);
        if (Dm.truthy(reinforced))
            Dm.@new<obj_item_stack_sheet_plasteel>(targetloc, 2);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/machinery/doors/firedoor.dm:982
public partial class obj_structure_firelock_frame_border_only : obj_structure_firelock_frame
{
    // tg: code/game/machinery/doors/firedoor.dm:992  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_firelock_frame_border_only__Initialize__loc_connections;

    // tg: code/game/machinery/doors/firedoor.dm:989
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), ROTATION_NEEDS_ROOM));

        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/machinery/doors/firedoor.dm:997
    [SignalHandler]
    public virtual object on_exit(datum source = null, atom_movable leaving = null, object direction = null)
    {
        if (leaving == this)
            return null; // Let's not block ourselves.

        if (!Dm.truthy(Dm.bitand(direction, dir)))
            return null;

        if (!Dm.truthy(density))
            return null;

        if (Dm.truthy(Dm.bitand(leaving.movement_type, PHASING)))
            return null;

        if (Dm.num(leaving.move_force) >= MOVE_FORCE_EXTREMELY_STRONG)
            return null;

        Waiting.NoSleep(leaving.Bump(this));
        return COMPONENT_ATOM_BLOCK_EXIT;
    }

    // tg: code/game/machinery/doors/firedoor.dm:1017
    public override async DmTask<object> CanPass(atom_movable mover = null, object border_dir = null)
    {
        return Dm.truthy(Dm.bitand(border_dir, dir)) ? await base.CanPass(mover, border_dir) : TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/doors/firedoor.dm:52  /obj/machinery/door/firedoor/var/static/list/merger_typecache
    internal DmList obj_machinery_door_firedoor__merger_typecache;

    // tg: code/game/machinery/doors/firedoor.dm:742  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_machinery_door_firedoor_border_only__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_machinery_door_firedoor_border_only.on_exit)));

    // tg: code/game/machinery/doors/firedoor.dm:992  var/static/list/loc_connections = list(COMSIG_ATOM_EXIT = PROC_REF(on_exit))
    internal DmList obj_structure_firelock_frame_border_only__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXIT, nameof(obj_structure_firelock_frame_border_only.on_exit)));
}

// tg: code/game/machinery/doors/firedoor.dm:958 (E13: the RCD)
public partial class obj_structure_firelock_frame : obj_structure
{
    // tg: code/game/machinery/doors/firedoor.dm:958
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_DECONSTRUCT))
            return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 16));
        else if ((constructionStep == CONSTRUCTION_NO_CIRCUIT) && Dm.truthy(Dm.bitand(the_rcd.construction_upgrades, RCD_UPGRADE_SIMPLE_CIRCUITS)))
            return Dm.list(Dm.kv("delay", 2 * SECONDS), Dm.kv("cost", 1));
        return FALSE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:965  (tg's switch compares the mode with RCD_UPGRADE_SIMPLE_CIRCUITS, an upgrade flag that
    // equals RCD_WINDOWGRILLE, as written)
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_UPGRADE_SIMPLE_CIRCUITS))
        {
            user.balloon_alert(user, I18n.T("circuit installed"));
            constructionStep = CONSTRUCTION_PANEL_OPEN;
            update_appearance();
            return TRUE;
        }
        else if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/doors/firedoor.dm:596
public partial class obj_machinery_door_firedoor : obj_machinery_door
{
    // tg: code/game/machinery/doors/firedoor.dm:596
    public override async DmTask<object> attack_ai(mob user = null)
    {
        add_fingerprint(user);
        if (Dm.truthy(welded) || Dm.truthy(operating) || Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return TRUE;
        if (Dm.truthy(density))
        {
            await open(opener: user as mob_living);
            if (Dm.truthy(active))
                _addtimer(Dm.@new<datum_callback>(this, nameof(correct_state)), 2 * SECONDS, TIMER_UNIQUE, file: "code/game/machinery/doors/firedoor.dm", line: 602);
        }
        else
            await close();
        return TRUE;
    }

    // tg: code/game/machinery/doors/firedoor.dm:608
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_ai(user);
    }
}

// tg: code/game/machinery/doors/firedoor.dm:611  (interleaved types, rules 1.9; gaps X5: an alien forcing a firelock)
public partial class obj_machinery_door_firedoor : obj_machinery_door
{
    // tg: code/game/machinery/doors/firedoor.dm:611
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        add_fingerprint(user);
        if (Dm.truthy(welded))
        {
            balloon_alert(user, I18n.T("refuses to budge!"));
            return null;
        }
        await open(opener: user);
        if (Dm.truthy(active))
            _addtimer(Dm.@new<datum_callback>(this, nameof(correct_state)), 2 * SECONDS, TIMER_UNIQUE, file: "code/game/machinery/doors/firedoor.dm", line: 618);
        return null;
    }
}
