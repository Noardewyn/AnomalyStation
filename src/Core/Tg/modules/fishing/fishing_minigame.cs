// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_minigame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fishing challenge. The float bobs over the spot while the fish takes its time (a lure has to be spun at
// intervals), it dips when something bites, a click in that window starts the minigame: tg's HUD game, a slider where the
// bait (reeled up while a mouse button is held, falling when it is let go) has to stay on the fish while the completion bar
// fills, with its active effects (antigravity, the flipped and the camouflaged background). The HUD is tg's own screen object
// with its bait, fish and completion bar in its vis_contents, drawn by the client at its screen_loc. The fishing achievements are
// owner-deferred (D332). The var blocks are generated (D111).
//
// GAP(types): tg's `reward_path` (:51, a text default) holds a typepath, an instance or a special text, and `location` (:69) an
// atom, so both are declared here (the generated var block takes the rest).

// tg: code/modules/fishing/fishing_minigame.dm:35  GLOBAL_LIST_EMPTY(fishing_challenges_by_user)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/fishing/fishing_minigame.dm:35  GLOBAL_LIST_EMPTY(fishing_challenges_by_user)
    public DmList fishing_challenges_by_user;

    // tg: code/modules/fishing/fishing_minigame.dm:35
    public virtual void InitGlobalfishing_challenges_by_user()
    {
        fishing_challenges_by_user = Dm.list();
        Dm.append(ref gvars_datum_init_order, "fishing_challenges_by_user");
    }
}

// tg: code/modules/fishing/fishing_minigame.dm:37
public partial class datum_fishing_challenge : datum
{
    /// Result path
    // tg: code/modules/fishing/fishing_minigame.dm:51
    public object reward_path = FISHING_DUD;
    ///The physical fishing spot our float is hovering
    // tg: code/modules/fishing/fishing_minigame.dm:69
    public atom location;

