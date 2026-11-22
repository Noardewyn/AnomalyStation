// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/martial_arts/plasma_fist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): the frayed scroll (the Plasma Fist), whole. The var blocks are generated (D111).

// tg: code/game/objects/items/granters/martial_arts/plasma_fist.dm:1
public partial class obj_item_book_granter_martial_plasma_fist : obj_item_book_granter_martial
{
    // tg: code/game/objects/items/granters/martial_arts/plasma_fist.dm:19
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        update_appearance();
    }

    // tg: code/game/objects/items/granters/martial_arts/plasma_fist.dm:23
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (uses <= 0)
        {
            name = "empty scroll";
            desc = "It's completely blank.";
            icon_state = "blankscroll";
        }
        else
        {
            name = Dm.initial(this, (obj_item_book_granter_martial_plasma_fist c) => c.name);
            desc = Dm.initial(this, (obj_item_book_granter_martial_plasma_fist c) => c.desc);
            icon_state = Dm.initial(this, (obj_item_book_granter_martial_plasma_fist c) => c.icon_state);
        }
        return dot;
    }
}
