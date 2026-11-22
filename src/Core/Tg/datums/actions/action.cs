// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/action.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3 (D146: action buttons move from M9 into M4c): the whole file. The key-combo input window of
// begin_creating_bind (tgui_input_keycombo) is a deferred leaf (tgui, M9). The var block is generated.

/**
 * # Action system
 *
 * A simple base for an modular behavior attached to atom or datum.
 */
// tg: code/datums/actions/action.dm:6
[DmPath("/datum/action")]
public partial class datum_action : datum
{
    // tg: code/datums/actions/action.dm:60
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        object Target = Dm.arg(__extra, 0);
        link_to(Target);
    }

    /// Links the passed target to our action, registering any relevant signals
    // tg: code/datums/actions/action.dm:64
    public virtual void link_to(object Target = null)
    {
        target = Target as datum;
        RegisterSignal(target, COMSIG_QDELETING, nameof(clear_ref), @override: TRUE);

        if (isatom(target))
            RegisterSignal(target, COMSIG_ATOM_UPDATED_ICON, nameof(on_target_icon_update));

        if (istype(target, typeof(datum_mind)))
            RegisterSignal(target, COMSIG_MIND_TRANSFERRED, nameof(on_target_mind_swapped));
    }

    // tg: code/datums/actions/action.dm:74
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(owner))
            Waiting.NoSleep(Remove(owner));
        target = null;
        QDEL_LIST_ASSOC_VAL(viewers); // Qdel the buttons in the viewers list **NOT THE HUDS**
        return base.Destroy(force);
    }

    /// Signal proc that clears any references based on the owner or target deleting
    /// If the owner's deleted, we will simply remove from them, but if the target's deleted, we will self-delete
    // tg: code/datums/actions/action.dm:83
    [SignalHandler]
    public virtual void clear_ref(datum @ref = null)
    {
        if (@ref == owner)
            Waiting.NoSleep(Remove(owner));
        if (@ref == target)
            qdel(this);
    }

    /// Grants the action to the passed mob, making it the owner
    // tg: code/datums/actions/action.dm:91
    public virtual async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        if (grant_to == null)
        {
            await Remove(owner);
            return null;
        }
        if (grant_to == owner)
            return null; // We already have it
        mob previous_owner = owner;
        owner = grant_to;
        if (previous_owner != null)
            await Remove(previous_owner);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ACTION_GRANTED]))
            this._SendSignal(COMSIG_ACTION_GRANTED, Dm.list(this, owner));
        if (Dm.truthy(owner._listen_lookup?[COMSIG_MOB_GRANTED_ACTION]))
            owner._SendSignal(COMSIG_MOB_GRANTED_ACTION, Dm.list(owner, this));
        RegisterSignal(owner, COMSIG_QDELETING, nameof(clear_ref), @override: TRUE);

        // Register some signals based on our check_flags
        // so that our button icon updates when relevant
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_CONSCIOUS)))
            RegisterSignal(owner, COMSIG_MOB_STATCHANGE, nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_INCAPACITATED)))
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), SIGNAL_REMOVETRAIT(TRAIT_INCAPACITATED)), nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_IMMOBILE)))
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_IMMOBILIZED), SIGNAL_REMOVETRAIT(TRAIT_IMMOBILIZED)), nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_HANDS_BLOCKED)))
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED), SIGNAL_REMOVETRAIT(TRAIT_HANDS_BLOCKED)), nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_LYING)))
            RegisterSignal(owner, COMSIG_LIVING_SET_BODY_POSITION, nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_PHASED)))
            RegisterSignals(owner, Dm.list(SIGNAL_ADDTRAIT(TRAIT_MAGICALLY_PHASED), SIGNAL_REMOVETRAIT(TRAIT_MAGICALLY_PHASED)), nameof(update_status_on_signal));
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_OPEN_TURF)))
            RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(update_status_on_signal));
        if (Dm.truthy(owner_has_control))
        {
            RegisterSignal(grant_to, COMSIG_MOB_KEYDOWN, nameof(keydown), @override: TRUE);
            await GiveAction(grant_to);
        }
        return null;
    }

    /// Remove the passed mob from being owner of our action
    // tg: code/datums/actions/action.dm:126
    [ShouldCallParent]
    public virtual async DmTask Remove(mob remove_from = null)
    {
        foreach (datum_hud hud in Dm.each<datum_hud>(viewers))
        {
            if (!Dm.truthy(hud.mymob))
                continue;
            HideFrom(hud.mymob);
        }
        if (remove_from != null)
            LAZYREMOVE(ref remove_from.actions, this); // We aren't always properly inserted into the viewers list, gotta make sure that action's cleared
        viewers = Dm.list();
        UnregisterSignal(remove_from, COMSIG_MOB_KEYDOWN);

        if (remove_from == null)
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ACTION_REMOVED]))
            this._SendSignal(COMSIG_ACTION_REMOVED, Dm.list(this, remove_from));
        if (Dm.truthy(remove_from._listen_lookup?[COMSIG_MOB_REMOVED_ACTION]))
            remove_from._SendSignal(COMSIG_MOB_REMOVED_ACTION, Dm.list(remove_from, this));
        UnregisterSignal(remove_from, COMSIG_QDELETING);

        // Clean up our check_flag signals
        UnregisterSignal(remove_from, Dm.list(
            COMSIG_LIVING_SET_BODY_POSITION,
            COMSIG_MOB_STATCHANGE,
            COMSIG_MOVABLE_MOVED,
            SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED),
            SIGNAL_ADDTRAIT(TRAIT_IMMOBILIZED),
            SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED),
            SIGNAL_ADDTRAIT(TRAIT_MAGICALLY_PHASED),
            SIGNAL_REMOVETRAIT(TRAIT_HANDS_BLOCKED),
            SIGNAL_REMOVETRAIT(TRAIT_IMMOBILIZED),
            SIGNAL_REMOVETRAIT(TRAIT_INCAPACITATED),
            SIGNAL_REMOVETRAIT(TRAIT_MAGICALLY_PHASED)
        ));

        if (target == remove_from)
            RegisterSignal(target, COMSIG_QDELETING, nameof(clear_ref));
        if (owner == remove_from)
            owner = null;
    }

    /// Actually triggers the effects of the action.
    /// Called when the on-screen button is clicked, for example.
    // tg: code/datums/actions/action.dm:165
    [ShouldCallParent]
    public virtual async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        // DEVIATION(DV27): the interaction guard of D132 for an action datum (as the storage datum's StorageGuard): an action
        // whose own type is inert (a /datum/action subtype whose tg procs nobody declared, so its Trigger, do_effect or
        // IsAvailable overrides would silently run the base's) refuses the trigger with tg's own-looking balloon.
        if (ActionGuard(clicker))
            return FALSE;
        if (!Dm.truthy(Dm.bitand(trigger_flags, TRIGGER_FORCE_AVAILABLE)) && !Dm.truthy(await IsAvailable(feedback: TRUE)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ACTION_TRIGGER]) ? NONE : this._SendSignal(COMSIG_ACTION_TRIGGER, Dm.list(this, this))), COMPONENT_ACTION_BLOCK_TRIGGER)))
            return FALSE;
        return TRUE;
    }

    /**
     * Whether our action is currently available to use or not
     * * feedback - If true this is being called to check if we have any messages to show to the owner
     */
    // tg: code/datums/actions/action.dm:177
    public virtual async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        if (!Dm.truthy(owner))
            return FALSE;
        if (Dm.truthy(action_disabled))
            return FALSE;
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_CONSCIOUS)) && IS_UNCONSCIOUS_OR_CRIT(owner))
        {
            if (Dm.truthy(feedback))
            {
                if (owner.stat == DEAD)
                    owner.balloon_alert(owner, I18n.T("dead!"));
                else if (IS_UNCONSCIOUS(owner))
                    owner.balloon_alert(owner, I18n.T("unconscious!"));
                else
                    owner.balloon_alert(owner, I18n.T("in critical!"));
            }
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_HANDS_BLOCKED)) && HAS_TRAIT(owner, TRAIT_HANDS_BLOCKED))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("hands blocked!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_IMMOBILE)) && HAS_TRAIT(owner, TRAIT_IMMOBILIZED))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("can't move!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_INCAPACITATED)) && HAS_TRAIT(owner, TRAIT_INCAPACITATED))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("incapacitated!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_LYING)) && isliving(owner))
        {
            mob_living action_owner = owner as mob_living;
            if (action_owner.body_position == LYING_DOWN)
            {
                if (Dm.truthy(feedback))
                    owner.balloon_alert(owner, I18n.T("must stand up!"));
                return FALSE;
            }
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_PHASED)) && HAS_TRAIT(owner, TRAIT_MAGICALLY_PHASED))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("incorporeal!"));
            return FALSE;
        }
        if (Dm.truthy(Dm.bitand(check_flags, AB_CHECK_OPEN_TURF)) && !isopenturf(owner.loc))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("not enough space!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Builds / updates all buttons we have shared or given out
    // tg: code/datums/actions/action.dm:220
    public virtual async DmTask build_all_button_icons(float? update_flags = null, object force = null)
    {
        update_flags ??= ALL;
        foreach (datum_hud hud in Dm.each_cast<datum_hud>(viewers))
            await build_button_icon(viewers[hud] as atom_movable_screen_movable_action_button, update_flags, force == null ? null : Dm.num(Dm.truthy(force)));
    }

    /**
     * Builds the icon of the button.
     *
     * Concept:
     * - Underlay (Background icon)
     * - Icon (button icon)
     * - Maptext
     * - Overlay (Background border)
     *
     * button - which button we are modifying the icon of
     * force - whether we're forcing a full update
     */
    // tg: code/datums/actions/action.dm:236
    public virtual async DmTask build_button_icon(atom_movable_screen_movable_action_button button = null, float? update_flags = null, float? force = null)
    {
        update_flags ??= ALL;
        force ??= FALSE;
        if (!Dm.truthy(button))
            return;

        button.actiontooltipstyle = buttontooltipstyle;

        if (Dm.truthy(Dm.bitand(Dm.num(update_flags), UPDATE_BUTTON_NAME)))
            update_button_name(button, force);

        if (Dm.truthy(Dm.bitand(Dm.num(update_flags), UPDATE_BUTTON_BACKGROUND)))
            apply_button_background(button, force);

        if (Dm.truthy(Dm.bitand(Dm.num(update_flags), UPDATE_BUTTON_ICON)))
            apply_button_icon(button, force);

        if (Dm.truthy(Dm.bitand(Dm.num(update_flags), UPDATE_BUTTON_OVERLAY)))
            apply_button_overlay(button, force);

        if (Dm.truthy(Dm.bitand(Dm.num(update_flags), UPDATE_BUTTON_STATUS)))
            await update_button_status(button, force);
    }

    /**
     * Updates the name and description of the button to match our action name and discription.
     *
     * current_button - what button are we editing?
     * force - whether an update is forced regardless of existing status
     */
    // tg: code/datums/actions/action.dm:263
    public virtual void update_button_name(atom_movable_screen_movable_action_button button = null, float? force = null, params object[] __extra)
    {
        force ??= FALSE;
        button.name = name;
        if (Dm.truthy(desc))
            button.desc = Dm.str(desc);
    }

    /**
     * Creates the background underlay for the button
     *
     * current_button - what button are we editing?
     * force - whether an update is forced regardless of existing status
     */
    // tg: code/datums/actions/action.dm:274
    public virtual void apply_button_background(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(background_icon) || !Dm.truthy(background_icon_state) || (Dm.eq(current_button.active_underlay_icon_state, background_icon_state) && !Dm.truthy(force)))
            return;

        // What icons we use for our background
        DmList icon_settings = Dm.list(
            // The icon file
            Dm.kv("bg_icon", background_icon),
            // The icon state, if is_action_active() returns FALSE
            Dm.kv("bg_state", background_icon_state),
            // The icon state, if is_action_active() returns TRUE
            Dm.kv("bg_state_active", background_icon_state)
        );

        // If background_icon_state is ACTION_BUTTON_DEFAULT_BACKGROUND instead use our hud's action button scheme
        if (background_icon_state == ACTION_BUTTON_DEFAULT_BACKGROUND && Dm.truthy(owner?.hud_used))
            icon_settings = owner.hud_used.get_action_buttons_icons() as DmList;

        // Determine which icon to use
        string used_icon_key = Dm.truthy(is_action_active(current_button)) ? "bg_state_active" : "bg_state";

        // Make the underlay
        current_button.underlays.Cut();
        Dm.append(current_button.underlays, image(icon: icon_settings["bg_icon"], icon_state: icon_settings[used_icon_key]));
        current_button.active_underlay_icon_state = icon_settings[used_icon_key];
    }

    /**
     * Applies our button icon and icon state to the button
     *
     * current_button - what button are we editing?
     * force - whether an update is forced regardless of existing status
     */
    // tg: code/datums/actions/action.dm:306
    public virtual void apply_button_icon(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        if (!Dm.truthy(button_icon) || !Dm.truthy(button_icon_state) || (Dm.eq(current_button.icon_state, button_icon_state) && !Dm.truthy(force)))
            return;

        current_button.icon = button_icon;
        current_button.icon_state = button_icon_state;
    }

    /**
     * Applies any overlays to our button
     *
     * current_button - what button are we editing?
     * force - whether an update is forced regardless of existing status
     */
    // tg: code/datums/actions/action.dm:319
    public virtual void apply_button_overlay(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;

        if (Dm.truthy(this._listen_lookup?[COMSIG_ACTION_OVERLAY_APPLY]))
            this._SendSignal(COMSIG_ACTION_OVERLAY_APPLY, Dm.list(this, current_button, force));

        if (!Dm.truthy(overlay_icon) || !Dm.truthy(overlay_icon_state) || (Dm.eq(current_button.active_overlay_icon_state, overlay_icon_state) && !Dm.truthy(force)))
            return;

        current_button.cut_overlay(current_button.button_overlay);
        current_button.button_overlay = mutable_appearance(icon: overlay_icon, icon_state: Dm.str(overlay_icon_state)) as mutable_appearance;
        current_button.add_overlay(current_button.button_overlay);
        current_button.active_overlay_icon_state = overlay_icon_state;
    }

    /**
     * Any other miscellaneous "status" updates within the action button is handled here,
     * such as redding out when unavailable or modifying maptext.
     *
     * current_button - what button are we editing?
     * force - whether an update is forced regardless of existing status
     */
    // tg: code/datums/actions/action.dm:338
    public virtual async DmTask update_button_status(atom_movable_screen_movable_action_button current_button = null, float? force = null)
    {
        force ??= FALSE;
        current_button.update_keybind_maptext(full_key);
        if (Dm.truthy(await IsAvailable()))
            current_button.color = rgb(255, 255, 255, 255);
        else
            current_button.color = Dm.truthy(transparent_when_unavailable) ? rgb(128, 0, 0, 128) : rgb(128, 0, 0);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ACTION_STATUS_UPDATE]))
            this._SendSignal(COMSIG_ACTION_STATUS_UPDATE, Dm.list(this, current_button, force));
    }

    /// Gives our action to the passed viewer.
    /// Puts our action in their actions list and shows them the button.
    // tg: code/datums/actions/action.dm:348
    public virtual async DmTask GiveAction(mob viewer = null)
    {
        datum_hud our_hud = viewer.hud_used;
        if (Dm.truthy(viewers[our_hud])) // Already have a copy of us? go away
            return;

        LAZYOR(ref viewer.actions, this); // Move this in
        await ShowTo(viewer);
    }

    /// Adds our action button to the screen of the passed viewer.
    // tg: code/datums/actions/action.dm:357
    public virtual async DmTask ShowTo(mob viewer = null)
    {
        datum_hud our_hud = viewer.hud_used;
        if (!Dm.truthy(our_hud) || Dm.truthy(viewers[our_hud])) // There's no point in this if you have no hud in the first place
            return;

        atom_movable_screen_movable_action_button button = await create_button(viewer) as atom_movable_screen_movable_action_button;
        SetId(button, viewer);

        button.our_hud = our_hud;
        viewers[our_hud] = button;
        if (Dm.truthy(viewer.client))
            Dm.append(viewer.client.screen, button);

        await button.load_position(); // tg passes the viewer, which load_position() does not declare (rules 7.3: dropped)
        await viewer.update_action_buttons();
    }

    /// Removes our action from the passed viewer.
    // tg: code/datums/actions/action.dm:374
    public virtual void HideFrom(mob viewer = null)
    {
        datum_hud our_hud = viewer.hud_used;
        atom_movable_screen_movable_action_button button = viewers[our_hud] as atom_movable_screen_movable_action_button;
        LAZYREMOVE(ref viewer.actions, this);
        if (Dm.truthy(button))
            qdel(button);
    }

    /// Creates an action button movable for the passed mob, and returns it.
    // tg: code/datums/actions/action.dm:382
    public virtual async DmTask<object> create_button(mob viewer = null)
    {
        atom_movable_screen_movable_action_button button = await viewer.hud_used.add_screen_object(typeof(atom_movable_screen_movable_action_button)) as atom_movable_screen_movable_action_button;
        button.linked_action = this;
        button.allow_observer_click = allow_observer_click;
        await build_button_icon(button, ALL, TRUE);
        return button;
    }

    // tg: code/datums/actions/action.dm:389
    public virtual void SetId(atom_movable_screen_movable_action_button our_button = null, mob owner = null)
    {
        //button id generation
        float bitfield = 0;
        foreach (datum_action action in Dm.each<datum_action>(owner.actions))
        {
            if (action == this) // This could be us, which is dumb
                continue;
            atom_movable_screen_movable_action_button button = action.viewers[owner.hud_used] as atom_movable_screen_movable_action_button;
            if (action.name == name && Dm.truthy(button.id_value))
                bitfield = Dm.bitor(bitfield, Dm.num(button.id_value));
        }

        bitfield = Dm.bitnot(bitfield); // Flip our possible ids, so we can check if we've found a unique one
        for (float i = 0, i__end = 23; i <= i__end; i++) // We get 24 possible bitflags in dm
        {
            float bitflag = Dm.shl(1, i); // Shift us over one
            if (Dm.truthy(Dm.bitand(bitfield, bitflag)))
            {
                our_button.id_value = bitflag;
                return;
            }
        }
    }

    /// Updates our buttons if our target's icon was updated
    // tg: code/datums/actions/action.dm:407
    [SignalHandler]
    public virtual void on_target_icon_update(datum source = null, object updates = null, object updated = null)
    {
        float update_flag = NONE;
        float forced = FALSE;
        if (Dm.truthy(Dm.bitand(updates, UPDATE_ICON_STATE)))
        {
            update_flag = Dm.bitor(update_flag, UPDATE_BUTTON_ICON);
            forced = TRUE;
        }
        if (Dm.truthy(Dm.bitand(updates, UPDATE_OVERLAYS)))
        {
            update_flag = Dm.bitor(update_flag, UPDATE_BUTTON_OVERLAY);
            forced = TRUE;
        }
        if (Dm.truthy(Dm.bitand(updates, Dm.bitor(UPDATE_NAME, UPDATE_DESC))))
            update_flag = Dm.bitor(update_flag, UPDATE_BUTTON_NAME);
        // Status is not relevant, and background is not relevant. Neither will change

        // Force the update if an icon state or overlay change was done
        Waiting.NoSleep(build_all_button_icons(update_flag, forced));
    }

    /// A general use signal proc that reacts to an event and updates JUST our button's status
    // tg: code/datums/actions/action.dm:426
    [SignalHandler]
    public virtual void update_status_on_signal(datum source = null, object new_stat = null, object old_stat = null)
    {
        Waiting.NoSleep(build_all_button_icons(UPDATE_BUTTON_STATUS));
    }

    /// Signal proc for COMSIG_MIND_TRANSFERRED - for minds, transfers our action to our new mob on mind transfer
    // tg: code/datums/actions/action.dm:432
    [SignalHandler]
    public virtual void on_target_mind_swapped(datum_mind source = null, mob old_current = null)
    {
        // Grant() calls Remove() from the existing owner so we're covered on that
        Waiting.NoSleep(Grant(source.current));
    }

    /// Checks if our action is actively selected. Used for selecting icons primarily.
    // tg: code/datums/actions/action.dm:439
    public virtual object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        return FALSE;
    }

    // tg: code/datums/actions/action.dm:442
    public virtual async DmTask begin_creating_bind(atom_movable_screen_movable_action_button current_button = null, mob user = null)
    {
        if (!Dm.truthy(current_button) || user != owner)
            return;
        if (full_key != null)
        {
            full_key = null;
            await update_button_status(current_button);
            return;
        }
        // full_key = tgui_input_keycombo(user, "Please bind a key for this action.") (:449): the key-combo input window is
        // tgui (M9); full_key stays null, as when the player closes the window.
        Dm.Deferred("code/datums/actions/action.dm:449", "begin_creating_bind: tgui_input_keycombo, the key-combo input window (tgui, M10+)");
        await update_button_status(current_button);
    }

    // tg: code/datums/actions/action.dm:452
    [SignalHandler]
    public virtual void keydown(mob source = null, object key = null, client client = null, object full_key = null)
    {
        if (full_key == null || !Dm.eq(full_key, this.full_key))
            return;
        if (source is mob)
        {
            if (source.next_click > world.time)
                return;
            else
                source.next_click = world.time + CLICK_CD_ACTIVATE_ABILITY;
        }
        Waiting.Detach(Trigger());
    }

    /// Used for setting the keybind via external sources.
    // tg: code/datums/actions/action.dm:464
    public virtual async DmTask set_key(object new_full_key = null)
    {
        full_key = new_full_key;
        await build_all_button_icons(UPDATE_BUTTON_STATUS);
    }

    // DEVIATION(DV27): the action datum's interaction guard (see Trigger). One host log line per type through Dm.Deferred,
    // tg's "not ported yet" balloon to the clicker, true.
    private bool ActionGuard(mob clicker)
    {
        if (!InertSeam.IsInertType(GetType()))
            return false;
        string path = DmTypes.PathOf(GetType()) ?? GetType().Name;
        Dm.Deferred("inert guard: " + path + " Trigger", "not ported yet: the action type's own tg procs are not translated (D132, DV27)");
        mob shown = clicker ?? owner;
        if (shown != null)
            shown.balloon_alert(shown, I18n.T("not ported yet"));
        return true;
    }
}
