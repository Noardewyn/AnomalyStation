// Translated from tgstation (snapshot 1d99bd7f): code/datums/ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed in wave 3 or later (with its module).
// Only the members that the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm, sorts/) name;
// bodies are Dm.NotTranslated until then.

// tg: code/datums/ruins.dm:1
[DmPath("/datum/map_template/ruin")]
public partial class datum_map_template_ruin : datum_map_template
{
    // tg: code/datums/ruins.dm:16
    public float cost = 0;
}
