// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/orbiter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 (D169): the whole file, the orbiter component (the energy ball's mini-balls orbit with it) and /atom/movable's orbit and
// stop_orbit, /atom's transfer_observers_to. The ghosts' ManualFollow and orbit menu call orbit since M10x slice C (D281). The var
// block is generated.

// tg: code/datums/components/orbiter.dm:1
[DmPath("/datum/component/orbiter")]
public partial class datum_component_orbiter : datum_component
{
    //radius: range to orbit at, radius of the circle formed by orbiting (in pixels)
    //clockwise: whether you orbit clockwise or anti clockwise
    //rotation_speed: how fast to rotate (how many ds should it take for a rotation to complete)
    //rotation_segments: the resolution of the orbit circle, less = a more block circle, this can be used to produce hexagons (6 segments) triangles (3 segments), and so on, 36 is the best default.
    //pre_rotation: Chooses to rotate src 90 degress towards the orbit dir (clockwise/anticlockwise), useful for things to go "head first" like ghosts
    // tg: code/datums/components/orbiter.dm:16
    [DmParams("orbiter", "radius", "clockwise", "rotation_speed", "rotation_segments", "pre_rotation")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object orbiter = Dm.arg(__extra, 0);
        object radius = Dm.arg(__extra, 1);
        object clockwise = Dm.arg(__extra, 2);
        object rotation_speed = Dm.arg(__extra, 3);
        object rotation_segments = Dm.arg(__extra, 4);
        object pre_rotation = Dm.arg(__extra, 5);
        if (!(orbiter is atom_movable) || !isatom(parent) || isarea(parent))
            return COMPONENT_INCOMPATIBLE;

        orbiter_list = Dm.list();
        orbiter_params = Dm.list();

        await begin_orbit((atom_movable)orbiter, radius, clockwise, rotation_speed, rotation_segments, pre_rotation);
        return null;
    }

    // tg: code/datums/components/orbiter.dm:25
    public override async DmTask RegisterWithParent()
    {
        atom target = (atom)parent;

        target.orbiters = this;
        if (ismovable(target))
            tracker = Dm.@new<datum_movement_detector>(target, Dm.@new<datum_callback>(this, nameof(move_react)));

        RegisterSignal(parent, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, nameof(orbiter_glide_size_update));
    }

