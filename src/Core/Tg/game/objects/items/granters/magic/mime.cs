// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/magic/mime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps J2, D298): the mime's granter base; the advanced guides (the blockade, finger guns) wait for their spells.

// tg: code/game/objects/items/granters/magic/mime.dm:1
public partial class obj_item_book_granter_action_spell_mime : obj_item_book_granter_action_spell
{
    // tg: code/game/objects/items/granters/magic/mime.dm:8
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (!Dm.truthy(dot))
            return dot;

        // Gives the user a vow ability if they don't have one
        datum_action_cooldown_spell_vow_of_silence vow = Dm.locate<datum_action_cooldown_spell_vow_of_silence>(user.actions) as datum_action_cooldown_spell_vow_of_silence;
        if (vow == null && user.mind != null)
        {
            vow = await Dm.new_async<datum_action_cooldown_spell_vow_of_silence>(user.mind);
            await vow.Grant(user);
        }
        return dot;
    }
}
