// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/mod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps G3, D291): the maintenance MOD module spawner of the maintenance loot. The general modules it makes are live
// since gaps MO1 (D333), the department ones since gaps MO2.

// tg: code/game/objects/effects/spawners/random/mod.dm:6
public partial class obj_effect_spawner_random_mod_maint : obj_effect_spawner_random_mod
{
    // tg: code/game/objects/effects/spawners/random/mod.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(is_multi_z_level(z)))
            Dm.append(ref loot, Dm.list(Dm.kv(typeof(obj_item_mod_module_atrocinator), 3)));
        return base.Initialize(mapload, __extra);
    }
}
