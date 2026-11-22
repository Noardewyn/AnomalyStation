// Translated from tgstation (snapshot 1d99bd7f): code/datums/timed_actions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The busy indicators it creates are a seam (lead decision D4, DEVIATION DV13): /datum/cogbar's New is
// deferred and /datum/progressbar's New is not translated (datums/cogbar.cs, datums/progressbar.cs). The statement
// order of New keeps tg's traps (docs/ai/specs/m0-reading/timed-actions-helpers.md section 2): the parameter
// timed_action_flags and the var of the same name are distinct, the progress bar gets the delay before the slowdown,
// and a mob whose cached_multiplicative_actions_slowdown is still null gets a delay of 0.

// tg: code/datums/timed_actions.dm:5
[DmPath("/datum/timed_action")]
public partial class datum_timed_action : datum
{
    // tg: code/datums/timed_actions.dm:1  #define ACTION_WORKING 0 (file-local, #undef at :254; do_after names it too, rules 11.4)
    internal const int ACTION_WORKING = 0;
    // tg: code/datums/timed_actions.dm:2  #define ACTION_FAILED 1 (file-local, #undef at :255)
    internal const int ACTION_FAILED = 1;
    // tg: code/datums/timed_actions.dm:3  #define ACTION_SUCCEEDED 2 (file-local, #undef at :256)
    internal const int ACTION_SUCCEEDED = 2;

    /// DEVIATION(DV348): a flag of a do_after a player keeps going by holding the button on its target: releasing it, or looking away, cancels. tg's bits end at 1 << 5.
    public const int DO_AFTER_HOLD = 1 << 6;

    /// Atom performing the action
    // tg: code/datums/timed_actions.dm:7
    public atom_movable user;
    /// Atoms on which the action is performed
    // tg: code/datums/timed_actions.dm:9
    public DmList targets;
    /// Progress bar displayed to the user/override
    // tg: code/datums/timed_actions.dm:11
    public datum_progressbar progressbar;
    /// Cog visual displayed to everyone else
    // tg: code/datums/timed_actions.dm:13
    public datum_cogbar cogbar;
    /// Callback to invoke each tick to check for condition validity
    // tg: code/datums/timed_actions.dm:15
    public datum_callback extra_checks;
    // Start and end world.tick for the action
    // tg: code/datums/timed_actions.dm:17
    public object start_time;
    // tg: code/datums/timed_actions.dm:18
    public object end_time;
    /// Flags of our action
    // tg: code/datums/timed_actions.dm:20
    public float timed_action_flags = NONE;
    /// Status of the action to pass into the main wait loop
    // tg: code/datums/timed_actions.dm:22
    public float status = ACTION_WORKING;
    /// DEVIATION(DV348): the player whose held button keeps a DO_AFTER_HOLD action going; null for any other action and for a mob with no player
    public client holder;

    // tg: code/datums/timed_actions.dm:24
    [DmParams("user", "targets", "delay", "show_progress", "timed_action_flags", "extra_checks", "cog_icon", "cog_iconstate", "bar_override")]
    public override async DmTask New(params object[] __extra)
    {
        atom_movable user = Dm.arg<atom_movable>(__extra, 0);
        // object, rules 3.4: tg tests it with isnull and islist and wraps a non-list target in a list (:29-32)
        object targets = Dm.arg(__extra, 1);
        object delay = Dm.arg(__extra, 2);
        float? show_progress = Dm.arg<float?>(__extra, 3);
        float? timed_action_flags = Dm.arg<float?>(__extra, 4);
        datum_callback extra_checks = Dm.arg<datum_callback>(__extra, 5);
        object cog_icon = Dm.arg(__extra, 6);
        object cog_iconstate = Dm.arg(__extra, 7);
        mob bar_override = Dm.arg<mob>(__extra, 8);
        show_progress ??= TRUE;
        timed_action_flags ??= NONE;
        await base.New(Dm.forward(__extra, user, targets, delay, show_progress, timed_action_flags, extra_checks, cog_icon, cog_iconstate, bar_override));
        this.user = user;
        this.timed_action_flags = Dm.num(timed_action_flags);
        this.extra_checks = extra_checks;
        if (Dm.truthy(Dm.bitand(timed_action_flags, DO_AFTER_HOLD)))
            holder = (user as mob)?.client;
        if (targets == null)
            targets = Dm.list(user);
        else if (!islist(targets))
            targets = Dm.list(targets);

        this.targets = targets as DmList;

        if (Dm.truthy(show_progress))
        {
            if (Dm.truthy((user as mob)?.client) || Dm.truthy(bar_override?.client))
                progressbar = Dm.@new<datum_progressbar>((bar_override is var or__1 && Dm.truthy(or__1) ? or__1 : (object)user), delay, (((DmList)targets)[1] is var or__2 && Dm.truthy(or__2) ? or__2 : user));

            if (cog_icon != null && Dm.ge(delay, 1 * SECONDS))
                cogbar = Dm.@new<datum_cogbar>(user, cog_icon, cog_iconstate);
        }

        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        if (!ismob(user))
            Dm.combine(ref timed_action_flags, Dm.bitor(Dm.bitor(IGNORE_HELD_ITEM, IGNORE_INCAPACITATED), IGNORE_SLOWDOWNS));
        else if (!Dm.truthy(Dm.bitand(timed_action_flags, IGNORE_SLOWDOWNS)))
            delay = Dm.mul(delay, (user as mob).cached_multiplicative_actions_slowdown);

        start_time = world.time;
        end_time = Dm.add(world.time, delay);

        register_signals();
    }

