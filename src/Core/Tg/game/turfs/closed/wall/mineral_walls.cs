// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/wall/mineral_walls.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice G3, D272): the shuttles' titanium walls, live so that they smooth (their copy keeps its turn when a
// shuttle moves); hulks and rust (M10+) are live-partial stubs. Gaps G2 (D291): the plastitanium walls and their pod variants
// (they deconstruct as every wall, to plastitanium sheets); the explosive one keeps its bomb core and stays inert. The other
// mineral walls wait for their own procs.

// tg: code/game/turfs/closed/wall/mineral_walls.dm:266
public partial class turf_closed_wall_mineral_titanium : turf_closed_wall_mineral
{
    // tg: code/game/turfs/closed/wall/mineral_walls.dm:303
    public override async DmTask<object> copyTurf(turf copy_to_turf = null, float? copy_air = null, object flags = null)
    {
        object dot = await base.copyTurf(copy_to_turf, copy_air, flags);
        ((turf)dot).transform = transform; // TURF-IDENTITY: the copy is the turf the base returns
        return dot;
    }
}

// tg: code/game/turfs/closed/wall/mineral_walls.dm:299
public partial class turf_closed_wall_mineral_titanium_interior : turf_closed_wall_mineral_titanium
{
    // tg: code/game/turfs/closed/wall/mineral_walls.dm:299
    public override async DmTask<object> copyTurf(turf copy_to_turf = null, float? copy_air = null, object flags = null)
    {
        object dot = await base.copyTurf(copy_to_turf, copy_air, flags);
        ((turf)dot).transform = transform;
        return dot;
    }
}

/////////////////////Plastitanium walls/////////////////////

// tg: code/game/turfs/closed/wall/mineral_walls.dm:316
public partial class turf_closed_wall_mineral_plastitanium : turf_closed_wall_mineral
{
    // tg: code/game/turfs/closed/wall/mineral_walls.dm:414
    public override async DmTask<object> copyTurf(turf copy_to_turf = null, float? copy_air = null, object flags = null)
    {
        object dot = await base.copyTurf(copy_to_turf, copy_air, flags);
        ((turf)dot).transform = transform; // TURF-IDENTITY: the copy is the turf the base returns
        return dot;
    }
}
