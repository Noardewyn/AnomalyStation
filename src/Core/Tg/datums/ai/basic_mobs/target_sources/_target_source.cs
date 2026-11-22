// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/_target_source.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

/// Singleton datum that defines how an acquire_target behavior gathers candidates.
/// Subtype collect_candidates() to change what atoms are considered.
/// Subtype with a typecache var to pre-filter by type before targeting_strategy validation.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:4
public partial class datum_target_source : datum
{
    /// Returns a list of candidate atoms for the behavior to filter and select from.
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:7
    public virtual object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        return Dm.list();
    }
}

/// Gathers nearby atoms via oview(). No type pre-filtering.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:11
public partial class datum_target_source_oview : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:13
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        return oview(range, pawn);
    }
}

/// Gathers nearby atoms via oview(), pre-filtered by a typecache
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:18
public partial class datum_target_source_oview_typed : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:20
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        if (typecache == null)
            throw Dm.CRASH(Dm.text("[] using [] ran [] with no typecache.", pawn, controller, this), "code/datums/ai/basic_mobs/target_sources/_target_source.dm", 24);
        return typecache_filter_list(oview(range, pawn), typecache);
    }
}

/// Gathers nearby atoms via oview(), pre-filtered by a typecache stored in a blackboard key.
/// Use this when the typecache varies per mob species (e.g. BB_BASIC_FOODS).
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:30
public partial class datum_target_source_oview_typed_from_bb_key : datum_target_source_oview_typed
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:32
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList bb_typecache = controller.blackboard[typecache_key] as DmList;
        if (bb_typecache == null)
            return oview(range, pawn);
        return typecache_filter_list(oview(range, pawn), bb_typecache);
    }
}

/// Gathers nearby atoms via hearers() plus any hostile machines on the same z-level. (I should probably split out the hostile machine part but oh well)
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:44
public partial class datum_target_source_hearers : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:46
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.sub(hearers(range, get_turf(pawn)), pawn) as DmList;
        turf mob_turf = get_turf(pawn);
        if (Dm.truthy(mob_turf?.z))
        {
            foreach (atom hostile_machine in Dm.each_cast<atom>(GLOB.hostile_machines_by_z[mob_turf.z]))
            {
                if (Dm.truthy(can_see(pawn, hostile_machine, range)))
                    Dm.append(ref candidates, hostile_machine);
            }
        }

        return candidates;
    }
}

/// Gathers turfs in range via RANGE_TURFS().
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:58
public partial class datum_target_source_range_turfs : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:60
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        return RANGE_TURFS(Dm.num(range), pawn);
    }
}

/// Gathers items currently held in the pawn's hands.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:64
public partial class datum_target_source_held_items : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:66
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        return Dm.truthy(pawn.held_items) ? pawn.held_items : Dm.list();
    }
}

/// Gathers items the pawn is carrying that match the prey typecache in BB_HUNTABLE_PREY.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:78
public partial class datum_target_source_carried_huntable_prey : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:80
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList prey = controller.blackboard[BB_HUNTABLE_PREY] as DmList;
        if (prey == null)
            return Dm.list();
        return typecache_filter_list(pawn.contents, prey);
    }
}

/// Reads candidates directly from a blackboard list. No spatial filtering; range is ignored.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:87
public partial class datum_target_source_from_bb_list : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:89
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        object list = controller.blackboard[list_key];
        return Dm.truthy(list) ? list : Dm.list();
    }
}

/// Gathers the items in view.
// tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:98
public partial class datum_target_source_oview_items : datum_target_source
{
    // tg: code/datums/ai/basic_mobs/target_sources/_target_source.dm:100
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList candidates = Dm.list();
        foreach (obj_item item_candidate in Dm.each<obj_item>(oview(range, pawn)))
            Dm.append(ref candidates, item_candidate);

        return candidates;
    }
}