    // tg: code/datums/timed_actions.dm:57
    public override object Destroy(float? force = null)
    {
        user = null;
        targets = null;
        // Only qdel these two in case of an await() runtime/early deletion/whatever, otherwise let them play out their animation and self-delete
        if (status == ACTION_WORKING)
        {
            qdel(progressbar);
            qdel(cogbar);
        }
        progressbar = null;
        cogbar = null;
        extra_checks = null;
        holder = null;
        STOP_PROCESSING(SStimed_actions, this);
        status = ACTION_FAILED;
        return base.Destroy(force);
    }

    // tg: code/datums/timed_actions.dm:71
    public virtual void register_signals()
    {
        RegisterSignal(user, COMSIG_QDELETING, nameof(on_user_deleted));

        if (!Dm.truthy(Dm.bitand(timed_action_flags, IGNORE_USER_LOC_CHANGE)))
            RegisterSignal(user, COMSIG_MOVABLE_MOVED, nameof(on_user_moved));

        if (!Dm.truthy(Dm.bitand(timed_action_flags, IGNORE_INCAPACITATED)))
            RegisterSignal(user, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), nameof(on_user_incapacitated));

        if (Dm.truthy(Dm.bitand(timed_action_flags, DO_AFTER_CHECK_NEXT_MOVE)))
            RegisterSignal(user, COMSIG_LIVING_CHANGENEXT_MOVE, nameof(on_changenext_move));

        if (!Dm.truthy(Dm.bitand(timed_action_flags, IGNORE_HELD_ITEM)))
        {
            RegisterSignal(user, COMSIG_MOB_EQUIPPED_ITEM, nameof(on_item_equipped));
            RegisterSignal(user, COMSIG_MOB_SWAP_HANDS, nameof(on_hands_swapped));
            RegisterSignal(user, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(on_item_dropped));
        }

