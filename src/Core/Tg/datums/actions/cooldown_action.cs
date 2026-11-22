// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/cooldown_action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2: the goose's vomit is the first live ability; C3-C5 build on it).

/// Preset for an action that has a cooldown.
// tg: code/datums/actions/cooldown_action.dm:5
public partial class datum_action_cooldown : datum_action
{
    // tg: code/datums/actions/cooldown_action.dm:1
    private const float COOLDOWN_NO_DISPLAY_TIME = 180 * SECONDS;

    // tg: code/datums/actions/cooldown_action.dm:49
    [DmParams("Target", "original")]
    public override async DmTask New(params object[] __extra)
    {
        object original = Dm.arg(__extra, 1) ?? TRUE;
        await base.New(__extra);
        if (Dm.truthy(active_background_icon_state))
            base_background_icon_state = Dm.truthy(base_background_icon_state) ? base_background_icon_state : background_icon_state;
        if (Dm.truthy(active_overlay_icon_state))
            base_overlay_icon_state = Dm.truthy(base_overlay_icon_state) ? base_overlay_icon_state : overlay_icon_state;
        if (Dm.truthy(active_icon_state))
            base_icon_state = Dm.truthy(base_icon_state) ? base_icon_state : button_icon_state;

        if (melee_cooldown_time == null)
            melee_cooldown_time = cooldown_time;

        if (Dm.truthy(original))
            create_sequence_actions();
    }

