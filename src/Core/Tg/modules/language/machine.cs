// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/machine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/language/machine.dm:1
public partial class datum_language_machine : datum_language
{
    // tg: code/modules/language/machine.dm:23
    // tg's override puts `unique` fifth, where the base has force_use_syllables, and force_use_syllables sixth (__extra[0]).
    [DmParams("gender", "name_count", "syllable_min", "syllable_max", "unique", "force_use_syllables")]
    public override object get_random_name(string gender = null, object name_count = null, object syllable_min = null, object syllable_max = null, float? force_use_syllables = null, params object[] __extra)
    {
        float? unique = force_use_syllables ?? FALSE;
        object force_use_syllables__6 = Dm.arg(__extra, 0) ?? FALSE;
        gender ??= NEUTER;
        name_count ??= 2;
        syllable_min ??= 2;
        syllable_max ??= 4;
        if (Dm.truthy(force_use_syllables__6))
            return base.get_random_name(gender, name_count, syllable_min, syllable_max, unique, force_use_syllables__6);
        if (prob(70))
            return Dm.text("[]-[]", pick(GLOB.posibrain_names), rand(100, 999));
        return pick(GLOB.ai_names);
    }
}
