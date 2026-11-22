// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_item_handling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: throwing (toggle_throw_mode, throw_mode_off, throw_mode_on, /mob/throw_item, /mob/living/throw_item: M4a P2)
// translated; giving and taking items (give, take, and click_ctrl_shift's offer) are M9.

// Throwing stuff
// tg: code/modules/mob/living/living_item_handling.dm:2
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_item_handling.dm:2
    public virtual void toggle_throw_mode()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(this))
            return;
        if (!HAS_TRAIT(this, TRAIT_CAN_THROW_ITEMS))
            return;
        if (Dm.truthy(throw_mode))
            throw_mode_off(THROW_MODE_TOGGLE);
        else
            throw_mode_on(THROW_MODE_TOGGLE);
    }


    // tg: code/modules/mob/living/living_item_handling.dm:13
    public virtual void throw_mode_off(object method = null)
    {
        if (!HAS_TRAIT(this, TRAIT_CAN_THROW_ITEMS))
            return;
        if (Dm.gt(throw_mode, method)) //A toggle doesnt affect a hold
            return;
        throw_mode = THROW_MODE_DISABLED;
        if (Dm.truthy(hud_used))
        {
            atom_movable_screen throw_button = hud_used.screen_objects[HUD_MOB_THROW] as atom_movable_screen;
            if (throw_button != null)
                throw_button.icon_state = "act_throw";
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_THROW_MODE_TOGGLE]))
            this._SendSignal(COMSIG_LIVING_THROW_MODE_TOGGLE, Dm.list(this, throw_mode));
    }


    // tg: code/modules/mob/living/living_item_handling.dm:24
    public virtual void throw_mode_on(float? mode = null)
    {
        mode ??= THROW_MODE_TOGGLE;
        if (!HAS_TRAIT(this, TRAIT_CAN_THROW_ITEMS))
            return;
        throw_mode = Dm.num(mode);
        if (Dm.truthy(hud_used))
        {
            atom_movable_screen throw_button = hud_used.screen_objects[HUD_MOB_THROW] as atom_movable_screen;
            if (throw_button != null)
                throw_button.icon_state = "act_throw_on";
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_THROW_MODE_TOGGLE]))
            this._SendSignal(COMSIG_LIVING_THROW_MODE_TOGGLE, Dm.list(this, throw_mode));
    }
}

// tg: code/modules/mob/living/living_item_handling.dm:32
public partial class mob : atom_movable
{
    // tg: code/modules/mob/living/living_item_handling.dm:32
    public virtual async DmTask<object> throw_item(atom target = null)
    {
        if (!HAS_TRAIT(this, TRAIT_CAN_THROW_ITEMS))
            return FALSE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOB_THROW]))
            this._SendSignal(COMSIG_MOB_THROW, Dm.list(this, target));
        return TRUE;
    }
}

