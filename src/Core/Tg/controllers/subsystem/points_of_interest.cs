// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/points_of_interest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice C (D281): the whole file, the ghosts' points of interest (the orbit menu, Jump to Mob, Observe). The var blocks are
// generated.

/// Subsystem for managing all POIs.
// tg: code/controllers/subsystem/points_of_interest.dm:2 SUBSYSTEM_DEF(points_of_interest)
[DmPath("/datum/controller/subsystem/points_of_interest")]
public partial class datum_controller_subsystem_points_of_interest : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(points_of_interest) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSpoints_of_interest)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/points_of_interest.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSpoints_of_interest != this)
        {
            if (SSpoints_of_interest is datum_controller_subsystem_points_of_interest)
            {
                Recover();
                qdel(SSpoints_of_interest);
            }
            SSpoints_of_interest = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    /**
     * Turns new_poi into a new point of interest by adding the /datum/element/point_of_interest element to it.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:23
    public virtual async DmTask make_point_of_interest(atom new_poi = null)
    {
        await new_poi.AddElement(typeof(datum_element_point_of_interest));
    }

    /**
     * Stops old_poi from being a point of interest by removing the /datum/element/point_of_interest element from it.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:29
    public virtual void remove_point_of_interest(atom old_poi = null)
    {
        old_poi.RemoveElement(typeof(datum_element_point_of_interest));
    }

    /**
     * Called by [/datum/element/point_of_interest] when it gets removed from old_poi.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:35
    public virtual void on_poi_element_added(atom new_poi = null)
    {
        datum_point_of_interest new_poi_datum;
        if (ismob(new_poi))
        {
            new_poi_datum = Dm.@new<datum_point_of_interest_mob_poi>(new_poi);
            binary_insert_poi(new_poi_datum, mob_points_of_interest);
            points_of_interest_by_target_ref[REF(new_poi)] = new_poi_datum;
        }
        else
        {
            new_poi_datum = Dm.@new<datum_point_of_interest>(new_poi);
            binary_insert_poi(new_poi_datum, other_points_of_interest);
            points_of_interest_by_target_ref[REF(new_poi)] = new_poi_datum;

            // NUKE DISK HELPER
            if (new_poi is obj_item_disk_nuclear nuke_disk)
            {
                if (!Dm.truthy(nuke_disk.fake))
                    Dm.append(ref real_nuclear_disks, nuke_disk);
            }
            // NAR'SIE HELPER
            else if (new_poi is obj_narsie)
            {
                Dm.append(ref narsies, new_poi);
            }
        }

        if (Dm.truthy(_listen_lookup?[COMSIG_ADDED_POINT_OF_INTEREST]))
            _SendSignal(COMSIG_ADDED_POINT_OF_INTEREST, Dm.list(this, new_poi));
    }

    // BINARY_INSERT_PROC_COMPARE(new_poi_datum, LIST, /datum/point_of_interest(/mob_poi), new_poi_datum, compare_to, COMPARE_KEY)
    // (__HELPERS/_lists.dm:177), expanded inline (rules 11.1) once for both lists: compare_to is virtual, so the mob list's items
    // compare as mob POIs.
    private static void binary_insert_poi(datum_point_of_interest input, DmList list)
    {
        float cttl = length(list);
        if (!Dm.truthy(cttl))
        {
            Dm.append(ref list, input);
            return;
        }
        float left = 1;
        float right = cttl;
        float mid = Dm.num(Dm.shr(left + right, 1));
        datum_point_of_interest item;
        while (left < right)
        {
            item = list[mid] as datum_point_of_interest;
            if (Dm.num(item.compare_to(input)) <= 0)
                left = mid + 1;
            else
                right = mid;
            mid = Dm.num(Dm.shr(left + right, 1));
        }
        item = list[mid] as datum_point_of_interest;
        mid = Dm.num(item.compare_to(input)) > 0 ? mid : mid + 1;
        list.Insert(mid, input);
    }

    /**
     * Called by [/datum/element/point_of_interest] when it gets removed from old_poi.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:62
    public virtual void on_poi_element_removed(atom old_poi = null)
    {
        object poi_ref = REF(old_poi);
        datum_point_of_interest poi_to_remove = points_of_interest_by_target_ref[poi_ref] as datum_point_of_interest;

        if (poi_to_remove == null)
            return;

        if (ismob(old_poi))
        {
            Dm.remove(ref mob_points_of_interest, poi_to_remove);
        }
        else
        {
            Dm.remove(ref other_points_of_interest, poi_to_remove);

            // NUKE DISK HELPER
            if (old_poi is obj_item_disk_nuclear nuke_disk)
            {
                if (!Dm.truthy(nuke_disk.fake))
                    Dm.remove(ref real_nuclear_disks, nuke_disk);
            }
            // NAR'SIE HELPER
            else if (old_poi is obj_narsie)
            {
                Dm.remove(ref narsies, old_poi);
            }
        }

        Dm.remove(ref points_of_interest_by_target_ref, poi_ref);

        poi_to_remove.target = null;

        if (Dm.truthy(_listen_lookup?[COMSIG_REMOVED_POINT_OF_INTEREST]))
            _SendSignal(COMSIG_REMOVED_POINT_OF_INTEREST, Dm.list(this, old_poi));
    }

    /**
     * If there is a valid POI for a given reference, it returns that POI's associated atom. Otherwise, it returns null.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:96
    public virtual object get_poi_atom_by_ref(object reference = null)
    {
        return (points_of_interest_by_target_ref[reference] as datum_point_of_interest)?.target;
    }

    /**
     * Returns a list of mob POIs with names as keys and mobs as values.
     *
     * If multiple POIs have the same name, then avoid_assoc_duplicate_keys is used alongside used_name_list to
     * tag them as Mob Name (1), Mob Name (2), Mob Name (3) etc.
     *
     * Arguments:
     * * poi_validation_override - [OPTIONAL] Callback to a proc that takes a single argument for the POI and returns TRUE if this POI should be included. Overrides standard POI validation.
     * * append_dead_role - [OPTIONAL] If TRUE, adds a ghost tag to the end of observer names and a dead tag to the end of any other mob which is not alive.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:110
    public virtual async DmTask<object> get_mob_pois(datum_callback poi_validation_override = null, float? append_dead_role = null)
    {
        append_dead_role ??= TRUE;
        DmList pois = Dm.list();
        DmList used_name_list = Dm.list();

        foreach (datum_point_of_interest_mob_poi mob_poi in Dm.each_cast<datum_point_of_interest_mob_poi>(mob_points_of_interest))
        {
            if (Dm.truthy(poi_validation_override))
            {
                if (!Dm.truthy(await poi_validation_override.Invoke(mob_poi)))
                    continue;
            }
            else if (!Dm.truthy(mob_poi.validate()))
            {
                continue;
            }

            mob target_mob = (mob)mob_poi.target;
            object name = Dm.add(avoid_assoc_duplicate_keys(target_mob.name, used_name_list), target_mob.get_realname_string());

            // Add the ghost/dead tag to the end of dead mob POIs.
            if (Dm.truthy(append_dead_role) && target_mob.stat == DEAD)
            {
                if (isobserver(target_mob))
                    name = Dm.add(name, I18n.T(" \\[ghost\\]"));
                else
                    name = Dm.add(name, I18n.T(" \\[dead\\]"));
            }

            pois[name] = target_mob;
        }

        return pois;
    }

    /**
     * Returns a list of non-mob POIs with names as keys and atoms as values.
     *
     * If multiple POIs have the same name, then avoid_assoc_duplicate_keys is used alongside used_name_list to
     * tag them as Object Name (1), Object Name (2), Object Name (3) etc.
     *
     * Arguments:
     * * poi_validation_override - [OPTIONAL] Callback to a proc that takes a single argument for the POI and returns TRUE if this POI should be included. Overrides standard POI validation.
     */
    // tg: code/controllers/subsystem/points_of_interest.dm:146
    public virtual async DmTask<object> get_other_pois(datum_callback poi_validation_override = null)
    {
        DmList pois = Dm.list();
        DmList used_name_list = Dm.list();

        foreach (datum_point_of_interest other_poi in Dm.each_cast<datum_point_of_interest>(other_points_of_interest))
        {
            if (Dm.truthy(poi_validation_override))
            {
                if (!Dm.truthy(await poi_validation_override.Invoke(other_poi)))
                    continue;
            }
            else if (!Dm.truthy(other_poi.validate()))
            {
                continue;
            }

            atom target_poi = other_poi.target;

            pois[avoid_assoc_duplicate_keys(target_poi.name, used_name_list)] = target_poi;
        }

        return pois;
    }

    /// Returns TRUE if potential_poi has an associated poi_datum that validates.
    // tg: code/controllers/subsystem/points_of_interest.dm:166
    public virtual async DmTask<object> is_valid_poi(atom potential_poi = null, datum_callback poi_validation_override = null)
    {
        datum_point_of_interest poi_datum = points_of_interest_by_target_ref[REF(potential_poi)] as datum_point_of_interest;

        if (poi_datum == null)
            return FALSE;

        if (Dm.truthy(poi_validation_override))
            return await poi_validation_override.Invoke(poi_datum);

        return poi_datum.validate();
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/points_of_interest.dm:2  SUBSYSTEM_DEF(points_of_interest): GLOBAL_REAL(SSpoints_of_interest, ...)
    internal datum_controller_subsystem_points_of_interest SSpoints_of_interest;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_points_of_interest SSpoints_of_interest => ref TgState.Current.SSpoints_of_interest;
}

/// Simple helper datum for points of interest.
// tg: code/controllers/subsystem/points_of_interest.dm:182
[DmPath("/datum/point_of_interest")]
public partial class datum_point_of_interest : datum
{
    // tg: code/controllers/subsystem/points_of_interest.dm:188
    [DmParams("poi_target")]
    public override async DmTask New(params object[] __extra)
    {
        object poi_target = Dm.arg(__extra, 0);
        if (!istype(poi_target, poi_type))
            throw Dm.CRASH(Dm.text("Incorrect target type provided to /datum/point_of_interest/New: Expected \\[[]\\]", poi_type), "code/controllers/subsystem/points_of_interest.dm", 190);

        target = (atom)poi_target;
        await DmTask.CompletedTask;
    }

    /// Validates the POI. Returns TRUE if the POI has valid state, returns FALSE if the POI has invalid state.
    // tg: code/controllers/subsystem/points_of_interest.dm:195
    public virtual object validate()
    {
        // In nullspace, invalid as a POI.
        if (!Dm.truthy(target.loc))
            return FALSE;

        return TRUE;
    }

    /// Comparison proc used to sort POIs. Override to implement logic used doing binary sort insertions.
    // tg: code/controllers/subsystem/points_of_interest.dm:203
    public virtual object compare_to(datum_point_of_interest rhs = null)
    {
        return cmp_name_asc(target, rhs.target);
    }
}

// tg: code/controllers/subsystem/points_of_interest.dm:206
[DmPath("/datum/point_of_interest/mob_poi")]
public partial class datum_point_of_interest_mob_poi : datum_point_of_interest
{
    /// Validation for mobs is expanded to invalidate stealthmins and /mob/dead/new_player as POIs.
    // tg: code/controllers/subsystem/points_of_interest.dm:210
    public override object validate()
    {
        object dot = base.validate();

        if (!Dm.truthy(dot))
            return dot;

        mob poi_mob = (mob)target;

        // Stealthmin, invalid as a POI.
        if (Dm.truthy(poi_mob.client?.holder?.fakekey))
            return FALSE;

        // POI is a /mob/dead/new_player, players in the lobby are invalid as POIs.
        if (isnewplayer(poi_mob))
            return FALSE;

        return TRUE;
    }

    /// Mob POIs are sorted by a simple priority list depending on their type. When their type priority is identical, they're sub-sorted by name.
    // tg: code/controllers/subsystem/points_of_interest.dm:228
    public override object compare_to(datum_point_of_interest rhs = null)
    {
        float sort_difference = Dm.num(get_type_sort_priority()) - Dm.num(((datum_point_of_interest_mob_poi)rhs).get_type_sort_priority());

        // If they're equal in priority, call parent to sort by name.
        if (sort_difference == 0)
            return base.compare_to(rhs);
        // Else sort by priority.
        else
            return sort_difference;
    }

    /// Priority list broadly stolen from /proc/sortmobs(). Lower numbers are higher priorities when sorted and appear closer to the top or start of lists.
    // tg: code/controllers/subsystem/points_of_interest.dm:239
    public virtual object get_type_sort_priority()
    {
        if (isAI(target))
            return 0;
        if (iseyemob(target))
            return 1;
        if (ispAI(target))
            return 2;
        if (iscyborg(target))
            return 3;
        if (ishuman(target))
            return 4;
        if (isbrain(target))
            return 5;
        if (isalien(target))
            return 6;
        if (isobserver(target))
            return 7;
        if (isnewplayer(target))
            return 8;
        if (isslime(target))
            return 9;
        if (isanimal(target))
            return 10;
        if (isbasicmob(target))
            return 11;
        return 12;
    }
}
