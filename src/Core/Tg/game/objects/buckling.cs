// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/buckling.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice A (D218, D223): the whole file; /atom/movable/attack_robot (:37, the cyborgs' unbuckling) since silicons SI2. The /atom/movable var block is hand-written (wave 4: atom_movable keeps its own var block);
// the drag and drop receiver (protocol version 17) reaches user_buckle_mob.

// tg: code/game/objects/buckling.dm:1
public partial class atom_movable : atom
{
    //literally just the above extension of attack_hand(), but for silicons instead (with an adjacency check, since attack_robot() being called doesn't mean that you're adjacent to something)
    // tg: code/game/objects/buckling.dm:37
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_robot(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(await Adjacent(user)) && Dm.truthy(can_buckle) && Dm.truthy(has_buckled_mobs()))
        {
            if (Dm.length(buckled_mobs) > 1)
            {
                mob_living unbuckled = await tgui_input_list(user, I18n.T("Who do you wish to unbuckle?"), I18n.T("Unbuckle"), sort_names(buckled_mobs) as DmList) as mob_living;
                if (unbuckled == null)
                    return null;
                return await user_unbuckle_mob(unbuckled, user);
            }
            else
                return await user_unbuckle_mob(buckled_mobs[1] as mob_living, user);
        }
        return dot;
    }

    /// Whether the atom allows mobs to be buckled to it. Can be ignored in [/atom/movable/proc/buckle_mob()] if force = TRUE
    // tg: code/game/objects/buckling.dm:3
    public float can_buckle = FALSE;
    /// Bed-like behaviour, forces mob.lying = buckle_lying if not set to [NO_BUCKLE_LYING].
    // tg: code/game/objects/buckling.dm:5
    public float buckle_lying = NO_BUCKLE_LYING;
    /// Bed-like behaviour, sets mob dir to buckle_dir if not set to [BUCKLE_MATCH_DIR]. If set to [BUCKLE_MATCH_DIR], makes mob dir match ours.
    // tg: code/game/objects/buckling.dm:7
    public float buckle_dir = BUCKLE_MATCH_DIR;
    /// Require people to be handcuffed before being able to buckle. eg: pipes
    // tg: code/game/objects/buckling.dm:9
    public float buckle_requires_restraints = FALSE;
    /// The mobs currently buckled to this atom
    // tg: code/game/objects/buckling.dm:11
    public DmList buckled_mobs; //list()
    /// How much time it takes to buckle a mob to us?
    // tg: code/game/objects/buckling.dm:13
    public float buckle_delay = 2 * SECONDS;
    /// The maximum number of mob/livings allowed to be buckled to this atom at once
    // tg: code/game/objects/buckling.dm:15
    public float max_buckled_mobs = 1;
    /// Whether things buckled to this atom can be pulled while they're buckled
    // tg: code/game/objects/buckling.dm:17
    public float buckle_prevents_pull = FALSE;
}

