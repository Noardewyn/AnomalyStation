// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/baseturfs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: ScrapeAway, the turf stack's way down. M3 P3: place_on_top, the way up, for the abandoned airlock helper
// (mapping_helpers.dm:265). M4c slice 5: depth_to_find_baseturf. M5x slice 1: baseturf_at_depth. M10 slice G2 (D272, the map
// templates and the shuttles' move code): the rest of the file.

// tg: code/game/turfs/baseturfs.dm:2
public partial class turf : atom
{
    // tg: code/game/turfs/baseturfs.dm:2
    public virtual async DmTask<object> ScrapeAway(float? amount = null, object flags = null)
    {
        amount ??= 1;
        if (!Dm.truthy(amount))
            return null;
        if (Dm.truthy(length(baseturfs)))
        {
            DmList new_baseturfs = ((DmList)baseturfs).Copy();
            object turf_type = new_baseturfs[Math.Max(1, new_baseturfs.len - amount.Value + 1)];
            while (ispath(turf_type, typeof(turf_baseturf_skipover)))
            {
                amount++;
                if (amount > new_baseturfs.len)
                    throw Dm.CRASH(Dm.text("The bottommost baseturf of a turf is a skipover []([])", this, type), "code/game/turfs/baseturfs.dm", 11);
                turf_type = new_baseturfs[Math.Max(1, new_baseturfs.len - amount.Value + 1)];
            }
            new_baseturfs.len -= Math.Min(amount.Value, new_baseturfs.len - 1); // No removing the very bottom
            if (new_baseturfs.len == 1)
                return await dm_change_turf_virtual(turf_type, new_baseturfs[1], flags);
            return await dm_change_turf_virtual(turf_type, new_baseturfs, flags);
        }

        if (Dm.eq(baseturfs, type))
            return this;

        return await dm_change_turf_virtual(baseturfs, baseturfs, flags); // The bottom baseturf will never go away
    }

    /// Places a turf at the top of the stack
    // tg: code/game/turfs/baseturfs.dm:31
    // Appendix A (M3 P3, decided): added_layer is Type, not slots.tsv's turf: tg's callers pass typepaths only (the wall
    // helper, openspace.dm:163-179, floor.dm:279, shuttle.dm:429), and /area/shuttle's place_on_top_react tests it with
    // ispath (areas/shuttles.dm:23).
    public virtual async DmTask<object> place_on_top(Type added_layer = null, object flags = null)
    {
        DmList new_baseturfs = Dm.list();

        new_baseturfs.Add(baseturfs);
        if (isopenturf(this))
            new_baseturfs.Add(type);
        area our_area = get_area(this) as area;
        flags = our_area.place_on_top_react(new_baseturfs, added_layer, flags);

        return await ChangeTurf(added_layer, new_baseturfs, flags);
    }

    /// Places the given turf on the bottom of the turf stack.
    // tg: code/game/turfs/baseturfs.dm:24
    public virtual async DmTask place_on_bottom(Type bottom_turf = null)
    {
        DmList stack = Dm.list(Dm.initial(bottom_turf, (turf p) => p.baseturfs), bottom_turf);
        Dm.append(ref stack, baseturfs);
        baseturfs = await baseturfs_string_list(stack, this);
    }

