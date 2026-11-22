// Translated from tgstation (snapshot 1d99bd7f): code/datums/brain_damage/special.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the violent psychosis came with syndicate X3b, D316; the rest with the brain traumas, gaps B1, D327): the special
// traumas, the bluespace stream and the securitron only their owner sees, and the veil of non-existence. The var blocks are
// generated (D111). The mood events and the combat PTSD's sanity are live since gaps MS1.

//Brain traumas that are rare and/or somewhat beneficial;
//they are the easiest to cure, which means that if you want
//to keep them, you can't cure your other traumas
// tg: code/datums/brain_damage/special.dm:4
[DmPath("/datum/brain_trauma/special")]
public partial class datum_brain_trauma_special : datum_brain_trauma
{
}

// tg: code/datums/brain_damage/special.dm:7
public partial class datum_brain_trauma_special_godwoken : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:16
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (SPT_PROB(2, seconds_per_tick))
        {
            if (prob(33) && (Dm.truthy(owner.IsStun()) || Dm.truthy(owner.IsParalyzed()) || Dm.truthy(owner.IsUnconscious())))
                await speak("unstun", TRUE);
            else if (prob(60) && owner.health <= owner.crit_threshold)
                await speak("heal", TRUE);
            else if (prob(30) && Dm.truthy(owner.combat_mode))
                await speak("aggressive");
            else
                await speak("neutral", Dm.num(prob(25)));
        }
    }

    // tg: code/datums/brain_damage/special.dm:28
    public override async DmTask<object> on_gain()
    {
        ADD_TRAIT(owner, TRAIT_HOLY, TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/special.dm:32
    public override async DmTask on_lose(object silent = null)
    {
        REMOVE_TRAIT(owner, TRAIT_HOLY, TRAUMA_TRAIT);
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/special.dm:36
    public virtual async DmTask speak(object type = null, float? include_owner = null)
    {
        include_owner ??= FALSE;
        object message = null;
        switch (Dm.str(type))
        {
            case "unstun":
                message = pick_list_replacements(BRAIN_DAMAGE_FILE, "god_unstun");
                break;
            case "heal":
                message = pick_list_replacements(BRAIN_DAMAGE_FILE, "god_heal");
                break;
            case "neutral":
                message = pick_list_replacements(BRAIN_DAMAGE_FILE, "god_neutral");
                break;
            case "aggressive":
                message = pick_list_replacements(BRAIN_DAMAGE_FILE, "god_aggressive");
                break;
            default:
                message = pick_list_replacements(BRAIN_DAMAGE_FILE, "god_neutral");
                break;
        }

        playsound(get_turf(owner), Dm.resource("sound/effects/magic/clockwork/invoke_general.ogg"), 200, TRUE, 5);
        await voice_of_god(message, owner, Dm.list("colossus", "yell"), 2.5f, include_owner, name, TRUE);
    }
}

// tg: code/datums/brain_damage/special.dm:53
public partial class datum_brain_trauma_special_bluespace_prophet : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:64
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (!(portal_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;

        portal_cooldown = world.time + 10 * SECONDS; // COOLDOWN_START, inline
        DmList possible_turfs = Dm.list();
        foreach (turf T in Dm.each_cast<turf>(RANGE_TURFS(8, owner)))
        {
            if (Dm.truthy(T.density))
                continue;

            bool clear = true;
            foreach (obj O in Dm.each<obj>(T))
            {
                if (Dm.truthy(O.density))
                {
                    clear = false;
                    break;
                }
            }
            if (clear)
                Dm.append(possible_turfs, T);
        }

        if (!Dm.truthy(LAZYLEN(possible_turfs)))
            return;

        turf first_turf = pick(possible_turfs) as turf;
        if (first_turf == null)
            return;

        Dm.remove(possible_turfs, Dm.bitand(possible_turfs, range(first_turf, 3)));

        turf second_turf = pick(possible_turfs) as turf;
        if (second_turf == null)
            return;

        obj_effect_client_image_holder_bluespace_stream first = Dm.@new<obj_effect_client_image_holder_bluespace_stream>(first_turf, owner);
        obj_effect_client_image_holder_bluespace_stream second = Dm.@new<obj_effect_client_image_holder_bluespace_stream>(second_turf, owner);

        first.linked_to = second;
        second.linked_to = first;
        await DmTask.CompletedTask;
    }
}

// tg: code/datums/brain_damage/special.dm:101
public partial class obj_effect_client_image_holder_bluespace_stream : obj_effect_client_image_holder
{
    // tg: code/datums/brain_damage/special.dm:109
    [DmParams("mapload", "mobs_which_see_us")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 30 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 30 * SECONDS, file: "code/datums/brain_damage/special.dm", line: 111); // QDEL_IN(src, 30 SECONDS), inline (rules 11.1 (f))
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:113
    public override object generate_image()
    {
        image dot = base.generate_image() as image;
        apply_wibbly_filters(dot);
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:117
    public override object Destroy(float? force = null)
    {
        if (!QDELETED(linked_to))
            qdel(linked_to);
        linked_to = null;
        return base.Destroy(force);
    }

    // tg: code/datums/brain_damage/special.dm:123
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.@in(user, who_sees_us) || linked_to == null)
            return dot;

        object slip_in_message = pick(I18n.T("slides sideways in an odd way, and disappears"), I18n.T("jumps into an unseen dimension"),
            I18n.T("sticks one leg straight out, wiggles [] foot, and is suddenly gone", user.p_their()), I18n.T("stops, then blinks out of reality"),
            I18n.T("is pulled into an invisible vortex, vanishing from sight"));
        object slip_out_message = pick(I18n.T("silently fades in"), I18n.T("leaps out of thin air"), I18n.T("appears"), I18n.T("walks out of an invisible doorway"),
            I18n.T("slides out of a fold in spacetime"));

        to_chat(user, span_notice(I18n.T("You try to align with the bluespace stream...")));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
            return dot;

        turf source_turf = get_turf(this);
        turf destination_turf = get_turf(linked_to);

        Dm.@new<obj_effect_temp_visual_bluespace_fissure>(source_turf);
        Dm.@new<obj_effect_temp_visual_bluespace_fissure>(destination_turf);

        user.visible_message(span_warning(I18n.T("[] [].", user, slip_in_message)), ignored_mobs: user);

        if (Dm.truthy(await do_teleport(user, destination_turf, no_effects: TRUE)))
            user.visible_message(span_warning(I18n.T("[] [].", user, slip_out_message)), span_notice(I18n.T("...and find your way to the other side.")));
        else
            user.visible_message(span_warning(I18n.T("[] [], ending up exactly where they left.", user, slip_out_message)), span_notice(I18n.T("...and find yourself where you started?")));
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:155
    public override async DmTask<object> attack_tk(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("\\The [] actively rejects your mind, and the bluespace energies surrounding it disrupt your telekinesis!", this)));
        await DmTask.CompletedTask;
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}

// tg: code/datums/brain_damage/special.dm:159
public partial class datum_brain_trauma_special_quantum_alignment : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:173
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (Dm.truthy(linked))
        {
            if (QDELETED(linked_target))
            {
                linked_target = null;
                linked = FALSE;
                return;
            }
            if (!Dm.truthy(returning) && snapback_cooldown <= world.time) // COOLDOWN_FINISHED, inline
                start_snapback();
            return;
        }
        if (SPT_PROB(2, seconds_per_tick))
            await try_entangle();
    }

    // tg: code/datums/brain_damage/special.dm:185
    public virtual async DmTask try_entangle()
    {
        //Check for pulled mobs
        if (ismob(owner.pulling))
        {
            entangle(owner.pulling);
            return;
        }
        //Check for adjacent mobs
        foreach (mob_living L in Dm.each<mob_living>(oview(1, owner)))
        {
            if (Dm.truthy(await owner.Adjacent(L)))
            {
                entangle(L);
                return;
            }
        }
        //Check for pulled objects
        if (isobj(owner.pulling))
        {
            entangle(owner.pulling);
            return;
        }

        //Check main hand
        obj_item held_item = owner.get_active_held_item() as obj_item;
        if (held_item != null && !(HAS_TRAIT(held_item, TRAIT_NODROP)))
        {
            entangle(held_item);
            return;
        }

        //Check off hand
        held_item = owner.get_inactive_held_item() as obj_item;
        if (held_item != null && !(HAS_TRAIT(held_item, TRAIT_NODROP)))
        {
            entangle(held_item);
            return;
        }

        //Just entangle with the turf
        entangle(get_turf(owner));
    }

    // tg: code/datums/brain_damage/special.dm:215
    public virtual void entangle(atom target = null)
    {
        to_chat(owner, span_notice(I18n.T("You start feeling a strong sense of connection to [].", target)));
        linked_target = target;
        linked = TRUE;
        snapback_cooldown = world.time + rand(45 * SECONDS, 10 * MINUTES); // COOLDOWN_START, inline
    }

    // tg: code/datums/brain_damage/special.dm:221
    public virtual void start_snapback()
    {
        if (QDELETED(linked_target))
        {
            linked_target = null;
            linked = FALSE;
            return;
        }
        to_chat(owner, span_warning(I18n.T("Your connection to [] suddenly feels extremely strong... you can feel it pulling you!", linked_target)));
        owner.playsound_local(owner, Dm.resource("sound/effects/magic/lightning_chargeup.ogg"), 75, FALSE);
        returning = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(snapback)), 10 * SECONDS, file: "code/datums/brain_damage/special.dm", line: 229);
    }

    // tg: code/datums/brain_damage/special.dm:231
    public virtual async DmTask snapback()
    {
        returning = FALSE;
        if (QDELETED(linked_target))
        {
            to_chat(owner, span_notice(I18n.T("The connection fades abruptly, and the pull with it.")));
            linked_target = null;
            linked = FALSE;
            return;
        }
        to_chat(owner, span_warning(I18n.T("You're pulled through spacetime!")));
        await do_teleport(owner, get_turf(linked_target), null, channel: TELEPORT_CHANNEL_QUANTUM);
        owner.playsound_local(owner, Dm.resource("sound/effects/magic/repulse.ogg"), 100, FALSE);
        linked_target = null;
        linked = FALSE;
    }
}

