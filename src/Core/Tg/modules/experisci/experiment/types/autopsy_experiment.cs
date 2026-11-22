// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/autopsy_experiment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the autopsy experiments, completed by an autopsy surgery on a human, a non-human
// or a xenomorph at a table beside the operating computer, whose experiment handler is always active. The var blocks are generated (D111).

// tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:1
public partial class datum_experiment_autopsy : datum_experiment
{
    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:7
    public override object is_complete()
    {
        return completed;
    }

    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:10
    [DmParams("experiment_handler", "target")]
    public override async DmTask<object> perform_experiment_actions(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        mob target = Dm.arg<mob>(__extra, 0);
        if (Dm.truthy(is_valid_autopsy(target)))
        {
            completed = TRUE;
            return TRUE;
        }
        else
            return FALSE;
    }

    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:17
    public virtual object is_valid_autopsy(mob target = null)
    {
        return TRUE;
    }
}

// tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:20
public partial class datum_experiment_autopsy_human : datum_experiment_autopsy
{
    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:24
    public override object is_valid_autopsy(mob target = null)
    {
        return Dm.num(ishumanbasic(target));
    }
}

// tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:27
public partial class datum_experiment_autopsy_nonhuman : datum_experiment_autopsy
{
    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:31
    public override object is_valid_autopsy(mob target = null)
    {
        return Dm.num(ishuman(target) && !ishumanbasic(target));
    }
}

// tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:34
public partial class datum_experiment_autopsy_xenomorph : datum_experiment_autopsy
{
    // tg: code/modules/experisci/experiment/types/autopsy_experiment.dm:38
    public override object is_valid_autopsy(mob target = null)
    {
        return Dm.num(isalien(target));
    }
}
