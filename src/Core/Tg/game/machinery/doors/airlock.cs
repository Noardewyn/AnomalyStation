// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/doors/airlock.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the airlock's M3 subset (M3 P3, scope 2f; D117): Initialize with its wires, the bump, bolts, power and its
// outages, the airlock state and appearance (update_icon_state, update_overlays with get_airlock_overlay), the animation
// timings and effects, open and close with tg's segment delays, the forced-door checks, cycle linking and close_others,
// Destroy and Exited, the external airlock and the multi-tile filler. The procs of later milestones are generated
// live-partial stubs (docs/ai/specs/live-partial.txt, ## P3) or stay Dm.NotTranslated below with theirs: hacking and the wires' UI
// (M4a, M7; the shock itself is the M7 shocks slice's), AI control (M10+), tools and seals (M4a), construction (M5), emag (M4a), the paint job (M5). M5 slice 6: the
// reinforcement layers (iron, plasteel) with their tool acts, the door seal, take_damage, on_break (all wires cut),
// electrification's loop and setter, the deconstruction into an assembly and its electronics. The sounds
// of the translated procs play (M4b audio slice 1); the hearers' messages (audible_message) are M9. The var block is
// generated (D111). M4c slice 5: open's cycling pump hook (cycle_pump.airlock_act) and the external airlock's powered
// safety latch (try_safety_unlock with is_safe_turf). E13: rcd_vals and rcd_act (the RCD). The cult airlock (a Lavaland ruin's
// doors): it lets in cultists, shades, constructs and everyone if friendly, flings everyone else away, and hides as a maintenance
// airlock.

// tg: code/game/machinery/doors/airlock.dm:25  (the global procs, rules 7.8)
public static partial class GlobalProcs
{
    /// Overlay cache.  Why isn't this just in /obj/machinery/door/airlock?  Because its used just a
    /// tiny bit in door_assembly.dm  Refactored so you don't have to make a null copy of airlock
    /// to get to the damn thing
    /// Someone, for the love of god, profile this.  Is there a reason to cache mutable_appearance
    /// if so, why are we JUST doing the airlocks when we can put this in mutable_appearance.dm for
    /// everything
    // tg: code/game/machinery/doors/airlock.dm:25
    public static object get_airlock_overlay(object icon_state = null, object icon_file = null, atom offset_spokesman = null, object em_block = null)
    {
        object dot = null;
        // DEVIATION(DV19): a GAGS icon is the client-composed name (text, "gags:<config>|<colours>"), which has no BYOND ref: the
        // name itself keys the cache (the material airlock and its assembly, door_assembly_types.dm:265).
        string base_icon_key = Dm.text("[][]", icon_state, istext(icon_file) ? icon_file : REF(icon_file));
        if (!Dm.truthy(dot = get_airlock_overlay__airlock_overlays[base_icon_key]))
            dot = get_airlock_overlay__airlock_overlays[base_icon_key] = mutable_appearance(icon_file, (string)icon_state);
        if (em_block == null)
            return dot;

        // DEVIATION(DV12): the emissive blocker or emissive overlay (an EMISSIVE_PLANE mutable_appearance coloured
        // GLOB.em_block_color or GLOB.emissive_color, :34-42) is not created: lighting planes and emissives are not ported
        // (D24, lead decision D2). The appearance comes alone, where tg returns list(., em_blocker).
        Dm.Deferred("code/game/machinery/doors/airlock.dm:34", "get_airlock_overlay: the em_block emissive blocker (DEVIATION DV12)");
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:26  var/static/list/airlock_overlays (a proc-level static, rules 10.6)
    private static ref DmList get_airlock_overlay__airlock_overlays => ref TgState.Current.GlobalProcs__get_airlock_overlay__airlock_overlays;
}

// tg: code/game/machinery/airlock_control.dm:2
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/doors/airlock.dm:102 and :106  var/main_power_timer = 0, var/backup_power_timer = 0: the ids of timers
    // (BYOND timer ids are text here), so hand-written as `object` (the generated block made them numbers: the first outage ran
    // into "type mismatch" when addtimer's id was stored; M7 EMP, a pulse of the power wires reaches loseMainPower)
    // A hand-written var makes the class's var block hand-written (types-converter.md 3.1, "Hand"): the constructor with tg's
    // overrides (airlock.dm:74-91, :157-158; airlock_control.dm:3) and the list defaults (dm_init_vars) are here.
    public object main_power_timer = 0f;
    public object backup_power_timer = 0f;

    // tg: code/game/machinery/airlock_control.dm:3
    public obj_machinery_door_airlock()
    {
        opens_with_door_remote = TRUE;
        name = "Airlock";
        icon = Dm.resource("icons/obj/doors/airlocks/station/public.dmi");
        icon_state = "closed";
        base_icon_state = null;
        max_integrity = 300;
        integrity_failure = 0.25f;
        damage_deflection = AIRLOCK_DAMAGE_DEFLECTION_N;
        autoclose = TRUE;
        explosion_block = 1;
        smoothing_groups = SMOOTH_GROUP_AIRLOCK;
        interaction_flags_machine = INTERACT_MACHINE_WIRES_IF_OPEN | INTERACT_MACHINE_ALLOW_SILICON | INTERACT_MACHINE_OPEN_SILICON | INTERACT_MACHINE_OPEN;
        interaction_flags_click = ALLOW_SILICON_REACH;
        blocks_emissive = EMISSIVE_BLOCK_NONE; // Custom emissive blocker. We don't want the normal behavior.
        flags_1 = HTML_USE_INITAL_ICON_1;
        rad_insulation = RAD_MEDIUM_INSULATION;
    }

