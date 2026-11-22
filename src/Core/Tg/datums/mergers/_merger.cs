// Translated from tgstation (snapshot 1d99bd7f): code/datums/mergers/_merger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b slice 2 (the firelocks close as a row): the whole file. MERGERS_DEBUG is FALSE (:1), so its debug colour lines are
// inactive (rules 1.7). The var block is generated but for id, hand-declared (text ids).

/// A datum that tracks a type or types of objects in a cluster
// tg: code/datums/mergers/_merger.dm:4
public partial class datum_merger : datum
{
    /// The unique ID for this merger datum, adjacent merg groups with the same id will combine
    // tg: code/datums/mergers/_merger.dm:6
    // (object, not the generator's Type (types-converter.md 3.5 V3): tg's ids are text, the firelocks' "firelocks")
    public object id;

    // tg: code/datums/mergers/_merger.dm:21
    [DmParams("id", "merged_typecache", "origin", "attempt_merge_proc")]
    public override async DmTask New(params object[] __extra)
    {
        object id = Dm.arg(__extra, 0);
        DmList merged_typecache = Dm.arg(__extra, 1) as DmList;
        atom origin = Dm.arg(__extra, 2) as atom;
        object attempt_merge_proc = Dm.arg(__extra, 3);
        // :22-24  #if MERGERS_DEBUG ... #endif: inactive (rules 1.7)
        this.id = id;
        this.merged_typecache = merged_typecache;
        this.origin = origin;
        this.attempt_merge_proc = attempt_merge_proc;
        await Refresh();
    }

    // tg: code/datums/mergers/_merger.dm:31
    public override object Destroy(float? force = null)
    {
        foreach (atom thing in Dm.each_cast<atom>(members))
            RemoveMember(thing);
        return base.Destroy(force);
    }

    // tg: code/datums/mergers/_merger.dm:36
    public virtual void RemoveMember(atom thing = null, float? clean = null)
    {
        clean ??= TRUE;
        if (Dm.truthy(thing._listen_lookup?[COMSIG_MERGER_REMOVING])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            thing._SendSignal(COMSIG_MERGER_REMOVING, Dm.list(thing, this));
        UnregisterSignal(thing, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(thing, COMSIG_QDELETING);
        if (thing.mergers == null)
            return;
        Dm.remove(ref thing.mergers, id);
        if (Dm.truthy(clean) && !Dm.truthy(Dm.length(thing.mergers)))
            thing.mergers = null;
        Dm.remove(ref members, thing);
        origin = null;
        if (origin == thing && Dm.truthy(Dm.length(members)))
            origin = pick(members) as atom;
    }

    // tg: code/datums/mergers/_merger.dm:50
    public virtual void AddMember(atom thing = null, object connected_dir = null) // note that this fires for the origin of the merger as well
    {
        if (Dm.truthy(thing._listen_lookup?[COMSIG_MERGER_ADDING])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            thing._SendSignal(COMSIG_MERGER_ADDING, Dm.list(thing, this));
        RegisterSignal(thing, COMSIG_MOVABLE_MOVED, nameof(QueueRefresh));
        RegisterSignal(thing, COMSIG_QDELETING, nameof(HandleMemberDel));
        if (thing.mergers == null)
            thing.mergers = Dm.list();
        else if (Dm.truthy(thing.mergers[id]))
        {
            datum_merger other_merger = thing.mergers[id] as datum_merger;
            other_merger.RemoveMember(thing);
            if (thing.mergers == null)
                thing.mergers = Dm.list();
        }

        thing.mergers[id] = this;
        members[thing] = connected_dir;
        if (origin == null)
            origin = thing;

        // :66-70  #if MERGERS_DEBUG ... #endif: inactive (rules 1.7)
    }

    // tg: code/datums/mergers/_merger.dm:73
    [SignalHandler]
    public virtual void HandleMemberDel(atom source = null)
    {
        RemoveMember(source);
        QueueRefresh();
    }

    // tg: code/datums/mergers/_merger.dm:78
    [SignalHandler]
    public virtual void QueueRefresh()
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(Refresh)), 1, TIMER_UNIQUE, file: "code/datums/mergers/_merger.dm", line: 80);
    }

