// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/leash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file but the debug spawner (a debug corgi on a bike horn, admin tooling) and the
// blackbox tally of the forced teleports (statistics). The var block is generated.

/// Keeps the parent within the distance of its owner as naturally as possible,
/// but teleporting if necessary.
// tg: code/datums/components/leash.dm:3
public partial class datum_component_leash : datum_component
{
    // tg: code/datums/components/leash.dm:68  var/static/list/container_connections
    private static DmList container_connections => TgState.Current.datum_component_leash__container_connections ??= Dm.list(
        Dm.kv(COMSIG_MOVABLE_MOVED, nameof(on_owner_moved)));

    // tg: code/datums/components/leash.dm:23
    [DmParams("owner", "distance", "force_teleport_out_effect", "force_teleport_in_effect", "silent", "snap_on_teleport")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object owner = Dm.arg(__extra, 0);
        object distance = Dm.arg(__extra, 1) ?? 3;
        object force_teleport_out_effect = Dm.arg(__extra, 2);
        object force_teleport_in_effect = Dm.arg(__extra, 3);
        object silent = Dm.arg(__extra, 4) ?? FALSE;
        object snap_on_teleport = Dm.arg(__extra, 5) ?? FALSE;
        await base.Initialize(__extra);

        if (!ismovable(parent))
        {
            stack_trace("Parent must be a movable", "code/datums/components/leash.dm", 34);
            return COMPONENT_INCOMPATIBLE;
        }

        if (!isatom(owner))
        {
            stack_trace(Dm.text("[] (owner) is not an atom", owner), "code/datums/components/leash.dm", 38);
            return COMPONENT_INCOMPATIBLE;
        }

        if (!isnum(distance))
        {
            stack_trace(Dm.text("[] (distance) must be a number", distance), "code/datums/components/leash.dm", 42);
            return COMPONENT_INCOMPATIBLE;
        }

        if (force_teleport_out_effect != null && !ispath(force_teleport_out_effect))
        {
            stack_trace(Dm.text("force_teleport_out_effect must be null or a path, not []", force_teleport_out_effect), "code/datums/components/leash.dm", 46);
            return COMPONENT_INCOMPATIBLE;
        }

        if (force_teleport_in_effect != null && !ispath(force_teleport_in_effect))
        {
            stack_trace(Dm.text("force_teleport_in_effect must be null or a path, not []", force_teleport_in_effect), "code/datums/components/leash.dm", 50);
            return COMPONENT_INCOMPATIBLE;
        }

        this.owner = (atom)owner;
        this.distance = distance;
        this.force_teleport_out_effect = force_teleport_out_effect as Type;
        this.force_teleport_in_effect = force_teleport_in_effect as Type;
        this.silent = Dm.num(silent);
        this.snap_on_teleport = Dm.num(snap_on_teleport);

        RegisterSignal(this.owner, COMSIG_QDELETING, nameof(on_owner_qdel));

        RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(on_parent_pre_move));
        check_distance();

        if (!ismovable(owner))
            return null;