// tg: code/datums/brain_damage/special.dm:244
public partial class datum_brain_trauma_special_psychotic_brawling : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:255
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        psychotic_brawling = Dm.@new<datum_martial_art_psychotic_brawling>(this);
        psychotic_brawling.locked_to_use = TRUE;
        await psychotic_brawling.teach(owner);
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:261
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        QDEL_NULL(ref psychotic_brawling);
    }
}

// tg: code/datums/brain_damage/special.dm:269
public partial class datum_brain_trauma_special_tenacity : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:278
    public override async DmTask<object> on_gain()
    {
        owner.add_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT, TRAIT_ANALGESIA), TRAUMA_TRAIT);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/special.dm:282
    public override async DmTask on_lose(object silent = null)
    {
        owner.remove_traits(Dm.list(TRAIT_NOSOFTCRIT, TRAIT_NOHARDCRIT, TRAIT_ANALGESIA), TRAUMA_TRAIT);
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/special.dm:286
public partial class datum_brain_trauma_special_death_whispers : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:296
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (!Dm.truthy(active) && prob(2))
            whispering();
    }

    // tg: code/datums/brain_damage/special.dm:301
    public override async DmTask on_lose(object silent = null)
    {
        if (Dm.truthy(active))
            cease_whispering();
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/special.dm:306
    public virtual void whispering()
    {
        ADD_TRAIT(owner, TRAIT_SIXTHSENSE, TRAUMA_TRAIT);
        active = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cease_whispering)), rand(5 * SECONDS, 30 * SECONDS), file: "code/datums/brain_damage/special.dm", line: 309);
    }

    // tg: code/datums/brain_damage/special.dm:311
    public virtual void cease_whispering()
    {
        REMOVE_TRAIT(owner, TRAIT_SIXTHSENSE, TRAUMA_TRAIT);
        active = FALSE;
    }
}

