// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/boxing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3b (D316): boxing (the tussle, its combos by the clicked button and the active hand, the haymaker, the
// throw mode block), evil boxing and hunter boxing, whole; the athletics skill's damage, knockout and block bonuses, the boxing
// experience and the exercised status effect came with gaps SL1 (D333). The var blocks are generated (D111).

// tg: code/datums/martial/boxing.dm:16
public partial class datum_martial_art_boxing : datum_martial_art
{
    // tg: code/datums/martial/boxing.dm:1-14  #define LEFT_RIGHT_COMBO "DH" ... (file-local, #undef at :515-529)
    private const string LEFT_RIGHT_COMBO = "DH";
    private const string RIGHT_LEFT_COMBO = "HD";
    private const string LEFT_LEFT_COMBO = "HH";
    private const string RIGHT_RIGHT_COMBO = "DD";

    protected const string STRAIGHT_PUNCH = "straight_punch";
    protected const string RIGHT_HOOK = "right_hook";
    protected const string LEFT_HOOK = "left_hook";
    protected const string UPPERCUT = "uppercut";
    protected const string LIGHT_JAB = "light_jab";
    protected const string DISCOMBOBULATE = "discombobulate";
    protected const string BLIND_JAB = "blind_jab";
    protected const string CRAVEN_BLOW = "craven_blow";
    protected const string NO_COMBO = "";

    // tg: code/datums/martial/boxing.dm:30
    public override object can_teach(mob_living new_holder = null)
    {
        return ishuman(new_holder) ? TRUE : FALSE;
    }

    // tg: code/datums/martial/boxing.dm:33
    public override async DmTask activate_style(mob_living new_holder = null)
    {
        await base.activate_style(new_holder);
        new_holder.add_traits(boxing_traits, BOXING_TRAIT);
        RegisterSignal(new_holder, COMSIG_LIVING_CHECK_BLOCK, nameof(check_block));
    }

    // tg: code/datums/martial/boxing.dm:38
    public override async DmTask deactivate_style(mob_living remove_from = null)
    {
        remove_from.remove_traits(boxing_traits, BOXING_TRAIT);
        UnregisterSignal(remove_from, Dm.list(COMSIG_LIVING_CHECK_BLOCK));
        await base.deactivate_style(remove_from);
    }

    ///Unlike most instances of this proc, this is actually called in _proc/tussle()
    ///Returns a multiplier on our skill damage bonus.
    // tg: code/datums/martial/boxing.dm:45
    public virtual async DmTask<string> check_streak(mob_living attacker = null, mob_living defender = null, obj_item_bodypart_arm active_arm = null)
    {
        if (Dm.truthy(check_behind(attacker, defender)) && !Dm.truthy(honorable_boxer))
        {
            await reset_streak();
            return CRAVEN_BLOW;
        }

        if (HAS_TRAIT(attacker, TRAIT_DETECTIVES_TASTE) && Dm.truthy(defender.is_blind())) //In short: discombobulate
        {
            await reset_streak();
            return DISCOMBOBULATE;
        }

        if (Dm.truthy(findtext(streak, LEFT_LEFT_COMBO)) && Dm.eq(active_arm.body_zone, BODY_ZONE_R_ARM) || Dm.truthy(findtext(streak, RIGHT_RIGHT_COMBO)) && Dm.eq(active_arm.body_zone, BODY_ZONE_L_ARM))
        {
            await reset_streak();
            if (Dm.truthy(attacker.is_blind()))
                return BLIND_JAB;
            else
                return LIGHT_JAB;
        }
        else if (Dm.truthy(findtext(streak, LEFT_LEFT_COMBO)) && Dm.eq(active_arm.body_zone, BODY_ZONE_L_ARM) || Dm.truthy(findtext(streak, RIGHT_RIGHT_COMBO)) && Dm.eq(active_arm.body_zone, BODY_ZONE_R_ARM))
        {
            await reset_streak();
            return STRAIGHT_PUNCH;
        }

        if (Dm.truthy(findtext(streak, LEFT_RIGHT_COMBO)) || Dm.truthy(findtext(streak, RIGHT_LEFT_COMBO)))
        {
            await reset_streak();
            if (Dm.eq(active_arm.body_zone, BODY_ZONE_L_ARM))
            {
                if (Dm.truthy(findtext(streak, RIGHT_LEFT_COMBO)))
                    return LEFT_HOOK;
            }
            else if (Dm.eq(active_arm.body_zone, BODY_ZONE_R_ARM))
            {
                if (Dm.truthy(findtext(streak, LEFT_RIGHT_COMBO)))
                    return RIGHT_HOOK;
            }
            else
            {
                return UPPERCUT;
            }
        }

        return NO_COMBO;
    }

