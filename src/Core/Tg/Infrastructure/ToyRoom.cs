using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// <summary>
/// The toy room (wave 4, W4.M; docs/ai/specs/m0-reading and the wave-4 scope section 7, "map without the loader"): the map of
/// the toy world and of the integration tests, built from tg's real types in the current world, with no map file. It is host
/// code, not tg code: BYOND's map loader (<c>code/modules/mapping/reader.dm</c>) is not ported before M3, and this is what
/// stands in for it. It runs as the <c>compiledMap</c> step of <see cref="TgWorld.Boot"/> (before <c>world/New</c>, so every
/// atom it makes waits for SSatoms, which the real Master initializes at tick 20) and in a test world that does not boot
/// (whose host initializes SSatoms itself afterwards).
/// <para>
/// What <see cref="Build"/> makes, in tg's own order of events: <c>world.turf</c> becomes <c>/turf/open/indestructible</c>
/// before the map grows (<c>world.maxx</c>, <c>maxy</c> and <c>maxz</c> fill every new cell with a <c>world.turf</c> in the
/// <c>world.area</c> instance, x fastest); the rim of every level is replaced by <c>/turf/closed/indestructible</c> walls
/// (<c>new /turf/closed/indestructible(locate(x, y, z))</c> is a new object: the replaced turf is dead, so the replacing
/// comes first and the area lists are written after, scope R13); SSmapping's per-z lists are grown to <c>world.maxz</c> as
/// <c>manage_z_level</c> does (mapping.dm:756-768: <c>gravity_by_z_level</c>, <c>z_level_to_plane_offset</c>,
/// <c>z_level_to_lowest_plane_offset</c>) and the world area's <c>turfs_by_zlevel</c> is filled as <c>build_area_turfs</c>
/// does (:776-781, the space-guaranteed branch); then one <c>/area/misc/testroom</c> is created with a null location and tg's
/// own <c>turf/change_area</c> moves every turf of the room into it (what the map reader does, reader.dm:929-937), walls
/// included, so <c>GLOB.areas</c>, <c>areas_by_type</c> and the areas' <c>turfs_by_zlevel</c> stay consistent. The room has
/// gravity (<c>/area/misc/testroom</c> is <c>STANDARD_GRAVITY</c>, unlike <c>/area/space</c>, tg's <c>world.area</c>).
/// </para>
/// <para>
/// Geometry (the same on every level): the rim is wall, the floor is the rest. The floor's bottom left corner is
/// (2, 2) and its top right corner is (<c>maxx</c> - 1, <c>maxy</c> - 1): <see cref="FloorBottomLeft"/> and
/// <see cref="FloorTopRight"/> give tg's <c>run_loc_floor_bottom_left</c> and <c>run_loc_floor_top_right</c> (unit_test.dm:61-62)
/// to the ported unit tests. The levels are separate: SSmapping's <c>multiz_levels</c> gets one entry per level with no link up
/// or down (what <c>generate_linkages_for_z_level</c>, mapping.dm:226, makes for a level without the UP and DOWN traits; the
/// atmos adjacency reads it), so no turf has a turf above or below; linked levels come with the M3 map loader.
/// </para>
/// <para>
/// What the world must already have (a booted world has all of it when <c>compiledMap</c> runs; a test world makes it first):
/// the map grid attached or attachable (<c>MapGrid</c>: a world whose size was never set), <c>GLOB</c> with its
/// <c>InitGlobal</c> procs run, <c>SSmapping</c>, and the logger and SSgarbage that creating atoms and areas reach. It never
/// initializes SSatoms, creates a mob or starts a subsystem.
/// </para>
/// </summary>
public static class ToyRoom
{
    /// <summary>The smallest room: a rim and one floor tile.</summary>
    public const int MinSize = 3;

    /// <summary>The default width and height of the room, rim included.</summary>
    public const int DefaultSize = 7;