        await AddComponent(typeof(datum_component_connect_containers), owner, container_connections);
        RegisterSignal(this.owner, COMSIG_MOVABLE_MOVED, nameof(on_owner_moved));
        return null;
    }

    // tg: code/datums/components/leash.dm:74
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/leash.dm:78
    public virtual void set_distance(object distance = null)
    {
        if (!(isnum(distance))) // ASSERT (__DEFINES/assert.dm:4), inline
            throw Dm.CRASH(assertion_message("code/datums/components/leash.dm", 79, "isnum(distance)"), "code/datums/components/leash.dm", 79);
        this.distance = distance;
        check_distance();
    }

    // tg: code/datums/components/leash.dm:83
    [SignalHandler]
    public virtual void on_owner_qdel()
    {
        qdel(this);
    }

    // tg: code/datums/components/leash.dm:89
    [SignalHandler]
    public virtual void on_owner_moved(atom_movable source = null)
    {
        check_distance();
    }

    // tg: code/datums/components/leash.dm:95
    [SignalHandler]
    public virtual object on_parent_pre_move(atom_movable source = null, atom new_location = null)
    {
        if (Dm.truthy(performing_path_move))
            return NONE;

        turf new_location_turf = get_turf(new_location) as turf;
        if (Dm.num(get_dist(new_location_turf, owner)) <= Dm.num(distance))
            return NONE;

        if (ismob(source) && !Dm.truthy(silent))
            source.balloon_alert(source as mob, "too far!");

        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    // tg: code/datums/components/leash.dm:111  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4; get_path_to may sleep)
    public virtual void check_distance()
    {
        Waiting.WaitforFalse(check_distance__body());
    }

    private async DmTask check_distance__body()
    {
        if (Dm.num(get_dist(parent, owner)) <= Dm.num(distance))
            return;

        atom_movable atom_parent = (atom_movable)parent;
        if (owner.loc == null)
        {
            await atom_parent.moveToNullspace(); // If our parent is in nullspace I guess we gotta go there too
            return;
        }
        if (atom_parent.loc == null)
        {
            await force_teleport_back("in nullspace"); // If we're in nullspace, get outta there
            return;
        }

        // SEND_SIGNAL(parent, COMSIG_LEASH_PATH_STARTED) (rules 11.1 (e), a statement)
        if (Dm.truthy(parent._listen_lookup?[COMSIG_LEASH_PATH_STARTED]))
            parent._SendSignal(COMSIG_LEASH_PATH_STARTED, Dm.list(parent));

        current_path_tick += 1;
        float our_path_tick = current_path_tick;

        DmList path = await get_path_to(atom_parent, owner, mintargetdist: distance) as DmList;

        if (last_completed_path_tick > our_path_tick || QDELETED(this))
            return;

        last_completed_path_tick = our_path_tick;

        commit_path(path);
    }

    // tg: code/datums/components/leash.dm:140
    [ShouldNotSleep]
    public virtual void commit_path(DmList path = null)
    {
        performing_path_move = TRUE;

        atom_movable movable_parent = (atom_movable)parent;

        foreach (turf to_move in Dm.each_cast<turf>(path))
        {
            // Could be an older path, don't make us teleport back
            if (!Dm.truthy(Waiting.NoSleep(to_move.Adjacent(parent as atom))))
                continue;

            if (!Dm.truthy(Waiting.NoSleep(movable_parent.Move(to_move))))
            {
                Waiting.NoSleep(force_teleport_back("bad path step"));
                performing_path_move = FALSE;
                return;
            }
        }

        if (Dm.num(get_dist(parent, owner)) > Dm.num(distance))
            Waiting.NoSleep(force_teleport_back("incomplete path"));

        performing_path_move = FALSE;
        // SEND_SIGNAL(parent, COMSIG_LEASH_PATH_COMPLETE) (rules 11.1 (e), a statement)
        if (Dm.truthy(parent._listen_lookup?[COMSIG_LEASH_PATH_COMPLETE]))
            parent._SendSignal(COMSIG_LEASH_PATH_COMPLETE, Dm.list(parent));
    }

    // tg: code/datums/components/leash.dm:164
    public virtual async DmTask force_teleport_back(object reason = null)
    {
        if (Dm.truthy(snap_on_teleport))
        {
            qdel(this);
            return;
        }

        atom_movable movable_parent = (atom_movable)parent;

        if (force_teleport_out_effect != null)
            Dm.@new(force_teleport_out_effect, movable_parent.loc);

        await movable_parent.forceMove(get_turf(owner) as atom);

        if (force_teleport_in_effect != null)
            Dm.@new(force_teleport_in_effect, movable_parent.loc);

        if (ismob(movable_parent))
        {
            // SSblackbox.record_feedback("tally", "leash_force_teleport_back", 1, reason) (:182): the round's statistics
            Dm.Deferred("code/datums/components/leash.dm:182", "force_teleport_back: the blackbox tally (statistics)");
            if (!Dm.truthy(silent))
                movable_parent.balloon_alert(movable_parent as mob, "moved out of range!");
        }

        // SEND_SIGNAL(parent, COMSIG_LEASH_FORCE_TELEPORT) (rules 11.1 (e), a statement)
        if (Dm.truthy(parent._listen_lookup?[COMSIG_LEASH_FORCE_TELEPORT]))
            parent._SendSignal(COMSIG_LEASH_FORCE_TELEPORT, Dm.list(parent));
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/leash.dm:68  var/static/list/container_connections
    internal DmList datum_component_leash__container_connections;
}
