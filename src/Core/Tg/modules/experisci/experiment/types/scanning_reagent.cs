// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_reagent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the reagent scan, a container whose main reagent is the required one at a
// purity or better. The var block is generated (D111).

/// An experiment where you scan a container with a specified reagent of certain purity
// tg: code/modules/experisci/experiment/types/scanning_reagent.dm:2
public partial class datum_experiment_scanning_reagent : datum_experiment_scanning
{
    // tg: code/modules/experisci/experiment/types/scanning_reagent.dm:11
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        object dot = await base.final_contributing_index_checks(experiment_handler, target, typepath);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!is_reagent_container(target))
            return FALSE;
        return await is_valid_scan_target(experiment_handler, target as obj_item_reagent_containers);
    }

    // tg: code/modules/experisci/experiment/types/scanning_reagent.dm:19
    [ShouldCallParent]
    public virtual async DmTask<object> is_valid_scan_target(datum_component_experiment_handler experiment_handler = null, obj_item_reagent_containers container = null)
    {
        if (((datum_reagents)container.reagents).total_volume == 0)
        {
            await experiment_handler.announce_message(I18n.T("Container empty!"));
            return FALSE;
        }
        datum_reagent master_reagent = ((datum_reagents)container.reagents).get_master_reagent() as datum_reagent;
        if (master_reagent.type != required_reagent)
        {
            await experiment_handler.announce_message(I18n.T("Reagent not found!"));
            return FALSE;
        }
        if (master_reagent.purity < min_purity)
        {
            await experiment_handler.announce_message(I18n.T("Purity too low!"));
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/experisci/experiment/types/scanning_reagent.dm:33
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        return EXPERIMENT_PROG_INT(I18n.T("Scan a reagent container with [] of at least []% purity.", Dm.initial(required_reagent, (datum_reagent p) => p.name), PERCENT(min_purity)),
            seen_instances.len, required_atoms[target]);
    }
}
