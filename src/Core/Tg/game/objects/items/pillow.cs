// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/pillow.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the file (the pillow lance with the spears, gaps WP1). The pillow fight: a pillow bonks a carbon for stamina damage with tg's soft or hard
// hit sound and its pillow_hit puff, costs its swinger stamina, smothers a lying (or strangled) victim with a right click, takes a
// sandstone brick (heavier, harder hits) and loses or regains its tag (alt-click; the tag names the last fighter), its sprite
// (`pillow_N` with or without `_t`) and in-hand state following; the random pillows pick a pattern and now and then hide a brick.
// The pillow suit bumps into people with its built-in pillow (datums/components/bumpattack.dm), and the mattress shield fortifies
// (a red pulsing outline, slower, harder to knock down). The pillow lance (gaps WP1, with weaponry/melee/spear.dm): a spear of
// stamina damage that, wielded and run in one direction for two tiles, charges (faster, costing its bearer stamina) and impales
// whom it bumps into for stamina by the tiles charged. The var blocks are generated (D111).

//Pillow and pillow related items
// tg: code/game/objects/items/pillow.dm:4
public partial class obj_item_pillow : obj_item
{
    // tg: code/game/objects/items/pillow.dm:1  #define FORTIFY_FILTER "ANGRY_GLOW" (#undef'd at the file's end; the mattress shield's)

    ///change the description based on the pillow tag
    // tg: code/game/objects/items/pillow.dm:16  var/static/tag_desc
    public static ref string tag_desc => ref TgState.Current.obj_item_pillow__tag_desc;

    // tg: code/game/objects/items/pillow.dm:43  var/static/list/slapcraft_recipe_list (Initialize's)
    private static DmList slapcraft_recipe_list => TgState.Current.obj_item_pillow_Initialize__slapcraft_recipe_list ??= Dm.list(
        typeof(datum_crafting_recipe_pillow_suit), typeof(datum_crafting_recipe_pillow_hood)
    );