// tg: code/modules/mob/living/living_item_handling.dm:38
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_item_handling.dm:38
    public override async DmTask<object> throw_item(atom target = null)
    {
        object dot = null;
        dot = await base.throw_item(target);
        throw_mode_off(THROW_MODE_TOGGLE);
        if (!HAS_TRAIT(this, TRAIT_CAN_THROW_ITEMS))
        {
            stack_trace(Dm.text("[] tried to throw [], but they shouldn't be able to throw things", this, target), "code/modules/mob/living/living_item_handling.dm", 42);
            return FALSE;
        }
        if (!Dm.truthy(target) || !isturf(loc))
            return FALSE;
        if (target is atom_movable_screen)
            return FALSE;
        atom_movable thrown_thing = null;
        obj_item held_item = get_active_held_item() as obj_item;
        object verb_text = pick("throw", "toss", "hurl", "chuck", "fling");
        if (prob(0.5f))
            verb_text = "yeet";
        float neckgrab_throw = FALSE; // we can't check for if it's a neckgrab throw when totaling up power_throw since we've already stopped pulling them by then, so get it early
        float frequency_number = 1; //We assign a default frequency number for the sound of the throw.
        if (!Dm.truthy(held_item))
        {
            if (Dm.truthy(pulling) && isliving(pulling) && grab_state >= GRAB_AGGRESSIVE)
            {
                mob_living throwable_mob = pulling as mob_living;
                if (!Dm.truthy(throwable_mob.buckled))
                {
                    thrown_thing = throwable_mob;
                    if (grab_state >= GRAB_NECK)
                        neckgrab_throw = TRUE;
                    await stop_pulling();
                    if (HAS_TRAIT(this, TRAIT_PACIFISM) || HAS_TRAIT(this, TRAIT_NO_THROWING))
                    {
                        to_chat(this, span_notice(I18n.T("You gently let go of [].", throwable_mob)));
                        return FALSE;
                    }
                }
            }
        }
        else
            thrown_thing = await held_item.on_thrown(this as mob_living_carbon, target) as atom_movable;
        if (!Dm.truthy(thrown_thing))
            return FALSE;
        if (isliving(thrown_thing))
        {
            turf start_T = get_turf(loc); //Get the start and target tile for the descriptors
            turf end_T = get_turf(target);
            if (start_T != null && end_T != null)
                log_combat(this, thrown_thing, "thrown", addition: Dm.text("grab from tile in [] towards tile at []", AREACOORD(start_T), AREACOORD(end_T)));
        }
        float power_throw = 0;
        float extra_throw_range = HAS_TRAIT(this, TRAIT_THROWINGARM) ? 2 : 0;

        obj_item_organ_cyberimp_chest_spine potential_spine = get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine;
        if (potential_spine is obj_item_organ_cyberimp_chest_spine)
        {
            power_throw += potential_spine.added_throw_speed;
            extra_throw_range += potential_spine.added_throw_range;
        }

        if (HAS_TRAIT(this, TRAIT_HULK))
            power_throw++;
        if (HAS_TRAIT(this, TRAIT_DWARF))
            power_throw--;
        if (HAS_TRAIT(thrown_thing, TRAIT_DWARF))
            power_throw++;
        if (Dm.truthy(neckgrab_throw))
            power_throw++;
        // GAP(lead): isgun (__DEFINES/is_helpers.dm) has no GlobalProcs member yet: expanded inline.
        if (HAS_TRAIT(this, TRAIT_TOSS_GUN_HARD) && istype(thrown_thing, typeof(obj_item_gun)))
            power_throw++;
        if (isitem(thrown_thing))
        {
            obj_item thrown_item = thrown_thing as obj_item;
            frequency_number = 1 - (thrown_item.w_class - 3) / 8; //At normal weight, the frequency is at 1. For tiny, it is 1.25. For huge, it is 0.75.
            if (Dm.truthy(thrown_item.throw_verb))
                verb_text = thrown_item.throw_verb;
        }
        await do_attack_animation(target, no_effect: 1);
        object throwsound = Dm.resource("sound/items/weapons/throw.ogg");
        string power_throw_text = ".";
        if (power_throw > 0) //If we have anything that boosts our throw power like hulk, we use the rougher heavier variant.
        {
            throwsound = Dm.resource("sound/items/weapons/throwhard.ogg");
            power_throw_text = " really hard!";
        }
        if (power_throw < 0) //if we have anything that weakens our throw power like dward, we use a slower variant.
        {
            throwsound = Dm.resource("sound/items/weapons/throwsoft.ogg");
            power_throw_text = " flimsily.";
        }
        frequency_number = frequency_number + (rand(-5, 5) / 100); //Adds a bit of randomness in the frequency to not sound exactly the same.
        //The volume of the sound takes the minimum between the distance thrown or the max range an item, but no more than 50. Short throws are quieter. A fast throwing speed also makes the noise sharper.
        playsound(this, throwsound, clamp(8 * min(get_dist(loc, target), thrown_thing.throw_range), 10, 50), vary: TRUE, extrarange: -1, frequency: frequency_number); // tg: code/modules/mob/living/living_item_handling.dm:109
        visible_message(span_danger(I18n.T("[] [][] [][]", this, verb_text, plural_s__local(verb_text), thrown_thing, power_throw_text)),
                        span_danger(I18n.T("You [] [][]", verb_text, thrown_thing, power_throw_text)));
        log_message(Dm.text("has thrown [] []", thrown_thing, power_throw_text), LOG_ATTACK);

        float drift_force = max(0.5f * NEWTONS, 1 * NEWTONS + power_throw);
        if (isitem(thrown_thing))
        {
            obj_item thrown_item = thrown_thing as obj_item;
            // WEIGHT_TO_NEWTONS(w_class) (__HELPERS/movement.dm:2): 0.2 NEWTONS + w_class * 0.4 NEWTONS, inline
            drift_force *= 0.2f * NEWTONS + thrown_item.w_class * 0.4f * NEWTONS;
        }

        await newtonian_move(get_angle(target, this), drift_force: drift_force);
        await thrown_thing.safe_throw_at(target, thrown_thing.throw_range + extra_throw_range, max(1, thrown_thing.throw_speed + power_throw), this, null, null, null, move_force);
        return dot;
    }

    // GAP(lead): tg's global proc plural_s (__HELPERS/pronouns.dm:63) has no GlobalProcs member yet; its body, a local copy
    // for throw_item.
    // tg: code/__HELPERS/pronouns.dm:63
    private static string plural_s__local(object pluralize)
    {
        string sw__1 = copytext_char(pluralize, -2);
        if (sw__1 == "ss" || sw__1 == "sh" || sw__1 == "ch")
            return "es";
        string sw__2 = copytext_char(pluralize, -1);
        if (sw__2 == "s" || sw__2 == "x" || sw__2 == "z")
            return "es";
        return "s";
    }
}

