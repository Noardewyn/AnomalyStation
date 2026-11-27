// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/magic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The function-like macros of the file (rules 11.1); its constants are generated (Defines).
public static partial class GlobalProcs
{
    // tg: code/__DEFINES/magic.dm:109  #define is_jaunting(atom) (istype(atom.loc, /obj/effect/dummy/phased_mob))
    public static bool is_jaunting(atom atom) => (atom as atom_movable)?.loc is obj_effect_dummy_phased_mob;
}
