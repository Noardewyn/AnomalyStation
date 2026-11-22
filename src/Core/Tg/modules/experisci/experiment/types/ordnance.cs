// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/ordnance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the ordnance experiments, completed by publishing a paper with the NT Frontier
// app (the doppler array's and the tank compressor's records, the papers and the app wait for the ordnance lab and the papers:
// not-translated.md section 195). The var blocks are generated (D111).

/**
 * A ordnance experiment datum. What gives the science in the first place.
 * One is instantiated by the techweb, another one is also kept on SSresearch for the briefing.
 * A disk should contain several of these in a list. Only one should get picked for the final paper.
 */
// tg: code/modules/experisci/experiment/types/ordnance.dm:6
public partial class datum_experiment_ordnance : datum_experiment
{
    // tg: code/modules/experisci/experiment/types/ordnance.dm:21
    public override object is_complete()
    {
        return completed;
    }

    // tg: code/modules/experisci/experiment/types/ordnance.dm:24
    public override async DmTask<object> check_progress()
    {
        DmList dot = null;
        string status_message = I18n.T("You must publish a paper on [] using the NT Frontier app", name);
        Dm.append(ref dot, EXPERIMENT_PROG_BOOL(status_message, is_complete()));
        return dot;
    }

    // tg: code/modules/experisci/experiment/types/ordnance.dm:28
    [DmParams("experiment_handler")]
    public override async DmTask<object> actionable(params object[] __extra)
    {
        datum_component_experiment_handler experiment_handler = Dm.arg<datum_component_experiment_handler>(__extra, 0);
        return Dm.not(is_complete());
    }
}
