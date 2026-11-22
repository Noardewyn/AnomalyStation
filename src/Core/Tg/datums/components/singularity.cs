// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/singularity.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 5 (D149, D150): the singularity component whole but the bloodthirsty subtype (its own move and target search: not
// used by the dev spawn, inert until a user); the bluespace artillery's blocker and beam came with gaps R1. The var block is
// generated.

/// Things that maybe move around and does stuff to things around them
/// Used for the singularity (duh) and Nar'Sie
// tg: code/datums/components/singularity.dm:13
[DmPath("/datum/component/singularity")]
public partial class datum_component_singularity : datum_component
{
    // tg: code/datums/components/singularity.dm:2  #define FIELD_CONTAINMENT_DISTANCE 30 (file-local)
    internal const float FIELD_CONTAINMENT_DISTANCE = 30;

    // tg: code/datums/components/singularity.dm:57
    [DmParams("bsa_targetable", "consume_range", "consume_callback", "disregard_failed_movements", "grav_pull", "notify_admins", "singularity_size", "roaming")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object bsa_targetable = Dm.arg(__extra, 0) ?? TRUE;
        object consume_range = Dm.arg(__extra, 1) ?? 0;
        datum_callback consume_callback = Dm.arg(__extra, 2) as datum_callback ?? Dm.@new<datum_callback>(this, nameof(default_singularity_act));
        object disregard_failed_movements = Dm.arg(__extra, 3) ?? FALSE;
        object grav_pull = Dm.arg(__extra, 4) ?? 4;
        object notify_admins = Dm.arg(__extra, 5) ?? TRUE;
        object singularity_size = Dm.arg(__extra, 6) ?? STAGE_ONE;
        object roaming = Dm.arg(__extra, 7) ?? TRUE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.bsa_targetable = bsa_targetable;
        this.consume_callback = consume_callback;
        this.consume_range = consume_range;
        this.disregard_failed_movements = disregard_failed_movements;
        this.grav_pull = grav_pull;
        this.notify_admins = notify_admins;
        this.roaming = roaming;
        this.singularity_size = singularity_size;
        return null;
    }

    // tg: code/datums/components/singularity.dm:79
    public override async DmTask RegisterWithParent()
    {
        START_PROCESSING(SSsinguloprocess, this);

        // The singularity stops drifting for no man!
        await parent.AddElement(typeof(datum_element_forced_gravity), FALSE);

        await parent.AddElement(typeof(datum_element_bsa_blocker));
        RegisterSignal(parent, COMSIG_ATOM_BSA_BEAM, nameof(bluespace_reaction));

        RegisterSignal(parent, COMSIG_ATOM_BLOB_ACT, nameof(block_blob));

        RegisterSignals(parent, Dm.list(
            COMSIG_ATOM_ATTACK_ANIMAL,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ATTACK_PAW
        ), nameof(consume_attack));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(consume_attackby));

        RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(moved));
        RegisterSignal(parent, COMSIG_ATOM_BUMPED, nameof(consume));
        DmList loc_connections = Dm.list(Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));
        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, loc_connections);

        RegisterSignal(parent, COMSIG_ATOM_PRE_BULLET_ACT, nameof(consume_bullets));

        if (Dm.truthy(notify_admins))
            admin_investigate_setup();

        Dm.combine(ref GLOB.singularities, this);
    }

    // tg: code/datums/components/singularity.dm:111
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.singularities, this);
        consume_callback = null;
        target = null;

        return base.Destroy(force);
    }

    // tg: code/datums/components/singularity.dm:118
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        STOP_PROCESSING(SSsinguloprocess, this);

        parent.RemoveElement(typeof(datum_element_bsa_blocker));
        parent.RemoveElement(typeof(datum_element_forced_gravity));

        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACK_ANIMAL,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ATTACK_PAW,
            COMSIG_ATOM_BLOB_ACT,
            COMSIG_ATOM_BSA_BEAM,
            COMSIG_ATOM_PRE_BULLET_ACT,
            COMSIG_ATOM_BUMPED,
            COMSIG_MOVABLE_PRE_MOVE,
            COMSIG_ATOM_ATTACKBY
        ));
    }

    // tg: code/datums/components/singularity.dm:138  (set waitfor = FALSE, inherited from /datum/process: the wrapper of
    // waiting-model.md 5.4; digest and move may sleep)
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        // We want to move and eat once a second, but want to process our turf consume queue the rest of the time
        time_since_last_eat = Dm.num(time_since_last_eat) + Dm.num(seconds_per_tick);
        await digest();
        if (TICK_CHECK())
            return null;
        if (Dm.num(time_since_last_eat) > 1) // Delta time is in seconds for "reasons"
        {
            time_since_last_eat = 0;
            if (Dm.truthy(roaming))
                await move();
            eat();
            await digest(); // Try and process as much as you can with the time we have left
        }
        return null;
    }

    // tg: code/datums/components/singularity.dm:151
    [SignalHandler]
    public virtual object block_blob()
    {
        return COMPONENT_CANCEL_BLOB_ACT;
    }

    /// Triggered when something enters the component's parent.
    // tg: code/datums/components/singularity.dm:157
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        consume(source, arrived);
    }

    // tg: code/datums/components/singularity.dm:161
    [SignalHandler]
    public virtual void consume(datum source = null, atom thing = null)
    {
        if (thing == parent)
        {
            stack_trace("Singularity tried to consume itself.", "code/datums/components/singularity.dm", 164);
            return;
        }

        // (a SIGNAL_HANDLER cannot sleep, and tg's singularity_acts do not: NoSleep)
        if (consume_callback != null)
            Waiting.NoSleep(consume_callback.Invoke(thing, this));
    }

    // tg: code/datums/components/singularity.dm:169
    [SignalHandler]
    public virtual object consume_attack(datum source = null, mob user = null)
    {
        consume(source, user);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/datums/components/singularity.dm:175
    [SignalHandler]
    public virtual void consume_attackby(datum source = null, obj_item item = null, mob user = null)
    {
        consume(source, user);
    }

    // Will there be an impact? Who knows.  Will we see it? No.
    // tg: code/datums/components/singularity.dm:181
    [SignalHandler]
    public virtual object consume_bullets(datum source = null, obj_projectile projectile = null)
    {
        qdel(projectile);
        return COMPONENT_BULLET_BLOCKED;
    }

    /// Calls singularity_act on the thing passed, usually destroying the object
    // tg: code/datums/components/singularity.dm:188
    public virtual async DmTask default_singularity_act(atom thing = null)
    {
        await thing.singularity_act();
    }

    // tg: code/datums/components/singularity.dm:191
    public virtual void eat()
    {
        Dm.combine(ref turfs_to_consume, spiral_range_turfs(grav_pull, parent));
        // DEVIATION(DV395): it pulls and eats the linked levels of the stack too, within its pull by get_dist's level distance (each level a tile)
        if (get_turf(parent) is turf centre)
        {
            foreach (int dz in LinkedLevels.Around(centre, (int)Dm.num(grav_pull)))
            {
                if (locate(centre.x, centre.y, centre.z + dz) is turf level)
                    Dm.combine(ref turfs_to_consume, spiral_range_turfs(Dm.num(grav_pull), level));
            }
        }
    }

    // We use a static index for this to prevent infinite runtimes (tg's `var/static/cached_index`: one for every singularity).
    // tg: code/datums/components/singularity.dm:202
    internal static int cached_index__static { get => TgState.Current.singularity_cached_index; set => TgState.Current.singularity_cached_index = value; }

    // tg: code/datums/components/singularity.dm:194
    public virtual async DmTask digest()
    {
        atom atom_parent = parent as atom;

        if (!isturf(atom_parent.loc))
            return;

        // Maybe a might overengineered, but let's be safe yes?
        if (cached_index__static != 0)
        {
            int old_index = cached_index__static;
            cached_index__static = 0; // Prevents infinite Cut() runtimes. Sorry MSO
            turfs_to_consume.Cut(1, old_index + 1);
        }

        turfs_to_consume ??= Dm.list();
        for (cached_index__static = 1; cached_index__static <= length(turfs_to_consume); cached_index__static++)
        {
            turf tile = turfs_to_consume[cached_index__static] as turf;
            float dist_to_tile = get_dist(tile, parent);

            if (Dm.num(grav_pull) < dist_to_tile) //If we've exited the singulo's range already, just skip us
                continue;

            bool in_consume_range = (dist_to_tile <= Dm.num(consume_range));
            if (in_consume_range)
            {
                // DEVIATION(DV363): the ceiling over an eaten tile is eaten with it, a layer a pass as tg's turf (no energy);
                // first, as the turf's ChangeTurf leaves this reference a replaced turf
                if (tile != null) CeilingSystem.Of(World.Current).SingularityAct((int)tile.x, (int)tile.y, (int)tile.z);
                consume(this, tile);
            }
            else if (!InertSeam.MissingOverride(tile, "singularity_pull")) // DEVIATION(DV27)
                await tile.singularity_pull(atom_parent, singularity_size);

            foreach (atom_movable thing in Dm.each_cast<atom_movable>(tile?.contents))
            {
                if (thing == parent)
                    continue;
                if (in_consume_range)
                    consume(this, thing);
                else if (!InertSeam.MissingOverride(thing, "singularity_pull")) // DEVIATION(DV27)
                    await thing.singularity_pull(atom_parent, singularity_size);
            }

            if (TICK_CHECK()) //Yes this means the singulo can eat all of its host subsystem's cpu, but like it's the singulo, and it was gonna do that anyway
            {
                turfs_to_consume.Cut(1, cached_index__static + 1);
                cached_index__static = 0;
                return;
            }
        }

        turfs_to_consume.Cut();
        cached_index__static = 0;
    }

    // tg: code/datums/components/singularity.dm:235
    public virtual async DmTask move()
    {
        object drifting_dir = pick(Dm.sub(GLOB.alldirs, last_failed_movement));

        if (!QDELETED(target) && prob(chance_to_move_to_target))
            drifting_dir = get_dir(parent, target);

        await step(parent as atom_movable, Dm.num(drifting_dir));
    }

    // tg: code/datums/components/singularity.dm:243
    [SignalHandler]
    public virtual object moved(datum source = null, atom new_location = null)
    {
        atom atom_parent = parent as atom;
        float current_direction = atom_parent.dir;
        turf current_turf = get_turf(parent as atom);

        foreach (object dir__obj in Dm.each(GLOB.cardinals))
        {
            float dir = Dm.num(dir__obj);
            if (Dm.truthy(Dm.bitand(current_direction, dir)))
            {
                current_turf = get_step(current_turf, dir);
                if (current_turf == null)
                    break;
                // eat the stuff if we're going to move into it so it doesn't mess up our movement
                foreach (atom thing_on_turf in Dm.each_cast<atom>(current_turf.contents))
                    consume(this, thing_on_turf);
                consume(this, current_turf);
            }
        }

        if (Dm.truthy(disregard_failed_movements) || Dm.truthy(check_turfs_in(current_direction)))
            last_failed_movement = null;
        else
        {
            last_failed_movement = current_direction;
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        }
        return null;
    }

    // tg: code/datums/components/singularity.dm:266
    public virtual object can_move(turf to_move = null)
    {
        if (to_move == null)
            return FALSE;

        foreach (atom thing in Dm.each_cast<atom>(to_move))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(thing._listen_lookup?[COMSIG_ATOM_SINGULARITY_TRY_MOVE]) ? NONE : thing._SendSignal(COMSIG_ATOM_SINGULARITY_TRY_MOVE, Dm.list(thing)), SINGULARITY_TRY_MOVE_BLOCK)))
                return FALSE;
        }

        return TRUE;
    }

    /// Makes sure we don't move out of the z-level by checking the turfs around us.
    /// Takes in the direction we're going, and optionally how many steps forward to look.
    /// If steps are not provided, it will be inferred by singularity_size.
    // tg: code/datums/components/singularity.dm:279
    public virtual object check_turfs_in(object direction = null, object steps = null)
    {
        if (!Dm.truthy(direction))
            return FALSE;
        atom atom_parent = parent as atom;
        if (!Dm.truthy(steps))
        {
            switch (Dm.num(singularity_size))
            {
                case STAGE_ONE: steps = 1; break;
                case STAGE_TWO: steps = 2; break;
                case STAGE_THREE: steps = 3; break;
                case STAGE_FOUR: steps = 4; break;
                case STAGE_FIVE: steps = 5; break;
            }
        }
        DmList turfs = Dm.list();
        turf farthest_turf = atom_parent.loc as turf;
        for (int i = 1; i <= Dm.num(steps); i++)
            farthest_turf = get_step(farthest_turf, Dm.num(direction));
        if (!isturf(farthest_turf))
            return FALSE;
        turfs.Add(farthest_turf);
        float dir2 = 0;
        float dir3 = 0;
        switch (Dm.num(direction))
        {
            case NORTH:
            case SOUTH:
                dir2 = EAST;
                dir3 = WEST;
                break;
            case EAST:
            case WEST:
                dir2 = NORTH;
                dir3 = SOUTH;
                break;
        }
        turf farthest_perpendicular_turf = farthest_turf;
        for (int i = 1; i <= Dm.num(steps) - 1; i++)
        {
            farthest_perpendicular_turf = get_step(farthest_perpendicular_turf, dir2);
            if (!isturf(farthest_perpendicular_turf))
                return FALSE;
            turfs.Add(farthest_perpendicular_turf);
        }
        for (int i = 1; i <= Dm.num(steps) - 1; i++)
        {
            farthest_turf = get_step(farthest_turf, dir3);
            if (!isturf(farthest_turf))
                return FALSE;
            turfs.Add(farthest_turf);
        }
        foreach (object turf_in_range in Dm.each<object>(turfs))
        {
            if (turf_in_range == null)
                continue;
            if (!Dm.truthy(can_move(turf_in_range as turf)))
                return FALSE;
        }
        return TRUE;
    }

    /// Logs to admins that a singularity was created
    // tg: code/datums/components/singularity.dm:331
    public virtual void admin_investigate_setup()
    {
        var spawned_turf = get_turf(parent) as turf;
        message_admins(Dm.text("A singulo has been created at [].", ADMIN_VERBOSEJMP(spawned_turf)));
        var atom_parent = (atom)parent;
        atom_parent.investigate_log(Dm.text("was made into a singularity at [].", AREACOORD(spawned_turf)), INVESTIGATE_ENGINE);
    }

    /// Fired when the singularity is fired at with the BSA and deletes it
    // tg: code/datums/components/singularity.dm:338
    [SignalHandler]
    public virtual void bluespace_reaction()
    {
        if (!Dm.truthy(bsa_targetable))
            return;

        var atom_parent = (atom)parent;
        atom_parent.investigate_log("has been shot by bluespace artillery and destroyed.", INVESTIGATE_ENGINE);
        qdel(parent);
    }
}