//Interaction
// tg: code/game/objects/buckling.dm:19
public partial class atom_movable : atom
{
    // tg: code/game/objects/buckling.dm:19
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(can_buckle) && Dm.truthy(has_buckled_mobs()))
        {
            if (Dm.length(buckled_mobs) > 1)
            {
                mob_living unbuckled = await tgui_input_list(user, I18n.T("Who do you wish to unbuckle?"), I18n.T("Unbuckle"), sort_names(buckled_mobs) as DmList) as mob_living;
                if (unbuckled == null)
                    return dot;
                if (Dm.truthy(await user_unbuckle_mob(unbuckled, user)))
                    return TRUE;
            }
            else
            {
                if (Dm.truthy(await user_unbuckle_mob(buckled_mobs[1] as mob_living, user)))
                    return TRUE;
            }
        }
        return dot;
    }

    // tg: code/game/objects/buckling.dm:49
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        // D132 (DV27): the base every movable's drop reaches; an inert type's own mouse_drop_receive is refused (interaction.md 7).
        if (InertSeam.MissingOverride(this, "mouse_drop_receive", user))
            return null;
        return await mouse_buckle_handling(dropped as mob_living, user as mob_living);
    }

    /**
     * Does some typechecks and then calls user_buckle_mob
     *
     * Arguments:
     * M - The mob being buckled to src
     * user - The mob buckling M to src
     */
    // tg: code/game/objects/buckling.dm:59
    public virtual async DmTask<object> mouse_buckle_handling(mob_living M = null, mob_living user = null)
    {
        if (Dm.truthy(can_buckle) && M is mob_living && user is mob_living)
            return await user_buckle_mob(M, user, check_loc: FALSE);
        return null;
    }

    /**
     * Returns TRUE if there are mobs buckled to this atom and FALSE otherwise
     */
    // tg: code/game/objects/buckling.dm:66
    public virtual object has_buckled_mobs()
    {
        if (Dm.truthy(length(buckled_mobs)))
            return TRUE;
        return FALSE;
    }

    /**
     * Set a mob as buckled to src
     *
     * If you want to have a mob buckling another mob to something, or you want a chat message sent, use user_buckle_mob instead.
     * Arguments:
     * M - The mob to be buckled to src
     * force - Set to TRUE to ignore src's can_buckle and M's can_buckle_to
     * check_loc - Set to FALSE to allow buckling from adjacent turfs, or TRUE if buckling is only allowed with src and M on the same turf.
     * buckle_mob_flags- Used for riding cyborgs and humans if we need to reserve an arm or two on either the rider or the ridden mob.
     * ignore_self - If set to TRUE, this will not do a check to see if the user can move into the turf of the mob and will just automatically mount them.
     */
    // tg: code/game/objects/buckling.dm:82
    public virtual async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        force ??= FALSE;
        check_loc ??= TRUE;
        buckle_mob_flags ??= NONE;
        if (!Dm.truthy(buckled_mobs))
            buckled_mobs = Dm.list();

        if (!Dm.truthy(await is_buckle_possible(M, force, check_loc)))
            return FALSE;

        // This signal will check if the mob is mounting this atom to ride it. There are 3 possibilities for how this goes
        // 1. This movable doesn't have a ridable element and can't be ridden, so nothing gets returned, so continue on
        // 2. There's a ridable element but we failed to mount it for whatever reason (maybe it has no seats left, for example), so we cancel the buckling
        // 3. There's a ridable element and we were successfully able to mount, so keep it going and continue on with buckling
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PREBUCKLE]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PREBUCKLE, Dm.list(this, M, force, buckle_mob_flags))), COMPONENT_BLOCK_BUCKLE)))
            return FALSE;

        if (Dm.truthy(M.pulledby))
        {
            if (Dm.truthy(buckle_prevents_pull))
                await M.pulledby.stop_pulling();
            else if (isliving(M.pulledby))
            {
                mob_living L = (mob_living)M.pulledby;
                L.reset_pull_offsets(M, TRUE);
            }
        }

        if (Dm.truthy(anchored))
            ADD_TRAIT(M, TRAIT_NO_FLOATING_ANIM, BUCKLED_TRAIT);
        if (!Dm.truthy(length(buckled_mobs)))
            RegisterSignal(this, COMSIG_MOVABLE_SET_ANCHORED, nameof(on_set_anchored));
        await M.set_buckled(this);
        Dm.combine(ref buckled_mobs, M);

        ///If the icon is too big, don't add it to the screen alert
        bool add_src_icon = true;
        DmList dim = get_icon_dimensions(icon);
        if (Dm.num(dim["height"]) > ICON_SIZE_Y || Dm.num(dim["width"]) > ICON_SIZE_X)
            add_src_icon = false;

        M.throw_alert(ALERT_BUCKLED, typeof(atom_movable_screen_alert_buckled), new_master: add_src_icon ? this : null);
        M.set_glide_size(glide_size);

        await M.Move(loc as atom);
        if (Dm.eq(buckle_dir, BUCKLE_MATCH_DIR))
            await M.setDir(dir);
        else
            await M.setDir(buckle_dir);

        //Something has unbuckled us in reaction to the above movement
        if (!Dm.truthy(M.buckled))
            return FALSE;

        await post_buckle_mob(M);

        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_BUCKLE]))
            this._SendSignal(COMSIG_MOVABLE_BUCKLE, Dm.list(this, M, force));
        if (Dm.truthy(M._listen_lookup?[COMSIG_MOB_BUCKLED]))
            M._SendSignal(COMSIG_MOB_BUCKLED, Dm.list(M, this));
        return TRUE;
    }

    /**
     * Set a mob as unbuckled from src
     *
     * The mob must actually be buckled to src or else bad things will happen.
     * Arguments:
     * buckled_mob - The mob to be unbuckled
     * force - TRUE if we should ignore buckled_mob.can_buckle_to
     */
    // tg: code/game/objects/buckling.dm:157
    public virtual async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        force ??= FALSE;
        can_fall ??= TRUE;
        object dot = null;

        if (!isliving(buckled_mob))
            throw Dm.CRASH(Dm.text("Non-living [] thing called unbuckle_mob() for source.", buckled_mob), "code/game/objects/buckling.dm", 160);
        if (buckled_mob.buckled != this)
            throw Dm.CRASH(Dm.text("[] called unbuckle_mob() for source while having buckled as [].", buckled_mob, buckled_mob.buckled), "code/game/objects/buckling.dm", 162);
        if (!Dm.truthy(force) && !Dm.truthy(buckled_mob.can_buckle_to))
            return dot;
        dot = buckled_mob;
        await buckled_mob.set_buckled(null);
        await buckled_mob.set_anchored(Dm.initial(buckled_mob, (mob_living p) => p.anchored));
        buckled_mob.clear_alert(ALERT_BUCKLED);
        buckled_mob.set_glide_size(DELAY_TO_GLIDE_SIZE(Dm.num(buckled_mob.cached_multiplicative_slowdown)));
        Dm.remove(ref buckled_mobs, buckled_mob);
        if (Dm.truthy(anchored))
            REMOVE_TRAIT(buckled_mob, TRAIT_NO_FLOATING_ANIM, BUCKLED_TRAIT);
        if (!Dm.truthy(length(buckled_mobs)))
            UnregisterSignal(this, COMSIG_MOVABLE_SET_ANCHORED);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_UNBUCKLE]))
            this._SendSignal(COMSIG_MOVABLE_UNBUCKLE, Dm.list(this, buckled_mob, force));
        if (Dm.truthy(buckled_mob._listen_lookup?[COMSIG_MOB_UNBUCKLED]))
            buckled_mob._SendSignal(COMSIG_MOB_UNBUCKLED, Dm.list(buckled_mob, this));

        if (Dm.truthy(can_fall))
        {
            turf location = buckled_mob.loc as turf;
            if (location is turf && !Dm.truthy(buckled_mob.currently_z_moving))
                await location.zFall(buckled_mob);
        }

        await post_unbuckle_mob(dot as mob_living);

        if (!QDELETED(buckled_mob) && !Dm.truthy(buckled_mob.currently_z_moving) && isturf(buckled_mob.loc)) // In the case they unbuckled to a flying movable midflight.
        {
            turf pitfall = buckled_mob.loc as turf;
            if (pitfall != null)
                await pitfall.zFall(buckled_mob);
        }
        return dot;
    }

    // tg: code/game/objects/buckling.dm:189
    [SignalHandler]
    public virtual void on_set_anchored(atom_movable source = null, object anchorvalue = null)
    {
        foreach (object _buckled_mob in Dm.each(buckled_mobs))
        {
            if (!Dm.truthy(_buckled_mob))
                continue;
            mob_living buckled_mob = _buckled_mob as mob_living;
            if (Dm.truthy(anchored))
                ADD_TRAIT(buckled_mob, TRAIT_NO_FLOATING_ANIM, BUCKLED_TRAIT);
            else
                REMOVE_TRAIT(buckled_mob, TRAIT_NO_FLOATING_ANIM, BUCKLED_TRAIT);
        }
    }

    /**
     * Call [/atom/movable/proc/unbuckle_mob] for all buckled mobs
     */
    // tg: code/game/objects/buckling.dm:203
    public virtual void unbuckle_all_mobs(float? force = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(has_buckled_mobs()))
            return;
        foreach (object m in Dm.each(buckled_mobs))
            Waiting.Detach(unbuckle_mob(m as mob_living, force));
    }

    //Handle any extras after buckling
    //Called on buckle_mob()
    // tg: code/game/objects/buckling.dm:211
    public virtual DmTask post_buckle_mob(mob_living buckled_mob = null)
    {
        return DmTask.CompletedTask;
    }

    //same but for unbuckle
    // tg: code/game/objects/buckling.dm:214
    public virtual DmTask post_unbuckle_mob(mob_living unbuckled_mob = null)
    {
        return DmTask.CompletedTask;
    }

    /**
     * Simple helper proc that runs a suite of checks to test whether it is possible or not to buckle the target mob to src.
     *
     * Returns FALSE if any conditions that should prevent buckling are satisfied. Returns TRUE otherwise.
     * Called from [/atom/movable/proc/buckle_mob] and [/atom/movable/proc/is_user_buckle_possible].
     * Arguments:
     * * target - Target mob to check against buckling to src.
     * * force - Whether or not the buckle should be forced. If TRUE, ignores src's can_buckle var and target's can_buckle_to
     * * check_loc - TRUE if target and src have to be on the same tile, FALSE if they are allowed to just be adjacent
     */
    // tg: code/game/objects/buckling.dm:226
    public virtual async DmTask<object> is_buckle_possible(mob_living target = null, float? force = null, float? check_loc = null)
    {
        force ??= FALSE;
        check_loc ??= TRUE;
        // Make sure target is mob/living
        if (target is not mob_living)
            return FALSE;

        // No bucking you to yourself.
        if (target == this)
            return FALSE;

        turf ground = get_turf(this);
        // If we're not already on the same turf as our target...
        if (get_turf(target) != ground)
        {
            // Check if the target to buckle isn't INSIDE OF A WALL
            if (!isopenturf(loc) || !isopenturf(target.loc))
                return FALSE;

            // Check if the target to buckle isn't INSIDE A SOLID OBJECT (not including vehicles)
            if (Dm.truthy(await ground.is_blocked_turf(exclude_mobs: TRUE, source_atom: this)))
                return FALSE;

            // If we're checking the loc, make sure the target is on the thing we're bucking them to.
            if (Dm.truthy(check_loc) && !Dm.truthy(await target.Adjacent(this)))
                return FALSE;
        }

        // Check if this atom can have things buckled to it.
        if (!Dm.truthy(can_buckle) && !Dm.truthy(force))
            return FALSE;

        // Make sure the target isn't already buckled to something.
        if (Dm.truthy(target.buckled))
            return FALSE;

        // Make sure this atom can still have more things buckled to it.
        if (LAZYLEN(buckled_mobs) >= max_buckled_mobs)
            return FALSE;

        // Stacking buckling leads to lots of jank and issues, better to just nix it entirely
        if (Dm.truthy(target.has_buckled_mobs()))
            return FALSE;

        // If the buckle requires restraints, make sure the target is actually restrained.
        if (Dm.truthy(buckle_requires_restraints) && !HAS_TRAIT(target, TRAIT_RESTRAINED))
            return FALSE;

        //If buckling is forbidden for the target, cancel
        if (!Dm.truthy(target.can_buckle_to) && !Dm.truthy(force))
            return FALSE;

        return TRUE;
    }

    /**
     * Simple helper proc that runs a suite of checks to test whether it is possible or not for user to buckle target mob to src.
     *
     * Returns FALSE if any conditions that should prevent buckling are satisfied. Returns TRUE otherwise.
     * Called from [/atom/movable/proc/user_buckle_mob].
     * Arguments:
     * * target - Target mob to check against buckling to src.
     * * user - The mob who is attempting to buckle the target to src.
     * * check_loc - TRUE if target and src have to be on the same tile, FALSE if buckling is allowed from adjacent tiles
     */
    // tg: code/game/objects/buckling.dm:286
    public virtual async DmTask<object> is_user_buckle_possible(mob_living target = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        // Standard adjacency and other checks.
        if (!Dm.truthy(await Adjacent(user)) || !Dm.truthy(await Adjacent(target)) || !isturf(user.loc) || Dm.truthy(user.incapacitated) || Dm.truthy(target.anchored))
            return FALSE;

        if (iscarbon(user))
        {
            mob_living_carbon carbon_user = (mob_living_carbon)user;
            if (carbon_user.usable_hands <= 0)
                return FALSE;
        }

        // In buckling even possible in the first place?
        if (!Dm.truthy(await is_buckle_possible(target, FALSE, check_loc)))
            return FALSE;

        return TRUE;
    }

    /**
     * Handles a mob buckling another mob to src and sends a visible_message
     *
     * Basically exists to do some checks on the user and then call buckle_mob where the real buckling happens.
     * First, checks if the buckle is valid and cancels if it isn't.
     * Second, checks if src is on a different turf from the target; if it is, does a do_after and another check for sanity
     * Finally, calls [/atom/movable/proc/buckle_mob] to buckle the target to src then gives chat feedback
     * Arguments:
     * * M - The target mob/living being buckled to src
     * * user - The other mob that's buckling M to src
     * * check_loc - TRUE if src and M have to be on the same turf, false otherwise
     */
    // tg: code/game/objects/buckling.dm:314
    public virtual async DmTask<object> user_buckle_mob(mob_living M = null, mob user = null, float? check_loc = null)
    {
        check_loc ??= TRUE;
        object dot = null;
        // Is buckling even possible? Do a full suite of checks.
        if (!Dm.truthy(await is_user_buckle_possible(M, user, check_loc)))
            return FALSE;

        add_fingerprint(user);

        // If the mob we're attempting to buckle is not stood on this atom's turf and it isn't the user buckling themselves,
        // we'll try it with a 2 second do_after delay.
        if (M != user && (get_turf(M) != get_turf(this)))
        {
            M.visible_message(span_warning(I18n.T("[] starts buckling [] to []!", user, M, this)),
                span_userdanger(I18n.T("[] starts buckling you to []!", user, this)),
                span_hear(I18n.T("You hear metal clanking.")));
            if (!Dm.truthy(await do_after(user, buckle_delay, M)))
                return FALSE;

            // Sanity check before we attempt to buckle. Is everything still in a kosher state for buckling after the 3 seconds have elapsed?
            // Covers situations where, for example, the chair was moved or there's some other issue.
            if (!Dm.truthy(await is_user_buckle_possible(M, user, check_loc)))
                return FALSE;
        }

        dot = await buckle_mob(M, check_loc: check_loc);
        if (Dm.truthy(dot))
            buckle_feedback(M, user);
        return dot;
    }

    /// Feedback displayed to nearby players after a mob is buckled to src.
    // tg: code/game/objects/buckling.dm:340
    public virtual void buckle_feedback(mob_living being_buckled = null, mob buckler = null)
    {
        if (being_buckled == buckler)
        {
            being_buckled.visible_message(
                span_notice(I18n.T("[] buckles []self to [].", buckler, buckler.p_them(), this)),
                span_notice(I18n.T("You buckle yourself to [].", this)),
                span_hear(I18n.T("You hear metal clanking."))
            );
        }
        else
        {
            being_buckled.visible_message(
                span_warning(I18n.T("[] buckles [] to []!", buckler, being_buckled, this)),
                span_warning(I18n.T("[] buckles you to []!", buckler, this)),
                span_hear(I18n.T("You hear metal clanking."))
            );
        }
    }

    /**
     * Handles a user unbuckling a mob from src and sends a visible_message
     *
     * Basically just calls unbuckle_mob, sets fingerprint, and sends a visible_message
     * about the user unbuckling the mob
     * Arguments:
     * buckled_mob - The mob/living to unbuckle
     * user - The mob unbuckling buckled_mob
     */
    // tg: code/game/objects/buckling.dm:363
    public virtual async DmTask<object> user_unbuckle_mob(mob_living buckled_mob = null, mob user = null)
    {
        if (!Dm.@in(buckled_mob, buckled_mobs) || !Dm.truthy(await buckled_mob.IsReachableBy(user)))
            return null;
        mob_living M = await unbuckle_mob(buckled_mob) as mob_living;
        if (Dm.truthy(M))
        {
            unbuckle_feedback(M, user);
            add_fingerprint(user);
            if (isliving(M.pulledby))
            {
                mob_living L = (mob_living)M.pulledby;
                await L.set_pull_offsets(M, L.grab_state);
            }
        }
        return M;
    }

    /// Feedback displayed to nearby players after a mob is unbuckled from src.
    // tg: code/game/objects/buckling.dm:376
    public virtual void unbuckle_feedback(mob_living unbuckled_mob = null, mob unbuckler = null)
    {
        if (unbuckled_mob == unbuckler)
        {
            unbuckled_mob.visible_message(
                span_notice(I18n.T("[] unbuckles []self from [].", unbuckler, unbuckler.p_them(), this)),
                span_notice(I18n.T("You unbuckle yourself from [].", this)),
                span_hear(I18n.T("You hear metal clanking."))
            );
        }
        else
        {
            unbuckled_mob.visible_message(
                span_notice(I18n.T("[] unbuckles [] from [].", unbuckler, unbuckled_mob, this)),
                span_notice(I18n.T("[] unbuckles you from [].", unbuckler, this)),
                span_hear(I18n.T("You hear metal clanking."))
            );
        }
    }
}

