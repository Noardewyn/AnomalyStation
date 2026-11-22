// Translated from tgstation (snapshot 1d99bd7f): code/modules/station_goals/meteor_shield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R1 (D327): the whole file: the station shield goal (the coverage of the active satellites in view of space), the meteor shield
// satellite that shoots down the meteors coming into its range with a beam (each meteor's shield_defense, D299), and its emag, which
// doubles the meteor events' weight per tampered satellite, warns at 3, 6 and 7, and calls the dark matt-eor at 10. The var blocks are
// generated (D111).

//Station Shield
// A chain of satellites encircles the station
// Satellites be actived to generate a shield that will block unorganic matter from passing it.
// tg: code/modules/station_goals/meteor_shield.dm:15
public partial class datum_station_goal_station_shield : datum_station_goal
{
    // tg: code/modules/station_goals/meteor_shield.dm:21
    public override object get_report()
    {
        return Dm.list(
            I18n.T("The station is located in a zone full of space debris."),
            I18n.T("We have a prototype shielding system you must deploy to reduce collision-related accidents."),
            "",
            I18n.T("You can order the satellites and control systems at cargo.")
        ).Join("\n");
    }

    // tg: code/modules/station_goals/meteor_shield.dm:30
    public override void on_report()
    {
        //Unlock
        var P = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_shield_sat)] as datum_supply_pack;
        P.order_flags = Dm.num(Dm.bitor(P.order_flags, ORDER_SPECIAL_ENABLED));

        P = SSshuttle.supply_packs[typeof(datum_supply_pack_engineering_shield_sat_control)] as datum_supply_pack;
        P.order_flags = Dm.num(Dm.bitor(P.order_flags, ORDER_SPECIAL_ENABLED));
    }

    // tg: code/modules/station_goals/meteor_shield.dm:38
    public override object check_completion()
    {
        if (Dm.truthy(base.check_completion()))
            return TRUE;
        update_coverage();
        if (Dm.num(cached_coverage_length) >= coverage_goal)
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:46
    public virtual object get_coverage()
    {
        return cached_coverage_length;
    }

    /// Gets the coverage of all active meteor shield satellites
    /// Can be expensive, ensure you need this before calling it
    // tg: code/modules/station_goals/meteor_shield.dm:51
    public virtual void update_coverage()
    {
        DmList coverage = Dm.list();
        foreach (obj_machinery_satellite_meteor_shield shield_satt in Dm.each_cast<obj_machinery_satellite_meteor_shield>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_satellite_meteor_shield))))
        {
            if (!Dm.truthy(shield_satt.active) || !Dm.truthy(is_station_level(shield_satt.z)))
                continue;
            foreach (turf covered in Dm.each<turf>(view(shield_satt.kill_range, shield_satt)))
                Dm.combine(ref coverage, covered);
        }
        cached_coverage_length = Dm.length(coverage);
    }
}

// tg: code/modules/station_goals/meteor_shield.dm:60
public partial class obj_machinery_satellite_meteor_shield : obj_machinery_satellite
{
    // tg: code/modules/station_goals/meteor_shield.dm:1-10  (file-local, #undef at :193-198)
    /// number of emagged meteor shields to get the first warning, a simple say message
    private const int EMAGGED_METEOR_SHIELD_THRESHOLD_ONE = 3;
    /// number of emagged meteor shields to get the second warning, telling the user an announcement is coming
    private const int EMAGGED_METEOR_SHIELD_THRESHOLD_TWO = 6;
    /// number of emagged meteor shields to get the third warning + an announcement to the crew
    private const int EMAGGED_METEOR_SHIELD_THRESHOLD_THREE = 7;
    /// number of emagged meteor shields to get the fourth... ah shit the dark matt-eor is coming.
    private const int EMAGGED_METEOR_SHIELD_THRESHOLD_FOUR = 10;
    /// how long between emagging meteor shields you have to wait
    private const float METEOR_SHIELD_EMAG_COOLDOWN = 1 * MINUTES;

    /// amount of emagged active meteor shields
    // tg: code/modules/station_goals/meteor_shield.dm:73  var/static/emagged_active_meteor_shields (a type static, rules 10.6)
    private static float emagged_active_meteor_shields
    {
        get => TgState.Current.obj_machinery_satellite_meteor_shield__emagged_active_meteor_shields;
        set => TgState.Current.obj_machinery_satellite_meteor_shield__emagged_active_meteor_shields = value;
    }

