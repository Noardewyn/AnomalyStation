// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/ranged_mob_full_auto.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file (the ranged holoparasite's held fire), but the click catcher's turf under a screen
// press or drag (parse_caught_click_modifiers, the 2D client's click catcher: the first-person client sends the world thing under the
// crosshair, as the guns' full auto, fullauto.cs). The var block is generated.

/// Allows a mob to autofire by holding down the cursor
// tg: code/datums/components/ranged_mob_full_auto.dm:5
public partial class datum_component_ranged_mob_full_auto : datum_component
{
    // tg: code/datums/components/ranged_mob_full_auto.dm:1
    private const float AUTOFIRE_MOUSEUP = 1;
    // tg: code/datums/components/ranged_mob_full_auto.dm:2
    private const float AUTOFIRE_MOUSEDOWN = 0;
    // tg: code/__HELPERS/view.dm:38  #define DEFAULT_SIGHT_DISTANCE 7 (CAN_THEY_SEE reads it; as fullauto.cs)
    private const int DEFAULT_SIGHT_DISTANCE = 7;

    // tg: code/datums/components/ranged_mob_full_auto.dm:21
    [DmParams("autofire_shot_delay")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object autofire_shot_delay = Dm.arg(__extra, 0) ?? 0.5f * SECONDS;
        await base.Initialize(__extra);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.autofire_shot_delay = autofire_shot_delay;

        mob_living living_parent = (mob_living)parent;
        if (living_parent.client == null)
            return null;
        on_gained_client(living_parent);
        return null;
    }