// tg: code/datums/components/singularity.dm:347
public partial class datum_component_singularity_bloodthirsty : datum_component_singularity
{
    // tg: code/datums/components/singularity.dm:350
    public override async DmTask move()
    {
        atom atom_parent = parent as atom;
        //handle current target
        if (target != null && !QDELETED(target))
        {
            if (istype(target, typeof(obj_machinery_power_singularity_beacon)))
            {
                await base.move(); //don't switch targets from a singulo beacon
                return;
            }
            if (!Dm.eq(target.z, atom_parent.z))
                target = null;
            mob_living potentially_closer = find_new_target() as mob_living;
            if (potentially_closer != target && prob(20))
                target = potentially_closer;
        }
        //if we lost that target get a new one
        if (target == null || QDELETED(target))
        {
            mob_living new_target = find_new_target() as mob_living;
            new_target?.ominous_nosebleed();
            target = new_target;
        }
        await base.move();
    }

    ///Searches the living list for the closest target, and begins chasing them down.
    // tg: code/datums/components/singularity.dm:369
    public virtual object find_new_target()
    {
        atom atom_parent = parent as atom;
        float closest_distance = INFINITY;
        mob_living closest_target = null;
        foreach (mob_living target in Dm.each<mob_living>(GLOB.mob_living_list))
        {
            if (!Dm.eq(target.z, atom_parent.z))
                continue;
            if (HAS_TRAIT(target, TRAIT_GODMODE))
                continue;
            float distance_from_target = get_dist(target, atom_parent);
            if (distance_from_target < closest_distance)
            {
                closest_distance = distance_from_target;
                closest_target = target;
            }
        }
        return closest_target;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/singularity.dm:202  var/static/cached_index (per world, rules 3.2: a static var is world state)
    internal int singularity_cached_index;
}
