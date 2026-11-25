// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/spacesuits/plasmamen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: the plasmaman's EVA suit with its built-in extinguisher and the envirosuit helmets (the torch, the
// welding visor, the crayon smile). The helmets' subtypes are var-only, generated (D111).

// tg: code/modules/clothing/spacesuits/plasmamen.dm:3
public partial class obj_item_clothing_suit_space_eva_plasmaman : obj_item_clothing_suit_space_eva
{
    // tg: code/modules/clothing/spacesuits/plasmamen.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        // the \s of DM (the plural after the number) is written out
        Dm.append(ref dot, span_notice(I18n.T("There [] [] extinguisher charge[] left in this suit.", extinguishes_left == 1 ? "is" : "are", extinguishes_left, extinguishes_left == 1 ? "" : "s")));
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:26
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_OCLOTHING)))
        {
            RegisterSignals(user, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_LIVING_IGNITED, SIGNAL_ADDTRAIT(TRAIT_HEAD_ATMOS_SEALED)), nameof(check_fire_state));
            check_fire_state();
        }
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:32
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        UnregisterSignal(user, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_LIVING_IGNITED, SIGNAL_ADDTRAIT(TRAIT_HEAD_ATMOS_SEALED)));
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:36
    [SignalHandler]
    public virtual void check_fire_state(datum source = null)
    {
        // SIGNAL_HANDLER

        if (!ishuman(loc))
            return;

        // This is weird but basically we're calling this proc once the cooldown ends in case our wearer gets set on fire again during said cooldown
        // This is why we're ignoring source and instead checking by loc
        mob_living_carbon_human owner = loc as mob_living_carbon_human;
        if (!Dm.truthy(owner.on_fire) || !Dm.truthy(owner.is_atmos_sealed(additional_flags: PLASMAMAN_PREVENT_IGNITION, check_hands: TRUE)))
            return;

        if (!Dm.truthy(extinguishes_left) || !(extinguish_timer <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;

        extinguishes_left -= 1;
        extinguish_timer = world.time + extinguish_cooldown; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))
        // Check if our (possibly other) wearer is on fire once the cooldown ends
        _addtimer(Dm.@new<datum_callback>(this, nameof(check_fire_state)), extinguish_cooldown, file: "code/modules/clothing/spacesuits/plasmamen.dm", line: 52);
        owner.visible_message(span_warning(I18n.T("[]'s suit automatically extinguishes []!", owner, owner.p_them())), span_warning(I18n.T("Your suit automatically extinguishes you.")));
        owner.extinguish_mob();
        Dm.@new<obj_effect_particle_effect_water>(get_turf(owner));
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:59
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_extinguisher_refill))
            return null;

        if (extinguishes_left == Dm.initial(this, (obj_item_clothing_suit_space_eva_plasmaman p) => p.extinguishes_left))
        {
            to_chat(user, span_notice(I18n.T("The inbuilt extinguisher is full.")));
            return ITEM_INTERACT_BLOCKING;
        }

        extinguishes_left = Dm.initial(this, (obj_item_clothing_suit_space_eva_plasmaman p) => p.extinguishes_left);
        to_chat(user, span_notice(I18n.T("You refill the suit's built-in extinguisher, using up the cartridge.")));
        check_fire_state();
        qdel(tool);
        return ITEM_INTERACT_SUCCESS;
    }
}

