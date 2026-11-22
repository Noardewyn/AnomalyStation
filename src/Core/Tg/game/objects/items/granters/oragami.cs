// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/oragami.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file: The Art of Origami's action (TRAIT_PAPER_MASTER: robust paper planes; tg's origami_on and
// origami_off buttons). The var blocks are generated (D111).

// tg: code/game/objects/items/granters/oragami.dm:17
public partial class datum_action_innate_origami : datum_action_innate
{
    // tg: code/game/objects/items/granters/oragami.dm:23
    public override async DmTask<object> Activate()
    {
        ADD_TRAIT(owner, TRAIT_PAPER_MASTER, ACTION_TRAIT);
        to_chat(owner, span_notice(I18n.T("You will now fold origami planes.")));
        active = TRUE;
        await build_all_button_icons(UPDATE_BUTTON_ICON);
        return null;
    }

    // tg: code/game/objects/items/granters/oragami.dm:29
    public override async DmTask<object> Deactivate()
    {
        REMOVE_TRAIT(owner, TRAIT_PAPER_MASTER, ACTION_TRAIT);
        to_chat(owner, span_notice(I18n.T("You will no longer fold origami planes.")));
        active = FALSE;
        await build_all_button_icons(UPDATE_BUTTON_ICON);
        return null;
    }

    // tg: code/game/objects/items/granters/oragami.dm:35
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        button_icon_state = Dm.text("origami_[]", Dm.truthy(active) ? "on" : "off");
        base.apply_button_icon(current_button, force);
    }
}