// tg: code/datums/brain_damage/special.dm:315
public partial class datum_brain_trauma_special_existential_crisis : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:327
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (veil == null && crisis_cooldown <= world.time && SPT_PROB(1.5f, seconds_per_tick)) // COOLDOWN_FINISHED, inline
        {
            if (isturf(owner.loc))
                await fade_out();
        }
    }

    // tg: code/datums/brain_damage/special.dm:333
    public override async DmTask on_lose(object silent = null)
    {
        if (veil != null)
            fade_in();
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/special.dm:338
    public virtual async DmTask fade_out()
    {
        if (veil != null)
            return;
        float duration = rand(5 * SECONDS, 45 * SECONDS);
        veil = Dm.@new<obj_effect_abstract_sync_holder_veil>(owner.drop_location());
        to_chat(owner, span_warning(Dm.text("[]", pick(Dm.list(
                I18n.T("Do you even exist?"),
                I18n.T("To be or not to be..."),
                I18n.T("Why exist?"),
                I18n.T("You simply fade away."),
                I18n.T("You stop keeping it real."),
                I18n.T("You stop thinking for a moment. Therefore you are not."))))));
        await owner.forceMove(veil);
        crisis_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START, inline
        _addtimer(Dm.@new<datum_callback>(this, nameof(fade_in)), duration, file: "code/datums/brain_damage/special.dm", line: 353);
    }

    // tg: code/datums/brain_damage/special.dm:355
    public virtual void fade_in()
    {
        QDEL_NULL(ref veil);
        to_chat(owner, span_notice(I18n.T("You fade back into reality.")));
        crisis_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START, inline
    }
}