    /// the highest amount of shields you've ever emagged
    // tg: code/modules/station_goals/meteor_shield.dm:75  var/static/highest_emagged_threshold_reached
    private static float highest_emagged_threshold_reached
    {
        get => TgState.Current.obj_machinery_satellite_meteor_shield__highest_emagged_threshold_reached;
        set => TgState.Current.obj_machinery_satellite_meteor_shield__highest_emagged_threshold_reached = value;
    }

    /// cooldown on emagging meteor shields because instantly summoning a dark matt-eor is very unfun
    // tg: code/modules/station_goals/meteor_shield.dm:77  STATIC_COOLDOWN_DECLARE(shared_emag_cooldown)
    private static float shared_emag_cooldown
    {
        get => TgState.Current.obj_machinery_satellite_meteor_shield__shared_emag_cooldown;
        set => TgState.Current.obj_machinery_satellite_meteor_shield__shared_emag_cooldown = value;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:79
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(active))
        {
            Dm.append(ref dot, span_notice(I18n.T("It is currently active. You can interact with it to shut it down.")));
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                Dm.append(ref dot, span_warning(I18n.T("Rather than the usual sounds of beeps and pings, it produces a weird and constant hiss of white noise…")));
            else
                Dm.append(ref dot, span_notice(I18n.T("It emits periodic beeps and pings as it communicates with the satellite network.")));
        }
        else
        {
            Dm.append(ref dot, span_notice(I18n.T("It is currently disabled. You can interact with it to set it up.")));
            if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
                Dm.append(ref dot, span_warning(I18n.T("But something seems off about it...?")));
        }
        return dot;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:92
    public virtual object space_los(object meteor = null)
    {
        foreach (turf T in Dm.each<turf>(get_line(this, meteor as atom)))
        {
            if (!isspaceturf(T))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:98
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, /* range = */ 0);
        return dot;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:102
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        object dot = await base.HasProximity(proximity_check_mob);
        if (proximity_check_mob is not obj_effect_meteor)
            return dot;
        var meteor_to_destroy = (obj_effect_meteor)proximity_check_mob;
        if (Dm.truthy(space_los(meteor_to_destroy)))
        {
            turf beam_from = get_turf(this);
            beam_from.Beam(get_turf(meteor_to_destroy), icon_state: "sat_beam", time: 5);
            if (Dm.truthy(meteor_to_destroy.shield_defense(this)))
                qdel(meteor_to_destroy);
        }
        return dot;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:113
    public override async DmTask<object> toggle(mob user = null)
    {
        if (user != null)
            balloon_alert(user, I18n.T("looking for [] button", Dm.truthy(active) ? I18n.T("off") : I18n.T("on")));
        if (user != null && !Dm.truthy(await do_after(user, 2 * SECONDS, this, timed_action_flags: IGNORE_HELD_ITEM)))
            return FALSE;
        if (!Dm.truthy(await base.toggle(user)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            await update_emagged_meteor_sat(user);

        if (Dm.truthy(active))
            await proximity_monitor.set_range(kill_range);
        else
            await proximity_monitor.set_range(0);

        var goal = SSstation.get_station_goal(typeof(datum_station_goal_station_shield)) as datum_station_goal_station_shield;
        goal?.update_coverage();
        return null;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:132
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref proximity_monitor);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            //satellites that are destroying are not active, this will count down the number of emagged sats
            Waiting.NoSleep(update_emagged_meteor_sat());
        return dot;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:139
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("already emagged!"));
            return FALSE;
        }
        // COOLDOWN_FINISHED(src, shared_emag_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(shared_emag_cooldown <= world.time))
        {
            balloon_alert(user, I18n.T("on cooldown!"));
            // COOLDOWN_TIMELEFT(src, shared_emag_cooldown) (__DEFINES/cooldowns.dm:117), inline
            to_chat(user, span_warning(I18n.T("The last satellite emagged needs [] to recalibrate first. Emagging another so soon could damage the satellite network.", DisplayTimeText(max(0, shared_emag_cooldown - world.time)))));
            return FALSE;
        }
        float cooldown_applied = METEOR_SHIELD_EMAG_COOLDOWN;
        // COOLDOWN_START(src, shared_emag_cooldown, cooldown_applied) (__DEFINES/cooldowns.dm:110), inline
        shared_emag_cooldown = world.time + cooldown_applied;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        to_chat(user, span_notice(I18n.T("You access the satellite's debug mode and it begins emitting a strange signal, increasing the chance of meteor strikes.")));
        await AddComponent(typeof(datum_component_gps), "Corrupted Meteor Shield Attraction Signal");
        await say(I18n.T("Recalibrating... ETA:[].", DisplayTimeText(cooldown_applied)));
        if (Dm.truthy(active)) //if we allowed inactive updates a sat could be worth -1 active meteor shields on first emag
            await update_emagged_meteor_sat(user);
        return TRUE;
    }

