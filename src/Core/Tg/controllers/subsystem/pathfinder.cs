// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/pathfinder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The JPS queue (gaps package C1, the AI). Left as rows: the path maps (swarmed_pathfind, build_map and their cache, sssp.dm), which only tg's admin path debugger reaches at this snapshot, and the
// debug arrows (render_path_*, admin tooling).

/// Queues and manages JPS pathfinding steps
// tg: code/controllers/subsystem/pathfinder.dm:2 SUBSYSTEM_DEF(pathfinder)
public partial class datum_controller_subsystem_pathfinder : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/pathfinder.dm:14  var/static space_type_cache (rules 10.6: a TgState field)
    public static ref DmList space_type_cache => ref TgState.Current.datum_controller_subsystem_pathfinder__space_type_cache;

    // The New() that SUBSYSTEM_DEF(pathfinder) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/pathfinder.dm:2
    public override async DmTask New(params object[] __extra)
    {
        if (SSpathfinder != this)
        {
            if (SSpathfinder is datum_controller_subsystem_pathfinder)
            {
                Recover();
                qdel(SSpathfinder);
            }
            SSpathfinder = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/pathfinder.dm:16
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        space_type_cache = typecacheof(typeof(turf_open_space)) as DmList;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/pathfinder.dm:20
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/pathfinder.dm:20", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // This is another one of those subsystems (hey lighting) in which one "Run" means fully processing a queue
    // We'll use a copy for this just to be nice to people reading the mc panel
    // tg: code/controllers/subsystem/pathfinder.dm:26
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            this.currentrun = active_pathing.Copy();
            this.currentmaps = deep_copy_list(source_to_maps) as DmList;
        }

        // Dies of sonic speed from caching datum var reads
        DmList currentrun = this.currentrun;
        while (Dm.truthy(length(currentrun)))
        {
            datum_pathfind path = currentrun[length(currentrun)] as datum_pathfind;
            if (!Dm.truthy(await path.search_step())) // Something's wrong
            {
                await path.early_exit();
                continue;
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
            await path.finished();
        }

        // Go over our existing pathmaps, clear out the ones we aren't using
        DmList currentmaps = this.currentmaps;
        float oldest_time = world.time - MAP_REUSE_SLOWEST;
        while (Dm.truthy(length(currentmaps)))
        {
            turf source = currentmaps[length(currentmaps)] as turf;
            DmList owned_maps = currentmaps[source] as DmList;
            foreach (datum_path_map map in Dm.each_cast<datum_path_map>(owned_maps))
            {
                if (Dm.num(map.creation_time) < oldest_time && !Dm.truthy(map.building))
                    Dm.remove_at(source_to_maps, source, map);
                owned_maps.len--;
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
            }
            if (!Dm.truthy(length(source_to_maps[source])))
                Dm.remove(ref source_to_maps, source);

            currentmaps.len--;
        }
    }

    /// Initiates a pathfind. Returns true if we're good, FALSE if something's failed
    // tg: code/controllers/subsystem/pathfinder.dm:60
    public virtual object pathfind(atom_movable requester = null, atom end = null, float? max_distance = null, object mintargetdist = null, object access = null, float? simulated_only = null, turf exclude = null, float? skip_first = null, float? diagonal_handling = null, DmList on_finish = null)
    {
        max_distance ??= 30;
        access ??= Dm.list();
        simulated_only ??= TRUE;
        skip_first ??= TRUE;
        diagonal_handling ??= DIAGONAL_REMOVE_CLUNKY;
        datum_pathfind_jps path = Dm.@new<datum_pathfind_jps>();
        path.setup(requester, access as DmList, max_distance, simulated_only, exclude, on_finish, end, mintargetdist, skip_first, diagonal_handling);
        if (Dm.truthy(path.start__proc()))
        {
            Dm.append(ref active_pathing, path);
            return TRUE;
        }
        return FALSE;
    }

    /// Begins to handle a pathfinding run based off the input /datum/pathfind datum
    /// You should not use this, it exists to allow for shenanigans. You do not know how to do shenanigans
    // tg: code/controllers/subsystem/pathfinder.dm:151
    public virtual object run_pathfind(datum_pathfind run = null)
    {
        Dm.append(ref active_pathing, run);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/pathfinder.dm:2  SUBSYSTEM_DEF(pathfinder): GLOBAL_REAL(SSpathfinder, /datum/controller/subsystem/pathfinder)
    internal datum_controller_subsystem_pathfinder SSpathfinder;
    // tg: code/controllers/subsystem/pathfinder.dm:14
    internal DmList datum_controller_subsystem_pathfinder__space_type_cache;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_pathfinder SSpathfinder => ref TgState.Current.SSpathfinder;
}
