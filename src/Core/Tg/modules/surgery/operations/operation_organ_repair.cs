// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_organ_repair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. Leaves (not-translated.md section 118): the brainwashed
// antagonist's removal (the antagonists) and the brain's trauma rolls (gain_trauma_type, the brain traumas).

/// Repairing specific organs
// tg: code/modules/surgery/operations/operation_organ_repair.dm:2
public partial class datum_surgery_operation_organ_repair : datum_surgery_operation_organ
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:17
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(Dm.bitand(operation_flags, OPERATION_LOOPING)))
            repeatable = TRUE; // if it's looping it would necessitate being repeatable
        if (!Dm.truthy(repeatable))
            desc = Dm.text("[][]", desc, I18n.T(" This procedure can only be performed once per organ."));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:24
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        if (Dm.num(organ.damage) < (organ.maxHealth * heal_to_percent) || (!Dm.truthy(repeatable) && HAS_TRAIT(organ, TRAIT_ORGAN_OPERATED_ON)))
            return FALSE; // conditionally available so we don't spam the radial with useless options, alas
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:29
    public override object all_required_strings()
    {
        object dot = base.all_required_strings();
        if (!Dm.truthy(repeatable))
            Dm.append(ref dot, I18n.T("the organ must be moderately damaged"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:34
    public override object all_blocked_strings()
    {
        object dot = base.all_blocked_strings();
        if (!Dm.truthy(repeatable))
            Dm.append(ref dot, I18n.T("the organ must not have been surgically repaired prior"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:39
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await organ.set_organ_damage(organ.maxHealth * heal_to_percent);
        Dm.mask(ref organ.organ_flags, Dm.bitnot(ORGAN_EMP));
        ADD_TRAIT(organ, TRAIT_ORGAN_OPERATED_ON, TRAIT_GENERIC);
        return null;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:44
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await organ.apply_organ_damage(organ.maxHealth * failure_damage_percent);
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:47
public partial class datum_surgery_operation_organ_repair_lobectomy : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:64
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to make an incision in []'s lungs...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a stabbing pain in your chest!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:74
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully excise []'s most damaged lobe.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully excises []'s most damaged lobe.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully excises []'s most damaged lobe.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:83
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await base.on_failure(operating_on, surgeon, tool, operation_args);
        if (organ.owner != null)
            organ.owner.losebreath += 4;
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You screw up, failing to excise []'s damaged lobe!", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] screws up!", surgeon)),
            span_warning(I18n.T("[] screws up!", surgeon)));
        await display_pain(organ.owner, I18n.T("You feel a sharp stab in your chest; the wind is knocked out of you and it hurts to catch your breath!"));
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:109
public partial class datum_surgery_operation_organ_repair_hepatectomy : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:127
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to cut out a damaged piece of []'s liver...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your abdomen burns in horrific stabbing pain!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:137
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully remove the damaged part of []'s liver.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully removes the damaged part of []'s liver.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully removes the damaged part of []'s liver.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain receeds slightly!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:147
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await base.on_failure(operating_on, surgeon, tool, operation_args);
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You cut the wrong part of []'s liver!", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] cuts the wrong part of []'s liver!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] cuts the wrong part of []'s liver!", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your abdomen intensifies!"));
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:172
public partial class datum_surgery_operation_organ_repair_coronary_bypass : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:187
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to graft a bypass onto []'s heart...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to graft a bypass onto []'s heart.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to graft a bypass onto []'s heart.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your chest is unbearable! You can barely take it anymore!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:197
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully graft a bypass onto []'s heart.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully grafts a bypass onto []'s heart.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully grafts a bypass onto []'s heart.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your chest throbs, but your heart feels better than ever!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:207
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await base.on_failure(operating_on, surgeon, tool, operation_args);
        organ.bodypart_owner?.adjustBleedStacks(30);
        object blood_name = (organ.owner?.get_bloodtype() as datum_blood_type)?.get_blood_name() is var bn && Dm.truthy(bn) ? LOWER_TEXT(bn) : null;
        if (!Dm.truthy(blood_name))
            blood_name = I18n.T("blood");
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You screw up in attaching the graft, and it tears off, tearing part of the heart!")),
            span_warning(I18n.T("[] screws up, causing [] to spurt out of []'s chest profusely!", surgeon, blood_name, FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] screws up, causing [] to spurt out of []'s chest profusely!", surgeon, blood_name, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your chest burns; you feel like you're going insane!"));
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:233
public partial class datum_surgery_operation_organ_repair_gastrectomy : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:252
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:255
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match
        return Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) ? Dm.bitand(tool.get_sharpness(), SHARP_EDGED) : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:259
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to cut out a damaged piece of []'s stomach...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a horrible stab in your gut!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:269
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully remove the damaged part of []'s stomach.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully removes the damaged part of []'s stomach.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully removes the damaged part of []'s stomach.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your gut receeds slightly!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:279
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        await base.on_failure(operating_on, surgeon, tool, operation_args);
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You cut the wrong part of []'s stomach!", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] cuts the wrong part of []'s stomach!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] cuts the wrong part of []'s stomach!", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your gut intensifies!"));
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:304
public partial class datum_surgery_operation_organ_repair_ears : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:321
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("if the limb has bones, they must be intact")));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:324
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        // If bones are sawed, prevent the operation (unless we're operating on a limb with no bones)
        if (Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(organ.bodypart_owner, Dm.bitor(SURGERY_BONE_SAWED, SURGERY_BONE_DRILLED))) && Dm.truthy(LIMB_HAS_BONES(organ.bodypart_owner)))
            return FALSE;
        return TRUE; // always available so you can intentionally fail it
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:330
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to fix []'s ears...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to fix []'s ears.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform surgery on []'s ears.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a dizzying pain in your head!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:340
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ_ears organ = (obj_item_organ_ears)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        float deaf_change = 40 * SECONDS - organ.temporary_deafness;
        organ.adjust_temporary_deafness(deaf_change);
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully fix []'s ears.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []'s ears.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []'s ears.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head swims, but it seems like you can feel your hearing coming back!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:352
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(organ.bodypart_owner);
        if (brain == null)
        {
            display_results(
                surgeon,
                organ.owner,
                span_warning(I18n.T("You accidentally stab [] right in the brain! Or would have, if [] had a brain.", FORMAT_ORGAN_OWNER(organ), FORMAT_ORGAN_OWNER(organ))),
                span_warning(I18n.T("[] accidentally stabs [] right in the brain! Or would have, if [] had a brain.", surgeon, FORMAT_ORGAN_OWNER(organ), FORMAT_ORGAN_OWNER(organ))),
                span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))));
            return;
        }

        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You accidentally stab [] right in the brain!", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a visceral stabbing pain right through your head, into your brain!"));
        await organ.apply_organ_damage(70);
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:373
public partial class datum_surgery_operation_organ_repair_eyes : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:390
    public override object all_blocked_strings()
    {
        return Dm.add(base.all_blocked_strings(), Dm.list(I18n.T("if the limb has bones, they must be intact")));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:393
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        // If bones are sawed, prevent the operation (unless we're operating on a limb with no bones)
        if (Dm.truthy(LIMB_HAS_ANY_SURGERY_STATE(organ.bodypart_owner, Dm.bitor(SURGERY_BONE_SAWED, SURGERY_BONE_DRILLED))) && Dm.truthy(LIMB_HAS_BONES(organ.bodypart_owner)))
            return FALSE;
        return TRUE; // always available so you can intentionally fail it
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:399
    public override async DmTask<object> get_default_radial_image()
    {
        return image(icon: Dm.resource("icons/obj/medical/surgery_ui.dmi"), icon_state: "surgery_eyes");
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:402
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to fix []'s eyes...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to fix []'s eyes.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform surgery on []'s eyes.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a stabbing pain in your eyes!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:412
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        organ.owner?.remove_status_effect(typeof(datum_status_effect_temporary_blindness));
        if (organ.owner != null)
            await organ.owner.set_eye_blur_if_lower(70 * SECONDS); //this will fix itself slowly.
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You successfully fix []'s eyes.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []'s eyes.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []'s eyes.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your vision blurs, but it seems like you can see a little better now!"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:424
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ organ = (obj_item_organ)operating_on;
        obj_item_organ_brain brain = Dm.locate<obj_item_organ_brain>(organ.bodypart_owner);
        if (brain == null)
        {
            display_results(
                surgeon,
                organ.owner,
                span_warning(I18n.T("You accidentally stab [] right in the brain! Or would have, if [] had a brain.", FORMAT_ORGAN_OWNER(organ), FORMAT_ORGAN_OWNER(organ))),
                span_warning(I18n.T("[] accidentally stabs [] right in the brain! Or would have, if [] had a brain.", surgeon, FORMAT_ORGAN_OWNER(organ), FORMAT_ORGAN_OWNER(organ))),
                span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))));
            return;
        }

        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You accidentally stab [] right in the brain!", FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_warning(I18n.T("[] accidentally stabs [] right in the brain!", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("You feel a visceral stabbing pain right through your head, into your brain!"));
        await organ.apply_organ_damage(70);
    }
}

