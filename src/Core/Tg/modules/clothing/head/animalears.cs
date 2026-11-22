// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/animalears.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the kitty ears dyed the wearer's hair colour). The var blocks are generated (D111).

// tg: code/modules/clothing/head/animalears.dm:1
public partial class obj_item_clothing_head_costume_kitty : obj_item_clothing_head_costume
{
    // tg: code/modules/clothing/head/animalears.dm:10
    [DmParams("user", "slot")]
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        if (ishuman(user) && Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HEAD)))
        {
            await update_icon(ALL, user);
            await user.update_worn_head(); //Color might have been changed by update_appearance.
        }
        return await base.visual_equipped(user, slot, initial);
    }

    // tg: code/modules/clothing/head/animalears.dm:16
    [DmParams("updates", "user")]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        mob_living_carbon_human user = Dm.arg<mob_living_carbon_human>(__extra, 0);
        object dot = await base.update_icon(updates, __extra);
        if (ishuman(user))
            await add_atom_colour(user.hair_color, FIXED_COLOUR_PRIORITY);
        return dot;
    }
}