    // tg: code/modules/fishing/fishing_minigame.dm:123
    [DmParams("comp", "rod", "user")]
    public override async DmTask New(params object[] __extra)
    {
        datum_component_fishing_spot comp = Dm.arg<datum_component_fishing_spot>(__extra, 0);
        obj_item_fishing_rod rod = Dm.arg<obj_item_fishing_rod>(__extra, 1);
        mob_living user = Dm.arg<mob_living>(__extra, 2);
        this.user = user;
        used_rod = rod;
        location = comp.parent as atom;
        @float = await Dm.new_async<obj_effect_fishing_float>(get_turf(location), location);
        @float.spin_frequency = rod.spin_frequency;
        RegisterSignal(location, COMSIG_QDELETING, nameof(on_spot_gone));
        RegisterSignal(comp, COMSIG_QDELETING, nameof(on_spot_gone));
        register_reward_signals(comp.fish_source);
        RegisterSignal(fish_source, COMSIG_FISHING_SOURCE_INTERRUPT_CHALLENGE, nameof(interrupt_challenge));
        background = comp.fish_source.background;
        if (comp.fish_source.wait_time_range != null)
            wait_time_range = comp.fish_source.wait_time_range;
        if (@float.spin_frequency != null) //Using a fishing lure narrows the range a bit, for better or worse.
            wait_time_range = Dm.list(Dm.num(wait_time_range[1]) + 8 * SECONDS, Dm.num(wait_time_range[2]) - 8 * SECONDS);
        user.SEND_SIGNAL(COMSIG_MOB_BEGIN_FISHING, Dm.list(user, this));
        rod.SEND_SIGNAL(COMSIG_ROD_BEGIN_FISHING, Dm.list(rod, this));
        GLOB.fishing_challenges_by_user[user] = this;

        /// Enable special parameters
        if (rod.line_value != null)
        {
            completion_gain += 1; // Any fishing line will provide a small boost by default
            if (Dm.truthy(Dm.bitand(rod.line_value.fishing_line_traits, FISHING_LINE_BOUNCY)))
                completion_loss -= 2;
            if (Dm.truthy(Dm.bitand(rod.line_value.fishing_line_traits, FISHING_LINE_STIFF)))
            {
                completion_loss += 1;
                completion_gain -= 1;
            }
            if (Dm.truthy(Dm.bitand(rod.line_value.fishing_line_traits, FISHING_LINE_AUTOREEL)))
                special_effects = Dm.bitor(special_effects, FISHING_MINIGAME_AUTOREEL);
        }
        if (rod.hook_value != null)
        {
            object hook_traits = rod.hook_value.fishing_hook_traits;
            if (Dm.truthy(Dm.bitand(hook_traits, FISHING_HOOK_WEIGHTED)))
                bait_bounce_mult = 0.1f;
            if (Dm.truthy(Dm.bitand(hook_traits, FISHING_HOOK_BIDIRECTIONAL)))
                special_effects = Dm.bitor(special_effects, FISHING_MINIGAME_RULE_BIDIRECTIONAL);
            if (Dm.truthy(Dm.bitand(hook_traits, FISHING_HOOK_NO_ESCAPE)))
                special_effects = Dm.bitor(special_effects, FISHING_MINIGAME_RULE_NO_ESCAPE);
            if (Dm.truthy(Dm.bitand(hook_traits, FISHING_HOOK_ENSNARE)))
                completion_loss -= 2;
            if (Dm.truthy(Dm.bitand(hook_traits, FISHING_HOOK_KILL)))
                special_effects = Dm.bitor(special_effects, FISHING_MINIGAME_RULE_KILL);
        }

        //Finish the minigame faster at higher skill. The value modifiers for fishing are negative values btw.
        completion_loss += Dm.num(user.mind?.get_skill_modifier(typeof(datum_skill_fishing), SKILL_VALUE_MODIFIER)) / 5;
        completion_gain -= Dm.num(user.mind?.get_skill_modifier(typeof(datum_skill_fishing), SKILL_VALUE_MODIFIER)) / 7.5f;

        reeling_velocity *= rod.bait_speed_mult;
        completion_gain *= rod.completion_speed_mult;
        bait_bounce_mult *= rod.bounciness_mult;
        deceleration_mult *= rod.deceleration_mult;
        gravity_velocity *= rod.gravity_mult;
        /**
         * The overlap multiplier is lower than 1 by default and exponentiation will make it even lower,
         * to offset the harder control however a bait velocity higher (or lower) than normal.
         */
        overlap_velocity_mult = (float)Math.Pow(overlap_velocity_mult, rod.bait_speed_mult);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:178
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.fishing_challenges_by_user, user);
        if (!Dm.truthy(completed))
            complete(win: FALSE);
        if (fishing_line != null)
        {
            //Stops the line snapped message from appearing everytime the minigame is over.
            UnregisterSignal(fishing_line, COMSIG_QDELETING);
            QDEL_NULL(ref fishing_line);
        }
        QDEL_NULL(ref @float);
        SStgui.close_uis(this);
        user = null;
        used_rod = null;
        location = null;
        QDEL_NULL(ref mover);
        return base.Destroy(force);
    }

    /**
     * Proc responsible for registering the signals for difficulty, possible reward, and challenge completion.
     * Call this if you want to override the fish source from which we roll rewards (preferably before the minigame phase).
     */
    // tg: code/modules/fishing/fishing_minigame.dm:199
    public virtual void register_reward_signals(datum_fish_source new_fish_source = null)
    {
        if (fish_source != null)
        {
            fish_source.UnregisterSignal(this, Dm.list(COMSIG_FISHING_CHALLENGE_ROLL_REWARD));
            fish_source.UnregisterSignal(user, COMSIG_MOB_COMPLETE_FISHING);
        }
        fish_source = new_fish_source;
        fish_source.RegisterSignal(this, COMSIG_FISHING_CHALLENGE_ROLL_REWARD, nameof(datum_fish_source.roll_reward_minigame));
        fish_source.RegisterSignal(user, COMSIG_MOB_COMPLETE_FISHING, nameof(datum_fish_source.on_challenge_completed));
    }

    // tg: code/modules/fishing/fishing_minigame.dm:209
    public virtual void send_alert(object message = null)
    {
        location?.balloon_alert(user, message);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:212
    [SignalHandler]
    public virtual void on_spot_gone(datum source = null)
    {
        send_alert(I18n.T("fishing spot gone!"));
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:217
    [SignalHandler]
    public virtual void interrupt_challenge(datum source = null, object reason = null)
    {
        if (Dm.truthy(reason))
            send_alert(reason);
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:223
    public virtual async DmTask start(mob_living user = null)
    {
        /// Create fishing line visuals
        if (!Dm.truthy(used_rod.@internal))
        {
            fishing_line = await used_rod.create_fishing_line(@float, user, target_py: @float.pixel_y + 4) as datum_beam;
            if (fishing_line == null) //couldn't create a fishing line, probably because we don't have a good line of sight.
            {
                qdel(this);
                return;
            }
            RegisterSignal(fishing_line, COMSIG_QDELETING, nameof(on_line_deleted));
        }
        else //if the rod doesnt have a fishing line, then it ends when they move away
        {
            RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(on_float_or_user_move));
            RegisterSignal(@float, COMSIG_MOVABLE_MOVED, nameof(on_float_or_user_move));
            RegisterSignal(user, SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED), nameof(on_hands_blocked));
        }
        RegisterSignal(user, SIGNAL_REMOVETRAIT(TRAIT_PROFOUND_FISHER), nameof(no_longer_fishing));
        active_effects = bitfield_to_list(Dm.bitand(special_effects, FISHING_MINIGAME_ACTIVE_EFFECTS)) as DmList;
        // If fishing line breaks los / rod gets dropped / deleted
        RegisterSignal(used_rod, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
        await user.add_mood_event("fishing", typeof(datum_mood_event_fishing));
        RegisterSignal(user, COMSIG_MOB_CLICKON, nameof(handle_click));
        start_baiting_phase();
        to_chat(user, span_notice(I18n.T("You start fishing...")));
        playsound(location, Dm.resource("sound/effects/splash.ogg"), 100);
    }

    ///Set the timers for lure that need to be spun at intervals.
    // tg: code/modules/fishing/fishing_minigame.dm:247
    public virtual void set_lure_timers()
    {
        @float.spin_ready = FALSE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(set_lure_ready)), Dm.num(@float.spin_frequency[1]), TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_DELETE_ME, file: "code/modules/fishing/fishing_minigame.dm", line: 249);
        _addtimer(Dm.@new<datum_callback>(this, nameof(missed_lure)), Dm.num(@float.spin_frequency[2]), TIMER_UNIQUE | TIMER_OVERRIDE | TIMER_DELETE_ME, file: "code/modules/fishing/fishing_minigame.dm", line: 250);
        @float.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:253
    public virtual void set_lure_ready()
    {
        if (phase != WAIT_PHASE)
            return;
        @float.spin_ready = TRUE;
        @float.update_appearance(UPDATE_OVERLAYS);
        if (Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_AUTOREEL)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(auto_spin)), 0.2f * SECONDS, file: "code/modules/fishing/fishing_minigame.dm", line: 259);
        playsound(@float, Dm.resource("sound/machines/ping.ogg"), 20, TRUE, MEDIUM_RANGE_SOUND_EXTRARANGE);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:262
    public virtual void auto_spin()
    {
        if (phase != WAIT_PHASE || !Dm.truthy(@float.spin_ready))
            return;
        @float.spin_ready = FALSE;
        @float.update_appearance(UPDATE_OVERLAYS);
        set_lure_timers();
        send_alert(I18n.T("spun"));
    }

    // tg: code/modules/fishing/fishing_minigame.dm:270
    public virtual void missed_lure()
    {
        if (phase != WAIT_PHASE)
            return;
        send_alert(I18n.T("miss!"));
        start_baiting_phase(TRUE); //Add in another 3 to 5 seconds for not spinning the lure.
    }

    // tg: code/modules/fishing/fishing_minigame.dm:276
    [SignalHandler]
    public virtual void on_line_deleted(datum source = null)
    {
        fishing_line = null;
        ///The float may be out of sight if the user has moed around a corner, so the message should be displayed over him instead.
        user.balloon_alert(user, Dm.truthy(user.is_holding(used_rod)) ? I18n.T("line snapped") : I18n.T("rod dropped"));
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:283
    [SignalHandler]
    public virtual void on_float_or_user_move(datum source = null)
    {
        if (!Dm.truthy(Waiting.NoSleep(location.IsReachableBy(user))))
        {
            user.balloon_alert(user, I18n.T("too far!"));
            interrupt();
        }
    }

    // tg: code/modules/fishing/fishing_minigame.dm:290
    [SignalHandler]
    public virtual void on_hands_blocked(datum source = null)
    {
        if (Dm.truthy(completed)) //the rod was dropped and therefore challenge already completed.
            return;
        user.balloon_alert(user, I18n.T("hands blocked!"));
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:297
    [SignalHandler]
    public virtual void no_longer_fishing(datum source = null)
    {
        if (Dm.truthy(completed)) //we already won/lost
            return;
        user.balloon_alert(user, I18n.T("interrupted!"));
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:303
    [SignalHandler]
    public virtual object handle_click(mob_living source = null, atom target = null, object modifiers = null)
    {
        if (HAS_TRAIT(source, TRAIT_HANDS_BLOCKED)) //blocked, can't do stuff
            return null;
        //Doing other stuff
        DmList modifier_list = modifiers as DmList;
        if (Dm.truthy(LAZYACCESS(modifier_list, SHIFT_CLICK)) || Dm.truthy(LAZYACCESS(modifier_list, CTRL_CLICK)) || Dm.truthy(LAZYACCESS(modifier_list, ALT_CLICK)))
            return null;
        //You need to be actively holding on the fishing rod to use it, unless you've the profound_fisher trait.
        if (!HAS_TRAIT(source, TRAIT_PROFOUND_FISHER) && source.get_active_held_item() != used_rod)
            return null;
        if (phase == WAIT_PHASE)
        {
            if (world.time < Dm.num(last_baiting_click) + 0.25f * SECONDS)
                return COMSIG_MOB_CANCEL_CLICKON; //Don't punish players if they accidentally double clicked.
            if (@float.spin_frequency != null)
            {
                if (!Dm.truthy(@float.spin_ready))
                {
                    send_alert(I18n.T("too early!"));
                    start_baiting_phase(TRUE); //Add in another 3 to 5 seconds for that blunder.
                }
                else
                {
                    send_alert(I18n.T("spun"));
                    last_baiting_click = world.time;
                }
                @float.spin_ready = FALSE;
                set_lure_timers();
            }
            else
            {
                send_alert(I18n.T("miss!"));
                start_baiting_phase(TRUE); //Add in another 3 to 5 seconds for that blunder.
            }
        }
        else if (phase == BITING_PHASE)
            Waiting.Detach(start_minigame_phase()); // a signal handler may not sleep; the minigame's start does not wait
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /// Challenge interrupted by something external
    // tg: code/modules/fishing/fishing_minigame.dm:333
    public virtual void interrupt()
    {
        if (!Dm.truthy(completed))
        {
            experience_multiplier *= 0.5f;
            complete(FALSE);
        }
    }

    // tg: code/modules/fishing/fishing_minigame.dm:338
    [SignalHandler]
    public virtual void on_attack_self(obj_item source = null, mob_living user = null)
    {
        Waiting.Detach(stop_fishing(source, user)); // INVOKE_ASYNC(src, PROC_REF(stop_fishing), source, user)
    }

    // tg: code/modules/fishing/fishing_minigame.dm:342
    public virtual async DmTask stop_fishing(obj_item rod = null, mob_living user = null)
    {
        if ((phase != MINIGAME_PHASE || Dm.truthy(await do_after(user, 3 * SECONDS, rod))) && !QDELETED(this) && !Dm.truthy(completed))
        {
            experience_multiplier *= 0.5f;
            send_alert(I18n.T("stopped fishing"));
            complete(FALSE);
        }
    }

    ///The multiplier of the fishing experience malus if the user's level is substantially above the difficulty.
    // tg: code/modules/fishing/fishing_minigame.dm:349  #define EXPERIENCE_MALUS_MULT 0.08
    private const float EXPERIENCE_MALUS_MULT = 0.08f;

    // tg: code/modules/fishing/fishing_minigame.dm:351
    public virtual void complete(float? win = null)
    {
        win ??= FALSE;
        if (Dm.truthy(completed))
            return;
        deltimer(next_phase_timer);
        completed = TRUE;
        if (phase == MINIGAME_PHASE)
            remove_minigame_hud();

        if (!QDELETED(user) && user.mind != null && Dm.truthy(start_time) && !Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_RULE_NO_EXP)))
        {
            float seconds_spent = (world.time - Dm.num(start_time)) * 0.1f;
            float extra_exp_malus = Dm.num(user.mind.get_skill_level(typeof(datum_skill_fishing))) - difficulty * 0.1f;
            if (extra_exp_malus > 0)
                experience_multiplier /= (1 + extra_exp_malus * EXPERIENCE_MALUS_MULT);
            experience_multiplier *= used_rod.experience_multiplier;
            user.mind.adjust_experience(typeof(datum_skill_fishing), round(seconds_spent * FISHING_SKILL_EXP_PER_SECOND * experience_multiplier));
            // tg: :366-367  the legendary fisher achievement (user.client?.give_award): the achievements are owner-deferred (D332)
        }

        if (!Dm.truthy(win))
        {
            user?.SEND_SIGNAL(COMSIG_MOB_COMPLETE_FISHING, Dm.list(user, this, FALSE));
            if (!QDELETED(this))
                qdel(this);
            return;
        }

        if (!Dm.eq(reward_path, FISHING_DUD))
            playsound(location, Dm.resource("sound/effects/bigsplash.ogg"), 100);

        // tg: :376-391  the fish catalog's achievement score of a live, unspawned catch (user.client?.give_award): the achievements
        // are owner-deferred (D332)

        user.SEND_SIGNAL(COMSIG_MOB_COMPLETE_FISHING, Dm.list(user, this, TRUE));
        if (!QDELETED(this))
            qdel(this);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:400
    public virtual void start_baiting_phase(float? penalty = null)
    {
        penalty ??= FALSE;
        reward_path = null; //In case we missed the biting phase, set the path back to null
        float wait_time;
        last_baiting_click = world.time;
        if (Dm.truthy(penalty))
            wait_time = min(Dm.num(timeleft(next_phase_timer)) + rand(3 * SECONDS, 5 * SECONDS), 30 * SECONDS);
        else
        {
            wait_time = rand(Dm.num(wait_time_range[1]), Dm.num(wait_time_range[2]));
            if (Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_AUTOREEL)) && wait_time >= 15 * SECONDS)
                wait_time = max(wait_time - 7.5f * SECONDS, 15 * SECONDS);
        }
        deltimer(next_phase_timer);
        phase = WAIT_PHASE;
        //Bobbing animation
        animate(@float, Dm.named("pixel_z", 1), Dm.named("time", 1 * SECONDS), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -1), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_biting_phase)), wait_time, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/modules/fishing/fishing_minigame.dm", line: 415);
        if (@float.spin_frequency != null)
            set_lure_timers();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:419
    public virtual void start_biting_phase()
    {
        phase = BITING_PHASE;

        DmList rewards = Dm.list();
        SEND_SIGNAL(COMSIG_FISHING_CHALLENGE_ROLL_REWARD, Dm.list(this, used_rod, user, location, rewards));
        if (Dm.truthy(length(rewards)))
            reward_path = pick(rewards);
        playsound(location, Dm.resource("sound/effects/fish_splash.ogg"), 100);

        if (HAS_MIND_TRAIT(user, TRAIT_REVEAL_FISH))
        {
            object possible_icon;
            if (reward_path is datum reward)
                possible_icon = GLOB.specific_fish_icons[reward.type];
            else
                possible_icon = reward_path != null ? GLOB.specific_fish_icons[reward_path] : null;
            fish_icon = (possible_icon as string) ?? FISH_ICON_DEF;
            switch (fish_icon)
            {
                case FISH_ICON_DEF:
                    send_alert(I18n.T("fish!!!"));
                    break;
                case FISH_ICON_HOSTILE:
                    send_alert(I18n.T("hostile!!!"));
                    break;
                case FISH_ICON_STAR:
                    send_alert(I18n.T("starfish!!!"));
                    break;
                case FISH_ICON_CHUNKY:
                    send_alert(I18n.T("round fish!!!"));
                    break;
                case FISH_ICON_JELLYFISH:
                    send_alert(I18n.T("jellyfish!!!"));
                    break;
                case FISH_ICON_SLIME:
                    send_alert(I18n.T("slime!!!"));
                    break;
                case FISH_ICON_COIN:
                    send_alert(I18n.T("valuable!!!"));
                    break;
                case FISH_ICON_GEM:
                    send_alert(I18n.T("ore!!!"));
                    break;
                case FISH_ICON_CRAB:
                    send_alert(I18n.T("crustacean!!!"));
                    break;
                case FISH_ICON_BONE:
                    send_alert(I18n.T("bones!!!"));
                    break;
                case FISH_ICON_ELECTRIC:
                    send_alert(I18n.T("zappy!!!"));
                    break;
                case FISH_ICON_WEAPON:
                    send_alert(I18n.T("weapon!!!"));
                    break;
                case FISH_ICON_CRITTER:
                    send_alert(I18n.T("critter!!!"));
                    break;
                case FISH_ICON_SEED:
                    send_alert(I18n.T("seed!!!"));
                    break;
                case FISH_ICON_BOTTLE:
                    send_alert(I18n.T("bottle!!!"));
                    break;
                case FISH_ICON_ORGAN:
                    send_alert(I18n.T("organ!!!"));
                    break;
            }
        }
        else
            send_alert("!!!");
        animate(@float, Dm.named("pixel_z", 3), Dm.named("time", 5), Dm.named("loop", -1), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -3), Dm.named("time", 5), Dm.named("flags", ANIMATION_RELATIVE));
        if (Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_AUTOREEL)))
            _addtimer(Dm.@new<datum_callback>(this, nameof(automatically_start_minigame)), 0.2f * SECONDS, file: "code/modules/fishing/fishing_minigame.dm", line: 473);
        // Setup next phase
        next_phase_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(start_baiting_phase)), BITING_TIME_WINDOW, Dm.bitor(TIMER_STOPPABLE, TIMER_DELETE_ME), file: "code/modules/fishing/fishing_minigame.dm", line: 475);
        ///If we're using a lure, we want the float to show a little green light during the minigame phase and not a red one.
        @float.spin_ready = TRUE;
        @float.update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:480
    public virtual async DmTask automatically_start_minigame()
    {
        if (phase == BITING_PHASE)
            await start_minigame_phase(auto_reel: TRUE);
    }

    ///The damage dealt per second to the fish when FISHING_MINIGAME_RULE_KILL is active.
    // tg: code/modules/fishing/fishing_minigame.dm:485  #define FISH_DAMAGE_PER_SECOND 2
    private const float FISH_DAMAGE_PER_SECOND = 2;

    ///The player is no longer around to play the minigame, so we interrupt it.
    // tg: code/modules/fishing/fishing_minigame.dm:488
    [SignalHandler]
    public virtual void on_user_logout(datum source = null)
    {
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:492
    [SignalHandler]
    public virtual void on_reward_removed(datum source = null)
    {
        send_alert(I18n.T("reward gone!"));
        interrupt();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:497
    [SignalHandler]
    public virtual void on_fish_death(obj_item_fish source = null)
    {
        if (Dm.eq(source.status, FISH_DEAD))
            win_anyway();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:502
    public virtual void win_anyway()
    {
        if (Dm.truthy(completed))
            return;
        //winning by timeout / fish death shouldn't give as much experience.
        experience_multiplier *= 0.5f;
        complete(TRUE);
    }

    // tg: code/modules/fishing/fishing_minigame.dm:509
    [SignalHandler]
    public virtual void hurt_fish(datum source = null, obj_item_fish reward = null)
    {
        if (reward != null)
        {
            float damage = CEILING((world.time - Dm.num(start_time)) / 10 * FISH_DAMAGE_PER_SECOND, 1);
            Waiting.NoSleep(reward.damage_fish(damage));
        }
    }

    // tg: code/modules/fishing/fishing_minigame.dm:515
    public virtual object get_difficulty()
    {
        object computed = fish_source?.calculate_difficulty_minigame(this, reward_path, used_rod, user);
        float new_difficulty = Dm.truthy(computed) ? Dm.num(computed) : Dm.num(Dm.initial(this, (datum_fishing_challenge p) => p.difficulty));
        if (user?.fishing_difficulty_mods_by_source != null)
        {
            foreach (object source in Dm.each(user.fishing_difficulty_mods_by_source))
                new_difficulty += Dm.num(user.fishing_difficulty_mods_by_source[source]);
        }

        //If you manage to be so well-equipped and skilled to completely crush the difficulty, just skip to the reward.
        if (new_difficulty <= 0)
        {
            complete(TRUE);
            return FALSE;
        }

        difficulty = clamp(round(new_difficulty), FISHING_MINIMUM_DIFFICULTY, 100);
        return TRUE;
    }

    // tg: code/modules/fishing/fishing_minigame.dm:529
    public virtual async DmTask update_difficulty()
    {
        if (phase != MINIGAME_PHASE)
            return;
        float old_difficulty = difficulty;
        //early return if the difficulty is the same or we crush the minigame all the way to 0 difficulty
        if (!Dm.truthy(get_difficulty()) || difficulty == old_difficulty)
            return;
        bait_height = Dm.num(Dm.initial(this, (datum_fishing_challenge p) => p.bait_height)) * used_rod.bait_height_mult;
        experience_multiplier -= difficulty * FISHING_SKILL_DIFFIULTY_EXP_MULT;
        mover.reset_difficulty_values();
        await adjust_to_difficulty();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:541
    public virtual async DmTask adjust_to_difficulty()
    {
        mover.adjust_to_difficulty();
        bait_height -= round(difficulty * BAIT_HEIGHT_DIFFICULTY_MALUS);
        bait_pixel_height = round(MINIGAME_BAIT_HEIGHT * (bait_height / Dm.num(Dm.initial(this, (datum_fishing_challenge p) => p.bait_height))), 1);
        experience_multiplier += difficulty * FISHING_SKILL_DIFFIULTY_EXP_MULT;
        await fishing_hud.hud_bait.adjust_to_difficulty(this);
    }

    ///Get the difficulty and other variables, than start the minigame
    // tg: code/modules/fishing/fishing_minigame.dm:548
    public virtual async DmTask start_minigame_phase(float? auto_reel = null)
    {
        auto_reel ??= FALSE;
        user.SEND_SIGNAL(COMSIG_MOB_BEGIN_FISHING_MINIGAME, Dm.list(user, this));
        if (!Dm.truthy(get_difficulty())) //we totalized 0 or less difficulty, instant win.
            return;

        if (difficulty > FISHING_DEFAULT_DIFFICULTY)
            completion -= MAX_FISH_COMPLETION_MALUS * (difficulty * 0.01f);

        bool is_fish_instance = isfish(reward_path);

        /// Fish minigame properties
        if (ispath(reward_path, typeof(obj_item_fish)) || is_fish_instance)
        {
            Type fish_type = is_fish_instance ? ((datum)reward_path).type : (Type)reward_path;
            Type movement_path = Dm.initial(fish_type, (obj_item_fish p) => p.fish_movement_type);
            mover = await Dm.new_async(movement_path, this) as datum_fish_movement;
            // Apply fish trait modifiers
            DmList fish_traits = is_fish_instance ? ((obj_item_fish)reward_path).fish_traits : (SSfishing.fish_properties[fish_type] as DmList)[FISH_PROPERTIES_TRAITS] as DmList;
            foreach (object fish_trait in Dm.each(fish_traits))
            {
                datum_fish_trait trait = GLOB.fish_traits[fish_trait] as datum_fish_trait;
                trait.minigame_mod(used_rod, user, this);
            }
        }
        else
            mover = await Dm.new_async<datum_fish_movement>(this);

        SEND_SIGNAL(COMSIG_FISHING_CHALLENGE_MOVER_INITIALIZED, Dm.list(this, mover));

        if (Dm.truthy(auto_reel))
            completion *= 1.3f;
        else
        {
            float time_left = Dm.num(timeleft(next_phase_timer));
            // DM's switch ranges: the first range that holds the value wins.
            if (time_left >= 0 && time_left <= BITING_TIME_WINDOW - 3 * SECONDS)
                completion *= 0.65f;
            else if (time_left >= BITING_TIME_WINDOW - 3 * SECONDS && time_left <= BITING_TIME_WINDOW - 2 * SECONDS)
                completion *= 0.82f;
            else if (time_left >= BITING_TIME_WINDOW - 1 * SECONDS && time_left <= BITING_TIME_WINDOW - 0.5f * SECONDS)
                completion *= 1.2f;
            else if (time_left >= BITING_TIME_WINDOW - 0.5f * SECONDS && time_left <= BITING_TIME_WINDOW)
                completion *= 1.4f;
        }
        //randomize the position of the fish a little
        fish_position = rand(0, (int)Math.Floor((FISHING_MINIGAME_AREA - fish_height) * 0.8f));
        float diff_dist = 100 + difficulty;
        bait_position = clamp(round(fish_position + rand(-diff_dist, diff_dist) - bait_height * 0.5f), 0, FISHING_MINIGAME_AREA - bait_height);

        if (!Dm.truthy(prepare_minigame_hud()))
        {
            get_stack_trace("couldn't prepare minigame hud for a fishing challenge."); //just to be sure. This shouldn't happen.
            qdel(this);
            return;
        }

        await adjust_to_difficulty();

        phase = MINIGAME_PHASE;
        deltimer(next_phase_timer);
        // tg tests `FISHING_MINIGAME_RULE_KILL in special_effects` (:599), `in` on a number, which BYOND answers with false, so the
        // jawed hook's timeout never starts; kept as tg has it.
        bool kill_rule_listed = Dm.@in(FISHING_MINIGAME_RULE_KILL, special_effects);
        if (kill_rule_listed && ispath(reward_path, typeof(obj_item_fish)))
        {
            Type fish = (Type)reward_path;
            float wait_time = (Dm.num(Dm.initial(fish, (obj p) => p.max_integrity)) * (1 - Dm.num(Dm.initial(fish, (obj p) => p.integrity_failure))) / FISH_DAMAGE_PER_SECOND) * SECONDS;
            _addtimer(Dm.@new<datum_callback>(this, nameof(win_anyway)), wait_time, TIMER_DELETE_ME, file: "code/modules/fishing/fishing_minigame.dm", line: 602);
        }
        else if (reward_path is atom_movable reward)
        {
            RegisterSignal(reward, COMSIG_MOVABLE_MOVED, nameof(on_reward_removed));
            if (is_fish_instance)
                RegisterSignal(reward, COMSIG_FISH_STATUS_CHANGED, nameof(on_fish_death));
        }
        start_time = world.time;
    }

    ///Throws a stack with prefixed text.
    // tg: code/modules/fishing/fishing_minigame.dm:611
    public virtual void get_stack_trace(object init_text = null)
    {
        string text = Dm.text("[] ", init_text);
        text += Dm.text("used rod: [], ", (object)used_rod ?? "null");
        if (used_rod != null)
            text += Dm.text("bait: [], ", (object)used_rod.bait_value ?? "null");
        text += Dm.text("reward: [], ", reward_path ?? "null");
        text += Dm.text("user: []", (object)user ?? "null");
        if (user != null)
        {
            if (QDELING(user))
                text += ", user qdeling";
            else if (user.client == null)
                text += ", user clientless";
        }
        text += ".";
        stack_trace(text, "code/modules/fishing/fishing_minigame.dm", 625);
    }

    ///Initialize the minigame hud and register some signals to make it work.
    // tg: code/modules/fishing/fishing_minigame.dm:630
    public virtual object prepare_minigame_hud()
    {
        if (user.client == null || Dm.truthy(user.incapacitated))
            return FALSE;
        object dot = TRUE;
        fishing_hud = Dm.@new<atom_movable_screen_fishing_hud>();
        fishing_hud.prepare_minigame(this);
        RegisterSignal(user.client, COMSIG_CLIENT_MOUSEDOWN, nameof(start_reeling));
        RegisterSignal(user.client, COMSIG_CLIENT_MOUSEUP, nameof(stop_reeling));
        RegisterSignal(user, COMSIG_MOB_LOGOUT, nameof(on_user_logout));
        if (Dm.truthy(length(active_effects)))
        {
            // Give the player a moment to prepare for active minigame effects
            active_effect_cd = world.time + rand(5, 9) * SECONDS; // COOLDOWN_START(src, active_effect_cd, rand(5, 9) SECONDS), inline
        }
        START_PROCESSING(SSfishing, this);
        return dot;
    }

    ///Stop processing and remove references to the minigame hud
    // tg: code/modules/fishing/fishing_minigame.dm:645
    public virtual void remove_minigame_hud()
    {
        STOP_PROCESSING(SSfishing, this);
        QDEL_NULL(ref fishing_hud);
    }

    ///While the mouse button is held down, the bait will be reeling up (or down on r-click if the bidirectional rule is enabled)
    // tg: code/modules/fishing/fishing_minigame.dm:650
    [SignalHandler]
    public virtual void start_reeling(client source = null, datum @object = null, object location = null, object control = null, object @params = null)
    {
        bool bidirectional = Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_RULE_BIDIRECTIONAL));
        DmList modifiers = params2list(@params) as DmList;
        if (bidirectional && Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            reeling_state = REELING_STATE_DOWN;
        else
            reeling_state = REELING_STATE_UP;
    }

    ///Reset the reeling state to idle once the mouse button is released
    // tg: code/modules/fishing/fishing_minigame.dm:660
    [SignalHandler]
    public virtual void stop_reeling(client source = null, datum @object = null, object location = null, object control = null, object @params = null)
    {
        reeling_state = REELING_STATE_IDLE;
    }

    ///Update the state of the fish, the bait and the hud
    // tg: code/modules/fishing/fishing_minigame.dm:665
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // COOLDOWN_FINISHED(src, active_effect_cd) (__DEFINES/cooldowns.dm:113), inline
        if (Dm.truthy(length(active_effects)) && active_effect_cd < world.time)
            select_active_effect();
        mover.move_fish(seconds_per_tick);
        move_bait(seconds_per_tick);
        if (!QDELETED(fishing_hud))
            update_visuals(seconds_per_tick);
        return null;
    }

    ///The proc that handles fancy effects like flipping the hud or skewing movement
    // tg: code/modules/fishing/fishing_minigame.dm:674
    public virtual void select_active_effect()
    {
        ///bring forth an active effect
        if (current_active_effect == null)
        {
            current_active_effect = pick(active_effects);
            float effect = Dm.num(current_active_effect);
            if (effect == FISHING_MINIGAME_RULE_ANTIGRAV)
            {
                fishing_hud.icon_state = "background_antigrav";
                Dm.output_to(user, sound(Dm.resource("sound/effects/arcade_jump.ogg"), volume: 50)); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
                active_effect_cd = world.time + rand(6, 9) * SECONDS; // COOLDOWN_START, inline
            }
            else if (effect == FISHING_MINIGAME_RULE_FLIP)
            {
                fishing_hud.icon_state = "background_flip";
                fishing_hud.transform = ((matrix)fishing_hud.transform).Scale(1, -1);
                Dm.output_to(user, sound(Dm.resource("sound/effects/boing.ogg"))); // SEND_SOUND
                active_effect_cd = world.time + rand(5, 6) * SECONDS; // COOLDOWN_START, inline
            }
            else if (effect == FISHING_MINIGAME_RULE_CAMO)
            {
                fishing_hud.icon_state = "background_camo";
                Dm.output_to(user, sound(Dm.resource("sound/effects/nightmare_poof.ogg"), volume: 15)); // SEND_SOUND
                active_effect_cd = world.time + rand(6, 8) * SECONDS; // COOLDOWN_START, inline
                animate(fishing_hud.hud_fish, Dm.named("alpha", 7), Dm.named("time", 2 * SECONDS));
            }
            return;
        }

        ///go back to normal
        float current = Dm.num(current_active_effect);
        if (current == FISHING_MINIGAME_RULE_ANTIGRAV)
        {
            sound inverted_sound = sound(Dm.resource("sound/effects/arcade_jump.ogg"), volume: 50);
            inverted_sound.frequency = -1;
            Dm.output_to(user, inverted_sound); // SEND_SOUND
            active_effect_cd = world.time + rand(10, 13) * SECONDS; // COOLDOWN_START, inline
        }
        else if (current == FISHING_MINIGAME_RULE_FLIP)
        {
            fishing_hud.transform = ((matrix)fishing_hud.transform).Scale(1, -1);
            active_effect_cd = world.time + rand(8, 12) * SECONDS; // COOLDOWN_START, inline
        }
        else if (current == FISHING_MINIGAME_RULE_CAMO)
        {
            active_effect_cd = world.time + rand(9, 16) * SECONDS; // COOLDOWN_START, inline
            Dm.output_to(user, sound(Dm.resource("sound/effects/nightmare_reappear.ogg"), volume: 15)); // SEND_SOUND
            animate(fishing_hud.hud_fish, Dm.named("alpha", 255), Dm.named("time", 1.2f * SECONDS));
        }

        fishing_hud.icon_state = background;
        current_active_effect = null;
    }

    ///The proc that moves the bait around, just like in the old TGUI, mostly.
    // tg: code/modules/fishing/fishing_minigame.dm:714
    public virtual void move_bait(object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        bool should_bounce = abs(bait_velocity) > BAIT_MIN_VELOCITY_BOUNCE;
        bait_position += bait_velocity * spt;
        // Hitting the top bound
        if (bait_position > FISHING_MINIGAME_AREA - bait_height)
        {
            bait_position = FISHING_MINIGAME_AREA - bait_height;
            if (reeling_state == REELING_STATE_UP || !should_bounce)
                bait_velocity = 0;
            else
                bait_velocity = -bait_velocity * bait_bounce_mult;
        }
        // Hitting rock bottom
        else if (bait_position < 0)
        {
            bait_position = 0;
            if (reeling_state == REELING_STATE_DOWN || !should_bounce)
                bait_velocity = 0;
            else
                bait_velocity = -bait_velocity * bait_bounce_mult;
        }

        bool fish_on_bait = (fish_position + fish_height >= bait_position) && (bait_position + bait_height >= fish_position);

        bool bidirectional = Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_RULE_BIDIRECTIONAL));

        float velocity_change = 0;
        if (reeling_state == REELING_STATE_UP)
            velocity_change = reeling_velocity;
        else if (reeling_state == REELING_STATE_DOWN)
            velocity_change = -reeling_velocity;
        else if (reeling_state == REELING_STATE_IDLE)
        {
            if (!bidirectional || bait_velocity > 0)
                velocity_change = gravity_velocity;
            else
                velocity_change = -gravity_velocity;
        }
        velocity_change *= (fish_on_bait ? overlap_velocity_mult : 1) * spt;

        velocity_change = round(velocity_change);

        if (Dm.eq(current_active_effect, FISHING_MINIGAME_RULE_ANTIGRAV))
            velocity_change = -velocity_change;

        if (reeling_state == REELING_STATE_IDLE && Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_AUTOREEL)))
        {
            float bait_center = bait_position + bait_height / 2;
            float auto_adjustment = reeling_velocity * 0.2f;
            if (fish_position > bait_center)
                velocity_change += auto_adjustment;
            else
                velocity_change -= auto_adjustment;
        }

        /**
         * Pull the brake on the velocity if the current velocity and the acceleration
         * have different directions, making the bait less slippery, thus easier to control
         */
        if (bait_velocity > 0 && velocity_change < 0)
            bait_velocity += max(-bait_velocity, velocity_change * deceleration_mult);
        else if (bait_velocity < 0 && velocity_change > 0)
            bait_velocity += min(-bait_velocity, velocity_change * deceleration_mult);

        ///bidirectional baits stay bouyant while idle
        if (bidirectional && reeling_state == REELING_STATE_IDLE)
        {
            if (velocity_change < 0)
                bait_velocity = max(bait_velocity + velocity_change, 0);
            else if (velocity_change > 0)
                bait_velocity = min(bait_velocity + velocity_change, 0);
        }
        else
            bait_velocity += velocity_change;

        //check that the fish area is still intersecting the bait now that it has moved
        if (fish_on_bait)
        {
            completion += completion_gain * spt;
            if (completion >= 100)
                complete(TRUE);
        }
        else
        {
            completion -= completion_loss * spt;
            if (completion <= 0 && !Dm.truthy(Dm.bitand(special_effects, FISHING_MINIGAME_RULE_NO_ESCAPE)))
            {
                user.balloon_alert(user, I18n.T("it got away!"));
                complete(FALSE);
            }
        }

        completion = clamp(completion, 0, 100);
    }

    ///update the vertical pixel position of both fish and bait, and the icon state of the completion bar
    // tg: code/modules/fishing/fishing_minigame.dm:793
    public virtual void update_visuals(object seconds_per_tick = null)
    {
        float spt = Dm.num(seconds_per_tick);
        float bait_offset_mult = bait_position / FISHING_MINIGAME_AREA;
        animate(fishing_hud.hud_bait, Dm.named("pixel_z", MINIGAME_SLIDER_HEIGHT * bait_offset_mult), Dm.named("time", spt * SECONDS));
        float fish_offset_mult = fish_position / FISHING_MINIGAME_AREA;
        animate(fishing_hud.hud_fish, Dm.named("pixel_z", MINIGAME_SLIDER_HEIGHT * fish_offset_mult), Dm.named("time", spt * SECONDS));
        fishing_hud.hud_completion.update_state(completion, seconds_per_tick);
    }

    // The file's defines (fishing_minigame.dm:1-33).
    // tg: code/modules/fishing/fishing_minigame.dm:2  #define WAIT_PHASE 1
    private const float WAIT_PHASE = 1;
    // tg: code/modules/fishing/fishing_minigame.dm:4  #define BITING_PHASE 2
    private const float BITING_PHASE = 2;
    // tg: code/modules/fishing/fishing_minigame.dm:6  #define MINIGAME_PHASE 3
    private const float MINIGAME_PHASE = 3;
    // tg: code/modules/fishing/fishing_minigame.dm:9  #define BAIT_MIN_VELOCITY_BOUNCE 150
    private const float BAIT_MIN_VELOCITY_BOUNCE = 150;
    // tg: code/modules/fishing/fishing_minigame.dm:12  #define MAX_FISH_COMPLETION_MALUS 15
    private const float MAX_FISH_COMPLETION_MALUS = 15;
    // tg: code/modules/fishing/fishing_minigame.dm:14  #define BITING_TIME_WINDOW 4 SECONDS
    private const float BITING_TIME_WINDOW = 4 * SECONDS;
    // tg: code/modules/fishing/fishing_minigame.dm:17  #define BAIT_HEIGHT_DIFFICULTY_MALUS 1.3
    private const float BAIT_HEIGHT_DIFFICULTY_MALUS = 1.3f;
    // tg: code/modules/fishing/fishing_minigame.dm:20-22  #define REELING_STATE_IDLE 0, REELING_STATE_UP 1, REELING_STATE_DOWN 2
    private const float REELING_STATE_IDLE = 0;
    private const float REELING_STATE_UP = 1;
    private const float REELING_STATE_DOWN = 2;
    // tg: code/modules/fishing/fishing_minigame.dm:25  #define MINIGAME_SLIDER_HEIGHT 76
    internal const float MINIGAME_SLIDER_HEIGHT = 76;
    // tg: code/modules/fishing/fishing_minigame.dm:27  #define MINIGAME_BAIT_HEIGHT 27
    internal const float MINIGAME_BAIT_HEIGHT = 27;
    // tg: code/modules/fishing/fishing_minigame.dm:29  #define MINIGAME_BAIT_TOP_AND_BOTTOM_HEIGHT 6
    internal const float MINIGAME_BAIT_TOP_AND_BOTTOM_HEIGHT = 6;
    // tg: code/modules/fishing/fishing_minigame.dm:33  #define MINIGAME_COMPLETION_BAR_HEIGHT 80
    internal const float MINIGAME_COMPLETION_BAR_HEIGHT = 80;
}

