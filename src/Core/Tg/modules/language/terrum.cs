// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/terrum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps L2 (D327): the procs of the file, the golems' language's names; the vars are generated (D111).

// tg: code/modules/language/terrum.dm:2
public partial class datum_language_terrum : datum_language
{
    // tg: code/modules/language/terrum.dm:22
    public override object get_random_name(string gender = null, object name_count = null, object syllable_min = null, object syllable_max = null, float? force_use_syllables = null, params object[] __extra)
    {
        if (Dm.truthy(force_use_syllables))
            return base.get_random_name(gender, name_count, syllable_min, syllable_max, force_use_syllables, __extra);

        string name = Dm.str(pick(GLOB.golem_names));
        // 3% chance to be given a human surname for "lore reasons"
        if (prob(3))
            name += Dm.text(" []", pick(GLOB.last_names));
        return name;
    }
}
