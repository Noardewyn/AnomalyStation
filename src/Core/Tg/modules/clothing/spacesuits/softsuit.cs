// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/spacesuits/softsuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the EVA helmet's examine and item_interaction translated (M4c slice 2: Meta's EVA suit and helmet). The NASA
// voidsuits and the EVA suit are var-only (live with their parents). Gaps CL1 (D333): the emergency suit's hit_reaction (the suit
// tears and loses its seal). The var blocks are generated.

// tg: code/modules/clothing/spacesuits/softsuit.dm:39
public partial class obj_item_clothing_head_helmet_space_eva : obj_item_clothing_head_helmet_space
{
    // tg: code/modules/clothing/spacesuits/softsuit.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("You can start constructing a critter sized mecha with a [].", span_bold(I18n.T("cyborg leg")))));
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/softsuit.dm:58
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_bodypart_leg_left_robot) && !(tool is obj_item_bodypart_leg_right_robot))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (ismob(loc))
        {
            user.balloon_alert(user, I18n.T("drop the helmet first!"));
            return ITEM_INTERACT_BLOCKING;
        }

        user.balloon_alert(user, I18n.T("leg attached"));
        Dm.@new<obj_item_bot_assembly_vim>(loc);
        qdel(tool);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/clothing/spacesuits/softsuit.dm:81
public partial class obj_item_clothing_suit_space_fragile : obj_item_clothing_suit_space
{
    // tg: code/modules/clothing/spacesuits/softsuit.dm:94
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        if (!Dm.truthy(torn) && prob(50))
        {
            to_chat(owner, span_warning(I18n.T("[] tears from the damage, breaking the airtight seal!", this)));
            clothing_flags = Dm.bitand(clothing_flags, Dm.bitnot(STOPSPRESSUREDAMAGE));
            name = I18n.T("torn [].", this);
            desc = I18n.T("A bulky suit meant to protect the user during emergency situations, at least until someone tore a hole in the suit.");
            torn = TRUE;
            playsound(loc, Dm.resource("sound/items/weapons/slashmiss.ogg"), 50, TRUE);
            playsound(loc, Dm.resource("sound/effects/refill.ogg"), 50, TRUE);
        }
        return null;
    }
}
