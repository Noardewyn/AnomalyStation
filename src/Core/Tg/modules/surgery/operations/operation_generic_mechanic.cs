// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_generic_mechanic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// Mechanical equivalents of basic surgical operations
/// Mechanical equivalent of cutting skin
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:3
public partial class datum_surgery_operation_limb_mechanical_incision : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:21
    public override object get_any_tool()
    {
        return I18n.T("Any sharp item");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:24
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "unscrew_shell");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:27
    public override object tool_check(obj_item tool = null)
    {
        // Require any sharpness OR a tool behavior match
        return tool.get_sharpness() is var or__1 && Dm.truthy(or__1) ? or__1 : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:31
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to unscrew the shell of []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unscrew the shell of [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unscrew the shell of [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel your [] grow numb as the shell is unscrewed.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:41
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_SKIN_CUT);
        return dot;
    }
}

/// Mechanical equivalent of opening skin and clamping vessels
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:46
public partial class datum_surgery_operation_limb_mechanical_open : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:62
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "open_hatch");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:65
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to open the hatch holders in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to open the hatch holders in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to open the hatch holders in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("The last faint pricks of tactile sensation fade from your [] as the hatch is opened.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:75
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        // We get both vessels and skin done at the same time wowee
        await limb.add_surgical_state(Dm.bitor(SURGERY_SKIN_OPEN, SURGERY_VESSELS_CLAMPED));
        await limb.remove_surgical_state(SURGERY_SKIN_CUT);
        return dot;
    }
}

/// Mechanical equivalent of cauterizing / closing skin
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:82
public partial class datum_surgery_operation_limb_mechanical_close : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:100
    public override object get_any_tool()
    {
        return I18n.T("Any sharp item");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:103
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "screw_shell");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:106
    public override object tool_check(obj_item tool = null)
    {
        // Require any sharpness OR a tool behavior match
        return tool.get_sharpness() is var or__1 && Dm.truthy(or__1) ? or__1 : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:110
    public override object state_check(atom_movable operating_on = null)
    {
        return LIMB_HAS_SKIN(operating_on);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:113
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to screw the shell of []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to screw the shell of [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to screw the shell of [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel the faint pricks of sensation return as your []'s shell is screwed in.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:123
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.remove_surgical_state(ALL_SURGERY_STATES_UNSET_ON_CLOSE);
        return dot;
    }
}

// Mechanical equivalent of cutting vessels and organs
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:128
public partial class datum_surgery_operation_limb_prepare_electronics : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:145
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "prepare_electronics");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:148
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to prepare electronics in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to prepare electronics in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to prepare electronics in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You can feel a faint buzz in your [] as the electronics reboot.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:158
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_ORGANS_CUT);
        return dot;
    }
}

// Mechanical equivalent of sawing bone
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:163
public partial class datum_surgery_operation_limb_mechanic_unwrench : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:179
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "unwrench_endoskeleton");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:182
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to unwrench some bolts in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unwrench some bolts in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unwrench some bolts in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a jostle in your [] as the bolts begin to loosen.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:192
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_BONE_SAWED);
        return dot;
    }
}

// Mechanical equivalent of unsawing bone
// tg: code/modules/surgery/operations/operation_generic_mechanic.dm:197
public partial class datum_surgery_operation_limb_mechanic_wrench : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:212
    public override object state_check(atom_movable operating_on = null)
    {
        return LIMB_HAS_BONES(operating_on);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:215
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the limb must have bones")));
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:218
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "wrench_endoskeleton");
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:221
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to wrench some bolts in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to wrench some bolts in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to wrench some bolts in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a jostle in your [] as the bolts begin to tighten.", limb.plaintext_zone), TRUE);
    }

    // tg: code/modules/surgery/operations/operation_generic_mechanic.dm:231
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.remove_surgical_state(SURGERY_BONE_SAWED);
        return dot;
    }
}