//base sync holder is in desynchronizer.dm
// tg: code/datums/brain_damage/special.dm:361 (/obj/effect/abstract/sync_holder/veil: a var block only, generated)

// tg: code/datums/brain_damage/special.dm:365
public partial class datum_brain_trauma_special_beepsky : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:376
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref beepsky);
        return base.Destroy(force);
    }

    // tg: code/datums/brain_damage/special.dm:380
    public override async DmTask<object> on_gain()
    {
        create_securitron();
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/special.dm:384
    public virtual void create_securitron()
    {
        QDEL_NULL(ref beepsky);
        turf where = locate(owner.x + Dm.num(pick(-12, 12)), owner.y + Dm.num(pick(-12, 12)), owner.z);
        beepsky = Dm.@new<obj_effect_client_image_holder_securitron>(where, owner);
    }

    // tg: code/datums/brain_damage/special.dm:389
    public override async DmTask on_lose(object silent = null)
    {
        QDEL_NULL(ref beepsky);
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/special.dm:393
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (QDELETED(beepsky) || beepsky.loc == null || beepsky.z != owner.z)
        {
            if (prob(30))
                create_securitron();
            else
                return;
        }

        if (get_dist(owner, beepsky) >= 10 && prob(20))
            create_securitron();

        if (IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            if (prob(20))
                owner.playsound_local(beepsky, Dm.resource("sound/mobs/non-humanoids/beepsky/iamthelaw.ogg"), 50);
            return;
        }

        if (get_dist(owner, beepsky) <= 1)
        {
            owner.playsound_local(owner, Dm.resource("sound/items/weapons/egloves.ogg"), 50);
            owner.visible_message(span_warning(I18n.T("[]'s body jerks as if it was shocked.", owner)), span_userdanger(I18n.T("You feel the fist of the LAW.")));
            await owner.adjust_stamina_loss(rand(40, 70));
            QDEL_NULL(ref beepsky);
        }

        if (prob(20) && get_dist(owner, beepsky) <= 8)
            owner.playsound_local(beepsky, Dm.resource("sound/mobs/non-humanoids/beepsky/criminal.ogg"), 40);
    }
}