    /// An extra effect on some moves and attacks.
    // tg: code/datums/martial/boxing.dm:80
    public virtual DmTask perform_extra_effect(mob_living attacker = null, mob_living defender = null)
    {
        return DmTask.CompletedTask;
    }

    // tg: code/datums/martial/boxing.dm:83
    public override async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(honor_check(defender)))
            await add_to_streak("D", defender);
        await tussle(attacker, defender);
        return MARTIAL_ATTACK_SUCCESS;
    }

    // tg: code/datums/martial/boxing.dm:89
    public override async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(honorable_boxer) && !Dm.truthy(ignore_grab_restriction))
        {
            attacker.balloon_alert(attacker, "no grabbing while boxing!");
            return MARTIAL_ATTACK_FAIL;
        }
        return MARTIAL_ATTACK_INVALID; //UNLESS YOU'RE EVIL
    }

    // tg: code/datums/martial/boxing.dm:95
    public override async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(honor_check(defender)))
            await add_to_streak("H", defender);
        await tussle(attacker, defender);
        return MARTIAL_ATTACK_SUCCESS;
    }

    // Our only boxing move, which occurs on literally all attacks; the tussle. However, quite a lot morphs the results of this proc. Combos, unlike most martial arts attacks, are checked in this proc rather than our standard unarmed procs
    // tg: code/datums/martial/boxing.dm:102
    public virtual async DmTask<object> tussle(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(honorable_boxer)) //Being a good sport, you never hit someone on the ground or already knocked down. It shows you're the better person.
        {
            if ((Dm.eq(defender.body_position, LYING_DOWN) && Dm.num(defender.get_stamina_loss()) >= 100) || IS_UNCONSCIOUS(defender)) //If they're in stamcrit or unconscious, don't bloody punch them
            {
                attacker.balloon_alert(attacker, "unsportsmanlike behaviour!");
                return FALSE;
            }
        }

        var active_arm = attacker.get_active_hand() as obj_item_bodypart_arm;

        //The values between which damage is rolled for punches
        float lower_force = Dm.num(active_arm.unarmed_damage_low);
        float upper_force = Dm.num(active_arm.unarmed_damage_high);

        //Determines knockout potential and armor penetration (if that matters)
        float base_unarmed_effectiveness = Dm.num(active_arm.unarmed_effectiveness);

        //Determines attack sound based on attacker arm
        object attack_sound = active_arm.unarmed_attack_sound;

        // Out athletics skill is added as a damage bonus
        float athletics_skill = Dm.num(attacker.mind?.get_skill_level(typeof(datum_skill_athletics)));

        // If true, grants experience for punching; we only gain experience if we punch another boxer.
        bool grant_experience = false;

        // What type of damage does our kind of boxing do? Defaults to STAMINA for normal boxing, unless you're performing EVIL BOXING. Subtypes use different damage types.
        string damage_type = Dm.truthy(honorable_boxer) ? default_damage_type : Dm.str(attacker.get_attack_type());

        await attacker.do_attack_animation(defender, ATTACK_EFFECT_PUNCH);

        // Our potential wound bonus on a punch. Only applies if we're dishonorable. Otherwise, we can't wound.
        float possible_wound_bonus = Dm.truthy(honorable_boxer) ? 0 : CANT_WOUND;

        // Determines damage dealt on a punch. Against a boxing defender, we apply our skill bonus.
        float damage = rand(lower_force, upper_force);

        // Attack verbs for our visible chat messages.
        string current_atk_verb = "punches";
        string current_atk_verbed = "punched";

        if (Dm.truthy(await defender.check_block(attacker, damage, Dm.text("[]'s punch", attacker), UNARMED_ATTACK)))
            return FALSE;

        // Similar to a normal punch, should we have a value of 0 for our lower force, we simply miss outright.
        if (lower_force == 0)
        {
            playsound(defender.loc, Dm.str(active_arm.unarmed_miss_sound), 25, TRUE, -1);
            defender.visible_message(span_warning(I18n.T("[]'s punch misses []!", attacker, defender)),
                span_danger(I18n.T("You avoid []'s punch!", attacker)), span_hear(I18n.T("You hear a swoosh!")), COMBAT_MESSAGE_RANGE, attacker);
            to_chat(attacker, span_warning(I18n.T("Your punch misses []!", defender)));
            log_combat(attacker, defender, "attempted to hit", "punch (boxing) ");
            return FALSE;
        }

        var affecting = defender.get_bodypart(Dm.str(defender.get_random_valid_zone(attacker.zone_selected))) as obj_item_bodypart;

        if (Dm.truthy(honor_check(defender)))
        {
            float strength_bonus = HAS_TRAIT(attacker, TRAIT_STRENGTH) ? 2 : 0; //Investing into genetic strength improvements makes you a better boxer

            var potential_spine = attacker.get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine; //Getting a cyberspine also pushes you further than just mere meat
            if (potential_spine != null)
                strength_bonus *= Dm.num(potential_spine.strength_bonus);

            string streak_augmentation = await check_streak(attacker, defender, active_arm);

            float combo_multiplier = 0;

            switch (streak_augmentation)
            {
                case STRAIGHT_PUNCH:
                    current_atk_verb = "straight punches";
                    current_atk_verbed = "straight punched";
                    combo_multiplier = 1;
                    break;

                case LIGHT_JAB:
                    current_atk_verb = "light jabs";
                    current_atk_verbed = "light jabbed";
                    combo_multiplier = 1;
                    break;

                case LEFT_HOOK:
                    current_atk_verb = "left hooks";
                    current_atk_verbed = "left hooked";
                    combo_multiplier = 1.5f;
                    attacker.changeNext_move(CLICK_CD_MELEE * 1.5f);
                    break;

                case RIGHT_HOOK:
                    current_atk_verb = "right hooks";
                    current_atk_verbed = "right hooked";
                    combo_multiplier = 1.5f;
                    attacker.changeNext_move(CLICK_CD_MELEE * 1.5f);
                    break;

                case UPPERCUT:
                    current_atk_verb = "uppercuts";
                    current_atk_verbed = "uppercutted";
                    base_unarmed_effectiveness *= 1.5f;
                    combo_multiplier = 1;
                    attacker.changeNext_move(CLICK_CD_MELEE * 1.5f);
                    break;

                case DISCOMBOBULATE:
                    current_atk_verb = "discombobulates";
                    current_atk_verbed = "discombobulated";
                    affecting = defender.get_bodypart(Dm.str(defender.get_random_valid_zone(BODY_ZONE_HEAD))) as obj_item_bodypart;
                    await defender.adjust_confusion_up_to(20 * SECONDS, 50 * SECONDS);
                    await defender.adjust_dizzy_up_to(20 * SECONDS, 50 * SECONDS);
                    combo_multiplier = 1;
                    break;

                case BLIND_JAB:
                    current_atk_verb = "blind jabs";
                    current_atk_verbed = "blind jabbed";
                    combo_multiplier = 0.5f;
                    attacker.changeNext_move(CLICK_CD_MELEE * 1.5f);
                    break;

                case CRAVEN_BLOW:
                    current_atk_verb = "sucker punches";
                    current_atk_verbed = "sucker punch";
                    possible_wound_bonus = damage;
                    combo_multiplier = 2;
                    possible_wound_bonus *= 1.5f;
                    affecting = defender.get_bodypart(Dm.str(defender.get_random_valid_zone(BODY_ZONE_HEAD))) as obj_item_bodypart;
                    await defender.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS); //why yes, this could result in them being knocked out in one.
                    break;
            }

            damage += round((athletics_skill + strength_bonus) * combo_multiplier, 1);

            if (combo_multiplier >= 1)
                await perform_extra_effect(attacker, defender);

            if (Dm.num(defender.stat) <= HARD_CRIT) // Do not grant experience against dead targets
                grant_experience = true;
        }

        float armor_block = Dm.num(defender.run_armor_check(affecting, MELEE, armour_penetration: base_unarmed_effectiveness));

        playsound(defender, Dm.str(attack_sound), 25, TRUE, -1);

        defender.visible_message(
            span_danger(I18n.T("[] [] []!", attacker, current_atk_verb, defender)),
            span_userdanger(I18n.T("You're [] by []!", current_atk_verbed, attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);

        to_chat(attacker, span_danger(I18n.T("You [] []!", current_atk_verbed, defender)));

        // Determines the total amount of experience earned per punch
        float experience_earned = round(damage / 4, 1);

        await defender.apply_damage(damage, damage_type, affecting, armor_block, wound_bonus: possible_wound_bonus);

        log_combat(attacker, defender, "punched (boxing) ");

        if (Dm.eq(defender.stat, DEAD) || !Dm.truthy(honor_check(defender))) //early returning here so we don't worry about knockout probs or experience gain
            return TRUE;

        if (grant_experience)
            await skill_experience_adjustment(attacker, defender, damage / lower_force);

        //Determine our attackers athletics level as a knockout probability bonus
        float attacker_athletics_skill = Dm.num(attacker.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_RANDS_MODIFIER)) + base_unarmed_effectiveness;

        // Defender boxing skill and armor block are used as a defense here. This has already factored in base_unarmed_effectiveness from the attacker
        float defender_athletics_skill = Dm.num(clamp(defender.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_RANDS_MODIFIER), 0, 100));

        //Determine our final probability, using a clamp to stop any prob() weirdness.
        float final_knockout_probability = clamp(round(attacker_athletics_skill - defender_athletics_skill, 1), 0, 100);

        if (!prob(final_knockout_probability))
            return TRUE;

        await crit_effect(attacker, defender, armor_block, damage_type, damage);

        experience_earned *= 2; //Double our experience gain on a crit hit

        playsound(defender, "sound/effects/coin2.ogg", 40, TRUE);
        Dm.@new<obj_effect_temp_visual_crit>(get_turf(defender));
        await skill_experience_adjustment(attacker, defender, experience_earned); //double experience for a successful crit

        return TRUE;
    }

    /// Our crit effect. For normal boxing, this applies a stagger, then applies a knockout if they're staggered. Other types of boxing apply different kinds of effects.
    // tg: code/datums/martial/boxing.dm:278
    public virtual async DmTask crit_effect(mob_living attacker = null, mob_living defender = null, float armor_block = 0, string damage_type = STAMINA, float damage = 0)
    {
        if (Dm.truthy(defender.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))))
        {
            defender.visible_message(
                span_danger(I18n.T("[] knocks [] out with a haymaker!", attacker, defender)),
                span_userdanger(I18n.T("You're knocked unconscious by []!", attacker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            to_chat(attacker, span_danger(I18n.T("You knock [] out with a haymaker!", defender)));
            await defender.apply_effect(20 * SECONDS, EFFECT_KNOCKDOWN, armor_block);
            await defender.SetSleeping(10 * SECONDS);
            log_combat(attacker, defender, "knocked out (boxing) ");
        }
        else
        {
            defender.visible_message(
                span_danger(I18n.T("[] staggers [] with a haymaker!", attacker, defender)),
                span_userdanger(I18n.T("You're nearly knocked off your feet by []!", attacker)),
                span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
                COMBAT_MESSAGE_RANGE,
                attacker);
            await defender.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS);
            to_chat(attacker, span_danger(I18n.T("You stagger [] with a haymaker!", defender)));
            log_combat(attacker, defender, "staggered (boxing) ");
        }

        if (attacker.pulling == defender && Dm.num(attacker.grab_state) >= GRAB_AGGRESSIVE) // dubious a normal boxer will be in a state where this happens, buuuut.
        {
            var throw_target = get_edge_target_turf(defender, attacker.dir) as atom;
            await defender.throw_at(throw_target, 2, 2, attacker);
        }
    }

    /// Returns whether whoever is checked by this proc is complying with the rules of boxing. The boxer cannot block non-boxers, and cannot apply their scariest moves against non-boxers.
    // tg: code/datums/martial/boxing.dm:308
    public virtual object honor_check(mob_living possible_boxer = null)
    {
        if (!Dm.truthy(honorable_boxer))
            return TRUE; //You scoundrel!!

        if (!HAS_TRAIT(possible_boxer, TRAIT_BOXING_READY))
            return FALSE;

        return TRUE;
    }

    /// Handles our instances of experience gain while boxing. It also applies the exercised status effect.
    // tg: code/datums/martial/boxing.dm:318
    public virtual async DmTask skill_experience_adjustment(mob_living boxer = null, mob_living defender = null, float experience_value = 0)
    {
        //Boxing in heavier gravity gives you more experience
        float gravity_modifier = Dm.num(boxer.has_gravity()) > STANDARD_GRAVITY ? 1 : 2;

        //You gotta sleep before you get any experience!
        boxer.mind?.adjust_experience(typeof(datum_skill_athletics), round(experience_value / gravity_modifier, 1));
        await boxer.apply_status_effect(typeof(datum_status_effect_exercised));
    }

    /// Handles our blocking signals, similar to hit_reaction() on items. Only blocks while the boxer is in throw mode.
    // tg: code/datums/martial/boxing.dm:327
    [SignalHandler]
    public virtual object check_block(mob_living boxer = null, atom_movable hitby = null, object damage = null, object attack_text = null, object attack_type = null, params object[] __extra)
    {
        if (!Dm.truthy(can_use(boxer)) || !Dm.truthy(boxer.throw_mode) || Dm.truthy(INCAPACITATED_IGNORING(boxer, INCAPABLE_GRAB)))
            return NONE;

        if (!Dm.eq(attack_type, UNARMED_ATTACK))
            return NONE;

        //Determines unarmed defense against boxers using our current active arm.
        var active_arm = boxer.get_active_hand() as obj_item_bodypart_arm;
        float base_unarmed_effectiveness = Dm.num(active_arm.unarmed_effectiveness);

        // Out athletics skill is added to our block potential
        float athletics_skill_rands = Dm.num(boxer.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_RANDS_MODIFIER));

        float block_chance = base_unarmed_effectiveness + athletics_skill_rands;

        string block_text = Dm.str(pick("block", "evade"));

        var attacker = get(hitby, typeof(mob_living)) as mob_living; // GET_ASSAILANT(hitby)

        if (!Dm.truthy(honor_check(attacker)))
            return NONE;

        float experience_earned = round(Dm.num(damage) / 4, 1);

        if (!Dm.truthy(damage))
            experience_earned = 2;

        // WE reward experience for getting punched while boxing
        Waiting.NoSleep(skill_experience_adjustment(boxer, attacker, experience_earned)); //just getting hit a bunch doesn't net you much experience however

        if (!prob(block_chance))
            return NONE;

        if (attacker != null && Dm.truthy(Waiting.NoSleep(boxer.Adjacent(attacker))))
        {
            Waiting.NoSleep(attacker.apply_damage(10, default_damage_type));
            Waiting.NoSleep(boxer.apply_damage(5, STAMINA));
            Waiting.NoSleep(perform_extra_effect(boxer, attacker));
        }

        boxer.visible_message(
            span_danger(I18n.T("[] []s []!", boxer, block_text, attack_text)),
            span_userdanger(I18n.T("You [] []!", block_text, attack_text)));
        if (block_text == "evade")
            playsound(boxer.loc, Dm.str(active_arm.unarmed_miss_sound), 25, TRUE, -1);

        return SUCCESSFUL_BLOCK;
    }

    // tg: code/datums/martial/boxing.dm:377
    public override object can_use(mob_living martial_artist = null)
    {
        if (!ishuman(martial_artist))
            return FALSE;
        return base.can_use(martial_artist);
    }

    // tg: code/datums/martial/boxing.dm:382
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, I18n.T("<b><i>You focus on your form, visualizing how best to throw a punch.</i></b>"));
        Dm.append(dot, I18n.T("<b><i>What moves you perform depend on what mouse buttons you click, and whether the last button clicked matches which hand you have selected when you throw the last punch.</i></b>"));

        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the matching hand. Regular damage.", span_notice("Straight Punch")));
        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the opposite hand. Regular damage. If you're blind, you'll make a blind jab instead.", span_notice("Jab")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the matching hand. Does extra damage, but slows your next hit.", span_notice("Left/Right Hook")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the opposite hand. Has a higher probability to knock out the target, but slows your next hit.</b>", span_notice("Uppercut")));

        Dm.append(dot, I18n.T("<b><i>While in Throw Mode, you can block incoming punches and return a bit of damage back to an attacker. Blocking attacks this way causes you to lose some stamina damage.</i></b>"));
        Dm.append(dot, I18n.T("<b><i>Your boxing abilities are only able to be used on other boxers.</i></b>"));
        return dot;
    }
}

