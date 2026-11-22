// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_defense.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: get_emp_protection (M7 EMP), electrocute_act (the M7 shocks slice), the shove collision handlers (disarm_precollide, disarm_collision: a human shoved into a human, DV17) and
// the covered mouth and eyes checks (the breath and vomit read them) translated (M2 P2), attack_hand (M2 T2) and
// check_self_for_injuries's unconscious check (M2 integration: its report is a deferred leaf), expose_reagents (M4b, the
// fire extinguisher); armour, attacks,
// reflection and the rest of the defence procs are M4a-M5.

// tg: code/modules/mob/living/carbon/human/human_defense.dm:119
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:100  (M9 slice F)
    public override async DmTask<object> grippedby(mob_living user = null, float? instant = null)
    {
        instant ??= FALSE;
        if (Dm.truthy(w_uniform))
            (w_uniform as atom).add_fingerprint(user);
        await base.grippedby(user, instant);
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:119
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        if (ishuman(user))
        {
            mob_living_carbon_human H = user as mob_living_carbon_human;
            await ((datum_species)dna.species).spec_attack_hand(H, this, null, modifiers);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:21
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:21
    public virtual object get_worn_bodypart_armor_value(obj_item_bodypart def_zone = null, object damage_type = null)
    {
        if (!Dm.truthy(damage_type))
            return 0;
        float unblocked_damage = 100;
        DmList covering_clothing = Dm.list(head, wear_mask, wear_suit, w_uniform, back, gloves, shoes, belt, s_store, glasses, ears, wear_id, wear_neck); //Everything but pockets. Pockets are l_store and r_store. (if pockets were allowed, putting something armored, gloves or hats for example, would double up on the armor)
        foreach (obj_item_clothing clothing_item in Dm.each<obj_item_clothing>(covering_clothing))
        {
            if (Dm.truthy(Dm.bitand(clothing_item.body_parts_covered, def_zone.body_part)))
                unblocked_damage *= (100 - min(Dm.num(clothing_item.get_armor_rating(damage_type)), 100)) * 0.01f;
        }
        return 100 - unblocked_damage;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:1 (M4a P2, the lead's assignment: run_armor_check's reads)
    public override object get_worn_armor_value(object def_zone = null, object damage_type = null)
    {
        float armorval = 0;
        float organnum = 0;

        if (Dm.truthy(def_zone))
        {
            if (isbodypart(def_zone))
            {
                obj_item_bodypart bp = def_zone as obj_item_bodypart;
                if (Dm.truthy(bp))
                    return get_worn_bodypart_armor_value(def_zone as obj_item_bodypart, damage_type);
            }
            obj_item_bodypart affecting = get_bodypart(Dm.str(check_zone(def_zone))) as obj_item_bodypart;
            if (Dm.truthy(affecting))
                return get_worn_bodypart_armor_value(affecting, damage_type);
            //If a specific bodypart is targeted, check how that bodypart is protected and return the value.
        }

        //If you don't specify a bodypart, it checks ALL your bodyparts for protection, and averages out the values
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(get_bodyparts()))
        {
            armorval += Dm.num(get_worn_bodypart_armor_value(part, damage_type));
            organnum++;
        }
        return (armorval / max(organnum, 1));
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:32
    public virtual object get_clothing_on_part(obj_item_bodypart def_zone = null)
    {
        DmList covering_part = Dm.list();
        foreach (obj_item_clothing equipped in Dm.each<obj_item_clothing>(get_equipped_items(INCLUDE_ABSTRACT)))
        {
            if (Dm.truthy(Dm.bitand(equipped.body_parts_covered, def_zone.body_part)))
                Dm.append(ref covering_part, equipped);
        }
        return covering_part;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:39  (the M7 projectiles slice)
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (hitting_projectile.firer == this && hitting_projectile.original == this) //can't block or reflect when shooting yourself
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);

        // The projectile cannot be reflected or you failed to pass a reflection% check
        if (!Dm.truthy(hitting_projectile.reflectable) || !Dm.truthy(await check_reflect(def_zone)))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);

        visible_message(
            span_danger(I18n.T("\\The [] gets reflected by []!", hitting_projectile, this)),
            span_userdanger(I18n.T("\\The [] gets reflected by []!", hitting_projectile, this))
        );
        // Finds and plays the block_sound of item which reflected
        foreach (obj_item held_item in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(await held_item.IsReflect(def_zone)))
                playsound(this, held_item.block_sound, BLOCK_SOUND_VOLUME, TRUE);
        }
        // Find a turf near or on the original location to bounce to
        if (!isturf(loc)) // Open canopy mech (ripley) check. if we're inside something and still got hit
            return await loc.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        hitting_projectile.reflect(this);
        return BULLET_ACT_FORCE_PIERCE; // complete projectile permutation
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:61  (the M7 projectiles slice)
    public override async DmTask<object> bullet_act(obj_projectile bullet = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        if (Dm.truthy(await check_block(bullet, bullet.damage, I18n.T("\\the []", bullet), PROJECTILE_ATTACK, bullet.armour_penetration, bullet.damage_type)))
            return await base.bullet_act(bullet, def_zone, piercing_hit, 100);
        return await base.bullet_act(bullet, def_zone, piercing_hit, blocked);
    }

    ///Reflection checks for anything in your l_hand, r_hand, or wear_suit based on the reflection chance of the object
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:67  (the M7 projectiles slice)
    public virtual async DmTask<object> check_reflect(object def_zone = null)
    {
        if (Dm.truthy(wear_suit))
        {
            if (Dm.truthy(await wear_suit.IsReflect(def_zone)))
                return TRUE;
        }
        if (Dm.truthy(head))
        {
            if (Dm.truthy(await head.IsReflect(def_zone)))
                return TRUE;
        }
        foreach (obj_item I in Dm.each<obj_item>(held_items))
        {
            if (Dm.truthy(await I.IsReflect(def_zone)))
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:127
    [SignalHandler]
    public virtual object disarm_precollide(datum source = null, mob_living shover = null, mob_living target = null, obj_item weapon = null)
    {
        return COMSIG_LIVING_ACT_SOLID;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:131
    [SignalHandler]
    public virtual object disarm_collision(datum source = null, mob_living shover = null, mob_living target = null, object shove_flags = null, obj_item weapon = null)
    {
        if (this == target || Dm.truthy(LAZYFIND(target.buckled_mobs, this)) || !iscarbon(target))
            return null;
        if (!Dm.truthy(Dm.bitand(shove_flags, SHOVE_KNOCKDOWN_BLOCKED)))
            Waiting.NoSleep(target.Knockdown(SHOVE_KNOCKDOWN_HUMAN, daze_amount: 3 * SECONDS));
        if (!HAS_TRAIT(this, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED))
            Waiting.NoSleep(Knockdown(SHOVE_KNOCKDOWN_COLLATERAL, daze_amount: 3 * SECONDS));
        target.visible_message(span_danger(I18n.T("[] shoves [] into []!", shover, target.name, name)),
            span_userdanger(I18n.T("You're shoved into [] by []!", name, shover)), span_hear(I18n.T("You hear aggressive shuffling followed by a loud thud!")), COMBAT_MESSAGE_RANGE, Dm.list(shover));
        to_chat(shover, span_danger(I18n.T("You shove [] into []!", target.name, name)));
        log_combat(shover, target, "shoved", addition: Dm.text("into [][]", name, (weapon != null ? Dm.text(" with []", weapon) : "")));
        return COMSIG_LIVING_SHOVE_HANDLED;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:556
    public override object check_self_for_injuries()
    {
        if (IS_UNCONSCIOUS(this))
            return null;
        DmList combined_msg = Dm.list();

        visible_message(span_notice(I18n.T("[] examines []self.", this, p_them())));

        Dm.append(ref combined_msg, span_notice(I18n.T("<b>You check yourself for injuries.</b>")));


        foreach ((object part_zone, object body_part_untyped) in Dm.pairs(get_bodyparts_by_zones()))
        {
            obj_item_bodypart body_part = body_part_untyped as obj_item_bodypart;
            if (body_part == null || Dm.truthy(IS_STUMP(body_part)))
            {
                Dm.append(ref combined_msg, span_boldannounce(I18n.T("&rdsh; Your [] is missing!", parse_zone(body_part?.body_zone is var or__1 && Dm.truthy(or__1) ? or__1 : part_zone))));
                continue;
            }
            if (Dm.truthy(Dm.bitand(body_part.bodypart_flags, BODYPART_PSEUDOPART))) //don't show injury text for fake bodyparts; ie chainsaw arms or synthetic armblades
                continue;

            object bodypart_report = body_part.check_for_injuries(this);
            if (Dm.truthy(bodypart_report))
                Dm.append(ref combined_msg, I18n.T("[] []", span_notice("&rdsh;"), bodypart_report));
        }

        float tox = Dm.num(get_tox_loss()) + (disgust / 5) + (HAS_TRAIT(this, TRAIT_SELF_AWARE) ? 0 : (rand(-3, 0) * 5));
        if (10 <= tox && tox <= 20)
            Dm.append(ref combined_msg, span_danger(I18n.T("You feel sick.")));
        else if (20 <= tox && tox <= 40)
            Dm.append(ref combined_msg, span_danger(I18n.T("You feel nauseated.")));
        else if (40 <= tox)
            Dm.append(ref combined_msg, span_danger(I18n.T("You feel very unwell!")));

        float cached_blood_volume = HAS_TRAIT(this, TRAIT_NOBLOOD) ? BLOOD_VOLUME_NORMAL : Dm.num(get_blood_volume(apply_modifiers: TRUE));
        float oxy = Dm.num(get_oxy_loss()) + (losebreath * 4) + (cached_blood_volume < BLOOD_VOLUME_NORMAL ? ((BLOOD_VOLUME_NORMAL - cached_blood_volume) * 0.1f) : 0) + (HAS_TRAIT(this, TRAIT_SELF_AWARE) ? 0 : (rand(-3, 0) * 5));
        if (10 <= oxy && oxy <= 20)
            Dm.append(ref combined_msg, span_danger(I18n.T("You feel lightheaded.")));
        else if (20 <= oxy && oxy <= 40)
            Dm.append(ref combined_msg, Dm.truthy(losebreath) ? span_danger(I18n.T("You're choking!")) : span_danger(I18n.T("Your thinking is clouded and distant.")));
        else if (40 <= oxy)
            Dm.append(ref combined_msg, span_danger(I18n.T("You feel like you're about to pass out!")));

        if (Dm.truthy(get_stamina_loss()))
        {
            if (Dm.num(get_stamina_loss()) > 30)
                Dm.append(ref combined_msg, span_info(I18n.T("You're completely exhausted.")));
            else
                Dm.append(ref combined_msg, span_info(I18n.T("You feel fatigued.")));
        }

        // boxed_message(combined_msg.Join("<br>")) (__DEFINES/chat.dm:48), inline
        to_chat(this, "<div class='boxed_message'>" + combined_msg.Join("<br>") + "</div>");
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:674
    public virtual async DmTask burn_clothing(object seconds_per_tick = null, object stacks = null)
    {
        DmList burning_items = Dm.list();
        //HEAD//

        if (Dm.truthy(glasses) && !Dm.truthy(Dm.bitand(covered_slots, HIDEEYES)))
            Dm.append(ref burning_items, glasses);
        if (Dm.truthy(wear_mask) && !Dm.truthy(Dm.bitand(covered_slots, HIDEMASK)))
            Dm.append(ref burning_items, wear_mask);
        if (Dm.truthy(wear_neck) && !Dm.truthy(Dm.bitand(covered_slots, HIDENECK)))
            Dm.append(ref burning_items, wear_neck);
        if (Dm.truthy(ears) && !Dm.truthy(Dm.bitand(covered_slots, HIDEEARS)))
            Dm.append(ref burning_items, ears);
        if (Dm.truthy(head))
            Dm.append(ref burning_items, head);

        //CHEST//
        if (Dm.truthy(w_uniform) && !Dm.truthy(Dm.bitand(covered_slots, HIDEJUMPSUIT)))
            Dm.append(ref burning_items, w_uniform);
        if (Dm.truthy(wear_suit))
            Dm.append(ref burning_items, wear_suit);

        //ARMS & HANDS//
        obj_item_clothing arm_clothes = null;
        if (Dm.truthy(gloves) && !Dm.truthy(Dm.bitand(covered_slots, HIDEGLOVES)))
            arm_clothes = gloves as obj_item_clothing;
        else if (Dm.truthy(wear_suit) && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, ARMS))))
            arm_clothes = wear_suit as obj_item_clothing;
        else if (Dm.truthy(w_uniform) && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, ARMS))))
            arm_clothes = w_uniform as obj_item_clothing;
        if (Dm.truthy(arm_clothes))
            Dm.combine(burning_items, arm_clothes);

        //LEGS & FEET//
        obj_item_clothing leg_clothes = null;
        if (Dm.truthy(shoes) && !Dm.truthy(Dm.bitand(covered_slots, HIDESHOES)))
            leg_clothes = shoes as obj_item_clothing;
        else if (Dm.truthy(wear_suit) && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, LEGS))))
            leg_clothes = wear_suit as obj_item_clothing;
        else if (Dm.truthy(w_uniform) && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, LEGS))))
            leg_clothes = w_uniform as obj_item_clothing;
        if (Dm.truthy(leg_clothes))
            Dm.combine(burning_items, leg_clothes);

        if (!Dm.truthy(gloves) || (!Dm.truthy(Dm.bitand(gloves.resistance_flags, FIRE_PROOF)) && Dm.truthy(Dm.bitand(gloves.resistance_flags, FLAMMABLE))))
        {
            foreach (obj_item burnable_item in Dm.each<obj_item>(held_items))
                Dm.combine(burning_items, burnable_item);
        }

        foreach (obj_item burning in Dm.each<obj_item>(burning_items))
        {
            // DEVIATION(DV27): the interaction guard (D132) on fire reaching an item: a type whose own fire_act is not
            // translated does nothing (its parent's fire_act does not run in its place).
            if (InertSeam.MissingOverride(burning, "fire_act"))
                continue;
            await burning.fire_act((Dm.num(stacks) * 25 * Dm.num(seconds_per_tick))); //damage taken is reduced to 2% of this value by fire_act()
        }
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:724  (M4b slice 3)
    public override async DmTask on_fire_stack(object seconds_per_tick = null, datum_status_effect_fire_handler_fire_stacks fire_handler = null)
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_HUMAN_BURNING]))
            _SendSignal(COMSIG_HUMAN_BURNING, Dm.list(this));
        await burn_clothing(seconds_per_tick, fire_handler.stacks);
        float no_protection = FALSE;
        if (HAS_TRAIT(this, TRAIT_IGNORE_FIRE_PROTECTION))
            no_protection = TRUE;
        await fire_handler.harm_human(seconds_per_tick, no_protection);
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:732  (M4b: the fire extinguisher's water)
    [DmParams("reagents", "source", "methods", "volume_modifier", "show_message")]
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        if (external != null || @internal != null)
        {
            methods = Dm.num(Dm.bitand(methods, Dm.bitnot(INHALE)));
            if (methods == NONE)
                return null;
        }
        return await base.expose_reagents(reagents, source, methods, show_message, __extra);
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:740
    public override object is_mouth_covered(float? check_flags = null)
    {
        check_flags ??= ALL;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_HEAD)) && head != null && Dm.truthy(Dm.bitand(head.flags_cover, HEADCOVERSMOUTH)))
            return head;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_MASK)) && wear_mask != null && Dm.truthy(Dm.bitand(wear_mask.flags_cover, MASKCOVERSMOUTH)))
            return wear_mask;
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:747
    public override object is_eyes_covered(float? check_flags = null)
    {
        check_flags ??= ALL;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_HEAD)) && head != null && Dm.truthy(Dm.bitand(head.flags_cover, HEADCOVERSEYES)))
            return head;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_MASK)) && wear_mask != null && Dm.truthy(Dm.bitand(wear_mask.flags_cover, MASKCOVERSEYES)))
            return wear_mask;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_EYES)) && glasses != null && Dm.truthy(Dm.bitand(glasses.flags_cover, GLASSESCOVERSEYES)))
            return glasses;
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:756  (M8 slice B: condensed capsaicin, the pepper spray)
    public override object is_pepper_proof(float? check_flags = null)
    {
        check_flags ??= ALL;
        object dot = base.is_pepper_proof(check_flags);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_HEAD)) && head != null && Dm.truthy(Dm.bitand(head.flags_cover, PEPPERPROOF)))
            return head;
        if (Dm.truthy(Dm.bitand(check_flags, ITEM_SLOT_MASK)) && wear_mask != null && Dm.truthy(Dm.bitand(wear_mask.flags_cover, PEPPERPROOF)))
            return wear_mask;
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:190 (interleaved types, rules 1.9; gaps X5: the alien's slash, tackle and
// disarm, the larva's bite)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:190
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        object dot = await base.attack_alien(user, modifiers);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK))) //Always drop item in hand if there is one. If there's no item, shove the target. If the target is incapacitated, slam them into the ground to stun them.
        {
            obj_item I = get_active_held_item() as obj_item;
            if (I != null && Dm.truthy(await dropItemToGround(I)))
            {
                playsound(loc, Dm.resource("sound/items/weapons/slash.ogg"), 25, TRUE, -1);
                visible_message(span_danger(I18n.T("[] disarms []!", user, this)),
                                span_userdanger(I18n.T("[] disarms you!", user)), span_hear(I18n.T("You hear aggressive shuffling!")), null, user);
                to_chat(user, span_danger(I18n.T("You disarm []!", this)));
            }
            else if (!HAS_TRAIT(this, TRAIT_INCAPACITATED))
            {
                playsound(loc, Dm.resource("sound/items/weapons/pierce.ogg"), 25, TRUE, -1);
                object shovetarget = get_edge_target_turf(user, get_dir(user, get_step_away(this, user)));
                await adjust_stamina_loss(35);
                await throw_at(shovetarget as atom, 4, 2, user, force: MOVE_FORCE_OVERPOWERING);
                log_combat(user, this, "shoved");
                visible_message(span_danger(I18n.T("[] tackles [] down!", user, this)),
                                span_userdanger(I18n.T("[] shoves you with great force!", user)), span_hear(I18n.T("You hear aggressive shuffling followed by a loud thud!")), null, user);
                to_chat(user, span_danger(I18n.T("You shove [] with great force!", this)));
            }
            else
            {
                await Paralyze(5 * SECONDS);
                playsound(loc, Dm.resource("sound/items/weapons/punch3.ogg"), 25, TRUE, -1);
                visible_message(span_danger(I18n.T("[] slams [] into the floor!", user, this)),
                                span_userdanger(I18n.T("[] slams you into the ground!", user)), span_hear(I18n.T("You hear something slam loudly onto the floor!")), null, user);
                to_chat(user, span_danger(I18n.T("You slam [] into the floor beneath you!", this)));
                log_combat(user, this, "slammed into the ground");
            }
            return TRUE;
        }

        if (Dm.truthy(user.combat_mode))
        {
            if (w_uniform != null)
                w_uniform.add_fingerprint(user);
            float damage = prob(90) ? rand(Dm.num(user.melee_damage_lower), Dm.num(user.melee_damage_upper)) : 0;
            if (!Dm.truthy(damage))
            {
                playsound(loc, Dm.resource("sound/items/weapons/slashmiss.ogg"), 50, TRUE, -1);
                visible_message(span_danger(I18n.T("[] lunges at []!", user, this)),
                                span_userdanger(I18n.T("[] lunges at you!", user)), span_hear(I18n.T("You hear a swoosh!")), null, user);
                to_chat(user, span_danger(I18n.T("You lunge at []!", this)));
                return FALSE;
            }
            obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(user.zone_selected) as string) as obj_item_bodypart;
            object armor_block = run_armor_check(affecting, MELEE, "", "", 10);

            playsound(loc, Dm.resource("sound/items/weapons/slice.ogg"), 25, TRUE, -1);
            visible_message(span_danger(I18n.T("[] slashes at []!", user, this)),
                            span_userdanger(I18n.T("[] slashes at you!", user)), span_hear(I18n.T("You hear a sickening sound of a slice!")), null, user);
            to_chat(user, span_danger(I18n.T("You slash at []!", this)));
            if (Dm.truthy(await dismembering_strike(user, user.zone_selected))) //Dismemberment successful
                await apply_damage(damage, BRUTE, affecting, Dm.num(armor_block));
            log_combat(user, this, "attacked");
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:242
    [DmParams("worm", "modifiers")]
    public override async DmTask<object> attack_larva(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_alien_larva worm = user as mob_living_carbon_alien_larva;
        object dot = await base.attack_larva(worm, modifiers);
        if (!Dm.truthy(dot))
            return dot; //successful larva bite.
        float damage = rand(Dm.num(worm.melee_damage_lower), Dm.num(worm.melee_damage_upper));
        if (!Dm.truthy(damage))
            return dot;
        if (Dm.truthy(await check_block(worm, damage, I18n.T("\\the []", worm), attack_type: UNARMED_ATTACK)))
            return FALSE;
        if (stat != DEAD)
        {
            worm.amount_grown = min(worm.amount_grown + damage, XENOMORPH_MAX_GROWTH);
            obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(worm.zone_selected) as string) as obj_item_bodypart;
            object armor_block = run_armor_check(affecting, MELEE);
            await apply_damage(damage, BRUTE, affecting, Dm.num(armor_block));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:257 (interleaved types, rules 1.9; M5x slice 3: a person in a blast)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:257  (set waitfor = FALSE, inherited from /atom/ex_act: the
    // wrapper of waiting-model.md 5.4; the damage and status procs may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);
        if (!Dm.truthy(dot.Value) || !Dm.truthy(severity) || QDELETED(this))
            return FALSE;

        float bomb_armor = Dm.num(getarmor(null, BOMB));
        float brute_loss = 0;
        float burn_loss = 0;
        float soundbang_intensity = SOUNDBANG_NORMAL;
        float ear_damage = 0;
        float deafen_duration = 0;
        float max_wounds = 0;
        float wound_prob = 0;
        float clothes_damage = 0;
        float knockdown_duration = 0 * SECONDS;
        float unconscious_duration = 0 * SECONDS;
        float dismember_prob = 33; // chance of a wound becoming dismemberment
        DmList wound_severities = Dm.list(WOUND_SEVERITY_MODERATE, WOUND_SEVERITY_SEVERE, WOUND_SEVERITY_CRITICAL);

        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                if (bomb_armor < EXPLODE_GIB_THRESHOLD) //gibs the mob if their bomb armor is lower than EXPLODE_GIB_THRESHOLD
                {
                    foreach (object thing in Dm.each<object>(contents))
                    {
                        switch (Dm.num(severity))
                        {
                            case EXPLODE_DEVASTATE:
                                Dm.append(ref SSexplosions.high_mov_atom, thing);
                                break;
                            case EXPLODE_HEAVY:
                                Dm.append(ref SSexplosions.med_mov_atom, thing);
                                break;
                            case EXPLODE_LIGHT:
                                Dm.append(ref SSexplosions.low_mov_atom, thing);
                                break;
                        }
                    }
                    investigate_log("has been gibbed by an explosion.", INVESTIGATE_DEATHS);
                    await gib(DROP_ALL_REMAINS);
                    return TRUE;
                }
                else
                {
                    brute_loss = 300;
                    burn_loss = 300;
                    soundbang_intensity = SOUNDBANG_OVERWHELMING;
                    ear_damage = 60;
                    deafen_duration = 360 * SECONDS;
                    max_wounds = 5;
                    wound_prob = 75;
                    dismember_prob = 50;
                    Dm.remove(wound_severities, WOUND_SEVERITY_MODERATE);
                    atom throw_target = get_edge_target_turf(this, get_dir(this, get_step_away(this, this))) as atom;
                    await throw_at(throw_target, 200, 4);
                    clothes_damage = 400;
                    knockdown_duration = 20 * SECONDS;
                    unconscious_duration = 3 * SECONDS;
                }
                break;
            case EXPLODE_HEAVY:
                max_wounds = 3;
                wound_prob = 40;
                brute_loss = 60;
                burn_loss = 60;
                soundbang_intensity = SOUNDBANG_MASSIVE;
                ear_damage = 30;
                deafen_duration = 240 * SECONDS;
                clothes_damage = 200;
                knockdown_duration = 20 * SECONDS;
                unconscious_duration = 2 * SECONDS;
                break;
            case EXPLODE_LIGHT:
                max_wounds = 2;
                wound_prob = 30;
                Dm.remove(wound_severities, WOUND_SEVERITY_CRITICAL);
                brute_loss = 30;
                soundbang_intensity = SOUNDBANG_STRONG;
                ear_damage = 15;
                deafen_duration = 120 * SECONDS;
                clothes_damage = 50;
                knockdown_duration = 16 * SECONDS;
                break;
        }

        await Unconscious(unconscious_duration); // do this first, before wounds, so we DONT get wound text
        DmList all_bodyparts = get_bodyparts() as DmList;
        foreach (obj_item_bodypart BP in Dm.each_cast<obj_item_bodypart>(all_bodyparts))
        {
            float bp_bomb_armor = Dm.num(getarmor(BP.body_zone, BOMB));
            float bomb_multi = clamp(bp_bomb_armor / 166.6f, 0, 1); // scales linearly up to 0.6 at 100 armor
            // tg passes bomb_multi * 100 as the third positional argument, def_zone (not blocked): apply_damage then picks the
            // limb from it (carbon/damage_procs.dm:20), translated as written (rules 0.2)
            await apply_damage(round(brute_loss / all_bodyparts.len, DAMAGE_PRECISION), BRUTE, (bomb_multi * 100), wound_bonus: CANT_WOUND);
            await apply_damage(round(burn_loss / all_bodyparts.len, DAMAGE_PRECISION), BURN, (bomb_multi * 100), wound_bonus: CANT_WOUND);
            // damage split among body parts leaves wounding extremely unlikely so instead we do our own wounds
            if (prob(wound_prob * (1 - bomb_multi))) // chance to do anything at all
            {
                if (prob(dismember_prob) && !Dm.eq(BP.body_zone, BODY_ZONE_HEAD) && !Dm.eq(BP.body_zone, BODY_ZONE_CHEST) && (Dm.num(severity) >= EXPLODE_HEAVY || !Dm.truthy(bp_bomb_armor))) // chance to dismember instead of wound
                {
                    if (!prob((bomb_armor - 50) * 2)) // bomb armor has a second chance to stop dismemberment if it's over 50
                    {
                        // only apply damage if the dismember works otherwise nodismember makes you extremely fragile to explosions
                        if (Dm.truthy(await BP.dismember()))
                            await BP.receive_damage(INFINITY, wound_bonus: CANT_WOUND); // capped by proc
                    }
                }
                else if (!prob(Dm.num(getarmor(BP.body_zone, WOUND)))) // time to roll for wounds
                {
                    object wound_type = pick_weight(Dm.list(Dm.kv(WOUND_SLASH, 1), Dm.kv(WOUND_BLUNT, 2), Dm.kv(WOUND_BURN, 2)));
                    object wound_power = pick(wound_severities);
                    await cause_wound_of_type_and_severity(wound_type, BP, WOUND_SEVERITY_MODERATE, wound_power, severity_pick_mode: WOUND_PICK_HIGHEST_SEVERITY);
                }
                max_wounds--;
                if (!Dm.truthy(max_wounds))
                    break;
            }
        }
        await Knockdown(knockdown_duration - (bomb_armor * 1.6f));
        await damage_clothes(max(clothes_damage - bomb_armor, 0), BRUTE, BOMB);
        await soundbang_act(soundbang_intensity, 0, ear_damage, deafen_duration, ignore_deafness: TRUE, send_sound: FALSE);
        return TRUE;
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:605
    public override async DmTask damage_clothes(object damage_amount = null, string damage_type = null, object damage_flag = null, object def_zone = null)
    {
        damage_type ??= BRUTE;
        damage_flag ??= 0;
        if (damage_type != BRUTE && damage_type != BURN)
            return;
        float amount = Dm.num(damage_amount) * 0.5f; //0.5 multiplier for balance reason, we don't want clothes to be too easily destroyed
        DmList torn_items = Dm.list();

        //HEAD//
        if (!Dm.truthy(def_zone) || Dm.eq(def_zone, BODY_ZONE_HEAD))
        {
            obj_item head_clothes = null;
            if (glasses != null)
                head_clothes = glasses;
            if (wear_mask != null)
                head_clothes = wear_mask;
            if (wear_neck != null)
                head_clothes = wear_neck;
            if (head != null)
                head_clothes = head;
            if (head_clothes != null)
                Dm.append(torn_items, head_clothes);
            else if (ears != null)
                Dm.append(torn_items, ears);
        }

        //CHEST//
        if (!Dm.truthy(def_zone) || Dm.eq(def_zone, BODY_ZONE_CHEST))
        {
            obj_item chest_clothes = null;
            if (w_uniform != null)
                chest_clothes = w_uniform;
            if (wear_suit != null)
                chest_clothes = wear_suit;
            if (chest_clothes != null)
                Dm.append(torn_items, chest_clothes);
        }

        //ARMS & HANDS//
        if (!Dm.truthy(def_zone) || Dm.eq(def_zone, BODY_ZONE_L_ARM) || Dm.eq(def_zone, BODY_ZONE_R_ARM))
        {
            obj_item arm_clothes = null;
            if (gloves != null)
                arm_clothes = gloves;
            if (w_uniform != null && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, ARMS))))
                arm_clothes = w_uniform;
            if (wear_suit != null && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, ARMS))))
                arm_clothes = wear_suit;
            if (arm_clothes != null)
                Dm.combine(torn_items, arm_clothes);
        }

        //LEGS & FEET//
        if (!Dm.truthy(def_zone) || Dm.eq(def_zone, BODY_ZONE_L_LEG) || Dm.eq(def_zone, BODY_ZONE_R_LEG))
        {
            obj_item leg_clothes = null;
            if (shoes != null)
                leg_clothes = shoes;
            if (w_uniform != null && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, LEGS))))
                leg_clothes = w_uniform;
            if (wear_suit != null && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, FEET)) || Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, LEGS))))
                leg_clothes = wear_suit;
            if (leg_clothes != null)
                Dm.combine(torn_items, leg_clothes);
        }

        foreach (obj_item I in Dm.each<obj_item>(torn_items))
            await I.take_damage(amount, damage_type, damage_flag, 0);
    }

    // tg: code/modules/mob/living/carbon/human/human_defense.dm:765
    public override object get_eye_protection()
    {
        float dot = Dm.num(base.get_eye_protection());
        foreach (obj_item_clothing clothing in Dm.each<obj_item_clothing>(get_equipped_items()))
            dot += clothing.flash_protect;
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:536 (interleaved types, rules 1.9; M5x slice 5)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:536
    public override async DmTask<object> singularity_act()
    {
        object dot = 20;
        Type role = mind?.assigned_role?.GetType();
        if (role == typeof(datum_job_chief_engineer) || role == typeof(datum_job_station_engineer))
            dot = 100;
        else if (role == typeof(datum_job_clown))
            dot = rand(-1000, 1000);
        await base.singularity_act(); //Called afterwards because getting the mind after getting gibbed is sketchy
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:363 (interleaved types, rules 1.9; the M7 shocks slice)
public partial class mob_living_carbon_human : mob_living_carbon
{
    ///Calculates the siemens coeff based on clothing and species, can also restart hearts.
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:363
    [DmParams("shock_damage", "source", "siemens_coeff", "flags", "jitter_time", "stutter_time", "stun_duration")]
    public override async DmTask<object> electrocute_act(object shock_damage = null, object source = null, float? siemens_coeff = null, float? flags = null, params object[] __extra)
    {
        float? jitter_time = Dm.arg<float?>(__extra, 0);
        float? stutter_time = Dm.arg<float?>(__extra, 1);
        float? stun_duration = Dm.arg<float?>(__extra, 2);
        siemens_coeff ??= 1;
        flags ??= NONE;
        jitter_time ??= 20 * SECONDS;
        stutter_time ??= 4 * SECONDS;
        stun_duration ??= 4 * SECONDS;
        object dot = null;
        //Calculates the siemens coeff based on clothing. Completely ignores the arguments
        if (Dm.truthy(Dm.bitand(flags, SHOCK_TESLA))) //I hate this entire block. This gets the siemens_coeff for tesla shocks
        {
            if (Dm.truthy(gloves) && gloves.siemens_coefficient <= 0)
                siemens_coeff = Dm.num(siemens_coeff) - 0.5f;
            if (Dm.truthy(wear_suit))
            {
                if (wear_suit.siemens_coefficient == -1)
                    siemens_coeff = Dm.num(siemens_coeff) - 1;
                else if (wear_suit.siemens_coefficient <= 0)
                    siemens_coeff = Dm.num(siemens_coeff) - 0.95f;
            }
            siemens_coeff = max(Dm.num(siemens_coeff), 0);
        }
        if (Dm.truthy(Dm.bitand(flags, SHOCK_NOGLOVES))) //This gets the siemens_coeff for all non tesla shocks
        {
            if (Dm.truthy(wear_suit))
                siemens_coeff = Dm.num(siemens_coeff) * wear_suit.siemens_coefficient;
        }
        else if (Dm.truthy(gloves))
            siemens_coeff = Dm.num(siemens_coeff) * gloves.siemens_coefficient;

        dot = await base.electrocute_act(shock_damage, source, siemens_coeff, flags, Dm.forward(__extra, jitter_time, stutter_time, stun_duration));
        //Don't go further if the shock was blocked/too weak.
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_ILLUSION)))
        {
            if (Dm.ge(Dm.mul(shock_damage, siemens_coeff), 5))
                force_say();
            //Note we both check that the user is in cardiac arrest and can actually heartattack
            //If they can't, they're missing their heart and this would runtime
            if (Dm.truthy(undergoing_cardiac_arrest()) && Dm.truthy(can_heartattack()) && Dm.ge(Dm.mul(shock_damage, siemens_coeff), 1) && prob(25))
            {
                obj_item_organ_heart heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
                if (Dm.truthy(heart.Restart()) && !IS_UNCONSCIOUS_OR_CRIT(this))
                    to_chat(this, span_notice(I18n.T("You feel your heart beating again!")));
            }
        }
        if (!Dm.truthy(Dm.bitand(flags, SHOCK_NO_HUMAN_ANIM)))
            await electrocution_animation(4 * SECONDS);
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:770 (interleaved types, rules 1.9; M7 EMP)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:770
    public override object get_emp_protection()
    {
        object dot = null;
        dot = base.get_emp_protection();
        foreach (obj_item_clothing each_clothing in Dm.each<obj_item_clothing>(get_equipped_items()))
            dot = Dm.num(dot) + each_clothing.emp_protection;
        return dot;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:145 (interleaved types, rules 1.9; gaps package C5: a monkey's bite)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:145
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        mob_living_carbon_human biter = user as mob_living_carbon_human;
        object dam_zone = pick(BODY_ZONE_CHEST, BODY_ZONE_PRECISE_L_HAND, BODY_ZONE_PRECISE_R_HAND, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG);
        obj_item_bodypart affecting = get_bodypart(get_random_valid_zone(dam_zone) as string) as obj_item_bodypart;

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK))) //Always drop item in hand, if no item, get stunned instead.
        {
            obj_item I = get_active_held_item() as obj_item;
            if (I != null && !Dm.truthy(Dm.bitand(I.item_flags, ABSTRACT)) && Dm.truthy(await dropItemToGround(I)))
            {
                playsound(loc, Dm.resource("sound/items/weapons/slash.ogg"), 25, TRUE, -1);
                visible_message(span_danger(I18n.T("[] disarmed []!", biter, this)),
                                span_userdanger(I18n.T("[] disarmed you!", biter)), span_hear("You hear aggressive shuffling!"), null, biter);
                to_chat(biter, span_danger(I18n.T("You disarm []!", this)));
            }
            else if (biter.client == null || prob(5)) // only natural monkeys get to stun reliably, (they only do it occasionaly)
            {
                playsound(loc, Dm.resource("sound/items/weapons/pierce.ogg"), 25, TRUE, -1);
                if (Dm.truthy(IsKnockdown()) && !Dm.truthy(IsParalyzed()))
                {
                    await Paralyze(40);
                    log_combat(biter, this, "pinned");
                    visible_message(span_danger(I18n.T("[] pins [] down!", biter, this)),
                                    span_userdanger(I18n.T("[] pins you down!", biter)), span_hear("You hear shuffling and a muffled groan!"), null, biter);
                    to_chat(biter, span_danger(I18n.T("You pin [] down!", this)));
                }
                else
                {
                    await Knockdown(30);
                    log_combat(biter, this, "tackled");
                    visible_message(span_danger(I18n.T("[] tackles [] down!", biter, this)),
                                    span_userdanger(I18n.T("[] tackles you down!", biter)), span_hear("You hear aggressive shuffling followed by a loud thud!"), null, biter);
                    to_chat(biter, span_danger(I18n.T("You tackle [] down!", this)));
                }
            }
            return TRUE;
        }

        if (!Dm.truthy(biter.combat_mode))
        {
            await base.attack_paw(user, modifiers); //shaking
            return FALSE;
        }

        if (Dm.truthy(biter.limb_destroyer))
            await dismembering_strike(biter, affecting.body_zone);

        if (Dm.truthy(try_inject(biter, affecting, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE))) //Thick suits can stop monkey bites.
        {
            if (Dm.truthy(await base.attack_paw(user, modifiers))) //successful monkey bite, this handles disease contraction.
            {
                obj_item_bodypart_head monkey_mouth = biter.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
                float damage = HAS_TRAIT(biter, TRAIT_PERFECT_ATTACKER) ? monkey_mouth.unarmed_damage_high : rand(monkey_mouth.unarmed_damage_low, monkey_mouth.unarmed_damage_high);
                if (!Dm.truthy(damage))
                    return FALSE;
                if (Dm.truthy(await check_block(biter, damage, I18n.T("\\the []", biter), attack_type: UNARMED_ATTACK)))
                    return FALSE;
                await apply_damage(damage, BRUTE, affecting, Dm.num(run_armor_check(affecting, MELEE)));
            }
            return TRUE;
        }
        return null;
    }
}