    /// Places a turf on top - for map loading
    // tg: code/game/turfs/baseturfs.dm:43
    public virtual async DmTask<object> load_on_top(Type added_layer = null, object flags = null)
    {
        area our_area = get_area(this) as area;
        flags = our_area.place_on_top_react(Dm.list(baseturfs), added_layer, flags);

        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_SKIP))) // We haven't been initialized
        {
            if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
                stack_trace(Dm.text("CHANGETURF_SKIP was used in a PlaceOnTop call for a turf that's initialized. This is a mistake. []([])", this, type), "code/game/turfs/baseturfs.dm", 49);
            await assemble_baseturfs();
        }

        turf new_turf;
        if (!Dm.truthy(length(baseturfs)))
            baseturfs = Dm.list(baseturfs);

        DmList old_baseturfs = ((DmList)baseturfs).Copy();
        if (!isclosedturf(this))
            Dm.append(ref old_baseturfs, type);

        new_turf = await ChangeTurf(added_layer, null, flags) as turf;
        await new_turf.assemble_baseturfs(Dm.initial(added_layer, (turf p) => p.baseturfs)); // The baseturfs list is created like roundstart
        // TURF-IDENTITY: tg's `list(baseturfs)` reads src, which BYOND has replaced in place by new_turf; the replaced turf's own
        // list here made a list inside the shuttles' baseturfs (gaps G3: a singularity eating a shuttle floor met it)
        if (!Dm.truthy(length(new_turf.baseturfs)))
            new_turf.baseturfs = Dm.list(new_turf.baseturfs);

        // The old baseturfs are put underneath, and we sort out the unwanted ones
        DmList stacked = old_baseturfs.Copy();
        Dm.append(ref stacked, Dm.sub(new_turf.baseturfs, GLOB.blacklisted_automated_baseturfs));
        new_turf.baseturfs = await baseturfs_string_list(stacked, new_turf);
        return new_turf;
    }

    // Copy an existing turf and put it on top
    // Returns the new turf
    // tg: code/game/turfs/baseturfs.dm:71
    public virtual async DmTask<object> CopyOnTop(turf copytarget = null, float? ignore_bottom = null, float? depth = null, float? copy_air = null, object flags = null)
    {
        ignore_bottom ??= 1;
        depth ??= float.PositiveInfinity;
        copy_air ??= FALSE;
        DmList new_baseturfs = Dm.list();
        Dm.append(ref new_baseturfs, baseturfs);
        Dm.append(ref new_baseturfs, type);

        if (Dm.truthy(depth))
        {
            DmList target_baseturfs = null;
            if (Dm.truthy(length(copytarget.baseturfs)))
            {
                // with default inputs this would be Copy(clamp(2, -INFINITY, baseturfs.len))
                // Don't forget a lower index is lower in the baseturfs stack, the bottom is baseturfs[1]
                var target_stack = (DmList)copytarget.baseturfs;
                target_baseturfs = target_stack.Copy(Math.Clamp(1 + ignore_bottom.Value, 1 + target_stack.len - depth.Value, target_stack.len));
            }
            else if (!Dm.truthy(ignore_bottom))
            {
                target_baseturfs = Dm.list(copytarget.baseturfs);
            }
            if (target_baseturfs != null)
            {
                Dm.remove(ref target_baseturfs, Dm.bitand(new_baseturfs, GLOB.blacklisted_automated_baseturfs));
                Dm.append(ref new_baseturfs, target_baseturfs);
            }
        }

        var new_turf = (turf)await copytarget.copyTurf(this, copy_air, flags);
        new_turf.baseturfs = await baseturfs_string_list(new_baseturfs, new_turf);
        return new_turf;
    }

    /// Similar to depth_to_find_baseturf, but directly returns the index of the found baseturf.
    /// This number can be passed into insert_baseturf to insert a baseturf before the found baseturf.
    // tg: code/game/turfs/baseturfs.dm:105
    public virtual object level_to_find_baseturf(object baseturf_type = null)
    {
        if (!islist(baseturfs))
            return Dm.eq(baseturfs, baseturf_type) ? 1 : null;
        float found = ((DmList)baseturfs).Find(baseturf_type);
        return found != 0 ? found : null;
    }

    /// Replaces all instances of needle_type in baseturfs with replacement_type
    // tg: code/game/turfs/baseturfs.dm:122
    public virtual async DmTask replace_baseturf(object needle_type = null, object replacement_type = null)
    {
        if (islist(baseturfs))
        {
            DmList new_baseturfs = ((DmList)baseturfs).Copy();

            for (float base_i = 1, base__end = length(new_baseturfs); base_i <= base__end; base_i++)
            {
                float found_index = new_baseturfs.Find(needle_type);
                if (found_index == 0)
                    break;

                new_baseturfs[found_index] = replacement_type;
            }

            if (Dm.truthy(length(new_baseturfs)))
                baseturfs = await baseturfs_string_list(new_baseturfs, this);
        }
        else if (Dm.eq(baseturfs, needle_type))
        {
            baseturfs = replacement_type;
        }
    }

    /// Removes all baseturfs that are found in the given typecache.
    // tg: code/game/turfs/baseturfs.dm:139
    public virtual async DmTask remove_baseturfs_from_typecache(DmList typecache = null)
    {
        if (islist(baseturfs))
        {
            DmList new_baseturfs = null;

            foreach (object baseturf in Dm.each((DmList)baseturfs))
            {
                if (!Dm.truthy(typecache[baseturf]))
                    continue;

                new_baseturfs ??= ((DmList)baseturfs).Copy();
                Dm.remove(ref new_baseturfs, baseturf);
            }

            if (new_baseturfs != null)
                baseturfs = await baseturfs_string_list(new_baseturfs, this);
        }
        else if (Dm.truthy(typecache[baseturfs]))
        {
            baseturfs = typeof(turf_baseturf_bottom);
        }
    }

    /// Returns the total number of baseturfs
    // tg: code/game/turfs/baseturfs.dm:156
    public virtual object count_baseturfs()
    {
        return islist(baseturfs) ? length(baseturfs) : 1;
    }

    /// Inserts a baseturf at the given level.
    /// "Level" here doesn't mean depth.
    /// For example, `insert_baseturf(2, /turf/open/floor/plating)` will make it so
    /// the 2nd to last turf in the list is plating.
    /// This is different from *depth*, since depth is the level from the top.
    // tg: code/game/turfs/baseturfs.dm:164
    public virtual async DmTask insert_baseturf(object level = null, object turf_type = null)
    {
        if (!islist(baseturfs))
        {
            await assemble_baseturfs();
            if (!islist(baseturfs))
                baseturfs = Dm.list(baseturfs);
        }

        DmList baseturfs_copy = ((DmList)baseturfs).Copy();
        baseturfs_copy.Insert(Dm.num(level), turf_type); // a null level is BYOND's Insert(0): the end
        baseturfs = await baseturfs_string_list(baseturfs_copy, this);
    }

    /// Places a baseturf ontop of a searched for baseturf.
    // tg: code/game/turfs/baseturfs.dm:175
    public virtual async DmTask stack_ontop_of_baseturf(object floor = null, object roof = null)
    {
        if (!islist(baseturfs))
            baseturfs = Dm.list(baseturfs);
        float floor_position = ((DmList)baseturfs).Find(floor);
        if (floor_position != 0)
            await insert_baseturf(floor_position + 1, roof);
    }

    /// Places a baseturf below a searched for baseturf.
    // tg: code/game/turfs/baseturfs.dm:183
    public virtual async DmTask stack_below_baseturf(object search_type = null, object stack_type = null)
    {
        if (!islist(baseturfs))
            baseturfs = Dm.list(baseturfs);
        float search_position = ((DmList)baseturfs).Find(search_type);
        if (search_position != 0)
            await insert_baseturf(search_position, stack_type);
        else if (Dm.eq(type, search_type))
            await insert_baseturf(0, stack_type); // insert_baseturf(turf_type = stack_type): the level null, BYOND's Insert at the end
    }

    /// Gets a copy of the baseturfs up to the specified depth
    // tg: code/game/turfs/baseturfs.dm:193
    public virtual object get_baseturfs_to_depth(object depth = null)
    {
        DmList baseturf_list = islist(baseturfs) ? (DmList)baseturfs : Dm.list(baseturfs);
        return baseturf_list.Copy(baseturf_list.len - Dm.num(depth) + 1);
    }

    /// Returns the baseturf at the given depth.
    /// For example, baseturf_at_depth(1) will give the baseturf that would show up when scraping once.
    // tg: code/game/turfs/baseturfs.dm:112
    public virtual object baseturf_at_depth(object index = null)
    {
        // TEST_ONLY_ASSERT(isnum(index), ...) (__DEFINES/unit_tests.dm): only in unit test builds (UNIT_TESTS), dropped
        if (islist(baseturfs))
            return LAZYACCESS(baseturfs as DmList, ((DmList)baseturfs).len - Dm.num(index) + 1);
        else if (Dm.eq(index, 1))
            return baseturfs;
        else
            return null;
    }

    /// Tries to find the given type in baseturfs.
    /// If found, returns how deep it is for use in other baseturf procs, or null if it cannot be found.
    /// For example, this number can be passed into ScrapeAway to scrape everything until that point.
    // tg: code/game/turfs/baseturfs.dm:95  (M4c slice 5: isshuttleturf, the airlock pump)
    public virtual object depth_to_find_baseturf(object baseturf_type = null)
    {
        if (!islist(baseturfs))
            return Dm.eq(baseturfs, baseturf_type) ? 1 : null;
        float index = ((DmList)baseturfs).Find(baseturf_type);
        if (index == 0)
            return null;
        return ((DmList)baseturfs).len - index + 1;
    }
}
