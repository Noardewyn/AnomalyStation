// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/physical_experiment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the physical experiment, which tracks the atom the experi-scanner scanned and is
// completed by an event on it. The var block is generated (D111).

// tg: code/modules/experisci/experiment/types/physical_experiment.dm:1
public partial class datum_experiment_physical : datum_experiment
{
    // tg: code/modules/experisci/experiment/types/physical_experiment.dm:12
    public override object is_complete()
    {
        return completed;
    }

    // tg: code/modules/experisci/experiment/types/physical_experiment.dm:15
    [DmParams("experiment_handler", "target")]
    public override async DmTask<object> perform_experiment_actions(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        atom target = Dm.arg<atom>(__extra, 0);
        if (Dm.truthy(currently_scanned_atom))
            unregister_events();
        currently_scanned_atom = target;
        linked_experiment_handler = experiment_handler;
        if (Dm.truthy(await register_events()))
            return TRUE;
        currently_scanned_atom = null;
        linked_experiment_handler = null;
        return FALSE;
    }

    /**
     * Handles registering to events relevant to the experiment
     */
    // tg: code/modules/experisci/experiment/types/physical_experiment.dm:29
    public virtual async DmTask<object> register_events()
    {
        return FALSE;
    }

    /**
     * Handles unregistering to events relevant to the experiment
     */
    // tg: code/modules/experisci/experiment/types/physical_experiment.dm:35
    public virtual void unregister_events()
    {
        return;
    }
}
