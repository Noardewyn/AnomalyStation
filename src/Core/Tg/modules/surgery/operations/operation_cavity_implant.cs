// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_cavity_implant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated.

// tg: code/modules/surgery/operations/operation_cavity_implant.dm:1
public partial class datum_surgery_operation_limb_prepare_cavity : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:14
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_retractor));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:17
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on chest (target chest)")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:20
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.eq(limb.body_zone, BODY_ZONE_CHEST));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:23
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to open [] cavity wide...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to open [] cavity wide.", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to open [] cavity wide.", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You can feel pressure as your [] is being opened wide!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:33
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.add_surgical_state(SURGERY_CAVITY_WIDENED);
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_cavity_implant.dm:37
public partial class datum_surgery_operation_limb_undo_prepare_cavity : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:49
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_retractor));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:52
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on chest (target chest)")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:55
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        return Dm.num(Dm.eq(limb.body_zone, BODY_ZONE_CHEST));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:58
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to close [] cavity...", FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to close [] cavity.", surgeon, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to close [] cavity.", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You can feel pressure as your [] is being closed!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:68
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        object dot = await base.on_success(operating_on, surgeon, tool, operation_args, __extra);
        await limb.remove_surgical_state(SURGERY_CAVITY_WIDENED);
        return dot;
    }
}

// tg: code/modules/surgery/operations/operation_cavity_implant.dm:72
public partial class datum_surgery_operation_limb_cavity_implant : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:86
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        heavy_cavity_implants = typecacheof(Dm.list(
            typeof(obj_item_transfer_valve))) as DmList;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:92
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on chest (target chest)")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:95
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/screen_gen.dmi"), "arrow_large_still");
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:98
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart_chest limb = operating_on as obj_item_bodypart_chest;
        if (!Dm.eq(((obj_item_bodypart)operating_on).body_zone, BODY_ZONE_CHEST))
            return FALSE;
        if (limb.cavity_item != null)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:105
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item realtool = (obj_item)tool;
        if (!Dm.truthy(surgeon.canUnEquip(realtool)))
            return FALSE;
        // Stops accidentally putting a tool you meant to operate with
        // Besides who really wants to put a scalpel or a wrench inside someone that's lame
        if (Dm.truthy(IS_ROBOTIC_LIMB(limb)) && Dm.@in(realtool.tool_behaviour, GLOB.all_mechanical_tools))
            return FALSE;
        if (Dm.truthy(IS_ORGANIC_LIMB(limb)) && Dm.@in(realtool.tool_behaviour, GLOB.all_surgical_tools))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:116
    public override object tool_check(obj_item tool = null)
    {
        if (tool.w_class > WEIGHT_CLASS_NORMAL && !Dm.truthy(is_type_in_typecache(tool, heavy_cavity_implants)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(tool.item_flags, Dm.bitor(Dm.bitor(ABSTRACT, DROPDEL), HAND_ITEM))))
            return FALSE;
        if (isorgan(tool))
            return FALSE; // use organ manipulation
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:125
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item realtool = (obj_item)tool;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to insert [] into []...", realtool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to insert [] into [].", surgeon, realtool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to insert [] into [].", surgeon, realtool.w_class > WEIGHT_CLASS_SMALL ? realtool : I18n.T("something"), FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You can feel something being inserted into your [], it hurts like hell!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:135
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart_chest limb = (obj_item_bodypart_chest)operating_on;
        obj_item realtool = (obj_item)tool;
        if (!Dm.truthy(await surgeon.transferItemToLoc(realtool, limb.owner, force: TRUE))) // shouldn't fail but just in case
        {
            display_results(
                surgeon,
                limb.owner,
                span_warning(I18n.T("You can't seem to fit [] in []'s []!", realtool, limb.owner, limb.plaintext_zone)),
                span_warning(I18n.T("[] can't seem to fit [] in []'s []!", surgeon, realtool, limb.owner, limb.plaintext_zone)),
                span_warning(I18n.T("[] can't seem to fit [] in []'s []!", surgeon, realtool.w_class > WEIGHT_CLASS_SMALL ? realtool : I18n.T("something"), limb.owner, limb.plaintext_zone)));
            return null;
        }

        limb.cavity_item = realtool;
        await limb.remove_surgical_state(SURGERY_CAVITY_WIDENED);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You stuff [] into [].", realtool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] stuffs [] into []!", surgeon, realtool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] stuffs [] into [].", surgeon, realtool.w_class > WEIGHT_CLASS_SMALL ? realtool : I18n.T("something"), FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_cavity_implant.dm:157
public partial class datum_surgery_operation_limb_undo_cavity_implant : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:172
    public override object all_required_strings()
    {
        return Dm.add(Dm.list(I18n.T("operate on chest (target chest)")), base.all_required_strings());
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:175
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/screen_gen.dmi"), "arrow_large_still");
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:178
    public override async DmTask<object> get_radial_options(atom_movable operating_on = null, obj_item tool = null, object operating_zone = null)
    {
        obj_item_bodypart_chest limb = (obj_item_bodypart_chest)operating_on;
        // Not bothering to cache this as the chance of hitting the same cavity item in the same round is rather low
        datum_radial_menu_choice option = Dm.@new<datum_radial_menu_choice>();
        option.name = I18n.T("remove []", limb.cavity_item);
        option.info = I18n.T("Replace the [] embededd in the patient's chest cavity.", limb.cavity_item);
        option.image = await get_generic_limb_radial_image(BODY_ZONE_CHEST);
        Dm.append(((image)option.image).overlays, add_radial_overlays(limb.cavity_item));
        return option;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:188
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart_chest limb = operating_on as obj_item_bodypart_chest;
        if (!Dm.eq(((obj_item_bodypart)operating_on).body_zone, BODY_ZONE_CHEST))
            return FALSE;
        // unlike implant removal, don't show the surgery as an option unless something is actually implanted
        // it would stand to reason standard implants would be hidden from view (requires a search)
        // while cavity implants would be blatantly visible (no search necessary)
        if (limb.cavity_item == null)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:198
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart_chest limb = (obj_item_bodypart_chest)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to extract [] from []...", limb.cavity_item, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to extract [] from [].", surgeon, limb.cavity_item, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to extract something from [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You feel a serious pain in your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_cavity_implant.dm:208
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart_chest limb = (obj_item_bodypart_chest)operating_on;
        if (limb.cavity_item == null) // something else could have removed it mid surgery?
        {
            display_results(
                surgeon,
                limb.owner,
                span_warning(I18n.T("You find nothing to remove from [].", FORMAT_LIMB_OWNER(limb))),
                span_warning(I18n.T("[] finds nothing to remove from [].", surgeon, FORMAT_LIMB_OWNER(limb))),
                span_warning(I18n.T("[] finds nothing to remove from [].", surgeon, FORMAT_LIMB_OWNER(limb))));
            return null;
        }

        obj_item implant = limb.cavity_item;
        limb.cavity_item = null;
        await limb.remove_surgical_state(SURGERY_CAVITY_WIDENED);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You pull [] out of [].", implant, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] pulls [] out of []!", surgeon, implant, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] pulls something out of []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("You can feel [] being pulled out of you!", implant.name));
        await surgeon.put_in_hands(implant);
        return null;
    }
}
