// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/wendigo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Wendigo blood

// tg: code/modules/mining/lavaland/mining_loot/megafauna/wendigo.dm:3
public partial class obj_item_wendigo_blood : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/wendigo.dm:9
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!ishuman(user))
            return null;
        mob_living_carbon_human human_user = (mob_living_carbon_human)user;
        if (human_user.mind == null)
            return null;
        to_chat(human_user, span_danger(I18n.T("Power courses through you! You can now shift your form at will.")));
        // The shapeshift spells wait for the wizard's spells, so this grants an inert spell for now.
        datum_action_cooldown_spell_shapeshift_polar_bear transformation_spell = Dm.@new<datum_action_cooldown_spell_shapeshift_polar_bear>((object)user.mind ?? user);
        await transformation_spell.Grant(user);
        playsound(human_user.loc, Dm.resource("sound/items/drink.ogg"), rand(10, 50), TRUE);
        qdel(this);
        return null;
    }
}
