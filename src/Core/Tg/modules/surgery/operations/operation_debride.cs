// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_debride.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_debride.dm:1
public partial class datum_surgery_operation_limb_debride : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_debride.dm:25
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_burn_flesh wound in Dm.each<datum_wound_burn_flesh>(limb.wounds))
        {
            if (HAS_TRAIT(wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:31
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_reagent_containers_applicator_patch_aiuri));
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:34
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must have a second degree or worse burn")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:37
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_burn_flesh wound = Dm.locate<datum_wound_burn_flesh>(limb.wounds);
        return Dm.num(Dm.num(wound?.infection) > 0);
    }

    /// To give the surgeon a heads up how much work they have ahead of them
    // tg: code/modules/surgery/operations/operation_debride.dm:42
    public virtual object get_progress(datum_wound_burn_flesh wound = null)
    {
        if (Dm.num(wound?.infection) <= 0)
            return null;

        float estimated_remaining_steps = Dm.num(wound.infection) / infestation_removed;
        string progress_text = null;

        float sw__1 = estimated_remaining_steps;
        if (sw__1 <= 1)
            return null;
        else if (sw__1 >= 1 && sw__1 <= 2)
            progress_text = I18n.T(", preparing to remove the last remaining bits of infection");
        else if (sw__1 >= 2 && sw__1 <= 4)
            progress_text = I18n.T(", steadily narrowing the remaining bits of infection");
        else if (sw__1 >= 5)
            progress_text = I18n.T(", though there's still quite a lot to excise");

        return progress_text;
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:62
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to excise infected flesh from []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to excise infected flesh from [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to excise infected flesh from [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("The infection in your [] stings like hell! It feels like you're being stabbed!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:72
    [DmParams("operating_on", "surgeon", "tool", "operation_args", "default_display_results")]
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        await limb.receive_damage(3, wound_bonus: CANT_WOUND, sharpness: Dm.num(((obj_item)tool).get_sharpness()), damage_source: tool);
        datum_wound_burn_flesh wound = Dm.locate<datum_wound_burn_flesh>(limb.wounds);
        if (wound != null)
        {
            wound.infection = Dm.num(wound.infection) - infestation_removed;
            wound.sanitization += sanitization_added;
        }
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully excise some of the infected flesh from [][].", FORMAT_LIMB_OWNER(limb), get_progress(wound))),
            span_notice(I18n.T("[] successfully excises some of the infected flesh from [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] successfully excises some of the infected flesh from  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_debride.dm:84
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You carve away some of the healthy flesh from [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] carves away some of the healthy flesh from [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] carves away some of the healthy flesh from  []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await limb.receive_damage(rand(4, 8), wound_bonus: CANT_WOUND, sharpness: Dm.num(((obj_item)tool).get_sharpness()), damage_source: tool);
    }
}
