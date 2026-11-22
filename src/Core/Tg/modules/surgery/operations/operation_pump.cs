// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_pump.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_pump.dm:1
public partial class datum_surgery_operation_organ_stomach_pump : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_pump.dm:15
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.initial(typeof(atom_movable_screen_alert_disgusted), (atom_movable_screen_alert a) => a.overlay_icon), Dm.initial(typeof(atom_movable_screen_alert_disgusted), (atom_movable_screen_alert a) => a.overlay_state));
    }

    // tg: code/modules/surgery/operations/operation_pump.dm:18
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the patient must not be husked")));
    }

    // tg: code/modules/surgery/operations/operation_pump.dm:21
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        return Dm.num(!HAS_TRAIT(organ.owner, TRAIT_HUSK));
    }

    // tg: code/modules/surgery/operations/operation_pump.dm:24
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to pump []'s stomach...", organ.owner)),
            span_notice(I18n.T("[] begins to pump []'s stomach.", surgeon, organ.owner)),
            span_notice(I18n.T("[] begins to press on []'s abdomen.", surgeon, organ.owner)));
        await display_pain(organ.owner, I18n.T("You feel a horrible sloshing feeling in your gut! You're going to be sick!"));
    }

    // tg: code/modules/surgery/operations/operation_pump.dm:34
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("[] forces [] to vomit, cleansing their stomach of some chemicals!", surgeon, organ.owner)),
            span_notice(I18n.T("[] forces [] to vomit, cleansing their stomach of some chemicals!", surgeon, organ.owner)),
            span_notice(I18n.T("[] forces [] to vomit!", surgeon, organ.owner)));
        await organ.owner.vomit(Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_STUN), lost_nutrition: 20, purge_ratio: 0.67f);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_pump.dm:44
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You screw up, bruising []'s chest!", organ.owner)),
            span_warning(I18n.T("[] screws up, bruising []'s chest!", surgeon, organ.owner)),
            span_warning(I18n.T("[] screws up!", surgeon)));
        await organ.apply_organ_damage(5);
        await organ.bodypart_owner.receive_damage(5);
    }
}
