// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/decoration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1d, the map-preview spawners): only the procs that kept Meta's spawners inert, so they spawn their loot and
// delete themselves as tg's do (tg: "If you see this while in a shift, please create a bug report"). The var blocks are generated.

// tg: code/game/objects/effects/spawners/random/decoration.dm:52
public partial class obj_effect_spawner_random_decoration_statue : obj_effect_spawner_random_decoration
{
    // tg: code/game/objects/effects/spawners/random/decoration.dm:83
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object statue = await base.make_item(spawn_loc, type_path_to_make);
        if (statue is obj_structure_statue made)
            await made.set_anchored(TRUE);

        return statue;
    }
}
