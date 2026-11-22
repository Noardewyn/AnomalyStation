// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/monkey.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the vars are generated.

// tg: code/modules/language/monkey.dm:2
public partial class datum_language_monkey : datum_language
{
    // tg: code/modules/language/monkey.dm:16
    public override object get_random_name(string gender = null, object name_count = null, object syllable_min = null, object syllable_max = null, float? force_use_syllables = null, params object[] __extra)
    {
        return Dm.text("monkey ([])", rand(1, 999));
    }
}

// tg: code/modules/language/_language_holder.dm:438 (the holder's lists are generated; a hand part puts it in init_language_holder_prototypes, DV25)
public partial class datum_language_holder_monkey : datum_language_holder
{
}
