// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_autopsy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The scanner's paper report (scan_cadaver) is translated
// (gaps autopsy, with S3's paper); the operating computer's experiment handler catalogues the autopsy for its experiments (gaps RD3).

// tg: code/modules/surgery/operations/operation_autopsy.dm:1
public partial class datum_surgery_operation_limb_autopsy : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_autopsy.dm:12
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_autopsy_scanner));
    }

    // tg: code/modules/surgery/operations/operation_autopsy.dm:15
    public override object all_required_strings()
    {
        object dot = Dm.list();
        Dm.append(ref dot, I18n.T("operate on chest (target chest)"));
        Dm.append(ref dot, base.all_required_strings());
        Dm.append(ref dot, I18n.T("the patient must be deceased"));
        Dm.append(ref dot, I18n.T("the patient must not have been autopsied prior"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_autopsy.dm:22
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.eq(limb.body_zone, BODY_ZONE_CHEST))
            return FALSE;
        if (!Dm.eq(limb.owner.stat, DEAD))
            return FALSE;
        if (HAS_TRAIT_FROM(limb.owner, TRAIT_DISSECTED, AUTOPSY_TRAIT))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_autopsy.dm:31
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin performing an autopsy on []...", limb.owner)),
            span_notice(I18n.T("[] uses [] to perform an autopsy on [].", surgeon, tool, limb.owner)),
            span_notice(I18n.T("[] uses [] on []'s chest.", surgeon, tool, limb.owner)));
    }

    // tg: code/modules/surgery/operations/operation_autopsy.dm:40
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_autopsy_scanner scanner = (obj_item_autopsy_scanner)tool;
        ADD_TRAIT(limb.owner, TRAIT_DISSECTED, AUTOPSY_TRAIT);
        ADD_TRAIT(limb.owner, TRAIT_SURGICALLY_ANALYZED, AUTOPSY_TRAIT);
        await scanner.scan_cadaver(surgeon as mob_living_carbon_human, limb.owner);
        obj_machinery_computer_operating operating_computer = locate_operating_computer(limb) as obj_machinery_computer_operating;
        // SEND_SIGNAL(operating_computer, COMSIG_OPERATING_COMPUTER_AUTOPSY_COMPLETE, limb.owner) (rules 11.1 (e))
        if (operating_computer != null && Dm.truthy(operating_computer._listen_lookup?[COMSIG_OPERATING_COMPUTER_AUTOPSY_COMPLETE]))
            operating_computer._SendSignal(COMSIG_OPERATING_COMPUTER_AUTOPSY_COMPLETE, Dm.list(operating_computer, limb.owner));
        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID))
            await surgeon.add_mood_event("morbid_dissection_success", typeof(datum_mood_event_morbid_dissection_success));
        return await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
    }
}
