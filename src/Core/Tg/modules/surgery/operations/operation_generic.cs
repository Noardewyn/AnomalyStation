// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_generic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// Basic operations for moving back and forth between surgery states
/// First step of every surgery, makes an incision in the skin
// tg: code/modules/surgery/operations/operation_generic.dm:3
public partial class datum_surgery_operation_limb_incise_skin : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:28
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:31
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "make_incision");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:34
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match
        if (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) || Dm.truthy(implements[tool.tool_behaviour]))
            return TRUE;
        // these are here by popular demand, even though they don't fit the above criteria
        if (istype(tool, typeof(obj_item_pen)) || istype(tool, typeof(obj_item_screwdriver)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:43
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(!Dm.truthy(Dm.bitand(limb.biological_state, biostate_blacklist)));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:46
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to make an incision in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to make an incision in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a stabbing in your [].", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:56
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra); // default success message
        await limb.add_surgical_state(Dm.bitor(SURGERY_SKIN_CUT, SURGERY_VESSELS_UNCLAMPED)); // ouch, cuts the vessels
        if (!Dm.truthy(limb.can_bleed()))
            return dot;

        object blood_name = (limb.owner?.get_bloodtype() as datum_blood_type)?.get_blood_name();
        if (!Dm.truthy(blood_name) && Dm.truthy(length(limb.blood_dna_info)))
        {
            datum_blood_type blood_type = limb.blood_dna_info[limb.blood_dna_info[1]] as datum_blood_type;
            blood_name = blood_type?.get_blood_name();
        }
        if (!Dm.truthy(blood_name))
            blood_name = I18n.T("Blood");

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("[] pools around the incision in [].", blood_name, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] pools around the incision in [].", blood_name, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] pools around the incision in [].", blood_name, FORMAT_LIMB_OWNER(limb))));
        return dot;
    }
}

/// Subtype for thick skinned creatures (Xenomorphs)
// tg: code/modules/surgery/operations/operation_generic.dm:78
public partial class datum_surgery_operation_limb_incise_skin_thick : datum_surgery_operation_limb_incise_skin
{
    // tg: code/modules/surgery/operations/operation_generic.dm:89
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item with decent force");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:92
    public override object tool_check(obj_item tool = null)
    {
        return Dm.num(Dm.truthy(base.tool_check(tool)) && Dm.num(tool.force) >= 10);
    }
}

/// Pulls the skin back to access internals
// tg: code/modules/surgery/operations/operation_generic.dm:101
public partial class datum_surgery_operation_limb_retract_skin : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:121
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "retract_skin");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:124
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to retract the skin in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to retract the skin in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to retract the skin in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a severe stinging pain spreading across your [] as the skin is pulled back.", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:134
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        // the limb SHOULD either have unclamped or clamped vessels if we're retracting skin
        // if it doesn't, some shenanigans happened (likely due to wounds), so we add unclamped if needed - just to be thorough
        await limb.add_surgical_state(Dm.bitor(SURGERY_SKIN_OPEN, LIMB_HAS_SURGERY_STATE(limb, SURGERY_VESSELS_CLAMPED) ? NONE : SURGERY_VESSELS_UNCLAMPED));
        await limb.remove_surgical_state(SURGERY_SKIN_CUT);
        return dot;
    }
}

/// Closes the skin
// tg: code/modules/surgery/operations/operation_generic.dm:146
public partial class datum_surgery_operation_limb_close_skin : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:172
    public override object get_any_tool()
    {
        return I18n.T("Any heat source");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:175
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "mend_incision");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:178
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the limb must have skin")));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:181
    public override object state_check(atom_movable operating_on = null)
    {
        return LIMB_HAS_SKIN(operating_on);
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:184
    public override object tool_check(obj_item tool = null)
    {
        if (istype(tool, typeof(obj_item_stack_medical_suture)))
            return TRUE;

        if (istype(tool, typeof(obj_item_gun_energy_laser)))
        {
            obj_item_gun_energy_laser lasergun = (obj_item_gun_energy_laser)tool;
            return Dm.num(Dm.num(lasergun.cell?.charge) > 0);
        }

        return Dm.num(Dm.num(tool.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST);
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:194
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to mend the incision in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to mend the incision in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to mend the incision in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Your [] is being []!", limb.plaintext_zone, istype(tool, typeof(obj_item_stack_medical_suture)) ? I18n.T("pinched") : I18n.T("burned")));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:204
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        if (LIMB_HAS_SURGERY_STATE(limb, SURGERY_BONE_SAWED))
            await limb.heal_damage(40);
        await limb.remove_surgical_state(ALL_SURGERY_STATES_UNSET_ON_CLOSE);
        return dot;
    }
}

/// Clamps bleeding blood vessels to prevent blood loss
// tg: code/modules/surgery/operations/operation_generic.dm:215
public partial class datum_surgery_operation_limb_clamp_bleeders : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:233
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "clamp_bleeders");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:236
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to clamp bleeders in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to clamp bleeders in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to clamp bleeders in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a pinch as the bleeding in your [] is slowed.", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:246
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        // free brute healing if you do it after sawing bones
        if (LIMB_HAS_SURGERY_STATE(limb, SURGERY_BONE_SAWED))
            await limb.heal_damage(20);
        await limb.add_surgical_state(SURGERY_VESSELS_CLAMPED);
        await limb.remove_surgical_state(SURGERY_VESSELS_UNCLAMPED);
        return dot;
    }
}

