// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tackle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298). Rust and blade heretic robes are read through their generated types.

// tg: code/datums/components/tackle.dm:14
public partial class datum_component_tackler : datum_component
{
    // The generated vars are untyped; these read them as tg's numbers.
    private float speed_value => Dm.num(speed);
    private float range_value => Dm.num(range);

    /// how many things can we knock off a table at once by diving into it?
    private const int MAX_TABLE_MESSES = 18;

    // tg: code/datums/components/tackle.dm:34
    [DmParams("stamina_cost", "base_knockdown", "range", "speed", "skill_mod", "min_distance", "silent_gain")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object stamina_cost = Dm.arg(__extra, 0) ?? 25;
        object base_knockdown = Dm.arg(__extra, 1) ?? 1 * SECONDS;
        object range = Dm.arg(__extra, 2) ?? 4;
        object speed = Dm.arg(__extra, 3) ?? 1;
        object skill_mod = Dm.arg(__extra, 4) ?? 0;
        object min_distance = Dm.arg(__extra, 5);
        object silent_gain = Dm.arg(__extra, 6) ?? FALSE;
        object dot = await base.Initialize(Dm.forward(__extra, stamina_cost, base_knockdown, range, speed, skill_mod, min_distance, silent_gain));
        if (!iscarbon(parent))
            return COMPONENT_INCOMPATIBLE;

        this.stamina_cost = stamina_cost;
        this.base_knockdown = base_knockdown;
        this.range = range;
        this.speed = speed;
        this.skill_mod = skill_mod;
        this.min_distance = min_distance;

        if (!Dm.truthy(silent_gain))
        {
            to_chat(parent, span_notice(I18n.T("You are now able to launch tackles! You can do so by activating throw mode, and ")) + span_boldnotice(I18n.T("RIGHT-CLICKING on your target with an empty hand.")));
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(resetTackle)), Dm.num(this.base_knockdown), TIMER_STOPPABLE, file: "code/datums/components/tackle.dm", line: 49);
        return dot;
    }

    // tg: code/datums/components/tackle.dm:51
    public override object Destroy(float? force = null)
    {
        to_chat(parent, span_notice(I18n.T("You can no longer tackle.")));
        return base.Destroy(force);
    }

    // tg: code/datums/components/tackle.dm:56
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOB_CLICKON, nameof(checkTackle));
        RegisterSignal(parent, COMSIG_MOVABLE_PRE_IMPACT, nameof(sack));
        RegisterSignal(parent, COMSIG_MOVABLE_POST_THROW, nameof(registerTackle));
    }

    // tg: code/datums/components/tackle.dm:61
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOB_CLICKON, COMSIG_MOVABLE_PRE_IMPACT, COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_POST_THROW));
    }

    ///Store the thrownthing datum for later use
    // tg: code/datums/components/tackle.dm:65
    [SignalHandler]
    public virtual void registerTackle(mob_living_carbon user = null, datum_thrownthing tackle = null, params object[] __extra)
    {
        tackle_ref = WEAKREF(tackle) as datum_weakref;
        tackle.thrower = WEAKREF(user) as datum_weakref;
    }

    ///See if we can tackle or not. If we can, leap!
    // tg: code/datums/components/tackle.dm:72
    [SignalHandler]
    public virtual object checkTackle(mob_living_carbon user = null, atom clicked_atom = null, DmList modifiers = null)
    {
        if (!Dm.truthy(modifiers?[RIGHT_CLICK]) || Dm.truthy(modifiers[ALT_CLICK]) || Dm.truthy(modifiers[SHIFT_CLICK]) || Dm.truthy(modifiers[CTRL_CLICK]) || Dm.truthy(modifiers[MIDDLE_CLICK]))
            return null;

        if (!Dm.truthy(user.throw_mode) || user.get_active_held_item() != null || user.pulling != null || user.buckled != null || Dm.truthy(user.incapacitated))
            return null;

        if (clicked_atom == null || !(isturf(clicked_atom) || isturf(clicked_atom.loc)))
            return null;

        if (HAS_TRAIT(user, TRAIT_HULK))
        {
            to_chat(user, span_warning(I18n.T("You're too angry to remember how to tackle!")));
            return null;
        }

        if (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
        {
            to_chat(user, span_warning(I18n.T("You need free use of your hands to tackle!")));
            return null;
        }

        if (user.body_position == LYING_DOWN)
        {
            to_chat(user, span_warning(I18n.T("You must be standing to tackle!")));
            return null;
        }

        if (Dm.truthy(tackling))
        {
            to_chat(user, span_warning(I18n.T("You're not ready to tackle!")));
            return null;
        }

        if (Dm.truthy(user.get_timed_status_effect_duration(typeof(datum_status_effect_staggered)))) // can't tackle if you're staggered
        {
            to_chat(user, span_warning(I18n.T("You're too off balance to tackle!")));
            return null;
        }

        user.face_atom(clicked_atom);

        tackling = TRUE;
        RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(checkObstacle));
        playsound(user, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 40, TRUE, -1);

        string leap_word = HAS_TRAIT(user, TRAIT_CATLIKE_INSTINCT) || HAS_TRAIT(user, TRAIT_TACKLING_TAILED_POUNCE) ? I18n.T("pounce") : I18n.T("leap"); //If cat, "pounce" instead of "leap".
        if (Dm.truthy(can_see(user, clicked_atom, 7)))
            user.visible_message(span_warning(I18n.T("[] []s at []!", user, leap_word, clicked_atom)), span_danger(I18n.T("You [] at []!", leap_word, clicked_atom)));
        else
            user.visible_message(span_warning(I18n.T("[] []s!", user, leap_word)), span_danger(I18n.T("You []!", leap_word)));

        if (Dm.num(get_dist(user, clicked_atom)) < Dm.num(min_distance))
        {
            object tackle_angle = get_angle(user, clicked_atom);
            clicked_atom = get_turf_in_angle(Dm.num(tackle_angle), get_turf(user) as turf, Dm.num(min_distance)) as atom;
        }

        Waiting.Detach(user.Knockdown(base_knockdown, ignore_canstun: TRUE));
        Waiting.Detach(user.adjust_stamina_loss(stamina_cost));
        Waiting.Detach(user.throw_at(clicked_atom, range, speed, user, FALSE));
        _addtimer(Dm.@new<datum_callback>(this, nameof(resetTackle)), Dm.num(base_knockdown), TIMER_STOPPABLE, file: "code/datums/components/tackle.dm", line: 123);
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /**
     * sack() is called when you actually smack into something, assuming we're mid-tackle.
     */
    // tg: code/datums/components/tackle.dm:141
    [SignalHandler]
    public virtual object sack(mob_living_carbon user = null, atom hit = null, params object[] __extra)
    {
        datum_thrownthing tackle = tackle_ref?.resolve() as datum_thrownthing;
        if (!Dm.truthy(tackling) || tackle == null)
            return null;

        user.toggle_throw_mode();
        if (!iscarbon(hit))
        {
            if (Dm.truthy(hit.density))
                Waiting.Detach(splat(user, hit)); // INVOKE_ASYNC
            return null;
        }

        mob_living_carbon target = hit as mob_living_carbon;
        string tackle_word = HAS_TRAIT(user, TRAIT_CATLIKE_INSTINCT) ? I18n.T("pounce") : I18n.T("tackle"); //If cat, "pounce" instead of "tackle".

        float roll = rollTackle(target);
        tackling = FALSE;
        tackle.gentle = TRUE;

        if (Dm.truthy(Waiting.NoSleep(target.check_block(user, 0, user.name, attack_type: LEAP_ATTACK))))
        {
            user.visible_message(span_danger(I18n.T("[]'s tackle is blocked by [], softening the effect!", user, target)), span_userdanger(I18n.T("Your tackle is blocked by [], softening the effect!", target)), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] blocks []'s tackle attempt, softening the effect!", target, user)));
            Waiting.Detach(neutral_outcome(user, target, tackle_word)); //Forces a neutral outcome so you're not screwed too much from being blocked while tackling
            return COMPONENT_MOVABLE_IMPACT_FLIP_HITPUSH;
        }

        if (roll <= -1)
            Waiting.Detach(negative_outcome(user, target, roll, tackle_word)); //OOF
        else if (roll >= 1)
            Waiting.Detach(positive_outcome(user, target, roll, tackle_word));
        else if (roll == 0) //nothing good, nothing bad
            Waiting.Detach(neutral_outcome(user, target, tackle_word));

        return COMPONENT_MOVABLE_IMPACT_FLIP_HITPUSH;
    }

    /// Helper to do a grab and then adjust the grab state if necessary
    // tg: code/datums/components/tackle.dm:181
    public virtual async DmTask do_grab(mob_living_carbon tackler = null, mob_living_carbon tackled = null, object skip_to_state = null)
    {
        skip_to_state ??= GRAB_PASSIVE;

        if (!Dm.eq(await tackler.grab(tackled), GRAB_SUCCESS) || tackler.pulling != tackled)
            return;

        if (!Dm.eq(tackler.grab_state, skip_to_state))
            await tackler.setGrabState(skip_to_state);
    }

    /**
     * Our positive tackling outcomes.
     */
    // tg: code/datums/components/tackle.dm:206
    public virtual async DmTask positive_outcome(mob_living_carbon user = null, mob_living_carbon target = null, float roll = 1, string tackle_word = null)
    {
        tackle_word ??= I18n.T("tackle");
        float potential_outcome = roll * 10;

        if (ishuman(target))
            potential_outcome *= (100 - Dm.num(target.run_armor_check(BODY_ZONE_CHEST, MELEE))) / 100;
        else
            potential_outcome *= 0.9f;

        // DM's switch ranges leave the gaps of a fractional outcome (20 to 21, 49 to 50) and 0 to 1 with no branch; kept
        if (potential_outcome <= 0) //I don't want to know how this has happened, okay?
        {
            await neutral_outcome(user, target, tackle_word); //Default to neutral (tg passes the roll where the word goes, a runtime-free slip in its message)
        }
        else if (potential_outcome >= 1 && potential_outcome <= 20)
        {
            user.visible_message(span_warning(I18n.T("[] lands a solid [] on [], knocking them both down hard!", user, tackle_word, target)), span_userdanger(I18n.T("You land a solid [] on [], knocking you both down hard!", tackle_word, target)), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] lands a solid [] on you, knocking you both down hard!", user, tackle_word)));

            await target.apply_damage(30, STAMINA);
            await target.Paralyze(0.5f * SECONDS);
            await user.Knockdown(1 * SECONDS);
            await target.Knockdown(2 * SECONDS);
            await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
        }
        else if (potential_outcome >= 21 && potential_outcome <= 49) // really good hit, the target is definitely worse off here. Without positive modifiers, this is as good a tackle as you can land
        {
            user.visible_message(span_warning(I18n.T("[] lands an expert [] on [], knocking [] down hard while landing on [] feet with a passive grip!", user, tackle_word, target, target.p_them(), user.p_their())), span_userdanger(I18n.T("You land an expert [] on [], knocking [] down hard while landing on your feet with a passive grip!", tackle_word, target, target.p_them())), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] lands an expert [] on you, knocking you down hard and maintaining a passive grab!", user, tackle_word)));

            // Ignore_canstun has to be true, or else a stunimmune user would stay knocked down.
            await user.SetKnockdown(0, ignore_canstun: TRUE);
            user.get_up(TRUE);
            await user.forceMove(get_turf(target) as atom);
            await target.apply_damage(40, STAMINA);
            await target.Paralyze(0.5f * SECONDS);
            await target.Knockdown(3 * SECONDS);
            await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
            Waiting.Detach(do_grab(user, target));
        }
        else if (potential_outcome >= 50) // absolutely BODIED
        {
            bool stamcritted_user = HAS_TRAIT_FROM(user, TRAIT_INCAPACITATED, STAMINA);
            if (stamcritted_user) // in case the user went into stamcrit from the tackle itself and cannot actually aggro grab (since they will be crit) we make the tackle effectivelly mutually assured...stamina crit
            {
                user.visible_message(span_warning(I18n.T("[] lands a monsterly reckless [] on [], knocking both of them senseless!", user, tackle_word, target)), span_userdanger(I18n.T("You land a monsterly reckless [] on [], knocking both of you senseless!", tackle_word, target)), ignored_mobs: target);
                to_chat(target, span_userdanger(I18n.T("[] lands a monsterly reckless [] on you, knocking the both of you senseless!", user, tackle_word)));
                await user.forceMove(get_turf(target) as atom);
                await target.apply_damage(100, STAMINA); // CRASHING THIS PLANE WITH NO SURVIVORS
                await target.Paralyze(1 * SECONDS);
                await target.Knockdown(5 * SECONDS);
                await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 3, 10 * SECONDS);
            }
            else
            {
                user.visible_message(span_warning(I18n.T("[] lands a monster [] on [], knocking [] senseless and applying an aggressive pin!", user, tackle_word, target, target.p_them())), span_userdanger(I18n.T("You land a monster [] on [], knocking [] senseless and applying an aggressive pin!", tackle_word, target, target.p_them())), ignored_mobs: target);
                to_chat(target, span_userdanger(I18n.T("[] lands a monster [] on you, knocking you senseless and aggressively pinning you!", user, tackle_word)));

                // Ignore_canstun has to be true, or else a stunimmune user would stay knocked down.
                await user.SetKnockdown(0, ignore_canstun: TRUE);
                user.get_up(TRUE);
                await user.forceMove(get_turf(target) as atom);
                await target.apply_damage(60, STAMINA);
                await target.Paralyze(0.5f * SECONDS);
                await target.Knockdown(3 * SECONDS);
                await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 3, 10 * SECONDS);
                Waiting.Detach(do_grab(user, target, GRAB_AGGRESSIVE));
            }
        }
    }

    /**
     * Our neutral tackling outcome.
     */
    // tg: code/datums/components/tackle.dm:273
    public virtual async DmTask neutral_outcome(mob_living_carbon user = null, mob_living_carbon target = null, string tackle_word = null)
    {
        tackle_word ??= I18n.T("tackle");
        user.visible_message(span_warning(I18n.T("[] lands a [] on [], briefly staggering them both!", user, tackle_word, target)), span_userdanger(I18n.T("You land a [] on [], briefly staggering [] and yourself!", tackle_word, target, target.p_them())), ignored_mobs: target);
        to_chat(target, span_userdanger(I18n.T("[] lands a [] on you, briefly staggering you both!", user, tackle_word)));

        await user.SetKnockdown(0, ignore_canstun: TRUE);
        user.get_up(TRUE);
        await user.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS);
        await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS); //okay maybe slightly good for the sacker, it's a mild benefit okay?
    }

    /**
     * Our negative tackling outcomes.
     */
    // tg: code/datums/components/tackle.dm:300
    public virtual async DmTask negative_outcome(mob_living_carbon user = null, mob_living_carbon target = null, float roll = -1, string tackle_word = null)
    {
        tackle_word ??= I18n.T("tackle");
        float potential_roll_outcome = roll * -10;

        if (ishuman(user))
            potential_roll_outcome *= (100 - Dm.num(target.run_armor_check(BODY_ZONE_CHEST, MELEE))) / 100;
        else
            potential_roll_outcome *= 0.9f;

        float actual_roll = Dm.num(rand(1, potential_roll_outcome));

        if (actual_roll <= 0) //I don't want to know how this has happened, okay?
        {
            await neutral_outcome(user, target, tackle_word); //Default to neutral
        }
        else if (actual_roll >= 1 && actual_roll <= 20) // It's not completely terrible! But you are somewhat vulernable for doing it.
        {
            user.visible_message(span_warning(I18n.T("[] lands a weak [] on [], briefly staggering []!", user, tackle_word, target, target.p_them())), span_userdanger(I18n.T("You land a weak [] on [], briefly staggering []!", tackle_word, target, target.p_them())), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] lands a weak [] on you, staggering you!", user, tackle_word)));

            await user.Knockdown(1 * SECONDS);
            await user.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
            await target.apply_damage(20, STAMINA);
            await target.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
        }
        else if (actual_roll >= 21 && actual_roll <= 49) // oughe
        {
            user.visible_message(span_warning(I18n.T("[] lands a dreadful [] on [], briefly knocking [] to the ground!", user, tackle_word, target, user.p_them())), span_userdanger(I18n.T("You land a dreadful [] on [], briefly knocking you to the ground!", tackle_word, target)), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] lands a dreadful [] on you, briefly knocking [] to the ground!", user, tackle_word, user.p_them())));

            await user.Knockdown(3 * SECONDS);
            await user.apply_damage(40, STAMINA);
            await user.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
        }
        else if (actual_roll >= 50) // It has been decided that you will suffer
        {
            user.visible_message(span_danger(I18n.T("[] botches [] [] and slams [] head into [], knocking []self silly!", user, user.p_their(), tackle_word, user.p_their(), target, user.p_them())), span_userdanger(I18n.T("You botch your [] and slam your head into [], knocking yourself silly!", tackle_word, target)), ignored_mobs: target);
            to_chat(target, span_userdanger(I18n.T("[] botches [] [] and slams [] head into you, knocking []self silly!", user, user.p_their(), tackle_word, user.p_their(), user.p_them())));

            await user.Paralyze(3 * SECONDS);
            await user.apply_damage(80, STAMINA);
            await user.apply_damage(20, BRUTE, BODY_ZONE_HEAD);
            await user.gain_trauma(typeof(datum_brain_trauma_mild_concussion));
            await user.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 3, 10 * SECONDS);
        }
    }

    /**
     * This handles all of the modifiers for the actual carbon-on-carbon tackling, and gets its own proc because of how many there are (with plenty more in mind!)
     */
    // tg: code/datums/components/tackle.dm:352
    public virtual float rollTackle(mob_living_carbon target = null)
    {
        float defense_mod = 0;
        float attack_mod = 0;

        // DE-FENSE

        // Drunks are easier to knock off balance
        float target_drunkenness = Dm.num(target.get_drunk_amount());
        if (target_drunkenness > 60)
            defense_mod -= 3;
        else if (target_drunkenness > 30)
            defense_mod -= 1;

        //Arms contribute a great deal to potential tackling prowess and defense. Better arms = better bonus
        obj_item_bodypart_arm defender_arm = target.get_active_hand() as obj_item_bodypart_arm;

        if (defender_arm != null) //the target may not actually have arms
            defense_mod += Dm.num(defender_arm.unarmed_effectiveness) / 10;
        else //sucks to be you if you don't though haha
            defense_mod -= 2;

        if (HAS_TRAIT(target, TRAIT_CLUMSY))
            defense_mod -= 2;
        if (HAS_TRAIT(target, TRAIT_OFF_BALANCE_TACKLER)) // chonkers are harder to knock over
            defense_mod += 1;
        if (HAS_TRAIT(target, TRAIT_GRABWEAKNESS))
            defense_mod -= 2;

        if (HAS_TRAIT(target, TRAIT_GIANT))
            defense_mod += 2;
        if (Dm.num(target.get_organic_health()) < 50)
            defense_mod -= 1;

        float leg_wounds = 0; // -1 defense per 2 leg wounds
        foreach (datum_wound iterwound in Dm.each<datum_wound>(target.all_wounds))
        {
            if (Dm.@in(iterwound.limb.body_zone, Dm.list(BODY_ZONE_L_LEG, BODY_ZONE_R_LEG)))
                leg_wounds++;
        }
        defense_mod -= round(leg_wounds * 0.5f);

        if (target is mob_living_carbon_human tackle_target)
        {
            if (Dm.num(tackle_target.mob_height) <= HUMAN_HEIGHT_SHORTEST) //WHO ARE YOU CALLING SHORT?
                defense_mod -= 2;

            if (tackle_target.wear_suit == null && tackle_target.w_uniform == null) // who honestly puts all of their effort into tackling a naked guy?
                defense_mod += 2;
            if (Dm.truthy(tackle_target.mob_negates_gravity()))
                defense_mod += 1;
            if (HAS_TRAIT(tackle_target, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED)) // riot armor and such
                defense_mod += 5;

            obj_item_organ_tail_lizard el_tail = tackle_target.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail_lizard;
            if (HAS_TRAIT(tackle_target, TRAIT_TACKLING_TAILED_DEFENDER) && el_tail == null)
                defense_mod -= 1;
            if (el_tail != null && Dm.truthy(Dm.bitand(el_tail.wag_flags, WAG_WAGGING))) // lizard tail wagging is robust and can swat away assailants!
                defense_mod += 1;

            if (tackle_target.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
                defense_mod += potential_spine.strength_bonus;

            if (istype(tackle_target.wear_suit, typeof(obj_item_clothing_suit_hooded_cultrobes_eldritch_blade)))
                defense_mod += 8;
            if (tackle_target.wear_suit is obj_item_clothing_suit_hooded_cultrobes_eldritch_rust rust_robes && Dm.truthy(rust_robes.rusted))
                defense_mod += 10;
        }

        // OF-FENSE
        mob_living_carbon sacker = parent as mob_living_carbon;
        float sacker_drunkenness = Dm.num(sacker.get_drunk_amount());

        //Arms contribute a great deal to potential tackling prowess and defense. Better arms = better bonus
        obj_item_bodypart_arm sacker_arm = sacker.get_active_hand() as obj_item_bodypart_arm;

        if (sacker_arm != null) //I have no idea how you would be tackling without hands, but just in case
            attack_mod += Dm.num(sacker_arm.unarmed_effectiveness) / 10;
        else //I don't want to know how you got to this point but if you have, fuck you, good luck tackling without ARMS
            attack_mod -= 4;

        if (sacker_drunkenness > 60) // you're far too drunk to hold back!
            attack_mod += 1;
        else if (sacker_drunkenness > 30) // if you're only a bit drunk though, you're just sloppy
            attack_mod -= 1;

        if (HAS_TRAIT(sacker, TRAIT_CLUMSY))
            attack_mod -= 2;
        if (HAS_TRAIT(sacker, TRAIT_GIANT))
            attack_mod += 2;
        if (HAS_TRAIT(sacker, TRAIT_NOGUNS)) //Those dedicated to martial combat are particularly skilled tacklers
            attack_mod += 2;

        if (HAS_TRAIT(sacker, TRAIT_TACKLING_TAILED_POUNCE))
        {
            attack_mod += sacker.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) is obj_item_organ_tail_lizard ? 2 : -2;
        }

        if (HAS_TRAIT(sacker, TRAIT_TACKLING_WINGED_ATTACKER))
        {
            obj_item_organ_wings_moth sacker_moth_wing = sacker.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) as obj_item_organ_wings_moth;
            //Flight potion wings cannot burn off. Only the moth's natural fragile pair can cost us the bonus
            if (sacker_moth_wing == null || Dm.truthy(sacker_moth_wing.burnt))
                attack_mod -= 2;
        }
        if (sacker.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) is obj_item_organ_wings)
            attack_mod += 2;

        if (sacker.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine sacker_spine)
            attack_mod += sacker_spine.strength_bonus;

        if (sacker is mob_living_carbon_human human_sacker)
        {
            if (Dm.num(human_sacker.mob_height) <= HUMAN_HEIGHT_SHORTEST) //JUST YOU WAIT TILL I FIND A CHAIR, BUDDY, THEN YOU'LL BE SORRY
                attack_mod -= 2;

            if (Dm.eq(human_sacker.mob_mood?.sanity_level, SANITY_LEVEL_INSANE)) //I've gone COMPLETELY INSANE
            {
                attack_mod += 15;
                Waiting.Detach(human_sacker.adjust_stamina_loss(100)); //AHAHAHAHAHAHAHAHA
            }

            if (HAS_TRAIT(human_sacker, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED)) // tackling with riot specialized armor, like riot armor, is effective but tiring
            {
                attack_mod += 2;
                Waiting.Detach(human_sacker.adjust_stamina_loss(20));
            }
        }

        return Dm.num(rand(-3, 3)) - defense_mod + attack_mod + Dm.num(skill_mod);
    }

    /**
     * This is where we handle diving into dense atoms, generally with effects ranging from bad to REALLY bad.
     */
    // tg: code/datums/components/tackle.dm:505
    public virtual async DmTask<object> splat(mob_living_carbon user = null, atom hit = null)
    {
        if (hit is obj_machinery_vending darth_vendor) // before we do anything else-
        {
            await darth_vendor.tilt(user, 100);
            return null;
        }
        else if (hit is obj_structure_window W)
        {
            await splatWindow(user, W);
            if (QDELETED(W))
                return COMPONENT_MOVABLE_IMPACT_NEVERMIND;
            return null;
        }

        float oopsie_mod = 0;
        float danger_zone = (speed_value - 1) * 13; // for every extra speed we have over 1, take away 13 of the safest chance
        danger_zone = max(min(danger_zone, 100), 1);

        if (HAS_TRAIT(user, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED))
            oopsie_mod -= 6;
        if (HAS_TRAIT(user, TRAIT_HEAD_INJURY_BLOCKED))
            oopsie_mod -= 6;

        if (user.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine) // Can't snap that spine if it's made of metal.
            oopsie_mod -= potential_spine.strength_bonus;

        if (HAS_TRAIT(user, TRAIT_CLUMSY))
            oopsie_mod += 6; //honk!

        if (HAS_TRAIT(user, TRAIT_TACKLING_FRAIL_ATTACKER))
            oopsie_mod += 6; // flies don't take smacking into a window/wall easily

        float oopsie = Dm.num(rand(danger_zone, 100));
        if (oopsie >= 94 && oopsie_mod < 0) // good job avoiding getting paralyzed! gold star!
            to_chat(user, span_notice(I18n.T("You're really glad you're wearing protection!")));
        oopsie += oopsie_mod;

        if (oopsie >= 99)
        {
            // can you imagine standing around minding your own business when all of the sudden some guy fucking launches himself into a wall at full speed and irreparably paralyzes himself?
            user.visible_message(span_danger(I18n.T("[] slams face-first into [] at an awkward angle, severing [] spinal column with a sickening crack! Fucking shit!", user, hit, user.p_their())), span_userdanger(I18n.T("You slam face-first into [] at an awkward angle, severing your spinal column with a sickening crack! Fucking shit!", hit)));
            await user.apply_damage(40, BRUTE, BODY_ZONE_HEAD, wound_bonus: 40);
            await user.apply_damage(30, STAMINA);
            playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 60, TRUE);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 70, TRUE);
            playsound(user, Dm.resource("sound/effects/wounds/crack2.ogg"), 70, TRUE);
            await user.emote("scream");
            await user.gain_trauma(typeof(datum_brain_trauma_severe_paralysis_paraplegic)); // oopsie indeed!
            shake_camera(user, 7, 7);
            await user.flash_act(1, TRUE, TRUE, length: 4.5f);
        }
        else if (oopsie >= 97 && oopsie <= 98)
        {
            user.visible_message(span_danger(I18n.T("[] slams skull-first into [] with a sound like crumpled paper, revealing a horrifying breakage in [] cranium! Holy shit!", user, hit, user.p_their())), span_userdanger(I18n.T("You slam skull-first into [] and your senses are filled with warm goo flooding across your face! Your skull is open!", hit)));
            await user.apply_damage(30, BRUTE, BODY_ZONE_HEAD, wound_bonus: 25);
            await user.apply_damage(30, STAMINA);
            await user.gain_trauma_type(typeof(datum_brain_trauma_mild));
            playsound(user, Dm.resource("sound/effects/blob/blobattack.ogg"), 60, TRUE);
            playsound(user, Dm.resource("sound/effects/splat.ogg"), 70, TRUE);
            await user.emote("gurgle");
            shake_camera(user, 7, 7);
            await user.flash_act(1, TRUE, TRUE, length: 4.5f);
        }
        else if (oopsie >= 93 && oopsie <= 96)
        {
            user.visible_message(span_danger(I18n.T("[] slams face-first into [] with a concerning squish, immediately going limp!", user, hit)), span_userdanger(I18n.T("You slam face-first into [], and immediately lose consciousness!", hit)));
            await user.apply_damage(30, BRUTE, spread_damage: TRUE);
            await user.apply_damage(30, STAMINA);
            await user.Unconscious(10 * SECONDS);
            await user.gain_trauma_type(typeof(datum_brain_trauma_mild));
            user.playsound_local(get_turf(user), Dm.resource("sound/items/weapons/flashbang.ogg"), 100, TRUE, 8);
            shake_camera(user, 6, 6);
            await user.flash_act(1, TRUE, TRUE, length: 3.5f);
        }
        else if (oopsie >= 86 && oopsie <= 92)
        {
            user.visible_message(span_danger(I18n.T("[] slams head-first into [], suffering major cranial trauma!", user, hit)), span_userdanger(I18n.T("You slam head-first into [], and the world explodes around you!", hit)));
            await user.apply_damage(30, BRUTE, spread_damage: TRUE);
            await user.apply_damage(30, STAMINA);
            await user.adjust_confusion(15 * SECONDS);
            if (prob(80))
                await user.gain_trauma(typeof(datum_brain_trauma_mild_concussion));
            user.playsound_local(get_turf(user), Dm.resource("sound/items/weapons/flashbang.ogg"), 100, TRUE, 8);
            await user.Knockdown(4 * SECONDS);
            shake_camera(user, 5, 5);
            await user.flash_act(1, TRUE, TRUE, length: 2.5f);
        }
        else if (oopsie >= 68 && oopsie <= 85)
        {
            user.visible_message(span_danger(I18n.T("[] slams hard into [], knocking [] senseless!", user, hit, user.p_them())), span_userdanger(I18n.T("You slam hard into [], knocking yourself senseless!", hit)));
            await user.apply_damage(10, BRUTE, spread_damage: TRUE);
            await user.apply_damage(30, STAMINA);
            await user.adjust_confusion(10 * SECONDS);
            await user.Knockdown(3 * SECONDS);
            shake_camera(user, 3, 4);
        }
        else if (oopsie >= 1 && oopsie <= 67)
        {
            user.visible_message(span_danger(I18n.T("[] slams into []!", user, hit)), span_userdanger(I18n.T("You slam into []!", hit)));
            await user.apply_damage(10, BRUTE, spread_damage: TRUE);
            await user.apply_damage(20, STAMINA);
            await user.Knockdown(2 * SECONDS);
            shake_camera(user, 2, 2);
        }

        playsound(user, Dm.resource("sound/items/weapons/smash.ogg"), 70, TRUE);
        return null;
    }

    // tg: code/datums/components/tackle.dm:606
    public virtual void resetTackle()
    {
        tackling = FALSE;
        qdel(tackle_ref); // QDEL_NULL
        tackle_ref = null;
        UnregisterSignal(parent, COMSIG_MOVABLE_MOVED);
    }

    ///A special case for splatting for handling windows
    // tg: code/datums/components/tackle.dm:612
    public virtual async DmTask splatWindow(mob_living_carbon user = null, obj_structure_window windscreen_casualty = null)
    {
        playsound(user, Dm.resource("sound/effects/glass/Glasshit.ogg"), 140, TRUE);

        Type windscreen_type = windscreen_casualty.GetType();
        if (windscreen_type == typeof(obj_structure_window) || windscreen_type == typeof(obj_structure_window_fulltile) || windscreen_type == typeof(obj_structure_window_unanchored) || windscreen_type == typeof(obj_structure_window_fulltile_unanchored)) // boring unreinforced windows
        {
            for (int i = 1; i <= speed_value; i++)
            {
                obj_item_shard shard = Dm.@new<obj_item_shard>(get_turf(user));
                datum_embedding embed = shard.get_embed() as datum_embedding;
                embed.embed_chance = 100;
                embed.ignore_throwspeed_threshold = TRUE;
                embed.impact_pain_mult = 1;
                await user.hitby(shard, skipcatch: TRUE, hitpush: FALSE);
                embed.embed_chance = Dm.initial(embed, (datum_embedding p) => p.embed_chance);
                embed.ignore_throwspeed_threshold = Dm.initial(embed, (datum_embedding p) => p.ignore_throwspeed_threshold);
                embed.impact_pain_mult = Dm.initial(embed, (datum_embedding p) => p.impact_pain_mult);
            }
            await windscreen_casualty.atom_destruction();
            await user.adjust_stamina_loss(10 * speed_value);
            await user.Paralyze(3 * SECONDS);
            user.visible_message(span_danger(I18n.T("[] smacks into [] and shatters it, shredding []self with glass!", user, windscreen_casualty, user.p_them())), span_userdanger(I18n.T("You smacks into [] and shatter it, shredding yourself with glass!", windscreen_casualty)));
        }
        else
        {
            user.visible_message(span_danger(I18n.T("[] smacks into [] like a bug!", user, windscreen_casualty)), span_userdanger(I18n.T("You smacks into [] like a bug!", windscreen_casualty)));
            await user.Paralyze(1 * SECONDS);
            await user.Knockdown(3 * SECONDS);
            await windscreen_casualty.take_damage(30 * speed_value);
            await user.adjust_stamina_loss(10 * speed_value, updating_stamina: FALSE);
            await user.adjust_brute_loss(5 * speed_value);
        }
    }

    // tg: code/datums/components/tackle.dm:639
    public virtual async DmTask delayedSmash(obj_structure_window windscreen_casualty = null)
    {
        if (windscreen_casualty != null)
        {
            await windscreen_casualty.atom_destruction();
            playsound(windscreen_casualty, SFX_SHATTER, 70, TRUE);
        }
    }

    ///Check to see if we hit a table, and if so, make a big mess!
    // tg: code/datums/components/tackle.dm:645
    [SignalHandler]
    public virtual void checkObstacle(mob_living_carbon owner = null, params object[] __extra)
    {
        if (!Dm.truthy(tackling))
            return;

        turf our_turf = get_turf(owner) as turf;
        obj_structure_table kevved = Dm.locate_type(typeof(obj_structure_table), our_turf) as obj_structure_table;
        if (kevved == null)
            return;

        DmList messes = Dm.list();

        // we split the mess-making into two parts (check what we're gonna send flying, intermission for dealing with the tackler, then actually send stuff flying) for the benefit of making sure the face-slam text
        // comes before the list of stuff that goes flying, but can still adjust text + damage to how much of a mess it made
        foreach (obj_item item_in_turf in Dm.each<obj_item>(our_turf))
        {
            if (!Dm.truthy(item_in_turf.anchored))
            {
                messes.Add(item_in_turf);
                if (length(messes) >= MAX_TABLE_MESSES)
                    break;
            }
        }

        // for telling HOW big of a mess we just made
        string HOW_big_of_a_miss_did_we_just_make = "";
        int mess_count = (int)length(messes);
        if (mess_count > 0)
        {
            if (mess_count < MAX_TABLE_MESSES * 0.125)
                HOW_big_of_a_miss_did_we_just_make = I18n.T(", making a mess");
            else if (mess_count < MAX_TABLE_MESSES * 0.25)
                HOW_big_of_a_miss_did_we_just_make = I18n.T(", making a big mess");
            else if (mess_count < MAX_TABLE_MESSES * 0.5)
                HOW_big_of_a_miss_did_we_just_make = I18n.T(", making a giant mess");
            else if (mess_count < MAX_TABLE_MESSES)
                HOW_big_of_a_miss_did_we_just_make = I18n.T(", making a gnarly mess");
            else
                HOW_big_of_a_miss_did_we_just_make = I18n.T(", making a ginormous mess!"); // an extra exclamation point!! for emphasis!!!
        }

        owner.visible_message(span_danger(I18n.T("[] trips over [] and slams into it face-first[]!", owner, kevved, HOW_big_of_a_miss_did_we_just_make)), span_userdanger(I18n.T("You trip over [] and slam into it face-first[]!", kevved, HOW_big_of_a_miss_did_we_just_make)));
        Waiting.Detach(owner.adjust_stamina_loss(15 + mess_count * 2, updating_stamina: FALSE));
        Waiting.Detach(owner.adjust_brute_loss(8 + mess_count, updating_health: FALSE));
        Waiting.Detach(owner.Paralyze(0.4f * SECONDS * mess_count)); // .4 seconds of paralyze for each thing you knock around
        Waiting.Detach(owner.Knockdown(2 * SECONDS + 0.4f * SECONDS * mess_count)); // 2 seconds of knockdown after the paralyze
        Waiting.Detach(owner.updatehealth());

        foreach (obj_item item_in_mess in Dm.each<obj_item>(messes))
        {
            // The amount of distance the object flies away when launched by our tackle
            float item_launch_distance = Dm.num(rand(1, 3));
            // The transfered speed at which an item is launched by our tackle
            float item_launch_speed = 2;
            if (prob(25 * (speed_value - 1))) // if our tackle speed is higher than 1, with chance (speed - 1 * 25%), throw the thing at our tackle speed + 1
                item_launch_speed = speed_value + 1;
            Waiting.Detach(item_in_mess.throw_at(get_ranged_target_turf(item_in_mess, pick(GLOB.alldirs), item_launch_distance) as atom, item_launch_distance, item_launch_speed));
            item_in_mess.visible_message(span_danger(I18n.T("[] goes flying[]!", item_in_mess, item_launch_speed < EMBED_THROWSPEED_THRESHOLD ? "" : I18n.T(" dangerously fast")))); // standard embed speed
        }

        datum_thrownthing tackle = tackle_ref?.resolve() as datum_thrownthing;

        playsound(owner, Dm.resource("sound/items/weapons/smash.ogg"), 70, TRUE);
        if (tackle != null)
            tackle.finalize(TRUE);
        resetTackle();
    }
}
