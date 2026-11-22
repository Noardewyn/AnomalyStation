// Translated from tgstation (snapshot 1d99bd7f): code/datums/helper_datums/teleport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// find_safe_turf (gaps package C6), is_safe_turf (M4c slice 5) and do_teleport with its checks (gaps package C4: the carp's rift); find_safe_turf waits for its first caller (a row).

public sealed partial class TgState
{
    // tg: code/datums/helper_datums/teleport.dm:190  (var/static/list/gases_to_check in /proc/is_safe_turf; a list of
    // lists and types, no datum and no world id: created at its first use, where the type-var reads have the world)
    internal DmList GlobalProcs__is_safe_turf__gases_to_check;
}

public static partial class GlobalProcs
{
    private static ref DmList is_safe_turf__gases_to_check => ref TgState.Current.GlobalProcs__is_safe_turf__gases_to_check;

    /**
     * Checks to see if a given turf is a "safe" location. Being safe requires the following to be true:
     * * Must be a [floor][/turf/open/floor]
     * * Must have air, and that air must have [breathable bounds][/proc/check_gases] for humans
     * * Must have goldilocks temperature
     * * Must have safe pressure
     *
     * Optionally:
     * * extended_safety_checks: Will make additional checks for turfs that technically pass all previous requirements but still may not be safe
     * * dense_atoms: Must be unobstructed (no blocking objects such as machines, structures or mobs)
     * * no_teleport: Must not have [NOTELEPORT][/area/var/area_flag]
     *
     * Returns TRUE if all conditions pass, FALSE otherwise.
     */
    // tg: code/datums/helper_datums/teleport.dm:174
    public static object is_safe_turf(turf random_location = null, float? extended_safety_checks = null, float? dense_atoms = null, float? no_teleport = null)
    {
        extended_safety_checks ??= FALSE;
        dense_atoms ??= FALSE;
        no_teleport ??= FALSE;
        // SHOULD_BE_PURE(TRUE): a compile-time lint (rules 11.1)

        object dot = FALSE;
        if (!isfloorturf(random_location))
            return dot;
        turf_open_floor floor_turf = random_location as turf_open_floor;
        area destination_area = floor_turf.loc as area;

        if (Dm.truthy(no_teleport) && Dm.truthy(Dm.bitand(destination_area.area_flags, NOTELEPORT)))
            return dot;

        datum_gas_mixture floor_gas_mixture = floor_turf.air;
        if (floor_gas_mixture == null)
            return dot;

        is_safe_turf__gases_to_check ??= Dm.list(
            Dm.kv(typeof(datum_gas_oxygen), Dm.list(Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.safe_oxygen_min), 100)),
            typeof(datum_gas_nitrogen),
            Dm.kv(typeof(datum_gas_carbon_dioxide), Dm.list(0, Dm.initial(typeof(obj_item_organ_lungs), (obj_item_organ_lungs p) => p.safe_co2_max))));
        if (!Dm.truthy(floor_gas_mixture.check_gases(is_safe_turf__gases_to_check)))
            return FALSE;

        // Aim for goldilocks temperatures and pressure
        if ((floor_gas_mixture.temperature <= BODYTEMP_COLD_DAMAGE_LIMIT) || (floor_gas_mixture.temperature >= BODYTEMP_HEAT_DAMAGE_LIMIT))
            return dot;
        float pressure = Dm.num(floor_gas_mixture.return_pressure());
        if ((pressure <= HAZARD_LOW_PRESSURE) || (pressure >= HAZARD_HIGH_PRESSURE))
            return dot;

        if (Dm.truthy(extended_safety_checks))
        {
            if (islava(floor_turf)) //chasms aren't /floor, and so are pre-filtered
            {
                // var/turf/open/lava/lava_turf = floor_turf; if(!lava_turf.is_safe()) return: /turf/open/lava is not a
                // /turf/open/floor, so isfloorturf above already returned for it (tg's own comment calls this dead code)
                Dm.NotTranslated("code/datums/helper_datums/teleport.dm:208", "is_safe_turf: lava_turf.is_safe(), lava (the planets, after M10)");
            }
        }

        // Check that we're not warping onto a table or window
        if (!Dm.truthy(dense_atoms))
        {
            bool density_found = false;
            foreach (atom_movable found_movable in Dm.each<atom_movable>(floor_turf))
            {
                if (Dm.truthy(found_movable.density))
                {
                    density_found = true;
                    break;
                }
            }
            if (density_found)
                return dot;
        }