/// Unclamps blood vessels to allow blood flow again
// tg: code/modules/surgery/operations/operation_generic.dm:259
public partial class datum_surgery_operation_limb_unclamp_bleeders : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:277
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "unclamp_bleeders");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:280
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the limb must have blood vessels")));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:283
    public override object state_check(atom_movable operating_on = null)
    {
        return LIMB_HAS_VESSELS(operating_on);
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:286
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to unclamp bleeders in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unclamp bleeders in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to unclamp bleeders in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a pressure release as blood starts flowing in your [] again.", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:296
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_VESSELS_UNCLAMPED);
        await limb.remove_surgical_state(SURGERY_VESSELS_CLAMPED);
        return dot;
    }
}

/// Saws through bones to access organs
// tg: code/modules/surgery/operations/operation_generic.dm:306
public partial class datum_surgery_operation_limb_saw_bones : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:336
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item with decent force");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:339
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "saw_bones");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:342
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness and sufficient force OR a tool behavior match
        return (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) && Dm.num(tool.force) >= 10) ? TRUE : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:346
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to saw through the bone in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to saw through the bone in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to saw through the bone in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a horrid ache spread through the inside of your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:356
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_BONE_SAWED);
        await limb.receive_damage(50, sharpness: Dm.num(((obj_item)tool).get_sharpness()), wound_bonus: CANT_WOUND, damage_source: tool);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You saw [] open.", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] saws [] open!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] saws [] open!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("It feels like something just broke in your []!", limb.plaintext_zone));
        return dot;
    }
}

/// Fixes sawed bones back together
// tg: code/modules/surgery/operations/operation_generic.dm:370
public partial class datum_surgery_operation_limb_fix_bones : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:394
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "fix_bones");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:397
    public override object all_required_strings()
    {
        return Dm.add(base.all_required_strings(), Dm.list(I18n.T("the limb must have bones")));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:400
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        if (!Dm.truthy(LIMB_HAS_BONES(limb)))
            return FALSE;

        // if a wound has given us the broken bone state, don't show this surgery as an option, to prevent confusion
        foreach (datum_wound wound in Dm.each_cast<datum_wound>(limb.wounds))
        {
            if (Dm.truthy(Dm.bitand(wound.surgery_states, any_surgery_states_required)))
                return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:411
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to fix the bones in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to fix the bones in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to fix the bones in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a grinding sensation in your [] as the bones are set back in place.", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:421
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        // if the limb lacks skin, fix bones needs to act as an analog to mend incision (clearing most surgical states)
        if (!Dm.truthy(LIMB_HAS_SKIN(limb)))
            await limb.remove_surgical_state(ALL_SURGERY_STATES_UNSET_ON_CLOSE);
        await limb.remove_surgical_state(ALL_SURGERY_BONE_STATES);
        await limb.heal_damage(40);
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_generic.dm:429
public partial class datum_surgery_operation_limb_drill_bones : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:449
    public override object get_any_tool()
    {
        return I18n.T("Any sharp pointed item with decent force");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:452
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "drill_bones");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:455
    public override object tool_check(obj_item tool = null)
    {
        // Require pointy sharpness and sufficient force OR a tool behavior match
        return (Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_POINTY)) && Dm.num(tool.force) >= 10) ? TRUE : implements[tool.tool_behaviour];
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:459
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to drill into the bone in []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to drill into the bone in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to drill into the bone in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a horrible piercing pain in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:469
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_BONE_DRILLED);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You drill into [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] drills into []!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] drills into []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_generic.dm:479
public partial class datum_surgery_operation_limb_incise_organs : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_generic.dm:502
    public override object get_any_tool()
    {
        return I18n.T("Any sharp edged item");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:505
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/surgery_radial.dmi"), "incise_organs");
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:508
    public override object tool_check(obj_item tool = null)
    {
        // Require edged sharpness OR a tool behavior match. Also saws are a no-go, you'll rip up the organs!
        return Dm.num((Dm.truthy(Dm.bitand(tool.get_sharpness(), SHARP_EDGED)) || Dm.truthy(implements[tool.tool_behaviour])) && !Dm.eq(tool.tool_behaviour, TOOL_SAW));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:512
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to make an incision in the organs within []...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to make an incision in the organs within [].", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to make an incision in the organs within [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a stabbing in your [].", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_generic.dm:522
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_ORGANS_CUT);
        await limb.receive_damage(10, sharpness: Dm.num(((obj_item)tool).get_sharpness()), wound_bonus: CANT_WOUND, damage_source: tool);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You make an incision in the organs within [].", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] makes an incision in the organs within []!", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] makes an incision in the organs within []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a sharp pain from inside your []!", limb.plaintext_zone));
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_generic.dm:534
public partial class datum_surgery_operation_limb_incise_organs_abductor : datum_surgery_operation_limb_incise_organs
{
    // tg: code/modules/surgery/operations/operation_generic.dm:538
    public override object state_check(atom_movable operating_on = null)
    {
        return TRUE; // You can incise chests without sawing ribs
    }
}
