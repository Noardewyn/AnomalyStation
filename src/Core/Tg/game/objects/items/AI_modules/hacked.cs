// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/hacked.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (silicons SI1, D326): the hacked law module; the infected AI module (/obj/item/malf_board) comes with the
// malfunctioning AI (SI8).

// tg: code/game/objects/items/AI_modules/hacked.dm:1
public partial class obj_item_ai_module_law_syndicate : obj_item_ai_module_law
{
    // tg: code/game/objects/items/AI_modules/hacked.dm:6
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        object targName = await tgui_input_text(user, "Enter a new law for the AI", "Freeform Law Entry", laws[1], max_length: config.Get(typeof(datum_config_entry_number_max_law_len)), multiline: TRUE);
        if (!Dm.truthy(targName) || !Dm.truthy(user.is_holding(this)))
            return dot;
        if (Dm.truthy(is_ic_filtered(targName)))
        {
            to_chat(user, span_warning(I18n.T("Error: Law contains invalid text.")));
            return dot;
        }
        DmList soft_filter_result = is_soft_ooc_filtered(targName);
        if (soft_filter_result != null)
        {
            if (!Dm.eq(await tgui_alert(user, Dm.text("Your law contains \"[]\". \"[]\", Are you sure you want to use it?", soft_filter_result[CHAT_FILTER_INDEX_WORD], soft_filter_result[CHAT_FILTER_INDEX_REASON]), "Soft Blocked Word", Dm.list("Yes", "No")), "Yes"))
                return dot;
            message_admins(Dm.text("[] has passed the soft filter for \"[]\" they may be using a disallowed term for an AI law. Law: \"[]\"", ADMIN_LOOKUPFLW(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], html_encode(targName)));
            log_admin_private(Dm.text("[] has passed the soft filter for \"[]\" they may be using a disallowed term for an AI law. Law: \"[]\"", key_name(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], targName));
        }
        laws[1] = targName;
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/hacked.dm:21
    public override void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        combined_lawset.add_hacked_law(laws[1]);
    }
}
