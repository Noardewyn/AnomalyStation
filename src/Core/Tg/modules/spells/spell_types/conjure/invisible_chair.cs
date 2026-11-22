// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/conjure/invisible_chair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/spells/spell_types/conjure/invisible_chair.dm:1
public partial class datum_action_cooldown_spell_conjure_invisible_chair : datum_action_cooldown_spell_conjure
{
    // tg: code/modules/spells/spell_types/conjure/invisible_chair.dm:25
    public override async DmTask post_summon(atom summoned_object = null, atom cast_on_atom = null)
    {
        mob_living_carbon_human cast_on = cast_on_atom as mob_living_carbon_human;
        if (!isobj(summoned_object))
            return;

        obj chair = summoned_object as obj;
        await chair.setDir(cast_on.dir);
        await chair.buckle_mob(cast_on);
    }
}
