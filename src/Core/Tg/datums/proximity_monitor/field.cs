// Translated from tgstation (snapshot 1d99bd7f): code/datums/proximity_monitor/field.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /datum/proximity_monitor/advanced whole (M4c slice 8: the gravity generator's graviton field). Not here: the
// admin debug field (`/obj/item/multitool/field_debug` and `/datum/proximity_monitor/advanced/debug`, :167-219, a spawn-only
// debug multitool that colours turfs; it stays inert until the admin tooling, M10). The var block is generated (D111).

/**
 * Movable and easily code-modified fields! Allows for custom AOE effects that affect movement
 * and anything inside of them, and can do custom turf effects!
 * Supports automatic recalculation/reset on movement.
 *
 * "What do I gain from using advanced over standard prox monitors?"
 * - You can set different effects on edge vs field entrance
 * - You can set effects when the proximity monitor starts and stops tracking a turf
 */
// tg: code/datums/proximity_monitor/field.dm:13
public partial class datum_proximity_monitor_advanced : datum_proximity_monitor
{
    // tg: code/datums/proximity_monitor/field.dm:1-2  #define FIELD_TURFS_KEY "field_turfs", #define EDGE_TURFS_KEY "edge_turfs"
    private const string FIELD_TURFS_KEY = "field_turfs";
    private const string EDGE_TURFS_KEY = "edge_turfs";

    // tg: code/datums/proximity_monitor/field.dm:23
    public override object Destroy(float? force = null)
    {
        cleanup_field();
        return base.Destroy(force);
    }

    // tg: code/datums/proximity_monitor/field.dm:27
    public virtual void cleanup_field()
    {
        foreach (turf turf in Dm.each_cast<turf>(edge_turfs))
            cleanup_edge_turf(turf);
        edge_turfs = Dm.list();
        foreach (turf turf in Dm.each_cast<turf>(field_turfs))
            cleanup_field_turf(turf);
        field_turfs = Dm.list();
    }

    //Call every time the field moves (done automatically if you use update_center) or a setup specification is changed.
    // tg: code/datums/proximity_monitor/field.dm:36
    public virtual void recalculate_field(object full_recalc = null)
    {
        full_recalc ??= FALSE;
        DmList new_turfs = update_new_turfs();

        DmList old_field_turfs = field_turfs;
        DmList old_edge_turfs = edge_turfs;
        field_turfs = new_turfs[FIELD_TURFS_KEY] as DmList;
        edge_turfs = new_turfs[EDGE_TURFS_KEY] as DmList;
        if (Dm.truthy(full_recalc))
        {
            field_turfs = Dm.list();
            edge_turfs = Dm.list();
        }

        foreach (turf old_turf in Dm.each_cast<turf>(Dm.sub(old_field_turfs, field_turfs)))
        {
            if (QDELETED(this))
                return;
            cleanup_field_turf(old_turf);
        }
        foreach (turf old_turf in Dm.each_cast<turf>(Dm.sub(old_edge_turfs, edge_turfs)))
        {
            if (QDELETED(this))
                return;
            cleanup_edge_turf(old_turf);
        }

        if (Dm.truthy(full_recalc))
        {
            old_field_turfs = Dm.list();
            old_edge_turfs = Dm.list();
            field_turfs = new_turfs[FIELD_TURFS_KEY] as DmList;
            edge_turfs = new_turfs[EDGE_TURFS_KEY] as DmList;
        }

        foreach (turf new_turf in Dm.each_cast<turf>(Dm.sub(field_turfs, old_field_turfs)))
        {
            if (QDELETED(this))
                return;
            setup_field_turf(new_turf);
        }

        foreach (turf new_turf in Dm.each_cast<turf>(Dm.sub(edge_turfs, old_edge_turfs)))
        {
            if (QDELETED(this))
                return;
            setup_edge_turf(new_turf);
        }
    }

    // tg: code/datums/proximity_monitor/field.dm:72
    [SignalHandler]
    public override void on_initialized(turf location = null, atom created = null, object init_flags = null)
    {
        base.on_initialized(location, created, init_flags);
        on_entered(location, created as atom_movable, null);
    }

    // tg: code/datums/proximity_monitor/field.dm:76
    [SignalHandler]
    public override void on_entered(atom source = null, atom_movable entered = null, turf old_loc = null)
    {
        base.on_entered(source, entered, old_loc);
        if (Dm.eq(get_dist(source, host), current_range))
            field_edge_crossed(entered, old_loc, source as turf);
        else
            field_turf_crossed(entered, old_loc, source as turf);
    }

