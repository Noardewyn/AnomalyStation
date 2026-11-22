// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/util/virtual_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the virtual antagonist's body without the organic needs.

// tg: code/modules/bitrunning/util/virtual_mob.dm:1
public partial class mob_living_carbon
{
    /// Removes all the "organic" traits from a mob, used for virtual antagonists
    // tg: code/modules/bitrunning/util/virtual_mob.dm:2
    public virtual void make_virtual_mob()
    {
        add_traits(Dm.list(
            TRAIT_NO_AUGMENTS,
            TRAIT_NO_DNA_COPY,
            TRAIT_NOBLOOD,
            TRAIT_NOBREATH,
            TRAIT_NOHUNGER,
            TRAIT_RESISTCOLD,
            TRAIT_RESISTHIGHPRESSURE,
            TRAIT_RESISTLOWPRESSURE,
            TRAIT_WEATHER_IMMUNE
            ), TRAIT_GENERIC);
    }
}