// tg: code/datums/brain_damage/special.dm:417
public partial class obj_effect_client_image_holder_securitron : obj_effect_client_image_holder
{
    // tg: code/datums/brain_damage/special.dm:423
    [DmParams("mapload", "mobs_which_see_us")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.str(pick(I18n.T("Officer Beepsky"), I18n.T("Officer Johnson"), I18n.T("Officer Pingsky")));
        START_PROCESSING(SSfastprocess, this);
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:428
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        return base.Destroy(force);
    }

    // tg: code/datums/brain_damage/special.dm:432
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (prob(40))
            return null;

        mob victim = pick(who_sees_us) as mob;
        Waiting.NoSleep(forceMove(get_step_towards(this, victim)));
        if (prob(5))
        {
            string beepskys_cry = I18n.T("Level 10 infraction alert!");
            to_chat(victim, I18n.T("[] exclaims, \"[]", span_name(Dm.text("[]", name)), span_robot(Dm.text("[]", beepskys_cry))));
            if (Dm.truthy(victim.client?.prefs.read_preference(typeof(datum_preference_toggle_enable_runechat))))
                victim.create_chat_message(this, raw_message: beepskys_cry, spans: Dm.list(SPAN_ROBOT));
        }
        return null;
    }
}

// Used by Veteran Security Advisor job.
// tg: code/datums/brain_damage/special.dm:445
public partial class datum_brain_trauma_special_ptsd : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:473
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return;

        if (!(ptsd_hallucinations <= world.time)) // COOLDOWN_FINISHED, inline
            return;

        await owner.cause_hallucination(pick(ptsd_hallucinations_list), "Caused by The Combat PTSD brain trauma");
        ptsd_hallucinations = world.time + rand(10 * SECONDS, 10 * MINUTES); // COOLDOWN_START, inline
    }

    // tg: code/datums/brain_damage/special.dm:483
    public override async DmTask<object> on_gain()
    {
        await owner.add_mood_event("combat_ptsd", typeof(datum_mood_event_desentized));
        if (owner.mob_mood != null)
            owner.mob_mood.mood_modifier -= 1; //Basically nothing can change your mood
        if (owner.mob_mood != null) //Makes sanity on a unstable level unless cured
            await owner.mob_mood.set_sanity(SANITY_DISTURBED, @override: TRUE);
        await owner.apply_status_effect(typeof(datum_status_effect_desensitized), REF(this), DESENSITIZED_THRESHOLD);
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/special.dm:490
    public override async DmTask on_lose(object silent = null)
    {
        owner.clear_mood_event("combat_ptsd");
        if (owner.mob_mood != null)
            owner.mob_mood.mood_modifier += 1;
        if (owner.mob_mood != null)
            await owner.mob_mood.set_sanity(SANITY_GREAT, @override: TRUE);
        owner.remove_status_effect(typeof(datum_status_effect_desensitized), REF(this));
        await base.on_lose(silent);
    }
}

