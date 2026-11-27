// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/tower_of_babel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only cure_curse_of_babel, which the Book of Babel calls; the admin's Tower of Babel event comes with the admin tools.

public static partial class GlobalProcs
{
    // tg: code/modules/spells/spell_types/tower_of_babel.dm:60
    public static void cure_curse_of_babel(mob_living_carbon to_cure = null)
    {
        if (!iscarbon(to_cure))
            return;
        if (to_cure.mind == null)
            return;

        // anyone who has this trait from another source is immune to being cursed by tower of babel
        if (!HAS_TRAIT_FROM(to_cure.mind, TRAIT_TOWER_OF_BABEL, TRAUMA_TRAIT))
            return;

        to_cure.remove_status_effect(typeof(datum_status_effect_tower_of_babel_magical));
    }
}
