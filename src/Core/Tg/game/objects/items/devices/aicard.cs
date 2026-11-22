// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/aicard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI4, D326): the intelliCard and its two skins. The screens' emissive glow is DV12's. The var blocks are
// generated (D111).

// tg: code/game/objects/items/devices/aicard.dm:1
public partial class obj_item_aicard : obj_item
{
    // tg: code/game/objects/items/devices/aicard.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI))
            return INITIALIZE_HINT_QDEL;
        ADD_TRAIT(this, TRAIT_CASTABLE_LOC, INNATE_TRAIT);
        return dot;
    }

    // tg: code/game/objects/items/devices/aicard.dm:27
    public override object Destroy(float? force = null)
    {
        if (AI != null)
        {
            AI.ghostize(can_reenter_corpse: FALSE);
            QDEL_NULL(ref AI);
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/aicard.dm:47
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is trying to upload []self into []! That's not going to work out well!", user, user.p_them(), this)));
        await DmTask.CompletedTask;
        return BRUTELOSS;
    }

    // tg: code/game/objects/items/devices/aicard.dm:51
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (AI != null)
        {
            if (await upload_ai(interacting_with, user))
                return ITEM_INTERACT_SUCCESS;
        }
        else
        {
            if (await capture_ai(interacting_with, user))
                return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }

    /// Tries to get an AI from the atom clicked
    // tg: code/game/objects/items/devices/aicard.dm:61
    public virtual async DmTask<bool> capture_ai(atom from_what = null, mob_living user = null)
    {
        await from_what.transfer_ai(AI_TRANS_TO_CARD, user, null, this);
        if (AI == null)
            return false;
        log_silicon(Dm.text("[] carded []", key_name(user), key_name(AI)), Dm.list(this));
        update_appearance();
        await AI.cancel_camera();
        RegisterSignal(AI, COMSIG_MOB_STATCHANGE, nameof(on_ai_stat_change));
        RegisterSignal(AI, COMSIG_ATOM_UPDATE_ICON, nameof(on_ai_icon_update));
        return true;
    }

    /// Tries to upload the AI we have captured to the atom clicked
    // tg: code/game/objects/items/devices/aicard.dm:73
    public virtual async DmTask<bool> upload_ai(atom to_what = null, mob_living user = null)
    {
        mob_living_silicon_ai old_ai = AI;
        await to_what.transfer_ai(AI_TRANS_FROM_CARD, user, AI, this);
        if (AI != null)
            return false;
        log_combat(user, old_ai, "uploaded", this, Dm.text("to [].", to_what));
        update_appearance();
        await old_ai.cancel_camera();
        UnregisterSignal(old_ai, COMSIG_MOB_STATCHANGE);
        UnregisterSignal(old_ai, COMSIG_ATOM_UPDATE_ICON);
        return true;
    }

    // tg: code/game/objects/items/devices/aicard.dm:85
    public virtual void on_ai_stat_change(datum source = null, object new_stat = null, object old_stat = null)
    {
        if (Dm.eq(new_stat, DEAD) || Dm.eq(old_stat, DEAD))
            update_appearance();
    }

    // tg: code/game/objects/items/devices/aicard.dm:90
    public virtual void on_ai_icon_update(datum source = null)
    {
        update_appearance();
    }

    // tg: code/game/objects/items/devices/aicard.dm:94
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);

        if (AI == null)
        {
            Waiting.NoSleep(set_light(0));
            return dot;
        }

        Waiting.NoSleep(set_light(0.5f, 0.5f, LIGHT_COLOR_FAINT_CYAN));
        return dot;
    }

    // tg: code/game/objects/items/devices/aicard.dm:103
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        if (AI != null)
            name = Dm.text("[] - []", Dm.initial(GetType(), (atom a) => a.name), AI.name);
        else
            name = Dm.str(Dm.initial(GetType(), (atom a) => a.name));
        return dot;
    }

    // tg: code/game/objects/items/devices/aicard.dm:110
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    //Support for different displays
    // tg: code/game/objects/items/devices/aicard.dm:115
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (AI == null)
            return dot;

        string card_display = Dm.str(AI.display_icon_override ?? "ai");

        if (Dm.eq(AI.stat, DEAD))
            card_display = card_display + "_dead";

        if (!Dm.truthy(icon_exists(icon, card_display))) // !(card_display in icon_states(icon))
        {
            if (Dm.eq(AI.stat, DEAD))
                card_display = "ai_dead";
            else
                card_display = "ai";
        }

        Dm.append(ref dot, mutable_appearance(icon, card_display));
        // DEVIATION(DV12): . += emissive_appearance(icon, card_display, src, alpha = src.alpha)

        if (Dm.truthy(AI.control_disabled))
        {
            string indicator_state = Dm.text("[]-off", base_icon_state);
            Dm.append(ref dot, mutable_appearance(icon, indicator_state));
            // DEVIATION(DV12): . += emissive_appearance(icon, indicator_state, src, alpha = src.alpha)
        }

        if (!Dm.truthy(AI.control_disabled))
        {
            string indicator_state = Dm.text("[]-on", base_icon_state);
            Dm.append(ref dot, mutable_appearance(icon, indicator_state));
            // DEVIATION(DV12): . += emissive_appearance(icon, indicator_state, src, alpha = src.alpha)
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/aicard.dm:143
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/devices/aicard.dm:146
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Intellicard", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/devices/aicard.dm:152
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        if (AI != null)
        {
            data["name"] = AI.name;
            data["laws"] = AI.laws.get_law_list(include_zeroth: TRUE, render_html: FALSE);
            data["health"] = (Dm.num(AI.health) + 100) / 2;
            data["wireless"] = Dm.truthy(AI.control_disabled) ? FALSE : TRUE;
            data["radio"] = AI.radio_enabled;
            data["isDead"] = Dm.eq(AI.stat, DEAD) ? TRUE : FALSE;
            data["isBraindead"] = AI.client != null ? FALSE : TRUE;
        }
        data["wiping"] = flush;
        return data;
    }

    // tg: code/game/objects/items/devices/aicard.dm:165
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (Dm.str(action))
        {
            case "wipe":
                if (Dm.truthy(flush))
                    flush = FALSE;
                else
                {
                    object confirm = await tgui_alert(usr as mob, "Are you sure you want to wipe this card's memory?", name, Dm.list("Yes", "No"));
                    if (Dm.eq(confirm, "Yes") && !Dm.truthy(await base.ui_act(action, @params, ui, state)))
                    {
                        flush = TRUE;
                        Waiting.Detach(wipe_ai()); // set waitfor = FALSE
                    }
                }
                dot = TRUE;
                break;
            case "wireless":
                AI.set_control_disabled(Dm.truthy(AI.control_disabled) ? FALSE : TRUE);
                to_chat(AI, span_warning(I18n.T("[]'s wireless port has been []!", this, Dm.truthy(AI.control_disabled) ? "disabled" : "enabled")));
                dot = TRUE;
                break;
            case "radio":
                AI.radio_enabled = Dm.truthy(AI.radio_enabled) ? FALSE : TRUE;
                to_chat(AI, span_warning(I18n.T("Your Subspace Transceiver has been []!", Dm.truthy(AI.radio_enabled) ? "enabled" : "disabled")));
                dot = TRUE;
                break;
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/items/devices/aicard.dm:188
    public virtual async DmTask wipe_ai()
    {
        if (AI != null && AI.loc == this)
        {
            to_chat(AI, span_userdanger(I18n.T("YOUR SYSTEM FILES ARE BEING WIPED!")));
            while (AI != null && !Dm.eq(AI.stat, DEAD) && Dm.truthy(flush))
            {
                await AI.adjust_oxy_loss(5);
                await AI.updatehealth();
                await Dm.sleep(0.5f * SECONDS);
            }
            flush = FALSE;
        }
    }

    // tg: code/game/objects/items/devices/aicard.dm:198
    public override async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        await base.used_in_craft(result, current_recipe);
        if (AI == null || result is not obj_item_aicard new_card)
            return;
        await AI.forceMove(new_card);
        new_card.AI = AI;
        new_card.update_appearance();
        AI = null;
    }
}

