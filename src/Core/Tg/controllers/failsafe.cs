// Translated from tgstation (snapshot 1d99bd7f): code/controllers/failsafe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: the Failsafe is not ported (translation-rules.md 13; docs/ai/DEVIATIONS.md DV10). Owned by the mc package (A).
// Master.Loop creates one when there is none or when it stopped poking (controllers/master.dm:701-702:
// `if (!Failsafe || (Failsafe.processing_interval > 0 && (Failsafe.lasttick + (Failsafe.processing_interval * 5)) < world.time))`),
// so the skeleton is exactly what keeps Loop from creating another one every iteration (lead decision W3 G4, risk R11):
// New only registers itself as Failsafe, and processing_interval is 0. Every proc is Dm.NotTranslated (W3.0 contracts).

/**
 * Failsafe
 *
 * Pretty much pokes the MC to make sure it's still alive.
 **/

// See initialization order in /code/game/world.dm
// tg: code/controllers/failsafe.dm:8  GLOBAL_REAL(Failsafe, /datum/controller/failsafe)
public sealed partial class TgState
{
    internal datum_controller_failsafe Failsafe;
}

public static partial class GlobalProcs
{
    /// <summary>tg <c>Failsafe</c> (rules 10.6): the Failsafe of this world, a skeleton that never runs (DV10).</summary>
    public static ref datum_controller_failsafe Failsafe => ref TgState.Current.Failsafe;
}

// tg: code/controllers/failsafe.dm:10
[DmPath("/datum/controller/failsafe")]
public partial class datum_controller_failsafe : datum_controller // This thing pretty much just keeps poking the master controller
{
    // tg: code/controllers/failsafe.dm:11
    public datum_controller_failsafe()
    {
        name = "Failsafe";
    }

    // The length of time to check on the MC (in deciseconds).
    // Set to 0 to disable.
    // tg: code/controllers/failsafe.dm:15
    // DEVIATION(DV10): 0 instead of 20. The Failsafe is not ported; with the interval 0 the Master's Loop never finds it
    // overdue and never replaces it (controllers/master.dm:701-702).
    public float processing_interval = 0;
    // The alert level. For every failed poke, we drop a DEFCON level. Once we hit DEFCON 1, restart the MC.
    // tg: code/controllers/failsafe.dm:17
    public float defcon = 5;
    //the world.time of the last check, so the mc can restart US if we hang.
    // (Real friends look out for *each other*)
    // tg: code/controllers/failsafe.dm:20
    public float lasttick = 0;

    // Track the MC iteration to make sure its still on track.
    // tg: code/controllers/failsafe.dm:23
    public float master_iteration = 0;
    // tg: code/controllers/failsafe.dm:24
    public float running = TRUE;

    // tg: code/controllers/failsafe.dm:26
    public override async DmTask New(params object[] __extra)
    {
        // DEVIATION(DV10): the Failsafe is not ported. tg's New clears usr, deletes an older Failsafe, registers itself and
        // starts its Initialize loop, which pokes the Master and recreates it when it hangs; the port only registers
        // itself, so the Master's Loop sees a Failsafe and leaves it alone.
        Failsafe = this;
    }

    // tg: code/controllers/failsafe.dm:37  (set waitfor = FALSE)
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:37", "Initialize: the Failsafe is not ported (translation-rules.md 13, DV10)");
        return null;
    }

    // tg: code/controllers/failsafe.dm:59
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:59", "Destroy: the Failsafe is not ported (translation-rules.md 13, DV10)");
        return null;
    }

    // tg: code/controllers/failsafe.dm:64
    public virtual async DmTask Loop()
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:64", "Loop: the Failsafe is not ported (translation-rules.md 13, DV10)");
    }

    //Emergency loop used when Master got deleted or the main loop exited while Defcon == 0
    //Loop is driven externally so runtimes only cancel the current recovery attempt
    // tg: code/controllers/failsafe.dm:131
    public virtual async DmTask<object> emergency_loop()
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:131", "emergency_loop: the Failsafe is not ported (translation-rules.md 13, DV10)");
        return null;
    }

    // tg: code/controllers/failsafe.dm:180
    public virtual object defcon_pretty()
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:180", "defcon_pretty: the Failsafe is not ported (translation-rules.md 13, DV10)");
        return null;
    }

    // tg: code/controllers/failsafe.dm:183
    public override object stat_entry(object msg = null)
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:183", "stat_entry: stat panels are not translated (translation-rules.md 13)");
        return null;
    }
}

public static partial class GlobalProcs
{
    ///Recreate all SSs which will still cause data survive due to Recover(), the new Master will then find and take them from global.vars
    // tg: code/controllers/failsafe.dm:152
    public static async DmTask<object> recover_all_SS_and_recreate_master()
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:152", "recover_all_SS_and_recreate_master: MC recovery is not ported (translation-rules.md 13, DV10)");
        return null;
    }

    ///Delete all existing SS to basically start over
    // tg: code/controllers/failsafe.dm:167
    public static async DmTask<object> delete_all_SS_and_recreate_master()
    {
        Dm.NotTranslated("code/controllers/failsafe.dm:167", "delete_all_SS_and_recreate_master: MC recovery is not ported (translation-rules.md 13, DV10)");
        return null;
    }
}
