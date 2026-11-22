// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/scope.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D, DV98: the scope component. Zooming puts the "scope" fullscreen on the user's screen, as tg does, and the first-person client zooms
// its field of view by the component's range_modifier while that screen object is there (the range_modifier rides in the hundredths of the
// tracker's layer above FULLSCREEN_LAYER, read by godot/scripts/Shared/Play/ScopeZoom.cs: the screen entry's appearance carries the layer, so no
// protocol change). tg's panning of the 2D view by the cursor (process, calculate_params, the pixel offsets of stop_zooming) and the
// shot's redirect to the cursor's turf (on_gun_fire, which needs given_turf) have no first-person counterpart: the crosshair is the aim. The
// var block is generated (D111).

///Used to allow reaching the maximum offset range without exiting the boundaries of the game screen.
// tg: code/datums/components/scope.dm:2  #define MOUSE_POINTER_OFFSET_MULT 1.1 (file-local, used by calculate_params only)

///A component that allows players to use the item to zoom out. Mainly intended for firearms, but now works with other items too.
// tg: code/datums/components/scope.dm:5
public partial class datum_component_scope : datum_component
{
    // tg: code/datums/components/scope.dm:17
    [DmParams("range_modifier", "zoom_method", "item_action_type")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object range_modifier = Dm.arg(__extra, 0) ?? 1;
        object zoom_method = Dm.arg(__extra, 1) ?? ZOOM_METHOD_RIGHT_CLICK;
        Type item_action_type = Dm.arg<Type>(__extra, 2);
        object dot = await base.Initialize(Dm.forward(__extra, range_modifier, zoom_method, item_action_type));
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        this.range_modifier = Dm.num(range_modifier);
        this.zoom_method = Dm.num(zoom_method);
        this.item_action_type = item_action_type;
        return dot;
    }

    // tg: code/datums/components/scope.dm:24
    public override object Destroy(float? force = null)
    {
        if (tracker != null)
            stop_zooming(tracker.owner);
        return base.Destroy(force);
    }

