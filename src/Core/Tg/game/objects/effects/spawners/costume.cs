// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/costume.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps G3, D291): the spawner base; its 28 costumes only list their items (generated var blocks).

// tg: code/game/objects/effects/spawners/costume.dm:1
public partial class obj_effect_spawner_costume : obj_effect_spawner
{
    // tg: code/game/objects/effects/spawners/costume.dm:9
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(items?.len))
        {
            foreach (object path in Dm.each(items))
                Dm.@new((Type)path, loc);
        }
        return dot;
    }
}
