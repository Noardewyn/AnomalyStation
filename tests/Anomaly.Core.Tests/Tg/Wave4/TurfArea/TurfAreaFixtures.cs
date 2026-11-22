using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

// Fixtures of package B's smoke tests (wave 4: turf and area). Fixture paths are /<kind>/test_turfarea_<name>
// (translation-rules.md 15), which no tg path starts with.

/// <summary>
/// A world as tg's world start leaves it for the map: GLOB with its InitGlobal procs, the logger, SSchat and the
/// configuration (TgTestGenesis), SSgarbage, SSdcs, SSmapping (with its per-z gravity list grown, as manage_z_level does),
/// SSobj, SSair and an uninitialized SSatoms, so that the map's atoms wait for <c>SSatoms.Initialize</c>. The map grid is attached
/// first; <c>world.turf</c> and <c>world.area</c> are set, then the map size.
/// </summary>
internal sealed class TurfAreaWorld
{
    public readonly World World;
    public readonly List<Exception> Runtimes = new();

    public TurfAreaWorld(int maxx, int maxy, int maxz, Type turfType = null, Type areaType = null, ulong seed = 11)
    {
        World = new World(seed) { RuntimeErrorSink = Runtimes.Add };
        MapGrid.Attach(World);
        Tools.GeneticsStrings.Mount(World); // gaps P3: setupGenetics' speech mutations read them
        using (World.Enter())
        {
            GLOB__var = new datum_controller_global_vars(); // the C# constructor: GLOB's New queues a timer (globals.dm:24)
            Dm.@new<datum_controller_subsystem_materials>(); // M5 slice 2: GLOB's make_datum_reference_lists reads SSmaterials' recipes (global_lists.dm:93)
            Dm.@new<datum_controller_subsystem_accessories>(); // M10x slice D2: tg makes it at PreInit, before GLOB (sprite_accessories.dm:47); bodies read its lists
            Dm.@new<datum_controller_subsystem_garbage>(); // M5 slice 8: before GLOB, as the Master makes it (master.cs:186-198): init_crafting_recipes qdels the recipes out of scope
            Waiting.NoSleep(GLOB.Initialize());
            TgTestGenesis.Install();
            Dm.@new<datum_controller_subsystem_processing_dcs>();
            Dm.@new<datum_controller_subsystem_mapping>();
            Dm.@new<datum_controller_subsystem_ore_generation>(); // a random rock that rolls an ore counts it there (minerals.dm:490), tg's MC always has it
            Dm.@new<datum_controller_subsystem_icon_smooth>();
            Dm.@new<datum_controller_subsystem_processing_obj>(); // /area/Destroy stops the area processing on it (areas.dm:411)
            Dm.@new<datum_controller_subsystem_air>(); // a turf's Destroy and a space turf's Initialize call into it
            Dm.@new<datum_controller_subsystem_atoms>();
            Dm.@new<datum_controller_subsystem_timer>(); // the slippery fake ice's wet floor queues its gc (wet_floor.dm:36)
            SSmapping.gravity_by_z_level.len = maxz;
            ToyRoom.LinkLevels(maxz);
            Dm.world.turf = turfType ?? typeof(turf_open_indestructible);
            Dm.world.area = areaType ?? typeof(area_misc_testroom);
            Dm.world.maxx = maxx;
            Dm.world.maxy = maxy;
            Dm.world.maxz = maxz;
        }
    }

    /// <summary>Runs SSatoms' Initialize as the Master's init_subsystem would.</summary>
    public static object InitializeSSatoms() => Waiting.NoSleep(SSatoms.Initialize());
}

/// <summary>A movable that records the Bump calls tg's /turf/Enter makes (A's /atom/movable/Bump is not needed here).</summary>
[DmPath("/obj/test_turfarea_mover")]
public class obj_test_turfarea_mover : obj
{
    [NotHashed] public readonly List<atom> bumped = new();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> Bump(atom bumped_atom = null)
    {
        bumped.Add(bumped_atom);
        return null;
    }
}

/// <summary>An occupant whose Cross refuses every mover (tg's /atom/movable/Cross answers through CanPass, package A).</summary>
[DmPath("/obj/test_turfarea_blocker")]
public class obj_test_turfarea_blocker : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override DmTask<object> Cross(atom_movable O = null) => DmTask<object>.FromResult(Dm.FALSE);
}
