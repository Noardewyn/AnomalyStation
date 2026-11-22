// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/granters/sign_language.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The var block is generated (D111).

/// Sign language book adds the sign language component to the reading Human.
/// Grants reader the ability to toggle sign language using a HUD button.
// tg: code/game/objects/items/granters/sign_language.dm:3
public partial class obj_item_book_granter_sign_language : obj_item_book_granter
{
    // tg: code/game/objects/items/granters/sign_language.dm:15
    public override object can_learn(mob_living user = null)
    {
        if (!iscarbon(user))
            return null;
        if (Dm.truthy(user.GetComponent(typeof(datum_component_sign_language))))
        {
            to_chat(user, span_warning(I18n.T("You already know all about sign language!")));
            return null;
        }
        return TRUE;
    }

    // tg: code/game/objects/items/granters/sign_language.dm:23
    public override async DmTask recoil(mob_living user = null)
    {
        to_chat(user, span_warning(I18n.T("You can't read it, the pages are too faded and smudged!")));
        await DmTask.CompletedTask;
    }

    /// Called when the reading is completely finished. This is where the actual granting should happen.
    // tg: code/game/objects/items/granters/sign_language.dm:27
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        await base.on_reading_finished(user);
        await user.AddComponent(typeof(datum_component_sign_language));
    }
}
