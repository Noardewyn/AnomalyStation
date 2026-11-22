// Translated from tgstation (snapshot 1d99bd7f): code/modules/power/singularity/narsie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, standing on a cult that is not ported yet: no mob is a cultist and no team exists, so its loops find nothing; the
// cult's own procs it calls are rows of docs/ai/specs/live-partial.txt. A leaf waits for what it names:
// the end-of-round cinematics (DV350: the round ends without them); a ghost's harvester shell waits for the constructs. The var block
// is generated.

/// Nar'Sie, the God of the blood cultists
// tg: code/modules/power/singularity/narsie.dm:12
public partial class obj_narsie : obj
{
    // tg: code/modules/power/singularity/narsie.dm:1
    private const int NARSIE_CHANCE_TO_PICK_NEW_TARGET = 5;
    // tg: code/modules/power/singularity/narsie.dm:2
    private const int NARSIE_CONSUME_RANGE = 12;
    // tg: code/modules/power/singularity/narsie.dm:3
    private const int NARSIE_GRAV_PULL = 10;
    // tg: code/modules/power/singularity/narsie.dm:4
    private const int NARSIE_MESMERIZE_CHANCE = 25;
    // tg: code/modules/power/singularity/narsie.dm:5
    private const int NARSIE_MESMERIZE_EFFECT = 60;
    // tg: code/modules/power/singularity/narsie.dm:6
    private const int NARSIE_SINGULARITY_SIZE = 12;
    // tg: code/modules/power/singularity/narsie.dm:8
    private const string ADMIN_WARNING_MESSAGE = "Invoking this will begin the Nar'Sie roundender. Assume that this WILL end the round in a few minutes. Are you sure?";