        // DING! You have passed the gauntlet, and are "probably" safe.
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    // tg: code/datums/helper_datums/teleport.dm:10
    public static async DmTask<object> do_teleport(atom_movable teleatom = null, atom destination = null, object precision = null, datum_effect_system effectin = null, datum_effect_system effectout = null, object asoundin = null, object asoundout = null, object no_effects = null, object channel = null, object forced = null)
    {
        channel ??= TELEPORT_CHANNEL_BLUESPACE;
        no_effects ??= FALSE;
        forced ??= FALSE;
        // teleporting most effects just deletes them
        DmList delete_atoms = TgState.Current.do_teleport__delete_atoms ??= zebra_typecacheof(Dm.list(
            Dm.kv(typeof(obj_effect), TRUE),
            Dm.kv(typeof(obj_effect_dummy_chameleon), FALSE),
            Dm.kv(typeof(obj_effect_wisp), FALSE),
            Dm.kv(typeof(obj_effect_mob_spawn), FALSE),
            Dm.kv(typeof(obj_effect_immovablerod), FALSE),
            Dm.kv(typeof(obj_effect_meteor), FALSE))) as DmList;
        if (Dm.truthy(delete_atoms[teleatom.GetType()]))
        {
            qdel(teleatom);
            return FALSE;
        }

        // argument handling
        // if the precision is not specified, default to 0, but apply BoH interference if there is one
        if (precision == null)
            precision = 0f;

        if (Dm.eq(channel, TELEPORT_CHANNEL_BLUESPACE))
        {
            // check bluespace interference
            float interference = 0;
            foreach (obj_item check in Dm.each_cast<obj_item>(teleatom.get_all_contents_type(typeof(obj_item))))
            {
                if (HAS_TRAIT(check, TRAIT_BLUESPACE_INTERFERENCE))
                    interference += 1;
            }
            if (interference != 0)
            {
                precision = Dm.max(rand(1, 100) * interference, 100);
                if (isliving(teleatom))
                    to_chat(teleatom, span_warning("The clashing pulls of bluespace interfere with your teleport!"));
            }
            // if effects are not specified and not explicitly disabled, sparks
            if ((effectin == null || effectout == null) && !Dm.truthy(no_effects))
            {
                datum_effect_system_basic_spark_spread sparks = Dm.@new<datum_effect_system_basic_spark_spread>(teleatom, 5f, TRUE);
                effectin ??= sparks;
                effectout ??= sparks;
            }
        }
        else if (Dm.eq(channel, TELEPORT_CHANNEL_QUANTUM))
        {
            // if effects are not specified and not explicitly disabled, rainbow sparks
            if ((effectin == null || effectout == null) && !Dm.truthy(no_effects))
            {
                datum_effect_system_basic_spark_spread_quantum sparks = Dm.@new<datum_effect_system_basic_spark_spread_quantum>(teleatom, 5f, TRUE);
                effectin ??= sparks;
                effectout ??= sparks;
            }
        }

        // perform the teleport
        turf curturf = get_turf(teleatom) as turf;
        if (curturf == null)
            return FALSE;

        turf destturf = null;
        turf og_destination = get_turf(destination) as turf;
        if (Dm.truthy(precision))
            destturf = get_valid_teleport_turf(curturf, og_destination, precision, forced) as turf;
        else if (!Dm.truthy(og_destination.is_transition_turf()))
            destturf = og_destination;

        if (destturf == null || (!Dm.truthy(forced) && !Dm.truthy(check_teleport_valid(teleatom, destturf, channel, destination))))
        {
            if (ismob(teleatom))
                teleatom.balloon_alert(teleatom as mob, "something holds you back!");
            return FALSE;
        }

        if (Dm.truthy(teleatom._listen_lookup?[COMSIG_MOVABLE_TELEPORTING]) && Dm.truthy(teleatom._SendSignal(COMSIG_MOVABLE_TELEPORTING, Dm.list(teleatom, destination, channel))))
            return FALSE;

        if (Dm.truthy(destturf._listen_lookup?[COMSIG_ATOM_INTERCEPT_TELEPORTING]) && Dm.truthy(Dm.bitand(destturf._SendSignal(COMSIG_ATOM_INTERCEPT_TELEPORTING, Dm.list(destturf, channel, curturf)), COMPONENT_INTERCEPT_TELEPORT)))
            return FALSE;

        if (isobserver(teleatom))
        {
            await teleatom.abstract_move(destturf);
            return TRUE;
        }

        tele_play_specials(teleatom, curturf, effectin, asoundin);
        object success = await teleatom.forceMove(destturf);
        if (!Dm.truthy(success))
            return FALSE;

        object dot = TRUE;
        tele_play_specials(teleatom, destturf, effectout, asoundout);

        if (ismob(teleatom))
        {
            mob M = teleatom as mob;
            Waiting.NoSleep(M.reset_perspective(null)); // cancel_camera: the verb's body (mob.dm) without the machine unset
        }

        if (Dm.truthy(teleatom._listen_lookup?[COMSIG_MOVABLE_POST_TELEPORT]))
            teleatom._SendSignal(COMSIG_MOVABLE_POST_TELEPORT, Dm.list(teleatom, destination, channel));

        //We need to teleport any riders as well
        if (Dm.truthy(teleatom.has_buckled_mobs()))
        {
            foreach (mob_living rider in Dm.each<mob_living>(teleatom.buckled_mobs))
            {
                await teleatom.unbuckle_mob(rider, TRUE, FALSE);
                object rider_success = await do_teleport(rider, destturf, precision, channel: channel, no_effects: TRUE);
                if (!Dm.truthy(rider_success))
                    continue;

                if (get_turf(rider) != destturf) //precision made them teleport somewhere else
                {
                    to_chat(rider, span_warning(Dm.text("As you reorient your senses, you realize you aren't riding [] anymore!", teleatom)));
                    continue;
                }

                await teleatom.buckle_mob(rider, force: TRUE);
            }
        }
        return dot;
    }