    // tg: code/datums/mergers/_merger.dm:82
    public virtual async DmTask Refresh()
    {
        // List of turf -> list(interesting dir, found matching atoms)
        DmList found_turfs = Dm.list();
        if (origin != null)
        {
            turf starting = get_turf(origin);
            await check_turf(starting, found_turfs, NONE);
        }
        for (float i = 1; i <= Dm.length(found_turfs); i++)
        {
            turf focus = found_turfs[i] as turf;
            DmList focus_packet = found_turfs[focus] as DmList;
            object dirs_checked = focus_packet[MERGE_TURF_PACKET_DIR];
            foreach (object dir in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(dirs_checked, dir)))
                    continue;
                turf location = get_step(focus, Dm.num(dir));
                if (location == null)
                    continue;
                if (!Dm.truthy(await check_turf(location, found_turfs, dir)))
                {
                    if (QDELETED(this))
                        return;
                    continue;
                }
                Dm.combine_at(focus_packet, MERGE_TURF_PACKET_DIR, dir);
            }
        }

        // Now that we have an idea of our connecting directions, build the fresh members list
        DmList fresh_members = Dm.list();
        foreach (turf location in Dm.each_cast<turf>(found_turfs))
        {
            DmList turf_packet = found_turfs[location] as DmList;
            object connected_dirs = turf_packet[MERGE_TURF_PACKET_DIR];
            foreach (datum member in Dm.each_cast<datum>(turf_packet[MERGE_TURF_PACKET_ATOMS]))
                fresh_members[member] = connected_dirs;
        }

        DmList leaving_members = Dm.sub(members, fresh_members) as DmList;
        foreach (atom thing in Dm.each_cast<atom>(leaving_members))
            RemoveMember(thing);

        DmList joining_members = Dm.sub(fresh_members, members) as DmList;
        foreach (atom thing in Dm.each_cast<atom>(joining_members))
            AddMember(thing, joining_members[thing]);

        // They may not need a full update but the connected dirs could change
        foreach (atom thing in Dm.each_cast<atom>(fresh_members))
            members[thing] = fresh_members[thing];

        if (Dm.truthy(_listen_lookup?[COMSIG_MERGER_REFRESH_COMPLETE])) // SEND_SIGNAL (__DEFINES/dcs/helpers.dm:7)
            _SendSignal(COMSIG_MERGER_REFRESH_COMPLETE, Dm.list(this, leaving_members, joining_members));

        if (!Dm.truthy(Dm.length(members)))
            qdel(this);
    }

    // Checks to see if the passed in location contains something interesting to us. If it does, return TRUE, otherwise return false
    // If it is interesting, we add it to our processing list
    // tg: code/datums/mergers/_merger.dm:131
    public virtual async DmTask<object> check_turf(turf location = null, DmList found_turfs = null, object asking_from = null)
    {
        float found_something = FALSE;
        // if asking_from is invalid (like if it's 0), we get a random output. that's bad, let's check for falsyness
        object us_to_them = Dm.truthy(asking_from) ? REVERSE_DIR(Dm.num(asking_from)) : asking_from;

        if (Dm.truthy(found_turfs[location]))
        {
            Dm.combine_at(found_turfs[location] as DmList, MERGE_TURF_PACKET_DIR, us_to_them);
            return TRUE;
        }

        foreach (atom_movable thing in Dm.each_cast<atom_movable>(location.contents))
        {
            if (!Dm.truthy(merged_typecache[thing.type]))
                continue;
            if (Dm.truthy(attempt_merge_proc) && !Dm.truthy(await Dm.Call(thing, attempt_merge_proc as string, this, found_turfs)))
                continue;
            if (thing.mergers != null && thing.mergers[id] != this)
            {
                datum_merger existing = thing.mergers[id] as datum_merger;
                qdel(this);
                await existing.Refresh();
                return FALSE;
            }
            if (!Dm.truthy(found_turfs[location]))
                found_turfs[location] = Dm.list(us_to_them, Dm.list());
            Dm.append_at((DmList)found_turfs[location], MERGE_TURF_PACKET_ATOMS, thing);
            found_something = TRUE;
        }

        return found_something;
    }
}
