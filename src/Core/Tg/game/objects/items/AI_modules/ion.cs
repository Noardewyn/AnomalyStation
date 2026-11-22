// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/ion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
CONTAINS:
/obj/item/ai_module/law/core/full/damaged
/obj/item/ai_module/law/toy_ai
*/

// tg: code/game/objects/items/AI_modules/ion.dm:7
public partial class obj_item_ai_module_law_core_full_damaged : obj_item_ai_module_law_core_full
{
    // tg: code/game/objects/items/AI_modules/ion.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_ioned(TRUE));
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/ion.dm:15
    public virtual void gen_laws()
    {
        laws.Cut();
        while (!Dm.truthy(Dm.length(laws)) && prob(75))
            laws[generate_ion_law()] = TRUE;
    }

    // tg: code/game/objects/items/AI_modules/ion.dm:20
    public override void on_rack_install(obj_machinery_ai_law_rack rack = null)
    {
        gen_laws();
    }

    // tg: code/game/objects/items/AI_modules/ion.dm:23
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        balloon_alert(user, I18n.T("repairing ion damage..?"));
        if (!Dm.truthy(await tool.use_tool(ismachinery(loc) ? loc : this, user, 4 * SECONDS, volume: 25, extra_checks: Dm.@new<datum_callback>(this, nameof(multitool_cb), loc, user, tool))))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("module repaired..?"));
        if (ismachinery(loc))
        {
            gen_laws();
            update_rack_laws();
        }
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/AI_modules/ion.dm:33
public partial class obj_item_ai_module_law_toy_ai : obj_item_ai_module_law // -- Incoming //No actual reason to inherit from ion boards here, either. *sigh* ~Miauw
{
    // tg: code/game/objects/items/AI_modules/ion.dm:40
    public override void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        combined_lawset.add_inherent_law(laws[1], 1, ioned: TRUE);
    }

    // tg: code/game/objects/items/AI_modules/ion.dm:43
    public override async DmTask<object> configure(mob user = null)
    {
        object dot = TRUE;
        laws.Cut();
        laws[generate_ion_law()] = TRUE;
        to_chat(user, span_notice(I18n.T("You press the button on [].", this)));
        playsound(user, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
        // tg: "[icon2html(src, viewers(loc))] [laws[1]]": the toy's picture in the chat line (the port's chat shows no inline icons) and the law
        this.loc.visible_message(span_warning(Dm.text("[]", laws[1])));
        return dot;
    }
}
