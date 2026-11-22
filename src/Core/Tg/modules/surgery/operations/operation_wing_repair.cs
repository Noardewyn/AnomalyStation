// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_wing_repair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The moth's wings and antennae healing (heal_wings,
// heal_antennae) waits for the moth species' organs (not-translated.md section 118): no moth is playable yet.

// tg: code/modules/surgery/operations/operation_wing_repair.dm:1
public partial class datum_surgery_operation_organ_fix_wings : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_wing_repair.dm:18
    public override async DmTask<object> get_default_radial_image()
    {
        return image(icon: Dm.resource("icons/mob/human/species/moth/moth_wings.dmi"), icon_state: "m_moth_wings_monarch_BEHIND");
    }

    // tg: code/modules/surgery/operations/operation_wing_repair.dm:21
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the wings must be burnt"), I18n.T("the patient must be dosed with >=5u []", Dm.initial(typeof(datum_reagent_medicine_c2_synthflesh), (datum_reagent o) => o.name))));
    }

    // tg: code/modules/surgery/operations/operation_wing_repair.dm:24
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("if the limb has bones, they must be intact")));
    }

    // tg: code/modules/surgery/operations/operation_wing_repair.dm:27
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ_wings_moth organ = (obj_item_organ_wings_moth)operating_on;
        if (!Dm.truthy(organ.burnt))
            return FALSE;
        // If bones are sawed, prevent the operation (unless we're operating on a limb with no bones)
        if (Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(organ.bodypart_owner, Dm.bitor(SURGERY_BONE_DRILLED, SURGERY_BONE_SAWED))) && Dm.truthy(LIMB_HAS_BONES(organ.bodypart_owner)))
            return FALSE;
        if (Dm.num(((datum_reagents)organ.owner.reagents)?.get_reagent_amount(typeof(datum_reagent_medicine_c2_synthflesh))) < 5)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_wing_repair.dm:37
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to repair []'s damaged wings...", organ.owner)),
            span_notice(I18n.T("[] begins to repair []'s damaged wings.", surgeon, organ.owner)),
            span_notice(I18n.T("[] begins to perform surgery on []'s damaged wings.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("Your wings sting like hell!"));
    }

    // tg: code/modules/surgery/operations/operation_wing_repair.dm:47
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You succeed in repairing []'s wings.", organ.owner)),
            span_notice(I18n.T("[] successfully repairs []'s wings!", surgeon, organ.owner)),
            span_notice(I18n.T("[] completes the surgery on []'s wings.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("You can feel your wings again!"));
        // heal the wings in question
        // might as well heal their antennae too
        Dm.Deferred("code/modules/surgery/operations/operation_wing_repair.dm:56", "on_success: organ.heal_wings(surgeon, ALL) and the antennae's heal_antennae (the moth's wings and antennae, species organs)");

        await organ.owner.update_body_parts();
        return null;
    }
}
