// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/body.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the monkified mutation (syndicate G4c, D316) and the other body mutations (gaps P3, D327): epilepsy, unstable DNA,
// cough, paranoia, dwarfism, acromegaly, gigantism, tourettes, glow and anti-glow, fiery sweat, spatial instability, acidic flesh,
// spastic, two left feet, internal martyrdom, H.A.R.S., hypermetabolic blood, inexorable, regeneration, chemical allergy and venomous
// strikes. The var blocks are generated (D111); clumsy, deaf, strong, stimmed, insulated and the rock eaters have vars only.

//These mutations change your overall "form" somehow, like size

//Epilepsy gives a very small chance to have a seizure every life tick, knocking you unconscious.
// tg: code/datums/mutations/body.dm:4
public partial class datum_mutation_epilepsy : datum_mutation
{
    // tg: code/datums/mutations/body.dm:13
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(0.5f * GET_MUTATION_SYNCHRONIZER(this), seconds_per_tick))
            await trigger_seizure();
    }

    // tg: code/datums/mutations/body.dm:17
    public virtual async DmTask trigger_seizure()
    {
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return;
        owner.visible_message(span_danger(I18n.T("[] starts having a seizure!", owner)), span_userdanger(I18n.T("You have a seizure!")));
        await owner.Unconscious(200 * GET_MUTATION_POWER(this));
        await owner.set_jitter(2000 * SECONDS * GET_MUTATION_POWER(this)); //yes this number looks crazy but the jitter animations are amplified based on the duration.
        await owner.add_mood_event("epilepsy", typeof(datum_mood_event_epilepsy));
        _addtimer(Dm.@new<datum_callback>(this, nameof(jitter_less)), 9 * SECONDS, file: "code/datums/mutations/body.dm", line: 24);
    }

    // tg: code/datums/mutations/body.dm:26
    public virtual async DmTask jitter_less()
    {
        if (QDELETED(owner))
            return;

        await owner.set_jitter(20 * SECONDS);
    }

    // tg: code/datums/mutations/body.dm:32
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOB_FLASHED, nameof(get_flashed_nerd));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:38
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        UnregisterSignal(owner, COMSIG_MOB_FLASHED);
        return null;
    }

    // tg: code/datums/mutations/body.dm:43
    [SignalHandler]
    public virtual void get_flashed_nerd(params object[] __extra)
    {
        if (!prob(30))
            return;
        Waiting.NoSleep(trigger_seizure());
    }
}

//Unstable DNA induces random mutations!
// tg: code/datums/mutations/body.dm:52
public partial class datum_mutation_bad_dna : datum_mutation
{
    // tg: code/datums/mutations/body.dm:60
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        to_chat(owner, text_gain_indication);
        // var/mob/new_mob: the three /mob/living/carbon procs return nothing (dna.dm:605, :616, :624), so it stays null
        mob new_mob = null;
        if (prob(95))
        {
            switch (rand(1, 3))
            {
                case 1:
                    await owner.easy_random_mutate(NEGATIVE + MINOR_NEGATIVE);
                    break;
                case 2:
                    await owner.random_mutate_unique_identity();
                    break;
                case 3:
                    await owner.random_mutate_unique_features();
                    break;
            }
        }
        else
            await owner.easy_random_mutate(POSITIVE);
        if (new_mob != null && ismob(new_mob))
            owner = new_mob as mob_living_carbon_human;
        dot = owner;
        await on_losing(owner);
        return dot;
    }
}

//Cough gives you a chronic cough that causes you to drop items.
// tg: code/datums/mutations/body.dm:83
public partial class datum_mutation_cough : datum_mutation
{
    // tg: code/datums/mutations/body.dm:92
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(2.5f * GET_MUTATION_SYNCHRONIZER(this), seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            await owner.drop_all_held_items();
            await owner.emote("cough");
            if (GET_MUTATION_POWER(this) > 1)
            {
                float cough_range = GET_MUTATION_POWER(this) * 4;
                turf target = get_ranged_target_turf(owner, REVERSE_DIR(Dm.num(owner.dir)), cough_range) as turf;
                await owner.throw_at(target, cough_range, GET_MUTATION_POWER(this));
            }
        }
    }
}

// tg: code/datums/mutations/body.dm:101
public partial class datum_mutation_paranoia : datum_mutation
{
    // tg: code/datums/mutations/body.dm:109
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(2.5f, seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            await owner.emote("scream");
            if (prob(25))
                await owner.adjust_hallucinations(40 * SECONDS);
        }
    }
}