        foreach (atom target in Dm.each_cast<atom>(targets))
        {
            if (target == user)
                continue;
            RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_deleted));
            if (!Dm.truthy(Dm.bitand(timed_action_flags, IGNORE_TARGET_LOC_CHANGE)))
                RegisterSignal(target, COMSIG_MOVABLE_MOVED, nameof(on_target_moved));
        }
    }

    // tg: code/datums/timed_actions.dm:95
    public virtual object cancel()
    {
        if (status != ACTION_WORKING)
            return FALSE;

        status = ACTION_FAILED;
        STOP_PROCESSING(SStimed_actions, this);
        return TRUE;
    }

    // tg: code/datums/timed_actions.dm:103
    public virtual async DmTask<object> @await(object delay = null)
    {
        delay ??= world.tick_lag;
        object dot = null;
        START_PROCESSING(SStimed_actions, this);
        status = ACTION_WORKING;

        while (status == ACTION_WORKING && Dm.lt(world.time, end_time))
            await sleep(world.tick_lag);

        if (status == ACTION_WORKING) // Due to how MC handles sleeping, await will tick first before the subsystem itself, so we need to tick ourselves one last time if we haven't been aborted
            process();

        dot = Dm.num(status == ACTION_SUCCEEDED);

        if (!QDELETED(progressbar))
            progressbar.end_progress();
        if (!QDELETED(cogbar))
            cogbar.remove();

        qdel(this);
        return dot;
    }

    // tg: code/datums/timed_actions.dm:122  (/datum/process is set waitfor = FALSE; this body cannot sleep, so it is a
    // plain method, waiting-model.md 5.4: InvokeAsync is the synchronous waitfor wrapper of the callback)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (holder != null && ((user as mob)?.client != holder || holder.dm_mouse_down == null || !Dm.@in(holder.dm_mouse_held_over, targets)))
        {
            cancel();
            return null;
        }

        if (Dm.truthy(extra_checks) && !Dm.truthy(extra_checks.InvokeAsync()))
        {
            cancel();
            return null;
        }

        if (!QDELETED(progressbar))
            progressbar.update(Dm.sub(world.time, start_time));

        if (Dm.ge(world.time, end_time))
        {
            status = ACTION_SUCCEEDED;
            return PROCESS_KILL;
        }
        return null;
    }

    // tg: code/datums/timed_actions.dm:134
    [SignalHandler]
    public virtual void on_user_deleted(datum source = null)
    {
        user = null;
        cancel();
    }

    // tg: code/datums/timed_actions.dm:139
    [SignalHandler]
    public virtual void on_target_deleted(datum source = null)
    {
        Dm.remove(ref targets, source);
        cancel();
    }

    // tg: code/datums/timed_actions.dm:144
    [SignalHandler]
    public virtual void on_user_incapacitated(datum source = null)
    {
        cancel();
    }

    // tg: code/datums/timed_actions.dm:148
    [SignalHandler]
    public virtual void on_changenext_move(datum source = null, object next_move = null, object delay = null)
    {
        if (Dm.gt(next_move, world.time))
            cancel();
    }

    // tg: code/datums/timed_actions.dm:153
    [SignalHandler]
    public virtual void on_item_equipped(mob source = null, obj_item item = null, object slot = null)
    {
        // We picked up an item
        if (Dm.eq(item, source.get_active_held_item()))
            cancel();
    }

    // tg: code/datums/timed_actions.dm:159
    [SignalHandler]
    public virtual void on_hands_swapped(datum source = null)
    {
        cancel();
    }

    // tg: code/datums/timed_actions.dm:163
    [SignalHandler]
    public virtual void on_item_dropped(mob source = null, obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, object invdrop = null, object silent = null, object hand_index = null)
    {
        // Dropped held item
        if (Dm.eq(source.active_hand_index, hand_index))
            cancel();
    }

    // tg: code/datums/timed_actions.dm:169
    [SignalHandler]
    public virtual void on_user_moved(datum source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (user.loc == old_loc)
            return;

        // DEVIATION(DV348): a held action, like tg's drifter, goes on while its user stays beside the target
        if (user.drift_handler == null && holder == null)
        {
            cancel();
            return;
        }

        foreach (atom target in Dm.each_cast<atom>(targets))
        {
            if (!Dm.truthy(Waiting.NoSleep(target.Adjacent(user))))
            {
                cancel();
                return;
            }
        }
    }

    // tg: code/datums/timed_actions.dm:184
    [SignalHandler]
    public virtual void on_target_moved(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null)
    {
        if (source.loc == old_loc)
            return;

        if (source.drift_handler == null || !Dm.truthy(Waiting.NoSleep(source.Adjacent(user))))
            cancel();
    }
}

