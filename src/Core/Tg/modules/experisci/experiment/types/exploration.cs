// Translated from tgstation (snapshot 1d99bd7f): code/modules/experisci/experiment/types/exploration.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the whole file: the exploration scans, complete once a scanned exploration site of the asked type
// or condition exists. The sites and the scanner array that performs the scan wait for the explorer drones (not-translated.md section
// 195): GLOB.exploration_sites stays empty. The var blocks are generated (D111).

// tg: code/modules/experisci/experiment/types/exploration.dm:1
public partial class datum_experiment_exploration_scan : datum_experiment
{
    // tg: code/modules/experisci/experiment/types/exploration.dm:13
    public override object is_complete()
    {
        foreach (datum_exploration_site site in Dm.each<datum_exploration_site>(GLOB.exploration_sites))
        {
            switch (required_scan_type)
            {
                case EXOSCAN_DEEP:
                    if (!Dm.truthy(site.deep_scan_complete))
                        continue;
                    break;
                case EXOSCAN_POINT:
                    if (!Dm.truthy(site.point_scan_complete))
                        continue;
                    break;
            }
            if (Dm.truthy(required_site_type) && !istype(site, required_site_type))
                continue;
            if (Dm.truthy(required_condition) && !Dm.truthy(Dm.locate_type(required_condition, site.scan_conditions)))
                continue;
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/experisci/experiment/types/exploration.dm:29
    public override async DmTask<object> perform_experiment_actions(datum_component_experiment_handler experiment_handler = null, params object[] __extra)
    {
        return is_complete();
    }

    // tg: code/modules/experisci/experiment/types/exploration.dm:32
    [DmParams("experiment_handler")]
    public override async DmTask<object> actionable(params object[] __extra)
    {
        datum_component_experiment_handler experiment_handler = Dm.arg<datum_component_experiment_handler>(__extra, 0);
        return Dm.not(is_complete());
    }
}

// tg: code/modules/experisci/experiment/types/exploration.dm:47
public partial class datum_experiment_exploration_scan_random : datum_experiment_exploration_scan
{
    // tg: code/modules/experisci/experiment/types/exploration.dm:55
    [DmParams("techweb")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (Dm.truthy(length(possible_random_site_types)))
            required_site_type = pick(possible_random_site_types) as Type;
        if (Dm.truthy(length(possible_random_conditions)))
            required_condition = pick(possible_random_conditions) as Type;
        DmList name_parts = Dm.list();
        Dm.append(ref name_parts, I18n.T("[] scan of", required_scan_type));
        if (Dm.truthy(required_site_type))
        {
            Type site = required_site_type;
            Dm.append(ref name_parts, Dm.initial(site, (datum_exploration_site p) => p.name));
        }
        else
            Dm.append(ref name_parts, I18n.T("site"));
        if (Dm.truthy(required_condition))
        {
            Type condition = required_condition;
            Dm.append(ref name_parts, I18n.T("affected by \\the []", Dm.initial(condition, (datum_scan_condition p) => p.name)));
        }
        name = (string)capitalize(name_parts.Join(""));
        description = name;
    }
}
