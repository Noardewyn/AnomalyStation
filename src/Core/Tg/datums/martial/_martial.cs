// Translated from tgstation (snapshot 1d99bd7f): code/datums/martial/_martial.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): the martial art base whole (the holder's unarmed strike, grab and grab resisting hooks, the
// streak with its combo HUD, teaching and unlearning, switching styles). The var block is generated (D111). tg's attack procs never
// sleep in a signal handler; their port's are async (they call the damage and block procs), so the handlers run them with NoSleep.

// tg: code/datums/martial/_martial.dm:1
[DmPath("/datum/martial_art")]
public partial class datum_martial_art : datum
{
    // tg: code/datums/martial/_martial.dm:46
    public override object serialize_list(DmList options = null, DmList semvers = null)
    {
        // . = ..(); .["name"] = name; .["id"] = id; .["pacifist_style"] = pacifist_style; SET_SERIALIZATION_SEMVER(semvers, "1.0.0")
        Dm.Deferred("code/datums/martial/_martial.dm:46", "serialize_list: the datum serialization (datum.cs) (M10+)");
        return default;
    }

    // tg: code/datums/martial/_martial.dm:56
    [DmParams("new_origin")]
    public override async DmTask New(params object[] __extra)
    {
        set_origin(Dm.arg<datum>(__extra, 0));
    }

    // tg: code/datums/martial/_martial.dm:59
    public override object Destroy(float? force = null)
    {
        if (holder != null)
            Waiting.NoSleep(unlearn(holder));
        if (origin != null)
            set_origin(null);
        return base.Destroy(force);
    }

    // tg: code/datums/martial/_martial.dm:66
    public virtual void set_origin(datum new_origin = null)
    {
        if (origin != null)
        {
            UnregisterSignal(origin, COMSIG_QDELETING);
            origin = null;
        }
        if (new_origin == null)
            return;
        origin = new_origin;
        RegisterSignal(origin, COMSIG_QDELETING, nameof(clear_origin));
    }

    // tg: code/datums/martial/_martial.dm:75
    [SignalHandler]
    public virtual void clear_origin()
    {
        qdel(this);
    }

    // tg: code/datums/martial/_martial.dm:79
    [SignalHandler]
    public virtual void clear_holder(datum source = null)
    {
        Waiting.NoSleep(unlearn(holder));
    }

