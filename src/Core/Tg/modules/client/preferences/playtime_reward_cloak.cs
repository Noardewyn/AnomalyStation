// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/playtime_reward_cloak.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps SL1, D333): only /client/proc/is_veteran (the veteran's cloak's skill reward element reads it); the preference
// toggle of the roundstart cloak waits with the playtime reward of the job's outfit (not-translated.md 221).

// tg: code/modules/client/preferences/playtime_reward_cloak.dm:19
public partial class client : datum
{
    /// Returns whether the client should receive the gamer cloak
    // tg: code/modules/client/preferences/playtime_reward_cloak.dm:19
    public virtual object is_veteran()
    {
        return Dm.num(Dm.num(get_exp_living(pure_numeric: TRUE)) >= PLAYTIME_VETERAN);
    }
}
