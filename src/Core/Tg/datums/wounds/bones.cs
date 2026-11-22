// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/bones.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 3: the wound types). The var blocks and the pregen data subtypes are generated (D111). Left for
// their milestones inside the translated procs: the screams (emote, M9: Dm.Deferred); the brain traumas of a head fracture came
// with the brain traumas (gaps B1). The gun's recoil of a broken arm firing is translated with the guns (M7x slice B).

/*
	Overwriting of base procs
*/
// tg: code/datums/wounds/bones.dm:15
public partial class datum_wound_blunt_bone : datum_wound_blunt
{
    // tg: code/datums/wounds/bones.dm:46
    public override async DmTask wound_injury(datum_wound old_wound = null, object attack_direction = null)
    {
        // hook into gaining/losing gauze so crit bone wounds can re-enable/disable depending if they're slung or not
        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD) && Dm.truthy(brain_trauma_group))
        {
            processes = TRUE;
            active_trauma = await victim.gain_trauma_type(brain_trauma_group as Type, TRAUMA_RESILIENCE_WOUND) as datum_brain_trauma;
            next_trauma_cycle = world.time + (rand(100 - WOUND_BONE_HEAD_TIME_VARIANCE, 100 + WOUND_BONE_HEAD_TIME_VARIANCE) * 0.01f * Dm.num(trauma_cycle_cooldown));
        }

        if (Dm.truthy(limb.held_index) && Dm.truthy(victim.get_item_for_held_index(limb.held_index)) && (Dm.truthy(disabling) || prob(30 * severity)))
        {
            obj_item I = victim.get_item_for_held_index(limb.held_index) as obj_item;
            if (istype(I, typeof(obj_item_offhand)))
                I = victim.get_inactive_held_item() as obj_item;

            if (I != null && Dm.truthy(await victim.dropItemToGround(I)))
                victim.visible_message(span_danger(I18n.T("[] drops [] in shock!", victim, I)), span_warning(I18n.T("<b>The force on your [] causes you to drop []!</b>", limb.plaintext_zone, I)), vision_distance: COMBAT_MESSAGE_RANGE);
        }

        update_inefficiencies();
        await base.wound_injury(old_wound, attack_direction);
    }

    // tg: code/datums/wounds/bones.dm:64
    public override async DmTask set_victim(object new_victim = null)
    {
        if (victim != null)
            UnregisterSignal(victim, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_MOB_FIRED_GUN));
        if (new_victim != null)
        {
            RegisterSignal((datum)new_victim, COMSIG_LIVING_UNARMED_ATTACK, nameof(attack_with_hurt_hand));
            RegisterSignal((datum)new_victim, COMSIG_MOB_FIRED_GUN, nameof(firing_with_messed_up_hand));
        }

        await base.set_victim(new_victim);
    }

    // tg: code/datums/wounds/bones.dm:74
    public override async DmTask remove_wound(object ignore_limb = null, float? replaced = null, float? destroying = null)
    {
        limp_slowdown = 0;
        limp_chance = 0;
        QDEL_NULL(ref active_trauma);
        await base.remove_wound(ignore_limb, replaced, destroying);
    }

    // tg: code/datums/wounds/bones.dm:80
    public override async DmTask handle_process(object seconds_per_tick = null)
    {
        await base.handle_process(seconds_per_tick);

        if (victim == null || HAS_TRAIT(victim, TRAIT_STASIS))
            return;

        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD) && Dm.truthy(brain_trauma_group) && world.time > Dm.num(next_trauma_cycle))
        {
            if (active_trauma != null)
                QDEL_NULL(ref active_trauma);
            else
                active_trauma = await victim.gain_trauma_type(brain_trauma_group as Type, TRAUMA_RESILIENCE_WOUND) as datum_brain_trauma;
            next_trauma_cycle = world.time + (rand(100 - WOUND_BONE_HEAD_TIME_VARIANCE, 100 + WOUND_BONE_HEAD_TIME_VARIANCE) * 0.01f * Dm.num(trauma_cycle_cooldown));
        }

        bool is_bone_limb = Dm.truthy(Dm.bitand(limb.biological_state, BIO_BONE)) && !Dm.truthy(Dm.bitand(limb.biological_state, BIO_FLESH | BIO_CHITIN));
        if (!Dm.truthy(healing) && (!Dm.truthy(gelled) || (!Dm.truthy(taped) && !is_bone_limb)))
            return;

        regen_ticks_current = Dm.num(regen_ticks_current) + 1;
        if (Dm.eq(victim.body_position, LYING_DOWN))
        {
            if (SPT_PROB(30, seconds_per_tick))
                regen_ticks_current = Dm.num(regen_ticks_current) + 1;
            if (Dm.truthy(victim.IsSleeping()) && SPT_PROB(30, seconds_per_tick))
                regen_ticks_current = Dm.num(regen_ticks_current) + 1;
        }

        if (!is_bone_limb && SPT_PROB(severity * 1.5f, seconds_per_tick))
        {
            await victim.take_bodypart_damage(rand(1, severity * 2), wound_bonus: CANT_WOUND);
            await victim.adjust_stamina_loss(rand(2, severity * 2.5f));
            if (prob(33))
                to_chat(victim, span_danger(I18n.T("You feel a sharp pain in your body as your bones are reforming!")));
        }

        if (Dm.num(regen_ticks_current) > Dm.num(regen_ticks_needed))
        {
            if (victim == null || limb == null)
            {
                qdel(this);
                return;
            }
            to_chat(victim, span_green(I18n.T("Your [] has recovered from its []!", limb.plaintext_zone, LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name))));
            await remove_wound();
        }
    }

    /// If we're a human who's punching something with a broken arm, we might hurt ourselves doing so
    // tg: code/datums/wounds/bones.dm:118
    [SignalHandler]
    public virtual object attack_with_hurt_hand(mob M = null, atom target = null, object proximity = null)
    {
        if (victim.get_active_hand() != limb || !Dm.truthy(proximity) || !Dm.truthy(victim.combat_mode) || !ismob(target) || severity <= WOUND_SEVERITY_MODERATE)
            return NONE;

        // With a severe or critical wound, you have a 15% or 30% chance to proc pain on hit
        if (prob((severity - 1) * 15))
        {
            // And you have a 70% or 50% chance to actually land the blow, respectively
            if (HAS_TRAIT(victim, TRAIT_ANALGESIA) || prob(70 - 20 * (severity - 1)))
            {
                if (!HAS_TRAIT(victim, TRAIT_ANALGESIA))
                    to_chat(victim, span_danger(I18n.T("The fracture in your [] shoots with pain as you strike []!", limb.plaintext_zone, target)));
                Waiting.NoSleep(victim.apply_damage(rand(1, 5), BRUTE, limb, wound_bonus: CANT_WOUND, wound_clothing: FALSE));
            }
            else
            {
                victim.visible_message(span_danger(I18n.T("[] weakly strikes [] with [] broken [], recoiling from pain!", victim, target, victim.p_their(), limb.plaintext_zone)),
                    span_userdanger(I18n.T("You fail to strike [] as the fracture in your [] lights up in unbearable pain!", target, limb.plaintext_zone)), vision_distance: COMBAT_MESSAGE_RANGE);
                Waiting.Detach(victim.emote("scream")); // INVOKE_ASYNC
                Waiting.NoSleep(victim.Stun(0.5f * SECONDS));
                Waiting.NoSleep(victim.apply_damage(rand(3, 7), BRUTE, limb, wound_bonus: CANT_WOUND, wound_clothing: FALSE));
                return COMPONENT_CANCEL_ATTACK_CHAIN;
            }
        }

        return NONE;
    }

    /// If we're a human who's firing a gun with a broken arm, we might hurt ourselves doing so
    // tg: code/datums/wounds/bones.dm:142
    [SignalHandler]
    public virtual void firing_with_messed_up_hand(datum source = null, obj_item_gun gun = null, atom firing_at = null, object @params = null, object zone = null, object bonus_spread_values = null)
    {
        if (Dm.eq(limb.body_zone, BODY_ZONE_L_ARM))
        {
            // Heavy guns use both hands so they will always get a penalty
            // (Yes, this means having two broken arms will make heavy weapons SOOO much worse)
            // Otherwise make sure THIS hand is firing THIS gun
            if (gun.weapon_weight <= WEAPON_MEDIUM && !IS_LEFT_INDEX(Dm.num(victim.get_held_index_of_item(gun))))
                return;
        }
        else if (Dm.eq(limb.body_zone, BODY_ZONE_R_ARM))
        {
            // Ditto but for right arm
            if (gun.weapon_weight <= WEAPON_MEDIUM && !IS_RIGHT_INDEX(Dm.num(victim.get_held_index_of_item(gun))))
                return;
        }
        else
        {
            // This is not arm wound, so we don't care
            return;
        }

        if (Dm.num(gun.calculate_recoil(victim as mob_living, gun.recoil)) > 1 && severity >= WOUND_SEVERITY_SEVERE && prob(25 * (severity - 1)))
        {
            if (!HAS_TRAIT(victim, TRAIT_ANALGESIA))
                to_chat(victim, span_danger(I18n.T("The fracture in your [] explodes with pain as [] kicks back!", limb.plaintext_zone, gun)));
            Waiting.NoSleep(victim.apply_damage(rand(1, 3) * (severity - 1) * gun.weapon_weight, BRUTE, limb, wound_bonus: CANT_WOUND, wound_clothing: FALSE));
        }

        if (!HAS_TRAIT(victim, TRAIT_ANALGESIA))
        {
            DmList spread = (DmList)bonus_spread_values;
            spread[MAX_BONUS_SPREAD_INDEX] = Dm.num(spread[MAX_BONUS_SPREAD_INDEX]) + (15 * severity * Dm.num(limb.get_splint_factor()));
        }
    }

    // tg: code/datums/wounds/bones.dm:170
    [DmParams("wounding_type", "wounding_dmg", "wound_bonus")]
    public override async DmTask receive_damage(object wounding_type = null, object wounding_dmg = null, object wound_bonus = null, object attack_direction = null, object damage_source = null)
    {
        if (victim == null || Dm.num(wounding_dmg) < WOUND_MINIMUM_DAMAGE || !Dm.truthy(victim.can_bleed()))
            return;

        if (Dm.eq(limb.body_zone, BODY_ZONE_CHEST) && Dm.truthy(victim.get_blood_volume()) && prob(internal_bleeding_chance + Dm.num(wounding_dmg)))
        {
            float blood_bled = rand(1, Dm.num(wounding_dmg) * (severity == WOUND_SEVERITY_CRITICAL ? 2 : 1.5f)); // 12 brute toolbox can cause up to 18/24 bleeding with a severe/critical chest wound
            // victim.bleed(blood_bled, TRUE): bleed declares one parameter (blood.dm:270), the TRUE is an extra argument
            if (1 <= blood_bled && blood_bled <= 6)
                await victim.bleed(blood_bled);
            else if (7 <= blood_bled && blood_bled <= 13)
            {
                victim.visible_message(
                    span_smalldanger(I18n.T("A thin stream of blood drips from []'s mouth from the blow to [] chest.", victim, victim.p_their())),
                    span_danger(I18n.T("You cough up a bit of blood from the blow to your chest.")),
                    vision_distance: COMBAT_MESSAGE_RANGE);
                await victim.bleed(blood_bled);
            }
            else if (14 <= blood_bled && blood_bled <= 19)
            {
                victim.visible_message(
                    span_smalldanger(I18n.T("Blood spews out of []'s mouth from the blow to [] chest!", victim, victim.p_their())),
                    span_danger(I18n.T("You spit out a string of blood from the blow to your chest!")),
                    vision_distance: COMBAT_MESSAGE_RANGE);
                await victim.create_splatter(victim.dir);
                await victim.bleed(blood_bled);
            }
            else if (20 <= blood_bled)
            {
                victim.visible_message(
                    span_danger(I18n.T("Blood spurts out of []'s mouth from the blow to [] chest!", victim, victim.p_their())),
                    span_bolddanger(I18n.T("You choke up on a spray of blood from the blow to your chest!")),
                    vision_distance: COMBAT_MESSAGE_RANGE);
                await victim.bleed(blood_bled);
                await victim.create_splatter(victim.dir);
                await victim.add_splatter_floor(get_step(victim.loc, victim.dir) as turf);
            }
        }
    }

    // tg: code/datums/wounds/bones.dm:204
    [DmParams("desc")]
    public override object modify_desc_before_span(object desc = null, mob user = null)
    {
        object dot = base.modify_desc_before_span(desc, user);

        obj_item_stack_medical_wrap current_gauze = LAZYACCESS(limb.applied_items, LIMB_ITEM_GAUZE) as obj_item_stack_medical_wrap;
        if (current_gauze == null)
        {
            if (Dm.truthy(taped))
                dot = Dm.text("[]", dot) + I18n.T(", []", span_notice(I18n.T("and appears to be reforming itself under some surgical tape!")));
            else if (Dm.truthy(gelled))
                dot = Dm.text("[]", dot) + I18n.T(", []", span_notice(I18n.T("with fizzing flecks of blue bone gel sparking off the bone!")));
        }
        return dot;
    }

    /*
        New common procs for /datum/wound/blunt/bone/
    */

    // tg: code/datums/wounds/bones.dm:221
    public override object get_scar_file(obj_item_bodypart scarred_limb = null, object add_to_scars = null)
    {
        if (Dm.truthy(Dm.bitand(scarred_limb.biological_state, BIO_BONE)) && !Dm.truthy(Dm.bitand(scarred_limb.biological_state, BIO_FLESH))) // only bone
            return BONE_SCAR_FILE;
        else if (Dm.truthy(Dm.bitand(scarred_limb.biological_state, BIO_FLESH)) && !Dm.truthy(Dm.bitand(scarred_limb.biological_state, BIO_BONE)))
            return FLESH_SCAR_FILE;

        return base.get_scar_file(scarred_limb, add_to_scars);
    }

    /// if someone is using bone gel on our wound
    // tg: code/datums/wounds/bones.dm:460
    public virtual async DmTask<object> gel(obj_item_stack_medical_bone_gel I = null, mob user = null)
    {
        // skellies get treated nicer with bone gel since their "reattach dismembered limbs by hand" ability sucks when it's still critically wounded
        if (Dm.truthy(Dm.bitand(limb.biological_state, BIO_BONE)) && !Dm.truthy(Dm.bitand(limb.biological_state, BIO_FLESH | BIO_CHITIN)))
            return await skelly_gel(I, user);

        if (Dm.truthy(gelled))
        {
            to_chat(user, span_warning(I18n.T("[] [] is already coated with bone gel!", user == victim ? I18n.T("Your") : I18n.T("[]'s", victim), limb.plaintext_zone)));
            return TRUE;
        }

        user.visible_message(span_danger(I18n.T("[] begins hastily applying [] to []'s' []...", user, I, victim, limb.plaintext_zone)), span_warning(I18n.T("You begin hastily applying [] to [] [], disregarding the warning label...", I, user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone)));

        if (!Dm.truthy(await do_after(user, base_treat_time * 1.5f * (user == victim ? 1.5f : 1), target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return TRUE;

        await I.use(1);
        await victim.emote("scream");
        if (user != victim)
        {
            user.visible_message(span_notice(I18n.T("[] finishes applying [] to []'s [], emitting a fizzing noise!", user, I, victim, limb.plaintext_zone)), span_notice(I18n.T("You finish applying [] to []'s []!", I, victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] finishes applying [] to your [], and you can feel the bones exploding with pain as they begin melting and reforming!", user, I, limb.plaintext_zone)));
        }
        else
        {
            if (!HAS_TRAIT(victim, TRAIT_ANALGESIA))
            {
                if (prob(25 + (20 * (severity - 2)) - min(Dm.num(victim.get_drunk_amount()), 10))) // 25%/45% chance to fail self-applying with severe and critical wounds, modded by drunkenness
                {
                    victim.visible_message(span_danger(I18n.T("[] fails to finish applying [] to [] [], passing out from the pain!", victim, I, victim.p_their(), limb.plaintext_zone)), span_notice(I18n.T("You pass out from the pain of applying [] to your [] before you can finish!", I, limb.plaintext_zone)));
                    await victim.AdjustUnconscious(5 * SECONDS);
                    return TRUE;
                }
            }
            victim.visible_message(span_notice(I18n.T("[] finishes applying [] to [] [], grimacing from the pain!", victim, I, victim.p_their(), limb.plaintext_zone)), span_notice(I18n.T("You finish applying [] to your [], and your bones explode in pain!", I, limb.plaintext_zone)));
        }

        await victim.apply_damage(25, BRUTE, limb, wound_bonus: CANT_WOUND);
        await victim.apply_damage(100, STAMINA);
        gelled = TRUE;
        return TRUE;
    }

    /// skellies are less averse to bone gel, since they're literally all bone
    // tg: code/datums/wounds/bones.dm:493
    public virtual async DmTask<object> skelly_gel(obj_item_stack_medical_bone_gel I = null, mob user = null)
    {
        if (Dm.truthy(gelled))
        {
            to_chat(user, span_warning(I18n.T("[] [] is already coated with bone gel!", user == victim ? I18n.T("Your") : I18n.T("[]'s", victim), limb.plaintext_zone)));
            return null;
        }

        user.visible_message(span_danger(I18n.T("[] begins applying [] to []'s' []...", user, I, victim, limb.plaintext_zone)), span_warning(I18n.T("You begin applying [] to [] []...", I, user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone)));

        if (!Dm.truthy(await do_after(user, base_treat_time * (user == victim ? 1.5f : 1), target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return null;

        await I.use(1);
        if (user != victim)
        {
            user.visible_message(span_notice(I18n.T("[] finishes applying [] to []'s [], emitting a fizzing noise!", user, I, victim, limb.plaintext_zone)), span_notice(I18n.T("You finish applying [] to []'s []!", I, victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] finishes applying [] to your [], and you feel a funny fizzy tickling as they begin to reform!", user, I, limb.plaintext_zone)));
        }
        else
            victim.visible_message(span_notice(I18n.T("[] finishes applying [] to [] [], emitting a funny fizzing sound!", victim, I, victim.p_their(), limb.plaintext_zone)), span_notice(I18n.T("You finish applying [] to your [], and feel a funny fizzy tickling as the bone begins to reform!", I, limb.plaintext_zone)));

        gelled = TRUE;
        processes = TRUE;
        return TRUE;
    }

    /// if someone is using surgical tape on our wound
    // tg: code/datums/wounds/bones.dm:515
    public virtual async DmTask<object> tape(obj_item_stack_medical_wrap_sticky_tape_surgical I = null, mob user = null)
    {
        if (!Dm.truthy(gelled))
        {
            to_chat(user, span_warning(I18n.T("[] [] must be coated with bone gel to perform this emergency operation!", user == victim ? I18n.T("Your") : I18n.T("[]'s", victim), limb.plaintext_zone)));
            return TRUE;
        }
        if (Dm.truthy(taped))
        {
            to_chat(user, span_warning(I18n.T("[] [] is already wrapped in [] and reforming!", user == victim ? I18n.T("Your") : I18n.T("[]'s", victim), limb.plaintext_zone, I.name)));
            return TRUE;
        }

        user.visible_message(span_danger(I18n.T("[] begins applying [] to []'s' []...", user, I, victim, limb.plaintext_zone)), span_warning(I18n.T("You begin applying [] to [] []...", I, user == victim ? I18n.T("your") : I18n.T("[]'s", victim), limb.plaintext_zone)));

        if (!Dm.truthy(await do_after(user, base_treat_time * (user == victim ? 1.5f : 1), target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return TRUE;

        if (victim == user)
            regen_ticks_needed = Dm.num(regen_ticks_needed) * 1.5f;

        await I.use(1);
        if (user != victim)
        {
            user.visible_message(span_notice(I18n.T("[] finishes applying [] to []'s [], emitting a fizzing noise!", user, I, victim, limb.plaintext_zone)), span_notice(I18n.T("You finish applying [] to []'s []!", I, victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_green(I18n.T("[] finishes applying [] to your [], you immediately begin to feel your bones start to reform!", user, I, limb.plaintext_zone)));
        }
        else
            victim.visible_message(span_notice(I18n.T("[] finishes applying [] to [] [], !", victim, I, victim.p_their(), limb.plaintext_zone)), span_green(I18n.T("You finish applying [] to your [], and you immediately begin to feel your bones start to reform!", I, limb.plaintext_zone)));

        taped = TRUE;
        processes = TRUE;
        return TRUE;
    }

    // tg: code/datums/wounds/bones.dm:542
    public override object item_can_treat(obj_item potential_treater = null, mob user = null)
    {
        // assume that - if working on a ready-to-operate limb - the surgery wants to do the real surgery instead of bone regeneration
        object dot = base.item_can_treat(potential_treater, user);
        return Dm.truthy(dot) ? (!HAS_TRAIT(limb, TRAIT_READY_TO_OPERATE) ? TRUE : FALSE) : dot;
    }

    // tg: code/datums/wounds/bones.dm:546
    public override async DmTask treat(obj_item tool = null, mob user = null)
    {
        if (istype(tool, typeof(obj_item_stack_medical_bone_gel)))
            await gel((obj_item_stack_medical_bone_gel)tool, user);
        if (istype(tool, typeof(obj_item_stack_medical_wrap_sticky_tape_surgical)))
            await tape((obj_item_stack_medical_wrap_sticky_tape_surgical)tool, user);
    }

    // tg: code/datums/wounds/bones.dm:552
    public override object get_scanner_description(mob user = null)
    {
        string dot = Dm.text("[]", base.get_scanner_description(user));

        dot += "<div class='ml-3'>";

        if (severity > WOUND_SEVERITY_MODERATE)
        {
            if (Dm.truthy(Dm.bitand(limb.biological_state, BIO_BONE)) && !Dm.truthy(Dm.bitand(limb.biological_state, BIO_FLESH | BIO_CHITIN)))
            {
                if (!Dm.truthy(gelled))
                    dot += I18n.T("Recommended Treatment: Apply bone gel directly to injured limb. Creatures of pure bone don't seem to mind bone gel application nearly as much as fleshed individuals. Surgical tape will also be unnecessary.\n");
                else
                    dot += I18n.T("[]\n", span_notice(I18n.T("Note: Bone regeneration in effect. Bone is []% regenerated.", round(Dm.num(regen_ticks_current) * 100 / Dm.num(regen_ticks_needed)))));
            }
            else
            {
                if (!Dm.truthy(gelled))
                    dot += I18n.T("Alternative Treatment: Apply bone gel directly to injured limb, then apply surgical tape to begin bone regeneration. This is both excruciatingly painful and slow, and only recommended in dire circumstances.\n");
                else if (!Dm.truthy(taped))
                    dot += I18n.T("[]\n", span_notice(I18n.T("Continue Alternative Treatment: Apply surgical tape directly to injured limb to begin bone regeneration. Note, this is both excruciatingly painful and slow, though sleep or laying down will speed recovery.")));
                else
                    dot += I18n.T("[]\n", span_notice(I18n.T("Note: Bone regeneration in effect. Bone is []% regenerated.", round(Dm.num(regen_ticks_current) * 100 / Dm.num(regen_ticks_needed)))));
            }
        }

        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
            dot += I18n.T("Cranial Trauma Detected: Patient will suffer random bouts of [] brain traumas until bone is repaired.", severity == WOUND_SEVERITY_SEVERE ? I18n.T("mild") : I18n.T("severe"));
        else if (Dm.eq(limb.body_zone, BODY_ZONE_CHEST) && Dm.truthy(CAN_HAVE_BLOOD(victim)))
            dot += I18n.T("Ribcage Trauma Detected: Further trauma to chest is likely to worsen internal bleeding until bone is repaired.");
        dot += "</div>";
        return dot;
    }
}

// tg: code/datums/wounds/bones.dm:214
public partial class datum_wound_blunt : datum_wound
{
    // tg: code/datums/wounds/bones.dm:214
    public override object get_limb_examine_description()
    {
        return span_warning(I18n.T("The bones in this limb appear badly cracked."));
    }
}

/// Joint Dislocation (Moderate Blunt)
// tg: code/datums/wounds/bones.dm:230
public partial class datum_wound_blunt_bone_moderate : datum_wound_blunt_bone
{
    // tg: code/datums/wounds/bones.dm:262
    public override object Destroy(float? force = null)
    {
        if (victim != null)
            UnregisterSignal(victim, COMSIG_LIVING_DOORCRUSHED);
        return base.Destroy(force);
    }

    // tg: code/datums/wounds/bones.dm:267
    public override async DmTask set_victim(object new_victim = null)
    {
        if (victim != null)
            UnregisterSignal(victim, COMSIG_LIVING_DOORCRUSHED);
        if (new_victim != null)
            RegisterSignal((datum)new_victim, COMSIG_LIVING_DOORCRUSHED, nameof(door_crush));

        await base.set_victim(new_victim);
    }

    // tg: code/datums/wounds/bones.dm:276
    public override object get_self_check_description(object self_aware = null)
    {
        return span_warning(I18n.T("It feels dislocated!"));
    }

    /// Getting smushed in an airlock/firelock is a last-ditch attempt to try relocating your limb
    // tg: code/datums/wounds/bones.dm:280
    [SignalHandler]
    public virtual object door_crush()
    {
        if (prob(40))
        {
            victim.visible_message(span_danger(I18n.T("[]'s dislocated [] pops back into place!", victim, limb.plaintext_zone)), span_userdanger(I18n.T("Your dislocated [] pops back into place! Ow!", limb.plaintext_zone)));
            Waiting.NoSleep(remove_wound());
            return DOORCRUSH_NO_WOUND;
        }
        return NONE;
    }

    // tg: code/datums/wounds/bones.dm:288
    public override async DmTask<object> try_handling(mob_living user = null)
    {
        if (user.usable_hands <= 0 || user.pulling != victim)
            return FALSE;
        if (user.hud_used?.screen_objects[HUD_MOB_ZONE_SELECTOR] != null && !Dm.eq(user.zone_selected, limb.body_zone))
            return FALSE;

        if (user.grab_state == GRAB_PASSIVE)
        {
            to_chat(user, span_warning(I18n.T("You must have [] in an aggressive grab to manipulate [] []!", victim, victim.p_their(), LOWER_TEXT(Dm.truthy(undiagnosed_name) ? undiagnosed_name : name))));
            return TRUE;
        }

        if (user.grab_state >= GRAB_AGGRESSIVE)
        {
            user.visible_message(span_danger(I18n.T("[] begins twisting and straining []'s dislocated []!", user, victim, limb.plaintext_zone)), span_notice(I18n.T("You begin twisting and straining []'s dislocated []...", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] begins twisting and straining your dislocated []!", user, limb.plaintext_zone)));
            if (!Dm.truthy(user.combat_mode))
                await chiropractice(user as mob_living_carbon_human);
            else
                await malpractice(user as mob_living_carbon_human);
            return TRUE;
        }
        return null;
    }

    /// If someone is snapping our dislocated joint back into place by hand with an aggro grab and help intent
    // tg: code/datums/wounds/bones.dm:308
    public virtual async DmTask chiropractice(mob_living_carbon_human user = null)
    {
        float time = base_treat_time;

        if (!Dm.truthy(await do_after(user, time, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return;

        if (prob(65))
        {
            user.visible_message(span_danger(I18n.T("[] snaps []'s dislocated [] back into place!", user, victim, limb.plaintext_zone)), span_notice(I18n.T("You snap []'s dislocated [] back into place!", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] snaps your dislocated [] back into place!", user, limb.plaintext_zone)));
            await victim.emote("scream");
            await victim.apply_damage(20, BRUTE, limb, wound_bonus: CANT_WOUND);
            qdel(this);
        }
        else
        {
            user.visible_message(span_danger(I18n.T("[] wrenches []'s dislocated [] around painfully!", user, victim, limb.plaintext_zone)), span_danger(I18n.T("You wrench []'s dislocated [] around painfully!", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] wrenches your dislocated [] around painfully!", user, limb.plaintext_zone)));
            await victim.apply_damage(10, BRUTE, limb, wound_bonus: CANT_WOUND);
            await chiropractice(user);
        }
    }

    /// If someone is snapping our dislocated joint into a fracture by hand with an aggro grab and harm or disarm intent
    // tg: code/datums/wounds/bones.dm:327
    public virtual async DmTask malpractice(mob_living_carbon_human user = null)
    {
        float time = base_treat_time;

        if (!Dm.truthy(await do_after(user, time, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return;

        if (prob(65))
        {
            user.visible_message(span_danger(I18n.T("[] snaps []'s dislocated [] with a sickening crack!", user, victim, limb.plaintext_zone)), span_danger(I18n.T("You snap []'s dislocated [] with a sickening crack!", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] snaps your dislocated [] with a sickening crack!", user, limb.plaintext_zone)));
            await victim.emote("scream");
            await victim.apply_damage(25, BRUTE, limb, wound_bonus: 30);
        }
        else
        {
            user.visible_message(span_danger(I18n.T("[] wrenches []'s dislocated [] around painfully!", user, victim, limb.plaintext_zone)), span_danger(I18n.T("You wrench []'s dislocated [] around painfully!", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] wrenches your dislocated [] around painfully!", user, limb.plaintext_zone)));
            await victim.apply_damage(10, BRUTE, limb, wound_bonus: CANT_WOUND);
            await malpractice(user);
        }
    }

    // tg: code/datums/wounds/bones.dm:344
    public override async DmTask treat(obj_item tool = null, mob user = null)
    {
        bool scanned = HAS_TRAIT(this, TRAIT_WOUND_SCANNED);
        float self_penalty_mult = user == victim ? 1.5f : 1;
        float scanned_mult = scanned ? 0.5f : 1;
        float treatment_delay = base_treat_time * self_penalty_mult * scanned_mult;

        if (victim == user)
            victim.visible_message(span_danger(I18n.T("[] begins [] resetting [] [] with [].", user, scanned ? I18n.T("expertly") : "", victim.p_their(), limb.plaintext_zone, tool)), span_warning(I18n.T("You begin resetting your [] with [][]...", limb.plaintext_zone, tool, scanned ? I18n.T(", keeping the holo-image's indications in mind") : "")));
        else
            user.visible_message(span_danger(I18n.T("[] begins [] resetting []'s [] with [].", user, scanned ? I18n.T("expertly") : "", victim, limb.plaintext_zone, tool)), span_notice(I18n.T("You begin resetting []'s [] with [][]...", victim, limb.plaintext_zone, tool, scanned ? I18n.T(", keeping the holo-image's indications in mind") : "")));

        if (!Dm.truthy(await do_after(user, treatment_delay, target: victim, extra_checks: Dm.@new<datum_callback>(this, nameof(still_exists)))))
            return;

        if (victim == user)
        {
            await victim.apply_damage(15, BRUTE, limb, wound_bonus: CANT_WOUND);
            victim.visible_message(span_danger(I18n.T("[] finishes resetting [] []!", user, victim.p_their(), limb.plaintext_zone)), span_userdanger(I18n.T("You reset your []!", limb.plaintext_zone)));
        }
        else
        {
            await victim.apply_damage(10, BRUTE, limb, wound_bonus: CANT_WOUND);
            user.visible_message(span_danger(I18n.T("[] finishes resetting []'s []!", user, victim, limb.plaintext_zone)), span_nicegreen(I18n.T("You finish resetting []'s []!", victim, limb.plaintext_zone)), ignored_mobs: victim);
            to_chat(victim, span_userdanger(I18n.T("[] resets your []!", user, limb.plaintext_zone)));
        }

        await victim.emote("scream");
        qdel(this);
    }
}

/// Compound Fracture (Critical Blunt)
// tg: code/datums/wounds/bones.dm:409
public partial class datum_wound_blunt_bone_critical : datum_wound_blunt_bone
{
    // doesn't make much sense for "a" bone to stick out of your head
    // tg: code/datums/wounds/bones.dm:453
    [DmParams("L", "silent", "old_wound", "smited", "attack_direction", "wound_source", "replacing")]
    public override async DmTask<object> apply_wound(obj_item_bodypart limb = null, float? silent = null, datum_wound old_wound = null, float? smited = null, object attack_direction = null, object wound_source = null, float? replacing = null, params object[] __extra)
    {
        if (Dm.eq(limb.body_zone, BODY_ZONE_HEAD))
        {
            occur_text = I18n.T("splits open, exposing a bare, cracked skull through the flesh and blood");
            examine_desc = I18n.T("has an unsettling indent, with bits of skull poking out");
        }
        return await base.apply_wound(limb, silent, old_wound, smited, attack_direction, wound_source, replacing, __extra);
    }
}