//Dwarfism shrinks your body and lets you pass tables.
// tg: code/datums/mutations/body.dm:116
public partial class datum_mutation_dwarfism : datum_mutation
{
    // tg: code/datums/mutations/body.dm:125
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_DWARF, GENETIC_MUTATION);
        owner.visible_message(span_danger(I18n.T("[] suddenly shrinks!", owner)), span_notice(I18n.T("Everything around you seems to grow..")));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:132
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        REMOVE_TRAIT(owner, TRAIT_DWARF, GENETIC_MUTATION);
        owner.visible_message(span_danger(I18n.T("[] suddenly grows!", owner)), span_notice(I18n.T("Everything around you seems to shrink..")));
        return null;
    }
}

// tg: code/datums/mutations/body.dm:138
public partial class datum_mutation_acromegaly : datum_mutation
{
    // tg: code/datums/mutations/body.dm:147
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_TOO_TALL, GENETIC_MUTATION);
        owner.visible_message(span_danger(I18n.T("[] suddenly grows tall!", owner)), span_notice(I18n.T("You feel a small strange urge to fight small men with slingshots. Or maybe play some basketball.")));
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(head_bonk));
        await owner.regenerate_icons();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:156
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        REMOVE_TRAIT(owner, TRAIT_TOO_TALL, GENETIC_MUTATION);
        owner.visible_message(span_danger(I18n.T("[] suddenly shrinks!", owner)), span_notice(I18n.T("You return to your usual height.")));
        UnregisterSignal(owner, COMSIG_MOVABLE_MOVED);
        await owner.regenerate_icons();
        return null;
    }

    // This is specifically happening because they're not used to their new height and are stumbling around into machinery made for normal humans
    // tg: code/datums/mutations/body.dm:165
    [SignalHandler]
    public virtual void head_bonk(mob_living parent = null, params object[] __extra)
    {
        atom_movable whacked_by = (atom_movable)Dm.locate<obj_machinery_door_airlock>(parent.loc) ?? (atom_movable)Dm.locate<obj_machinery_door_firedoor>(parent.loc) ?? Dm.locate<obj_structure_mineral_door>(parent.loc);
        if (whacked_by == null || prob(100 - (8 * GET_MUTATION_SYNCHRONIZER(this))))
            return;
        to_chat(parent, span_danger(I18n.T("You hit your head on the []'s header!", whacked_by)));
        float dmg = HAS_TRAIT(parent, TRAIT_HEAD_INJURY_BLOCKED) ? rand(1, 4) : rand(2, 9);
        Waiting.NoSleep(parent.apply_damage(dmg, BRUTE, BODY_ZONE_HEAD));
        Waiting.NoSleep(parent.do_attack_animation(whacked_by, ATTACK_EFFECT_PUNCH));
        playsound(whacked_by, "sound/effects/bang.ogg", 10, TRUE);
        Waiting.NoSleep(parent.adjust_staggered_up_to(STAGGERED_SLOWDOWN_LENGTH, 10 * SECONDS));
    }
}

// tg: code/datums/mutations/body.dm:177
public partial class datum_mutation_gigantism : datum_mutation
{
    // tg: code/datums/mutations/body.dm:184
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        ADD_TRAIT(owner, TRAIT_GIANT, GENETIC_MUTATION);
        await owner.update_transform(1.25f);
        owner.visible_message(span_danger(I18n.T("[] suddenly grows!", owner)), span_notice(I18n.T("Everything around you seems to shrink..")));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:192
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        REMOVE_TRAIT(owner, TRAIT_GIANT, GENETIC_MUTATION);
        await owner.update_transform(0.8f);
        owner.visible_message(span_danger(I18n.T("[] suddenly shrinks!", owner)), span_notice(I18n.T("Everything around you seems to grow..")));
        return null;
    }
}

//Tourettes causes you to randomly stand in place and shout.
// tg: code/datums/mutations/body.dm:209
public partial class datum_mutation_tourettes : datum_mutation
{
    // tg: code/datums/mutations/body.dm:217
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(5 * GET_MUTATION_SYNCHRONIZER(this), seconds_per_tick) && !IS_UNCONSCIOUS_OR_CRIT(owner) && !Dm.truthy(owner.IsStun()))
        {
            switch (rand(1, 3))
            {
                case 1:
                    await owner.emote("twitch");
                    break;
                case 2:
                case 3:
                    // the shouted words are speech, not interface text (rules 5.3)
                    await owner.say(Dm.text("[][]", prob(50) ? ";" : "", pick("SHIT", "PISS", "FUCK", "CUNT", "COCKSUCKER", "MOTHERFUCKER", "TITS")), forced: name);
                    break;
            }
            float w_offset = rand(-2, 2);
            float z_offset = rand(-1, 1);
            animate(owner, Dm.named("pixel_w", w_offset), Dm.named("pixel_z", z_offset), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
            animate(owner, Dm.named("pixel_w", -w_offset), Dm.named("pixel_z", -z_offset), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        }
    }
}