// tg: code/modules/mob/living/living_item_handling.dm:130
public partial class mob_living : mob
{
    // Giving stuff
    /**
     * Proc called when offering an item to another player
     *
     * This handles creating an alert and adding an overlay to it
     * Arguments:
     * * offered - The player being offered the item (optional, if null the offer is to everyone around)
     */
    // tg: code/modules/mob/living/living_item_handling.dm:130  (M9 slice F)
    public virtual async DmTask give(mob_living offered = null, obj_item item_bypass = null)
    {
        if (Dm.truthy(has_status_effect(typeof(datum_status_effect_offering))))
        {
            to_chat(this, span_warning(I18n.T("You're already offering something!")));
            return;
        }

        if (Dm.truthy(this.incapacitated))
        {
            to_chat(this, span_warning(I18n.T("You're unable to offer anything in your current state!")));
            return;
        }

        obj_item offered_item = item_bypass != null ? item_bypass : get_active_held_item() as obj_item;
        // if it's an abstract item, should consider it to be non-existent (unless it's a HAND_ITEM, which means it's an obj/item that is just a representation of our hand)
        if (offered_item == null || ((Dm.truthy(Dm.bitand(offered_item.item_flags, ABSTRACT)) && !HAS_TRAIT(offered_item, TRAIT_BORG_GIVE)) && !HAS_TRAIT(offered_item, TRAIT_OFFERED_WHEN_PULLED) && !Dm.truthy(Dm.bitand(offered_item.item_flags, HAND_ITEM))))
        {
            to_chat(this, span_warning(I18n.T("You're not holding anything to offer!")));
            return;
        }

        if (offered != null)
        {
            if (offered == this)
            {
                if (!Dm.truthy(swap_hand(get_inactive_hand_index()))) //have to swap hands first to take something
                {
                    to_chat(this, span_warning(I18n.T("You try to take [] from yourself, but fail.", offered_item)));
                    return;
                }
                if (!Dm.truthy(await put_in_active_hand(offered_item)))
                {
                    to_chat(this, span_warning(I18n.T("You try to take [] from yourself, but fail.", offered_item)));
                    return;
                }
                else
                {
                    to_chat(this, span_notice(I18n.T("You take [] from yourself.", offered_item)));
                    return;
                }
            }

            if (Dm.truthy(offered.incapacitated))
            {
                to_chat(this, span_warning(I18n.T("[] unable to take anything in [] current state!", offered.p_Theyre(), offered.p_their())));
                return;
            }

            if (!Dm.truthy(await offered.IsReachableBy(this)))
            {
                to_chat(this, span_warning(I18n.T("You have to be beside []!", offered.p_them())));
                return;
            }

            if (!HAS_TRAIT(offered, TRAIT_CAN_HOLD_ITEMS))
            {
                to_chat(this, span_warning(I18n.T("[] can't hold anything you offer!", offered.p_They())));
                return;
            }
        }
        else if (Dm.locate<mob_living>(orange(1, this)) == null)
        {
            to_chat(this, span_warning(I18n.T("There's nobody beside you to take it!")));
            return;
        }

        if (Dm.truthy(await offered_item.on_offered(this, offered))) // see if the item interrupts with its own behavior
            return;

        balloon_alert_to_viewers(I18n.T("offers something"));
        visible_message(span_notice(I18n.T("[] is offering [][].", this, offered != null ? Dm.text("[] ", offered) : "", offered_item)),
                        span_notice(I18n.T("You offer [][].", offered != null ? Dm.text("[] ", offered) : "", offered_item)), null, 2);

        await apply_status_effect(typeof(datum_status_effect_offering), offered_item, null, offered);
    }

