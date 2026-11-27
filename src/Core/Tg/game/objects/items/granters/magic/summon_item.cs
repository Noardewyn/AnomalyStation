// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/magic/summon_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the instant summons spellbook). The var block is generated.

// tg: code/game/objects/items/granters/magic/summon_item.dm:1
public partial class obj_item_book_granter_action_spell_summonitem : obj_item_book_granter_action_spell
{
    // tg: code/game/objects/items/granters/magic/summon_item.dm:16
    public override async DmTask recoil(mob_living user = null)
    {
        await base.recoil(user);
        to_chat(user, span_warning(I18n.T("[] suddenly vanishes!", this)));
        qdel(this);
    }
}
