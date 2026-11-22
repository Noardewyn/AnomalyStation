// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/_shoes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /obj/item/clothing/shoes's worn_overlays (both definitions: worn_overlays__d0 and worn_overlays),
// separate_worn_overlays, visual_equipped, equipped, restore_offsets, dropped, update_clothes_damaged_state, attack_hand and
// oh_what_a_neat_hat translated (M4a clothing: shoes are put on, taken off and drawn on the wearer), and M8z slice B's shoelaces
// (adjust_laces, handle_tying, still_shoed, check_trip, attack_self, the verbs of the laces and the untied and knotted alerts: the
// strip menu knots, unties and unknots them), examine (X3); gaps CL1 (D333): the alt right click to wear shoes under the
// uniform, the suicide act, the fantasy bonuses, the trait helper (worn on the feet only; DV523 keeps a trait attached off the
// feet) and the cerulean's shoes worn as a hat;
// the screentip (add_context) is a live-partial row (D291). The digitigrade icon is a deferred leaf (the GAGS of digitigrade
// clothing, not-translated.md 61). [DmPath] is here: the earliest .dme file that declares the type (rules 2.3).

// tg: code/modules/clothing/shoes/_shoes.dm:1
[DmPath("/obj/item/clothing/shoes")]
public partial class obj_item_clothing_shoes : obj_item_clothing
{
    // tg: code/modules/clothing/shoes/_shoes.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // register_context(): the context tips (screentips stay deferred, D291; add_context is a live-partial row)
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:43
    public override async DmTask<object> click_alt_secondary(mob user = null)
    {
        alternate_worn_layer = Dm.eq(alternate_worn_layer, UNDER_UNIFORM_LAYER) ? NONE : UNDER_UNIFORM_LAYER;

        update_slot_icon();
        balloon_alert(user, I18n.T("wearing [] uniforms", Dm.eq(alternate_worn_layer, UNDER_UNIFORM_LAYER) ? I18n.T("under") : I18n.T("over")));
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:53
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (prob(50))
        {
            user.visible_message(span_suicide(I18n.T("[] begins fastening \\the [] up waaay too tightly! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
            obj_item_bodypart_leg left = user.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart_leg;
            obj_item_bodypart_leg right = user.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart_leg;
            if (left != null)
                await left.dismember();
            if (right != null)
                await right.dismember();
            playsound(user, SFX_DESECRATION, 50, TRUE, -1);
            return BRUTELOSS;
        }
        else //didnt realize this suicide act existed (was in miscellaneous.dm) and didnt want to remove it, so made it a 50/50 chance. Why not!
        {
            user.visible_message(span_suicide(I18n.T("[] is bashing [] own head in with []! Ain't that a kick in the head?", user, user.p_their(), this)));
            for (float i = 1; i <= 3; i++)
            {
                await sleep(0.3f * SECONDS);
                playsound(user, Dm.resource("sound/items/weapons/genhit2.ogg"), 50, TRUE);
            }
            return BRUTELOSS;
        }
    }

    // The earlier of the type's two worn_overlays definitions (rules 7.5: the later one's ..() calls it).
    // tg: code/modules/clothing/shoes/_shoes.dm:71
    public object worn_overlays__d0(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        if (Dm.truthy(damaged_clothes))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/item_damage.dmi"), "damagedshoe"));
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:78
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            return dot;
        object blood_overlay = await get_blood_overlay("shoe", bodyshape);
        if (Dm.truthy(blood_overlay))
            Dm.append(ref dot, blood_overlay);
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:86
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        if (!ishuman(loc))
            return dot;

        if (tied == SHOES_UNTIED)
            Dm.append(ref dot, I18n.T("The [] are [].", fastening_type, untied_adjective()));
        else if (tied == SHOES_KNOTTED)
            Dm.append(ref dot, I18n.T("The [] are all knotted together.", fastening_type));
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:97
    [DmParams("user", "slot")]
    public override async DmTask<object> visual_equipped(mob user = null, object slot = null, float? initial = null)
    {
        object dot = null;
        dot = await base.visual_equipped(user, slot, initial);
        if (Dm.truthy(offset) && Dm.truthy(Dm.bitand(slot_flags, slot)))
        {
            user.pixel_z += Dm.num(offset);
            worn_y_dimension -= (Dm.num(offset) * 2);
            await user.update_worn_shoes();
            equipped_before_drop = TRUE;
        }
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:105
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (fastening_type != SHOES_SLIPON && tied == SHOES_UNTIED)
        {
            our_alert_ref = WEAKREF(user.throw_alert(ALERT_SHOES_KNOT, typeof(atom_movable_screen_alert_shoes_untied)) as datum) as datum_weakref;
            RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(check_trip), @override: TRUE);
        }
        oh_what_a_neat_hat(user as mob_living_carbon, TRUE);
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:112
    public virtual void restore_offsets(mob user = null)
    {
        equipped_before_drop = FALSE;
        user.pixel_z -= Dm.num(offset);
        worn_y_dimension = ICON_SIZE_Y;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:117
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = null;
        atom_movable_screen_alert our_alert = (atom_movable_screen_alert)our_alert_ref?.resolve();
        if (!Dm.truthy(our_alert))
            our_alert_ref = null;
        if (our_alert?.owner == user)
            user.clear_alert(ALERT_SHOES_KNOT);
        if (Dm.truthy(offset) && Dm.truthy(equipped_before_drop))
            restore_offsets(user);
        dot = await base.dropped(user, silent, __extra);
        oh_what_a_neat_hat(user as mob_living_carbon, FALSE);
        return dot;
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:128
    public override async DmTask update_clothes_damaged_state(float? damaged_state = null)
    {
        damaged_state ??= CLOTHING_DAMAGED;
        await base.update_clothes_damaged_state(damaged_state);
        if (ismob(loc))
        {
            mob M = loc as mob;
            await M.update_worn_shoes();
        }
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:134
    public override object generate_digitigrade_icons(icon base_icon = null, object greyscale_colors = null)
    {
        return Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade), greyscale_colors), "boots_worn");
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:282
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(user is mob_living_carbon_human))
            return await base.attack_hand(user, modifiers, __extra);
        if (loc == user && tied != SHOES_TIED && Dm.truthy(Dm.bitand(((mob_living)user).mobility_flags, MOBILITY_USE)))
        {
            await handle_tying(user as mob_living);
            return null;
        }
        await base.attack_hand(user, modifiers, __extra);
        return null;
    }

    /**
     * adjust_laces adjusts whether our shoes (assuming they can be tied) and tied, untied, or knotted
     *
     * In addition to setting the state, it will deal with getting rid of alerts if they exist, as well as registering and unregistering the stepping signals
     *
     * Arguments:
     * *
     * * state: SHOES_UNTIED, SHOES_TIED, or SHOES_KNOTTED, depending on what you want them to become
     * * user: used to check to see if we're the ones unknotting our own laces
     * * force_lacing: boolean. if TRUE, ignores whether we actually have laces
     */
    // tg: code/modules/clothing/shoes/_shoes.dm:148
    public virtual void adjust_laces(object state = null, mob user = null, float? force_lacing = null)
    {
        force_lacing ??= FALSE;
        if (fastening_type == SHOES_SLIPON && !Dm.truthy(force_lacing))
            return;

        mob_living_carbon_human our_guy = null;
        if (ishuman(loc))
            our_guy = loc as mob_living_carbon_human;

        tied = Dm.num(state);
        if (tied == SHOES_TIED)
        {
            if (our_guy != null)
                our_guy.clear_alert(ALERT_SHOES_KNOT);
            UnregisterSignal(this, COMSIG_SHOES_STEP_ACTION);
        }
        else
        {
            if (tied == SHOES_UNTIED && our_guy != null && user == our_guy)
                our_alert_ref = WEAKREF(our_guy.throw_alert(ALERT_SHOES_KNOT, typeof(atom_movable_screen_alert_shoes_untied)) as datum) as datum_weakref; // if we're the ones unknotting our own laces, of course we know they're untied
            RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(check_trip), @override: TRUE);
        }
    }

    /**
     * handle_tying deals with all the actual tying/untying/knotting, inferring your intent from who you are in relation to the state of the laces
     *
     * If you're the wearer, you want them to move towards tied-ness (knotted -> untied -> tied). If you're not, you're pranking them, so you're moving towards knotted-ness (tied -> untied -> knotted)
     *
     * Arguments:
     * *
     * * user: who is the person interacting with the shoes?
     */
    // tg: code/modules/clothing/shoes/_shoes.dm:175
    public virtual async DmTask handle_tying(mob_living user = null)
    {
        ///our_guy here is the wearer, if one exists (and he must exist, or we don't care)
        mob_living_carbon_human our_guy = loc as mob_living_carbon_human;
        if (our_guy == null)
            return;

        if (!isliving(user) || !Dm.truthy(Dm.bitand(user.mobility_flags, MOBILITY_USE)))
            return;

        if (!in_range(user, our_guy))
        {
            to_chat(user, span_warning(I18n.T("You aren't close enough to interact with []'s []!", this, fastening_type)));
            return;
        }

        if (user == loc && tied != SHOES_TIED) // if they're our own shoes, go tie-wards
        {
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, our_guy)))
            {
                to_chat(user, span_warning(I18n.T("You're already interacting with []!", this)));
                return;
            }
            user.visible_message(span_notice(I18n.T("[] begins [] the [] of [] [].", user, tied != 0 ? I18n.T("unknotting") : fastening_verb(), fastening_type, user.p_their(), name)), span_notice(I18n.T("You begin [] the [] of your []...", tied != 0 ? I18n.T("unknotting") : fastening_verb(), fastening_type, name)));