// tg: code/datums/mutations/body.dm:240
public partial class datum_mutation_race : datum_mutation
{
    // tg: code/datums/mutations/body.dm:253
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        if (HAS_TRAIT(owner, TRAIT_LESSER_HUMANOID))
            return dot;
        original_species = ((datum_species)owner.dna.species).type;
        original_name = owner.real_name;
        owner.monkeyize();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:263
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (owner.stat == DEAD)
            return null;
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        if (QDELETED(owner))
            return dot;

        await owner.fully_replace_character_name(null, original_name);
        owner.humanize(original_species);
        return dot;
    }
}

// tg: code/datums/mutations/body.dm:275
public partial class datum_mutation_glow : datum_mutation
{
    // tg: code/datums/mutations/body.dm:288
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        glow_color = get_glow_color();
        glow = owner.mob_light() as obj_effect_dummy_lighting_obj_moblight;
        return dot;
    }

    // Override modify here without a parent call, because we don't actually give an action.
    // tg: code/datums/mutations/body.dm:296
    public override async DmTask<object> setup()
    {
        await DmTask.CompletedTask;
        if (glow == null)
            return null;

        glow.set_light_range_power_color(glow_range * GET_MUTATION_POWER(this), glow_power, glow_color);
        return null;
    }

    // tg: code/datums/mutations/body.dm:302
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        QDEL_NULL(ref glow);
        return dot;
    }

    /// Returns a color for the glow effect
    // tg: code/datums/mutations/body.dm:309
    public virtual object get_glow_color()
    {
        return pick(COLOR_RED, COLOR_BLUE, COLOR_YELLOW, COLOR_GREEN, COLOR_PURPLE, COLOR_ORANGE);
    }
}

// tg: code/datums/mutations/body.dm:312
public partial class datum_mutation_glow_anti : datum_mutation_glow
{
    // tg: code/datums/mutations/body.dm:321
    public override object get_glow_color()
    {
        return COLOR_BLACK;
    }
}

// tg: code/datums/mutations/body.dm:352
public partial class datum_mutation_fire : datum_mutation
{
    // tg: code/datums/mutations/body.dm:363
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB((0.05f + (100 - dna.stability) / 19.5f) * GET_MUTATION_SYNCHRONIZER(this), seconds_per_tick))
        {
            await owner.adjust_fire_stacks(2 * GET_MUTATION_POWER(this));
            owner.ignite_mob();
        }
    }

    // tg: code/datums/mutations/body.dm:368
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        MODIFY_PHYSIOLOGY(owner, BURN, 0.5f);
        return dot;
    }

    // tg: code/datums/mutations/body.dm:374
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        MODIFY_PHYSIOLOGY(owner, BURN, 2);
        return null;
    }
}

// tg: code/datums/mutations/body.dm:379
public partial class datum_mutation_badblink : datum_mutation
{
    // tg: code/datums/mutations/body.dm:392
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(warpchance, seconds_per_tick))
        {
            object warpmessage = pick(
                span_warning(I18n.T("With a sickening 720-degree twist of [] back, [] vanishes into thin air.", owner.p_their(), owner)),
                span_warning(I18n.T("[] does some sort of strange backflip into another dimension. It looks pretty painful.", owner)),
                span_warning(I18n.T("[] does a jump to the left, a step to the right, and warps out of reality.", owner)),
                span_warning(I18n.T("[]'s torso starts folding inside out until it vanishes from reality, taking [] with it.", owner, owner)),
                span_warning(I18n.T("One moment, you see []. The next, [] is gone.", owner, owner)));
            owner.visible_message(warpmessage, span_userdanger(I18n.T("You feel a wave of nausea as you fall through reality!")));
            float warpdistance = rand(10, 15) * GET_MUTATION_POWER(this);
            await do_teleport(owner, get_turf(owner), warpdistance, channel: TELEPORT_CHANNEL_FREE);
            owner.adjust_disgust(GET_MUTATION_SYNCHRONIZER(this) * (warpchance * warpdistance));
            warpchance = 0;
            owner.visible_message(span_danger(I18n.T("[] appears out of nowhere!", owner)));
        }
        else
            warpchance += 0.0625f * Dm.num(seconds_per_tick) / GET_MUTATION_ENERGY(this);
    }
}

// tg: code/datums/mutations/body.dm:409
public partial class datum_mutation_acidflesh : datum_mutation
{
    // tg: code/datums/mutations/body.dm:420
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (SPT_PROB(13, seconds_per_tick))
        {
            if (msgcooldown <= world.time) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            {
                to_chat(owner, span_danger(I18n.T("Your acid flesh bubbles...")));
                msgcooldown = world.time + 20 * SECONDS; // COOLDOWN_START, inline (rules 11.1 (a))
            }
            if (prob(15))
            {
                await owner.acid_act(rand(30, 50), 10);
                owner.visible_message(span_warning(I18n.T("[]'s skin bubbles and pops.", owner)), span_userdanger(I18n.T("Your bubbling flesh pops! It burns!")));
                playsound(owner, "sound/items/weapons/sear.ogg", 50, TRUE);
            }
        }
    }
}