///The screen object which bait, fish, and completion bar are visually attached to.
// tg: code/modules/fishing/fishing_minigame.dm:802
public partial class atom_movable_screen_fishing_hud : atom_movable_screen
{
    ///Initialize bait, fish and completion bar and add them to the visual appearance of this screen object.
    // tg: code/modules/fishing/fishing_minigame.dm:815
    public virtual void prepare_minigame(datum_fishing_challenge challenge = null)
    {
        icon_state = challenge.background;
        object frame = challenge.used_rod?.get_frame(challenge);
        add_overlay(Dm.truthy(frame) ? frame : "frame_wood");
        hud_bait = Dm.@new<atom_movable_screen_hud_bait>(null, null, challenge);
        hud_fish = Dm.@new<atom_movable_screen_hud_fish>(null, null, challenge);
        hud_completion = Dm.@new<atom_movable_screen_hud_completion>(null, null);
        Dm.append(vis_contents, Dm.list(hud_bait, hud_fish, hud_completion));
        Dm.append(challenge.user.client.screen, this);
        challenge.update_visuals(0); // Set all states to their initial positions so they don't jump around when the game starts
        master_ref = WEAKREF(challenge) as datum_weakref;
    }

    // tg: code/modules/fishing/fishing_minigame.dm:826
    public override object Destroy(float? force = null)
    {
        datum_fishing_challenge challenge = master_ref?.resolve() as datum_fishing_challenge;
        if (challenge != null)
            Dm.remove(challenge.user?.client?.screen, this);
        QDEL_NULL(ref hud_fish);
        QDEL_NULL(ref hud_bait);
        QDEL_NULL(ref hud_completion);
        return base.Destroy(force);
    }
}