// tg: code/datums/brain_damage/special.dm:497
public partial class datum_brain_trauma_special_primal_instincts : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:510
    public override async DmTask<object> on_gain()
    {
        object dot = await base.on_gain();
        if (owner.ai_controller != null)
        {
            old_ai_controller_type = owner.ai_controller.GetType();
            qdel(owner.ai_controller); // QDEL_NULL(owner.ai_controller)
            owner.ai_controller = null;
        }

        datum_ai_controller controller = Dm.@new<datum_ai_controller_monkey>(owner);
        owner.ai_controller = controller;
        controller.continue_processing_when_client = TRUE;
        controller.ai_traits = Dm.bitor(controller.ai_traits, RUN_WHILE_UNWATCHED);
        await controller.force_ai_off();
        return dot;
    }

    // tg: code/datums/brain_damage/special.dm:521
    public override async DmTask on_lose(object silent = null)
    {
        await base.on_lose(silent);
        if (QDELING(owner))
            return;

        qdel(owner.ai_controller); // QDEL_NULL(owner.ai_controller)
        owner.ai_controller = null;
        if (Dm.truthy(old_ai_controller_type))
            owner.ai_controller = Dm.@new((Type)old_ai_controller_type, owner);
        owner.remove_language(typeof(datum_language_monkey), UNDERSTOOD_LANGUAGE, TRAUMA_TRAIT);
    }

    // tg: code/datums/brain_damage/special.dm:531
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (owner.ai_controller == null)
        {
            qdel(this);
            return;
        }

        if (!SPT_PROB(3, seconds_per_tick))
            return;

        datum_ai_controller controller = (datum_ai_controller)owner.ai_controller;
        owner.grant_language(typeof(datum_language_monkey), UNDERSTOOD_LANGUAGE, TRAUMA_TRAIT);
        controller.set_blackboard_key(BB_MONKEY_AGGRESSIVE, Dm.num(prob(75)));
        if (Dm.eq(controller.ai_status, AI_STATUS_OFF))
        {
            await controller.clear_forced_off();
            owner.log_message(Dm.text("became controlled by monkey instincts ([])", Dm.truthy(controller.blackboard[BB_MONKEY_AGGRESSIVE]) ? "aggressive" : "docile"), LOG_ATTACK, color: "orange");
            to_chat(owner, span_warning(I18n.T("You feel the urge to act on your primal instincts...")));
        }
        // extend original timer if we roll the effect while it's already ongoing
        _addtimer(Dm.@new<datum_callback>(this, nameof(primal_instincts_off)), rand(20 * SECONDS, 40 * SECONDS), Dm.bitor(Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_NO_HASH_WAIT), TIMER_OVERRIDE), TIMER_DELETE_ME), file: "code/datums/brain_damage/special.dm", line: 546);
    }

    // tg: code/datums/brain_damage/special.dm:548
    public virtual async DmTask primal_instincts_off()
    {
        await ((datum_ai_controller)owner.ai_controller).force_ai_off();
        owner.remove_language(typeof(datum_language_monkey), UNDERSTOOD_LANGUAGE, TRAUMA_TRAIT);
        to_chat(owner, span_green(I18n.T("The urge subsides.")));
    }
}

// tg: code/datums/brain_damage/special.dm:553
public partial class datum_brain_trauma_special_axedoration : datum_brain_trauma_special
{
    // tg: code/datums/brain_damage/special.dm:562  var/static/list/talk_lines (rules 10.6)
    public static ref DmList talk_lines => ref TgState.Current.datum_brain_trauma_special_axedoration__talk_lines;

    // tg: code/datums/brain_damage/special.dm:573  var/static/list/hurt_lines (rules 10.6)
    public static ref DmList hurt_lines => ref TgState.Current.datum_brain_trauma_special_axedoration__hurt_lines;

