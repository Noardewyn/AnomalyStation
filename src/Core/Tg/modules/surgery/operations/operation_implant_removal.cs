// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_implant_removal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_implant_removal.dm:1
public partial class datum_surgery_operation_basic_implant_removal : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_implant_removal.dm:17
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/obj/medical/syringe.dmi"), "implantcase-b");
    }

    // tg: code/modules/surgery/operations/operation_implant_removal.dm:20
    public override object any_optional_strings()
    {
        return Dm.add(base.any_optional_strings(), Dm.list(I18n.T("have an implant case below or inhand to store removed implants")));
    }

    // tg: code/modules/surgery/operations/operation_implant_removal.dm:23
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You search for implants in []...", patient)),
            span_notice(I18n.T("[] searches for implants in [].", surgeon, patient)),
            span_notice(I18n.T("[] searches for something in [].", surgeon, patient)));
        if (Dm.truthy(LAZYLEN(patient.implants)))
            await display_pain(patient, I18n.T("You feel a serious pain as [] digs around inside you!", surgeon));
    }

    // tg: code/modules/surgery/operations/operation_implant_removal.dm:34
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        obj_item_implant implant = LAZYACCESS(patient.implants, 1) as obj_item_implant;
        if (implant == null)
        {
            display_results(
                surgeon,
                patient,
                span_warning(I18n.T("You find no implant to remove from [].", patient)),
                span_warning(I18n.T("[] finds no implant to remove from [].", surgeon, patient)),
                span_warning(I18n.T("[] finds nothing to remove from [].", surgeon, patient)));
            return null;
        }

        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You successfully remove [] from [].", implant, patient)),
            span_notice(I18n.T("[] successfully removes [] from []!", surgeon, implant, patient)),
            span_notice(I18n.T("[] successfully removes something from []!", surgeon, patient)));
        await display_pain(patient, I18n.T("You can feel your [] pulled out of you!", implant.name));
        await implant.removed(patient);

        if (QDELETED(implant))
            return null;

        obj_item_implantcase @case = get_case(surgeon, patient) as obj_item_implantcase;
        if (@case == null)
            return null;

        @case.imp = implant;
        await implant.forceMove(@case);
        @case.update_appearance();
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You place [] into [].", implant, @case)),
            span_notice(I18n.T("[] places [] into [].", surgeon, implant, @case)),
            span_notice(I18n.T("[] places something into [].", surgeon, @case)));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_implant_removal.dm:73
    public virtual object get_case(mob_living surgeon = null, mob_living target = null)
    {
        DmList locations = Dm.list(
            surgeon.is_holding_item_of_type(typeof(obj_item_implantcase)),
            Dm.locate<obj_item_implantcase>(surgeon.loc),
            Dm.locate<obj_item_implantcase>(target.loc));

        foreach (obj_item_implantcase @case in Dm.each<obj_item_implantcase>(locations))
        {
            if (!Dm.truthy(@case.imp))
                return @case;
        }

        return null;
    }
}
