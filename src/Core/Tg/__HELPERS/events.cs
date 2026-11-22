// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/events.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the spawn finders and the forced events (syndicate N1c, D316: the round's events), and Gaps R3 (D327): the decoration
// colours of the holidays (request_decoration_colors, get_decoration_color_from_pattern), with the station traits' colours (gaps R2).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/events.dm:1  #define UNLIT_AREA_BRIGHTNESS 0.2
    private const float UNLIT_AREA_BRIGHTNESS = 0.2f;

    /**
     * Finds us a generic maintenance spawn location.
     *
     * Goes through the list of the generic mainteance landmark locations, checking for atmos safety if required, and returns
     * a valid turf. Returns MAP_ERROR if no valid locations are present.
     * Returns nothing and alerts admins if no valid points are found. Keep this in mind
     * when using this helper.
     */
    // tg: code/__HELPERS/events.dm:13
    public static object find_maintenance_spawn(float? atmos_sensitive = null, float? require_darkness = null)
    {
        atmos_sensitive ??= FALSE;
        require_darkness ??= FALSE;
        DmList possible_spawns = Dm.list();
        foreach (object spawn_location in Dm.each(GLOB.generic_maintenance_landmarks))
        {
            var spawn_turf = get_turf(spawn_location as atom) as turf;

            if (Dm.truthy(atmos_sensitive) && !Dm.truthy(is_safe_turf(spawn_turf)))
                continue;

            if (Dm.truthy(require_darkness) && Dm.truthy(spawn_turf.check_lumcount_above(UNLIT_AREA_BRIGHTNESS)))
                continue;

            Dm.append(ref possible_spawns, spawn_turf);
        }

        if (!Dm.truthy(length(possible_spawns)))
            return null;

        return pick(possible_spawns);
    }

    /**
     * Finds us a generic spawn location in space.
     *
     * Goes through the list of the space carp spawn locations, picks from the list, and
     * returns that turf. Returns MAP_ERROR if no landmarks are found.
     */
    // tg: code/__HELPERS/events.dm:37
    public static object find_space_spawn()
    {
        DmList possible_spawns = Dm.list();
        foreach (obj_effect_landmark_carpspawn spawn_location in Dm.each<obj_effect_landmark_carpspawn>(GLOB.landmarks_list))
        {
            if (!isturf(spawn_location.loc))
            {
                stack_trace(Dm.text("Carp spawn found not on a turf: [] on []", spawn_location.type, spawn_location.loc == null ? "null" : spawn_location.loc.type),
                    "code/__HELPERS/events.dm", 41);
                continue;
            }
            Dm.append(ref possible_spawns, get_turf(spawn_location));
        }

        if (!Dm.truthy(length(possible_spawns)))
            return null;

        return pick(possible_spawns);
    }

    /// Finds us all suitable vent spawn locations on the station.
    // tg: code/__HELPERS/events.dm:51
    public static object find_vent_spawns()
    {
        DmList vents = Dm.list();
        DmList vent_pumps = SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_atmospherics_components_unary_vent_pump)) as DmList;
        foreach (obj_machinery_atmospherics_components_unary_vent_pump temp_vent in Dm.each<obj_machinery_atmospherics_components_unary_vent_pump>(vent_pumps))
        {
            if (QDELETED(temp_vent))
                continue;
            if (!Dm.truthy(is_station_level(temp_vent.loc.z)) || Dm.truthy(temp_vent.welded))
                continue;
            var temp_vent_parent = temp_vent.parents[1] as datum_pipeline;
            if (temp_vent_parent == null)
                continue;
            // Stops antagonists getting stuck in small networks.
            // See: Security, Virology
            if (length(temp_vent_parent.other_atmos_machines) <= 20)
                continue;
            Dm.append(ref vents, temp_vent);
        }
        return vents;
    }

    // tg: code/__HELPERS/events.dm:68
    public static async DmTask force_event(object event_typepath = null, object cause = null)
    {
        var our_event = locate_event_control(event_typepath);
        if (our_event == null)
            throw Dm.CRASH(Dm.text("Attempted to force event [], but the event path could not be found!", event_typepath), "code/__HELPERS/events.dm", 71);
        await our_event.run_event(event_cause: cause);
    }

    // tg: code/__HELPERS/events.dm:74
    public static void force_event_async(object event_typepath = null, object cause = null)
    {
        var our_event = locate_event_control(event_typepath);
        if (our_event == null)
            throw Dm.CRASH(Dm.text("Attempted to force event [], but the event path could not be found!", event_typepath), "code/__HELPERS/events.dm", 77);
        Waiting.Detach(our_event.run_event(event_cause: cause)); // INVOKE_ASYNC
    }

    // tg: code/__HELPERS/events.dm:80
    public static void force_event_after(object event_typepath = null, object cause = null, object duration = null)
    {
        var our_event = locate_event_control(event_typepath);
        if (our_event == null)
            throw Dm.CRASH(Dm.text("Attempted to force event [], but the event path could not be found!", event_typepath), "code/__HELPERS/events.dm", 83);
        _addtimer(Dm.@new<datum_callback>(our_event, nameof(datum_round_event_control.run_event), FALSE, null, FALSE, cause), Dm.num(duration),
            file: "code/__HELPERS/events.dm", line: 84);
    }

    // locate(event_typepath) in SSevents.control: the first control of that type or a subtype
    private static datum_round_event_control locate_event_control(object event_typepath)
    {
        foreach (datum_round_event_control control in Dm.each<datum_round_event_control>(SSevents?.control))
        {
            if (istype(control, event_typepath as Type))
                return control;
        }
        return null;
    }
}