    // tg: code/datums/actions/cooldown_action.dm:64
    public override async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await base.create_button(viewer) as atom_movable_screen_movable_action_button;
        button.maptext = "";
        button.maptext_x = 4;
        button.maptext_y = 2;
        button.maptext_width = 32;
        button.maptext_height = 16;
        return button;
    }

    // tg: code/datums/actions/cooldown_action.dm:73
    public override async DmTask update_button_status(atom_movable_screen_movable_action_button button = null, float? force = null)
    {
        force ??= FALSE;
        await base.update_button_status(button, force);
        float time_left = Dm.max(next_use_time - world.time, 0);
        if (!Dm.truthy(text_cooldown) || owner == null || time_left == 0 || time_left >= COOLDOWN_NO_DISPLAY_TIME)
            button.maptext = "";
        else
        {
            if (cooldown_rounding > 0)
                button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("[]", Dm.round(time_left / 10, cooldown_rounding)));
            else
                button.maptext = MAPTEXT_TINY_UNICODE(Dm.text("[]", Dm.round(time_left / 10)));
        }

        if (!Dm.truthy(await IsAvailable()) || !Dm.truthy(is_action_active(button)))
            return;
        // If we don't change the icon state, or don't apply a special overlay,
        if (Dm.truthy(active_background_icon_state) || Dm.truthy(active_icon_state) || Dm.truthy(active_overlay_icon_state))
            return;
        // ...we need to show it's active somehow. So, make it greeeen
        button.color = COLOR_GREEN;
    }

    // tg: code/datums/actions/cooldown_action.dm:92
    public override void apply_button_background(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        if (Dm.truthy(active_background_icon_state))
            background_icon_state = (Dm.truthy(is_action_active(current_button)) ? active_background_icon_state : base_background_icon_state) as string;
        base.apply_button_background(current_button, force);
    }

    // tg: code/datums/actions/cooldown_action.dm:97
    public override void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        if (Dm.truthy(active_icon_state))
            button_icon_state = (Dm.truthy(is_action_active(current_button)) ? active_icon_state : base_icon_state) as string;
        base.apply_button_icon(current_button, force);
    }

    // tg: code/datums/actions/cooldown_action.dm:102
    public override void apply_button_overlay(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        if (Dm.truthy(active_overlay_icon_state))
            overlay_icon_state = (Dm.truthy(is_action_active(current_button)) ? active_overlay_icon_state : base_overlay_icon_state) as string;
        base.apply_button_overlay(current_button, force);
    }

    // tg: code/datums/actions/cooldown_action.dm:107
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return Dm.truthy(click_to_activate) && current_button.our_hud?.mymob?.click_intercept == this ? TRUE : FALSE;
    }

    // tg: code/datums/actions/cooldown_action.dm:110
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(initialized_actions);
        return base.Destroy(force);
    }

    // tg: code/datums/actions/cooldown_action.dm:114
    public override async DmTask<object> Grant(mob granted_to = null, params object[] __extra)
    {
        object dot = await base.Grant(granted_to, __extra);
        if (owner == null)
            return dot;
        await build_all_button_icons();
        if (next_use_time > world.time)
            START_PROCESSING(SSfastprocess, this);
        RegisterSignal(granted_to, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(handle_melee_attack));
        foreach (datum_action_cooldown ability in Dm.each_cast<datum_action_cooldown>(initialized_actions))
            await ability.Grant(granted_to);
        return dot;
    }

    // tg: code/datums/actions/cooldown_action.dm:125
    public override async DmTask Remove(mob removed_from = null)
    {
        UnregisterSignal(removed_from, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        if (Dm.truthy(click_to_activate) && removed_from.click_intercept == this)
            await unset_click_ability(removed_from, refund_cooldown: FALSE);
        foreach (datum_action_cooldown ability in Dm.each_cast<datum_action_cooldown>(initialized_actions))
            await ability.Remove(removed_from);
        await base.Remove(removed_from);
    }

    // tg: code/datums/actions/cooldown_action.dm:133
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        return Dm.truthy(await base.IsAvailable(feedback)) && next_use_time <= world.time ? TRUE : FALSE;
    }

    /// Initializes any sequence actions
    // tg: code/datums/actions/cooldown_action.dm:137
    public virtual void create_sequence_actions()
    {
        if (!Dm.truthy(LAZYLEN(sequence_actions)))
            return;
        // remove existing actions if any
        QDEL_LIST(initialized_actions);
        initialized_actions = Dm.list();
        foreach (object type_path in Dm.each<object>(sequence_actions))
        {
            datum_action_cooldown ability = Dm.@new((Type)type_path, target, FALSE) as datum_action_cooldown;
            // prevents clients from using the individual abilities in sequences (this stops it from being added to mob actions when granted as well)
            ability.owner_has_control = FALSE;
            // [ability] = delay
            initialized_actions[ability] = sequence_actions[type_path];
        }
    }

    /// Starts a cooldown time to be shared with similar abilities
    /// Will use default cooldown time if an override is not specified
    // tg: code/datums/actions/cooldown_action.dm:152
    public virtual void StartCooldown(object override_cooldown_time = null, object override_melee_cooldown_time = null)
    {
        // "Shared cooldowns" covers actions which are not the same type,
        // but have the same cooldown group and are on the same mob
        if (shared_cooldown != NONE)
            StartCooldownOthers(override_cooldown_time);

        StartCooldownSelf(override_cooldown_time);

        if (isnum(override_melee_cooldown_time))
            next_melee_use_time = world.time + Dm.num(override_melee_cooldown_time);
        else
            next_melee_use_time = world.time + Dm.num(melee_cooldown_time);
    }

    /// Starts a cooldown time for this ability only
    /// Will use default cooldown time if an override is not specified
    // tg: code/datums/actions/cooldown_action.dm:167
    public virtual void StartCooldownSelf(object override_cooldown_time = null)
    {
        if (isnum(override_cooldown_time))
            next_use_time = world.time + Dm.num(override_cooldown_time);
        else
            next_use_time = world.time + Dm.num(cooldown_time);
        // Don't start a cooldown if we have a cooldown time of 0 seconds
        if (next_use_time == world.time)
            return;
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
        START_PROCESSING(SSfastprocess, this);
    }

    /// Starts a cooldown time for other abilities that share a cooldown with this. Has some niche usage with more complicated attack ai!
    /// Will use default cooldown time if an override is not specified
    // tg: code/datums/actions/cooldown_action.dm:180
    public virtual void StartCooldownOthers(object override_cooldown_time = null)
    {
        if (!Dm.truthy(length(owner?.actions)))
            return; // Possible if they have an action they don't control
        foreach (datum_action_cooldown shared_ability in Dm.each<datum_action_cooldown>(Dm.sub(owner.actions, this)))
        {
            if (!Dm.truthy(Dm.bitand(shared_cooldown, shared_ability.shared_cooldown)))
                continue;
            if (isnum(override_cooldown_time))
                shared_ability.StartCooldownSelf(override_cooldown_time);
            else
                shared_ability.StartCooldownSelf(cooldown_time);
        }
    }

    /// Resets the cooldown of this ability
    // tg: code/datums/actions/cooldown_action.dm:192
    public virtual void ResetCooldown()
    {
        next_use_time = world.time;
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
    }

    /// Re-enables this cooldown action
    // tg: code/datums/actions/cooldown_action.dm:197
    public virtual void enable()
    {
        action_disabled = FALSE;
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
    }

    /// Disables this cooldown action
    // tg: code/datums/actions/cooldown_action.dm:202
    public virtual void disable()
    {
        action_disabled = TRUE;
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
    }

    /// Re-enables all cooldown actions
    // tg: code/datums/actions/cooldown_action.dm:207
    public virtual void enable_cooldown_actions()
    {
        foreach (datum_action_cooldown cd_action in Dm.each<datum_action_cooldown>(owner.actions))
            cd_action.enable();
    }

    /// Disables all cooldown actions
    // tg: code/datums/actions/cooldown_action.dm:212
    public virtual void disable_cooldown_actions()
    {
        foreach (datum_action_cooldown cd_action in Dm.each<datum_action_cooldown>(owner.actions))
            cd_action.disable();
    }

    // tg: code/datums/actions/cooldown_action.dm:216
    [DmParams("clicker", "trigger_flags", "target")]
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        atom target = Dm.arg<atom>(__extra, 0);
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        if (owner == null)
            return FALSE;

        mob user = clicker ?? owner;

        // If our cooldown action is a click_to_activate action:
        // The actual action is activated on whatever the user clicks on -
        // the target is what the action is being used on
        // In trigger, we handle setting the click intercept
        if (Dm.truthy(click_to_activate))
        {
            if (target != null)
                // For automatic / mob handling
                return await InterceptClickOn(user as mob_living, null, target);

            datum already_set = user.click_intercept;
            if (already_set == this)
                // if we clicked ourself and we're already set, unset and return
                return await unset_click_ability(user, refund_cooldown: TRUE);

            else if (already_set is datum_action_cooldown already_set_cooldown)
                // if we have an active set already, unset it before we set our's
                await already_set_cooldown.unset_click_ability(user, refund_cooldown: TRUE);

            return await set_click_ability(user);
        }

        // If our cooldown action is not a click_to_activate action:
        // We can just continue on and use the action
        // the target is the user of the action (often, the owner)
        return await PreActivate(user);
    }

    /// Intercepts client owner clicks to activate the ability
    // tg: code/datums/actions/cooldown_action.dm:251
    public virtual async DmTask<object> InterceptClickOn(mob_living clicker = null, object @params = null, atom target = null)
    {
        if (!Dm.truthy(await IsAvailable(feedback: TRUE)))
            return FALSE;
        if (target == null)
            return FALSE;
        // The actual action begins here
        if (!Dm.truthy(await PreActivate(target)))
            return FALSE;

        // And if we reach here, the action was complete successfully
        if (Dm.truthy(unset_after_click))
            await unset_click_ability(clicker, refund_cooldown: FALSE);
        clicker.next_click = world.time + click_cd_override;

        return TRUE;
    }

    /// For signal calling
    // tg: code/datums/actions/cooldown_action.dm:268
    public virtual async DmTask<object> PreActivate(atom target = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(owner._listen_lookup?[COMSIG_MOB_ABILITY_STARTED]) ? NONE : owner._SendSignal(COMSIG_MOB_ABILITY_STARTED, Dm.list(owner, this, target)), COMPONENT_BLOCK_ABILITY_START)))
            return null;
        // Note, that PreActivate handles no cooldowns at all by default.
        // Be sure to call StartCooldown() in Activate() where necessary.
        object dot = await Activate(target);
        // There is a possibility our action (or owner) is qdeleted in Activate().
        if (!QDELETED(this) && !QDELETED(owner) && Dm.truthy(owner._listen_lookup?[COMSIG_MOB_ABILITY_FINISHED]))
            owner._SendSignal(COMSIG_MOB_ABILITY_FINISHED, Dm.list(owner, this));
        return dot;
    }

    /// To be implemented by subtypes (if not generic)
    // tg: code/datums/actions/cooldown_action.dm:279
    public virtual async DmTask<object> Activate(atom target = null)
    {
        float total_delay = 0;
        foreach (datum_action_cooldown ability in Dm.each_cast<datum_action_cooldown>(initialized_actions))
        {
            if (Dm.truthy(LAZYLEN(ability.initialized_actions)))
                ability.initialized_actions = Dm.list();
            _addtimer(Dm.@new<datum_callback>(ability, nameof(Activate), target), total_delay, file: "code/datums/actions/cooldown_action.dm", line: 288);
            total_delay += Dm.num(initialized_actions[ability]);
        }
        StartCooldown();
        return null;
    }

    /// Cancels melee attacks if they are on cooldown.
    // tg: code/datums/actions/cooldown_action.dm:289
    [SignalHandler]
    public virtual object handle_melee_attack(mob source = null, mob target = null)
    {
        if (next_melee_use_time > world.time)
            return COMPONENT_HOSTILE_NO_ATTACK;
        return null;
    }

    // tg: code/datums/actions/cooldown_action.dm:294
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (owner == null || (next_use_time - world.time) <= 0)
        {
            Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
            STOP_PROCESSING(SSfastprocess, this);
            return null;
        }

        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
        return null;
    }

    /**
     * Set our action as the click override on the passed mob.
     */
    // tg: code/datums/actions/cooldown_action.dm:305
    public virtual async DmTask<object> set_click_ability(mob on_who = null)
    {
        on_who.click_intercept = this;
        if (Dm.truthy(ranged_mousepointer))
        {
            if (on_who.client != null)
                on_who.client.mouse_override_icon = ranged_mousepointer;
            on_who.update_mouse_pointer();
        }
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
        return TRUE;
    }

    /**
     * Unset our action as the click override of the passed mob.
     *
     * if refund_cooldown is TRUE, we are being unset by the user clicking the action off
     * if refund_cooldown is FALSE, we are being forcefully unset, likely by someone actually using the action
     */
    // tg: code/datums/actions/cooldown_action.dm:321
    public virtual async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        on_who.click_intercept = null;
        if (Dm.truthy(ranged_mousepointer))
        {
            if (on_who.client != null)
                on_who.client.mouse_override_icon = null; // initial(on_who.client?.mouse_override_icon)
            on_who.update_mouse_pointer();
        }
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
        return TRUE;
    }
}
