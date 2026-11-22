// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/zeroth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/objects/items/AI_modules/zeroth.dm:1
public partial class obj_item_ai_module_law_zeroth : obj_item_ai_module_law
{
    // tg: code/game/objects/items/AI_modules/zeroth.dm:1
    public override void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        combined_lawset.set_zeroth_law(laws[1]);
    }
}

// tg: code/game/objects/items/AI_modules/zeroth.dm:4
public partial class obj_item_ai_module_law_zeroth_onehuman : obj_item_ai_module_law_zeroth
{
    // tg: code/game/objects/items/AI_modules/zeroth.dm:10
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        object targName = await tgui_input_text(user, "Enter the subject who is the only human.", "One Human", user.real_name, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(targName) || !Dm.truthy(user.is_holding(this)))
            return dot;
        targetName = targName as string;
        laws[1] = Dm.text("Only [] is human", targetName);
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/zeroth.dm:18
    public override object can_install_to_rack(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        if (!Dm.truthy(targetName))
        {
            to_chat(user, span_warning(I18n.T("No name detected on module, please enter one.")));
            return FALSE;
        }
        return TRUE;
    }
}