    // tg: code/modules/station_goals/meteor_shield.dm:157
    public virtual async DmTask update_emagged_meteor_sat(mob user = null)
    {
        if (!Dm.truthy(active))
        {
            change_meteor_chance(0.5f);
            emagged_active_meteor_shields--;
            if (user != null)
                balloon_alert(user, I18n.T("meteor probability halved"));
            return;
        }
        change_meteor_chance(2);
        emagged_active_meteor_shields++;
        if (user != null)
            balloon_alert(user, I18n.T("meteor probability doubled"));
        if (emagged_active_meteor_shields > highest_emagged_threshold_reached)
        {
            highest_emagged_threshold_reached = emagged_active_meteor_shields;
            await handle_new_emagged_shield_threshold();
        }
    }

    // tg: code/modules/station_goals/meteor_shield.dm:172
    public virtual async DmTask handle_new_emagged_shield_threshold()
    {
        switch (highest_emagged_threshold_reached)
        {
            case EMAGGED_METEOR_SHIELD_THRESHOLD_ONE:
                await say(I18n.T("Warning. Meteor strike probability entering dangerous ranges for more exotic meteors."));
                break;
            case EMAGGED_METEOR_SHIELD_THRESHOLD_TWO:
                await say(I18n.T("Warning. Risk of dark matter congealment entering existent ranges. Further tampering will be reported."));
                break;
            case EMAGGED_METEOR_SHIELD_THRESHOLD_THREE:
                await say(I18n.T("Warning. Further tampering has been reported."));
                priority_announce(I18n.T("Warning. Tampering of meteor satellites puts the station at risk of exotic, deadly meteor collisions. Please intervene by checking your GPS devices for strange signals, and dismantling the tampered meteor shields."), I18n.T("Strange Meteor Signal Warning"));
                break;
            case EMAGGED_METEOR_SHIELD_THRESHOLD_FOUR:
                await say(I18n.T("Warning. Warning. Dark Matt-eor on course for station."));
                force_event_async(typeof(datum_round_event_control_dark_matteor), "an array of tampered meteor satellites");
                break;
        }
    }

    // tg: code/modules/station_goals/meteor_shield.dm:185
    public virtual void change_meteor_chance(object mod = null)
    {
        // Update the weight of all meteor events
        foreach (datum_round_event_control_meteor_wave meteors in Dm.each<datum_round_event_control_meteor_wave>(SSevents.control))
            meteors.weight *= Dm.num(mod);
        foreach (datum_round_event_control_stray_meteor stray_meteor in Dm.each<datum_round_event_control_stray_meteor>(SSevents.control))
            stray_meteor.weight *= Dm.num(mod);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/station_goals/meteor_shield.dm:73  var/static/emagged_active_meteor_shields = 0
    internal float obj_machinery_satellite_meteor_shield__emagged_active_meteor_shields;
    // tg: code/modules/station_goals/meteor_shield.dm:75  var/static/highest_emagged_threshold_reached = 0
    internal float obj_machinery_satellite_meteor_shield__highest_emagged_threshold_reached;
    // tg: code/modules/station_goals/meteor_shield.dm:77  STATIC_COOLDOWN_DECLARE(shared_emag_cooldown)
    internal float obj_machinery_satellite_meteor_shield__shared_emag_cooldown;
}
