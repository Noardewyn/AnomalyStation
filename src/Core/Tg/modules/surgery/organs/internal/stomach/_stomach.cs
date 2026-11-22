// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/stomach/_stomach.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The stomach's Initialize, Destroy, on_life with handle_hunger, the disgust bookkeeping and its insertion hooks
// (M2 P2, plan.md Q7: a thin body; hunger ticks in M2), on_vomit with empty_contents (the M7 visual check: a vomiting
// carbon's signal reached it), and the digestion of reagents and of swallowed things (M8 slice A, D194: tg's two on_life
// definitions, the earlier one on_life__d0, rules 7.5). Gaps S1 (D291): the hunger display's updates, the junk food's jitter and
// the punched-out vomiting. Gaps OG1: the scalpel and cautery on a stomach, the cybernetic stomachs' acid. M8 slice B:
// handle_disgust's effects and alerts (morphine, syriniver and musiver make a body disgusted) with their mood events (gaps MS1).

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:4
[DmPath("/obj/item/organ/stomach")]
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:5-31
    public obj_item_organ_stomach()
    {
        name = "stomach";
        desc = "Onaka ga suite imasu.";
        icon_state = "stomach";

        w_class = WEIGHT_CLASS_SMALL;
        zone = BODY_ZONE_CHEST;
        slot = ORGAN_SLOT_STOMACH;

        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY * 1.15f; // ~13 minutes, the stomach is one of the first organs to die

        low_threshold_passed = span_info("Your stomach flashes with pain before subsiding. Food doesn't seem like a good idea right now.");
        high_threshold_passed = span_warning("Your stomach flares up with constant pain- you can hardly stomach the idea of food right now!");
        high_threshold_cleared = span_info("The pain in your stomach dies down for now, but food still seems unappealing.");
        low_threshold_cleared = span_info("The last bouts of pain in your stomach have died out.");

        //This is a reagent user and needs more then the 10u from edible component
        reagent_vol = 1000;

        cell_line = CELL_LINE_ORGAN_STOMACH;
        cells_minimum = 1;
        cells_maximum = 2;

        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:12-23  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        attack_verb_continuous = Dm.list("gores", "squishes", "slaps", "digests");
        attack_verb_simple = Dm.list("gore", "squish", "slap", "digest");

        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_organ_tissue_stomach_lining), 5));
    }

    ///The rate that disgust decays
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:34
    public float disgust_metabolism = 1;

    ///The rate that the stomach will transfer reagents to the body
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:37
    public float metabolism_efficiency = 0.05f; // the lowest we should go is 0.025

    /// Multiplier for hunger rate
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:40
    public float hunger_modifier = 1;
    /// Whether the stomach's been repaired with surgery and can be fixed again or not
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:42
    public float operated = FALSE;
    /// List of all atoms within the stomach
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:44
    public DmList stomach_contents;
    /// Have we been cut open with a scalpel? If so, how much damage from it we still have from it and can be recovered with a cauterizing tool.
    /// All healing goes towards recovering this.
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:47
    public float cut_open_damage = 0;
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:48 (interleaved types, rules 1.9)
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        //None edible organs do not get a reagent holder by default
        if (!Dm.truthy(reagents))
            create_reagents(reagent_vol, REAGENT_HOLDER_ALIVE);
        else
            ((datum_reagents)reagents).flags = Dm.bitor(((datum_reagents)reagents).flags, REAGENT_HOLDER_ALIVE);
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:56
    public override object Destroy(float? force = null)
    {
        QDEL_LAZYLIST(ref stomach_contents);
        return base.Destroy(force);
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:2  #define STOMACH_METABOLISM_CONSTANT 0.25 //The contant in the rate of reagent transfer on life ticks (file-local, #undef at :586)
    internal const float STOMACH_METABOLISM_CONSTANT = 0.25f;

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:61  (the earlier of two definitions on this type, rules 7.5)
    public async DmTask on_life__d0(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);

        //Manage species digestion
        if (ishuman(owner))
        {
            mob_living_carbon_human humi = (mob_living_carbon_human)owner;
            if (!Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
                await handle_hunger(humi, seconds_per_tick);
        }

        mob_living_carbon body = owner;

        // digest food, sent all reagents that can metabolize to the body
        foreach (datum_reagent bit in Dm.each_cast<datum_reagent>((reagents as datum_reagents)?.reagent_list))
        {
            // If the reagent does not metabolize then it will sit in the stomach
            // This has an effect on items like plastic causing them to take up space in the stomach
            if (bit.metabolization_rate <= 0)
                continue;

            //Ensure that the the minimum is equal to the metabolization_rate of the reagent if it is higher then the STOMACH_METABOLISM_CONSTANT
            float rate_min = max(bit.metabolization_rate, STOMACH_METABOLISM_CONSTANT);
            //Do not transfer over more then we have
            float amount_max = Dm.num(bit.volume);

            //If the reagent is part of the food reagents for the organ
            //prevent all the reagents form being used leaving the food reagents
            object amount_food = food_reagents?[bit.type];
            if (Dm.truthy(amount_food))
                amount_max = max(amount_max - Dm.num(amount_food), 0);

            // Transfer the amount of reagents based on volume with a min amount of 1u
            float amount = min((round(metabolism_efficiency * amount_max, 0.05f) + rate_min) * Dm.num(seconds_per_tick), amount_max);

            if (amount <= 0)
                continue;

            // transfer the reagents over to the body at the rate of the stomach metabolim
            // this way the body is where all reagents that are processed and react
            // the stomach manages how fast they are feed in a drip style
            await ((datum_reagents)reagents).trans_to(body, amount, target_id: bit.type);
        }

        //Handle disgust
        if (body != null)
            // rules 3.3 (decided, gaps X5, Appendix A): tg declares the parameter /mob/living/carbon/human but passes the carbon body unchecked
            // (a monkey's or an alien's stomach), and every member it reads is a carbon's, so the parameter is the carbon.
            await handle_disgust(body, seconds_per_tick);

        //If the stomach is not damage exit out
        if (Dm.num(damage) < low_threshold)
            return;

        //We are checking if we have nutriment in a damaged stomach.
        // locate(/datum/reagent/consumable/nutriment) in reagents?.reagent_list
        datum_reagent nutri = null;
        foreach (object r__1 in Dm.each((reagents as datum_reagents)?.reagent_list))
        {
            if (r__1 is datum_reagent_consumable_nutriment)
            {
                nutri = (datum_reagent)r__1;
                break;
            }
        }
        //No nutriment found lets exit out
        if (nutri == null)
            return;

        // remove the food reagent amount
        float nutri_vol = Dm.num(nutri.volume);
        object nutri_food = food_reagents?[nutri.type];
        if (Dm.truthy(nutri_food))
            nutri_vol = max(nutri_vol - Dm.num(nutri_food), 0);

        // found nutriment was stomach food reagent
        if (!(nutri_vol > 0))
            return;

        //The stomach is damage has nutriment but low on theshhold, lo prob of vomit
        if (SPT_PROB(0.0125f * Dm.num(damage) * nutri_vol * nutri_vol, seconds_per_tick))
        {
            await body.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: Dm.num(damage));
            to_chat(body, span_warning(I18n.T("Your stomach reels in pain as you're incapable of holding down all that food!")));
            return;
        }

        // the change of vomit is now high
        if (Dm.num(damage) > high_threshold && SPT_PROB(0.05f * Dm.num(damage) * nutri_vol * nutri_vol, seconds_per_tick))
        {
            await body.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: Dm.num(damage));
            to_chat(body, span_warning(I18n.T("Your stomach reels in pain as you're incapable of holding down all that food!")));
        }
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:137
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:137
    public virtual async DmTask handle_hunger(mob_living_carbon_human human = null, object seconds_per_tick = null)
    {
        if (HAS_TRAIT(human, TRAIT_NOHUNGER))
            return; //hunger is for BABIES

        //The fucking TRAIT_FAT mutation is the dumbest shit ever. It makes the code so difficult to work with
        if (HAS_TRAIT_FROM(human, TRAIT_FAT, OBESITY)) //I share your pain, past coder.
        {
            if (human.overeatduration < (OVEREAT_TIME_LIMIT))
            {
                to_chat(human, span_notice(I18n.T("You feel fit again!")));
                human.remove_traits(Dm.list(TRAIT_FAT, TRAIT_OFF_BALANCE_TACKLER), OBESITY);
            }
        }
        else
        {
            if (human.overeatduration >= (OVEREAT_TIME_LIMIT))
            {
                to_chat(human, span_danger(I18n.T("You suddenly feel blubbery!")));
                human.add_traits(Dm.list(TRAIT_FAT, TRAIT_OFF_BALANCE_TACKLER), OBESITY);
            }
        }

        // nutrition decrease and satiety
        if (human.nutrition > 0 && human.stat != DEAD)
        {
            // THEY HUNGER
            float hunger_rate = HUNGER_FACTOR;
            if (human.mob_mood != null && human.mob_mood.sanity > SANITY_DISTURBED)
                hunger_rate *= max(1 - 0.002f * human.mob_mood.sanity, 0.5f); //0.85 to 0.75
            // Whether we cap off our satiety or move it towards 0
            if (human.satiety > MAX_SATIETY)
                human.satiety = MAX_SATIETY;
            else if (human.satiety > 0)
                human.satiety--;
            else if (human.satiety < -MAX_SATIETY)
                human.satiety = -MAX_SATIETY;
            else if (human.satiety < 0)
            {
                human.satiety++;
                if (SPT_PROB(round(-human.satiety / 77), seconds_per_tick))
                    await human.set_jitter_if_lower(10 * SECONDS);
                hunger_rate = 3 * HUNGER_FACTOR;
            }
            hunger_rate *= hunger_modifier * Dm.num(GET_PHYSIOLOGY(human, PHYS_COEFF_HUNGER_MOD));
            await human.adjust_nutrition(-hunger_rate * Dm.num(seconds_per_tick));
        }

        float nutrition = human.nutrition;
        if (nutrition > NUTRITION_LEVEL_FULL && !HAS_TRAIT(human, TRAIT_NOFAT))
        {
            if (human.overeatduration < 20 * MINUTES) //capped so people don't take forever to unfat
                human.overeatduration = min(human.overeatduration + (1 * SECONDS * Dm.num(seconds_per_tick)), 20 * MINUTES);
        }
        else
        {
            if (human.overeatduration > 0)
                human.overeatduration = max(human.overeatduration - (2 * SECONDS * Dm.num(seconds_per_tick)), 0); //doubled the unfat rate
        }

        //metabolism change
        if (nutrition > NUTRITION_LEVEL_FAT)
            human.metabolism_efficiency = 1;
        else if (nutrition > NUTRITION_LEVEL_FED && human.satiety > 80)
        {
            if (human.metabolism_efficiency != 1.25f)
            {
                to_chat(human, span_notice(I18n.T("You feel vigorous.")));
                human.metabolism_efficiency = 1.25f;
            }
        }
        else if (nutrition < NUTRITION_LEVEL_STARVING + 50)
        {
            if (human.metabolism_efficiency != 0.8f)
                to_chat(human, span_notice(I18n.T("You feel sluggish.")));
            human.metabolism_efficiency = 0.8f;
        }
        else
        {
            if (human.metabolism_efficiency == 1.25f)
                to_chat(human, span_notice(I18n.T("You no longer feel vigorous.")));
            human.metabolism_efficiency = 1;
        }
    }

    ///This gets called after the owner takes a bite of food
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:201
    public virtual async DmTask after_eat(atom edible = null)
    {
        return;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:204
    public virtual async DmTask<object> consume_thing(atom_movable thing = null)
    {
        RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(content_moved));
        RegisterSignal(thing, COMSIG_QDELETING, nameof(content_deleted));
        LAZYADD(ref stomach_contents, thing);
        await thing.forceMove((atom)owner ?? this); // We assert that if we have no owner, we will not be nullspaced
        return TRUE;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:211
    public virtual void content_deleted(atom_movable source = null)
    {
        // SIGNAL_HANDLER
        LAZYREMOVE(ref stomach_contents, source);
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:215
    public virtual void content_moved(atom_movable source = null)
    {
        // SIGNAL_HANDLER
        if (source.loc == this || source.loc == owner) // not in us? out da list then
            return;
        LAZYREMOVE(ref stomach_contents, source);
        UnregisterSignal(source, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:222
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (loc == null && owner != null)
        {
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(stomach_contents))
                await thing.forceMove(owner);
        }
        else if (loc != null)
        {
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(stomach_contents))
                await thing.forceMove(this);
        }
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:232
    public virtual async DmTask<object> empty_contents(float? chance = null, float? damaging = null, float? min_amount = null)
    {
        chance ??= 100;
        damaging ??= FALSE;
        min_amount ??= 0;
        float emptied = 0;
        atom_movable drop_as = (atom_movable)owner ?? this;
        object drop_loc = drop_as.drop_location();
        foreach (atom_movable nugget in Dm.each_cast<atom_movable>(stomach_contents))
        {
            float total_chance = chance.Value;
            // If min_amount is set, make sure that we vomit at least some of our contents
            if (Dm.truthy(min_amount))
                total_chance += 100 / (LAZYLEN(stomach_contents) + 1 - min_amount.Value);
            if (!prob(total_chance))
                continue;
            await nugget.forceMove(drop_loc as atom);
            emptied += 1;
            min_amount -= 1;
            if (!Dm.truthy(damaging) || QDELETED(owner))
                continue;
            float damage = 5;
            if (isitem(nugget))
            {
                obj_item as_item = (obj_item)nugget;
                damage = as_item.w_class * 2;
            }
            else if (isliving(nugget))
            {
                mob_living as_living = (mob_living)nugget;
                damage = Dm.num(as_living.mob_size) * 5;
            }
            await owner.apply_damage(damage, BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND, wound_clothing: FALSE);
        }
        return emptied;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:258  (the later of two definitions on this type, rules 7.5)
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await on_life__d0(seconds_per_tick);
        if (owner == null || SSmobs.times_fired % 3 != 0)
            return;

        if (!Dm.truthy(LAZYLEN(stomach_contents)))
            return;

        obj_item_bodypart_chest chest = owner.get_bodypart(zone) as obj_item_bodypart_chest;
        datum_wound_slash_flesh slash = chest.get_wound_type(typeof(datum_wound_slash_flesh)) as datum_wound_slash_flesh;
        // A chance to spill out all the contents
        if (Dm.truthy(cut_open_damage) && Dm.ge(slash?.severity, WOUND_SEVERITY_CRITICAL))
        {
            if (SPT_PROB(Dm.num(chest.get_damage()), Dm.num(seconds_per_tick) * 3))
            {
                float emptied = Dm.num(await empty_contents());
                if (emptied > 0)
                {
                    await owner.apply_damage(emptied * 5, BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND, wound_clothing: FALSE);
                    playsound(get_turf(this), Dm.resource("sound/effects/splat.ogg"), 50); // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:276
                    owner.visible_message(span_danger(I18n.T("Contents of []'s intestines spill out from a huge cut in [] []!", owner, owner.p_their(), chest)),
                        span_userdanger(I18n.T("Contents of your intestines spill out from a huge cut in your []!", chest)));
                }
                return;
            }
        }

        // Digest the stuff in our stomach, just a bit
        foreach (atom_movable thing in Dm.each_cast<atom_movable>(stomach_contents))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(thing._listen_lookup?[COMSIG_ATOM_STOMACH_DIGESTED]) ? NONE : thing._SendSignal(COMSIG_ATOM_STOMACH_DIGESTED, Dm.list(thing, this, owner, seconds_per_tick)), COMPONENT_CANCEL_DIGESTION)))
                continue;
            object acid_pwr = stomach_acid_power(thing, seconds_per_tick);
            if (Dm.truthy(acid_pwr))
                Waiting.NoSleep(thing.acid_act(acid_pwr, 10));

            // If you have strong stomach you can eat glass, literally
            if (!isitem(thing) || HAS_TRAIT(owner, TRAIT_STRONG_STOMACH))
                continue;

            obj_item as_item = (obj_item)thing;
            // If your stomach is cut open, it will hurt like hell
            if (Dm.truthy(cut_open_damage))
            {
                if (chest != null && chest.cavity_item == null && as_item.w_class <= WEIGHT_CLASS_NORMAL)
                {
                    // Oopsie!
                    chest.cavity_item = as_item;
                    LAZYREMOVE(ref stomach_contents, as_item);
                    continue;
                }

                await owner.apply_damage(as_item.w_class * (Dm.truthy(as_item.sharpness) ? 2 : 1), BRUTE, BODY_ZONE_CHEST, wound_bonus: CANT_WOUND,
                    sharpness: as_item.sharpness, attacking_item: as_item, wound_clothing: FALSE);
            }

            if (!Dm.truthy(as_item.sharpness))
                continue;

            object was_failing = Dm.bitand(organ_flags, ORGAN_FAILING);
            await apply_organ_damage(as_item.w_class);
            // Damage caused organ failure
            if (Dm.truthy(was_failing) || !Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
                continue;

            cut_open_damage = maxHealth * 0.5f;
            if (HAS_TRAIT(owner, TRAIT_ANALGESIA))
                continue;

            owner.visible_message(span_warning(I18n.T("[] doubles over in pain!", owner)), span_userdanger(I18n.T("You feel a sharp, searing sensation in your stomach!")));
            await owner.Paralyze(1 * SECONDS);
            await owner.adjust_eye_blur(5 * SECONDS);
        }
    }

    /// Acid power of whatever we're digesting
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:321
    public virtual object stomach_acid_power(atom_movable nomnom = null, object seconds_per_tick = null)
    {
        if (isliving(nomnom)) // NO VORE ALLOWED
            return 0;
        // Yeah maybe don't, if something edible ended up here it should either handle itself or not be digested
        if (Dm.truthy(nomnom.GetComponent(typeof(datum_component_edible)))) // IS_EDIBLE(nomnom), __DEFINES/is_helpers.dm
            return 0;
        if (HAS_TRAIT(owner, TRAIT_STRONG_STOMACH))
            return 10;
        return 0;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:331
    public virtual async DmTask handle_disgust(mob_living_carbon disgusted = null, object seconds_per_tick = null)
    {
        float old_disgust = disgusted.old_disgust;
        float disgust = disgusted.disgust;

        if (Dm.truthy(disgust))
        {
            float pukeprob = 2.5f + (0.025f * disgust);
            if (disgust >= DISGUST_LEVEL_GROSS)
            {
                if (SPT_PROB(5, seconds_per_tick))
                {
                    await disgusted.adjust_stutter(2 * SECONDS);
                    await disgusted.adjust_confusion(2 * SECONDS);
                }
                if (SPT_PROB(5, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(disgusted))
                    to_chat(disgusted, span_warning(I18n.T("You feel kind of iffy...")));
                await disgusted.adjust_jitter(-6 * SECONDS);
            }
            if (disgust >= DISGUST_LEVEL_VERYGROSS)
            {
                if (SPT_PROB(pukeprob, seconds_per_tick)) //iT hAndLeS mOrE ThaN PukInG
                {
                    await disgusted.adjust_confusion(2.5f * SECONDS);
                    await disgusted.adjust_stutter(2 * SECONDS);
                    await disgusted.vomit(VOMIT_CATEGORY_KNOCKDOWN, distance: 0);
                    disgusted.adjust_disgust(-50);
                }
                await disgusted.set_dizzy_if_lower(10 * SECONDS);
            }
            if (disgust >= DISGUST_LEVEL_DISGUSTED)
            {
                if (SPT_PROB(13, seconds_per_tick))
                    await disgusted.set_eye_blur_if_lower(6 * SECONDS); //We need to add more shit down here
            }

            disgusted.adjust_disgust(-0.25f * disgust_metabolism * Dm.num(seconds_per_tick));
        }

        // I would consider breaking this up into steps matching the disgust levels
        // But disgust is used so rarely it wouldn't save a significant amount of time, and it makes the code just way worse
        // We're in the same state as the last time we processed, so don't bother
        if (old_disgust == disgust)
            return;

        disgusted.old_disgust = disgust;
        // switch(disgust): the first matching range runs
        if (disgust >= 0 && disgust <= DISGUST_LEVEL_GROSS)
        {
            disgusted.clear_alert(ALERT_DISGUST);
            disgusted.clear_mood_event("disgust");
        }
        else if (disgust >= DISGUST_LEVEL_GROSS && disgust <= DISGUST_LEVEL_VERYGROSS)
        {
            disgusted.throw_alert(ALERT_DISGUST, typeof(atom_movable_screen_alert_gross));
            await disgusted.add_mood_event("disgust", typeof(datum_mood_event_gross));
        }
        else if (disgust >= DISGUST_LEVEL_VERYGROSS && disgust <= DISGUST_LEVEL_DISGUSTED)
        {
            disgusted.throw_alert(ALERT_DISGUST, typeof(atom_movable_screen_alert_verygross));
            await disgusted.add_mood_event("disgust", typeof(datum_mood_event_verygross));
        }
        else if (disgust >= DISGUST_LEVEL_DISGUSTED)
        {
            disgusted.throw_alert(ALERT_DISGUST, typeof(atom_movable_screen_alert_disgusted));
            await disgusted.add_mood_event("disgust", typeof(datum_mood_event_disgusted));
        }
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:405
    public virtual void on_punched(datum source = null, mob_living_carbon_human attacker = null, object damage = null, object attack_type = null, object atk_effect = null, obj_item_bodypart affecting = null, object final_armor_block = null, object limb_sharpness = null)
    {
        if (!Dm.truthy(LAZYLEN(stomach_contents)) || Dm.num(damage) < 9 || Dm.truthy(final_armor_block) || Dm.eq(atk_effect, ATTACK_EFFECT_KICK))
            return;
        if (Dm.truthy(Waiting.NoSleep(owner.vomit(Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_FORCE)))))
            // Since we vomited with a force flag, we should've vomited out at least one item
            owner.visible_message(span_danger(I18n.T("[] doubles over from []'s punch, vomiting out the contents of [] stomach!", owner, attacker, owner.p_their())));
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:414
    public virtual void on_vomit(mob_living_carbon vomiter = null, object distance = null, object force = null)
    {
        // If we're forced to vomit, try to spew out at least one item
        Waiting.NoSleep(empty_contents(chance: 60, damaging: TRUE, min_amount: Dm.truthy(force) ? 1 : 0));
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:197 (interleaved types, rules 1.9)
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:197
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutantstomach;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:377 (interleaved types, rules 1.9)
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:377
    [DmParams("receiver", "special", "movement_flags")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon receiver = organ_owner;
        object dot = null;
        dot = await base.on_mob_insert(receiver, special, movement_flags);
        if (receiver.hud_used?.screen_objects?[HUD_MOB_HUNGER] is atom_movable_screen_hunger hunger_bar)
            hunger_bar.update_hunger_bar();
        RegisterSignal(receiver, COMSIG_CARBON_VOMITED, nameof(on_vomit));
        RegisterSignal(receiver, COMSIG_HUMAN_GOT_PUNCHED, nameof(on_punched));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:385
    [DmParams("stomach_owner", "special", "movement_flags")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon stomach_owner = organ_owner as mob_living_carbon;
        if (ishuman(stomach_owner))
        {
            mob_living_carbon_human human_owner = (mob_living_carbon_human)stomach_owner;
            human_owner.clear_alert(ALERT_DISGUST);
            human_owner.clear_mood_event("disgust");
        }
        if (stomach_owner.hud_used?.screen_objects?[HUD_MOB_HUNGER] is atom_movable_screen_hunger hunger_bar)
            hunger_bar.update_hunger_bar();
        UnregisterSignal(stomach_owner, Dm.list(COMSIG_CARBON_VOMITED, COMSIG_HUMAN_GOT_PUNCHED));
        await base.on_mob_remove(stomach_owner, special, movement_flags);
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:397 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:397
    public override object feel_for_damage(object self_aware = null)
    {
        if (Dm.num(damage) < low_threshold)
            return "";
        if (Dm.num(damage) < high_threshold)
            return span_warning(I18n.T("Your stomach hurts."));
        return span_boldwarning(I18n.T("Your stomach cramps in pain!"));
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:460
    public override async DmTask<object> apply_organ_damage(object damage_amount = null, object maximum = null, float? required_organ_flag = null)
    {
        object dot = await base.apply_organ_damage(damage_amount, maximum, required_organ_flag);
        // So after a while, or a bunch of stomach meds, even a cut stomach can recover
        if (Dm.num(dot) > 0)
            cut_open_damage = max(0, Dm.num(cut_open_damage) - Dm.num(dot));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:466
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(cut_open_damage))
            Dm.append(ref dot, span_danger(I18n.T("It has a sizeable cut in it, exposing its insides!")));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:494  (silicons SI3: the exosuit fabricator prints it)
public partial class obj_item_organ_stomach_cybernetic : obj_item_organ_stomach
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:505
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        // COOLDOWN_FINISHED(src, severe_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(Dm.num(severe_cooldown) <= world.time)) //So we cant just spam emp to kill people.
        {
            if (prob(100 / Dm.num(severity)))
                await owner.vomit(vomit_flags: Dm.bitor(MOB_VOMIT_MESSAGE, MOB_VOMIT_HARM));
            severe_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START(src, severe_cooldown, 10 SECONDS)
        }
        if (prob(emp_vulnerability / Dm.num(severity))) //Chance of permanent effects
            organ_flags = Dm.bitor(organ_flags, ORGAN_EMP); //Starts organ faliure - gonna need replacing soon.
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:419 (gaps OG1: cutting a stomach open and mending it)
public partial class obj_item_organ_stomach : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:419
    public override async DmTask<object> tool_act(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.eq(tool.tool_behaviour, TOOL_SCALPEL))
        {
            if (cut_open_damage > 0)
            {
                balloon_alert(user, I18n.T("already cut open!"));
                return ITEM_INTERACT_FAILURE;
            }

            balloon_alert(user, I18n.T("cutting open..."));
            playsound(user, Dm.resource("sound/items/handling/surgery/scalpel1.ogg"), 75);
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            {
                balloon_alert(user, I18n.T("interrupted!"));
                await apply_organ_damage(tool.force);
                return ITEM_INTERACT_FAILURE;
            }

            playsound(user, Dm.resource("sound/items/handling/surgery/scalpel2.ogg"), 75);
            float emptied = Dm.num(await empty_contents());
            if (emptied > 0)
                playsound(get_turf(this), Dm.resource("sound/effects/splat.ogg"), 50);
            user.visible_message(span_warning(I18n.T("[] cuts [] open[]", user, this, emptied != 0 ? "!" : I18n.T(", but it's empty."))), span_notice(I18n.T("You cut [] open[]", this, emptied != 0 ? "." : I18n.T(", but there's nothing inside."))));
            cut_open_damage += Dm.num(await apply_organ_damage(maxHealth * 0.5f));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.eq(tool.tool_behaviour, TOOL_CAUTERY))
            return await base.tool_act(user, tool, modifiers);

        if (cut_open_damage <= 0)
        {
            balloon_alert(user, I18n.T("fully intact!"));
            return ITEM_INTERACT_FAILURE;
        }

        playsound(user, Dm.resource("sound/items/handling/surgery/cautery1.ogg"), 75);
        balloon_alert(user, I18n.T("mending the incision..."));
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
        {
            balloon_alert(user, I18n.T("interrupted!"));
            await apply_organ_damage(tool.force);
            return ITEM_INTERACT_FAILURE;
        }

        playsound(user, Dm.resource("sound/items/handling/surgery/cautery2.ogg"), 75);
        balloon_alert(user, I18n.T("incision mended"));
        await apply_organ_damage(-cut_open_damage);
        cut_open_damage = 0; // Just in case
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:516 (gaps OG1)
public partial class obj_item_organ_stomach_cybernetic_tier2 : obj_item_organ_stomach_cybernetic
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:525
    [DmParams("nomnom")]
    public override object stomach_acid_power(atom_movable nomnom = null, object seconds_per_tick = null)
    {
        if (isliving(nomnom))
            return 0;
        if (IS_EDIBLE(nomnom))
            return 0;
        return 20;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:532 (gaps OG1)
public partial class obj_item_organ_stomach_cybernetic_tier3 : obj_item_organ_stomach_cybernetic
{
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:542
    [DmParams("nomnom")]
    public override object stomach_acid_power(atom_movable nomnom = null, object seconds_per_tick = null)
    {
        if (isliving(nomnom))
            return 0;
        if (IS_EDIBLE(nomnom))
            return 0;
        return 35;
    }
}

// tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:549 (gaps OG1)
public partial class obj_item_organ_stomach_cybernetic_surplus : obj_item_organ_stomach_cybernetic
{
    //surplus organs are so awful that they explode when removed, unless failing
    // tg: code/modules/surgery/organs/internal/stomach/_stomach.dm:560
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dangerous_organ_removal), /*surgical = */ TRUE));
        return dot;
    }
}
