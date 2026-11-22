// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_amputation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_amputation.dm:1
public partial class datum_surgery_operation_limb_amputate : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_amputation.dm:27
    public override object get_recommended_tool()
    {
        return TOOL_SAW;
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:30
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "amputate");
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:33
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (Dm.eq(limb.body_zone, BODY_ZONE_CHEST))
            return FALSE;
        if (Dm.truthy(Dm.bitand(limb.bodypart_flags, BODYPART_UNREMOVABLE)))
            return FALSE;
        if (HAS_TRAIT(limb.owner, TRAIT_NODISMEMBER))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:42
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to sever []'s []...", limb.owner, limb.plaintext_zone)),
            span_notice(I18n.T("[] begins to sever []'s [].", surgeon, limb.owner, limb.plaintext_zone)),
            span_notice(I18n.T("[] begins to sever []'s [] with [].", surgeon, limb.owner, limb.plaintext_zone, tool)));
        await display_pain(limb.owner, I18n.T("You feel a gruesome pain in your []'s joint!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:52
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully amputate []'s []!", limb.owner, limb.plaintext_zone)),
            span_notice(I18n.T("[] successfully amputates []'s []!", surgeon, limb.owner, limb.plaintext_zone)),
            span_notice(I18n.T("[] finishes severing []'s [].", surgeon, limb.owner, limb.plaintext_zone)));
        await display_pain(limb.owner, I18n.T("You can no longer feel your []!", limb.plaintext_zone));
        if (HAS_MIND_TRAIT(surgeon, TRAIT_MORBID))
            await surgeon.add_mood_event("morbid_dissection_success", typeof(datum_mood_event_morbid_dissection_success));
        await limb.drop_limb();
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_amputation.dm:66
public partial class datum_surgery_operation_limb_amputate_mechanic : datum_surgery_operation_limb_amputate
{
    // tg: code/modules/surgery/operations/operation_amputation.dm:83
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        // added requirement for bone sawed to prevent accidental head removals.
        return Dm.num(Dm.truthy(base.state_check(operating_on)) && (!Dm.eq(limb.body_zone, BODY_ZONE_HEAD) || LIMB_HAS_SURGERY_STATE(limb, SURGERY_BONE_SAWED)));
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:87
    public override object any_required_strings()
    {
        return Dm.add(base.any_required_strings(), Dm.list(
            I18n.T("if operating on the head, the bone MUST be sawed"),
            I18n.T("otherwise, the state of the bone doesn't matter")));
    }

    // tg: code/modules/surgery/operations/operation_amputation.dm:93
    public override object get_recommended_tool()
    {
        return Dm.text("[] / []", TOOL_WRENCH, TOOL_SAW);
    }
}

// tg: code/modules/surgery/operations/operation_amputation.dm:96
public partial class datum_surgery_operation_limb_amputate_pegleg : datum_surgery_operation_limb_amputate
{
    // tg: code/modules/surgery/operations/operation_amputation.dm:114
    public override object all_required_strings()
    {
        object dot = base.all_required_strings();
        Dm.append(ref dot, I18n.T("the limb must be wooden"));
        return dot;
    }
}