    // tg: code/datums/helper_datums/teleport.dm:121
    public static void tele_play_specials(atom_movable teleatom = null, atom location = null, datum_effect_system effect = null, object sound = null)
    {
        if (location == null)
            return;
        if (Dm.truthy(sound))
            playsound(location, sound, 60, TRUE);
        if (effect != null)
            Waiting.NoSleep(effect.attach(location).start());
    }

    /// Returns a turf in range of the center that a teleport can land on, or null.
    // tg: code/datums/helper_datums/teleport.dm:225
    public static object get_valid_teleport_turf(turf origin = null, turf center = null, object range = null, object skip_restrictions = null)
    {
        DmList turfs = Dm.list();
        area origin_area = origin.loc as area;
        foreach (turf turf in Dm.each_cast<turf>(RANGE_TURFS(Dm.num(range), center)))
        {
            if (Dm.truthy(turf.is_transition_turf()))
                continue; // Avoid picking these at all cost
            if (Dm.truthy(skip_restrictions))
            {
                Dm.append(ref turfs, turf);
                continue;
            }
            if (HAS_TRAIT(turf, TRAIT_NO_TELEPORT))
                continue;
            area area = turf.loc as area;
            if (Dm.truthy(Dm.bitand(area.area_flags, NOTELEPORT)))
                continue;
            if ((Dm.truthy(Dm.bitand(origin_area.area_flags, LOCAL_TELEPORT)) || Dm.truthy(Dm.bitand(area.area_flags, LOCAL_TELEPORT))) && origin_area != area)
                continue;
            Dm.append(ref turfs, turf);
        }

        if (Dm.length(turfs) > 0)
            return pick(turfs);
        return null;
    }

    /// Validates that the teleatom can take the given teleport
    // tg: code/datums/helper_datums/teleport.dm:249
    public static object check_teleport_valid(atom teleported_atom = null, atom destination = null, object channel = null, atom original_destination = null)
    {
        if (destination == null)
            return FALSE; // Teleporting FROM nullspace is fine, but TO nullspace is not
        area origin_area = get_area(teleported_atom) as area;
        area destination_area = get_area(destination) as area;
        turf destination_turf = get_turf(destination) as turf;

        if (HAS_TRAIT(teleported_atom, TRAIT_NO_TELEPORT) || HAS_TRAIT(destination_turf, TRAIT_NO_TELEPORT))
            return FALSE;

        // prevent teleports across reserved areas: the reservations are not ported, so a teleport only meets the checks below

        if (Dm.truthy(Dm.bitand(origin_area.area_flags, NOTELEPORT)) || Dm.truthy(Dm.bitand(destination_area.area_flags, NOTELEPORT)))
            return FALSE;

        if ((Dm.truthy(Dm.bitand(origin_area.area_flags, LOCAL_TELEPORT)) || Dm.truthy(Dm.bitand(destination_area.area_flags, LOCAL_TELEPORT))) && destination_area != origin_area)
            return FALSE;

        return TRUE;
    }
}

public sealed partial class TgState
{
    internal DmList do_teleport__delete_atoms;
}

public static partial class GlobalProcs
{
    // tg: code/datums/helper_datums/teleport.dm:139
    public static object find_safe_turf(object zlevel_or_levels = null, float? extended_safety_checks = null, float? dense_atoms = null)
    {
        DmList zlevels;
        if (zlevel_or_levels is DmList levels)
            zlevels = levels;
        else if (Dm.truthy(zlevel_or_levels))
            zlevels = Dm.list(zlevel_or_levels);
        else
            zlevels = SSmapping.levels_by_trait(ZTRAIT_STATION) as DmList;

        for (int cycle = 1; cycle <= 1000; cycle++)
        {
            float x = rand(1, world.maxx);
            float y = rand(1, world.maxy);
            float z = Dm.num(pick(zlevels));
            turf random_location = locate(x, y, z);
            bool keep_trying_no_teleport = cycle < 300; //if the area is mostly NOTELEPORT (centcom) we gotta give up on this fantasy at some point
            if (Dm.truthy(is_safe_turf(random_location, extended_safety_checks, dense_atoms, keep_trying_no_teleport ? TRUE : FALSE)))
                return random_location;
        }
        return null;
    }
}
