// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/tgs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the TGS (tgstation-server) API is not ported (translation-rules.md 13: TGS wrappers get a seam); only the
// /world procs of it that M0 calls exist, as no-op seams (W3.0 contracts): TgsInitializationComplete, which the Master
// Controller calls at the end of its Initialize (controllers/master.dm:470), and TgsAvailable, which world/SetupLogs reads
// (game/world.dm:268). Owned by the boot package (C). world/InitTgs, which would call TgsNew, is itself a no-op seam
// (game/world.cs). docs/ai/specs/not-translated.md lists them.

public static partial class GlobalProcs
{
    /**
     * Consumers MUST call this when world initializations are complete and the game is ready to play before any player interactions happen.
     *
     * This may use [/world/var/sleep_offline] to make this happen so ensure no changes are made to it while this call is running.
     * Afterwards, consider explicitly setting it to what you want to avoid this BYOND bug: http://www.byond.com/forum/post/2575184
     * This function should not be called before ..() in [/world/proc/New].
     */
    // tg: code/__DEFINES/tgs.dm:178 (the TGS API's body: modules/tgs/core/core.dm:76)
    // PORT: TGS is not ported (rules 13); a no-op seam.
    public static DmTask TgsInitializationComplete(this DmWorld world) => DmTask.CompletedTask;

    /**
     * Returns [TRUE] if DreamDaemon was launched under TGS, the API matches, and was properly initialized. [FALSE] will be returned otherwise.
     */
    // tg: code/__DEFINES/tgs.dm:483
    // PORT: TGS is not ported (rules 13); a seam that returns what the API's own declaration returns (tgs.dm:485, null): the
    // world never runs under TGS.
    public static object TgsAvailable(this DmWorld world) => null;
}
