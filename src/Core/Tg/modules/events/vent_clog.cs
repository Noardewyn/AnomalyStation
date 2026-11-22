// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/vent_clog.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the vent clogs (minor, major, critical and strange): a station vent spews filth and creatures, one
// at a time while fewer than its cap live, until the event ends or a plunger sends them back; a destroyed vent moves the clog to
// another. The var blocks are generated (D111).

// tg: code/modules/events/vent_clog.dm:3
public partial class datum_round_event_control_vent_clog : datum_round_event_control
{
    // tg: code/modules/events/vent_clog.dm:11
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(
            SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_vent_pump))))
        {
            var vent_turf = get_turf(vent) as turf;
            var vent_area = get_area(vent) as area;
            if (vent_turf != null && Dm.truthy(is_station_level(vent_turf.z)) && !Dm.truthy(vent.welded) && vent_area is area_station)
                return TRUE; //make sure we have a valid vent to spawn from.
        }
        return FALSE;
    }
}

// tg: code/modules/events/vent_clog.dm:22
public partial class datum_round_event_vent_clog : datum_round_event
{
    // tg: code/modules/events/vent_clog.dm:1
    protected const int MOB_SPAWN_MINIMUM = 3;

    // tg: code/modules/events/vent_clog.dm:77  var/static/list/mob_list (a proc-level static, rules 10.6)
    private static DmList get_mob__mob_list => TgState.Current.datum_round_event_vent_clog__get_mob__mob_list ??= Dm.list(
        typeof(mob_living_basic_butterfly),
        typeof(mob_living_basic_cockroach),
        typeof(mob_living_basic_cockroach_bloodroach),
        typeof(mob_living_basic_spider_maintenance),
        typeof(mob_living_basic_mouse),
        typeof(mob_living_basic_snail));

    // tg: code/modules/events/vent_clog.dm:40
    public override async DmTask announce__proc(object fake = null)
    {
        object event_area = Dm.truthy(fake) ? pick(GLOB.teleportlocs) : get_area_name(vent);
        priority_announce(Dm.text("Minor biological obstruction detected in the ventilation network. Blockage is believed to be in the [].", event_area), "Custodial Notification");
    }

    // tg: code/modules/events/vent_clog.dm:44
    public override async DmTask<object> setup()
    {
        vent = get_vent();
        spawned_mob = get_mob();
        end_when = rand(300, 600);
        maximum_spawns = rand(MOB_SPAWN_MINIMUM, 10);
        spawn_delay = rand(10, 15);
        filth_spawn_types = Dm.list(
            typeof(obj_effect_decal_cleanable_vomit),
            typeof(obj_effect_decal_cleanable_insectguts),
            typeof(obj_effect_decal_cleanable_blood_oil));
        return null;
    }

    // tg: code/modules/events/vent_clog.dm:56
    public override async DmTask<object> start()
    {
        clog_vent();
        announce_to_ghosts(vent);
        return null;
    }

    // tg: code/modules/events/vent_clog.dm:60
    public override async DmTask tick() //Checks if spawn_interval is met, then sends signal to vent to produce a mob.
    {
        if (activeFor % spawn_delay == 0)
        {
            life_check();
            if (living_mobs.len < maximum_spawns)
                produce_mob();
        }
    }

    // tg: code/modules/events/vent_clog.dm:66
    public override async DmTask end() //No end announcement. If you want to take the easy way out and just leave the vent welded, you must open it at your own peril.
    {
        vent = null;
        living_mobs.Cut();
    }

    /**
     * Selects which mob will be spawned for a given vent clog event.
     *
     * Creates a static list of mobs, which is different based on the severity of the event being run, and returns a pick() of it.
     */
    // tg: code/modules/events/vent_clog.dm:76
    public virtual Type get_mob()
    {
        return (Type)pick(get_mob__mob_list);
    }