/// Evil Boxing; for sick, evil scoundrels. Has no honor, making it more lethal (therefore unable to be used by pacifists).
/// Grants Strength and Stimmed to speed up any experience gain.
// tg: code/datums/martial/boxing.dm:402
public partial class datum_martial_art_boxing_evil : datum_martial_art_boxing
{
    // tg: code/datums/martial/boxing.dm:410
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, I18n.T("<b><i>You contemplate on the violence ahead, visualizing how best to throw a punch.</i></b>"));
        Dm.append(dot, I18n.T("<b><i>What moves you perform depend on what mouse buttons you click, and whether the last button clicked matches which hand you have selected when you throw the last punch.</i></b>"));

        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the matching hand. Regular damage.", span_notice("Straight Punch")));
        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the opposite hand. Regular damage. If you're blind, you'll make a blind jab instead.", span_notice("Jab")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the matching hand. Does extra damage, but slows your next hit.", span_notice("Left/Right Hook")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the opposite hand. Has a higher probability to knock out the target, but slows your next hit.", span_notice("Uppercut")));
        Dm.append(dot, I18n.T("[]: Any combination done to a vulnerable target becomes a sucker punch. This could knock them out in one!.</b>", span_notice("Sucker Punch")));

        Dm.append(dot, I18n.T("<b><i>While in Throw Mode, you can block incoming punches and return a bit of damage back to an attacker. Blocking attacks this way causes you to lose some stamina damage.</i></b>"));
        return dot;
    }
}

