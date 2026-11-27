// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/types/rift.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish of the rifts (the chrystarfish that embeds and teleports, the biting dolphish, the flumpulus, the
// gullion, the mossglob, and the babbelfish whose wail kills fish nearby and whose halves are the babbel ears).
// The var blocks are generated (D111).

// tg: code/modules/fishing/fish/types/rift.dm:1
public partial class obj_item_fish_starfish_chrystarfish : obj_item_fish_starfish
{
    // tg: code/modules/fishing/fish/types/rift.dm:63
    public override async DmTask set_status(object new_status = null, float? silent = null)
    {
        await base.set_status(new_status, silent);
        if (Dm.eq(new_status, FISH_DEAD))
        {
            if (fillet_type != null)
                Dm.@new(fillet_type, get_turf(this));
            playsound(this, SFX_SHATTER, 50);
            qdel(this);
        }
    }

    // tg: code/modules/fishing/fish/types/rift.dm:71
    public override object add_emissive()
    {
        return null;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:74
    public override object get_base_edible_reagents_to_add()
    {
        DmList return_list = base.get_base_edible_reagents_to_add() as DmList;
        return_list[typeof(datum_reagent_bluespace)] = 5;
        return return_list;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:79
    public override async DmTask flinch_on_eat(mob_living eater = null, mob_living feeder = null)
    {
        if (status != FISH_ALIVE)
            return;
        to_chat(feeder, span_warning(I18n.T("[] slips out of the spacetime in pain!", this)));

        float tp_range = 6 * clamp(Dm.num(weight) / average_weight, 3, 9); // usually 6, plus or minus fish weight
        // teleports itself if on a turf otherwise its container - whatever it is
        await do_teleport(isturf(loc) ? this : loc as atom_movable, get_turf(feeder), tp_range, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:88
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like they're trying to commit suicide!", user, this)));
        await forceMove(user);
        // *everything*
        foreach (obj thing in Dm.each<obj>(user.get_contents()))
        {
            if (QDELETED(thing) || thing is obj_item_bodypart_chest)
                continue; // don't want a gib
            await stoplag(0.1f * SECONDS);
            playsound(this, Dm.resource("sound/effects/phasein.ogg"), 15, TRUE);
            await do_teleport(thing, get_turf(user), 2, asoundin: null, channel: TELEPORT_CHANNEL_BLUESPACE);
        }
        qdel(this);
        return MANUAL_SUICIDE;
    }
}

// Basically a ninja star that's highly likely to embed and teleports you around if you don't stop to remove it. However it doesn't deal that much damage!
// tg: code/modules/fishing/fish/types/rift.dm:47
public partial class datum_embedding_chrystarfish : datum_embedding
{
    // tg: code/modules/fishing/fish/types/rift.dm:59
    public override async DmTask jostle_effects()
    {
        await do_teleport(owner, get_turf(owner), 3, asoundin: Dm.resource("sound/effects/phasein.ogg"), channel: TELEPORT_CHANNEL_BLUESPACE);
        owner.visible_message(span_danger(I18n.T("[] teleports as [] jostles inside of []!", owner, parent, owner.p_them())));
    }
}

// tg: code/modules/fishing/fish/types/rift.dm:105
public partial class obj_item_fish_dolphish : obj_item_fish
{
    // Prevents the first 2 messages from spamming on each patience update.
    // tg: code/modules/fishing/fish/types/rift.dm:102-103  #define PATIENCE_FLINCH, PATIENCE_UNCOMFY (file-local)
    private const string PATIENCE_FLINCH = "PATIENCE_FLINCH";
    private const string PATIENCE_UNCOMFY = "PATIENCE_UNCOMFY";

    // tg: code/modules/fishing/fish/types/rift.dm:168
    public override void get_force_rank()
    {
        float multiplier = 1;
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                multiplier = 0.35f;
                break;
            case WEIGHT_CLASS_SMALL:
                multiplier = 0.5f;
                break;
            case WEIGHT_CLASS_NORMAL:
                multiplier = 0.75f;
                break;
            // bulky is avergae
            case WEIGHT_CLASS_HUGE:
                multiplier = 1.15f;
                break;
            case WEIGHT_CLASS_GIGANTIC:
                multiplier = 1.35f;
                break;
        }

        if (status == FISH_DEAD)
            multiplier -= 0.35f; // huge nerf if dead

        force *= multiplier;
        attack_speed *= multiplier;
        demolition_mod *= multiplier;
        block_chance *= multiplier;
        armour_penetration *= multiplier;
        wound_bonus *= multiplier;
        exposed_wound_bonus *= multiplier;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:194
    public override async DmTask do_fish_process(object seconds_per_tick = null)
    {
        await base.do_fish_process(seconds_per_tick);
        if (QDELETED(this))
            return;

        float patience_reduction = 1;

        turf onturf = get_turf(loc) as turf;
        //gas check
        datum_gas_mixture turf_gasmix = await onturf.return_air() as datum_gas_mixture;
        // likes water, gets sleepy, gets very sleepy
        if (Dm.num(turf_gasmix.moles[typeof(datum_gas_water_vapor)]) >= 5)
            patience_reduction *= 0.5f;
        if (Dm.num(turf_gasmix.moles[typeof(datum_gas_nitrous_oxide)]) >= 5)
            patience_reduction *= 0.25f;
        if (Dm.num(turf_gasmix.moles[typeof(datum_gas_healium)]) >= 5)
            patience_reduction *= 0.1f;

        if (!ismob(loc))
        {
            // dividing by the multiplier nets us an increasing value. happy dolphish gain patience quicker
            float patience_bonus = (1 / patience_reduction);
            patience = clamp(patience + patience_bonus * Dm.num(seconds_per_tick), 0, max_patience);
            return;
        }

        mob_living moc = (mob_living)loc;
        if (Dm.truthy(Dm.bitand(moc.mob_biotypes, MOB_AQUATIC)))
            patience_reduction *= 0.6f;
        if (HAS_TRAIT(moc, TRAIT_IS_WET))
            patience_reduction *= 0.6f;

        patience = clamp(patience - (patience_reduction * Dm.num(seconds_per_tick)), 0, max_patience);

        // DM's switch ranges: the first range that holds the value wins.
        if (patience == 0)
        {
            // No check, we always want sharky to bite jerky on 0
            moc.visible_message(span_bolddanger(I18n.T("[] bites directly into [] and squirms away from [] grasp!", this, moc, moc.p_their())), span_userdanger(I18n.T("[] sinks its fangs into you!!", this)));
            await moc.apply_damage(force, BRUTE, moc.get_active_hand(), wound_bonus: wound_bonus, exposed_wound_bonus: exposed_wound_bonus, sharpness: sharpness, attacking_item: this);
            await forceMove(moc.drop_location() as atom);
            moc.painful_scream();
            patience = max_patience;
            playsound(this, hitsound, 45);
        }
        else if (patience >= 1 && patience <= 10)
        {
            // No check, final warning as they struggle, also funny.
            visible_message(span_bolddanger(I18n.T("[] thrashes against []'s grasp!", this, moc)));
            moc.shake_up_animation();
        }
        else if (patience >= 10 && patience <= 15)
        {
            if (Dm.eq(last_effect, PATIENCE_FLINCH))
                return;
            visible_message(span_danger(I18n.T("[] flinches away from []!", this, moc)));
            moc.shake_up_animation();
            last_effect = PATIENCE_FLINCH;
        }
        else if (patience >= 15 && patience <= 20)
        {
            if (Dm.eq(last_effect, PATIENCE_UNCOMFY))
                return;
            visible_message(span_notice(I18n.T("[] seems uncomfortable in []'s grasp.", this, moc)));
            last_effect = PATIENCE_UNCOMFY;
        }
    }

    // tg: code/modules/fishing/fish/types/rift.dm:256
    public override async DmTask<object> pet_fish(mob_living user = null, object in_aquarium = null)
    {
        user.visible_message(
            span_warning(I18n.T("[] tries to pet [], but it sinks its fangs into [] hand!", user, this, user.p_their())),
            span_warning(I18n.T("You try to pet [], but it sinks its fangs into your hand!", this)),
            vision_distance: DEFAULT_MESSAGE_RANGE - 3);
        await user.apply_damage(force, BRUTE, user.get_active_hand(), wound_bonus: wound_bonus, exposed_wound_bonus: exposed_wound_bonus, sharpness: sharpness, attacking_item: this);
        if (!Dm.truthy(in_aquarium))
            await forceMove(user.drop_location() as atom);
        user.painful_scream();
        return null;
    }
}

// tg: code/modules/fishing/fish/types/rift.dm:267
public partial class obj_item_fish_flumpulus : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/rift.dm:307
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like they're trying to commit suicide!", user, this)));
        await forceMove(user);
        object dot = MANUAL_SUICIDE;
        float times = rand(5, 15);
        for (int i = 1; i <= times; i++)
        {
            _addtimer(Dm.@new<datum_callback>(this, nameof(flump_attack), user), 0.4f * SECONDS * i, file: "code/modules/fishing/fish/types/rift.dm", line: 312);
            await user.death();
        }
        return dot;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:315
    public virtual async DmTask flump_attack(mob_living user = null)
    {
        obj_item_organ_eyes eyes = user.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
        Type new_eyes_type = pick(Dm.list(typeof(obj_item_organ_eyes_snail), typeof(obj_item_organ_eyes_night_vision_mushroom))) as Type;
        obj_item_organ_eyes new_eyes = await Dm.new_async(new_eyes_type, user) as obj_item_organ_eyes;
        await new_eyes.Insert(user as mob_living_carbon);
        playsound(user, Dm.resource("sound/effects/cartoon_sfx/cartoon_pop.ogg"), 50, TRUE);
        user.visible_message(I18n.T("[]'s [] suddenly sprout stalks and turn into []!", user, eyes != null ? eyes : I18n.T("eye holes"), new_eyes));
        spawn(-1, async () =>
        {
            await user.emote("scream");
            await eyes.throw_at(get_edge_target_turf(user, pick(GLOB.alldirs)) as atom, rand(1, 10), rand(1, 10));
            await sleep(5 * SECONDS);
            if (!QDELETED(eyes))
            {
                eyes.visible_message(span_danger(I18n.T("[] rapidly turn to dust.", eyes)));
                await eyes.dust();
            }
        });
    }

    // tg: code/modules/fishing/fish/types/rift.dm:330
    public override object get_base_edible_reagents_to_add()
    {
        DmList return_list = base.get_base_edible_reagents_to_add() as DmList;
        //return_list[/datum/reagent/flumpulus_extract] = 10
        return_list[typeof(datum_reagent_medicine_oculine_flumpuline)] = 10;
        return return_list;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:336
    public override async DmTask<object> intercept_zImpact(DmList falling_movables = null, float? levels = null)
    {
        object dot = await base.intercept_zImpact(falling_movables, levels);
        if (status == FISH_DEAD)
            return dot;

        foreach (mob_living fallen_mob in Dm.each<mob_living>(falling_movables))
        {
            visible_message(span_danger(I18n.T("[] flattens like a pancake as [] lands on top of it!", this, fallen_mob)));
            await damage_fish(max_integrity * integrity_failure * 0.9f); // very "durable"
            await AddElement(typeof(datum_element_squish), 15 * SECONDS);
            await fallen_mob.Paralyze(0.5f * SECONDS);
            playsound(this, Dm.resource("sound/effects/cartoon_sfx/cartoon_splat.ogg"), 75);
        }

        return Dm.bitor(FALL_INTERCEPTED, FALL_NO_MESSAGE);
    }
}

// tg: code/modules/fishing/fish/types/rift.dm:350
public partial class obj_item_fish_gullion : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/rift.dm:395
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        visible_message(span_suicide(I18n.T("[] swallows [] whole! It looks like they're trying to commit suicide!", user, this)));
        await forceMove(user);
        datum_gas_mixture environment = await user.loc.return_air() as datum_gas_mixture;
        object oxygen_in_air = Dm.locate_type(typeof(datum_gas_oxygen), environment.moles);
        if (!Dm.truthy(oxygen_in_air) || (status == FISH_DEAD))
        {
            visible_message(span_suicide(I18n.T("[] chokes and dies! (Wait, from the fish or from lack of air?)", user)));
            return OXYLOSS;
        }

        await user.petrify(statue_timer: INFINITY);
        await user.death();
        visible_message(span_suicide(I18n.T("[]'s skin turns into quartz upon contact with the oxygen in the air!'", user)));
        qdel(this);
        return MANUAL_SUICIDE;
    }
}

// The (other) reason you DON'T fish in rifts.
// This thing is a plague - throws itself around and envenoms those it hits.
// Worse, it's immune to the environment and revives either way.
// tg: code/modules/fishing/fish/types/rift.dm:413
public partial class obj_item_fish_mossglob : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/rift.dm:460
    [DmParams("mapload", "apply_qualities")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_haunted), COLOR_GREEN));
        return dot;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:464
    public override async DmTask set_status(object new_status = null, float? silent = null)
    {
        await base.set_status(new_status, silent);
        if (Dm.eq(new_status, FISH_DEAD))
            RemoveElement(typeof(datum_element_haunted), COLOR_GREEN);
        else if (Dm.eq(new_status, FISH_ALIVE))
            await AddElement(typeof(datum_element_haunted), COLOR_GREEN);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:471
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        visible_message(span_suicide(I18n.T("[] sticks [] arm deep into []! It looks like they're trying to offer themselves to it!", user, user.p_their(), this)));
        await user.drop_everything();
        await set_status(FISH_ALIVE);
        transform = (transform as matrix).Scale(1.15f, 1.15f);
        await update_size_and_weight(new_size: Dm.num(size) * 1.15f, new_weight: Dm.num(weight) * 1.15f);
        visible_message(span_suicide(I18n.T("[] is absorbed into []!", user, this)));
        await objectify(user, this);
        return MANUAL_SUICIDE_NONLETHAL;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:481
    public override void get_force_rank()
    {
        float multiplier = 1;
        switch (w_class)
        {
            case WEIGHT_CLASS_TINY:
                multiplier = 0.35f;
                break;
            case WEIGHT_CLASS_SMALL:
                multiplier = 0.5f;
                break;
            case WEIGHT_CLASS_NORMAL:
                multiplier = 0.75f;
                break;
            case WEIGHT_CLASS_BULKY:
                multiplier = 0.85f;
                break;
            // huge is avergae
            case WEIGHT_CLASS_GIGANTIC:
                multiplier = 1.15f;
                break;
        }

        if (status == FISH_DEAD)
            multiplier -= 0.35f; // huge nerf if dead

        force *= multiplier;
        attack_speed *= multiplier;
        demolition_mod *= multiplier;
        block_chance *= multiplier;
        armour_penetration *= multiplier;
    }
}