// Special Cards

// tg: code/game/objects/items/devices/aicard.dm:32
public partial class obj_item_aicard_aitater : obj_item_aicard
{
    // tg: code/game/objects/items/devices/aicard.dm:210
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/aicard.dm:214
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        await base.update_overlays(__extra);
        DmList dot = Dm.list();

        if (AI == null)
            return dot;

        string face_state = Dm.text("[][]", base_icon_state, Dm.eq(AI.stat, DEAD) ? "-404" : "-full");
        Dm.append(ref dot, mutable_appearance(icon, face_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, face_state, src, alpha = src.alpha)

        string indicator_state = Dm.text("[][]", base_icon_state, Dm.truthy(AI.control_disabled) ? "-off" : "-on");
        Dm.append(ref dot, mutable_appearance(icon, indicator_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, indicator_state, src, alpha = src.alpha)
        return dot;
    }
}

// tg: code/game/objects/items/devices/aicard.dm:39
public partial class obj_item_aicard_aispook : obj_item_aicard
{
    // tg: code/game/objects/items/devices/aicard.dm:230
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.str(base_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/aicard.dm:234
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        await base.update_overlays(__extra);
        DmList dot = Dm.list();

        if (AI == null)
            return dot;

        string face_state = Dm.text("[][]", base_icon_state, Dm.eq(AI.stat, DEAD) ? "-404" : "-full");
        Dm.append(ref dot, mutable_appearance(icon, face_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, face_state, src, alpha = src.alpha)

        string indicator_state = Dm.text("[][]", base_icon_state, Dm.truthy(AI.control_disabled) ? "-off" : "-on");
        Dm.append(ref dot, mutable_appearance(icon, indicator_state));
        // DEVIATION(DV12): . += emissive_appearance(icon, indicator_state, src, alpha = src.alpha)
        return dot;
    }
}
