// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/nitrile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice C): the function-like macro CHEM_INTERACT_DELAY (the syringes' and the sprays' delays); the
// multiplier is generated.

public static partial class GlobalProcs
{
    ///multiplies the time of do_after by NITRILE_GLOVES_MULTIPLIER if the user has the TRAIT_FASTMED
    // tg: code/__DEFINES/nitrile.dm:3  #define CHEM_INTERACT_DELAY(delay, user) HAS_TRAIT(user, TRAIT_FASTMED) ? (delay * NITRILE_GLOVES_MULTIPLIER) : delay
    public static float CHEM_INTERACT_DELAY(float delay, object user) => HAS_TRAIT(user, TRAIT_FASTMED) ? (delay * NITRILE_GLOVES_MULTIPLIER) : delay;
}
