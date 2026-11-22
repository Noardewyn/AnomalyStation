// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/hypnotized/hypnotized.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps B1 (D327): the whole file, the hypnotized victim's antagonist datum (its objective and antag HUD; the hypnosis trauma makes
// it, brain_damage/hypnosis.cs). The var block is generated (D111).

/// Antag datum associated with the hypnosis brain trauma, used for displaying objectives and antag hud
// tg: code/modules/antagonists/hypnotized/hypnotized.dm:2
public partial class datum_antagonist_hypnotized : datum_antagonist
{
    // tg: code/modules/antagonists/hypnotized/hypnotized.dm:17
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref trauma);
        return base.Destroy(force);
    }
}
