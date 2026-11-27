// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the function-like macros GET_FISH_SLOWDOWN and GET_FISH_WEIGHT_RANK translated (the fish); the constants are
// generated, FISH_AUTOWIKI_FILENAME belongs to the autowiki (not ported).
public static partial class GlobalProcs
{
    ///The slowdown of the fish when carried begins at this value
    // tg: code/__DEFINES/fish.dm:177  #define GET_FISH_SLOWDOWN(weighty) round(((weighty/FISH_WEIGHT_SLOWDOWN_DIVISOR)**FISH_WEIGHT_SLOWDOWN_EXPONENT)-1.3, 0.1)
    public static float GET_FISH_SLOWDOWN(object weighty) => round(pow(Dm.num(weighty) / FISH_WEIGHT_SLOWDOWN_DIVISOR, FISH_WEIGHT_SLOWDOWN_EXPONENT) - 1.3f, 0.1f);

    ///Used to calculate the force of the fish by comparing (1 + log(weight/this_define)) and the w_class of the item.
    // tg: code/__DEFINES/fish.dm:184  #define GET_FISH_WEIGHT_RANK(weighty) max(round(1 + log(2, max(weighty/FISH_WEIGHT_FORCE_DIVISOR, 1)), 1), 1)
    public static float GET_FISH_WEIGHT_RANK(object weighty) => max(round(1 + log(2, max(Dm.num(weighty) / FISH_WEIGHT_FORCE_DIVISOR, 1)), 1), 1);
}