// Gaps R3 (D327): the holidays' decoration colours (events.dm:88-120)
public static partial class GlobalProcs
{
    //Request a color in line with the current holiday or station traits of the station (or not if RANDOM/RAINBOW patterns are used.
    // tg: code/__HELPERS/events.dm:88
    public static object request_decoration_colors(atom thing_to_color = null, object pattern = null, object skip_station_trait = null)
    {
        if (Dm.eq(pattern, PATTERN_RANDOM))
            return Dm.text("#[]", random_short_color());
        if (Dm.eq(pattern, PATTERN_RAINBOW))
            return get_decoration_color_from_pattern(thing_to_color, PATTERN_DEFAULT, PRIDE_FLAG_COLORS());

        if (!Dm.truthy(skip_station_trait))
        {
            // tg: :96  (gaps R2: the station traits' decal colours, the radioactive nebula's)
            // SSstation?: a host-built room without the Master has no SSstation (rules 10.6), as the robot's SSjob? (robot.cs)
            foreach (datum_station_trait trait in Dm.each<datum_station_trait>(SSstation?.station_traits))
            {
                object decal_color = trait.get_decal_color(thing_to_color, (pattern is var or__1 && Dm.truthy(or__1) ? or__1 : PATTERN_DEFAULT));
                if (Dm.truthy(decal_color))
                    return decal_color;
            }
        }

        foreach (object holiday_key in Dm.each(GLOB.holidays))
        {
            var holiday_real = (datum_holiday)GLOB.holidays[holiday_key];
            if (!Dm.truthy(holiday_real.holiday_colors))
                continue;
            return holiday_real.get_holiday_colors(thing_to_color, pattern);
        }
        return null;
    }

    /// Proc to return colors for recoloring atoms based on a pattern and the position of the atom. Primarily used by holidays
    // tg: code/__HELPERS/events.dm:108
    public static object get_decoration_color_from_pattern(atom thing_to_color = null, object pattern = null, DmList colors = null)
    {
        pattern ??= PATTERN_DEFAULT;
        if (Dm.length(colors) == 0)
            return null;
        switch (Dm.str(pattern))
        {
            case PATTERN_DEFAULT:
                return colors[(Dm.num(thing_to_color.y) % colors.len) + 1];
            case PATTERN_VERTICAL_STRIPE:
                return colors[(Dm.num(thing_to_color.x) % colors.len) + 1];
        }
        return null;
    }
}