// tg: code/modules/fishing/fishing_minigame.dm:835
public partial class atom_movable_screen_hud_bait : atom_movable_screen
{
    // tg: code/modules/fishing/fishing_minigame.dm:841
    [DmParams("mapload", "hud_owner", "challenge")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_fishing_challenge challenge = Dm.arg<datum_fishing_challenge>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (challenge == null || challenge.bait_pixel_height == datum_fishing_challenge.MINIGAME_BAIT_HEIGHT)
        {
            Waiting.NoSleep(update_icon());
            return dot;
        }

        Waiting.NoSleep(adjust_to_difficulty(challenge));
        return dot;
    }

    // tg: code/modules/fishing/fishing_minigame.dm:849
    public virtual async DmTask adjust_to_difficulty(datum_fishing_challenge challenge = null)
    {
        cur_height = challenge.bait_pixel_height;
        await update_icon();
    }

    // tg: code/modules/fishing/fishing_minigame.dm:853
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        mutable_appearance bait_top = mutable_appearance(icon, "bait_top") as mutable_appearance;
        bait_top.pixel_z += cur_height - datum_fishing_challenge.MINIGAME_BAIT_TOP_AND_BOTTOM_HEIGHT;
        Dm.append(ref dot, bait_top);
        for (int i = 1; i <= cur_height - datum_fishing_challenge.MINIGAME_BAIT_TOP_AND_BOTTOM_HEIGHT; i++)
        {
            mutable_appearance bait_bar = mutable_appearance(icon, "bait_bar") as mutable_appearance;
            bait_bar.pixel_z += i;
            Dm.append(ref dot, bait_bar);
        }
        return dot;
    }
}