    // tg: code/game/objects/items/pillow.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (pillow_trophy == null)
            pillow_trophy = Dm.@new<obj_item_clothing_neck_pillow_tag>(this);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", 5),
            Dm.named("force_wielded", 10)));
        Waiting.NoSleep(AddElement(typeof(datum_element_disarm_attack)));
        RegisterSignal(this, COMSIG_ITEM_CAN_DISARM_ATTACK, nameof(can_disarm_attack));

        Waiting.NoSleep(AddElement(
            typeof(datum_element_slapcrafting),
            Dm.named("slapcraft_recipes", slapcraft_recipe_list)));
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:52
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref pillow_trophy);
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:56
    public virtual object can_smother(mob_living victim = null, mob_living user = null)
    {
        return Dm.num(victim.body_position == LYING_DOWN || (user.grab_state >= GRAB_AGGRESSIVE && user.pulling == victim));
    }

    // tg: code/game/objects/items/pillow.dm:59
    [SignalHandler]
    public virtual object can_disarm_attack(datum source = null, mob_living victim = null, mob_living user = null, object message = null)
    {
        if (Dm.truthy(can_smother(victim, user)))
            return COMPONENT_BLOCK_ITEM_DISARM_ATTACK;
        return null;
    }

    // tg: code/game/objects/items/pillow.dm:64
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        if (!iscarbon(target_mob))
            return dot;
        if (Dm.truthy(bricked) || HAS_TRAIT(this, TRAIT_WIELDED))
            hit_sound = Dm.resource("sound/items/pillow/pillow_hit2.ogg");
        else
            hit_sound = Dm.resource("sound/items/pillow/pillow_hit.ogg");
        await user.apply_damage(stam_recoil, STAMINA); //Had to be done so one person cannot keep multiple people stam critted
        last_fighter = user;
        Dm.@new<obj_effect_temp_visual_pillow_hit>(get_turf(target_mob));
        playsound(user, hit_sound, 80); //the basic 50 vol is barely audible
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:77
    public override async DmTask<object> attack_secondary(mob_living victim = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attack_secondary(victim, user, modifiers, attack_modifiers);
        if (!(victim is mob_living_carbon victim_carbon)) // tg's parameter is mob/living/carbon/victim (rules 3.3)
            return dot;
        if (Dm.truthy(victim_carbon.is_mouth_covered()) || !Dm.truthy(victim_carbon.get_bodypart(BODY_ZONE_HEAD)))
            return dot;
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_notice(I18n.T("You can't bring yourself to harm []", victim)));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (Dm.truthy(can_smother(victim, user)))
        {
            user.visible_message(I18n.T("[] starts to smother []!", user, victim), span_notice(I18n.T("You begin smothering []!", victim)), vision_distance: COMBAT_MESSAGE_RANGE);
            Waiting.Detach(smothering(user as mob_living_carbon, victim_carbon));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:91
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(bricked) && tool is obj_item_stack_sheet_mineral_sandstone)
        {
            obj_item_stack_sheet_mineral_sandstone brick = (obj_item_stack_sheet_mineral_sandstone)tool;
            balloon_alert(user, I18n.T("inserting brick..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
                return ITEM_INTERACT_BLOCKING;
            if (!Dm.truthy(await brick.use(1)))
            {
                balloon_alert(user, I18n.T("not enough bricks!"));
                return ITEM_INTERACT_BLOCKING;
            }
            balloon_alert(user, I18n.T("bricked!"));
            await become_bricked();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_clothing_neck_pillow_tag)
        {
            if (pillow_trophy != null)
            {
                balloon_alert(user, I18n.T("tag is intact."));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            pillow_trophy = (obj_item_clothing_neck_pillow_tag)tool;
            balloon_alert(user, I18n.T("honor reclaimed!"));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/items/pillow.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(bricked))
            Dm.append(ref dot, span_info(I18n.T("[] feel[] unnaturally heavy.", p_They(), p_s())));
        if (pillow_trophy != null)
            Dm.append(ref dot, span_notice(I18n.T("Alt-click to remove the tag!")));
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:124
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(user.can_hold_items(this)))
            return CLICK_ACTION_BLOCKING;
        if (pillow_trophy == null)
        {
            balloon_alert(user, I18n.T("no tag!"));
            return CLICK_ACTION_BLOCKING;
        }
        balloon_alert(user, I18n.T("removing tag..."));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, this)))
            return CLICK_ACTION_BLOCKING;
        if (Dm.truthy(last_fighter))
            pillow_trophy.desc = I18n.T("A pillow tag taken from [] after a gruesome pillow fight.", last_fighter);
        await user.put_in_hands(pillow_trophy);
        pillow_trophy = null;
        balloon_alert(user, I18n.T("tag removed"));
        playsound(user, Dm.resource("sound/items/poster/poster_ripped.ogg"), 50);
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/pillow.dm:142
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        if (pillow_trophy == null)
        {
            desc = I18n.T("A soft and fluffy pillow. You can smack someone with this! []", tag_desc);
            icon_state = Dm.text("pillow_[]", variation);
            inhand_icon_state = "pillow_no_t";
        }
        else
        {
            desc = I18n.T("A soft and fluffy pillow. You can smack someone with this!");
            icon_state = Dm.text("pillow_[]_t", variation);
            inhand_icon_state = "pillow_t";
        }
        return dot;
    }

    /// Puts a brick inside the pillow, increasing its damage
    // tg: code/game/objects/items/pillow.dm:154
    public virtual async DmTask become_bricked()
    {
        bricked = TRUE;
        datum_component_two_handed two_handed = GetComponent(typeof(datum_component_two_handed)) as datum_component_two_handed;
        if (two_handed != null)
            await AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", Dm.num(two_handed.force_unwielded) + 5), Dm.named("force_wielded", Dm.num(two_handed.force_wielded) + 10));
        force += 5;
        update_appearance();
    }

    /// Smothers the victim while the do_after succeeds and the victim is laying down or being strangled
    // tg: code/game/objects/items/pillow.dm:163
    public virtual async DmTask smothering(mob_living_carbon user = null, mob_living_carbon victim = null)
    {
        while (victim != null)
        {
            if (!Dm.truthy(can_smother(victim, user)))
                break;
            if (!Dm.truthy(await do_after(user, 1 * SECONDS, victim)))
                break;
            victim.losebreath += 1;
        }
        victim.visible_message(I18n.T("[] manages to escape being smothered!", victim), span_notice(I18n.T("You break free!")), vision_distance: COMBAT_MESSAGE_RANGE);
    }
}

// tg: code/game/objects/items/pillow.dm:172
public partial class obj_item_pillow_random : obj_item_pillow
{
    // tg: code/game/objects/items/pillow.dm:174
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        variation = rand(1, 4);
        icon_state = Dm.text("pillow_[]_t", variation);
        //random pillows spawn bricked sometimes, fuck you
        if (prob(1))
            Waiting.NoSleep(become_bricked());
        return dot;
    }
}

// tg: code/game/objects/items/pillow.dm:185
public partial class obj_item_clothing_suit_pillow_suit : obj_item_clothing_suit
{
    // tg: code/game/objects/items/pillow.dm:203
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        unstoppably_plushed = Dm.@new<obj_item_pillow_suit_pillow>(this);
        Waiting.NoSleep(AddComponent(typeof(datum_component_bumpattack), Dm.named("proxy_weapon", unstoppably_plushed), Dm.named("valid_inventory_slot", ITEM_SLOT_OCLOTHING)));
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:208
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref unstoppably_plushed);
        return dot;
    }
}

