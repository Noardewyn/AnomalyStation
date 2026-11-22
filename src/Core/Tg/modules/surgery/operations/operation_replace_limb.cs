// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_replace_limb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_replace_limb.dm:1
public partial class datum_surgery_operation_limb_replace_limb : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_replace_limb.dm:14
    public override object get_recommended_tool()
    {
        return I18n.T("cybernetic limb");
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:17
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_bodypart_chest_robot));
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:20
    public override async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_radial_menu_choice option = LAZYACCESS(cached_augment_options, tool.type) as datum_radial_menu_choice;
        if (option == null)
        {
            option = Dm.@new<datum_radial_menu_choice>();
            option.name = I18n.T("augment with []", Dm.initial(tool, (obj_item o) => o.name));
            option.info = I18n.T("Replace the patient's [] with [].", Dm.initial(limb, (obj_item o) => o.name), Dm.initial(tool, (obj_item o) => o.name));
            option.image = image(tool.type);
            LAZYSET(ref cached_augment_options, tool.type, option);
        }

        return option;
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:31
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_bodypart new_limb = (obj_item_bodypart)tool;
        if (!Dm.truthy(surgeon.canUnEquip(new_limb)))
            return FALSE;
        if (!Dm.eq(limb.body_zone, new_limb.body_zone))
            return FALSE;
        if (!Dm.truthy(new_limb.can_attach_limb(limb.owner)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:40
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(!HAS_TRAIT(limb.owner, TRAIT_NO_AUGMENTS) && !Dm.truthy(Dm.bitand(limb.bodypart_flags, BODYPART_UNREMOVABLE)));
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:43
    public override object tool_check(obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(tool.item_flags, Dm.bitor(Dm.bitor(ABSTRACT, DROPDEL), HAND_ITEM))))
            return FALSE;
        if (!isbodypart(tool))
            return FALSE;
        obj_item_bodypart limb = (obj_item_bodypart)tool;
        if (!Dm.truthy(IS_ROBOTIC_LIMB(limb)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:53
    public override async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart new_limb = (obj_item_bodypart)tool;
        if (!Dm.truthy(length(new_limb.contents)))
            return TRUE;
        // Prevents quickly filling someone with high-tier organs by augmenting them with a pre-stuffed limb
        to_chat(surgeon, span_warning(I18n.T("[] needs to be empty in order to be attached!", new_limb)));
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:60
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        // purposefully doesn't use plaintext zone for more context on what is being replaced with what
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to augment []'s [] with []...", limb.owner, limb.name, tool)),
            span_notice(I18n.T("[] begins to augment []'s [] with [].", surgeon, limb.owner, limb.name, tool)),
            span_notice(I18n.T("[] begins to augment []'s [].", surgeon, limb.owner, limb.name)));
        await display_pain(limb.owner, I18n.T("You feel a horrible pain in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_replace_limb.dm:71
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_bodypart new_limb = (obj_item_bodypart)tool;
        if (!Dm.truthy(await surgeon.temporarilyRemoveItemFromInventory(new_limb)))
            return null; // should never happen

        mob_living_carbon patient = limb.owner; // owner's about to be nulled
        if (!Dm.truthy(await new_limb.replace_limb(patient)))
        {
            display_results(
                surgeon,
                patient,
                span_warning(I18n.T("You can't seem to fit [] onto []'s body!", new_limb, patient)),
                span_warning(I18n.T("[] can't seem to fit [] onto []'s body!", surgeon, new_limb, patient)),
                span_warning(I18n.T("[] can't seem to fit [] onto []'s body!", surgeon, new_limb, patient)));
            await new_limb.forceMove(patient.drop_location() as atom);
            return null; // could possibly happen
        }

        if (Dm.truthy(new_limb.check_for_frankenstein(patient as mob_living_carbon_human)))
            Dm.combine(ref new_limb.bodypart_flags, BODYPART_IMPLANTED);

        display_results(
            surgeon,
            patient,
            span_notice(I18n.T("You successfully augment []'s [] with []!", patient, limb.plaintext_zone, new_limb)),
            span_notice(I18n.T("[] successfully augments []'s [] with []!", surgeon, patient, limb.plaintext_zone, new_limb)),
            span_notice(I18n.T("[] finishes augmenting []'s [].", surgeon, patient, limb.plaintext_zone)));
        await display_pain(patient, I18n.T("Your [] comes awash with synthetic sensation!", limb.plaintext_zone), TRUE);
        log_combat(surgeon, patient, "augmented", addition: Dm.text("by giving him new []", new_limb));
        return null;
    }
}
