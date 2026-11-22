// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_virus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_virus.dm:1
public partial class datum_surgery_operation_basic_viral_bonding : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_virus.dm:23
    public override object get_any_tool()
    {
        return I18n.T("Any heat source");
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:26
    public override object all_required_strings()
    {
        object dot = base.all_required_strings();
        Dm.append(ref dot, I18n.T("the patient must have a virus to bond"));
        foreach (Type chem in Dm.each_cast<Type>(required_chems))
            Dm.append(ref dot, I18n.T("the patient must be dosed with >=1u []", Dm.initial(chem, (datum_reagent o) => o.name)));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:32
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_clothing_mask_surgical));
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:35
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        foreach (object chem in Dm.each(required_chems))
        {
            if (Dm.num(((datum_reagents)patient.reagents)?.get_reagent_amount(chem)) < 1)
                return FALSE;
        }
        foreach (datum_disease infected_disease in Dm.each_cast<datum_disease>(patient.diseases))
        {
            if (!Dm.eq(infected_disease.severity, DISEASE_SEVERITY_UNCURABLE))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:44
    public override object tool_check(obj_item tool = null)
    {
        return Dm.num(Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST);
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:47
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You start heating []'s bone marrow with []...", patient, tool)),
            span_notice(I18n.T("[] starts heating []'s bone marrow with []...", surgeon, patient, tool)),
            span_notice(I18n.T("[] starts heating something in []'s chest with []...", surgeon, patient, tool)));
        await display_pain(patient, I18n.T("You feel a searing heat spread through your chest!"));
    }

    // tg: code/modules/surgery/operations/operation_virus.dm:57
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("[]'s bone marrow begins pulsing slowly. The viral bonding is complete.", patient)),
            span_notice(I18n.T("[]'s bone marrow begins pulsing slowly.", patient)),
            span_notice(I18n.T("[] finishes the operation.", surgeon)));
        await display_pain(patient, I18n.T("You feel a faint throbbing in your chest."));
        foreach (datum_disease infected_disease in Dm.each_cast<datum_disease>(patient.diseases))
        {
            if (!Dm.eq(infected_disease.severity, DISEASE_SEVERITY_UNCURABLE)) //no curing quirks, sweaty
                infected_disease.carrier = TRUE;
        }
        return TRUE;
    }
}
