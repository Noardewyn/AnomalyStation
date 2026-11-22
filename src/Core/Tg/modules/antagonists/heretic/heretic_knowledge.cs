// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/heretic/heretic_knowledge.dm
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

// tg: code/modules/antagonists/heretic/heretic_knowledge.dm:11
[DmPath("/datum/heretic_knowledge")]
public partial class datum_heretic_knowledge : datum
{
    // tg: code/modules/antagonists/heretic/heretic_knowledge.dm:36
    public float priority = 0;
}
