// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signboards/holosign.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the holographic signboard and is_color_dark_with_saturation translated (gaps ST5, D333); its circuit component
// (/obj/item/circuit_component/holo_signboard, and the USB port the sign makes for it) waits for the integrated circuits (wiremod
// is inert, not-translated.md 213). The holographic sign is written by hand, lit teal while it shows text, recoloured through tg's
// colour picker on a right click (greyscale sprite tinted; too dark a colour refused), locked to the name on an ID card swiped on
// it, and an emag shorts its lock (its "_emag" sprite). Its text over the board is the signboard's (`_signboard.cs`: the 3D client
// draws no private image's maptext yet). The var blocks are generated (D111).

// tg: code/game/objects/structures/signboards/holosign.dm:1
public partial class obj_structure_signboard_holosign : obj_structure_signboard
{
    // tg: code/game/objects/structures/signboards/holosign.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(current_color))
            Waiting.Detach(set_color(current_color));
        Dm.Deferred("code/game/objects/structures/signboards/holosign.dm:25", "Initialize: AddComponent(/datum/component/usb_port, list(/obj/item/circuit_component/holo_signboard)): the integrated circuits (wiremod, inert)");
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:29
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        object locked = is_locked(user);
        if (held_item is obj_item_card_emag)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Short Out Locking Mechanisms";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        else if (!Dm.truthy(locked) && held_item?.GetID() is obj_item_usb_cable)
            context[SCREENTIP_CONTEXT_LMB] = "Connect USB Cable";
        else if (!Dm.truthy(locked) && held_item?.GetID() is obj_item_card_id)
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(registered_owner) ? "Remove ID Lock" : "Lock To ID";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        if (!Dm.truthy(locked))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Set Sign Color";
            dot = CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:44
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        base_icon_state = Dm.truthy(current_color) ? Dm.text("[]_greyscale", Dm.initial(this, (atom p) => p.base_icon_state)) : Dm.initial(this, (atom p) => p.base_icon_state);
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            icon_state += "_emag";
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:50
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            Dm.append(ref dot, span_warning(I18n.T("<br>Its locking mechanisms appear to be shorted out!")));
        else if (Dm.truthy(registered_owner))
            Dm.append(ref dot, span_info(I18n.T("<br>It is locked to the ID of [].", span_name(Dm.str(registered_owner)))));
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:57
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (Dm.truthy(sign_text))
            Dm.Deferred("code/game/objects/structures/signboards/holosign.dm:60", "update_overlays: emissive_appearance(icon, \"holographic_sign_e\", src), the text's glow in the dark (DEVIATION DV12)");
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:62
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, "color") || Dm.eq(var_name, nameof(current_color)))
        {
            Waiting.Detach(set_color(var_value));
            datum_flags = Dm.num(Dm.bitor(datum_flags, DF_VAR_EDITED));
            return TRUE;
        }
        return await base.vv_edit_var(var_name, var_value);
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:69
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        obj_item item = tool as obj_item;
        obj_item_card_id id = item?.GetID() as obj_item_card_id;
        if (id == null || !Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return NONE;

        string trimmed_id_name = trimtext(id.registered_name);
        if (!Dm.truthy(trimmed_id_name))
        {
            balloon_alert(user, I18n.T("no name on id!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            balloon_alert(user, I18n.T("lock shorted out!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(registered_owner))
        {
            if (!Dm.truthy(check_locked(user)))
            {
                registered_owner = null;
                balloon_alert(user, I18n.T("id lock removed"));
                investigate_log(Dm.text("([]) removed id lock", key_name(user)), INVESTIGATE_SIGNBOARD);
            }
        }
        else
        {
            registered_owner = trimmed_id_name;
            balloon_alert(user, I18n.T("locked to id"));
            investigate_log(Dm.text("([]) added id lock for \"[]\"", key_name(user), registered_owner), INVESTIGATE_SIGNBOARD);
        }
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:94
    public override object is_locked(mob user = null)
    {
        object dot = base.is_locked(user);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(registered_owner) && isliving(user))
        {
            obj_item_card_id id = ((mob_living)user).get_idcard() as obj_item_card_id;
            if (id == null || QDELING(id))
                return TRUE;
            return Dm.num(!Dm.truthy(cmptext(trimtext(id.registered_name), registered_owner)));
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:104
    public override async DmTask<object> set_text(object new_text = null, float? force = null)
    {
        object dot = await base.set_text(new_text, force);
        await set_light(l_on: Dm.num(Dm.truthy(sign_text)));
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:108
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (Dm.truthy(await try_set_color(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:115
    public virtual async DmTask<object> try_set_color(mob user = null)
    {
        object dot = TRUE;
        if (!Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return FALSE;
        if (Dm.truthy(check_locked(user)))
            return dot;
        object new_color = sanitize_color(await tgui_color_picker(user, I18n.T("Set Sign Color"), full_capitalize(name), current_color));
        if (Dm.truthy(new_color) && Dm.truthy(is_color_dark_with_saturation(new_color, 25)))
        {
            balloon_alert(user, I18n.T("color too dark!"));
            return dot;
        }
        if (Dm.truthy(check_locked(user)))
            return dot;
        Waiting.Detach(set_color(new_color));
        if (Dm.truthy(new_color))
        {
            balloon_alert(user, I18n.T("set color to []", new_color));
            investigate_log(Dm.text("([]) set the color to []", key_name(user), Dm.truthy(new_color) ? new_color : "(none)"), INVESTIGATE_SIGNBOARD);
        }
        else
        {
            balloon_alert(user, I18n.T("unset color"));
            investigate_log(Dm.text("([]) cleared the color", key_name(user)), INVESTIGATE_SIGNBOARD);
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:135
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        playsound(this, SFX_SPARKS, vol: 100, vary: TRUE, extrarange: SHORT_RANGE_SOUND_EXTRARANGE);
        do_sparks(3, cardinal_only: FALSE, source: this);
        balloon_alert(user, I18n.T("lock broken"));
        investigate_log(Dm.text("was emagged by [] (previous owner: [])", key_name(user), Dm.truthy(registered_owner) ? registered_owner : "(none)"), INVESTIGATE_SIGNBOARD);
        registered_owner = null;
        obj_flags = Dm.num(Dm.bitor(obj_flags, EMAGGED));
        update_appearance();
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:146
    public virtual object sanitize_color(object color = null)
    {
        object dot = sanitize_hexcolor(color);
        if (!Dm.truthy(dot) || Dm.eq(dot, COLOR_BLACK))
            return null;
        return dot;
    }

    // tg: code/game/objects/structures/signboards/holosign.dm:151
    public virtual async DmTask set_color(object new_color = null)
    {
        new_color = sanitize_color(new_color);
        if (!Dm.truthy(new_color))
        {
            current_color = null;
            await remove_atom_colour(FIXED_COLOUR_PRIORITY);
        }
        else
        {
            current_color = new_color;
            await add_atom_colour(new_color, FIXED_COLOUR_PRIORITY);
        }
        await set_light(l_color: Dm.truthy(current_color) ? current_color : (object)Dm.initial(this, (atom p) => p.light_color));
        update_appearance();
    }
}

public static partial class GlobalProcs
{
    /// Given a color in the format of "#RRGGBB", will return if the color
    /// is dark. Value is mixed with Saturation and Brightness from HSV.
    // tg: code/game/objects/structures/signboards/holosign.dm:237
    public static object is_color_dark_with_saturation(object color = null, float? threshold = null)
    {
        threshold ??= 25;
        DmList hsl = rgb2num(Dm.str(color), COLORSPACE_HSL);
        return Dm.num(Dm.num(hsl[3]) < threshold);
    }
}