// tg: code/datums/mutations/body.dm:430
public partial class datum_mutation_spastic : datum_mutation
{
    // tg: code/datums/mutations/body.dm:439
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        await owner.apply_status_effect(typeof(datum_status_effect_spasms));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:445
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        if (Dm.truthy(await base.on_losing(owner)))
            return null;
        this.owner.remove_status_effect(typeof(datum_status_effect_spasms));
        return null;
    }
}

// tg: code/datums/mutations/body.dm:450
public partial class datum_mutation_extrastun : datum_mutation
{
    // tg: code/datums/mutations/body.dm:459
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:465
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(this.owner, COMSIG_MOVABLE_MOVED);
        return dot;
    }

    ///Triggers on moved(). Randomly makes the owner trip
    // tg: code/datums/mutations/body.dm:472
    [SignalHandler]
    public virtual void on_move(params object[] __extra)
    {
        if (prob(99.5f)) //The brawl mutation
            return;
        if (owner.buckled != null || owner.body_position == LYING_DOWN || HAS_TRAIT(owner, TRAIT_IMMOBILIZED) || owner.throwing != null || Dm.truthy(Dm.bitand(owner.movement_type, VENTCRAWLING | FLYING | FLOATING)))
            return; //remove the 'edge' cases
        to_chat(owner, span_danger(I18n.T("You trip over your own feet.")));
        Waiting.NoSleep(owner.Knockdown(30));
    }
}

// tg: code/datums/mutations/body.dm:482
public partial class datum_mutation_martyrdom : datum_mutation
{
    // tg: code/datums/mutations/body.dm:492
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(owner, COMSIG_MOB_STATCHANGE, nameof(bloody_shower));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:498
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return TRUE;
        UnregisterSignal(this.owner, COMSIG_MOB_STATCHANGE);
        return dot;
    }

    // tg: code/datums/mutations/body.dm:504
    [SignalHandler]
    public virtual void bloody_shower(datum source = null, object new_stat = null)
    {
        if (Dm.num(new_stat) < HARD_CRIT)
            return;
        DmList organs = owner.get_organs_for_zone(BODY_ZONE_HEAD, TRUE) as DmList;

        foreach (obj_item_organ I in Dm.each<obj_item_organ>(organs))
            qdel(I);

        // explosion_cause = src: the port's explosion types the cause as an atom (explosions.dm:213 declares atom/explosion_cause),
        // and the mutation is a datum, so the log names the origin (the owner) as the cause
        Waiting.NoSleep(explosion(owner, light_impact_range: 2, adminlog: TRUE));
        foreach (mob_living_carbon_human splashed in Dm.each<mob_living_carbon_human>(view(2, owner)))
        {
            obj_item_organ_eyes eyes = splashed.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (eyes != null)
            {
                to_chat(splashed, span_userdanger(I18n.T("You are blinded by a shower of blood!")));
                Waiting.NoSleep(eyes.apply_organ_damage(5));
            }
            else
                to_chat(splashed, span_userdanger(I18n.T("You are knocked down by a wave of... blood?!")));
            Waiting.NoSleep(splashed.Stun(2 * SECONDS));
            Waiting.NoSleep(splashed.set_eye_blur_if_lower(40 * SECONDS));
            Waiting.NoSleep(splashed.adjust_confusion(3 * SECONDS));
        }
        foreach (mob_living_silicon borgo in Dm.each<mob_living_silicon>(view(2, owner)))
        {
            to_chat(borgo, span_userdanger(I18n.T("Your sensors are disabled by a shower of blood!")));
            Waiting.NoSleep(borgo.Paralyze(6 * SECONDS));
        }
        owner.investigate_log("has been gibbed by the martyrdom mutation.", INVESTIGATE_DEATHS);
        Waiting.NoSleep(owner.gib(DROP_ALL_REMAINS));
    }
}

// tg: code/datums/mutations/body.dm:530
public partial class datum_mutation_headless : datum_mutation
{
    // tg: code/datums/mutations/body.dm:541
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;

        obj_item_organ_brain brain = owner.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (brain != null)
        {
            await brain.Remove(owner, special: TRUE, movement_flags: NO_ID_TRANSFER);
            brain.zone = BODY_ZONE_CHEST;
            await brain.Insert(owner, special: TRUE, movement_flags: NO_ID_TRANSFER);
        }