    // tg: code/modules/power/singularity/narsie.dm:41
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        narsie_spawn_animation();
        return dot;
    }

    // tg: code/modules/power/singularity/narsie.dm:46
    public override object Destroy(float? force = null)
    {
        if (GLOB.cult_narsie == this)
        {
            fall_of_the_harbinger();
            GLOB.cult_narsie = null;
        }

        return base.Destroy(force);
    }

    /// This proc sets up all of Nar'Sie's abilities, stats, and begins her round-ending capabilities. She does not do anything unless this proc is invoked.
    /// This is only meant to be invoked after this instance is initialized in specific pro-sumer procs, as it WILL derail the entire round.
    // tg: code/modules/power/singularity/narsie.dm:55
    public virtual async DmTask start_ending_the_round()
    {
        GLOB.cult_narsie = this;
        await SSpoints_of_interest.make_point_of_interest(this);

        singularity = WEAKREF((await AddComponent(
            typeof(datum_component_singularity),
            Dm.named("bsa_targetable", FALSE),
            Dm.named("consume_callback", Dm.@new<datum_callback>(this, nameof(consume))),
            Dm.named("consume_range", NARSIE_CONSUME_RANGE),
            Dm.named("disregard_failed_movements", TRUE),
            Dm.named("grav_pull", NARSIE_GRAV_PULL),
            Dm.named("roaming", FALSE), /* This is set once the animation finishes */
            Dm.named("singularity_size", NARSIE_SINGULARITY_SIZE))) as datum) as datum_weakref;

        send_to_playing_players(span_narsie(I18n.T("NAR'SIE HAS RISEN")));
        sound_to_playing_players("sound/music/antag/bloodcult/narsie_rises.ogg");

        area area = get_area(this) as area;
        if (area != null)
        {
            var alert_overlay = (mutable_appearance)mutable_appearance("icons/effects/cult.dmi", "ghostalertsie");
            notify_ghosts(
                I18n.T("Nar'Sie has risen in []. Reach out to the Geometer to be given a new shell for your soul.", area),
                source: this,
                header: I18n.T("Nar'Sie has risen!"),
                click_interact: TRUE,
                alert_overlay: alert_overlay);
        }

        DmList all_cults = Dm.list();

        foreach (datum_antagonist_cult cultist in Dm.each<datum_antagonist_cult>(GLOB.antagonists))
        {
            if (cultist.owner == null)
                continue;
            Dm.combine(ref all_cults, cultist.cult_team);
        }

        foreach (object _cult_team in Dm.each(all_cults))
        {
            datum_team_cult cult_team = _cult_team as datum_team_cult;
            await cult_team.set_blood_target(this, duration: INFINITY);
            datum_objective_eldergod summon_objective = Dm.locate<datum_objective_eldergod>(cult_team.objectives);
            if (summon_objective != null)
                summon_objective.summoned = TRUE;
        }

        foreach (datum_mind cult_mind in Dm.each<datum_mind>(get_antag_minds(typeof(datum_antagonist_cult))))
        {
            if (isliving(cult_mind.current))
            {
                mob_living L = cult_mind.current as mob_living;
                await L.narsie_act();
            }
        }

        foreach (mob_living_carbon player in Dm.each<mob_living_carbon>(GLOB.player_list))
        {
            if (!Dm.eq(player.stat, DEAD) && Dm.truthy(is_station_level((player.loc as atom)?.z)) && !Dm.truthy(IS_CULTIST(player)))
                souls_needed[player] = TRUE;
        }

        soul_goal = round(1 + Dm.num(LAZYLEN(souls_needed)) * 0.75f);
        Waiting.DetachSync(() => begin_the_end());
    }

    /// Cleans up all of Nar'Sie's abilities, stats, and ends her round-ending capabilities. This should only be called if `start_ending_the_round()` successfully started.
    // tg: code/modules/power/singularity/narsie.dm:111
    public virtual void fall_of_the_harbinger()
    {
        DmList all_cults = Dm.list();

        foreach (datum_antagonist_cult cultist in Dm.each<datum_antagonist_cult>(GLOB.antagonists))
        {
            if (cultist.owner == null)
                continue;
            Dm.combine(ref all_cults, cultist.cult_team);
        }

        foreach (object _cult_team in Dm.each(all_cults))
        {
            datum_team_cult cult_team = _cult_team as datum_team_cult;
            datum_objective_eldergod summon_objective = Dm.locate<datum_objective_eldergod>(cult_team.objectives);
            if (summon_objective != null)
            {
                summon_objective.summoned = FALSE;
                summon_objective.killed = TRUE;
            }
        }

        send_to_playing_players(span_narsie(span_bold(Dm.str(pick("Nooooo...", "Not die. How-", "Die. Mort-", "Sas tyen re-")))));
        sound_to_playing_players("sound/effects/magic/demon_dies.ogg", 50);
    }

    // tg: code/modules/power/singularity/narsie.dm:129
    public override object vv_get_dropdown()
    {
        object dot = base.vv_get_dropdown();
        Dm.Deferred("code/modules/power/singularity/narsie.dm:131", "vv_get_dropdown: the roundender's entry in the view variables panel (admin tooling, M10+)");
        return dot;
    }

    // tg: code/modules/power/singularity/narsie.dm:134
    public override async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        object dot = await base.vv_do_topic(href_list);

        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(href_list[VV_HK_BEGIN_NARSIE_ROUNDEND]) || !Dm.truthy(check_rights(R_FUN, show_msg: TRUE)))
            return dot;

        if (!Dm.eq(await tgui_alert(usr, ADMIN_WARNING_MESSAGE, "Begin Nar'Sie Roundender", Dm.list("I'm Sure", "Abort")), "I'm Sure"))
            return dot;

        log_admin(Dm.text("[] has triggered the Nar'Sie roundender.", key_name(usr)));
        await start_ending_the_round();
        return dot;
    }

    // tg: code/modules/power/singularity/narsie.dm:149
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        if (Dm.truthy(is_banned_from(user.ckey, ROLE_CULTIST)))
            return null;
        if (Dm.eq(await tgui_alert(user, "Do you wish to become an occult harvester?", "Become Harvester?", Dm.list("Yes", "No"), timeout: 10 * SECONDS), "Yes"))
            Dm.Deferred("code/modules/power/singularity/narsie.dm:153", "attack_ghost: make_new_construct, a harvester's shell (the cult's constructs)");
        return null;
    }

    // tg: code/modules/power/singularity/narsie.dm:155
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        return Waiting.WaitforFalse(process__body(), null); // the slot's set waitfor = FALSE (processing.dm:48)
    }

    private async DmTask<object> process__body()
    {
        datum_component_singularity singularity_component = singularity.resolve() as datum_component_singularity;

        if (singularity_component != null && (singularity_component.target == null || prob(NARSIE_CHANCE_TO_PICK_NEW_TARGET)))
            pickcultist();

        if (prob(NARSIE_MESMERIZE_CHANCE))
            await mesmerize();
        return null;
    }

    // tg: code/modules/power/singularity/narsie.dm:164
    public override async DmTask<object> Bump(atom target = null)
    {
        turf target_turf = get_turf(target);
        if (target_turf == loc)
            target_turf = get_step(target, target.dir); //please don't slam into a window like a bird, Nar'Sie
        await forceMove(target_turf);
        return null;
    }

    /// Stun people around Nar'Sie that aren't cultists
    // tg: code/modules/power/singularity/narsie.dm:171
    public virtual async DmTask mesmerize()
    {
        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(viewers(NARSIE_CONSUME_RANGE, this)))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(victim))
            {
                if (!Dm.truthy(IS_CULTIST(victim)))
                {
                    to_chat(victim, span_cult(I18n.T("You feel conscious thought crumble away in an instant as you gaze upon []...", this)));
                    await victim.apply_effect(NARSIE_MESMERIZE_EFFECT, EFFECT_STUN);
                }
            }
        }
    }

    /// Narsie rewards her cultists with being devoured first, then picks a ghost to follow.
    // tg: code/modules/power/singularity/narsie.dm:179
    public virtual void pickcultist()
    {
        DmList cultists = Dm.list();
        DmList noncultists = Dm.list();

        foreach (mob_living_carbon food in Dm.each<mob_living_carbon>(GLOB.alive_mob_list)) //we don't care about constructs or cult-Ians or whatever. cult-monkeys are fair game i guess
        {
            turf pos = get_turf(food);
            if (pos == null || !Dm.eq(pos.z, z))
                continue;

            if (Dm.truthy(IS_CULTIST(food)))
                Dm.append(ref cultists, food);
            else
                Dm.append(ref noncultists, food);

            if (Dm.truthy(cultists.len)) //cultists get higher priority
            {
                acquire(pick(cultists) as atom);
                return;
            }

            if (Dm.truthy(noncultists.len))
            {
                acquire(pick(noncultists) as atom);
                return;
            }
        }

        //no living humans, follow a ghost instead.
        foreach (mob_dead_observer ghost in Dm.each<mob_dead_observer>(GLOB.player_list))
        {
            turf pos = get_turf(ghost);
            if (pos == null || !Dm.eq(pos.z, z))
                continue;
            Dm.append(ref cultists, ghost);
        }
        if (Dm.truthy(cultists.len))
        {
            acquire(pick(cultists) as atom);
            return;
        }
    }

    /// Nar'Sie gets a taste of something, and will start to gravitate towards it
    // tg: code/modules/power/singularity/narsie.dm:212
    public virtual void acquire(atom food = null)
    {
        datum_component_singularity singularity_component = singularity.resolve() as datum_component_singularity;

        if (singularity_component == null)
            return;

        object old_target = singularity_component.target;
        if (food == old_target)
            return;

        to_chat(old_target, span_cult(I18n.T("NAR'SIE HAS LOST INTEREST IN YOU.")));
        singularity_component.target = food;
        if (ishuman(food))
            to_chat(food, span_cult(I18n.T("NAR'SIE HUNGERS FOR YOUR SOUL.")));
        else
            to_chat(food, span_cult(I18n.T("NAR'SIE HAS CHOSEN YOU TO LEAD HER TO HER NEXT MEAL.")));
    }

    /// Called to make Nar'Sie convert objects to cult stuff, or to eat
    // tg: code/modules/power/singularity/narsie.dm:230
    public virtual async DmTask consume(atom target = null)
    {
        if (isturf(target))
            await target.narsie_act();
    }

    // tg: code/modules/power/singularity/narsie.dm:234
    public virtual void narsie_spawn_animation()
    {
        Waiting.NoSleep(setDir(SOUTH));
        Dm.flick("narsie_spawn_anim", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(narsie_spawn_animation_end)), 3.5f * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 237);
    }

    // tg: code/modules/power/singularity/narsie.dm:239
    public virtual void narsie_spawn_animation_end()
    {
        datum_component_singularity singularity_component = singularity?.resolve() as datum_component_singularity;
        if (singularity_component != null)
            singularity_component.roaming = TRUE;
    }
}