    // tg: code/datums/components/orbiter.dm:34
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOVABLE_UPDATE_GLIDE_SIZE);
        atom target = (atom)parent;
        target.orbiters = null;
        QDEL_NULL(ref tracker);
    }

    // tg: code/datums/components/orbiter.dm:40
    public override object Destroy(float? force = null)
    {
        atom master = (atom)parent;
        if (master.orbiters == this)
            master.orbiters = null;
        foreach (object i in Dm.each(orbiter_list))
            Waiting.NoSleep(end_orbit(i as atom_movable));
        orbiter_list = null;
        orbiter_params = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/orbiter.dm:50
    [DmParams("newcomp", "original", "orbiter", "radius", "clockwise", "rotation_speed", "rotation_segments", "pre_rotation")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_orbiter newcomp = C as datum_component_orbiter;
        if (!Dm.truthy(newcomp))
        {
            DmList args = Dm.args(C, i_am_original, __extra);
            await Dm.Call(this, nameof(begin_orbit), Dm.arglist(args.Copy(3)));
            return;
        }
        // The following only happens on component transfers
        foreach (object o in Dm.each(newcomp.orbiter_list))
        {
            atom_movable incoming_orbiter = (atom_movable)o;
            incoming_orbiter.orbiting = this;
            // It is important to transfer the signals so we don't get locked to the new orbiter component for all time
            newcomp.UnregisterSignal(incoming_orbiter, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, COMSIG_ATOM_AFTER_SHUTTLE_MOVE));
            RegisterSignal(incoming_orbiter, COMSIG_MOVABLE_MOVED, nameof(orbiter_move_react));
            RegisterSignal(incoming_orbiter, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, nameof(orbiter_before_shuttle_move));
        }

        Dm.append(ref orbiter_list, newcomp.orbiter_list);
        Dm.append(ref orbiter_params, newcomp.orbiter_params);
        newcomp.orbiter_list = null;
        newcomp.orbiter_params = null;
    }

    // tg: code/datums/components/orbiter.dm:68
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        if (!isatom(new_parent) || isarea(new_parent) || !Dm.truthy(get_turf(new_parent)))
            return COMPONENT_INCOMPATIBLE;
        move_react(new_parent as atom_movable);
        return null;
    }

    // tg: code/datums/components/orbiter.dm:73
    public virtual async DmTask begin_orbit(atom_movable orbiter = null, object radius = null, object clockwise = null, object rotation_speed = null, object rotation_segments = null, object pre_rotation = null, params object[] __extra)
    {
        if (Dm.truthy(orbiter.orbiting))
        {
            if (orbiter.orbiting == this)
                await orbiter.orbiting.end_orbit(orbiter, TRUE);
            else
                await orbiter.orbiting.end_orbit(orbiter);
        }

        DmList args = Dm.args(orbiter, radius, clockwise, rotation_speed, rotation_segments, pre_rotation, __extra);
        orbiter_params[orbiter] = args.Copy(2);
        orbiter_list[orbiter] = TRUE;
        orbiter.orbiting = this;

        ADD_TRAIT(orbiter, TRAIT_NO_FLOATING_ANIM, ORBITING_TRAIT);
        RegisterSignal(orbiter, COMSIG_MOVABLE_MOVED, nameof(orbiter_move_react));
        RegisterSignal(orbiter, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, nameof(orbiter_before_shuttle_move));

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_ORBIT_BEGIN]))
            parent._SendSignal(COMSIG_ATOM_ORBIT_BEGIN, Dm.list(parent, orbiter));

        matrix initial_transform = matrix(orbiter.transform);
        orbiter_list[orbiter] = initial_transform;

        // Head first!
        if (Dm.truthy(pre_rotation))
        {
            matrix M = matrix(orbiter.transform);
            float pre_rot = 90;
            if (!Dm.truthy(clockwise))
                pre_rot = -90;
            M.Turn(pre_rot);
            orbiter.transform = M;
        }

        matrix shift = matrix(orbiter.transform);
        shift.Translate(0, Dm.num(radius));
        orbiter.transform = shift;

        orbiter.SpinAnimation(Dm.num(rotation_speed), -1, Dm.num(clockwise), Dm.num(rotation_segments), parallel: FALSE);

        if (ismob(orbiter))
        {
            mob orbiter_mob = (mob)orbiter;
            orbiter_mob.updating_glide_size = FALSE;
        }
        if (ismovable(parent))
        {
            atom_movable movable_parent = (atom_movable)parent;
            orbiter.glide_size = movable_parent.glide_size;
        }

        await orbiter.abstract_move(get_turf(parent));
        to_chat(orbiter, span_notice(I18n.T("Now orbiting [].", parent)));
    }

    // tg: code/datums/components/orbiter.dm:118
    [SignalHandler]
    public virtual void orbiter_before_shuttle_move(atom source = null)
    {
        // We need to detach ourselves before the shuttle moves and reattach afterwards
        Waiting.NoSleep(end_orbit(source as atom_movable, TRUE));
        RegisterSignal(source, COMSIG_ATOM_AFTER_SHUTTLE_MOVE, nameof(orbiter_after_shuttle_move));
    }

    // tg: code/datums/components/orbiter.dm:124
    [SignalHandler]
    public virtual void orbiter_after_shuttle_move(atom source = null)
    {
        UnregisterSignal(source, COMSIG_ATOM_AFTER_SHUTTLE_MOVE);
        Waiting.NoSleep(Dm.Call(this, nameof(begin_orbit), Dm.arglist(Dm.add(Dm.list(source), orbiter_params[source]))));
    }

    // tg: code/datums/components/orbiter.dm:129
    public virtual async DmTask end_orbit(atom_movable orbiter = null, float? refreshing = null)
    {
        refreshing ??= FALSE;
        if (!Dm.truthy(orbiter_list[orbiter]))
            return;
        UnregisterSignal(orbiter, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_ATOM_BEFORE_SHUTTLE_MOVE, COMSIG_ATOM_AFTER_SHUTTLE_MOVE));
        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_ORBIT_STOP]))
            parent._SendSignal(COMSIG_ATOM_ORBIT_STOP, Dm.list(parent, orbiter));
        orbiter.SpinAnimation(0, 0);
        if (orbiter_list[orbiter] is matrix) //This is ugly.
            orbiter.transform = orbiter_list[orbiter];
        Dm.remove(ref orbiter_list, orbiter);
        if (!Dm.truthy(refreshing))
            Dm.remove(ref orbiter_params, orbiter);
        await orbiter.stop_orbit(this, refreshing);
        orbiter.orbiting = null;

        if (ismob(orbiter))
        {
            mob orbiter_mob = (mob)orbiter;
            orbiter_mob.updating_glide_size = TRUE;
            orbiter_mob.glide_size = 8;

            if (isobserver(orbiter))
            {
                mob_dead_observer ghostie = (mob_dead_observer)orbiter;
                ghostie.orbiting_ref = null;
            }
        }

        REMOVE_TRAIT(orbiter, TRAIT_NO_FLOATING_ANIM, ORBITING_TRAIT);

        if (!Dm.truthy(refreshing) && !Dm.truthy(Dm.length(orbiter_list)) && !QDELING(this))
            qdel(this);
    }

    // This proc can receive signals by either the thing being directly orbited or anything holding it
    // tg: code/datums/components/orbiter.dm:158  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4; abstract_move may sleep)
    public virtual void move_react(atom_movable master = null, atom mover = null, atom oldloc = null, object direction = null)
    {
        Waiting.WaitforFalse(move_react__body(master, mover, oldloc, direction));
    }

    private async DmTask move_react__body(atom_movable master, atom mover, atom oldloc, object direction)
    {
        if (master.loc == oldloc)
            return;

        turf newturf = get_turf(master);
        if (!Dm.truthy(newturf))
            qdel(this);

        atom curloc = master.loc;
        foreach (atom_movable movable_orbiter in Dm.each_cast<atom_movable>(orbiter_list))
        {
            if (QDELETED(movable_orbiter) || movable_orbiter.loc == newturf)
                continue;
            await movable_orbiter.abstract_move(newturf);
            // CHECK_TICK as a value is (TICK_CHECK ? stoplag() : 0): TICK_CHECK is false (DV3, DV33), so it never sleeps
            if (TICK_CHECK() && master.loc != curloc)
            {
                // We moved again during the checktick, cancel current operation
                break;
            }
        }
    }

    // tg: code/datums/components/orbiter.dm:178
    [SignalHandler]
    public virtual void orbiter_move_react(atom_movable orbiter = null, atom oldloc = null, object direction = null)
    {
        if (orbiter.loc == get_turf(parent))
            return;
        Waiting.NoSleep(end_orbit(orbiter));
    }

    // tg: code/datums/components/orbiter.dm:185
    [SignalHandler]
    public virtual void orbiter_glide_size_update(datum source = null, object target = null)
    {
        foreach (object orbiter in Dm.each(orbiter_list))
        {
            atom_movable movable_orbiter = (atom_movable)orbiter;
            movable_orbiter.glide_size = Dm.num(target);
        }
    }
}