        obj_item_bodypart_head head = owner.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (head != null)
        {
            owner.visible_message(span_warning(I18n.T("[]'s head splatters with a sickening crunch!", owner)), ignored_mobs: Dm.list(owner));
            Dm.@new<obj_effect_gibspawner_generic>(get_turf(owner), owner);
            await head.drop_organs();
            await head.dismember(dam_type: BRUTE, silent: TRUE);
            qdel(head);
        }
        RegisterSignal(owner, COMSIG_ATTEMPT_CARBON_ATTACH_LIMB, nameof(abort_attachment));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:561
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return TRUE;

        UnregisterSignal(owner, COMSIG_ATTEMPT_CARBON_ATTACH_LIMB);
        object successful = await owner.regenerate_limb(BODY_ZONE_HEAD);
        if (!Dm.truthy(successful))
        {
            stack_trace("HARS mutation head regeneration failed! (usually caused by headless syndrome having a head)", "code/datums/mutations/body.dm", 569);
            return TRUE;
        }
        obj_item_organ_brain brain = owner.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        if (brain != null)
        {
            await brain.Remove(owner, special: TRUE, movement_flags: NO_ID_TRANSFER);
            brain.zone = Dm.initial(brain, (obj_item_organ_brain b) => b.zone);
            await brain.Insert(owner, special: TRUE, movement_flags: NO_ID_TRANSFER);
        }

        DmList excluded_zones = Dm.sub(GLOB.all_body_zones, BODY_ZONE_HEAD) as DmList;
        await ((datum_species)owner.dna.species).regenerate_organs(owner, replace_current: FALSE, excluded_zones: excluded_zones); //replace_current needs to be FALSE to prevent weird adding and removing mutation healing
        await owner.apply_damage(damage: 50, damagetype: BRUTE, def_zone: BODY_ZONE_HEAD); //and this to DISCOURAGE organ farming, or at least not make it free.
        owner.visible_message(span_warning(I18n.T("[]'s head returns with a sickening crunch!", owner)), span_warning(I18n.T("Your head regrows with a sickening crack! Ouch.")));
        Dm.@new<obj_effect_gibspawner_generic>(get_turf(owner), owner);
        return dot;
    }

    // tg: code/datums/mutations/body.dm:583
    [SignalHandler]
    public virtual object abort_attachment(datum source = null, obj_item_bodypart new_limb = null, object special = null) //you aren't getting your head back
    {
        if (new_limb is obj_item_bodypart_head)
            return COMPONENT_NO_ATTACH;
        return null;
    }
}

// You bleed faster but regenerate blood faster
// tg: code/datums/mutations/body.dm:590
public partial class datum_mutation_bloodier : datum_mutation
{
    // tg: code/datums/mutations/body.dm:608
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(physiology_modified))
        {
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, bleed_rate);
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLOOD_REGEN, blood_regen_rate);
            physiology_modified = TRUE;
        }
        return dot;
    }

    // tg: code/datums/mutations/body.dm:617
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(physiology_modified))
        {
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 1 / Dm.num(bleed_rate));
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLOOD_REGEN, 1 / blood_regen_rate);
            physiology_modified = FALSE; // just in case
        }
        return dot;
    }

    // tg: code/datums/mutations/body.dm:626
    public override async DmTask<object> setup()
    {
        await DmTask.CompletedTask;
        if (owner != null && Dm.truthy(physiology_modified))
        {
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, 1 / Dm.num(bleed_rate));
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLOOD_REGEN, 1 / blood_regen_rate);
            physiology_modified = FALSE;
        }

        bleed_rate = clamp(Dm.num(Dm.initial(this, (datum_mutation_bloodier b) => b.bleed_rate)) * GET_MUTATION_SYNCHRONIZER(this) * GET_MUTATION_POWER(this), 1, 2);
        blood_regen_rate = clamp(Dm.num(Dm.initial(this, (datum_mutation_bloodier b) => b.blood_regen_rate)) * GET_MUTATION_POWER(this), 4, 12);

        if (owner != null && !Dm.truthy(physiology_modified)) // redundant but just in case
        {
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLEED, bleed_rate);
            MODIFY_PHYSIOLOGY(owner, PHYS_COEFF_BLOOD_REGEN, blood_regen_rate);
            physiology_modified = TRUE;
        }
        return TRUE;
    }
}

