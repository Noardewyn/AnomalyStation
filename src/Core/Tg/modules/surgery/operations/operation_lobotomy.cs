// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_lobotomy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The new traumas it may give go through the brain's
// gain_trauma_type, a deferred leaf until the brain traumas; the brainwashed antagonist's removal waits for the antagonists.

// tg: code/modules/surgery/operations/operation_lobotomy.dm:1
public partial class datum_surgery_operation_organ_lobotomy : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_lobotomy.dm:23
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_lobotomy.dm:26
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match
        return Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) ? Dm.bitand(tool.get_sharpness(), SHARP_EDGED) : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_lobotomy.dm:30
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to perform a lobotomy on []'s brain...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform a lobotomy on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head pounds with unimaginable pain!"));
    }

    // tg: code/modules/surgery/operations/operation_lobotomy.dm:40
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully perform a lobotomy on []!", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully lobotomizes []!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] finishes performing surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head goes totally numb for a moment, the pain is overwhelming!"));

        organ.cure_all_traumas(TRAUMA_RESILIENCE_LOBOTOMY);
        if (organ.owner != null || organ.brainmob != null)
            Dm.Deferred("code/modules/surgery/operations/operation_lobotomy.dm:52", "on_success: mind?.remove_antag_datum(/datum/antagonist/brainwashed), the lobotomy clearing a brainwashing (the antagonists)");

        if (!prob(75))
            return null;

        float sw__1 = rand(1, 3); //Now let's see what hopefully-not-important part of the brain we cut off
        if (sw__1 == 1)
            await organ.gain_trauma_type(typeof(datum_brain_trauma_mild) /* BRAIN_TRAUMA_MILD */, TRAUMA_RESILIENCE_MAGIC);
        else if (sw__1 == 2)
        {
            if (HAS_TRAIT(organ, TRAIT_SPECIAL_TRAUMA_BOOST) && prob(50))
                await organ.gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, TRAUMA_RESILIENCE_MAGIC);
            else
                await organ.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_MAGIC);
        }
        else if (sw__1 == 3)
            await organ.gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, TRAUMA_RESILIENCE_MAGIC);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_lobotomy.dm:69
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You remove the wrong part, causing more damage!")),
            span_notice(I18n.T("[] unsuccessfully attempts to lobotomize []!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your head only seems to get worse!"));
        await organ.apply_organ_damage(80);
        float sw__1 = rand(1, 3);
        if (sw__1 == 1)
            await organ.gain_trauma_type(typeof(datum_brain_trauma_mild) /* BRAIN_TRAUMA_MILD */, TRAUMA_RESILIENCE_MAGIC);
        else if (sw__1 == 2)
        {
            if (HAS_TRAIT(organ, TRAIT_SPECIAL_TRAUMA_BOOST) && prob(50))
                await organ.gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, TRAUMA_RESILIENCE_MAGIC);
            else
                await organ.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_MAGIC);
        }
        else if (sw__1 == 3)
            await organ.gain_trauma_type(typeof(datum_brain_trauma_special) /* BRAIN_TRAUMA_SPECIAL */, TRAUMA_RESILIENCE_MAGIC);
    }
}