    // tg: code/datums/components/scope.dm:29
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_move));
        switch ((int)zoom_method)
        {
            case ZOOM_METHOD_RIGHT_CLICK:
                RegisterSignal(parent, COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM_SECONDARY, nameof(do_secondary_zoom));
                break;
            case ZOOM_METHOD_WIELD:
                RegisterSignal(parent, SIGNAL_ADDTRAIT(TRAIT_WIELDED), nameof(on_wielded));
                RegisterSignal(parent, SIGNAL_REMOVETRAIT(TRAIT_WIELDED), nameof(on_unwielded));
                break;
        }
        if (item_action_type != null)
        {
            obj_item parent_item = parent as obj_item;
            datum_action_item_action scope = await parent_item.add_item_action(item_action_type) as datum_action_item_action;
            RegisterSignal(scope, COMSIG_ACTION_TRIGGER, nameof(on_action_trigger));
        }
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        if (isgun(parent))
            RegisterSignal(parent, COMSIG_GUN_TRY_FIRE, nameof(on_gun_fire));
    }

    // tg: code/datums/components/scope.dm:45
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        if (item_action_type != null)
        {
            obj_item parent_item = parent as obj_item;
            datum_action scope = Dm.locate_type(item_action_type, parent_item.actions) as datum_action;
            parent_item.remove_item_action(scope);
        }
        UnregisterSignal(parent, Dm.list(
            COMSIG_MOVABLE_MOVED,
            COMSIG_RANGED_ITEM_INTERACTING_WITH_ATOM_SECONDARY,
            SIGNAL_ADDTRAIT(TRAIT_WIELDED),
            SIGNAL_REMOVETRAIT(TRAIT_WIELDED),
            COMSIG_GUN_TRY_FIRE,
            COMSIG_ATOM_EXAMINE));
    }

    // tg: code/datums/components/scope.dm:70
    [SignalHandler]
    public virtual void on_move(atom_movable source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (tracker == null)
            return;
        stop_zooming(tracker.owner);
    }

    // tg: code/datums/components/scope.dm:77
    [SignalHandler]
    public virtual object do_secondary_zoom(datum source = null, mob user = null, atom target = null, DmList modifiers = null)
    {
        if (tracker != null)
            stop_zooming(user);
        else
            Waiting.NoSleep(zoom(user));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/datums/components/scope.dm:86
    [SignalHandler]
    public virtual void on_action_trigger(datum_action source = null)
    {
        obj_item item = source.target as obj_item;
        mob_living user = item.loc as mob_living;
        if (tracker != null)
            stop_zooming(user);
        else
            Waiting.NoSleep(zoom(user));
    }

    // tg: code/datums/components/scope.dm:95
    [SignalHandler]
    public virtual void on_wielded(obj_item source = null, object trait = null)
    {
        mob_living user = source.loc as mob_living;
        Waiting.NoSleep(zoom(user));
    }

    // tg: code/datums/components/scope.dm:100
    [SignalHandler]
    public virtual void on_unwielded(obj_item source = null, object trait = null)
    {
        mob_living user = source.loc as mob_living;
        stop_zooming(user);
    }

    // tg: code/datums/components/scope.dm:105
    [SignalHandler]
    public virtual object on_gun_fire(obj_item_gun source = null, mob_living user = null, atom target = null, object flag = null, object @params = null)
    {
        // tracker.given_turf is the cursor's turf: null in the first-person view (DV98), where the shot goes along the crosshair
        if (tracker?.given_turf == null || target == get_target(tracker.given_turf))
            return NONE;
        Waiting.Detach(source.fire_gun(get_target(tracker.given_turf), user));
        return COMPONENT_CANCEL_GUN_FIRE;
    }

    // tg: code/datums/components/scope.dm:113
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        string scope = isgun(parent) ? "scope in" : "zoom out";
        switch ((int)zoom_method)
        {
            case ZOOM_METHOD_RIGHT_CLICK:
                Dm.append(ref examine_list, span_notice(Dm.text("You can [] with <b>right-click</b>.", scope)));
                break;
            case ZOOM_METHOD_WIELD:
                Dm.append(ref examine_list, span_notice(Dm.text("You can [] by wielding it with both hands.", scope)));
                break;
        }
    }

    /**
     * We find and return the best target to hit on a given turf.
     *
     * Arguments:
     * * target_turf: The turf we are looking for targets on.
    */
    // tg: code/datums/components/scope.dm:129
    public virtual atom get_target(turf target_turf = null)
    {
        DmList object_targets = Dm.list();
        DmList non_dense_targets = Dm.list();
        foreach (atom_movable possible_target in Dm.each<atom_movable>(target_turf.contents))
        {
            if (possible_target.layer <= PROJECTILE_HIT_THRESHHOLD_LAYER)
                continue;
            if (possible_target.invisibility > tracker.owner.see_invisible)
                continue;
            if (!Dm.truthy(possible_target.mouse_opacity))
                continue;
            if (iseffect(possible_target))
                continue;
            if (ismob(possible_target))
            {
                if (possible_target == tracker.owner)
                    continue;
                return possible_target;
            }
            if (!Dm.truthy(possible_target.density))
            {
                Dm.append(ref non_dense_targets, possible_target);
                continue;
            }
            Dm.append(ref object_targets, possible_target);
        }
        foreach (obj important_object in Dm.each<obj>(object_targets))
            return important_object;
        foreach (obj unimportant_object in Dm.each<obj>(non_dense_targets))
            return unimportant_object;
        return target_turf;
    }

    /**
     * We start zooming by adding our tracker overlay and starting our processing.
     *
     * Arguments:
     * * user: The mob we are starting zooming on.
    */
    // tg: code/datums/components/scope.dm:161
    public virtual async DmTask<object> zoom(mob user = null)
    {
        if (user.client == null)
            return null;
        if (HAS_TRAIT(user, TRAIT_USER_SCOPED))
        {
            user.balloon_alert(user, "already zoomed!");
            return null;
        }
        user.playsound_local(parent as atom, Dm.resource("sound/items/weapons/scope.ogg"), 75, TRUE);
        tracker = user.overlay_fullscreen("scope", typeof(atom_movable_screen_fullscreen_cursor_catcher_scope), isgun(parent) ? 1 : 0) as atom_movable_screen_fullscreen_cursor_catcher_scope;
        tracker.assign_to_mob(user, range_modifier);
        // DV98: the first-person client reads the zoom from the scope screen object's layer, FULLSCREEN_LAYER + range_modifier / 100
        tracker.layer = FULLSCREEN_LAYER + range_modifier / 100f;
        tracker_owner_ckey = user.ckey;
        if (Dm.truthy(user.is_holding(parent as obj_item)))
        {
            RegisterSignals(user, Dm.list(COMSIG_MOB_SWAP_HANDS, COMSIG_QDELETING), nameof(stop_zooming));
            RegisterSignal(user, COMSIG_ATOM_ENTERING, nameof(on_enter_new_loc));
        }
        else // The item is likely worn (eg. mothic cap)
        {
            RegisterSignal(user, COMSIG_QDELETING, nameof(stop_zooming));
            RegisterSignal(user, COMSIG_ATOM_ENTERING, nameof(on_enter_new_loc));
            RegisterSignals(user, Dm.list(COMSIG_LIVING_STATUS_KNOCKDOWN, COMSIG_LIVING_STATUS_PARALYZE, COMSIG_LIVING_STATUS_STUN), nameof(on_incapacitated));
        }
        // START_PROCESSING(SSprojectiles, src): the 2D view's panning, DV98
        ADD_TRAIT(user, TRAIT_USER_SCOPED, REF(this));
        return TRUE;
    }

    ///Stop scoping if the `newloc` we move to is not a turf
    // tg: code/datums/components/scope.dm:188
    [SignalHandler]
    public virtual void on_enter_new_loc(datum source = null, atom newloc = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!isturf(newloc))
            stop_zooming(tracker.owner);
    }

    // tg: code/datums/components/scope.dm:194
    [SignalHandler]
    public virtual void on_incapacitated(mob_living source = null, object amount = null, object ignore_canstun = null)
    {
        if (Dm.num(amount) > 0)
            stop_zooming(source);
    }

    /**
     * We stop zooming, canceling processing, resetting stuff back to normal and deleting our tracker.
     *
     * Arguments:
     * * user: The mob we are canceling zooming on.
    */
    // tg: code/datums/components/scope.dm:206
    [SignalHandler]
    public virtual void stop_zooming(mob user = null, params object[] __extra)
    {
        if (!HAS_TRAIT(user, TRAIT_USER_SCOPED))
            return;

        // STOP_PROCESSING(SSprojectiles, src): not started (DV98)
        UnregisterSignal(user, Dm.list(
            COMSIG_LIVING_STATUS_KNOCKDOWN,
            COMSIG_LIVING_STATUS_PARALYZE,
            COMSIG_LIVING_STATUS_STUN,
            COMSIG_MOB_SWAP_HANDS,
            COMSIG_QDELETING,
            COMSIG_ATOM_ENTERING));
        REMOVE_TRAIT(user, TRAIT_USER_SCOPED, REF(this));

        user.playsound_local(parent as atom, Dm.resource("sound/items/weapons/scope.ogg"), 75, TRUE, frequency: -1);
        user.clear_fullscreen("scope");

        // tg: :217-231  if the client has ended up in another mob, find that mob so we can fix their cursor; the pan back of the client's
        // pixel offsets (animate(user.client, 0.2 SECONDS, pixel_x = 0, pixel_y = 0)): the 2D view was never panned (DV98)
        tracker = null;
        tracker_owner_ckey = null;
    }
}
