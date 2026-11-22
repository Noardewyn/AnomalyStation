// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/paiwire.dm and code/modules/pai/debug.dm (make_pai)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole paiwire.dm; debug.dm's admin verb "Make pAI" is admin tooling (not-translated.md), its mob proc is here.

// tg: code/game/objects/items/paiwire.dm:1
public partial class obj_item_pai_cable : obj_item
{
    // tg: code/game/objects/items/paiwire.dm:10
    public override object Destroy(float? force = null)
    {
        hacking_machine = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/paiwire.dm:14
    public virtual async DmTask plugin(obj_machinery M = null, mob_living user = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(this, M)))
            return;
        user.visible_message(span_notice(I18n.T("[] inserts [] into a data port on [].", user, this, M)), span_notice(I18n.T("You insert [] into a data port on [].", this, M)), span_hear(I18n.T("You hear the satisfying click of a wire jack fastening into place.")));
        hacking_machine = M;
    }
}

public partial class mob
{
    /**
     * Creates a new pAI.
     *
     * @param {boolean} delete_old - If TRUE, deletes the old pAI.
     */
    // tg: code/modules/pai/debug.dm:36
    public virtual async DmTask make_pai(object delete_old = null)
    {
        var card = Dm.@new<obj_item_pai_card>(this);
        var pai = Dm.@new<mob_living_silicon_pai>(card);
        pai.PossessByPlayer(key);
        pai.name = name;
        card.set_personality(pai);
        if (Dm.truthy(delete_old))
            qdel(this);
    }
}
