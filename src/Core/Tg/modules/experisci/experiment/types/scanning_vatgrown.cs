// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_vatgrown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the cytology scans, which count only vat-grown creatures (tg's TRAIT_VATGROWN,
// given by the growing vat of cytology, not ported yet: not-translated.md section 195). The var block is generated (D111).

// tg: code/modules/experisci/experiment/types/scanning_vatgrown.dm:1
public partial class datum_experiment_scanning_cytology : datum_experiment_scanning
{
    // tg: code/modules/experisci/experiment/types/scanning_vatgrown.dm:5
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        return (await base.final_contributing_index_checks(experiment_handler, target, typepath) is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.num(HAS_TRAIT(target, TRAIT_VATGROWN)));
    }

    // tg: code/modules/experisci/experiment/types/scanning_vatgrown.dm:8
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        return EXPERIMENT_PROG_INT(I18n.T("Scan samples of \\a vat-grown []", Dm.initial(target, (atom p) => p.name)), seen_instances.len, required_atoms[target]);
    }
}