    // tg: code/datums/brain_damage/special.dm:586
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(owner))
            return;

        if (GLOB.bridge_axe == null)
        {
            if (SPT_PROB(0.5f, seconds_per_tick))
            {
                to_chat(owner, span_warning(I18n.T("I've failed my duty...")));
                await owner.set_jitter_if_lower(5 * SECONDS);
                await owner.set_stutter_if_lower(5 * SECONDS);
                if (SPT_PROB(20, seconds_per_tick))
                    await owner.vomit(VOMIT_CATEGORY_DEFAULT);
            }
            return;
        }

        atom axe_location = get_axe_location() as atom;
        if (!SPT_PROB(1.5f, seconds_per_tick))
            return;
        if (isliving(axe_location))
        {
            mob_living axe_holder = (mob_living)axe_location;
            if (axe_holder == owner)
            {
                await talk_tuah(Dm.str(pick(talk_lines)));
                return;
            }
            datum_job holder_job = axe_holder.mind?.assigned_role as datum_job;
            if (holder_job != null && (Dm.@in(typeof(datum_job_department_command), holder_job.departments_list)))
            {
                to_chat(owner, span_notice(I18n.T("I hope the axe is in good hands...")));
                await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_neutral));
                return;
            }
            to_chat(owner, span_warning(I18n.T("You start having a bad feeling...")));
            await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_missing));
            return;
        }

        if (!isarea(axe_location))
        {
            await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_gone));
            return;
        }

        if (istype(axe_location, typeof(area_station_command)))
        {
            to_chat(owner, span_notice(I18n.T("You feel a sense of relief...")));
            if (istype(GLOB.bridge_axe.loc, typeof(obj_structure_fireaxecabinet)))
                return;
            await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_neutral));
            return;
        }

        to_chat(owner, span_warning(I18n.T("You start having a bad feeling...")));
        await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_missing));
    }

    // tg: code/datums/brain_damage/special.dm:630
    public override async DmTask<object> on_gain()
    {
        RegisterSignal(owner, COMSIG_MOB_EQUIPPED_ITEM, nameof(on_equip));
        RegisterSignal(owner, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(on_unequip));
        RegisterSignal(owner, COMSIG_MOB_EXAMINING, nameof(on_examine));
        if (GLOB.bridge_axe == null)
        {
            axe_gone();
            return await base.on_gain();
        }
        RegisterSignal(GLOB.bridge_axe, COMSIG_QDELETING, nameof(axe_gone));
        if (istype(get_axe_location(), typeof(area_station_command)) && istype(GLOB.bridge_axe.loc, typeof(obj_structure_fireaxecabinet)))
            await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_cabinet));
        else if (Dm.truthy(owner.is_holding(GLOB.bridge_axe)))
            on_equip(owner, GLOB.bridge_axe);
        else
            await owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_neutral));
        RegisterSignal(GLOB.bridge_axe, COMSIG_ITEM_AFTERATTACK, nameof(on_axe_attack));
        return await base.on_gain();
    }

    // tg: code/datums/brain_damage/special.dm:648
    public override async DmTask on_lose(object silent = null)
    {
        owner.clear_mood_event("fireaxe");
        UnregisterSignal(owner, Dm.list(COMSIG_MOB_EQUIPPED_ITEM, COMSIG_MOB_UNEQUIPPED_ITEM, COMSIG_MOB_EXAMINING));
        if (GLOB.bridge_axe != null)
            UnregisterSignal(GLOB.bridge_axe, COMSIG_ITEM_AFTERATTACK);
        await base.on_lose(silent);
    }

    // tg: code/datums/brain_damage/special.dm:655
    [SignalHandler]
    public virtual void axe_gone(object source = null)
    {
        to_chat(owner, span_danger(I18n.T("You feel a great disturbance in the force.")));
        Waiting.NoSleep(owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_gone)));
        Waiting.NoSleep(owner.set_jitter_if_lower(15 * SECONDS));
        Waiting.NoSleep(owner.set_stutter_if_lower(15 * SECONDS));
    }

    // tg: code/datums/brain_damage/special.dm:662
    [SignalHandler]
    public virtual void on_equip(object source = null, obj_item picked_up = null, object slot = null)
    {
        if (!istype(picked_up, typeof(obj_item_fireaxe)))
            return;
        Waiting.NoSleep(owner.set_jitter_if_lower(3 * SECONDS));
        if (picked_up == GLOB.bridge_axe)
        {
            to_chat(owner, span_hypnophrase(I18n.T("I have it. It's time to put it back.")));
            Waiting.NoSleep(owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_held)));
            return;
        }
        ADD_TRAIT(picked_up, TRAIT_NODROP, GetType());
        to_chat(owner, span_warning(I18n.T("...This is not the one I'm looking after.")));
        Waiting.NoSleep(owner.Immobilize(2 * SECONDS));
        _addtimer(Dm.@new<datum_callback>(this, nameof(throw_faker), picked_up), 2 * SECONDS, file: "code/datums/brain_damage/special.dm", line: 674);
    }

    // tg: code/datums/brain_damage/special.dm:676
    public virtual async DmTask throw_faker(obj_item faker = null)
    {
        REMOVE_TRAIT(faker, TRAIT_NODROP, GetType());
        object held_index = owner.get_held_index_of_item(faker);
        if (!Dm.truthy(held_index))
            return;
        to_chat(owner, span_warning(I18n.T("Be gone with you.")));
        owner.swap_hand(held_index, silent: TRUE);
        turf target_turf = get_ranged_target_turf(owner, owner.dir, faker.throw_range) as turf;
        await owner.throw_item(target_turf);
    }

    // tg: code/datums/brain_damage/special.dm:686
    [SignalHandler]
    public virtual void on_unequip(datum source = null, obj_item dropped_item = null, object force = null, object new_location = null)
    {
        if (dropped_item != GLOB.bridge_axe)
            return;
        if (get_axe_location() == owner)
            return;
        if (istype(new_location, typeof(obj_structure_fireaxecabinet)))
        {
            if (istype(get_area(new_location as atom), typeof(area_station_command)))
            {
                to_chat(owner, span_nicegreen(I18n.T("Ah! Back where it belongs!")));
                Waiting.NoSleep(owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_cabinet)));
                Waiting.Detach(owner.emote("smile"));
                return;
            }
            to_chat(owner, span_warning(I18n.T("Leaving it outside of command? Am I sure about that?")));
            Waiting.NoSleep(owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_neutral)));
            return;
        }
        to_chat(owner, span_warning(I18n.T("Should I really leave it here?")));
        Waiting.NoSleep(owner.add_mood_event("fireaxe", typeof(datum_mood_event_axe_neutral)));
    }

    // tg: code/datums/brain_damage/special.dm:704
    [SignalHandler]
    public virtual void on_examine(mob source = null, atom target = null, DmList examine_strings = null, DmList examine_overrides = null)
    {
        if (!istype(target, typeof(obj_item_fireaxe)))
            return;
        if (target == GLOB.bridge_axe)
            Dm.append(examine_strings, span_notice(I18n.T("It's the axe I've sworn to protect.")));
        else
            Dm.append(examine_strings, span_warning(I18n.T("It's a simulacra, a fake axe made to fool the masses.")));
    }

    // tg: code/datums/brain_damage/special.dm:713
    [SignalHandler]
    public virtual void on_axe_attack(obj_item axe = null, atom target = null, mob user = null, DmList modifiers = null)
    {
        if (user != owner)
            return;
        Waiting.NoSleep(talk_tuah(Dm.str(pick(hurt_lines))));
    }

    // tg: code/datums/brain_damage/special.dm:719
    public virtual async DmTask talk_tuah(string sent_message = null)
    {
        sent_message ??= "Hello World.";
        await owner.Hear(GLOB.bridge_axe, owner.get_selected_language(), sent_message);
    }

    // tg: code/datums/brain_damage/special.dm:722
    public virtual object get_axe_location()
    {
        if (GLOB.bridge_axe == null)
            return null;
        atom axe_loc = GLOB.bridge_axe.loc;
        while (!ismob(axe_loc) && !isarea(axe_loc) && axe_loc != null)
            axe_loc = axe_loc.loc;
        return axe_loc;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/brain_damage/special.dm:562  /datum/brain_trauma/special/axedoration var/static/list/talk_lines
    internal DmList datum_brain_trauma_special_axedoration__talk_lines = Dm.list(
        I18n.T("I'm proud of you."),
        I18n.T("I believe in you!"),
        I18n.T("Do I bother you?"),
        I18n.T("Praise me!"),
        I18n.T("Fires burn."),
        I18n.T("We made it!"),
        I18n.T("Mother, my body disgusts me."),
        I18n.T("There's a gap where we meet, where I end and you begin."),
        I18n.T("Humble yourself."));

    // tg: code/datums/brain_damage/special.dm:573  /datum/brain_trauma/special/axedoration var/static/list/hurt_lines
    internal DmList datum_brain_trauma_special_axedoration__hurt_lines = Dm.list(
        I18n.T("Ow!"),
        I18n.T("Ouch!"),
        I18n.T("Ack!"),
        I18n.T("It burns!"),
        I18n.T("Stop!"),
        I18n.T("Arghh!"),
        I18n.T("Please!"),
        I18n.T("End it!"),
        I18n.T("Cease!"),
        I18n.T("Ah!"));
}
