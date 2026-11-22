// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/scanning_people.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the people scans, which count unique humans that pass the experiment's own test.
// The var block is generated (D111).

/// An experiment where you scan your fellow humans
// tg: code/modules/experisci/experiment/types/scanning_people.dm:2
public partial class datum_experiment_scanning_people : datum_experiment_scanning
{
    // tg: code/modules/experisci/experiment/types/scanning_people.dm:11
    [DmParams()]
    public override async DmTask New(params object[] __extra)
    {
        required_atoms = Dm.list(Dm.kv(typeof(mob_living_carbon_human), required_count));
        await base.New(__extra);
    }

    // tg: code/modules/experisci/experiment/types/scanning_people.dm:15
    public override async DmTask<object> final_contributing_index_checks(datum_component_experiment_handler experiment_handler = null, atom target = null, object typepath = null)
    {
        object dot = await base.final_contributing_index_checks(experiment_handler, target, typepath);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!ishuman(target))
            return FALSE;
        return await is_valid_scan_target(target as mob_living_carbon_human, experiment_handler);
    }

    /// Checks that the passed mob is valid human to scan
    // tg: code/modules/experisci/experiment/types/scanning_people.dm:24
    [ShouldCallParent]
    public virtual async DmTask<object> is_valid_scan_target(mob_living_carbon_human check = null, datum_component_experiment_handler experiment_handler = null)
    {
        if (!Dm.truthy(mind_required) || check.mind != null)
            return TRUE;
        if (isliving(usr))
            await experiment_handler.announce_message(I18n.T("Subject is mindless!"));
        return FALSE;
    }

    // tg: code/modules/experisci/experiment/types/scanning_people.dm:32
    public override async DmTask<object> serialize_progress_stage(Type target = null, DmList seen_instances = null)
    {
        return EXPERIMENT_PROG_INT(I18n.T("Scan unique individuals with [].", required_traits_desc), seen_instances.len, required_atoms[target]);
    }
}