    /**
     * Finds a valid vent to spawn mobs from.
     *
     * Randomly selects a vent that is on-station, unwelded, and hosted by a passable turf. If no vents are found, the event
     * is immediately killed.
     */
    // tg: code/modules/events/vent_clog.dm:94
    public virtual obj_machinery_atmospherics_components_unary_vent_pump get_vent()
    {
        DmList vent_list = Dm.list();
        foreach (obj_machinery_atmospherics_components_unary_vent_pump vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(
            SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_vent_pump))))
        {
            var vent_turf = get_turf(vent) as turf;
            var vent_area = get_area(vent) as area;
            if (vent_turf != null && Dm.truthy(is_station_level(vent_turf.z)) && !Dm.truthy(vent.welded) && vent_area is area_station && !Dm.truthy(vent_turf.is_blocked_turf_ignore_climbable()))
                Dm.append(ref vent_list, vent);
        }

        if (!Dm.truthy(length(vent_list)))
        {
            kill();
            throw Dm.CRASH("Unable to find suitable vent.", "code/modules/events/vent_clog.dm", 104);
        }

        return pick(vent_list) as obj_machinery_atmospherics_components_unary_vent_pump;
    }

    /**
     * Checks which mobs in the mob spawn list are alive.
     *
     * Checks each mob in the living_mobs list, to see if they're dead or not. If dead, they're removed from the list.
     * This is used to keep new mobs spawning as the old ones die.
     */
    // tg: code/modules/events/vent_clog.dm:115
    public virtual void life_check()
    {
        foreach (datum_weakref mob_ref in Dm.each_cast<datum_weakref>(living_mobs.Copy()))
        {
            var real_mob = mob_ref.resolve() as mob_living;
            if (QDELETED(real_mob) || real_mob.stat == DEAD)
                Dm.remove(ref living_mobs, mob_ref);
        }
    }

    /**
     * Finds a new vent for the event if the original is destroyed.
     *
     * This is used when the vent for the event is destroyed. It picks a new vent and announces that the event has moved elsewhere.
     * Handles the vent ref if there are no valid vents to replace it with.
     */
    // tg: code/modules/events/vent_clog.dm:128
    [SignalHandler]
    public virtual void vent_move(datum source = null)
    {
        vent = null; //If by some great calamity, the last valid vent is destroyed, the ref is cleared.
        vent = get_vent();

        clog_vent();

        announce_to_ghosts(vent);
        priority_announce(Dm.text("Lifesign readings have moved to a new location in the ventilation network. New Location: []", prob(50) ? "Unknown." : Dm.text("[].", get_area_name(vent))), "Lifesign Notification");
    }

    /**
     * Handles the production of our mob and adds it to our living_mobs list
     *
     * Used by the vent clog random event to handle the spawning of mobs. The proc receives the mob that will be spawned,
     * and the event's current list of living mobs produced by the event so far. After checking if the vent is welded, the
     * new mob is created on the vent's turf, then added to the living_mobs list.
     */
    // tg: code/modules/events/vent_clog.dm:146
    public virtual void produce_mob()
    {
        var vent_loc = get_turf(vent) as turf;
        if (vent_loc == null)
            throw Dm.CRASH(Dm.text("[] has no loc, aborting mobspawn", vent), "code/modules/events/vent_clog.dm", 149);

        if (Dm.truthy(vent.welded) || Dm.truthy(vent_loc.is_blocked_turf_ignore_climbable())) // vents under tables can still spawn stuff
            return;

        var new_mob = Dm.@new(spawned_mob, vent_loc) as mob; // we spawn it early so we can actually use is_blocked_turf
        Dm.append(ref living_mobs, WEAKREF(new_mob));
        vent.visible_message(span_warning(I18n.T("[] crawls out of []!", new_mob, vent)));

        DmList potential_locations = Dm.list(vent_loc); // already confirmed to be accessable via the 2nd if check of the proc

        // exists to prevent mobs from trying to move onto turfs they physically cannot
        foreach (turf nearby_turf in Dm.each<turf>(oview(1, get_turf(vent)))) // oview, since we always add our loc to the list
        {
            if (!Dm.truthy(Waiting.NoSleep(nearby_turf.is_blocked_turf(source_atom: new_mob))))
                Dm.append(ref potential_locations, nearby_turf);
        }

        var spawn_location = pick(potential_locations) as turf;
        Waiting.NoSleep(new_mob.Move(spawn_location));

        object filth_to_spawn = pick(filth_spawn_types);
        Dm.@new(filth_to_spawn as Type, spawn_location);
        playsound(spawn_location, "sound/effects/splat.ogg", 30, TRUE);
    }

    ///Signal catcher for plunger_act()
    // tg: code/modules/events/vent_clog.dm:173
    [SignalHandler]
    public virtual object plunger_unclog(datum source = null, obj_item_plunger attacking_plunger = null, mob user = null, object reinforced = null)
    {
        Waiting.Detach(attempt_unclog(user)); // INVOKE_ASYNC
        return COMPONENT_NO_AFTERATTACK;
    }

    ///Handles the actual unclogging action and ends the event on completion.
    // tg: code/modules/events/vent_clog.dm:179
    public virtual async DmTask attempt_unclog(mob user = null)
    {
        if (Dm.truthy(vent.welded))
        {
            to_chat(user, span_notice(I18n.T("You cannot pump [] if it's welded shut!", vent)));
            return;
        }

        user.balloon_alert_to_viewers(I18n.T("plunging vent..."), I18n.T("plunging clogged vent..."));
        if (Dm.truthy(await do_after(user, 6 * SECONDS, target: vent)))
        {
            user.balloon_alert_to_viewers(I18n.T("finished plunging"));
            clear_signals();
            kill();
        }
    }

    // tg: code/modules/events/vent_clog.dm:190
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(vent.welded))
        {
            Dm.append(examine_list, span_notice(I18n.T("Something's moving around in it, but it's being blocked by the welded opening.")));
            return;
        }
        Dm.append(examine_list, span_warning(I18n.T("Something's trying to get out. You could use a [] to send it back on its way.", span_green(I18n.T("plunger")))));
    }

    ///Handles the initial steps of clogging a vent, either at event start or when the vent moves.
    // tg: code/modules/events/vent_clog.dm:197
    public virtual void clog_vent()
    {
        RegisterSignal(vent, COMSIG_QDELETING, nameof(vent_move));
        RegisterSignal(vent, COMSIG_PLUNGER_ACT, nameof(plunger_unclog));
        RegisterSignal(vent, COMSIG_ATOM_EXAMINE, nameof(on_examine));

        foreach (turf nearby_turf in Dm.each<turf>(view(2, get_turf(vent))))
        {
            if (isopenturf(nearby_turf) && prob(85))
                Dm.@new<obj_effect_decal_cleanable_dirt>(nearby_turf);
        }

        produce_mob();
    }

    ///Clears the signals related to the event, before we wrap things up.
    // tg: code/modules/events/vent_clog.dm:209
    public virtual void clear_signals()
    {
        UnregisterSignal(vent, Dm.list(COMSIG_QDELETING, COMSIG_PLUNGER_ACT, COMSIG_ATOM_EXAMINE));
    }
}