//I just want the light feature of helmets
// tg: code/modules/clothing/spacesuits/plasmamen.dm:75
public partial class obj_item_clothing_head_helmet_space_plasmaman : obj_item_clothing_head_helmet_space
{
    // tg: code/modules/clothing/spacesuits/plasmamen.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        visor_toggling();
        update_appearance();
        register_context();
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:115
    [DmParams("loose_hat")]
    public override async DmTask add_stabilizer(float? loose_hat = null)
    {
        loose_hat ??= FALSE;
        await base.add_stabilizer(loose_hat);
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:118
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        context[SCREENTIP_CONTEXT_ALT_LMB] = I18n.T("Toggle Welding Screen");
        if (held_item is obj_item_toy_crayon)
            context[SCREENTIP_CONTEXT_LMB] = I18n.T("Vandalize");

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:125
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await adjust_visor(user as mob_living);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:129
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (actiontype is datum_action_item_action_toggle_welding_screen)
        {
            await adjust_visor(user as mob_living);
            return null;
        }

        return await base.ui_action_click(user, actiontype);
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:136
    public override async DmTask<object> adjust_visor(mob_living user = null)
    {
        object dot = await base.adjust_visor(user);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.truthy(helmet_on))
        {
            to_chat(user, span_notice(I18n.T("Your helmet's torch can't pass through your welding visor!")));
            await set_light_on(FALSE);
            helmet_on = FALSE;
        }
        update_appearance();
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:146
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = (string)Dm.text("[][]", Dm.initial(this, (obj_item_clothing_head_helmet_space_plasmaman p) => p.icon_state), Dm.truthy(helmet_on) ? "-light" : "");
        inhand_icon_state = icon_state;
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:151
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.truthy(up))
            Dm.append(ref dot, visor_icon);
        if (Dm.truthy(smile))
        {
            mutable_appearance smiley = mutable_appearance(icon, smile_state) as mutable_appearance;
            smiley.color = smile_color;
            Dm.append(ref dot, smiley);
        }
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:160
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_toy_crayon))
            return NONE;

        if (Dm.truthy(smile))
        {
            to_chat(user, span_warning(I18n.T("Seems like someone already drew something on []'s visor!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item_toy_crayon crayon = tool as obj_item_toy_crayon;
        to_chat(user, span_notice(I18n.T("You start drawing a smiley face on []'s visor...", this)));
        if (!Dm.truthy(await do_after(user, 2.5f * SECONDS, target: this)))
            return ITEM_INTERACT_BLOCKING;

        smile = TRUE;
        smile_color = crayon.paint_color;
        to_chat(user, I18n.T("You draw a smiley on [] visor.", this));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    ///By the by, helmets have the update_icon_updates_onmob element, so we don't have to call mob.update_worn_head()
    // tg: code/modules/clothing/spacesuits/plasmamen.dm:180
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (!Dm.truthy(isinhands) && !Dm.truthy(up))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/mob/clothing/head/plasmaman_head.dmi"), visor_icon));
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:185
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (!Dm.truthy(isinhands) && Dm.truthy(smile))
        {
            mutable_appearance smiley = mutable_appearance(Dm.resource("icons/mob/clothing/head/plasmaman_head.dmi"), smile_state) as mutable_appearance;
            smiley.color = smile_color;
            Dm.append(ref dot, smiley);
        }
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:192
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        object dot = NONE;
        if (Dm.truthy(smile) && Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_HARD_DECAL)))
        {
            smile = FALSE;
            update_appearance(UPDATE_OVERLAYS);
            dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        dot = Dm.bitor(dot, await base.wash(clean_types, __extra));
        return dot;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:200
    [DmParams("user")]
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        helmet_on = Dm.truthy(helmet_on) ? FALSE : TRUE;
        update_appearance();

        if (Dm.truthy(helmet_on))
        {
            if (!Dm.truthy(up))
            {
                to_chat(user, span_notice(I18n.T("Your helmet's torch can't pass through your welding visor!")));
                await set_light_on(FALSE);
            }
            else
                await set_light_on(TRUE);
        }
        else
            await set_light_on(FALSE);

        await update_item_action_buttons();
        return null;
    }

    // tg: code/modules/clothing/spacesuits/plasmamen.dm:215
    public override async DmTask<object> on_saboteur(datum source = null, object disrupt_duration = null)
    {
        object dot = await base.on_saboteur(source, disrupt_duration);
        if (!Dm.truthy(helmet_on))
            return FALSE;
        helmet_on = FALSE;
        update_appearance();
        return TRUE;
    }
}

// tg: code/modules/clothing/spacesuits/plasmamen.dm:417
public partial class obj_item_clothing_head_helmet_space_plasmaman_clown : obj_item_clothing_head_helmet_space_plasmaman
{
    // tg: code/modules/clothing/spacesuits/plasmamen.dm:424
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/clothing/spacesuits/plasmamen.dm:426", "Initialize: AddElement(/datum/element/swabable, CELL_LINE_TABLE_CLOWN, ...): the cell lines (the sample swab, not ported)");
        return dot;
    }
}