// Soft crit is disabed
// tg: code/datums/mutations/body.dm:666
public partial class datum_mutation_inexorable : datum_mutation
{
    // tg: code/datums/mutations/body.dm:677
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(acquirer, COMSIG_LIVING_HEALTH_UPDATE, nameof(check_health));
        check_health();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:684
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_LIVING_HEALTH_UPDATE);
        REMOVE_TRAIT(owner, TRAIT_FORCE_WHISPER, REF(this));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:691
    [SignalHandler]
    public virtual void check_health(params object[] __extra)
    {
        if (owner.health > owner.crit_threshold || IS_UNCONSCIOUS_OR_CRIT(owner))
            REMOVE_TRAIT(owner, TRAIT_FORCE_WHISPER, REF(this));
        else
            ADD_TRAIT(owner, TRAIT_FORCE_WHISPER, REF(this));
    }

    // tg: code/datums/mutations/body.dm:698
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (owner.health > owner.crit_threshold || IS_UNCONSCIOUS_OR_CRIT(owner) || HAS_TRAIT(owner, TRAIT_STASIS))
            return;
        if (HAS_TRAIT(owner, TRAIT_NOCRITDAMAGE) && owner.health <= owner.hardcrit_threshold + 10)
            return;
        float spt = Dm.num(seconds_per_tick);
        // Gives you 30 seconds of being in fake soft crit... give or take
        if (HAS_TRAIT(owner, TRAIT_TOXIMMUNE) || HAS_TRAIT(owner, TRAIT_TOXINLOVER))
            await owner.adjust_brute_loss(1 * spt * GET_MUTATION_SYNCHRONIZER(this));
        else
        {
            await owner.adjust_tox_loss(0.5f * spt * GET_MUTATION_SYNCHRONIZER(this));
            await owner.adjust_brute_loss(0.5f * spt * GET_MUTATION_SYNCHRONIZER(this));
        }
        // Offsets suffocation but not entirely
        await owner.adjust_oxy_loss(-0.5f * spt);
    }
}

// tg: code/datums/mutations/body.dm:712
public partial class datum_mutation_limb_regeneration : datum_mutation
{
    // tg: code/datums/mutations/body.dm:728
    public override object can_acquire(mob_living_carbon_human acquirer = null)
    {
        return !HAS_TRAIT(acquirer, TRAIT_NOHUNGER) ? TRUE : FALSE;
    }

    // tg: code/datums/mutations/body.dm:731
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (owner.stat >= HARD_CRIT)
            return;
        if (!SPT_PROB(5 * (Dm.truthy(notified_of_ability) ? 4 : 1) * Dm.pow(GET_MUTATION_POWER(this), 2), seconds_per_tick))
            return;

        DmList missing_limbs = owner.get_missing_limbs() as DmList;
        DmList missing_important_organs = owner.get_missing_organs(include_appendix: GET_MUTATION_POWER(this) > 1 ? TRUE : FALSE) as DmList; // appendix can regrow if you have power
        DmList missing_special_organs = Dm.list();
        foreach (object organ_type in Dm.each(((datum_species)owner.dna?.species)?.mutant_organs))
        {
            if (owner.get_organ_by_type(organ_type) == null && Dm.truthy(should_visual_organ_apply_to(organ_type, owner)))
                Dm.append(ref missing_special_organs, organ_type);
        }

        if (!Dm.truthy(length(missing_limbs)) && !Dm.truthy(length(missing_important_organs)) && !Dm.truthy(length(missing_special_organs)))
            return;

        if (owner.nutrition <= nutrition_threshold)
        {
            if (IS_UNCONSCIOUS(owner) && !Dm.truthy(notified_of_ability))
            {
                to_chat(owner, span_green(I18n.T("You feel a strange tingling, as if your body is trying to do something - though you feel like you could use a meal first.")));
                notified_of_ability = TRUE;
            }
            return;
        }
        if (!IS_UNCONSCIOUS(owner))
        {
            if (owner.nutrition > nutrition_threshold && !Dm.truthy(notified_of_ability))
            {
                to_chat(owner, span_green(I18n.T("You feel a strange tingling, as if your body is trying to do something - though you feel like you could use a nap first.")));
                notified_of_ability = TRUE;
            }
            return;
        }