/////////////////////

// tg: code/datums/components/orbiter.dm:193
public partial class atom_movable : atom
{
    // tg: code/datums/components/orbiter.dm:193
    public virtual async DmTask<object> orbit(atom A = null, float? radius = null, float? clockwise = null, float? rotation_speed = null, float? rotation_segments = null, float? pre_rotation = null, params object[] __extra)
    {
        radius ??= 10;
        clockwise ??= FALSE;
        rotation_speed ??= 20;
        rotation_segments ??= 36;
        pre_rotation ??= TRUE;
        if (!(A is atom) || !Dm.truthy(get_turf(A)) || A == this)
            return null;
        if (HAS_TRAIT(A, TRAIT_ORBITING_FORBIDDEN))
        {
            // Stealth-mins have an empty name, don't want "You cannot orbit   at this time."
            to_chat(this, span_notice(I18n.T("You cannot orbit [] at this time.", Dm.truthy(Dm.text("[]", A)) ? Dm.text("[]", A) : "them")));
            return null;
        }
        orbit_target = A;
        return await A.AddComponent(typeof(datum_component_orbiter), this, radius, clockwise, rotation_speed, rotation_segments, pre_rotation);
    }

    // tg: code/datums/components/orbiter.dm:203
    public virtual async DmTask stop_orbit(datum_component_orbiter orbits = null, float? refreshing = null)
    {
        refreshing ??= FALSE;
        if (Dm.truthy(refreshing))
            return; //Only null the target if we're actually stopping the orbit for real, not if we're merely shuttle moving(or orbiting the same thing again). We will never get it back unless the orbit is fully deleted and reinstated.
        orbit_target = null;
    }
}

// tg: code/datums/components/orbiter.dm:209
public partial class atom : datum
{
    // tg: code/datums/components/orbiter.dm:209
    public virtual async DmTask transfer_observers_to(atom target = null)
    {
        if (!Dm.truthy(orbiters) || !(target is atom) || !Dm.truthy(get_turf(target)) || target == this)
            return;
        await target.TakeComponent(orbiters);
    }
}
