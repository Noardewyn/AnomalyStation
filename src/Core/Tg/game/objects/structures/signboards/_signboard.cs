// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/signboards/_signboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST5 (D333): the file whole. The foldable wooden sign (a crafting recipe): a pen (or a hand, for the holographic one) writes
// its text through tg's text input, up to 106 characters, past the IC and soft filters; an alt right click with a pen wipes it; a
// wrench (un)secures it, and an unsecured wooden sign shows no text. Its sprite is "sign" written on, "sign_blank" when empty; the
// text is read in its examine. tg also draws the text over the board in a maptext image that a hovering player's client is given
// (MouseEntered, client.images): the server keeps that image and gives it, but the 3D client draws no private image's maptext yet,
// so the text over the board waits for a client change (not-translated.md 213; the brief's stop point, reported to the lead).
// The var block is generated (D111).

// tg: code/game/objects/structures/signboards/_signboard.dm:1-4 (file-local, #undef at :211-213; INVESTIGATE_SIGNBOARD is used by holosign.dm too)
public partial class obj_structure_signboard
{
    internal const string INVESTIGATE_SIGNBOARD = "signboard";
    internal const float SIGNBOARD_WIDTH = ICON_SIZE_X * 3.5f;
    internal const float SIGNBOARD_HEIGHT = ICON_SIZE_Y * 2.5f;
    internal const float MAX_SIGN_LEN = 106;
}