        notified_of_ability = FALSE;
        // "core organs" and limbs are prioritized
        if (Dm.truthy(length(missing_important_organs)) || Dm.truthy(length(missing_limbs)))
        {
            if (Dm.truthy(length(missing_important_organs)) && (prob(50) || !Dm.truthy(length(missing_limbs))))
            {
                object replacement_type = ((datum_species)owner.dna.species).get_mutant_organ_type_for_slot(pick(missing_important_organs));
                obj_item_organ replacement = Dm.@new(replacement_type as Type) as obj_item_organ;
                await replacement.Insert(owner, special: TRUE);
                to_chat(owner, span_green(regrowth_message(replacement, replacement.name)));
            }
            else
            {
                object replacing_zone = pick(missing_limbs);
                await owner.regenerate_limb(replacing_zone);
                obj_item_bodypart replacement = owner.get_bodypart(Dm.str(replacing_zone)) as obj_item_bodypart;
                to_chat(owner, span_green(regrowth_message(replacement, replacement.plaintext_zone)));
                owner.visible_message(span_warning(I18n.T("[]'s [] reforms, making a loud, grotesque sound!", owner, replacement.plaintext_zone)), ignored_mobs: Dm.list(owner));
            }
            await owner.adjust_nutrition(-NUTRITION_LEVEL_FULL * 0.5f * GET_MUTATION_SYNCHRONIZER(this));
            playsound(owner, "sound/effects/magic/demon_consume.ogg", 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            return;
        }

        // "cosmetic" organs are second priority and cheaper to regenerate
        if (Dm.truthy(length(missing_special_organs)))
        {
            object replacement_type = pick(missing_special_organs);
            obj_item_organ replacement = Dm.@new(replacement_type as Type) as obj_item_organ;
            await replacement.Insert(owner, special: TRUE);
            to_chat(owner, span_green(regrowth_message(replacement, replacement.name)));
            if (Dm.truthy(Dm.bitand(replacement.organ_flags, ORGAN_EXTERNAL)))
                owner.visible_message(span_warning(I18n.T("[]'s [] reforms, making a loud, grotesque sound!", owner, replacement.name)), ignored_mobs: Dm.list(owner));
            await owner.adjust_nutrition(-NUTRITION_LEVEL_FULL * 0.3f * GET_MUTATION_SYNCHRONIZER(this));
            playsound(owner, "sound/effects/magic/demon_consume.ogg", 33, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            return;
        }

        stack_trace(Dm.text("Limb regeneration failed to find a missing limb or organ to regenerate for [] despite passing the checks", owner), "code/datums/mutations/body.dm", 787);
    }

    // The three regrowth messages (body.dm:762, :768 and :779) pick "some"/"a" and "s were"/" was" by the part's gender inside one
    // string; the port keeps the two sentences whole so each is one translatable text (rules 5.3).
    private static string regrowth_message(atom replacement, object part_name)
    {
        if (Dm.eq(replacement.gender, PLURAL))
            return I18n.T("The tingling feeling builds to a climax, until ultimately you feel some new [] where your old ones were!", part_name);
        return I18n.T("The tingling feeling builds to a climax, until ultimately you feel a new [] where your old one was!", part_name);
    }
}

// tg: code/datums/mutations/body.dm:790
public partial class datum_mutation_chemical_allergy : datum_mutation
{
    /// Typecache of reagents that trigger the allergy
    // tg: code/datums/mutations/body.dm:800  var/static/list/danger_reagents (a type static, rules 10.6)
    public static ref DmList danger_reagents => ref TgState.Current.datum_mutation_chemical_allergy__danger_reagents;

    // tg: code/datums/mutations/body.dm:804
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(length(danger_reagents)))
            return;

        danger_reagents = Dm.sub(Dm.sub(typecacheof(Dm.list(
            typeof(datum_reagent_medicine),
            typeof(datum_reagent_drug))), typecacheof(Dm.list(
            typeof(datum_reagent_medicine_mannitol),
            typeof(datum_reagent_medicine_mutadone)))), GLOB.allergy_reagent_blacklist) as DmList;
    }

    // tg: code/datums/mutations/body.dm:817
    public override async DmTask<object> on_acquiring(mob_living_carbon_human acquirer = null)
    {
        object dot = await base.on_acquiring(acquirer);
        if (!Dm.truthy(dot))
            return dot;
        RegisterSignal(acquirer, COMSIG_LIVING_HEALTHSCAN, nameof(on_healthscan));
        return dot;
    }

    // tg: code/datums/mutations/body.dm:823
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        UnregisterSignal(owner, COMSIG_LIVING_HEALTHSCAN);
        return dot;
    }

    // tg: code/datums/mutations/body.dm:829
    [SignalHandler]
    public virtual void on_healthscan(datum source = null, DmList render_list = null, object advanced = null, mob user = null, object mode = null, object tochat = null)
    {
        Dm.append(ref render_list, "<span class='alert ml-1'>");
        Dm.append(ref render_list, conditional_tooltip(I18n.T("Subject has []", name), I18n.T("Subject will react negatively to most forms of medicine - avoid administering chemicals as a part of treatment unless absolutely necessary."), tochat));
        Dm.append(ref render_list, "</span><br>");
    }

    // tg: code/datums/mutations/body.dm:837
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (!has_danger_reagent())
        {
            if (Dm.truthy(was_experiencing_allergy))
            {
                was_experiencing_allergy = FALSE;
                instability *= 2;
                await owner.dna.update_instability();
            }
            return;
        }
        if (!SPT_PROB(80, seconds_per_tick))
            return;
        if (!Dm.truthy(was_experiencing_allergy))
        {
            was_experiencing_allergy = TRUE;
            instability *= 0.5f; // halves the negative instability it rewards, which could push you into danger territory!
            await owner.dna.update_instability();
        }

        if (SPT_PROB(66, seconds_per_tick))
            await owner.set_stutter_if_lower(4 * SECONDS);
        if (SPT_PROB(33, seconds_per_tick))
        {
            owner.adjust_disgust(24, DISGUST_LEVEL_VERYDISGUSTED);
            await owner.adjust_tox_loss(pick(2, 3, 4), forced: TRUE);
        }
        if (SPT_PROB(12, seconds_per_tick))
        {
            await owner.adjust_jitter_up_to(6 * SECONDS, 36 * SECONDS);
            await owner.adjust_dizzy_up_to(6 * SECONDS, 36 * SECONDS);
            owner.adjust_blood_volume(-Dm.num(pick(12, 16, 20, 24)), minimum: BLOOD_VOLUME_OKAY);
        }
        if (SPT_PROB(6, seconds_per_tick))
        {
            await owner.adjust_confusion_up_to(4 * SECONDS, 12 * SECONDS);
            owner.losebreath = max(owner.losebreath, 4);
        }
    }

    // tg: code/datums/mutations/body.dm:864
    public virtual bool has_danger_reagent()
    {
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>((owner.reagents as datum_reagents)?.reagent_list))
        {
            if (!Dm.truthy(is_type_in_typecache(reagent, danger_reagents)))
                continue;
            if (!Dm.truthy(reagent.metabolizing) || Dm.num(reagent.volume) < 1)
                continue;
            return true;
        }

        return false;
    }
}