    // tg: code/datums/components/ranged_mob_full_auto.dm:33
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOB_LOGIN, nameof(on_gained_client));
        RegisterSignal(parent, COMSIG_MOB_LOGOUT, nameof(on_lost_client));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/ranged_mob_full_auto.dm:37
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOB_LOGIN, COMSIG_MOB_LOGOUT));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/ranged_mob_full_auto.dm:40
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(Waiting.NoSleep(try_shooting())))
            return PROCESS_KILL;
        return null;
    }

    /// Try and take a shot, returns false if we are unable to do so and should stop trying
    // tg: code/datums/components/ranged_mob_full_auto.dm:45
    public virtual async DmTask<object> try_shooting()
    {
        if (!Dm.truthy(is_firing))
            return FALSE;
        if (!(next_shot_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
            return TRUE; // Don't fire but also keep processing

        mob_living living_parent = (mob_living)parent;

        if (target == null || get_turf(target) != target_loc) // Target moved or got destroyed since we last aimed.
        {
            set_target(target_loc);
            target = target_loc; // So we keep firing on the emptied tile until we move our mouse and find a new target.
        }
        if (Dm.num(get_dist(living_parent, target)) <= 0)
        {
            set_target(get_step(living_parent, living_parent.dir) as atom); // Shoot in the direction faced if the mouse is on the same tile as we are.
            target_loc = target as turf;
        }
        else if (!(Dm.truthy(Dm.@in(living_parent, viewers(DEFAULT_SIGHT_DISTANCE, target))) || Dm.truthy(in_range(target, living_parent)))) // CAN_THEY_SEE (__HELPERS/view.dm:45), inline
        {
            stop_firing();
            return FALSE; // Can't see shit
        }

        living_parent.face_atom(target);
        next_shot_cooldown = world.time + Dm.num(autofire_shot_delay); // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        await living_parent.RangedAttack(target);
        return TRUE;
    }

    /// Setter for reference handling
    // tg: code/datums/components/ranged_mob_full_auto.dm:69
    public virtual void set_target(atom new_target = null)
    {
        if (target != null)
            UnregisterSignal(target, COMSIG_QDELETING);
        target = new_target;
        if (target != null)
            RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_deleted));
    }

    /// Don't hang references
    // tg: code/datums/components/ranged_mob_full_auto.dm:77
    [SignalHandler]
    public virtual void on_target_deleted()
    {
        set_target(null);
    }

    /// When we gain a client, start tracking clicks
    // tg: code/datums/components/ranged_mob_full_auto.dm:82
    [SignalHandler]
    public virtual void on_gained_client(mob_living source = null)
    {
        clicker = source.client;
        RegisterSignal(clicker, COMSIG_CLIENT_MOUSEDOWN, nameof(on_mouse_down));
    }

    /// When we lose our client, stop functioning
    // tg: code/datums/components/ranged_mob_full_auto.dm:88
    [SignalHandler]
    public virtual void on_lost_client(mob_living source = null)
    {
        if (clicker != null)
            UnregisterSignal(clicker, Dm.list(COMSIG_CLIENT_MOUSEDOWN, COMSIG_CLIENT_MOUSEDRAG, COMSIG_CLIENT_MOUSEUP));
        stop_firing();
        clicker = null;
    }

    /// On mouse down start shooting!
    // tg: code/datums/components/ranged_mob_full_auto.dm:96
    [SignalHandler]
    public virtual void on_mouse_down(client source = null, atom target = null, turf location = null, object control = null, object @params = null)
    {
        if (awaiting_status != AUTOFIRE_MOUSEDOWN)
            return; // Avoid a double mousedown with no mouseup
        DmList modifiers = params2list(@params);

        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
            return;
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
            return;
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
            return;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return;
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
            return;
        mob_living living_parent = (mob_living)parent;
        if (!isturf(living_parent.loc) || Dm.truthy(Waiting.NoSleep(living_parent.Adjacent(target))))
            return;

        if (location == null || istype(target, typeof(atom_movable_screen))) // Clicking on a screen object.
        {
            if (target.plane != CLICKCATCHER_PLANE) // The clickcatcher is a special case. We want the click to trigger then, under it.
                return; // If we click and drag on our worn backpack, for example, we want it to open instead.
            // set_target(parse_caught_click_modifiers(modifiers, get_turf(source.eye), source)) (:118): the 2D client's click catcher; the
            // first-person client never presses on one (it sends the thing under the crosshair), as fullauto.cs
            Dm.NotTranslated("code/datums/components/ranged_mob_full_auto.dm:118", "on_mouse_down: parse_caught_click_modifiers, the turf under a click catcher press (the 2D client's click catcher)");
            return;
        }

        awaiting_status = AUTOFIRE_MOUSEUP;
        source.click_intercept_time = world.time; // From this point onwards Click() will no longer be triggered.
        if (Dm.truthy(is_firing))
            stop_firing();

        set_target(target);
        target_loc = get_turf(target) as turf;
        Waiting.Detach(start_firing()); // INVOKE_ASYNC
    }

    /// Start tracking mouse movement and processing our shots
    // tg: code/datums/components/ranged_mob_full_auto.dm:134
    public virtual async DmTask start_firing()
    {
        if (Dm.truthy(is_firing))
            return;

        is_firing = TRUE;
        if (!Dm.truthy(await try_shooting())) // First one is immediate
        {
            stop_firing();
            return;
        }

        clicker.mouse_override_icon = Dm.resource("icons/effects/mouse_pointers/weapon_pointer.dmi");
        clicker.mouse_pointer_icon = clicker.mouse_override_icon;

        START_PROCESSING(SSprojectiles, this);
        RegisterSignal(clicker, COMSIG_CLIENT_MOUSEUP, nameof(on_mouse_up));
        RegisterSignal(clicker, COMSIG_CLIENT_MOUSEDRAG, nameof(on_mouse_drag));
    }

    /// When the mouse moved let's try and shift our aim
    // tg: code/datums/components/ranged_mob_full_auto.dm:151
    [SignalHandler]
    public virtual void on_mouse_drag(client source = null, atom src_object = null, atom over_object = null, turf src_location = null, turf over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (over_location != null)
        {
            set_target(over_object);
            target_loc = get_turf(over_object) as turf;
            return;
        }

        //This happens when the mouse is over an inventory or screen object, or on entering deep darkness, for example.
        // parse_caught_click_modifiers (:160-175): the 2D client's click catcher; the first-person client sends no drag over one (fullauto.cs)
        Dm.NotTranslated("code/datums/components/ranged_mob_full_auto.dm:160", "on_mouse_drag: parse_caught_click_modifiers, the turf under a click catcher drag (the 2D client's click catcher)");
    }

    /// When the mouse is released we should stop
    // tg: code/datums/components/ranged_mob_full_auto.dm:178
    [SignalHandler]
    public virtual object on_mouse_up()
    {
        if (awaiting_status != AUTOFIRE_MOUSEUP)
            return null;
        stop_firing();
        return COMPONENT_CLIENT_MOUSEUP_INTERCEPT;
    }

    /// Stop watching our mouse and processing shots
    // tg: code/datums/components/ranged_mob_full_auto.dm:186
    public virtual void stop_firing()
    {
        if (!Dm.truthy(is_firing))
            return;

        is_firing = FALSE;
        set_target(null);
        target_loc = null;
        STOP_PROCESSING(SSprojectiles, this);
        awaiting_status = AUTOFIRE_MOUSEDOWN;

        if (clicker == null)
            return;
        UnregisterSignal(clicker, Dm.list(COMSIG_CLIENT_MOUSEDRAG, COMSIG_CLIENT_MOUSEUP));
        clicker.mouse_override_icon = null;
        clicker.mouse_pointer_icon = null;
    }
}
