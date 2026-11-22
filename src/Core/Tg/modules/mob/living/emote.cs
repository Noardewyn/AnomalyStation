// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/emote.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The procs of the file (M9 slice B, docs/ai/specs/social.md; its emotes' vars are generated), the kiss's (it puts a hand item in the hand)
// since M9 slice F, with the hand items and the offering. The custom emote's word filters and the
// emote ban read config/word_filter.toml and the ban database, which the port does not have (as mob_say.cs: they match nothing).

// tg: code/modules/mob/living/emote.dm:7
public partial class datum_emote_living_taunt : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:13
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        user.spin(TAUNT_EMOTE_DURATION, 0.1f * SECONDS);
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:17
public partial class datum_emote_living_tongue : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:22
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        var human_user = user as mob_living_carbon_human;
        if (human_user != null && !Dm.truthy(human_user.get_organ_slot(ORGAN_SLOT_TONGUE)))
        {
            to_chat(human_user, span_warning(I18n.T("You don't have a tongue!")));
            return null;
        }
        object dot = await base.run_emote(user, @params, type_override, intentional);
        QDEL_IN_emote(human_user == null ? null : await human_user.give_emote_overlay(typeof(datum_bodypart_overlay_simple_emote_tongue)), 5.2f * SECONDS, 28);
        return dot;
    }

    // QDEL_IN(item, time) (__DEFINES/qdel.dm:60, it expands addtimer: inline, rules 11.1 (f)) for the emotes' overlays
    internal static void QDEL_IN_emote(object item, float time, int line)
    {
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? (object)WEAKREF(item as datum) : item), time, file: "code/modules/mob/living/emote.dm", line: line);
    }
}

// tg: code/modules/mob/living/emote.dm:31
public partial class datum_emote_living_blush : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:36
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!ishuman(user))
            return dot;
        var human_user = (mob_living_carbon_human)user;
        datum_emote_living_tongue.QDEL_IN_emote(await human_user.give_emote_overlay(typeof(datum_bodypart_overlay_simple_emote_blush)), 5.2f * SECONDS, 41);
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:80
public partial class datum_emote_living_collapse : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:86
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (isliving(user))
        {
            var living = (mob_living)user;
            await living.Unconscious(4 * SECONDS);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:98
public partial class datum_emote_living_deathgasp : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:115
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = null;
        var living_user = user as mob_living;
        if (!Dm.truthy(is_type_in_typecache(user, mob_type_allowed_typecache)))
            return dot;
        object custom_message = living_user.death_message;
        if (Dm.truthy(custom_message))
            message_animal_or_basic = Dm.str(custom_message);

        if (Dm.truthy(living_user.has_quirk(typeof(datum_quirk_death_mimicry))))
        {
            await living_user.Unconscious(30 * SECONDS, ignore_canstun: TRUE);
            ADD_TRAIT(living_user, TRAIT_FAKEDEATH, QUIRK_TRAIT);
            _addtimer(TRAIT_CALLBACK_REMOVE(living_user, TRAIT_FAKEDEATH, QUIRK_TRAIT), 30 * SECONDS, file: "code/modules/mob/living/emote.dm", line: 124);
        }

        dot = await base.run_emote(user, @params, type_override, intentional);
        message_animal_or_basic = Dm.initial(typeof(datum_emote_living_deathgasp), (datum_emote_living_deathgasp p) => p.message_animal_or_basic);
        return dot;
    }

    // tg: code/modules/mob/living/emote.dm:129
    public override object get_sound(mob_living user = null)
    {
        object dot = base.get_sound(user);
        if (!Dm.truthy(dot) && Dm.truthy(user.death_sound))
            return user.death_sound;
        return dot;
    }

    // tg: code/modules/mob/living/emote.dm:134
    public override object should_play_sound(mob user = null, float? intentional = null)
    {
        var living_user = (mob_living)user;
        if (!Dm.truthy(living_user.can_speak__proc()) || Dm.num(living_user.get_oxy_loss()) >= 50)
            return FALSE; //stop the sound if oxyloss too high/cant speak
        return base.should_play_sound(user, intentional);
    }
}

