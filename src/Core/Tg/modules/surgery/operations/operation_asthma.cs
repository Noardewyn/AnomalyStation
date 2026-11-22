// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_asthma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The asthma quirk's inflammation (adjust_inflammation)
// waits for the quirks (not-translated.md section 118); no one has the quirk until then.

// tg: code/modules/surgery/operations/operation_asthma.dm:1
public partial class datum_surgery_operation_organ_asthmatic_bypass : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_asthma.dm:20
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the patient must be asthmatic")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_asthma.dm:23
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        if (!Dm.truthy(organ.owner.has_quirk(typeof(datum_quirk_item_quirk_asthma))))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_asthma.dm:28
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to stretch []'s windpipe, trying your best to avoid nearby blood vessels...", organ.owner)),
            span_notice(I18n.T("[] begins to stretch []'s windpipe, taking care to avoid any nearby blood vessels.", surgeon, organ.owner)),
            span_notice(I18n.T("[] begins to stretch []'s windpipe.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("You feel an agonizing stretching sensation in your neck!"));
    }

    // tg: code/modules/surgery/operations/operation_asthma.dm:38
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        datum_quirk_item_quirk_asthma asthma = organ.owner.get_quirk(typeof(datum_quirk_item_quirk_asthma)) as datum_quirk_item_quirk_asthma;
        if (asthma == null)
            return null;

        Dm.Deferred("code/modules/surgery/operations/operation_asthma.dm:43", "on_success: asthma.adjust_inflammation(-inflammation_reduction) (the asthma quirk)");

        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You stretch []'s windpipe with [], managing to avoid the nearby blood vessels.", organ.owner, tool)),
            span_notice(I18n.T("[] succeeds at stretching []'s windpipe with [], avoiding the nearby blood vessels.", surgeon, organ.owner, tool)),
            span_notice(I18n.T("[] finishes stretching []'s windpipe.", surgeon, organ.owner)));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_asthma.dm:52
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        datum_quirk_item_quirk_asthma asthma = organ.owner.get_quirk(typeof(datum_quirk_item_quirk_asthma)) as datum_quirk_item_quirk_asthma;
        if (asthma == null)
            return;

        Dm.Deferred("code/modules/surgery/operations/operation_asthma.dm:57", "on_failure: asthma.adjust_inflammation(-inflammation_reduction) (the asthma quirk)");

        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You stretch []'s windpipe with [], but accidentally clip a few arteries!", organ.owner, tool)),
            span_warning(I18n.T("[] succeeds at stretching []'s windpipe with [], but accidentally clips a few arteries!", surgeon, organ.owner, tool)),
            span_warning(I18n.T("[] finishes stretching []'s windpipe, but screws up!", surgeon, organ.owner)));

        organ.owner.losebreath++;

        if (prob(30))
            await organ.owner.cause_wound_of_type_and_severity(WOUND_SLASH, organ.bodypart_owner, WOUND_SEVERITY_MODERATE, WOUND_SEVERITY_CRITICAL, WOUND_PICK_LOWEST_SEVERITY, tool);
        await organ.bodypart_owner.receive_damage(brute: 10, wound_bonus: ((obj_item)tool).wound_bonus, sharpness: SHARP_EDGED, damage_source: tool);
    }
}
