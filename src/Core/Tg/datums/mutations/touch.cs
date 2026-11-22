// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/touch.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the shock touch and the mending touch, which moves the patient's wounds, damage and blood onto its
// healer and smites the undead and the evil instead. The var blocks are generated (D111).

// tg: code/datums/mutations/touch.dm:1
public partial class datum_mutation_shock : datum_mutation
{
    // tg: code/datums/mutations/touch.dm:14
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_touch_shock to_modify) // null or invalid
            return dot;

        if (GET_MUTATION_POWER(this) <= 1)
        {
            to_modify.stagger = Dm.initial(to_modify, (datum_action_cooldown_spell_touch_shock s) => s.stagger);
            return dot;
        }

        to_modify.stagger = TRUE;
        return dot;
    }
}

// tg: code/datums/mutations/touch.dm:27
public partial class datum_action_cooldown_spell_touch_shock : datum_action_cooldown_spell_touch
{
    // tg: code/datums/mutations/touch.dm:44
    public override async DmTask<object> cast_on_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon caster = null)
    {
        if (iscarbon(victim))
        {
            mob_living_carbon carbon_victim = victim as mob_living_carbon;
            if (Dm.truthy(await carbon_victim.electrocute_act(5, caster, 1, SHOCK_NOGLOVES | SHOCK_NOSTUN))) //doesn't stun. never let this stun
            {
                obj_item_bodypart affecting = carbon_victim.get_bodypart(Dm.str(carbon_victim.get_random_valid_zone(caster.zone_selected))) as obj_item_bodypart;
                object armor_block = carbon_victim.run_armor_check(affecting, ENERGY);
                await carbon_victim.apply_damage(20, STAMINA, def_zone: affecting, blocked: Dm.num(armor_block));

                await carbon_victim.dropItemToGround(carbon_victim.get_active_held_item() as obj_item);
                await carbon_victim.dropItemToGround(carbon_victim.get_inactive_held_item() as obj_item);
                await carbon_victim.adjust_confusion(15 * SECONDS);
                carbon_victim.visible_message(
                    span_danger(I18n.T("[] electrocutes []!", caster, victim)),
                    span_userdanger(I18n.T("[] electrocutes you!", caster)));
                if (Dm.truthy(stagger))
                    await carbon_victim.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
                return TRUE;
            }
        }
        else if (isliving(victim))
        {
            mob_living living_victim = victim as mob_living;
            if (Dm.truthy(await living_victim.electrocute_act(15, caster, 1, SHOCK_NOSTUN))) //We do damage here because non-carbon mobs typically ignore stamina damage.
            {
                living_victim.visible_message(
                    span_danger(I18n.T("[] electrocutes []!", caster, victim)),
                    span_userdanger(I18n.T("[] electrocutes you!", caster)));
                if (Dm.truthy(stagger))
                    await living_victim.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * 2, 10 * SECONDS);
                return TRUE;
            }
        }

        to_chat(caster, span_warning(I18n.T("The electricity doesn't seem to affect []...", victim)));
        return TRUE;
    }
}

// tg: code/datums/mutations/touch.dm:85
public partial class datum_mutation_lay_on_hands : datum_mutation
{
    // tg: code/datums/mutations/touch.dm:99
    public override async DmTask<object> setup()
    {
        object dot = await base.setup();
        if (dot is not datum_action_cooldown_spell_touch_lay_on_hands to_modify) // null or invalid
            return dot;

        // Transfers more damage if strengthened. (1.5 with power chromosome)
        to_modify.power_coefficient = GET_MUTATION_POWER(this);
        // Halves transferred damage if synchronized. (0.5 with synchronizer chromosome)
        to_modify.synchronizer_coefficient = GET_MUTATION_SYNCHRONIZER(this);
        return dot;
    }
}

