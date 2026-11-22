// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/extinguisher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b (the fire extinguisher): the extinguisher cabinet, its hand, item and wrench paths, its icon and door overlay. M5 slice 7: wrench_act,
// atom_break and atom_deconstruct. Left for their milestones (rows in docs/ai/specs/live-partial.txt): contents_explosion
// (explosions, M5x); attack_tk since gaps ST5 (telekinesis takes the extinguisher out or toggles the door). The cabinet frame
// (/obj/item/wallframe/extinguisher_cabinet) and the directional helpers (MAPPING_DIRECTIONAL_HELPERS) are var-only. The var
// block is generated (D111).

// tg: code/game/objects/structures/extinguisher.dm:1
[DmPath("/obj/structure/extinguisher_cabinet")]
public partial class obj_structure_extinguisher_cabinet : obj_structure
{
    // tg: code/game/objects/structures/extinguisher.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(mapload))
            opened = TRUE;
        else
        {
            stored_extinguisher = Dm.@new<obj_item_extinguisher>(this);
            Waiting.NoSleep(find_and_mount_on_atom());
        }
        update_appearance(UPDATE_ICON);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:25
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);

        if (held_item == null)
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(opened) ? "Close" : "Open";
            if (stored_extinguisher != null)
                context[SCREENTIP_CONTEXT_LMB] = "Take extinguisher"; //Yes, this shows whether or not it's open! Extinguishers are taken immediately on LMB click when closed
            return CONTEXTUAL_SCREENTIP_SET;
        }

        if (stored_extinguisher != null)
            return NONE;

        if (Dm.eq(held_item.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Disassemble cabinet";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (istype(held_item, typeof(obj_item_extinguisher)) && Dm.truthy(opened))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Insert extinguisher";
            return CONTEXTUAL_SCREENTIP_SET;
        }

        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:46
    public override object Destroy(float? force = null)
    {
        if (stored_extinguisher != null)
            QDEL_NULL(ref stored_extinguisher);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/extinguisher.dm:51  (M5x slice 1: explosions)
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        if (!Dm.truthy(stored_extinguisher))
            return;

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, stored_extinguisher);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, stored_extinguisher);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, stored_extinguisher);
                break;
        }
    }

    // tg: code/game/objects/structures/extinguisher.dm:63
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == stored_extinguisher)
        {
            stored_extinguisher = null;
            update_appearance(UPDATE_ICON);
        }
        return null;
    }

    // tg: code/game/objects/structures/extinguisher.dm:81
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (iscyborg(user) || isalien(user))
            return NONE;
        if (Dm.truthy(user.combat_mode))
            return ITEM_INTERACT_SKIP_TO_ATTACK;
        if (istype(tool, typeof(obj_item_extinguisher)))
        {
            if (stored_extinguisher != null || !Dm.truthy(opened))
            {
                toggle_cabinet(user);
                return ITEM_INTERACT_SUCCESS;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            stored_extinguisher = (obj_item_extinguisher)tool;
            balloon_alert(user, I18n.T("extinguisher stored"));
            update_appearance(UPDATE_ICON);
            return ITEM_INTERACT_SUCCESS;
        }
        toggle_cabinet(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/extinguisher.dm:99
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (iscyborg(user) || isalien(user))
            return dot;
        if (stored_extinguisher != null)
        {
            await user.put_in_hands(stored_extinguisher);
            user.balloon_alert(user, I18n.T("extinguisher removed"));
            if (!Dm.truthy(opened))
            {
                opened = 1;
                playsound(loc, Dm.resource("sound/machines/click.ogg"), 15, TRUE, -3);
                update_appearance(UPDATE_ICON);
            }
        }
        else
            toggle_cabinet(user);
        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:115
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await ((mob_living)user).can_perform_action(this, Dm.bitor(NEED_DEXTERITY, Dm.bitor(NEED_HANDS, ALLOW_RESTING)))))
            return await base.attack_hand_secondary(user, modifiers);
        toggle_cabinet(user);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/extinguisher.dm:121  (gaps ST5)
    public override async DmTask<object> attack_tk(mob user = null)
    {
        object dot = COMPONENT_CANCEL_ATTACK_CHAIN;
        if (stored_extinguisher != null)
        {
            await stored_extinguisher.forceMove(loc);
            to_chat(user, span_notice(I18n.T("You telekinetically remove [] from [].", stored_extinguisher, this)));
            stored_extinguisher = null;
            opened = TRUE;
            playsound(loc, Dm.resource("sound/machines/click.ogg"), 15, TRUE, -3);
            update_appearance(UPDATE_ICON);
            return dot;
        }
        toggle_cabinet(user);
        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:134
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/extinguisher.dm:137
    public virtual void toggle_cabinet(mob user = null)
    {
        if (Dm.truthy(opened) && Dm.truthy(broken))
            user.balloon_alert(user, I18n.T("it's broken!"));
        else
        {
            playsound(loc, Dm.resource("sound/machines/click.ogg"), 15, TRUE, -3);
            opened = Dm.not(opened);
            update_appearance(UPDATE_ICON);
        }
    }

    // tg: code/game/objects/structures/extinguisher.dm:145
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = "extinguisher";

        if (stored_extinguisher == null)
            icon_state += "";
        else if (istype(stored_extinguisher, typeof(obj_item_extinguisher_mini)))
            icon_state += "_mini";
        else if (istype(stored_extinguisher, typeof(obj_item_extinguisher_advanced)))
            icon_state += "_advanced";
        else if (istype(stored_extinguisher, typeof(obj_item_extinguisher_crafted)))
            icon_state += "_crafted";
        else if (istype(stored_extinguisher, typeof(obj_item_extinguisher)))
            icon_state += "_default";

        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/extinguisher.dm:161
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, mutable_appearance(icon, Dm.truthy(opened) ? "door_open" : "door"));
        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:68  (M5 slice 7)
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (stored_extinguisher != null)
        {
            balloon_alert(user, I18n.T("must be empty!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert(user, I18n.T("deconstructing cabinet..."));
        tool.play_tool_sound(this);
        if (!Dm.truthy(await tool.use_tool(this, user, 6 * SECONDS)))
            return ITEM_INTERACT_BLOCKING;
        playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/objects/structures/extinguisher.dm:76
        user.balloon_alert(user, I18n.T("cabinet deconstructed"));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/extinguisher.dm:165  (M5 slice 7)
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
        {
            broken = 1;
            opened = 1;
            if (stored_extinguisher != null)
            {
                await stored_extinguisher.forceMove(loc);
                stored_extinguisher = null;
            }
            update_appearance(UPDATE_ICON);
        }
        return dot;
    }

    // tg: code/game/objects/structures/extinguisher.dm:176  (M5 slice 7)
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (Dm.truthy(disassembled))
            Dm.@new<obj_item_wallframe_extinguisher_cabinet>(loc);
        else
            Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        if (stored_extinguisher != null)
        {
            await stored_extinguisher.forceMove(loc);
            stored_extinguisher = null;
        }
    }
}
