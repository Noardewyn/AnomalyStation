// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_core.dm:1
public partial class datum_surgery_operation_basic_core_removal : datum_surgery_operation_basic
{
    // tg: code/modules/surgery/operations/operation_core.dm:15
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(mob_living_basic_slime));
    }

    // tg: code/modules/surgery/operations/operation_core.dm:18
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on a deceased slime")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_core.dm:21
    public override object state_check(atom_movable operating_on = null)
    {
        mob_living patient = (mob_living)operating_on;
        return Dm.num(isslime(patient) && Dm.eq(patient.stat, DEAD));
    }

    // tg: code/modules/surgery/operations/operation_core.dm:24
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        mob_living patient = (mob_living)operating_on;
        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You begin to extract []'s core...", patient)),
            span_notice(I18n.T("[] begins to extract []'s core.", surgeon, patient)),
            span_notice(I18n.T("[] begins to extract []'s core.", surgeon, patient)));
    }

    // tg: code/modules/surgery/operations/operation_core.dm:32
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        mob_living_basic_slime patient = (mob_living_basic_slime)operating_on;
        float core_count = patient.cores;
        if (Dm.truthy(core_count) && Dm.truthy(patient.try_extract_cores(count: core_count)))
        {
            display_results(
                surgeon,
                patient,
                span_notice(I18n.T("You successfully extract [] core[] from [].", core_count, core_count == 1 ? "" : "s", patient)),
                span_notice(I18n.T("[] successfully extracts [] core[] from []!", surgeon, core_count, core_count == 1 ? "" : "s", patient)),
                span_notice(I18n.T("[] successfully extracts [] core[] from []!", surgeon, core_count, core_count == 1 ? "" : "s", patient)));
        }
        else
            to_chat(surgeon, span_warning(I18n.T("There aren't any cores left in []!", patient)));
        return null;
    }
}
