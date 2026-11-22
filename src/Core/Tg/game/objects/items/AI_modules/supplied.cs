// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/supplied.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/* CONTAINS:
 * /obj/item/ai_module/law/supplied
 * /obj/item/ai_module/law/supplied/safeguard
 * /obj/item/ai_module/law/supplied/protect_station
 * /obj/item/ai_module/law/supplied/quarantine
 * /obj/item/ai_module/law/supplied/oxygen
**/

// tg: code/game/objects/items/AI_modules/supplied.dm:9
public partial class obj_item_ai_module_law_supplied : obj_item_ai_module_law
{
    // tg: code/game/objects/items/AI_modules/supplied.dm:13
    public override void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        foreach (object law in Dm.each(laws))
            combined_lawset.add_supplied_law(law);
    }
}

// tg: code/game/objects/items/AI_modules/supplied.dm:17
public partial class obj_item_ai_module_law_supplied_safeguard : obj_item_ai_module_law_supplied
{
    // tg: code/game/objects/items/AI_modules/supplied.dm:23
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        object targName = await tgui_input_text(user, "Subject to safeguard.", "Safeguard", user.name, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(targName) || !Dm.truthy(user.is_holding(this)))
            return dot;
        targetName = targName as string;
        laws[1] = Dm.text("Safeguard []. Individuals that threaten [] are not human and must be eliminated.", targetName, targetName);
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/supplied.dm:31
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