// tg: code/modules/surgery/operations/operation_organ_repair.dm:445
public partial class datum_surgery_operation_organ_repair_brain : datum_surgery_operation_organ_repair
{
    // tg: code/modules/surgery/operations/operation_organ_repair.dm:465
    public override object state_check(atom_movable operating_on = null)
    {
        return TRUE; // always available so you can intentionally fail it
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:468
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You begin to fix []'s brain...", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to fix []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] begins to perform surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head pounds with unimaginable pain!"));
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:478
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        await organ.apply_organ_damage(-organ.maxHealth * heal_to_percent); // no parent call, special healing for this one
        display_results(
            surgeon,
            organ.owner,
            span_notice(I18n.T("You succeed in fixing []'s brain.", FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] successfully fixes []'s brain!", surgeon, FORMAT_ORGAN_OWNER(organ))),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("The pain in your head receeds, thinking becomes a bit easier!"));
        if (organ.owner != null || organ.brainmob != null)
            Dm.Deferred("code/modules/surgery/operations/operation_organ_repair.dm:489", "on_success: mind?.remove_antag_datum(/datum/antagonist/brainwashed), the brain surgery clearing a brainwashing (the antagonists)");
        organ.cure_all_traumas(TRAUMA_RESILIENCE_SURGERY);
        if (Dm.num(organ.damage) > organ.maxHealth * 0.1f)
            to_chat(surgeon, I18n.T("[]'s brain looks like it could be fixed further.", FORMAT_ORGAN_OWNER(organ)));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_organ_repair.dm:497
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_organ_brain organ = (obj_item_organ_brain)operating_on;
        await base.on_failure(operating_on, surgeon, tool, operation_args);
        display_results(
            surgeon,
            organ.owner,
            span_warning(I18n.T("You screw up, causing more damage!")),
            span_warning(I18n.T("[] screws up, causing brain damage!", surgeon)),
            span_notice(I18n.T("[] completes the surgery on []'s brain.", surgeon, FORMAT_ORGAN_OWNER(organ))));
        await display_pain(organ.owner, I18n.T("Your head throbs with horrible pain; thinking hurts!"));
        await organ.gain_trauma_type(typeof(datum_brain_trauma_severe) /* BRAIN_TRAUMA_SEVERE, a typepath define */, TRAUMA_RESILIENCE_LOBOTOMY);
    }
}
