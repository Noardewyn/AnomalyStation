// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_bone_repair.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// Surgical analog to manual dislocation treatment
// tg: code/modules/surgery/operations/operation_bone_repair.dm:2
public partial class datum_surgery_operation_limb_repair_dislocation : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:14
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_blunt_bone bone_wound in Dm.each<datum_wound_blunt_bone>(limb.wounds))
        {
            if (HAS_TRAIT(bone_wound, TRAIT_WOUND_SCANNED) && Dm.@in(TOOL_BONESET, bone_wound.treatable_tools))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:20
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_bonesetter));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:23
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must be dislocated")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:26
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        foreach (datum_wound_blunt_bone bone_wound in Dm.each<datum_wound_blunt_bone>(limb.wounds))
        {
            if (Dm.@in(TOOL_BONESET, bone_wound.treatable_tools))
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:33
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to reset the dislocation in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to reset the dislocation in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to reset the dislocation in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] aches with pain!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:43
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        foreach (datum_wound_blunt_bone bone_wound in Dm.each<datum_wound_blunt_bone>(limb.wounds))
        {
            if (Dm.@in(TOOL_BONESET, bone_wound.treatable_tools))
                qdel(bone_wound);
        }

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully reset the dislocation in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully resets the dislocation in []!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully resets the dislocation in []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] feels much better now!", limb.plaintext_zone));
        return null;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:57
    public override async DmTask on_failure(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You fail to reset the dislocation in [], causing further damage!", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] fails to reset the dislocation in [], causing further damage!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] fails to reset the dislocation in []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("The pain in your [] intensifies!", limb.plaintext_zone));
        await limb.receive_damage(25, damage_source: tool);
    }
}

// tg: code/modules/surgery/operations/operation_bone_repair.dm:67
public partial class datum_surgery_operation_limb_repair_hairline : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:81
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_blunt_bone_critical bone_wound in Dm.each<datum_wound_blunt_bone_critical>(limb.wounds))
        {
            if (HAS_TRAIT(bone_wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:87
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_bonesetter));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:90
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must have a hairline fracture")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:93
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(Dm.locate<datum_wound_blunt_bone_severe>(limb.wounds)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:98
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to repair the fracture in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to repair the fracture in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to repair the fracture in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] aches with pain!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:108
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_blunt_bone fracture = Dm.locate<datum_wound_blunt_bone>(limb.wounds);
        qdel(fracture);

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully repair the fracture in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully repairs the fracture in []!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully repairs the fracture in []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_bone_repair.dm:119
public partial class datum_surgery_operation_limb_reset_compound : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:133
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_blunt_bone_severe bone_wound in Dm.each<datum_wound_blunt_bone_severe>(limb.wounds))
        {
            if (HAS_TRAIT(bone_wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:139
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_bonesetter));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:142
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb must have a compound fracture")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:145
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_blunt_bone_critical fracture = Dm.locate<datum_wound_blunt_bone_critical>(limb.wounds);
        if (fracture == null || Dm.truthy(fracture.reset))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:151
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to reset the bone in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to reset the bone in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to reset the bone in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("The aching pain in your [] is overwhelming!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:161
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_blunt_bone_critical fracture = Dm.locate<datum_wound_blunt_bone_critical>(limb.wounds);
        if (fracture != null)
            fracture.reset = TRUE;

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully reset the bone in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully resets the bone in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] successfully resets the bone in []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_bone_repair.dm:172
public partial class datum_surgery_operation_limb_repair_compound : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:185
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_blunt_bone_critical bone_wound in Dm.each<datum_wound_blunt_bone_critical>(limb.wounds))
        {
            if (HAS_TRAIT(bone_wound, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:191
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_stack_medical_bone_gel));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:194
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the limb's compound fracture has been reset")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:197
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_blunt_bone_critical fracture = Dm.locate<datum_wound_blunt_bone_critical>(limb.wounds);
        if (fracture == null || !Dm.truthy(fracture.reset))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:203
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to repair the fracture in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to repair the fracture in [] with [].", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] begins to repair the fracture in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("The aching pain in your [] is overwhelming!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:213
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_blunt_bone_critical fracture = Dm.locate<datum_wound_blunt_bone_critical>(limb.wounds);
        qdel(fracture);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully repair the fracture in [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] successfully repairs the fracture in [] with []!", surgeon, FORMAT_LIMB_OWNER(limb), tool)),
            span_notice(I18n.T("[] successfully repairs the fracture in []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_bone_repair.dm:223
public partial class datum_surgery_operation_limb_prepare_cranium_repair : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:235
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_cranial_fissure fissure in Dm.each<datum_wound_cranial_fissure>(limb.wounds))
        {
            if (HAS_TRAIT(fissure, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:241
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_hemostat));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:244
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the cranium must be fractured")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:247
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_cranial_fissure fissure = Dm.locate<datum_wound_cranial_fissure>(limb.wounds);
        if (fissure == null || Dm.truthy(fissure.prepped))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:253
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to discard the smaller skull debris in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to discard the smaller skull debris in []...", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to poke around in []...", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your brain feels like it's getting stabbed by little shards of glass!"));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:263
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        datum_wound_cranial_fissure fissure = Dm.locate<datum_wound_cranial_fissure>(limb.wounds);
        if (fissure != null)
            fissure.prepped = TRUE;
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_bone_repair.dm:268
public partial class datum_surgery_operation_limb_repair_cranium : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_bone_repair.dm:280
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (datum_wound_cranial_fissure fissure in Dm.each<datum_wound_cranial_fissure>(limb.wounds))
        {
            if (HAS_TRAIT(fissure, TRAIT_WOUND_SCANNED))
                dot *= 0.5f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:286
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_stack_medical_bone_gel));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:289
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("the debris has been cleared from the cranial fissure")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:292
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_cranial_fissure fissure = Dm.locate<datum_wound_cranial_fissure>(limb.wounds);
        if (fissure == null || !Dm.truthy(fissure.prepped))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:298
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to repair []'s skull as best you can...", (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] begins to repair []'s skull with [].", surgeon, (object)limb.owner ?? limb, tool)),
            span_notice(I18n.T("[] begins to repair []'s skull.", surgeon, (object)limb.owner ?? limb)));

        await display_pain(limb.owner, I18n.T("You can feel pieces of your skull rubbing against your brain!"));
    }

    // tg: code/modules/surgery/operations/operation_bone_repair.dm:309
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        datum_wound_cranial_fissure fissure = Dm.locate<datum_wound_cranial_fissure>(limb.wounds);
        qdel(fissure);

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You successfully repair []'s skull.", (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] successfully repairs []'s skull with [].", surgeon, (object)limb.owner ?? limb, tool)),
            span_notice(I18n.T("[] successfully repairs []'s skull.", surgeon, (object)limb.owner ?? limb)));
        return null;
    }
}