// tg: code/modules/mob/living/carbon/human/human_defense.dm:396 (interleaved types, rules 1.9; silicons SI1c, D326: the acid)
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defense.dm:396
    [DmParams("acidpwr", "acid_volume", "bodyzone_hit")]
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra) //todo: update this to utilize obscured_slots //and make sure it's check_obscured_slots(TRUE) to stop aciding through visors etc
    {
        object bodyzone_hit = Dm.arg(__extra, 0);
        object dot = null;
        DmList damaged = Dm.list();
        DmList inventory_items_to_kill = Dm.list();
        float acidity = Dm.num(acidpwr) * min(Dm.num(acid_volume) * 0.005f, 0.1f);
        //HEAD//
        if (!Dm.truthy(bodyzone_hit) || Dm.eq(bodyzone_hit, BODY_ZONE_HEAD)) //only if we didn't specify a zone or if that zone is the head.
        {
            obj_item head_clothes = null;
            if (glasses != null)
                head_clothes = glasses;
            if (wear_mask != null)
                head_clothes = wear_mask;
            if (wear_neck != null)
                head_clothes = wear_neck;
            if (head != null)
                head_clothes = head;
            if (head_clothes != null)
            {
                if (!Dm.truthy(Dm.bitand(head_clothes.resistance_flags, UNACIDABLE)))
                {
                    await head_clothes.acid_act(acidpwr, acid_volume);
                    await update_worn_glasses();
                    await update_worn_mask();
                    await update_worn_neck();
                    await update_worn_head();
                }
                else
                    to_chat(this, span_notice(I18n.T("Your [] protects your head and face from the acid!", head_clothes.name)));
            }
            else
            {
                dot = get_bodypart(BODY_ZONE_HEAD);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
                if (ears != null)
                    Dm.append(inventory_items_to_kill, ears);
            }
        }

        //CHEST//
        if (!Dm.truthy(bodyzone_hit) || Dm.eq(bodyzone_hit, BODY_ZONE_CHEST))
        {
            obj_item chest_clothes = null;
            if (w_uniform != null)
                chest_clothes = w_uniform;
            if (wear_suit != null)
                chest_clothes = wear_suit;
            if (chest_clothes != null)
            {
                if (!Dm.truthy(Dm.bitand(chest_clothes.resistance_flags, UNACIDABLE)))
                {
                    await chest_clothes.acid_act(acidpwr, acid_volume);
                    await update_worn_undersuit();
                    await update_worn_oversuit();
                }
                else
                    to_chat(this, span_notice(I18n.T("Your [] protects your body from the acid!", chest_clothes.name)));
            }
            else
            {
                dot = get_bodypart(BODY_ZONE_CHEST);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
                if (wear_id != null)
                    Dm.append(inventory_items_to_kill, wear_id);
                if (r_store != null)
                    Dm.append(inventory_items_to_kill, r_store);
                if (l_store != null)
                    Dm.append(inventory_items_to_kill, l_store);
                if (s_store != null)
                    Dm.append(inventory_items_to_kill, s_store);
            }
        }


        //ARMS & HANDS//
        if (!Dm.truthy(bodyzone_hit) || Dm.eq(bodyzone_hit, BODY_ZONE_L_ARM) || Dm.eq(bodyzone_hit, BODY_ZONE_R_ARM))
        {
            obj_item arm_clothes = null;
            if (gloves != null)
                arm_clothes = gloves;
            if (w_uniform != null && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, ARMS))))
                arm_clothes = w_uniform;
            if (wear_suit != null && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, HANDS)) || Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, ARMS))))
                arm_clothes = wear_suit;

            if (arm_clothes != null)
            {
                if (!Dm.truthy(Dm.bitand(arm_clothes.resistance_flags, UNACIDABLE)))
                {
                    await arm_clothes.acid_act(acidpwr, acid_volume);
                    await update_worn_gloves();
                    await update_worn_undersuit();
                    await update_worn_oversuit();
                }
                else
                    to_chat(this, span_notice(I18n.T("Your [] protects your arms and hands from the acid!", arm_clothes.name)));
            }
            else
            {
                dot = get_bodypart(BODY_ZONE_R_ARM);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
                dot = get_bodypart(BODY_ZONE_L_ARM);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
            }
        }


        //LEGS & FEET//
        if (!Dm.truthy(bodyzone_hit) || Dm.eq(bodyzone_hit, BODY_ZONE_L_LEG) || Dm.eq(bodyzone_hit, BODY_ZONE_R_LEG) || Dm.eq(bodyzone_hit, "feet"))
        {
            obj_item leg_clothes = null;
            if (shoes != null)
                leg_clothes = shoes;
            if (w_uniform != null && (Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, FEET)) || (!Dm.eq(bodyzone_hit, "feet") && Dm.truthy(Dm.bitand(w_uniform.body_parts_covered, LEGS)))))
                leg_clothes = w_uniform;
            if (wear_suit != null && (Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, FEET)) || (!Dm.eq(bodyzone_hit, "feet") && Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, LEGS)))))
                leg_clothes = wear_suit;
            if (leg_clothes != null)
            {
                if (!Dm.truthy(Dm.bitand(leg_clothes.resistance_flags, UNACIDABLE)))
                {
                    await leg_clothes.acid_act(acidpwr, acid_volume);
                    await update_worn_shoes();
                    await update_worn_undersuit();
                    await update_worn_oversuit();
                }
                else
                    to_chat(this, span_notice(I18n.T("Your [] protects your legs and feet from the acid!", leg_clothes.name)));
            }
            else
            {
                dot = get_bodypart(BODY_ZONE_R_LEG);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
                dot = get_bodypart(BODY_ZONE_L_LEG);
                if (Dm.truthy(dot))
                    Dm.append(damaged, dot);
            }
        }


        //DAMAGE//
        foreach (obj_item_bodypart affecting in Dm.each<obj_item_bodypart>(damaged))
        {
            float damage_mod = 1;
            if (Dm.eq(affecting.body_zone, BODY_ZONE_HEAD) && prob(min(Dm.num(acidpwr) * Dm.num(acid_volume) * 0.1f, 90))) //Applies disfigurement
            {
                damage_mod = 2;
                await emote("scream");
                set_facial_hairstyle("Shaved", update: FALSE);
                set_hairstyle("Bald"); //This calls update_body_parts()
                ADD_TRAIT(affecting, TRAIT_DISFIGURED, TRAIT_GENERIC);
            }

            await apply_damage(acidity * damage_mod, BRUTE, affecting);
            await apply_damage(acidity * damage_mod * 2, BURN, affecting);
        }

        //MELTING INVENTORY ITEMS//
        //these items are all outside of armour visually, so melt regardless.
        if (!Dm.truthy(bodyzone_hit))
        {
            if (back != null)
                Dm.append(inventory_items_to_kill, back);
            if (belt != null)
                Dm.append(inventory_items_to_kill, belt);

            Dm.append(inventory_items_to_kill, held_items);
        }

        foreach (obj_item inventory_item in Dm.each<obj_item>(inventory_items_to_kill))
            await inventory_item.acid_act(acidpwr, acid_volume);
        return TRUE;
    }
}