    // tg: code/game/machinery/doors/airlock.dm:86, :116  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hud_possible = Dm.list(DIAG_AIRLOCK_HUD);
        close_others = Dm.list();
    }

    // tg: code/game/machinery/doors/airlock.dm:49-73  the file-local #defines (#undef at :2642-2665)
    private const string AIRLOCK_FRAME_CLOSED = "closed";
    private const string AIRLOCK_FRAME_CLOSING = "closing";
    private const string AIRLOCK_FRAME_OPEN = "open";
    private const string AIRLOCK_FRAME_OPENING = "opening";
    private const int AIRLOCK_SECURITY_NONE = 0; //Normal airlock //Wires are not secured
    private const int AIRLOCK_SECURITY_IRON = 1; //Medium security airlock //There is a simple iron plate over wires (use welder)
    private const int AIRLOCK_SECURITY_PLASTEEL_I_S = 2; //Sliced inner plating (use crowbar), jumps to 0
    private const int AIRLOCK_SECURITY_PLASTEEL_I = 3; //Removed outer plating, second layer here (use welder)
    private const int AIRLOCK_SECURITY_PLASTEEL_O_S = 4; //Sliced outer plating (use crowbar)
    private const int AIRLOCK_SECURITY_PLASTEEL_O = 5; //There is first layer of plasteel (use welder)
    private const int AIRLOCK_SECURITY_PLASTEEL = 6; //Max security airlock //Fully secured wires (use wirecutters to remove grille, that is electrified)
    private const int AIRLOCK_INTEGRITY_N = 300; // Normal airlock integrity
    private const float AIRLOCK_INTEGRITY_MULTIPLIER = 1.5f; // How much reinforced doors health increases
    /// How much extra health airlocks get when braced with a seal
    private const int AIRLOCK_SEAL_MULTIPLIER = 2;
    private const int AIRLOCK_DAMAGE_DEFLECTION_N = 21; // Normal airlock damage deflection
    private const int AIRLOCK_DAMAGE_DEFLECTION_R = 30; // Reinforced airlock damage deflection
    private const float AIRLOCK_DENY_ANIMATION_TIME = 0.6f * SECONDS; /// The amount of time for the airlock deny animation to show
    private const int DOOR_CLOSE_WAIT = 60; /// Time before a door closes, if not overridden
    private const int DOOR_VISION_DISTANCE = 11; ///The maximum distance a door will see out to

    /// Forces the airlock to unbolt and open
    // tg: code/game/machinery/airlock_control.dm:17
    public virtual async DmTask secure_open()
    {
        locked = FALSE;
        update_appearance();

        await stoplag(0.2f * SECONDS);
        await open(FORCING_DOOR_CHECKS);

        locked = TRUE;
        update_appearance();
    }

    /// Forces the airlock to close and bolt
    // tg: code/game/machinery/airlock_control.dm:28
    public virtual async DmTask secure_close(float? force_crush = null)
    {
        force_crush ??= FALSE;
        locked = FALSE;
        await close(forced: TRUE, force_crush);

        locked = TRUE;
        await stoplag(0.2f * SECONDS);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:166
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(glass))
            airlock_material = "glass";
        dot = base.Initialize(mapload, __extra);
        set_wires(Waiting.NoSleep(get_wires()) as datum_wires);
        if (security_level > AIRLOCK_SECURITY_IRON)
        {
            atom_integrity = normal_integrity * AIRLOCK_INTEGRITY_MULTIPLIER;
            max_integrity = normal_integrity * AIRLOCK_INTEGRITY_MULTIPLIER;
        }
        else
        {
            atom_integrity = normal_integrity;
            max_integrity = normal_integrity;
        }
        if (damage_deflection == AIRLOCK_DAMAGE_DEFLECTION_N && security_level > AIRLOCK_SECURITY_IRON)
            damage_deflection = AIRLOCK_DAMAGE_DEFLECTION_R;

        prepare_huds();
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.add_atom_to_hud(this);

        diag_hud_set_electrified();

        // Click on the floor to close airlocks
        Dm.Deferred("code/game/machinery/doors/airlock.dm:187", "Initialize: AddComponent(/datum/component/redirect_attack_hand_from_turf, interact_check = CALLBACK(src, PROC_REF(drag_check))): clicking the floor below (clicks, M4a)");

        Waiting.NoSleep(AddElement(typeof(datum_element_nav_computer_icon), Dm.resource("icons/effects/nav_computer_indicators.dmi"), "airlock", TRUE));

        RegisterSignal(this, COMSIG_MACHINERY_BROKEN, nameof(on_break));

        RegisterSignal(SSdcs, COMSIG_GLOB_GREY_TIDE, nameof(grey_tide));
        return dot;
    }

    // if dragging, block 'Click on the floor to close airlocks'
    // tg: code/game/machinery/doors/airlock.dm:196
    public virtual object drag_check(mob user = null)
    {
        if (Dm.truthy(user.pulling))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:201
    [SignalHandler]
    public virtual void grey_tide(datum source = null, DmList grey_tide_areas = null)
    {
        if (!Dm.truthy(is_station_level(z)) || Dm.truthy(critical_machine))
            return; //Skip doors in critical positions, such as the SM chamber.

        foreach (object area_type in Dm.each(grey_tide_areas))
        {
            if (!istype(get_area(this), area_type as Type))
                continue;
            Waiting.Detach(prison_open()); //Sleep gets called further down in open(), so we have to invoke async
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:212
    public override DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        if (Dm.truthy(id_tag))
            id_tag = Dm.text("[]_[]", port.shuttle_id, id_tag);
        return DmTask<object>.FromResult(default);
    }

    // tg: code/game/machinery/doors/airlock.dm:216
    public virtual void update_other_id()
    {
        foreach (obj_machinery_door_airlock Airlock in Dm.each_cast<obj_machinery_door_airlock>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_airlock))))
        {
            if (Dm.eq(Airlock.closeOtherId, closeOtherId) && Airlock != this)
            {
                if (!Dm.@in(Airlock, close_others))
                    Dm.append(ref close_others, Airlock);
                if (!Dm.@in(this, Airlock.close_others))
                    Dm.append(ref Airlock.close_others, this);
            }
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:224
    public virtual void cyclelinkairlock()
    {
        if (Dm.truthy(cyclelinkedairlock))
        {
            cyclelinkedairlock.cyclelinkedairlock = null;
            cyclelinkedairlock = null;
        }
        if (!Dm.truthy(cyclelinkeddir))
            return;
        float limit = DOOR_VISION_DISTANCE;
        turf T = get_turf(this);
        obj_machinery_door_airlock FoundDoor = null;
        do
        {
            T = get_step(T, cyclelinkeddir);
            FoundDoor = Dm.locate<obj_machinery_door_airlock>(T);
            if (Dm.truthy(FoundDoor) && FoundDoor.cyclelinkeddir != get_dir(FoundDoor, this))
                FoundDoor = null;
            limit--;
        }
        while (!Dm.truthy(FoundDoor) && Dm.truthy(limit));
        if (!Dm.truthy(FoundDoor))
        {
            // GAP(lead): AREACOORD (__DEFINES/admin.dm:67) needs /atom/proc/Admin_Coordinates_Readable (admin.dm:83) and
            // /proc/get_area_name (__HELPERS/game.dm:5), not declared: the mapping log names the coordinates only.
            string AREACOORD__gap = Dm.text("([],[],[])", x, y, z);
            log_mapping(Dm.text("[] at [] failed to find a valid airlock to cyclelink with!", this, AREACOORD__gap));
            return;
        }
        FoundDoor.cyclelinkedairlock = this;
        cyclelinkedairlock = FoundDoor;
    }

    // tg: code/game/machinery/doors/airlock.dm:254
    public override void @lock()
    {
        bolt();
    }

    // tg: code/game/machinery/doors/airlock.dm:257
    public virtual void bolt()
    {
        if (Dm.truthy(locked))
            return;
        set_bolt(TRUE);
        playsound(this, boltDown, 30, FALSE, 3);
        audible_message(span_hear(I18n.T("You hear a click from the bottom of the door.")), null, 1);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:265
    public virtual object set_bolt(object should_bolt = null)
    {
        object dot = null;
        if (Dm.eq(locked, should_bolt))
            return dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_AIRLOCK_SET_BOLT]))
            this._SendSignal(COMSIG_AIRLOCK_SET_BOLT, Dm.list(this, should_bolt));
        dot = locked;
        locked = Dm.num(should_bolt);
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:272
    public override void unlock()
    {
        unbolt();
    }

    // tg: code/game/machinery/doors/airlock.dm:275
    public virtual void unbolt()
    {
        if (!Dm.truthy(locked))
            return;
        set_bolt(FALSE);
        playsound(this, boltUp, 30, FALSE, 3);
        audible_message(span_hear(I18n.T("You hear a click from the bottom of the door.")), null, 1);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:303
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref electronics);
        if (Dm.truthy(cyclelinkedairlock))
        {
            if (cyclelinkedairlock.cyclelinkedairlock == this)
                cyclelinkedairlock.cyclelinkedairlock = null;
            cyclelinkedairlock = null;
        }
        if (Dm.truthy(close_others)) //remove this airlock from the list of every linked airlock
        {
            closeOtherId = null;
            foreach (obj_machinery_door_airlock otherlock in Dm.each_cast<obj_machinery_door_airlock>(close_others))
                Dm.remove(ref otherlock.close_others, this);
            close_others.Cut();
        }
        QDEL_NULL(ref note);
        QDEL_NULL(ref seal);
        datum_atom_hud_data_diagnostic diag_hud = GLOB.huds[DATA_HUD_DIAGNOSTIC] as datum_atom_hud_data_diagnostic;
        diag_hud.remove_atom_from_hud(this);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/doors/airlock.dm:320
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (gone == note)
        {
            note = null;
            update_appearance();
        }
        if (gone == seal)
        {
            seal = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:329
    public override async DmTask bumpopen(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(hasPower()))
            return;

        if (issilicon(user) || !iscarbon(user))
        {
            await base.bumpopen(user, __extra);
            return;
        }

        if (Dm.truthy(isElectrified()) && Dm.truthy(await shock__proc(user as mob_living, 100)))
            return;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(user._listen_lookup?[COMSIG_CARBON_BUMPED_AIRLOCK_OPEN]) ? NONE : user._SendSignal(COMSIG_CARBON_BUMPED_AIRLOCK_OPEN, Dm.list(user, this))), STOP_BUMP)))
            return;

        await base.bumpopen(user, __extra);
    }

    // tg: code/game/machinery/doors/airlock.dm:344
    public virtual object isElectrified()
    {
        return Dm.num(secondsElectrified != MACHINE_NOT_ELECTRIFIED);
    }

    // tg: code/game/machinery/doors/airlock.dm:347
    public virtual object canAIControl(mob user = null)
    {
        return Dm.num((aiControlDisabled != AI_WIRE_DISABLED) && !Dm.truthy(isAllPowerCut()));
    }

    // tg: code/game/machinery/doors/airlock.dm:350
    public virtual object canAIHack(params object[] __extra)
    {
        return Dm.num((aiControlDisabled == AI_WIRE_DISABLED) && (!Dm.truthy(hackProof)) && (!Dm.truthy(isAllPowerCut())));
    }

    // tg: code/game/machinery/doors/airlock.dm:353
    public override object hasPower()
    {
        return Dm.num((!Dm.truthy(remaining_main_outage()) || !Dm.truthy(remaining_backup_outage())) && !Dm.truthy(Dm.bitand(machine_stat, NOPOWER)));
    }

    // tg: code/game/machinery/doors/airlock.dm:356
    public override object requiresID()
    {
        return Dm.num(!(Dm.truthy(wires.is_cut(WIRE_IDSCAN)) || Dm.truthy(aiDisabledIdScanner)));
    }

    // tg: code/game/machinery/doors/airlock.dm:359
    public virtual object isAllPowerCut()
    {
        if ((Dm.truthy(wires.is_cut(WIRE_POWER1)) || Dm.truthy(wires.is_cut(WIRE_POWER2))) && (Dm.truthy(wires.is_cut(WIRE_BACKUP1)) || Dm.truthy(wires.is_cut(WIRE_BACKUP2))))
            return TRUE;
        return null;
    }

    /// Returns the amount of time we have to wait before main power comes back
    /// Assuming it was actively regenerating
    /// Returns 0 if it is active
    // tg: code/game/machinery/doors/airlock.dm:366
    public virtual object remaining_main_outage()
    {
        if (Dm.truthy(main_power_timer))
            return timeleft(main_power_timer);
        return main_power_time;
    }

    /// Returns the amount of time we have to wait before backup power comes back
    /// Assuming it was actively regenerating
    /// Returns 0 if it is active
    // tg: code/game/machinery/doors/airlock.dm:374
    public virtual object remaining_backup_outage()
    {
        if (Dm.truthy(backup_power_timer))
            return timeleft(backup_power_timer);
        return backup_power_time;
    }

    // tg: code/game/machinery/doors/airlock.dm:379
    public virtual void set_main_outage(object delay = null)
    {
        // Clear out the timer so we don't accidentially take from it later
        if (Dm.truthy(main_power_timer))
        {
            deltimer(main_power_timer);
            main_power_timer = null;
        }
        object old_time = main_power_time;
        main_power_time = delay;
        handle_main_power();
        if (Dm.truthy(old_time) != Dm.truthy(delay))
            update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:390
    public virtual void set_backup_outage(object delay = null)
    {
        // Clear out the timer so we don't accidentially take from it later
        if (Dm.truthy(backup_power_timer))
        {
            deltimer(backup_power_timer);
            backup_power_timer = null;
        }
        object old_time = backup_power_time;
        backup_power_time = delay;
        handle_backup_power();
        if (Dm.truthy(old_time) != Dm.truthy(delay))
            update_appearance();
    }

    /// Call to update our main power outage timer
    /// Will trigger a proper timer if we're actively restoring power, if not we'll dump the remaining time in a var on the airlock
    // tg: code/game/machinery/doors/airlock.dm:403
    public virtual void handle_main_power()
    {
        if (Dm.le(main_power_time, 0))
        {
            deltimer(main_power_timer);
            main_power_timer = null;
            return;
        }

        // If we can, we'll start a timer that hits when we're done
        if (!Dm.truthy(wires.is_cut(WIRE_POWER1)) && !Dm.truthy(wires.is_cut(WIRE_POWER2)))
        {
            if (!Dm.truthy(main_power_timer) || !Dm.eq(timeleft(main_power_timer), main_power_time))
                main_power_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(regainMainPower)), Dm.num_or_null(main_power_time), Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/game/machinery/doors/airlock.dm", line: 412);
        }
        // Otherwise, we'll ensure the timer matches main_power_time
        else if (Dm.truthy(main_power_timer))
        {
            main_power_time = timeleft(main_power_timer);
            deltimer(main_power_timer);
            main_power_timer = null;
        }
    }

    /// Call to update our backup power outage timer
    /// Will trigger a proper timer if we're actively restoring power, if not we'll dump the remaining time in a var on the airlock
    // tg: code/game/machinery/doors/airlock.dm:421
    public virtual void handle_backup_power()
    {
        if (Dm.le(backup_power_time, 0))
        {
            deltimer(backup_power_timer);
            backup_power_timer = null;
            return;
        }

        // If we can, we'll start a timer that hits when we're done
        if (!Dm.truthy(wires.is_cut(WIRE_BACKUP1)) && !Dm.truthy(wires.is_cut(WIRE_BACKUP2)))
        {
            if (!Dm.truthy(backup_power_timer) || !Dm.eq(timeleft(backup_power_timer), backup_power_time))
                backup_power_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(regainBackupPower)), Dm.num_or_null(backup_power_time), Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/game/machinery/doors/airlock.dm", line: 430);
        }
        // Otherwise, we'll ensure the timer matches backup_power_time
        else if (Dm.truthy(backup_power_timer))
        {
            backup_power_time = timeleft(backup_power_timer);
            deltimer(backup_power_timer);
            backup_power_timer = null;
        }
    }

    // Alright, we're gonna do a meme here
    // tg: code/game/machinery/doors/airlock.dm:438
    public override void set_wires(datum_wires new_wires = null)
    {
        if (Dm.truthy(wires))
        {
            UnregisterSignal(wires, Dm.list(
                COMSIG_CUT_WIRE(WIRE_POWER1),
                COMSIG_CUT_WIRE(WIRE_POWER2),
                COMSIG_CUT_WIRE(WIRE_BACKUP1),
                COMSIG_CUT_WIRE(WIRE_BACKUP2),
                COMSIG_MEND_WIRE(WIRE_POWER1),
                COMSIG_MEND_WIRE(WIRE_POWER2),
                COMSIG_MEND_WIRE(WIRE_BACKUP1),
                COMSIG_MEND_WIRE(WIRE_BACKUP2)
                ));
        }
        base.set_wires(new_wires);
        if (Dm.truthy(new_wires))
        {
            RegisterSignals(new_wires, Dm.list(
                COMSIG_CUT_WIRE(WIRE_POWER1),
                COMSIG_CUT_WIRE(WIRE_POWER2),
                COMSIG_CUT_WIRE(WIRE_BACKUP1),
                COMSIG_CUT_WIRE(WIRE_BACKUP2),
                COMSIG_MEND_WIRE(WIRE_POWER1),
                COMSIG_MEND_WIRE(WIRE_POWER2),
                COMSIG_MEND_WIRE(WIRE_BACKUP1),
                COMSIG_MEND_WIRE(WIRE_BACKUP2)
                ), nameof(power_wires_changed));
        }
    }

    /// If our power wires have changed, then our backup/main power regen may have failed, so let's just check in yeah?
    // tg: code/game/machinery/doors/airlock.dm:464
    [SignalHandler]
    public virtual void power_wires_changed(datum source = null, object wire = null)
    {
        handle_main_power();
        handle_backup_power();
    }

    // tg: code/game/machinery/doors/airlock.dm:469
    public virtual void regainMainPower()
    {
        set_main_outage(0 * SECONDS);
    }

    // tg: code/game/machinery/doors/airlock.dm:472
    public virtual void loseMainPower()
    {
        if (!Dm.truthy(remaining_main_outage()))
        {
            set_main_outage(60 * SECONDS);
            if (Dm.lt(remaining_backup_outage(), 10 * SECONDS))
                set_backup_outage(10 * SECONDS);
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:478
    public virtual void loseBackupPower()
    {
        if (Dm.lt(remaining_backup_outage(), 60 * SECONDS))
            set_backup_outage(60 * SECONDS);
    }

    // tg: code/game/machinery/doors/airlock.dm:482
    public virtual void regainBackupPower()
    {
        set_backup_outage(0 * SECONDS);
    }

    // tg: code/game/machinery/doors/airlock.dm:485  (the M7 shocks slice: an airlock electrified by an EMP or the shock wire)
    public override async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        if (!Dm.truthy(hasPower())) // unpowered, no shock
            return FALSE;
        if (!isliving(shocking))
            return FALSE;
        if (HAS_TRAIT(shocking, TRAIT_AIRLOCK_SHOCKIMMUNE)) // Be a bit more clever man come on
            return FALSE;
        if (!(shockCooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return FALSE; //Already shocked someone recently?
        if (!Dm.truthy(await base.shock__proc(shocking, chance, shock_source, siemens_coeff)))
            return FALSE;
        shockCooldown = world.time + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        // Provides timed airlock shock immunity, to prevent overly cheesy deathtraps
        ADD_TRAIT(shocking, TRAIT_AIRLOCK_SHOCKIMMUNE, REF(this));
        _addtimer(TRAIT_CALLBACK_REMOVE(shocking, TRAIT_AIRLOCK_SHOCKIMMUNE, REF(this)), 1 * SECONDS, file: "code/game/machinery/doors/airlock.dm", line: 499);
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:502
    public virtual object is_secure()
    {
        return Dm.num(security_level > 0);
    }

    /**
     * Checks if this door would be affected by any currently active RETA grants
     * If a grant is active, return the authorized department
     */
    // tg: code/game/machinery/doors/airlock.dm:509
    public virtual object has_active_reta_access()
    {
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_reta_enabled))))
            return FALSE;

        if (!Dm.truthy(length(req_access)) && !Dm.truthy(length(req_one_access)))
            return FALSE;

        // Check if this door belongs to a department providing access via RETA
        foreach (object target_dept in Dm.each(GLOB.reta_active_grants))
        {
            DmList active_origins = GLOB.reta_active_grants[target_dept] as DmList;
            foreach (object origin_dept in Dm.each(active_origins))
            {
                DmList origin_dept_access = GLOB.reta_dept_grants[origin_dept] as DmList;
                if (!Dm.truthy(origin_dept_access))
                    continue;

                foreach (object required_access in Dm.each(req_access))
                {
                    if (Dm.@in(required_access, origin_dept_access))
                        return LOWER_TEXT(target_dept);
                }

                foreach (object required_access in Dm.each(req_one_access))
                {
                    if (Dm.@in(required_access, origin_dept_access))
                        return LOWER_TEXT(target_dept);
                }
            }
        }

        return FALSE;
    }

    /**
     * Set the airlock state to a new value, change the icon state
     * and run the associated animation if required.
     */
    // tg: code/game/machinery/doors/airlock.dm:538
    public virtual async DmTask set_airlock_state(object new_state = null, float? animated = null, float? force_type = null)
    {
        animated ??= FALSE;
        force_type ??= DEFAULT_DOOR_CHECKS;
        if (!Dm.truthy(new_state))
            new_state = Dm.truthy(density) ? AIRLOCK_CLOSED : AIRLOCK_OPEN;
        airlock_state = new_state;
        if (Dm.truthy(animated))
        {
            operating = TRUE;
            await run_animation(airlock_state, force_type);
            return;
        }
        operating = FALSE;
        set_animation();
    }

    // tg: code/game/machinery/doors/airlock.dm:549
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        if (!Dm.truthy(airlock_state))
            airlock_state = icon_state;

        return await base.update_icon(updates, __extra);
    }

    // tg: code/game/machinery/doors/airlock.dm:555
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        if (Dm.truthy(animation))
            icon_state = Dm.text("[][]", base_icon_state, animation);
        else if (Dm.eq(airlock_state, AIRLOCK_OPEN))
            icon_state = Dm.text("[]open", base_icon_state);
        else
            icon_state = Dm.text("[]closed", base_icon_state);
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:564
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);

        string frame_state = null;
        string light_state = null;
        if (Dm.truthy(Dm.bitand(machine_stat, MAINT))) // in the process of being emagged
            frame_state = AIRLOCK_FRAME_CLOSED;
        else
        {
            object sw__1 = airlock_state;
            if (Dm.eq(sw__1, AIRLOCK_CLOSED))
            {
                frame_state = AIRLOCK_FRAME_CLOSED;
                if (Dm.truthy(locked))
                    light_state = AIRLOCK_LIGHT_BOLTS;
                else if (Dm.truthy(emergency))
                    light_state = AIRLOCK_LIGHT_EMERGENCY;
                else if (Dm.truthy(has_active_reta_access()))
                    light_state = AIRLOCK_LIGHT_RETA;
            }
            else if (Dm.eq(sw__1, AIRLOCK_DENY))
            {
                frame_state = AIRLOCK_FRAME_CLOSED;
                light_state = AIRLOCK_LIGHT_DENIED;
            }
            else if (Dm.eq(sw__1, AIRLOCK_CLOSING))
            {
                frame_state = AIRLOCK_FRAME_CLOSING;
                light_state = AIRLOCK_LIGHT_CLOSING;
            }
            else if (Dm.eq(sw__1, AIRLOCK_OPEN))
                frame_state = AIRLOCK_FRAME_OPEN;
            else if (Dm.eq(sw__1, AIRLOCK_OPENING))
            {
                frame_state = AIRLOCK_FRAME_OPENING;
                light_state = AIRLOCK_LIGHT_OPENING;
            }
        }

        Dm.append(ref dot, get_airlock_overlay(frame_state, icon, this, em_block: TRUE));
        if (Dm.truthy(airlock_material))
            Dm.append(ref dot, get_airlock_overlay(Dm.text("[]_[]", airlock_material, frame_state), overlays_file, this, em_block: TRUE));
        else
            Dm.append(ref dot, get_airlock_overlay(Dm.text("fill_[]", frame_state), icon, this, em_block: TRUE));

        if (Dm.truthy(feedback) && Dm.truthy(hasPower()) && Dm.truthy(light_state))
            Dm.append(ref dot, get_airlock_overlay(Dm.text("lights_[]", light_state), overlays_file, this, em_block: FALSE));

        if (Dm.truthy(panel_open))
            Dm.append(ref dot, get_airlock_overlay(Dm.text("panel_[][]", frame_state, security_level != 0 ? "_protected" : null), overlays_file, this, em_block: TRUE));

        if (frame_state == AIRLOCK_FRAME_CLOSED && Dm.truthy(welded))
            Dm.append(ref dot, get_airlock_overlay("welded", overlays_file, this, em_block: TRUE));

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT))) // in the process of being emagged
            Dm.append(ref dot, get_airlock_overlay("sparks", overlays_file, this, em_block: FALSE));

        if (Dm.truthy(hasPower()))
        {
            if (frame_state == AIRLOCK_FRAME_CLOSED)
            {
                if (Dm.lt(atom_integrity, Dm.mul(integrity_failure, max_integrity)))
                    Dm.append(ref dot, get_airlock_overlay("sparks_broken", overlays_file, this, em_block: FALSE));
                else if (Dm.lt(atom_integrity, Dm.mul(0.75f, max_integrity)))
                    Dm.append(ref dot, get_airlock_overlay("sparks_damaged", overlays_file, this, em_block: FALSE));
            }
            else if (frame_state == AIRLOCK_FRAME_OPEN)
            {
                if (Dm.lt(atom_integrity, Dm.mul(0.75f, max_integrity)))
                    Dm.append(ref dot, get_airlock_overlay("sparks_open", overlays_file, this, em_block: FALSE));
            }
        }

        if (Dm.truthy(note))
            Dm.append(ref dot, get_airlock_overlay(get_note_state(frame_state), note_overlay_file, this, em_block: TRUE));

        if (frame_state == AIRLOCK_FRAME_CLOSED && Dm.truthy(seal))
            Dm.append(ref dot, get_airlock_overlay("sealed", overlays_file, this, em_block: TRUE));

        if (Dm.truthy(hasPower()) && Dm.truthy(unres_sides))
        {
            foreach (object heading in Dm.each(Dm.list(NORTH, SOUTH, EAST, WEST)))
            {
                if (!Dm.truthy(Dm.bitand(unres_sides, heading)))
                    continue;
                mutable_appearance floorlight = mutable_appearance(Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi"), Dm.text("unres_[]", heading), FLOAT_LAYER, this, O_LIGHTING_VISUAL_PLANE, appearance_flags: Dm.bitor(RESET_COLOR, KEEP_APART)) as mutable_appearance;
                floorlight.color = LIGHT_COLOR_DEFAULT;
                object sw__2 = heading;
                if (Dm.eq(sw__2, NORTH))
                {
                    floorlight.pixel_w = 0;
                    floorlight.pixel_z = 32;
                }
                else if (Dm.eq(sw__2, SOUTH))
                {
                    floorlight.pixel_w = 0;
                    floorlight.pixel_z = -32;
                }
                else if (Dm.eq(sw__2, EAST))
                {
                    floorlight.pixel_w = 32;
                    floorlight.pixel_z = 0;
                }
                else if (Dm.eq(sw__2, WEST))
                {
                    floorlight.pixel_w = -32;
                    floorlight.pixel_z = 0;
                }
                Dm.append(ref dot, floorlight);
            }
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:647
    public override async DmTask run_animation(object animation = null, float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        if (Dm.eq(animation, DOOR_DENY_ANIMATION))
        {
            if (Dm.truthy(machine_stat))
                return;
            await set_airlock_state(AIRLOCK_DENY, animated: FALSE, force_type: force_type);
        }

        await base.run_animation(animation, force_type);
    }

    // tg: code/game/machinery/doors/airlock.dm:655
    public override async DmTask animation_effects(object animation = null, float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        if (force_type == BYPASS_DOOR_CHECKS)
        {
            playsound(this, soundin: Dm.resource("sound/machines/airlock/airlockforced.ogg"), vol: 30, vary: TRUE);
            return;
        }

        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
        {
            await use_energy(50 * JOULES);
            playsound(this, soundin: doorOpen, vol: 30, vary: TRUE);
        }
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
        {
            await use_energy(50 * JOULES);
            playsound(this, soundin: doorClose, vol: 30, vary: TRUE);
        }
        else if (Dm.eq(sw__1, DOOR_DENY_ANIMATION))
        {
            if (Dm.truthy(feedback))
                playsound(this, soundin: doorDeni, vol: 50, vary: FALSE, extrarange: 3);
            _addtimer(Dm.@new<datum_callback>(this, nameof(handle_deny_end)), AIRLOCK_DENY_ANIMATION_TIME, file: "code/game/machinery/doors/airlock.dm", line: 670);
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:672
    public virtual async DmTask handle_deny_end()
    {
        if (Dm.eq(airlock_state, AIRLOCK_DENY))
            await set_airlock_state(AIRLOCK_CLOSED, animated: FALSE);
    }

    // tg: code/game/machinery/doors/airlock.dm:676
    public override object animation_length(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, DOOR_OPENING_ANIMATION))
            return 0.6f * SECONDS;
        else if (Dm.eq(sw__1, DOOR_CLOSING_ANIMATION))
            return 0.6f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:683
    public override object animation_segment_delay(object animation = null)
    {
        object sw__1 = animation;
        if (Dm.eq(sw__1, AIRLOCK_OPENING_TRANSPARENT))
            return 0.1f * SECONDS;
        else if (Dm.eq(sw__1, AIRLOCK_OPENING_PASSABLE))
            return 0.5f * SECONDS;
        else if (Dm.eq(sw__1, AIRLOCK_OPENING_FINISHED))
            return 0.6f * SECONDS;
        else if (Dm.eq(sw__1, AIRLOCK_CLOSING_UNPASSABLE))
            return 0.2f * SECONDS;
        else if (Dm.eq(sw__1, AIRLOCK_CLOSING_OPAQUE))
            return 0.5f * SECONDS;
        else if (Dm.eq(sw__1, AIRLOCK_CLOSING_FINISHED))
            return 0.6f * SECONDS;
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:698
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(closeOtherId))
            Dm.append(ref dot, span_warning(I18n.T("This airlock cycles on ID: [].", sanitize(closeOtherId))));
        else if (cyclelinkedairlock != null)
            Dm.append(ref dot, span_warning(I18n.T("This airlock cycles with: [].", cyclelinkedairlock.name)));
        else
            Dm.append(ref dot, span_warning(I18n.T("This airlock does not cycle.")));
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_warning(I18n.T("Its access panel is smoking slightly.")));
        if (note != null)
        {
            if (!in_range(user, this))
                Dm.append(ref dot, I18n.T("There's a [] pinned to the front. You can't read it from here.", note.name));
            else
            {
                Dm.append(ref dot, I18n.T("There's a [] pinned to the front...", note.name));
                Dm.append(ref dot, await note.examine(user));
            }
            Dm.append(ref dot, span_notice(I18n.T("The attached [] can be taken down with some [].", note.name, EXAMINE_HINT(I18n.T("wirecutters")))));
        }
        if (seal != null)
            Dm.append(ref dot, I18n.T("It's been braced with \\a [].", seal));
        if (Dm.truthy(welded))
            Dm.append(ref dot, I18n.T("It's welded shut."));
        if (Dm.truthy(panel_open))
        {
            float sw__1 = security_level;
            if (sw__1 == AIRLOCK_SECURITY_NONE)
                Dm.append(ref dot, I18n.T("Its wires are exposed!"));
            else if (sw__1 == AIRLOCK_SECURITY_IRON)
                Dm.append(ref dot, I18n.T("Its wires are hidden behind a welded iron cover."));
            else if (sw__1 == AIRLOCK_SECURITY_PLASTEEL_I_S)
                Dm.append(ref dot, I18n.T("There is some shredded plasteel inside."));
            else if (sw__1 == AIRLOCK_SECURITY_PLASTEEL_I)
                Dm.append(ref dot, I18n.T("Its wires are behind an inner layer of plasteel."));
            else if (sw__1 == AIRLOCK_SECURITY_PLASTEEL_O_S)
                Dm.append(ref dot, I18n.T("There is some shredded plasteel inside."));
            else if (sw__1 == AIRLOCK_SECURITY_PLASTEEL_O)
                Dm.append(ref dot, I18n.T("There is a welded plasteel cover hiding its wires."));
            else if (sw__1 == AIRLOCK_SECURITY_PLASTEEL)
                Dm.append(ref dot, I18n.T("There is a protective grille over its panel."));
        }
        else if (Dm.truthy(security_level))
        {
            if (security_level == AIRLOCK_SECURITY_IRON)
                Dm.append(ref dot, I18n.T("It looks a bit stronger."));
            else
                Dm.append(ref dot, I18n.T("It looks very robust."));
        }

        object active_reta = has_active_reta_access();
        if (Dm.truthy(active_reta))
            Dm.append(ref dot, span_nicegreen(I18n.T("Emergency Temporary Access is enabled for [] ID cards.", EXAMINE_HINT(active_reta))));

        if (issilicon(user) && !Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
        {
            Dm.append(ref dot, span_notice(I18n.T("Shift-click [] to [] it.", this, Dm.truthy(density) ? I18n.T("open") : I18n.T("close"))));
            Dm.append(ref dot, span_notice(I18n.T("Ctrl-click [] to [] its bolts.", this, Dm.truthy(locked) ? I18n.T("raise") : I18n.T("drop"))));
            Dm.append(ref dot, span_notice(I18n.T("Alt-click [] to [] it.", this, Dm.truthy(secondsElectrified) ? I18n.T("un-electrify") : I18n.T("permanently electrify"))));
            Dm.append(ref dot, span_notice(I18n.T("Ctrl-Shift-click [] to [] emergency access.", this, Dm.truthy(emergency) ? I18n.T("disable") : I18n.T("enable"))));
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:809
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (!Dm.truthy(canAIControl(user)))
        {
            if (Dm.truthy(canAIHack()))
            {
                hack(user);
                return null;
            }
            else
                to_chat(user, span_warning(I18n.T("Airlock AI control has been blocked with a firewall. Unable to hack.")));
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            to_chat(user, span_warning(I18n.T("Unable to interface: Airlock is unresponsive.")));
            return null;
        }

        await ui_interact(user);
        return null;
    }

    ///Performs basic checks to make sure we are still able to hack an airlock. If control is restored early through outside means, opens the airlock's control interface.
    // tg: code/game/machinery/doors/airlock.dm:823
    public virtual async DmTask<object> check_hacking(mob user = null, object success_message = null)
    {
        if (QDELETED(this))
        {
            to_chat(user, span_warning(I18n.T("Connection lost! Unable to locate airlock on network.")));
            aiHacking = FALSE;
            return FALSE;
        }
        if (Dm.truthy(canAIControl(user)))
        {
            to_chat(user, span_notice(I18n.T("Alert cancelled. Airlock control has been restored without our assistance.")));
            aiHacking = FALSE;
            if (user != null)
                await attack_ai(user); //bring up airlock dialog
            return null;
        }
        else if (!Dm.truthy(canAIHack()))
        {
            to_chat(user, span_warning(I18n.T("Connection lost! Unable to hack airlock.")));
            aiHacking = FALSE;
            return null;
        }
        if (Dm.truthy(success_message))
            to_chat(user, span_notice(success_message as string));
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:843
    public virtual void hack(mob user = null)
    {
        Waiting.WaitforFalse(hack__body(user)); // set waitfor = 0
    }

    private async DmTask hack__body(mob user)
    {
        if (!Dm.truthy(aiHacking))
        {
            aiHacking = TRUE;
            to_chat(user, span_warning(I18n.T("Airlock AI control has been blocked. Beginning fault-detection.")));
            await sleep(5 * SECONDS);

            if (!Dm.truthy(await check_hacking(user, I18n.T("Fault confirmed: airlock control wire disabled or cut."))))
                return;
            await sleep(2 * SECONDS);

            if (!Dm.truthy(await check_hacking(user, I18n.T("Attempting to hack into airlock. This may take some time."))))
                return;
            await sleep(20 * SECONDS);

            if (!Dm.truthy(await check_hacking(user, I18n.T("Upload access confirmed. Loading control program into airlock software."))))
                return;
            await sleep(17 * SECONDS);

            if (!Dm.truthy(await check_hacking(user, I18n.T("Transfer complete. Forcing airlock to execute program."))))
                return;
            await sleep(5 * SECONDS);

            if (!Dm.truthy(await check_hacking(user, I18n.T("Receiving control information from airlock."))))
                return;
            aiControlDisabled = AI_WIRE_HACKED; //disable blocked control
            await sleep(1 * SECONDS);

            aiHacking = FALSE;
            if (QDELETED(this))
            {
                to_chat(user, span_warning(I18n.T("Connection lost! Unable to locate airlock on network.")));
                return;
            }
            if (user != null)
                await attack_ai(user); //bring up airlock dialog
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:886
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts yet.
        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178)
        if (!HAS_TRAIT(user, TRAIT_SILICON_ACCESS))
        {
            if (Dm.truthy(isElectrified()) && Dm.truthy(await shock__proc(user as mob_living, 100)))
                return dot;
        }

        if (user is mob_living_carbon_human H && prob(40) && Dm.truthy(density))
        {
            if (HAS_TRAIT(H, TRAIT_DUMB) && Dm.truthy(await Adjacent(user)))
            {
                playsound(this, Dm.resource("sound/effects/bang.ogg"), 25, TRUE);
                if (H.head is not obj_item_clothing_head_helmet)
                {
                    H.visible_message(span_danger(Dm.text("[] headbutts the airlock.", user)),
                        span_userdanger(I18n.T("You headbutt the airlock!")));
                    await H.Paralyze(100);
                    await H.apply_damage(10, BRUTE, BODY_ZONE_HEAD);
                }
                else
                {
                    // GAP(lead): user.p_theyre() (__HELPERS/pronouns.dm) is not declared: the neutral "they're"
                    visible_message(span_danger(Dm.text("[] headbutts the airlock. Good thing they're wearing a helmet.", user)));
                }
            }
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:912
    public virtual async DmTask electrified_loop()
    {
        while (secondsElectrified > MACHINE_NOT_ELECTRIFIED)
        {
            await sleep(1 * SECONDS);
            if (QDELETED(this))
                return;

            if (secondsElectrified <= MACHINE_NOT_ELECTRIFIED) //make sure they weren't unelectrified during the sleep.
                break;
            secondsElectrified = max(MACHINE_NOT_ELECTRIFIED, secondsElectrified - 1); //safety to make sure we don't end up permanently electrified during a timed electrification.
        }
        // This is to protect against changing to permanent, mid loop.
        if (secondsElectrified == MACHINE_NOT_ELECTRIFIED)
            await set_electrified(MACHINE_NOT_ELECTRIFIED);
        else
            await set_electrified(MACHINE_ELECTRIFIED_PERMANENT);
    }

    // tg: code/game/machinery/doors/airlock.dm:927
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(has_access_panel))
        {
            to_chat(user, span_warning(I18n.T("[] has no maintenance panel!", this)));
            return ITEM_INTERACT_SUCCESS;
        }
        toggle_panel_open();
        to_chat(user, span_notice(I18n.T("You [] the maintenance panel of the airlock.", Dm.truthy(panel_open) ? "open" : "close")));
        tool.play_tool_sound(this);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/airlock.dm:937
    public override async DmTask<object> screwdriver_act_secondary(mob_living user = null, obj_item tool = null)
    {
        return await screwdriver_act(user, tool);
    }

    // tg: code/game/machinery/doors/airlock.dm:940
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(panel_open) && Dm.eq(security_level, AIRLOCK_SECURITY_PLASTEEL))
        {
            if (Dm.truthy(hasPower()) && Dm.truthy(await shock__proc(user, 60))) // Protective grille of wiring is electrified
                return ITEM_INTERACT_BLOCKING;
            to_chat(user, span_notice(I18n.T("You start cutting through the outer grille.")));
            if (!Dm.truthy(await tool.use_tool(this, user, 10, volume: 100)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(panel_open))  // double check it wasn't closed while we were trying to snip
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(span_notice(I18n.T("[] cut through []'s outer grille.", user, this)),
                span_notice(I18n.T("You cut through []'s outer grille.", this)));
            security_level = AIRLOCK_SECURITY_PLASTEEL_O;
            return ITEM_INTERACT_SUCCESS;
        }
        if (note != null)
        {
            if (Dm.truthy(await IsReachableBy(user)))
                user.visible_message(span_notice(I18n.T("[] cuts down [] from [].", user, note, this)), span_notice(I18n.T("You remove [] from [].", note, this)));
            else //telekinesis
                visible_message(span_notice(I18n.T("[] cuts down [] from [].", tool, note, this)));
            tool.play_tool_sound(this);
            await note.forceMove(tool.drop_location() as atom);
            note = null;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:964
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {

        if (!Dm.truthy(panel_open) || Dm.eq(security_level, AIRLOCK_SECURITY_NONE))
            return await base.crowbar_act(user, tool);

        string layer_flavor;
        float next_level;
        float starting_level = security_level;

        switch (security_level)
        {
            case AIRLOCK_SECURITY_PLASTEEL_O_S:
                layer_flavor = "outer layer of shielding";
                next_level = AIRLOCK_SECURITY_PLASTEEL_I;
                break;

            case AIRLOCK_SECURITY_PLASTEEL_I_S:
                layer_flavor = "inner layer of shielding";
                next_level = AIRLOCK_SECURITY_NONE;
                break;
            default:
                return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(span_notice(I18n.T("You start prying away []'s [].", this, layer_flavor)));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 100)))
            return ITEM_INTERACT_SUCCESS;
        if (!Dm.truthy(panel_open) || security_level != starting_level)
        {
            // if the plating's already been broken, don't break it again
            return ITEM_INTERACT_SUCCESS;
        }
        user.visible_message(span_notice(I18n.T("[] removes []'s shielding.", user, this)),
            span_notice(I18n.T("You remove []'s [].", this, layer_flavor)));
        security_level = next_level;
        spawn_atom_to_turf(typeof(obj_item_stack_sheet_plasteel), user.loc, 1);
        if (next_level == AIRLOCK_SECURITY_NONE)
        {
            await modify_max_integrity(max_integrity / AIRLOCK_INTEGRITY_MULTIPLIER);
            damage_deflection = AIRLOCK_DAMAGE_DEFLECTION_N;
            update_appearance();
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/airlock.dm:1000
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(locked))
            return null;
        if (!Dm.truthy(panel_open))
        {
            balloon_alert(user, I18n.T("panel is closed!"));
            return null;
        }
        if (!Dm.eq(security_level, AIRLOCK_SECURITY_NONE))
        {
            balloon_alert(user, I18n.T("airlock is reinforced!"));
            return null;
        }

        if (istype(tool, typeof(obj_item_wrench_bolter)))
        {
            balloon_alert(user, I18n.T("raising bolts..."));
            if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
                return null;
            unbolt();
        }

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/airlock.dm:1018
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {

        if (!Dm.truthy(panel_open) || Dm.eq(security_level, AIRLOCK_SECURITY_NONE))
            return await base.welder_act(user, tool);

        string layer_flavor;
        float next_level;
        float starting_level = security_level;

        Type material_to_spawn = null;
        float amount_to_spawn = 0;

        switch (security_level)
        {
            case AIRLOCK_SECURITY_IRON:
                layer_flavor = "panel's shielding";
                next_level = AIRLOCK_SECURITY_NONE;
                material_to_spawn = typeof(obj_item_stack_sheet_iron);
                amount_to_spawn = 2;
                break;
            case AIRLOCK_SECURITY_PLASTEEL_O:
                layer_flavor = "outer layer of shielding";
                next_level = AIRLOCK_SECURITY_PLASTEEL_O_S;
                break;
            case AIRLOCK_SECURITY_PLASTEEL_I:
                layer_flavor = "inner layer of shielding";
                next_level = AIRLOCK_SECURITY_PLASTEEL_I_S;
                break;
            default:
                return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_SUCCESS;

        to_chat(user, span_notice(I18n.T("You begin cutting the []...", layer_flavor)));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_SUCCESS;

        if (!Dm.truthy(panel_open) || security_level != starting_level)
        {
            // see if anyone's screwing with us
            return ITEM_INTERACT_SUCCESS;
        }

        user.visible_message(
            span_notice(I18n.T("[] cuts through []'s shielding.", user, this)),  // passers-by don't get the full picture
            span_notice(I18n.T("You cut through []'s [].", this, layer_flavor)),
            span_hear(I18n.T("You hear welding."))
        );

        security_level = next_level;

        if (material_to_spawn != null)
            spawn_atom_to_turf(material_to_spawn, user.loc, amount_to_spawn);

        if (security_level == AIRLOCK_SECURITY_NONE)
            update_appearance();

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/airlock.dm:1073
    public virtual async DmTask<object> try_reinforce(mob user = null, obj_item_stack_sheet material = null, object amt_required = null, object new_security_level = null)
    {
        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178), as in attacked_by
        if (!HAS_TRAIT(user, TRAIT_SILICON_ACCESS) && Dm.truthy(isElectrified()) && Dm.truthy(await shock__proc(user as mob_living, 75)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.num(material.get_amount()) < Dm.num(amt_required))
        {
            to_chat(user, span_warning(I18n.T("You need at least [] sheets of [] to reinforce [].", amt_required, material, this)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You start reinforcing [].", this)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(panel_open) || !Dm.truthy(await material.use(amt_required)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] reinforces [] with [].", user, this, material)),
            span_notice(I18n.T("You reinforce [] with [].", this, material)));
        security_level = Dm.num(new_security_level);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/doors/airlock.dm:1090
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178), with isAdminGhostAI as above
        if (!HAS_TRAIT(user, TRAIT_SILICON_ACCESS) && Dm.truthy(isElectrified()) && Dm.truthy(Dm.bitand(attacking_item.obj_flags, CONDUCTS_ELECTRICITY)) && Dm.truthy(await shock__proc(user, 75)))
            return ATTACK_FAILED;
        return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/game/machinery/doors/airlock.dm:1095
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // tg: code/game/machinery/doors/airlock.dm:1096
        add_fingerprint(user);

        // is_wire_tool(tool) (datums/wires/_wires.dm:13, check_secured = FALSE)
        // GAP(lead): /proc/is_wire_tool is not declared (datums/wires/_wires.cs, the wires' interface: M7); its body, inline.
        bool is_wire_tool__inline = istype(tool, typeof(obj_item)) && (Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER) || Dm.eq(tool.tool_behaviour, TOOL_MULTITOOL) || istype(tool, typeof(obj_item_assembly)));
        if (is_wire_tool__inline && Dm.truthy(panel_open))
        {
            await attempt_wire_interaction(user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(panel_open) && Dm.eq(security_level, AIRLOCK_SECURITY_NONE) && istype(tool, typeof(obj_item_stack_sheet)))
        {
            if (istype(tool, typeof(obj_item_stack_sheet_iron)))
                return await try_reinforce(user, (obj_item_stack_sheet)tool, 2, AIRLOCK_SECURITY_IRON);

            if (istype(tool, typeof(obj_item_stack_sheet_plasteel)))
            {
                if (Dm.truthy(Dm.bitand(await try_reinforce(user, (obj_item_stack_sheet)tool, 2, AIRLOCK_SECURITY_PLASTEEL), ITEM_INTERACT_BLOCKING)))
                    return ITEM_INTERACT_BLOCKING;
                await modify_max_integrity(max_integrity * AIRLOCK_INTEGRITY_MULTIPLIER);
                damage_deflection = AIRLOCK_DAMAGE_DEFLECTION_R;
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }

            return await base.item_interaction(user, tool, modifiers, __extra);
        }

        if (istype(tool, typeof(obj_item_pai_cable))) // silicons SI6 (D326)
        {
            var cable = (obj_item_pai_cable)tool;
            await cable.plugin(this, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_airlock_painter)))
        {
            await change_paintjob(tool as obj_item_airlock_painter, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_door_seal))) //adding the seal
        {
            obj_item_door_seal airlockseal = (obj_item_door_seal)tool;
            if (!Dm.truthy(density))
            {
                to_chat(user, span_warning(I18n.T("[] must be closed before you can seal it!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (seal != null)
            {
                to_chat(user, span_warning(I18n.T("[] has already been sealed!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] begins sealing [].", user, this)), span_notice(I18n.T("You begin sealing [].", this)));
            playsound(this, Dm.resource("sound/items/tools/jaws_pry.ogg"), 30, TRUE);
            if (!Dm.truthy(await do_after(user, airlockseal.seal_time, target: this)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(density))
            {
                to_chat(user, span_warning(I18n.T("[] must be closed before you can seal it!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (seal != null)
            {
                to_chat(user, span_warning(I18n.T("[] has already been sealed!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(airlockseal, this)))
            {
                to_chat(user, span_warning(I18n.T("For some reason, you can't attach []!", airlockseal)));
                return ITEM_INTERACT_BLOCKING;
            }
            playsound(this, Dm.resource("sound/machines/airlock/airlockforced.ogg"), 30, TRUE);
            user.visible_message(span_notice(I18n.T("[] finishes sealing [].", user, this)), span_notice(I18n.T("You finish sealing [].", this)));
            seal = airlockseal;
            await modify_max_integrity(max_integrity * AIRLOCK_SEAL_MULTIPLIER);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paper)) || istype(tool, typeof(obj_item_photo)))
        {
            if (note != null)
            {
                to_chat(user, span_warning(I18n.T("There's already something pinned to this airlock! Use wirecutters to remove it.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("For some reason, you can't attach []!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] pins [] to [].", user, tool, this)), span_notice(I18n.T("You pin [] to [].", tool, this)));
            note = tool;
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }


    // tg: code/game/machinery/doors/airlock.dm:1168
    public override async DmTask try_to_weld(obj_item_weldingtool W = null, mob user = null)
    {
        if (!Dm.truthy(operating) && Dm.truthy(density))
        {
            if (seal != null)
            {
                to_chat(user, span_warning(I18n.T("[] is blocked by a seal!", this)));
                return;
            }

            if (Dm.lt(atom_integrity, max_integrity))
            {
                if (!Dm.truthy(W.tool_start_check(user as mob_living, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
                    return;
                user.visible_message(span_notice(I18n.T("[] begins welding the airlock.", user)),
                                span_notice(I18n.T("You begin repairing the airlock...")),
                                span_hear(I18n.T("You hear welding.")));
                if (Dm.truthy(await W.use_tool(this, user as mob_living, 40, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(weld_checks), W, user))))
                {
                    atom_integrity = max_integrity;
                    await set_machine_stat(Dm.bitand(machine_stat, Dm.bitnot(BROKEN)));
                    user.visible_message(span_notice(I18n.T("[] finishes welding [].", user, this)),
                                        span_notice(I18n.T("You finish repairing the airlock.")));
                    update_appearance();
                }
            }
            else
                to_chat(user, span_notice(I18n.T("The airlock doesn't need repairing.")));
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:1189
    public override async DmTask try_to_weld_secondary(obj_item_weldingtool tool = null, mob user = null)
    {
        if (!Dm.truthy(tool.tool_start_check(user as mob_living, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return;
        user.visible_message(span_notice(I18n.T("[] begins [] the airlock.", user, Dm.truthy(welded) ? "unwelding" : "welding")),
            span_notice(I18n.T("You begin [] the airlock...", Dm.truthy(welded) ? "unwelding" : "welding")),
            span_hear(I18n.T("You hear welding.")));
        if (!Dm.truthy(await tool.use_tool(this, user as mob_living, 40, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(weld_checks), tool, user))))
            return;
        welded = Dm.not(welded);
        user.visible_message(span_notice(I18n.T("[] [] [].", user, Dm.truthy(welded) ? "welds shut" : "unwelds", this)),
            span_notice(I18n.T("You [].", Dm.truthy(welded) ? "weld the airlock shut" : "unweld the airlock")));
        user.log_message(Dm.text("[] airlock [] with [].", Dm.truthy(welded) ? "welded" : "unwelded", this, tool), LOG_GAME);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:1203
    public virtual object weld_checks(obj_item_weldingtool W = null, mob user = null)
    {
        return Dm.num(!Dm.truthy(operating) && Dm.truthy(density));
    }

    /**
     * Called when attempting to remove the seal from an airlock
     *
     * Here because we need to call it and return if there was a seal so we don't try to open the door
     * or try its safety lock while it's sealed
     * Returns TRUE (there was a seal and we either removed it or failed to)
     * or FALSE (there was no seal)
     */
    // tg: code/game/machinery/doors/airlock.dm:1214
    public override async DmTask<object> try_remove_seal(mob user = null)
    {
        if (seal == null)
            return FALSE;
        var airlockseal = (obj_item_door_seal)seal;
        if (user is not mob_living_carbon_human)
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to remove the seal!")));
            return TRUE;
        }
        user.visible_message(span_notice(Dm.text("[] begins removing the seal from [].", user, this)), span_notice(Dm.text("You begin removing []'s pneumatic seal.", this)));
        playsound(this, Dm.resource("sound/machines/airlock/airlockforced.ogg"), 30, TRUE);
        if (!Dm.truthy(await do_after(user, airlockseal.unseal_time, target: this)))
            return TRUE;
        if (seal == null)
            return TRUE;
        playsound(this, Dm.resource("sound/items/tools/jaws_pry.ogg"), 30, TRUE);
        await airlockseal.forceMove(get_turf(user) as atom);
        user.visible_message(span_notice(Dm.text("[] finishes removing the seal from [].", user, this)), span_notice(Dm.text("You finish removing []'s pneumatic seal.", this)));
        seal = null;
        await modify_max_integrity(max_integrity / AIRLOCK_SEAL_MULTIPLIER);
        update_appearance();
        return TRUE;
    }

    /// Returns if a crowbar would remove the airlock electronics
    // tg: code/game/machinery/doors/airlock.dm:1236
    public virtual object should_try_removing_electronics()
    {
        if (!Dm.eq(security_level, 0))
            return FALSE;

        if (!Dm.truthy(panel_open))
            return FALSE;

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return TRUE;

        if (!Dm.truthy(density))
            return FALSE;

        if (!Dm.truthy(welded))
            return FALSE;

        if (Dm.truthy(hasPower()))
            return FALSE;

        if (Dm.truthy(locked))
            return FALSE;

        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:1260
    public override async DmTask try_to_crowbar(obj_item tool = null, mob user = null, float? forced = null)
    {
        forced ??= FALSE;
        if (tool != null && Dm.eq(tool.tool_behaviour, TOOL_CROWBAR) && Dm.truthy(should_try_removing_electronics()) && !Dm.truthy(operating))
        {
            user.visible_message(span_notice(I18n.T("[] removes the electronics from the airlock assembly.", user)),
                span_notice(I18n.T("You start to remove electronics from the airlock assembly...")));

            if (Dm.truthy(await tool.use_tool(this, user as mob_living, 40, volume: 100)))
            {
                // tg passes the user as a second argument; /obj/proc/deconstruct takes only disassembled (obj_defense.dm:195)
                await deconstruct(TRUE);
                return;
            }
        }

        if (seal != null)
        {
            to_chat(user, span_warning(I18n.T("Remove the seal first!")));
            return;
        }
        if (Dm.truthy(locked))
        {
            to_chat(user, span_warning(I18n.T("The airlock's bolts prevent it from being forced!")));
            return;
        }
        if (Dm.truthy(welded))
        {
            to_chat(user, span_warning(I18n.T("It's welded, it won't budge!")));
            return;
        }

        if (!Dm.truthy(hasPower()))
        {
            if (Dm.truthy(operating))
                return;

            if (istype(tool, typeof(obj_item_fireaxe)) && !HAS_TRAIT(tool, TRAIT_WIELDED)) //being fireaxe'd
            {
                to_chat(user, span_warning(I18n.T("You need to be wielding [] to do that!", tool)));
                return;
            }

            // INVOKE_ASYNC(src, density ? PROC_REF(open) : PROC_REF(close), BYPASS_DOOR_CHECKS, user): close's second
            // argument is force_crush (airlock.dm:1410), so tg's user there is a truthy force_crush (a forced shut crushes).
            if (Dm.truthy(density))
                Waiting.Detach(open(BYPASS_DOOR_CHECKS, user as mob_living));
            else
                Waiting.Detach(close(BYPASS_DOOR_CHECKS, Dm.truthy(user) ? TRUE : FALSE));
            return;
        }

        if (!Dm.truthy(forced))
        {
            to_chat(user, span_warning(I18n.T("The airlock's motors resist your efforts to force it!")));
            return;
        }

        object check_electrified = isElectrified(); //setting this so we can check if the mob got shocked during the do_after below
        if (Dm.truthy(check_electrified) && Dm.truthy(await shock__proc(user as mob_living, 100)))
            return; //it's like sticking a fork in a power socket

        if (!Dm.truthy(density))//already open
            return;

        if (Dm.truthy(prying_so_hard))
            return;

        float time_to_open = 5 * SECONDS;
        playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE); //is it aliens or just the CE being a dick?
        prying_so_hard = TRUE;

        if (!Dm.truthy(await tool.use_tool(this, user as mob_living, time_to_open, volume: 100)))
        {
            prying_so_hard = FALSE;
            return;
        }

        if (tool != null)
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(tool._listen_lookup?[COMSIG_TOOL_FORCE_OPEN_AIRLOCK]) ? NONE : tool._SendSignal(COMSIG_TOOL_FORCE_OPEN_AIRLOCK, Dm.list(tool, user, this)), COMPONENT_TOOL_DO_NOT_ALLOW_FORCE_OPEN)))
            {
                prying_so_hard = FALSE;
                return;
            }
        }

        prying_so_hard = FALSE;

        if (Dm.truthy(check_electrified) && Dm.truthy(await shock__proc(user as mob_living, 100)))
            return;

        await open(BYPASS_DOOR_CHECKS, user as mob_living);
        await take_damage(AIRLOCK_PRY_DAMAGE, BRUTE, 0, 0); // Enough to sometimes spark
        if (Dm.truthy(density) && !Dm.truthy(await open(BYPASS_DOOR_CHECKS, user as mob_living)))
            to_chat(user, span_warning(I18n.T("Despite your attempts, [] refuses to open.", this)));
    }

    // tg: code/game/machinery/doors/airlock.dm:1327
    public override async DmTask<object> open(float? forced = null, mob_living opener = null)
    {
        forced ??= DEFAULT_DOOR_CHECKS;
        if (Dm.truthy(cycle_pump) && !Dm.truthy(operating) && !Dm.truthy(welded) && !Dm.truthy(seal) && Dm.truthy(locked) && Dm.truthy(density))
        {
            await cycle_pump.airlock_act(this);
            return FALSE; // The rest will be handled by the pump
        }

        if (Dm.truthy(operating) || Dm.truthy(welded) || Dm.truthy(locked) || Dm.truthy(seal))
            return FALSE;

        if (!Dm.truthy(density))
            return TRUE;

        // Since we aren't physically held shut, do extra checks to see if we should open.
        if (!Dm.truthy(try_to_force_door_open(forced)))
            return FALSE;

        if (Dm.truthy(autoclose))
            autoclose_in(Dm.truthy(normalspeed) ? 8 * SECONDS : 1.5f * SECONDS);

        if (closeOther != null && closeOther is obj_machinery_door_airlock)
            _addtimer(Dm.@new<datum_callback>(closeOther, nameof(close)), BYPASS_DOOR_CHECKS, file: "code/game/machinery/doors/airlock.dm", line: 1346);

        if (Dm.truthy(close_others))
        {
            foreach (obj_machinery_door_airlock otherlock in Dm.each_cast<obj_machinery_door_airlock>(close_others))
            {
                if (!Dm.truthy(shuttledocked) && !Dm.truthy(emergency) && !Dm.truthy(otherlock.shuttledocked) && !Dm.truthy(otherlock.emergency))
                {
                    if (Dm.truthy(otherlock.operating))
                        otherlock.delayed_close_requested = TRUE;
                    else
                        _addtimer(Dm.@new<datum_callback>(otherlock, nameof(close)), BYPASS_DOOR_CHECKS, file: "code/game/machinery/doors/airlock.dm", line: 1354);
                }
            }
        }

        if (Dm.truthy(cyclelinkedairlock))
        {
            if (!Dm.truthy(shuttledocked) && !Dm.truthy(emergency) && !Dm.truthy(cyclelinkedairlock.shuttledocked) && !Dm.truthy(cyclelinkedairlock.emergency))
            {
                if (Dm.truthy(cyclelinkedairlock.operating))
                    cyclelinkedairlock.delayed_close_requested = TRUE;
                else
                    _addtimer(Dm.@new<datum_callback>(cyclelinkedairlock, nameof(close)), BYPASS_DOOR_CHECKS, file: "code/game/machinery/doors/airlock.dm", line: 1361);
            }
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_AIRLOCK_OPEN]))
            this._SendSignal(COMSIG_AIRLOCK_OPEN, Dm.list(this, forced));
        if (Dm.truthy(opener))
        {
            if (Dm.truthy(opener._listen_lookup?[COMSIG_MOB_OPENED_AIRLOCK]))
                opener._SendSignal(COMSIG_MOB_OPENED_AIRLOCK, Dm.list(opener, forced));
        }
        await set_airlock_state(AIRLOCK_OPENING, animated: TRUE, force_type: forced);
        object transparent_delay = animation_segment_delay(AIRLOCK_OPENING_TRANSPARENT);
        await sleep(Dm.num(transparent_delay));
        await set_opacity(FALSE);
        if (Dm.truthy(multi_tile))
            await filler.set_opacity(FALSE);
        object passable_delay = Dm.sub(animation_segment_delay(AIRLOCK_OPENING_PASSABLE), transparent_delay);
        await sleep(Dm.num(passable_delay));
        set_density(FALSE);
        if (Dm.truthy(multi_tile))
            filler.set_density(FALSE);
        Dm.mask(ref flags_1, Dm.bitnot(PREVENT_CLICK_UNDER_1));
        air_update_turf(TRUE, FALSE);
        object open_delay = Dm.sub(Dm.sub(animation_segment_delay(AIRLOCK_OPENING_FINISHED), transparent_delay), passable_delay);
        await sleep(Dm.num(open_delay));
        layer = OPEN_DOOR_LAYER;
        await set_airlock_state(AIRLOCK_OPEN, animated: FALSE);
        if (Dm.truthy(delayed_close_requested))
        {
            delayed_close_requested = FALSE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(close)), FORCING_DOOR_CHECKS, file: "code/game/machinery/doors/airlock.dm", line: 1385);
        }
        return TRUE;
    }

    /// Additional checks depending on what we want to happen to door (should we try and open it normally, or do we want this open at all costs?)
    // tg: code/game/machinery/doors/airlock.dm:1389
    public override object try_to_force_door_open(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        float sw__1 = Dm.num(force_type);
        if (sw__1 == DEFAULT_DOOR_CHECKS) // Regular behavior.
        {
            if (!Dm.truthy(hasPower()) || Dm.truthy(wires.is_cut(WIRE_OPEN)) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == FORCING_DOOR_CHECKS) // Only one check.
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == BYPASS_DOOR_CHECKS) // No power usage, special sound, get it open.
            return TRUE;
        else
            stack_trace(Dm.text("Invalid forced argument '[]' passed to open() on this airlock.", force_type), "code/game/machinery/doors/airlock.dm", 1405);

        // If we got here, shit's fucked, hope parent can help us out here
        return base.try_to_force_door_open(force_type);
    }

    // tg: code/game/machinery/doors/airlock.dm:1410
    [DmParams("forced", "force_crush")]
    public override async DmTask<object> close(float? forced = null, params object[] __extra)
    {
        float? force_crush = Dm.arg<float?>(__extra, 0);
        forced ??= DEFAULT_DOOR_CHECKS;
        force_crush ??= FALSE;
        if (Dm.truthy(operating) || Dm.truthy(welded) || Dm.truthy(locked) || Dm.truthy(seal))
            return FALSE;
        if (Dm.truthy(density))
            return TRUE;
        if (forced == DEFAULT_DOOR_CHECKS) // Do this up here and outside of try_to_force_door_shut because if we don't have power, we shouldn't be doing any dangerous_close stuff.
        {
            if (!Dm.truthy(hasPower()) || Dm.truthy(wires.is_cut(WIRE_BOLTS)))
                return FALSE;
        }

        float dangerous_close = Dm.num(!Dm.truthy(safe) || Dm.truthy(force_crush));
        if (!Dm.truthy(dangerous_close))
        {
            foreach (turf checked_turf in Dm.each<turf>(locs))
            {
                foreach (atom_movable blocking in Dm.each<atom_movable>(checked_turf))
                {
                    object sigreturn = (!Dm.truthy(blocking._listen_lookup?[COMSIG_MOVABLE_BLOCKING_AIRLOCK]) ? NONE : blocking._SendSignal(COMSIG_MOVABLE_BLOCKING_AIRLOCK, Dm.list(blocking, this, forced, force_crush)));
                    if (Dm.truthy(Dm.bitand(sigreturn, AIRLOCK_BLOCK_FORCE_CRUSH)))
                    {
                        dangerous_close = TRUE;
                        continue;
                    }

                    if (Dm.truthy(blocking.density) && blocking != this)
                    {
                        autoclose_in(DOOR_CLOSE_WAIT);
                        return FALSE;
                    }
                }
            }
        }

        if (!Dm.truthy(try_to_force_door_shut(forced)))
            return FALSE;

        obj_structure_window killthis = Dm.locate<obj_structure_window>(get_turf(this));
        if (Dm.truthy(killthis))
            Dm.append(ref SSexplosions.med_mov_atom, killthis);
        if (Dm.truthy(this._listen_lookup?[COMSIG_AIRLOCK_CLOSE]))
            this._SendSignal(COMSIG_AIRLOCK_CLOSE, Dm.list(this, forced));
        await set_airlock_state(AIRLOCK_CLOSING, animated: TRUE, force_type: forced);
        layer = CLOSED_DOOR_LAYER;
        if (Dm.truthy(air_tight))
        {
            set_density(TRUE);
            if (Dm.truthy(multi_tile))
                filler.set_density(TRUE);
            Dm.combine(ref flags_1, PREVENT_CLICK_UNDER_1);
            air_update_turf(TRUE, TRUE);
        }
        object unpassable_delay = animation_segment_delay(AIRLOCK_CLOSING_UNPASSABLE);
        await sleep(Dm.num(unpassable_delay));
        if (!Dm.truthy(air_tight))
        {
            set_density(TRUE);
            if (Dm.truthy(multi_tile))
                filler.set_density(TRUE);
            Dm.combine(ref flags_1, PREVENT_CLICK_UNDER_1);
            air_update_turf(TRUE, TRUE);
        }
        object opaque_delay = Dm.sub(animation_segment_delay(AIRLOCK_CLOSING_OPAQUE), unpassable_delay);
        await sleep(Dm.num(opaque_delay));
        if (Dm.truthy(dangerous_close))
            await crush();
        if (Dm.truthy(visible) && !Dm.truthy(glass))
        {
            await set_opacity(TRUE);
            if (Dm.truthy(multi_tile))
                await filler.set_opacity(TRUE);
        }
        object close_delay = Dm.sub(Dm.sub(animation_segment_delay(AIRLOCK_CLOSING_FINISHED), unpassable_delay), opaque_delay);
        await sleep(Dm.num(close_delay));
        await set_airlock_state(AIRLOCK_CLOSED, animated: FALSE);
        delayed_close_requested = FALSE;
        if (!Dm.truthy(dangerous_close))
            await CheckForMobs();
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:1471
    public override object try_to_force_door_shut(float? force_type = null)
    {
        force_type ??= DEFAULT_DOOR_CHECKS;
        float sw__1 = Dm.num(force_type);
        if (sw__1 >= DEFAULT_DOOR_CHECKS && sw__1 <= FORCING_DOOR_CHECKS)
        {
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                return FALSE;
            return TRUE;
        }
        else if (sw__1 == BYPASS_DOOR_CHECKS)
            return TRUE;
        else
            stack_trace(Dm.text("Invalid forced argument '[]' passed to close() on this airlock.", force_type), "code/game/machinery/doors/airlock.dm", 1482);

        // shit's fucked, let's hope parent has something to handle it.
        return base.try_to_force_door_shut(force_type);
    }

    // tg: code/game/machinery/doors/airlock.dm:1487
    public virtual async DmTask prison_open()
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return;
        if (Dm.truthy(locked))
            unbolt();
        await open();
        bolt();
        return;
    }

    // gets called when a player uses an airlock painter on this airlock
    // tg: code/game/machinery/doors/airlock.dm:1497
    public virtual async DmTask change_paintjob(obj_item_airlock_painter painter = null, mob user = null)
    {
        // engineering E14 (the airlock painter)
        if (!in_range(this, user) || !Dm.truthy(painter.can_use(user))) // user should be adjacent to the airlock, and the painter should have a toner cartridge that isn't empty
            return;

        // reads from the airlock painter's `available paintjob` list. lets the player choose a paint option, or cancel painting
        object current_paintjob = await tgui_input_list(user, I18n.T("Paintjob for this airlock"), I18n.T("Customize"), sort_list(painter.available_paint_jobs) as DmList);
        if (current_paintjob == null || !in_range(this, user) || !Dm.truthy(painter.can_use(user))) // if the user clicked cancel on the popup, or moved away, or ran out of ink, return
            return;

        Type airlock = painter.available_paint_jobs[Dm.text("[]", current_paintjob)] as Type; // get the airlock type path associated with the airlock name the user just chose
        Type assembly = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.assemblytype); // we need to create a new instance of the airlock and assembly to read vars from them

        if (Dm.eq(airlock_material, "glass") && Dm.truthy(Dm.initial(assembly, (obj_structure_door_assembly p) => p.noglass))) // prevents painting glass airlocks with a paint job that doesn't have a glass version, such as the freezer
        {
            to_chat(user, span_warning(I18n.T("This paint job can only be applied to non-glass airlocks.")));
            return;
        }

        // applies the user-chosen airlock's icon, overlays and assemblytype to the src airlock
        painter.use_paint(user);
        if (Dm.truthy(Dm.initial(airlock, (obj_machinery_door_airlock p) => p.greyscale_config)))
        {
            greyscale_config = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.greyscale_config);
            greyscale_colors = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.greyscale_colors);
            await update_greyscale();
        }
        else
            icon = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.icon);
        overlays_file = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.overlays_file);
        assemblytype = Dm.initial(airlock, (obj_machinery_door_airlock p) => p.assemblytype);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:1530
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(operating) && Dm.truthy(density) && Dm.truthy(hasPower()) && !Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            if (emag_card is obj_item_card_emag_doorjack doorjack_card)
                doorjack_card.use_charge(user);
            await set_machine_stat(Dm.bitor(machine_stat, MAINT)); // flash the airlock lights and display some sparks
            await set_airlock_state(AIRLOCK_CLOSED);
            operating = TRUE;
            _addtimer(Dm.@new<datum_callback>(this, nameof(finish_emag_act)), 0.6f * SECONDS, file: "code/game/machinery/doors/airlock.dm", line: 1539);
            return TRUE;
        }
        return FALSE;
    }

    /// Timer proc, called ~0.6 seconds after [emag_act]. Finishes the emag sequence by breaking the airlock, permanently locking it, and disabling power.
    // tg: code/game/machinery/doors/airlock.dm:1543
    public virtual async DmTask<object> finish_emag_act()
    {
        if (QDELETED(this))
            return FALSE;
        operating = FALSE;
        if (!Dm.truthy(await open()))
            await set_airlock_state(AIRLOCK_CLOSED);
        await set_machine_stat(Dm.bitand(machine_stat, ~MAINT));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        feedback = FALSE;
        locked = TRUE;
        loseMainPower();
        loseBackupPower();
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:1608
    [SignalHandler]
    public virtual void on_break()
    {
        set_panel_open(TRUE);
        Waiting.NoSleep(wires.cut_all());
    }

    // tg: code/game/machinery/doors/airlock.dm:1614
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (prob(Dm.num(severity) * 10 - 20) && (secondsElectrified < 30) && (secondsElectrified != MACHINE_ELECTRIFIED_PERMANENT))
        {
            await set_electrified(30);
            // GAP(types): shockedby is generated untyped (object); LAZYADD takes the list by reference
            DmList shockedby_list = shockedby as DmList;
            LAZYADD(ref shockedby_list, Dm.text("\\[[]\\]EM Pulse", server_timestamp()));
            shockedby = shockedby_list;
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:1622
    public virtual async DmTask set_electrified(object seconds = null, mob user = null)
    {
        secondsElectrified = Dm.num(seconds);
        diag_hud_set_electrified();
        if (secondsElectrified > MACHINE_NOT_ELECTRIFIED)
            Waiting.Detach(electrified_loop());

        if (user != null)
        {
            string message;
            if (secondsElectrified == MACHINE_ELECTRIFIED_PERMANENT)
                message = "permanently shocked";
            else if (secondsElectrified == MACHINE_NOT_ELECTRIFIED)
                message = "unshocked";
            else
                message = Dm.text("temp shocked for [] seconds", secondsElectrified);
            // GAP(types): shockedby is generated untyped (object); LAZYADD takes the list by reference
            DmList shockedby_list = shockedby as DmList;
            LAZYADD(ref shockedby_list, Dm.text("\\[[]\\] [] - ([])", server_timestamp(), key_name(user), uppertext(message)));
            shockedby = shockedby_list;
            log_combat(user, this, message);
            // tg: code/game/machinery/doors/airlock.dm:1638
            add_hiddenprint(user);
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:1641
    public override async DmTask<object> take_damage(object damage_amount = null, string damage_type = null, object damage_flag = null, float? sound_effect = null, object attack_dir = null, float? armour_penetration = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        sound_effect ??= 1;
        if ((Dm.num(damage_amount) >= Dm.num(atom_integrity)) && Dm.eq(damage_flag, BOMB))
            obj_flags = Dm.bitor(obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION);  //If an explosive took us out, don't drop the assembly
        object dot = await base.take_damage(damage_amount, damage_type, damage_flag, sound_effect, attack_dir, armour_penetration);
        if (Dm.num(atom_integrity) < (0.75f * max_integrity))
            update_appearance();
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:1648
    public virtual async DmTask prepare_deconstruction_assembly(obj_structure_door_assembly assembly = null)
    {
        assembly.heat_proof_finished = heat_proof; //tracks whether there's rglass in
        await assembly.set_anchored(TRUE);
        assembly.glass = glass;
        assembly.state = AIRLOCK_ASSEMBLY_NEEDS_ELECTRONICS;
        assembly.created_name = name;
        assembly.previous_assembly = previous_airlock;
        assembly.update_name();
        assembly.update_appearance();
        assembly.dir = dir;
    }

    // tg: code/game/machinery/doors/airlock.dm:1659
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        obj_structure_door_assembly A;
        if (assemblytype != null)
            A = (obj_structure_door_assembly)Dm.@new(assemblytype, loc);
        else
        {
            A = Dm.@new<obj_structure_door_assembly>(loc);
            //If you come across a null assemblytype, it will produce the default assembly instead of disintegrating.
        }
        await prepare_deconstruction_assembly(A);

        if (!Dm.truthy(disassembled))
            A?.update_integrity(A.max_integrity * 0.5f);

        else if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            obj_item_electronics_airlock ae;
            if (electronics == null)
            {
                ae = Dm.@new<obj_item_electronics_airlock>(loc);
                if (Dm.truthy(closeOtherId))
                    ae.passed_cycle_id = closeOtherId;
                if (Dm.truthy(length(req_one_access)))
                {
                    ae.one_access = 1;
                    ae.accesses = req_one_access as DmList;
                }
                else if (Dm.truthy(length(req_access)))
                    ae.accesses = req_access as DmList;
            }
            else
            {
                ae = electronics;
                electronics = null;
                await ae.forceMove(drop_location() as atom);
            }
        }
        return null;
    }

    /**
     * Returns a string representing the type of note pinned to this airlock
     * Arguments:
     * * frame_state - The AIRLOCK_FRAME_ value, as used in update_overlays()
     **/
    // tg: code/game/machinery/doors/airlock.dm:1711
    public virtual object get_note_state(object frame_state = null)
    {
        if (!Dm.truthy(note))
            return null;
        else if (note is obj_item_paper pinned_paper) // gaps S3
        {
            if (Dm.truthy(pinned_paper.get_total_length()) && Dm.truthy(pinned_paper.show_written_words))
                return Dm.text("note_words_[]", frame_state);
            else
                return Dm.text("note_[]", frame_state);
        }
        else if (note is obj_item_photo)
            return Dm.text("photo_[]", frame_state);
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:1587
    public override async DmTask hostile_lockdown(mob origin = null)
    {
        // Must be powered and have working AI wire.
        if (Dm.truthy(canAIControl(origin)) && !Dm.truthy(machine_stat))
        {
            locked = FALSE; //For airlocks that were bolted open.
            safe = FALSE; //DOOR CRUSH
            await close();
            bolt(); //Bolt it!
            await set_electrified(MACHINE_ELECTRIFIED_PERMANENT); //Shock it!
            if (origin != null)
            {
                // GAP(types): shockedby is generated untyped (object); LAZYADD takes the list by reference
                DmList shockedby_list = shockedby as DmList;
                LAZYADD(ref shockedby_list, Dm.text("\\[[]\\] []", server_timestamp(), key_name(origin)));
                shockedby = shockedby_list;
            }
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:1599
    public override async DmTask disable_lockdown()
    {
        // Must be powered and have working AI wire.
        if (Dm.truthy(canAIControl()) && !Dm.truthy(machine_stat))
        {
            unbolt();
            await set_electrified(MACHINE_NOT_ELECTRIFIED);
            await open();
            safe = TRUE;
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:1727
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AiAirlock", name);
            await ui.open();
        }
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:1734
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList power = Dm.list();
        power["main"] = Dm.truthy(remaining_main_outage()) ? 0 : 2; // boolean
        power["main_timeleft"] = round(Dm.num(remaining_main_outage()) / 10);
        power["backup"] = Dm.truthy(remaining_backup_outage()) ? 0 : 2; // boolean
        power["backup_timeleft"] = round(Dm.num(remaining_backup_outage()) / 10);
        data["power"] = power;

        data["shock"] = secondsElectrified == MACHINE_NOT_ELECTRIFIED ? 2 : 0;
        data["shock_timeleft"] = secondsElectrified;
        data["id_scanner"] = Dm.not(aiDisabledIdScanner);
        data["emergency"] = emergency; // access
        data["locked"] = locked; // bolted
        data["feedback"] = feedback; // lights and sounds
        data["safe"] = safe; // safeties
        data["speed"] = normalspeed; // safe speed
        data["welded"] = welded; // welded
        data["opened"] = Dm.not(density); // opened

        DmList wire = Dm.list();
        wire["main_1"] = Dm.not(wires.is_cut(WIRE_POWER1));
        wire["main_2"] = Dm.not(wires.is_cut(WIRE_POWER2));
        wire["backup_1"] = Dm.not(wires.is_cut(WIRE_BACKUP1));
        wire["backup_2"] = Dm.not(wires.is_cut(WIRE_BACKUP2));
        wire["shock"] = Dm.not(wires.is_cut(WIRE_SHOCK));
        wire["id_scanner"] = Dm.not(wires.is_cut(WIRE_IDSCAN));
        wire["bolts"] = Dm.not(wires.is_cut(WIRE_BOLTS));
        wire["feedback"] = Dm.not(wires.is_cut(WIRE_FEEDBACK));
        wire["safe"] = Dm.not(wires.is_cut(WIRE_SAFETY));
        wire["timing"] = Dm.not(wires.is_cut(WIRE_TIMING));

        data["wires"] = wire;
        return data;
    }

    // tg: code/game/machinery/doors/airlock.dm:1770
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(user_allowed(usr)))
            return dot;
        switch (action as string)
        {
            case "disrupt-main":
                if (!Dm.truthy(main_power_timer))
                {
                    loseMainPower();
                    update_appearance();
                }
                else
                    to_chat(usr, span_warning(I18n.T("Main power is already offline.")));
                dot = TRUE;
                break;
            case "disrupt-backup":
                if (!Dm.truthy(backup_power_timer))
                {
                    loseBackupPower();
                    update_appearance();
                }
                else
                    to_chat(usr, span_warning(I18n.T("Backup power is already offline.")));
                dot = TRUE;
                break;
            case "shock-restore":
                await shock_restore(usr);
                dot = TRUE;
                break;
            case "shock-temp":
                await shock_temp(usr);
                dot = TRUE;
                break;
            case "shock-perm":
                await shock_perm(usr);
                dot = TRUE;
                break;
            case "idscan-toggle":
                aiDisabledIdScanner = Dm.not(aiDisabledIdScanner);
                dot = TRUE;
                break;
            case "emergency-toggle":
                toggle_emergency(usr);
                dot = TRUE;
                break;
            case "bolt-toggle":
                toggle_bolt(usr);
                dot = TRUE;
                break;
            case "light-toggle":
                feedback = Dm.not(feedback);
                update_appearance();
                dot = TRUE;
                break;
            case "safe-toggle":
                safe = Dm.not(safe);
                dot = TRUE;
                break;
            case "speed-toggle":
                normalspeed = Dm.not(normalspeed);
                dot = TRUE;
                break;
            case "open-close":
                await user_toggle_open(usr);
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:1724
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return (base.IsContainedAtomAccessible(contained, user) is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.num(contained == note));
    }

    // tg: code/game/machinery/doors/airlock.dm:1824
    public virtual object user_allowed(mob user = null)
    {
        // GAP(lead): isAdminGhostAI (modules/mob/mob_helpers.dm:357) is not declared: no admin ghosts in M3.
        bool isAdminGhostAI__gap = false;
        // HAS_SILICON_ACCESS(user) (__DEFINES/is_helpers.dm:178), with isAdminGhostAI as above
        return Dm.num(((HAS_TRAIT(user, TRAIT_SILICON_ACCESS) || isAdminGhostAI__gap) && Dm.truthy(canAIControl(user))) || isAdminGhostAI__gap);
    }

    // tg: code/game/machinery/doors/airlock.dm:1827
    public virtual async DmTask shock_restore(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        if (Dm.truthy(wires.is_cut(WIRE_SHOCK)))
            to_chat(user, span_warning(I18n.T("Can't un-electrify the airlock - The electrification wire is cut.")));
        else if (Dm.truthy(isElectrified()))
            await set_electrified(MACHINE_NOT_ELECTRIFIED, user);
    }

    // tg: code/game/machinery/doors/airlock.dm:1835
    public virtual async DmTask shock_temp(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        if (Dm.truthy(wires.is_cut(WIRE_SHOCK)))
            to_chat(user, span_warning(I18n.T("The electrification wire has been cut.")));
        else
            await set_electrified(MACHINE_DEFAULT_ELECTRIFY_TIME, user);
    }

    // tg: code/game/machinery/doors/airlock.dm:1843
    public virtual async DmTask shock_perm(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        if (Dm.truthy(wires.is_cut(WIRE_SHOCK)))
            to_chat(user, span_warning(I18n.T("The electrification wire has been cut.")));
        else
            await set_electrified(MACHINE_ELECTRIFIED_PERMANENT, user);
    }

    // tg: code/game/machinery/doors/airlock.dm:1851
    public virtual void toggle_bolt(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        if (Dm.truthy(wires.is_cut(WIRE_BOLTS)))
        {
            to_chat(user, span_warning(I18n.T("The door bolt drop wire is cut - you can't toggle the door bolts.")));
            return;
        }
        if (Dm.truthy(locked))
        {
            if (!Dm.truthy(hasPower()))
                to_chat(user, span_warning(I18n.T("The door has no power - you can't raise the door bolts.")));
            else
            {
                unbolt();
                log_combat(user, this, "unbolted");
            }
        }
        else
        {
            bolt();
            log_combat(user, this, "bolted");
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:1867
    public virtual void toggle_emergency(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        emergency = Dm.not(emergency);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:1873
    public virtual async DmTask user_toggle_open(mob user = null)
    {
        if (!Dm.truthy(user_allowed(user)))
            return;
        if (Dm.truthy(welded))
            to_chat(user, span_warning(I18n.T("The airlock has been welded shut!")));
        else if (Dm.truthy(locked))
            to_chat(user, span_warning(I18n.T("The door bolts are down!")));
        else if (!Dm.truthy(density))
            await close();
        else
            await open(opener: user as mob_living);
    }

    /**
     * Generates the airlock's wire layout based on the current area the airlock resides in.
     *
     * Returns a new /datum/wires/ with the appropriate wire layout based on the airlock_wires
     * of the area the airlock is in.
     */
    // tg: code/game/machinery/doors/airlock.dm:1891
    public virtual async DmTask<object> get_wires()
    {
        area source_area = get_area(this) as area;
        return (Dm.truthy(source_area?.airlock_wires) ? await Dm.new_async(source_area.airlock_wires, this) : await Dm.new_async<datum_wires_airlock>(this));
    }
}

// tg: code/game/machinery/doors/airlock.dm:1895
public partial class obj_structure_fluff_airlock_filler : obj_structure_fluff
{
    // tg: code/game/machinery/doors/airlock.dm:1895
    public override object Destroy(float? force = null)
    {
        filled_airlock = null;
        return base.Destroy(force);
    }

    /**
     * Create a ref to our parent airlock and listen for a QDEL, which we will al
     */
    // tg: code/game/machinery/doors/airlock.dm:1902
    public virtual void pair_airlock(obj_machinery_door parent_airlock = null)
    {
        if (parent_airlock == null)
        {
            stack_trace("Attempted to pair an airlock filler with no parent airlock specified!", "code/game/machinery/doors/airlock.dm", 1904);
            return;
        }

        filled_airlock = parent_airlock;
        RegisterSignal(filled_airlock, COMSIG_QDELETING, nameof(no_airlock));
    }

    /**
     * Multi-tile airlocks pair with a filler panel, if one goes so does the other.
     */
    // tg: code/game/machinery/doors/airlock.dm:1913
    [SignalHandler]
    public virtual void no_airlock()
    {
        qdel(this);
    }

    /**
     * Multi-tile airlocks (using a filler panel) have special handling for movables with PASSGLASS
     */
    // tg: code/game/machinery/doors/airlock.dm:1921
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;

        if (mover is atom_movable && Dm.truthy(Dm.bitand(mover.pass_flags, PASSGLASS)))
            return Dm.not(opacity);
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:1929
    [DmParams("user", "force")]
    public override object can_be_pulled(object user = null, object force = null)
    {
        return FALSE;
    }

    // tg: code/game/machinery/doors/airlock.dm:1932 (the M5 leftovers: the filler of a multi-tile airlock is never eaten)
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:1935
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}

// tg: code/game/machinery/doors/airlock.dm:2582
public partial class obj_machinery_door_airlock_multi_tile : obj_machinery_door_airlock
{
    // tg: code/game/machinery/doors/airlock.dm:2590
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        set_bounds();
    }

    // tg: code/game/machinery/doors/airlock.dm:2609
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        return null;
    }
}

// tg: code/game/machinery/doors/airlock.dm:1938
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/doors/airlock.dm:1938
    public virtual void set_cycle_pump(obj_machinery_atmospherics_components_unary_airlock_pump pump = null)
    {
        RegisterSignal(pump, COMSIG_QDELETING, nameof(unset_cycle_pump));
        cycle_pump = pump;
    }

    // tg: code/game/machinery/doors/airlock.dm:1942
    [SignalHandler]
    public virtual void unset_cycle_pump()
    {
        if (Dm.truthy(locked))
        {
            unbolt();
            Waiting.NoSleep(say(I18n.T("Link broken, unbolting.")));
        }
        cycle_pump = null;
    }
}

// tg: code/game/machinery/doors/airlock.dm:2169
public partial class obj_machinery_door_airlock_uranium : obj_machinery_door_airlock
{
    // tg: code/game/machinery/doors/airlock.dm:2176
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(actually_radioactive) && world.time > last_event + 20)
        {
            if (prob(50))
                radiate();
            last_event = world.time;
        }
        return null;
    }

    // tg: code/game/machinery/doors/airlock.dm:2182
    public virtual void radiate()
    {
        radiation_pulse(
            this,
            max_range: 2,
            threshold: RAD_LIGHT_INSULATION,
            chance: URANIUM_IRRADIATION_CHANCE,
            minimum_exposure_time: URANIUM_RADIATION_MINIMUM_EXPOSURE_TIME
        );
    }
}

// tg: code/game/machinery/doors/airlock.dm:2301
public partial class obj_machinery_door_airlock_external : obj_machinery_door_airlock
{
    // tg: code/game/machinery/doors/airlock.dm:2310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // default setting is for mapping only, let overrides work
        if (!Dm.truthy(mapload))
            req_access = null;

        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/machinery/doors/airlock.dm:2317
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        if (Dm.truthy(space_dir))
            Dm.combine(ref unres_sides, space_dir);
    }

    // tg: code/game/machinery/doors/airlock.dm:2322
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(space_dir))
            Dm.append(ref dot, span_notice(I18n.T("It has labels indicating that it has an emergency mechanism to open from the [] side with <b>just your hands</b> even if there's no power.", dir2text(space_dir))));
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:2327
    public override void cyclelinkairlock()
    {
        base.cyclelinkairlock();
        obj_machinery_door_airlock_external cycle_linked_external_airlock = cyclelinkedairlock as obj_machinery_door_airlock_external;
        if (cycle_linked_external_airlock is obj_machinery_door_airlock_external)
        {
            Dm.combine(ref cycle_linked_external_airlock.space_dir, space_dir);
            Dm.combine(ref space_dir, cycle_linked_external_airlock.space_dir);
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:2334
    public override async DmTask<object> try_safety_unlock(mob user = null)
    {
        if (Dm.truthy(space_dir) && Dm.truthy(density))
        {
            if (!Dm.truthy(hasPower()))
            {
                to_chat(user, span_notice(I18n.T("You begin unlocking the airlock safety mechanism...")));
                if (Dm.truthy(await do_after(user, 15 * SECONDS, target: this)))
                {
                    await try_to_crowbar(null, user, TRUE);
                    return TRUE;
                }
            }
            else
            {
                // always open from the space side
                // get_dir(src, user) & space_dir, checked in unresricted_sides
                bool should_safety_open = Dm.truthy(shuttledocked) || Dm.truthy(cyclelinkedairlock?.shuttledocked) || Dm.truthy(is_safe_turf(get_step(this, Dm.num(space_dir)), TRUE, FALSE));
                return await try_to_activate_door(user, should_safety_open ? TRUE : FALSE);
            }
        }

        return await base.try_safety_unlock(user);
    }
}

// Cult Airlocks
// tg: code/game/machinery/doors/airlock.dm:2455
public partial class obj_machinery_door_airlock_cult : obj_machinery_door_airlock
{
    // tg: code/game/machinery/doors/airlock.dm:2468
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.@new(openingoverlaytype, loc);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_ALL));
        return dot;
    }

    // tg: code/game/machinery/doors/airlock.dm:2473
    public override object canAIControl(mob user = null)
    {
        return Dm.num(Dm.truthy(IS_CULTIST(user)) && !Dm.truthy(isAllPowerCut()));
    }

    // tg: code/game/machinery/doors/airlock.dm:2476
    [SignalHandler]
    public override void on_break()
    {
        set_panel_open(TRUE);
    }

    // tg: code/game/machinery/doors/airlock.dm:2479
    public override object isElectrified()
    {
        return FALSE;
    }

    // tg: code/game/machinery/doors/airlock.dm:2482
    public override object hasPower()
    {
        return TRUE;
    }

    // tg: code/game/machinery/doors/airlock.dm:2485
    [DmParams("L")]
    public override async DmTask<object> allowed__proc(mob accessor = null)
    {
        await DmTask.CompletedTask;
        var L = accessor as mob_living;
        if (!Dm.truthy(density))
            return TRUE;
        if (Dm.truthy(friendly) || Dm.truthy(IS_CULTIST(L)) || L is mob_living_basic_shade || isconstruct(L))
        {
            if (!Dm.truthy(stealthy))
                Dm.@new(openingoverlaytype, loc);
            return TRUE;
        }
        else
        {
            if (!Dm.truthy(stealthy))
            {
                Dm.@new<obj_effect_temp_visual_cult_sac>(loc);
                atom throwtarget;
                throwtarget = get_edge_target_turf(this, get_dir(this, get_step_away(L, this))) as atom;
                Dm.output_to(L, sound(SFX_HALLUCINATION_TURN_AROUND, 0, 1, 50)); // SEND_SOUND
                flash_color(L, flash_color: COLOR_CULT_RED, flash_time: 20);
                await L.Paralyze(40);
                await L.throw_at(throwtarget, 5, 1);
            }
            return FALSE;
        }
    }

    // tg: code/game/machinery/doors/airlock.dm:2503
    public virtual void conceal()
    {
        icon = Dm.resource("icons/obj/doors/airlocks/station/maintenance.dmi");
        overlays_file = Dm.resource("icons/obj/doors/airlocks/station/overlays.dmi");
        name = "Airlock";
        desc = "It opens and closes.";
        stealthy = TRUE;
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:2511
    public virtual void reveal()
    {
        icon = Dm.initial(type, (obj_machinery_door_airlock_cult a) => a.icon);
        overlays_file = Dm.initial(type, (obj_machinery_door_airlock_cult a) => a.overlays_file);
        name = Dm.initial(type, (obj_machinery_door_airlock_cult a) => a.name);
        desc = Dm.initial(type, (obj_machinery_door_airlock_cult a) => a.desc);
        stealthy = Dm.initial(type, (obj_machinery_door_airlock_cult a) => a.stealthy);
        update_appearance();
    }

    // tg: code/game/machinery/doors/airlock.dm:2519
    public override async DmTask<object> narsie_act(params object[] __extra)
    {
        await DmTask.CompletedTask;
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/doors/airlock.dm:26  /proc/get_airlock_overlay: var/static/list/airlock_overlays = list()
    internal DmList GlobalProcs__get_airlock_overlay__airlock_overlays = Dm.list();
}

public partial class obj_machinery_door_airlock
{
    public override async DmTask<object> attempt_wire_interaction(mob user = null)
    {
        if (Dm.truthy(security_level))
        {
            to_chat(user, span_warning("Wires are protected!"));
            return WIRE_INTERACTION_FAIL;
        }
        return await base.attempt_wire_interaction(user);
    }
}

// tg: code/game/machinery/doors/airlock.dm:1687 (E13: the RCD)
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/doors/airlock.dm:1687
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        object sw__1 = the_rcd.mode;
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            if (Dm.truthy(seal))
            {
                to_chat(user, span_notice(I18n.T("[]'s seal needs to be removed first.", this)));
                return FALSE;
            }
            if (!Dm.eq(security_level, AIRLOCK_SECURITY_NONE))
            {
                to_chat(user, span_notice(I18n.T("[]'s reinforcement needs to be removed first.", this)));
                return FALSE;
            }
            return Dm.list(Dm.kv("delay", 5 * SECONDS), Dm.kv("cost", 32));
        }
        return FALSE;
    }

    // tg: code/game/machinery/doors/airlock.dm:1699
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        object sw__1 = rcd_data[RCD_DESIGN_MODE];
        if (Dm.eq(sw__1, RCD_DECONSTRUCT))
        {
            qdel(this);
            return TRUE;
        }
        return FALSE;
    }
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/doors/airlock.dm:1526
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        //Airlock is passable if it is open (!density), bot has access, and is not bolted shut or powered off)
        return Dm.num(!Dm.truthy(density) || (Dm.truthy(check_access_list(pass_info.access)) && !Dm.truthy(locked) && Dm.truthy(hasPower()) && !Dm.truthy(pass_info.no_id)));
    }
}

// tg: code/game/machinery/doors/airlock.dm:1556  (interleaved types, rules 1.9; gaps X5: an alien prying an airlock open)
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/game/machinery/doors/airlock.dm:1556
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        if (Dm.truthy(isElectrified()) && Dm.truthy(await shock__proc(user, 100))) //Mmm, fried xeno!
        {
            add_fingerprint(user);
            return null;
        }
        if (!Dm.truthy(density)) //Already open
            return await base.attack_alien(user, modifiers);
        if (Dm.truthy(user.combat_mode))
            return await base.attack_alien(user, modifiers);
        if (Dm.truthy(locked) || Dm.truthy(welded) || Dm.truthy(seal)) //Extremely generic, as aliens only understand the basics of how airlocks work.
        {
            to_chat(user, span_warning(I18n.T("[] refuses to budge!", this)));
            user.log_message(Dm.text("Tried to pry open [], located at [], but failed due to the airlock being sealed.", this, loc_name(this)), LOG_GAME);
            return null;
        }
        add_fingerprint(user);
        user.visible_message(span_warning(I18n.T("[] begins prying open [].", user, this)),
                            span_noticealien(I18n.T("You begin digging your claws into [] with all your might!", this)),
                            span_warning(I18n.T("You hear groaning metal...")));
        user.log_message(Dm.text("Started prying open [], located at [].", this, loc_name(this)), LOG_GAME);
        float time_to_open = 5; //half a second
        if (Dm.truthy(hasPower()))
        {
            time_to_open = 5 * SECONDS; //Powered airlocks take longer to open, and are loud.
            playsound(this, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), 100, TRUE);
        }

        if (Dm.truthy(await do_after(user, time_to_open, this)))
        {
            if (Dm.truthy(density) && !Dm.truthy(await open(BYPASS_DOOR_CHECKS))) //The airlock is still closed, but something prevented it opening. (Another player noticed and bolted/welded the airlock in time!)
            {
                to_chat(user, span_warning(I18n.T("Despite your efforts, [] managed to resist your attempts to open it!", this)));
                user.log_message(Dm.text("Tried and failed to pry open [], located at [], due to the airlock getting sealed during the do_after.", this, loc_name(this)), LOG_GAME);
                return null;
            }
            user.log_message(Dm.text("Successfully pried open [], located at [].", this, loc_name(this)), LOG_GAME);
            return null;
        }
        user.log_message(Dm.text("Tried and failed to pry open [], located at [], due to getting interrupted.", this, loc_name(this)), LOG_GAME);
        return null;
    }
}