            if (Dm.truthy(await do_after(user, lace_time, target: our_guy, extra_checks: Dm.@new<datum_callback>(this, nameof(still_shoed), our_guy))))
            {
                to_chat(user, span_notice(I18n.T("You [] the [] of your [].", tied != 0 ? I18n.T("unknot") : fasten_verb(), fastening_type, name)));
                if (tied == SHOES_UNTIED)
                    adjust_laces(SHOES_TIED, user);
                else
                    adjust_laces(SHOES_UNTIED, user);
            }
        }
        else // if they're someone else's shoes, go knot-wards
        {
            if (user.body_position == STANDING_UP)
            {
                to_chat(user, span_warning(I18n.T("You must be on the floor to interact with []!", this)));
                return;
            }
            if (tied == SHOES_KNOTTED)
            {
                to_chat(user, span_warning(I18n.T("The [] on []'s [] are already a hopelessly tangled mess!", fastening_type, loc, name)));
                return;
            }
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, our_guy)))
            {
                to_chat(user, span_warning(I18n.T("You're already interacting with []!", this)));
                return;
            }

            float mod_time = lace_time;
            to_chat(user, span_notice(I18n.T("You quietly set to work [] []'s []...", tied != 0 ? I18n.T("un[]", fastening_verb()) : I18n.T("knotting"), loc, name)));
            if (HAS_TRAIT(user, TRAIT_CLUMSY)) // based clowns trained their whole lives for this
                mod_time *= 0.75f;

            if (Dm.truthy(await do_after(user, mod_time, target: our_guy, extra_checks: Dm.@new<datum_callback>(this, nameof(still_shoed), our_guy), cog_icon: null)))
            {
                to_chat(user, span_notice(I18n.T("You [] the [] on []'s [].", tied != 0 ? I18n.T("un[]", fasten_verb()) : I18n.T("knot"), fastening_type, loc, name)));
                if (tied == SHOES_UNTIED)
                    adjust_laces(SHOES_KNOTTED, user);
                else
                    adjust_laces(SHOES_UNTIED, user);
            }
            else // if one of us moved
            {
                user.visible_message(span_danger(I18n.T("[] stamps on []'s hand, mid-[]!", our_guy, user, tied != 0 ? I18n.T("knotting") : I18n.T("un[]", fastening_verb()))), span_userdanger(I18n.T("Ow! [] stamps on your hand!", our_guy)), ignored_mobs: Dm.list(our_guy));
                to_chat(our_guy, span_userdanger(I18n.T("You stamp on []'s hand! What the- [] [] [] your []!", user, user.p_they(), user.p_were(), tied != 0 ? I18n.T("knotting") : I18n.T("un[]", fastening_verb()), fastening_type)));
                await user.emote("scream");
                await user.apply_damage(10, BRUTE, user.get_active_hand(), wound_bonus: CANT_WOUND);
                await user.apply_damage(40, STAMINA);
                await user.Paralyze(1 * SECONDS);
            }
        }
    }

    ///checking to make sure we're still on the person we're supposed to be, for lacing do_after's
    // tg: code/modules/clothing/shoes/_shoes.dm:233
    public virtual object still_shoed(mob_living_carbon our_guy = null)
    {
        return loc == our_guy ? TRUE : FALSE;
    }

    ///check_trip runs on each step to see if we fall over as a result of our lace status. Knotted laces are a guaranteed trip, while untied shoes are just a chance to stumble
    // tg: code/modules/clothing/shoes/_shoes.dm:237
    [SignalHandler]
    public virtual void check_trip()
    {
        mob_living_carbon_human our_guy = loc as mob_living_carbon_human;
        if (our_guy == null) // are they REALLY /our guy/?
            return;
        if (our_guy.head == this) //a cerulean couldnt even fathom creating a triphazard by putting shoes on your feet
            return;

        if (tied == SHOES_KNOTTED)
        {
            Waiting.NoSleep(our_guy.Paralyze(5));
            Waiting.NoSleep(our_guy.Knockdown(10));
            our_guy.visible_message(span_danger(I18n.T("[] trips on [] knotted [] and falls! What a klutz!", our_guy, our_guy.p_their(), fastening_type)), span_userdanger(I18n.T("You trip on your knotted [] and fall over!", fastening_type)));
            Waiting.NoSleep(our_guy.add_mood_event("trip", typeof(datum_mood_event_tripped))); // well we realized they're knotted now!
            our_alert_ref = WEAKREF(our_guy.throw_alert(ALERT_SHOES_KNOT, typeof(atom_movable_screen_alert_shoes_knotted)) as datum) as datum_weakref;
        }
        else if (tied == SHOES_UNTIED)
        {
            bool wiser = true; // did we stumble and realize our laces are undone?
            float roll = rand(1, 1000);
            if (roll == 1) // .1% chance to trip and fall over (note these are per step while our laces are undone)
            {
                Waiting.NoSleep(our_guy.Paralyze(5));
                Waiting.NoSleep(our_guy.Knockdown(10));
                Waiting.NoSleep(our_guy.add_mood_event("trip", typeof(datum_mood_event_tripped))); // well we realized they're knotted now!
                our_guy.visible_message(span_danger(I18n.T("[] trips on [] [] [] and falls! What a klutz!", our_guy, our_guy.p_their(), untied_adjective(), fastening_type)), span_userdanger(I18n.T("You trip on your [] [] and fall over!", untied_adjective(), fastening_type)));
            }
            else if (roll >= 2 && roll <= 5) // .4% chance to stumble and lurch forward
            {
                Waiting.NoSleep(our_guy.throw_at(get_step(our_guy, our_guy.dir), 3, 2));
                to_chat(our_guy, span_danger(I18n.T("You stumble on your [] [] and lurch forward!", untied_adjective(), fastening_type)));
            }
            else if (roll >= 6 && roll <= 13) // .7% chance to stumble and fling what we're holding
            {
                bool have_anything = false;
                foreach (obj_item I in Dm.each<obj_item>(our_guy.held_items))
                {
                    have_anything = true;
                    Waiting.NoSleep(our_guy.accident(I));
                }
                to_chat(our_guy, span_danger(I18n.T("You trip on your [] a bit[]!", fastening_type, have_anything ? I18n.T(", flinging what you were holding") : "")));
            }
            else if (roll >= 14 && roll <= 25) // 1.3ish% chance to stumble and be a bit off balance (like being disarmed)
            {
                to_chat(our_guy, span_danger(I18n.T("You stumble a bit on your [] []!", untied_adjective(), fastening_type)));
                Waiting.NoSleep(our_guy.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
            }
            else
                wiser = false;
            if (wiser)
            {
                Waiting.NoSleep(our_guy.add_mood_event("untied", typeof(datum_mood_event_untied))); // well we realized they're untied now!
                our_alert_ref = WEAKREF(our_guy.throw_alert(ALERT_SHOES_KNOT, typeof(atom_movable_screen_alert_shoes_untied)) as datum) as datum_weakref;
            }
        }
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:292
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);

        if (fastening_type == SHOES_SLIPON)
            return dot;

        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
        {
            to_chat(user, span_warning(I18n.T("You're already interacting with []!", this)));
            return dot;
        }

        to_chat(user, span_notice(I18n.T("You begin [][] the [] on []...", tied != 0 ? "un" : "", fastening_verb(), fastening_type, this)));

        if (Dm.truthy(await do_after(user, lace_time, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(still_shoed), user))))
        {
            to_chat(user, span_notice(I18n.T("You [][] the [] on [].", tied != 0 ? "un" : "", fasten_verb(), fastening_type, this)));
            adjust_laces(tied != 0 ? SHOES_UNTIED : SHOES_TIED, user);
        }
        return dot;
    }

    /// Returns appropriate description for unfastened shoes
    // tg: code/modules/clothing/shoes/_shoes.dm:318
    public virtual object untied_adjective()
    {
        if (fastening_type == SHOES_LACED)
            return "untied";
        else if (fastening_type == SHOES_VELCRO || fastening_type == SHOES_STRAPS)
            return "loose";

        return "nonexistant";
    }

    /// Returns appropriate verb for how to fasten shoes
    // tg: code/modules/clothing/shoes/_shoes.dm:331
    public virtual object fasten_verb()
    {
        if (fastening_type == SHOES_LACED)
            return "tie";
        else if (fastening_type == SHOES_VELCRO || fastening_type == SHOES_STRAPS)
            return "fasten";

        return "do something mysterious to";
    }

    /// Returns appropriate verb for fastening shoes
    // tg: code/modules/clothing/shoes/_shoes.dm:340
    public virtual object fastening_verb()
    {
        if (fastening_type == SHOES_LACED)
            return "tying";
        else if (fastening_type == SHOES_VELCRO || fastening_type == SHOES_STRAPS)
            return "fastening";

        return "doing something mysterious to";
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:306
    public override async DmTask apply_fantasy_bonuses(object bonus = null)
    {
        await base.apply_fantasy_bonuses(bonus);
        slowdown = Dm.num(modify_fantasy_variable("slowdown", slowdown, Dm.num(Dm.neg(bonus)) * 0.1f, 0));
        if (ismob(loc))
        {
            mob wearer = (mob)loc;
            await wearer.update_equipment_speed_mods();
        }
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:313
    public override async DmTask remove_fantasy_bonuses(object bonus = null)
    {
        slowdown = Dm.num(reset_fantasy_variable("slowdown", slowdown));
        if (ismob(loc))
        {
            mob wearer = (mob)loc;
            await wearer.update_equipment_speed_mods();
        }
        await base.remove_fantasy_bonuses(bonus);
    }

    // tg: code/modules/clothing/shoes/_shoes.dm:350
    // DV523: tg returns before the base proc when the shoes are not on the feet, so a trait attached then (magboots switched on in a
    // hand, or on the floor) is lost: the boots say "enabled" and never give it, even once worn. Here the shoes keep it in their
    // clothing_traits like any clothing (equipped hands those to the wearer); only the wearer's own traits wait for the feet (a
    // cerulean's shoes worn as a hat stay without them, tg's reason for the check).
    [DmParams("traits")]
    public override void attach_clothing_traits(object trait_or_traits = null)
    {
        if (!Dm.truthy(Dm.bitand((loc as mob)?.get_slot_by_item(this), ITEM_SLOT_FEET)))
        {
            if (!islist(trait_or_traits))
                trait_or_traits = Dm.list(trait_or_traits);
            clothing_traits = LAZYLISTDUPLICATE(clothing_traits);
            LAZYOR(ref clothing_traits, trait_or_traits);
            if (Dm.truthy(clothing_traits))
                clothing_traits = string_list(clothing_traits) as DmList;
            return;
        }
        base.attach_clothing_traits(trait_or_traits);
    }

    /// when the shoes are worn on the headslot we have to override the overlay's icon to look for the correct file
    // tg: code/modules/clothing/shoes/_shoes.dm:356
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        isinhands ??= FALSE;
        bodyshape ??= NONE;
        DmList dot = null;
        dot = worn_overlays__d0(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands) || !Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_CERULEAN)))
            return dot;
        DmList current_overlays = dot;
        object icon_to_use = (worn_icon is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.resource("icons/mob/clothing/feet.dmi")); // DEFAULT_SHOES_FILE (__DEFINES/species_clothing_paths.dm:5)
        mutable_appearance standing_overlay = current_overlays[1] as mutable_appearance;
        standing_overlay.icon = icon_to_use;
        standing.icon = icon_to_use;
        worn_y_offset = 28; //about how many px up to draw on the head
        return dot;
    }

    /// ceruleans wear shoes as a hat. are they doing a bit or are they dumb? i guess we will never know
    // tg: code/modules/clothing/shoes/_shoes.dm:368
    public virtual void oh_what_a_neat_hat(mob_living_carbon user = null, object up_or_down = null)
    {
        if (user != null && !Dm.truthy(Dm.bitand(user?.bodyshape, BODYSHAPE_CERULEAN)))
            return;
        if (Dm.truthy(up_or_down))
        {
            Dm.combine(ref slot_flags, ITEM_SLOT_HEAD);
            detach_clothing_traits(clothing_traits);
        }
        else
        {
            slot_flags = Dm.initial(this, (obj_item p) => p.slot_flags);
            worn_y_offset = Dm.initial(this, (obj_item p) => p.worn_y_offset);
        }
    }
}