// tg: code/modules/mob/living/emote.dm:144
public partial class datum_emote_living_faint : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:149
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (isliving(user))
        {
            var living = (mob_living)user;
            await living.SetSleeping(20 * SECONDS);
        }
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:155
public partial class datum_emote_living_flap : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:162
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!ishuman(user))
            return dot;
        var human_user = (mob_living_carbon_human)user;
        var wings = human_user.get_organ_slot(ORGAN_SLOT_EXTERNAL_WINGS) as obj_item_organ_wings;

        // play a flapping noise if the wing has this implemented
        if (wings == null)
            return dot;
        wings.make_flap_sound(human_user);

        if (!Dm.truthy(wings.has_open_sprite))
            return dot;
        float open = wings.wings_open;
        if (Dm.truthy(open))
            await wings.close_wings();
        else
            await wings.open_wings();
        _addtimer(Dm.@new<datum_callback>(wings, Dm.truthy(open) ? nameof(obj_item_organ_wings.open_wings) : nameof(obj_item_organ_wings.close_wings)), wing_time, file: "code/modules/mob/living/emote.dm", line: 181);
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:300
public partial class datum_emote_living_laugh : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:320
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        return Dm.num(Dm.truthy(await base.can_run_emote(user, status_check, intentional, @params)) && Dm.truthy(((mob_living)user).can_speak__proc(allow_mimes: TRUE)));
    }
}

// tg: code/modules/mob/living/emote.dm:334
public partial class datum_emote_living_point : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:342
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        message_param = Dm.initial(typeof(datum_emote_living_point), (datum_emote_living_point p) => p.message_param); // reset
        if (iscarbon(user))
        {
            var our_carbon = (mob_living_carbon)user;
            if (our_carbon.usable_hands <= 0 || Dm.truthy(Dm.bitand(user.incapacitated, INCAPABLE_RESTRAINTS)) || HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
            {
                if (our_carbon.usable_legs > 0)
                {
                    bool one_leg = false;
                    object has_shoes = our_carbon.get_item_by_slot(ITEM_SLOT_FEET);
                    if (our_carbon.usable_legs == 1)
                        one_leg = true;
                    float success_prob = 65;
                    if (HAS_TRAIT(our_carbon, TRAIT_FREERUNNING))
                        success_prob += 35;
                    if (one_leg)
                        success_prob -= 40;
                    if (prob(success_prob))
                        message_param = I18n.T("[]points at %t with their []!", one_leg ? I18n.T("jumps into the air and ") : "", Dm.truthy(has_shoes) ? I18n.T("leg") : I18n.T("toes"));
                    else
                    {
                        message_param = I18n.T("[]tries to point at %t with their [], falling down in the process!", one_leg ? I18n.T("jumps into the air and ") : "", Dm.truthy(has_shoes) ? I18n.T("leg") : I18n.T("toes"));
                        await our_carbon.Paralyze(2 * SECONDS);
                    }
                    PointCooldown(user, 1 * SECONDS, 365);
                }
                else
                {
                    if (Dm.truthy(our_carbon.get_organ_slot(ORGAN_SLOT_EYES)))
                    {
                        message_param = I18n.T("gives a meaningful glance at %t!");
                        PointCooldown(this, 1.5f * SECONDS, 369);
                    }
                    else
                    {
                        if (Dm.truthy(our_carbon.get_organ_slot(ORGAN_SLOT_TONGUE)))
                        {
                            message_param = I18n.T("motions their tongue towards %t!");
                            PointCooldown(this, 2 * SECONDS, 373);
                        }
                        else
                        {
                            message_param = I18n.T("[] trying to motion towards %t.", span_userdanger(I18n.T("bumps [] head on the ground", user.p_their())));
                            await our_carbon.adjust_organ_loss(ORGAN_SLOT_BRAIN, 5);
                            playsound(user, Dm.resource("sound/effects/glass/glassbash.ogg"), 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
                            PointCooldown(this, 2.5f * SECONDS, 378);
                        }
                    }
                }
            }
        }
        return await base.run_emote(user, @params, type_override, intentional);
    }

    // TIMER_COOLDOWN_START(source, "point_verb_emote_cooldown", time) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline,
    // rules 11.1 (f)); tg starts it on the emote datum (src) in three of the four branches, as here
    private static void PointCooldown(datum source, float time, int line)
    {
        LAZYSET(ref source.cooldowns, "point_verb_emote_cooldown", _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), source, "point_verb_emote_cooldown"), time, file: "code/modules/mob/living/emote.dm", line: line));
    }
}

// tg: code/modules/mob/living/emote.dm:395
public partial class datum_emote_living_cough : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:420
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        return Dm.num(!HAS_TRAIT(user, TRAIT_SOOTHED_THROAT) && Dm.truthy(await base.can_run_emote(user, status_check, intentional, @params)));
    }
}