// Babbelfish are psychic 'predators' that don't physically attack their prey, but emit a psychic aura that kills them, eating their corpses.
// When they die they emit a horrendous wail that deafens and debilitates people nearby - let alone fish.
// tg: code/modules/fishing/fish/types/rift.dm:507
public partial class obj_item_fish_babbelfish : obj_item_fish
{
    // tg: code/modules/fishing/fish/types/rift.dm:545
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        // DEVIATION(DV675): tg adds here a long excerpt from a third-party novel (a dialogue about babel fish) as the closer
        // look's text. The excerpt is not tg's own writing, so the port does not carry it; the closer look shows the rest.
        return dot;
    }

    /**
     * In the suicide:
     * - If the fish is dead:
     *  - If someone is already inside, cancel the suicide.
     * Drop the suicider's items and shove them inside the object. They are now the fish.
     *
     * - If the fish is alive:
     * the idiot uses the babbelfish as a vuvuzela and becomes a god for half a second before their brain is imploded.
     */
    // tg: code/modules/fishing/fish/types/rift.dm:568
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (status == FISH_DEAD)
        {
            if (moron_inside != null)
            {
                visible_message(span_suicide(I18n.T("[] puts [] against their lips, but [] is already full!", user, this, this)));
                return SHAME;
            }
            visible_message(span_suicide(I18n.T("[] puts [] against their lips, but []'s psychic afterimage sucks [] inward!", user, this, this, user.p_them())));
            await user.drop_everything();
            await objectify(user, this);
            await user.fully_replace_character_name(null, name); // fish's name
            await set_status(FISH_ALIVE); // RIIIIIISE!!!!!
            moron_inside = user;
            RegisterSignal(this, COMSIG_FISH_STATUS_CHANGED, nameof(fishes_die_twice));
            RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(check_loc));
            return MANUAL_SUICIDE_NONLETHAL; // in case they somehow break out
        }

        visible_message(span_suicide(I18n.T("[] puts [] against their lips! It looks like they're preparing to say something!", user, this)));
        object psychic_speech = await tgui_input_text(user, message: I18n.T("Say something!"), title: I18n.T("What are your last words?"), timeout: 15 * SECONDS);
        if (!Dm.truthy(psychic_speech) || !Dm.@in(this, user.get_contents()))
        {
            await user.say("Err, umm... uhh... erm...", forced: "blustering like a moron due to babbelfish suicide");
            visible_message(span_suicide(I18n.T("[] dies from shame!", user)));
            return OXYLOSS;
        }

        await voice_of_god(psychic_speech, user, Dm.list("big", "alertalien"), base_multiplier: 5, include_speaker: TRUE, forced: TRUE, ignore_spam: TRUE);
        await psy_wail();
        await user.adjust_organ_loss(ORGAN_SLOT_BRAIN, INFINITY, INFINITY, ORGAN_SLOT_BRAIN);
        await user.death();
        return MANUAL_SUICIDE;
    }

    /**
     * When the fish dies you die in real life.
     * Consequently, when the fish is magically resuscitated the bound mob is revived as well.
     */
    // tg: code/modules/fishing/fish/types/rift.dm:600
    public virtual async DmTask fishes_die_twice()
    {
        if (moron_inside == null)
            UnregisterSignal(this, COMSIG_FISH_STATUS_CHANGED);
        if (status == FISH_DEAD)
            await moron_inside.death();
        if (status == FISH_ALIVE)
            await moron_inside.revive(HEAL_ALL);
    }

    /**
     * If they somehow escaped null the variable.
     */
    // tg: code/modules/fishing/fish/types/rift.dm:611
    public virtual void check_loc()
    {
        if (Dm.@in(moron_inside, contents))
            return;
        UnregisterSignal(moron_inside, COMSIG_MOVABLE_MOVED);
        moron_inside = null;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:617
    public override async DmTask set_status(object new_status = null, float? silent = null)
    {
        await base.set_status(new_status, silent);
        // Death message plays here
        if (!Dm.eq(new_status, FISH_DEAD))
        {
            damtype = BRAIN;
            return;
        }
        await psy_wail();
        damtype = BRUTE;
    }

    /**
     * When a babbelfish dies, it will let out a wail that kills any fish nearby, alongside severely incapacitating anyone else around.
     * This is punishment for neglecting your catches.
     */
    // tg: code/modules/fishing/fish/types/rift.dm:630
    public virtual async DmTask psy_wail()
    {
        manual_emote("wails!");
        playsound(this, Dm.resource("sound/mobs/non-humanoids/fish/fish_psyblast.ogg"), 100);
        DmList mobs_in_range = get_hearers_in_range(7, this) as DmList;
        foreach (mob_living screeched in Dm.each<mob_living>(mobs_in_range))
        {
            if (Dm.truthy(screeched.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 1)))
            {
                to_chat(screeched, span_notice(I18n.T("You resist the psychic wail!")));
                continue;
            }
            float power = 1;
            if (HAS_TRAIT(screeched, TRAIT_DEAF)) // bit weaker if deaf. but its still psychic
                power *= 0.5f;
            float affect_time = 15 * SECONDS * power;
            // it really fucks you up
            await screeched.Knockdown(affect_time * 0.1f);
            screeched.adjust_disgust(affect_time);
            await screeched.adjust_stutter(affect_time);
            await screeched.adjust_slurring(affect_time);
            await screeched.adjust_dizzy(affect_time);
            await screeched.adjust_staggered(affect_time);
            await screeched.adjust_jitter(affect_time);
            await screeched.adjust_confusion(affect_time);
            await screeched.adjust_hallucinations(affect_time);
            await screeched.adjust_eye_blur(affect_time);
            if (iscarbon(screeched))
            {
                mob_living_carbon carbon_screeched = (mob_living_carbon)screeched;
                await carbon_screeched.vomit(MOB_VOMIT_MESSAGE);
                await carbon_screeched.adjust_organ_loss(ORGAN_SLOT_BRAIN, 50);
            }
        }

        float affected = 0;
        foreach (obj_item_fish fishie in Dm.each<obj_item_fish>(range(7, this)))
        {
            if (HAS_TRAIT(fishie, TRAIT_RESIST_PSYCHIC))
                continue;
            if (fishie.status == FISH_DEAD)
                continue;
            await fishie.set_status(FISH_DEAD);
            affected++;
        }
        if (affected != 0)
            visible_message(span_bolddanger(I18n.T("[]'s wail kills [] fish nearby!", this, affected))); // m-m-m-m-m-MONSTER KILL
    }

    // tg: code/modules/fishing/fish/types/rift.dm:669
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (!Dm.@in(this, user.contents))
            return await base.attack_hand(user, modifiers, __extra);

        if ((living_user.usable_hands < 2) && !HAS_TRAIT(user, TRAIT_STRENGTH))
        {
            to_chat(user, span_notice(I18n.T("[] is too dense to twist apart with only one hand.", this)));
            return null;
        }

        to_chat(user, span_danger(I18n.T("You start pulling and twisting [], trying to split it down the middle...", this)));
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return null;

        playsound(get_turf(user), Dm.resource("sound/effects/wounds/crack1.ogg"), 60);
        await set_status(FISH_DEAD);
        obj_item_organ_ears_babbelfish cracked = await Dm.new_async<obj_item_organ_ears_babbelfish>(user);
        await user.put_in_hands(cracked);
        qdel(this);
        return null;
    }
}