/// Hunter Boxing: for the uncaring, completely deranged one-spacer ecological disaster.
/// The honor check accepts boxing ready targets, OR various biotypes as valid targets. Uses a special crit effect rather than the standard one (against monsters).
/// I guess technically, this allows for lethal boxing. If you want.
// tg: code/datums/martial/boxing.dm:428
public partial class datum_martial_art_boxing_hunter : datum_martial_art_boxing
{
    // tg: code/datums/martial/boxing.dm:442
    public override object get_style_help()
    {
        DmList dot = Dm.list();

        Dm.append(dot, I18n.T("<b><i>You focus on your Fists. You focus on Adventure. You focus on the Hunt.</i></b>"));
        Dm.append(dot, I18n.T("<b><i>What moves you perform depend on what mouse buttons you click, and whether the last button clicked matches which hand you have selected when you throw the last punch.</i></b>"));

        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the matching hand. Regular damage.", span_notice("Straight Punch")));
        Dm.append(dot, I18n.T("[]: Left Left/Right Right with the opposite hand. Regular damage. If you're blind, you'll make a blind jab instead.", span_notice("Jab")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the matching hand. Does extra damage, but slows your next hit.", span_notice("Left/Right Hook")));
        Dm.append(dot, I18n.T("[]: Left Right/Right Left with the opposite hand. Has a higher probability to critically hit the target, but slows your next hit.</b>", span_notice("Uppercut")));

        Dm.append(dot, I18n.T("<b><i>While in Throw Mode, you can block incoming punches and return a bit of damage back to an attacker. Blocking attacks this way causes you to lose some stamina damage.</i></b>"));
        Dm.append(dot, I18n.T("<b><i>Stringing together effective combos restores some of your health and deals even more damage.</i></b>"));

        Dm.append(dot, I18n.T("<b><i>Your hunter boxing abilities are only able to be used on the various flora, fauna and unnatural creatures that reside in this universe. Against normal humanoids, you are just a boxer.</i></b>"));
        return dot;
    }

    // tg: code/datums/martial/boxing.dm:459
    public override object honor_check(mob_living possible_boxer = null)
    {
        if (HAS_TRAIT(possible_boxer, TRAIT_BOXING_READY))
            return TRUE;

        if (Dm.truthy(Dm.bitand(possible_boxer.mob_biotypes, MOB_HUMANOID)) && possible_boxer is not mob_living_simple_animal_hostile_megafauna) //We're after animals, not people. Unless they want to box. (Or a megafauna)
            return FALSE;

        if (Dm.truthy(Dm.bitand(possible_boxer.mob_biotypes, honorable_mob_biotypes))) //We're after animals, not people
            return TRUE;

        return FALSE; //rather than default assume TRUE, we default assume FALSE. After all, there could be mobs that are none of our biotypes and also not humanoid. By default, they would be valid for being boxed if TRUE.
    }

    // Our hunter boxer applies a rebuke and double damage against the target of their crit. If the target is humanoid, we just perform our regular crit effect instead.
    // tg: code/datums/martial/boxing.dm:473
    public override async DmTask crit_effect(mob_living attacker = null, mob_living defender = null, float armor_block = 0, string damage_type = STAMINA, float damage = 0)
    {
        if (Dm.truthy(Dm.bitand(defender.mob_biotypes, MOB_HUMANOID)) && defender is not mob_living_simple_animal_hostile_megafauna)
        {
            await base.crit_effect(attacker, defender, armor_block, damage_type, damage); //Applies the regular crit effect if it is a normal human, and not a megafauna
            return;
        }

        object first_word_pick = pick(first_word_strike);
        object second_word_pick = pick(second_word_strike);

        defender.visible_message(
            span_danger(I18n.T("[] knocks the absolute bajeezus out of [] utilizing the terrifying [][]!!!", attacker, defender, first_word_pick, second_word_pick)),
            span_userdanger(I18n.T("You have the absolute bajeezus knocked out of you by []!!!", attacker)),
            span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")),
            COMBAT_MESSAGE_RANGE,
            attacker);
        to_chat(attacker, span_danger(I18n.T("You knock the absolute bajeezus out of [] out with the terrifying [][]!!!", defender, first_word_pick, second_word_pick)));
        if (attacker is mob_living_carbon_human human_attacker)
        {
            human_attacker.force_say();
            await human_attacker.say(Dm.text("[][]!!!", first_word_pick, second_word_pick), forced: "hunter boxing enthusiastic battlecry");
        }
        await defender.apply_status_effect(typeof(datum_status_effect_rebuked));
        await defender.apply_damage(damage * 2, default_damage_type, BODY_ZONE_CHEST, armor_block); //deals double our damage AGAIN

        float healing_factor = round(damage / 3, 1);
        await attacker.heal_overall_damage(healing_factor, healing_factor, healing_factor);
        log_combat(attacker, defender, "hunter crit punched (boxing)");
    }

    // Our hunter boxer does a sizable amount of extra damage on a successful combo or block
    // tg: code/datums/martial/boxing.dm:501
    public override async DmTask perform_extra_effect(mob_living attacker = null, mob_living defender = null)
    {
        if (Dm.truthy(Dm.bitand(defender.mob_biotypes, MOB_HUMANOID)) && defender is not mob_living_simple_animal_hostile_megafauna)
            return; // Does not apply to humans (who aren't megafauna)

        await defender.apply_damage(rand(15, 20), default_damage_type, BODY_ZONE_CHEST);
    }

    // tg: code/datums/martial/boxing.dm:507
    public override async DmTask skill_experience_adjustment(mob_living boxer = null, mob_living defender = null, float experience_value = 0)
    {
        if (Dm.truthy(Dm.bitand(defender.mob_biotypes, MOB_HUMANOID)) && defender is not mob_living_simple_animal_hostile_megafauna)
        {
            await base.skill_experience_adjustment(boxer, defender, experience_value); //IF they're a normal human, we give the normal amount of experience instead
            return;
        }

        float gravity_modifier = Dm.num(boxer.has_gravity()) > STANDARD_GRAVITY ? 2 : 1;
        float big_game_bonus = Dm.num(defender.maxHealth) / 500;

        boxer.mind?.adjust_experience(typeof(datum_skill_athletics), round(experience_value * (gravity_modifier + big_game_bonus), 1));
    }
}