// tg: code/modules/events/vent_clog.dm:222
public partial class datum_round_event_vent_clog_major : datum_round_event_vent_clog
{
    // tg: code/modules/events/vent_clog.dm:234  var/static/list/mob_list (a proc-level static, rules 10.6)
    private static DmList get_mob__mob_list => TgState.Current.datum_round_event_vent_clog_major__get_mob__mob_list ??= Dm.list(
        typeof(mob_living_basic_bee),
        typeof(mob_living_basic_cockroach_hauberoach),
        typeof(mob_living_basic_spider_giant),
        typeof(mob_living_basic_mouse_rat));

    // tg: code/modules/events/vent_clog.dm:222
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        maximum_spawns = rand(MOB_SPAWN_MINIMUM, 5);
        spawn_delay = rand(15, 20);
        filth_spawn_types = Dm.list(
            typeof(obj_effect_decal_cleanable_blood),
            typeof(obj_effect_decal_cleanable_insectguts),
            typeof(obj_effect_decal_cleanable_fuel_pool),
            typeof(obj_effect_decal_cleanable_blood_oil));
        return dot;
    }

    // tg: code/modules/events/vent_clog.dm:233
    public override Type get_mob()
    {
        return (Type)pick(get_mob__mob_list);
    }

    // tg: code/modules/events/vent_clog.dm:242
    public override async DmTask announce__proc(object fake = null)
    {
        object event_area = Dm.truthy(fake) ? pick(GLOB.teleportlocs) : get_area_name(vent);
        priority_announce(Dm.text("Major biological obstruction detected in the ventilation network. Blockage is believed to be in the [] area.", event_area), "Infestation Alert");
    }
}