    /**
     * Proc called when the player clicks the give alert
     *
     * Handles checking if the player taking the item has open slots and is in range of the offerer
     * Also deals with the actual transferring of the item to the players hands
     * Arguments:
     * * offerer - The living mob giving the original item
     * * offered_item - The item being given by the offerer
     */
    // tg: code/modules/mob/living/living_item_handling.dm:190  (M9 slice F)
    public virtual async DmTask take(mob_living offerer = null, obj_item offered_item = null, object bypass = null)
    {
        clear_alert(Dm.text("[]", offerer));
        if (Dm.truthy(this.incapacitated))
        {
            to_chat(this, span_warning(I18n.T("You're unable to take anything in your current state!")));
            return;
        }
        if (get_dist(this, offerer) > 1)
        {
            to_chat(this, span_warning(I18n.T("[] is out of range!", offerer)));
            return;
        }
        if (offered_item == null || offerer.get_active_held_item() != offered_item && !Dm.truthy(bypass))
        {
            to_chat(this, span_warning(I18n.T("[] is no longer holding the item they were offering!", offerer)));
            return;
        }
        if (!Dm.truthy(get_empty_held_indexes()))
        {
            to_chat(this, span_warning(I18n.T("You have no empty hands!")));
            return;
        }

        if (Dm.truthy(await offered_item.on_offer_taken(offerer, this))) // see if the item has special behavior for being accepted
            return;

        if (!Dm.truthy(await offerer.temporarilyRemoveItemFromInventory(offered_item)))
        {
            visible_message(span_notice(I18n.T("[] tries to hand over [] but it's stuck to them....", offerer, offered_item)));
            return;
        }

        visible_message(span_notice(I18n.T("[] takes [] from [].", this, offered_item, offerer)),
                        span_notice(I18n.T("You take [] from [].", offered_item, offerer)));
        // do_pickup_animation(src, offerer): tg passes the offerer as its turf/source, whose x and y are the offerer's tile
        offered_item.do_pickup_animation(this, get_turf(offerer));
        await put_in_hands(offered_item);
    }
}

// tg: code/modules/mob/living/living_item_handling.dm:217 (declared in M4a: the guard's row at /mob/living, interaction.md 7)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_item_handling.dm:217
    public override async DmTask<object> click_ctrl_shift(mob user = null)
    {
        if (HAS_TRAIT(this, TRAIT_CAN_HOLD_ITEMS))
        {
            mob_living living_user = user as mob_living;
            await living_user.give(this);
        }
        return null;
    }
}
