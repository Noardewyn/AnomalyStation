// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/levels.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only is_valid_z_level (M3 R4: the gravity generator's shake_everyone) and get_linked_z_levels_in_range (syndicate N1c: a
// navigation computer's eye crossing to a linked level) and get_linked_z_angle (silicons SI2: the GPS); the planet and the other level helpers come with their users.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/levels.dm:13
    public static object is_valid_z_level(turf source_loc = null, turf checking_loc = null)
    {
        // DEVIATION(DV362): a null turf (an atom in nullspace, such as the character preview's dummy) is on no level; tg reads null.z and runtimes
        if (source_loc == null || checking_loc == null)
            return FALSE;
        // if we're both on "station", regardless of multi-z, we'll pass by.
        if (Dm.truthy(is_station_level(source_loc.z)) && Dm.truthy(is_station_level(checking_loc.z)))
            return TRUE;
        if (Dm.eq(source_loc.z, checking_loc.z))
            return TRUE;
        return FALSE;
    }

    /**
     * Gets all connected z-levels within a given manhattan distance of center.
     *
     * Arguments:
     * * center: The starting Z level. Can either be a numeric z-level, or a [/datun/space_level].
     * * dist: The maximum distance to search.
     */
    // tg: code/__HELPERS/levels.dm:75
    public static object get_linked_z_angle(object start = null, object end = null)
    {
        if (Dm.isnum(start))
            start = SSmapping.get_level(start);
        if (Dm.isnum(end))
            end = SSmapping.get_level(end);
        datum_space_level start_level = (datum_space_level)start;
        datum_space_level end_level = (datum_space_level)end;
        // Check the neighbors first, and return the appropiate angle if it is a neighbor.
        foreach (object direction in Dm.each(start_level.neigbours))
        {
            datum_space_level neighbor = start_level.neigbours[direction] as datum_space_level;
            if (neighbor == end_level)
            {
                object angle = GLOB.cardinal_angles[direction];
                if (angle != null)
                    return angle;
            }
        }
        // Otherwise, if they're both crosslinked, calculate the angle using their grid coordinates.
        if (Dm.eq(start_level.linkage, CROSSLINKED) && Dm.eq(end_level.linkage, CROSSLINKED))
        {
            float dx = Dm.num(end_level.xi) - Dm.num(start_level.xi);
            float dy = Dm.num(end_level.yi) - Dm.num(start_level.yi);
            return round(Dm.num(delta_to_angle(dy, dx)));
        }
        return null;
    }

    // tg: code/__HELPERS/levels.dm:101
    public static DmList get_linked_z_levels_in_range(object center = null, float dist = 0)
    {
        if (Dm.isnum(center))
            center = SSmapping.get_level(center);
        DmList to_check = Dm.list(center);
        DmList @checked = Dm.list();
        float total_search_distance = 0;
        while (Dm.truthy(to_check.len) && total_search_distance <= dist)
        {
            DmList current_pass = to_check.Copy();
            to_check.Cut();
            foreach (datum_space_level level in Dm.each<datum_space_level>(current_pass))
            {
                @checked[level] = TRUE;
                foreach (object direction in Dm.each(level.neigbours))
                {
                    var neighbor = level.neigbours[direction] as datum_space_level;
                    if (!Dm.truthy(@checked[neighbor]))
                        Dm.combine(ref to_check, neighbor);
                }
            }
            total_search_distance++;
        }
        return @checked;
    }
}
