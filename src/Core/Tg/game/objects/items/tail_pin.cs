// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tail_pin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD1 (D333): pin the tail on the corgi: the tail pin lands on the party game poster where it was clicked. The rest of the file
// is var blocks only, generated (D111).

// tg: code/game/objects/items/tail_pin.dm:30
public partial class obj_structure_sign_poster_party_game : obj_structure_sign_poster
{
    // tg: code/game/objects/items/tail_pin.dm:38
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_tail_pin)//We're using the same trick that tables use for placing objects x and y onto the click location.
            return NONE;

        float x_offset = 0;
        float y_offset = 0;
        if (Dm.truthy(modifiers?[ICON_X]) && Dm.truthy(modifiers?[ICON_Y]))
        {
            x_offset = clamp(Dm.num(text2num(modifiers[ICON_X])) - 16, -(ICON_SIZE_X / 2), ICON_SIZE_X / 2);
            y_offset = clamp(Dm.num(text2num(modifiers[ICON_Y])) - 16, -(ICON_SIZE_Y / 2), ICON_SIZE_Y / 2);
        }
        if (!Dm.truthy(await user.transfer_item_to_turf(tool, drop_location() as turf, x_offset, y_offset, silent: FALSE)))
            return ITEM_INTERACT_BLOCKING;
        return ITEM_INTERACT_SUCCESS;
    }
}