// tg: code/game/objects/structures/signboards/_signboard.dm:6
public partial class obj_structure_signboard : obj_structure
{
    // tg: code/game/objects/structures/signboards/_signboard.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(sign_text))
        {
            Waiting.NoSleep(set_text(sign_text, force: TRUE));
            investigate_log(Dm.text("had its text set on load to \"[]\"", sign_text), INVESTIGATE_SIGNBOARD);
        }
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:37
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref text_image);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:41
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.truthy(is_locked(user)))
            return null;
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WRENCH))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unsecure" : "Secure";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if ((Dm.truthy(edit_by_hand) || held_item is obj_item_pen) && (Dm.truthy(anchored) || Dm.truthy(show_while_unanchored)))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Set Displayed Text";
            if (Dm.truthy(sign_text))
                context[SCREENTIP_CONTEXT_ALT_RMB] = "Clear Sign";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:54
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList ?? Dm.list();
        if (!Dm.truthy(edit_by_hand))
            Dm.append(ref dot, span_notice(I18n.T("You can write on the sign with a <b>pen.</b>")));
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's secured to the floor, you could use a <b>wrench</b> to unsecure and move it.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("It's unsecured, you could use a <b>wrench</b> to secure it in place.")));
        if (Dm.truthy(sign_text))
        {
            Dm.append(ref dot, span_boldnotice(I18n.T("\nIt currently displays the following:")));
            Dm.append(ref dot, span_info(html_encode(sign_text)));
        }
        else
            Dm.append(ref dot, span_notice(I18n.T("\nIt's blank.")));
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:68
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(sign_text) ? "" : "_blank");
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:72
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (Dm.eq(var_name, nameof(sign_text)))
        {
            if (!Dm.truthy(await set_text(var_value, force: TRUE)))
                return FALSE;
            datum_flags = Dm.num(Dm.bitor(datum_flags, DF_VAR_EDITED));
            return TRUE;
        }
        return await base.vv_edit_var(var_name, var_value);
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:80
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_pen)
            return NONE;
        await try_set_text(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:86
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(edit_by_hand) && !Dm.truthy(user.is_holding_item_of_type(typeof(obj_item_pen))))
        {
            balloon_alert(user, I18n.T("need a pen!"));
            return TRUE;
        }
        if (Dm.truthy(await try_set_text(user as mob_living)))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:96
    public virtual async DmTask<object> try_set_text(mob_living user = null)
    {
        object dot = FALSE;
        if (!Dm.truthy(anchored) && !Dm.truthy(show_while_unanchored))
            return FALSE;
        if (Dm.truthy(check_locked(user)))
            return FALSE;
        object new_text = await tgui_input_text(
            user,
            message: I18n.T("What would you like to set this sign's text to?"),
            title: full_capitalize(name),
            @default: sign_text,
            max_length: max_length,
            multiline: TRUE,
            encode: FALSE);
        if (QDELETED(this) || !Dm.truthy(new_text) || Dm.truthy(check_locked(user)))
            return FALSE;
        bool can_bypass = user?.client?.holder != null; // CAN_BYPASS_FILTER(user) (__DEFINES/chat_filter.dm:12)
        DmList filter_result = can_bypass ? null : is_ic_filtered(new_text);
        if (filter_result != null)
        {
            // REPORT_CHAT_FILTER_TO_USER(user, filter_result) (__DEFINES/chat_filter.dm:8)
            to_chat(user, span_warning(I18n.T("The word <b>[]</b> is prohibited: []", html_encode(filter_result[CHAT_FILTER_INDEX_WORD]), html_encode(filter_result[CHAT_FILTER_INDEX_REASON]))));
            return FALSE;
        }
        DmList soft_filter_result = can_bypass ? null : is_soft_ic_filtered(new_text);
        if (soft_filter_result != null)
        {
            if (!Dm.eq(await tgui_alert(user, I18n.T("Your message contains \"[]\". \"[]\", Are you sure you want to say it?", soft_filter_result[CHAT_FILTER_INDEX_WORD], soft_filter_result[CHAT_FILTER_INDEX_REASON]), I18n.T("Soft Blocked Word"), Dm.list("Yes", "No")), "Yes"))
                return FALSE;
            message_admins(Dm.text("[] has passed the soft filter for \"[]\" when writing to the sign at [], they may be using a disallowed term. Sign text: \"[]\"", ADMIN_LOOKUPFLW(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], ADMIN_VERBOSEJMP(this), html_encode(new_text)));
            log_admin_private(Dm.text("[] has passed the soft filter for \"[]\" when writing to the sign at [], they may be using a disallowed term. Sign text: \"[]\"", key_name(user), soft_filter_result[CHAT_FILTER_INDEX_WORD], loc_name(this), new_text));
        }
        if (Dm.truthy(await set_text(new_text)))
        {
            balloon_alert(user, I18n.T("set text"));
            investigate_log(Dm.text("([]) set text to \"[]\"", key_name(user), Dm.truthy(sign_text) ? sign_text : "(none)"), INVESTIGATE_SIGNBOARD);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:128
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        object dot = await base.click_alt_secondary(user);
        if (!Dm.truthy(sign_text) || !Dm.truthy(await can_interact(user)) || !Dm.truthy(await user.can_perform_action(this, NEED_DEXTERITY)))
            return dot;
        if (!Dm.truthy(edit_by_hand) && !Dm.truthy(user.is_holding_item_of_type(typeof(obj_item_pen))))
        {
            balloon_alert(user, I18n.T("need a pen!"));
            return dot;
        }
        if (Dm.truthy(check_locked(user)))
            return dot;
        if (Dm.truthy(await set_text(null)))
        {
            balloon_alert(user, I18n.T("cleared text"));
            investigate_log(Dm.text("([]) cleared the text", key_name(user)), INVESTIGATE_SIGNBOARD);
        }
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:141
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wrench_act(user, tool);
        if (!Dm.truthy(anchored) || !Dm.truthy(check_locked(user)))
            await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:147
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (!Dm.truthy(same_z_layer))
            SET_PLANE_EXPLICIT(text_image, ABOVE_GAME_PLANE, this);
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:152
    public override async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = await base.set_anchored(anchorvalue);
        update_text();
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:156
    public virtual object is_locked(mob user = null)
    {
        // isAdminGhostAI(user) (mob_helpers.dm:357): an admin observer with admin rights (check_rights_for, M10); no mob is one yet
        return locked;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:161
    public virtual object check_locked(mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        object dot = is_locked(user);
        if (Dm.truthy(dot) && !Dm.truthy(silent))
            balloon_alert(user, I18n.T("locked!"));
        return dot;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:166
    public virtual object should_display_text()
    {
        if (QDELETED(this) || !isturf(loc) || !Dm.truthy(sign_text))
            return FALSE;
        if (!Dm.truthy(anchored) && !Dm.truthy(show_while_unanchored))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:173
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (QDELETED(this) || !Dm.truthy(should_display_text()))
            return;
        Dm.combine(usr.client.images, text_image);
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:179
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        base.MouseExited(location, control, @params);
        Dm.remove(usr.client.images, text_image);
    }

    /// Creates [text_image] if it doesn't exist, and sets its maptext to [sign_text]
    // tg: code/game/objects/structures/signboards/_signboard.dm:184
    public virtual void update_text()
    {
        // PROTECTED_PROC(TRUE) // Use set_text instead
        float safe_width = Dm.truthy(bound_width) ? bound_width : ICON_SIZE_X;
        float safe_height = Dm.truthy(bound_height) ? bound_height : ICON_SIZE_Y;
        string text_html = MAPTEXT_GRAND9K(Dm.text("<span style='text-align: center; line-height: 1'>[]</span>", html_encode(sign_text)));
        if (text_image == null)
        {
            text_image = Dm.@new<image>();
            SET_PLANE_EXPLICIT(text_image, ABOVE_GAME_PLANE, this);
            text_image.alpha = 192;
            text_image.loc = this;
            text_image.maptext_x = (SIGNBOARD_WIDTH - safe_width) * -0.5f;
            text_image.maptext_y = safe_height;
            text_image.maptext_width = SIGNBOARD_WIDTH;
            text_image.maptext_height = SIGNBOARD_HEIGHT;
        }
        text_image.maptext = text_html;
    }

    // tg: code/game/objects/structures/signboards/_signboard.dm:200
    public virtual async DmTask<object> set_text(object new_text = null, float? force = null)
    {
        force ??= FALSE;
        object dot = FALSE;
        if (QDELETED(this) || (Dm.truthy(locked) && !Dm.truthy(force)))
            return dot;
        if (!istext(new_text) && new_text != null)
        {
            throw Dm.CRASH(Dm.text("Attempted to set invalid signtext: []", new_text), "code/game/objects/structures/signboards/_signboard.dm", 205);
        }
        dot = TRUE;
        sign_text = trim(new_text, max_length);
        update_text();
        update_appearance();
        await DmTask.CompletedTask;
        return dot;
    }
}
