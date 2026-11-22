// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/operations/operation_dental.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package P1 (D298): the whole file. The var blocks are generated. The pill's attack log is a deferred leaf (logs).

// tg: code/modules/surgery/operations/operation_dental.dm:1
public partial class datum_surgery_operation_limb_add_dental_implant : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_dental.dm:12
    public override object all_required_strings()
    {
        object dot = Dm.list();
        Dm.append(ref dot, I18n.T("operate on mouth (target mouth)"));
        Dm.append(ref dot, base.all_required_strings());
        Dm.append(ref dot, I18n.T("the mouth must have teeth"));
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:18
    public override async DmTask<object> get_default_radial_image()
    {
        return image(Dm.resource("icons/hud/implants.dmi"), "reagents");
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:21
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        return Dm.num(Dm.truthy(base.snowflake_check_availability(operating_on, surgeon, tool, operated_zone)) && Dm.truthy(surgeon.canUnEquip(tool as obj_item)) && Dm.eq(operated_zone, BODY_ZONE_PRECISE_MOUTH));
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:24
    public override object state_check(atom_movable operating_on = null)
    {
        obj_item_bodypart_head teeth_receptangle = operating_on as obj_item_bodypart_head;
        if (!istype(teeth_receptangle, typeof(obj_item_bodypart_head)))
            return FALSE;
        if (teeth_receptangle.teeth_count <= 0)
            return FALSE;
        float count = 0;
        foreach (obj_item_reagent_containers_applicator_pill dental in Dm.each<obj_item_reagent_containers_applicator_pill>(operating_on))
            count++;
        if (count >= teeth_receptangle.teeth_count)
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:37
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin to wedge [] in []...", tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to wedge \\the [] in [].", surgeon, tool, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] begins to wedge something in [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        await display_pain(limb.owner, I18n.T("Something's being jammed into your []!", limb.plaintext_zone));
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:47
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        obj_item pill = (obj_item)tool;
        // Pills go into head
        await surgeon.transferItemToLoc(pill, limb, TRUE);

        datum_action_item_action_activate_pill pill_action = Dm.@new<datum_action_item_action_activate_pill>(pill);
        pill_action.name = I18n.T("Activate []", pill.name);
        await pill_action.build_all_button_icons();
        await pill_action.Grant(limb.owner); //The pill never actually goes in an inventory slot, so the owner doesn't inherit actions from it

        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You wedge [] into [].", pill, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] wedges [] into []!", surgeon, pill, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] wedges something into []!", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// tg: code/modules/surgery/operations/operation_dental.dm:64
public partial class datum_surgery_operation_limb_remove_dental_implant : datum_surgery_operation_limb
{
    // tg: code/modules/surgery/operations/operation_dental.dm:76
    public override async DmTask<object> get_default_radial_image()
    {
        return image(typeof(obj_item_reagent_containers_applicator_pill));
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:79
    public override object snowflake_check_availability(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, object operated_zone = null)
    {
        return Dm.num(Dm.truthy(base.snowflake_check_availability(operating_on, surgeon, tool, operated_zone)) && Dm.eq(operated_zone, BODY_ZONE_PRECISE_MOUTH));
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:82
    public override object get_time_modifiers(atom_movable operating_on = null, mob_living surgeon = null, object tool = null)
    {
        float dot = Dm.num(base.get_time_modifiers(operating_on, surgeon, tool));
        foreach (obj_item_flashlight light in Dm.each<obj_item_flashlight>(surgeon))
        {
            if (Dm.truthy(light.light_on)) // Hey I can see a better!
                dot *= 0.8f;
        }
        return dot;
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:88
    public override async DmTask on_preop(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You begin looking in []'s mouth for dental implants...", (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] begins to look in []'s mouth.", surgeon, (object)limb.owner ?? limb)),
            span_notice(I18n.T("[] begins to examine []'s teeth.", surgeon, (object)limb.owner ?? limb)));
        await display_pain(limb.owner, I18n.T("You feel fingers poke around at your teeth."));
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:98
    public override async DmTask<object> on_success(atom_movable operating_on = null, mob_living surgeon = null, object tool = null, DmList operation_args = null, params object[] __extra)
    {
        obj_item_bodypart limb = (obj_item_bodypart)operating_on;
        DmList pills = Dm.list();
        foreach (obj_item_reagent_containers_applicator_pill dental in Dm.each<obj_item_reagent_containers_applicator_pill>(limb))
            Dm.append(ref pills, dental);
        if (!Dm.truthy(length(pills)))
        {
            display_results(
                surgeon,
                limb.owner,
                span_notice(I18n.T("You don't find any dental implants in [].", FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] doesn't find any dental implants in [].", surgeon, FORMAT_LIMB_OWNER(limb))),
                span_notice(I18n.T("[] finishes examining [].", surgeon, FORMAT_LIMB_OWNER(limb))));
            return null;
        }

        obj_item_reagent_containers_applicator_pill yoinked = pick(pills) as obj_item_reagent_containers_applicator_pill;
        foreach (datum_action_item_action_activate_pill associated_action in Dm.each<datum_action_item_action_activate_pill>(limb.owner.actions))
        {
            if (associated_action.target == yoinked)
                qdel(associated_action);
        }

        await surgeon.put_in_hands(yoinked);
        display_results(
            surgeon,
            limb.owner,
            span_notice(I18n.T("You carefully remove [] from [].", yoinked, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] carefully removes [] from [].", surgeon, yoinked, FORMAT_LIMB_OWNER(limb))),
            span_notice(I18n.T("[] carefully removes something from [].", surgeon, FORMAT_LIMB_OWNER(limb))));
        return null;
    }
}

// Teeth pill code
// tg: code/modules/surgery/operations/operation_dental.dm:128
public partial class datum_action_item_action_activate_pill : datum_action_item_action
{
    // tg: code/modules/surgery/operations/operation_dental.dm:132
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (IS_UNCONSCIOUS(owner))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    // tg: code/modules/surgery/operations/operation_dental.dm:137
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        owner.balloon_alert_to_viewers(I18n.T("[] grinds their teeth!", owner), I18n.T("you grit your teeth"));
        if (!Dm.truthy(await do_after(owner, Dm.num(owner.stat) * (2.5f * SECONDS), owner, Dm.bitor(IGNORE_USER_LOC_CHANGE, IGNORE_INCAPACITATED))))
            return FALSE;
        obj_item pill = (obj_item)target;
        to_chat(owner, span_notice(I18n.T("You grit your teeth and burst the implanted []!", pill.name)));
        owner.log_message(Dm.text("swallowed an implanted pill, []", pill), LOG_ATTACK);
        await ((datum_reagents)pill.reagents).trans_to(owner, Dm.num(((datum_reagents)pill.reagents).total_volume), transferred_by: owner, methods: INGEST);
        qdel(pill);
        return TRUE;
    }
}