    /// Signal proc for [COMSIG_LIVING_UNARMED_ATTACK] to hook into the appropriate proc
    // tg: code/datums/martial/_martial.dm:84
    [SignalHandler]
    public virtual object unarmed_strike(mob_living source = null, atom attack_target = null, object proximity = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity) || !isliving(attack_target))
            return NONE;

        if (HAS_TRAIT(attack_target, TRAIT_MARTIAL_ARTS_IMMUNE))
            return NONE;

        if (!Dm.truthy(can_use(source)))
            return NONE;

        mob_living defender = attack_target as mob_living;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return Waiting.NoSleep(disarm_act(source, defender));

        if (Dm.truthy(source.combat_mode))
        {
            if (HAS_TRAIT(source, TRAIT_PACIFISM) && !Dm.truthy(pacifist_style))
                return NONE;

            return Waiting.NoSleep(harm_act(source, defender));
        }

        return Waiting.NoSleep(help_act(source, defender));
    }

    /// Signal proc for [COMSIG_LIVING_GRAB] to hook into the grab
    // tg: code/datums/martial/_martial.dm:107
    [SignalHandler]
    public virtual object attempt_grab(mob_living source = null, mob_living grabbing = null)
    {
        if (HAS_TRAIT(grabbing, TRAIT_MARTIAL_ARTS_IMMUNE))
            return NONE;

        if (!Dm.truthy(source.can_unarmed_attack())) // For parity with unarmed attacks
            return NONE;

        if (!Dm.truthy(can_use(source)))
            return NONE;

        return Waiting.NoSleep(grab_act(source, grabbing));
    }

    /// Signal proc for [COMSIG_MOVABLE_GRABBED_RESISTING] to modify grab stats
    // tg: code/datums/martial/_martial.dm:122
    [SignalHandler]
    public virtual void grabbed_resisting(mob_living source = null, mob_living grabbed = null, DmList grab_stats = null)
    {
        if (!Dm.truthy(can_use(source)))
            return;

        grab_stats[GRAB_STAT_EFFECTIVE_STATE] = Dm.num(grab_stats[GRAB_STAT_EFFECTIVE_STATE]) + grab_state_modifier;
        grab_stats[GRAB_STAT_FAIL_DAMAGE] = Dm.num(grab_stats[GRAB_STAT_FAIL_DAMAGE]) + grab_damage_modifier;
        grab_stats[GRAB_STAT_ESCAPE_CHANCE] = Dm.num(grab_stats[GRAB_STAT_ESCAPE_CHANCE]) + grab_escape_chance_modifier;
    }

    /**
     * Called when help-intenting on someone
     *
     * What is checked going into this:
     * Adjacency, [TRAIT_MARTIAL_ARTS_IMMUNE], attacker incapacitated, can_unarmed_attack, can_use
     *
     * What is NOT:
     * check_block
     *
     * Arguments
     * * mob/living/attacker - The mob attacking
     * * mob/living/defender - The mob being attacked
     *
     * Returns
     * * MARTIAL_ATTACK_SUCCESS - The attack was successful, and the attack chain should end.
     * * MARTIAL_ATTACK_FAIL - The attack was unsuccessful, and the attack chain should continue.
     * * MARTIAL_ATTACK_INVALID - The attack was invalid, and the attack chain should continue.
     */
    // tg: code/datums/martial/_martial.dm:149
    [DmProtected]
    public virtual async DmTask<object> help_act(mob_living attacker = null, mob_living defender = null)
    {
        return MARTIAL_ATTACK_INVALID;
    }

    /// Called when disarm-intenting on someone (the checks and the returns are help_act's)
    // tg: code/datums/martial/_martial.dm:173
    [DmProtected]
    public virtual async DmTask<object> disarm_act(mob_living attacker = null, mob_living defender = null)
    {
        return MARTIAL_ATTACK_INVALID;
    }

    /// Called when harm-intenting on someone (the checks and the returns are help_act's, and TRAIT_PACIFISM)
    // tg: code/datums/martial/_martial.dm:197
    [DmProtected]
    public virtual async DmTask<object> harm_act(mob_living attacker = null, mob_living defender = null)
    {
        return MARTIAL_ATTACK_INVALID;
    }

    /// Called when grabbing someone (the checks and the returns are help_act's)
    // tg: code/datums/martial/_martial.dm:221
    [DmProtected]
    public virtual async DmTask<object> grab_act(mob_living attacker = null, mob_living defender = null)
    {
        return MARTIAL_ATTACK_INVALID;
    }

    /// Checks if the passed mob can use this martial art.
    // tg: code/datums/martial/_martial.dm:236
    public virtual object can_use(mob_living martial_artist = null)
    {
        return TRUE;
    }

    /// Gets the bodypart we will be attacking with for unarmed attacks, if any. Returns null if no specific limb is preferred.
    // tg: code/datums/martial/_martial.dm:246
    [ShouldNotOverride]
    public virtual obj_item_bodypart get_attacking_limb(mob_living martial_artist = null, mob_living target = null)
    {
        if (!Dm.truthy(can_use(martial_artist)))
            return null;
        object preferred_zone = get_prefered_attacking_limb(martial_artist, target);
        if (!Dm.truthy(preferred_zone))
            return null;
        return martial_artist.get_bodypart(Dm.str(preferred_zone)) as obj_item_bodypart;
    }

    /// Gets the zone of the bodypart we prefer to attack with, if any.
    // tg: code/datums/martial/_martial.dm:262
    [DmProtected]
    public virtual object get_prefered_attacking_limb(mob_living martial_artist = null, mob_living target = null)
    {
        return null;
    }

    /// Adds the passed element to the current streak, resetting it if the target is not the same as the last target.
    // tg: code/datums/martial/_martial.dm:275
    public virtual async DmTask add_to_streak(string element = null, mob_living defender = null)
    {
        if (!IS_WEAKREF_OF(defender, current_target))
            await reset_streak(defender);
        streak += element;
        if (length(streak) > max_streak_length)
            streak = copytext(streak, 2); // 1 + length(streak[1]): its first character
        if (!Dm.truthy(display_combos))
            return;
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_streak), null, FALSE), combo_timer, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/datums/martial/_martial.dm", line: 283);
        if (holder.hud_used?.screen_objects?[HUD_MOB_COMBO] is atom_movable_screen_combo combo_display)
            await combo_display.update_icon_state(streak, combo_timer - 2 * SECONDS);
    }

    /// Resets the current streak.
    // tg: code/datums/martial/_martial.dm:294
    public virtual async DmTask reset_streak(mob_living new_target = null, float? update_icon = null)
    {
        update_icon ??= TRUE;
        if (Dm.truthy(timerid))
            deltimer(timerid);
        current_target = WEAKREF(new_target) as datum_weakref;
        streak = "";
        if (holder?.hud_used?.screen_objects?[HUD_MOB_COMBO] is atom_movable_screen_combo combo_display && Dm.truthy(display_combos) && Dm.truthy(update_icon))
            await combo_display.update_icon_state(streak);
    }

    /// Signal proc for [COMSIG_LIVING_TABLE_SLAMMING] and [COMSIG_LIVING_TABLE_LIMB_SLAMMING]
    // tg: code/datums/martial/_martial.dm:308
    [SignalHandler]
    public virtual void smash_table(mob_living source = null, mob_living pushed_mob = null, obj_structure_table table = null)
    {
        if (Dm.truthy(smashes_tables))
            Waiting.NoSleep(table.deconstruct(FALSE));
    }

    /// Teaches the passed mob this martial art.
    // tg: code/datums/martial/_martial.dm:322
    [ShouldNotOverride]
    public virtual async DmTask<object> teach(mob_living new_holder = null)
    {
        if (!Dm.truthy(can_teach(new_holder)) || holder == new_holder)
            return FALSE;

        holder = new_holder;
        if (origin != new_holder)
            RegisterSignal(holder, COMSIG_QDELETING, nameof(clear_holder));
        // locked martial arts always get inserted as the next up
        // (so if you learn two locked martial arts, and you get rid of the first, the second will slot itself in)
        if (Dm.truthy(locked_to_use) && LAZYLEN(new_holder.martial_arts) >= 2)
            LAZYINSERT(ref new_holder.martial_arts, 2, this);
        else
            LAZYADD(ref new_holder.martial_arts, this);

        if (Dm.truthy(LAZYLEN(new_holder.martial_arts) == 1 ? get_style_help() : (Dm.locate<datum_action_swap_arts>(new_holder.actions) == null ? TRUE : FALSE)))
        {
            var new_action = Dm.@new<datum_action_swap_arts>(new_holder, this);
            await new_action.Grant(new_holder);
        }
        if (LAZYLEN(new_holder.martial_arts) >= 2)
            // if the active one is locked, this will no-op, which is fine
            await new_holder.switch_style(LAZYACCESS(new_holder.martial_arts, 1) as datum_martial_art, this); // GET_ACTIVE_MARTIAL_ART (__DEFINES/combat.dm:353), inline
        else if (!Dm.truthy(active))
            await activate_style(new_holder);
        return TRUE;
    }

    /// Checks if the passed mob can be taught this martial art.
    // tg: code/datums/martial/_martial.dm:355
    public virtual object can_teach(mob_living new_holder = null)
    {
        return isliving(new_holder) ? TRUE : FALSE;
    }

    /// Removes this martial art from the passed mob.
    // tg: code/datums/martial/_martial.dm:369
    [ShouldNotOverride]
    public virtual async DmTask<object> unlearn(mob_living old_holder = null)
    {
        if (old_holder != holder)
            return FALSE;

        if (LAZYLEN(old_holder.martial_arts) >= 2 && !QDELING(old_holder))
            await old_holder.switch_style(this, LAZYACCESS(old_holder.martial_arts, 2) as datum_martial_art); // GET_NEXT_MARTIAL_ART (__DEFINES/combat.dm:355), inline
        else if (Dm.truthy(active))
            await deactivate_style(old_holder);
        if (origin != old_holder)
            UnregisterSignal(old_holder, COMSIG_QDELETING);
        LAZYREMOVE(ref old_holder.martial_arts, this);
        holder = null;
        if (LAZYLEN(old_holder.martial_arts) < 1) //no more arts, we check above to switch style already.
        {
            var swap_button = Dm.locate<datum_action_swap_arts>(old_holder.actions) as datum_action_swap_arts;
            qdel(swap_button);
        }
        return TRUE;
    }

    /// Called when this martial art is added to a mob.
    // tg: code/datums/martial/_martial.dm:398
    [ShouldCallParent]
    public virtual async DmTask activate_style(mob_living new_holder = null)
    {
        active = TRUE;
        RegisterSignal(new_holder, COMSIG_LIVING_UNARMED_ATTACK, nameof(unarmed_strike));
        RegisterSignal(new_holder, COMSIG_LIVING_GRAB, nameof(attempt_grab));
        RegisterSignal(new_holder, COMSIG_MOVABLE_GRABBED_RESISTING, nameof(grabbed_resisting));
        RegisterSignals(new_holder, Dm.list(COMSIG_LIVING_TABLE_SLAMMING, COMSIG_LIVING_TABLE_LIMB_SLAMMING), nameof(smash_table));
        if (Dm.truthy(display_combos))
        {
            RegisterSignal(new_holder, COMSIG_MOB_HUD_CREATED, nameof(on_hud_created));
            if (new_holder.hud_used != null)
                on_hud_created(new_holder);
        }
    }

    /// Called when this martial art is removed from a mob.
    // tg: code/datums/martial/_martial.dm:418
    [ShouldCallParent]
    public virtual async DmTask deactivate_style(mob_living remove_from = null)
    {
        active = FALSE;
        UnregisterSignal(remove_from, Dm.list(
            COMSIG_LIVING_UNARMED_ATTACK,
            COMSIG_LIVING_GRAB,
            COMSIG_LIVING_TABLE_SLAMMING,
            COMSIG_LIVING_TABLE_LIMB_SLAMMING,
            COMSIG_MOVABLE_GRABBED_RESISTING,
            COMSIG_MOB_HUD_CREATED));
        if (remove_from.hud_used != null)
            await remove_from.hud_used.remove_screen_object(HUD_MOB_COMBO);
    }

    ///Gives the owner of the martial art the combo HUD.
    // tg: code/datums/martial/_martial.dm:432
    [SignalHandler]
    public virtual void on_hud_created(mob source = null)
    {
        Waiting.NoSleep(source.hud_used.add_screen_object(typeof(atom_movable_screen_combo), HUD_MOB_COMBO, HUD_GROUP_INFO, update_screen: TRUE));
    }

    ///To be overwritten for artstyle help.
    // tg: code/datums/martial/_martial.dm:458
    public virtual object get_style_help()
    {
        return FALSE;
    }
}

