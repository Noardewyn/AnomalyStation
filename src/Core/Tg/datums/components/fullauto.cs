// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/fullauto.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the automatic fire component, whole, with the gun's four procs (in modules/projectiles/gun.cs). The held button reaches it as
// BYOND's MouseDown, MouseDrag and MouseUp (protocol 34, ClientSeam.Mouse.cs): the aim of the press and of each drag rides the params
// (DV94), so every burst shot is aimed by the camera. The clickcatcher branches (a press on the click catcher or the HUD) are the 2D client's:
// the first-person client sends the thing under the crosshair, never a catcher. The var block is generated (D111).

// tg: code/datums/components/fullauto.dm:1  #define AUTOFIRE_MOUSEUP 0 (file-local)
// tg: code/datums/components/fullauto.dm:2  #define AUTOFIRE_MOUSEDOWN 1 (file-local)

// tg: code/datums/components/fullauto.dm:4
public partial class datum_component_automatic_fire : datum_component
{
    private const int AUTOFIRE_MOUSEUP = 0;
    private const int AUTOFIRE_MOUSEDOWN = 1;
    // tg: code/__HELPERS/view.dm:38  #define DEFAULT_SIGHT_DISTANCE 7
    private const int DEFAULT_SIGHT_DISTANCE = 7;

    // tg: code/datums/components/fullauto.dm:36
    [DmParams("autofire_shot_delay", "windup_autofire", "windup_autofire_reduction_multiplier", "windup_autofire_cap", "windup_spindown", "allow_akimbo", "firing_sound_loop")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object autofire_shot_delay = Dm.arg(__extra, 0);
        object windup_autofire = Dm.arg(__extra, 1);
        object windup_autofire_reduction_multiplier = Dm.arg(__extra, 2);
        object windup_autofire_cap = Dm.arg(__extra, 3);
        object windup_spindown = Dm.arg(__extra, 4);
        object allow_akimbo = Dm.arg(__extra, 5) ?? TRUE;
        object firing_sound_loop = Dm.arg(__extra, 6);
        object dot = await base.Initialize(Dm.forward(__extra, autofire_shot_delay, windup_autofire, windup_autofire_reduction_multiplier, windup_autofire_cap, windup_spindown, allow_akimbo, firing_sound_loop));
        if (!isgun(parent))
            return COMPONENT_INCOMPATIBLE;
        obj_item gun = parent as obj_item;
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(wake_up));
        if (Dm.truthy(autofire_shot_delay))
            this.autofire_shot_delay = Dm.num(autofire_shot_delay);
        this.allow_akimbo = allow_akimbo;
        if (Dm.truthy(windup_autofire))
        {
            this.windup_autofire = Dm.num(windup_autofire);
            this.windup_autofire_reduction_multiplier = Dm.num(windup_autofire_reduction_multiplier);
            this.windup_autofire_cap = Dm.num(windup_autofire_cap);
            this.windup_spindown = Dm.num(windup_spindown);
        }
        if (autofire_stat == AUTOFIRE_STAT_IDLE && ismob(gun.loc))
        {
            mob user = gun.loc as mob;
            wake_up(this, user);
        }

