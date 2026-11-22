// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/mobfactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only FAST_FACTION_CHECK translated (the M7 projectiles slice: a projectile passes the firer's allies, can_hit_target);
// the object-like #defines are emitted by the defines converter.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/mobfactions.dm:147  #define FAST_FACTION_CHECK(faction_A, faction_B, allies_A, allies_B, exact_match)
    // ( !(exact_match) ? (LAZYLEN((faction_A) & (faction_B)) || LAZYLEN((allies_A) & (allies_B))) : ((LAZYLEN((faction_A) & (faction_B)) == LAZYLEN(faction_A)) && LAZYLEN((allies_A) & (allies_B))) )
    public static float FAST_FACTION_CHECK(object faction_A, object faction_B, object allies_A, object allies_B, object exact_match) =>
        (!Dm.truthy(exact_match)
            ? (LAZYLEN(Dm.bitand((faction_A), (faction_B))) is var or__1 && Dm.truthy(or__1) ? or__1 : LAZYLEN(Dm.bitand((allies_A), (allies_B))))
            : (Dm.num(LAZYLEN(Dm.bitand((faction_A), (faction_B))) == LAZYLEN(faction_A)) is var and__1 && !Dm.truthy(and__1) ? and__1 : LAZYLEN(Dm.bitand((allies_A), (allies_B)))));
}