// tg: code/datums/mutations/touch.dm:111
public partial class datum_action_cooldown_spell_touch_lay_on_hands : datum_action_cooldown_spell_touch
{
    // tg: code/datums/mutations/touch.dm:138
    public override async DmTask<object> create_hand(mob_living_carbon cast_on = null)
    {
        object dot = await base.create_hand(cast_on);
        if (!Dm.truthy(dot))
            return dot;
        obj_item_bodypart transfer_limb = cast_on.get_active_hand() as obj_item_bodypart;
        if (!Dm.truthy(IS_ORGANIC_LIMB(transfer_limb)))
        {
            to_chat(cast_on, span_notice(I18n.T("You fail to channel your mending powers through your inorganic hand.")));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/datums/mutations/touch.dm:149
    public override async DmTask<object> cast_on_hand_hit(obj_item_melee_touch_attack hand = null, atom victim = null, mob_living_carbon mendicant = null)
    {
        mob_living hurtguy = victim as mob_living;

        heal_multiplier = Dm.initial(this, (datum_action_cooldown_spell_touch_lay_on_hands l) => l.heal_multiplier) * power_coefficient;
        pain_multiplier = Dm.initial(this, (datum_action_cooldown_spell_touch_lay_on_hands l) => l.pain_multiplier) * synchronizer_coefficient;

        // Message to show on a successful heal if the healer has a special pacifism interaction with the mutation.
        string peaceful_message = null;

        object success;

        object hurt_this_guy = determine_if_this_hurts_instead(mendicant, hurtguy);

        if (Dm.truthy(hurt_this_guy) && (HAS_TRAIT(mendicant, TRAIT_PACIFISM) || !Dm.truthy(mendicant.combat_mode))) //Returns if we're a pacifist and we'd hurt them, or we're not in combat mode and we'll hurt them
        {
            mendicant.balloon_alert(mendicant, I18n.T("[] would be hurt!", hurtguy));
            return FALSE;
        }

        if (Dm.truthy(hurt_this_guy))
            return await by_gods_light_i_smite_you(mendicant, hurtguy, heal_multiplier);

        // Heal more, hurt a bit more.
        // If you crunch the numbers it sounds crazy good,
        // but I think that's a fair reward for combining the efforts of Genetics, Medbay, and Mining to reach a hidden mechanic.
        if (HAS_TRAIT_FROM(mendicant, TRAIT_HIPPOCRATIC_OATH, HIPPOCRATIC_OATH_TRAIT))
        {
            heal_multiplier *= 2;
            pain_multiplier *= 0.5f;
            peaceful_message = span_boldnotice(I18n.T("You can feel the magic of the Rod of Aesculapius aiding your efforts!"));
            beam_icon = "sendbeam";
            obj_item_rod_of_asclepius rod = Dm.locate_type(typeof(obj_item_rod_of_asclepius), mendicant.contents) as obj_item_rod_of_asclepius;
            if (rod != null)
            {
                rod.add_filter("cool_glow", 2, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", COLOR_VERY_PALE_LIME_GREEN), Dm.kv("size", 1.25f)));
                _addtimer(Dm.@new<datum_callback>(rod, nameof(rod.remove_filter), "cool_glow"), 6 * SECONDS, file: "code/datums/mutations/touch.dm", line: 183);
            }
        }
        // If a normal pacifist, transfer more.
        else if (HAS_TRAIT(mendicant, TRAIT_PACIFISM))
        {
            heal_multiplier *= 1.75f;
            peaceful_message = span_boldnotice(I18n.T("Your peaceful nature helps you guide all the pain to yourself."));
        }

        if (iscarbon(hurtguy))
            success = await do_complicated_heal(mendicant, hurtguy as mob_living_carbon, heal_multiplier, pain_multiplier);
        else
            success = await do_simple_heal(mendicant, hurtguy, heal_multiplier, pain_multiplier);

        // No healies in the end, cancel
        if (!Dm.truthy(success))
            return FALSE;

        if (peaceful_message != null)
            to_chat(mendicant, peaceful_message);

        // Both types can be ignited (technically at least), so we can just do this here.
        if (Dm.num(hurtguy.fire_stacks) > 0)
        {
            await mendicant.set_fire_stacks(Dm.num(hurtguy.fire_stacks) * pain_multiplier, remove_wet_stacks: TRUE);
            if (Dm.truthy(hurtguy.on_fire))
            {
                mendicant.ignite_mob();
                hurtguy.extinguish_mob();
            }
        }

        mendicant.Beam(hurtguy, icon_state: beam_icon, time: 0.5f * SECONDS);
        beam_icon = Dm.initial(this, (datum_action_cooldown_spell_touch_lay_on_hands l) => l.beam_icon);

        await hurtguy.update_damage_overlays();
        await mendicant.update_damage_overlays();

        hurtguy.visible_message(span_notice(I18n.T("[] lays hands on []!", mendicant, hurtguy)));
        to_chat(hurtguy, span_boldnotice(I18n.T("[] lays hands on you, healing you!", mendicant)));
        Dm.@new<obj_effect_temp_visual_heal>(get_turf(hurtguy), COLOR_VERY_PALE_LIME_GREEN);
        return success;
    }

    // tg: code/datums/mutations/touch.dm:218
    public virtual async DmTask<object> do_simple_heal(mob_living_carbon mendicant = null, mob_living hurtguy = null, float heal_multiplier = 0, float pain_multiplier = 0)
    {
        // Did the transfer work?
        object dot = FALSE;

        // Damage to heal
        float brute_to_heal = Dm.min(Dm.num(hurtguy.get_brute_loss()), 35 * heal_multiplier);
        // no double dipping
        float burn_to_heal = Dm.min(Dm.num(hurtguy.get_fire_loss()), (35 - brute_to_heal) * heal_multiplier);

        // Get at least organic limb to transfer the damage to
        DmList mendicant_organic_limbs = Dm.list();
        foreach (obj_item_bodypart possible_limb in Dm.each<obj_item_bodypart>(mendicant.get_bodyparts()))
        {
            if (Dm.truthy(IS_ORGANIC_LIMB(possible_limb)))
                Dm.append(ref mendicant_organic_limbs, possible_limb);
        }
        // None? Gtfo
        if (!Dm.truthy(length(mendicant_organic_limbs)))
        {
            mendicant.balloon_alert(mendicant, I18n.T("no organic limbs!"));
            return dot;
        }

        // Try to use our active hand, otherwise pick at random
        obj_item_bodypart mendicant_transfer_limb = mendicant.get_active_hand() as obj_item_bodypart;
        if (!Dm.@in(mendicant_transfer_limb, mendicant_organic_limbs))
        {
            mendicant_transfer_limb = pick(mendicant_organic_limbs) as obj_item_bodypart;
            await mendicant_transfer_limb.receive_damage(brute_to_heal * pain_multiplier, burn_to_heal * pain_multiplier, forced: TRUE, wound_bonus: CANT_WOUND);
        }

        if (Dm.truthy(brute_to_heal))
        {
            await hurtguy.adjust_brute_loss(-brute_to_heal);
            dot = TRUE;
        }

        if (Dm.truthy(burn_to_heal))
        {
            await hurtguy.adjust_fire_loss(-burn_to_heal);
            dot = TRUE;
        }

        if (!Dm.truthy(dot))
            hurtguy.balloon_alert(mendicant, I18n.T("unhurt!"));
        return dot;
    }

    // tg: code/datums/mutations/touch.dm:254
    public virtual async DmTask<object> do_complicated_heal(mob_living_carbon mendicant = null, mob_living_carbon hurtguy = null, float heal_multiplier = 0, float pain_multiplier = 0)
    {
        // Did the transfer work?
        object dot = FALSE;

        // Counter to make sure we don't take too much from separate limbs
        float healing_budget = 35 * heal_multiplier;
        // Transfer damage from each limb to the mendicant's counterpart.
        foreach (obj_item_bodypart affected_limb in Dm.each_cast<obj_item_bodypart>(hurtguy.get_bodyparts()))
        {
            if (!Dm.truthy(Dm.bitand(affected_limb.bodytype, BODYTYPE_ORGANIC)))
                continue;
            obj_item_bodypart mendicant_transfer_limb = mendicant.get_bodypart(Dm.str(affected_limb.body_zone)) as obj_item_bodypart;
            DmList mendicant_organic_limbs = Dm.list();
            foreach (obj_item_bodypart possible_limb in Dm.each<obj_item_bodypart>(mendicant.get_bodyparts(include_stumps: TRUE))) // Keeping this here in case stumps can store damage in the future
            {
                if (Dm.truthy(IS_ORGANIC_LIMB(possible_limb)) && !Dm.truthy(IS_STUMP(possible_limb))) // No stumps are currently organic, this is futureproofing.
                    Dm.append(ref mendicant_organic_limbs, possible_limb);
            }
            // Spread the healing between brute and burn
            float affected_limb_damage = Dm.num(affected_limb.brute_dam) + Dm.num(affected_limb.burn_dam);
            float brute_damage = 0;
            float burn_damage = 0;
            if (healing_budget >= affected_limb_damage)
            {
                healing_budget -= affected_limb_damage;
                brute_damage = Dm.num(affected_limb.brute_dam);
                burn_damage = Dm.num(affected_limb.burn_dam);
            }
            else
            {
                if (Dm.num(affected_limb.brute_dam) > Dm.num(affected_limb.burn_dam))
                {
                    burn_damage = Dm.min(Dm.num(affected_limb.burn_dam), healing_budget / 2);
                    brute_damage = healing_budget - burn_damage;
                }
                else
                {
                    brute_damage = Dm.min(Dm.num(affected_limb.burn_dam), healing_budget / 2);
                    burn_damage = healing_budget - brute_damage;
                }
                healing_budget = 0;
            }
            if (Dm.truthy(brute_damage) || Dm.truthy(burn_damage))
            {
                dot = TRUE;
                float brute_taken = brute_damage * pain_multiplier;
                float burn_taken = burn_damage * pain_multiplier;
                // Heal!
                await affected_limb.heal_damage(brute_damage, burn_damage, required_bodytype: BODYTYPE_ORGANIC);
                // Hurt!
                if (Dm.@in(mendicant_transfer_limb, mendicant_organic_limbs))
                    await mendicant_transfer_limb.receive_damage(brute_taken, burn_taken, forced: TRUE, wound_bonus: CANT_WOUND);
                else // spread damage if we're trying to heal a bodypart we have no organic parallel of
                    await mendicant.take_overall_damage(brute_damage, burn_damage, required_bodytype: BODYTYPE_ORGANIC);
            }

            // Force light wounds onto you.
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(affected_limb.wounds))
            {
                float severity = Dm.num(iter_wound.severity);
                if (severity == WOUND_SEVERITY_SEVERE) // half and half
                {
                    if (prob(50 * heal_multiplier))
                        continue;
                }
                else if (severity == WOUND_SEVERITY_CRITICAL)
                {
                    if (heal_multiplier < 1.5f && prob(30 * heal_multiplier)) // need buffs to transfer crit wounds
                        continue;
                }
                dot = TRUE;
                await iter_wound.remove_wound();
                await iter_wound.apply_wound(mendicant_transfer_limb);
            }

            if (!Dm.truthy(healing_budget))
                break;
        }

        if (!Dm.truthy(CAN_HAVE_BLOOD(mendicant)) || !Dm.truthy(CAN_HAVE_BLOOD(hurtguy)))
            return dot;

        // 10% base
        float max_blood_transfer = (BLOOD_VOLUME_NORMAL * 0.10f) * heal_multiplier;
        float hurtguy_blood = Dm.num(hurtguy.get_blood_volume());
        // Too little blood
        if (hurtguy_blood < BLOOD_VOLUME_NORMAL)
        {
            float amount_to_transfer = Dm.min(max_blood_transfer, BLOOD_VOLUME_NORMAL - hurtguy_blood);
            // We ignore incompatibility here.
            object blood_transferred = await mendicant.transfer_blood_to(hurtguy, amount_to_transfer, ignore_low_blood: TRUE, ignore_incompatibility: TRUE);

            if (!Dm.truthy(blood_transferred))
                return dot; // a bare return gives .

            to_chat(mendicant, span_notice(I18n.T("Your veins (and brain) feel a bit lighter.")));
            dot = TRUE;
            // Because we do our own spin on it!
            if (Dm.eq(hurtguy.get_blood_compatibility(mendicant), FALSE))
            {
                await hurtguy.adjust_tox_loss((Dm.num(blood_transferred) * 0.1f) * pain_multiplier); // 1 dmg per 10 blood
                to_chat(hurtguy, span_notice(I18n.T("Your veins feel thicker, but they itch a bit.")));
            }
            else
                to_chat(hurtguy, span_notice(I18n.T("Your veins feel thicker!")));
            return dot; // a bare return gives .
        }

        if (hurtguy_blood < BLOOD_VOLUME_MAXIMUM)
            return dot;

        float amount_to_receive = Dm.min(max_blood_transfer, hurtguy_blood - BLOOD_VOLUME_MAXIMUM);
        // We ignore incompatibility here.
        object blood_received = await hurtguy.transfer_blood_to(mendicant, amount_to_receive, ignore_incompatibility: TRUE);

        if (!Dm.truthy(blood_received))
            return dot; // a bare return gives .

        to_chat(hurtguy, span_notice(I18n.T("Your veins don't feel quite so swollen anymore.")));
        dot = TRUE;
        // Because we do our own spin on it!
        if (Dm.eq(mendicant.get_blood_compatibility(hurtguy), FALSE))
        {
            await mendicant.adjust_tox_loss((Dm.num(blood_received) * 0.1f) * pain_multiplier); // 1 dmg per 10 blood
            to_chat(mendicant, span_notice(I18n.T("Your veins swell and itch!")));
        }
        else
            to_chat(mendicant, span_notice(I18n.T("Your veins swell!")));
        if (!Dm.truthy(dot))
            mendicant.balloon_alert(hurtguy, I18n.T("no damaged organic limbs!"));
        return dot;
    }

    // tg: code/datums/mutations/touch.dm:361
    public virtual object determine_if_this_hurts_instead(mob_living_carbon mendicant = null, mob_living hurtguy = null)
    {
        object hurtguy_smiteable = hurtguy.SEND_SIGNAL(COMSIG_ON_LAY_ON_HANDS, Dm.list(hurtguy, mendicant));

        if (Dm.truthy(Dm.bitand(hurtguy.mob_biotypes, MOB_UNDEAD)) && Dm.truthy(Dm.bitand(mendicant.mob_biotypes, MOB_UNDEAD)))
            return FALSE; //always return false if we're both undead //undead solidarity

        if (Dm.truthy(Dm.bitand(hurtguy.mob_biotypes, MOB_UNDEAD)) && !HAS_TRAIT(mendicant, TRAIT_EVIL)) //Is the mob undead and we're not evil? If so, hurt.
            return TRUE;

        if (HAS_TRAIT(hurtguy, TRAIT_EVIL) && !HAS_TRAIT(mendicant, TRAIT_EVIL)) //Is the guy evil and we're not evil? If so, hurt.
            return TRUE;

        if (Dm.truthy(hurtguy_smiteable)) //Is some other property of the target (like the empath component) causing them to be smited? If so, hurt.
            return TRUE;
        return FALSE;
    }

    ///If our target was undead or evil, we blast them with a firey beam rather than healing them. For, you know, 'holy' reasons. When did genes become so morally uptight?
    // tg: code/datums/mutations/touch.dm:380
    public virtual async DmTask<object> by_gods_light_i_smite_you(mob_living_carbon smiter = null, mob_living motherfucker_to_hurt = null, float smite_multiplier = 0)
    {
        float our_smite_multiplier = smite_multiplier;
        bool evil_smite = HAS_TRAIT(smiter, TRAIT_EVIL);
        bool divine_champion = Dm.num(smiter.mind?.holy_role) >= HOLY_ROLE_PRIEST;
        string smite_text_to_target = I18n.T("lays hands on you");

        if (divine_champion || HAS_TRAIT(smiter, TRAIT_SPIRITUAL))
        {
            // Defaults for possible deity. You know, just in case.
            object possible_deity = evil_smite ? "Satan" : "God";

            mob_living_carbon_human human_smiter = smiter as mob_living_carbon_human;

            // If we have a client, check their deity pref and use that instead of our chaps god if our smiter is a spiritualist
            client smiter_client = smiter.client;

            if (smiter_client != null && HAS_TRAIT(smiter, TRAIT_SPIRITUAL))
                possible_deity = smiter_client.prefs?.read_preference(typeof(datum_preference_name_deity));
            else if (Dm.truthy(GLOB.deity))
                possible_deity = GLOB.deity;

            if (ishuman(human_smiter))
            {
                human_smiter.force_say();
                if (evil_smite)
                    await human_smiter.say(I18n.T("in []'s dark name, I COMMAND YOU TO PERISH!!!", possible_deity), forced: "compelled by the power of their deity");
                else
                    await human_smiter.say(I18n.T("By []'s might, I SMITE YOU!!!", possible_deity), forced: "compelled by the power of their deity");
            }
            our_smite_multiplier *= divine_champion ? 5 : 1; //good luck surviving this if they're a chap
        }

        if (evil_smite)
        {
            motherfucker_to_hurt.visible_message(span_warning(I18n.T("[] snaps [] fingers in front of []'s face, and []'s body twists violently from an unseen force!", smiter, smiter.p_their(), motherfucker_to_hurt, motherfucker_to_hurt)));
            await motherfucker_to_hurt.apply_damage(10 * our_smite_multiplier, BRUTE, spread_damage: TRUE, wound_bonus: 5 * our_smite_multiplier);
            await motherfucker_to_hurt.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH * our_smite_multiplier, 25 * SECONDS);
            await smiter.emote("snap");
            smite_text_to_target = I18n.T("crushes you psychically with a snap of [] fingers", smiter.p_their());
        }
        else
        {
            motherfucker_to_hurt.visible_message(span_warning(I18n.T("[] lays hands on [], but it shears [] with a brilliant energy!", smiter, motherfucker_to_hurt, motherfucker_to_hurt.p_them())));
            await motherfucker_to_hurt.apply_damage(10 * our_smite_multiplier, BURN, spread_damage: TRUE, wound_bonus: 5 * our_smite_multiplier);
            await motherfucker_to_hurt.adjust_fire_stacks(3 * our_smite_multiplier);
            motherfucker_to_hurt.ignite_mob();
        }

        await motherfucker_to_hurt.update_damage_overlays();

        to_chat(motherfucker_to_hurt, span_bolddanger(I18n.T("[] [], hurting you!", smiter, smite_text_to_target)));
        await motherfucker_to_hurt.emote("scream");
        Dm.@new<obj_effect_temp_visual_explosion>(get_turf(motherfucker_to_hurt), evil_smite ? LIGHT_COLOR_BLOOD_MAGIC : LIGHT_COLOR_HOLY_MAGIC);
        return TRUE;
    }
}
