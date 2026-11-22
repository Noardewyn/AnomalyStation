// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_pacify.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_pacify.dm:1
public partial class datum_surgery_operation_organ_pacify : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_pacify.dm:21
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.initial(typeof(atom_movable_screen_alert_status_effect_high), (atom_movable_screen_alert a) => a.overlay_icon), Dm.initial(typeof(atom_movable_screen_alert_status_effect_high), (atom_movable_screen_alert a) => a.overlay_state));
    }

    // tg: code/modules/surgery/operations/operation_pacify.dm:24
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to pacify []...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to fix []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head pounds with unimaginable pain!"));
    }

    // tg: code/modules/surgery/operations/operation_pacify.dm:34
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You succeed in pacifying [].", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head pounds... the concept of violence flashes in your head, and nearly makes you hurl!"));
        await organ.gain_trauma(typeof(datum_brain_trauma_severe_pacifism), TRAUMA_RESILIENCE_LOBOTOMY);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_pacify.dm:45
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You screw up, rewiring []'s brain the wrong way around...", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] screws up, causing brain damage!", surgeon)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head pounds, and it feels like it's getting worse!"));
        await organ.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE */, TRAUMA_RESILIENCE_LOBOTOMY);
    }
}
