// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lavaland/vent_mining/vent_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the mining epicenter's landmark (the dust cloud is the spawner's, live since its own package).

// tg: code/game/objects/structures/lavaland/vent_mining/vent_misc.dm:2
public partial class obj_effect_landmark_mining_center : obj_effect_landmark
{
    // tg: code/game/objects/structures/lavaland/vent_mining/vent_misc.dm:6
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);

        foreach (object mining_mark in Dm.each(GLOB.mining_center))
        {
            if (Dm.eq(z, ((atom)mining_mark).z))
                throw Dm.CRASH(Dm.text("\\The [] spawned on Z level [] already exists! Maps should only have at most one mining epicenter for normal ore generation.", this, z), "code/game/objects/structures/lavaland/vent_mining/vent_misc.dm", 11);
        }

        Dm.append(GLOB.mining_center, loc);
        return INITIALIZE_HINT_QDEL;
    }
}