// tg: code/modules/fishing/fishing_minigame.dm:863
public partial class atom_movable_screen_hud_fish : atom_movable_screen
{
    // tg: code/modules/fishing/fishing_minigame.dm:868
    [DmParams("mapload", "hud_owner", "challenge")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_fishing_challenge challenge = Dm.arg<datum_fishing_challenge>(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (challenge != null)
            icon_state = challenge.fish_icon;
        return dot;
    }
}

// tg: code/modules/fishing/fishing_minigame.dm:873
public partial class atom_movable_screen_hud_completion : atom_movable_screen
{
    // tg: code/modules/fishing/fishing_minigame.dm:878
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_filter("completion_mask", 1, alpha_mask_filter(icon: icon(icon, "completion_overlay")) as DmList);
        return dot;
    }

    // tg: code/modules/fishing/fishing_minigame.dm:882
    public virtual void update_state(object completion = null, object seconds_per_tick = null)
    {
        animate(get_filter("completion_mask"), Dm.named("y", -datum_fishing_challenge.MINIGAME_COMPLETION_BAR_HEIGHT * (1 - Dm.num(completion) * 0.01f)), Dm.named("time", Dm.num(seconds_per_tick) * SECONDS));
    }
}

/// The visual that appears over the fishing spot
// tg: code/modules/fishing/fishing_minigame.dm:886
public partial class obj_effect_fishing_float : obj_effect
{
    // tg: code/modules/fishing/fishing_minigame.dm:899
    [DmParams("mapload", "spot")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom spot = Dm.arg<atom>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (spot == null)
            return dot;
        if (ismovable(spot)) // we want the float and therefore the fishing line to stay connected with the fishing spot.
            RegisterSignal(spot, COMSIG_MOVABLE_MOVED, nameof(follow_movable));
        // SET_BASE_PIXEL(spot.pixel_x, spot.pixel_y) (__DEFINES/visual_helpers.dm:3), inline
        pixel_x = spot.pixel_x;
        base_pixel_x = spot.pixel_x;
        pixel_y = spot.pixel_y;
        base_pixel_y = spot.pixel_y;
        // SET_BASE_VISUAL_PIXEL(spot.pixel_w, spot.pixel_z) (__DEFINES/visual_helpers.dm:14), inline
        pixel_w = spot.pixel_w;
        base_pixel_w = spot.pixel_w;
        pixel_z = spot.pixel_z;
        base_pixel_z = spot.pixel_z;
        // early return for spots with a plane lower than this. the floor plane is topdown and we don't want to inherit their layers.
        if (spot.plane < plane)
            return dot;
        if (spot.plane > plane) //We want this to render above the fishing spot.
        {
            turf turf = get_turf(spot);
            SET_PLANE_EXPLICIT(this, Dm.num(PLANE_TO_TRUE(spot.plane)), turf);
        }
        if (spot.layer > layer) //Ditto. New stuff renders above old stuff if the layer is the same iirc (with some caveats).
            layer = spot.layer;
        return dot;
    }

    // tg: code/modules/fishing/fishing_minigame.dm:917
    [SignalHandler]
    public virtual void follow_movable(atom_movable source = null)
    {
        set_glide_size(source.glide_size);
        Waiting.NoSleep(forceMove(source.loc));
    }

    // tg: code/modules/fishing/fishing_minigame.dm:923
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (spin_frequency == null)
            return dot;
        mutable_appearance overlay = mutable_appearance(icon, "lure_light") as mutable_appearance;
        overlay.color = Dm.truthy(spin_ready) ? COLOR_GREEN : COLOR_RED;
        Dm.append(ref dot, overlay);
        // DEVIATION(DV12): . += emissive_appearance(icon, "lure_light_emissive", src, alpha = src.alpha, effect_type = EMISSIVE_NO_BLOOM), the light's glow on the emissive plane
        return dot;
    }
}