// tg: code/datums/mutations/body.dm:874
public partial class datum_mutation_venomous_strikes : datum_mutation
{
    // tg: code/datums/mutations/body.dm:887
    public override async DmTask<object> on_acquiring(mob_living_carbon_human owner = null)
    {
        text_gain_indication = Dm.eq((owner.get_active_hand() as obj_item_bodypart)?.unarmed_attack_effect, ATTACK_EFFECT_CLAW) ? span_notice(I18n.T("You teeth and claws feel sharper.")) : Dm.initial(this, (datum_mutation_venomous_strikes v) => v.text_gain_indication);
        object dot = await base.on_acquiring(owner);
        if (!Dm.truthy(dot))
            return dot;
        await apply_venom();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:894
    public override async DmTask<object> on_losing(mob_living_carbon_human owner = null)
    {
        text_lose_indication = Dm.eq((owner.get_active_hand() as obj_item_bodypart)?.unarmed_attack_effect, ATTACK_EFFECT_CLAW) ? span_notice(I18n.T("You teeth and claws feel duller.")) : Dm.initial(this, (datum_mutation_venomous_strikes v) => v.text_lose_indication);
        object dot = await base.on_losing(owner);
        if (Dm.truthy(dot))
            return dot;
        remove_venom();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:901
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        if (!Dm.eq(var_name, nameof(venom_type)) && !Dm.eq(var_name, nameof(venom_amount)))
            return await base.vv_edit_var(var_name, var_value);

        remove_venom();
        object dot = await base.vv_edit_var(var_name, var_value);
        await apply_venom();
        return dot;
    }

    // tg: code/datums/mutations/body.dm:909
    public override async DmTask pre_apply_chromosome()
    {
        await apply_venom();
    }

    // tg: code/datums/mutations/body.dm:912
    public override async DmTask post_apply_chromosome()
    {
        await DmTask.CompletedTask;
        remove_venom();
    }

    // tg: code/datums/mutations/body.dm:915
    public virtual async DmTask apply_venom()
    {
        await owner.AddElement(typeof(datum_element_venomous), venom_type, venom_amount * GET_MUTATION_POWER(this), INJECT_CHECK_IGNORE_SPECIES);
    }

    // tg: code/datums/mutations/body.dm:918
    public virtual void remove_venom()
    {
        owner.RemoveElement(typeof(datum_element_venomous), venom_type, venom_amount * GET_MUTATION_POWER(this), INJECT_CHECK_IGNORE_SPECIES);
    }

    // tg: code/datums/mutations/body.dm:921
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await ((datum_reagents)owner.reagents).remove_reagent(venom_type, round(venom_amount * 0.33f, CHEMICAL_VOLUME_ROUNDING) * Dm.num(seconds_per_tick));
    }
}

public sealed partial class TgState
{
    /// tg <c>var/static/list/danger_reagents</c> of <c>/datum/mutation/chemical_allergy</c> (rules 10.6).
    // tg: code/datums/mutations/body.dm:800
    internal DmList datum_mutation_chemical_allergy__danger_reagents;
}
