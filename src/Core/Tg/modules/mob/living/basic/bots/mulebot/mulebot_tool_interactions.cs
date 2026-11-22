// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_tool_interactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot.dm:1
public partial class mob_living_basic_bot_mulebot : mob_living_basic_bot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_tool_interactions.dm:1
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.screwdriver_act(user, tool);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_tool_interactions.dm:5
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)) || Dm.truthy(user.combat_mode))
            return null;
        if (cell == null)
        {
            to_chat(user, span_warning(Dm.text("[] doesn't have a power cell!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        cell.add_fingerprint(user);
        user.visible_message(
            span_notice(Dm.text("[] crowbars [] out from [].", user, cell, this)),
            span_notice(Dm.text("You pry [] out of [].", cell, this)));
        if (Dm.truthy(await Adjacent(user)) && !issilicon(user))
            await user.put_in_hands(cell);
        else
            await cell.forceMove(drop_location() as atom);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_tool_interactions.dm:21
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stock_parts_power_store_cell && Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            if (cell != null)
            {
                to_chat(user, span_warning(Dm.text("[] already has a power cell!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(
                span_notice(Dm.text("[] inserts \\a [] into [].", user, cell, this)),
                span_notice(Dm.text("You insert [] into [].", cell, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_MAINTS_OPEN)))
        {
            await attack_hand(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_tool_interactions.dm:38
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        await base.emag_act(user, emag_card, __extra);
        if (!Dm.truthy(Dm.bitand(bot_access_flags, BOT_COVER_EMAGGED)))
            return null;
        flick(Dm.text("[]-emagged", base_icon_state), this);
        playsound(this, SFX_SPARKS, 100, FALSE, SHORT_RANGE_SOUND_EXTRARANGE);
        return TRUE;
    }
}
