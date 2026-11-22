// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1). The lists are generated.

/// Gathers visible turfs in range matching a typecache. Shuffled for variety.
// tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:2
public partial class datum_target_source_range_turfs_typecache_visible : datum_target_source_range_turfs
{
    // tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:5
    public override object collect_candidates(mob_living pawn = null, datum_ai_controller controller = null, object range = null)
    {
        DmList found = RANGE_TURFS(Dm.num(range), pawn) as DmList;
        DmList valid = Dm.list();
        foreach (turf candidate in Dm.each_cast<turf>(found))
        {
            if (!Dm.truthy(is_type_in_typecache(candidate, typecache)))
                continue;
            if (Dm.truthy(can_see(pawn, candidate, range)))
                Dm.append(ref valid, candidate);
        }
        return valid;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:15
public partial class datum_target_source_range_turfs_typecache_visible_deer_grass : datum_target_source_range_turfs_typecache_visible
{
    // tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:18
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        typecache = typecacheof(typecache) as DmList;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:22
public partial class datum_target_source_range_turfs_typecache_visible_deer_water : datum_target_source_range_turfs_typecache_visible
{
    // tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:25
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        typecache = typecacheof(typecache) as DmList;
    }
}

// tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:29
public partial class datum_target_source_range_turfs_typecache_visible_ice : datum_target_source_range_turfs_typecache_visible
{
    // tg: code/datums/ai/basic_mobs/target_sources/range_turfs_typecache_visible.dm:32
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        typecache = typecacheof(typecache) as DmList;
    }
}