/** These ears grant amazing and supernatural hearing, but they also screw over your knowledge of language.
 * Three outcomes:
 * The user is able to speak all languages, but can't understand any. Even their own words come across as unintelligible gibberish.
 * The user is able to understand all languages, but can't speak any. They can only 'make strange noises'.
 * The user is able to understand and speak all languages. Rare.
 * All this to be a curator, this is silly.
*/
// tg: code/modules/fishing/fish/types/rift.dm:695
public partial class obj_item_organ_ears_babbelfish : obj_item_organ_ears
{
    // tg: code/modules/fishing/fish/types/rift.dm:731
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noticable_organ), I18n.T("%PRONOUN_They %PRONOUN_have weird, deep-rooted obsidian tubes sticking out of where their ears should be.")));
        babbel_overlay = Dm.@new<datum_bodypart_overlay_simple_babbearfish>();
        removal_holder = Dm.@new<datum_language_holder>(this);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:737
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref babbel_overlay);
        QDEL_NULL(ref removal_holder);
        QDEL_NULL(ref bound_component);
        return base.Destroy(force);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:743
    public override async DmTask on_bodypart_insert(obj_item_bodypart limb = null)
    {
        await base.on_bodypart_insert(limb);
        await limb.add_bodypart_overlay(babbel_overlay);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:747
    public override async DmTask on_bodypart_remove(obj_item_bodypart limb = null, object movement_flags = null)
    {
        await base.on_bodypart_remove(limb, movement_flags);
        await limb.remove_bodypart_overlay(babbel_overlay);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:751
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        object dot = await base.attack__proc(target_mob, user, modifiers, attack_modifiers, __extra);
        obj_item_organ_ears ears = target_mob.get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
        if (ears == null)
        {
            to_chat(user, span_notice(I18n.T("[] ears to shove [] into!", target_mob == user ? I18n.T("You don't have") : Dm.add(target_mob, "has no"), this)));
            return dot;
        }

        to_chat(user, span_danger(I18n.T("You start shoving [] into [] ears. Probably a bad idea.", this, target_mob == user ? I18n.T("your") : Dm.add(target_mob, "'s"))));
        if (!Dm.truthy(await do_after(user, 2.5f * SECONDS * (target_mob == user ? 1 : 3), this)))
            return dot;

        await user.apply_damage(25, BRUTE, user.get_bodypart(ears.zone), attacking_item: this);
        to_chat(user, span_notice(I18n.T("As you're shoving them in, the [] take on a life of their own and brutishly crawl right into [] ears, taking their place entirely while maiming []  []!", this, target_mob == user ? I18n.T("your") : Dm.add(target_mob, "'s"), target_mob == user ? I18n.T("your") : target_mob.p_their(), ears.zone)));
        playsound(user, Dm.resource("sound/effects/magic/demon_consume.ogg"), vol: 100, falloff_exponent: 2, vary: TRUE);
        // bad moodlet
        await user.temporarilyRemoveItemFromInventory(this, TRUE);
        await Insert(user as mob_living_carbon, special: TRUE, movement_flags: DELETE_IF_REPLACED);
        return dot;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:769
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_insert(organ_owner, special, movement_flags);
        bound_component = await organ_owner.AddComponent(
            typeof(datum_component_anti_magic),
            Dm.named("antimagic_flags", MAGIC_RESISTANCE_MIND),
            Dm.named("inventory_flags", null),
            Dm.named("charges", maxHealth * 0.1f),
            Dm.named("block_magic", Dm.@new<datum_callback>(this, nameof(on_drain_magic))),
            Dm.named("expiration", Dm.@new<datum_callback>(this, nameof(on_expire))));

        if (HAS_MIND_TRAIT(organ_owner, TRAIT_TOWER_OF_BABEL))
        {
            to_chat(organ_owner, span_noticealien(I18n.T("You don't feel that much different this time. Looks like your brain has attuned to the []'s effect.", this)));
            return null;
        }

        if (removal_holder == null)
            removal_holder = Dm.@new<datum_language_holder>(this);
        removal_holder.copy_languages(organ_owner.get_language_holder() as datum_language_holder, LANGUAGE_BABEL);

        float roll = rand(1, 100);
        if (roll >= 1 && roll <= 45)
        {
            // Can understand nothing
            organ_owner.remove_all_languages(source: LANGUAGE_ALL);
            //but speak everything
            organ_owner.grant_all_languages(language_flags: SPOKEN_LANGUAGE, grant_omnitongue: FALSE, source: LANGUAGE_BABEL);
            to_chat(organ_owner, span_noticealien(I18n.T("You feel like you've been given the first half of a cosmic puzzle!")));
        }
        else if (roll >= 46 && roll <= 90)
        {
            // Can speak nothing
            organ_owner.remove_all_languages(source: LANGUAGE_ALL);
            // but understand everything
            organ_owner.grant_all_languages(language_flags: UNDERSTOOD_LANGUAGE, grant_omnitongue: FALSE, source: LANGUAGE_BABEL);
            to_chat(organ_owner, span_noticealien(I18n.T("You feel like you've been given the second half of a cosmic puzzle!")));
        }
        else if (roll >= 91 && roll <= 100)
        {
            // jackpot!
            organ_owner.grant_all_languages(language_flags: ALL, grant_omnitongue: TRUE, source: LANGUAGE_BABEL);
            to_chat(organ_owner, span_noticealien(I18n.T("You feel like you've been given both halves of a cosmic puzzle!")));
            to_chat(organ_owner, span_boldnicegreen(I18n.T("So <i>that's</i> what they said to you that one time...")));
        }

        if (organ_owner.mind != null)
            ADD_TRAIT(organ_owner.mind, TRAIT_TOWER_OF_BABEL, MAGIC_TRAIT); // only one roll per mind
        return null;
    }

    // tg: code/modules/fishing/fish/types/rift.dm:810
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon carbon_owner = organ_owner as mob_living_carbon;

        // Reset
        carbon_owner.remove_all_languages(source: LANGUAGE_ALL);
        carbon_owner.copy_languages(removal_holder);
        to_chat(carbon_owner, span_notice(I18n.T("You feel significantly more mundane.")));
        QDEL_NULL(ref removal_holder);
        QDEL_NULL(ref bound_component);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:820
    public virtual void on_drain_magic(mob user = null)
    {
        to_chat(user, span_noticealien(I18n.T("Your [] pop as they protect your mind from psychic phenomena!", this)));
        adjust_temporary_deafness(40 * SECONDS);
    }

    // tg: code/modules/fishing/fish/types/rift.dm:824
    public virtual async DmTask on_expire(mob user = null)
    {
        to_chat(user, span_noticealien(I18n.T("Your [] suddenly burst apart!", this)));
        await apply_organ_damage(maxHealth, maxHealth);
    }
}

/**
 * The bodypart overlay for babbearfish.
 * We don't need anything other than this icon (and to hide it sometimes), so it being mutant is unnecessary and a waste of space..
 * Which breaks everything else. So we need to make it simple. Which organs don't innately support, so we need to just haphazardly slap it on.
 */
// tg: code/modules/fishing/fish/types/rift.dm:722
public partial class datum_bodypart_overlay_simple_babbearfish : datum_bodypart_overlay_simple
{
    // tg: code/modules/fishing/fish/types/rift.dm:728
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon owner = null)
    {
        return Dm.truthy(base.can_draw_on_bodypart(bodypart_owner, owner)) && !Dm.truthy(Dm.bitand(bodypart_owner.owner?.obscured_slots, HIDEEARS));
    }
}