public partial class mob_living
{
    // tg: code/datums/martial/_martial.dm:436
    public virtual async DmTask cycle_style()
    {
        var current = LAZYACCESS(martial_arts, 1) as datum_martial_art; // GET_ACTIVE_MARTIAL_ART, inline
        var next = LAZYACCESS(martial_arts, 2) as datum_martial_art; // GET_NEXT_MARTIAL_ART, inline

        if (Dm.truthy(current.locked_to_use))
        {
            to_chat(this, span_warning(I18n.T("You can't stop practicing []! It's too ingrained in your muscle memory.", current)));
            return;
        }

        await switch_style(current, next);
        to_chat(this, span_notice(I18n.T("You stop practicing [] and start practicing [].", current, next)));
    }

    /// Deactivates the current martial art and activates the next one.
    // tg: code/datums/martial/_martial.dm:448
    public virtual async DmTask switch_style(datum_martial_art current_martial = null, datum_martial_art next_martial = null)
    {
        if (Dm.truthy(current_martial.locked_to_use))
            return;
        // something's wrong if this assertion fails, but not terribly wrong that we need a stack trace
        if (!Dm.truthy(current_martial.active) || Dm.truthy(next_martial.active))
            return;

        var swap_button = Dm.locate<datum_action_swap_arts>(actions) as datum_action_swap_arts;
        swap_button.current_used_art = next_martial;

        await current_martial.deactivate_style(this);
        await next_martial.activate_style(this);
        // front of the list with ye
        LAZYREMOVE(ref martial_arts, next_martial);
        LAZYINSERT(ref martial_arts, 1, next_martial);
        // back of the list with ye
        LAZYREMOVE(ref martial_arts, current_martial);
        LAZYADD(ref martial_arts, current_martial);
    }
}