    /// <summary>
    /// Builds the toy room of <paramref name="width"/> x <paramref name="height"/> turfs (rim included) on
    /// <paramref name="levels"/> levels in the current world and returns its <c>/area/misc/testroom</c>. The world's map must
    /// still be empty (<c>world.maxx</c> is 0): the room is the whole map.
    /// </summary>
    public static area_misc_testroom Build(int width = DefaultSize, int height = DefaultSize, int levels = 1)
    {
        if (width < MinSize || height < MinSize || levels < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "the toy room needs a rim and at least one floor tile (3 x 3) on at least one level");
        World world = World.Current;
        if (Dm.world.maxx != 0 || Dm.world.maxy != 0 || Dm.world.maxz != 0)
            throw new InvalidOperationException("the toy room is the whole map: the world's map must be empty (maxx, maxy and maxz are 0)");
        if (GLOB__var == null)
            throw new InvalidOperationException("the toy room needs GLOB (its areas register in GLOB.areas)");
        if (SSmapping == null)
            throw new InvalidOperationException("the toy room needs SSmapping (its per-z lists are grown to world.maxz)");

        // The map hook first (a boot attaches it already), so that every turf is created at the assignment that makes its cell.
        MapGrid.Attach(world);

        // R13: world.turf is the floor type before the map grows, so no floor is ever replaced.
        Dm.world.turf = typeof(turf_open_indestructible);
        Dm.world.maxx = width;
        Dm.world.maxy = height;
        // The levels are the station's (tg loads them through SSmapping's add_new_zlevel, which grows the map with
        // world.incrementMaxZ() and so SSmobs' per-z lists, game/world.dm:491-494), one level at a time.
        while (Dm.world.maxz < levels)
            Dm.world.incrementMaxZ();

        // mapping.dm:756-768 (manage_z_level): the per-z lists the code reads, grown to world.maxz.
        SSmapping.gravity_by_z_level.len = levels;
        SSmapping.z_level_to_plane_offset.len = levels;
        SSmapping.z_level_to_lowest_plane_offset.len = levels;
        for (int z = 1; z <= levels; z++)
        {
            SSmapping.z_level_to_plane_offset[(float)z] = 0;
            SSmapping.z_level_to_lowest_plane_offset[(float)z] = 0;
        }
        LinkLevels(levels);

        // The walls replace the rim, silently (BYOND: new /turf at an occupied cell replaces the turf).
        for (int z = 1; z <= levels; z++)
        {
            for (int y = 1; y <= height; y++)
            {
                for (int x = 1; x <= width; x++)
                {
                    if (x == 1 || y == 1 || x == width || y == height)
                        Dm.@new<turf_closed_indestructible>(locate(x, y, z));
                }
            }
        }

        // mapping.dm:776-781 (build_area_turfs, the space-guaranteed branch), after the turfs are final.
        var world_area = (area)locate(1, 1, 1).loc;
        world_area.turfs_by_zlevel.len = levels;
        for (int z = 1; z <= levels; z++)
            world_area.turfs_by_zlevel[(float)z] = block(1, 1, z, width, height, z);

        // reader.dm:929-937: a new area with a null location, then change_area per turf.
        area_misc_testroom room = Dm.@new<area_misc_testroom>();
        for (int z = 1; z <= levels; z++)
        {
            foreach (turf T in Dm.each<turf>(block(1, 1, z, width, height, z)))
                T.change_area((area)T.loc, room);
        }
        return room;
    }

    /// <summary>
    /// SSmapping's <c>multiz_levels</c> for levels 1 to <paramref name="levels"/> without the UP and DOWN traits: what tg's
    /// <c>generate_linkages_for_z_level</c> (mapping.dm:226-238) makes for each level it adds, and what a host-built map
    /// (this room, the server's test room, a test's own builder) makes as tg would; the atmos adjacency reads it
    /// (LINDA_system.dm:69).
    /// </summary>
    public static void LinkLevels(int levels)
    {
        if (SSmapping.multiz_levels.len < levels)
            SSmapping.multiz_levels.len = levels;
        for (int z = 1; z <= levels; z++)
        {
            DmList links = Dm.list_n(Defines.LARGEST_Z_LEVEL_INDEX);
            links[Defines.Z_LEVEL_UP] = Dm.FALSE;
            links[Defines.Z_LEVEL_DOWN] = Dm.FALSE;
            SSmapping.multiz_levels[(float)z] = links;
        }
    }

    /// <summary>tg's <c>run_loc_floor_bottom_left</c> (unit_test.dm:61): the floor turf at (2, 2) of level <paramref name="z"/>.</summary>
    public static turf FloorBottomLeft(float z = 1) => locate(2, 2, z);

    /// <summary>tg's <c>run_loc_floor_top_right</c> (unit_test.dm:62): the floor turf at (maxx - 1, maxy - 1) of level <paramref name="z"/>.</summary>
    public static turf FloorTopRight(float z = 1) => locate(Dm.world.maxx - 1, Dm.world.maxy - 1, z);
}