// tg: code/game/objects/items/pillow.dm:250
public partial class obj_item_spear_pillow : obj_item_spear
{
    // tg: code/game/objects/items/pillow.dm:276
    public override object on_wield(obj_item source = null, mob_living_carbon user = null)
    {
        object dot = base.on_wield(source, user);
        RegisterSignal(user, COMSIG_MOB_CLIENT_MOVED, nameof(check_move));
        RegisterSignal(user, COMSIG_LIVING_MOB_BUMP, nameof(impale));
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:281
    public override object on_unwield(obj_item source = null, mob_living_carbon user = null)
    {
        object dot = base.on_unwield(source, user);
        reset_user(user);
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:285
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        reset_user(user);
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:289
    public virtual void reset_user(mob user = null)
    {
        UnregisterSignal(user, Dm.list(COMSIG_MOB_CLIENT_MOVED, COMSIG_LIVING_MOB_BUMP));
        reset_charge(user as mob_living);
    }

    // tg: code/game/objects/items/pillow.dm:293
    public virtual void reset_charge(mob_living user = null)
    {
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_lance_charge));
        current_tile_charge = Dm.initial(this, (obj_item_spear_pillow p) => p.current_tile_charge);
        current_direction = user.dir;
        charging = FALSE;
    }

    // tg: code/game/objects/items/pillow.dm:299
    [SignalHandler]
    public virtual void check_move(mob_living user = null)
    {
        if (!Dm.eq(current_direction, user.dir) || world.time > Dm.num(last_charge_move) + 0.5f * SECONDS)
            reset_charge(user);
        else
        {
            if (Dm.num(current_tile_charge) >= min_tile_charge)
            {
                if (!Dm.truthy(charging))
                {
                    Waiting.NoSleep(user.add_movespeed_modifier(typeof(datum_movespeed_modifier_lance_charge)));
                    user.balloon_alert(user, I18n.T("charging!"));
                    charging = TRUE;
                }
                Waiting.NoSleep(user.adjust_stamina_loss(stamina_per_tile));
            }
            current_tile_charge = Dm.num(current_tile_charge) + 1;
        }
        last_charge_move = world.time;
    }

    // tg: code/game/objects/items/pillow.dm:314
    [SignalHandler]
    public virtual void impale(mob_living user = null, mob_living target = null)
    {
        if (Dm.num(current_tile_charge) < min_tile_charge)
        {
            user.balloon_alert(user, I18n.T("too slow!"));
            return;
        }
        Waiting.Detach(target.attackby(this, user));
        Waiting.NoSleep(target.adjust_stamina_loss(damage_boost_per_tile * Dm.num(current_tile_charge)));
        playsound(user, Dm.resource("sound/effects/bang.ogg"), 40, TRUE);
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_lance_charge));
    }

    // tg: code/game/objects/items/pillow.dm:325
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await base.afterattack(target, user, modifiers, attack_modifiers);

        Dm.@new<obj_effect_temp_visual_pillow_hit>(get_turf(target));
    }
}

// tg: code/game/objects/items/pillow.dm:330
public partial class obj_item_shield_mattress : obj_item_shield
{
    // tg: code/game/objects/items/pillow.dm:1  #define FORTIFY_FILTER "ANGRY_GLOW" (#undef'd at the file's end)
    private const string FORTIFY_FILTER = "ANGRY_GLOW";

    // tg: code/game/objects/items/pillow.dm:347
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await base.afterattack(target, user, modifiers, attack_modifiers);

        Dm.@new<obj_effect_temp_visual_pillow_hit>(get_turf(target));
    }

    // tg: code/game/objects/items/pillow.dm:352
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (Dm.truthy(hunkered))
            un_fortify(user);
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:357
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        object dot = await base.ui_action_click(user, actiontype);

        if (!Dm.truthy(hunkered))
            await fortify(user);
        else
            un_fortify(user);
        return dot;
    }

    // tg: code/game/objects/items/pillow.dm:365
    public virtual async DmTask fortify(mob user = null)
    {
        hunkered = TRUE;
        force += 5;
        block_chance += 20;
        ADD_TRAIT(user, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED, HELD_ITEM_TRAIT);
        await user.add_movespeed_modifier(typeof(datum_movespeed_modifier_pillow_fortify));
        user.visible_message(span_alert(I18n.T("[] hunkers down into a defensive stance!", user.name)));
        user.add_filter(FORTIFY_FILTER, 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", outline_colour), Dm.kv("alpha", 0), Dm.kv("size", 1)));
        object filter = user.get_filter(FORTIFY_FILTER);
        Dm.animate(filter, Dm.named("alpha", 200), Dm.named("time", 0.5f * SECONDS), Dm.named("loop", -1));
        Dm.animate(Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
    }

    // tg: code/game/objects/items/pillow.dm:377
    public virtual void un_fortify(mob user = null)
    {
        hunkered = FALSE;
        force -= 5;
        block_chance -= 20;
        REMOVE_TRAIT(user, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED, HELD_ITEM_TRAIT);
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_pillow_fortify));
        object filter = user.get_filter(FORTIFY_FILTER);
        Dm.animate(filter);
        user.remove_filter(FORTIFY_FILTER);
        user.visible_message(span_alert(I18n.T("[] loosens up and relaxes a bit.", user)));
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/pillow.dm:16  var/static/tag_desc
    internal string obj_item_pillow__tag_desc = "This one seems to have its tag removed.";
    // tg: code/game/objects/items/pillow.dm:43  var/static/list/slapcraft_recipe_list (Initialize's)
    internal DmList obj_item_pillow_Initialize__slapcraft_recipe_list;
}
