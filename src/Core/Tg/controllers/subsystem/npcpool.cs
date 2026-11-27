// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/npcpool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but stat_entry (the MC panel, rules 13). The var block is generated.

// tg: code/controllers/subsystem/npcpool.dm:1 SUBSYSTEM_DEF(npcpool)
public partial class datum_controller_subsystem_npcpool : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(npcpool) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/npcpool.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSnpcpool != this)
        {
            if (SSnpcpool is datum_controller_subsystem_npcpool)
            {
                Recover();
                qdel(SSnpcpool);
            }
            SSnpcpool = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/npcpool.dm:9
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/npcpool.dm:9", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/npcpool.dm:14
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        if (!Dm.truthy(resumed))
        {
            DmList activelist = GLOB.simple_animals[AI_ON] as DmList;
            this.currentrun = activelist.Copy();
        }

        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;

        while (Dm.truthy(currentrun.len))
        {
            mob_living_simple_animal SA = currentrun[currentrun.len] as mob_living_simple_animal;
            --currentrun.len;

            if (QDELETED(SA)) // Some issue causes nulls to get into this list some times. This keeps it running, but the bug is still there.
            {
                ((DmList)GLOB.simple_animals[AI_ON]).Remove(SA);
                stack_trace(Dm.text("Found a null in simple_animals active list []!", SA?.type), "code/controllers/subsystem/npcpool.dm", 30);
                continue;
            }

            if (!Dm.truthy(SA.ckey) && !HAS_TRAIT(SA, TRAIT_NO_TRANSFORM))
            {
                // each call is an isolation boundary: a runtime ends that proc only, as in DM (translation-rules.md 9.2)
                if (SA.stat != DEAD)
                {
                    try { SA.handle_automated_movement(); }
                    catch (Exception e) { World.Current.ReportRuntime(e); }
                }
                if (SA.stat != DEAD)
                {
                    try { SA.handle_automated_action(); }
                    catch (Exception e) { World.Current.ReportRuntime(e); }
                }
                if (SA.stat != DEAD)
                {
                    try { SA.handle_automated_speech(); }
                    catch (Exception e) { World.Current.ReportRuntime(e); }
                }
            }
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/npcpool.dm:1  SUBSYSTEM_DEF(npcpool): GLOBAL_REAL(SSnpcpool, /datum/controller/subsystem/npcpool)
    internal datum_controller_subsystem_npcpool SSnpcpool;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_npcpool SSnpcpool => ref TgState.Current.SSnpcpool;
}
