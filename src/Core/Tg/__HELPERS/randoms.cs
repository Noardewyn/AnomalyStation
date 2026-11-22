// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/randoms.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate G4a, D316): random_nukecode, for the red pill junk mail; ion_num (silicons SI1, D326).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/randoms.dm:30
    public static string random_nukecode()
    {
        float val = rand(0, 99999);
        string str = Dm.text("[]", val);
        while (length(str) < 5)
            str = "0" + str;
        return str;
    }
}

public static partial class GlobalProcs
{
    ///Picks a string of symbols to display as the law number for hacked or ion laws
    // tg: code/__HELPERS/randoms.dm:26
    public static string ion_num() //! is at the start to prevent us from changing say modes via get_message_mode()
    {
        return Dm.text("![][][][]", pick("!", "@", "#", "$", "%", "^", "&"), pick("!", "@", "#", "$", "%", "^", "&", "*"), pick("!", "@", "#", "$", "%", "^", "&", "*"), pick("!", "@", "#", "$", "%", "^", "&", "*"));
    }
}