// tg: code/game/objects/buckling.dm:136
public partial class obj : atom_movable
{
    // tg: code/game/objects/buckling.dm:136
    [DmParams("M", "force", "check_loc")]
    public override async DmTask<object> buckle_mob(mob_living M = null, float? force = null, float? check_loc = null, float? buckle_mob_flags = null)
    {
        force ??= FALSE;
        check_loc ??= TRUE;
        object dot = await base.buckle_mob(M, force, check_loc, buckle_mob_flags);
        if (Dm.truthy(dot))
        {
            if (Dm.truthy(Dm.bitand(resistance_flags, ON_FIRE))) //Sets the mob on fire if you buckle them to a burning atom/movableect
            {
                await M.adjust_fire_stacks(1);
                M.ignite_mob();
            }
            if (Dm.truthy(buckle_sound))
                playsound(this, buckle_sound, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, pressure_affected: TRUE, ignore_walls: FALSE);
        }
        return dot;
    }

    // tg: code/game/objects/buckling.dm:146
    public override async DmTask<object> unbuckle_mob(mob_living buckled_mob = null, float? force = null, float? can_fall = null)
    {
        object dot = await base.unbuckle_mob(buckled_mob, force, can_fall);
        if (Dm.truthy(dot) && Dm.truthy(unbuckle_sound))
            playsound(this, unbuckle_sound, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, pressure_affected: TRUE, ignore_walls: FALSE);
        return dot;
    }
}