public static partial class GlobalProcs
{
    /**
     * Timed action involving one mob user. Target is optional.
     *
     * Checks that `user` does not move, change hands, get stunned, etc. for the
     * given `delay`. Returns `TRUE` on success or `FALSE` on failure.
     *
     * - user - The mob performing the action.
     * - delay - The time in deciseconds. Use the SECONDS define for readability. `1 SECONDS` is 10 deciseconds.
     * - target - The target of the action. This is where the progressbar will display.
     * - timed_action_flags - Flags to control the behavior of the timed action.
     * - show_progress - Whether to display a progress bar / cogbar.
     * - extra_checks - Additional checks to perform before the action is executed.
     * - interaction_key - The assoc key under which the do_after is capped, with max_interact_count being the cap. Interaction key will default to target if not set.
     * - max_interact_count - The maximum amount of interactions allowed.
     * - cog_icon - The icon file of the cog. Default: 'icons/effects/progressbar.dmi'
     * - cog_iconstate - The icon state of the cog. Default: "Cog"
     * - bar_override - Mob which should see the bar instead of the user
     */
    // tg: code/datums/timed_actions.dm:211
    public static async DmTask<object> do_after(atom_movable user = null, object delay = null, object target = null, float? timed_action_flags = null, float? show_progress = null, datum_callback extra_checks = null, object interaction_key = null, float? max_interact_count = null, object cog_icon = null, string cog_iconstate = null, mob bar_override = null)
    {
        timed_action_flags ??= NONE;
        show_progress ??= TRUE;
        max_interact_count ??= 1;
        // A DM default applies to an omitted or null argument (rules 7.2), so an explicit cog_icon = null gets the default icon too.
        cog_icon ??= Dm.resource("icons/effects/progressbar.dmi");
        cog_iconstate ??= "cog";
        object dot = null;
        if (!Dm.truthy(user))
            return FALSE;

        if (!(isnum(delay)))
            throw Dm.CRASH(assertion_message("code/datums/timed_actions.dm", 215, "isnum(delay)", Dm.text("do_after was passed a non-number delay: [].", (delay is var or__1 && Dm.truthy(or__1) ? or__1 : "null"))), "code/datums/timed_actions.dm", 215);
        if (!(!isnum(target)))
            throw Dm.CRASH(assertion_message("code/datums/timed_actions.dm", 216, "!isnum(target)", Dm.text("a do_after created by [] had a target set as [] - probably intended to be the time instead.", user, target)), "code/datums/timed_actions.dm", 216);
        if (!(!isatom(delay)))
            throw Dm.CRASH(assertion_message("code/datums/timed_actions.dm", 217, "!isatom(delay)", Dm.text("a do_after created by [] had a timer of [] - probably intended to be the target instead.", user, delay)), "code/datums/timed_actions.dm", 217);

        if (Dm.le(delay, 0))
            return TRUE;

        if (!Dm.truthy(interaction_key) && ismob(user))
        {
            if (!islist(target))
                interaction_key = target;
            else
            {
                DmList temp = Dm.list();
                foreach (atom atom in Dm.each_cast<atom>(target))
                    Dm.append(ref temp, Dm.@ref(atom));

                sortTim(temp, "/proc/" + nameof(GlobalProcs.cmp_text_asc));
                interaction_key = jointext(temp, "-");
            }
        }

        if (Dm.truthy(interaction_key) && ismob(user)) // Do we have a interaction_key now?
        {
            mob as_mob = user as mob;
            object current_interaction_count = LAZYACCESS(as_mob.do_afters, interaction_key);
            if (Dm.ge(current_interaction_count, max_interact_count)) // We are at our peak
                return null;
            LAZYSET(ref as_mob.do_afters, interaction_key, Dm.add(current_interaction_count, 1));
        }

        if (Dm.truthy(user._listen_lookup?[COMSIG_DO_AFTER_BEGAN]))
            user._SendSignal(COMSIG_DO_AFTER_BEGAN, Dm.list(user));

        datum_timed_action action = Dm.@new<datum_timed_action>(user, target, delay, show_progress, timed_action_flags, extra_checks, cog_icon, cog_iconstate, bar_override);
        dot = await action.@await();

        if (Dm.truthy(interaction_key) && ismob(user))
        {
            mob as_mob = user as mob;
            object reduced_interaction_count = LAZYACCESS(as_mob.do_afters, interaction_key);
            if (Dm.gt(reduced_interaction_count, 1)) // Not done yet!
                LAZYSET(ref as_mob.do_afters, interaction_key, Dm.sub(reduced_interaction_count, 1));
            else
                LAZYREMOVE(ref as_mob.do_afters, interaction_key);
        }
        if (Dm.truthy(user._listen_lookup?[COMSIG_DO_AFTER_ENDED]))
            user._SendSignal(COMSIG_DO_AFTER_ENDED, Dm.list(user));
        return dot;
    }
}
