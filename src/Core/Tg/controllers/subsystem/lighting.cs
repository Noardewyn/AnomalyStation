// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/lighting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lumcount. The var block is generated (D111); this part adds the statics and the procs. stat_entry and Recover are
// NotTranslated (rules 13); not here: the VISUALIZE_LIGHT_UPDATES vars (a debug define, off). DEVIATION(DV85): no lighting objects exist, so
// objects_queue and fire's third phase (the objects' visual update) are not translated (docs/ai/specs/not-translated.md
// section 42).

// tg: code/controllers/subsystem/lighting.dm:1 SUBSYSTEM_DEF(lighting)
public partial class datum_controller_subsystem_lighting : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/lighting.dm:9  var/static/list/sources_queue = list() // List of lighting sources queued for update.
    public static ref DmList sources_queue => ref TgState.Current.datum_controller_subsystem_lighting__sources_queue;
    // tg: code/controllers/subsystem/lighting.dm:10  var/static/list/corners_queue = list() // List of lighting corners queued for update.
    public static ref DmList corners_queue => ref TgState.Current.datum_controller_subsystem_lighting__corners_queue;
    // tg: code/controllers/subsystem/lighting.dm:12  var/static/list/current_sources = list()
    public static ref DmList current_sources => ref TgState.Current.datum_controller_subsystem_lighting__current_sources;

    // The New() that SUBSYSTEM_DEF(lighting) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSlighting)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/lighting.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSlighting != this)
        {
            if (SSlighting is datum_controller_subsystem_lighting)
            {
                Recover();
                qdel(SSlighting);
            }
            SSlighting = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/lighting.dm:18
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/subsystem/lighting.dm:18", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/lighting.dm:23
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!Dm.truthy(initialized))
        {
            create_all_lighting_objects();
            initialized = TRUE;
        }

        await fire(FALSE, TRUE);

        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/lighting.dm:33
    public virtual void create_all_lighting_objects()
    {
        foreach (area area in Dm.each<area>(GLOB.areas))
        {
            if (!Dm.truthy(area.static_lighting))
                continue;
            foreach (DmList zlevel_turfs in Dm.each<DmList>(area.get_zlevel_turf_lists()))
            {
                foreach (turf area_turf in Dm.each<turf>(zlevel_turfs))
                {
                    if (Dm.truthy(area_turf.space_lit))
                        continue;
                    area_turf.dm_lighting_object_new(); // new /atom/movable/lighting_object(null, area_turf) (DEVIATION DV85)
                }
                CHECK_TICK();
            }
            CHECK_TICK();
        }
    }

    // tg: code/controllers/subsystem/lighting.dm:45
    [DmParams("resumed", "init_tick_checks")]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        bool init_tick_checks = Dm.truthy(Dm.arg<object>(__extra, 0));
        // MC_SPLIT_TICK_INIT(3) (__DEFINES/MC.dm:3), inline (rules 11.3)
        float original_tick_limit = datum_controller_master.current_ticklimit;
        float split_tick_phases = 3;
        if (!init_tick_checks)
            MC_SPLIT_TICK(original_tick_limit, ref split_tick_phases);

        if (!Dm.truthy(resumed))
        {
            current_sources = sources_queue;
            sources_queue = Dm.list();
        }

        // UPDATE SOURCE QUEUE
        int i = 0;
        // something something cache locally for sonic speed
        DmList queue = current_sources;
        while (i < queue.len) //we don't use for loop here because i cannot be changed during an iteration
        {
            i += 1;

            datum_light_source L = queue[i] as datum_light_source;
            L.update_corners();
            if (!QDELETED(L))
                L.needs_update = LIGHTING_NO_UPDATE;
            else
                i -= 1; // update_corners() has removed L from the list, move back so we don't overflow or skip the next element

            // We unroll TICK_CHECK here so we can clear out the queue to ensure any removals/additions when sleeping don't fuck us
            if (init_tick_checks)
            {
                if (!TICK_CHECK())
                    continue;
                queue.Cut(1, i + 1);
                i = 0;
                await stoplag();
            }
            else if (MC_TICK_CHECK())
                break;
        }
        if (i != 0)
        {
            queue.Cut(1, i + 1);
            i = 0;
        }

        if (!init_tick_checks)
            MC_SPLIT_TICK(original_tick_limit, ref split_tick_phases);

        // UPDATE CORNERS QUEUE
        queue = corners_queue;
        while (i < queue.len) //we don't use for loop here because i cannot be changed during an iteration
        {
            i += 1;

            datum_lighting_corner C = queue[i] as datum_lighting_corner;
            C.needs_update = FALSE; //update_objects() can call qdel if the corner is storing no data
            C.update_objects();

            // We unroll TICK_CHECK here so we can clear out the queue to ensure any removals/additions when sleeping don't fuck us
            if (init_tick_checks)
            {
                if (!TICK_CHECK())
                    continue;
                queue.Cut(1, i + 1);
                i = 0;
                await stoplag();
            }
            else if (MC_TICK_CHECK())
                break;
        }
        if (i != 0)
        {
            queue.Cut(1, i + 1);
            i = 0;
        }

        if (!init_tick_checks)
            MC_SPLIT_TICK(original_tick_limit, ref split_tick_phases);

        // tg: :109-131  UPDATE OBJECTS QUEUE: the lighting objects' visual update (DEVIATION DV85: no lighting objects)
    }

    // MC_TICK_CHECK (__DEFINES/MC.dm:1), inline (rules 11.3)
    private bool MC_TICK_CHECK() => Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0);

    // MC_SPLIT_TICK (__DEFINES/MC.dm:4-10), over the locals MC_SPLIT_TICK_INIT declared
    private static void MC_SPLIT_TICK(float original_tick_limit, ref float split_tick_phases)
    {
        if (split_tick_phases > 1)
        {
            datum_controller_master.current_ticklimit = ((original_tick_limit - TICK_USAGE()) / split_tick_phases) + TICK_USAGE();
            --split_tick_phases;
        }
        else
            datum_controller_master.current_ticklimit = original_tick_limit;
    }

    // tg: code/controllers/subsystem/lighting.dm:133
    public override void Recover()
    {
        Dm.NotTranslated("code/controllers/subsystem/lighting.dm:133", "Recover: the Master's recovery is not translated (translation-rules.md 13)");
    }

    /// Takes a list of turfs in, and sets up static lighting for them as needed.
    /// Exactly what it says on the tin.
    // tg: code/controllers/subsystem/lighting.dm:139
    public virtual void setup_static_lighting_if_needed(DmList turfs = null)
    {
        foreach (turf unlit in Dm.each<turf>(turfs))
        {
            if (Dm.truthy(unlit.space_lit))
                continue;
            area loc_area = unlit.loc as area;
            if (Dm.truthy(loc_area.static_lighting))
                unlit.lighting_build_overlay();
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/lighting.dm:1  SUBSYSTEM_DEF(lighting): GLOBAL_REAL(SSlighting, /datum/controller/subsystem/lighting)
    internal datum_controller_subsystem_lighting SSlighting;
    // tg: code/controllers/subsystem/lighting.dm:9-12  the statics (rules 10.6)
    internal DmList datum_controller_subsystem_lighting__sources_queue = Dm.list();
    internal DmList datum_controller_subsystem_lighting__corners_queue = Dm.list();
    internal DmList datum_controller_subsystem_lighting__current_sources = Dm.list();
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_lighting SSlighting => ref TgState.Current.SSlighting;
}
