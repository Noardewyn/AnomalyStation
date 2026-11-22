// Translated from tgstation (snapshot 1d99bd7f): code/datums/girder_wall_recipe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 4: the whole file (the var block is generated).

/// Contains recipe data for constructing a specific type of wall on a girder.
/// Only the [/datum/element/uses_girder_wall_recipes] should be using these.
// tg: code/datums/girder_wall_recipe.dm:3
public partial class datum_girder_wall_recipe : datum
{
    // tg: code/datums/girder_wall_recipe.dm:20
    [DmParams("wall_type", "stack_type", "stack_amount", "girder_type", "girder_state", "make_delay", "start_alert")]
    public override async DmTask New(params object[] __extra)
    {
        this.wall_type = Dm.arg<Type>(__extra, 0);
        this.stack_type = Dm.arg<Type>(__extra, 1);
        this.stack_amount = Dm.arg(__extra, 2);
        this.girder_type = Dm.arg<Type>(__extra, 3);
        this.girder_state = Dm.arg(__extra, 4);
        this.make_delay = Dm.arg(__extra, 5);
        this.start_alert = Dm.arg(__extra, 6);
    }
}
