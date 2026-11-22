// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_add_limb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// This surgery is so snowflake that it doesn't use any of the operation subtypes, it forges its own path
// tg: code/modules/surgery/operations/operation_add_limb.dm:4
public partial class datum_surgery_operation_limb_prosthetic_replacement : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_add_limb.dm:1  #define OPERATION_REJECTION_DAMAGE "tox_damage"
    private const string OPERATION_REJECTION_DAMAGE = "tox_damage";

    // tg: code/modules/surgery/operations/operation_add_limb.dm:25
    public override object get_recommended_tool()
    {
        return I18n.T("any limb / any item");
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:28
    public override object get_any_tool()
    {
        return I18n.T("Any suitable arm replacement");
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:31
    public override object all_required_strings()
    {
        object dot = base.all_required_strings();
        Dm.append(ref dot, I18n.T("the limb must be missing / a stump"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:35
    public override object any_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("arms may receive any suitable item in lieu of a replacement limb")), base.any_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:38
    public override async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        datum_radial_menu_choice option = LAZYACCESS(cached_prosthetic_options, tool.type) as datum_radial_menu_choice;
        if (option == null)
        {
            option = Dm.@new<datum_radial_menu_choice>();
            option.name = I18n.T("attach []", Dm.initial(tool, (obj_item o) => o.name));
            option.info = I18n.T("Replace the patient's missing limb with [].", Dm.initial(tool, (obj_item o) => o.name));
            option.image = image(tool.type);
            LAZYSET(ref cached_prosthetic_options, tool.type, option);
        }

        return option;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:49
    public override object state_check(atom_movable operating_on = null)
    {
        return IS_STUMP(operating_on);
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:52
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        obj_item_bodypart chest = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(surgeon.canUnEquip(tool as obj_item)))
            return FALSE;
        object real_operated_zone = deprecise_zone(operated_zone);
        // check bodyshape compatibility for real bodyparts
        if (isbodypart(tool))
        {
            obj_item_bodypart new_limb = (obj_item_bodypart)tool;
            if (!Dm.eq(real_operated_zone, new_limb.body_zone))
                return FALSE;
            if (!Dm.truthy(new_limb.can_attach_limb(chest.owner)))
                return FALSE;
        }
        // arbitrary prosthetics can only be used on arms (for now)
        else if (!Dm.@in(real_operated_zone, GLOB.arm_zones))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:68
    public override object tool_check(obj_item tool = null)
    {
        if (Dm.truthy(Dm.bitand(tool.item_flags, Dm.bitor(Dm.bitor(ABSTRACT, DROPDEL), HAND_ITEM))))
            return FALSE;
        if (isbodypart(tool))
            return TRUE; // auto pass - "intended" use case
        if (Dm.truthy(is_type_in_list(tool, always_accepted_prosthetics)))
            return TRUE; // auto pass - soulful prosthetics
        if (tool.w_class < WEIGHT_CLASS_NORMAL || tool.w_class > WEIGHT_CLASS_BULKY)
            return FALSE; // too large or too small items don't make sense as a limb replacement
        if (HAS_TRAIT(tool, TRAIT_WIELDED))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:81
    public override async DmTask<object> pre_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        object dot = await base.pre_preop(operating_on, surgeon, tool, operation_args);
        // always operate on absolute body zones
        operation_args[OPERATION_TARGET_ZONE] = deprecise_zone(operation_args[OPERATION_TARGET_ZONE]);
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:86
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            // "You begin to attach the right arm to john doe's right arm stump"
            span_notice(I18n.T("You begin to attach []'s to []...", tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to attach []'s to [].", surgeon, tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to attach []'s to [].", surgeon, tool, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel an uncomfortable sensation where your [] should be!", parse_zone(limb.body_zone)));

        operation_args[OPERATION_REJECTION_DAMAGE] = 10;
        if (isbodypart(tool))
        {
            obj_item_bodypart new_limb = (obj_item_bodypart)tool;
            if (Dm.truthy(IS_ROBOTIC_LIMB(new_limb)))
                operation_args[OPERATION_REJECTION_DAMAGE] = 0;
            else if (Dm.truthy(new_limb.check_for_frankenstein(limb.owner as mob_living_carbon_human)))
                operation_args[OPERATION_REJECTION_DAMAGE] = 30;
        }
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:105
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(await surgeon.temporarilyRemoveItemFromInventory((obj_item)tool)))
            return null; // should never happen
        if (Dm.num(operation_args[OPERATION_REJECTION_DAMAGE]) > 0)
            await limb.owner.apply_damage(Dm.num(operation_args[OPERATION_REJECTION_DAMAGE]), TOX);
        if (isbodypart(tool))
        {
            await handle_bodypart(limb.owner, surgeon, (obj_item_bodypart)tool);
            return null;
        }
        await handle_arbitrary_prosthetic(limb.owner, surgeon, (obj_item)tool, operation_args[OPERATION_TARGET_ZONE]);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:115
    public virtual async DmTask handle_bodypart(mob_living_carbon patient = null, mob_living surgeon = null, obj_item_bodypart bodypart_to_attach = null)
    {
        await bodypart_to_attach.try_attach_limb(patient);
        if (Dm.truthy(bodypart_to_attach.check_for_frankenstein(patient as mob_living_carbon_human)))
            Dm.combine(ref bodypart_to_attach.bodypart_flags, BODYPART_IMPLANTED);
        display_results(
            surgeon, patient,
            span_notice(I18n.T("You succeed in replacing []'s [].", patient, bodypart_to_attach.plaintext_zone)),
            span_notice(I18n.T("[] successfully replaces []'s [] with []!", surgeon, patient, bodypart_to_attach.plaintext_zone, bodypart_to_attach)),
            span_notice(I18n.T("[] successfully replaces []'s []!", surgeon, patient, bodypart_to_attach.plaintext_zone)));
        await display_pain(patient, I18n.T("You feel synthetic sensation wash from your [], which you can feel again!", bodypart_to_attach.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:127
    public virtual async DmTask handle_arbitrary_prosthetic(mob_living_carbon patient = null, mob_living surgeon = null, obj_item thing_to_attach = null, object target_zone = null)
    {
        SSblackbox.record_feedback("tally", "arbitrary_prosthetic", 1, Dm.initial(thing_to_attach, (obj_item o) => o.name));
        obj_item_bodypart new_limb = await patient.make_item_prosthetic(thing_to_attach, (string)target_zone, 80) as obj_item_bodypart;
        await new_limb.add_surgical_state(SURGERY_PROSTHETIC_UNSECURED);
        display_results(
            surgeon, patient,
            span_notice(I18n.T("You attach [].", thing_to_attach)),
            span_notice(I18n.T("[] finishes attaching []!", surgeon, thing_to_attach)),
            span_notice(I18n.T("[] finishes the attachment procedure!", surgeon)));
        await display_pain(patient, I18n.T("You feel a strange sensation as [] takes the place of your arm!", thing_to_attach), TRUE);
    }
}

// tg: code/modules/surgery/operations/operation_add_limb.dm:142
public partial class datum_surgery_operation_limb_secure_arbitrary_prosthetic : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_add_limb.dm:154
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_stack_medical_suture));
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:157
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_stack_medical medical_tool = (obj_item_stack_medical)tool;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to [] [] to []'s body.", medical_tool.singular_name, limb, limb.owner)),
            span_notice(I18n.T("[] begins to [] [] to []'s body.", surgeon, medical_tool.singular_name, limb, limb.owner)),
            span_notice(I18n.T("[] begins to [] something to []'s body.", surgeon, medical_tool.singular_name, limb.owner)));
        await display_pain(limb.owner, I18n.T("[] begins to [] [] to your body!", surgeon, medical_tool.singular_name, limb), IS_ROBOTIC_LIMB(limb));
    }

    // tg: code/modules/surgery/operations/operation_add_limb.dm:167
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item_stack_medical medical_tool = (obj_item_stack_medical)tool;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You finish [] [] to []'s body.", medical_tool.apply_verb, limb, limb.owner)),
            span_notice(I18n.T("[] finishes [] [] to []'s body.", surgeon, medical_tool.apply_verb, limb, limb.owner)),
            span_notice(I18n.T("[] finishes the [] procedure!", surgeon, medical_tool.apply_verb)));
        await display_pain(limb.owner, I18n.T("You feel more secure as your prosthetic is firmly attached to your body!"), IS_ROBOTIC_LIMB(limb));
        await limb.remove_surgical_state(SURGERY_PROSTHETIC_UNSECURED);
        await limb.AddComponent(typeof(datum_component_item_as_prosthetic_limb), null, 0); // updates drop probability to zero
        await medical_tool.use(1);
        return null;
    }
}
