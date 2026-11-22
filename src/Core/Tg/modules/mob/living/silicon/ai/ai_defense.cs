// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the AI's defense, whole: blobs, EMPs, explosions, flashes, emags and its panel and bolts.

// tg: code/modules/mob/living/silicon/ai/ai_defense.dm:1
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:1
    public override async DmTask<object> blob_act(obj_structure_blob B = null)
    {
        if (!Dm.eq(stat, DEAD))
        {
            await adjust_brute_loss(60);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:7
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        disconnect_shell();
        if (prob(30))
        {
            switch (Dm.num(pick(1, 2)))
            {
                case 1:
                    await view_core();
                    break;
                case 2:
                    SSshuttle.requestEvac(this, "ALERT: Energy surge detected in AI core! Station integrity may be compromised! Initiati--%m091#ar-BZZT");
                    break;
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:19
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                investigate_log("has been gibbed by an explosion.", INVESTIGATE_DEATHS);
                Waiting.NoSleep(gib(DROP_ALL_REMAINS));
                break;
            case EXPLODE_HEAVY:
                if (!Dm.eq(stat, DEAD))
                {
                    Waiting.NoSleep(adjust_brute_loss(60));
                    Waiting.NoSleep(adjust_fire_loss(60));
                }
                break;
            case EXPLODE_LIGHT:
                if (!Dm.eq(stat, DEAD))
                    Waiting.NoSleep(adjust_brute_loss(30));
                break;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:33
    public override async DmTask<object> flash_act(float? intensity = null, float? override_blindness_check = null, float? affect_silicon = null, float? visual = null, Type type = null, float? length = null)
    {
        return null; // no eyes, no flashing
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:36
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra) ///emags access panel lock, so you can crowbar it without robotics access or consent
    {
        await base.emag_act(user, emag_card);
        if (Dm.truthy(emagged))
        {
            balloon_alert(user, I18n.T("access panel lock already shorted!"));
            return null;
        }
        balloon_alert(user, I18n.T("access panel lock shorted"));
        string message = user != null ? I18n.T("[] shorts out your access panel lock!", user) : I18n.T("Your access panel lock was short circuited!");
        to_chat(this, span_warning(message));
        do_sparks(3, FALSE, this); // just a bit of extra "oh shit" to the ai - might grab its attention
        emagged = TRUE;
        return TRUE;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:48
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(incapacitated) && (client != null || deployed_shell?.client != null))
        {
            // alive and well AIs control their floor bolts
            balloon_alert(user, I18n.T("the AI's bolt motors resist!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("[] bolts...", !Dm.truthy(is_anchored) ? "tightening" : "loosening"));
        balloon_alert(this, I18n.T("bolts being []...", !Dm.truthy(is_anchored) ? "tightened" : "loosened"));
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS)))
            return ITEM_INTERACT_SUCCESS;
        flip_anchored();
        balloon_alert(user, I18n.T("bolts []", Dm.truthy(is_anchored) ? "tightened" : "loosened"));
        balloon_alert(this, I18n.T("bolts []", Dm.truthy(is_anchored) ? "tightened" : "loosened"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:62
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        await base.crowbar_act(user, tool);
        if (Dm.truthy(user.combat_mode))
            return null;
        if (!Dm.truthy(is_anchored))
        {
            balloon_alert(user, I18n.T("bolt it down first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(opened))
        {
            if (Dm.truthy(emagged))
            {
                balloon_alert(user, I18n.T("access panel lock damaged!"));
                return ITEM_INTERACT_SUCCESS;
            }
            balloon_alert(user, I18n.T("closing access panel..."));
            balloon_alert(this, I18n.T("access panel being closed..."));
            if (!Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
                return ITEM_INTERACT_SUCCESS;
            balloon_alert(this, I18n.T("access panel closed"));
            balloon_alert(user, I18n.T("access panel closed"));
            opened = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.eq(stat, DEAD))
            to_chat(user, span_warning(I18n.T("The access panel looks damaged, you try dislodging the cover.")));
        else
        {
            object consent;
            bool consent_override = false;
            if (user is mob_living_carbon_human human_user)
            {
                if (human_user.wear_id != null)
                {
                    DmList access = human_user.wear_id.GetAccess() as DmList;
                    if (Dm.@in(ACCESS_ROBOTICS, access))
                        consent_override = true;
                }
            }
            if (mind != null)
            {
                consent = await tgui_alert(this, Dm.text("[] is attempting to open your access panel, unlock the cover?", user), "AI Access Panel", Dm.list("Yes", "No"));
                if (Dm.eq(consent, "No") && !consent_override && !Dm.truthy(emagged))
                {
                    to_chat(user, span_notice(I18n.T("[] refuses to unlock its access panel.", this)));
                    return ITEM_INTERACT_SUCCESS;
                }
                if (!Dm.eq(consent, "Yes") && (consent_override || Dm.truthy(emagged)))
                    to_chat(user, span_warning(I18n.T("[] refuses to unlock its access panel...so you[]open it anyway!", this, !Dm.truthy(emagged) ? " swipe your ID and " : " ")));
            }
            else
            {
                if (!consent_override && !Dm.truthy(emagged))
                {
                    to_chat(user, span_notice(I18n.T("[] did not respond to your request to unlock its access panel cover lock.", this)));
                    return ITEM_INTERACT_SUCCESS;
                }
                else
                    to_chat(user, span_notice(I18n.T("[] did not respond to your request to unlock its access panel cover lock. You[]open it anyway.", this, !Dm.truthy(emagged) ? " swipe your ID and " : " ")));
            }
        }
        balloon_alert(user, I18n.T("prying open access panel..."));
        balloon_alert(this, I18n.T("access panel being pried open..."));
        if (!Dm.truthy(await tool.use_tool(this, user, Dm.eq(stat, DEAD) ? 40 * SECONDS : 5 * SECONDS)))
            return ITEM_INTERACT_SUCCESS;
        balloon_alert(this, I18n.T("access panel opened"));
        balloon_alert(user, I18n.T("access panel opened"));
        opened = TRUE;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:117
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await base.wirecutter_act(user, tool);
        if (Dm.truthy(user.combat_mode))
            return null;
        if (!Dm.truthy(is_anchored))
        {
            balloon_alert(user, I18n.T("bolt it down first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(opened))
        {
            balloon_alert(user, I18n.T("open the access panel first!"));
            return ITEM_INTERACT_SUCCESS;
        }
        balloon_alert(this, I18n.T("neural network being disconnected..."));
        balloon_alert(user, I18n.T("disconnecting neural network..."));
        if (!Dm.truthy(await tool.use_tool(this, user, Dm.eq(stat, DEAD) ? 5 * SECONDS : 40 * SECONDS)))
            return ITEM_INTERACT_SUCCESS;
        if (Dm.truthy(IS_MALF_AI(this)))
        {
            to_chat(user, span_userdanger(I18n.T("The voltage inside the wires rises dramatically!")));
            await user.electrocute_act(120, this);
            opened = FALSE;
            return ITEM_INTERACT_SUCCESS;
        }
        to_chat(this, span_danger(I18n.T("You feel incredibly confused and disorientated.")));
        atom ai_structure = await ai_mob_to_structure();
        ai_structure.balloon_alert(user, I18n.T("disconnected neural network"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_defense.dm:142
    public override async DmTask<object> attack_effects(object damage_done = null, object hit_zone = null, object armor_block = null, obj_item attacking_item = null, mob_living attacker = null)
    {
        bool dot = false;
        if (Dm.num(damage_done) > 0 && !Dm.eq(attacking_item.damtype, STAMINA) && !Dm.eq(stat, DEAD))
        {
            await spark_system.start();
            dot = true;
        }
        object result = await base.attack_effects(damage_done, hit_zone, armor_block, attacking_item, attacker);
        return Dm.truthy(result) ? result : (dot ? TRUE : FALSE);
    }
}