        if (Dm.truthy(firing_sound_loop))
            autofire_sound_loop = Dm.@new((Type)firing_sound_loop, parent) as datum_looping_sound;
        return dot;
    }

    // tg: code/datums/components/fullauto.dm:56
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref autofire_sound_loop);
        autofire_off();
        return base.Destroy(force);
    }

    // tg: code/datums/components/fullauto.dm:61
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (autofire_stat != AUTOFIRE_STAT_FIRING)
        {
            STOP_PROCESSING(SSprojectiles, this);
            return null;
        }
        process_shot();
        return null;
    }

    // tg: code/datums/components/fullauto.dm:67
    [SignalHandler]
    public virtual void wake_up(datum source = null, mob user = null, object slot = null)
    {
        if (autofire_stat == AUTOFIRE_STAT_ALERT)
            return; //We've updated the firemode. No need for more.
        if (autofire_stat == AUTOFIRE_STAT_FIRING)
        {
            stop_autofiring(); //Let's stop shooting to avoid issues.
            return;
        }
        if (Dm.truthy(user.is_holding(parent as obj_item)))
            autofire_on(user.client);
    }

    // There is a gun and there is a user wielding it. The component now waits for the mouse click.
    // tg: code/datums/components/fullauto.dm:78
    [SignalHandler]
    public virtual void autofire_on(client usercli = null)
    {
        if (autofire_stat != AUTOFIRE_STAT_IDLE)
            return;
        autofire_stat = AUTOFIRE_STAT_ALERT;
        if (!QDELETED(usercli))
        {
            clicker = usercli;
            shooter = clicker.mob as mob_living;
            RegisterSignal(clicker, COMSIG_CLIENT_MOUSEDOWN, nameof(on_mouse_down));
        }
        if (!QDELETED(shooter))
        {
            RegisterSignal(shooter, COMSIG_MOB_LOGOUT, nameof(autofire_off));
            UnregisterSignal(shooter, COMSIG_MOB_LOGIN);
        }
        RegisterSignals(parent, Dm.list(COMSIG_QDELETING, COMSIG_ITEM_DROPPED), nameof(autofire_off));
        parent.RegisterSignal(this, COMSIG_AUTOFIRE_ONMOUSEDOWN, nameof(obj_item_gun.autofire_bypass_check));
        parent.RegisterSignal(parent, COMSIG_AUTOFIRE_SHOT, nameof(obj_item_gun.do_autofire));
    }

    // tg: code/datums/components/fullauto.dm:96
    [SignalHandler]
    public virtual void autofire_off(datum source = null)
    {
        if (autofire_stat == AUTOFIRE_STAT_IDLE)
            return;
        if (autofire_stat == AUTOFIRE_STAT_FIRING)
            stop_autofiring();

        autofire_stat = AUTOFIRE_STAT_IDLE;

        if (!QDELETED(clicker))
            UnregisterSignal(clicker, Dm.list(COMSIG_CLIENT_MOUSEDOWN, COMSIG_CLIENT_MOUSEUP, COMSIG_CLIENT_MOUSEDRAG));
        mouse_status = AUTOFIRE_MOUSEUP; //In regards to the component there's no click anymore to care about.
        clicker = null;
        if (!QDELETED(shooter))
        {
            RegisterSignal(shooter, COMSIG_MOB_LOGIN, nameof(on_client_login));
            UnregisterSignal(shooter, COMSIG_MOB_LOGOUT);
        }
        UnregisterSignal(parent, Dm.list(COMSIG_QDELETING, COMSIG_ITEM_DROPPED));
        shooter = null;
        parent.UnregisterSignal(parent, COMSIG_AUTOFIRE_SHOT);
        parent.UnregisterSignal(this, COMSIG_AUTOFIRE_ONMOUSEDOWN);
    }

    // tg: code/datums/components/fullauto.dm:116
    [SignalHandler]
    public virtual void on_client_login(mob source = null)
    {
        if (source.client == null)
            return;
        if (Dm.truthy(source.is_holding(parent as obj_item)))
            autofire_on(source.client);
    }

    // tg: code/datums/components/fullauto.dm:122
    [SignalHandler]
    public virtual void on_mouse_down(client source = null, atom _target = null, object location = null, object control = null, object @params = null)
    {
        DmList modifiers = params2list(@params); //If they're shift+clicking, for example, let's not have them accidentally shoot.

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
        if (Dm.truthy(source.mob.throw_mode))
            return;
        if (!isturf(source.mob.loc)) //No firing inside lockers and stuff.
            return;
        if (Dm.num(get_dist(source.mob, _target)) < 2) //Adjacent clicking.
            return;

        if (location == null || istype(_target, typeof(atom_movable_screen))) //Clicking on a screen object.
        {
            if (_target.plane != CLICKCATCHER_PLANE) //The clickcatcher is a special case. We want the click to trigger then, under it.
                return; //If we click and drag on our worn backpack, for example, we want it to open instead.
            // The first-person client never presses on a click catcher (it sends the thing under the crosshair), so tg's
            // parse_caught_click_modifiers (the screen_loc of a catcher click to a turf) has no caller here.
            Dm.NotTranslated("code/__HELPERS/turfs.dm:264", "on_mouse_down: parse_caught_click_modifiers, the turf under a click catcher press (the 2D client's click catcher; the first-person client sends the world thing under the crosshair)");
            return;
        }

        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_AUTOFIRE_ONMOUSEDOWN]) ? (object)NONE : _SendSignal(COMSIG_AUTOFIRE_ONMOUSEDOWN, Dm.list(this, source, _target, location, control, @params)), COMPONENT_AUTOFIRE_ONMOUSEDOWN_BYPASS)))
            return;

        source.click_intercept_time = world.time; //From this point onwards Click() will no longer be triggered.

        if (autofire_stat == (AUTOFIRE_STAT_IDLE))
            CRASH(Dm.text("on_mouse_down() called with [] autofire_stat", autofire_stat));
        if (autofire_stat == AUTOFIRE_STAT_FIRING)
            stop_autofiring(); //This can happen if we click and hold and then alt+tab, printscreen or other such action. MouseUp won't be called then and it will keep autofiring.

        target = _target;
        target_loc = get_turf(target);
        mouse_parameters = @params;
        Waiting.Detach(start_autofiring());
    }

    //Dakka-dakka
    // tg: code/datums/components/fullauto.dm:162
    public virtual async DmTask start_autofiring()
    {
        if (autofire_stat == AUTOFIRE_STAT_FIRING)
            return;
        autofire_stat = AUTOFIRE_STAT_FIRING;

        clicker.mouse_override_icon = Dm.resource("icons/effects/mouse_pointers/weapon_pointer.dmi");
        clicker.mouse_pointer_icon = clicker.mouse_override_icon;

        if (mouse_status == AUTOFIRE_MOUSEUP) //See mouse_status definition for the reason for this.
        {
            RegisterSignal(clicker, COMSIG_CLIENT_MOUSEUP, nameof(on_mouse_up));
            mouse_status = AUTOFIRE_MOUSEDOWN;
        }

        RegisterSignal(shooter, COMSIG_MOB_SWAP_HANDS, nameof(stop_autofiring));

        if (isgun(parent))
        {
            obj_item_gun shoota = parent as obj_item_gun;
            if (!Dm.truthy(await shoota.on_autofire_start(shooter))) //This is needed because the minigun has a do_after before firing and signals are async.
            {
                stop_autofiring();
                return;
            }
        }
        if (autofire_stat != AUTOFIRE_STAT_FIRING)
            return; //Things may have changed while on_autofire_start() was being processed, due to do_after's sleep.

        if (!process_shot()) //First shot is processed instantly.
            return; //If it fails, such as when the gun is empty, then there's no need to schedule a second shot.

        START_PROCESSING(SSprojectiles, this);
        RegisterSignal(clicker, COMSIG_CLIENT_MOUSEDRAG, nameof(on_mouse_drag));

        if (autofire_sound_loop != null)
            autofire_sound_loop.start(shooter);
    }

    // tg: code/datums/components/fullauto.dm:190
    [SignalHandler]
    public virtual object on_mouse_up(datum source = null, atom @object = null, object location = null, object control = null, object @params = null)
    {
        UnregisterSignal(clicker, COMSIG_CLIENT_MOUSEUP);
        mouse_status = AUTOFIRE_MOUSEUP;
        if (autofire_stat == AUTOFIRE_STAT_FIRING)
            stop_autofiring();
        return COMPONENT_CLIENT_MOUSEUP_INTERCEPT;
    }

    // tg: code/datums/components/fullauto.dm:198
    [SignalHandler]
    public virtual void stop_autofiring(mob_living source = null, obj_item swapped_to = null, obj_item swapped_from = null)
    {
        if (autofire_stat != AUTOFIRE_STAT_FIRING)
            return;
        STOP_PROCESSING(SSprojectiles, this);
        autofire_stat = AUTOFIRE_STAT_ALERT;
        if (clicker != null)
        {
            clicker.mouse_override_icon = null;
            clicker.mouse_pointer_icon = clicker.mouse_override_icon;
            UnregisterSignal(clicker, COMSIG_CLIENT_MOUSEDRAG);
        }
        if (!QDELETED(shooter))
            UnregisterSignal(shooter, COMSIG_MOB_SWAP_HANDS);
        target = null;
        target_loc = null;
        mouse_parameters = null;

        if (autofire_sound_loop != null)
            autofire_sound_loop.stop();
    }

    // tg: code/datums/components/fullauto.dm:218
    [SignalHandler]
    public virtual void on_mouse_drag(client source = null, atom src_object = null, atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
        if (over_location == null) //This happens when the mouse is over an inventory or screen object, or on entering deep darkness, for example.
        {
            // The first-person client sends no drag over a click catcher: an inventory or screen object under the mouse is not
            // the world (parse_caught_click_modifiers is the 2D client's, see on_mouse_down).
            Dm.NotTranslated("code/__HELPERS/turfs.dm:264", "on_mouse_drag: parse_caught_click_modifiers, the turf under a click catcher drag (the 2D client's click catcher; the first-person client sends the world thing under the crosshair)");
            return;
        }
        target = over_object;
        target_loc = get_turf(over_object);
        mouse_parameters = @params as string;
    }

    // tg: code/datums/components/fullauto.dm:236
    public virtual bool process_shot()
    {
        if (autofire_stat != AUTOFIRE_STAT_FIRING)
            return false;
        if (!(next_shot_cd <= world.time)) // !COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return true;
        if (QDELETED(target) || get_turf(target) != target_loc) //Target moved or got destroyed since we last aimed.
            target = target_loc; //So we keep firing on the emptied tile until we move our mouse and find a new target.
        if (Dm.num(get_dist(shooter, target)) <= 0)
        {
            target = get_step(shooter, Dm.num(shooter.dir)) as atom; //Shoot in the direction faced if the mouse is on the same tile as we are.
            target_loc = target as turf;
        }
        // DEVIATION(DV393): a target on another level is never in BYOND's view, so held full auto did not fire up or down through open
        // space; the sight check is tg's on the shooter's own level only (the server took the target as in the 3D view, InViewOf).
        else if (Dm.eq(get_turf(shooter)?.z, get_turf(target)?.z) && !(Dm.truthy(Dm.@in(shooter, viewers(DEFAULT_SIGHT_DISTANCE, target))) || Dm.truthy(in_range(target, shooter)))) // CAN_THEY_SEE (__HELPERS/view.dm:45)
        {
            stop_autofiring(); //Elvis has left the building.
            return false;
        }
        shooter.face_atom(target);
        float next_delay = autofire_shot_delay;
        if (windup_autofire != 0)
        {
            next_delay = clamp(next_delay - current_windup_reduction, round(autofire_shot_delay * windup_autofire_cap), autofire_shot_delay);
            current_windup_reduction = (current_windup_reduction + round(autofire_shot_delay * windup_autofire_reduction_multiplier));
            timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(windup_reset), FALSE), windup_spindown, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), file: "code/datums/components/fullauto.dm", line: 252);
        }
        if (HAS_TRAIT(shooter, TRAIT_DOUBLE_TAP))
            next_delay = round(next_delay * 0.5f, SSprojectiles.wait);
        next_shot_cd = world.time + next_delay; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        if (Dm.truthy(Dm.bitand(!Dm.truthy(parent._listen_lookup?[COMSIG_AUTOFIRE_SHOT]) ? (object)NONE : parent._SendSignal(COMSIG_AUTOFIRE_SHOT, Dm.list(parent, target, shooter, allow_akimbo, mouse_parameters)), COMPONENT_AUTOFIRE_SHOT_SUCCESS)))
            return true;
        stop_autofiring();
        return false;
    }

    /// Reset for our windup, resetting everything back to initial values after a variable set amount of time (determined by var/windup_spindown).
    // tg: code/datums/components/fullauto.dm:267
    public virtual void windup_reset(object deltimer = null)
    {
        current_windup_reduction = Dm.initial(this, (datum_component_automatic_fire p) => p.current_windup_reduction);
        if (Dm.truthy(deltimer) && Dm.truthy(timerid))
            Anomaly.Core.Tg.GlobalProcs.deltimer(timerid);
    }
}