public static partial class GlobalProcs
{
    /**
     * Begins the process of ending the round via cult narsie win
     * Consists of later called procs (in order of called):
     *  * [/proc/narsie_end_begin_check()]
     *  * [/proc/narsie_end_second_check()]
     *  * [/proc/narsie_start_destroy_station()]
     *  * [/proc/narsie_apocalypse()]
     *  * [/proc/narsie_last_second_win()]
     *  * [/proc/cult_ending_helper()]
     */
    // tg: code/modules/power/singularity/narsie.dm:253
    public static void begin_the_end()
    {
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(narsie_end_begin_check)), 5 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 254);
    }

    ///First crew last second win check and flufftext for [/proc/begin_the_end()]
    // tg: code/modules/power/singularity/narsie.dm:257
    public static void narsie_end_begin_check()
    {
        if (QDELETED(GLOB.cult_narsie)) // uno
        {
            priority_announce("Status report? We detected an anomaly, but it disappeared almost immediately.", Dm.text("[] Higher Dimensional Affairs", command_name()), "sound/announcer/notice/notice1.ogg");
            GLOB.cult_narsie = null;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(cult_ending_helper), CULT_FAILURE_NARSIE_KILLED), 2 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 261);
            return;
        }
        priority_announce(
            text: "An acausal dimensional event has been detected in your sector. Event has been flagged EXTINCTION-CLASS. Directing all available assets toward simulating solutions. SOLUTION ETA: 60 SECONDS.",
            title: Dm.text("[] Higher Dimensional Affairs", command_name()),
            sound: "sound/announcer/alarm/airraid.ogg");
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(narsie_end_second_check)), 50 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 268);
    }

    ///Second crew last second win check and flufftext for [/proc/begin_the_end()]
    // tg: code/modules/power/singularity/narsie.dm:271
    public static void narsie_end_second_check()
    {
        if (QDELETED(GLOB.cult_narsie)) // dos
        {
            priority_announce("Simulations aborted, sensors report that the acasual event is normalizing. Good work, crew.", Dm.text("[] Higher Dimensional Affairs", command_name()), "sound/announcer/notice/notice1.ogg");
            GLOB.cult_narsie = null;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(cult_ending_helper), CULT_FAILURE_NARSIE_KILLED), 2 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 275);
            return;
        }
        priority_announce("Simulations on acausal dimensional event complete. Deploying solution package now. Deployment ETA: ONE MINUTE. ", Dm.text("[] Higher Dimensional Affairs", command_name()));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(narsie_start_destroy_station)), 5 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 278);
    }

    ///security level and shuttle lockdowns for [/proc/begin_the_end()]
    // tg: code/modules/power/singularity/narsie.dm:281
    public static void narsie_start_destroy_station()
    {
        SSsecurity_level.set_level(SEC_LEVEL_DELTA);
        SSshuttle.registerHostileEnvironment(GLOB.cult_narsie);
        SSshuttle.lockdown = TRUE;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(narsie_apocalypse)), 1 * MINUTES, file: "code/modules/power/singularity/narsie.dm", line: 285);
    }

    ///Third crew last second win check and flufftext for [/proc/begin_the_end()]
    // tg: code/modules/power/singularity/narsie.dm:288
    public static void narsie_apocalypse()
    {
        if (QDELETED(GLOB.cult_narsie)) // tres
        {
            priority_announce("Normalization detected! Abort the solution package!", Dm.text("[] Higher Dimensional Affairs", command_name()), "sound/announcer/notice/notice1.ogg");
            SSshuttle.clearHostileEnvironment(GLOB.cult_narsie);
            GLOB.cult_narsie = null;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(narsie_last_second_win)), 2 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 294);
            return;
        }
        if (!Dm.truthy(GLOB.cult_narsie.resolved))
        {
            GLOB.cult_narsie.resolved = TRUE;
            sound_to_playing_players("sound/announcer/alarm/nuke_alarm.ogg", 70);
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(cult_ending_helper)), 12 * SECONDS, file: "code/modules/power/singularity/narsie.dm", line: 299);
        }
    }

    ///Called only if the crew managed to destroy narsie at the very last second for [/proc/begin_the_end()]
    // tg: code/modules/power/singularity/narsie.dm:301
    public static void narsie_last_second_win()
    {
        SSsecurity_level.set_level(SEC_LEVEL_RED);
        SSshuttle.lockdown = FALSE;
        Waiting.DetachSync(() => cult_ending_helper(CULT_FAILURE_NARSIE_KILLED));
    }

    ///Helper to set the round to end asap. Current usage Cult round end code
    // tg: code/modules/power/singularity/narsie.dm:307
    public static void ending_helper()
    {
        SSticker.force_ending = FORCE_END_ROUND;
    }

    /**
     * Selects cinematic to play as part of the cult end depending on the outcome then ends the round afterward
     * called either when narsie eats everyone, or when [/proc/begin_the_end()] reaches its conclusion
     */
    // tg: code/modules/power/singularity/narsie.dm:314
    public static void cult_ending_helper(float? ending_type = null)
    {
        ending_type ??= CULT_VICTORY_NUKE;
        // DEVIATION(DV350): tg plays /datum/cinematic/cult_fail, cult_arm or nuke/cult over the whole screen and ends the round from its
        // callback; the cinematics are not ported, so the round ends at once
        ending_helper();
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/__HELPERS/global_lists.dm  GLOBAL_VAR(cult_narsie)
    public obj_narsie cult_narsie;

    // tg: (GLOBAL_UNMANAGED(cult_narsie), __DEFINES/__globals.dm)
    public virtual void InitGlobalcult_narsie()
    {
        Dm.append(ref gvars_datum_init_order, "cult_narsie");
    }
}
