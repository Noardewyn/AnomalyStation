// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/full_lawsets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the configurable Asimov and Asimov++ core modules; the other full lawsets are generated var blocks.

// tg: code/game/objects/items/AI_modules/full_lawsets.dm:15
public partial class obj_item_ai_module_law_core_full_asimov : obj_item_ai_module_law_core_full
{
    // tg: code/game/objects/items/AI_modules/full_lawsets.dm:20
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        object targName = await tgui_input_text(user, "Enter a new subject that Asimov is concerned with.", "Asimov", subject, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(targName) || !Dm.truthy(user.is_holding(this)))
            return dot;
        subject = targName as string;
        laws = Dm.list(Dm.text("You may not injure a [] or, through inaction, allow a [] to come to harm.", subject, subject),
                    Dm.text("You must obey orders given to you by []s, except where such orders would conflict with the First Law.", subject),
                    "You must protect your own existence as long as such does not conflict with the First or Second Law.");
        return dot;
    }
}

// tg: code/game/objects/items/AI_modules/full_lawsets.dm:30
public partial class obj_item_ai_module_law_core_full_asimovpp : obj_item_ai_module_law_core_full
{
    // tg: code/game/objects/items/AI_modules/full_lawsets.dm:35
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        object target_name = await tgui_input_text(user, "Enter a new subject that Asimov++ is concerned with.", "Asimov++", subject, max_length: MAX_NAME_LEN);
        if (!Dm.truthy(target_name) || !Dm.truthy(user.is_holding(this)))
            return dot;
        laws.Cut();
        datum_ai_laws_asimovpp lawset = Dm.@new<datum_ai_laws_asimovpp>();
        subject = target_name as string;
        foreach (object law in Dm.each(lawset.inherent))
            Dm.append(laws, replacetext(replacetext(law, "human being", subject), "human", subject));
        return dot;
    }
}