// tg: code/modules/mob/living/emote.dm:435
public partial class datum_emote_living_scream : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:466
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        intentional ??= FALSE;
        if (!Dm.truthy(intentional) && HAS_TRAIT(user, TRAIT_ANALGESIA))
            return null;
        return await base.run_emote(user, @params, type_override, intentional);
    }

    // tg: code/modules/mob/living/emote.dm:471
    public override object select_message_type(mob user = null, object msg = null, object intentional = null)
    {
        object dot = base.select_message_type(user, msg, intentional);
        if (!Dm.truthy(intentional) && isanimal_or_basicmob(user))
            return I18n.T("makes a loud and pained whimper.");
        return dot;
    }

    // tg: code/modules/mob/living/emote.dm:476
    public override object get_sound(mob_living user = null)
    {
        object dot = base.get_sound(user);
        if (!ishuman(user) || !Dm.truthy(dot))
            return dot;
        var humie = (mob_living_carbon_human)user;
        //Rare chance for this banger of a classic to play instead of the avarage joe screams
        if (Dm.eq(humie.physique, MALE) && prob(1) && ishumanbasic(humie))
            return Dm.resource("sound/mobs/humanoids/human/scream/wilhelm_scream.ogg");
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:495
public partial class datum_emote_living_shiver : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:500  #define SHIVER_LOOP_DURATION (1 SECONDS)
    private const float SHIVER_LOOP_DURATION = 1 * SECONDS;

    // tg: code/modules/mob/living/emote.dm:501
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Dm.animate(user, Dm.named("pixel_w", 1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        for (int i = 1; i <= SHIVER_LOOP_DURATION / (0.2f * SECONDS); i++) //desired total duration divided by the iteration duration to give the necessary iteration count
        {
            Dm.animate(Dm.named("pixel_w", -2), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
            Dm.animate(Dm.named("pixel_w", 2), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
        }
        Dm.animate(Dm.named("pixel_w", -1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:510
public partial class datum_emote_living_sigh : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:524
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!ishuman(user))
            return dot;
        image emote_animation = image(Dm.resource("icons/mob/human/emote_visuals.dmi"), user, "sigh");
        flick_overlay_global(emote_animation, GLOB.clients, 2.0f * SECONDS);
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:591
public partial class datum_emote_living_surrender : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:597
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (isliving(user))
        {
            var living = (mob_living)user;
            await living.Paralyze(20 * SECONDS);
            living.remove_status_effect(typeof(datum_status_effect_grouped_surrender));
        }
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:604
public partial class datum_emote_living_sway : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:609
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Dm.animate(user, Dm.named("pixel_w", 2), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        for (int i = 1; i <= 2; i++)
        {
            Dm.animate(Dm.named("pixel_w", -6), Dm.named("time", 1.0f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
            Dm.animate(Dm.named("pixel_w", 6), Dm.named("time", 1.0f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
        }
        Dm.animate(Dm.named("pixel_w", -2), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:622
public partial class datum_emote_living_tremble : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:627  #define TREMBLE_LOOP_DURATION (4.4 SECONDS)
    private const float TREMBLE_LOOP_DURATION = 4.4f * SECONDS;

    // tg: code/modules/mob/living/emote.dm:628
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Dm.animate(user, Dm.named("pixel_w", 2), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        for (int i = 1; i <= TREMBLE_LOOP_DURATION / (0.4f * SECONDS); i++) //desired total duration divided by the iteration duration to give the necessary iteration count
        {
            Dm.animate(Dm.named("pixel_w", -4), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
            Dm.animate(Dm.named("pixel_w", 4), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_CONTINUE)));
        }
        Dm.animate(Dm.named("pixel_w", -2), Dm.named("time", 0.2f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:637
public partial class datum_emote_living_twitch : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:642
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Dm.animate(user, Dm.named("pixel_w", 1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        Dm.animate(Dm.named("pixel_w", -2), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        Dm.animate(Dm.named("time", 0.1f * SECONDS));
        Dm.animate(Dm.named("pixel_w", 2), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        Dm.animate(Dm.named("pixel_w", -1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:650
public partial class datum_emote_living_twitch_s : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:655
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Dm.animate(user, Dm.named("pixel_w", -1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", Dm.bitor(ANIMATION_RELATIVE, ANIMATION_PARALLEL)));
        Dm.animate(Dm.named("pixel_w", 1), Dm.named("time", 0.1f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        return dot;
    }
}

// tg: code/modules/mob/living/emote.dm:689
public partial class datum_emote_living_yawn : datum_emote_living
{
    /// The base chance for your yawn to propagate to someone else if they're on the same tile as you
    // tg: code/modules/mob/living/emote.dm:685  #define YAWN_PROPAGATE_CHANCE_BASE 20
    private const float YAWN_PROPAGATE_CHANCE_BASE = 20;
    /// The amount the base chance to propagate yawns falls for each tile of distance
    // tg: code/modules/mob/living/emote.dm:687  #define YAWN_PROPAGATE_CHANCE_DECAY 4
    private const float YAWN_PROPAGATE_CHANCE_DECAY = 4;

    // tg: code/modules/mob/living/emote.dm:700
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        if (!isliving(user))
            return dot;

        if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(user, COOLDOWN_YAWN_PROPAGATION)))
        {
            // TIMER_COOLDOWN_START (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
            LAZYSET(ref user.cooldowns, COOLDOWN_YAWN_PROPAGATION, _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), user, COOLDOWN_YAWN_PROPAGATION), cooldown * 3, file: "code/modules/mob/living/emote.dm", line: 706));
        }

        if (Dm.truthy(Dm.bitand((user as mob_living_carbon)?.obscured_slots, HIDEFACE)))
            return dot; // if your face is obscured, skip propagation

        float propagation_distance = user.client != null ? 5 : 2; // mindless mobs are less able to spread yawns
        foreach (mob_living iter_living in Dm.each<mob_living>(view(user, propagation_distance)))
        {
            if (Dm.truthy(iter_living.incapacitated) || Dm.truthy(TIMER_COOLDOWN_RUNNING(iter_living, COOLDOWN_YAWN_PROPAGATION)))
                continue;
            float dist_between = Dm.num(get_dist(user, iter_living));
            bool recently_examined = false; // if you yawn just after someone looks at you, it forces them to yawn as well. Tradecraft!
            if (iter_living.client != null)
            {
                object examine_time = LAZYACCESS(iter_living.client?.recent_examines, user);
                if (Dm.truthy(examine_time) && (Dm.num(world.time) - Dm.num(examine_time) < YAWN_PROPAGATION_EXAMINE_WINDOW))
                    recently_examined = true;
            }
            if (!recently_examined && !prob(YAWN_PROPAGATE_CHANCE_BASE - (YAWN_PROPAGATE_CHANCE_DECAY * dist_between)))
                continue;
            float yawn_delay = Dm.num(rand(0.2f * SECONDS, 0.7f * SECONDS)) * dist_between;
            _addtimer(Dm.@new<datum_callback>(this, nameof(propagate_yawn), iter_living), yawn_delay, file: "code/modules/mob/living/emote.dm", line: 724);
        }
        return dot;
    }

    /// This yawn has been triggered by someone else yawning specifically, likely after a delay. Check again if they don't have the yawned recently trait
    // tg: code/modules/mob/living/emote.dm:731
    public virtual async DmTask propagate_yawn(mob user = null)
    {
        if (user == null || Dm.truthy(TIMER_COOLDOWN_RUNNING(user, COOLDOWN_YAWN_PROPAGATION)))
            return;
        await user.emote("yawn");
    }
}

// tg: code/modules/mob/living/emote.dm:746
public partial class datum_emote_living_custom : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:752
    public override async DmTask<object> can_run_emote(mob user = null, float? status_check = null, float? intentional = null, object @params = null)
    {
        object dot = await base.can_run_emote(user, status_check, intentional, @params);
        if (!Dm.truthy(dot) || !Dm.truthy(intentional))
            return FALSE;
        // tg: :756-758  is_banned_from(user.ckey, "Emote"): the port has no ban database (as mob_say.cs), nobody is banned
        if (QDELETED(user))
            return FALSE;
        // tg: :761-763  user.client.prefs.muted & MUTE_IC: the admin mutes are M10's admin tools; nobody is muted
        return dot;
    }

    // tg: code/modules/mob/living/emote.dm:775
    public static ref regex stop_bad_mime => ref TgState.Current.datum_emote_living_custom__stop_bad_mime;

    // tg: code/modules/mob/living/emote.dm:768
    public virtual async DmTask<object> emote_is_valid(mob user = null, object input = null)
    {
        // We're assuming clientless mobs custom emoting is something codebase-driven and not player-driven.
        // If players ever get the ability to force clientless mobs to emote, we'd need to reconsider this.
        if (user.client == null)
            return TRUE;
        // tg: :773  CAN_BYPASS_FILTER(user): the admins' bypass (M10); nobody bypasses
        stop_bad_mime ??= Dm.regex("says|exclaims|yells|asks");
        if (Dm.truthy(stop_bad_mime.Find(input, 1, 1)))
        {
            to_chat(user, span_danger(I18n.T("Invalid emote.")));
            return FALSE;
        }
        // tg: :780-801  the IC word filters (is_ic_filtered, is_soft_ic_filtered, their warnings, logs and soft alert): the port has
        // no config/word_filter.toml, so tg's filters match nothing and these branches never run (as mob_say.cs)
        return TRUE;
    }

    // tg: code/modules/mob/living/emote.dm:804
    public override object get_message_flags(object intentional = null)
    {
        object dot = base.get_message_flags(intentional);
        return Dm.bitor(dot, WITH_EMPHASIS_MESSAGE);
    }

    // tg: code/modules/mob/living/emote.dm:808
    public virtual async DmTask<object> get_custom_emote_from_user()
    {
        return copytext(sanitize(await Dm.input("Choose an emote to display.")), 1, MAX_MESSAGE_LEN);
    }

    // tg: code/modules/mob/living/emote.dm:811
    public virtual async DmTask<object> get_custom_emote_type_from_user()
    {
        object type = await Dm.input("Is this a visible or hearable emote?", Dm.list("Visible", "Hearable", "Both"));
        switch (type)
        {
            case "Visible":
                return EMOTE_VISIBLE;
            case "Hearable":
                return EMOTE_AUDIBLE;
            case "Both":
                return EMOTE_VISIBLE | EMOTE_AUDIBLE;
            default:
                await tgui_alert(usr, I18n.T("Unable to use this emote, must be either hearable or visible."));
                return FALSE;
        }
    }

    // tg: code/modules/mob/living/emote.dm:825
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        intentional ??= FALSE;
        object our_message = Dm.truthy(@params) ? @params : await get_custom_emote_from_user();
        if (!Dm.truthy(await emote_is_valid(user, our_message)))
            return FALSE;
        if (!Dm.truthy(@params))
        {
            object user_emote_type = await get_custom_emote_type_from_user();
            if (!Dm.truthy(user_emote_type))
                return FALSE;
            type_override = user_emote_type;
        }
        return await base.run_emote(user: user, @params: our_message, type_override: type_override, intentional: intentional);
    }

    // tg: code/modules/mob/living/emote.dm:837
    public override object replace_pronoun(mob user = null, object msg = null)
    {
        return msg;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/emote.dm:775  var/static/regex/stop_bad_mime
    internal regex datum_emote_living_custom__stop_bad_mime;
}

// tg: code/modules/mob/living/emote.dm:266
public partial class datum_emote_living_kiss : datum_emote_living
{
    // tg: code/modules/mob/living/emote.dm:271
    public override async DmTask<object> run_emote(mob user = null, object @params = null, object type_override = null, float? intentional = null)
    {
        object dot = await base.run_emote(user, @params, type_override, intentional);
        Type kiss_type = typeof(obj_item_hand_item_kisser);

        if (HAS_TRAIT(user, TRAIT_GARLIC_BREATH))
            kiss_type = typeof(obj_item_hand_item_kisser_french);

        if (HAS_TRAIT(user, TRAIT_CHEF_KISS))
            kiss_type = typeof(obj_item_hand_item_kisser_chef);

        if (HAS_TRAIT(user, TRAIT_SYNDIE_KISS))
            kiss_type = typeof(obj_item_hand_item_kisser_syndie);

        if (HAS_TRAIT(user, TRAIT_KISS_OF_DEATH))
            kiss_type = typeof(obj_item_hand_item_kisser_death);

        var ink_action = Dm.locate<datum_action_cooldown_ink_spit>(user.actions);
        if (ink_action != null && Dm.truthy(await ink_action.IsAvailable()))
            kiss_type = typeof(obj_item_hand_item_kisser_ink);
        else
            ink_action = null;

        var kiss_blower = Dm.@new(kiss_type, user) as obj_item;
        if (Dm.truthy(await user.put_in_hands(kiss_blower)))
        {
            to_chat(user, span_notice(I18n.T("You ready your kiss-blowing hand.")));
            if (ink_action != null)
                Dm.NotTranslated("code/modules/mob/living/emote.dm:296", "run_emote: ink_action.StartCooldown(), the ink spit's cooldown (the cooldown actions, with the mutations)");
            return dot;
        }

        qdel(kiss_blower);
        to_chat(user, span_warning(I18n.T("You're incapable of blowing a kiss in your current state.")));
        return dot;
    }
}