    // tg: code/datums/proximity_monitor/field.dm:83
    [SignalHandler]
    public override void on_moved(atom_movable movable = null, atom old_loc = null)
    {
        base.on_moved(movable, old_loc);
        if (Dm.truthy(ignore_if_not_on_turf))
        {
            //Early return if it's not the host that has moved.
            if (movable != host)
                return;
            //Cleanup the field if the host was on a turf but isn't anymore.
            if (!isturf(host.loc))
            {
                if (isturf(old_loc))
                    cleanup_field();
                return;
            }
        }
        recalculate_field(full_recalc: FALSE);
    }

    // tg: code/datums/proximity_monitor/field.dm:96
    [SignalHandler]
    public override void on_uncrossed(atom source = null, atom_movable gone = null, object direction = null)
    {
        if (Dm.eq(get_dist(source, host), current_range))
            field_edge_uncrossed(gone, source as turf, get_turf(gone));
        else
            field_turf_uncrossed(gone, source as turf, get_turf(gone));
    }

    /// Called when a turf in the field of the monitor is linked
    // tg: code/datums/proximity_monitor/field.dm:103
    public virtual void setup_field_turf(turf target = null)
    {
        return;
    }

    /// Called when a turf in the field of the monitor is unlinked
    /// Do NOT call this manually, requires management of the field_turfs list
    // tg: code/datums/proximity_monitor/field.dm:108
    public virtual void cleanup_field_turf(turf target = null)
    {
        return;
    }

    /// Called when a turf in the edge of the monitor is linked
    // tg: code/datums/proximity_monitor/field.dm:112
    public virtual void setup_edge_turf(turf target = null)
    {
        if (Dm.truthy(edge_is_a_field)) // If the edge is considered a field, set it up like one
            setup_field_turf(target);
    }

    /// Called when a turf in the edge of the monitor is unlinked
    /// Do NOT call this manually, requires management of the edge_turfs list
    // tg: code/datums/proximity_monitor/field.dm:118
    public virtual void cleanup_edge_turf(turf target = null)
    {
        if (Dm.truthy(edge_is_a_field)) // If the edge is considered a field, clean it up like one
            cleanup_field_turf(target);
    }

    // tg: code/datums/proximity_monitor/field.dm:122
    public virtual DmList update_new_turfs()
    {
        if (Dm.truthy(ignore_if_not_on_turf) && !isturf(host.loc))
            return Dm.list(Dm.kv(FIELD_TURFS_KEY, Dm.list()), Dm.kv(EDGE_TURFS_KEY, Dm.list()));
        DmList local_field_turfs = Dm.list();
        DmList local_edge_turfs = Dm.list();
        turf center = get_turf(host);
        if (Dm.gt(current_range, 0))
            Dm.append(ref local_field_turfs, RANGE_TURFS(Dm.num(current_range) - 1, center));
        if (Dm.gt(current_range, 1))
            local_edge_turfs = Dm.sub(RANGE_TURFS(current_range, center), local_field_turfs) as DmList;
        return Dm.list(Dm.kv(FIELD_TURFS_KEY, local_field_turfs), Dm.kv(EDGE_TURFS_KEY, local_edge_turfs));
    }

    //Gets edge direction/corner, only works with square radius/WDH fields!
    // tg: code/datums/proximity_monitor/field.dm:135
    public virtual object get_edgeturf_direction(turf T = null, turf center_override = null)
    {
        turf checking_from = get_turf(host);
        if (center_override is turf)
            checking_from = center_override;
        if (!Dm.@in(T, edge_turfs))
            return null;
        float range = Dm.num(current_range);
        if (((T.x == (checking_from.x + range)) || (T.x == (checking_from.x - range))) && ((T.y == (checking_from.y + range)) || (T.y == (checking_from.y - range))))
            return get_dir(checking_from, T);
        if (T.x == (checking_from.x + range))
            return EAST;
        if (T.x == (checking_from.x - range))
            return WEST;
        if (T.y == (checking_from.y - range))
            return SOUTH;
        if (T.y == (checking_from.y + range))
            return NORTH;
        return null;
    }

    // tg: code/datums/proximity_monitor/field.dm:152
    public virtual void field_turf_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        return;
    }

    // tg: code/datums/proximity_monitor/field.dm:155
    public virtual void field_turf_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        return;
    }

    // tg: code/datums/proximity_monitor/field.dm:158
    public virtual void field_edge_crossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (Dm.truthy(edge_is_a_field)) // If the edge is considered a field, pass crossed to that
            field_turf_crossed(movable, old_location, new_location);
    }

    // tg: code/datums/proximity_monitor/field.dm:162
    public virtual void field_edge_uncrossed(atom_movable movable = null, turf old_location = null, turf new_location = null)
    {
        if (Dm.truthy(edge_is_a_field)) // If the edge is considered a field, pass uncrossed to that
            field_turf_uncrossed(movable, old_location, new_location);
    }
}