// tg: code/modules/events/vent_clog.dm:257
public partial class datum_round_event_vent_clog_critical : datum_round_event_vent_clog
{
    // tg: code/modules/events/vent_clog.dm:271  var/static/list/mob_list (a proc-level static, rules 10.6)
    private static DmList get_mob__mob_list => TgState.Current.datum_round_event_vent_clog_critical__get_mob__mob_list ??= Dm.list(
        typeof(mob_living_basic_bee_toxin),
        typeof(mob_living_basic_carp),
        typeof(mob_living_basic_cockroach_glockroach));

    // tg: code/modules/events/vent_clog.dm:257
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        spawn_delay = rand(15, 25);
        maximum_spawns = rand(MOB_SPAWN_MINIMUM, 6);
        filth_spawn_types = Dm.list(
            typeof(obj_effect_decal_cleanable_blood),
            typeof(obj_effect_decal_cleanable_blood_splatter));
        return dot;
    }

    // tg: code/modules/events/vent_clog.dm:266
    public override async DmTask announce__proc(object fake = null)
    {
        object event_area = Dm.truthy(fake) ? pick(GLOB.teleportlocs) : get_area_name(vent);
        priority_announce(Dm.text("Potentially hazardous lifesigns detected in the [] ventilation network.", event_area), "Security Alert");
    }

    // tg: code/modules/events/vent_clog.dm:270
    public override Type get_mob()
    {
        return (Type)pick(get_mob__mob_list);
    }
}

// tg: code/modules/events/vent_clog.dm:287
public partial class datum_round_event_vent_clog_strange : datum_round_event_vent_clog
{
    // tg: code/modules/events/vent_clog.dm:304  var/static/list/mob_list (a proc-level static, rules 10.6)
    private static DmList get_mob__mob_list => TgState.Current.datum_round_event_vent_clog_strange__get_mob__mob_list ??= Dm.list(
        typeof(mob_living_basic_bear),
        typeof(mob_living_basic_cockroach_glockroach_mobroach),
        typeof(mob_living_basic_goose),
        typeof(mob_living_basic_lightgeist),
        typeof(mob_living_basic_mothroach),
        typeof(mob_living_basic_mushroom),
        typeof(mob_living_basic_viscerator),
        typeof(mob_living_basic_pet_gondola));

    // tg: code/modules/events/vent_clog.dm:287
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        end_when = rand(600, 900);
        spawn_delay = rand(6, 25);
        maximum_spawns = rand(MOB_SPAWN_MINIMUM, 10);
        filth_spawn_types = Dm.list(
            typeof(obj_effect_decal_cleanable_blood_xeno),
            typeof(obj_effect_decal_cleanable_fuel_pool),
            typeof(obj_effect_decal_cleanable_greenglow),
            typeof(obj_effect_decal_cleanable_vomit));
        return dot;
    }

    // tg: code/modules/events/vent_clog.dm:299
    public override async DmTask announce__proc(object fake = null)
    {
        object event_area = Dm.truthy(fake) ? pick(GLOB.teleportlocs) : get_area_name(vent);
        priority_announce(Dm.text("Unusual lifesign readings detected in the [] ventilation network.", event_area), "Lifesign Alert", ANNOUNCER_ALIENS);
    }

    // tg: code/modules/events/vent_clog.dm:303
    public override Type get_mob()
    {
        return (Type)pick(get_mob__mob_list);
    }
}

public sealed partial class TgState
{
    internal DmList datum_round_event_vent_clog__get_mob__mob_list;
    internal DmList datum_round_event_vent_clog_major__get_mob__mob_list;
    internal DmList datum_round_event_vent_clog_critical__get_mob__mob_list;
    internal DmList datum_round_event_vent_clog_strange__get_mob__mob_list;
}
