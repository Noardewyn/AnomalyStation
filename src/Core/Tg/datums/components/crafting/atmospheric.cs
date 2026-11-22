// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/atmospheric.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the file's only proc. The recipes themselves are var blocks (generated, D111).

///abstract path for pipe crafting recipes that set the pipe_type of their results and have other checks as well
// tg: code/datums/components/crafting/atmospheric.dm:45
public partial class datum_crafting_recipe_spec_pipe : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/atmospheric.dm:49
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        obj_item_pipe required_pipe = (collected_requirements[typeof(obj_item_pipe)] as DmList)[1] as obj_item_pipe;
        if (ispath(required_pipe.pipe_type, typeof(obj_machinery_atmospherics_pipe_smart)))
            return TRUE;
        return FALSE;
    }
}
